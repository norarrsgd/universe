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
    public void Set_ExactByteBoundaryExpiresAndRemovesAccounting()
    {
        DocumentCache cache = new(new(TimeSpan.FromMilliseconds(10), 10, false, 5, 5));
        DocumentCacheKey key = new("key");
        cache.Set(key, DocumentCacheOperation.SingleQuery, "scope", "abc");
        Assert.True(cache.TryGet(key, out string _));
        Thread.Sleep(20);
        Assert.False(cache.TryGet(key, out string _));
        cache.Set(key, DocumentCacheOperation.SingleQuery, "scope", "abc");
        Assert.True(cache.TryGet(key, out string _));
        cache.ClearQueries("scope");
        cache.Set(new("next"), DocumentCacheOperation.PointRead, "scope", "abc");
        Assert.True(cache.TryGet(new("next"), out string _));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public void Options_RejectNonpositiveByteBudgets(int entryBytes, long totalBytes)
    {
        Assert.Throws<Universe.Exception.UniverseException>(() => new DocumentCacheOptions(null, 10, true, entryBytes, totalBytes));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Set_EnforcesSerializedByteLimitsAndReplacementAccounting(bool clone)
    {
        DocumentCache cache = new(new(null, 10, clone, maxEntryBytes: 100, maxTotalBytes: 100));
        DocumentCacheKey first = new("first");
        DocumentCacheKey second = new("second");
        cache.Set(first, DocumentCacheOperation.PointRead, "scope", new string('a', 60));
        cache.Set(second, DocumentCacheOperation.PointRead, "scope", new string('b', 60));
        Assert.False(cache.TryGet(first, out string _));
        Assert.True(cache.TryGet(second, out string _));
        cache.Set(second, DocumentCacheOperation.PointRead, "scope", "small");
        cache.Set(first, DocumentCacheOperation.PointRead, "scope", new string('a', 60));
        Assert.True(cache.TryGet(second, out string _));
        cache.Set(first, DocumentCacheOperation.PointRead, "scope", new string('a', 101));
        Assert.False(cache.TryGet(first, out string _));
        cache.Remove(second);
        cache.Set(first, DocumentCacheOperation.PointRead, "scope", new string('a', 90));
        Assert.True(cache.TryGet(first, out string _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Set_ReadFillCannotUndoMutation(bool query)
    {
        DocumentCache cache = new(new());
        DocumentCacheKey key = new("key");
        DocumentCacheOperation operation = query ? DocumentCacheOperation.SingleQuery : DocumentCacheOperation.PointRead;
        long generation = cache.CaptureGeneration("scope");
        cache.Set(key, operation, "scope", "updated");
        cache.Set(key, operation, "scope", "stale", expectedGeneration: generation);
        Assert.True(cache.TryGet(key, out string actual));
        Assert.Equal("updated", actual);
        generation = cache.CaptureGeneration("scope");
        cache.Remove(key);
        cache.ClearQueries("scope");
        cache.Set(key, operation, "scope", "stale", expectedGeneration: generation);
        Assert.False(cache.TryGet(key, out string _));
    }

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
    public void QueryKey_ComplexValues_CannotProduceCacheEntries()
    {
        DocumentCache cache = new(new());
        object[] values =
        [
            new { userId = "alice" },
            new CacheEntity { Name = "alice" },
            new[] { new { userId = "alice" } },
            new Dictionary<string, object> { ["userId"] = "alice" },
            JsonSerializer.SerializeToElement(new { userId = "alice" })
        ];

        foreach (object value in values)
            Assert.Throws<NotSupportedException>(() => QueryKey(cache, "SELECT * FROM c WHERE c.shares = @a", [("@a", value)]));
    }

    [Fact]
    public void QueryKey_PreservesScalarRuntimeTypes()
    {
        DocumentCache cache = new(new());
        const string sql = "SELECT * FROM c WHERE c.value = @a";
        DocumentCacheKey integer = QueryKey(cache, sql, [("@a", 1)]);
        DocumentCacheKey floating = QueryKey(cache, sql, [("@a", 1.0)]);
        DocumentCacheKey text = QueryKey(cache, sql, [("@a", "1")]);
        DocumentCacheKey missing = QueryKey(cache, sql, [("@a", null)]);

        Assert.NotEqual(integer, floating);
        Assert.NotEqual(integer, text);
        Assert.NotEqual(missing, QueryKey(cache, sql, [("@a", "null")]));
    }

    [Fact]
    public void QueryKey_DistinguishesUnpairedSurrogatesInStrings()
    {
        DocumentCache cache = new(new());
        const string sql = "SELECT * FROM c WHERE c.value = @a";

        DocumentCacheKey first = QueryKey(cache, sql, [("@a", "\uD800")]);
        DocumentCacheKey second = QueryKey(cache, sql, [("@a", "\uD801")]);
        DocumentCacheKey replacement = QueryKey(cache, sql, [("@a", "\uFFFD")]);

        Assert.NotEqual(first, second);
        Assert.NotEqual(first, replacement);
    }

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
