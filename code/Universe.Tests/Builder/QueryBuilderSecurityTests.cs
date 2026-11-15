using Universe.Builder;
using Universe.Builder.Options;
using Universe.Exception;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class QueryBuilderSecurityTests : IDisposable
{
    private readonly UniverseBuilder _builder = new(recordQueries: true);

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void GroupBy_FormatsWildcardProjectionAndDeduplicatesNestedPaths()
    {
        using UniverseBuilder builder = new(false, JsonNamingPolicy.CamelCase);

        string sql = builder.CreateQuery(null, groups: ["Category", "Metadata.Type", "Category"]).QueryText;

        Assert.Equal("SELECT c[\"category\"], c[\"metadata\"][\"type\"] FROM c GROUP BY c[\"category\"], c[\"metadata\"][\"type\"]", sql);
    }

    [Fact]
    public void GroupBy_OpeningBracketIsAPropertyCharacter()
    {
        string sql = _builder.CreateQuery(null, groups: ["category[raw"]).QueryText;

        Assert.Equal("SELECT c[\"category[raw\"] FROM c GROUP BY c[\"category[raw\"]", sql);
    }

    [Fact]
    public void GroupBy_ExpressionShapedTextCannotBecomeSqlSyntax()
    {
        string sql = _builder.CreateQuery(null, groups: ["CONCAT('[', secret)"]).QueryText;

        Assert.Equal("SELECT c[\"CONCAT(\\u0027[\\u0027, secret)\"] FROM c GROUP BY c[\"CONCAT(\\u0027[\\u0027, secret)\"]", sql);
    }

    [Fact]
    public void GroupBy_AggregateAndJoinPathsKeepAliasesAndOrder()
    {
        using UniverseBuilder builder = new(false, JsonNamingPolicy.CamelCase);
        ColumnOptions options = new(["Category"], Aggregates: [new("Price", Q.Aggregate.Sum)],
            Join: new("item", "Items", ["Sku"], [new("Price", Q.Aggregate.Sum)]));

        string sql = builder.CreateQuery(null, options, groups: ["Metadata.Type", "Category"]).QueryText;

        Assert.Equal("SELECT c[\"category\"], SUM(c[\"price\"]) AS price_Sum, item[\"sku\"], SUM(item[\"price\"]) AS price_Sum FROM c JOIN item IN c[\"items\"] GROUP BY c[\"metadata\"][\"type\"], c[\"category\"], item[\"sku\"]", sql);
    }

    [Fact]
    public void PropertyPath_EscapesBackslashAndNamingPolicyOutput()
    {
        Assert.Equal("SELECT c[\"path\\\\name\"] FROM c", _builder.CreateQuery(null, new(["path\\name"])).QueryText);
        using UniverseBuilder builder = new(false, new QuoteNamingPolicy());

        Assert.Equal("SELECT c[\"Name\\u0022\\\\tail\"] FROM c", builder.CreateQuery(null, new(["Name"])).QueryText);
    }

    private sealed class QuoteNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => name + "\"\\tail";
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a-b$c")]
    [InlineData("x) OR true --")]
    [InlineData("租户\n\"\\")]
    public void ParameterName_EncodesCustomIdsAsBoundedAscii(string id)
    {
        Catalyst catalyst = new("metadata.name", "value") { CatalystId = id };

        Assert.Matches("^p_[a-f0-9]{64}$", catalyst.ParameterName());
        Assert.Equal(catalyst.ParameterName(), catalyst.ParameterName());
    }

    [Fact]
    public void ParameterName_DistinguishesNullEmptyAndAmbiguousColumnIdPairs()
    {
        Assert.NotEqual(new Catalyst("name") { CatalystId = null }.ParameterName(), new Catalyst("name") { CatalystId = "" }.ParameterName());
        Assert.NotEqual(new Catalyst("a-b") { CatalystId = "c" }.ParameterName(), new Catalyst("ab") { CatalystId = "c" }.ParameterName());
        Assert.NotEqual(new Catalyst("ab") { CatalystId = "c" }.ParameterName(), new Catalyst("a") { CatalystId = "bc" }.ParameterName());
    }

    [Fact]
    public void SharedCatalystId_DifferentValues_KeepSeparateScalarBindings()
    {
        Catalyst first = new("tenant", "first") { CatalystId = "same" };
        Catalyst second = new("tenant", "second", Where: Q.Where.Or) { CatalystId = "same" };

        QueryDefinition query = _builder.CreateQuery([new([first, second])]);
        IReadOnlyList<(string Name, object Value)> parameters = query.GetQueryParameters();

        Assert.Equal(2, parameters.Count);
        Assert.Equal(["first", "second"], parameters.Select(parameter => parameter.Value));
        Assert.NotEqual(parameters[0].Name, parameters[1].Name);
        Assert.Equal($"SELECT * FROM c WHERE (c[\"tenant\"] = {parameters[0].Name} OR c[\"tenant\"] = {parameters[1].Name})", query.QueryText);
    }

    [Fact]
    public void SharedCatalystId_EqualInstantsWithDifferentOffsets_KeepExactValues()
    {
        DateTimeOffset utc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Catalyst first = new("timestamp", utc) { CatalystId = "shared" };
        Catalyst second = first with { Value = utc.ToOffset(TimeSpan.FromHours(1)) };

        QueryDefinition query = _builder.CreateQuery([new([first, second])]);
        IReadOnlyList<(string Name, object Value)> parameters = query.GetQueryParameters();

        Assert.Equal(2, parameters.Count);
        Assert.Same(first.Value, parameters[0].Value);
        Assert.Same(second.Value, parameters[1].Value);
        Assert.Equal(TimeSpan.Zero, ((DateTimeOffset)parameters[0].Value).Offset);
        Assert.Equal(TimeSpan.FromHours(1), ((DateTimeOffset)parameters[1].Value).Offset);
        Assert.NotEqual(parameters[0].Name, parameters[1].Name);
        Assert.Equal($"SELECT * FROM c WHERE (c[\"timestamp\"] = {parameters[0].Name} AND c[\"timestamp\"] = {parameters[1].Name})", query.QueryText);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RankBindings_EncodeIdsConsistentlyAcrossSelectOrderAndFullText(bool vector, bool multiple)
    {
        Catalyst rank = vector
            ? new("embedding", new float[] { 0.1f, 0.2f }, Operator: Q.Operator.VectorDistance) { CatalystId = "bad) OR true --" }
            : new("text", new[] { "one", "two" }, Operator: Q.Operator.FTScore) { CatalystId = "bad) OR true --" };
        List<Catalyst> catalysts = [rank];
        if (multiple)
            catalysts.Add(new("text", new[] { "three", "four" }, Operator: Q.Operator.FTScore) { CatalystId = rank.CatalystId });
        catalysts.Add(new("tenant", "allowed") { CatalystId = "bad) OR true --" });

        QueryDefinition query = _builder.CreateQuery([new(catalysts)], new(null, Top: 5));
        (_, IReadOnlyList<string> names) = QueryParameterNormalizer.Normalize(query.QueryText);
        IReadOnlyList<(string Name, object Value)> parameters = query.GetQueryParameters();

        Assert.DoesNotContain("OR true --", query.QueryText);
        Assert.All(parameters, parameter => Assert.Matches("^@p_[a-f0-9]{64}(_[0-9]+)?(_ft[0-9]+)?$", parameter.Name));
        Assert.Equal(parameters.Select(parameter => parameter.Name).Order(), names.Distinct().Order());
        Assert.Equal((vector ? 1 : 2) + (multiple ? 2 : 0) + 1, parameters.Count);
        Assert.Contains(parameters, parameter => Equals(parameter.Value, "allowed"));
        Assert.Contains(parameters, parameter => vector ? ReferenceEquals(parameter.Value, rank.Value) : Equals(parameter.Value, "one"));
        if (multiple)
            Assert.Contains(parameters, parameter => Equals(parameter.Value, "three"));
    }

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
