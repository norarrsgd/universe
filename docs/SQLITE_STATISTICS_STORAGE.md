# SQLite Statistics Storage

This document explains the SQLite-based statistics storage feature in Universe. This storage backend provides high-performance, persistent query statistics with minimal configuration.

## Version History

- **v3.2.0+**: SQLite storage option added

## Overview

SQLite statistics storage is a lightweight, high-performance option for persisting query execution statistics. It offers significant advantages over file-based JSON storage for applications with high query volumes or those requiring efficient historical analysis.

## Key Features

- **WAL Mode**: Write-Ahead Logging for concurrent read/write performance
- **Non-blocking Writes**: Fire-and-forget pattern doesn't slow down query execution
- **Batched Inserts**: Configurable batching for efficient disk I/O
- **Auto-cleanup**: Automatic removal of old statistics based on retention policy
- **Indexed Queries**: Fast lookups by query hash and timestamp
- **Single File**: All data stored in one portable `.db` file

## Quick Start

### Basic Usage

```csharp
// Default configuration - creates universe-stats.db in app directory
UniverseOptions options = UniverseOptions.WithSqlitePersistence();

public class MyRepository : Galaxy<MyObject>
{
    public MyRepository(CosmosClient client)
        : base(client, "database", "container", ["partitionKey"], options)
    {
    }
}
```

### Custom Path

```csharp
UniverseOptions options = UniverseOptions.WithSqlitePersistence("/data/cosmos-stats.db");
```

### With Retention Policy

```csharp
// Keep statistics for 14 days (default is 7)
UniverseOptions options = UniverseOptions.WithSqlitePersistence(retentionDays: 14);
```

### Full Configuration

```csharp
UniverseOptions options = UniverseOptions.WithSqlitePersistence(
    path: "/data/cosmos-stats.db",  // Custom database location
    retentionDays: 30,              // Keep 30 days of history
    batchSize: 20,                  // Flush every 20 writes
    flushIntervalSeconds: 10        // Or every 10 seconds
);
```

## Configuration Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `path` | `string` | `{AppDirectory}/universe-stats.db` | Path to SQLite database file |
| `retentionDays` | `int` | `7` | Days to retain statistics before auto-cleanup |
| `batchSize` | `int` | `10` | Number of records to batch before flushing |
| `flushIntervalSeconds` | `int` | `5` | Seconds between automatic flushes |

## Architecture

### Database Schema

```sql
CREATE TABLE query_statistics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    query_hash TEXT NOT NULL,
    query_type INTEGER NOT NULL,
    ru REAL NOT NULL,
    execution_time_ms INTEGER NOT NULL,
    result_count INTEGER NOT NULL,
    success INTEGER NOT NULL,
    timestamp INTEGER NOT NULL,  -- Unix timestamp
    strategy_used TEXT,
    hints_used TEXT              -- JSON serialized
);

-- Indexes for fast lookups
CREATE INDEX idx_hash_timestamp ON query_statistics(query_hash, timestamp DESC);
CREATE INDEX idx_timestamp ON query_statistics(timestamp);
```

### Write Strategy

The SQLite storage uses a non-blocking write pattern:

1. **Enqueue**: `SaveAsync()` adds statistics to an in-memory queue and returns immediately
2. **Batch**: Records accumulate until batch size is reached
3. **Flush**: Batched records are written in a single transaction
4. **Timer**: A background timer ensures data is flushed even with low query volume

```
Query Execution
      |
      v
  SaveAsync() ──> Queue ──> Batch Full? ──> FlushAsync()
      |                         |                |
      v                         v                v
   Return                    Timer           Transaction
  Immediately              (5 sec)            Commit
```

### Read Strategy

Reads flush pending writes first to ensure consistency:

```csharp
// LoadRecentAsync flushes queue before reading
IList<QueryExecutionStatistics> recent = await storage.LoadRecentAsync(100);

// GetByQueryHashAsync also flushes first
IList<QueryExecutionStatistics> forQuery = await storage.GetByQueryHashAsync(
    "abc123hash",
    TimeSpan.FromHours(24)
);
```

## Performance Characteristics

### Compared to File Storage (JSON)

| Aspect | File Storage | SQLite Storage |
|--------|--------------|----------------|
| Write latency | Blocking | Non-blocking |
| Write throughput | ~10-50/sec | ~1000+/sec |
| Read by hash | O(n) scan | O(log n) index |
| Concurrent access | Lock contention | WAL mode |
| Disk usage | Verbose JSON | Compact binary |
| Startup time | Parse entire file | Instant |

### Benchmarks (Approximate)

- **Write**: < 1ms (queued, non-blocking)
- **Flush 10 records**: ~5-10ms
- **Read recent 100**: ~2-5ms
- **Read by hash (24h window)**: ~1-3ms

## Best Practices

### For Development

```csharp
// Use default settings for quick setup
UniverseOptions options = UniverseOptions.WithSqlitePersistence();
```

