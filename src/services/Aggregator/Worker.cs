using Aggregator.Internal.Domain;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
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
            _logger.LogInformation("Aggregator Worker iniciado.");
            await Task.Delay(10000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 5
                    }, stoppingToken);

                    if (response?.Messages != null)
                    {
                        foreach (var message in response.Messages)
                        {
                            try
                            {
                                using JsonDocument doc = JsonDocument.Parse(message.Body);
                                JsonElement root = doc.RootElement;

                                // Extração segura usando TryGetProperty
                                var processedEvent = new ProcessedEvent
                                {
                                    EventId = root.TryGetProperty("event_id", out var eid) ? eid.GetString() : Guid.NewGuid().ToString(),
                                    DeveloperId = root.TryGetProperty("developer_id", out var did) ? did.GetString() : "unknown",
                                    MetricType = root.TryGetProperty("metric_type", out var mt) ? mt.GetString() : "unknown",
                                    Value = root.TryGetProperty("value", out var val) ? val.GetDouble() : 0,
                                    Repository = root.TryGetProperty("repository", out var repo) ? repo.GetString() : "none",
                                    Timestamp = root.TryGetProperty("timestamp", out var ts) ? ts.GetDateTime() : DateTime.UtcNow
                                };

                                await saveEventToDynamoAsync(processedEvent, stoppingToken);
                                await updateDeveloperSummaryAsync(processedEvent, stoppingToken);

                                await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                                _logger.LogInformation("Evento {Id} processado com sucesso.", processedEvent.EventId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Erro ao processar mensagem específica.");
                            }
                        }
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Erro no loop de consumo.");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task updateDeveloperSummaryAsync(ProcessedEvent ev, CancellationToken cancellationToken)
        {
            string metricField = ev.MetricType switch
            {
                "commit" => "total_commits",
                "pull_request" => "total_pull_requests",
                "review_time" => "total_review_time_sum",
                _ => "other_metrics"
            };

            var request = new UpdateItemRequest
            {
                TableName = "developer_summary",
                Key = new Dictionary<string, AttributeValue> { { "developer_id", new AttributeValue { S = ev.DeveloperId } } },
                UpdateExpression = "ADD #field :inc, events_processed :one SET last_activity = :now",
                ExpressionAttributeNames = new Dictionary<string, string> { { "#field", metricField } },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":inc", new AttributeValue { N = ev.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) } },
                    { ":one", new AttributeValue { N = "1" } },
                    { ":now", new AttributeValue { S = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") } }
                }
            };
            await _dynamoDb.UpdateItemAsync(request, cancellationToken);
        }

        private async Task saveEventToDynamoAsync(ProcessedEvent ev, CancellationToken cancellationToken)
        {
            // Simplificado para apenas persistir o evento
            var request = new PutItemRequest
            {
                TableName = "events",
                Item = new Dictionary<string, AttributeValue>
                {
                    { "event_id", new AttributeValue { S = ev.EventId } },
                    { "developer_id", new AttributeValue { S = ev.DeveloperId } },
                    { "value", new AttributeValue { N = ev.Value.ToString() } }
                }
            };
            await _dynamoDb.PutItemAsync(request, cancellationToken);
        }
    }
}