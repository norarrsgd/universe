using DarkMatter.Models;
using Universe.Builder.Options;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

/// <summary>
/// Demonstrates the Contains and NotContains operators, which check if a document's
/// scalar field value exists (or does not exist) within a user-provided collection.
/// This is the Cosmos DB equivalent of SQL's IN ('a','b','c') / NOT IN ('a','b','c').
/// </summary>
public class Example15_ContainsOperator(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override Task<double> RunAsync()
    {
        Console.WriteLine("\n=== EXAMPLE 15: Contains / NotContains Operators ===\n");
        Console.WriteLine("Contains checks if a user-provided list includes the document's field value.");
        Console.WriteLine("This is the SQL equivalent of: WHERE field IN ('a','b','c')\n");

        // Example 1: Contains with a string array
        Console.WriteLine("1. Contains — filter documents whose Category is in a given list:");
        string[] allowedCategories = ["Electronics", "Books", "Clothing"];
        Gravity containsQuery = galaxy.Query()
            .Select(
                nameof(MyObject.id),
                nameof(MyObject.Name).ToLowerCamelCase(),
                nameof(MyObject.Category).ToLowerCamelCase())
            .Cluster(c => c.Contains(nameof(MyObject.Category), allowedCategories))
            .GenerateQuery();

        Console.WriteLine($"   Query: {containsQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", containsQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        // Example 2: NotContains — exclude certain categories
        Console.WriteLine("2. NotContains — exclude documents whose Code is in a blocklist:");
        string[] blockedCodes = ["DISC-001", "DISC-002"];
        Gravity notContainsQuery = galaxy.Query()
            .Select(nameof(MyObject.id), nameof(MyObject.Code).ToLowerCamelCase())
            .Cluster(c => c.NotContains(nameof(MyObject.Code), blockedCodes))
            .GenerateQuery();

        Console.WriteLine($"   Query: {notContainsQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", notContainsQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        // Example 3: Contains combined with other operators
        Console.WriteLine("3. Contains combined with other operators:");
        Gravity combinedQuery = galaxy.Query()
            .Select(
                nameof(MyObject.id),
                nameof(MyObject.Name).ToLowerCamelCase(),
                nameof(MyObject.Category).ToLowerCamelCase(),
                nameof(MyObject.Price).ToLowerCamelCase())
            .Top(25)
            .Cluster(c => c
                .Contains(nameof(MyObject.Category), allowedCategories)
                .And().Gte(nameof(MyObject.Price), 50.0))
            .Cluster(c => c.Defined(nameof(MyObject.Description)))
            .OrderByDescending(nameof(MyObject.Price).ToLowerCamelCase())
            .GenerateQuery();

        Console.WriteLine($"   Query: {combinedQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", combinedQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        Console.WriteLine("Contains/NotContains examples completed. All queries generated without execution.");

        return Task.FromResult(0.0);
    }
}
