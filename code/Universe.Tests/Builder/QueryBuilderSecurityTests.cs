using Universe.Builder;
using Universe.Builder.Options;
using Universe.Exception;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class QueryBuilderSecurityTests : IDisposable
{
    private readonly UniverseBuilder _builder = new(recordQueries: true);

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AggregateAlias_UsesSanitizedPathAlias()
    {
        ColumnOptions colOpts = new(
            Names: ["category"],
            Aggregates: [new("metadata.price", Q.Aggregate.Sum)]);

        string sql = _builder.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("SUM(c[\"metadata\"][\"price\"]) AS metadata_price_Sum", sql);
        Assert.DoesNotContain("AS metadata.price_Sum", sql);
    }

    [Fact]
    public void VectorDistanceScoreAlias_UsesSanitizedPathAlias()
    {
        float[] vector = [0.1f, 0.2f, 0.3f];
        List<Cluster> clusters = [new([new("metadata.embedding", vector, Operator: Q.Operator.VectorDistance)])];
        ColumnOptions colOpts = new(Names: ["name"], Top: 5);

        string sql = _builder.CreateQuery(clusters, colOpts).QueryText;

        Assert.Contains("VectorDistance(c[\"metadata\"][\"embedding\"]", sql);
        Assert.Contains("AS metadata_embeddingScore", sql);
        Assert.DoesNotContain("AS metadata.embeddingScore", sql);
    }

    [Fact]
    public void GeneratedAlias_ReservedKeyword_AppendsSafeSuffix()
    {
        ColumnOptions colOpts = new(
            Names: ["category"],
            Aggregates: [new("order", Q.Aggregate.Sum)]);

        string sql = _builder.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("SUM(c[\"order\"]) AS order_alias_Sum", sql);
        Assert.DoesNotContain(" AS order_Sum", sql);
    }

    [Fact]
    public void VectorDistanceScoreAlias_ShortCatalystId_UsesAvailableSuffix()
    {
        float[] vector = [0.1f, 0.2f, 0.3f];
        List<Cluster> clusters =
        [
            new(
            [
                new("embeddingA", vector, Operator: Q.Operator.VectorDistance),
                new("embeddingB", vector, Operator: Q.Operator.VectorDistance) { CatalystId = "a-b$c" }
            ])
        ];
        ColumnOptions colOpts = new(Names: ["name"], Top: 5);

        string sql = _builder.CreateQuery(clusters, colOpts).QueryText;

        Assert.Contains("AS embeddingBScorea_b_c", sql);
    }

    [Theory]
    [InlineData("line item")]
    [InlineData("line-item")]
    [InlineData("SELECT")]
    public void JoinAlias_InvalidRawAlias_Throws(string alias)
    {
        ColumnOptions colOpts = new(
            Names: ["name"],
            Join: new(alias, "items", ["sku"]));

        Assert.Throws<UniverseException>(() => _builder.CreateQuery(null, colOpts));
    }

    [Theory]
    [InlineData("line item")]
    [InlineData("line-item")]
    [InlineData("JOIN")]
    public void CatalystAlias_InvalidRawAlias_Throws(string alias)
    {
        List<Cluster> clusters = [new([new("sku", "abc", Alias: alias)])];

        Assert.Throws<UniverseException>(() => _builder.CreateQuery(clusters));
    }

    [Fact]
    public void SafeJoinAndCatalystAliases_GenerateExpectedSql()
    {
        ColumnOptions colOpts = new(
            Names: ["name"],
            Join: new("lineItem", "items", ["sku"]));
        List<Cluster> clusters = [new([new("sku", "abc", Alias: "lineItem")])];

        string sql = _builder.CreateQuery(clusters, colOpts).QueryText;

        Assert.Contains("JOIN lineItem IN c[\"items\"]", sql);
        Assert.Contains("lineItem[\"sku\"]", sql);
    }
}
