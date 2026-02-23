namespace Universe.Builder.Strategies.Storage;

/// <summary>
/// Interface for persisting query statistics
/// </summary>
public interface IQueryStatisticsStorage
{
    /// <summary>
    /// Save a query execution statistic
    /// </summary>
    Task SaveAsync(QueryExecutionStatistics stats);

    /// <summary>
    /// Load recent statistics
    /// </summary>
    Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count);

    /// <summary>
    /// Load statistics for a specific query hash within a time window
    /// </summary>
    /// <param name="queryHash">The query hash to filter by</param>
    /// <param name="window">Time window from now to look back</param>
    /// <returns>Statistics matching the query hash within the time window</returns>
    Task<IList<QueryExecutionStatistics>> GetByQueryHashAsync(string queryHash, TimeSpan window);

    /// <summary>
    /// Clear old statistics (older than specified timespan)
    /// </summary>
    Task ClearOldAsync(TimeSpan olderThan);
}
