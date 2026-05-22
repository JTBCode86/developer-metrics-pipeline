using System;
using System.Text.Json.Serialization;

namespace Processor.Internal.Domain
{
    public class RawEvent
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; set; } = string.Empty;

        [JsonPropertyName("developer_id")]
        public string DeveloperId { get; set; } = string.Empty;

        [JsonPropertyName("metric_type")]
        public string MetricType { get; set; } = string.Empty;

        public double Value { get; set; }
        public string Repository { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        // Regras de negócio rígidas pedidas no PDF do Case
        public (bool IsValid, string FailureReason) Validate()
        {
            var validations = new List<(Func<bool> Check, string Error)>
            {
                (() => Guid.TryParse(this.EventId, out _), "event_id obrigatório e deve ser um UUID válido."),
                (() => !string.IsNullOrWhiteSpace(DeveloperId), "developer_id obrigatório e não pode ser vazio."),
                (() => new[] { "commits", "pull_requests", "review_time_minutes" }.Contains(MetricType), "metric_type inválido."),
                (() => Value >= 0, "value não pode ser negativo."),
                (() => !(MetricType == "review_time_minutes" && Value > 1440), "Para 'review_time_minutes', o valor máximo é 1440."),
                (() => Timestamp <= DateTime.UtcNow, "timestamp não pode ser uma data futura.")
            };

            foreach (var rule in validations)
            {
                if (!rule.Check()) return (false, rule.Error);
            }
            return (true, string.Empty);
        }
    }
}