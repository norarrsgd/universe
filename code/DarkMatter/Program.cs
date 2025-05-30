using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Universe;
using Universe.Interfaces;
using Universe.Builder.Options;
using Universe.Response;

string CosmosDbUri = "<FROM AZURE>";
string CosmosDbPrimaryKey = "<FROM AZURE>";

// Imagine this part here as your dependency injection
CosmosClient cosmosClient = new(
    CosmosDbUri,
    CosmosDbPrimaryKey,
    clientOptions: new()
    {
        Serializer = new UniverseSerializer(),
        AllowBulkExecution = true // This will tell the underlying code to allow async bulk operations
    }
);

IGalaxy<MyObject> galaxy = new MyRepo(
    client: cosmosClient,
    database: "<DATABASE NAME>",
    container: "<CONTAINER NAME>",
    partitionKey: "/<PARTITION KEY>"
);
// end of dependency injection

// EXAMPLE 1: Basic Paged Query with Filtering, Column Selection, and Sorting
Console.WriteLine("\n=== EXAMPLE 1: Basic Paged Query ===\n");
(Gravity g1, IList<MyObject> results1) = await galaxy.Paged(
    page: new Q.Page(50),
    clusters:
    [
        new(Catalysts:
        [
            new(nameof(MyObject.Links), "<VALUE TO QUERY>", Operator: Q.Operator.In),
            new(nameof(MyObject.Code), "<VALUE TO QUERY>", Where: Q.Where.Or)
        ], Where: Q.Where.And),
        new(Catalysts:
        [
            new(nameof(MyObject.Name), Operator: Q.Operator.Defined),
            new(nameof(MyObject.Description), Operator: Q.Operator.Defined)
        ], Where: Q.Where.And)
    ],
    columnOptions: new(
        Names:
        [
            nameof(MyObject.id),
            nameof(MyObject.Code),
            nameof(MyObject.Name),
            nameof(MyObject.Description)
        ]
    ),
    sorting:
    [
        new(nameof(MyObject.Name), Sorting.Direction.DESC)
    ]
);

PrintQueryResults(g1, results1);

// EXAMPLE 2: Using Aggregates with Group By
Console.WriteLine("\n=== EXAMPLE 2: Using Aggregates with Group By ===\n");
(Gravity g2, IList<MyObject> results2) = await galaxy.List(
    clusters:
    [
        new(Catalysts:
        [
            new(nameof(MyObject.AddedOn), DateTime.Now.AddMonths(-3), Operator: Q.Operator.Gte)
        ])
    ],
    columnOptions: new(
        Names: [nameof(MyObject.Category)],
        Aggregates: new Dictionary<string, Q.Aggregate>
        {
            { nameof(MyObject.Quantity), Q.Aggregate.Count },
            { nameof(MyObject.Price), Q.Aggregate.Sum },
            { nameof(MyObject.Price), Q.Aggregate.Avg },
            { nameof(MyObject.Quantity), Q.Aggregate.Max },
            { nameof(MyObject.Quantity), Q.Aggregate.Min }
        }
    )
);

PrintQueryResults(g2, results2);

// EXAMPLE 3: Using TOP and DISTINCT
Console.WriteLine("\n=== EXAMPLE 3: Using TOP and DISTINCT ===\n");
(Gravity g3, IList<MyObject> results3) = await galaxy.List(
    clusters: null, // No filtering, return all records
    columnOptions: new(
        Names: [nameof(MyObject.Code), nameof(MyObject.Name)],
        IsDistinct: true,
        Top: 10
    )
);

PrintQueryResults(g3, results3);

// EXAMPLE 4: Complex Filtering with Different Operators
Console.WriteLine("\n=== EXAMPLE 4: Complex Filtering ===\n");
(Gravity g4, IList<MyObject> results4) = await galaxy.List(
    clusters:
    [
        new(Catalysts:
        [
            new(nameof(MyObject.Name), "%Test%", Operator: Q.Operator.Like),
            new(nameof(MyObject.AddedOn), DateTime.Now.AddDays(-30), Operator: Q.Operator.Gte, Where: Q.Where.And),
            new(nameof(MyObject.Price), 50.0, Operator: Q.Operator.Lte, Where: Q.Where.And)
        ], Where: Q.Where.And),
        new(Catalysts:
        [
            new(nameof(MyObject.Code), "SPECIAL", Where: Q.Where.Or),
            new(nameof(MyObject.Category), "Premium", Where: Q.Where.And)
        ])
    ],
    columnOptions: new(
        Names: [nameof(MyObject.id), nameof(MyObject.Name), nameof(MyObject.Price), nameof(MyObject.Category)],
        Top: 20
    ),
    sorting: [new(nameof(MyObject.Price), Sorting.Direction.DESC)]
);

PrintQueryResults(g4, results4);

