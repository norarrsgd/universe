using Universe.Response;

namespace Universe.Builder.Strategies;

/// <summary>
/// Gateway mode strategy for complex queries and when direct mode fails
/// </summary>
internal sealed class GatewayQueryStrategy : IQueryExecutionStrategy
{
	public string Name => QueryExecutionStrategy.Gateway.ToStrategyName();
	
	public int Priority => 50; // Lower priority, fallback strategy

	// Can handle any query as fallback
	bool IQueryExecutionStrategy.CanHandle(QueryDefinition query, QueryContext context) => true;

	async Task<(Gravity gravity, IList<T> results)> IQueryExecutionStrategy.ExecuteAsync<T>(
		Container container,
		QueryDefinition query,
		QueryContext context,
		bool recordQueries)
	{
		double requestCharge = 0;
		List<T> collection = [];

		QueryRequestOptions requestOptions = new()
		{
			MaxItemCount = context.MaxItemCount ?? Q.Limits.MaxItems,
			EnableOptimisticDirectExecution = false,
			MaxConcurrency = 1, // Conservative for complex queries
			ResponseContinuationTokenLimitInKb = 1 // Smaller continuation tokens
		};

		// Apply query hints if available
		if (context.Hints is not null)
		{
			if (context.Hints.TryGetValue("MaxBufferedItemCount", out object bufferedCount))
				requestOptions.MaxBufferedItemCount = (int)bufferedCount;

			if (context.Hints.TryGetValue("MaxConcurrency", out object concurrency))
				requestOptions.MaxConcurrency = (int)concurrency;

			if (context.Hints.TryGetValue("ResponseContinuationTokenLimitInKb", out object tokenLimit))
				requestOptions.ResponseContinuationTokenLimitInKb = (int)tokenLimit;
		}

		using FeedIterator<T> queryResponse = container.GetItemQueryIterator<T>(query, requestOptions: requestOptions);

		while (queryResponse.HasMoreResults)
		{
			FeedResponse<T> next = await queryResponse.ReadNextAsync();
			collection.AddRange(next);
			requestCharge += next.RequestCharge;
		}

		return (new(
			RU: requestCharge,
			ContinuationToken: null,
			Query: recordQueries ? (query.QueryText, query.GetQueryParameters()) : default
		), collection);
	}
}