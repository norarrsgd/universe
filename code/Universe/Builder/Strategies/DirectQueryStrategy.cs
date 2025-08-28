using Universe.Response;

namespace Universe.Builder.Strategies;

/// <summary>
/// Direct execution strategy for simple queries
/// </summary>
internal sealed class DirectQueryStrategy : IQueryExecutionStrategy
{
	public string Name => QueryExecutionStrategy.Direct.ToStrategyName();
	
	public int Priority => 100; // High priority for simple queries

	bool IQueryExecutionStrategy.CanHandle(QueryDefinition query, QueryContext context)
	{
		// Handle simple queries without complex operations
		string queryText = query.QueryText.ToUpperInvariant();

		return context.Type is QueryType.Simple ||
		       (!queryText.Contains("RRF") &&
		        !queryText.Contains(Q.Operator.VectorDistance.Value().ToUpperInvariant()) &&
		        !queryText.Contains(Q.Operator.FTScore.Value().ToUpperInvariant()) &&
		        !queryText.Contains("GROUP BY"));
	}

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
			EnableOptimisticDirectExecution = true,
			MaxConcurrency = -1 // Unlimited parallelism
		};

		// Apply hints if available
		if (context.Hints is not null)
		{
			if (context.Hints.TryGetValue(nameof(QueryHints.MaxBufferedItemCount), out object bufferedCount))
				requestOptions.MaxBufferedItemCount = (int)bufferedCount;

			if (context.Hints.TryGetValue(nameof(QueryHints.EnableOptimisticDirectExecution), out object optimistic))
				requestOptions.EnableOptimisticDirectExecution = (bool)optimistic;
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