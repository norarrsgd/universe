using Microsoft.Azure.Cosmos;
using Universe.Builder;
using Universe.Builder.Options;
using Universe.Builder.Strategies;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class QueryTypeDetectorTests
{
    [Theory]
    [InlineData("VectorDistance")]
    [InlineData("FullTextScore")]
    [InlineData("RRF")]
    [InlineData("JOIN")]
    [InlineData("GROUP BY")]
    [InlineData("COUNT(")]
    [InlineData("AVG(")]
    [InlineData("VectorDistance(")]
    public void OperatorNamedPropertiesAndParameters_DoNotChangeQueryType(string name)
    {
        QueryContext context = Classify([new([new(name, "VectorDistance FullTextScore RRF JOIN GROUP BY")])], new([name]), expectedStrategy: "Direct");

        Assert.Equal(QueryType.Simple, context.Type);
        Assert.False(context.HasRankOperations);
        Assert.False(context.HasGrouping);
    }

    [Theory]
    [InlineData(Q.Aggregate.Count)]
    [InlineData(Q.Aggregate.Sum)]
    [InlineData(Q.Aggregate.Min)]
    [InlineData(Q.Aggregate.Max)]
    [InlineData(Q.Aggregate.Avg)]
    public void AggregateOptions_SelectAggregationAndGateway(Q.Aggregate aggregate)
    {
        QueryContext context = Classify(null, new(["category"], Aggregates: [new("price", aggregate)]), expectedStrategy: "Gateway");

        Assert.Equal(QueryType.Aggregation, context.Type);
        Assert.True(context.HasGrouping);
    }

    [Theory]
    [InlineData(Q.Operator.FTContains)]
    [InlineData(Q.Operator.NotFTContains)]
    [InlineData(Q.Operator.FTContainsAll)]
    [InlineData(Q.Operator.NotFTContainsAll)]
    [InlineData(Q.Operator.FTContainsAny)]
    [InlineData(Q.Operator.NotFTContainsAny)]
    public void FullTextPredicates_SelectFullTextAndDirect(Q.Operator op)
    {
        object value = op is Q.Operator.FTContains or Q.Operator.NotFTContains ? "term" : new[] { "term" };
        QueryContext context = Classify([new([new("description", value, Operator: op)])], expectedStrategy: "Direct");

        Assert.Equal(QueryType.FullTextSearch, context.Type);
        Assert.False(context.HasRankOperations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VectorOperations_SelectVectorOrHybrid(bool fullText)
    {
        List<Catalyst> catalysts = [new("embedding", new float[] { 0.1f, 0.2f }, Operator: Q.Operator.VectorDistance)];
        if (fullText)
            catalysts.Add(new("description", "term", Operator: Q.Operator.FTContains));
        QueryContext context = Classify([new(catalysts)], new(["name"], Top: 5), expectedStrategy: "VectorSearch");

        Assert.Equal(fullText ? QueryType.HybridSearch : QueryType.VectorSearch, context.Type);
        Assert.True(context.HasRankOperations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullTextRanking_SelectsVectorStrategy(bool multipleRankers)
    {
        List<Catalyst> catalysts = [new("description", new[] { "term" }, Operator: Q.Operator.FTScore)];
        if (multipleRankers)
            catalysts.Add(new("title", new[] { "term" }, Operator: Q.Operator.FTScore));
        QueryContext context = Classify([new(catalysts)], new(null, Top: 5), expectedStrategy: "VectorSearch");

        Assert.Equal(QueryType.FullTextSearch, context.Type);
        Assert.True(context.HasRankOperations);
    }

    [Fact]
    public void GroupingWithoutAggregates_SelectsGateway()
    {
        QueryContext context = Classify(null, groups: ["category"], expectedStrategy: "Gateway");

        Assert.Equal(QueryType.Aggregation, context.Type);
        Assert.True(context.HasGrouping);
    }

    [Fact]
    public void JoinOptions_SelectJoinAndDirect()
    {
        QueryContext context = Classify(null, new(null, Join: new("item", "items", null)), expectedStrategy: "Direct");

        Assert.Equal(QueryType.Join, context.Type);
    }

    [Fact]
    public void LongScalarQuery_PreservesComplexClassification()
    {
        QueryContext context = Classify([new(Enumerable.Range(0, 8).Select(i => new Catalyst($"field{i}", i)).ToArray())], expectedStrategy: "Direct");

        Assert.Equal(QueryType.Complex, context.Type);
    }

    private static QueryContext Classify(IReadOnlyList<Cluster> clusters, ColumnOptions? options = null, IReadOnlyList<string> groups = null, string expectedStrategy = null)
    {
        using UniverseBuilder builder = new();
        using QueryTuner tuner = new();
        QueryDefinition query = builder.CreateQuery(clusters, out QueryContext context, options, groups: groups);
        QueryStrategySelector selector = new(tuner);
        Assert.Equal(expectedStrategy, selector.SelectStrategy(query, context).Name);
        return context;
    }
}
