using System;

namespace Processor.Internal.Domain
{
    public class RawEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string DeveloperId { get; set; } = string.Empty;
        public string MetricType { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Repository { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        // Regras de negócio rígidas pedidas no PDF do Case
        public (bool IsValid, string FailureReason) Validate()
        {
            if (string.IsNullOrWhiteSpace(EventId) || !Guid.TryParse(EventId, out _))
                return (false, "event_id obrigatorio e deve ser um UUID v4 valido.");

            if (string.IsNullOrWhiteSpace(DeveloperId))
                return (false, "developer_id obrigatorio e nao pode ser vazio.");

            if (MetricType != "commits" && MetricType != "pull_requests" && MetricType != "review_time_minutes")
                return (false, "metric_type deve ser: 'commits', 'pull_requests' ou 'review_time_minutes'.");

            if (Value < 0)
                return (false, "value nao pode ser negativo.");

            if (MetricType == "review_time_minutes" && Value > 1440)
                return (false, "Para 'review_time_minutes', o valor maximo permitido eh 1440 (24h).");

            if (Timestamp > DateTime.UtcNow)
                return (false, "timestamp nao pode ser uma data futura.");

            return (true, string.Empty);
        }
    }
}