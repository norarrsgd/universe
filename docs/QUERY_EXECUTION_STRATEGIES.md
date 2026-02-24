# Query Execution Strategies

This document explains the query execution strategies feature in Universe. This feature provides intelligent query optimization and automatic performance tuning.

## Version History

- **v3.1.x**: Rule-based optimization with automatic strategy selection
- **v3.2.0+**: Adaptive learning with historical statistics tracking (current)

## Overview

The query execution strategies system automatically selects the optimal execution approach for your queries based on their type and complexity. Starting with v3.2.0, the system learns from execution history to provide data-driven recommendations.

## Key Features

### 1. Automatic Strategy Selection
- **Direct Strategy**: Optimized for simple queries with unlimited parallelism
- **Gateway Strategy**: Conservative approach for complex queries and fallback scenarios  
- **Vector Search Strategy**: Specialized optimization for vector similarity searches

### 2. Query Type Detection
The system automatically detects query types:
- `Simple`: Basic queries without complex operations
- `Aggregation`: Queries with GROUP BY, COUNT, SUM operations
- `VectorSearch`: Vector similarity search queries
- `FullTextSearch`: Full-text search queries
- `HybridSearch`: Combined vector and full-text search
- `Join`: Queries with JOIN operations
- `Complex`: Queries with multiple operations or RRF

### 3. Performance Tuning (v3.2.0+)
- **Adaptive Learning**: Tracks execution statistics and learns from query patterns
- **Historical Analysis**: Maintains last 1000 executions and analyzes queries from last 24 hours
- **Data-Driven Recommendations**: Provides hints based on actual performance when sufficient data is available
- **Automatic Fallback**: Uses rule-based recommendations when insufficient historical data
- **Persistent Storage**: Optional file-based persistence across application restarts
- Manual query hints for fine-tuning

## Usage Examples

### Basic Query with Hints

```csharp
QueryHints hints = new(
    MaxItemCount: 100,
    EnableOptimisticDirectExecution: true,
    MaxConcurrency: Environment.ProcessorCount
);

(Gravity gravity, IList<MyObject> results) = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new(nameof(MyObject.Category), "Electronics", Operator: Q.Operator.Eq)
        ])
    ],
    columnOptions: new(Names: [nameof(MyObject.Name), nameof(MyObject.Price)], Top: 10),
    hints: hints
);
```

### Force Specific Strategy

```csharp
QueryHints gatewayHints = new(
    ForceStrategy: QueryExecutionStrategy.Gateway,
    MaxItemCount: 50,
    MaxConcurrency: 1
);

var results = await galaxy.List(clusters, columnOptions: null, hints: gatewayHints);
```

### Vector Search Optimization

```csharp
QueryHints vectorHints = new(
    MaxItemCount: 50,
    MaxBufferedItemCount: 100,
    EnableOptimisticDirectExecution: true
);

float[] searchVector = GetEmbedding("search query");

var results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new(nameof(MyObjectVector.TitleEmbedding), searchVector, Operator: Q.Operator.VectorDistance)
        ])
    ],
    columnOptions: new(Top: 5),
    hints: vectorHints
);
```

### Get Performance Recommendations (v3.2.0+)

```csharp
// Get adaptive recommendations for a specific query type
QueryTuningRecommendations recommendations = galaxy.GetQueryRecommendations("query_pattern", QueryType.VectorSearch);

// Check if recommendations are data-driven or rule-based
Console.WriteLine($"Data-Driven: {recommendations.IsDataDriven}");
Console.WriteLine($"Sample Size: {recommendations.SampleSize}");

// Display performance metrics (available when data-driven)
if (recommendations.AverageRU.HasValue)
    Console.WriteLine($"Average RU: {recommendations.AverageRU:F2}");

if (recommendations.SuccessRate.HasValue)
    Console.WriteLine($"Success Rate: {recommendations.SuccessRate:P2}");

if (recommendations.AverageExecutionTime.HasValue)
    Console.WriteLine($"Avg Execution Time: {recommendations.AverageExecutionTime.Value.TotalMilliseconds:F2}ms");

if (recommendations.RecommendedStrategy != null)
    Console.WriteLine($"Recommended Strategy: {recommendations.RecommendedStrategy}");

// Display suggested hints
if (recommendations.SuggestedHints?.Any() == true)
{
    Console.WriteLine("Suggested Hints:");
    foreach (var hint in recommendations.SuggestedHints)
    {
        Console.WriteLine($"  {hint.Key} = {hint.Value}");
    }
}
```

