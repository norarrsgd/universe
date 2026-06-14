using DarkMatter.Helpers;
using DarkMatter.Models;
using Universe.Builder.Options;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example7_AdvancedAggregation(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override async Task<double> RunAsync()
    {
        Console.WriteLine("\n=== EXAMPLE 7: Advanced Aggregation by Category ===\n");

        // Create some sample data with categories using the helper
        List<MyObject> categoryItems = TestDataGenerator.CreateCategoryTestItems();

        // Bulk create the category items
        Gravity g7a = await galaxy.Create(categoryItems);
        Console.WriteLine($"Created {categoryItems.Count} category items for aggregation, RU: {g7a.RU}");

        // Now run an aggregation query grouped by Category
        (Gravity g7b, IList<MyObjectAggregation> results7) = await galaxy.Query()
            .Select(nameof(MyObject.Category).ToLowerCamelCase())
            .Aggregate(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Sum)
            .Aggregate(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Avg)
            .Aggregate(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Sum)
            .Aggregate(nameof(MyObject.id).ToLowerCamelCase(), Q.Aggregate.Count)
            .ToListAsync<MyObjectAggregation>();

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
            await galaxy.Remove(item.id, [.. item.PartitionKeys()]);
        }

        ruUsed = g7a.RU + g7b.RU;
        return ruUsed;
    }
}