// EXAMPLE 5: Single Item Operations
Console.WriteLine("\n=== EXAMPLE 5: Single Item Operations ===\n");

// Create a new item
MyObject newObject = new()
{
    Code = "NEW-ITEM-" + DateTime.Now.Ticks,
    Name = "New Test Item",
    Description = "Created via Universe API",
    Links = ["link1", "link2"],
    Price = 29.99,
    Quantity = 10,
    Category = "Electronics"
};

(Gravity g5a, string newId) = await galaxy.Create(newObject);
Console.WriteLine($"Created new item with ID: {newId}, RU: {g5a.RU}");

// Get the item by id and partition key
(Gravity g5b, MyObject retrievedObject) = await galaxy.Get(newId, newObject.Code);
Console.WriteLine($"Retrieved item: {retrievedObject.Name}, RU: {g5b.RU}");

// Update the item
retrievedObject.Description = "Updated description";
(Gravity g5c, MyObject updatedObject) = await galaxy.Modify(retrievedObject);
Console.WriteLine($"Updated item: {updatedObject.Description}, RU: {g5c.RU}");

// Delete the item
Gravity g5d = await galaxy.Remove(newId, newObject.Code);
Console.WriteLine($"Deleted item, RU: {g5d.RU}");

// EXAMPLE 6: Bulk Operations
Console.WriteLine("\n=== EXAMPLE 6: Bulk Operations ===\n");

// Create multiple items
List<MyObject> bulkItems = [];
for (int i = 1; i <= 3; i++)
{
    bulkItems.Add(new MyObject
    {
        Code = "BULK-" + i,
        Name = $"Bulk Item {i}",
        Description = "Part of bulk operation",
        Links = [$"bulk-link-{i}"],
        Price = 10.99 * i,
        Quantity = i * 5,
        Category = i % 2 == 0 ? "Electronics" : "Books"
    });
}

Gravity g6 = await galaxy.Create(bulkItems);
Console.WriteLine($"Created {bulkItems.Count} items in bulk, RU: {g6.RU}");

// Clean up bulk items (in a real application, you might not want to do this immediately)
foreach (MyObject item in bulkItems)
{
    await galaxy.Remove(item.id, item.Code);
}

// EXAMPLE 7: Advanced Aggregation Examples
Console.WriteLine("\n=== EXAMPLE 7: Advanced Aggregation by Category ===\n");

// First create some sample data with categories
List<MyObject> categoryItems = [];
string[] categories = ["Electronics", "Books", "Clothing", "Toys"];
Random random = new();

for (int i = 1; i <= 10; i++)
{
    string category = categories[random.Next(categories.Length)];
    categoryItems.Add(new MyObject
    {
        Code = $"CAT-ITEM-{i}",
        Name = $"Category Item {i}",
        Description = $"Category example item {i}",
        Links = [$"cat-link-{i}"],
        Price = Math.Round(random.NextDouble() * 100, 2),
        Quantity = random.Next(1, 50),
        Category = category
    });
}

// Bulk create the category items
Gravity g7a = await galaxy.Create(categoryItems);
Console.WriteLine($"Created {categoryItems.Count} category items for aggregation, RU: {g7a.RU}");

// Now run an aggregation query grouped by Category
(Gravity g7b, IList<MyObject> results7) = await galaxy.List(
    clusters: null,
    columnOptions: new(
        Names: [nameof(MyObject.Category)],
        Aggregates: new Dictionary<string, Q.Aggregate>
        {
            { nameof(MyObject.Price), Q.Aggregate.Sum },
            { nameof(MyObject.Price), Q.Aggregate.Avg },
            { nameof(MyObject.Quantity), Q.Aggregate.Sum },
            { nameof(MyObject.id), Q.Aggregate.Count }
        }
    )
);

Console.WriteLine("Category Aggregation Results:");
Console.WriteLine($"RU: {g7b.RU}");
if (g7b.Query != default)
{
    Console.WriteLine($"Query: {g7b.Query.Text}");
}

foreach (MyObject item in results7)
{
    // Note: With GROUP BY queries, you'd typically access the dynamic properties
    // based on the column name + aggregate function suffix
    // In a real scenario, you might use dynamic or JObject to access these properties
    Console.WriteLine($"Results in result set: {results7.Count}");
}

// Clean up category items
foreach (MyObject item in categoryItems)
{
    await galaxy.Remove(item.id, item.Code);
}

// EXAMPLE 8: Sales Analysis Scenario
Console.WriteLine("\n=== EXAMPLE 8: Sales Analysis Scenario ===\n");

// Create sample sales data
List<MyObject> salesData = [];
string[] regions = ["North", "South", "East", "West"];
string[] products = ["Laptop", "Phone", "Tablet", "Desktop"];
DateTime today = DateTime.Today;

