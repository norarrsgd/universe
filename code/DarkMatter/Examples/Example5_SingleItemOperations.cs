using DarkMatter.Helpers;
using DarkMatter.Models;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example5_SingleItemOperations(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
	public override async Task<double> RunAsync()
	{
		Console.WriteLine("\n=== EXAMPLE 5: Single Item Operations ===\n");

		// Create a new item using the helper
		MyObject newObject = TestDataGenerator.CreateSingleTestItem();

		(Gravity g5a, string newId) = await galaxy.Create(newObject);
		Console.WriteLine($"Created new item with ID: {newId}, RU: {g5a.RU}");

		// Get the item by id and partition key
		(Gravity g5b, MyObject retrievedObject) = await galaxy.Get(newId, [.. newObject.PartitionKeys()]);
		Console.WriteLine($"Retrieved item: {retrievedObject.Name}, RU: {g5b.RU}");

		// Get the item by custom filter
		(Gravity g5c, MyObject filteredObject) = await galaxy.Get(
			clusters:
			[
				new(Catalysts:
				[
					new(nameof(MyObject.Name).ToLowerCamelCase(), newObject.Name)
				])
			]
		);
		Console.WriteLine($"Filtered item: {filteredObject.Name}, RU: {g5c.RU}");

		// Update the item
		retrievedObject.Description = "Updated description";
		(Gravity g5d, MyObject updatedObject) = await galaxy.Modify(retrievedObject);
		Console.WriteLine($"Updated item: {updatedObject.Description}, RU: {g5d.RU}");

		// Delete the item
		Gravity g5e = await galaxy.Remove(newId, [.. newObject.PartitionKeys()]);
		Console.WriteLine($"Deleted item, RU: {g5e.RU}");

		ruUsed = g5a.RU + g5b.RU + g5c.RU + g5d.RU;
		return ruUsed;
	}
}