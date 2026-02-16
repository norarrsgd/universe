using Universe.Builder.Strategies;
using Universe.Builder.Strategies.Storage;
using Universe.Tests.Helpers;
using Xunit;

namespace Universe.Tests.Storage;

public sealed class SqliteStatisticsStorageTests : IDisposable
{
	private readonly string _dbPath;
	private readonly SqliteStatisticsStorage _storage;

	public SqliteStatisticsStorageTests()
	{
		_dbPath = Path.Combine(AppContext.BaseDirectory, $"test-{Guid.NewGuid()}.db");
		_storage = new SqliteStatisticsStorage(_dbPath, batchSize: 1, flushIntervalSeconds: 60);
	}

	public void Dispose()
	{
		_storage.Dispose();
		TryDeleteFiles(_dbPath);
	}

	private static void TryDeleteFiles(string dbPath)
	{
		foreach (string suffix in new[] { "", "-wal", "-shm" })
		{
			try { if (File.Exists(dbPath + suffix)) File.Delete(dbPath + suffix); }
			catch { /* best effort cleanup */ }
		}
	}

	#region Round-trip

	[Fact]
	public async Task SaveAndLoad_RoundTrip_AllFieldsPreserved()
	{
		var hints = new Dictionary<string, object> { ["MaxItemCount"] = 50 };
		var stat = TestStatisticsFactory.Create(
			queryHash: "abc123",
			type: QueryType.VectorSearch,
			ru: 42.5,
			executionTime: TimeSpan.FromMilliseconds(150),
			resultCount: 25,
			success: true,
			timestamp: DateTime.UtcNow,
			strategyUsed: "VectorSearch",
			hintsUsed: hints);

		await _storage.SaveAsync(stat);
		IList<QueryExecutionStatistics> loaded = await _storage.LoadRecentAsync(10);

		Assert.Single(loaded);
		var result = loaded[0];
		Assert.Equal("abc123", result.QueryHash);
		Assert.Equal(QueryType.VectorSearch, result.Type);
		Assert.Equal(42.5, result.RU);
		Assert.Equal(150, result.ExecutionTime.TotalMilliseconds);
		Assert.Equal(25, result.ResultCount);
		Assert.True(result.Success);
		Assert.Equal("VectorSearch", result.StrategyUsed);
		Assert.NotNull(result.HintsUsed);
	}

	[Fact]
	public async Task SaveAndLoad_NullHintsAndStrategy_Preserved()
	{
		var stat = TestStatisticsFactory.Create(strategyUsed: null, hintsUsed: null);
		await _storage.SaveAsync(stat);
		var loaded = await _storage.LoadRecentAsync(10);

		Assert.Single(loaded);
		Assert.Null(loaded[0].StrategyUsed);
		Assert.Null(loaded[0].HintsUsed);
	}

	#endregion

	#region Query behavior

	[Fact]
	public async Task LoadRecentAsync_RespectsCountAndOrdering()
	{
		for (int i = 0; i < 5; i++)
		{
			await _storage.SaveAsync(TestStatisticsFactory.Create(
				queryHash: $"hash-{i}",
				timestamp: DateTime.UtcNow.AddMinutes(-i)));
		}

		var loaded = await _storage.LoadRecentAsync(3);

		Assert.Equal(3, loaded.Count);
		// Most recent first
		Assert.True(loaded[0].Timestamp >= loaded[1].Timestamp);
		Assert.True(loaded[1].Timestamp >= loaded[2].Timestamp);
	}

	[Fact]
	public async Task GetByQueryHashAsync_FiltersByHashAndTimeWindow()
	{
		await _storage.SaveAsync(TestStatisticsFactory.Create(queryHash: "target"));
		await _storage.SaveAsync(TestStatisticsFactory.Create(queryHash: "other"));
		await _storage.SaveAsync(TestStatisticsFactory.Create(
			queryHash: "target",
			timestamp: DateTime.UtcNow.AddDays(-10))); // outside window

		var results = await _storage.GetByQueryHashAsync("target", TimeSpan.FromHours(1));

		Assert.Single(results);
		Assert.Equal("target", results[0].QueryHash);
	}

	#endregion

	#region Cleanup

	[Fact]
	public async Task ClearOldAsync_RemovesExpiredRecords()
	{
		await _storage.SaveAsync(TestStatisticsFactory.Create(
			queryHash: "old", timestamp: DateTime.UtcNow.AddDays(-5)));
		await _storage.SaveAsync(TestStatisticsFactory.Create(
			queryHash: "recent", timestamp: DateTime.UtcNow));

		var allBefore = await _storage.LoadRecentAsync(100);
		Assert.Equal(2, allBefore.Count);

		await _storage.ClearOldAsync(TimeSpan.FromDays(1));

		var allAfter = await _storage.LoadRecentAsync(100);
		Assert.Single(allAfter);
		Assert.Equal("recent", allAfter[0].QueryHash);
	}

	[Fact]
	public async Task ClearOldAsync_EmptyDatabase_DoesNotThrow()
	{
		await _storage.ClearOldAsync(TimeSpan.FromDays(1));
	}

	#endregion

	#region Batching

