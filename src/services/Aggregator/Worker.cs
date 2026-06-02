using Aggregator.Internal.Domain;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aggregator
{
    public class Worker : BackgroundService
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly ILogger<Worker> _logger;
        private readonly string _queueUrl;

        public Worker(IAmazonSQS sqsClient, IAmazonDynamoDB dynamoDb, IConfiguration configuration, ILogger<Worker> logger)
        {
            _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
            _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var baseUrl = configuration["AWS:ServiceURL"] ?? configuration["AWS__ServiceURL"] ?? "http://localstack:4566";
            _queueUrl = $"{baseUrl}/000000000000/raw-events";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Aggregator Worker iniciado e ouvindo a fila processed-events.");
            await Task.Delay(10000, stoppingToken);
            _logger.LogInformation("Infraestrutura pronta. Iniciando Worker...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var receiveRequest = new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 5
                    };

                    var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, stoppingToken);
                    if (response?.Messages != null)
                    {
                        foreach (var message in response.Messages)
                        {
                            try
                            {
                                using (JsonDocument doc = JsonDocument.Parse(message.Body))
                                {
                                    JsonElement root = doc.RootElement;

                                    // Extraindo valores manualmente das chaves JSON
                                    string eventId = root.GetProperty("event_id").GetString();
                                    string developerId = root.GetProperty("developer_id").GetString();
                                    string metricType = root.GetProperty("metric_type").GetString();
                                    double value = root.GetProperty("value").GetDouble();
                                    string repository = root.GetProperty("repository").GetString();
                                    DateTime timestamp = root.GetProperty("timestamp").GetDateTime();

                                    // Criando o objeto manualmente com os dados extraídos
                                    var processedEvent = new ProcessedEvent
                                    {
                                        EventId = eventId,
                                        DeveloperId = developerId,
                                        MetricType = metricType,
                                        Value = value,
                                        Repository = repository,
                                        Timestamp = timestamp
                                    };

                                    using (_logger.BeginScope("{EventId}", processedEvent.EventId))
                                    {
                                        _logger.LogInformation("Consumindo evento processado para agregação.");

                                        bool isDuplicate = await checkIdempotencyAsync(processedEvent.EventId, stoppingToken);
                                        if (isDuplicate)
                                        {
                                            _logger.LogWarning("Evento duplicado. Ignorando.");
                                            await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                                            continue;
                                        }

                                        await saveEventToDynamoAsync(processedEvent, stoppingToken);
                                        await updateDeveloperSummaryAsync(processedEvent, stoppingToken);

                                        await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                                        _logger.LogInformation("Métricas persistidas com sucesso.");
                                    }
                                }        
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Erro ao processar agregacao da mensagem.");
                            }
                        }
                    }
                }
                catch (Amazon.SQS.Model.QueueDoesNotExistException)
                {
                    // Tratamento elegante: aguarda a criação da fila pelo LocalStack
                    _logger.LogWarning("A fila 'processed-events' ainda nao existe. Aguardando inicializacao...");
                    await Task.Delay(3000, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Erro no loop de consumo do SQS no Aggregator.");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task<bool> checkIdempotencyAsync(string eventId, CancellationToken cancellationToken)
        {
            var request = new GetItemRequest
            {
                TableName = "events",
                Key = new Dictionary<string, AttributeValue> { { "event_id", new AttributeValue { S = eventId } } }
            };

            var response = await _dynamoDb.GetItemAsync(request, cancellationToken);
            return response.IsItemSet;
        }

        private async Task saveEventToDynamoAsync(ProcessedEvent ev, CancellationToken cancellationToken)
        {
            var request = new PutItemRequest
            {
                TableName = "events",
                Item = new Dictionary<string, AttributeValue>
                {
                    { "event_id", new AttributeValue { S = ev.EventId } },
                    { "developer_id", new AttributeValue { S = ev.DeveloperId } },
                    { "metric_type", new AttributeValue { S = ev.MetricType } },
                    { "value", new AttributeValue { N = ev.Value.ToString() } },
                    { "repository", new AttributeValue { S = ev.Repository } },
                    { "timestamp", new AttributeValue { S = ev.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ") } },
                    { "processed_at", new AttributeValue { S = ev.ProcessedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") } },
                    { "processor_id", new AttributeValue { S = ev.ProcessorId } }
                }
            };
            await _dynamoDb.PutItemAsync(request, cancellationToken);
        }

        private async Task updateDeveloperSummaryAsync(ProcessedEvent ev, CancellationToken cancellationToken)
        {
            // Mapeamos qual campo deve ser incrementado baseando-se no tipo da métrica
            string metricField = ev.MetricType switch
            {
                "commit" => "total_commits",
                "pull_request" => "total_pull_requests",
                "review_time" => "total_review_time_sum",
                _ => "other_metrics" // fallback para contagem genérica
            };

            var request = new UpdateItemRequest
            {
                TableName = "developer_summary",
                Key = new Dictionary<string, AttributeValue> { { "developer_id", new AttributeValue { S = ev.DeveloperId } } },

                // A lógica atômica: ADD soma o valor, SET atualiza a data da última atividade
                UpdateExpression = "ADD #field :inc, events_processed :one SET last_activity = :now",
                
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#field", metricField }
                },

                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":inc", new AttributeValue { N = ev.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) } },
                    { ":one", new AttributeValue { N = "1" } },
                    { ":now", new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") } }
                }
            };

            try
            {
                await _dynamoDb.UpdateItemAsync(request, cancellationToken);
                _logger.LogInformation("Métricas de {DevId} atualizadas atomicamente. Campo: {Field}", ev.DeveloperId, metricField);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na atualização atômica do DynamoDB para o desenvolvedor {DevId}", ev.DeveloperId);
                throw; // Importante: relançar a exceção para o worker tratar o retry (Backoff)
            }
        }
    }
}