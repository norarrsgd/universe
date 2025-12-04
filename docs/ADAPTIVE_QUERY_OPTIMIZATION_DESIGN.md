# Adaptive Query Optimization - Design Document

**Status**: Planned for v3.2.0+
**Current Version**: v3.1.1 (Rule-Based Optimization Only)
**Target Release**: v3.2.0
**Author**: Universe Development Team
**Last Updated**: 2025-12-04

---

## Executive Summary

This document outlines the design for implementing adaptive query optimization with historical statistics tracking in Universe. This feature will enhance the existing rule-based optimization by learning from actual query execution patterns to provide data-driven recommendations.

### Current State (v3.1.x)
- ✅ Automatic strategy selection (Direct, Gateway, VectorSearch)
- ✅ Query type detection (7 types)
- ✅ Rule-based optimization hints
- ✅ RU consumption tracking per query
- ✅ Manual hints override

### Planned Enhancement (v3.2.0+)
- 📋 Historical query execution tracking
- 📋 Performance metrics collection
- 📋 Adaptive learning from query patterns
- 📋 Data-driven recommendations
- 📋 Strategy effectiveness comparison

---

## Goals

### Primary Goals
1. **Learn from History**: Track query execution patterns and performance over time
2. **Data-Driven Recommendations**: Provide hints based on actual performance data, not just rules
3. **Strategy Optimization**: Identify which strategies perform best for specific query patterns
4. **Performance Insights**: Surface metrics like average RU, success rate, and execution time

### Non-Goals
1. Real-time query plan optimization (that's Cosmos DB's job)
2. Cross-application statistics sharing
3. Machine learning or predictive modeling (keep it simple)
4. Storage of raw query text (privacy concerns)

---

## Architecture Design

### 1. Statistics Storage Model

```csharp
/// <summary>
/// Represents performance metrics for a single query execution
/// </summary>
internal sealed record QueryExecutionStatistics
{
    /// <summary>
    /// Hash of the query structure (not the raw query text)
    /// </summary>
    public required string QueryHash { get; init; }

    /// <summary>
    /// Detected query type
    /// </summary>
    public required QueryType Type { get; init; }

    /// <summary>
    /// Request Units consumed
    /// </summary>
    public required double RU { get; init; }

    /// <summary>
    /// Total execution time
    /// </summary>
    public required TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Number of results returned
    /// </summary>
    public required int ResultCount { get; init; }

    /// <summary>
    /// Whether the query succeeded
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Timestamp of execution (UTC)
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Strategy used for execution
    /// </summary>
    public required string StrategyUsed { get; init; }

    /// <summary>
    /// Query hints that were applied
    /// </summary>
    public IReadOnlyDictionary<string, object>? HintsUsed { get; init; }
}
```

### 2. Enhanced QueryTuningRecommendations

```csharp
/// <summary>
/// Query tuning recommendations based on historical performance data
/// </summary>
public readonly record struct QueryTuningRecommendations(
    /// <summary>
    /// Suggested query hints optimized for this query type
    /// </summary>
    IReadOnlyDictionary<string, object>? SuggestedHints = null,

    /// <summary>
    /// Recommended execution strategy based on historical performance
    /// </summary>
    string? RecommendedStrategy = null,

    /// <summary>
    /// Average RU consumption for this query type (last 24 hours)
    /// </summary>
    double? AverageRU = null,

    /// <summary>
    /// Success rate for this query type (0.0 to 1.0)
    /// </summary>
    double? SuccessRate = null,

    /// <summary>
    /// Number of historical executions analyzed
    /// </summary>
    int SampleSize = 0,

    /// <summary>
    /// Average execution time
    /// </summary>
    TimeSpan? AverageExecutionTime = null,

    /// <summary>
    /// Whether recommendations are based on actual data (true) or rules (false)
    /// </summary>
    bool IsDataDriven = false
);
```

### 3. Enhanced QueryTuner Implementation

