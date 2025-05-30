# Universe
A simpler way of querying a CosmosDb Namespace

## Installation

```
dotnet add package Universe
```

## How-to:
1. Your models / cosmos entities should inherit from the interface
```csharp
public class MyCosmosEntity : ICosmicEntity
{
  public string FirstName { get; set; }
  
  public string LastName { get; set; }
  
  // The properties below are implementations from ICosmicEntity
  public string id { get; set; }
  public DateTime AddedOn { get; set; }
  public DateTime ModifiedOn { get; set; }

  [JsonIgnore]
  public string PartitionKey => FirstName;
}
```

2. Create a repository like so:
```csharp
public class MyRepository : Galaxy<MyModel>
{
    public MyRepository(CosmosClient client, string database, string container, string partitionKey) : base(client, database, container, partitionKey)
    {
    }
}

// If you want to see debug information such as the full Query text executed, use the format below:
public class MyRepository : Galaxy<MyModel>
{
    public MyRepository(CosmosClient client, string database, string container, string partitionKey) : base(client, database, container, partitionKey, true)
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
        Serializer = new UniverseSerializer(), // This is from Universe.Options
        AllowBulkExecution = true // This will tell the underlying code to allow async bulk operations
    }
));
```

4. In your Startup.cs / Main method / Program.cs, configure your CosmosDb repository like so:
```csharp
_ = services.AddScoped<IGalaxy<MyModel>, MyRepository>(service => new MyRepository(
    client: service.GetRequiredService<CosmosClient>(),
    database: "database-name",
    container: "container-name",
    partitionKey: "/partitionKey"
));
```

5. Inject your `IGalaxy<MyModel>` dependency into your classes and enjoy a simpler way to query CosmosDb

## Basic Operations

### Simple Query Operations

```csharp
// Get a single document by id and partition key
(Gravity gravity, MyModel model) = await galaxy.Get("document-id", "partition-key-value");

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

## Advanced Query Examples

### Complex Queries with Multiple Conditions

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

### Special Operators

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

### Sorting and Column Selection

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

### Pagination

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

### Group By Queries

```csharp
// Group by a property
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>() { /* query conditions */ },
    group: new List<string> { nameof(MyModel.Category) }
);

// Group by with COUNT
(Gravity gravity, IList<MyModel> results) = await galaxy.List(
    clusters: new List<Cluster>() { /* query conditions */ },
    columnOptions: new ColumnOptions(
        Names: new List<string> { nameof(MyModel.Category) },
        Count: true
    )
);
```

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
