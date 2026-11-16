using System.Net;
using System.Reflection;
using Microsoft.Azure.Cosmos;
using Universe.Attributes;
using Universe.Builder;
using Universe.Builder.Options;
using Universe.Builder.Strategies;
using Universe.Exception;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;
using Universe.Tests.Helpers;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class ReadConsistencyTests
{
    [Theory]
    [InlineData(ConsistencyLevel.Strong)]
    [InlineData(ConsistencyLevel.BoundedStaleness)]
    [InlineData(ConsistencyLevel.Session)]
    [InlineData(ConsistencyLevel.ConsistentPrefix)]
    [InlineData(ConsistencyLevel.Eventual)]
    public async Task PointOverrides_BypassCacheWithoutRefreshingIt(ConsistencyLevel level)
    {
        using CosmosClient client = Client();
        QueryTestContainer container = new() { ReadItemResult = new Entity { id = "cached" } };
        using TestGalaxy repository = new(client, container);
        IGalaxyBasic<Entity> galaxy = repository;
        await galaxy.Get("id", "tenant");
        container.ReadItemResult = new Entity { id = "fresh" };
        (_, Entity fresh) = await galaxy.Get("id", ["tenant"], new ReadOptions(level));
        Assert.Equal("fresh", fresh.id);
        Assert.Equal(level, container.LastItemOptions.ConsistencyLevel);
        (_, Entity cached) = await galaxy.Get("id", "tenant");
        Assert.Equal("cached", cached.id);
        Assert.Equal(2, container.ReadItemCalls);
    }

    [Theory]
    [InlineData("Direct", QueryType.Simple)]
    [InlineData("Gateway", QueryType.Aggregation)]
    [InlineData("VectorSearch", QueryType.VectorSearch)]
    public async Task EveryStrategy_RetainsOverrideThroughRecommendationMerging(string name, QueryType type)
    {
        using UniverseBuilder builder = new();
        QueryTestContainer container = new() { QueryItems = [1] };
        QueryContext context = new(type, Hints: new Dictionary<string, object> { [nameof(QueryHints.ForceStrategy)] = name }, ConsistencyLevel: ConsistencyLevel.Strong);
        await builder.GetListFromQuery<int>(container, new QueryDefinition("SELECT * FROM c"), context);
        Assert.Equal(ConsistencyLevel.Strong, container.LastQueryOptions.ConsistencyLevel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryGetOverride_BypassesCacheAndUsesProjection(bool generic)
    {
        using CosmosClient client = Client();
        QueryTestContainer container = new() { QueryItems = [new Entity { id = "cached" }] };
        using TestGalaxy repository = new(client, container);
        IGalaxy<Entity> galaxy = repository;
        await galaxy.Query().GetAsync();
        container.QueryItems = [new Entity { id = "fresh" }];
        Orbit<Entity> orbit = galaxy.Query().WithConsistency(ConsistencyLevel.Strong);
        Entity result;
        if (generic)
            (_, result) = await orbit.GetAsync<Entity>();
        else
            (_, result) = await orbit.GetAsync();
        Assert.Equal("fresh", result.id);
        Assert.Equal(ConsistencyLevel.Strong, container.LastQueryOptions.ConsistencyLevel);
        (_, Entity cached) = await galaxy.Query().GetAsync();
        Assert.Equal("cached", cached.id);
        Assert.Equal(2, container.QueryCalls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ListAndPaging_PropagateProjectionAndConsistency(bool paged, bool generic)
    {
        using CosmosClient client = Client();
        QueryTestContainer container = new() { QueryItems = [new Entity { id = "first" }] };
        using TestGalaxy repository = new(client, container);
        Orbit<Entity> orbit = ((IGalaxy<Entity>)repository).Query().Project(p => p.Field("id", "id")).WithConsistency(ConsistencyLevel.Session);
        if (paged)
            orbit.Paged(3, "next-token");
        if (generic)
            await orbit.ToListAsync<Entity>();
        else
            await orbit.ToListAsync();
        Assert.Equal(ConsistencyLevel.Session, container.LastQueryOptions.ConsistencyLevel);
        Assert.Contains("AS id", container.LastQuery.QueryText);
        if (paged)
        {
            Assert.Equal(3, container.LastQueryOptions.MaxItemCount);
            Assert.Equal("next-token", container.LastContinuationToken);
        }
    }

    [Fact]
    public async Task ProjectedGet_HonorsFullShapeAndStopsAtFirstResult()
    {
        using CosmosClient client = Client();
        QueryTestContainer container = new() { QueryPages = [[], [new Entity { id = "first" }], [new Entity()]], ThrowOnQueryPage = 3 };
        using TestGalaxy repository = new(client, container);
        IGalaxy<Entity> galaxy = repository;
        (_, Entity result) = await galaxy.Query().Project(p => p.Field("id", "id"))
            .Join("items", "item").OrderByDescending("id").Top(5).WithHints(new(MaxBufferedItemCount: 100))
            .WithConsistency(ConsistencyLevel.Strong).GetAsync();
        Assert.Equal("first", result.id);
        Assert.Contains("TOP 5", container.LastQuery.QueryText);
        Assert.Contains("JOIN item", container.LastQuery.QueryText);
        Assert.Contains("ORDER BY c[\"id\"] DESC", container.LastQuery.QueryText);
        Assert.Equal(1, container.LastQueryOptions.MaxBufferedItemCount);
        Assert.Equal(2, container.QueryPageReads);
        Assert.True(container.QueryIteratorDisposed);
        await Assert.ThrowsAsync<UniverseException>(() => galaxy.Query().Project(p => p.Field("id", "id")).Paged(1).GetAsync());
    }

    [Fact]
    public async Task InvalidAndConflictingOverrides_FailBeforeCacheAndUnsupportedStrongRetainsCause()
    {
        using CosmosClient client = Client();
        QueryTestContainer container = new() { ReadItemResult = new Entity() };
        using TestGalaxy repository = new(client, container);
        IGalaxy<Entity> galaxy = repository;
        await Assert.ThrowsAsync<UniverseException>(() => galaxy.Get("id", ["tenant"], new((ConsistencyLevel)999)));
        Assert.Equal(0, container.ReadItemCalls);
        CosmosException unsupported = new("Strong not supported", HttpStatusCode.BadRequest, 0, "activity", 0);
        container.ReadException = unsupported;
        UniverseException failure = await Assert.ThrowsAsync<UniverseException>(() => galaxy.Get("id", ["tenant"], new(ConsistencyLevel.Strong)));
        Assert.Same(unsupported, failure.InnerException);
        using CosmosClient advanced = Client(new CosmosClientOptions { ReadConsistencyStrategy = ReadConsistencyStrategy.Eventual });
        using TestGalaxy conflicting = new(advanced, container);
        await Assert.ThrowsAsync<UniverseException>(() => ((IGalaxy<Entity>)conflicting).Query().WithConsistency(ConsistencyLevel.Strong).ToListAsync());
    }

    [Fact]
    public async Task SeparateOrbits_DoNotLeakConsistencyToDefaultReads()
    {
        using CosmosClient client = Client();
        QueryTestContainer container = new() { QueryItems = [new Entity()] };
        using TestGalaxy repository = new(client, container);
        IGalaxy<Entity> galaxy = repository;
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        container.QueryReadGate = gate.Task;
        Task first = galaxy.Query().WithConsistency(ConsistencyLevel.Strong).ToListAsync();
        Task second = galaxy.Query().WithConsistency(ConsistencyLevel.Eventual).ToListAsync();
        gate.SetResult();
        await Task.WhenAll(first, second);
        Assert.Contains(container.QueryOptionsHistory, options => options.ConsistencyLevel == ConsistencyLevel.Strong);
        Assert.Contains(container.QueryOptionsHistory, options => options.ConsistencyLevel == ConsistencyLevel.Eventual);
        Assert.NotSame(container.QueryOptionsHistory[0], container.QueryOptionsHistory[1]);
        await galaxy.Query().ToListAsync();
        Assert.Null(container.LastQueryOptions.ConsistencyLevel);
        Assert.Null(client.ClientOptions.ConsistencyLevel);
    }

    private static CosmosClient Client(CosmosClientOptions options = null) => new("https://localhost:8081", Convert.ToBase64String(new byte[64]), options);

    [Fact]
    public async Task ColdOverrides_DoNotPopulateCacheAndDefaultReadsStillCache()
    {
        using CosmosClient client = Client();
        QueryTestContainer container = new() { QueryItems = [new Entity { id = "override" }], ReadItemResult = new Entity { id = "override" } };
        using TestGalaxy repository = new(client, container);
        IGalaxy<Entity> galaxy = repository;
        await galaxy.Get("id", ["tenant"], new(ConsistencyLevel.Eventual));
        await galaxy.Query().WithConsistency(ConsistencyLevel.Eventual).GetAsync();
        container.ReadItemResult = new Entity { id = "default" };
        container.QueryItems = [new Entity { id = "default" }];
        (_, Entity point) = await galaxy.Get("id", ["tenant"], new ReadOptions());
        (_, Entity query) = await galaxy.Get(null, null, null, null, null, new ReadOptions());
        Assert.Equal("default", point.id);
        Assert.Equal("default", query.id);
        await galaxy.Get("id", "tenant");
        await galaxy.Query().GetAsync();
        Assert.Equal(2, container.ReadItemCalls);
        Assert.Equal(2, container.QueryCalls);
    }

    [Fact]
    public async Task ProjectedGetGeneric_GroupedAggregateHonorsShapeAndLegacyGetStillIgnoresShaping()
    {
        using CosmosClient client = Client();
        QueryTestContainer container = new() { QueryItems = [new Entity()] };
        using TestGalaxy repository = new(client, container);
        IGalaxy<Entity> galaxy = repository;
        await galaxy.Query().Project(p => p.Object("data", d => d.Field("tenant", "Tenant").Aggregate("count", "id", Q.Aggregate.Count))).GetAsync<Entity>();
        Assert.Contains("COUNT(1)", container.LastQuery.QueryText);
        Assert.Contains("GROUP BY c[\"Tenant\"]", container.LastQuery.QueryText);
        Assert.False(container.LastQueryOptions.EnableOptimisticDirectExecution);
        await galaxy.Query().Join("items", "item").GroupBy("Tenant").Aggregate("id", Q.Aggregate.Count).OrderBy("Tenant").Top(4).GetAsync();
        Assert.Equal("SELECT TOP 1 * FROM c", container.LastQuery.QueryText);
    }

    private sealed class TestGalaxy : Galaxy<Entity>
    {
        public TestGalaxy(CosmosClient client, Container container)
            : base(client, "db", "container", ["/Tenant"], new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache(), recordQueries: true)
            => typeof(GalaxyCore).GetField("_container", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, container);
    }

    private sealed record Entity : CosmicEntity
    {
        [PartitionKey]
        public string Tenant { get; set; } = "tenant";
    }
}
