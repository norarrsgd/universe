namespace Universe.Builder.Strategies;

/// <summary>
/// Represents performance metrics for a single query execution
/// </summary>
public sealed record QueryExecutionStatistics
{
	/// <summary>
	/// Hash of the query structure (not the raw query text)
	/// </summary>
	public required string QueryHash { get; init; }

	/// <summary>
	/// Detected query type
	/// </summary>
	public required QueryType Type { get; init; }

	/// <summary>
	/// Request Units consumed
	/// </summary>
	public required double RU { get; init; }

	/// <summary>
	/// Total execution time
	/// </summary>
	public required TimeSpan ExecutionTime { get; init; }

	/// <summary>
	/// Number of results returned
	/// </summary>
	public required int ResultCount { get; init; }

	/// <summary>
	/// Whether the query succeeded
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Timestamp of execution (UTC)
	/// </summary>
	public required DateTime Timestamp { get; init; }

	/// <summary>
	/// Strategy used for execution
	/// </summary>
	public required string StrategyUsed { get; init; }

	/// <summary>
	/// Query hints that were applied
	/// </summary>
	public IReadOnlyDictionary<string, object> HintsUsed { get; init; }
}
