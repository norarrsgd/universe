using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Universe.Builder.Strategies;

/// <summary>
/// Selects the best execution strategy for a given query
/// </summary>
internal sealed class QueryStrategySelector(QueryTuner tuner)
{
	private readonly IReadOnlyList<IQueryExecutionStrategy> _strategies =
	[
		new VectorSearchStrategy(),
		new DirectQueryStrategy(),
		new GatewayQueryStrategy() // Always last as fallback
	];

	public IQueryExecutionStrategy SelectStrategy(QueryDefinition query, QueryContext context)
	{
		// Get tuning recommendations
		string queryHash = ComputeQueryHash(query.QueryText);
		QueryTuningRecommendations recommendations = tuner.GetRecommendations(queryHash, context.Type);

		// Try recommended strategy first if available
		if (!string.IsNullOrWhiteSpace(recommendations.RecommendedStrategy))
		{
			IQueryExecutionStrategy recommendedStrategy = _strategies
				.FirstOrDefault(s => s.Name == recommendations.RecommendedStrategy);

			return EnhancedStrategy(query, context, recommendedStrategy, recommendations);
		}

		// Check if strategy is forced via hints
		if (context.Hints?.TryGetValue(nameof(QueryHints.ForceStrategy), out object forcedStrategy) == true)
		{
			IQueryExecutionStrategy forced = _strategies
				.FirstOrDefault(s => s.Name == (string)forcedStrategy);
			if (forced?.CanHandle(query, context) == true)
				return EnhancedStrategy(query, context, forced, recommendations);
		}

		// Fall back to priority-based selection
		return _strategies
			.Where(strategy => strategy.CanHandle(query, context))
			.OrderByDescending(strategy => strategy.Priority)
			.First();
	}

	private static EnhancedContextStrategy EnhancedStrategy(QueryDefinition query, QueryContext context, IQueryExecutionStrategy recommendedStrategy, QueryTuningRecommendations recommendations)
	{
		if (recommendedStrategy?.CanHandle(query, context) == true)
		{
			// Apply suggested hints and return with enhanced context
			Dictionary<string, object> mergedHints = new(context.Hints ?? new Dictionary<string, object>());
			if (recommendations.SuggestedHints is not null)
			{
				foreach ((string key, object value) in recommendations.SuggestedHints)
				{
					mergedHints[key] = value;
				}
			}

			QueryContext enhancedContext = context with { Hints = mergedHints };
			return new(recommendedStrategy, enhancedContext);
		}

		return new(recommendedStrategy, context);
	}

	private static string ComputeQueryHash(string queryText)
	{
		string normalizedQuery = NormalizeQuery(queryText);

		using SHA256 sha256 = SHA256.Create();
		byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedQuery));
		return Convert.ToHexString(hashBytes)[..16];
	}

	private static string NormalizeQuery(string query)
	{
		int paramIndex = 0;

		// Replace parameters with normalized names
		string normalized = Regex.Replace(query, @"@[a-zA-Z]\w*",
			_ => $"@param{++paramIndex}");

		// Normalize all whitespace in one operation
		return Regex.Replace(normalized, @"\s+", " ").Trim();
	}
}