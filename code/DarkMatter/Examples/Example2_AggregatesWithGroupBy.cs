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
        (Gravity g2, IList<MyObject> results2) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.AddedOn), DateTime.Now.AddMonths(-3), Operator: Q.Operator.Gte)
                ])
            ],
            columnOptions: new(
                Names: [nameof(MyObject.Category)],
                Aggregates:
                [
                    new(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Count),
                    new(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Sum),
                    new(nameof(MyObject.Price).ToLowerCamelCase(), Q.Aggregate.Avg),
                    new(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Max),
                    new(nameof(MyObject.Quantity).ToLowerCamelCase(), Q.Aggregate.Min)
                ]
            )
        );

        ruUsed = g2.RU;
        PrintQueryResults(g2, results2);
        return ruUsed;
    }
}
