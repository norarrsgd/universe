using System.Net;
using Universe.Response;

namespace Universe;

/// <summary>Inherit repositories to implement a more advanced Universe</summary>
public abstract class Galaxy<T>(
	CosmosClient client,
	string database,
	string container,
	IReadOnlyList<string> partitionKey,
	bool recordQueries = false) : GalaxyBasic<T>(client, database, container, partitionKey, recordQueries), IGalaxy<T> where T : class, ICosmicEntity
{
	async Task<(Gravity, T)> IGalaxy<T>.Get(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns)
		=> await InternalGet<T>(clusters, columns);

	async Task<(Gravity g, S S)> IGalaxy<T>.Get<S>(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns)
		=> await InternalGet<S>(clusters, columns);

	private async Task<(Gravity g, ArgType S)> InternalGet<ArgType>(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns) where ArgType : ICosmicEntity
	{
		try
		{
			QueryDefinition query = QBuilder.CreateQuery(clusters, columnOptions: columns is null || !columns.Any() ? null : new(columns));
			return await QBuilder.GetOneFromQuery<ArgType>(_container, query);
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

	async Task<(Gravity, IList<T>)> IGalaxy<T>.List(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group)
		=> await InternalList<T>(clusters, columnOptions, sorting, group);

	async Task<(Gravity g, IList<S> T)> IGalaxy<T>.List<S>(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group)
		=> await InternalList<S>(clusters, columnOptions, sorting, group);

	private async Task<(Gravity g, IList<ArgType> T)> InternalList<ArgType>(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group) where ArgType : ICosmicEntity
	{
		try
		{
			QueryDefinition query = QBuilder.CreateQuery(clusters: clusters, columnOptions: columnOptions, sorting: sorting, groups: group);
			return await QBuilder.GetListFromQuery<ArgType>(_container, query);
		}
		catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
		{
			throw;
		}
	}

	async Task<(Gravity, IList<T>)> IGalaxy<T>.Paged(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group)
		=> await InternalPaged<T>(page, clusters, columnOptions, sorting, group);

	async Task<(Gravity g, IList<S> T)> IGalaxy<T>.Paged<S>(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group)
		=> await InternalPaged<S>(page, clusters, columnOptions, sorting, group);

	private async Task<(Gravity g, IList<ArgType> T)> InternalPaged<ArgType>(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> group) where ArgType : ICosmicEntity
	{
		try
		{
			QueryDefinition query = QBuilder.CreateQuery(clusters: clusters, columnOptions: columnOptions, sorting: sorting, groups: group);

			double requestUnit = 0;
			string continuationToken = string.Empty;
			List<ArgType> collection = [];
			using FeedIterator<ArgType> queryResponse = _container.GetItemQueryIterator<ArgType>(query,
				requestOptions: new() { MaxItemCount = page.Size },
				continuationToken: string.IsNullOrWhiteSpace(page.ContinuationToken) ? null : page.ContinuationToken
			);
			while (queryResponse.HasMoreResults)
			{
				FeedResponse<ArgType> next = await queryResponse.ReadNextAsync();
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