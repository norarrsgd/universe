# Full-Text Search Query Examples for Universe Library

This document demonstrates how to use **Full-Text Search** functionality with the Universe library for Azure Cosmos DB full-text search operations.

## Overview

The Universe library supports comprehensive full-text search through various operators that leverage Azure Cosmos DB's built-in full-text search capabilities:

- `Q.Operator.FTContains` - Search for text within a field
- `Q.Operator.FTContainsAll` - All terms must be present
- `Q.Operator.FTContainsAny` - Any of the terms must be present
- `Q.Operator.FTScore` - Relevance scoring for ranking results

## Key Features

1. **Text matching operators** - Search for specific terms or phrases
2. **Relevance scoring** - Rank results by text relevance using `FTScore`
3. **RRF support** - Combine multiple full-text searches with Reciprocal Rank Fusion
4. **Hybrid search** - Combine full-text with traditional filters
5. **Vector + Text hybrid** - Combine `FTScore` with `VectorDistance` for semantic + lexical search
6. **Weighted ranking** - Apply custom weights to multiple ranking scores

## Generated Query Examples

### Setup

Initialize your Cosmos DB client and repository:

```csharp
MyRepo galaxy = new(
    client: cosmosClient,
    database: "test-database",
    container: "documents-container",
    partitionKey: typeof(MyObject).BuildPartitionKey()
);
```

### Basic Full-Text Contains Search
```csharp
// Input
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("title", "machine learning", Operator: Q.Operator.FTContains)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "description"], Top: 10)
);
```

**Generated SQL:**
```sql
SELECT TOP 10 c.id, c.title, c.description 
FROM c 
WHERE (FullTextContains(c.title, @title))
```

### Full-Text Search with Relevance Scoring
```csharp
// Input
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("content", new[] { "artificial", "intelligence" }, Operator: Q.Operator.FTScore)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "content"], Top: 5)
);
```

**Generated SQL:**
```sql
SELECT TOP 5 c.id, c.title, c.content 
FROM c 
ORDER BY RANK FullTextScore(c.content, @content)
```

### Multi-Field Full-Text Search with RRF
```csharp
// Input
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("title", new[] { "machine", "learning" }, Operator: Q.Operator.FTScore),
            new("description", new[] { "neural", "networks" }, Operator: Q.Operator.FTScore)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "description"], Top: 10)
);
```

**Generated SQL:**
```sql
SELECT TOP 10 c.id, c.title, c.description 
FROM c 
ORDER BY RANK RRF(FullTextScore(c.title, @title), FullTextScore(c.description, @description))
```

### Weighted Multi-Field Full-Text Search
```csharp
// Input
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("title", new[] { "machine", "learning" }, Operator: Q.Operator.FTScore),
            new("description", new[] { "deep", "learning" }, Operator: Q.Operator.FTScore)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "description"], Top: 10),
    sorting: [
        new(Column: "[0.8, 0.2]", Direction: Sorting.Direction.WEIGHTED)
    ]
);
```

**Generated SQL:**
```sql
SELECT TOP 10 c.id, c.title, c.description 
FROM c 
ORDER BY RANK RRF(FullTextScore(c.title, @title), FullTextScore(c.description, @description), [0.8, 0.2])
```

### Full-Text Contains All Terms
```csharp
// Input - All specified terms must be present
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("content", new[] { "machine", "learning", "algorithms" }, Operator: Q.Operator.FTContainsAll)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "content"], Top: 10)
);
```

**Generated SQL:**
```sql
SELECT TOP 10 c.id, c.title, c.content 
FROM c 
WHERE (FullTextContainsAll(c.content, @content))
```

### Full-Text Contains Any Terms
```csharp
// Input - Any of the specified terms can be present
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("tags", new[] { "AI", "ML", "DL", "NLP" }, Operator: Q.Operator.FTContainsAny)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "tags"], Top: 15)
);
```

**Generated SQL:**
```sql
SELECT TOP 15 c.id, c.title, c.tags 
FROM c 
WHERE (FullTextContainsAny(c.tags, @tags))
```

### Negated Full-Text Search
```csharp
// Input - Exclude documents containing specific terms
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("content", "deprecated", Operator: Q.Operator.NotFTContains),
            new("title", new[] { "obsolete", "legacy" }, Operator: Q.Operator.NotFTContainsAny)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "content"], Top: 10)
);
```

