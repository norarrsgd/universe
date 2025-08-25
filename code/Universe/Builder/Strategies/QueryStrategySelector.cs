using System.Security.Cryptography;

namespace Universe.Builder.Strategies;

/// <summary>
/// Selects the best execution strategy for a given query
/// </summary>
internal sealed class QueryStrategySelector
{
	private readonly IReadOnlyList<IQueryExecutionStrategy> _strategies;
	private readonly QueryTuner _tuner;

	public QueryStrategySelector(QueryTuner tuner)
	{
		_tuner = tuner;
		_strategies =
		[
			new VectorSearchStrategy(),
			new DirectQueryStrategy(),
			new GatewayQueryStrategy() // Always last as fallback
		];
	}

	public IQueryExecutionStrategy SelectStrategy(QueryDefinition query, QueryContext context)
	{
		// Get tuning recommendations
		string queryHash = ComputeQueryHash(query.QueryText);
		QueryTuningRecommendations recommendations = _tuner.GetRecommendations(queryHash, context.Type);

		// Try recommended strategy first if available
		if (!string.IsNullOrWhiteSpace(recommendations.RecommendedStrategy))
		{
			IQueryExecutionStrategy recommendedStrategy = _strategies
				.FirstOrDefault(s => s.Name == recommendations.RecommendedStrategy);

			if (recommendedStrategy?.CanHandle(query, context) == true)
			{
				// Apply suggested hints
				Dictionary<string, object> mergedHints = new(context.Hints ?? new Dictionary<string, object>());
				if (recommendations.SuggestedHints is not null)
				{
					foreach ((string key, object value) in recommendations.SuggestedHints)
					{
						mergedHints[key] = value;
					}
				}

				QueryContext enhancedContext = context with { Hints = mergedHints };
				return recommendedStrategy;
			}
		}

		// Check if strategy is forced via hints
		if (context.Hints?.TryGetValue("ForceStrategy", out object forcedStrategy) == true)
		{
			IQueryExecutionStrategy forced = _strategies
				.FirstOrDefault(s => s.Name == (string)forcedStrategy);
			if (forced?.CanHandle(query, context) == true)
				return forced;
		}

		// Fall back to priority-based selection
		return _strategies
			.Where(strategy => strategy.CanHandle(query, context))
			.OrderByDescending(strategy => strategy.Priority)
			.First();
	}

	private static string ComputeQueryHash(string queryText)
	{
		// Normalize query for hashing (remove parameters, whitespace, etc.)
		string normalizedQuery = NormalizeQuery(queryText);

		using SHA256 sha256 = SHA256.Create();
		byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedQuery));
		return Convert.ToHexString(hashBytes)[..16]; // First 16 chars
	}

	// Remove parameter references and normalize whitespace
	// Parameters follow pattern @{ColumnName}{CatalystId} (e.g., @Name123ABC, @Category456DEF)
	private static string NormalizeQuery(string query)
	{
		int paramIndex = 0;
		string normalized = System.Text.RegularExpressions.Regex.Replace(query, @"@[a-zA-Z]\w*",
			match => $"@param{++paramIndex}");

		return normalized
			.Replace('\n', ' ')
			.Replace('\r', ' ')
			.Replace('\t', ' ')
			.Trim();
	}
}