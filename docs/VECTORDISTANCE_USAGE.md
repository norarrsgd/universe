# VectorDistance Query Examples for Universe Library

This document demonstrates how to use **VectorDistance** functionality with the Universe library for Azure Cosmos DB vector search operations.

## Overview

The Universe library supports vector similarity search through the fluent `.VectorDistance(...)` query operator, which leverages Azure Cosmos DB's built-in vector search capabilities.

## Key Requirements

1. **TOP clause is mandatory** - Vector searches require `.Top(...)` in the fluent query chain
2. **Vector format** - Values must be `float[]` arrays with finite numbers
3. **Container setup** - Your Cosmos DB container must have vector indexing configured
4. **No traditional sorting** - Cannot combine `VectorDistance` with regular `ORDER BY` clauses
5. Ensure your container has a proper **Vector Index Policy** setup

## Generated Query Examples

### Setup

Make sure to initialize your Cosmos DB client and create a vector repository:

```csharp
using Universe.Extensions;
using Universe.Interfaces;

IGalaxy<MyObjectWithVector> galaxy = ...;
```

### Single Vector Search
```csharp
// Input
float[] queryVector = [0.1f, 0.8f, 0.3f, 0.9f, 0.2f];

(Gravity gravity, IList<MyObjectWithVector> results) = await galaxy.Query()
    .Select("id", "name")
    .Top(5)
    .Cluster(c => c.VectorDistance(nameof(MyObjectWithVector.DescriptionEmbedding), queryVector))
    .ToListAsync();
```

**Generated SQL:**
```sql
SELECT TOP 5 c.id, c.name, VectorDistance(c.DescriptionEmbedding, @DescriptionEmbedding) AS DescriptionEmbeddingScore 
FROM c 
ORDER BY VectorDistance(c.DescriptionEmbedding, @DescriptionEmbedding)
```

### Multi-Vector Search with RRF (Reciprocal Rank Fusion)
```csharp
// Input
float[] titleVector = [0.2f, 0.9f, 0.1f, 0.8f];
float[] descVector = [0.3f, 0.7f, 0.2f, 0.9f];

(Gravity gravity, IList<MyObjectWithVector> results) = await galaxy.Query()
    .Select("id", "name")
    .Top(3)
    .Cluster(c => c
        .VectorDistance(nameof(MyObjectWithVector.TitleEmbedding), titleVector)
        .VectorDistance(nameof(MyObjectWithVector.DescriptionEmbedding), descVector))
    .ToListAsync();
```

**Generated SQL:**
```sql
SELECT TOP 3 c.id, c.name, 
       VectorDistance(c.TitleEmbedding, @TitleEmbedding) AS TitleEmbeddingScore,
       VectorDistance(c.DescriptionEmbedding, @DescriptionEmbedding) AS DescriptionEmbeddingScore
FROM c 
ORDER BY RANK RRF(VectorDistance(c.TitleEmbedding, @TitleEmbedding), VectorDistance(c.DescriptionEmbedding, @DescriptionEmbedding))
```

### Multi-Vector Search with Weighted RRF
```csharp
// Input
float[] titleVector = [0.2f, 0.9f, 0.1f, 0.8f];
float[] descVector = [0.3f, 0.7f, 0.2f, 0.9f];

(Gravity gravity, IList<MyObjectWithVector> results) = await galaxy.Query()
    .Select("id", "name")
    .Top(3)
    .Cluster(c => c
        .VectorDistance(nameof(MyObjectWithVector.TitleEmbedding), titleVector)
        .VectorDistance(nameof(MyObjectWithVector.DescriptionEmbedding), descVector))
    .WithWeights("[1, 2]")
    .ToListAsync();
```

