using DarkMatter.Models;
using Universe.Builder.Options;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example2_AggregatesWithGroupBy(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
    public override async Task<double> RunAsync()
    {
        Console.WriteLine("\n=== EXAMPLE 2: Using Aggregates with Group By ===\n");
        (Gravity g2, IList<MyObject> results2) = await galaxy.Query()
            .Select(nameof(MyObject.Category))
            .Aggregate(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Count)
            .Aggregate(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Sum)
            .Aggregate(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Avg)
            .Aggregate(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Max)
            .Aggregate(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Min)
            .Cluster(c => c.Gte(nameof(MyObject.AddedOn), DateTime.Now.AddMonths(-3)))
            .ToListAsync();

        ruUsed = g2.RU;
        PrintQueryResults(g2, results2);
        return ruUsed;
    }
}
