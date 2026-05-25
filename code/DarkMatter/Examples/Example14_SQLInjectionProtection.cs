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

        // Test 10: Attempt to inject through WithWeights weight value
        await TestWeightValueInjection();

        Console.WriteLine($"Total RU Used: {ruUsed}\n");
        return ruUsed;
    }

    private async Task TestColumnNameInjection()
    {
        Console.WriteLine("--- Test 1: Column Name Injection Attempt ---");
        Console.WriteLine("Attempting to inject SQL through column name: \"name\"; DROP TABLE c; --\"\n");

        try
        {
            _ = await galaxy.Query()
                .Paged(10)
                .Cluster(c => c.Eq("name\"; DROP TABLE c; --", "<VALUE>"))
                .ToListAsync();
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
            _ = await galaxy.Query()
                .Paged(10)
                .Cluster(c => c.Eq(nameof(MyObject.Code), "<VALUE>", alias: "c] OR 1=1 --"))
                .ToListAsync();
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
            _ = await galaxy.Query()
                .Paged(10)
                .Cluster(c => c.Eq(nameof(MyObject.Code), "<VALUE>"))
                .OrderBy("code; DELETE FROM c; --")
                .ToListAsync();
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
            _ = await galaxy.Query()
                .Select(nameof(MyObject.Name))
                .Aggregate(nameof(MyObject.Name), Q.Aggregate.Count)
                .GroupBy("name\"], [c.id")
                .Cluster(c => c.Eq(nameof(MyObject.Code), "<VALUE>"))
                .ToListAsync();
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
            _ = await galaxy.Query()
                .Select(nameof(MyObject.Code))
                .Paged(10)
                .Join(arrayPath: "relatedItems", alias: "items] OR 1=1 --")
                .Cluster(c => c.Eq(nameof(MyObject.Code), "<VALUE>"))
                .ToListAsync();
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
            _ = await galaxy.Query()
                .Paged(10)
                .Cluster(c => c.Eq("code -- SELECT * FROM c", "<VALUE>"))
                .ToListAsync();
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
            (Gravity g, IList<MyObject> results) = await galaxy.Query()
                .Select(
                    nameof(MyObject.id),
                    nameof(MyObject.Code).ToLowerCamelCase(),
                    nameof(MyObject.Name).ToLowerCamelCase())
                .Paged(10)
                .Cluster(c => c.Defined(nameof(MyObject.Name)))
                .OrderBy(nameof(MyObject.Name).ToLowerCamelCase())
                .ToListAsync();

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
            _ = await galaxy.Query()
                .Select(nameof(MyObject.Name))
                .Top(10)
                .Cluster(c => c.FTScore(nameof(MyObject.Name), ["test') OR ('1'='1"]))
                .ToListAsync();
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

    private async Task TestWeightValueInjection()
    {
        Console.WriteLine("--- Test 10: Weight Value Injection Attempt ---");
        Console.WriteLine("Attempting to inject SQL through weight value: \"0.7], COUNT(*) AS total --\"\n");

        try
        {
            _ = await galaxy.Query()
                .Select(nameof(MyObject.Name))
                .Top(10)
                .Cluster(c => c
                    .FTScore(nameof(MyObject.Name), ["test"])
                    .FTScore(nameof(MyObject.Name), ["test2"]))
                .WithWeights("0.7], COUNT(*) AS total --")
                .ToListAsync();
            Console.WriteLine("FAILED: Injection was not blocked!\n");
        }
        catch (UniverseException ex)
        {
            Console.WriteLine($"BLOCKED: {ex.Message}\n");
        }
    }

    private async Task TestFTScoreMultiRankInjection()
    {
        Console.WriteLine("--- Test 9: FTScore Multi-Rank RRF Injection Attempt ---");
        Console.WriteLine("Attempting to inject SQL through FTScore in RRF: \"test'); DROP TABLE c; --\"\n");

        try
        {
            _ = await galaxy.Query()
                .Select(nameof(MyObject.Name))
                .Top(10)
                .Cluster(c => c
                    .FTScore(nameof(MyObject.Name), ["test'); DROP TABLE c; --"])
                    .FTScore(nameof(MyObject.Name), ["safe value"]))
                .ToListAsync();
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
