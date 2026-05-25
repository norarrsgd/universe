using DarkMatter.Models;
using Universe.Builder.Options;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example12_QueryGeneration(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override Task<double> RunAsync()
    {
        Console.WriteLine("\n=== EXAMPLE 12: Query Generation Without Execution ===\n");

        // Example 1: Generate a simple query
        Console.WriteLine("1. Generating Simple Query:");
        Gravity simpleQuery = galaxy.Query()
            .Select(nameof(MyObject.id), nameof(MyObject.Name).ToLowerCamelCase())
            .Cluster(c => c.Like(nameof(MyObject.Code), "%SAMPLE_CODE%"))
            .GenerateQuery();

        Console.WriteLine($"   Query: {simpleQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", simpleQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        // Example 2: Generate a complex query with aggregations
        Console.WriteLine("2. Generating Query with Aggregations:");
        Gravity aggregateQuery = galaxy.Query()
            .Select(
                nameof(MyObject.Category).ToLowerCamelCase(),
                nameof(MyObject.Code).ToLowerCamelCase())
            .Top(50)
            .Aggregate(nameof(MyObject.Price), Q.Aggregate.Sum)
            .Aggregate(nameof(MyObject.Price), Q.Aggregate.Avg)
            .Aggregate("*", Q.Aggregate.Count)
            .GroupBy(
                nameof(MyObject.Category).ToLowerCamelCase(),
                nameof(MyObject.Code).ToLowerCamelCase())
            .Cluster(c => c
                .In(nameof(MyObject.Links), "active")
                .And().Defined(nameof(MyObject.Name)))
            .OrderBy(nameof(MyObject.Category).ToLowerCamelCase())
            .GenerateQuery();

        Console.WriteLine($"   Query: {aggregateQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", aggregateQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        // Example 3: Generate a filtered query with sorting
        Console.WriteLine("3. Generating Filtered Query with Sorting:");
        Gravity filteredQuery = galaxy.Query()
            .Select(
                nameof(MyObject.id),
                nameof(MyObject.Name).ToLowerCamelCase(),
                nameof(MyObject.Price).ToLowerCamelCase(),
                nameof(MyObject.Category).ToLowerCamelCase())
            .Distinct()
            .Top(25)
            .Cluster(c => c
                .Gte(nameof(MyObject.Price), 100.0)
                .And().Gte(nameof(MyObject.AddedOn), DateTime.Now.AddDays(-30)))
            .Or()
            .Cluster(c => c.Defined(nameof(MyObject.Description)))
            .OrderByDescending(nameof(MyObject.Price).ToLowerCamelCase())
            .OrderBy(nameof(MyObject.Name).ToLowerCamelCase())
            .GenerateQuery();

        Console.WriteLine($"   Query: {filteredQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", filteredQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        // Example 4: Generate a query with array joins
        Console.WriteLine("4. Generating Query with Array Joins:");
        Gravity joinQuery = galaxy.Query()
            .Select(nameof(MyObject.id), nameof(MyObject.Name).ToLowerCamelCase())
            .Top(10)
            .Join(
                arrayPath: nameof(MyObject.Links).ToLowerCamelCase(),
                alias: "link",
                columns: ["url", "type"],
                aggregates: [new("*", Q.Aggregate.Count)])
            .Cluster(c => c.Like(nameof(MyObject.Category), "%Electronics%"))
            .GenerateQuery();

        Console.WriteLine($"   Query: {joinQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", joinQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        Console.WriteLine("Query generation completed successfully! All queries generated without execution.");
        Console.WriteLine("You can now use these queries for debugging, logging, or manual execution.");

        return Task.FromResult(0.0); // No RU consumed since no queries were executed
    }
}
