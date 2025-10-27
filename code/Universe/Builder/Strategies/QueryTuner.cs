namespace Universe.Builder.Strategies;

/// <summary>
/// Provides query tuning recommendations based on query type
/// </summary>
internal sealed class QueryTuner
{
	public QueryTuningRecommendations GetRecommendations(QueryType queryType)
	{
		Dictionary<string, object> hints = GenerateHints(queryType);

		return new(SuggestedHints: hints);
	}

	private static Dictionary<string, object> GenerateHints(QueryType queryType)
	{
		Dictionary<string, object> hints = [];

		// Generate hints based on query type patterns
		switch (queryType)
		{
			case QueryType.HybridSearch:
			case QueryType.VectorSearch:
			case QueryType.FullTextSearch:
				hints[nameof(QueryHints.MaxItemCount)] = Q.Limits.MaxVectorItems;
				hints[nameof(QueryHints.MaxBufferedItemCount)] = Q.Limits.MaxVectorItems;
				break;

			case QueryType.Aggregation:
				hints[nameof(QueryHints.MaxItemCount)] = 500; // Larger batches for aggregations
				hints[nameof(QueryHints.MaxBufferedItemCount)] = Q.Limits.MaxItems;
				break;

			case QueryType.Complex:
				hints[nameof(QueryHints.MaxConcurrency)] = 1; // Conservative for complex queries
				hints[nameof(QueryHints.ResponseContinuationTokenLimitInKb)] = 1;
				break;

			case QueryType.Simple:
			case QueryType.Join:
				// Use default hints
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(queryType), queryType, null);
		}

		return hints;
	}
}