using Universe.Builder.Caching;
using Universe.Attributes;
using Universe.Interfaces;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Universe.Tests.Caching;

public sealed class DocumentCacheTests
{
    [Fact]
    public async Task CloneFailure_DoesNotEvictAConcurrentReplacement()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DocumentCache cache = new(new());
        DocumentCacheKey key = new("replacement");
        cache.Set(key, DocumentCacheOperation.PointRead, "scope", new CloneRaceEntity("before"));
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        int writes = 0;
        CloneRaceConverter.OnWrite = () =>
        {
            if (Interlocked.Increment(ref writes) != 1)
                return;
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10), cancellationToken))
                throw new TimeoutException("Replacement did not complete.");
            throw new JsonException("Old snapshot cannot be cloned.");
        };
        Task<bool> oldRead = Task.Run(() => cache.TryGet<CloneRaceEntity>(key, out _), cancellationToken);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), cancellationToken));
            cache.Set(key, DocumentCacheOperation.PointRead, "scope", new CloneRaceEntity("after"), "new-etag");
            release.Set();
            Assert.False(await oldRead.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken));

            Assert.True(cache.TryGet(key, out CloneRaceEntity replacement, out string eTag));
            Assert.Equal("after", replacement.Name);
            Assert.Equal("new-etag", eTag);
            Assert.Equal(1, CacheCount(cache, "_insertionOrder"));
        }
        finally
        {
            release.Set();
            await oldRead.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            CloneRaceConverter.OnWrite = null;
        }
    }

    [JsonConverter(typeof(CloneRaceConverter))]
    private sealed record CloneRaceEntity(string Name);

    private sealed class CloneRaceConverter : JsonConverter<CloneRaceEntity>
    {
        public static Action OnWrite;

        public override CloneRaceEntity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, CloneRaceEntity value, JsonSerializerOptions options)
        {
            OnWrite?.Invoke();
            writer.WriteStringValue(value.Name);
        }
    }

    [Fact]
    public void Set_RepeatedOverwrites_KeepEvictionMetadataBoundedAndRefreshValue()
    {
        DocumentCache cache = new(new(maxEntries: 2));
        DocumentCacheKey key = new("same-key");
        for (int index = 0; index < 1000; index++)
            cache.Set(key, DocumentCacheOperation.PointRead, "scope", new CacheEntity { Name = index.ToString() }, index.ToString());

        Assert.Equal(1, CacheCount(cache, "_entries"));
        Assert.Equal(1, CacheCount(cache, "_insertionOrder"));
        Assert.True(cache.TryGet(key, out CacheEntity result, out string eTag));
        Assert.Equal("999", result.Name);
        Assert.Equal("999", eTag);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemoveAndInvalidation_ReleaseEvictionMetadata(bool invalidate)
    {
        DocumentCache cache = new(new());
        for (int index = 0; index < 100; index++)
        {
            DocumentCacheKey key = new(index.ToString());
            cache.Set(key, DocumentCacheOperation.SingleQuery, "scope", new CacheEntity());
            if (invalidate)
                cache.ClearQueries("scope");
            else
                cache.Remove(key);
        }

        Assert.Equal(0, CacheCount(cache, "_entries"));
        Assert.Equal(0, CacheCount(cache, "_insertionOrder"));
    }

    [Fact]
    public void Expiration_ReleasesEvictionMetadata()
    {
        DocumentCache cache = new(new(TimeSpan.FromMilliseconds(1)));
        DocumentCacheKey key = new("expired");
        cache.Set(key, DocumentCacheOperation.PointRead, "scope", new CacheEntity());
        Thread.Sleep(20);

        Assert.False(cache.TryGet<CacheEntity>(key, out _));
        Assert.Equal(0, CacheCount(cache, "_insertionOrder"));
    }

    [Fact]
    public void Set_OverwritesPreserveFifoAndReinsertionsJoinTail()
    {
        DocumentCache cache = new(new(maxEntries: 2));
        DocumentCacheKey first = new("first");
        DocumentCacheKey second = new("second");
        DocumentCacheKey third = new("third");
        cache.Set(first, DocumentCacheOperation.PointRead, "scope", new CacheEntity());
        cache.Set(second, DocumentCacheOperation.PointRead, "scope", new CacheEntity());
        cache.Set(first, DocumentCacheOperation.PointRead, "scope", new CacheEntity());
        cache.Set(third, DocumentCacheOperation.PointRead, "scope", new CacheEntity());
        Assert.False(cache.TryGet<CacheEntity>(first, out _));
        Assert.True(cache.TryGet<CacheEntity>(second, out _));

        cache.Remove(second);
        cache.Set(second, DocumentCacheOperation.PointRead, "scope", new CacheEntity());
        cache.Set(first, DocumentCacheOperation.PointRead, "scope", new CacheEntity());
        Assert.False(cache.TryGet<CacheEntity>(third, out _));
        Assert.True(cache.TryGet<CacheEntity>(second, out _));
    }

    [Fact]
    public void ConcurrentMutations_KeepEntriesAndMetadataBounded()
    {
        DocumentCache cache = new(new(maxEntries: 4, cloneDocuments: false));
        Parallel.For(0, 2000, index =>
        {
            DocumentCacheKey key = new((index % 8).ToString());
            cache.Set(key, DocumentCacheOperation.SingleQuery, "scope", new CacheEntity());
            cache.TryGet<CacheEntity>(key, out _);
            if (index % 3 == 0)
                cache.Remove(key);
            if (index % 7 == 0)
                cache.ClearQueries("scope");
        });

        Assert.InRange(CacheCount(cache, "_entries"), 0, 4);
        Assert.Equal(CacheCount(cache, "_entries"), CacheCount(cache, "_insertionOrder"));
        cache.ClearQueries("scope");
        Assert.Equal(0, CacheCount(cache, "_insertionOrder"));
    }

    private static int CacheCount(DocumentCache cache, string fieldName)
    {
        object collection = typeof(DocumentCache).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache)!;
        return (int)collection.GetType().GetProperty("Count")!.GetValue(collection)!;
    }

    [Theory]
    [InlineData("SELECT * FROM c WHERE c[\"access@a\"] = @a", "SELECT * FROM c WHERE c[\"access@b\"] = @b")]
    [InlineData("SELECT '@a' FROM c WHERE c.id = @a", "SELECT '@b' FROM c WHERE c.id = @b")]
    [InlineData("SELECT * FROM c WHERE c[\"escaped\\\"@a\"] = @a", "SELECT * FROM c WHERE c[\"escaped\\\"@b\"] = @b")]
    public void QueryKey_PreservesQuotedParameterLikeText(string firstSql, string secondSql)
    {
        DocumentCache cache = new(new());

        DocumentCacheKey first = QueryKey(cache, firstSql, [("@a", true)]);
        DocumentCacheKey second = QueryKey(cache, secondSql, [("@b", true)]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void QueryKey_UsesBindingOrderRatherThanParameterEnumeration()
    {
        DocumentCache cache = new(new());
        const string sql = "SELECT * FROM c WHERE c.id = @a AND c.tenant = @b";

        DocumentCacheKey first = QueryKey(cache, sql, [("@a", "id"), ("@b", "tenant")]);
        DocumentCacheKey reordered = QueryKey(cache, sql, [("@b", "tenant"), ("@a", "id")]);
        DocumentCacheKey swapped = QueryKey(cache, sql, [("@b", "id"), ("@a", "tenant")]);

        Assert.Equal(first, reordered);
        Assert.NotEqual(first, swapped);
    }

    [Fact]
    public void QueryKey_PreservesRepeatedParameterValues()
    {
        DocumentCache cache = new(new());
        DocumentCacheKey repeated = QueryKey(cache, "SELECT * FROM c WHERE c.a = @a OR c.b = @a", [("@a", 1)]);
        DocumentCacheKey equivalent = QueryKey(cache, "SELECT * FROM c WHERE c.a = @x OR c.b = @y", [("@x", 1), ("@y", 1)]);
        DocumentCacheKey different = QueryKey(cache, "SELECT * FROM c WHERE c.a = @x OR c.b = @y", [("@x", 1), ("@y", 2)]);

        Assert.Equal(repeated, equivalent);
        Assert.NotEqual(repeated, different);
    }

    [Fact]
    public void QueryKey_MissingBinding_CannotProduceCacheEntry()
    {
        DocumentCache cache = new(new());

        Assert.Throws<InvalidOperationException>(() => QueryKey(cache, "SELECT * FROM c WHERE c.id = @missing", []));
    }

    private static DocumentCacheKey QueryKey(DocumentCache cache, string sql, IReadOnlyList<(string, object)> parameters)
        => cache.CreateQueryKey("client", "db", "container", typeof(CacheEntity), typeof(CacheEntity), sql, parameters);

    [Fact]
    public void PointKey_DoesNotContainReadableInputs()
    {
        DocumentCache cache = new(new(TimeSpan.FromMinutes(5), 1000, cloneDocuments: true));

        DocumentCacheKey key = cache.CreatePointKey("client",
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
        DocumentCacheKey key = cache.CreatePointKey("client", "db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-1", ["tenant-1"]);

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
        DocumentCacheKey key = cache.CreatePointKey("client", "db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-1", ["tenant-1"]);

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
        DocumentCacheKey key = cache.CreatePointKey("client", "db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-1", ["tenant-1"]);
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
        DocumentCacheKey firstKey = cache.CreatePointKey("client", "db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-1", ["tenant-1"]);
        DocumentCacheKey secondKey = cache.CreatePointKey("client", "db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-2", ["tenant-1"]);

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
        DocumentCacheKey queryKey = cache.CreateQueryKey("client", "db", "container", typeof(CacheEntity), typeof(CacheEntity), "SELECT * FROM c", []);
        DocumentCacheKey pointKey = cache.CreatePointKey("client", "db", "container", typeof(CacheEntity), typeof(CacheEntity), "id-1", ["tenant-1"]);

        string scopeHash = cache.CreateScopeHash("client", "db", "container", typeof(CacheEntity));
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

        DocumentCacheKey first = cache.CreateQueryKey("client",
            "db",
            "container",
            typeof(CacheEntity),
            typeof(CacheEntity),
            "SELECT * FROM c WHERE c[\"name\"] = @name018f1",
            [("@name018f1", "value")]);
        DocumentCacheKey second = cache.CreateQueryKey("client",
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
