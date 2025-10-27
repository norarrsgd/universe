namespace Universe.Builder.Strategies;

/// <summary>
/// Context information for query execution
/// </summary>
internal readonly record struct QueryContext(
	QueryType Type,
	int? MaxItemCount = null,
	IReadOnlyDictionary<string, object> Hints = null);

/// <summary>
/// Types of queries for strategy selection
/// </summary>
public enum QueryType
{
	/// <summary>Simple queries without complex operations</summary>
	Simple,

	/// <summary>Queries with aggregation operations like GROUP BY, COUNT, SUM</summary>
	Aggregation,

	/// <summary>Vector similarity search queries</summary>
	VectorSearch,

	/// <summary>Full-text search queries</summary>
	FullTextSearch,

	/// <summary>Hybrid queries combining vector and full-text search</summary>
	HybridSearch,

	/// <summary>Queries with JOIN operations</summary>
	Join,

	/// <summary>Complex queries with multiple operations or RRF</summary>
	Complex
}

/// <summary>
/// Query tuning recommendations based on execution history
/// </summary>
public readonly record struct QueryTuningRecommendations(IReadOnlyDictionary<string, object> SuggestedHints = null);