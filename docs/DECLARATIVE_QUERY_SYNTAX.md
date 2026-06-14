# Declarative Query Syntax Reference (Cluster / Catalyst)

The declarative syntax is the original lower-level query construction API in Universe. It uses `Cluster` and `Catalyst` structs to build queries by composing filter conditions as data structures.

This API is still fully supported for compatibility and advanced scenarios where callers already have query parts represented as data. For new application code and examples, prefer the [fluent Orbit API](FLUENT_QUERY_BUILDER.md), which provides the same query behavior through `galaxy.Query()`.

## Query with Clusters

```csharp
// Basic query with a single filter condition
(Gravity gravity, MyModel model) = await galaxy.Get(
    clusters: new List<Cluster>()
    {
        new Cluster(Catalysts: new List<Catalyst>
        {
            new Catalyst(nameof(MyModel.PropertyName), "value")
        })
    }
);
```

## Complex Queries with Multiple Conditions

```csharp
// Query with multiple conditions in a single cluster
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>()
    {
        new Cluster(Catalysts: new List<Catalyst>
        {
            new Catalyst(nameof(MyModel.PropertyName), "value"),
            new Catalyst(nameof(MyModel.NumberProperty), 123, Where: Q.Where.And)
        })
    }
);

// Query with multiple clusters (combining conditions with AND/OR)
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>()
    {
        new Cluster(Catalysts: new List<Catalyst>
        {
            new Catalyst(nameof(MyModel.PropertyName), "value"),
            new Catalyst(nameof(MyModel.AnotherProperty), 123, Where: Q.Where.Or)
        }, Where: Q.Where.And),
        new Cluster(Catalysts: new List<Catalyst>
        {
            new Catalyst(nameof(MyModel.Status), "Active")
        })
    }
);
```

## Special Operators

```csharp
// Using In operator for array properties
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>()
    {
        new Cluster(Catalysts: new List<Catalyst>
        {
            new Catalyst(nameof(MyModel.Tags), "tag1", Operator: Q.Operator.In)
        })
    }
);

// Check if a property is defined
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>()
    {
        new Cluster(Catalysts: new List<Catalyst>
        {
            new Catalyst(nameof(MyModel.OptionalProperty), Operator: Q.Operator.Defined)
        })
    }
);

// Comparison operators
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>()
    {
        new Cluster(Catalysts: new List<Catalyst>
        {
            new Catalyst(nameof(MyModel.NumberProperty), 100, Operator: Q.Operator.Gt)
        })
    }
);
```

## Sorting and Column Selection

```csharp
// Query with sorting
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>() { /* query conditions */ },
    sorting: new List<Sorting.Option>
    {
        new Sorting.Option(nameof(MyModel.PropertyName), Sorting.Direction.DESC)
    }
);

// Query with column selection
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>() { /* query conditions */ },
    columnOptions: new ColumnOptions(
        Names: new List<string>
        {
            nameof(MyModel.id),
            nameof(MyModel.PropertyName),
            nameof(MyModel.AnotherProperty)
        }
    )
);

// Using TOP to limit results
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>() { /* query conditions */ },
    columnOptions: new ColumnOptions(
        Names: new List<string>
        {
            nameof(MyModel.id),
            nameof(MyModel.PropertyName)
        },
        Top: 10
    )
);

// Using DISTINCT
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>() { /* query conditions */ },
    columnOptions: new ColumnOptions(
        Names: new List<string>
        {
            nameof(MyModel.PropertyName)
        },
        IsDistinct: true
    )
);
```

For fluent examples of sorting and selection, see [Fluent Query Builder (Orbit)](FLUENT_QUERY_BUILDER.md).

## Pagination

```csharp
// First page
(Gravity gravity, IList<MyModel> items) = await galaxy.Paged(
    page: new Q.Page(25), // 25 items per page
    clusters: new List<Cluster>() { /* query conditions */ }
);

// Access continuation token from the gravity object
string continuationToken = gravity.ContinuationToken;

// Next page using continuation token
(Gravity nextGravity, IList<MyModel> nextItems) = await galaxy.Paged(
    page: new Q.Page(25, continuationToken),
    clusters: new List<Cluster>() { /* same query conditions */ }
);
```

For fluent pagination examples, see [Fluent Query Builder (Orbit)](FLUENT_QUERY_BUILDER.md#pagination).

## Aggregation and Group By Queries

```csharp
// Group by a property
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>() { /* query conditions */ },
    group: new List<string> { nameof(MyModel.Category) }
);

// Using aggregation functions with ColumnOptions.Aggregates
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>() { /* query conditions */ },
    columnOptions: new ColumnOptions(
        Names: new List<string> { nameof(MyModel.Category) },
        Aggregates: [
            new AggregationOption(nameof(MyModel.Price), Q.Aggregate.Sum),
            new AggregationOption(nameof(MyModel.Quantity), Q.Aggregate.Count)
        ]
    )
);

// Multiple aggregation functions in one query
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>() { /* query conditions */ },
    columnOptions: new ColumnOptions(
        Names: new List<string> { nameof(MyModel.Category) },
        Aggregates: [
            new AggregationOption(nameof(MyModel.Price), Q.Aggregate.Sum),
            new AggregationOption(nameof(MyModel.Price), Q.Aggregate.Avg),
            new AggregationOption(nameof(MyModel.Quantity), Q.Aggregate.Max)
        ]
    )
);
```

The `Aggregates` parameter in `ColumnOptions` takes an array of `AggregationOption` structs, each specifying a column name and an aggregate function to apply. It supports the following aggregate functions:

- `Q.Aggregate.Count`: Counts the number of items
- `Q.Aggregate.Sum`: Calculates the sum of the specified column
- `Q.Aggregate.Min`: Finds the minimum value of the specified column
- `Q.Aggregate.Max`: Finds the maximum value of the specified column
- `Q.Aggregate.Avg`: Calculates the average of the specified column

When using aggregates, the query will automatically be grouped by the columns specified in the `Names` parameter. The output column names will be suffixed with the aggregate function name (e.g., `Price_Sum`, `Price_Avg`, `Quantity_Max`).

For fluent aggregation examples, see [Fluent Query Builder (Orbit)](FLUENT_QUERY_BUILDER.md#aggregation).

## See Also

- [Fluent Query Builder (Orbit)](FLUENT_QUERY_BUILDER.md) — the recommended query-builder API for new code
- [Vector Distance Search](VECTORDISTANCE_USAGE.md) — vector similarity search
- [Full-Text Search](FULLTEXT_USAGE.md) — full-text search operators
- [Query Execution Strategies](QUERY_EXECUTION_STRATEGIES.md) — query optimization and strategy selection
- [DarkMatter Examples](https://github.com/norarrsgd/universe/blob/dev/code/DarkMatter/Examples) — runnable fluent-first examples covering all features
