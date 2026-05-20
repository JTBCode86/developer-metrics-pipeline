using System;

namespace Processor.Internal.Domain
{
    public class ProcessedEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string DeveloperId { get; set; } = string.Empty;
        public string MetricType { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Repository { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        // Campos de enriquecimento (Metadados de auditoria)
        public DateTime ProcessedAt { get; set; }
        public string ProcessorId { get; set; } = string.Empty;
    }
}