### For Production

```csharp
// Longer retention, larger batches, less frequent flushes
UniverseOptions options = UniverseOptions.WithSqlitePersistence(
    path: "/var/data/universe/stats.db",
    retentionDays: 30,
    batchSize: 50,
    flushIntervalSeconds: 30
);
```

### For High-Volume Applications

```csharp
// Larger batches reduce I/O overhead
UniverseOptions options = UniverseOptions.WithSqlitePersistence(
    batchSize: 100,
    flushIntervalSeconds: 60
);
```

### For Real-Time Analytics

```csharp
// Smaller batches, frequent flushes for fresher data
UniverseOptions options = UniverseOptions.WithSqlitePersistence(
    batchSize: 5,
    flushIntervalSeconds: 2
);
```

## Storage Comparison

| Storage Type | Best For | Persistence | Performance |
|-------------|----------|-------------|-------------|
| **In-memory** | Development, testing | None | Fastest reads |
| **File (JSON)** | Simple apps, debugging | Yes | Human-readable |
| **SQLite** | Production, high volume | Yes | Best write throughput |

## Cleanup and Maintenance

### Automatic Cleanup

Old records are automatically deleted on startup:

```csharp
// Records older than retentionDays are deleted when storage initializes
UniverseOptions options = UniverseOptions.WithSqlitePersistence(retentionDays: 7);
```

### Manual Cleanup

```csharp
// Clear records older than 3 days
await storage.ClearOldAsync(TimeSpan.FromDays(3));
```

### Database Size

Typical record size: ~200-500 bytes

Estimated sizes:
- 10,000 records: ~3-5 MB
- 100,000 records: ~30-50 MB
- 1,000,000 records: ~300-500 MB

## Disposal

The SQLite storage implements `IDisposable`. Proper disposal ensures all pending writes are flushed:

```csharp
// Manual disposal
if (options.StatisticsStorage is IDisposable disposable)
{
    disposable.Dispose();
}

// Or use dependency injection with proper lifetime management
services.AddSingleton(UniverseOptions.WithSqlitePersistence());
```

## Troubleshooting

### Database Locked

If you see "database is locked" errors:
- Ensure only one process accesses the database file
- Check that the previous process disposed properly
- WAL mode should handle most concurrent access scenarios

### Missing Data

If recent statistics aren't appearing:
- Data may be in the write queue; call `LoadRecentAsync()` which flushes first
- Check retention policy isn't too aggressive
- Verify the database path is correct

### Large Database Size

If the database grows too large:
- Reduce `retentionDays`
- Run `VACUUM` command manually: `sqlite3 universe-stats.db "VACUUM;"`
- Consider archiving old data before deletion

## API Reference

### SqliteStatisticsStorage

```csharp
public sealed class SqliteStatisticsStorage : IQueryStatisticsStorage, IDisposable
{
    // Constructor
    public SqliteStatisticsStorage(
        string dbPath = null,
        int retentionDays = 7,
        int batchSize = 10,
        int flushIntervalSeconds = 5);

    // Save (non-blocking, queues for batch write)
    public Task SaveAsync(QueryExecutionStatistics stats);

    // Load recent statistics (flushes queue first)
    public Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count);

    // Load by query hash within time window (flushes queue first)
    public Task<IList<QueryExecutionStatistics>> GetByQueryHashAsync(
        string queryHash,
        TimeSpan window);

    // Clear old records
    public Task ClearOldAsync(TimeSpan olderThan);

    // Flush pending writes and close connection
    public void Dispose();
}
```

### UniverseOptions Factory

```csharp
public static UniverseOptions WithSqlitePersistence(
    string path = null,
    int retentionDays = 7,
    int batchSize = 10,
    int flushIntervalSeconds = 5);
```

## Migration from File Storage

To migrate from file-based to SQLite storage:

```csharp
// Before
UniverseOptions options = UniverseOptions.WithFilePersistence();

// After
UniverseOptions options = UniverseOptions.WithSqlitePersistence();
```

Note: Historical data from JSON files is not automatically migrated. The SQLite storage starts fresh. If you need to preserve history, consider:

1. Running both storage backends temporarily
2. Writing a migration script to import JSON data
3. Accepting that new learning will begin with SQLite

## Example: Complete Repository Setup

```csharp
using Microsoft.Azure.Cosmos;
using Universe;
using Universe.Interfaces;

public class ProductRepository : Galaxy<Product>
{
    private static readonly UniverseOptions Options = UniverseOptions.WithSqlitePersistence(
        path: Path.Combine(AppContext.BaseDirectory, "data", "query-stats.db"),
        retentionDays: 14,
        batchSize: 25,
        flushIntervalSeconds: 10
    );

    public ProductRepository(CosmosClient client)
        : base(client, "ecommerce", "products", ["categoryId"], Options)
    {
    }
}
```
