using System.Collections.Concurrent;

namespace Universe.Builder.Strategies;

/// <summary>
/// Analyzes query execution patterns and provides tuning recommendations
/// </summary>
internal sealed class QueryTuner
{
	private readonly ConcurrentQueue<QueryExecutionStats> _executionHistory = new();
	private const int MaxHistorySize = 1000;

	public void RecordExecution(QueryExecutionStats stats)
	{
		_executionHistory.Enqueue(stats);

		// Keep only recent history
		while (_executionHistory.Count > MaxHistorySize)
		{
			_executionHistory.TryDequeue(out _);
		}
	}

	public QueryTuningRecommendations GetRecommendations(string queryHash, QueryType queryType)
	{
		List<QueryExecutionStats> allStats = [.. _executionHistory];
		DateTime cutoff = DateTime.UtcNow.AddHours(-24);

		List<QueryExecutionStats> similarQueries = [.. allStats.Where(s => (s.QueryHash == queryHash || s.QueryType == queryType) && s.ExecutedAt > cutoff)];

		if (similarQueries.Count == 0)
			return new();

		double avgRu = similarQueries.Average(s => s.RequestUnits);
		TimeSpan avgTime = TimeSpan.FromMilliseconds(similarQueries.Average(s => s.ExecutionTime.TotalMilliseconds));
		double successRate = similarQueries.Count(s => s.WasSuccessful) / (double)similarQueries.Count;

		string bestStrategy = similarQueries
			.GroupBy(s => s.Strategy)
			.OrderBy(g => g.Average(s => s.RequestUnits + s.ExecutionTime.TotalMilliseconds))
			.FirstOrDefault()?.Key;

		Dictionary<string, object> hints = GenerateHints(similarQueries, queryType);

		return new(
			RecommendedStrategy: bestStrategy,
			AverageRU: avgRu,
			AverageExecutionTime: avgTime,
			SuccessRate: successRate,
			SuggestedHints: hints);
	}

	private static Dictionary<string, object> GenerateHints(IReadOnlyList<QueryExecutionStats> queries, QueryType queryType)
	{
		Dictionary<string, object> hints = [];

		// Generate hints based on query patterns
		switch (queryType)
		{
			case QueryType.HybridSearch:
			case QueryType.VectorSearch:
			case QueryType.FullTextSearch:
				hints[nameof(QueryHints.MaxItemCount)] = Q.Limits.MaxVectorItems;
				hints[nameof(QueryHints.MaxBufferedItemCount)] = Q.Limits.MaxVectorItems;
				break;

			case QueryType.Aggregation:
				double avgResultCount = queries.Average(q => q.ResultCount);
				if (avgResultCount > Q.Limits.MaxItems)
				{
					hints[nameof(QueryHints.MaxItemCount)] = 500; // Larger batches for aggregations
					hints[nameof(QueryHints.MaxBufferedItemCount)] = Q.Limits.MaxItems;
				}

				break;

			case QueryType.Complex:
				hints[nameof(QueryHints.MaxConcurrency)] = 1; // Conservative for complex queries
				hints[nameof(QueryHints.ResponseContinuationTokenLimitInKb)] = 1;
				break;

			case QueryType.Simple:
			case QueryType.Join:
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(queryType), queryType, null);
		}

		return hints;
	}
}