for (int i = 1; i <= 20; i++)
{
    // Create data for the last 30 days
    int daysAgo = random.Next(0, 30);
    string region = regions[random.Next(regions.Length)];
    string product = products[random.Next(products.Length)];

    salesData.Add(new MyObject
    {
        Code = $"SALE-{i}",
        Name = $"{product} Sale",
        Description = $"Sale in {region} region",
        Links = [$"sale-{i}"],
        Price = Math.Round(random.NextDouble() * 1000, 2),
        Quantity = random.Next(1, 10),
        Category = region,
        AddedOn = today.AddDays(-daysAgo)
    });
}

// Bulk create the sales data
Gravity g8a = await galaxy.Create(salesData);
Console.WriteLine($"Created {salesData.Count} sales records, RU: {g8a.RU}");

// Run an analysis query: Sales by Region
Console.WriteLine("\nSales Analysis by Region:");
(Gravity g8b, IList<MyObject> resultsByRegion) = await galaxy.List(
    clusters: null,
    columnOptions: new(
        Names: [nameof(MyObject.Category) /* represents Region in this example */],
        Aggregates: new Dictionary<string, Q.Aggregate>
        {
            { nameof(MyObject.Price), Q.Aggregate.Sum },
            { nameof(MyObject.Quantity), Q.Aggregate.Sum },
            { nameof(MyObject.id), Q.Aggregate.Count }
        }
    )
);

PrintQueryResults(g8b, resultsByRegion);

// Run an analysis query: Sales by Date (last 7 days vs older)
Console.WriteLine("\nSales Analysis by Recency:");

// First, recent sales (last 7 days)
(Gravity g8c, IList<MyObject> recentSales) = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new(nameof(MyObject.AddedOn), today.AddDays(-7), Operator: Q.Operator.Gte)
        ])
    ],
    columnOptions: new(
        Names: [],
        Aggregates: new Dictionary<string, Q.Aggregate>
        {
            { nameof(MyObject.Price), Q.Aggregate.Sum },
            { nameof(MyObject.Quantity), Q.Aggregate.Sum },
            { nameof(MyObject.id), Q.Aggregate.Count }
        }
    )
);

Console.WriteLine("Recent Sales (Last 7 days):");
PrintQueryResults(g8c, recentSales);

// Then, older sales
(Gravity g8d, IList<MyObject> olderSales) = await galaxy.List(
    clusters: [
        new(Catalysts: [
            new(nameof(MyObject.AddedOn), today.AddDays(-7), Operator: Q.Operator.Lt),
            new(nameof(MyObject.AddedOn), today.AddDays(-30), Operator: Q.Operator.Gte, Where: Q.Where.And)
        ])
    ],
    columnOptions: new(
        Names: [],
        Aggregates: new Dictionary<string, Q.Aggregate>
        {
            { nameof(MyObject.Price), Q.Aggregate.Sum },
            { nameof(MyObject.Quantity), Q.Aggregate.Sum },
            { nameof(MyObject.id), Q.Aggregate.Count }
        }
    )
);

Console.WriteLine("Older Sales (7-30 days ago):");
PrintQueryResults(g8d, olderSales);

// Clean up sales data
foreach (MyObject item in salesData)
{
    await galaxy.Remove(item.id, item.Code);
}

Console.WriteLine("\nExamples complete. Press Enter to exit.");
Console.ReadLine();

// Helper method for printing query results
void PrintQueryResults(Gravity g, IList<MyObject> results)
{
    Console.WriteLine($"RU Spent: {g.RU}");

    // Display query information if available
    if (g.Query != default)
    {
        Console.WriteLine($"Query: {g.Query.Text}");
        foreach ((string, object) p in g.Query.Parameters)
            Console.WriteLine($"  Parameter: {p.Item1} = {p.Item2}");
    }

    Console.WriteLine($"Result Count: {results.Count}");

    // Print first few results if any
    int displayCount = Math.Min(results.Count, 3);
    for (int i = 0; i < displayCount; i++)
    {
        Console.WriteLine($"  Item {i + 1}: {results[i].id} - {results[i].Name}");
    }
}

// Object definitions
class MyObject : ICosmicEntity
{
    // Universe Generated
    public string id { get; set; }
    public DateTime AddedOn { get; set; }
    public DateTime? ModifiedOn { get; set; }

    [JsonIgnore] public string PartitionKey => Code;

    public string Code { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string[] Links { get; set; }

    // Numeric properties for aggregation examples
    public double Price { get; set; }

    public int Quantity { get; set; }

    public string Category { get; set; }
}

class MyRepo : Galaxy<MyObject>
{
#if DEBUG
    public MyRepo(CosmosClient client, string database, string container, string partitionKey) : base(client, database, container, partitionKey, true)
    {
    }
#else
    public MyRepo(CosmosClient client, string database, string container, string partitionKey) : base(client, database, container, partitionKey)
    {
    }
#endif
}