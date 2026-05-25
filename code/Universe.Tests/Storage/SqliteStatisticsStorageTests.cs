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
        string fullPath = Path.GetFullPath(dbPath);
        string baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);

        string relativePath = Path.GetRelativePath(baseDirectory, fullPath);
        if (Path.IsPathRooted(relativePath) || relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Test cleanup path must stay under the test output directory.");

        string directory = Path.GetDirectoryName(fullPath)!;
        string fileName = Path.GetFileName(fullPath);
        HashSet<string> allowedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            fileName,
            fileName + "-wal",
            fileName + "-shm"
        };

        foreach (FileInfo file in new DirectoryInfo(directory).EnumerateFiles(fileName + "*"))
        {
            if (!allowedNames.Contains(file.Name))
                continue;

            try { file.Delete(); }
            catch { /* best effort cleanup */ }
        }
    }

    #region Round-trip

    [Fact]
    public async Task SaveAndLoad_RoundTrip_AllFieldsPreserved()
    {
        Dictionary<string, object> hints = new Dictionary<string, object> { ["MaxItemCount"] = 50 };
        QueryExecutionStatistics stat = TestStatisticsFactory.Create(
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
        QueryExecutionStatistics result = loaded[0];
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
        QueryExecutionStatistics stat = TestStatisticsFactory.Create(strategyUsed: null, hintsUsed: null);
        await _storage.SaveAsync(stat);
        IList<QueryExecutionStatistics> loaded = await _storage.LoadRecentAsync(10);

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

        IList<QueryExecutionStatistics> loaded = await _storage.LoadRecentAsync(3);

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

        IList<QueryExecutionStatistics> results = await _storage.GetByQueryHashAsync("target", TimeSpan.FromHours(1));

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

        IList<QueryExecutionStatistics> allBefore = await _storage.LoadRecentAsync(100);
        Assert.Equal(2, allBefore.Count);

        await _storage.ClearOldAsync(TimeSpan.FromDays(1));

        IList<QueryExecutionStatistics> allAfter = await _storage.LoadRecentAsync(100);
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
        using SqliteStatisticsStorage storage = new SqliteStatisticsStorage(dbPath, batchSize: 5, flushIntervalSeconds: 300);

        for (int i = 0; i < 5; i++)
            await storage.SaveAsync(TestStatisticsFactory.Create(queryHash: $"batch-{i}"));

        // Allow fire-and-forget flush triggered at batchSize threshold
        await Task.Delay(500, TestContext.Current.CancellationToken);

        IList<QueryExecutionStatistics> loaded = await storage.LoadRecentAsync(100);
        Assert.Equal(5, loaded.Count);

        storage.Dispose();
        TryDeleteFiles(dbPath);
    }

    [Fact]
    public async Task TimerFlush_PersistsDataBelowBatchThreshold()
    {
        string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-timer-{Guid.NewGuid()}.db");
        using SqliteStatisticsStorage storage = new SqliteStatisticsStorage(dbPath, batchSize: 100, flushIntervalSeconds: 1);

        // Save below batch threshold
        for (int i = 0; i < 3; i++)
            await storage.SaveAsync(TestStatisticsFactory.Create(queryHash: $"timer-{i}"));

        // Wait for timer-based flush (1 second interval)
        await Task.Delay(2000, TestContext.Current.CancellationToken);

        IList<QueryExecutionStatistics> loaded = await storage.LoadRecentAsync(100);
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
        SqliteStatisticsStorage storage = new SqliteStatisticsStorage(dbPath, batchSize: 100, flushIntervalSeconds: 300);

        for (int i = 0; i < 5; i++)
            await storage.SaveAsync(TestStatisticsFactory.Create(queryHash: $"dispose-{i}"));

        storage.Dispose();

        // Re-open and verify data was flushed during Dispose
        using SqliteStatisticsStorage storage2 = new SqliteStatisticsStorage(dbPath, batchSize: 1, flushIntervalSeconds: 60);
        IList<QueryExecutionStatistics> loaded = await storage2.LoadRecentAsync(100);
        Assert.Equal(5, loaded.Count);

        storage2.Dispose();
        TryDeleteFiles(dbPath);
    }

    [Fact]
    public void DoubleDispose_DoesNotThrow()
    {
        string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-dd-{Guid.NewGuid()}.db");
        SqliteStatisticsStorage storage = new SqliteStatisticsStorage(dbPath, batchSize: 1, flushIntervalSeconds: 60);
        storage.Dispose();
        storage.Dispose(); // should not throw
        TryDeleteFiles(dbPath);
    }

    [Fact]
    public async Task SaveAsync_AfterDispose_IsNoOp()
    {
        string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-noop-{Guid.NewGuid()}.db");
        SqliteStatisticsStorage storage = new SqliteStatisticsStorage(dbPath, batchSize: 1, flushIntervalSeconds: 60);
        storage.Dispose();

        await storage.SaveAsync(TestStatisticsFactory.Create()); // should not throw
        TryDeleteFiles(dbPath);
    }

    #endregion

    #region Validation

    [Fact]
    public void CustomPath_IsAllowed()
    {
        string customPath = Path.Combine(AppContext.BaseDirectory, "custom-dir", $"universe-test-{Guid.NewGuid()}.db");
        using SqliteStatisticsStorage storage = new SqliteStatisticsStorage(customPath, batchSize: 1, flushIntervalSeconds: 60);
        storage.Dispose();
        TryDeleteFiles(customPath);
        try
        {
            string dir = Path.GetDirectoryName(customPath)!;
            if (Directory.Exists(dir)) Directory.Delete(dir);
        }
        catch { /* best effort */ }
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

    [Fact]
    public void ResolveDefaultPath_NonAzure_UsesAppBaseDirectory()
    {
        string original1 = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        string original2 = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");

        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
            Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", null);

            string expected = Path.Combine(AppContext.BaseDirectory, "universe-stats.db");
            Assert.Equal(expected, SqliteStatisticsStorage.ResolveDefaultPath());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", original1);
            Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", original2);
        }
    }

    [Fact]
    public void ResolveDefaultPath_Azure_UsesTempDirectory()
    {
        string original = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");

        try
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "test-instance");

            string result = SqliteStatisticsStorage.ResolveDefaultPath();

            Assert.EndsWith("universe-stats.db", result);
            Assert.DoesNotContain("wwwroot", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", original);
        }
    }

    #endregion

    #region Concurrency

    [Fact]
    public async Task ConcurrentSaves_AllRecordsPersisted()
    {
        string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-conc-{Guid.NewGuid()}.db");
        using SqliteStatisticsStorage storage = new SqliteStatisticsStorage(dbPath, batchSize: 5, flushIntervalSeconds: 1);

        IEnumerable<Task> tasks = Enumerable.Range(0, 10).Select(taskId =>
            Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await storage.SaveAsync(TestStatisticsFactory.Create(
                        queryHash: $"t{taskId}-i{i}"));
                }
            }));

        await Task.WhenAll(tasks);

        IList<QueryExecutionStatistics> loaded = await storage.LoadRecentAsync(200);
        Assert.Equal(100, loaded.Count);

        storage.Dispose();
        TryDeleteFiles(dbPath);
    }

    [Fact]
    public async Task ConcurrentSaveAndLoad_CompletesWithoutDeadlock()
    {
        string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-dl-{Guid.NewGuid()}.db");
        using SqliteStatisticsStorage storage = new SqliteStatisticsStorage(dbPath, batchSize: 2, flushIntervalSeconds: 1);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        Task saveTask = Task.Run(async () =>
        {
            for (int i = 0; i < 50 && !cts.IsCancellationRequested; i++)
                await storage.SaveAsync(TestStatisticsFactory.Create(queryHash: $"dl-{i}"));
        }, cts.Token);

        Task loadTask = Task.Run(async () =>
        {
            for (int i = 0; i < 10 && !cts.IsCancellationRequested; i++)
            {
                await storage.LoadRecentAsync(10);
                await Task.Delay(50, cts.Token);
            }
        }, cts.Token);

        // If we reach here without timeout, no deadlock occurred
        await Task.WhenAll(saveTask, loadTask);

        storage.Dispose();
        TryDeleteFiles(dbPath);
    }

    #endregion
}
