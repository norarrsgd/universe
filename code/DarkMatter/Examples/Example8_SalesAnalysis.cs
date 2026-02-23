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
        (Gravity g8b, IList<MyObjectAggregation> resultsByRegion) = await galaxy.List<MyObjectAggregation>(
            clusters: null,
            columnOptions: new(
                Names: [nameof(MyObject.Category) /* represents Region in this example */],
                Aggregates:
                [
                    new(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Sum),
                    new(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Sum),
                    new(nameof(MyObject.id).ToLowerCamelCase(), Q.Aggregate.Count)
                ]
            )
        );

        PrintQueryResults(g8b, resultsByRegion);

        // Run an analysis query: Sales by Date (last 7 days vs older)
        Console.WriteLine("\nSales Analysis by Recency:");

        // First, recent sales (last 7 days)
        (Gravity g8c, IList<MyObjectAggregation> recentSales) = await galaxy.List<MyObjectAggregation>(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.AddedOn).ToLowerCamelCase(), today.AddDays(-7), Operator: Q.Operator.Gte)
                ])
            ],
            columnOptions: new(
                Names: [nameof(MyObject.Category).ToLowerCamelCase()],
                Aggregates:
                [
                    new(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Sum),
                    new(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Sum),
                    new(nameof(MyObject.id).ToLowerCamelCase(), Q.Aggregate.Count)
                ]
            )
        );

        Console.WriteLine("Recent Sales (Last 7 days):");
        PrintQueryResults(g8c, recentSales);

        // Then, older sales
        (Gravity g8d, IList<MyObject> olderSales) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.AddedOn).ToLowerCamelCase(), today.AddDays(-7), Operator: Q.Operator.Lt),
                    new(nameof(MyObject.AddedOn).ToLowerCamelCase(), today.AddDays(-30), Operator: Q.Operator.Gte, Where: Q.Where.And)
                ])
            ],
            columnOptions: new(
                Names: [nameof(MyObject.Category).ToLowerCamelCase()],
                Aggregates:
                [
                    new(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Sum),
                    new(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Sum),
                    new(nameof(MyObject.id).ToLowerCamelCase(), Q.Aggregate.Count)
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