	[Fact]
	public async Task BatchFlushing_DataPersistsCorrectly()
	{
		string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-batch-{Guid.NewGuid()}.db");
		using var storage = new SqliteStatisticsStorage(dbPath, batchSize: 5, flushIntervalSeconds: 300);

		for (int i = 0; i < 5; i++)
			await storage.SaveAsync(TestStatisticsFactory.Create(queryHash: $"batch-{i}"));

		// Allow fire-and-forget flush triggered at batchSize threshold
		await Task.Delay(500);

		var loaded = await storage.LoadRecentAsync(100);
		Assert.Equal(5, loaded.Count);

		storage.Dispose();
		TryDeleteFiles(dbPath);
	}

	[Fact]
	public async Task TimerFlush_PersistsDataBelowBatchThreshold()
	{
		string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-timer-{Guid.NewGuid()}.db");
		using var storage = new SqliteStatisticsStorage(dbPath, batchSize: 100, flushIntervalSeconds: 1);

		// Save below batch threshold
		for (int i = 0; i < 3; i++)
			await storage.SaveAsync(TestStatisticsFactory.Create(queryHash: $"timer-{i}"));

		// Wait for timer-based flush (1 second interval)
		await Task.Delay(2000);

		var loaded = await storage.LoadRecentAsync(100);
		Assert.Equal(3, loaded.Count);

		storage.Dispose();
		TryDeleteFiles(dbPath);
	}

	#endregion

	#region Dispose

	[Fact]
	public async Task Dispose_FlushesRemainingQueue()
	{
		string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-dispose-{Guid.NewGuid()}.db");
		// Large batch + long interval so items stay queued until Dispose
		var storage = new SqliteStatisticsStorage(dbPath, batchSize: 100, flushIntervalSeconds: 300);

		for (int i = 0; i < 5; i++)
			await storage.SaveAsync(TestStatisticsFactory.Create(queryHash: $"dispose-{i}"));

		storage.Dispose();

		// Re-open and verify data was flushed during Dispose
		using var storage2 = new SqliteStatisticsStorage(dbPath, batchSize: 1, flushIntervalSeconds: 60);
		var loaded = await storage2.LoadRecentAsync(100);
		Assert.Equal(5, loaded.Count);

		storage2.Dispose();
		TryDeleteFiles(dbPath);
	}

	[Fact]
	public void DoubleDispose_DoesNotThrow()
	{
		string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-dd-{Guid.NewGuid()}.db");
		var storage = new SqliteStatisticsStorage(dbPath, batchSize: 1, flushIntervalSeconds: 60);
		storage.Dispose();
		storage.Dispose(); // should not throw
		TryDeleteFiles(dbPath);
	}

	[Fact]
	public async Task SaveAsync_AfterDispose_IsNoOp()
	{
		string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-noop-{Guid.NewGuid()}.db");
		var storage = new SqliteStatisticsStorage(dbPath, batchSize: 1, flushIntervalSeconds: 60);
		storage.Dispose();

		await storage.SaveAsync(TestStatisticsFactory.Create()); // should not throw
		TryDeleteFiles(dbPath);
	}

	#endregion

	#region Validation

	[Fact]
	public void InvalidPath_OutsideAppDirectory_Throws()
	{
		Assert.Throws<Universe.Exception.UniverseException>(() =>
			new SqliteStatisticsStorage("/tmp/outside-app-dir.db"));
	}

	[Fact]
	public void InvalidRetentionDays_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new SqliteStatisticsStorage(retentionDays: 0));
	}

	[Fact]
	public void InvalidBatchSize_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new SqliteStatisticsStorage(batchSize: 0));
	}

	[Fact]
	public void InvalidFlushInterval_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new SqliteStatisticsStorage(flushIntervalSeconds: 0));
	}

	#endregion

	#region Concurrency

	[Fact]
	public async Task ConcurrentSaves_AllRecordsPersisted()
	{
		string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-conc-{Guid.NewGuid()}.db");
		using var storage = new SqliteStatisticsStorage(dbPath, batchSize: 5, flushIntervalSeconds: 1);

		var tasks = Enumerable.Range(0, 10).Select(taskId =>
			Task.Run(async () =>
			{
				for (int i = 0; i < 10; i++)
				{
					await storage.SaveAsync(TestStatisticsFactory.Create(
						queryHash: $"t{taskId}-i{i}"));
				}
			}));

		await Task.WhenAll(tasks);

		var loaded = await storage.LoadRecentAsync(200);
		Assert.Equal(100, loaded.Count);

		storage.Dispose();
		TryDeleteFiles(dbPath);
	}

	[Fact]
	public async Task ConcurrentSaveAndLoad_CompletesWithoutDeadlock()
	{
		string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-dl-{Guid.NewGuid()}.db");
		using var storage = new SqliteStatisticsStorage(dbPath, batchSize: 2, flushIntervalSeconds: 1);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

		var saveTask = Task.Run(async () =>
		{
			for (int i = 0; i < 50 && !cts.IsCancellationRequested; i++)
				await storage.SaveAsync(TestStatisticsFactory.Create(queryHash: $"dl-{i}"));
		});

		var loadTask = Task.Run(async () =>
		{
			for (int i = 0; i < 10 && !cts.IsCancellationRequested; i++)
			{
				await storage.LoadRecentAsync(10);
				await Task.Delay(50);
			}
		});

		// If we reach here without timeout, no deadlock occurred
		await Task.WhenAll(saveTask, loadTask);

		storage.Dispose();
		TryDeleteFiles(dbPath);
	}

	#endregion
}
