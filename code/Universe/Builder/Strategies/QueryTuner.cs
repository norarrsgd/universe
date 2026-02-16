using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Universe.Builder.Strategies.Storage;

namespace Universe.Builder.Strategies;

/// <summary>
/// Provides query tuning recommendations based on query type and historical performance
/// </summary>
internal sealed partial class QueryTuner : IDisposable
{
	private readonly ConcurrentDictionary<QueryType, ConcurrentQueue<QueryExecutionStatistics>> _partitions;
	private readonly IQueryStatisticsStorage _storage;
	private int _totalCount;
	private const int MaxStoredExecutions = 1000;
	private const int MinSampleSizeForRecommendations = 10;

	[GeneratedRegex(@"@\w+")]
	private static partial Regex ParameterNamePattern();

	public QueryTuner(IQueryStatisticsStorage storage = null)
	{
		_partitions = new ConcurrentDictionary<QueryType, ConcurrentQueue<QueryExecutionStatistics>>();
		foreach (QueryType type in Enum.GetValues<QueryType>())
			_partitions[type] = new ConcurrentQueue<QueryExecutionStatistics>();
		_storage = storage ?? new InMemoryStatisticsStorage();

		// Load persisted statistics on startup (async, don't block constructor)
		_ = Task.Run(LoadPersistedStatisticsAsync);
	}

	/// <summary>
	/// Record a query execution for learning
	/// </summary>
	public void RecordExecution(QueryExecutionStatistics stats)
	{
		_partitions[stats.Type].Enqueue(stats);
		Interlocked.Increment(ref _totalCount);

		while (Interlocked.CompareExchange(ref _totalCount, 0, 0) > MaxStoredExecutions)
		{
			if (TryEvictOldest())
				Interlocked.Decrement(ref _totalCount);
			else
				break;
		}

		_ = PersistAsync(stats);
	}

	private bool TryEvictOldest()
	{
		QueryType? oldestType = null;
		DateTime oldestTimestamp = DateTime.MaxValue;

		foreach (var kvp in _partitions)
		{
			if (kvp.Value.TryPeek(out var front) && front.Timestamp < oldestTimestamp)
			{
				oldestTimestamp = front.Timestamp;
				oldestType = kvp.Key;
			}
		}

		if (oldestType.HasValue)
			return _partitions[oldestType.Value].TryDequeue(out _);

		return false;
	}

	private async Task PersistAsync(QueryExecutionStatistics stats)
	{
		try
		{
			await _storage.SaveAsync(stats);
		}
		catch (System.Exception ex)
		{
			Trace.TraceWarning($"[UniverseQuery] Failed to persist query statistics: {ex.Message}");
		}
	}

	/// <summary>
	/// Get recommendations for a query type
	/// </summary>
	public QueryTuningRecommendations GetRecommendations(QueryType queryType)
	{
		DateTime last24Hours = DateTime.UtcNow.AddHours(-24);

		// Get relevant statistics from last 24 hours
		List<QueryExecutionStatistics> relevantStats = [.. _partitions[queryType]
			.Where(s => s.Timestamp >= last24Hours)];

		// If insufficient data, fall back to rule-based recommendations
		if (relevantStats.Count < MinSampleSizeForRecommendations)
		{
			return GetRuleBasedRecommendations(queryType);
		}

		// Generate data-driven recommendations
		return new QueryTuningRecommendations(
			SuggestedHints: CalculateOptimalHints(relevantStats),
			RecommendedStrategy: DetermineOptimalStrategy(relevantStats),
			AverageRU: relevantStats.Average(s => s.RU),
			SuccessRate: relevantStats.Count(s => s.Success) / (double)relevantStats.Count,
			SampleSize: relevantStats.Count,
			AverageExecutionTime: TimeSpan.FromMilliseconds(
				relevantStats.Average(s => s.ExecutionTime.TotalMilliseconds)
			),
			IsDataDriven: true
		);
	}

