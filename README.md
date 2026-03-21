# Universe
A simpler way of querying a CosmosDb Namespace

## Installation

```
dotnet add package UniverseQuery
```

## How-to:
1. Your models / cosmos entities should inherit from the base class
```csharp
public class MyCosmosEntity : CosmicEntity
{
  [PartitionKey]
  public string FirstName { get; set; }

  public string LastName { get; set; }
}
```
This will allow you to use the `PartitionKey` attribute to specify the partition key for your Cosmos DB documents. You can also use multiple partition keys by specifying the order in the attribute, e.g., `[PartitionKey(1)]`, `[PartitionKey(2)]`, etc.

2. Create a repository like so:
```csharp
public class MyRepository : Galaxy<MyModel>
{
    public MyRepository(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey) : base(client, database, container, partitionKey)
    {
    }
}

// If you want to see debug information such as the full Query text executed, use the format below:
public class MyRepository : Galaxy<MyModel>
{
    public MyRepository(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey) : base(client, database, container, partitionKey, true)
    {
    }
}
```

3. In your Startup.cs / Main method / Program.cs, configure the CosmosClient like so:
```csharp
_ = services.AddScoped(_ => new CosmosClient(
    System.Environment.GetEnvironmentVariable("CosmosDbUri"),
    System.Environment.GetEnvironmentVariable("CosmosDbPrimaryKey"),
    clientOptions: new CosmosClientOptions()
    {
        Serializer = new UniverseSerializer(), // This is from Universe.Builder.Options
        AllowBulkExecution = true // This will tell the underlying code to allow async bulk operations
    }
));
```

Below are the default options for the `UniverseSerializer`:
```csharp
new JsonSerializerOptions()
{
    PropertyNamingPolicy = null, // To leave the property names as they are in the model
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    IgnoreReadOnlyFields = true,
    IgnoreReadOnlyProperties = true
}
```

4. In your Startup.cs / Main method / Program.cs, configure your CosmosDb repository like so:
```csharp
_ = services.AddScoped<IGalaxy<MyModel>, MyRepository>(service => new MyRepository(
    client: service.GetRequiredService<CosmosClient>(),
    database: "database-name",
    container: "container-name",
    partitionKey: typeof(MyModel).BuildPartitionKey()
));
```

5. Inject your `IGalaxy<MyModel>` dependency into your classes and enjoy a simpler way to query CosmosDb

## Understanding the Gravity Object

The `Gravity` object is returned by all operations and contains valuable information:

```csharp
(Gravity gravity, MyModel model) = await galaxy.Get("document-id", "partition-key-value");

// Request Units consumed by the operation
double requestUnits = gravity.RU;

// Continuation token for pagination (only populated in Paged queries)
string continuationToken = gravity.ContinuationToken;

// Query information (only available when debug mode is enabled)
if (gravity.Query.HasValue)
{
    string queryText = gravity.Query.Value.Text;
    IEnumerable<(string, object)> parameters = gravity.Query.Value.Parameters;

    Console.WriteLine($"Query: {queryText}");
    foreach ((string name, object value) in parameters)
    {
        Console.WriteLine($"Parameter: {name} = {value}");
    }
}
```