**Generated SQL:**
```sql
SELECT TOP 10 c.id, c.title, c.content 
FROM c 
WHERE (NOT FullTextContains(c.content, @content) AND NOT FullTextContainsAny(c.title, @title))
```

### Hybrid Search: Full-Text + Traditional Filters
```csharp
// Input - Combine full-text search with regular filters
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        // Full-text search cluster
        new(Catalysts: [
            new("content", new[] { "machine", "learning" }, Operator: Q.Operator.FTScore)
        ]),
        // Traditional filter cluster
        new(Where: Q.Where.And, Catalysts: [
            new("category", "Technology", Operator: Q.Operator.Eq),
            new("publishDate", DateTime.Now.AddDays(-30), Operator: Q.Operator.Gte),
            new("status", "Published", Operator: Q.Operator.Eq)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "category", "publishDate"], Top: 10)
);
```

**Generated SQL:**
```sql
SELECT TOP 10 c.id, c.title, c.category, c.publishDate 
FROM c 
WHERE (c.category = @category AND c.publishDate >= @publishDate AND c.status = @status)
ORDER BY RANK FullTextScore(c.content, @content)
```

### Complex Multi-Cluster Full-Text Search
```csharp
// Input - Multiple search clusters with different criteria
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        // Primary content search
        new(Catalysts: [
            new("title", new[] { "artificial", "intelligence" }, Operator: Q.Operator.FTScore)
        ]),
        // Secondary content search
        new(Where: Q.Where.OR, Catalysts: [
            new("description", new[] { "machine", "learning" }, Operator: Q.Operator.FTContainsAny),
            new("tags", new[] { "AI", "ML" }, Operator: Q.Operator.FTContainsAny)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "description", "tags"], Top: 20)
);
```

**Generated SQL:**
```sql
SELECT TOP 20 c.id, c.title, c.description, c.tags 
FROM c 
WHERE (FullTextContainsAny(c.description, @description) AND FullTextContainsAny(c.tags, @tags))
ORDER BY RANK FullTextScore(c.title, @title)
```

### Full-Text Search with Aggregation
```csharp
// Input - Group results by category and count
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("content", new[] { "technology", "innovation" }, Operator: Q.Operator.FTContainsAny)
        ])
    ],
    columnOptions: new(
        Names: ["category"],
        Aggregates: [
            new(Aggregate: Q.Aggregate.Count)
        ]
    )
);
```

**Generated SQL:**
```sql
SELECT c.category, COUNT(1) AS CountAggregate 
FROM c 
WHERE (FullTextContainsAny(c.content, @content))
GROUP BY c.category
```

### Hybrid Search: Full-Text + Vector Search
```csharp
// Input - Combine full-text scoring with vector similarity
float[] queryVector = [0.1f, 0.8f, 0.3f, 0.9f, 0.2f];

(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("contentEmbedding", queryVector, Operator: Q.Operator.VectorDistance),
            new("title", new[] { "machine", "learning" }, Operator: Q.Operator.FTScore),
            new("description", new[] { "artificial", "intelligence" }, Operator: Q.Operator.FTScore)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "description"], Top: 10)
);
```

**Generated SQL:**
```sql
SELECT TOP 10 c.id, c.title, c.description, VectorDistance(c.contentEmbedding, @contentEmbedding) AS contentEmbeddingScore
FROM c 
ORDER BY RANK RRF(VectorDistance(c.contentEmbedding, @contentEmbedding), FullTextScore(c.title, @title), FullTextScore(c.description, @description))
```

### Weighted Hybrid Search: Full-Text + Vector Search
```csharp
// Input - Combine full-text and vector search with custom weights
float[] queryVector = [0.1f, 0.8f, 0.3f, 0.9f, 0.2f];

(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("contentEmbedding", queryVector, Operator: Q.Operator.VectorDistance),
            new("title", new[] { "machine", "learning" }, Operator: Q.Operator.FTScore)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "content"], Top: 10),
    sorting: [
        new(Column: "[0.6, 0.4]", Direction: Sorting.Direction.WEIGHTED) // 60% vector, 40% text
    ]
);
```

**Generated SQL:**
```sql
SELECT TOP 10 c.id, c.title, c.content, VectorDistance(c.contentEmbedding, @contentEmbedding) AS contentEmbeddingScore
FROM c 
ORDER BY RANK RRF(VectorDistance(c.contentEmbedding, @contentEmbedding), FullTextScore(c.title, @title), [0.6, 0.4])
```

