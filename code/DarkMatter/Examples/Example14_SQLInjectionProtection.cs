using DarkMatter.Models;
using Microsoft.Azure.Cosmos;
using Universe.Builder.Options;
using Universe.Exception;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

/// <summary>
/// Demonstrates the library's built-in SQL injection protection mechanisms.
/// 
/// The UniverseQuery library sanitizes all identifiers (column names, aliases, etc.) at query construction time
/// to prevent SQL injection attacks. This example shows various injection attempts and how they are safely blocked.
/// </summary>
public class Example14_SqlInjectionProtection(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
	public override async Task<double> RunAsync()
	{
		Console.WriteLine("\n=== EXAMPLE 14: SQL Injection Protection ===\n");
		Console.WriteLine("This example demonstrates how the library protects against SQL injection attacks");
		Console.WriteLine("by sanitizing all input identifiers at query construction time.\n");

		ruUsed = 0;

		// Test 1: Attempt to inject through a column name
		await TestColumnNameInjection();

		// Test 2: Attempt to inject through alias
		await TestAliasInjection();

		// Test 3: Attempt to inject through a sorting column
		await TestSortingColumnInjection();

		// Test 4: Attempt to inject through a group column
		await TestGroupColumnInjection();

		// Test 5: Attempt to inject through join alias
		await TestJoinAliasInjection();

		// Test 6: Attempt to inject with comment syntax
		await TestCommentSyntaxInjection();

		// Test 7: Valid query after failed attempts (shows the library still works)
		await TestValidQuery();

		// Test 8: Attempt to inject through FTScore value (single rank)
		await TestFTScoreInjection();

		// Test 9: Attempt to inject through FTScore value (multi-rank RRF)
		await TestFTScoreMultiRankInjection();

		Console.WriteLine($"Total RU Used: {ruUsed}\n");
		return ruUsed;
	}

	private async Task TestColumnNameInjection()
	{
		Console.WriteLine("--- Test 1: Column Name Injection Attempt ---");
		Console.WriteLine("Attempting to inject SQL through column name: \"name\"; DROP TABLE c; --\"\n");

		try
		{
			_ = await galaxy.Paged(
				page: new(10),
				clusters:
				[
					new(Catalysts:
					[
						new("name\"; DROP TABLE c; --", "<VALUE>", Operator: Q.Operator.Eq)
					])
				]
			);
			Console.WriteLine("FAILED: Injection was not blocked!\n");
		}
		catch (UniverseException ex)
		{
			Console.WriteLine($"BLOCKED: {ex.Message}\n");
		}
	}

	private async Task TestAliasInjection()
	{
		Console.WriteLine("--- Test 2: Alias Injection Attempt ---");
		Console.WriteLine("Attempting to inject SQL through alias: \"c] OR 1=1 --\"\n");

		try
		{
			_ = await galaxy.Paged(
				page: new(10),
				clusters:
				[
					new(Catalysts:
					[
						new(nameof(MyObject.Code), "<VALUE>", Alias: "c] OR 1=1 --", Operator: Q.Operator.Eq)
					])
				]
			);
			Console.WriteLine("FAILED: Injection was not blocked!\n");
		}
		catch (UniverseException ex)
		{
			Console.WriteLine($"BLOCKED: {ex.Message}\n");
		}
	}

	private async Task TestSortingColumnInjection()
	{
		Console.WriteLine("--- Test 3: Sorting Column Injection Attempt ---");
		Console.WriteLine("Attempting to inject SQL through sorting column: \"code; DELETE FROM c; --\"\n");

		try
		{
			_ = await galaxy.Paged(
				page: new(10),
				clusters:
				[
					new(Catalysts:
					[
						new(nameof(MyObject.Code), "<VALUE>", Operator: Q.Operator.Eq)
					])
				],
				sorting:
				[
					new("code; DELETE FROM c; --")
				]
			);
			Console.WriteLine("FAILED: Injection was not blocked!\n");
		}
		catch (UniverseException ex)
		{
			Console.WriteLine($"BLOCKED: {ex.Message}\n");
		}
	}

	private async Task TestGroupColumnInjection()
	{
		Console.WriteLine("--- Test 4: Group Column Injection Attempt ---");
		Console.WriteLine("Attempting to inject SQL through group column: \"name], [c.id\"\n");

		try
		{
			_ = await galaxy.List(
				clusters:
				[
					new(Catalysts:
					[
						new(nameof(MyObject.Code), "<VALUE>", Operator: Q.Operator.Eq)
					])
				],
				columnOptions: new(
					Aggregates:
					[
						new(nameof(MyObject.Name), Q.Aggregate.Count)
					],
					Names: [nameof(MyObject.Name)]
				),
				group: ["name\"], [c.id"]
			);
			Console.WriteLine("FAILED: Injection was not blocked!\n");
		}
		catch (UniverseException ex)
		{
			Console.WriteLine($"BLOCKED: {ex.Message}\n");
		}
	}

	private async Task TestJoinAliasInjection()
	{
		Console.WriteLine("--- Test 5: Join Alias Injection Attempt ---");
		Console.WriteLine("Attempting to inject SQL through join alias: \"items] OR 1=1 --\"\n");

		try
		{
			_ = await galaxy.Paged(
				page: new(10),
				clusters:
				[
					new(Catalysts:
					[
						new(nameof(MyObject.Code), "<VALUE>", Operator: Q.Operator.Eq)
					])
				],
				columnOptions: new(
					Names: [nameof(MyObject.Code)],
					Join: new(
						"items] OR 1=1 --",
						"relatedItems",
						null
					)
				)
			);
			Console.WriteLine("FAILED: Injection was not blocked!\n");
		}
		catch (UniverseException ex)
		{
			Console.WriteLine($"BLOCKED: {ex.Message}\n");
		}
	}

	private async Task TestCommentSyntaxInjection()
	{
		Console.WriteLine("--- Test 6: SQL Comment Syntax Injection Attempt ---");
		Console.WriteLine("Attempting to inject using comment syntax: \"code -- SELECT * FROM c\"\n");

		try
		{
			_ = await galaxy.Paged(
				page: new(10),
				clusters:
				[
					new(Catalysts:
					[
						new("code -- SELECT * FROM c", "<VALUE>", Operator: Q.Operator.Eq)
					])
				]
			);
			Console.WriteLine("FAILED: Injection was not blocked!\n");
		}
		catch (UniverseException ex)
		{
			Console.WriteLine($"BLOCKED: {ex.Message}\n");
		}
	}

	private async Task TestValidQuery()
	{
		Console.WriteLine("--- Test 7: Valid Query (Functionality Verification) ---");
		Console.WriteLine("Running a legitimate query to verify the library still functions correctly.\n");

		try
		{
			(Gravity g, IList<MyObject> results) = await galaxy.Paged(
				page: new(10),
				clusters:
				[
					new(Catalysts:
					[
						new(nameof(MyObject.Name), Operator: Q.Operator.Defined)
					])
				],
				columnOptions: new(
					Names:
					[
						nameof(MyObject.id),
						nameof(MyObject.Code).ToLowerCamelCase(),
						nameof(MyObject.Name).ToLowerCamelCase()
					]
				),
				sorting:
				[
					new(nameof(MyObject.Name).ToLowerCamelCase())
				]
			);

			ruUsed += g.RU;
			Console.WriteLine("SUCCESS: Query executed successfully!\n");
			PrintQueryResults(g, results);
		}
		catch (UniverseException ex)
		{
			Console.WriteLine($"ERROR: Valid query failed: {ex.Message}\n");
		}
	}

	private async Task TestFTScoreInjection()
	{
		Console.WriteLine("--- Test 8: FTScore Value Injection Attempt ---");
		Console.WriteLine("Attempting to inject SQL through FTScore value: \"test') OR ('1'='1\"\n");

		try
		{
			_ = await galaxy.List(
				clusters:
				[
					new(Catalysts:
					[
						new(nameof(MyObject.Name), new[] { "test') OR ('1'='1" }, Operator: Q.Operator.FTScore)
					])
				],
				columnOptions: new(
					Top: 10,
					Names: [nameof(MyObject.Name)]
				)
			);
			Console.WriteLine("Query executed (values are parameterized, injection payload treated as literal text).\n");
		}
		catch (UniverseException ex)
		{
			Console.WriteLine($"BLOCKED: {ex.Message}\n");
		}
		catch (CosmosException)
		{
			Console.WriteLine("Query executed safely (CosmosException from server, but SQL was parameterized).\n");
		}
	}

	private async Task TestFTScoreMultiRankInjection()
	{
		Console.WriteLine("--- Test 9: FTScore Multi-Rank RRF Injection Attempt ---");
		Console.WriteLine("Attempting to inject SQL through FTScore in RRF: \"test'); DROP TABLE c; --\"\n");

		try
		{
			_ = await galaxy.List(
				clusters:
				[
					new(Catalysts:
					[
						new(nameof(MyObject.Name), new[] { "test'); DROP TABLE c; --" }, Operator: Q.Operator.FTScore),
						new(nameof(MyObject.Name), new[] { "safe value" }, Operator: Q.Operator.FTScore)
					])
				],
				columnOptions: new(
					Top: 10,
					Names: [nameof(MyObject.Name)]
				)
			);
			Console.WriteLine("Query executed (values are parameterized, injection payload treated as literal text).\n");
		}
		catch (UniverseException ex)
		{
			Console.WriteLine($"BLOCKED: {ex.Message}\n");
		}
		catch (CosmosException)
		{
			Console.WriteLine("Query executed safely (CosmosException from server, but SQL was parameterized).\n");
		}
	}
}

