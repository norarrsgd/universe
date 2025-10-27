using Universe.Response;

namespace Universe.Builder.Strategies;

/// <summary>
/// Specialized strategy for vector search queries
/// </summary>
internal sealed class VectorSearchStrategy : IQueryExecutionStrategy
{
	public string Name => QueryExecutionStrategy.VectorSearch.ToStrategyName();

	public int Priority => 200; // High priority for vector queries

	bool IQueryExecutionStrategy.CanHandle(QueryDefinition query, QueryContext context)
	{
		string queryText = query.QueryText.ToUpperInvariant();

		return context.Type is QueryType.VectorSearch or QueryType.HybridSearch ||
		       queryText.Contains(Q.Operator.VectorDistance.Value().ToUpperInvariant()) ||
		       queryText.Contains(Q.Operator.FTScore.Value().ToUpperInvariant());
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
			EnableOptimisticDirectExecution = false,
			MaxConcurrency = Environment.ProcessorCount // CPU-bound operations
		};

		// Vector search specific optimizations
		if (context.Hints is not null)
		{
			if (context.Hints.TryGetValue(nameof(QueryHints.MaxBufferedItemCount), out object bufferedCount))
				requestOptions.MaxBufferedItemCount = (int)bufferedCount;
			
			if (context.Hints.TryGetValue(nameof(QueryHints.MaxItemCount), out object itemCount))
				requestOptions.MaxItemCount = (int)itemCount;

			if (context.Hints.TryGetValue(nameof(QueryHints.MaxConcurrency), out object concurrency))
				requestOptions.MaxConcurrency = (int)concurrency;

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