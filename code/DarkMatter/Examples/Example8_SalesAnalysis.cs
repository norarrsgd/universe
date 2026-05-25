using DarkMatter.Helpers;
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

        // Create sample sales data using the helper
        List<MyObject> salesData = TestDataGenerator.CreateSalesTestData();
        DateTime today = DateTime.Today;

        // Bulk create the sales data
        Gravity g8a = await galaxy.Create(salesData);
        Console.WriteLine($"Created {salesData.Count} sales records, RU: {g8a.RU}");

        // Run an analysis query: Sales by Region
        Console.WriteLine("\nSales Analysis by Region:");
        (Gravity g8b, IList<MyObjectAggregation> resultsByRegion) = await galaxy.Query()
            .Select(nameof(MyObject.Category) /* represents Region in this example */)
            .Aggregate(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Sum)
            .Aggregate(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Sum)
            .Aggregate(nameof(MyObject.id).ToLowerCamelCase(), Q.Aggregate.Count)
            .ToListAsync<MyObjectAggregation>();

        PrintQueryResults(g8b, resultsByRegion);

        // Run an analysis query: Sales by Date (last 7 days vs older)
        Console.WriteLine("\nSales Analysis by Recency:");

        // First, recent sales (last 7 days)
        (Gravity g8c, IList<MyObjectAggregation> recentSales) = await galaxy.Query()
            .Select(nameof(MyObject.Category).ToLowerCamelCase())
            .Aggregate(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Sum)
            .Aggregate(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Sum)
            .Aggregate(nameof(MyObject.id).ToLowerCamelCase(), Q.Aggregate.Count)
            .Cluster(c => c.Gte(nameof(MyObject.AddedOn).ToLowerCamelCase(), today.AddDays(-7)))
            .ToListAsync<MyObjectAggregation>();

        Console.WriteLine("Recent Sales (Last 7 days):");
        PrintQueryResults(g8c, recentSales);

        // Then, older sales
        (Gravity g8d, IList<MyObject> olderSales) = await galaxy.Query()
            .Select(nameof(MyObject.Category).ToLowerCamelCase())
            .Aggregate(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Sum)
            .Aggregate(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Sum)
            .Aggregate(nameof(MyObject.id).ToLowerCamelCase(), Q.Aggregate.Count)
            .Cluster(c => c
                .Lt(nameof(MyObject.AddedOn).ToLowerCamelCase(), today.AddDays(-7))
                .And().Gte(nameof(MyObject.AddedOn).ToLowerCamelCase(), today.AddDays(-30)))
            .ToListAsync();

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