## Validation Rules

### Required Parameters
- **String values**: `FTContains` and `NotFTContains` require a string value
- **String arrays**: `FTContainsAll`, `FTContainsAny`, `FTScore` and their negations require non-empty string arrays
- **Non-empty strings**: All elements in string arrays must be non-empty

### Operator Combinations
- **Hybrid ranking**: `FTScore` can be combined with `VectorDistance` using RRF (Reciprocal Rank Fusion)
- **RRF support**: Multiple `FTScore` operators automatically use Reciprocal Rank Fusion
- **No scalar sorting with vectors**: Cannot use traditional `ORDER BY` with scalar fields when `VectorDistance` is present
- **Weight limitations**: Only one `WEIGHTED` sorting option allowed per query
- **Unique catalysts**: Each catalyst must have unique Column+Operator combination per cluster

### Performance Considerations
- **Indexing**: Ensure your container has full-text indexing enabled
- **Query complexity**: Complex multi-field searches may consume more RUs
- **Result limits**: Use appropriate `Top` values to control result set size

## Sample Data Model

```csharp
public record ArticleDocument : MyObject
{
    /// <summary>Article title for full-text search</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Article content for full-text search</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Article description/summary</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Tags array for categorical search</summary>
    public string[] Tags { get; set; } = [];

    /// <summary>Category for grouping and filtering</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Publication date for temporal filtering</summary>
    public DateTime PublishDate { get; set; }

    /// <summary>Article status</summary>
    public string Status { get; set; } = "Draft";
}
```

## Usage Examples

For complete working examples, refer to:
- `Examples/Example10_FullTextSearch.cs` - Comprehensive full-text search examples
- `Helpers/TestDataGenerator.cs` - Sample data generation with text content

## Cosmos DB Setup Requirements

Your Azure Cosmos DB container should have full-text indexing configured:

## Production Considerations

1. **Text Preprocessing**: Clean and normalize text data before indexing
2. **Language Support**: Consider language-specific text processing requirements
3. **Index Strategy**: Configure appropriate full-text indexes for searchable fields
4. **Performance**: Monitor RU consumption for complex full-text queries
5. **Relevance Tuning**: Use weighted scoring to prioritize important fields
6. **Case Sensitivity**: Full-text search is typically case-insensitive
7. **Stop Words**: Be aware of language-specific stop word filtering

## Error Handling

Common errors and solutions:
- **"Value required for Full-text search operators"**: Ensure non-null values for all FT operators
- **"Value must be a string"**: Use string values for `FTContains` and `NotFTContains`
- **"Value must be a non-empty array of strings"**: Provide valid string arrays for array-based operators
- **"All elements must be non-empty strings"**: Avoid null or empty strings in arrays
- **"Only one WEIGHT option allowed"**: Use single weighted sorting configuration
- **"Duplicate catalysts"**: Ensure unique Column+Operator combinations per cluster

## Advanced Features

### Custom Relevance Scoring
```csharp
// Prioritize title matches over content matches
double titleWeight = 0.7;
double contentWeight = 0.3;

(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("title", searchTerms, Operator: Q.Operator.FTScore),
            new("content", searchTerms, Operator: Q.Operator.FTScore)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "content"], Top: 10),
    sorting: [
        new(Column: $"[{titleWeight}, {contentWeight}]", Direction: Sorting.Direction.WEIGHTED)
    ]
);
```

### Boolean Full-Text Logic
```csharp
// Must contain "machine learning" AND any of the AI-related terms
(Gravity, IList<T>) results = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new("content", "machine learning", Operator: Q.Operator.FTContains),
            new("content", new[] { "AI", "artificial intelligence", "neural" }, Operator: Q.Operator.FTContainsAny, Where: Q.Where.And)
        ])
    ],
    columnOptions: new(Names: ["id", "title", "content"], Top: 10)
);
```

## Limitations

- **Scalar sorting limitations**: Cannot use traditional `ORDER BY` clauses with scalar fields when `VectorDistance` is present
- **Weighted sorting**: Only supported with multiple ranking operators (`FTScore`, `VectorDistance`) in RRF scenarios
- **Query complexity**: Highly complex full-text queries may impact performance
- **Real-time indexing**: Text changes may have slight indexing delays
- **Vector requirements**: When combining with `VectorDistance`, `ColumnOptions.Top` must be specified
