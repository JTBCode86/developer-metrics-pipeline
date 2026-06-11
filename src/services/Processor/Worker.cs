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
using Processor.Internal.Domain; // Certifique-se de importar o namespace do serviço

namespace Processor
{
    public class Worker : BackgroundService
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly ILogger<Worker> _logger;
        private readonly EventProcessorService _processorService; // Serviço de Lógica
        private readonly string _rawQueueUrl;
        private readonly string _processedQueueUrl;
        private readonly string _dlqQueueUrl;
        private readonly int _maxParallelWorkers;

        private static readonly string InstanceId = "processor-instance-" + Guid.NewGuid().ToString().Substring(0, 8);

        // --- CONSTRUTOR ATUALIZADO ---
        public Worker(
            IAmazonSQS sqsClient,
            IConfiguration configuration,
            ILogger<Worker> logger,
            EventProcessorService processorService) // Injetamos o serviço aqui
        {
            _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processorService = processorService ?? throw new ArgumentNullException(nameof(processorService));

            var baseUrl = configuration["AWS:ServiceURL"] ?? "http://localstack:4566";
            _rawQueueUrl = $"{baseUrl}/000000000000/raw-events";
            _processedQueueUrl = $"{baseUrl}/000000000000/processed-events";
            _dlqQueueUrl = $"{baseUrl}/000000000000/raw-events-dlq";

            _maxParallelWorkers = int.TryParse(configuration["WorkerSettings:Count"], out var count) ? count : 2;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Processor iniciado. Instance: {InstanceId}", InstanceId);
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
                        AttributeNames = new List<string> { "ApproximateReceiveCount" }
                    };

                    var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, stoppingToken);

                    if (response?.Messages != null)
                    {
                        foreach (var message in response.Messages)
                        {
                            await semaphore.WaitAsync(stoppingToken);

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var rawEvent = JsonSerializer.Deserialize<RawEvent>(message.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                                    // --- UTILIZANDO O SERVIÇO ---
                                    var (isValid, failureReason) = _processorService.Validate(rawEvent);

                                    if (!isValid)
                                    {
                                        _logger.LogError("Validação falhou: {Reason}. Enviando para DLQ.", failureReason);
                                        await _sqsClient.SendMessageAsync(_dlqQueueUrl, message.Body, stoppingToken);
                                        await _sqsClient.DeleteMessageAsync(_rawQueueUrl, message.ReceiptHandle, stoppingToken);
                                        return;
                                    }

                                    var processedEvent = _processorService.Transform(rawEvent, InstanceId);

                                    var publishBody = JsonSerializer.Serialize(processedEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                                    await _sqsClient.SendMessageAsync(_processedQueueUrl, publishBody, stoppingToken);
                                    await _sqsClient.DeleteMessageAsync(_rawQueueUrl, message.ReceiptHandle, stoppingToken);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Erro no processamento.");
                                }
                                finally
                                {
                                    semaphore.Release();
                                }
                            }, stoppingToken);
                        }
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Erro no polling SQS.");
                    await Task.Delay(3000, stoppingToken);
                }
            }
        }
    }
}