using Universe.Builder.Strategies;

namespace Universe.Tests.Helpers;

internal static class TestStatisticsFactory
{
    public static QueryExecutionStatistics Create(
        string queryHash = "testhash",
        QueryType type = QueryType.Simple,
        double ru = 5.0,
        TimeSpan? executionTime = null,
        int resultCount = 10,
        bool success = true,
        DateTime? timestamp = null,
        string strategyUsed = "Direct",
        IReadOnlyDictionary<string, object> hintsUsed = null)
    {
        return new QueryExecutionStatistics
        {
            QueryHash = queryHash,
            Type = type,
            RU = ru,
            ExecutionTime = executionTime ?? TimeSpan.FromMilliseconds(50),
            ResultCount = resultCount,
            Success = success,
            Timestamp = timestamp ?? DateTime.UtcNow,
            StrategyUsed = strategyUsed,
            HintsUsed = hintsUsed
        };
    }

    public static List<QueryExecutionStatistics> CreateBatch(
        int count,
        QueryType type = QueryType.Simple,
        string queryHash = "batchhash",
        string strategyUsed = "Direct")
    {
        return Enumerable.Range(0, count)
            .Select(i => Create(
                queryHash: queryHash,
                type: type,
                ru: 5.0 + i * 0.1,
                executionTime: TimeSpan.FromMilliseconds(50 + i),
                resultCount: 10 + i,
                timestamp: DateTime.UtcNow.AddMinutes(-i),
                strategyUsed: strategyUsed))
            .ToList();
    }
}
