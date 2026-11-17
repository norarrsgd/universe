using Microsoft.Azure.Cosmos;
using Universe.Builder;
using Universe.Builder.Options;
using Universe.Exception;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class QuerySecurityBudgetTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void ExplicitBlankAlias_IsRejected(string alias)
    {
        using UniverseBuilder builder = new();
        Assert.Throws<UniverseException>(() => builder.CreateQuery([new([new("tenant", "restricted", Alias: alias)])]));
    }

    [Theory]
    [InlineData(" . ")]
    [InlineData("a..b")]
    [InlineData("a. .b")]
    [InlineData(".a")]
    [InlineData("a.")]
    public void EmptyPathSegment_IsRejectedOnEverySurface(string path)
    {
        using UniverseBuilder builder = new();
        Assert.Throws<UniverseException>(() => builder.CreateQuery([new([new(path, 1)])]));
        Assert.Throws<UniverseException>(() => builder.CreateQuery(null, new([path])));
        Assert.Throws<UniverseException>(() => builder.CreateQuery(null, sorting: [new(path)]));
        Assert.Throws<UniverseException>(() => builder.CreateQuery(null, groups: [path]));
        Assert.Throws<UniverseException>(() => builder.CreateQuery(null, new(null, Join: new("j", path, null))));
        Assert.Throws<UniverseException>(() => builder.CreateQuery(null, new ColumnOptions(null) { Projection = new ProjectionBuilder().Field("result", path).Build() }));
        Assert.Throws<UniverseException>(() => builder.CreateQuery(null, new(["id"], Aggregates: [new(path, Q.Aggregate.Sum)])));
        Assert.Throws<UniverseException>(() => builder.CreateQuery(null, new(null, Join: new("j", "items", [path]))));
        Assert.Throws<UniverseException>(() => builder.CreateQuery([new([Catalyst.Compare(QueryOperand.Field("id"), Q.Operator.Eq, QueryOperand.ArrayLength(path))])]));
    }

    [Fact]
    public void NullAlias_UsesDefaultRoot()
    {
        using UniverseBuilder builder = new();
        Assert.Contains("c[\"tenant\"] = @", builder.CreateQuery([new([new("tenant", "restricted", Alias: null)])]).QueryText);
    }

    [Fact]
    public void RepeatedRankCatalyst_PreservesEverySeparator()
    {
        using UniverseBuilder builder = new();
        Catalyst catalyst = new("content", new[] { "term" }, Operator: Q.Operator.FTScore);
        QueryDefinition query = builder.CreateQuery([new([catalyst, catalyst])], new(null, Top: 5));
        Assert.Contains(", FullTextScore(", query.QueryText);
        Assert.Single(query.GetQueryParameters());
    }

    [Fact]
    public void ExcessiveCatalysts_AreRejectedBeforeEnumeration()
    {
        using UniverseBuilder builder = new();
        Catalyst[] catalysts = Enumerable.Repeat(new Catalyst("value", 1), 1025).ToArray();
        Assert.Throws<UniverseException>(() => builder.CreateQuery([new(catalysts)]));
    }

    [Fact]
    public void ExcessiveRankExpressions_AreRejected()
    {
        using UniverseBuilder builder = new();
        Catalyst[] catalysts = Enumerable.Repeat(new Catalyst("content", new[] { "term" }, Operator: Q.Operator.FTScore), 65).ToArray();
        Assert.Throws<UniverseException>(() => builder.CreateQuery([new(catalysts)], new(null, Top: 5)));
    }

    [Theory]
    [InlineData(65, 1)]
    [InlineData(1, 4097)]
    public void ExcessiveFullTextTerms_AreRejected(int count, int size)
    {
        using UniverseBuilder builder = new();
        string[] terms = Enumerable.Repeat(new string('x', size), count).ToArray();
        Assert.Throws<UniverseException>(() => builder.CreateQuery([new([new("content", terms, Operator: Q.Operator.FTScore)])], new(null, Top: 5)));
    }

    [Fact]
    public void OversizedTotalFullTextParameterBytes_AreRejected()
    {
        using UniverseBuilder builder = new();
        string[] terms = Enumerable.Repeat(new string('x', 4096), 64).ToArray();
        Catalyst[] catalysts = Enumerable.Range(0, 5).Select(index => new Catalyst($"content{index}", terms, Operator: Q.Operator.FTScore)).ToArray();
        Assert.Throws<UniverseException>(() => builder.CreateQuery([new(catalysts)], new(null, Top: 5)));
    }

    [Fact]
    public void OversizedGeneratedQuery_IsRejected()
    {
        using UniverseBuilder builder = new();
        Catalyst[] catalysts = Enumerable.Range(0, 1024).Select(index => new Catalyst(new string('x', 250), index)).ToArray();
        Assert.Throws<UniverseException>(() => builder.CreateQuery([new(catalysts)]));
    }

    [Fact]
    public void FullTextTermLimit_UsesUtf8Bytes()
    {
        using UniverseBuilder builder = new();
        Assert.Throws<UniverseException>(() => builder.CreateQuery([new([new("content", new[] { new string('\u20ac', 1366) }, Operator: Q.Operator.FTScore)])], new(null, Top: 5)));
    }

    [Fact]
    public void FullTextBudgets_AcceptBoundaryCountsAndTermBytes()
    {
        using UniverseBuilder builder = new();
        QueryDefinition terms = builder.CreateQuery([new([new("content", Enumerable.Repeat("term", 64).ToArray(), Operator: Q.Operator.FTScore)])], new(null, Top: 5));
        QueryDefinition bytes = builder.CreateQuery([new([new("content", new[] { new string('x', 4096) }, Operator: Q.Operator.FTScore)])], new(null, Top: 5));
        Assert.Equal(64, terms.GetQueryParameters().Count);
        Assert.Equal(4096, ((string)Assert.Single(bytes.GetQueryParameters()).Value).Length);
    }

    [Fact]
    public void OrdinaryParameters_AreNotSerializedDuringQueryGeneration()
    {
        using UniverseBuilder builder = new();
        object value = new UnserializableValue();
        QueryDefinition query = builder.CreateQuery([new([new("value", value)])]);
        Assert.Same(value, Assert.Single(query.GetQueryParameters()).Value);
    }

    private sealed class UnserializableValue
    {
        public string Value => throw new InvalidOperationException("Must not serialize during query generation.");
    }
}
