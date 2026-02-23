using Universe.Response;

namespace Universe.Builder.Strategies;

/// <summary>
/// Defines the contract for query execution strategies
/// </summary>
internal interface IQueryExecutionStrategy
{
    /// <summary>
    /// Strategy name for identification
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines if this strategy can handle the given query
    /// </summary>
    bool CanHandle(QueryDefinition query, QueryContext context);

    /// <summary>
    /// Gets the priority of this strategy (higher = more preferred)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Executes the query and returns results with gravity information
    /// </summary>
    Task<(Gravity gravity, IList<T> results)> ExecuteAsync<T>(
        Container container,
        QueryDefinition query,
        QueryContext context,
        bool recordQueries,
        QueryTuner queryTuner = null);
}