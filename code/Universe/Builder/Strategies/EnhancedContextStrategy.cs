using Universe.Response;

namespace Universe.Builder.Strategies;

internal sealed class EnhancedContextStrategy(IQueryExecutionStrategy innerStrategy, QueryContext enhancedContext) : IQueryExecutionStrategy
{
    // Delegate properties to inner strategy
    public string Name => innerStrategy.Name;
    public int Priority => innerStrategy.Priority;

    // Delegate methods but use enhanced context
    bool IQueryExecutionStrategy.CanHandle(QueryDefinition query, QueryContext context) => innerStrategy.CanHandle(query, enhancedContext);

    Task<(Gravity gravity, IList<T> results)> IQueryExecutionStrategy.ExecuteAsync<T>(
        Container container,
        QueryDefinition query,
        QueryContext _,
        bool recordQueries,
        QueryTuner queryTuner)
        => innerStrategy.ExecuteAsync<T>(container, query, enhancedContext, recordQueries, queryTuner);
}