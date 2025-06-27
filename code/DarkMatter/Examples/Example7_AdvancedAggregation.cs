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
        (Gravity g7b, IList<MyObjectAggregation> results7) = await galaxy.List<MyObjectAggregation>(
            clusters: null,
            columnOptions: new(
                Names: [nameof(MyObject.Category)],
                Aggregates:
                [
                    new(nameof(MyObject.Price), Q.Aggregate.Sum),
                    new(nameof(MyObject.Price), Q.Aggregate.Avg),
                    new(nameof(MyObject.Quantity), Q.Aggregate.Sum),
                    new(nameof(MyObject.id), Q.Aggregate.Count)
                ]
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
            await galaxy.Remove(item.id, [.. item.PartitionKeys()]);
        }

        ruUsed = g7a.RU + g7b.RU;
        return ruUsed;
    }
}
