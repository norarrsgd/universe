using DarkMatter.Models;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example5_SingleItemOperations(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override async Task<double> RunAsync()
    {
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
            Category = "Electronics",
        };

        (Gravity g5a, string newId) = await galaxy.Create(newObject);
        Console.WriteLine($"Created new item with ID: {newId}, RU: {g5a.RU}");

        // Get the item by id and partition key
        (Gravity g5b, MyObject retrievedObject) = await galaxy.Get(newId, newObject.PartitionKey);
        Console.WriteLine($"Retrieved item: {retrievedObject.Name}, RU: {g5b.RU}");

        // Update the item
        retrievedObject.Description = "Updated description";
        (Gravity g5c, MyObject updatedObject) = await galaxy.Modify(retrievedObject);
        Console.WriteLine($"Updated item: {updatedObject.Description}, RU: {g5c.RU}");

        // Delete the item
        Gravity g5d = await galaxy.Remove(newId, newObject.PartitionKey);
        Console.WriteLine($"Deleted item, RU: {g5d.RU}");

        ruUsed = g5a.RU + g5b.RU + g5c.RU + g5d.RU;
        return ruUsed;
    }
}
