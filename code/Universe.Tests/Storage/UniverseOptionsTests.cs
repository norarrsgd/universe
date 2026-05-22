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
