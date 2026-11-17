using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;

namespace Universe.Builder.Strategies.Storage;

/// <summary>
/// SQLite-based storage for query statistics with high-performance features:
/// - WAL mode for concurrent read/write
/// - Bounded, non-blocking writes through one tracked writer
/// - Batched inserts for efficiency
/// - Hard row, database-page, and WAL limits in addition to age cleanup
/// - Schema versioning for safe upgrades
/// </summary>
public sealed class SqliteStatisticsStorage : IQueryStatisticsStorage, IDisposable
{
    private sealed record WriteRequest(
        QueryExecutionStatistics Statistics,
        TaskCompletionSource<bool> Completion = null,
        bool IsPeriodicFlush = false);

    private readonly string _dbPath;
    private readonly int _retentionDays;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly SqliteConnection _connection;
    private readonly Channel<WriteRequest> _writeQueue;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Timer _flushTimer;
    private readonly Task _writerTask;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private readonly object _lifecycleLock = new();
    private TaskCompletionSource<bool> _activeOperationsDrained;
    private int _activeOperations;
    private int _disposed;
    private int _periodicFlushQueued;
    private long _droppedWriteCount;
    private DateTime _lastCleanup = DateTime.UtcNow;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);
    private const int CurrentSchemaVersion = 1;
    private const int MaxFlushRetries = 3;
    private const int WriteQueueCapacity = 1000;
    internal const int MaxStoredRecords = 1000;
    internal const long MaxDatabaseBytes = 64L * 1024 * 1024;
    private const long MaxWalBytes = 4L * 1024 * 1024;

    internal int PendingWriteCount => _writeQueue.Reader.Count;
    internal long DroppedWriteCount => Interlocked.Read(ref _droppedWriteCount);

    // Prepared statement cache
    private SqliteCommand _insertCommand;
    private SqliteCommand _selectRecentCommand;
    private SqliteCommand _selectByHashCommand;
    private SqliteCommand _deleteOldCommand;
    private SqliteCommand _deleteOverflowCommand;

    /// <summary>
    /// Creates a new SQLite statistics storage
    /// </summary>
    /// <param name="dbPath">Path to the SQLite database file. If null, uses a platform-aware default
    /// (temp directory on Azure, application directory otherwise).
    /// <b>Warning:</b> do not pass a value derived from untrusted user input; the path is used
    /// directly for file and directory creation after normalization via <see cref="Path.GetFullPath(string)"/>.</param>
    /// <param name="retentionDays">Number of days to retain statistics (minimum 1). Older records are auto-cleaned.</param>
    /// <param name="batchSize">Number of records to batch before flushing to database (minimum 1).</param>
    /// <param name="flushIntervalSeconds">Interval in seconds between automatic flushes (minimum 1).</param>
    public SqliteStatisticsStorage(
        string dbPath = null,
        int retentionDays = 7,
        int batchSize = 10,
        int flushIntervalSeconds = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionDays, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(flushIntervalSeconds, 1);

        _dbPath = PlatformDetection.ValidateStoragePath(dbPath ?? ResolveDefaultPath());
        _retentionDays = retentionDays;
        _batchSize = Math.Min(batchSize, WriteQueueCapacity);
        _flushInterval = TimeSpan.FromSeconds(flushIntervalSeconds);

        // Ensure directory exists with restrictive permissions
        string directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            PlatformDetection.SetRestrictivePermissions(directory);
        }

        // Create connection with WAL mode for better concurrency
        SqliteConnectionStringBuilder connectionBuilder = new()
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        _connection = new SqliteConnection(connectionBuilder.ConnectionString);
        _connection.Open();

        // Enable WAL mode and other performance optimizations
        ExecutePragmas();

        // Run integrity check on startup
        RunIntegrityCheck();

        // Initialize schema with versioning
        InitializeSchema();

        // Prepare statements
        PrepareStatements();

        RunInitialCleanup();

        _writeQueue = Channel.CreateBounded<WriteRequest>(new BoundedChannelOptions(WriteQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _writerTask = ProcessWritesAsync();

        // Start flush timer
        _flushTimer = new Timer(
            RequestPeriodicFlush,
            null,
            _flushInterval,
            _flushInterval);
    }

    /// <summary>
    /// Resolves the default database path based on the runtime environment.
    /// On Azure (Functions / App Service), uses local temp storage to avoid
    /// SMB-mounted paths where SQLite WAL mode is unsupported.
    /// <para>
    /// <b>Azure caveat:</b> Temp directories are local to the VM instance and are cleared on
    /// cold starts, scale events, or platform updates. The tuner starts fresh in these cases.
    /// For durable persistence across restarts, provide an explicit <c>dbPath</c> pointing to
    /// a writable local disk.
    /// </para>
    /// </summary>
    internal static string ResolveDefaultPath()
    {
        if (PlatformDetection.IsAzureEnvironment())
            return Path.Combine(PlatformDetection.GetLocalTempDirectory(), "universe-stats.db");

        return Path.Combine(AppContext.BaseDirectory, "universe-stats.db");
    }

    private void ExecutePragmas()
    {
        using SqliteCommand pragmaCommand = _connection.CreateCommand();
        pragmaCommand.CommandText = """
			PRAGMA journal_mode = WAL;
			PRAGMA synchronous = NORMAL;
			PRAGMA cache_size = 10000;
			PRAGMA temp_store = MEMORY;
			""";
        pragmaCommand.ExecuteNonQuery();

        using SqliteCommand pageSizeCommand = _connection.CreateCommand();
        pageSizeCommand.CommandText = "PRAGMA page_size";
        long pageSize = Convert.ToInt64(pageSizeCommand.ExecuteScalar());
        long maxPageCount = Math.Max(1, MaxDatabaseBytes / pageSize);

        using SqliteCommand limitCommand = _connection.CreateCommand();
        limitCommand.CommandText = $"""
            PRAGMA max_page_count = {maxPageCount};
            PRAGMA journal_size_limit = {MaxWalBytes};
            PRAGMA wal_autocheckpoint = 1000;
            """;
        limitCommand.ExecuteNonQuery();
    }

    private void RunIntegrityCheck()
    {
        try
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check";
            string result = cmd.ExecuteScalar() as string;
            if (result != "ok")
                Trace.TraceWarning($"[UniverseQuery] SQLite integrity check failed: {result}");
        }
        catch (SqliteException ex)
        {
            Trace.TraceWarning($"[UniverseQuery] SQLite integrity check error: {ex.Message}");
        }
    }

    private void InitializeSchema()
    {
        using SqliteCommand schemaCommand = _connection.CreateCommand();
        schemaCommand.CommandText = """
			CREATE TABLE IF NOT EXISTS schema_version (
				version INTEGER NOT NULL
			);

			CREATE TABLE IF NOT EXISTS query_statistics (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				query_hash TEXT NOT NULL,
				query_type INTEGER NOT NULL,
				ru REAL NOT NULL,
				execution_time_ms INTEGER NOT NULL,
				result_count INTEGER NOT NULL,
				success INTEGER NOT NULL,
				timestamp INTEGER NOT NULL,
				strategy_used TEXT,
				hints_used TEXT
			);

			CREATE INDEX IF NOT EXISTS idx_hash_timestamp
				ON query_statistics(query_hash, timestamp DESC);

			CREATE INDEX IF NOT EXISTS idx_timestamp
				ON query_statistics(timestamp);

			CREATE INDEX IF NOT EXISTS idx_query_type_timestamp
				ON query_statistics(query_type, timestamp DESC);

			CREATE INDEX IF NOT EXISTS idx_strategy_used
				ON query_statistics(strategy_used);
			""";
        schemaCommand.ExecuteNonQuery();

        // Check and set schema version
        using SqliteCommand versionCheck = _connection.CreateCommand();
        versionCheck.CommandText = "SELECT version FROM schema_version LIMIT 1";
        object result = versionCheck.ExecuteScalar();

        if (result == null)
        {
            using SqliteCommand insertVersion = _connection.CreateCommand();
            insertVersion.CommandText = "INSERT INTO schema_version (version) VALUES (@version)";
            insertVersion.Parameters.AddWithValue("@version", CurrentSchemaVersion);
            insertVersion.ExecuteNonQuery();
        }
        else
        {
            int existingVersion = Convert.ToInt32(result);
            if (existingVersion < CurrentSchemaVersion)
                MigrateSchema(existingVersion);
        }
    }

    private void MigrateSchema(int fromVersion)
    {
        Trace.TraceInformation($"[UniverseQuery] Migrating SQLite schema from version {fromVersion} to {CurrentSchemaVersion}.");

        // Future migrations go here, e.g.:
        // if (fromVersion < 2) { ... apply v2 migration ... }

        using SqliteCommand updateVersion = _connection.CreateCommand();
        updateVersion.CommandText = "UPDATE schema_version SET version = @version";
        updateVersion.Parameters.AddWithValue("@version", CurrentSchemaVersion);
        updateVersion.ExecuteNonQuery();
    }

    private void PrepareStatements()
    {
        // Insert statement (will be used for batched inserts)
        _insertCommand = _connection.CreateCommand();
        _insertCommand.CommandText = """
			INSERT INTO query_statistics
				(query_hash, query_type, ru, execution_time_ms, result_count, success, timestamp, strategy_used, hints_used)
			VALUES
				(@hash, @type, @ru, @time, @count, @success, @timestamp, @strategy, @hints)
			""";

        // Select recent statement
        _selectRecentCommand = _connection.CreateCommand();
        _selectRecentCommand.CommandText = """
			SELECT query_hash, query_type, ru, execution_time_ms, result_count, success, timestamp, strategy_used, hints_used
			FROM query_statistics
			ORDER BY timestamp DESC
			LIMIT @count
			""";

        // Select by hash statement
        _selectByHashCommand = _connection.CreateCommand();
        _selectByHashCommand.CommandText = """
			SELECT query_hash, query_type, ru, execution_time_ms, result_count, success, timestamp, strategy_used, hints_used
			FROM query_statistics
			WHERE query_hash = @hash AND timestamp > @cutoff
			ORDER BY timestamp DESC
			""";

        // Delete old records statement
        _deleteOldCommand = _connection.CreateCommand();
        _deleteOldCommand.CommandText = "DELETE FROM query_statistics WHERE timestamp < @cutoff";

        // Enforce a hard row ceiling regardless of the configured time retention.
        _deleteOverflowCommand = _connection.CreateCommand();
        _deleteOverflowCommand.CommandText = """
            DELETE FROM query_statistics
            WHERE id IN (
                SELECT id
                FROM query_statistics
                ORDER BY timestamp DESC, id DESC
                LIMIT -1 OFFSET @maxRows
            )
            """;
    }

    private void RunInitialCleanup()
    {
        _deleteOldCommand.Parameters.Clear();
        _deleteOldCommand.Parameters.AddWithValue(
            "@cutoff",
            DateTimeOffset.UtcNow.AddDays(-_retentionDays).ToUnixTimeSeconds());
        _deleteOldCommand.ExecuteNonQuery();

        _deleteOverflowCommand.Parameters.Clear();
        _deleteOverflowCommand.Parameters.AddWithValue("@maxRows", MaxStoredRecords);
        _deleteOverflowCommand.ExecuteNonQuery();
    }

    /// <summary>
    /// Queue a statistic for best-effort saving without blocking query execution.
    /// Records are dropped when the bounded telemetry queue is full.
    /// </summary>
    public Task SaveAsync(QueryExecutionStatistics stats)
    {
        if (!TryEnterOperation()) return Task.CompletedTask;

        try
        {
            if (!_writeQueue.Writer.TryWrite(new WriteRequest(stats)))
                Interlocked.Increment(ref _droppedWriteCount);

            return Task.CompletedTask;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Load recent statistics from the database
    /// </summary>
    /// <exception cref="TimeoutException">Thrown when the write lock cannot be acquired within the timeout period.</exception>
    public async Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count)
    {
        if (!TryEnterOperation()) return [];

        try
        {
            // Flush pending writes first to ensure consistency
            await FlushPendingAsync();

            if (!await _writeLock.WaitAsync(LockTimeout))
                throw new TimeoutException("Lock acquisition timed out in LoadRecentAsync.");
            try
            {
                _selectRecentCommand.Parameters.Clear();
                _selectRecentCommand.Parameters.AddWithValue("@count", count);

                using SqliteDataReader reader = await _selectRecentCommand.ExecuteReaderAsync();
                return ReadStatistics(reader);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Load statistics for a specific query hash within a time window
    /// </summary>
    /// <exception cref="TimeoutException">Thrown when the write lock cannot be acquired within the timeout period.</exception>
    public async Task<IList<QueryExecutionStatistics>> GetByQueryHashAsync(string queryHash, TimeSpan window)
    {
        if (!TryEnterOperation()) return [];

        try
        {
            // Flush pending writes first to ensure consistency
            await FlushPendingAsync();

            long cutoffTimestamp = DateTimeOffset.UtcNow.Subtract(window).ToUnixTimeSeconds();

            if (!await _writeLock.WaitAsync(LockTimeout))
                throw new TimeoutException("Lock acquisition timed out in GetByQueryHashAsync.");
            try
            {
                _selectByHashCommand.Parameters.Clear();
                _selectByHashCommand.Parameters.AddWithValue("@hash", queryHash);
                _selectByHashCommand.Parameters.AddWithValue("@cutoff", cutoffTimestamp);

                using SqliteDataReader reader = await _selectByHashCommand.ExecuteReaderAsync();
                return ReadStatistics(reader);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Clear old statistics (older than specified timespan)
    /// </summary>
    /// <exception cref="TimeoutException">Thrown when the write lock cannot be acquired within the timeout period.</exception>
    public async Task ClearOldAsync(TimeSpan olderThan)
    {
        if (!TryEnterOperation()) return;

        try
        {
            await FlushPendingAsync();

            long cutoffTimestamp = DateTimeOffset.UtcNow.Subtract(olderThan).ToUnixTimeSeconds();

            if (!await _writeLock.WaitAsync(LockTimeout))
                throw new TimeoutException("Lock acquisition timed out in ClearOldAsync.");
            try
            {
                _deleteOldCommand.Parameters.Clear();
                _deleteOldCommand.Parameters.AddWithValue("@cutoff", cutoffTimestamp);
                await _deleteOldCommand.ExecuteNonQueryAsync();

                _deleteOverflowCommand.Parameters.Clear();
                _deleteOverflowCommand.Parameters.AddWithValue("@maxRows", MaxStoredRecords);
                await _deleteOverflowCommand.ExecuteNonQueryAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    private List<QueryExecutionStatistics> ReadStatistics(SqliteDataReader reader)
    {
        List<QueryExecutionStatistics> results = [];

        while (reader.Read())
        {
            string hintsJson = reader.IsDBNull(8) ? null : reader.GetString(8);
            IReadOnlyDictionary<string, object> hints = null;

            if (!string.IsNullOrEmpty(hintsJson))
            {
                try
                {
                    hints = JsonSerializer.Deserialize<Dictionary<string, object>>(hintsJson);
                }
                catch (JsonException ex)
                {
                    Trace.TraceWarning($"[UniverseQuery] Failed to deserialize query hints: {ex.Message}");
                }
            }

            QueryExecutionStatistics stat = new()
            {
                QueryHash = reader.GetString(0),
                Type = (QueryType)reader.GetInt32(1),
                RU = reader.GetDouble(2),
                ExecutionTime = TimeSpan.FromMilliseconds(reader.GetInt64(3)),
                ResultCount = reader.GetInt32(4),
                Success = reader.GetInt32(5) == 1,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)).UtcDateTime,
                StrategyUsed = reader.IsDBNull(7) ? null : reader.GetString(7),
                HintsUsed = hints
            };

            results.Add(stat);
        }

        return results;
    }

    private async Task ProcessWritesAsync()
    {
        List<QueryExecutionStatistics> batch = new(Math.Min(_batchSize, WriteQueueCapacity));

        await foreach (WriteRequest request in _writeQueue.Reader.ReadAllAsync())
        {
            if (request.Statistics is not null)
                batch.Add(request.Statistics);

            if (batch.Count >= _batchSize || request.Statistics is null)
            {
                await PersistBatchWithRetriesAsync(batch);
                batch.Clear();
            }

            request.Completion?.TrySetResult(true);
            if (request.IsPeriodicFlush)
                Interlocked.Exchange(ref _periodicFlushQueued, 0);
        }

        await PersistBatchWithRetriesAsync(batch);
    }

    private async Task PersistBatchWithRetriesAsync(List<QueryExecutionStatistics> batch)
    {
        if (batch.Count == 0)
            return;

        for (int attempt = 1; attempt <= MaxFlushRetries; attempt++)
        {
            if (await TryPersistBatchAsync(batch))
                return;
        }

        Interlocked.Add(ref _droppedWriteCount, batch.Count);
        Trace.TraceWarning($"[UniverseQuery] Dropping {batch.Count} query statistics after {MaxFlushRetries} failed flush attempts.");
    }

    private async Task<bool> TryPersistBatchAsync(List<QueryExecutionStatistics> batch)
    {
        if (!await _writeLock.WaitAsync(LockTimeout))
        {
            Trace.TraceWarning("[UniverseQuery] SQLite flush timed out waiting for the write lock.");
            return false;
        }

        try
        {
            using SqliteTransaction transaction = _connection.BeginTransaction();
            bool cleanupDue = DateTime.UtcNow - _lastCleanup > CleanupInterval;

            foreach (QueryExecutionStatistics stat in batch)
            {
                _insertCommand.Transaction = transaction;
                _insertCommand.Parameters.Clear();
                _insertCommand.Parameters.AddWithValue("@hash", stat.QueryHash);
                _insertCommand.Parameters.AddWithValue("@type", (int)stat.Type);
                _insertCommand.Parameters.AddWithValue("@ru", stat.RU);
                _insertCommand.Parameters.AddWithValue("@time", (long)stat.ExecutionTime.TotalMilliseconds);
                _insertCommand.Parameters.AddWithValue("@count", stat.ResultCount);
                _insertCommand.Parameters.AddWithValue("@success", stat.Success ? 1 : 0);
                DateTime utcTimestamp = stat.Timestamp.Kind == DateTimeKind.Utc
                    ? stat.Timestamp
                    : stat.Timestamp.ToUniversalTime();
                _insertCommand.Parameters.AddWithValue("@timestamp", new DateTimeOffset(utcTimestamp).ToUnixTimeSeconds());
                _insertCommand.Parameters.AddWithValue("@strategy", stat.StrategyUsed ?? (object)DBNull.Value);
                _insertCommand.Parameters.AddWithValue("@hints",
                    stat.HintsUsed != null ? JsonSerializer.Serialize(stat.HintsUsed, _jsonOptions) : DBNull.Value);

                await _insertCommand.ExecuteNonQueryAsync();
            }

            _deleteOverflowCommand.Transaction = transaction;
            _deleteOverflowCommand.Parameters.Clear();
            _deleteOverflowCommand.Parameters.AddWithValue("@maxRows", MaxStoredRecords);
            await _deleteOverflowCommand.ExecuteNonQueryAsync();

            if (cleanupDue)
            {
                _deleteOldCommand.Transaction = transaction;
                _deleteOldCommand.Parameters.Clear();
                _deleteOldCommand.Parameters.AddWithValue(
                    "@cutoff",
                    DateTimeOffset.UtcNow.AddDays(-_retentionDays).ToUnixTimeSeconds());
                await _deleteOldCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            if (cleanupDue)
                _lastCleanup = DateTime.UtcNow;
            return true;
        }
        catch (System.Exception ex) when (ex is SqliteException or IOException or JsonException)
        {
            Trace.TraceWarning($"[UniverseQuery] SQLite flush failed: {ex.Message}");
            return false;
        }
        finally
        {
            _insertCommand.Transaction = null;
            _deleteOldCommand.Transaction = null;
            _deleteOverflowCommand.Transaction = null;
            _writeLock.Release();
        }
    }

    private async Task FlushPendingAsync()
    {
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await _writeQueue.Writer.WriteAsync(new WriteRequest(null, completion));
            await completion.Task;
        }
        catch (ChannelClosedException)
        {
            // Disposal drains every accepted write before closing the channel.
        }
    }

    private bool TryEnterOperation()
    {
        lock (_lifecycleLock)
        {
            if (_disposed != 0) return false;
            _activeOperations++;
            return true;
        }
    }

    private void ExitOperation()
    {
        TaskCompletionSource<bool> drained = null;
        lock (_lifecycleLock)
        {
            _activeOperations--;
            if (_disposed != 0 && _activeOperations == 0)
                drained = _activeOperationsDrained;
        }
        drained?.TrySetResult(true);
    }

    private void RequestPeriodicFlush(object state)
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _periodicFlushQueued, 1) != 0)
            return;

        if (!_writeQueue.Writer.TryWrite(new WriteRequest(null, IsPeriodicFlush: true)))
            Interlocked.Exchange(ref _periodicFlushQueued, 0);
    }

    /// <summary>
    /// Dispose the storage, flushing any pending writes
    /// </summary>
    public void Dispose()
    {
        Task operationsDrained;
        lock (_lifecycleLock)
        {
            if (_disposed != 0) return;
            Volatile.Write(ref _disposed, 1);
            if (_activeOperations == 0)
                operationsDrained = Task.CompletedTask;
            else
            {
                _activeOperationsDrained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                operationsDrained = _activeOperationsDrained.Task;
            }
        }

        _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _flushTimer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        // Admitted readers need the writer alive to complete their ordered flush barriers.
        operationsDrained.GetAwaiter().GetResult();
        _writeQueue.Writer.TryComplete();
        try
        {
            _writerTask.GetAwaiter().GetResult();
        }
        catch (System.Exception ex)
        {
            Trace.TraceWarning($"[UniverseQuery] Failed to drain SQLite statistics during disposal: {ex.Message}");
        }

        _insertCommand?.Dispose();
        _selectRecentCommand?.Dispose();
        _selectByHashCommand?.Dispose();
        _deleteOldCommand?.Dispose();
        _deleteOverflowCommand?.Dispose();
        _connection?.Dispose();
        _writeLock?.Dispose();
    }
}
