using System.Diagnostics;
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
                EnableOptimisticDirectExecution = false,
                MaxConcurrency = Environment.ProcessorCount // CPU-bound operations
            };

            // Vector search specific optimizations
            if (context.Hints is not null)
            {
                if (context.Hints.TryGetValue(nameof(QueryHints.MaxBufferedItemCount), out object bufferedCount))
                    requestOptions.MaxBufferedItemCount = bufferedCount.ToInt();

                if (context.Hints.TryGetValue(nameof(QueryHints.MaxItemCount), out object itemCount))
                    requestOptions.MaxItemCount = itemCount.ToInt();

                if (context.Hints.TryGetValue(nameof(QueryHints.MaxConcurrency), out object concurrency))
                    requestOptions.MaxConcurrency = concurrency.ToInt();

                if (context.Hints.TryGetValue(nameof(QueryHints.EnableOptimisticDirectExecution), out object optimistic))
                    requestOptions.EnableOptimisticDirectExecution = optimistic.ToBool();
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