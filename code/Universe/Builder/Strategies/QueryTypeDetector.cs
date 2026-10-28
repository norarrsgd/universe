namespace Universe.Builder.Strategies;

internal static class QueryTypeDetector
{
    internal static QueryContext CreateContext(
        IReadOnlyList<Cluster> clusters,
        ColumnOptions? columnOptions,
        IReadOnlyList<string> groups,
        string queryText)
    {
        bool hasVectorSearch = false;
        bool hasFullTextSearch = false;
        bool hasRankOperations = false;
        foreach (Cluster cluster in clusters ?? [])
        {
            foreach (Catalyst catalyst in cluster.Catalysts)
            {
                hasVectorSearch |= catalyst.Operator is Q.Operator.VectorDistance;
                hasRankOperations |= catalyst.Operator is Q.Operator.VectorDistance or Q.Operator.FTScore;
                hasFullTextSearch |= catalyst.Operator is Q.Operator.FTScore
                    or Q.Operator.FTContains or Q.Operator.NotFTContains
                    or Q.Operator.FTContainsAll or Q.Operator.NotFTContainsAll
                    or Q.Operator.FTContainsAny or Q.Operator.NotFTContainsAny;
            }
        }

        bool hasGrouping = groups is { Count: > 0 };
        bool hasAggregates = columnOptions?.Aggregates is { Count: > 0 }
            || columnOptions?.Join?.Aggregates is { Count: > 0 };
        QueryType type = (hasVectorSearch, hasFullTextSearch) switch
        {
            (true, true) => QueryType.HybridSearch,
            (true, false) => QueryType.VectorSearch,
            (false, true) => QueryType.FullTextSearch,
            _ when hasGrouping || hasAggregates => QueryType.Aggregation,
            _ when columnOptions?.Join is not null => QueryType.Join,
            _ when queryText.Split(' ').Length > 20 => QueryType.Complex,
            _ => QueryType.Simple
        };

        return new(type, HasRankOperations: hasRankOperations, HasGrouping: hasGrouping);
    }
}