**Generated SQL:**
```sql
SELECT TOP 3 c.id, c.name, 
       VectorDistance(c.TitleEmbedding, @TitleEmbedding) AS TitleEmbeddingScore,
       VectorDistance(c.DescriptionEmbedding, @DescriptionEmbedding) AS DescriptionEmbeddingScore
FROM c 
ORDER BY RANK RRF(VectorDistance(c.TitleEmbedding, @TitleEmbedding), VectorDistance(c.DescriptionEmbedding, @DescriptionEmbedding), [1, 2])
```

### Hybrid Search (Vector + Filters)
```csharp
// Input  
float[] queryVector = [0.4f, 0.6f, 0.8f, 0.2f];

(Gravity gravity, IList<MyObjectWithVector> results) = await galaxy.Query()
    .Select("id", "name", "price")
    .Top(5)
    .Cluster(c => c.VectorDistance(nameof(MyObjectWithVector.DescriptionEmbedding), queryVector))
    .Cluster(c => c
        .Eq(nameof(MyObjectWithVector.Category), "Electronics")
        .And().Lt(nameof(MyObjectWithVector.Price), 1000.0))
    .ToListAsync();
```

**Generated SQL:**
```sql
SELECT TOP 5 c.id, c.name, c.price, VectorDistance(c.DescriptionEmbedding, @DescriptionEmbedding) AS DescriptionEmbeddingScore
FROM c 
WHERE (c.Category = @Category AND c.Price < @Price)
ORDER BY VectorDistance(c.DescriptionEmbedding, @DescriptionEmbedding)
```

### Validation Rules
- VectorDistance requires `.Top(...)`
- Cannot combine VectorDistance with regular sorting (scalar fields)
- Vector values must be non-empty `float[]` with finite numbers
- Each rank condition must use a unique column/operator combination
- Only one `.WithWeights(...)` option is allowed per query
- Weight values passed to `.WithWeights(...)` must be formatted as bracketed arrays (e.g., `"[0.8, 1.2]"`)
- Weighted sorting only works with multiple VectorDistance operators (RRF scenarios)

## Sample Data Model

```csharp
public record MyObjectWithVector : MyObject
{
    /// <summary>Vector embedding of the product title</summary>
    public float[] TitleEmbedding { get; set; } = [];

    /// <summary>Vector embedding of the product description</summary>
    public float[] DescriptionEmbedding { get; set; } = [];

    /// <summary>Combined embedding of multiple text fields</summary>
    public float[] CombinedEmbedding { get; set; } = [];
}
```

## Usage Examples

See the following files for complete working examples:
- `Examples/Example9_VectorSearch.cs` - Comprehensive vector search examples
- `Helpers/VectorDataGenerator.cs` - Sample data generation with embeddings

## Cosmos DB Setup Requirements

Your Azure Cosmos DB container needs vector indexing configured:

```json
{
  "indexingPolicy": {
    "vectorIndexes": [
      {
        "path": "/TitleEmbedding",
        "type": "quantizedFlat"
      },
      {
        "path": "/DescriptionEmbedding", 
        "type": "quantizedFlat"
      }
    ]
  }
}
```

## Production Considerations

1. **Embedding Generation**: Use proper embedding models like OpenAI's `text-embedding-ada-002`
2. **Vector Dimensions**: Match your embedding model's output dimensions
3. **Indexing Strategy**: Choose appropriate vector index types (`quantizedFlat`, `diskANN`, etc.)
4. **Performance**: Monitor RU consumption as vector searches can be expensive
5. **Batch Operations**: Consider batch embedding generation for efficiency

## Error Handling

Common errors and solutions:
- **"Top value required"**: Always specify `.Top(...)` for VectorDistance
- **"Invalid vector"**: Ensure vectors are non-empty `float[]` with finite values
- **"Sorting not supported"**: Don't mix VectorDistance with manual sorting
- **"Duplicate catalysts"**: Ensure unique column/operator combinations inside each fluent `.Cluster(...)`

## Limitations

- Weight vectors are supported only for RRF scenarios with multiple VectorDistance operators
- VectorDistance does not support manual sorting or complex ordering with scalar fields
- Weighted sorting requires bracketed array format in the Column property
