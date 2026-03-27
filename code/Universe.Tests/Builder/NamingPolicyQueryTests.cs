using System.Text.Json;
using Universe.Builder;
using Universe.Builder.Options;
using Xunit;

namespace Universe.Tests.Builder;

/// <summary>
/// Tests that the naming policy from UniverseSerializer is correctly applied
/// to all column name references in generated SQL queries.
/// </summary>
public sealed class NamingPolicyQueryTests : IDisposable
{
    private readonly UniverseBuilder _camel = new(recordQueries: true, namingPolicy: JsonNamingPolicy.CamelCase);
    private readonly UniverseBuilder _none = new(recordQueries: true);

    public void Dispose()
    {
        _camel.Dispose();
        _none.Dispose();
    }

    #region SELECT

    [Fact]
    public void Select_AppliesNamingPolicy()
    {
        ColumnOptions colOpts = new(Names: ["Name", "Price"]);
        string sql = _camel.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("c[\"name\"]", sql);
        Assert.Contains("c[\"price\"]", sql);
        Assert.DoesNotContain("c[\"Name\"]", sql);
        Assert.DoesNotContain("c[\"Price\"]", sql);
    }

    [Fact]
    public void Select_WithoutPolicy_PreservesNames()
    {
        ColumnOptions colOpts = new(Names: ["Name", "Price"]);
        string sql = _none.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("c[\"Name\"]", sql);
        Assert.Contains("c[\"Price\"]", sql);
    }

    #endregion

    #region WHERE

    [Fact]
    public void Where_AppliesNamingPolicy()
    {
        List<Cluster> clusters = [new([new("Category", "electronics")])];
        string sql = _camel.CreateQuery(clusters).QueryText;

        Assert.Contains("c[\"category\"]", sql);
        Assert.DoesNotContain("c[\"Category\"]", sql);
    }

    [Fact]
    public void Where_WithoutPolicy_PreservesNames()
    {
        List<Cluster> clusters = [new([new("Category", "electronics")])];
        string sql = _none.CreateQuery(clusters).QueryText;

        Assert.Contains("c[\"Category\"]", sql);
    }

    #endregion

    #region GROUP BY

    [Fact]
    public void GroupBy_WithoutAggregates_AppliesNamingPolicy()
    {
        string sql = _camel.CreateQuery(null, groups: ["Category", "Code"]).QueryText;

        Assert.Contains("GROUP BY c[\"category\"], c[\"code\"]", sql);
        Assert.DoesNotContain("GROUP BY Category", sql);
    }

    [Fact]
    public void GroupBy_WithoutAggregates_WithoutPolicy_FormatsColumns()
    {
        string sql = _none.CreateQuery(null, groups: ["Category", "Code"]).QueryText;

        Assert.Contains("GROUP BY c[\"Category\"], c[\"Code\"]", sql);
    }

    [Fact]
    public void GroupBy_WithAggregates_AppliesNamingPolicy()
    {
        ColumnOptions colOpts = new(
            Names: ["Category"],
            Aggregates: [new("Price", Q.Aggregate.Sum)]
        );
        string sql = _camel.CreateQuery(null, colOpts, groups: ["Category"]).QueryText;

        Assert.Contains("GROUP BY c[\"category\"]", sql);
        Assert.Contains("SUM(c[\"price\"])", sql);
    }

    #endregion

    #region Aggregation Aliases

    [Fact]
    public void AggregateAlias_Sum_AppliesNamingPolicy()
    {
        ColumnOptions colOpts = new(
            Names: ["Category"],
            Aggregates: [new("Price", Q.Aggregate.Sum)]
        );
        string sql = _camel.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("SUM(c[\"price\"]) AS price_Sum", sql);
        Assert.DoesNotContain("AS Price_Sum", sql);
    }