```csharp
internal sealed class QueryTuner
{
    private readonly ConcurrentQueue<QueryExecutionStatistics> _recentExecutions;
    private readonly IQueryStatisticsStorage _storage;
    private const int MaxStoredExecutions = 1000;
    private const int MinSampleSizeForRecommendations = 10;

    public QueryTuner(IQueryStatisticsStorage storage)
    {
        _recentExecutions = new ConcurrentQueue<QueryExecutionStatistics>();
        _storage = storage;

        // Load persisted statistics on startup
        LoadPersistedStatistics();
    }

    /// <summary>
    /// Record a query execution for learning
    /// </summary>
    public void RecordExecution(QueryExecutionStatistics stats)
    {
        // Add to in-memory queue
        _recentExecutions.Enqueue(stats);

        // Maintain size limit
        while (_recentExecutions.Count > MaxStoredExecutions)
        {
            _recentExecutions.TryDequeue(out _);
        }

        // Persist asynchronously (fire and forget)
        _ = _storage.SaveAsync(stats);
    }

    /// <summary>
    /// Get recommendations for a query type
    /// </summary>
    public QueryTuningRecommendations GetRecommendations(QueryType queryType)
    {
        var last24Hours = DateTime.UtcNow.AddHours(-24);

        // Get relevant statistics from last 24 hours
        var relevantStats = _recentExecutions
            .Where(s => s.Type == queryType && s.Timestamp >= last24Hours)
            .ToList();

        // If insufficient data, fall back to rule-based recommendations
        if (relevantStats.Count < MinSampleSizeForRecommendations)
        {
            return GetRuleBasedRecommendations(queryType);
        }

        // Generate data-driven recommendations
        return new QueryTuningRecommendations(
            SuggestedHints: CalculateOptimalHints(relevantStats),
            RecommendedStrategy: DetermineOptimalStrategy(relevantStats),
            AverageRU: relevantStats.Average(s => s.RU),
            SuccessRate: relevantStats.Count(s => s.Success) / (double)relevantStats.Count,
            SampleSize: relevantStats.Count,
            AverageExecutionTime: TimeSpan.FromMilliseconds(
                relevantStats.Average(s => s.ExecutionTime.TotalMilliseconds)
            ),
            IsDataDriven: true
        );
    }

    /// <summary>
    /// Fallback to rule-based recommendations (current v3.1.x behavior)
    /// </summary>
    private QueryTuningRecommendations GetRuleBasedRecommendations(QueryType queryType)
    {
        Dictionary<string, object> hints = GenerateHints(queryType);
        return new(
            SuggestedHints: hints,
            IsDataDriven: false
        );
    }

    /// <summary>
    /// Calculate optimal hints based on historical performance
    /// </summary>
    private Dictionary<string, object> CalculateOptimalHints(List<QueryExecutionStatistics> stats)
    {
        var hints = new Dictionary<string, object>();

        // Analyze performance by different hint configurations
        var hintPerformance = stats
            .Where(s => s.HintsUsed != null)
            .GroupBy(s => GetHintsSignature(s.HintsUsed!))
            .Select(g => new
            {
                Hints = g.First().HintsUsed!,
                AverageRU = g.Average(s => s.RU),
                SuccessRate = g.Count(s => s.Success) / (double)g.Count(),
                Count = g.Count()
            })
            .Where(x => x.Count >= 3) // Require at least 3 samples
            .OrderBy(x => x.AverageRU) // Prefer lower RU
            .ThenByDescending(x => x.SuccessRate) // Then higher success rate
            .FirstOrDefault();

        if (hintPerformance != null)
        {
            foreach (var hint in hintPerformance.Hints)
            {
                hints[hint.Key] = hint.Value;
            }
        }

        return hints;
    }

    /// <summary>
    /// Determine which strategy performs best for this query type
    /// </summary>
    private string DetermineOptimalStrategy(List<QueryExecutionStatistics> stats)
    {
        var strategyPerformance = stats
            .GroupBy(s => s.StrategyUsed)
            .Select(g => new
            {
                Strategy = g.Key,
                AverageRU = g.Average(s => s.RU),
                SuccessRate = g.Count(s => s.Success) / (double)g.Count(),
                Count = g.Count()
            })
            .Where(x => x.Count >= 5) // Require at least 5 samples
            .OrderByDescending(x => x.SuccessRate) // Prefer higher success rate
            .ThenBy(x => x.AverageRU) // Then lower RU
            .FirstOrDefault();

        return strategyPerformance?.Strategy ?? "Auto";
    }

    /// <summary>
    /// Generate a signature for a set of hints (for grouping)
    /// </summary>
    private static string GetHintsSignature(IReadOnlyDictionary<string, object> hints)
    {
        var sorted = hints.OrderBy(kvp => kvp.Key);
        return string.Join("|", sorted.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
    }

    // Current v3.1.x rule-based logic (keep as fallback)
    private static Dictionary<string, object> GenerateHints(QueryType queryType)
    {
        // ... existing implementation ...
    }

    private void LoadPersistedStatistics()
    {
        // Load from storage on startup
        var persisted = _storage.LoadRecentAsync(MaxStoredExecutions).Result;
        foreach (var stat in persisted)
        {
            _recentExecutions.Enqueue(stat);
        }
    }
}
```

