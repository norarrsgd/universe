using Universe.Builder.Strategies.Storage;
using Universe.Exception;
using Xunit;

namespace Universe.Tests.Storage;

public sealed class UniverseOptionsTests : IDisposable
{
    private readonly List<string> _paths = [];

    public void Dispose()
    {
        foreach (string path in _paths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);

                string walPath = $"{path}-wal";
                if (File.Exists(walPath))
                    File.Delete(walPath);

                string shmPath = $"{path}-shm";
                if (File.Exists(shmPath))
                    File.Delete(shmPath);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    [Fact]
    public void DefaultOptions_EnableAutoProvisioning()
    {
        UniverseOptions options = new();

        Assert.True(options.AutoProvisionContainers);
    }

    [Fact]
    public void DefaultOptions_DisableDocumentCache()
    {
        UniverseOptions options = new();

        Assert.Null(options.DocumentCache);
    }

    [Fact]
    public void WithDocumentCache_EnablesDocumentCacheWithDefaults()
    {
        UniverseOptions options = new UniverseOptions().WithDocumentCache();

        Assert.NotNull(options.DocumentCache);
        Assert.Equal(TimeSpan.FromMinutes(5), options.DocumentCache.TimeToLive);
        Assert.Equal(1000, options.DocumentCache.MaxEntries);
        Assert.True(options.DocumentCache.CloneDocuments);
    }

    [Fact]
    public void WithDocumentCache_AcceptsCustomOptions()
    {
        UniverseOptions options = new UniverseOptions().WithDocumentCache(
            timeToLive: TimeSpan.FromSeconds(30),
            maxEntries: 25,
            cloneDocuments: false);

        Assert.NotNull(options.DocumentCache);
        Assert.Equal(TimeSpan.FromSeconds(30), options.DocumentCache.TimeToLive);
        Assert.Equal(25, options.DocumentCache.MaxEntries);
        Assert.False(options.DocumentCache.CloneDocuments);
    }

    [Fact]
    public void WithoutDocumentCache_DisablesDocumentCache()
    {
        UniverseOptions options = new UniverseOptions()
            .WithDocumentCache()
            .WithoutDocumentCache();

        Assert.Null(options.DocumentCache);
    }

    [Fact]
    public void WithDocumentCache_InvalidTimeToLive_ThrowsUniverseException()
    {
        UniverseException exception = Assert.Throws<UniverseException>(
            () => new UniverseOptions().WithDocumentCache(TimeSpan.Zero));

        Assert.Equal("Document cache time-to-live must be greater than zero.", exception.Message);
    }

    [Fact]
    public void WithDocumentCache_InvalidMaxEntries_ThrowsUniverseException()
    {
        UniverseException exception = Assert.Throws<UniverseException>(
            () => new UniverseOptions().WithDocumentCache(maxEntries: 0));

        Assert.Equal("Document cache max entries must be greater than zero.", exception.Message);
    }

    [Fact]
    public void WithAutoProvisioningFalse_DisablesAutoProvisioning()
    {
        UniverseOptions options = new UniverseOptions().WithAutoProvisioning(false);

        Assert.False(options.AutoProvisionContainers);
    }

    [Fact]
    public void GalaxyCore_NullOptions_ThrowsUniverseException()
    {
        UniverseException exception = Assert.Throws<UniverseException>(() => new NullOptionsGalaxyCore());

        Assert.Equal("Universe options are required.", exception.Message);
    }

    [Fact]
    public void FilePersistence_WithAutoProvisioningFalse_PreservesStorage()
    {
        string path = Track(Path.Combine(AppContext.BaseDirectory, $"options-{Guid.NewGuid()}.json"));

        UniverseOptions options = UniverseOptions.WithFilePersistence(path);
        try
        {
            options = options.WithAutoProvisioning(false);

            Assert.False(options.AutoProvisionContainers);
            Assert.IsType<FileStatisticsStorage>(options.StatisticsStorage);
        }
        finally
        {
            DisposeStatisticsStorage(options);
        }
    }

    [Fact]
    public void FilePersistence_WithDocumentCache_PreservesStorage()
    {
        string path = Track(Path.Combine(AppContext.BaseDirectory, $"options-{Guid.NewGuid()}.json"));

        UniverseOptions options = UniverseOptions.WithFilePersistence(path);
        try
        {
            options = options.WithDocumentCache(TimeSpan.FromSeconds(15), 10);

            Assert.NotNull(options.DocumentCache);
            Assert.IsType<FileStatisticsStorage>(options.StatisticsStorage);
        }
        finally
        {
            DisposeStatisticsStorage(options);
        }
    }

    [Fact]
    public void SqlitePersistence_WithAutoProvisioningFalse_PreservesStorage()
    {
        string path = Track(Path.Combine(AppContext.BaseDirectory, $"options-{Guid.NewGuid()}.db"));

        UniverseOptions options = UniverseOptions.WithSqlitePersistence(path);
        try
        {
            options = options.WithAutoProvisioning(false);

            Assert.False(options.AutoProvisionContainers);
            Assert.IsType<SqliteStatisticsStorage>(options.StatisticsStorage);
        }
        finally
        {
            DisposeStatisticsStorage(options);
        }
    }

    private string Track(string path)
    {
        _paths.Add(path);
        return path;
    }

    private static void DisposeStatisticsStorage(UniverseOptions options)
        => (options.StatisticsStorage as IDisposable)?.Dispose();

    private sealed class NullOptionsGalaxyCore : GalaxyCore
    {
        public NullOptionsGalaxyCore()
            : base(client: null, database: "database", container: "container", partitionKey: [], options: null)
        {
        }
    }
}
