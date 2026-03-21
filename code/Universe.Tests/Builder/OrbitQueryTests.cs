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
        string expected = _builder.CreateQuery(declarative).QueryText;

        // Fluent
        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void SingleCluster_MultipleAndCatalysts()
    {
        List<Cluster> declarative = [new([
            new("name", "test"),
            new("price", 50.0, Operator: Q.Operator.Gt)
        ])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test").And().Gt("price", 50.0));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

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
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test").Or().Eq("code", "ABC").And().Gte("price", 10.0));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

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
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test"))
             .And()
             .Cluster(c => c.Eq("code", "ABC"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void MultipleClusters_WithOr()
    {
        List<Cluster> declarative = [
            new([new("name", "test")]),
            new([new("code", "ABC")], Where: Q.Where.Or)
        ];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test"))
             .Or()
             .Cluster(c => c.Eq("code", "ABC"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Column Selection Tests

    [Fact]
    public void SelectColumns_WithTop()
    {
        List<Cluster> declarative = [new([new("name", "test")])];
        ColumnOptions colOpts = new(Names: ["id", "name", "price"], Top: 10);
        string expected = _builder.CreateQuery(declarative, colOpts).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Select("id", "name", "price").Top(10)
             .Cluster(c => c.Eq("name", "test"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? fluentColOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, fluentColOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void Distinct()
    {
        List<Cluster> declarative = [new([new("category", "Electronics")])];
        ColumnOptions colOpts = new(Names: ["category"], IsDistinct: true);
        string expected = _builder.CreateQuery(declarative, colOpts).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Select("category").Distinct()
             .Cluster(c => c.Eq("category", "Electronics"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? fluentColOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, fluentColOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public void OrderByDescending()
    {
        List<Cluster> declarative = [new([new("name", "test")])];
        List<Sorting.Option> sort = [new("price", Sorting.Direction.DESC)];
        string expected = _builder.CreateQuery(declarative, sorting: sort).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("name", "test")).OrderByDescending("price");
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Operator Tests

    [Fact]
    public void LikeOperator()
    {
        List<Cluster> declarative = [new([new("name", "%Test%", Operator: Q.Operator.Like)])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Like("name", "%Test%"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void DefinedOperator()
    {
        List<Cluster> declarative = [new([
            new("name", Operator: Q.Operator.Defined),
            new("description", Operator: Q.Operator.NotDefined)
        ])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Defined("name").And().NotDefined("description"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void ContainsOperator()
    {
        string[] values = ["Electronics", "Books"];
        List<Cluster> declarative = [new([new("category", values, Operator: Q.Operator.Contains)])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Contains("category", values));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void NotContainsOperator()
    {
        string[] values = ["Blocked1", "Blocked2"];
        List<Cluster> declarative = [new([new("code", values, Operator: Q.Operator.NotContains)])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.NotContains("code", values));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void InOperator()
    {
        List<Cluster> declarative = [new([new("links", "someValue", Operator: Q.Operator.In)])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.In("links", "someValue"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void NotInOperator()
    {
        List<Cluster> declarative = [new([new("tags", "removed", Operator: Q.Operator.NotIn)])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.NotIn("tags", "removed"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void LenOperator()
    {
        List<Cluster> declarative = [new([new("items", 5, Operator: Q.Operator.Len)])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Len("items", 5));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void NotLikeOperator()
    {
        List<Cluster> declarative = [new([new("name", "%skip%", Operator: Q.Operator.NotLike)])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.NotLike("name", "%skip%"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

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
        string expected = _builder.CreateQuery(declarative, colOpts, groups: group).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Select("category")
             .Aggregate("price", Q.Aggregate.Sum)
             .Aggregate("id", Q.Aggregate.Count)
             .GroupBy("category")
             .Cluster(c => c.Eq("status", "active"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? fluentColOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, fluentColOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region No Clusters Tests

    [Fact]
    public void NoClusters_SelectAll()
    {
        string expected = _builder.CreateQuery(null).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

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
        string expected = _builder.CreateQuery(declarative, colOpts).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Select("name", "description").Top(5)
             .Cluster(c => c.VectorDistance("embedding", vector));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? fluentColOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, fluentColOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    #endregion

    #region Full-Text Search Tests

    [Fact]
    public void FTContains()
    {
        List<Cluster> declarative = [new([new("description", "machine", Operator: Q.Operator.FTContains)])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.FTContains("description", "machine"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void FTContainsAll()
    {
        string[] keywords = ["machine", "learning"];
        List<Cluster> declarative = [new([new("description", keywords, Operator: Q.Operator.FTContainsAll)])];
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.FTContainsAll("description", keywords));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

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
        string expected = _builder.CreateQuery(declarative, colOpts, sort).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
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
        (IReadOnlyList<Cluster> clusters, ColumnOptions? fluentColOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, fluentColOpts, sorting, groups).QueryText;

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
        string expected = _builder.CreateQuery(declarative).QueryText;

        Orbit<TestEntity> orbit = new Orbit<TestEntity>(null);
        orbit.Cluster(c => c.Eq("status", "active"))
             .And()
             .Cluster(c => c.Gte("priority", 1))
             .Or()
             .Cluster(c => c.NotDefined("archived"));
        (IReadOnlyList<Cluster> clusters, ColumnOptions? colOpts, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = orbit.Build();
        string actual = _builder.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

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