### Configure Statistics Storage (v3.2.0+)

```csharp
// Option 1: In-memory only (default, no persistence)
public class MyRepository : Galaxy<MyObject>
{
    public MyRepository(CosmosClient client)
        : base(client, "database", "container", ["partitionKey"])
    {
    }
}

// Option 2: File-based persistence (JSON, survives restarts)
public class MyRepository : Galaxy<MyObject>
{
    public MyRepository(CosmosClient client)
        : base(client, "database", "container", ["partitionKey"],
               UniverseOptions.WithFilePersistence())
    {
    }
}

// Option 3: SQLite persistence (recommended for production)
public class MyRepository : Galaxy<MyObject>
{
    public MyRepository(CosmosClient client)
        : base(client, "database", "container", ["partitionKey"],
               UniverseOptions.WithSqlitePersistence())
    {
    }
}

// Option 4: SQLite with custom configuration
public class MyRepository : Galaxy<MyObject>
{
    public MyRepository(CosmosClient client)
        : base(client, "database", "container", ["partitionKey"],
               UniverseOptions.WithSqlitePersistence(
                   path: "/data/stats.db",
                   retentionDays: 14,
                   batchSize: 20,
                   flushIntervalSeconds: 10))
    {
    }
}
```

## Available Query Hints

### Execution Strategy Selection
- `ForceStrategy`: Force a specific execution strategy using the `QueryExecutionStrategy` enum
  - `QueryExecutionStrategy.Direct`: High-performance strategy for simple queries
  - `QueryExecutionStrategy.Gateway`: Conservative strategy for complex queries
  - `QueryExecutionStrategy.VectorSearch`: Optimized strategy for vector similarity searches

### Performance Hints
- `MaxItemCount`: Maximum items per batch (default varies by strategy)
- `MaxBufferedItemCount`: Maximum buffered items for parallel processing
- `MaxConcurrency`: Degree of parallelism (-1 for unlimited)
- `EnableOptimisticDirectExecution`: Enable/disable direct execution optimization

### Strategy Control
- `ForceStrategy`: Force a specific strategy (`QueryExecutionStrategy.Direct`, `QueryExecutionStrategy.Gateway`, `QueryExecutionStrategy.VectorSearch`)
- `ResponseContinuationTokenLimitInKb`: Limit continuation token size

### Example Hint Configurations

#### High-Performance Simple Queries
```csharp
new QueryHints(
    MaxItemCount: 1000,
    MaxBufferedItemCount: 5000,
    MaxConcurrency: -1,
    EnableOptimisticDirectExecution: true
)
```

#### Conservative Complex Queries
```csharp
new QueryHints(
    ForceStrategy: QueryExecutionStrategy.Gateway,
    MaxItemCount: 100,
    MaxConcurrency: 1,
    ResponseContinuationTokenLimitInKb: 1
)
```

#### Vector Search Optimization
```csharp
new QueryHints(
    MaxItemCount: 50,
    MaxBufferedItemCount: 100,
    MaxConcurrency: Environment.ProcessorCount
)
```

## Strategy Selection Logic

The system follows this priority order:

1. **Forced Strategy**: Respects `ForceStrategy` hint when provided
2. **Automatic Selection**: Chooses based on query type and strategy priority
   - VectorSearchStrategy (Priority: 200) - For vector/hybrid/full-text queries
   - DirectQueryStrategy (Priority: 100) - For simple queries with high performance
   - GatewayQueryStrategy (Priority: 50, fallback) - Conservative approach for complex queries

## Performance Monitoring

### Basic RU Tracking

The system tracks Request Unit (RU) consumption for each query execution, returned in the `Gravity` response object:

```csharp
(Gravity gravity, IList<MyObject> results) = await galaxy.List(clusters, columnOptions, hints);
Console.WriteLine($"RU consumed: {gravity.RU}");
```

### Adaptive Learning (v3.2.0+)

The system automatically tracks detailed execution statistics for every query:

- **Query Hash**: Identifies query patterns without storing sensitive data
- **Query Type**: Detected query classification
- **RU Consumption**: Request Units consumed
- **Execution Time**: Total query execution duration
- **Result Count**: Number of results returned
- **Success/Failure**: Whether the query succeeded
- **Strategy Used**: Which execution strategy was selected
- **Hints Applied**: Query hints that were used

**Statistics Retention**:
- In-memory: Last 1000 executions
- Time window: Last 24 hours for recommendations
- File storage: Persisted across restarts (optional)

**Privacy**: Only query structure is hashed, never raw query text or parameter values.

## Best Practices

### For Simple Queries
- Use Direct strategy with high concurrency
- Enable optimistic direct execution
- Use larger batch sizes for better throughput

### For Vector Searches
- Use smaller batch sizes (50-100 items)
- Enable optimistic direct execution
- Match concurrency to CPU cores

### For Complex Aggregations
- Use Gateway strategy for stability
- Use larger batch sizes (500-1000 items)
- Moderate concurrency (1-4 threads)

### For Production Workloads (v3.2.0+)
- Enable file-based persistence to maintain learning across restarts
- Monitor RU consumption using the `Gravity` response
- Check `IsDataDriven` property to know when you have sufficient historical data
- Use data-driven recommendations once sample size reaches 10+
- Test different hint combinations - the system will learn which performs best
- Allow the system to accumulate data before making optimization decisions

## How Adaptive Learning Works (v3.2.0+)

### Data Collection

Every query execution automatically records:
1. Performance metrics (RU, execution time, result count)
2. Query characteristics (type, strategy used, hints applied)
3. Success/failure status

### Recommendation Engine

When you request recommendations:

1. **Filters** queries from the last 24 hours matching the query type
2. **Analyzes** performance across different hint configurations
3. **Recommends**:
   - Best-performing hints (lowest RU + highest success rate)
   - Optimal strategy (if multiple strategies were tested)
   - Performance expectations (average RU, success rate, execution time)

### Fallback Behavior

- **< 10 samples**: Uses rule-based recommendations (`IsDataDriven: false`)
- **≥ 10 samples**: Switches to data-driven recommendations (`IsDataDriven: true`)
- Requires minimum 3 samples per hint configuration for reliability
- Requires minimum 5 samples per strategy for comparison

### Storage Options

| Storage Type | Persistence | Use Case |
|-------------|-------------|----------|
| **In-memory** (default) | Until restart | Development, testing |
| **File-based** | Across restarts | Simple apps, debugging |
| **SQLite** | Across restarts | Production, high volume |
| **Custom** | User-defined | Advanced scenarios |

Default file location: `{CurrentWorkingDirectory}/query-statistics.json`
Default SQLite location: `{AppDirectory}/universe-stats.db`

See [SQLITE_STATISTICS_STORAGE.md](./SQLITE_STATISTICS_STORAGE.md) for detailed SQLite configuration.

## Integration with Existing Code

The query execution strategies are backward compatible. Existing code will automatically benefit from the new optimization system without any changes required.

```csharp
// v3.1.x code (still works, uses rule-based optimization)
var results = await galaxy.List(clusters, columnOptions);

// v3.2.0+ code (with adaptive learning)
var results = await galaxy.List(clusters, columnOptions, hints: new QueryHints(MaxItemCount: 100));

// v3.2.0+ with file persistence
public class MyRepository : Galaxy<MyObject>
{
    public MyRepository(CosmosClient client)
        : base(client, "database", "container", ["partitionKey"],
               UniverseOptions.WithFilePersistence())
    {
    }
}

// v3.2.0+ with SQLite persistence (recommended for production)
public class MyRepository : Galaxy<MyObject>
{
    public MyRepository(CosmosClient client)
        : base(client, "database", "container", ["partitionKey"],
               UniverseOptions.WithSqlitePersistence())
    {
    }
}
```