### 4. Storage Abstraction

```csharp
/// <summary>
/// Interface for persisting query statistics
/// </summary>
internal interface IQueryStatisticsStorage
{
    /// <summary>
    /// Save a query execution statistic
    /// </summary>
    Task SaveAsync(QueryExecutionStatistics stats);

    /// <summary>
    /// Load recent statistics
    /// </summary>
    Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count);

    /// <summary>
    /// Clear old statistics (older than specified timespan)
    /// </summary>
    Task ClearOldAsync(TimeSpan olderThan);
}

/// <summary>
/// In-memory storage (default, no persistence)
/// </summary>
internal sealed class InMemoryStatisticsStorage : IQueryStatisticsStorage
{
    public Task SaveAsync(QueryExecutionStatistics stats) => Task.CompletedTask;

    public Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count)
        => Task.FromResult<IList<QueryExecutionStatistics>>(Array.Empty<QueryExecutionStatistics>());

    public Task ClearOldAsync(TimeSpan olderThan) => Task.CompletedTask;
}

/// <summary>
/// File-based storage (JSON persistence)
/// </summary>
internal sealed class FileStatisticsStorage : IQueryStatisticsStorage
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileStatisticsStorage(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Directory.GetCurrentDirectory(),
            "query-statistics.json"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public async Task SaveAsync(QueryExecutionStatistics stats)
    {
        await _lock.WaitAsync();
        try
        {
            var existing = await LoadAllAsync();
            existing.Add(stats);

            // Keep only last 1000
            var toSave = existing
                .OrderByDescending(s => s.Timestamp)
                .Take(1000)
                .ToList();

            var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count)
    {
        var all = await LoadAllAsync();
        return all.OrderByDescending(s => s.Timestamp)
                  .Take(count)
                  .ToList();
    }

    public async Task ClearOldAsync(TimeSpan olderThan)
    {
        await _lock.WaitAsync();
        try
        {
            var cutoff = DateTime.UtcNow - olderThan;
            var existing = await LoadAllAsync();
            var toKeep = existing.Where(s => s.Timestamp >= cutoff).ToList();

            var json = JsonSerializer.Serialize(toKeep, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<QueryExecutionStatistics>> LoadAllAsync()
    {
        if (!File.Exists(_filePath))
            return new List<QueryExecutionStatistics>();

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<QueryExecutionStatistics>>(json)
               ?? new List<QueryExecutionStatistics>();
    }
}
```

### 5. Integration in Query Strategies

Each strategy (`DirectQueryStrategy`, `GatewayQueryStrategy`, `VectorSearchStrategy`) needs to be updated to record execution metrics:

