using System.Net;
using System.Reflection;
using Microsoft.Azure.Cosmos;
using Universe.Attributes;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;
using Universe.Tests.Helpers;
using Xunit;

namespace Universe.Tests.Caching;

public sealed class DocumentCacheRepositoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PointGet_DifferentClients_DoNotShareDocumentsOrETags(bool differentAccount, bool arrayPartitionKey)
    {
        using CosmosClient firstClient = CreateClient();
        using CosmosClient secondClient = CreateClient(differentAccount ? "https://other-account:8081" : "https://localhost:8081");
        UniverseOptions options = new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache();
        QueryTestContainer firstContainer = new() { ReadItemResult = Entity("private-first"), ETag = "first-etag" };
        QueryTestContainer secondContainer = new() { ReadItemResult = Entity("private-second"), ETag = "second-etag" };
        using TestGalaxy first = new(firstContainer, options, client: firstClient);
        using TestGalaxy second = new(secondContainer, options, client: secondClient);

        await first.PointGet("id-1", "tenant-1");
        (Gravity gravity, CacheEntity result) = arrayPartitionKey
            ? await second.PointGetArray("id-1", ["tenant-1"])
            : await second.PointGet("id-1", "tenant-1");

        Assert.Equal("private-second", result.Name);
        Assert.Equal("second-etag", gravity.ETag);
        Assert.Equal(9.5, gravity.RU);
        Assert.Equal(1, secondContainer.ReadItemCalls);
    }

    [Fact]
    public async Task PointGet_DifferentClient_StillEnforcesContainerAuthorization()
    {
        UniverseOptions options = new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache();
        using TestGalaxy first = new(new() { ReadItemResult = Entity("private") }, options);
        QueryTestContainer denied = new() { ReadException = new CosmosException("Forbidden", HttpStatusCode.Forbidden, 0, "test", 0) };
        using TestGalaxy second = new(denied, options);
        await first.PointGet("id-1", "tenant-1");

        var exception = await Assert.ThrowsAsync<Universe.Exception.UniverseException>(() => second.PointGet("id-1", "tenant-1"));

        Assert.IsType<CosmosException>(exception.InnerException);
        Assert.Equal(1, denied.ReadItemCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryGet_DifferentClients_DoNotShareResults(bool projected)
    {
        UniverseOptions options = new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache();
        QueryTestContainer firstContainer = new() { QueryItems = projected ? [new CacheProjection { Name = "first" }] : [Entity("first")] };
        QueryTestContainer secondContainer = new() { QueryItems = projected ? [new CacheProjection { Name = "second" }] : [Entity("second")] };
        using TestGalaxy first = new(firstContainer, options);
        using TestGalaxy second = new(secondContainer, options);

        if (projected)
        {
            await first.QueryGetProjection();
            (Gravity gravity, CacheProjection result) = await second.QueryGetProjection();
            Assert.Equal("second", result.Name);
            Assert.Equal(7.25, gravity.RU);
        }
        else
        {
            await first.QueryGet("same-name");
            (Gravity gravity, CacheEntity result) = await second.QueryGet("same-name");
            Assert.Equal("second", result.Name);
            Assert.Equal(7.25, gravity.RU);
        }

        Assert.Equal(1, secondContainer.QueryCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Modify_ScopesSharedHitsAndInvalidationToClient(bool sameClient)
    {
        using CosmosClient firstClient = CreateClient();
        using CosmosClient otherClient = CreateClient();
        UniverseOptions options = new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache();
        QueryTestContainer firstContainer = new() { ReadItemResult = Entity("first"), QueryItems = [Entity("first")], ReplaceItemResult = Entity("updated") };
        QueryTestContainer secondContainer = new() { ReadItemResult = Entity("second"), QueryItems = [Entity("second")] };
        using TestGalaxy first = new(firstContainer, options, client: firstClient);
        using TestGalaxy second = new(secondContainer, options, client: sameClient ? firstClient : otherClient);
        await first.PointGet("id-1", "tenant-1");
        await first.QueryGet("same-name");
        await second.PointGet("id-1", "tenant-1");
        await second.QueryGet("same-name");
        Assert.Equal(sameClient ? 0 : 1, secondContainer.ReadItemCalls);
        Assert.Equal(sameClient ? 0 : 1, secondContainer.QueryCalls);

        await first.ModifyEntity(Entity("updated"));
        secondContainer.QueryItems = [Entity("query-after-write")];
        (Gravity pointGravity, CacheEntity point) = await second.PointGet("id-1", "tenant-1");
        (Gravity queryGravity, CacheEntity query) = await second.QueryGet("same-name");

        Assert.Equal(sameClient ? "updated" : "second", point.Name);
        Assert.Equal(0, pointGravity.RU);
        Assert.Equal(sameClient ? "query-after-write" : "second", query.Name);
        Assert.Equal(sameClient ? 7.25 : 0, queryGravity.RU);
    }

    private static CacheEntity Entity(string name) => new() { id = "id-1", TenantId = "tenant-1", Name = name };

    private static CosmosClient CreateClient(string endpoint = "https://localhost:8081")
        => new(endpoint, Convert.ToBase64String(new byte[64]), new CosmosClientOptions { AllowBulkExecution = true });

    [Fact]
    public async Task PointGet_CacheDisabled_ReadsContainerEveryTime()
    {
        QueryTestContainer container = new()
        {
            ReadItemResult = new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "from-cosmos" }
        };
        TestGalaxy repo = new(container, new UniverseOptions().WithAutoProvisioning(false));

        (_, CacheEntity first) = await repo.PointGet("id-1", "tenant-1");
        (_, CacheEntity second) = await repo.PointGet("id-1", "tenant-1");

        Assert.Equal("from-cosmos", first.Name);
        Assert.Equal("from-cosmos", second.Name);
        Assert.Equal(2, container.ReadItemCalls);
    }

    [Fact]
    public async Task PointGet_CacheEnabled_ReturnsSecondReadFromCache()
    {
        QueryTestContainer container = new()
        {
            ReadItemResult = new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "from-cosmos" }
        };
        TestGalaxy repo = new(container, new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache());

        (Gravity firstGravity, CacheEntity first) = await repo.PointGet("id-1", "tenant-1");
        first.Name = "caller-mutation";
        (Gravity secondGravity, CacheEntity second) = await repo.PointGet("id-1", "tenant-1");

        Assert.Equal(9.5, firstGravity.RU);
        Assert.Equal(0, secondGravity.RU);
        Assert.Equal("from-cosmos", second.Name);
        Assert.Equal(1, container.ReadItemCalls);
    }

    [Fact]
    public async Task PointGet_CacheEnabled_PreservesETag()
    {
        QueryTestContainer container = new()
        {
            ReadItemResult = new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "from-cosmos" }
        };
        TestGalaxy repo = new(container, new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache());

        (Gravity firstGravity, _) = await repo.PointGet("id-1", "tenant-1");
        (Gravity cachedGravity, _) = await repo.PointGet("id-1", "tenant-1");

        Assert.Equal("fake-etag", firstGravity.ETag);
        Assert.Equal("fake-etag", cachedGravity.ETag);
    }

    [Fact]
    public async Task QueryGet_CacheEnabled_UsesStableKeyAcrossGeneratedCatalystIds()
    {
        QueryTestContainer container = new()
        {
            QueryItems = [new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "from-cosmos" }]
        };
        TestGalaxy repo = new(container, new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache());

        (Gravity firstGravity, CacheEntity first) = await repo.QueryGet("same-name");
        (Gravity secondGravity, CacheEntity second) = await repo.QueryGet("same-name");

        Assert.Equal(7.25, firstGravity.RU);
        Assert.Equal(0, secondGravity.RU);
        Assert.Equal("from-cosmos", first.Name);
        Assert.Equal("from-cosmos", second.Name);
        Assert.Equal(1, container.QueryCalls);
    }

    [Fact]
    public async Task QueryGet_CacheHit_PreservesRecordedQueryDetails()
    {
        QueryTestContainer container = new()
        {
            QueryItems = [new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "from-cosmos" }]
        };
        TestGalaxy repo = new(container, new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache(), recordQueries: true);

        await repo.QueryGet("same-name");
        (Gravity secondGravity, _) = await repo.QueryGet("same-name");

        Assert.Equal(0, secondGravity.RU);
        Assert.StartsWith("SELECT", secondGravity.Query.Text);
        Assert.NotEmpty(secondGravity.Query.Parameters);
        Assert.Equal(1, container.QueryCalls);
    }

    [Fact]
    public async Task Modify_UpdatesPointReadCache()
    {
        QueryTestContainer container = new()
        {
            ReadItemResult = new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "before" },
            ReplaceItemResult = new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "after" }
        };
        TestGalaxy repo = new(container, new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache());

        await repo.PointGet("id-1", "tenant-1");
        await repo.ModifyEntity(new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "after" });
        (Gravity gravity, CacheEntity cached) = await repo.PointGet("id-1", "tenant-1");

        Assert.Equal(0, gravity.RU);
        Assert.Equal("after", cached.Name);
        Assert.Equal(1, container.ReadItemCalls);
    }

    [Fact]
    public async Task Modify_ClearsSingleQueryCacheForRepositoryScope()
    {
        QueryTestContainer container = new()
        {
            QueryItems = [new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "before" }],
            ReplaceItemResult = new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "after" }
        };
        TestGalaxy repo = new(container, new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache());

        await repo.QueryGet("same-name");
        await repo.QueryGet("same-name");
        await repo.ModifyEntity(new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "after" });
        container.QueryItems = [new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "after" }];
        (_, CacheEntity afterInvalidation) = await repo.QueryGet("same-name");

        Assert.Equal("after", afterInvalidation.Name);
        Assert.Equal(2, container.QueryCalls);
    }

    [Fact]
    public async Task Remove_InvalidatesPointReadCache()
    {
        QueryTestContainer container = new()
        {
            ReadItemResult = new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "before" }
        };
        TestGalaxy repo = new(container, new UniverseOptions().WithAutoProvisioning(false).WithDocumentCache());

        await repo.PointGet("id-1", "tenant-1");
        await repo.RemoveEntity("id-1", "tenant-1");
        container.ReadItemResult = new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "after" };
        (_, CacheEntity afterInvalidation) = await repo.PointGet("id-1", "tenant-1");

        Assert.Equal("after", afterInvalidation.Name);
        Assert.Equal(2, container.ReadItemCalls);
    }

    private sealed class TestGalaxy : Galaxy<CacheEntity>
    {
        public TestGalaxy(QueryTestContainer container, UniverseOptions options, bool recordQueries = false, CosmosClient client = null)
            : base(client ?? CreateClient(), "database", "container", typeof(CacheEntity).BuildPartitionKey(), options, recordQueries)
        {
            typeof(GalaxyCore)
                .GetField("_container", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(this, container);
        }

        public Task<(Gravity, CacheEntity)> PointGet(string id, string tenantId)
            => ((IGalaxyBasic<CacheEntity>)this).Get(id, tenantId);

        public Task<(Gravity, CacheEntity)> PointGetArray(string id, string[] partitionKeys)
            => ((IGalaxyBasic<CacheEntity>)this).Get(id, partitionKeys);

        public Task<(Gravity, CacheProjection)> QueryGetProjection()
            => ((IGalaxy<CacheEntity>)this).Get<CacheProjection>(null, ["Name"]);

        public Task<(Gravity, CacheEntity)> QueryGet(string name)
            => ((IGalaxy<CacheEntity>)this).Get([new([new("Name", name)])]);

        public Task<(Gravity, CacheEntity)> ModifyEntity(CacheEntity entity)
            => ((IGalaxyBasic<CacheEntity>)this).Modify(entity);

        public Task<Gravity> RemoveEntity(string id, string tenantId)
            => ((IGalaxyBasic<CacheEntity>)this).Remove(id, tenantId);

    }

    private sealed record CacheProjection
    {
        public string Name { get; set; }
    }

    private sealed record CacheEntity : CosmicEntity
    {
        [PartitionKey]
        public string TenantId { get; set; }

        public string Name { get; set; }
    }

}
