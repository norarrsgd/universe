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
        Gravity simpleQuery = galaxy.GenerateQuery(
            clusters:
            [
                new(Catalysts:
                [
                    new(Column: nameof(MyObject.Code), Value: "%SAMPLE_CODE%", Operator: Q.Operator.Like)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.id), nameof(MyObject.Name).ToLowerCamelCase()])
        );

        Console.WriteLine($"   Query: {simpleQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", simpleQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        // Example 2: Generate a complex query with aggregations
        Console.WriteLine("2. Generating Query with Aggregations:");
        Gravity aggregateQuery = galaxy.GenerateQuery(
            clusters:
            [
                new(Catalysts:
                [
                    new(Column: nameof(MyObject.Links), Value: "active", Operator: Q.Operator.In),
                    new(Column: nameof(MyObject.Name), Operator: Q.Operator.Defined, Where: Q.Where.And)
                ])
            ],
            columnOptions: new(
                Names:
                [
                    nameof(MyObject.Category).ToLowerCamelCase(),
                    nameof(MyObject.Code).ToLowerCamelCase()
                ],
                Aggregates:
                [
                    new(nameof(MyObject.Price), Q.Aggregate.Sum),
                    new(nameof(MyObject.Price), Q.Aggregate.Avg),
                    new("*", Q.Aggregate.Count)
                ],
                IsDistinct: false,
                Top: 50
            ),
            sorting:
            [
                new(nameof(MyObject.Category).ToLowerCamelCase(), Sorting.Direction.ASC)
            ],
            group: [nameof(MyObject.Category).ToLowerCamelCase(), nameof(MyObject.Code).ToLowerCamelCase()]
        );

        Console.WriteLine($"   Query: {aggregateQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", aggregateQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        // Example 3: Generate a filtered query with sorting
        Console.WriteLine("3. Generating Filtered Query with Sorting:");
        Gravity filteredQuery = galaxy.GenerateQuery(
            clusters:
            [
                new(Catalysts:
                [
                    new(Column: nameof(MyObject.Price), Value: 100.0, Operator: Q.Operator.Gte),
                    new(Column: nameof(MyObject.AddedOn), Value: DateTime.Now.AddDays(-30), Operator: Q.Operator.Gte, Where: Q.Where.And)
                ], Where: Q.Where.And),
                new(Catalysts:
                [
                    new(Column: nameof(MyObject.Description), Operator: Q.Operator.Defined)
                ], Where: Q.Where.Or)
            ],
            columnOptions: new(
                Names:
                [
                    nameof(MyObject.id),
                    nameof(MyObject.Name).ToLowerCamelCase(),
                    nameof(MyObject.Price).ToLowerCamelCase(),
                    nameof(MyObject.Category).ToLowerCamelCase()
                ],
                IsDistinct: true,
                Top: 25
            ),
            sorting:
            [
                new(nameof(MyObject.Price).ToLowerCamelCase(), Sorting.Direction.DESC),
                new(nameof(MyObject.Name).ToLowerCamelCase(), Sorting.Direction.ASC)
            ]
        );

        Console.WriteLine($"   Query: {filteredQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", filteredQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        // Example 4: Generate a query with array joins
        Console.WriteLine("4. Generating Query with Array Joins:");
        Gravity joinQuery = galaxy.GenerateQuery(
            clusters:
            [
                new(Catalysts:
                [
                    new(Column: nameof(MyObject.Category), Value: "%Electronics%", Operator: Q.Operator.Like)
                ])
            ],
            columnOptions: new(
                Names: [nameof(MyObject.id), nameof(MyObject.Name).ToLowerCamelCase()],
                Join: new(
                    ArrayPath: nameof(MyObject.Links).ToLowerCamelCase(),
                    Alias: "link",
                    Columns: ["url", "type"],
                    Aggregates:
                    [
                        new("*", Q.Aggregate.Count)
                    ]
                ),
                Top: 10
            )
        );

        Console.WriteLine($"   Query: {joinQuery.Query.Text}");
        Console.WriteLine($"   Parameters: {string.Join(", ", joinQuery.Query.Parameters.Select(p => $"{p.Item1}={p.Item2}"))}");
        Console.WriteLine();

        Console.WriteLine("Query generation completed successfully! All queries generated without execution.");
        Console.WriteLine("You can now use these queries for debugging, logging, or manual execution.");

        return Task.FromResult(0.0); // No RU consumed since no queries were executed
    }
}