	/// <summary>
	/// Compute hash for query pattern recognition
	/// </summary>
	public static string ComputeQueryHash(QueryContext context, string queryText)
	{
		// Normalize parameter names to produce consistent hashes for structurally identical queries.
		// Parameter names contain dynamic CatalystIds (GUIDs) that vary per instance,
		// so identical logical queries would otherwise get different hashes.
		string normalizedQuery = ParameterNamePattern().Replace(queryText, "@p");
		string signature = $"{context.Type}|{normalizedQuery}";
		byte[] bytes = Encoding.UTF8.GetBytes(signature);
		byte[] hash = SHA256.HashData(bytes);
		return Convert.ToBase64String(hash)[..16];
	}

	/// <summary>
	/// Fallback to rule-based recommendations (v3.1.x behavior)
	/// </summary>
	private static QueryTuningRecommendations GetRuleBasedRecommendations(QueryType queryType)
	{
		Dictionary<string, object> hints = GenerateHints(queryType);
		return new(
			SuggestedHints: hints,
			IsDataDriven: false
		);
	}

	/// <summary>
	/// Calculate optimal hints based on historical performance
	/// </summary>
	private static Dictionary<string, object> CalculateOptimalHints(List<QueryExecutionStatistics> stats)
	{
		// Analyze performance by different hint configurations
		var hintPerformance = stats
			.Where(s => s.HintsUsed != null && s.HintsUsed.Count > 0)
			.GroupBy(s => GetHintsSignature(s.HintsUsed!))
			.Select(g => new
			{
				Hints = g.First().HintsUsed!,
				AverageRU = g.Average(s => s.RU),
				SuccessRate = g.Count(s => s.Success) / (double)g.Count(),
				Count = g.Count()
			})
			.Where(x => x.Count >= 3) // Require at least 3 samples
			.OrderBy(x => x.AverageRU) // Prefer lower RU
			.ThenByDescending(x => x.SuccessRate) // Then higher success rate
			.FirstOrDefault();

		if (hintPerformance != null)
		{
			Dictionary<string, object> hints = [];
			foreach (var hint in hintPerformance.Hints)
			{
				hints[hint.Key] = hint.Value;
			}
			return hints;
		}

		return null;
	}

	/// <summary>
	/// Determine which strategy performs best for this query type
	/// </summary>
	private static string DetermineOptimalStrategy(List<QueryExecutionStatistics> stats)
	{
		var strategyPerformance = stats
			.GroupBy(s => s.StrategyUsed)
			.Select(g => new
			{
				Strategy = g.Key,
				AverageRU = g.Average(s => s.RU),
				SuccessRate = g.Count(s => s.Success) / (double)g.Count(),
				Count = g.Count()
			})
			.Where(x => x.Count >= 5) // Require at least 5 samples
			.OrderByDescending(x => x.SuccessRate) // Prefer higher success rate
			.ThenBy(x => x.AverageRU) // Then lower RU
			.FirstOrDefault();

		return strategyPerformance?.Strategy;
	}

	/// <summary>
	/// Generate a signature for a set of hints (for grouping)
	/// </summary>
	private static string GetHintsSignature(IReadOnlyDictionary<string, object> hints)
	{
		var sorted = hints.OrderBy(kvp => kvp.Key);
		return string.Join("|", sorted.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
	}

	/// <summary>
	/// Rule-based hint generation (v3.1.x logic kept as fallback)
	/// </summary>
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

	/// <summary>
	/// Load persisted statistics on startup
	/// </summary>
	private async Task LoadPersistedStatisticsAsync()
	{
		try
		{
			IList<QueryExecutionStatistics> persisted = await _storage.LoadRecentAsync(MaxStoredExecutions);
			foreach (QueryExecutionStatistics stat in persisted)
			{
				_partitions[stat.Type].Enqueue(stat);
				Interlocked.Increment(ref _totalCount);
			}
		}
		catch (System.Exception ex)
		{
			Trace.TraceWarning($"[UniverseQuery] Failed to load persisted statistics: {ex.Message}");
		}
	}

	public void Dispose()
	{
		if (_storage is IDisposable disposable)
			disposable.Dispose();
	}
}