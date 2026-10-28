using Microsoft.Azure.Cosmos;
using Universe.Attributes;
using Universe.Builder;
using Universe.Builder.Options;
using Universe.Builder.Strategies;
using Universe.Builder.Strategies.Storage;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;
using Universe.Tests.Helpers;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class QueryExecutionTests
{
    [Theory]
    [InlineData("Direct", QueryType.Simple)]
    [InlineData("Gateway", QueryType.Aggregation)]
    [InlineData("VectorSearch", QueryType.VectorSearch)]
    public async Task GetOne_StopsAfterFirstNonemptyPageAndRecordsConsumedWork(string strategyName, QueryType type)
    {
        RecordingStorage storage = new();
        using QueryTuner tuner = new(storage);
        using UniverseBuilder builder = new(true, tuner);
        QueryTestContainer container = new() { QueryPages = [[], [1, 2], [3]], ThrowOnQueryPage = 3 };
        QueryDefinition query = new("SELECT * FROM c WHERE c[\"value\"] = @value");
        query.WithParameter("@value", 1);
        QueryContext context = new(type, Hints: new Dictionary<string, object>
        {
            [nameof(QueryHints.ForceStrategy)] = strategyName,
            [nameof(QueryHints.MaxItemCount)] = 50,
            [nameof(QueryHints.MaxBufferedItemCount)] = 100
        });

        (Gravity gravity, int value) = await builder.GetOneFromQuery<int>(container, query, context);

        Assert.Equal(1, value);
        Assert.Equal(2, container.QueryPageReads);
        Assert.True(container.QueryIteratorDisposed);
        Assert.Equal(14.5, gravity.RU);
        Assert.Equal(query.QueryText, gravity.Query.Text);
        Assert.Equal(1, Assert.Single(gravity.Query.Parameters).Item2);
        Assert.Equal(1, container.LastQueryOptions.MaxItemCount);
        Assert.Equal(1, container.LastQueryOptions.MaxBufferedItemCount);
        QueryExecutionStatistics stats = Assert.Single(storage.Saved);
        Assert.Equal(strategyName, stats.StrategyUsed);
        Assert.Equal(1, stats.ResultCount);
        Assert.Equal(14.5, stats.RU);
        Assert.True(stats.Success);
    }

    [Theory]
    [InlineData("Direct")]
    [InlineData("Gateway")]
    [InlineData("VectorSearch")]
    public async Task GetOne_NoMatches_ReturnsDefaultAndDisposesIterator(string strategyName)
    {
        using UniverseBuilder builder = new();
        QueryTestContainer container = new() { QueryPages = [[], []] };
        QueryContext context = Context(strategyName);

        (Gravity gravity, string value) = await builder.GetOneFromQuery<string>(container, new("SELECT * FROM c"), context);

        Assert.Null(value);
        Assert.Null(gravity.Query.Text);
        Assert.Equal(14.5, gravity.RU);
        Assert.Equal(2, container.QueryPageReads);
        Assert.True(container.QueryIteratorDisposed);
    }

    [Theory]
    [InlineData("Direct")]
    [InlineData("Gateway")]
    [InlineData("VectorSearch")]
    public async Task GetOne_ReadFailure_PropagatesAndDisposesIterator(string strategyName)
    {
        RecordingStorage storage = new();
        using QueryTuner tuner = new(storage);
        using UniverseBuilder builder = new(false, tuner);
        QueryTestContainer container = new() { QueryPages = [[], [1]], ThrowOnQueryPage = 2 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.GetOneFromQuery<int>(container, new("SELECT * FROM c"), Context(strategyName)));

        Assert.Equal(2, container.QueryPageReads);
        Assert.True(container.QueryIteratorDisposed);
        QueryExecutionStatistics stats = Assert.Single(storage.Saved);
        Assert.False(stats.Success);
        Assert.Equal(7.25, stats.RU);
    }

    [Theory]
    [InlineData("Direct")]
    [InlineData("Gateway")]
    [InlineData("VectorSearch")]
    public async Task List_PageSizeOne_DoesNotLimitTotalResults(string strategyName)
    {
        using UniverseBuilder builder = new();
        QueryTestContainer container = new() { QueryPages = [[1], [2], [3]] };
        QueryContext context = Context(strategyName) with { MaxItemCount = 1 };

        (Gravity gravity, IList<int> values) = await builder.GetListFromQuery<int>(container, new("SELECT * FROM c"), context);

        Assert.Equal([1, 2, 3], values);
        Assert.Equal(3, container.QueryPageReads);
        Assert.Equal(21.75, gravity.RU);
        Assert.True(container.QueryIteratorDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepositoryGet_UsesTopOneBeforeExecutionAndCaching(bool projected)
    {
        using CosmosClient client = new("https://localhost:8081", Convert.ToBase64String(new byte[64]));
        QueryTestContainer container = new() { QueryItems = [new Entity { id = "first", Tenant = "tenant" }] };
        using TestGalaxy repository = new(client, container);
        IGalaxy<Entity> galaxy = repository;

        if (projected)
        {
            await galaxy.Get<Entity>(null, ["id"]);
            (Gravity cached, _) = await galaxy.Get<Entity>(null, ["id"]);
            Assert.Equal(0, cached.RU);
            Assert.Equal("SELECT TOP 1 c[\"id\"] FROM c", cached.Query.Text);
        }
        else
        {
            await galaxy.Get(null);
            Assert.Equal("SELECT TOP 1 * FROM c", container.LastQuery.QueryText);
        }

        Assert.StartsWith("SELECT TOP 1 ", container.LastQuery.QueryText);
        Assert.Equal(1, container.QueryCalls);
    }

    [Theory]
    [InlineData("VectorDistance", false)]
    [InlineData("FullTextScore", true)]
    [InlineData("RRF", false)]
    [InlineData("GROUP BY", true)]
    public async Task RepositoryList_OperatorNamedProperty_UsesDirectStrategy(string property, bool hinted)
    {
        using CosmosClient client = new("https://localhost:8081", Convert.ToBase64String(new byte[64]));
        QueryTestContainer container = new() { QueryPages = [[new Entity()], [new Entity()]] };
        using TestGalaxy repository = new(client, container);

        (_, IList<Entity> results) = await ((IGalaxy<Entity>)repository).List(
            [new([new(property, "value")])], new([property]),
            hints: hinted ? new QueryHints(MaxConcurrency: 1) : null);

        Assert.Equal(2, results.Count);
        Assert.True(container.LastQueryOptions.EnableOptimisticDirectExecution);
    }

    [Theory]
    [InlineData(Q.Operator.VectorDistance)]
    [InlineData(Q.Operator.FTScore)]
    public async Task RepositoryGet_RankingOperation_RemainsBoundedWithRecommendedHints(Q.Operator op)
    {
        using CosmosClient client = new("https://localhost:8081", Convert.ToBase64String(new byte[64]));
        QueryTestContainer container = new() { QueryPages = [[], [new Entity { id = "first" }], [new Entity { id = "second" }]], ThrowOnQueryPage = 3 };
        using TestGalaxy repository = new(client, container);
        object value = op == Q.Operator.VectorDistance ? new float[] { 0.1f, 0.2f } : new[] { "term" };

        (Gravity gravity, Entity result) = await ((IGalaxy<Entity>)repository).Get([new([new("content", value, Operator: op)])], ["id"]);

        Assert.Equal("first", result.id);
        Assert.StartsWith("SELECT TOP 1 ", gravity.Query.Text);
        Assert.Contains("ORDER BY", gravity.Query.Text);
        Assert.Equal(2, container.QueryPageReads);
        Assert.False(container.LastQueryOptions.EnableOptimisticDirectExecution);
        Assert.Equal(1, container.LastQueryOptions.MaxItemCount);
        Assert.Equal(1, container.LastQueryOptions.MaxBufferedItemCount);
    }

    private static QueryContext Context(string strategy) => new(
        strategy == "VectorSearch" ? QueryType.VectorSearch : QueryType.Simple,
        Hints: new Dictionary<string, object> { [nameof(QueryHints.ForceStrategy)] = strategy });

    private sealed class TestGalaxy : Galaxy<Entity>
    {
        public TestGalaxy(CosmosClient client, Container container)
            : base(client, "db", "container", ["/Tenant"], new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache(), recordQueries: true)
            => typeof(GalaxyCore).GetField("_container", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(this, container);
    }

    private sealed record Entity : CosmicEntity
    {
        [PartitionKey]
        public string Tenant { get; set; }
    }

    private sealed class RecordingStorage : IQueryStatisticsStorage
    {
        public List<QueryExecutionStatistics> Saved { get; } = [];
        public Task SaveAsync(QueryExecutionStatistics stats) { Saved.Add(stats); return Task.CompletedTask; }
        public Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count) => Task.FromResult<IList<QueryExecutionStatistics>>([]);
        public Task<IList<QueryExecutionStatistics>> GetByQueryHashAsync(string queryHash, TimeSpan window) => Task.FromResult<IList<QueryExecutionStatistics>>([]);
        public Task ClearOldAsync(TimeSpan olderThan) => Task.CompletedTask;
    }
}
