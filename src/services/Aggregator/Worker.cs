using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aggregator.Internal.Domain;

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
            _queueUrl = $"{baseUrl}/000000000000/processed-events";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Aggregator Worker iniciado e ouvindo a fila processed-events.");

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
                                var processedEvent = JsonSerializer.Deserialize<ProcessedEvent>(message.Body, new JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                });

                                if (processedEvent == null) continue;

                                using (_logger.BeginScope("{EventId}", processedEvent.EventId))
                                {
                                    _logger.LogInformation("Consumindo evento processado para agregacao.");

                                    bool isDuplicate = await checkIdempotencyAsync(processedEvent.EventId, stoppingToken);
                                    if (isDuplicate)
                                    {
                                        _logger.LogWarning("Evento duplicado detectado. Ignorando processamento.");
                                        await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                                        continue;
                                    }

                                    await saveEventToDynamoAsync(processedEvent, stoppingToken);
                                    await updateDeveloperSummaryAsync(processedEvent, stoppingToken);

                                    await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                                    _logger.LogInformation("Métricas agregadas e persistidas com sucesso.");
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
            var getRequest = new GetItemRequest
            {
                TableName = "developer_summary",
                Key = new Dictionary<string, AttributeValue> { { "developer_id", new AttributeValue { S = ev.DeveloperId } } }
            };

            var getResponse = await _dynamoDb.GetItemAsync(getRequest, cancellationToken);
            var summary = new DeveloperSummary { DeveloperId = ev.DeveloperId };

            if (getResponse.IsItemSet)
            {
                summary.TotalCommits = int.Parse(getResponse.Item["total_commits"].N);
                summary.TotalPullRequests = int.Parse(getResponse.Item["total_pull_requests"].N);
                summary.AvgReviewTimeMinutes = double.Parse(getResponse.Item["avg_review_time_minutes"].N);
                summary.EventsProcessed = int.Parse(getResponse.Item["events_processed"].N);
                summary.LastActivity = DateTime.Parse(getResponse.Item["last_activity"].S);

                if (getResponse.Item.TryGetValue("total_review_time_sum", out var reviewSumAttr))
                    summary.TotalReviewTimeSum = double.Parse(reviewSumAttr.N);
            }

            summary.UpdateMetrics(ev.MetricType, ev.Value, ev.Timestamp);

            var putRequest = new PutItemRequest
            {
                TableName = "developer_summary",
                Item = new Dictionary<string, AttributeValue>
                {
                    { "developer_id", new AttributeValue { S = summary.DeveloperId } },
                    { "total_commits", new AttributeValue { N = summary.TotalCommits.ToString() } },
                    { "total_pull_requests", new AttributeValue { N = summary.TotalPullRequests.ToString() } },
                    { "avg_review_time_minutes", new AttributeValue { N = summary.AvgReviewTimeMinutes.ToString().Replace(",", ".") } },
                    { "events_processed", new AttributeValue { N = summary.EventsProcessed.ToString() } },
                    { "last_activity", new AttributeValue { S = summary.LastActivity.ToString("yyyy-MM-ddTHH:mm:ssZ") } },
                    { "total_review_time_sum", new AttributeValue { N = summary.TotalReviewTimeSum.ToString().Replace(",", ".") } }
                }
            };
            await _dynamoDb.PutItemAsync(putRequest, cancellationToken);
        }
    }
}