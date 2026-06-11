using Aggregator.Internal.Domain;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;

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
            _sqsClient = sqsClient;
            _dynamoDb = dynamoDb;
            _logger = logger;

            // Configuração robusta da URL da fila
            var baseUrl = configuration["AWS:ServiceURL"] ?? "http://localstack:4566";
            _queueUrl = $"{baseUrl}/000000000000/raw-events";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Aggregator Worker iniciado e aguardando mensagens...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 20
                    }, stoppingToken);

                    if (response?.Messages != null && response.Messages.Count > 0)
                    {
                        await ProcessMessagesAsync(response.Messages, stoppingToken);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Erro crítico no loop do Worker.");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task ProcessMessagesAsync(List<Message> messages, CancellationToken ct)
        {
            foreach (var message in messages)
            {
                try
                {
                    using var doc = JsonDocument.Parse(message.Body);
                    var root = doc.RootElement;

                    var ev = new ProcessedEvent
                    {
                        EventId = root.TryGetProperty("event_id", out var eid) ? eid.GetString() : Guid.NewGuid().ToString(),
                        DeveloperId = root.TryGetProperty("developer_id", out var did) ? did.GetString() : "unknown",
                        MetricType = root.TryGetProperty("metric_type", out var mt) ? mt.GetString() : "unknown",
                        Value = root.TryGetProperty("value", out var val) ? val.GetDouble() : 0,
                        Timestamp = DateTime.UtcNow
                    };

                    // Executa operações de persistência
                    await SaveEventToDynamoAsync(ev, ct);
                    await UpdateDeveloperSummaryAsync(ev, ct);

                    // Deleta apenas se tudo ocorreu bem
                    await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, ct);
                    _logger.LogInformation("Evento {Id} processado e removido da fila.", ev.EventId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao processar mensagem {Id}.", message.MessageId);
                }
            }
        }

        private async Task UpdateDeveloperSummaryAsync(ProcessedEvent ev, CancellationToken ct)
        {
            string metricField = ev.MetricType switch
            {
                "commit" or "commits" => "total_commits",
                "pull_request" or "pull_requests" => "total_pull_requests",
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
                },
                ReturnValues = "UPDATED_NEW"
            };

            await _dynamoDb.UpdateItemAsync(request, ct);
        }

        private async Task SaveEventToDynamoAsync(ProcessedEvent ev, CancellationToken ct)
        {
            var request = new PutItemRequest
            {
                TableName = "events",
                Item = new Dictionary<string, AttributeValue>
                {
                    { "event_id", new AttributeValue { S = ev.EventId } },
                    { "developer_id", new AttributeValue { S = ev.DeveloperId } },
                    { "value", new AttributeValue { N = ev.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) } }
                }
            };
            await _dynamoDb.PutItemAsync(request, ct);
        }
    }
}