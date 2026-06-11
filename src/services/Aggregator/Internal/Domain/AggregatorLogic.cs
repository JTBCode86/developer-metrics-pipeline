namespace Aggregator.Internal.Services
{
    public static class AggregatorLogic
    {
        public static string DetermineCategory(string metricType)
        {
            return metricType?.ToLower() switch
            {
                "commit" => "total_commits",
                "pull_request" => "total_pull_requests",
                _ => "other_metrics"
            };
        }
    }
}