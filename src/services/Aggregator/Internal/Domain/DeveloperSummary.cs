using Amazon.DynamoDBv2.DataModel;
using System;
using System.Text.Json.Serialization;

namespace Aggregator.Internal.Domain
{
    [DynamoDBTable("developer_summary")]
    public class DeveloperSummary
    {
        [DynamoDBHashKey]
        [JsonPropertyName("developer_id")]
        public string DeveloperId { get; set; } = string.Empty;

        [DynamoDBProperty("total_commits")]
        [JsonPropertyName("total_commits")]
        public int TotalCommits { get; set; }

        [DynamoDBProperty("total_pull_requests")]
        [JsonPropertyName("total_pull_requests")]
        public int TotalPullRequests { get; set; }

        [DynamoDBProperty("avg_review_time_minutes")]
        [JsonPropertyName("avg_review_time_minutes")]
        public double AvgReviewTimeMinutes { get; set; }

        [DynamoDBProperty("events_processed")]
        [JsonPropertyName("events_processed")]
        public int EventsProcessed { get; set; }

        [DynamoDBProperty("last_activity")]
        [JsonPropertyName("last_activity")]
        public DateTime LastActivity { get; set; }

        // Campo auxiliar interno para controle da média ponderada
        [JsonIgnore]
        [DynamoDBIgnore]
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