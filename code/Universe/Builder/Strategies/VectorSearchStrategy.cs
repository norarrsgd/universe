using Universe.Response;

namespace Universe.Builder.Strategies;

/// <summary>
/// Specialized strategy for vector search queries
/// </summary>
internal sealed class VectorSearchStrategy : IQueryExecutionStrategy
{
	public string Name => "VectorSearch";
	public int Priority => 200; // High priority for vector queries

	public bool CanHandle(QueryDefinition query, QueryContext context)
	{
		string queryText = query.QueryText.ToUpperInvariant();

		return context.Type is QueryType.VectorSearch or QueryType.HybridSearch ||
		       queryText.Contains(Q.Operator.VectorDistance.Value().ToUpperInvariant()) ||
		       queryText.Contains(Q.Operator.FTScore.Value().ToUpperInvariant());
	}

	public async Task<(Gravity gravity, IList<T> results)> ExecuteAsync<T>(
		Container container,
		QueryDefinition query,
		QueryContext context,
		bool recordQueries)
	{
		double requestCharge = 0;
		List<T> collection = [];

		QueryRequestOptions requestOptions = new()
		{
			MaxItemCount = context.MaxItemCount ?? 100, // Smaller batches for vector queries
			EnableOptimisticDirectExecution = true,
			MaxConcurrency = Environment.ProcessorCount, // CPU-bound operations
		};

		// Vector search specific optimizations
		if (context.Hints is not null)
		{
			if (context.Hints.TryGetValue("MaxItemCount", out object itemCount))
				requestOptions.MaxItemCount = (int)itemCount;

			if (context.Hints.TryGetValue("MaxConcurrency", out object concurrency))
				requestOptions.MaxConcurrency = (int)concurrency;
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