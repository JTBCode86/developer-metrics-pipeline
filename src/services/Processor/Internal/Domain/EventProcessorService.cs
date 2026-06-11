using Processor.Internal.Domain;

public class EventProcessorService
{
    public (bool isValid, string reason) Validate(RawEvent raw)
    {
        if (raw == null) return (false, "Evento nulo.");

        if (string.IsNullOrEmpty(raw.DeveloperId))
            return (false, "DeveloperId ausente.");

        if (string.IsNullOrEmpty(raw.MetricType))
            return (false, "MetricType ausente.");

        // Só chega aqui se não falhou em nenhuma das verificações acima
        return (true, string.Empty);
    }

    public ProcessedEvent Transform(RawEvent raw, string instanceId)
    {
        return new ProcessedEvent
        {
            EventId = raw.EventId,
            DeveloperId = raw.DeveloperId,
            MetricType = raw.MetricType,
            Value = raw.Value,
            Repository = raw.Repository,
            Timestamp = raw.Timestamp,
            ProcessedAt = DateTime.UtcNow,
            ProcessorId = instanceId
        };
    }
}