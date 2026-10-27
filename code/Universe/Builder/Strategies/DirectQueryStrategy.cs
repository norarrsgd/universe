using System.Diagnostics;
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
        => !context.HasRankOperations && !context.HasGrouping &&
           context.Type is not QueryType.VectorSearch and not QueryType.HybridSearch;

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
                EnableOptimisticDirectExecution = true,
                MaxConcurrency = Environment.ProcessorCount
            };

            // Apply hints if available
            if (context.Hints is not null)
            {
                if (context.Hints.TryGetValue(nameof(QueryHints.MaxBufferedItemCount), out object bufferedCount))
                    requestOptions.MaxBufferedItemCount = bufferedCount.ToInt();

                if (context.Hints.TryGetValue(nameof(QueryHints.MaxConcurrency), out object concurrency))
                    requestOptions.MaxConcurrency = concurrency.ToInt();

                if (context.Hints.TryGetValue(nameof(QueryHints.EnableOptimisticDirectExecution), out object optimistic))
                    requestOptions.EnableOptimisticDirectExecution = optimistic.ToBool();

                if (context.Hints.TryGetValue(nameof(QueryHints.ResponseContinuationTokenLimitInKb), out object tokenLimit))
                    requestOptions.ResponseContinuationTokenLimitInKb = tokenLimit.ToInt();
            }

            if (context.SingleResult)
            {
                requestOptions.MaxItemCount = 1;
                requestOptions.MaxBufferedItemCount = 1;
            }

            using FeedIterator<T> queryResponse = container.GetItemQueryIterator<T>(query, requestOptions: requestOptions);

            while (queryResponse.HasMoreResults)
            {
                FeedResponse<T> next = await queryResponse.ReadNextAsync();
                collection.AddRange(context.SingleResult ? next.Take(1) : next);
                requestCharge += next.RequestCharge;
                if (context.SingleResult && collection.Count > 0)
                    break;
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