    [Fact]
    public void AggregateAlias_Min_AppliesNamingPolicy()
    {
        ColumnOptions colOpts = new(
            Names: ["Category"],
            Aggregates: [new("Price", Q.Aggregate.Min)]
        );
        string sql = _camel.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("MIN(c[\"price\"]) AS price_Min", sql);
    }

    [Fact]
    public void AggregateAlias_Max_AppliesNamingPolicy()
    {
        ColumnOptions colOpts = new(
            Names: ["Category"],
            Aggregates: [new("Price", Q.Aggregate.Max)]
        );
        string sql = _camel.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("MAX(c[\"price\"]) AS price_Max", sql);
    }

    [Fact]
    public void AggregateAlias_Avg_AppliesNamingPolicy()
    {
        ColumnOptions colOpts = new(
            Names: ["Category"],
            Aggregates: [new("Price", Q.Aggregate.Avg)]
        );
        string sql = _camel.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("AVG(c[\"price\"]) AS price_Avg", sql);
    }

    [Fact]
    public void AggregateAlias_WithoutPolicy_PreservesNames()
    {
        ColumnOptions colOpts = new(
            Names: ["Category"],
            Aggregates: [new("Price", Q.Aggregate.Sum)]
        );
        string sql = _none.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("AS Price_Sum", sql);
    }

    #endregion

    #region COUNT Alias

    [Fact]
    public void CountAlias_AppliesNamingPolicy()
    {
        ColumnOptions colOpts = new(
            Names: ["Category"],
            Aggregates: [new("Price", Q.Aggregate.Count)]
        );
        string sql = _camel.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("COUNT(1) AS countAggregate", sql);
        Assert.DoesNotContain("AS CountAggregate", sql);
    }

    [Fact]
    public void CountAlias_WithoutPolicy_PreservesPascalCase()
    {
        ColumnOptions colOpts = new(
            Names: ["Category"],
            Aggregates: [new("Price", Q.Aggregate.Count)]
        );
        string sql = _none.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("COUNT(1) AS CountAggregate", sql);
    }

    #endregion

    #region VectorDistance Score Alias

    [Fact]
    public void VectorDistanceScoreAlias_AppliesNamingPolicy()
    {
        float[] vector = [0.1f, 0.2f, 0.3f];
        List<Cluster> clusters = [new([new("Embedding", vector, Operator: Q.Operator.VectorDistance)])];
        ColumnOptions colOpts = new(Names: ["Name"], Top: 10);
        string sql = _camel.CreateQuery(clusters, colOpts).QueryText;

        Assert.Contains("AS embeddingScore", sql);
        Assert.DoesNotContain("AS EmbeddingScore", sql);
    }

    [Fact]
    public void VectorDistanceScoreAlias_WithoutPolicy_PreservesName()
    {
        float[] vector = [0.1f, 0.2f, 0.3f];
        List<Cluster> clusters = [new([new("Embedding", vector, Operator: Q.Operator.VectorDistance)])];
        ColumnOptions colOpts = new(Names: ["Name"], Top: 10);
        string sql = _none.CreateQuery(clusters, colOpts).QueryText;

        Assert.Contains("AS EmbeddingScore", sql);
    }

    #endregion

    #region JOIN Aggregates

    [Fact]
    public void JoinAggregateAlias_AppliesNamingPolicy()
    {
        ColumnOptions colOpts = new(
            Names: ["Name"],
            Join: new("link", "Links", ["Url"], [new("Price", Q.Aggregate.Sum)])
        );
        string sql = _camel.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("SUM(link[\"price\"]) AS price_Sum", sql);
        Assert.Contains("link[\"url\"]", sql);
    }

    [Fact]
    public void JoinCountAlias_AppliesNamingPolicy()
    {
        ColumnOptions colOpts = new(
            Names: ["Name"],
            Join: new("link", "Links", ["Url"], [new("*", Q.Aggregate.Count)])
        );
        string sql = _camel.CreateQuery(null, colOpts).QueryText;

        Assert.Contains("COUNT(1) AS countAggregate", sql);
    }

    #endregion
}
