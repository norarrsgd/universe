using System.Collections;
using Microsoft.Azure.Cosmos;
using Universe.Builder;
using Universe.Builder.Options;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class ScalarFilterTests
{
    [Fact]
    public void RepeatedCatalyst_EmitsConnectorForEveryOccurrence()
    {
        using UniverseBuilder builder = new();
        Catalyst catalyst = new("name", "test", Where: Q.Where.Or) { CatalystId = "a" };

        QueryDefinition query = builder.CreateQuery([new([catalyst, catalyst])]);

        Assert.Equal("SELECT * FROM c WHERE (c[\"name\"] = @namea OR c[\"name\"] = @namea)", query.QueryText);
        Assert.Equal("test", Assert.Single(query.GetQueryParameters()).Value);
    }

    [Fact]
    public void RankBeforeScalar_DoesNotEmitLeadingConnector()
    {
        using UniverseBuilder builder = new();
        Catalyst rank = new("embedding", new float[] { 0.1f, 0.2f }, Operator: Q.Operator.VectorDistance) { CatalystId = "v" };
        Catalyst first = new("active", true, Where: Q.Where.Or) { CatalystId = "a" };
        Catalyst second = new("price", 10, Where: Q.Where.Or, Operator: Q.Operator.Gt) { CatalystId = "b" };
        Catalyst third = new("name", "test") { CatalystId = "c" };

        QueryDefinition query = builder.CreateQuery([new([rank, first, second]), new([third], Q.Where.Or)], new(null, Top: 5));

        Assert.Contains(" WHERE (c[\"active\"] = @activea OR c[\"price\"] > @priceb) OR (c[\"name\"] = @namec)", query.QueryText);
        Assert.Equal(4, query.GetQueryParameters().Count);
    }

    [Fact]
    public void RankOnlyCluster_DoesNotEmitWhereClause()
    {
        using UniverseBuilder builder = new();
        QueryDefinition query = builder.CreateQuery([new([new("text", new[] { "term" }, Operator: Q.Operator.FTScore)])], new(null, Top: 5));

        Assert.DoesNotContain(" WHERE ", query.QueryText);
        Assert.Contains("ORDER BY RANK FullTextScore(", query.QueryText);
    }

    [Fact]
    public void ManyScalarFilters_TraverseCollectionLinearly()
    {
        using UniverseBuilder builder = new();
        CountingCatalysts catalysts = new(Enumerable.Range(0, 256).Select(index => new Catalyst($"field{index}", index)).ToArray());

        QueryDefinition query = builder.CreateQuery([new(catalysts)]);

        Assert.Equal(256, query.GetQueryParameters().Count);
        Assert.InRange(catalysts.Visited, 256, 256 * 20);
    }

    private sealed class CountingCatalysts(IReadOnlyList<Catalyst> items) : IReadOnlyList<Catalyst>
    {
        public int Visited { get; private set; }
        public int Count => items.Count;
        public Catalyst this[int index] { get { Visited++; return items[index]; } }

        public IEnumerator<Catalyst> GetEnumerator()
        {
            foreach (Catalyst item in items)
            {
                Visited++;
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
