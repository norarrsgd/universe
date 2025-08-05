using Universe.Response;
using DarkMatter.Models;
using Universe.Interfaces;

namespace DarkMatter.Examples;

public abstract class ExampleBase(IGalaxy<MyObject> galaxy)
{
	protected readonly IGalaxy<MyObject> galaxy = galaxy;
	protected double ruUsed = 0;

	public abstract Task<double> RunAsync();

	protected void PrintQueryResults<T>(Gravity g, IList<T> results) where T : MyObject
	{
		Console.WriteLine($"RU Spent: {g.RU}");

		// Display query information if available
		if (g.Query != default)
		{
			Console.WriteLine($"Query: {g.Query.Text}");
			foreach ((string, object) p in g.Query.Parameters)
				Console.WriteLine($"  Parameter: {p.Item1} = {p.Item2}");
		}

		Console.WriteLine($"Result Count: {results.Count}");

		// Print first few results if any
		int displayCount = Math.Min(results.Count, 3);
		for (int i = 0; i < displayCount; i++)
		{
			Console.WriteLine($"  Item {i + 1}: {results[i].Code} - {results[i].Name}");
		}
	}
}