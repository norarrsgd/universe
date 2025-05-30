using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos.Scripts;
using Universe.Response;

namespace Universe;

/// <summary>Inherit repositories to implement a more advanced Universe</summary>
public abstract class Galaxy<T>(
    CosmosClient client,
    string database,
    string container,
    string partitionKey,
    bool recordQueries = false) : GalaxyBasic<T>(client, database, container, partitionKey, recordQueries), IGalaxy<T> where T : class, ICosmicEntity
{
    async Task<(Gravity, T)> IGalaxy<T>.Get(IList<Cluster> clusters, IList<string> columns)
    {
        try
        {
            QueryDefinition query = _qBuilder.CreateQuery(clusters, columnOptions: columns is null || !columns.Any() ? null : new(columns));
            return await _qBuilder.GetOneFromQuery(_container, query);
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

    async Task<(Gravity, IList<T>)> IGalaxy<T>.List(IList<Cluster> clusters, ColumnOptions? columnOptions, IList<Sorting.Option> sorting, IList<string> group)
    {
        try
        {
            QueryDefinition query = _qBuilder.CreateQuery(clusters: clusters, columnOptions: columnOptions, sorting: sorting, groups: group);
            return await _qBuilder.GetListFromQuery(_container, query);
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    async Task<(Gravity, IList<T>)> IGalaxy<T>.Paged(Q.Page page, IList<Cluster> clusters, ColumnOptions? columnOptions, IList<Sorting.Option> sorting, IList<string> group)
    {
        try
        {
            QueryDefinition query = _qBuilder.CreateQuery(clusters: clusters, columnOptions: columnOptions, sorting: sorting, groups: group);

            double requestUnit = 0;
            string continuationToken = string.Empty;
            List<T> collection = [];
            using FeedIterator<T> queryResponse = _container.GetItemQueryIterator<T>(query,
                requestOptions: new() { MaxItemCount = page.Size },
                continuationToken: string.IsNullOrWhiteSpace(page.ContinuationToken) ? null : page.ContinuationToken
            );
            while (queryResponse.HasMoreResults)
            {
                FeedResponse<T> next = await queryResponse.ReadNextAsync();
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
}