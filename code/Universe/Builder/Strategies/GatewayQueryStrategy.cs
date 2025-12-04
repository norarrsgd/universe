using System.Diagnostics;
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
		bool recordQueries,
		QueryTuner queryTuner)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		string queryHash = queryTuner != null ? QueryTuner.ComputeQueryHash(context, query.QueryText) : string.Empty;

		double requestCharge = 0;
		List<T> collection = [];

		try
		{
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
				if (context.Hints.TryGetValue(nameof(QueryHints.MaxBufferedItemCount), out object bufferedCount))
					requestOptions.MaxBufferedItemCount = bufferedCount.ToInt();

				if (context.Hints.TryGetValue(nameof(QueryHints.MaxConcurrency), out object concurrency))
					requestOptions.MaxConcurrency = concurrency.ToInt();

				if (context.Hints.TryGetValue(nameof(QueryHints.ResponseContinuationTokenLimitInKb), out object tokenLimit))
					requestOptions.ResponseContinuationTokenLimitInKb = tokenLimit.ToInt();
			}

			using FeedIterator<T> queryResponse = container.GetItemQueryIterator<T>(query, requestOptions: requestOptions);

			while (queryResponse.HasMoreResults)
			{
				FeedResponse<T> next = await queryResponse.ReadNextAsync();
				collection.AddRange(next);
				requestCharge += next.RequestCharge;
			}

			stopwatch.Stop();

			// Record successful execution
			queryTuner?.RecordExecution(new QueryExecutionStatistics
			{
				QueryHash = queryHash,
				Type = context.Type,
				RU = requestCharge,
				ExecutionTime = stopwatch.Elapsed,
				ResultCount = collection.Count,
				Success = true,
				Timestamp = DateTime.UtcNow,
				StrategyUsed = Name,
				HintsUsed = context.Hints
			});

			return (new(
				RU: requestCharge,
				ContinuationToken: null,
				Query: recordQueries ? (query.QueryText, query.GetQueryParameters()) : default
			), collection);
		}
		catch (SystemException)
		{
			stopwatch.Stop();

			// Record failed execution
			queryTuner?.RecordExecution(new QueryExecutionStatistics
			{
				QueryHash = queryHash,
				Type = context.Type,
				RU = requestCharge,
				ExecutionTime = stopwatch.Elapsed,
				ResultCount = 0,
				Success = false,
				Timestamp = DateTime.UtcNow,
				StrategyUsed = Name,
				HintsUsed = context.Hints
			});

			throw;
		}
	}
}