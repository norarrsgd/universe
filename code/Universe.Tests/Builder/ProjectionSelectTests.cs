using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Universe.Builder;
using Universe.Builder.Options;
using Universe.Extensions;
using Xunit;

namespace Universe.Tests.Builder;

/// <summary>
/// Tests for the type-based Select&lt;TProjection&gt;() method on Orbit,
/// including property extraction, filtering, and SQL generation.
/// </summary>
public sealed class ProjectionSelectTests : IDisposable
{
	private readonly UniverseBuilder _builder = new(recordQueries: true);
	private readonly UniverseBuilder _camel = new(recordQueries: true, namingPolicy: JsonNamingPolicy.CamelCase);

	private static readonly Regex ParameterNormalizer = new(@"@\w+", RegexOptions.Compiled);
	private static string Normalize(string sql) => ParameterNormalizer.Replace(sql, "@p");

	public void Dispose()
	{
		_builder.Dispose();
		_camel.Dispose();
	}

	#region Property Extraction

	[Fact]
	public void Select_ExtractsAllPublicProperties()
	{
		Orbit<TestEntity> orbit = new(null);
		orbit.Select<SimpleProjection>();
		(_, ColumnOptions? colOpts, _, _) = orbit.Build();

		Assert.NotNull(colOpts);
		Assert.Contains("Name", colOpts.Value.Names);
		Assert.Contains("Price", colOpts.Value.Names);
		Assert.Equal(2, colOpts.Value.Names.Count);
	}

	[Fact]
	public void Select_ExcludesJsonIgnoreProperties()
	{
		Orbit<TestEntity> orbit = new(null);
		orbit.Select<ProjectionWithIgnore>();
		(_, ColumnOptions? colOpts, _, _) = orbit.Build();

		Assert.NotNull(colOpts);
		Assert.Contains("Name", colOpts.Value.Names);
		Assert.DoesNotContain("Secret", colOpts.Value.Names);
		Assert.Single(colOpts.Value.Names);
	}

	[Fact]
	public void Select_IncludesInheritedProperties()
	{
		Orbit<TestEntity> orbit = new(null);
		orbit.Select<DerivedProjection>();
		(_, ColumnOptions? colOpts, _, _) = orbit.Build();

		Assert.NotNull(colOpts);
		Assert.Contains("Name", colOpts.Value.Names);
		Assert.Contains("Price", colOpts.Value.Names);
		Assert.Contains("Category", colOpts.Value.Names);
		Assert.Equal(3, colOpts.Value.Names.Count);
	}

	[Fact]
	public void Select_EmptyProjection_ProducesNoColumns()
	{
		Orbit<TestEntity> orbit = new(null);
		orbit.Select<EmptyProjection>();
		(_, ColumnOptions? colOpts, _, _) = orbit.Build();

		Assert.Null(colOpts);
	}

	[Fact]
	public void Select_WorksWithRecordStruct()
	{
		Orbit<TestEntity> orbit = new(null);
		orbit.Select<StructProjection>();
		(_, ColumnOptions? colOpts, _, _) = orbit.Build();

		Assert.NotNull(colOpts);
		Assert.Contains("Name", colOpts.Value.Names);
		Assert.Contains("Price", colOpts.Value.Names);
		Assert.Equal(2, colOpts.Value.Names.Count);
	}

	#endregion

	#region Combining with String Select

	[Fact]
	public void Select_CombinesWithStringSelect()
	{
		Orbit<TestEntity> orbit = new(null);
		orbit.Select<SimpleProjection>().Select("Extra");
		(_, ColumnOptions? colOpts, _, _) = orbit.Build();

		Assert.NotNull(colOpts);
		Assert.Contains("Name", colOpts.Value.Names);
		Assert.Contains("Price", colOpts.Value.Names);
		Assert.Contains("Extra", colOpts.Value.Names);
		Assert.Equal(3, colOpts.Value.Names.Count);
	}

	[Fact]
	public void Select_StringThenProjection_CombinesBoth()
	{
		Orbit<TestEntity> orbit = new(null);
		orbit.Select("Extra").Select<SimpleProjection>();
		(_, ColumnOptions? colOpts, _, _) = orbit.Build();

		Assert.NotNull(colOpts);
		Assert.Contains("Extra", colOpts.Value.Names);
		Assert.Contains("Name", colOpts.Value.Names);
		Assert.Contains("Price", colOpts.Value.Names);
		Assert.Equal(3, colOpts.Value.Names.Count);
	}

	#endregion

	#region SQL Generation

	[Fact]
	public void Select_GeneratesSameSqlAsStringSelect()
	{
		// Projection-based
		Orbit<TestEntity> projection = new(null);
		projection.Select<SimpleProjection>().Cluster(c => c.Eq("category", "test"));
		(var pClusters, ColumnOptions? pColOpts, var pSort, var pGroups) = projection.Build();
		string projectionSql = _builder.CreateQuery(pClusters, pColOpts, pSort, pGroups).QueryText;

		// String-based (same column names)
		Orbit<TestEntity> manual = new(null);
		manual.Select("Name", "Price").Cluster(c => c.Eq("category", "test"));
		(var mClusters, ColumnOptions? mColOpts, var mSort, var mGroups) = manual.Build();
		string manualSql = _builder.CreateQuery(mClusters, mColOpts, mSort, mGroups).QueryText;

		Assert.Equal(Normalize(manualSql), Normalize(projectionSql));
	}

	[Fact]
	public void Select_NamingPolicyAppliedToExtractedNames()
	{
		Orbit<TestEntity> orbit = new(null);
		orbit.Select<SimpleProjection>();
		(var clusters, ColumnOptions? colOpts, var sorting, var groups) = orbit.Build();
		string sql = _camel.CreateQuery(clusters, colOpts, sorting, groups).QueryText;

		Assert.Contains("c[\"name\"]", sql);
		Assert.Contains("c[\"price\"]", sql);
		Assert.DoesNotContain("c[\"Name\"]", sql);
		Assert.DoesNotContain("c[\"Price\"]", sql);
	}

	#endregion

	#region Cache Behavior

	[Fact]
	public void Cache_ReturnsSameInstanceForSameType()
	{
		IReadOnlyList<string> first = ProjectionColumnExtractor.GetColumnNames<SimpleProjection>();
		IReadOnlyList<string> second = ProjectionColumnExtractor.GetColumnNames<SimpleProjection>();

		Assert.Same(first, second);
	}

	[Fact]
	public void Cache_ReturnsDifferentInstancesForDifferentTypes()
	{
		IReadOnlyList<string> simple = ProjectionColumnExtractor.GetColumnNames<SimpleProjection>();
		IReadOnlyList<string> derived = ProjectionColumnExtractor.GetColumnNames<DerivedProjection>();

		Assert.NotSame(simple, derived);
	}

	#endregion
}

#region Test Projection Types

internal record SimpleProjection
{
	public string Name { get; set; }
	public decimal Price { get; set; }
}

internal record ProjectionWithIgnore
{
	public string Name { get; set; }
	[JsonIgnore] public string Secret { get; set; }
}

internal record DerivedProjection : SimpleProjection
{
	public string Category { get; set; }
}

internal record EmptyProjection;

internal record struct StructProjection
{
	public string Name { get; set; }
	public decimal Price { get; set; }
}

#endregion
