using DarkMatter.Models;
using Universe.Builder.Options;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example1_BasicPagedQuery(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override async Task<double> RunAsync()
    {
        Console.WriteLine("\n=== EXAMPLE 1: Basic Paged Query ===\n");
        (Gravity g1, IList<MyObject> results1) = await galaxy.Paged(
            page: new(50),
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Links), "<VALUE TO QUERY>", Operator: Q.Operator.In),
                    new(nameof(MyObject.Code), "<VALUE TO QUERY>", Where: Q.Where.Or)
                ], Where: Q.Where.And),
                new(Catalysts:
                [
                    new(nameof(MyObject.Name), Operator: Q.Operator.Defined),
                    new(nameof(MyObject.Description), Operator: Q.Operator.Defined)
                ], Where: Q.Where.And)
            ],
            columnOptions: new(
                Names:
                [
                    nameof(MyObject.id),
                    nameof(MyObject.Code).ToLowerCamelCase(),
                    nameof(MyObject.Name).ToLowerCamelCase(),
                    nameof(MyObject.Description).ToLowerCamelCase()
                ]
            ),
            sorting:
            [
                new(nameof(MyObject.Name).ToLowerCamelCase(), Sorting.Direction.DESC)
            ]
        );

        ruUsed = g1.RU;
        PrintQueryResults(g1, results1);
        return ruUsed;
    }
}