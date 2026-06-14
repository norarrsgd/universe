# Fluent Query Builder (Orbit)

The `Orbit<T>` fluent query builder provides the recommended, chainable API for building Cosmos DB queries. It produces the same SQL as the lower-level declarative `Cluster`/`Catalyst` API while keeping query intent easier to read in application code.

## Quick Start

```csharp
using Universe.Extensions; // Provides the .Query() extension method

// Fluent
(Gravity g, IList<MyModel> results) = await galaxy.Query()
    .Select("id", "name", "price")
    .Top(20)
    .Cluster(c => c.Like("name", "%Test%").And().Lte("price", 50.0))
    .Or()
    .Cluster(c => c.Eq("code", "SPECIAL"))
    .OrderByDescending("price")
    .ToListAsync();
```

Internally, the fluent chain is materialized into the same query structures used by the lower-level declarative API.

## Entry Point

Call `.Query()` on any `IGalaxy<T>` instance to start building a fluent query:

```csharp
using Universe.Builder;
using Universe.Extensions;

IGalaxy<MyModel> galaxy = ...; // injected via DI

Orbit<MyModel> orbit = galaxy.Query();
```

The `Query()` extension method is defined in the `Universe.Extensions` namespace, which is automatically available within the Universe library via global usings.

## Building Queries

### Clusters and Catalysts

Clusters group filter conditions. Use the `.Cluster()` method with a lambda that receives a `ClusterBuilder`:

```csharp
// Single cluster with one condition
galaxy.Query()
    .Cluster(c => c.Eq("status", "active"))
    .ToListAsync();

// Single cluster with multiple AND conditions
galaxy.Query()
    .Cluster(c => c
        .Eq("status", "active")
        .And().Gte("price", 10.0)
        .And().Lte("price", 100.0))
    .ToListAsync();

// Single cluster with OR conditions
galaxy.Query()
    .Cluster(c => c
        .Eq("category", "Electronics")
        .Or().Eq("category", "Books"))
    .ToListAsync();
```

### Combining Multiple Clusters

Use `.And()` or `.Or()` between `.Cluster()` calls to control how clusters are combined:

```csharp
// (status = "active" AND price >= 10) AND (category = "Electronics")
galaxy.Query()
    .Cluster(c => c.Eq("status", "active").And().Gte("price", 10.0))
    .And()
    .Cluster(c => c.Eq("category", "Electronics"))
    .ToListAsync();

// (status = "active") OR (featured IS_DEFINED)
galaxy.Query()
    .Cluster(c => c.Eq("status", "active"))
    .Or()
    .Cluster(c => c.Defined("featured"))
    .ToListAsync();
```

If no `.And()` or `.Or()` is called between clusters, `.And()` is used by default:

```csharp
// These are equivalent:
galaxy.Query()
    .Cluster(c => c.Eq("a", 1))
    .Cluster(c => c.Eq("b", 2))  // implicitly AND
    .ToListAsync();

galaxy.Query()
    .Cluster(c => c.Eq("a", 1))
    .And()
    .Cluster(c => c.Eq("b", 2))
    .ToListAsync();
```

## Operator Methods

The `ClusterBuilder` provides convenience methods for every supported operator. You can also use the generic `.Catalyst()` method with any `Q.Operator`.

### Comparison Operators

| Method | Operator | SQL Generated |
|--------|----------|---------------|
| `.Eq(column, value)` | `Q.Operator.Eq` | `c["column"] = @param` |
| `.NotEq(column, value)` | `Q.Operator.NotEq` | `c["column"] != @param` |
| `.Gt(column, value)` | `Q.Operator.Gt` | `c["column"] > @param` |
| `.Gte(column, value)` | `Q.Operator.Gte` | `c["column"] >= @param` |
| `.Lt(column, value)` | `Q.Operator.Lt` | `c["column"] < @param` |
| `.Lte(column, value)` | `Q.Operator.Lte` | `c["column"] <= @param` |

```csharp
galaxy.Query()
    .Cluster(c => c
        .Gte("price", 10.0)
        .And().Lt("price", 100.0)
        .And().NotEq("status", "archived"))
    .ToListAsync();
```

### Pattern Matching

| Method | Operator | SQL Generated |
|--------|----------|---------------|
| `.Like(column, pattern)` | `Q.Operator.Like` | `c["column"] LIKE @param` |
| `.NotLike(column, pattern)` | `Q.Operator.NotLike` | `c["column"] NOT LIKE @param` |

Patterns must include `%` wildcards:

```csharp
galaxy.Query()
    .Cluster(c => c.Like("name", "%Test%").And().NotLike("code", "%DRAFT%"))
    .ToListAsync();
```

### Existence Checks

