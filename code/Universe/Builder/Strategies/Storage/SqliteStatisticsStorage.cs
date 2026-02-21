using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Universe.Builder.Strategies.Storage;

/// <summary>
/// SQLite-based storage for query statistics with high-performance features:
/// - WAL mode for concurrent read/write
/// - Non-blocking fire-and-forget writes
/// - Batched inserts for efficiency
/// - Auto-cleanup of old data
/// - Schema versioning for safe upgrades
/// </summary>
public sealed class SqliteStatisticsStorage : IQueryStatisticsStorage, IDisposable
{
	private readonly string _dbPath;
	private readonly int _retentionDays;
	private readonly int _batchSize;
	private readonly TimeSpan _flushInterval;
	private readonly SqliteConnection _connection;
	private readonly ConcurrentQueue<QueryExecutionStatistics> _writeQueue = new();
	private readonly SemaphoreSlim _writeLock = new(1, 1);
	private readonly Timer _flushTimer;
	private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
	private bool _disposed;
	private DateTime _lastCleanup = DateTime.UtcNow;
	private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
	private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);
	private const int CurrentSchemaVersion = 1;
	private const int MaxFlushRetries = 3;
	private readonly ConcurrentDictionary<QueryExecutionStatistics, int> _retryCounts = new(ReferenceEqualityComparer.Instance);

	// Prepared statement cache
	private SqliteCommand _insertCommand;
	private SqliteCommand _selectRecentCommand;
	private SqliteCommand _selectByHashCommand;
	private SqliteCommand _deleteOldCommand;

	/// <summary>
	/// Creates a new SQLite statistics storage
	/// </summary>
	/// <param name="dbPath">Path to the SQLite database file. If null, uses a platform-aware default
	/// (temp directory on Azure, application directory otherwise).</param>
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

		_dbPath = Path.GetFullPath(dbPath ?? ResolveDefaultPath());
		_retentionDays = retentionDays;
		_batchSize = batchSize;
		_flushInterval = TimeSpan.FromSeconds(flushIntervalSeconds);

		// Ensure directory exists
		string directory = Path.GetDirectoryName(_dbPath);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			Directory.CreateDirectory(directory);

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

		// Run initial cleanup
		FireAndForget(CleanupOldRecordsAsync);

		// Start flush timer
		_flushTimer = new Timer(
			_ => FireAndForget(FlushAsync),
			null,
			_flushInterval,
			_flushInterval);
	}

	/// <summary>
	/// Resolves the default database path based on the runtime environment.
	/// On Azure (Functions / App Service), uses local temp storage to avoid
	/// SMB-mounted paths where SQLite WAL mode is unsupported.
	/// </summary>
	internal static string ResolveDefaultPath()
	{
		if (IsAzureEnvironment())
		{
			string localTemp = Environment.GetEnvironmentVariable("TMP")
				?? Environment.GetEnvironmentVariable("TEMP")
				?? Path.GetTempPath();

			return Path.Combine(localTemp, "universe-stats.db");
		}

		return Path.Combine(AppContext.BaseDirectory, "universe-stats.db");
	}

	/// <summary>
	/// Detects Azure App Service or Azure Functions by checking well-known environment variables.
	/// </summary>
	internal static bool IsAzureEnvironment() =>
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"))
		|| !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME"));

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
		catch (SystemException ex)
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
	}

	/// <summary>
	/// Queue a statistic for saving (non-blocking)
	/// </summary>
	public Task SaveAsync(QueryExecutionStatistics stats)
	{
		if (_disposed) return Task.CompletedTask;

		_writeQueue.Enqueue(stats);

		// Trigger flush if batch size reached
		if (_writeQueue.Count >= _batchSize)
			FireAndForget(FlushAsync);

		return Task.CompletedTask;
	}

	/// <summary>
	/// Load recent statistics from the database
	/// </summary>
	/// <exception cref="TimeoutException">Thrown when the write lock cannot be acquired within the timeout period.</exception>
	public async Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count)
	{
		if (_disposed) return [];

		// Flush pending writes first to ensure consistency
		await FlushAsync();

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

	/// <summary>
	/// Load statistics for a specific query hash within a time window
	/// </summary>
	/// <exception cref="TimeoutException">Thrown when the write lock cannot be acquired within the timeout period.</exception>
	public async Task<IList<QueryExecutionStatistics>> GetByQueryHashAsync(string queryHash, TimeSpan window)
	{
		if (_disposed) return [];

		// Flush pending writes first to ensure consistency
		await FlushAsync();

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

	/// <summary>
	/// Clear old statistics (older than specified timespan)
	/// </summary>
	/// <exception cref="TimeoutException">Thrown when the write lock cannot be acquired within the timeout period.</exception>
	public async Task ClearOldAsync(TimeSpan olderThan)
	{
		if (_disposed) return;

		long cutoffTimestamp = DateTimeOffset.UtcNow.Subtract(olderThan).ToUnixTimeSeconds();

		if (!await _writeLock.WaitAsync(LockTimeout))
			throw new TimeoutException("Lock acquisition timed out in ClearOldAsync.");
		try
		{
			_deleteOldCommand.Parameters.Clear();
			_deleteOldCommand.Parameters.AddWithValue("@cutoff", cutoffTimestamp);
			await _deleteOldCommand.ExecuteNonQueryAsync();
		}
		finally
		{
			_writeLock.Release();
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
				catch (SystemException ex)
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

	private async Task FlushAsync()
	{
		if (_disposed || _writeQueue.IsEmpty) return;

		// Collect items to write
		List<QueryExecutionStatistics> toWrite = [];
		while (toWrite.Count < _batchSize * 2 && _writeQueue.TryDequeue(out QueryExecutionStatistics stat))
		{
			toWrite.Add(stat);
		}

		if (toWrite.Count == 0) return;

		if (!await _writeLock.WaitAsync(LockTimeout))
		{
			RequeueWithRetryLimit(toWrite);
			throw new TimeoutException("Lock acquisition timed out in FlushAsync.");
		}
		try
		{
			using SqliteTransaction transaction = _connection.BeginTransaction();

			foreach (QueryExecutionStatistics stat in toWrite)
			{
				_insertCommand.Transaction = transaction;
				_insertCommand.Parameters.Clear();
				_insertCommand.Parameters.AddWithValue("@hash", stat.QueryHash);
				_insertCommand.Parameters.AddWithValue("@type", (int)stat.Type);
				_insertCommand.Parameters.AddWithValue("@ru", stat.RU);
				_insertCommand.Parameters.AddWithValue("@time", (long)stat.ExecutionTime.TotalMilliseconds);
				_insertCommand.Parameters.AddWithValue("@count", stat.ResultCount);
				_insertCommand.Parameters.AddWithValue("@success", stat.Success ? 1 : 0);
				_insertCommand.Parameters.AddWithValue("@timestamp", new DateTimeOffset(stat.Timestamp).ToUnixTimeSeconds());
				_insertCommand.Parameters.AddWithValue("@strategy", stat.StrategyUsed ?? (object)DBNull.Value);
				_insertCommand.Parameters.AddWithValue("@hints",
					stat.HintsUsed != null ? JsonSerializer.Serialize(stat.HintsUsed, _jsonOptions) : DBNull.Value);

				await _insertCommand.ExecuteNonQueryAsync();
			}

			await transaction.CommitAsync();

			// Clear retry tracking for successfully persisted items
			foreach (QueryExecutionStatistics stat in toWrite)
				_retryCounts.TryRemove(stat, out _);
		}
		catch (SystemException ex)
		{
			Trace.TraceWarning($"[UniverseQuery] SQLite flush failed: {ex.Message}");
			RequeueWithRetryLimit(toWrite);
		}
		finally
		{
			_writeLock.Release();
		}

		if (DateTime.UtcNow - _lastCleanup > CleanupInterval)
		{
			_lastCleanup = DateTime.UtcNow;
			FireAndForget(CleanupOldRecordsAsync);
		}
	}

	private void RequeueWithRetryLimit(List<QueryExecutionStatistics> items)
	{
		foreach (QueryExecutionStatistics item in items)
		{
			int retries = _retryCounts.AddOrUpdate(item, 1, (_, count) => count + 1);
			if (retries <= MaxFlushRetries)
			{
				_writeQueue.Enqueue(item);
			}
			else
			{
				_retryCounts.TryRemove(item, out _);
				Trace.TraceWarning($"[UniverseQuery] Dropping statistics for query '{item.QueryHash}' after {MaxFlushRetries} failed flush attempts.");
			}
		}
	}

	/// <summary>
	/// Runs an async task fire-and-forget, logging any exceptions instead of leaving them unobserved.
	/// </summary>
	private static async void FireAndForget(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (System.Exception ex)
		{
			Trace.TraceWarning($"[UniverseQuery] Background operation failed: {ex.Message}");
		}
	}

	private async Task CleanupOldRecordsAsync() => await ClearOldAsync(TimeSpan.FromDays(_retentionDays));

	/// <summary>
	/// Dispose the storage, flushing any pending writes
	/// </summary>
	public void Dispose()
	{
		if (_disposed) return;

		// Stop timer callbacks before final flush
		_flushTimer?.Change(Timeout.Infinite, Timeout.Infinite);
		_flushTimer?.Dispose();

		// Final flush BEFORE setting _disposed (FlushAsync checks _disposed and would skip)
		try
		{
			FlushAsync().Wait(TimeSpan.FromSeconds(10));
		}
		catch (SystemException ex)
		{
			Trace.TraceWarning($"[UniverseQuery] Final flush during dispose failed: {ex.Message}");
		}

		_disposed = true;

		_insertCommand?.Dispose();
		_selectRecentCommand?.Dispose();
		_selectByHashCommand?.Dispose();
		_deleteOldCommand?.Dispose();
		_connection?.Dispose();
		_writeLock?.Dispose();
	}
}
