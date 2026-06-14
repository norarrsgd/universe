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
        (Gravity g1, IList<MyObject> results1) = await galaxy.Query()
            .Select(
                nameof(MyObject.id),
                nameof(MyObject.Code).ToLowerCamelCase(),
                nameof(MyObject.Name).ToLowerCamelCase(),
                nameof(MyObject.Description).ToLowerCamelCase())
            .Paged(50)
            .Cluster(c => c
                .In(nameof(MyObject.Links), "<VALUE TO QUERY>")
                .Or().Eq(nameof(MyObject.Code), "<VALUE TO QUERY>"))
            .Cluster(c => c
                .Defined(nameof(MyObject.Name))
                .And().Defined(nameof(MyObject.Description)))
            .OrderByDescending(nameof(MyObject.Name).ToLowerCamelCase())
            .ToListAsync();

        ruUsed = g1.RU;
        PrintQueryResults(g1, results1);
        return ruUsed;
    }
}
