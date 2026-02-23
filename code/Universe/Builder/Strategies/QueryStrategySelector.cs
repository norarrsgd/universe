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
        // Get tuning recommendations for suggested hints
        QueryTuningRecommendations recommendations = tuner.GetRecommendations(context.Type);

        // Check if the strategy is forced via hints
        if (context.Hints?.TryGetValue(nameof(QueryHints.ForceStrategy), out object forcedStrategy) == true)
        {
            IQueryExecutionStrategy forced = _strategies
                .FirstOrDefault(s => s.Name == (string)forcedStrategy);
            if (forced?.CanHandle(query, context) == true)
                return ApplyRecommendedHints(forced, context, recommendations);
        }

        // Priority-based selection
        IQueryExecutionStrategy strategy = _strategies
            .Where(s => s.CanHandle(query, context))
            .OrderByDescending(s => s.Priority)
            .First();

        return ApplyRecommendedHints(strategy, context, recommendations);
    }

    private static EnhancedContextStrategy ApplyRecommendedHints(IQueryExecutionStrategy strategy, QueryContext context, QueryTuningRecommendations recommendations)
    {
        // Merge recommended hints with existing context hints (user hints take precedence)
        Dictionary<string, object> mergedHints = new(recommendations.SuggestedHints ?? new Dictionary<string, object>());

        if (context.Hints is not null)
        {
            foreach ((string key, object value) in context.Hints)
                mergedHints[key] = value; // User hints override recommendations
        }

        QueryContext enhancedContext = context with { Hints = mergedHints };
        return new(strategy, enhancedContext);
    }
}