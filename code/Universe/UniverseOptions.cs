using Universe.Builder.Strategies.Storage;

namespace Universe;

/// <summary>
/// Configuration options for Universe query optimization
/// </summary>
public sealed class UniverseOptions
{
    /// <summary>
    /// Storage backend for query statistics. Defaults to InMemoryStatisticsStorage.
    /// </summary>
    public IQueryStatisticsStorage StatisticsStorage { get; set; }

    /// <summary>
    /// Whether repository construction should create the Cosmos database and container if they do not exist.
    /// Defaults to true for backward compatibility.
    /// </summary>
    public bool AutoProvisionContainers { get; set; }

    /// <summary>
    /// Creates default Universe options with in-memory storage
    /// </summary>
    public UniverseOptions()
    {
        StatisticsStorage = null; // Will default to InMemoryStatisticsStorage in QueryTuner
        AutoProvisionContainers = true;
    }

    /// <summary>
    /// Enables or disables automatic Cosmos database/container provisioning during repository construction.
    /// Disable this in production environments where infrastructure is managed separately.
    /// </summary>
    /// <param name="enabled">True to create missing containers automatically; false to use existing containers only.</param>
    /// <returns>The same UniverseOptions instance for fluent configuration.</returns>
    public UniverseOptions WithAutoProvisioning(bool enabled)
    {
        AutoProvisionContainers = enabled;
        return this;
    }

    /// <summary>
    /// Creates Universe options with file-based persistence (JSON)
    /// </summary>
    /// <param name="statisticsFilePath">Optional custom path for statistics file. If null, uses default location.</param>
    /// <returns>UniverseOptions configured for file persistence</returns>
    public static UniverseOptions WithFilePersistence(string statisticsFilePath = null) => new()
    {
        StatisticsStorage = new FileStatisticsStorage(statisticsFilePath)
    };

    /// <summary>
    /// Creates Universe options with SQLite-based persistence for high-performance statistics storage.
    /// Features WAL mode for concurrent access, batched writes, and automatic cleanup.
    /// </summary>
    /// <param name="path">Optional custom path for the SQLite database file. If null, uses 'universe-stats.db' in the application directory.</param>
    /// <param name="retentionDays">Number of days to retain statistics. Older records are automatically cleaned up. Default is 7 days.</param>
    /// <param name="batchSize">Number of records to batch before flushing to database. Default is 10.</param>
    /// <param name="flushIntervalSeconds">Interval in seconds between automatic flushes. Default is 5 seconds.</param>
    /// <returns>UniverseOptions configured for SQLite persistence</returns>
    public static UniverseOptions WithSqlitePersistence(
        string path = null,
        int retentionDays = 7,
        int batchSize = 10,
        int flushIntervalSeconds = 5) => new()
        {
            StatisticsStorage = new SqliteStatisticsStorage(path, retentionDays, batchSize, flushIntervalSeconds)
        };
}
