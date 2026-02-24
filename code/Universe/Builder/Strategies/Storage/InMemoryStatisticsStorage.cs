namespace Universe.Builder.Strategies.Storage;

/// <summary>
/// In-memory storage (default, no persistence across restarts)
/// </summary>
public sealed class InMemoryStatisticsStorage : IQueryStatisticsStorage
{
    /// <summary>
    /// Save is a no-op for in-memory storage (handled by QueryTuner's queue)
    /// </summary>
    public Task SaveAsync(QueryExecutionStatistics stats) => Task.CompletedTask;

    /// <summary>
    /// Load is a no-op for in-memory storage (QueryTuner maintains its own queue)
    /// </summary>
    public Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count)
        => Task.FromResult<IList<QueryExecutionStatistics>>([]);

    /// <summary>
    /// GetByQueryHash is a no-op for in-memory storage (QueryTuner maintains its own queue)
    /// </summary>
    public Task<IList<QueryExecutionStatistics>> GetByQueryHashAsync(string queryHash, TimeSpan window)
        => Task.FromResult<IList<QueryExecutionStatistics>>([]);

    /// <summary>
    /// Clear is a no-op for in-memory storage
    /// </summary>
    public Task ClearOldAsync(TimeSpan olderThan) => Task.CompletedTask;
}
