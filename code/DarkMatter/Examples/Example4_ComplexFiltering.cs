using DarkMatter.Models;
using Universe.Builder.Options;
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
                    new(nameof(MyObject.Name), "%Test%", Operator: Q.Operator.Like),
                    new(nameof(MyObject.AddedOn), DateTime.Now.AddDays(-30), Operator: Q.Operator.Gte, Where: Q.Where.And),
                    new(nameof(MyObject.Price), 50.0, Operator: Q.Operator.Lte, Where: Q.Where.And)
                ], Where: Q.Where.And),
                new(Catalysts:
                [
                    new(nameof(MyObject.Code), "SPECIAL", Where: Q.Where.Or),
                    new(nameof(MyObject.Category), "Premium", Where: Q.Where.And)
                ])
            ],
            columnOptions: new(
                Names: [nameof(MyObject.id), nameof(MyObject.Name), nameof(MyObject.Price), nameof(MyObject.Category)],
                Top: 20
            ),
            sorting: [new(nameof(MyObject.Price), Sorting.Direction.DESC)]
        );

        ruUsed = g4.RU;
        PrintQueryResults(g4, results4);
        return ruUsed;
    }
}
