using Universe.Builder.Caching;
using Universe.Attributes;
using Universe.Interfaces;
using Xunit;

namespace Universe.Tests.Caching;

public sealed class DocumentCacheTests
{
    [Fact]
    public void PointKey_DoesNotContainReadableInputs()
    {
        DocumentCache cache = new(new(TimeSpan.FromMinutes(5), 1000, cloneDocuments: true));

        DocumentCacheKey key = cache.CreatePointKey(
            database: "db-secret",
            container: "container-secret",
            sourceType: typeof(CacheEntity),
            resultType: typeof(CacheEntity),
            id: "document-secret",
            partitionKeys: ["tenant-secret"]);

        Assert.DoesNotContain("db-secret", key.Value);
        Assert.DoesNotContain("container-secret", key.Value);
        Assert.DoesNotContain("document-secret", key.Value);
        Assert.DoesNotContain("tenant-secret", key.Value);
        Assert.Equal(64, key.Value.Length);
    }

    [Fact]
    public void TryGet_ReturnsFalseAfterExpiration()
    {
        DocumentCache cache = new(new(TimeSpan.FromMilliseconds(1), 1000, cloneDocuments: true));
        DocumentCacheKey key = cache.CreatePointKey("db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-1", ["tenant-1"]);

        cache.Set(key, DocumentCacheOperation.PointRead, scopeHash: "scope", new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "cached" });
        Thread.Sleep(20);

        bool found = cache.TryGet<CacheEntity>(key, out CacheEntity value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_ClonesCachedValueByDefault()
    {
        DocumentCache cache = new(new(TimeSpan.FromMinutes(5), 1000, cloneDocuments: true));
        DocumentCacheKey key = cache.CreatePointKey("db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-1", ["tenant-1"]);

        cache.Set(key, DocumentCacheOperation.PointRead, scopeHash: "scope", new CacheEntity { id = "id-1", TenantId = "tenant-1", Name = "cached" });

        Assert.True(cache.TryGet(key, out CacheEntity first));
        first.Name = "mutated";
        Assert.True(cache.TryGet(key, out CacheEntity second));

        Assert.Equal("cached", second.Name);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void TryGet_ReturnsSameInstanceWhenCloningDisabled()
    {
        DocumentCache cache = new(new(TimeSpan.FromMinutes(5), 1000, cloneDocuments: false));
        DocumentCacheKey key = cache.CreatePointKey("db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-1", ["tenant-1"]);
        CacheEntity entity = new() { id = "id-1", TenantId = "tenant-1", Name = "cached" };

        cache.Set(key, DocumentCacheOperation.PointRead, scopeHash: "scope", entity);

        Assert.True(cache.TryGet(key, out CacheEntity first));
        Assert.True(cache.TryGet(key, out CacheEntity second));

        Assert.Same(first, second);
    }

    [Fact]
    public void Set_EvictsOldestEntryWhenMaxEntriesExceeded()
    {
        DocumentCache cache = new(new(TimeSpan.FromMinutes(5), 1, cloneDocuments: true));
        DocumentCacheKey firstKey = cache.CreatePointKey("db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-1", ["tenant-1"]);
        DocumentCacheKey secondKey = cache.CreatePointKey("db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-2", ["tenant-1"]);

        cache.Set(firstKey, DocumentCacheOperation.PointRead, scopeHash: "scope", new CacheEntity { id = "id-1", TenantId = "tenant-1" });
        cache.Set(secondKey, DocumentCacheOperation.PointRead, scopeHash: "scope", new CacheEntity { id = "id-2", TenantId = "tenant-1" });

        Assert.False(cache.TryGet(firstKey, out CacheEntity first));
        Assert.True(cache.TryGet(secondKey, out CacheEntity second));
        Assert.Equal("id-2", second.id);
    }

    [Fact]
    public void ClearQueries_RemovesOnlyQueryEntriesForScope()
    {
        DocumentCache cache = new(new(TimeSpan.FromMinutes(5), 1000, cloneDocuments: true));
        DocumentCacheKey queryKey = cache.CreateQueryKey("db", "container", typeof(CacheEntity), typeof(CacheEntity), "SELECT * FROM c", []);
        DocumentCacheKey pointKey = cache.CreatePointKey("db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-1", ["tenant-1"]);

        string scopeHash = cache.CreateScopeHash("db", "container", typeof(CacheEntity));
        cache.Set(queryKey, DocumentCacheOperation.SingleQuery, scopeHash, new CacheEntity { id = "id-1", TenantId = "tenant-1" });
        cache.Set(pointKey, DocumentCacheOperation.PointRead, scopeHash, new CacheEntity { id = "id-1", TenantId = "tenant-1" });

        cache.ClearQueries(scopeHash);

        Assert.False(cache.TryGet(queryKey, out CacheEntity queryValue));
        Assert.True(cache.TryGet(pointKey, out CacheEntity pointValue));
    }

    [Fact]
    public void QueryKey_NormalizesGeneratedParameterNames()
    {
        DocumentCache cache = new(new(TimeSpan.FromMinutes(5), 1000, cloneDocuments: true));

        DocumentCacheKey first = cache.CreateQueryKey(
            "db",
            "container",
            typeof(CacheEntity),
            typeof(CacheEntity),
            "SELECT * FROM c WHERE c[\"name\"] = @name018f1",
            [("@name018f1", "value")]);
        DocumentCacheKey second = cache.CreateQueryKey(
            "db",
            "container",
            typeof(CacheEntity),
            typeof(CacheEntity),
            "SELECT * FROM c WHERE c[\"name\"] = @name999f2",
            [("@name999f2", "value")]);

        Assert.Equal(first, second);
    }

    private sealed record CacheEntity : CosmicEntity
    {
        [PartitionKey]
        public string TenantId { get; set; }

        public string Name { get; set; }
    }
}