| Method | Operator | SQL Generated |
|--------|----------|---------------|
| `.Defined(column)` | `Q.Operator.Defined` | `IS_DEFINED(c["column"])` |
| `.NotDefined(column)` | `Q.Operator.NotDefined` | `NOT IS_DEFINED(c["column"])` |

```csharp
galaxy.Query()
    .Cluster(c => c.Defined("email").And().NotDefined("deletedAt"))
    .ToListAsync();
```

### Array Operators

| Method | Operator | SQL Generated | Use Case |
|--------|----------|---------------|----------|
| `.In(column, value)` | `Q.Operator.In` | `ARRAY_CONTAINS(c["column"], @param)` | Document's array field contains a scalar |
| `.NotIn(column, value)` | `Q.Operator.NotIn` | `NOT ARRAY_CONTAINS(c["column"], @param)` | Document's array field does NOT contain a scalar |
| `.Contains(column, values)` | `Q.Operator.Contains` | `ARRAY_CONTAINS(@param, c["column"])` | Scalar field value is in a provided collection |
| `.NotContains(column, values)` | `Q.Operator.NotContains` | `NOT ARRAY_CONTAINS(@param, c["column"])` | Scalar field value is NOT in a provided collection |
| `.Len(column, length)` | `Q.Operator.Len` | `ARRAY_LENGTH(c["column"]) = @param` | Array field has a specific length |

```csharp
// Check if a document's "tags" array contains "featured"
galaxy.Query()
    .Cluster(c => c.In("tags", "featured"))
    .ToListAsync();

// Check if the document's "category" is in a list of allowed values
string[] allowed = ["Electronics", "Books", "Clothing"];
galaxy.Query()
    .Cluster(c => c.Contains("category", allowed))
    .ToListAsync();

// Find documents with exactly 3 items in their array
galaxy.Query()
    .Cluster(c => c.Len("items", 3))
    .ToListAsync();
```

### Vector Distance Search

| Method | Operator | SQL Generated |
|--------|----------|---------------|
| `.VectorDistance(column, vector)` | `Q.Operator.VectorDistance` | `VectorDistance(c["column"], @param) AS columnScore` |

Requires `.Top()` to be set. See [VECTORDISTANCE_USAGE.md](VECTORDISTANCE_USAGE.md) for details.

```csharp
float[] queryVector = /* your embedding */;
(Gravity g, IList<MyModel> results) = await galaxy.Query()
    .Select("name", "description")
    .Top(10)
    .Cluster(c => c.VectorDistance("descriptionEmbedding", queryVector))
    .ToListAsync();
```

Multi-vector search with RRF (Reciprocal Rank Fusion):

```csharp
float[] titleVector = /* ... */;
float[] descVector = /* ... */;
(Gravity g, IList<MyModel> results) = await galaxy.Query()
    .Select("name", "description")
    .Top(10)
    .Cluster(c => c
        .VectorDistance("titleEmbedding", titleVector)
        .VectorDistance("descriptionEmbedding", descVector))
    .ToListAsync();
```

### Full-Text Search

| Method | Operator | SQL Generated |
|--------|----------|---------------|
| `.FTContains(column, keyword)` | `Q.Operator.FTContains` | `FullTextContains(c["column"], @param)` |
| `.NotFTContains(column, keyword)` | `Q.Operator.NotFTContains` | `NOT FullTextContains(c["column"], @param)` |
| `.FTContainsAll(column, keywords)` | `Q.Operator.FTContainsAll` | `FullTextContainsAll(c["column"], @param)` |
| `.NotFTContainsAll(column, keywords)` | `Q.Operator.NotFTContainsAll` | `NOT FullTextContainsAll(c["column"], @param)` |
| `.FTContainsAny(column, keywords)` | `Q.Operator.FTContainsAny` | `FullTextContainsAny(c["column"], @param)` |
| `.NotFTContainsAny(column, keywords)` | `Q.Operator.NotFTContainsAny` | `NOT FullTextContainsAny(c["column"], @param)` |
| `.FTScore(column, terms)` | `Q.Operator.FTScore` | `FullTextScore(c["column"], @param)` |

See [FULLTEXT_USAGE.md](FULLTEXT_USAGE.md) for details.

```csharp
// Single keyword search
galaxy.Query()
    .Cluster(c => c.FTContains("description", "machine learning"))
    .ToListAsync();

// Must contain ALL keywords
galaxy.Query()
    .Cluster(c => c.FTContainsAll("description", ["machine", "learning", "algorithms"]))
    .ToListAsync();

// Must contain ANY keyword
galaxy.Query()
    .Cluster(c => c.FTContainsAny("description", ["AI", "ML", "deep learning"]))
    .ToListAsync();

// Relevance scoring with TOP
galaxy.Query()
    .Select("name", "description")
    .Top(10)
    .Cluster(c => c.FTScore("description", ["artificial", "intelligence"]))
    .ToListAsync();
```

