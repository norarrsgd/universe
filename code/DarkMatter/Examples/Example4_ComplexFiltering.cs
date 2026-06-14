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
        (Gravity g4, IList<MyObject> results4) = await galaxy.Query()
            .Select(
                nameof(MyObject.id).ToLowerCamelCase(),
                nameof(MyObject.Name).ToLowerCamelCase(),
                nameof(MyObject.Price).ToLowerCamelCase(),
                nameof(MyObject.Category).ToLowerCamelCase())
            .Top(20)
            .Cluster(c => c
                .Like(nameof(MyObject.Name).ToLowerCamelCase(), "%Test%")
                .And().Gte(nameof(MyObject.AddedOn).ToLowerCamelCase(), DateTime.Now.AddDays(-30))
                .And().Lte(nameof(MyObject.Price).ToLowerCamelCase(), 50.0))
            .Cluster(c => c
                .Eq(nameof(MyObject.Code).ToLowerCamelCase(), "SPECIAL")
                .And().Eq(nameof(MyObject.Category).ToLowerCamelCase(), "Premium"))
            .OrderByDescending(nameof(MyObject.Price).ToLowerCamelCase())
            .ToListAsync();

        ruUsed = g4.RU;
        PrintQueryResults(g4, results4);
        return ruUsed;
    }
}
