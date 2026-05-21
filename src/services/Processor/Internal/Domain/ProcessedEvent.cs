using Amazon.DynamoDBv2.DataModel;
using System;
using System.Text.Json.Serialization;

namespace Processor.Internal.Domain
{
    [DynamoDBTable("events")]
    public class ProcessedEvent
    {
        [DynamoDBHashKey]
        [JsonPropertyName("event_id")]
        public string EventId { get; set; } = string.Empty;
        
        [DynamoDBProperty]
        [JsonPropertyName("developer_id")]
        public string DeveloperId { get; set; } = string.Empty;
        
        [DynamoDBProperty]
        [JsonPropertyName("metric_type")]
        public string MetricType { get; set; } = string.Empty;
        
        [DynamoDBProperty]
        [JsonPropertyName("value")]
        public double Value { get; set; }
        [DynamoDBProperty]
        public string Repository { get; set; } = string.Empty;
        [DynamoDBProperty]
        public DateTime Timestamp { get; set; }

        // Campos de enriquecimento (Metadados de auditoria)
        [DynamoDBProperty]
        public DateTime ProcessedAt { get; set; }
        [DynamoDBProperty]
        public string ProcessorId { get; set; } = string.Empty;
    }
}