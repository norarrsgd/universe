using Universe.Response;
using Universe.Builder.Strategies;

namespace Universe.Interfaces;

/// <summary></summary>
public interface IGalaxy<T> : IGalaxyBasic<T> where T : ICosmicEntity
{
    /// <summary>Execute a full query shape and return its first result, with optional read consistency.</summary>
    Task<(Gravity g, T T)> Get(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group, QueryHints? hints, ReadOptions readOptions)
        => readOptions?.ConsistencyLevel is null && columnOptions?.Projection is null
            && columnOptions?.Join is null && columnOptions?.Aggregates is not { Count: > 0 }
            && columnOptions?.IsDistinct != true && (columnOptions?.Top ?? 0) is >= 0 and <= 1
            && sorting is not { Count: > 0 } && group is not { Count: > 0 } && hints is null
            ? Get(clusters, columnOptions?.Names)
            : throw new NotSupportedException("This IGalaxy implementation does not support full-shape single reads or consistency overrides.");

    /// <summary>Execute a list query with per-operation consistency.</summary>
    Task<(Gravity g, IList<T> T)> List(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group, QueryHints? hints, ReadOptions readOptions)
        => readOptions?.ConsistencyLevel is null && columnOptions?.Projection is null
            ? List(clusters, columnOptions, sorting, group, hints)
            : throw new NotSupportedException("This IGalaxy implementation does not support structured projections or consistency overrides.");

    /// <summary>Execute a paged query with per-operation consistency.</summary>
    Task<(Gravity g, IList<T> T)> Paged(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group, ReadOptions readOptions)
        => readOptions?.ConsistencyLevel is null && columnOptions?.Projection is null
            ? Paged(page, clusters, columnOptions, sorting, group)
            : throw new NotSupportedException("This IGalaxy implementation does not support structured projections or consistency overrides.");

    /// <summary>Execute a full query shape and return its first result, with optional read consistency.</summary>
    Task<(Gravity g, TS T)> Get<TS>(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group, QueryHints? hints, ReadOptions readOptions)
        => readOptions?.ConsistencyLevel is null && columnOptions?.Projection is null
            && columnOptions?.Join is null && columnOptions?.Aggregates is not { Count: > 0 }
            && columnOptions?.IsDistinct != true && (columnOptions?.Top ?? 0) is >= 0 and <= 1
            && sorting is not { Count: > 0 } && group is not { Count: > 0 } && hints is null
            ? Get<TS>(clusters, columnOptions?.Names)
            : throw new NotSupportedException("This IGalaxy implementation does not support full-shape single reads or consistency overrides.");

    /// <summary>Execute a list query with per-operation consistency.</summary>
    Task<(Gravity g, IList<TS> T)> List<TS>(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group, QueryHints? hints, ReadOptions readOptions)
        => readOptions?.ConsistencyLevel is null && columnOptions?.Projection is null
            ? List<TS>(clusters, columnOptions, sorting, group, hints)
            : throw new NotSupportedException("This IGalaxy implementation does not support structured projections or consistency overrides.");

    /// <summary>Execute a paged query with per-operation consistency.</summary>
    Task<(Gravity g, IList<TS> T)> Paged<TS>(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group, ReadOptions readOptions)
        => readOptions?.ConsistencyLevel is null && columnOptions?.Projection is null
            ? Paged<TS>(page, clusters, columnOptions, sorting, group)
            : throw new NotSupportedException("This IGalaxy implementation does not support structured projections or consistency overrides.");

    /// <summary>
    /// Get one model from the database
    /// </summary>
    Task<(Gravity g, T T)> Get(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns = null);

    /// <summary>
    /// Get one model from the database with a different type
    /// </summary>
    Task<(Gravity g, TS S)> Get<TS>(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns = null);

    /// <summary>
    /// Get list from the database with optional query optimization hints
    /// </summary>
    Task<(Gravity g, IList<T> T)> List(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null, QueryHints? hints = null);

    /// <summary>
    /// Get list from the database with a different type
    /// </summary>
    Task<(Gravity g, IList<TS> T)> List<TS>(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null, QueryHints? hints = null);

    /// <summary>
    /// Get a paginated list from the database
    /// </summary>
    Task<(Gravity g, IList<T> T)> Paged(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null);

    /// <summary>
    /// Get a paginated list from the database
    /// </summary>
    Task<(Gravity g, IList<TS> T)> Paged<TS>(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null);

    /// <summary>
    /// Generate SQL query without executing it
    /// </summary>
    Gravity GenerateQuery(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null);

    /// <summary>
    /// Get query execution recommendations for optimization
    /// </summary>
    QueryTuningRecommendations GetQueryRecommendations(string queryPattern, QueryType queryType);
}
