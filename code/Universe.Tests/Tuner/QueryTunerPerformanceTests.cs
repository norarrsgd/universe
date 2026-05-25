using System.Diagnostics;
using Universe.Builder.Strategies;
using Universe.Builder.Strategies.Storage;
using Universe.Tests.Helpers;
using Xunit;

namespace Universe.Tests.Tuner;

public sealed class QueryTunerPerformanceTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    [Trait("Category", "Performance")]
    public void GetRecommendations_EmptyQueueVsLoadedQueue_ShowsOverhead()
    {
        // Baseline: empty queue
        using QueryTuner emptyTuner = new QueryTuner();
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
            emptyTuner.GetRecommendations(QueryType.Simple);
        sw.Stop();
        TimeSpan emptyTime = sw.Elapsed;

        // Loaded: 1000-item queue
        using QueryTuner loadedTuner = new QueryTuner();
        for (int i = 0; i < 1000; i++)
            loadedTuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple,
                timestamp: DateTime.UtcNow));

        sw.Restart();
        for (int i = 0; i < 1000; i++)
            loadedTuner.GetRecommendations(QueryType.Simple);
        sw.Stop();
        TimeSpan loadedTime = sw.Elapsed;

        _output.WriteLine($"Empty queue: {emptyTime.TotalMilliseconds:F2}ms for 1000 calls");
        _output.WriteLine($"Loaded queue (1000 items): {loadedTime.TotalMilliseconds:F2}ms for 1000 calls");
        _output.WriteLine($"Ratio: {loadedTime.TotalMilliseconds / Math.Max(emptyTime.TotalMilliseconds, 0.001):F2}x");

        // Loaded should be measurably slower due to LINQ iteration over the full queue
        // Use ratio threshold to tolerate CI variability
        double ratio = loadedTime.TotalMilliseconds / Math.Max(emptyTime.TotalMilliseconds, 0.001);
        Assert.True(ratio > 1.1 || loadedTime.TotalMilliseconds > emptyTime.TotalMilliseconds + 0.5,
            $"Expected loaded ({loadedTime.TotalMilliseconds:F2}ms) to be meaningfully slower than empty ({emptyTime.TotalMilliseconds:F2}ms), ratio: {ratio:F2}x");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void GetRecommendations_LinearScaling_TimingsIncreaseWithQueueSize()
    {
        int[] sizes = [0, 100, 250, 500, 750, 1000];
        Dictionary<int, double> timings = new Dictionary<int, double>();

        using QueryTuner tuner = new QueryTuner();
        int currentSize = 0;

        foreach (int targetSize in sizes)
        {
            while (currentSize < targetSize)
            {
                tuner.RecordExecution(TestStatisticsFactory.Create(
                    type: QueryType.Simple,
                    timestamp: DateTime.UtcNow));
                currentSize++;
            }

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
                tuner.GetRecommendations(QueryType.Simple);
            sw.Stop();

            timings[targetSize] = sw.Elapsed.TotalMilliseconds;
            _output.WriteLine($"Queue size {targetSize,4}: {sw.Elapsed.TotalMilliseconds:F2}ms for 100 calls");
        }

        // Verify general upward trend with tolerance for CI variability
        double scalingRatio = timings[1000] / Math.Max(timings[0], 0.001);
        Assert.True(scalingRatio > 1.1 || timings[1000] > timings[0] + 0.5,
            $"Expected 1000-item timing ({timings[1000]:F2}ms) to be meaningfully slower than 0-item timing ({timings[0]:F2}ms), ratio: {scalingRatio:F2}x");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void GetRecommendations_CumulativeOverhead_WithFullQueue()
    {
        using QueryTuner tuner = new QueryTuner();
        for (int i = 0; i < 1000; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple,
                timestamp: DateTime.UtcNow));

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
            tuner.GetRecommendations(QueryType.Simple);
        sw.Stop();

        _output.WriteLine($"1000 GetRecommendations calls with 1000-item queue: {sw.Elapsed.TotalMilliseconds:F2}ms total");
        _output.WriteLine($"Per call: {sw.Elapsed.TotalMilliseconds / 1000:F4}ms");
        _output.WriteLine("This overhead is added to EVERY Cosmos DB query when SQLite storage is configured.");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void GetRecommendations_MixedTypes_UnrelatedTypeIsNotAffected()
    {
        using QueryTuner tuner = new QueryTuner();

        // Load 950 Simple entries and 50 Aggregation entries
        for (int i = 0; i < 950; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple, queryHash: $"simple-{i}",
                timestamp: DateTime.UtcNow));
        for (int i = 0; i < 50; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Aggregation, queryHash: $"agg-{i}",
                timestamp: DateTime.UtcNow));

        // Measure 1000 calls for Simple (950-item partition)
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
            tuner.GetRecommendations(QueryType.Simple);
        sw.Stop();
        TimeSpan simpleTime = sw.Elapsed;

        // Measure 1000 calls for Aggregation (50-item partition)
        sw.Restart();
        for (int i = 0; i < 1000; i++)
            tuner.GetRecommendations(QueryType.Aggregation);
        sw.Stop();
        TimeSpan aggTime = sw.Elapsed;

        _output.WriteLine($"Simple (950 items): {simpleTime.TotalMilliseconds:F2}ms for 1000 calls");
        _output.WriteLine($"Aggregation (50 items): {aggTime.TotalMilliseconds:F2}ms for 1000 calls");
        _output.WriteLine($"Ratio: {simpleTime.TotalMilliseconds / Math.Max(aggTime.TotalMilliseconds, 0.001):F2}x");

        // Aggregation with 50 items should be faster than Simple with 950 items
        // Use ratio threshold to tolerate CI variability
        double typeRatio = simpleTime.TotalMilliseconds / Math.Max(aggTime.TotalMilliseconds, 0.001);
        Assert.True(typeRatio > 1.1 || simpleTime.TotalMilliseconds > aggTime.TotalMilliseconds + 0.5,
            $"Expected Simple ({simpleTime.TotalMilliseconds:F2}ms) to be meaningfully slower than Aggregation ({aggTime.TotalMilliseconds:F2}ms), ratio: {typeRatio:F2}x");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void ComputeQueryHash_10000Calls_Performance()
    {
        QueryContext context = new QueryContext(QueryType.Simple);
        string queryText = "SELECT * FROM c WHERE c.id = @p1 AND c.name = @p2 AND c.status = @p3";

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
            QueryTuner.ComputeQueryHash(context, queryText);
        sw.Stop();

        _output.WriteLine($"10000 ComputeQueryHash calls: {sw.Elapsed.TotalMilliseconds:F2}ms");
        _output.WriteLine($"Per call: {sw.Elapsed.TotalMilliseconds / 10000:F4}ms");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void RecordExecution_WithSqlite_StaysResponsive()
    {
        string dbPath = Path.Combine(AppContext.BaseDirectory, $"perf-record-{Guid.NewGuid()}.db");

        using (SqliteStatisticsStorage storage = new SqliteStatisticsStorage(dbPath, batchSize: 10, flushIntervalSeconds: 60))
        using (QueryTuner tuner = new QueryTuner(storage))
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
                tuner.RecordExecution(TestStatisticsFactory.Create(queryHash: $"perf-{i}"));
            sw.Stop();

            _output.WriteLine($"100 RecordExecution calls with SQLite: {sw.Elapsed.TotalMilliseconds:F2}ms");
            _output.WriteLine($"Per call: {sw.Elapsed.TotalMilliseconds / 100:F4}ms");

            // Fire-and-forget persistence should keep RecordExecution fast
            Assert.True(sw.Elapsed.TotalMilliseconds < 1000,
                $"100 RecordExecution calls took {sw.Elapsed.TotalMilliseconds:F2}ms, expected < 1000ms");
        }

        TryDeleteFiles(dbPath);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task StartupLoad_SqliteVsInMemory_MeasuresImpact()
    {
        string dbPath = Path.Combine(AppContext.BaseDirectory, $"perf-startup-{Guid.NewGuid()}.db");

        // Pre-seed SQLite with 1000 records
        using (SqliteStatisticsStorage seedStorage = new SqliteStatisticsStorage(dbPath, batchSize: 50, flushIntervalSeconds: 60))
        {
            for (int i = 0; i < 1000; i++)
                await seedStorage.SaveAsync(TestStatisticsFactory.Create(
                    type: QueryType.Simple,
                    queryHash: $"startup-{i}",
                    timestamp: DateTime.UtcNow.AddMinutes(-i)));

            // Ensure flushed
            await seedStorage.LoadRecentAsync(1);
        }

        // Measure InMemory startup
        Stopwatch sw = Stopwatch.StartNew();
        using QueryTuner inMemoryTuner = new QueryTuner();
        await Task.Delay(100, TestContext.Current.CancellationToken); // Give async loading a chance
        QueryTuningRecommendations inMemoryRec = inMemoryTuner.GetRecommendations(QueryType.Simple);
        sw.Stop();
        TimeSpan inMemoryTime = sw.Elapsed;

        // Measure SQLite startup (loading 1000 records into queue)
        QueryTuningRecommendations sqliteRec;
        TimeSpan sqliteTime;
        using (SqliteStatisticsStorage sqliteStorage = new SqliteStatisticsStorage(dbPath, batchSize: 50, flushIntervalSeconds: 60))
        using (QueryTuner sqliteTuner = new QueryTuner(sqliteStorage))
        {
            sw.Restart();

            // Wait for async loading to complete
            sqliteRec = default;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                sqliteRec = sqliteTuner.GetRecommendations(QueryType.Simple);
                if (sqliteRec.IsDataDriven) break;
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
            sw.Stop();
            sqliteTime = sw.Elapsed;
        }

        _output.WriteLine($"InMemory startup: {inMemoryTime.TotalMilliseconds:F2}ms (IsDataDriven: {inMemoryRec.IsDataDriven})");
        _output.WriteLine($"SQLite startup (1000 records): {sqliteTime.TotalMilliseconds:F2}ms (IsDataDriven: {sqliteRec.IsDataDriven})");

        Assert.True(sqliteRec.IsDataDriven,
            "SQLite should provide data-driven recommendations after loading persisted data");
        Assert.False(inMemoryRec.IsDataDriven,
            "InMemory should fall back to rule-based (no persisted data)");

        TryDeleteFiles(dbPath);
    }

    private static void TryDeleteFiles(string dbPath)
    {
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            try { if (File.Exists(dbPath + suffix)) File.Delete(dbPath + suffix); }
            catch { /* best effort cleanup */ }
        }
    }
}
