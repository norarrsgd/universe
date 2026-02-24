using Universe.Builder.Strategies;
using Universe.Builder.Strategies.Storage;
using Universe.Tests.Helpers;
using Xunit;

namespace Universe.Tests.Tuner;

public sealed class QueryTunerTests
{
    #region Queue management

    [Fact]
    public void RecordExecution_CapsAt1000Entries()
    {
        using var tuner = new QueryTuner();

        // Record 200 Aggregation entries first
        for (int i = 0; i < 200; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Aggregation, queryHash: $"agg-{i}"));

        // Record 1000 Simple entries — should evict all Aggregation entries
        for (int i = 0; i < 1000; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple, queryHash: $"simple-{i}"));

        // Aggregation entries should be fully evicted
        var aggRec = tuner.GetRecommendations(QueryType.Aggregation);
        Assert.False(aggRec.IsDataDriven,
            "Aggregation records should have been evicted by queue cap");

        // Simple entries have enough data for data-driven
        var simpleRec = tuner.GetRecommendations(QueryType.Simple);
        Assert.True(simpleRec.IsDataDriven,
            "Simple records should provide data-driven recommendations");
    }

    [Fact]
    public void GetRecommendations_OnlyConsidersEntriesOfRequestedType()
    {
        using var tuner = new QueryTuner();

        // Record 200 Simple, 200 Aggregation, 100 VectorSearch
        for (int i = 0; i < 200; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple, queryHash: $"simple-{i}"));
        for (int i = 0; i < 200; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Aggregation, queryHash: $"agg-{i}"));
        for (int i = 0; i < 100; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.VectorSearch, queryHash: $"vec-{i}"));

        var simpleRec = tuner.GetRecommendations(QueryType.Simple);
        var aggRec = tuner.GetRecommendations(QueryType.Aggregation);
        var vecRec = tuner.GetRecommendations(QueryType.VectorSearch);
        var joinRec = tuner.GetRecommendations(QueryType.Join);

        Assert.True(simpleRec.IsDataDriven);
        Assert.Equal(200, simpleRec.SampleSize);

        Assert.True(aggRec.IsDataDriven);
        Assert.Equal(200, aggRec.SampleSize);

        Assert.True(vecRec.IsDataDriven);
        Assert.Equal(100, vecRec.SampleSize);

        // Join has no entries — should be rule-based
        Assert.False(joinRec.IsDataDriven);
    }

    [Fact]
    public void RecordExecution_GlobalCapAppliesAcrossAllTypes()
    {
        using var tuner = new QueryTuner();

        // Record 500 Simple (oldest), then 500 Aggregation, then 100 VectorSearch
        for (int i = 0; i < 500; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple, queryHash: $"simple-{i}",
                timestamp: DateTime.UtcNow.AddMinutes(-1000 + i)));
        for (int i = 0; i < 500; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Aggregation, queryHash: $"agg-{i}",
                timestamp: DateTime.UtcNow.AddMinutes(-500 + i)));
        for (int i = 0; i < 100; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.VectorSearch, queryHash: $"vec-{i}",
                timestamp: DateTime.UtcNow.AddMinutes(-i)));

        // Total inserted: 1100 → 100 oldest (Simple) should be evicted
        var simpleRec = tuner.GetRecommendations(QueryType.Simple);
        var aggRec = tuner.GetRecommendations(QueryType.Aggregation);
        var vecRec = tuner.GetRecommendations(QueryType.VectorSearch);

        // Simple should have lost 100 entries (evicted as oldest)
        Assert.True(simpleRec.IsDataDriven);
        Assert.Equal(400, simpleRec.SampleSize);

        // Aggregation should be untouched
        Assert.True(aggRec.IsDataDriven);
        Assert.Equal(500, aggRec.SampleSize);

        // VectorSearch should be untouched
        Assert.True(vecRec.IsDataDriven);
        Assert.Equal(100, vecRec.SampleSize);
    }

    #endregion

    #region Recommendations: rule-based vs data-driven

    [Fact]
    public void GetRecommendations_LessThan10Samples_ReturnsRuleBased()
    {
        using var tuner = new QueryTuner();
        for (int i = 0; i < 9; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(type: QueryType.Simple));

        var rec = tuner.GetRecommendations(QueryType.Simple);

        Assert.False(rec.IsDataDriven);
        Assert.Equal(0, rec.SampleSize);
    }

    [Fact]
    public void GetRecommendations_AtLeast10Samples_ReturnsDataDriven()
    {
        using var tuner = new QueryTuner();
        for (int i = 0; i < 15; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple,
                ru: 10.0 + i,
                executionTime: TimeSpan.FromMilliseconds(100 + i * 10),
                success: i < 12));

        var rec = tuner.GetRecommendations(QueryType.Simple);

        Assert.True(rec.IsDataDriven);
        Assert.Equal(15, rec.SampleSize);
        Assert.NotNull(rec.AverageRU);
        Assert.NotNull(rec.SuccessRate);
        Assert.NotNull(rec.AverageExecutionTime);
    }

    #endregion

    #region Time filtering

    [Fact]
    public void GetRecommendations_OnlyConsidersLast24Hours()
    {
        using var tuner = new QueryTuner();

        // Add 15 records older than 24 hours
        for (int i = 0; i < 15; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple,
                timestamp: DateTime.UtcNow.AddHours(-25)));

        var rec = tuner.GetRecommendations(QueryType.Simple);
        Assert.False(rec.IsDataDriven);
    }

    #endregion

    #region Correctness of calculations

    [Fact]
    public void GetRecommendations_CorrectAverageRU()
    {
        using var tuner = new QueryTuner();
        double[] rus = [10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0, 90.0, 100.0];
        for (int i = 0; i < rus.Length; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple, ru: rus[i]));

        var rec = tuner.GetRecommendations(QueryType.Simple);

        Assert.True(rec.IsDataDriven);
        Assert.Equal(55.0, rec.AverageRU);
    }

    [Fact]
    public void GetRecommendations_CorrectSuccessRate()
    {
        using var tuner = new QueryTuner();
        for (int i = 0; i < 10; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple, success: i < 7));

        var rec = tuner.GetRecommendations(QueryType.Simple);

        Assert.True(rec.IsDataDriven);
        Assert.Equal(0.7, rec.SuccessRate);
    }

    [Fact]
    public void GetRecommendations_CorrectAverageExecutionTime()
    {
        using var tuner = new QueryTuner();
        for (int i = 0; i < 10; i++)
            tuner.RecordExecution(TestStatisticsFactory.Create(
                type: QueryType.Simple,
                executionTime: TimeSpan.FromMilliseconds(100 * (i + 1))));

        var rec = tuner.GetRecommendations(QueryType.Simple);

        Assert.True(rec.IsDataDriven);
        // Average of 100, 200, ..., 1000 = 550ms
        Assert.Equal(550.0, rec.AverageExecutionTime!.Value.TotalMilliseconds);
    }

    #endregion

    #region Rule-based hints per QueryType

    [Fact]
    public void GetRuleBasedRecommendations_VectorSearch_HasCorrectHints()
    {
        using var tuner = new QueryTuner();
        var rec = tuner.GetRecommendations(QueryType.VectorSearch);

        Assert.False(rec.IsDataDriven);
        Assert.NotNull(rec.SuggestedHints);
        Assert.True(rec.SuggestedHints.ContainsKey("MaxItemCount"));
        Assert.True(rec.SuggestedHints.ContainsKey("MaxBufferedItemCount"));
    }

    [Fact]
    public void GetRuleBasedRecommendations_FullTextSearch_HasCorrectHints()
    {
        using var tuner = new QueryTuner();
        var rec = tuner.GetRecommendations(QueryType.FullTextSearch);

        Assert.False(rec.IsDataDriven);
        Assert.NotNull(rec.SuggestedHints);
        Assert.True(rec.SuggestedHints.ContainsKey("MaxItemCount"));
    }

    [Fact]
    public void GetRuleBasedRecommendations_Aggregation_HasCorrectHints()
    {
        using var tuner = new QueryTuner();
        var rec = tuner.GetRecommendations(QueryType.Aggregation);

        Assert.False(rec.IsDataDriven);
        Assert.NotNull(rec.SuggestedHints);
        Assert.Equal(500, rec.SuggestedHints["MaxItemCount"]);
        Assert.Equal(1000, rec.SuggestedHints["MaxBufferedItemCount"]);
    }

    [Fact]
    public void GetRuleBasedRecommendations_Complex_HasCorrectHints()
    {
        using var tuner = new QueryTuner();
        var rec = tuner.GetRecommendations(QueryType.Complex);

        Assert.False(rec.IsDataDriven);
        Assert.NotNull(rec.SuggestedHints);
        Assert.Equal(1, rec.SuggestedHints["MaxConcurrency"]);
        Assert.Equal(1, rec.SuggestedHints["ResponseContinuationTokenLimitInKb"]);
    }

    [Fact]
    public void GetRuleBasedRecommendations_Simple_EmptyHints()
    {
        using var tuner = new QueryTuner();
        var rec = tuner.GetRecommendations(QueryType.Simple);

        Assert.False(rec.IsDataDriven);
        Assert.NotNull(rec.SuggestedHints);
        Assert.Empty(rec.SuggestedHints);
    }

    #endregion

    #region ComputeQueryHash

    [Fact]
    public void ComputeQueryHash_Deterministic()
    {
        var context = new QueryContext(QueryType.Simple);
        string query = "SELECT * FROM c WHERE c.id = @p1";

        string hash1 = QueryTuner.ComputeQueryHash(context, query);
        string hash2 = QueryTuner.ComputeQueryHash(context, query);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeQueryHash_NormalizesParameterNames()
    {
        var context = new QueryContext(QueryType.Simple);
        string query1 = "SELECT * FROM c WHERE c.id = @param_abc123";
        string query2 = "SELECT * FROM c WHERE c.id = @param_xyz789";

        string hash1 = QueryTuner.ComputeQueryHash(context, query1);
        string hash2 = QueryTuner.ComputeQueryHash(context, query2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeQueryHash_DifferentQueries_DifferentHashes()
    {
        var context = new QueryContext(QueryType.Simple);
        string query1 = "SELECT * FROM c WHERE c.id = @p1";
        string query2 = "SELECT * FROM c WHERE c.name = @p1";

        string hash1 = QueryTuner.ComputeQueryHash(context, query1);
        string hash2 = QueryTuner.ComputeQueryHash(context, query2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeQueryHash_DifferentQueryTypes_DifferentHashes()
    {
        var context1 = new QueryContext(QueryType.Simple);
        var context2 = new QueryContext(QueryType.Aggregation);
        string query = "SELECT * FROM c WHERE c.id = @p1";

        string hash1 = QueryTuner.ComputeQueryHash(context1, query);
        string hash2 = QueryTuner.ComputeQueryHash(context2, query);

        Assert.NotEqual(hash1, hash2);
    }

    #endregion

    #region Startup loading

    [Fact]
    public async Task Constructor_LoadsPersistedStatisticsFromSqlite()
    {
        string dbPath = Path.Combine(AppContext.BaseDirectory, $"test-startup-{Guid.NewGuid()}.db");

        // Pre-seed SQLite with 15 Simple records
        using (var seedStorage = new SqliteStatisticsStorage(dbPath, batchSize: 1, flushIntervalSeconds: 60))
        {
            for (int i = 0; i < 15; i++)
                await seedStorage.SaveAsync(TestStatisticsFactory.Create(
                    type: QueryType.Simple,
                    queryHash: $"startup-{i}",
                    timestamp: DateTime.UtcNow.AddMinutes(-i)));

            // Ensure flushed
            await seedStorage.LoadRecentAsync(1);
        }

        // Create new QueryTuner with fresh SQLite storage
        using var storage = new SqliteStatisticsStorage(dbPath, batchSize: 1, flushIntervalSeconds: 60);
        using var tuner = new QueryTuner(storage);

        // Wait for async loading (poll until data-driven or timeout)
        QueryTuningRecommendations rec = default;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            rec = tuner.GetRecommendations(QueryType.Simple);
            if (rec.IsDataDriven) break;
            await Task.Delay(100);
        }

        Assert.True(rec.IsDataDriven,
            "QueryTuner should load persisted statistics on startup");
        Assert.Equal(15, rec.SampleSize);

        TryDeleteFiles(dbPath);
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_DisposesUnderlyingStorage()
    {
        var trackable = new TrackingDisposableStorage();
        var tuner = new QueryTuner(trackable);

        tuner.Dispose();

        Assert.True(trackable.IsDisposed);
    }

    private sealed class TrackingDisposableStorage : IQueryStatisticsStorage, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public Task SaveAsync(QueryExecutionStatistics stats) => Task.CompletedTask;
        public Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count)
            => Task.FromResult<IList<QueryExecutionStatistics>>([]);
        public Task<IList<QueryExecutionStatistics>> GetByQueryHashAsync(string queryHash, TimeSpan window)
            => Task.FromResult<IList<QueryExecutionStatistics>>([]);
        public Task ClearOldAsync(TimeSpan olderThan) => Task.CompletedTask;
        public void Dispose() => IsDisposed = true;
    }

    #endregion

    private static void TryDeleteFiles(string dbPath)
    {
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            try { if (File.Exists(dbPath + suffix)) File.Delete(dbPath + suffix); }
            catch { /* best effort cleanup */ }
        }
    }
}
