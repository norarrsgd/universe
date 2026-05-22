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
    QueryExecutionStrategy? ForceStrategy = null,
    int? MaxItemCount = null,
    int? MaxBufferedItemCount = null,
    int? MaxConcurrency = null,
    bool? EnableOptimisticDirectExecution = null,
    int? ResponseContinuationTokenLimitInKb = null)
{
    /// <summary>
    /// Convert to context hints dictionary
    /// </summary>
    public IReadOnlyDictionary<string, object> ToContextHints()
    {
        Validate();

        Dictionary<string, object> hints = [];

        if (MaxItemCount.HasValue)
            hints[nameof(MaxItemCount)] = MaxItemCount.Value;
        if (MaxBufferedItemCount.HasValue)
            hints[nameof(MaxBufferedItemCount)] = MaxBufferedItemCount.Value;
        if (MaxConcurrency.HasValue)
            hints[nameof(MaxConcurrency)] = MaxConcurrency.Value;
        if (ForceStrategy.HasValue)
            hints[nameof(ForceStrategy)] = ForceStrategy.Value.ToStrategyName();
        if (EnableOptimisticDirectExecution.HasValue)
            hints[nameof(EnableOptimisticDirectExecution)] = EnableOptimisticDirectExecution.Value;
        if (ResponseContinuationTokenLimitInKb.HasValue)
            hints[nameof(ResponseContinuationTokenLimitInKb)] = ResponseContinuationTokenLimitInKb.Value;

        return hints;
    }

    private void Validate()
    {
        ValidateRange(MaxItemCount, nameof(MaxItemCount), 1, Q.Limits.MaxItems);
        ValidateRange(MaxBufferedItemCount, nameof(MaxBufferedItemCount), 1, Q.Limits.MaxItems);
        ValidateRange(MaxConcurrency, nameof(MaxConcurrency), 1, Environment.ProcessorCount);
        ValidateRange(ResponseContinuationTokenLimitInKb, nameof(ResponseContinuationTokenLimitInKb), 1, Q.Limits.MaxContinuationTokenKb);
    }

    private static void ValidateRange(int? value, string name, int min, int max)
    {
        if (value is null)
            return;

        if (value.Value < min || value.Value > max)
            throw new UniverseException($"{name} must be between {min} and {max}.");
    }
}