### Generic Catalyst Method

For any operator, you can use the generic `.Catalyst()` method directly:

```csharp
galaxy.Query()
    .Cluster(c => c
        .Catalyst("name", "test", Q.Operator.Eq)
        .And()
        .Catalyst("price", 50.0, Q.Operator.Gt))
    .ToListAsync();
```

## Column Selection

### Select Specific Columns

```csharp
galaxy.Query()
    .Select("id", "name", "price", "category")
    .Cluster(c => c.Eq("status", "active"))
    .ToListAsync();
```

### Type-Based Projection Select

Instead of listing column names as strings, use `.Select<TProjection>()` to extract columns from a type's public properties:

```csharp
// Define a projection type — no ICosmicEntity required
record ProductSummary
{
    public string Name { get; init; }
    public double Price { get; init; }
    public string Category { get; init; }
}

// These two produce identical SQL:
galaxy.Query().Select("Name", "Price", "Category") ...
galaxy.Query().Select<ProductSummary>() ...
```

**How it works:**
- Extracts all public instance properties with a getter from the projection type
- Properties marked with `[JsonIgnore]` are excluded
- Inherited properties are included
- Works with any type — classes, records, structs, record structs
- When a naming policy is configured on `UniverseSerializer`, column names are transformed automatically (same as string `Select`)
- Results are cached per type, so reflection only runs once

**Combining with string Select:**

`.Select<TProjection>()` is additive — it can be combined with `.Select()` to include extra columns not in the projection type:

```csharp
galaxy.Query()
    .Select<ProductSummary>()   // Name, Price, Category
    .Select("Description")      // plus Description
    .Cluster(c => c.Eq("status", "active"))
    .ToListAsync();
```

**Excluding properties with `[JsonIgnore]`:**

```csharp
record InventoryView
{
    public string Code { get; init; }
    public string Name { get; init; }
    public int Quantity { get; init; }
    [JsonIgnore] public bool IsLowStock => Quantity < 10; // excluded from SELECT
}

// Generates: SELECT c["Code"], c["Name"], c["Quantity"] FROM c ...
galaxy.Query()
    .Select<InventoryView>()
    .ToListAsync();
```

