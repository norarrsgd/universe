using System.Net;
using Universe.Response;
using Universe.Builder.Strategies;

namespace Universe;

/// <summary>Inherit repositories to implement a more advanced Universe</summary>
public abstract class Galaxy<T> : GalaxyBasic<T>, IGalaxy<T> where T : class, ICosmicEntity
{
    /// <summary>Create a new Galaxy with default settings</summary>
    protected Galaxy(
        CosmosClient client,
        string database,
        string container,
        IReadOnlyList<string> partitionKey,
        bool recordQueries = false) : base(client, database, container, partitionKey, recordQueries)
    {
    }

    /// <summary>Create a new Galaxy with custom Universe options</summary>
    protected Galaxy(
        CosmosClient client,
        string database,
        string container,
        IReadOnlyList<string> partitionKey,
        UniverseOptions options,
        bool recordQueries = false) : base(client, database, container, partitionKey, options, recordQueries)
    {
    }

    async Task<(Gravity, T)> IGalaxy<T>.Get(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns)
        => await InternalGet<T>(clusters, columns);

    async Task<(Gravity g, TS S)> IGalaxy<T>.Get<TS>(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns)
        => await InternalGet<TS>(clusters, columns);

    async Task<(Gravity, IList<T>)> IGalaxy<T>.List(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group, QueryHints? hints)
        => hints is null ? await InternalList<T>(clusters, columnOptions, sorting, group) : await InternalListWithHints<T>(clusters, columnOptions, sorting, group, hints);

    async Task<(Gravity g, IList<TS> T)> IGalaxy<T>.List<TS>(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group, QueryHints? hints)
        => hints is null ? await InternalList<TS>(clusters, columnOptions, sorting, group) : await InternalListWithHints<TS>(clusters, columnOptions, sorting, group, hints);

    async Task<(Gravity, IList<T>)> IGalaxy<T>.Paged(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group)
        => await InternalPaged<T>(page, clusters, columnOptions, sorting, group);

    async Task<(Gravity g, IList<TS> T)> IGalaxy<T>.Paged<TS>(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group)
        => await InternalPaged<TS>(page, clusters, columnOptions, sorting, group);

    private async Task<(Gravity g, TArgType S)> InternalGet<TArgType>(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns)
    {
        try
        {
            QueryDefinition query = QBuilder.CreateQuery(clusters, columnOptions: columns is null || !columns.Any() ? null : new(columns));
            return await QBuilder.GetOneFromQuery<TArgType>(_container, query);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UniverseException($"{typeof(T).Name} does not exist.");
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    private async Task<(Gravity g, IList<TArgType> T)> InternalList<TArgType>(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group)
    {
        try
        {
            QueryDefinition query = QBuilder.CreateQuery(clusters: clusters, columnOptions: columnOptions, sorting: sorting, groups: group);
            return await QBuilder.GetListFromQuery<TArgType>(_container, query);
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    private async Task<(Gravity g, IList<TArgType> T)> InternalPaged<TArgType>(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group)
    {
        try
        {
            QueryDefinition query = QBuilder.CreateQuery(clusters: clusters, columnOptions: columnOptions, sorting: sorting, groups: group);

            double requestUnit = 0;
            string continuationToken = string.Empty;
            List<TArgType> collection = [];
            using FeedIterator<TArgType> queryResponse = _container.GetItemQueryIterator<TArgType>(query,
                requestOptions: new() { MaxItemCount = page.Size },
                continuationToken: string.IsNullOrWhiteSpace(page.ContinuationToken) ? null : page.ContinuationToken
            );
            while (queryResponse.HasMoreResults)
            {
                FeedResponse<TArgType> next = await queryResponse.ReadNextAsync();
                collection.AddRange(next);
                requestUnit += next.RequestCharge;
                if (next.Count > 0 && !query.QueryText.Contains("GROUP BY"))
                {
                    continuationToken = next.ContinuationToken;
                    break;
                }
            }

            return (new(requestUnit, continuationToken, _recordQuery ? (query.QueryText, query.GetQueryParameters()) : default), collection);
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    Gravity IGalaxy<T>.GenerateQuery(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group)
    {
        QueryDefinition query = QBuilder.CreateQuery(clusters: clusters, columnOptions: columnOptions, sorting: sorting, groups: group);
        return new(0, string.Empty, (query.QueryText, query.GetQueryParameters()));
    }

    QueryTuningRecommendations IGalaxy<T>.GetQueryRecommendations(string queryPattern, QueryType queryType) => QBuilder.GetQueryRecommendations(queryType);

    private async Task<(Gravity g, IList<TArgType> T)> InternalListWithHints<TArgType>(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group, QueryHints? hints)
    {
        try
        {
            QueryDefinition query = QBuilder.CreateQuery(clusters: clusters, columnOptions: columnOptions, sorting: sorting, groups: group);

            QueryContext context = new(
                Type: InferQueryType(query),
                Hints: hints?.ToContextHints()
            );

            return await QBuilder.GetListFromQuery<TArgType>(_container, query, context);
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    private static QueryType InferQueryType(QueryDefinition query)
    {
        string queryText = query.QueryText.ToUpperInvariant();

        if (queryText.Contains("VECTORDISTANCE") && queryText.Contains("FULLTEXTSCORE"))
            return QueryType.HybridSearch;
        else if (queryText.Contains("VECTORDISTANCE"))
            return QueryType.VectorSearch;
        else if (queryText.Contains("FULLTEXTSCORE"))
            return QueryType.FullTextSearch;
        else if (queryText.Contains("GROUP BY") || queryText.Contains("COUNT") || queryText.Contains("SUM"))
            return QueryType.Aggregation;
        else if (queryText.Contains("JOIN"))
            return QueryType.Join;
        else if (queryText.Contains("RRF") || queryText.Split(' ').Length > 20)
            return QueryType.Complex;

        return QueryType.Simple;
    }
}