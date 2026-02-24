using DarkMatter.Helpers;
using DarkMatter.Models;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example6_BulkOperations(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override async Task<double> RunAsync()
    {
        Console.WriteLine("\n=== EXAMPLE 6: Bulk Operations ===\n");

        // Create multiple items using the helper
        List<MyObject> bulkItems = TestDataGenerator.CreateBulkTestItems();

        Gravity g6 = await galaxy.Create(bulkItems);
        Console.WriteLine($"Created {bulkItems.Count} items in bulk, RU: {g6.RU}");

        // Clean up bulk items (in a real application, you might not want to do this immediately)
        foreach (MyObject item in bulkItems)
        {
            await galaxy.Remove(item.id, [.. item.PartitionKeys()]);
        }

        ruUsed = g6.RU;
        return ruUsed;
    }
}