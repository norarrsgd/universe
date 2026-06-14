using DarkMatter.Models;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example3_TopAndDistinct(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override async Task<double> RunAsync()
    {
        Console.WriteLine("\n=== EXAMPLE 3: Using TOP and DISTINCT ===\n");
        (Gravity g3, IList<MyObject> results3) = await galaxy.Query()
            .Select(
                nameof(MyObject.Code).ToLowerCamelCase(),
                nameof(MyObject.Name).ToLowerCamelCase())
            .Distinct()
            .Top(10)
            .ToListAsync();

        ruUsed = g3.RU;
        PrintQueryResults(g3, results3);
        return ruUsed;
    }
}
