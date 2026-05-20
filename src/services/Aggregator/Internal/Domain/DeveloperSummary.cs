using System;
using System.Text.Json.Serialization;

namespace Aggregator.Internal.Domain
{
    public class DeveloperSummary
    {
        [JsonPropertyName("developer_id")]
        public string DeveloperId { get; set; } = string.Empty;

        [JsonPropertyName("total_commits")]
        public int TotalCommits { get; set; }

        [JsonPropertyName("total_pull_requests")]
        public int TotalPullRequests { get; set; }

        [JsonPropertyName("avg_review_time_minutes")]
        public double AvgReviewTimeMinutes { get; set; }

        [JsonPropertyName("events_processed")]
        public int EventsProcessed { get; set; }

        [JsonPropertyName("last_activity")]
        public DateTime LastActivity { get; set; }

        // Campo auxiliar interno para controle da média ponderada
        [JsonIgnore]
        public double TotalReviewTimeSum { get; set; }

        // Regra de negócio sênior: Agregação incremental na memória antes de salvar
        public void UpdateMetrics(string metricType, double value, DateTime activityDate)
        {
            EventsProcessed++;

            if (activityDate > LastActivity)
                LastActivity = activityDate;

            switch (metricType.ToLower())
            {
                case "commits":
                    TotalCommits += (int)value;
                    break;
                case "pull_requests":
                    TotalPullRequests += (int)value;
                    break;
                case "review_time_minutes":
                    TotalReviewTimeSum += value;
                    // Calcula a média baseada no volume de eventos de review processados
                    int reviewEventsCount = EventsProcessed - TotalCommits - TotalPullRequests;
                    reviewEventsCount = Math.Max(reviewEventsCount, 1);
                    AvgReviewTimeMinutes = Math.Round(TotalReviewTimeSum / reviewEventsCount, 1);
                    break;
            }
        }
    }
}