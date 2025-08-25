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

		double avgRU = similarQueries.Average(s => s.RequestUnits);
		TimeSpan avgTime = TimeSpan.FromMilliseconds(similarQueries.Average(s => s.ExecutionTime.TotalMilliseconds));
		double successRate = similarQueries.Count(s => s.WasSuccessful) / (double)similarQueries.Count;

		string bestStrategy = similarQueries
			.GroupBy(s => s.Strategy)
			.OrderBy(g => g.Average(s => s.RequestUnits + s.ExecutionTime.TotalMilliseconds))
			.FirstOrDefault()?.Key;

		Dictionary<string, object> hints = GenerateHints(similarQueries, queryType);

		return new(
			RecommendedStrategy: bestStrategy,
			AverageRU: avgRU,
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
			case QueryType.VectorSearch:
			case QueryType.HybridSearch:
			case QueryType.FullTextSearch:
				hints["MaxItemCount"] = 50; // Smaller batches for vector queries
				hints["MaxBufferedItemCount"] = 100;
				break;

			case QueryType.Aggregation:
				double avgResultCount = queries.Average(q => q.ResultCount);
				if (avgResultCount > 1000)
				{
					hints["MaxItemCount"] = 500; // Larger batches for aggregations
					hints["MaxBufferedItemCount"] = 1000;
				}

				break;

			case QueryType.Complex:
				hints["MaxConcurrency"] = 1; // Conservative for complex queries
				hints["ResponseContinuationTokenLimitInKb"] = 1;
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