```csharp
public async Task<(Gravity gravity, IList<T> results)> ExecuteAsync<T>(QueryContext context)
{
    var stopwatch = Stopwatch.StartNew();
    var queryHash = ComputeQueryHash(context);

    try
    {
        var result = await ExecuteQueryInternal<T>(context);
        stopwatch.Stop();

        // Record successful execution
        _queryTuner.RecordExecution(new QueryExecutionStatistics
        {
            QueryHash = queryHash,
            Type = context.Type,
            RU = result.gravity.RU,
            ExecutionTime = stopwatch.Elapsed,
            ResultCount = result.results.Count,
            Success = true,
            Timestamp = DateTime.UtcNow,
            StrategyUsed = this.GetType().Name,
            HintsUsed = context.Hints?.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)
        });

        return result;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();

        // Record failed execution
        _queryTuner.RecordExecution(new QueryExecutionStatistics
        {
            QueryHash = queryHash,
            Type = context.Type,
            RU = 0,
            ExecutionTime = stopwatch.Elapsed,
            ResultCount = 0,
            Success = false,
            Timestamp = DateTime.UtcNow,
            StrategyUsed = this.GetType().Name,
            HintsUsed = context.Hints?.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)
        });

        throw;
    }
}

private static string ComputeQueryHash(QueryContext context)
{
    // Hash the query structure (not the values) for pattern recognition
    var signature = $"{context.Type}|{context.Clusters?.Count ?? 0}|{context.ColumnOptions?.Top ?? 0}";
    using var sha256 = SHA256.Create();
    var bytes = Encoding.UTF8.GetBytes(signature);
    var hash = sha256.ComputeHash(bytes);
    return Convert.ToBase64String(hash).Substring(0, 16);
}
```

---

## Configuration

### Enable Statistics Tracking

Always enabled by default in v3.2.0+. Minimal overhead.

### Choose Storage Backend

```csharp
// Option 1: In-memory only (default, no persistence)
GalaxyOptions options = new()
{
    StatisticsStorage = new InMemoryStatisticsStorage()
};

// Option 2: File-based persistence
GalaxyOptions options = new()
{
    StatisticsStorage = new FileStatisticsStorage()
};

// Option 3: Custom file path
GalaxyOptions options = new()
{
    StatisticsStorage = new FileStatisticsStorage("/custom/path/stats.json")
};
```

### Maintenance

Automatic cleanup of old statistics:

```csharp
// In Galaxy initialization
_ = Task.Run(async () =>
{
    while (!_cancellationToken.IsCancellationRequested)
    {
        await Task.Delay(TimeSpan.FromHours(6)); // Every 6 hours
        await _statisticsStorage.ClearOldAsync(TimeSpan.FromDays(7)); // Keep 7 days
    }
});
```

---

## Performance Considerations

### Memory Impact
- **In-memory storage**: ~200 KB for 1000 entries (200 bytes per entry)
- **File storage**: ~200 KB JSON file (gzip would reduce to ~50 KB)
- **Bounded**: Fixed size limit (1000 entries, 24-hour window)

### CPU Impact
- **Recording**: < 1ms per query (async, fire-and-forget)
- **Analysis**: < 10ms when getting recommendations (runs infrequently)
- **File I/O**: Async, doesn't block query execution

### Concurrency
- Thread-safe `ConcurrentQueue` for in-memory storage
- `SemaphoreSlim` for file writes
- No locks on the hot path (query execution)

---

## Migration Path

### Phase 1: v3.2.0 - Core Implementation
- ✅ Add `QueryExecutionStatistics` model
- ✅ Enhance `QueryTuningRecommendations` with new properties
- ✅ Update `QueryTuner` with adaptive logic
- ✅ Implement in-memory + file storage
- ✅ Integrate recording in all strategies
- ✅ Update examples and documentation

### Phase 2: v3.2.1 - Enhancements
- 📋 Add export/import functionality for statistics
- 📋 Provide dashboard/visualization helper methods
- 📋 Add more granular query pattern hashing

