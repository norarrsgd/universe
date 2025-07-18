using DarkMatter.Models;
using Universe.Builder.Options;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example4_ComplexFiltering(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override async Task<double> RunAsync()
    {
        Console.WriteLine("\n=== EXAMPLE 4: Complex Filtering ===\n");
        (Gravity g4, IList<MyObject> results4) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Name).ToLowerCamelCase(), "%Test%", Operator: Q.Operator.Like),
                    new(nameof(MyObject.AddedOn).ToLowerCamelCase(), DateTime.Now.AddDays(-30), Operator: Q.Operator.Gte, Where: Q.Where.And),
                    new(nameof(MyObject.Price).ToLowerCamelCase(), 50.0, Operator: Q.Operator.Lte, Where: Q.Where.And)
                ], Where: Q.Where.And),
                new(Catalysts:
                [
                    new(nameof(MyObject.Code).ToLowerCamelCase(), "SPECIAL", Where: Q.Where.Or),
                    new(nameof(MyObject.Category).ToLowerCamelCase(), "Premium", Where: Q.Where.And)
                ])
            ],
            columnOptions: new(
                Names: [
                    nameof(MyObject.id).ToLowerCamelCase(),
                    nameof(MyObject.Name).ToLowerCamelCase(),
                    nameof(MyObject.Price).ToLowerCamelCase(),
                    nameof(MyObject.Category).ToLowerCamelCase()
                ],
                Top: 20
            ),
            sorting: [new(nameof(MyObject.Price).ToLowerCamelCase(), Sorting.Direction.DESC)]
        );

        ruUsed = g4.RU;
        PrintQueryResults(g4, results4);
        return ruUsed;
    }
}
