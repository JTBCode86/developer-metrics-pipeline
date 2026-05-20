using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Processor.Internal.Domain;

namespace Processor
{
    public class Worker : BackgroundService
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly ILogger<Worker> _logger;
        private readonly string _rawQueueUrl;
        private readonly string _processedQueueUrl;
        private readonly int _maxParallelWorkers;

        // CORREÇÃO: Usando Substring clássico para compatibilidade total com SDKs antigos
        private static readonly string InstanceId = "processor-instance-" + Guid.NewGuid().ToString().Substring(0, 8);

        public Worker(IAmazonSQS sqsClient, IConfiguration configuration, ILogger<Worker> logger)
        {
            _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient), "O cliente IAmazonSQS não foi injetado com sucesso.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var baseUrl = configuration["AWS:ServiceURL"] ?? configuration["AWS__ServiceURL"] ?? "http://localstack:4566";
            _rawQueueUrl = baseUrl + "/000000000000/raw-events";
            _processedQueueUrl = baseUrl + "/000000000000/processed-events";

            _maxParallelWorkers = int.TryParse(configuration["WorkerSettings:Count"] ?? configuration["WorkerSettings__Count"], out var count) ? count : 2;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Processor iniciado com {Workers} threads concorrentes. Instance: {InstanceId}", _maxParallelWorkers, InstanceId);

            var semaphore = new SemaphoreSlim(_maxParallelWorkers);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var receiveRequest = new ReceiveMessageRequest
                    {
                        QueueUrl = _rawQueueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 5,
                        // CORREÇÃO: Instanciação explícita compatível com compiladores antigos
                        AttributeNames = new List<string> { "ApproximateReceiveCount" }
                    };

                    var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, stoppingToken);

                    if (response?.Messages != null)
                    {
                        foreach (var message in response.Messages)
                        {
                            if (message == null) continue;

                            await semaphore.WaitAsync(stoppingToken);

                            _ = Task.Run(async () =>
                            {
                                string correlationId = extractEventId(message.Body);
                                using (_logger.BeginScope("{EventId}", correlationId))
                                {
                                    try
                                    {
                                        var rawEvent = JsonSerializer.Deserialize<RawEvent>(message.Body, new JsonSerializerOptions
                                        {
                                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                        });

                                        if (rawEvent == null)
                                            throw new ArgumentException("Payload invalido ou nulo.");

                                        _logger.LogInformation("Mensagem capturada na fila raw-events.");

                                        var (isValid, failureReason) = rawEvent.Validate();

                                        if (!isValid)
                                        {
                                            _logger.LogWarning("Evento rejeitado na validacao. Motivo: {FailureReason}", failureReason);
                                            throw new ArgumentException("Falha de validacao: " + failureReason);
                                        }

                                        var processedEvent = new ProcessedEvent
                                        {
                                            EventId = rawEvent.EventId,
                                            DeveloperId = rawEvent.DeveloperId,
                                            MetricType = rawEvent.MetricType,
                                            Value = rawEvent.Value,
                                            Repository = rawEvent.Repository,
                                            Timestamp = rawEvent.Timestamp,
                                            ProcessedAt = DateTime.UtcNow,
                                            ProcessorId = InstanceId
                                        };

                                        var publishBody = JsonSerializer.Serialize(processedEvent, new JsonSerializerOptions
                                        {
                                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                        });

                                        await _sqsClient.SendMessageAsync(_processedQueueUrl, publishBody, stoppingToken);
                                        await _sqsClient.DeleteMessageAsync(_rawQueueUrl, message.ReceiptHandle, stoppingToken);

                                        _logger.LogInformation("Evento enriquecido e despachado com sucesso.");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Erro encontrado no processamento do evento.");

                                        //int receiveCount = int.TryParse(message.Attributes["ApproximateReceiveCount"], out var rc) ? rc : 1;
                                        int receiveCount = 1;
                                        if (message.Attributes != null && message.Attributes.TryGetValue("ApproximateReceiveCount", out var countStr))
                                        {
                                            int.TryParse(countStr, out receiveCount);
                                        }
                                        if (receiveCount < 3)
                                        {
                                            int delaySeconds = (int)Math.Pow(2, receiveCount) * 5;

                                            _logger.LogWarning("Aplicando Backoff Exponencial. Tentativa: {Count}. Ocultando por {Secs}s.", receiveCount, delaySeconds);

                                            await _sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                                            {
                                                QueueUrl = _rawQueueUrl,
                                                ReceiptHandle = message.ReceiptHandle,
                                                VisibilityTimeout = delaySeconds
                                            }, stoppingToken);
                                        }
                                    }
                                    finally
                                    {
                                        semaphore.Release();
                                    }
                                }
                            }, stoppingToken);
                        }
                    }
                }
                catch (Amazon.SQS.Model.QueueDoesNotExistException)
                {
                    _logger.LogWarning("A fila 'raw-events' ainda nao existe no LocalStack. Aguardando inicializacao dos recursos...");
                    await Task.Delay(3000, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Erro no loop de polling do SQS.");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private static string extractEventId(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("eventId", out var prop)) return prop.GetString() ?? "unknown";
                if (doc.RootElement.TryGetProperty("event_id", out var propSnake)) return propSnake.GetString() ?? "unknown";
            }
            catch { }
            return "unknown";
        }
    }
}