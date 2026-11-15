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

        Assert.Equal("SELECT * FROM c WHERE (c[\"name\"] = @p_9594e71c2dff3a38d84ff4ffd98d1a5b56c651cd585a828749f37001c53e7ae5 OR c[\"name\"] = @p_9594e71c2dff3a38d84ff4ffd98d1a5b56c651cd585a828749f37001c53e7ae5)", query.QueryText);
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

        Assert.Contains(" WHERE (c[\"active\"] = @p_b8d146b12f7e230987b7a0c0e4b3aeecd0b33b8b690960f6af9455ee57aafc9f OR c[\"price\"] > @p_99d9a6d1e66435617ce63d6e3b82e22023a26659c6967b798f75b1eb0a2d7481) OR (c[\"name\"] = @p_dff3a0168a62effdb15776ecfc445c0cabce7b73e94e5c8303e80907cb0c54a7)", query.QueryText);
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
