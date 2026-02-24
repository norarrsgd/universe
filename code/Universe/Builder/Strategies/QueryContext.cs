namespace Universe.Builder.Strategies;

internal readonly record struct QueryContext(
    QueryType Type,
    int? MaxItemCount = null,
    IReadOnlyDictionary<string, object> Hints = null);

/// <summary>
/// Types of queries supported in Universe
/// </summary>
public enum QueryType
{
    /// <summary>Simple queries without complex operations</summary>
    Simple,

    /// <summary>Aggregation queries for summarizing data</summary>
    Aggregation,

    /// <summary>Vector search queries for high-dimensional data</summary>
    VectorSearch,

    /// <summary>Full-text search queries for unstructured text data</summary>
    FullTextSearch,

    /// <summary>Hybrid search queries combining multiple techniques</summary>
    HybridSearch,

    /// <summary>Join queries for combining data from multiple sources</summary>
    Join,

    /// <summary>Complex queries involving advanced logic and multiple steps</summary>
    Complex
}

/// <summary>
/// Recommendations for query tuning based on collected statistics
/// </summary>
public readonly record struct QueryTuningRecommendations(
    IReadOnlyDictionary<string, object> SuggestedHints = null,

    string RecommendedStrategy = null,

    double? AverageRU = null,

    double? SuccessRate = null,

    int SampleSize = 0,

    TimeSpan? AverageExecutionTime = null,

    bool IsDataDriven = false
);