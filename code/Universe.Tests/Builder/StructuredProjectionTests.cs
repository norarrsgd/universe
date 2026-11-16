using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Universe.Builder;
using Universe.Builder.Options;
using Universe.Builder.Strategies;
using Universe.Exception;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class StructuredProjectionTests
{
    [Fact]
    public void NestedObjects_CombineWithColumnsAndPreserveOutputNames()
    {
        using UniverseBuilder builder = new(false, JsonNamingPolicy.CamelCase);
        ProjectionBuilder projection = new();
        QueryProjection snapshot = projection.Object("data", p => p.Field("OwnerName", "Data.Owner").Object("nested", n => n.Field("quote\"key", "Data.TicketId"))).Build();
        projection.Field("later", "extra");
        QueryDefinition query = builder.CreateQuery(null, new(["Id"]) { Projection = snapshot });
        Assert.Equal("SELECT c[\"id\"], {\"OwnerName\": c[\"data\"][\"owner\"], \"nested\": {\"quote\\u0022key\": c[\"data\"][\"ticketId\"]}} AS data FROM c", query.QueryText);
        Assert.DoesNotContain("later", query.QueryText);
    }

    [Theory]
    [InlineData(Q.Aggregate.Sum, "SUM(c[\"price\"])")]
    [InlineData(Q.Aggregate.Min, "MIN(c[\"price\"])")]
    [InlineData(Q.Aggregate.Max, "MAX(c[\"price\"])")]
    [InlineData(Q.Aggregate.Avg, "AVG(c[\"price\"])")]
    [InlineData(Q.Aggregate.Count, "COUNT(1)")]
    public void NestedAggregates_GroupScalarLeavesAndClassify(Q.Aggregate aggregate, string sql)
    {
        ProjectionBuilder projection = new();
        projection.Object("data", p => p.Field("category", "category").Aggregate("total", "price", aggregate));
        using UniverseBuilder builder = new();
        QueryDefinition query = builder.CreateQuery(null, out QueryContext context, new(null) { Projection = projection.Build() });
        Assert.Contains($"\"total\": {sql}", query.QueryText);
        Assert.EndsWith("GROUP BY c[\"category\"]", query.QueryText);
        Assert.Equal(QueryType.Aggregation, context.Type);
        Assert.True(context.HasGrouping);
    }

    [Fact]
    public void GlobalAggregate_RequiresNoArtificialGrouping()
    {
        using UniverseBuilder builder = new();
        QueryDefinition query = builder.CreateQuery(null, out QueryContext context, new(null) { Projection = new ProjectionBuilder().Aggregate("total", "id", Q.Aggregate.Count).Build() });
        Assert.Equal("SELECT COUNT(1) AS total FROM c", query.QueryText);
        Assert.Equal(QueryType.Aggregation, context.Type);
        Assert.False(context.HasGrouping);
    }

    [Fact]
    public void JoinsDistinctAndLegacyAggregates_ComposeWithProjection()
    {
        using UniverseBuilder builder = new();
        ColumnOptions options = new(["category"], IsDistinct: true, Top: 5, Aggregates: [new("price", Q.Aggregate.Sum)], Join: new("item", "items", ["kind"]))
        { Projection = new ProjectionBuilder().Object("data", p => p.Field("name", "name", "item")).Build() };
        string sql = builder.CreateQuery(null, options).QueryText;
        Assert.StartsWith("SELECT DISTINCT TOP 5 ", sql);
        Assert.Contains("SUM(c[\"price\"]) AS price_Sum", sql);
        Assert.Contains("JOIN item IN c[\"items\"]", sql);
        Assert.Contains("\"name\": item[\"name\"]", sql);
        Assert.Contains("GROUP BY c[\"category\"], item[\"kind\"], item[\"name\"]", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Projection_ComposesWithVectorAndFullTextRanking(bool fullText)
    {
        using UniverseBuilder builder = new();
        List<Catalyst> catalysts = [new("embedding", new float[] { 0.1f }, Operator: Q.Operator.VectorDistance)];
        if (fullText)
            catalysts.Add(new("text", new[] { "term" }, Operator: Q.Operator.FTScore));
        QueryDefinition query = builder.CreateQuery([new(catalysts)], out QueryContext context, new(null, Top: 5) { Projection = new ProjectionBuilder().Object("data", p => p.Field("id", "id")).Build() });
        Assert.Contains("AS embeddingScore", query.QueryText);
        Assert.Contains(fullText ? "ORDER BY RANK RRF(" : "ORDER BY VectorDistance(", query.QueryText);
        Assert.DoesNotContain("FullTextScore", query.QueryText.Split(" FROM ")[0]);
        Assert.True(context.HasRankOperations);
    }

    [Fact]
    public void InvalidProjectionNamesAndUngroupedFields_AreRejected()
    {
        using UniverseBuilder builder = new();
        QueryProjection[] invalid =
        [
            new ProjectionBuilder().Build(),
            new ProjectionBuilder().Field("bad;alias", "safe").Build(),
            new ProjectionBuilder().Field("value", "id").Build(),
            new ProjectionBuilder().Field("VALUE", "id").Build(),
            new ProjectionBuilder().Field("data", "bad\"];--").Build(),
            new ProjectionBuilder().Object("data", p => p.Field("same", "a").Field("same", "b")).Build(),
            new ProjectionBuilder().Field("id", "other").Build()
        ];
        foreach (QueryProjection projection in invalid)
            Assert.Throws<UniverseException>(() => builder.CreateQuery(null, new(["id"]) { Projection = projection }));
        Assert.Throws<UniverseException>(() => builder.CreateQuery(null, new(null) { Projection = new ProjectionBuilder().Field("a", "a").Build() }, groups: ["b"]));
    }

    [Fact]
    public void FluentProjection_IsAdditiveAndEquivalentToDeclarative()
    {
        Orbit<Entity> orbit = new(null);
        orbit.Select("id").Project(p => p.Object("data", d => d.Field("name", "name"))).Project(p => p.Field("other", "other"));
        (_, ColumnOptions? options, _, _) = orbit.Build();
        using UniverseBuilder builder = new();
        string sql = builder.CreateQuery(null, options).QueryText;
        Assert.Contains("c[\"id\"], {\"name\": c[\"name\"]} AS data, c[\"other\"] AS other", sql);
    }

    private sealed record Entity : Universe.Interfaces.CosmicEntity;

    [Fact]
    public void CollisionDetection_UsesRenderedNamesAndLegacyCountNamingPolicy()
    {
        using UniverseBuilder builder = new(false, JsonNamingPolicy.CamelCase);
        Assert.Throws<UniverseException>(() => builder.CreateQuery(null, new(["id. "]) { Projection = new ProjectionBuilder().Field("id", "other").Build() }));
        ColumnOptions options = new(["category"], Aggregates: [new("id", Q.Aggregate.Count)])
        { Projection = new ProjectionBuilder().Object("data", p => p.Field("category", "category")).Build() };
        Assert.Contains("COUNT(1) AS countAggregate", builder.CreateQuery(null, options).QueryText);
        Assert.Throws<UniverseException>(() => builder.CreateQuery(null, options with { Projection = new ProjectionBuilder().Field("countAggregate", "other").Build() }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupedProjection_RejectsUnsupportedOrdering(bool ranked)
    {
        using UniverseBuilder builder = new();
        ColumnOptions options = new(null, Top: 5)
        { Projection = new ProjectionBuilder().Field("category", "category").Aggregate("total", "price", Q.Aggregate.Sum).Build() };
        IReadOnlyList<Cluster> clusters = ranked ? [new([new("text", new[] { "term" }, Operator: Q.Operator.FTScore)])] : null;
        IReadOnlyList<Sorting.Option> sorting = ranked ? null : [new("category")];
        Assert.Throws<UniverseException>(() => builder.CreateQuery(clusters, options, sorting));
    }
}
