using DarkMatter.Models;
using Universe.Interfaces;
using Universe.Builder.Options;
using Universe.Builder.Strategies;
using Universe.Response;
using Universe.Extensions;

namespace DarkMatter.Examples;

/// <summary>
/// Example demonstrating query execution strategies and optimization
/// </summary>
public class Example13_QueryOptimization(IGalaxy<MyObjectVector> vectorGalaxy)
{
	protected readonly IGalaxy<MyObjectVector> galaxy = vectorGalaxy;

	public async Task<double> RunAsync()
	{
		Console.WriteLine("=== EXAMPLE 13: Query Execution Strategy Examples ===\n");

		double totalRU = 0;

		// Example 1: Query with specific hints for performance optimization
		Console.WriteLine("1. Query with Performance Hints:");
		QueryHints hints = new(
			MaxItemCount: 100,
			EnableOptimisticDirectExecution: true,
			MaxConcurrency: Environment.ProcessorCount
		);

		(Gravity gravity1, IList<MyObjectVector> results1) = await galaxy.List(
			clusters:
			[
				new(Catalysts:
				[
					new(nameof(MyObjectVector.Category), "Electronics", Operator: Q.Operator.Eq)
				])
			],
			columnOptions: new(Names: [nameof(MyObjectVector.Name), nameof(MyObjectVector.Price)], Top: 10),
			hints: hints
		);

		totalRU += gravity1.RU;
		Console.WriteLine($"RU Used: {gravity1.RU:F2}");
		Console.WriteLine($"Results: {results1.Count}");
		Console.WriteLine();

		// Example 2: Force specific strategy for complex queries
		Console.WriteLine("2. Forcing Gateway Strategy for Complex Query:");
		QueryHints gatewayHints = new(
			ForceStrategy: QueryExecutionStrategy.Gateway,
			MaxItemCount: 50,
			MaxConcurrency: 1
		);

		(Gravity gravity2, IList<MyObjectVector> results2) = await galaxy.List(
			clusters:
			[
				new(Catalysts:
				[
					new(nameof(MyObjectVector.Price), 1000.0, Operator: Q.Operator.Lt),
					new(nameof(MyObjectVector.Category), "Electronics", Operator: Q.Operator.Eq)
				], Where: Q.Where.And)
			],
			columnOptions: new(Names: [nameof(MyObjectVector.Name), nameof(MyObjectVector.Price)], Top: 10),
			hints: gatewayHints
		);

		totalRU += gravity2.RU;
		Console.WriteLine($"RU Used: {gravity2.RU:F2}");
		Console.WriteLine($"Results: {results2.Count}");
		Console.WriteLine();

		// Example 3: Vector search with optimized hints
		Console.WriteLine("3. Vector Search with Optimization:");
		QueryHints vectorHints = new(
			MaxItemCount: 50,
			MaxBufferedItemCount: 100,
			EnableOptimisticDirectExecution: true
		);

		float[] searchVector = [.. Enumerable.Range(1, 1536).Select(i => (float)Math.Sin(i * 0.1))];

		(Gravity gravity3, IList<MyObjectVector> results3) = await galaxy.List(
			clusters:
			[
				new(Catalysts:
				[
					new(nameof(MyObjectVector.TitleEmbedding), searchVector, Operator: Q.Operator.VectorDistance)
				])
			],
			columnOptions: new(Names: [nameof(MyObjectVector.Name), nameof(MyObjectVector.Description)], Top: 5),
			hints: vectorHints
		);

		totalRU += gravity3.RU;
		Console.WriteLine($"RU Used: {gravity3.RU:F2}");
		Console.WriteLine($"Results: {results3.Count}");
		Console.WriteLine();

		// Example 4: Get optimization recommendations (adaptive learning)
		Console.WriteLine("4. Query Optimization Recommendations:");
		QueryTuningRecommendations recommendations = galaxy.GetQueryRecommendations("category_filter", QueryType.Simple);

		Console.WriteLine($"Data-Driven: {recommendations.IsDataDriven}");
		Console.WriteLine($"Sample Size: {recommendations.SampleSize}");

		if (recommendations.AverageRU.HasValue)
			Console.WriteLine($"Average RU: {recommendations.AverageRU:F2}");

		if (recommendations.SuccessRate.HasValue)
			Console.WriteLine($"Success Rate: {recommendations.SuccessRate:P2}");

		if (recommendations.AverageExecutionTime.HasValue)
			Console.WriteLine($"Average Execution Time: {recommendations.AverageExecutionTime.Value.TotalMilliseconds:F2}ms");

		if (recommendations.RecommendedStrategy != null)
			Console.WriteLine($"Recommended Strategy: {recommendations.RecommendedStrategy}");

		if (recommendations.SuggestedHints != null && recommendations.SuggestedHints.Any())
		{
			Console.WriteLine("Suggested Hints:");
			foreach (var hint in recommendations.SuggestedHints)
			{
				Console.WriteLine($"  {hint.Key} = {hint.Value}");
			}
		}
		else
		{
			Console.WriteLine("No specific hints suggested (need more query history for data-driven recommendations)");
		}

		Console.WriteLine();

		// Example 5: Aggregation query with hints
		Console.WriteLine("5. Aggregation Query with Performance Hints:");
		QueryHints aggregationHints = new(
			MaxItemCount: 500,
			MaxBufferedItemCount: 1000,
			MaxConcurrency: 2
		);

		(Gravity gravity5, IList<MyObjectVector> results5) = await galaxy.List(
			clusters:
			[
				new(Catalysts:
				[
					new(nameof(MyObjectVector.Category).ToLowerCamelCase(), "Electronics", Operator: Q.Operator.Eq)
				])
			],
			columnOptions: new(
				Names: [nameof(MyObjectVector.Category).ToLowerCamelCase()],
				Aggregates: [new(nameof(MyObjectVector.Category).ToLowerCamelCase(), Q.Aggregate.Count)]
			),
			group: [nameof(MyObjectVector.Category).ToLowerCamelCase()],
			hints: aggregationHints
		);

		totalRU += gravity5.RU;
		Console.WriteLine($"RU Used: {gravity5.RU:F2}");
		Console.WriteLine($"Results: {results5.Count}");
		Console.WriteLine();

		Console.WriteLine($"Total RU consumed: {totalRU:F2}");
		return totalRU;
	}
}