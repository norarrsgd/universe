using Universe.Builder.Strategies;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class QueryTypeDetectorTests
{
    [Theory]
    [InlineData("SELECT MIN(c[\"price\"]) AS price_Min FROM c")]
    [InlineData("SELECT MAX(c[\"price\"]) AS price_Max FROM c")]
    [InlineData("SELECT AVG(c[\"price\"]) AS price_Avg FROM c")]
    public void Infer_AggregateFunction_ReturnsAggregation(string queryText)
    {
        QueryType queryType = QueryTypeDetector.Infer(queryText);

        Assert.Equal(QueryType.Aggregation, queryType);
    }

    [Theory]
    [InlineData("SELECT * FROM c WHERE FullTextContains(c[\"description\"], @description)")]
    [InlineData("SELECT * FROM c WHERE NOT FullTextContains(c[\"description\"], @description)")]
    [InlineData("SELECT * FROM c WHERE FullTextContainsAll(c[\"description\"], @description)")]
    [InlineData("SELECT * FROM c WHERE NOT FullTextContainsAll(c[\"description\"], @description)")]
    [InlineData("SELECT * FROM c WHERE FullTextContainsAny(c[\"description\"], @description)")]
    [InlineData("SELECT * FROM c WHERE NOT FullTextContainsAny(c[\"description\"], @description)")]
    public void Infer_FullTextContainsOperator_ReturnsFullTextSearch(string queryText)
    {
        QueryType queryType = QueryTypeDetector.Infer(queryText);

        Assert.Equal(QueryType.FullTextSearch, queryType);
    }

    [Fact]
    public void Infer_VectorDistanceWithFullTextContains_ReturnsHybridSearch()
    {
        const string queryText = "SELECT TOP 5 c[\"name\"], VectorDistance(c[\"embedding\"], @embedding) AS embeddingScore FROM c WHERE FullTextContains(c[\"description\"], @description) ORDER BY VectorDistance(c[\"embedding\"], @embedding)";

        QueryType queryType = QueryTypeDetector.Infer(queryText);

        Assert.Equal(QueryType.HybridSearch, queryType);
    }
}
