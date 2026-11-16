using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Universe.Builder;
using Universe.Builder.Options;
using Universe.Exception;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class ExtendedPredicateTests
{
    [Theory]
    [InlineData(Q.Operator.StartsWith, "STARTSWITH")]
    [InlineData(Q.Operator.StringContains, "CONTAINS")]
    [InlineData(Q.Operator.StringEquals, "STRINGEQUALS")]
    public void StringPredicates_ParameterizeLiteralAndCaseOption(Q.Operator op, string function)
    {
        using UniverseBuilder builder = new();
        Catalyst catalyst = new("data.name", "%_';--", Operator: op) { IgnoreCase = true };
        QueryDefinition query = builder.CreateQuery([new([catalyst])]);
        Assert.Contains($"{function}(c[\"data\"][\"name\"], @{catalyst.ParameterName()}, true)", query.QueryText);
        Assert.Equal("%_';--", Assert.Single(query.GetQueryParameters()).Value);
        Assert.DoesNotContain("';--", query.QueryText);
    }

    [Theory]
    [InlineData(Q.Operator.IsNull, "IS_NULL")]
    [InlineData(Q.Operator.IsString, "IS_STRING")]
    public void UnaryPredicates_DoNotBindValues(Q.Operator op, string function)
    {
        using UniverseBuilder builder = new();
        QueryDefinition query = builder.CreateQuery([new([new("data.value", Operator: op)])]);
        Assert.Contains($"{function}(c[\"data\"][\"value\"])", query.QueryText);
        Assert.Empty(query.GetQueryParameters());
    }

    [Fact]
    public void PartialObjectMatch_BindsEntireObject()
    {
        using UniverseBuilder builder = new();
        Dictionary<string, string> pattern = new() { ["key"] = "user';--" };
        Catalyst catalyst = new("data.shares", pattern, Operator: Q.Operator.In) { PartialMatch = true };
        QueryDefinition query = builder.CreateQuery([new([catalyst])]);
        Assert.Contains($"ARRAY_CONTAINS(c[\"data\"][\"shares\"], @{catalyst.ParameterName()}, true)", query.QueryText);
        Assert.Same(pattern, Assert.Single(query.GetQueryParameters()).Value);
    }

    [Theory]
    [InlineData(Q.Operator.Eq, "=")]
    [InlineData(Q.Operator.NotEq, "!=")]
    [InlineData(Q.Operator.Gt, ">")]
    [InlineData(Q.Operator.Gte, ">=")]
    [InlineData(Q.Operator.Lt, "<")]
    [InlineData(Q.Operator.Lte, "<=")]
    public void OperandComparisons_CompareArrayLengthToField(Q.Operator op, string symbol)
    {
        using UniverseBuilder builder = new(false, JsonNamingPolicy.CamelCase);
        Catalyst catalyst = Catalyst.Compare(QueryOperand.ArrayLength("Data.ShareEvents"), op, QueryOperand.Field("Data.AuditQueued", "share"));
        QueryDefinition query = builder.CreateQuery([new([catalyst])]);
        Assert.Contains($"ARRAY_LENGTH(c[\"data\"][\"shareEvents\"]) {symbol} share[\"data\"][\"auditQueued\"]", query.QueryText);
        Assert.Empty(query.GetQueryParameters());
    }

    [Fact]
    public void OperandParameter_IsBoundAndDoesNotMergeWithDifferentExpression()
    {
        using UniverseBuilder builder = new();
        object shared = 5;
        Catalyst first = Catalyst.Compare(QueryOperand.Field("value"), Q.Operator.Gt, QueryOperand.Parameter(shared)) with { CatalystId = "same" };
        Catalyst second = Catalyst.Compare(QueryOperand.ArrayLength("value"), Q.Operator.Gt, QueryOperand.Parameter(shared)) with { CatalystId = "same" };
        QueryDefinition query = builder.CreateQuery([new([first, second])]);
        Assert.Equal(2, query.GetQueryParameters().Select(parameter => parameter.Name).Distinct().Count());
        Assert.Contains("ARRAY_LENGTH", query.QueryText);
    }

    [Theory]
    [InlineData("bad\"];SELECT * FROM c--")]
    [InlineData("bad;name")]
    public void OperandIdentifiers_AreValidated(string path)
    {
        using UniverseBuilder builder = new();
        Assert.Throws<UniverseException>(() => builder.CreateQuery([new([Catalyst.Compare(QueryOperand.Field("safe"), Q.Operator.Eq, QueryOperand.Field(path))])]));
    }

    [Fact]
    public void InvalidFlagsValuesAndOperands_FailClearly()
    {
        using UniverseBuilder builder = new();
        Catalyst[] invalid =
        [
            new("name", 1, Operator: Q.Operator.StartsWith),
            new("name", Operator: Q.Operator.StringContains),
            new("name", "value", Operator: Q.Operator.IsNull),
            new("name", "value") { IgnoreCase = true },
            new("name", "value") { PartialMatch = true }
        ];
        foreach (Catalyst catalyst in invalid)
            Assert.Throws<UniverseException>(() => builder.CreateQuery([new([catalyst])]));
        Assert.Throws<UniverseException>(() => Catalyst.Compare(QueryOperand.Field("a"), Q.Operator.In, QueryOperand.Field("b")));
        Assert.Throws<UniverseException>(() => QueryOperand.Parameter(null));
    }

    [Fact]
    public void FluentPredicates_MatchDeclarativeAndPreserveLegacyOperators()
    {
        ClusterBuilder fluent = new();
        fluent.StartsWith("pk", "tenant:").StringContains("name", "", true).StringEquals("owner", "USER", true)
            .IsNull("deleted").Or().IsString("name").ArrayContains("shares", new { key = "user" }, true)
            .Compare(QueryOperand.Field("a"), Q.Operator.Gt, QueryOperand.Parameter(1));
        using UniverseBuilder builder = new();
        QueryDefinition query = builder.CreateQuery([fluent.Build(Q.Where.And)]);
        Assert.Contains("STARTSWITH", query.QueryText);
        Assert.Contains(" OR IS_STRING", query.QueryText);
        Assert.Equal(5, query.GetQueryParameters().Count);
        Assert.Equal(22, (int)Q.Operator.FTScore);
        Assert.Contains("ARRAY_CONTAINS(@", builder.CreateQuery([new([new("name", new[] { "a" }, Operator: Q.Operator.Contains)])]).QueryText);
    }
}
