using DarkMatter.Models;
using Universe.Builder.Options;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example8_SalesAnalysis(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override async Task<double> RunAsync()
    {
        Console.WriteLine("\n=== EXAMPLE 8: Sales Analysis Scenario ===\n");
        Random random = new();

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
        (Gravity g8b, IList<MyObjectAggregation> resultsByRegion) = await galaxy.List<MyObjectAggregation>(
            clusters: null,
            columnOptions: new(
                Names: [nameof(MyObject.Category) /* represents Region in this example */],
                Aggregates:
                [
                    new(nameof(MyObject.Price), Q.Aggregate.Sum),
                    new(nameof(MyObject.Quantity), Q.Aggregate.Sum),
                    new(nameof(MyObject.id), Q.Aggregate.Count)
                ]
            )
        );

        PrintQueryResults(g8b, resultsByRegion);

        // Run an analysis query: Sales by Date (last 7 days vs older)
        Console.WriteLine("\nSales Analysis by Recency:");

        // First, recent sales (last 7 days)
        (Gravity g8c, IList<MyObjectAggregation> recentSales) = await galaxy.List<MyObjectAggregation>(
            clusters: [
                new(Catalysts: [
                    new(nameof(MyObject.AddedOn), today.AddDays(-7), Operator: Q.Operator.Gte)
                ])
            ],
            columnOptions: new(
                Names: [nameof(MyObject.Category)],
                Aggregates:
                [
                    new(nameof(MyObject.Price), Q.Aggregate.Sum),
                    new(nameof(MyObject.Quantity), Q.Aggregate.Sum),
                    new(nameof(MyObject.id), Q.Aggregate.Count)
                ]
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
                Names: [nameof(MyObject.Category)],
                Aggregates:
                [
                    new(nameof(MyObject.Price), Q.Aggregate.Sum),
                    new(nameof(MyObject.Quantity), Q.Aggregate.Sum),
                    new(nameof(MyObject.id), Q.Aggregate.Count)
                ]
            )
        );

        Console.WriteLine("Older Sales (7-30 days ago):");
        PrintQueryResults(g8d, olderSales);

        // Clean up sales data
        foreach (MyObject item in salesData)
        {
            await galaxy.Remove(item.id, [.. item.PartitionKeys()]);
        }

        ruUsed = g8a.RU + g8b.RU + g8c.RU + g8d.RU;
        return ruUsed;
    }
}
