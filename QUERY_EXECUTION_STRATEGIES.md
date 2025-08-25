# Query Execution Strategies

This document explains the new query execution strategies feature in Universe version 3.1.0. This feature provides intelligent query optimization and automatic performance tuning based on query patterns and execution history.

## Overview

The query execution strategies system automatically selects the optimal execution approach for your queries based on their type and complexity, while also learning from execution patterns to improve future performance.

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

### 3. Performance Tuning
- Tracks execution statistics (RU consumption, execution time, success rate)
- Provides automatic recommendations based on historical performance
- Supports manual query hints for fine-tuning

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

### Get Performance Recommendations

```csharp
QueryTuningRecommendations recommendations = galaxy.GetQueryRecommendations("query_pattern", QueryType.VectorSearch);

Console.WriteLine($"Recommended Strategy: {recommendations.RecommendedStrategy}");
Console.WriteLine($"Average RU: {recommendations.AverageRU:F2}");
Console.WriteLine($"Success Rate: {recommendations.SuccessRate:P2}");

if (recommendations.SuggestedHints?.Any() == true)
{
    foreach (var hint in recommendations.SuggestedHints)
    {
        Console.WriteLine($"Suggested Hint: {hint.Key} = {hint.Value}");
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

1. **Tuning Recommendations**: Uses historical performance data if available
2. **Forced Strategy**: Respects `ForceStrategy` hint when provided
3. **Automatic Selection**: Chooses based on query type and strategy priority
   - VectorSearchStrategy (Priority: 200)
   - DirectQueryStrategy (Priority: 100)  
   - GatewayQueryStrategy (Priority: 50, fallback)

## Performance Monitoring

The system automatically tracks:
- Request Unit (RU) consumption
- Execution time
- Result count
- Success/failure rate
- Query hash for pattern recognition

Statistics are maintained for the last 1000 query executions and queries from the last 24 hours are used for recommendations.

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

### For Production Workloads
- Monitor recommendations regularly
- Start with conservative hints and optimize based on recommendations
- Use query pattern-based tuning for recurring queries

## Integration with Existing Code

The query execution strategies are backward compatible. Existing code will automatically benefit from the new optimization system without any changes required. To take advantage of advanced features, simply add QueryHints to your List() method calls.

```csharp
// Before (still works)
var results = await galaxy.List(clusters, columnOptions);

// After (with optimization)
var results = await galaxy.List(clusters, columnOptions, hints: new QueryHints(MaxItemCount: 100));
```
