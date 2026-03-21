using System.Text.RegularExpressions;
using Universe.Builder;
using Universe.Builder.Options;
using Universe.Interfaces;
using Xunit;

namespace Universe.Tests.Builder;

/// <summary>
/// Tests that the fluent Orbit builder generates identical SQL to the declarative Cluster/Catalyst API.
/// </summary>
public sealed class OrbitQueryTests : IDisposable
{
    private readonly UniverseBuilder _builder = new(recordQueries: true);

    public void Dispose() => _builder.Dispose();

    private static readonly Regex ParameterNormalizer = new(@"@\w+", RegexOptions.Compiled);

    private static string Normalize(string sql) =>
        ParameterNormalizer.Replace(sql, "@p");

    #region Single Cluster Tests

    [Fact]
    public void SingleCluster_SingleCatalyst()
    {
        // Declarative
        List<Cluster> declarative = [new([new("name", "test")])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        // Fluent
        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test"));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void SingleCluster_MultipleAndCatalysts()
    {
        List<Cluster> declarative = [new([
            new("name", "test"),
            new("price", 50.0, Operator: Q.Operator.Gt)
        ])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test").And().Gt("price", 50.0));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void SingleCluster_MixedAndOrCatalysts()
    {
        List<Cluster> declarative = [new([
            new("name", "test"),
            new("code", "ABC", Where: Q.Where.Or),
            new("price", 10.0, Operator: Q.Operator.Gte)
        ])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test").Or().Eq("code", "ABC").And().Gte("price", 10.0));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Multiple Cluster Tests

    [Fact]
    public void MultipleClusters_WithAnd()
    {
        List<Cluster> declarative = [
            new([new("name", "test")]),
            new([new("code", "ABC")], Where: Q.Where.And)
        ];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test"))
             .And()
             .Cluster(c => c.Eq("code", "ABC"));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void MultipleClusters_WithOr()
    {
        List<Cluster> declarative = [
            new([new("name", "test")]),
            new([new("code", "ABC")], Where: Q.Where.Or)
        ];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test"))
             .Or()
             .Cluster(c => c.Eq("code", "ABC"));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Column Selection Tests

    [Fact]
    public void SelectColumns_WithTop()
    {
        List<Cluster> declarative = [new([new("name", "test")])];
        ColumnOptions colOpts = new(Names: ["id", "name", "price"], Top: 10);
        var expected = _builder.CreateQuery(declarative, colOpts).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Select("id", "name", "price").Top(10)
             .Cluster(c => c.Eq("name", "test"));
        var (clusters, fluentColOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, fluentColOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void Distinct()
    {
        List<Cluster> declarative = [new([new("category", "Electronics")])];
        ColumnOptions colOpts = new(Names: ["category"], IsDistinct: true);
        var expected = _builder.CreateQuery(declarative, colOpts).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Select("category").Distinct()
             .Cluster(c => c.Eq("category", "Electronics"));
        var (clusters, fluentColOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, fluentColOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public void OrderByDescending()
    {
        List<Cluster> declarative = [new([new("name", "test")])];
        List<Sorting.Option> sort = [new("price", Sorting.Direction.DESC)];
        var expected = _builder.CreateQuery(declarative, sorting: sort).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test")).OrderByDescending("price");
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Operator Tests

    [Fact]
    public void LikeOperator()
    {
        List<Cluster> declarative = [new([new("name", "%Test%", Operator: Q.Operator.Like)])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Like("name", "%Test%"));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void DefinedOperator()
    {
        List<Cluster> declarative = [new([
            new("name", Operator: Q.Operator.Defined),
            new("description", Operator: Q.Operator.NotDefined)
        ])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Defined("name").And().NotDefined("description"));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void ContainsOperator()
    {
        string[] values = ["Electronics", "Books"];
        List<Cluster> declarative = [new([new("category", values, Operator: Q.Operator.Contains)])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Contains("category", values));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void NotContainsOperator()
    {
        string[] values = ["Blocked1", "Blocked2"];
        List<Cluster> declarative = [new([new("code", values, Operator: Q.Operator.NotContains)])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.NotContains("code", values));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void InOperator()
    {
        List<Cluster> declarative = [new([new("links", "someValue", Operator: Q.Operator.In)])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.In("links", "someValue"));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void NotInOperator()
    {
        List<Cluster> declarative = [new([new("tags", "removed", Operator: Q.Operator.NotIn)])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.NotIn("tags", "removed"));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void LenOperator()
    {
        List<Cluster> declarative = [new([new("items", 5, Operator: Q.Operator.Len)])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Len("items", 5));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void NotLikeOperator()
    {
        List<Cluster> declarative = [new([new("name", "%skip%", Operator: Q.Operator.NotLike)])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.NotLike("name", "%skip%"));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Aggregation Tests

    [Fact]
    public void Aggregation_WithGroupBy()
    {
        List<Cluster> declarative = [new([new("status", "active")])];
        ColumnOptions colOpts = new(
            Names: ["category"],
            Aggregates: [
                new("price", Q.Aggregate.Sum),
                new("id", Q.Aggregate.Count)
            ]);
        List<string> group = ["category"];
        var expected = _builder.CreateQuery(declarative, colOpts, groups: group).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Select("category")
             .Aggregate("price", Q.Aggregate.Sum)
             .Aggregate("id", Q.Aggregate.Count)
             .GroupBy("category")
             .Cluster(c => c.Eq("status", "active"));
        var (clusters, fluentColOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, fluentColOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region No Clusters Tests

    [Fact]
    public void NoClusters_SelectAll()
    {
        var expected = _builder.CreateQuery(null).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Vector Search Tests

    [Fact]
    public void VectorDistance()
    {
        float[] vector = [0.1f, 0.2f, 0.3f];
        List<Cluster> declarative = [new([new("embedding", vector, Operator: Q.Operator.VectorDistance)])];
        ColumnOptions colOpts = new(Names: ["name", "description"], Top: 5);
        var expected = _builder.CreateQuery(declarative, colOpts).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Select("name", "description").Top(5)
             .Cluster(c => c.VectorDistance("embedding", vector));
        var (clusters, fluentColOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, fluentColOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Full-Text Search Tests

    [Fact]
    public void FTContains()
    {
        List<Cluster> declarative = [new([new("description", "machine", Operator: Q.Operator.FTContains)])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.FTContains("description", "machine"));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void FTContainsAll()
    {
        string[] keywords = ["machine", "learning"];
        List<Cluster> declarative = [new([new("description", keywords, Operator: Q.Operator.FTContainsAll)])];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.FTContainsAll("description", keywords));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Complex Multi-Cluster Tests

    [Fact]
    public void ComplexMultiCluster_MirrorsExample4()
    {
        // Mirrors Example4_ComplexFiltering pattern
        List<Cluster> declarative = [
            new(Catalysts: [
                new("name", "%Test%", Operator: Q.Operator.Like),
                new("addedOn", "2025-01-01", Operator: Q.Operator.Gte, Where: Q.Where.And),
                new("price", 50.0, Operator: Q.Operator.Lte, Where: Q.Where.And)
            ], Where: Q.Where.And),
            new(Catalysts: [
                new("code", "SPECIAL", Where: Q.Where.Or),
                new("category", "Premium", Where: Q.Where.And)
            ])
        ];
        ColumnOptions colOpts = new(Names: ["id", "name", "price", "category"], Top: 20);
        List<Sorting.Option> sort = [new("price", Sorting.Direction.DESC)];
        var expected = _builder.CreateQuery(declarative, colOpts, sort).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Select("id", "name", "price", "category")
             .Top(20)
             .Cluster(c => c
                 .Like("name", "%Test%")
                 .And().Gte("addedOn", "2025-01-01")
                 .And().Lte("price", 50.0))
             .Cluster(c => c
                 .Eq("code", "SPECIAL")
                 .And().Eq("category", "Premium"))
             .OrderByDescending("price");
        var (clusters, fluentColOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, fluentColOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void ThreeClusters_MixedLogic()
    {
        List<Cluster> declarative = [
            new([new("status", "active")]),
            new([new("priority", 1, Operator: Q.Operator.Gte)], Where: Q.Where.And),
            new([new("archived", Operator: Q.Operator.NotDefined)], Where: Q.Where.Or)
        ];
        var expected = _builder.CreateQuery(declarative).QueryText;

        var orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("status", "active"))
             .And()
             .Cluster(c => c.Gte("priority", 1))
             .Or()
             .Cluster(c => c.NotDefined("archived"));
        var (clusters, colOpts, sorting, groups) = orbit.Build();
        var actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion
}

/// <summary>Minimal ICosmicEntity for test purposes.</summary>
internal record TestEntity : ICosmicEntity
{
    public string id { get; set; } = Guid.NewGuid().ToString();
    public DateTime AddedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedOn { get; set; }
    public long CountAggregate { get; set; }
}
