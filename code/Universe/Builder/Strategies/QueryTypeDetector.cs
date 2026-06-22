namespace Universe.Builder.Strategies;

internal static class QueryTypeDetector
{
    public static QueryType Infer(QueryDefinition query)
        => Infer(query.QueryText);

    public static QueryType Infer(string queryText)
    {
        string normalized = queryText.ToUpperInvariant();

        bool hasVectorSearch = normalized.Contains(Q.Operator.VectorDistance.Value().ToUpperInvariant());
        bool hasFullTextSearch = ContainsFullTextOperator(normalized);

        if (hasVectorSearch && hasFullTextSearch)
            return QueryType.HybridSearch;
        if (hasVectorSearch)
            return QueryType.VectorSearch;
        if (hasFullTextSearch)
            return QueryType.FullTextSearch;
        if (ContainsAggregateOperator(normalized))
            return QueryType.Aggregation;
        if (normalized.Contains("JOIN"))
            return QueryType.Join;
        if (normalized.Contains("RRF") || normalized.Split(' ').Length > 20)
            return QueryType.Complex;

        return QueryType.Simple;
    }

    private static bool ContainsFullTextOperator(string queryText)
        => queryText.Contains(Q.Operator.FTScore.Value().ToUpperInvariant())
           || queryText.Contains(Q.Operator.FTContains.Value().ToUpperInvariant())
           || queryText.Contains(Q.Operator.FTContainsAll.Value().ToUpperInvariant())
           || queryText.Contains(Q.Operator.FTContainsAny.Value().ToUpperInvariant());

    private static bool ContainsAggregateOperator(string queryText)
        => queryText.Contains("GROUP BY")
           || queryText.Contains("COUNT(")
           || queryText.Contains("SUM(")
           || queryText.Contains("MIN(")
           || queryText.Contains("MAX(")
           || queryText.Contains("AVG(");
}
