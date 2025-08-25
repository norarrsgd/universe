namespace Universe.Builder.Options;

/// <summary>
/// Supported query execution strategies
/// </summary>
public enum QueryExecutionStrategy
{
	/// <summary>Direct execution strategy for simple queries</summary>
	Direct,

	/// <summary>Gateway mode strategy for complex queries and fallbacks</summary>
	Gateway,

	/// <summary>Specialized strategy for vector search queries</summary>
	VectorSearch
}

/// <summary>
/// Extension methods for QueryExecutionStrategy
/// </summary>
public static class QueryExecutionStrategyExtensions
{
	/// <summary>
	/// Get the strategy name as used internally
	/// </summary>
	public static string ToStrategyName(this QueryExecutionStrategy strategy) => strategy switch
	{
		QueryExecutionStrategy.Direct => "Direct",
		QueryExecutionStrategy.Gateway => "Gateway",
		QueryExecutionStrategy.VectorSearch => "VectorSearch",
		_ => throw new ArgumentOutOfRangeException(nameof(strategy))
	};
}

/// <summary>
/// Query hints for optimization
/// </summary>
public readonly record struct QueryHints(
	int? MaxItemCount = null,
	int? MaxBufferedItemCount = null,
	int? MaxConcurrency = null,
	QueryExecutionStrategy? ForceStrategy = null,
	bool? EnableOptimisticDirectExecution = null,
	int? ResponseContinuationTokenLimitInKb = null)
{
	/// <summary>
	/// Convert to context hints dictionary
	/// </summary>
	public IReadOnlyDictionary<string, object> ToContextHints()
	{
		Dictionary<string, object> hints = [];

		if (MaxItemCount.HasValue)
			hints["MaxItemCount"] = MaxItemCount.Value;
		if (MaxBufferedItemCount.HasValue)
			hints["MaxBufferedItemCount"] = MaxBufferedItemCount.Value;
		if (MaxConcurrency.HasValue)
			hints["MaxConcurrency"] = MaxConcurrency.Value;
		if (ForceStrategy.HasValue)
			hints["ForceStrategy"] = ForceStrategy.Value.ToStrategyName();
		if (EnableOptimisticDirectExecution.HasValue)
			hints["EnableOptimisticDirectExecution"] = EnableOptimisticDirectExecution.Value;
		if (ResponseContinuationTokenLimitInKb.HasValue)
			hints["ResponseContinuationTokenLimitInKb"] = ResponseContinuationTokenLimitInKb.Value;

		return hints;
	}
}