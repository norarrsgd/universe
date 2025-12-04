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
	/// Clear old statistics (older than specified timespan)
	/// </summary>
	Task ClearOldAsync(TimeSpan olderThan);
}