## Examples
This section provides examples of how to use the `Galaxy` repository for basic and advanced operations with Cosmos DB.
_[Here](https://github.com/kuromukira/universe/blob/dev/code/DarkMatter/Examples)._

## Basic CRUD Operations

### Get a Document

```csharp
// Get a single document by id and partition key
(Gravity gravity, MyModel model) = await galaxy.Get("document-id", "partition-key-value");
```

```csharp
// Get a single document by id and multiple partition keys
(Gravity gravity, MyModel model) = await galaxy.Get("document-id", "partition-key-value1", "partition-key-value2");
```

### Creating Documents

```csharp
// Create a single document
MyModel model = new MyModel
{
    PropertyName = "value",
    // Set other properties
};
(Gravity gravity, string id) = await galaxy.Create(model);

// Bulk create multiple documents
List<MyModel> models = new List<MyModel>
{
    new MyModel { /* properties */ },
    new MyModel { /* properties */ }
};
Gravity gravity = await galaxy.Create(models);
```

### Updating Documents

```csharp
// Update a single document
model.PropertyName = "new value";
(Gravity gravity, MyModel updatedModel) = await galaxy.Modify(model);

// Bulk update multiple documents
foreach (MyModel item in models)
{
    item.PropertyName = "new value";
}
Gravity gravity = await galaxy.Modify(models);
```

### Deleting Documents

```csharp
// Delete a document
Gravity gravity = await galaxy.Remove("document-id", "partition-key-value");
```

```csharp
// Delete a document by id and multiple partition keys
Gravity gravity = await galaxy.Remove("document-id", "partition-key-value1", "partition-key-value2");
```

## Querying Documents

Universe provides two equivalent ways to build queries. Both produce identical Cosmos DB SQL — choose whichever style you prefer.

### Fluent Query Builder (Orbit)

The `Orbit<T>` fluent query builder provides a chainable, readable API for constructing queries. Call `.Query()` on any `IGalaxy<T>` instance to start building.

```csharp
using Universe.Extensions; // Provides the .Query() extension method

// Filter + sort
var (g, results) = await galaxy.Query()
    .Select("id", "name", "price")
    .Top(20)
    .Cluster(c => c.Like("name", "%Test%").And().Lte("price", 50.0))
    .Or()
    .Cluster(c => c.Eq("code", "SPECIAL"))
    .OrderByDescending("price")
    .ToListAsync();
```

```csharp
// Pagination
var (g1, page1) = await galaxy.Query()
    .Select("id", "name", "price")
    .Paged(25)
    .Cluster(c => c.Eq("status", "active"))
    .OrderByDescending("addedOn")
    .ToListAsync();

// Next page
var (g2, page2) = await galaxy.Query()
    .Select("id", "name", "price")
    .Paged(25, g1.ContinuationToken)
    .Cluster(c => c.Eq("status", "active"))
    .OrderByDescending("addedOn")
    .ToListAsync();
```

```csharp
// Aggregation
var (g, results) = await galaxy.Query()
    .Select("category")
    .Aggregate("price", Q.Aggregate.Sum)
    .Aggregate("price", Q.Aggregate.Avg)
    .Aggregate("id", Q.Aggregate.Count)
    .GroupBy("category")
    .Cluster(c => c.Gte("addedOn", DateTime.Now.AddMonths(-3)))
    .ToListAsync();
```

See the [Fluent Query Builder (Orbit) Reference](https://github.com/kuromukira/universe/blob/dev/docs/FLUENT_QUERY_BUILDER.md) for the complete API covering all operators, vector search, full-text search, joins, query hints, and more.

### Declarative Syntax (Cluster / Catalyst)

The declarative syntax uses `Cluster` and `Catalyst` structs to compose filter conditions as data structures.

```csharp
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
```

See the [Declarative Query Syntax Reference](https://github.com/kuromukira/universe/blob/dev/docs/DECLARATIVE_QUERY_SYNTAX.md) for complex queries, special operators, sorting, pagination, aggregation, and more.

## Vector Distance Search
The Universe library supports vector similarity search through the `Q.Operator.VectorDistance` operator, which leverages Azure Cosmos DB's built-in vector search capabilities.

See the [VECTORDISTANCE_USAGE.md](https://github.com/kuromukira/universe/blob/dev/docs/VECTORDISTANCE_USAGE.md)

## Full-Text Search
The Universe library provides a simple way to perform full-text search queries using the `Q.Operator.FT*` operators. This allows you to search for documents containing specific text in designated fields.

See the [FULLTEXT_USAGE.md](https://github.com/kuromukira/universe/blob/dev/docs/FULLTEXT_USAGE.md)

## Query Execution Strategies
The library uses a strategy pattern for query execution, automatically selecting the optimal strategy based on query characteristics. Strategies include Direct (standard queries), Gateway (cross-partition), and VectorSearch (vector similarity). You can also provide `QueryHints` to fine-tune execution behavior.

See the [QUERY_EXECUTION_STRATEGIES.md](https://github.com/kuromukira/universe/blob/dev/docs/QUERY_EXECUTION_STRATEGIES.md)

## Stored Procedures

You can manage and execute Cosmos DB stored procedures using the `IGalaxyProcedure` interface. Inject your repository as `IGalaxyProcedure` and use its methods for full stored procedure lifecycle management and execution.

```csharp
IGalaxyProcedure galaxyProcedure = ...; // Injected or resolved from DI

// Execute a stored procedure and get a result of type T
(Gravity gravity, MyModel result) = await galaxyProcedure.ExecSProc<MyModel>(
    procedureName: "myStoredProcedure",
    partitionKey: "partition-key-value",
    parameters: new object[] { /* procedure parameters */ }
);

// Create a new stored procedure
Gravity createResult = await galaxyProcedure.CreateSProc(
    procedureName: "myStoredProcedure",
    body: "function (...) { /* JS code */ }"
);

// Read a stored procedure's body
(Gravity readGravity, string body) = await galaxyProcedure.ReadSProc("myStoredProcedure");

// Replace an existing stored procedure
Gravity replaceResult = await galaxyProcedure.ReplaceSProc(
    procedureName: "myStoredProcedure",
    newBody: "function (...) { /* new JS code */ }"
);

// Delete a stored procedure
Gravity deleteResult = await galaxyProcedure.DeleteSProc("myStoredProcedure");

// List all stored procedure names
(Gravity listGravity, IList<string> names) = await galaxyProcedure.ListSProcs();
```

- `ExecSProc<T>`: Executes a stored procedure and returns a tuple of `Gravity` and the deserialized result of type `T`.
- `CreateSProc`: Creates a new stored procedure with the given name and body.
- `ReadSProc`: Reads the body of a stored procedure.
- `ReplaceSProc`: Replaces the body of an existing stored procedure.
- `DeleteSProc`: Deletes a stored procedure by name.
- `ListSProcs`: Lists all stored procedure names in the container.

The `Gravity` object provides RU and diagnostic information for each operation.

## Error Handling

```csharp
try
{
    (Gravity gravity, MyModel model) = await galaxy.Get("non-existent-id", "partition-key");
}
catch (UniverseException ex)
{
    // Universe-specific exceptions
    Console.WriteLine($"Universe error: {ex.Message}");
}
catch (CosmosException ex)
{
    // Cosmos DB specific exceptions
    Console.WriteLine($"Cosmos error: {ex.Message}, Status: {ex.StatusCode}");
}
catch (Exception ex)
{
    // Other errors
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Performance Considerations

- **Bulk Operations**: Enable `AllowBulkExecution` in the CosmosClientOptions for efficient batch processing.
- **RU Tracking**: The `Gravity` object provides RU consumption information for cost optimization.
- **Column Selection**: Select only the columns you need to reduce data transfer.
- **Debug Mode**: The debug mode (enabled by passing `true` to the Galaxy constructor) provides query details but adds overhead.
- **Partition Key**: Always consider your partition strategy for best performance.

## Documentation

| # | Document | Description |
|---|----------|-------------|
| 1 | [Fluent Query Builder (Orbit)](https://github.com/kuromukira/universe/blob/dev/docs/FLUENT_QUERY_BUILDER.md) | Complete Orbit API reference — operators, pagination, aggregation, vector search, full-text search, joins |
| 2 | [Declarative Query Syntax](https://github.com/kuromukira/universe/blob/dev/docs/DECLARATIVE_QUERY_SYNTAX.md) | Cluster/Catalyst query syntax — complex queries, sorting, pagination, aggregation |
| 3 | [Vector Distance Search](https://github.com/kuromukira/universe/blob/dev/docs/VECTORDISTANCE_USAGE.md) | Vector similarity search with VECTOR_DISTANCE, RRF, hybrid search |
| 4 | [Full-Text Search](https://github.com/kuromukira/universe/blob/dev/docs/FULLTEXT_USAGE.md) | Full-text search operators — FTContains, FTScore, hybrid search |
| 5 | [Query Execution Strategies](https://github.com/kuromukira/universe/blob/dev/docs/QUERY_EXECUTION_STRATEGIES.md) | Strategy pattern, query hints, and performance tuning |
| 6 | [SQLite Statistics Storage](https://github.com/kuromukira/universe/blob/dev/docs/SQLITE_STATISTICS_STORAGE.md) | SQLite-based query statistics persistence and configuration |
| 7 | [Adaptive Query Optimization](https://github.com/kuromukira/universe/blob/dev/docs/ADAPTIVE_QUERY_OPTIMIZATION_DESIGN.md) | Design document for adaptive learning (v3.2.0+) |
