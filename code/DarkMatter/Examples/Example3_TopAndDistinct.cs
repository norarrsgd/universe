using DarkMatter.Models;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example3_TopAndDistinct(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override async Task<double> RunAsync()
    {
        Console.WriteLine("\n=== EXAMPLE 3: Using TOP and DISTINCT ===\n");
        (Gravity g3, IList<MyObject> results3) = await galaxy.List(
            clusters: null, // No filtering, return all records
            columnOptions: new(
                Names: [nameof(MyObject.Code), nameof(MyObject.Name)],
                IsDistinct: true,
                Top: 10
            )
        );

        ruUsed = g3.RU;
        PrintQueryResults(g3, results3);
        return ruUsed;
    }
}