### Phase 3: v3.3.0+ - Advanced Features (Optional)
- 📋 Cosmos DB-based storage option (store in dedicated container)
- 📋 Redis-based storage for distributed scenarios
- 📋 Cross-application statistics aggregation (opt-in)

---

## Breaking Changes

### API Changes (Backward Compatible)

**Before (v3.1.x):**
```csharp
QueryTuningRecommendations recommendations = galaxy.GetQueryRecommendations("pattern", QueryType.Simple);
// Only has SuggestedHints
```

**After (v3.2.0):**
```csharp
QueryTuningRecommendations recommendations = galaxy.GetQueryRecommendations("pattern", QueryType.Simple);
// Now has SuggestedHints, RecommendedStrategy, AverageRU, SuccessRate, SampleSize, etc.
Console.WriteLine($"Average RU: {recommendations.AverageRU}");
Console.WriteLine($"Success Rate: {recommendations.SuccessRate:P2}");
Console.WriteLine($"Based on {recommendations.SampleSize} samples");
```

**Compatibility**: Existing code continues to work. New properties are nullable.

---

## Testing Strategy

### Unit Tests
- Test statistics recording
- Test recommendation calculation logic
- Test storage implementations (in-memory and file)
- Test fallback to rule-based when insufficient data

### Integration Tests
- Test full query execution with statistics tracking
- Test recommendation accuracy after N executions
- Test cleanup of old statistics

### Performance Tests
- Measure overhead of statistics recording
- Measure memory usage with 1000 entries
- Measure file I/O performance

---

## Documentation Updates

### Files to Update
1. `QUERY_EXECUTION_STRATEGIES.md` - Update to describe adaptive learning
2. `Example13_QueryOptimization.cs` - Show new properties in action
3. `README.md` - Highlight adaptive learning in features
4. `CHANGELOG.md` - Document in v3.2.0 release notes

### New Documentation
1. `ADAPTIVE_OPTIMIZATION_GUIDE.md` - Deep dive on adaptive learning
2. API docs for new types and properties

---

## Privacy & Security Considerations

### What We Store
✅ Query structure hash (not raw query text)
✅ Query type
✅ Performance metrics (RU, time, result count)
✅ Strategy used
✅ Hints applied
❌ **NOT stored**: Raw query text, parameter values, result data

### Data Retention
- In-memory: Until application restart
- File: Automatically cleaned up after 7 days
- User can delete the statistics file at any time

### Opt-Out
While always enabled, users can:
1. Use in-memory storage only (no persistence)
2. Delete the statistics file
3. Ignore the additional recommendation properties

---

## Open Questions

1. **Should we hash query parameter types?** (e.g., int vs string filters)
2. **Should we track correlation between hint combinations?** (might be complex)
3. **Should we provide a way to reset/clear statistics via API?**
4. **Should we add telemetry opt-in for aggregate anonymous statistics?**

---

## Success Metrics

After implementation, we'll measure:
- Adoption rate (% of users with file storage enabled)
- Recommendation accuracy (do data-driven hints reduce RU?)
- Performance overhead (< 1% degradation target)
- User feedback on adaptive vs. rule-based recommendations

---

## Conclusion

This design enhances Universe's query optimization from rule-based to adaptive learning while:
- ✅ Maintaining backward compatibility
- ✅ Keeping implementation simple (no ML complexity)
- ✅ Respecting user privacy (no raw query storage)
- ✅ Providing immediate value (better RU optimization)
- ✅ Allowing incremental adoption (in-memory → file → database)

**Next Steps**: Review this design, gather feedback, and begin implementation for v3.2.0.

---

## Related Documents

- `/QUERY_EXECUTION_STRATEGIES.md` - Current feature documentation
- `/QUERY_OPTIMIZER_ANALYSIS.md` - Gap analysis that motivated this design
- `/code/Universe/Builder/Strategies/` - Current implementation

---

**Document Version**: 1.0
**Status**: Draft - Awaiting Review
**Contact**: Universe Development Team