> **Note:** `Select<TProjection>()` only determines which columns appear in the SELECT clause. To deserialize results into the projection type, pass the same type to the terminal method: `.ToListAsync<ProductSummary>()`. See [Type Projection](#type-projection) below.

### TOP and DISTINCT

```csharp
// Limit results
galaxy.Query()
    .Select("name", "price")
    .Top(10)
    .Cluster(c => c.Eq("category", "Electronics"))
    .ToListAsync();

// Distinct values
galaxy.Query()
    .Select("category")
    .Distinct()
    .Cluster(c => c.Defined("category"))
    .ToListAsync();

// Combined
galaxy.Query()
    .Select("category")
    .Distinct()
    .Top(5)
    .Cluster(c => c.Defined("category"))
    .ToListAsync();
```

## Sorting

```csharp
// Ascending (default)
galaxy.Query()
    .Cluster(c => c.Eq("category", "Books"))
    .OrderBy("price")
    .ToListAsync();

// Descending
galaxy.Query()
    .Cluster(c => c.Eq("category", "Books"))
    .OrderByDescending("price")
    .ToListAsync();

// Multiple sort columns
galaxy.Query()
    .Cluster(c => c.Eq("status", "active"))
    .OrderBy("category")
    .OrderByDescending("price")
    .ToListAsync();

// Weighted sorting for RRF (vector/full-text ranking)
galaxy.Query()
    .Select("name", "description")
    .Top(10)
    .Cluster(c => c
        .VectorDistance("titleEmbedding", titleVector)
        .VectorDistance("descriptionEmbedding", descVector))
    .WithWeights("[0.8, 0.2]")
    .ToListAsync();
```

## Aggregation

```csharp
galaxy.Query()
    .Select("category")
    .Aggregate("price", Q.Aggregate.Sum)
    .Aggregate("price", Q.Aggregate.Avg)
    .Aggregate("quantity", Q.Aggregate.Max)
    .Aggregate("id", Q.Aggregate.Count)
    .GroupBy("category")
    .Cluster(c => c.Gte("addedOn", DateTime.Now.AddMonths(-3)))
    .ToListAsync();
```

Supported aggregation functions: `Count`, `Sum`, `Min`, `Max`, `Avg`.

## Pagination

```csharp
// First page
(Gravity g1, IList<MyModel> page1) = await galaxy.Query()
    .Select("id", "name", "price")
    .Paged(25)
    .Cluster(c => c.Eq("status", "active"))
    .OrderByDescending("addedOn")
    .ToListAsync();

// Next page using continuation token
(Gravity g2, IList<MyModel> page2) = await galaxy.Query()
    .Select("id", "name", "price")
    .Paged(25, g1.ContinuationToken)
    .Cluster(c => c.Eq("status", "active"))
    .OrderByDescending("addedOn")
    .ToListAsync();
```

> **Note:** `.Paged()` and `.WithHints()` cannot be used together. Attempting to do so will throw an `UniverseException`.

## Joins

```csharp
galaxy.Query()
    .Join(arrayPath: "orders", alias: "o", columns: ["o.total", "o.date"])
    .Cluster(c => c.Eq("status", "active"))
    .ToListAsync();
```

## Query Hints

```csharp
galaxy.Query()
    .Select("name", "price")
    .Top(10)
    .Cluster(c => c.Eq("category", "Electronics"))
    .WithHints(new QueryHints(
        MaxItemCount: 100,
        EnableOptimisticDirectExecution: true))
    .ToListAsync();
```

## Terminal Operations

Every fluent chain must end with a terminal operation that executes or generates the query:

| Method | Returns | Description |
|--------|---------|-------------|
| `.ToListAsync()` | `Task<(Gravity g, IList<T> T)>` | Execute and return a list of results |
| `.ToListAsync<TS>()` | `Task<(Gravity g, IList<TS> T)>` | Execute with type projection |
| `.GetAsync()` | `Task<(Gravity g, T T)>` | Execute and return the first matching result |
| `.GetAsync<TS>()` | `Task<(Gravity g, TS S)>` | Get first result with type projection |
| `.GenerateQuery()` | `Gravity` | Generate SQL without executing (for debugging) |

All terminal operations return the same `Gravity` response object with RU consumption, continuation tokens, and optional query debug info.

### Type Projection

Project results to a different type. The projection target can be any type — classes, records, structs, or record structs. No `ICosmicEntity` constraint required.

```csharp
// String select + generic terminal method
(Gravity g, IList<ProductSummary> results) = await galaxy.Query()
    .Select("Name", "Price")
    .Cluster(c => c.Eq("category", "Electronics"))
    .ToListAsync<ProductSummary>();

// Type-based select + generic terminal method
(Gravity g, IList<ProductSummary> results) = await galaxy.Query()
    .Select<ProductSummary>()
    .Cluster(c => c.Eq("category", "Electronics"))
    .ToListAsync<ProductSummary>();
```

`Select<TProjection>()` controls which columns appear in the SQL SELECT clause. `ToListAsync<TS>()` / `GetAsync<TS>()` controls what type the results are deserialized into. They are independent concerns that typically use the same type.

### Query Generation (Debugging)

Generate the SQL without executing it:

```csharp
Gravity queryInfo = galaxy.Query()
    .Select("id", "name")
    .Cluster(c => c.Like("code", "%SAMPLE%"))
    .GenerateQuery();

Console.WriteLine(queryInfo.Query.Value.Text);
// Output: SELECT c["id"], c["name"] FROM c WHERE (c["code"] LIKE @param1)
```

## No-Filter Queries

Omit `.Cluster()` entirely to query all documents:

```csharp
(Gravity g, IList<MyModel> all) = await galaxy.Query()
    .Select("id", "name")
    .Top(100)
    .ToListAsync();
```

## Chaining Order

The fluent API allows methods to be called in any order (except terminal operations, which must be last). All state is accumulated and materialized when the terminal operation is called.

```csharp
// All of these are equivalent:
galaxy.Query().Select("a").Cluster(c => c.Eq("b", 1)).Top(10).ToListAsync();
galaxy.Query().Top(10).Cluster(c => c.Eq("b", 1)).Select("a").ToListAsync();
galaxy.Query().Cluster(c => c.Eq("b", 1)).Select("a").Top(10).ToListAsync();
```

## Custom Alias

All operator methods accept an optional `alias` parameter (defaults to `"c"`):

```csharp
galaxy.Query()
    .Cluster(c => c.Eq("name", "test", alias: "doc"))
    .ToListAsync();
```

## Complete Example

```csharp
// Complex multi-cluster query with all features
(Gravity gravity, IList<MyModel> results) = await galaxy.Query()
    .Select("id", "name", "price", "category")
    .Top(20)
    .Cluster(c => c
        .Like("name", "%Premium%")
        .And().Gte("addedOn", DateTime.UtcNow.AddDays(-30))
        .And().Lte("price", 500.0))
    .Or()
    .Cluster(c => c
        .Eq("category", "Featured")
        .And().Defined("promotionEndDate"))
    .OrderByDescending("price")
    .ToListAsync();

Console.WriteLine($"Found {results.Count} items, consumed {gravity.RU} RU");
```
