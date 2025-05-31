// filepath: d:\github\universe\code\DarkMatter\Program.cs
using Microsoft.Azure.Cosmos;
using Universe.Builder.Options;
using DarkMatter.Repository;
using DarkMatter.Examples;

namespace DarkMatter;

class Program
{
    static async Task Main(string[] args)
    {
        // Database connection details
        string CosmosDbUri = "<FROM AZURE COSMOS DB ACCOUNT>";
        string CosmosDbPrimaryKey = "<FROM AZURE COSMOS DB ACCOUNT KEY>";

        // Initialize CosmosClient with appropriate options
        CosmosClient cosmosClient = new(
            CosmosDbUri,
            CosmosDbPrimaryKey,
            clientOptions: new()
            {
                Serializer = new UniverseSerializer(),
                AllowBulkExecution = true
            }
        );

        // Create the repository instance
        MyRepo galaxy = new(
            client: cosmosClient,
            database: "test-database",
            container: "my-container",
            partitionKey: "/Code"
        );

        // Track the total request units spent
        double totalRu = 0.0;

        // Run all examples sequentially
        totalRu += await new Example1_BasicPagedQuery(galaxy).RunAsync();
        totalRu += await new Example2_AggregatesWithGroupBy(galaxy).RunAsync();
        totalRu += await new Example3_TopAndDistinct(galaxy).RunAsync();
        totalRu += await new Example4_ComplexFiltering(galaxy).RunAsync();
        totalRu += await new Example5_SingleItemOperations(galaxy).RunAsync();
        totalRu += await new Example6_BulkOperations(galaxy).RunAsync();
        totalRu += await new Example7_AdvancedAggregation(galaxy).RunAsync();
        totalRu += await new Example8_SalesAnalysis(galaxy).RunAsync();

        // Display summary information
        Console.WriteLine($"\nTotal RU spent across all examples: {totalRu}");
        Console.WriteLine("\nExamples complete. Press Enter to exit.");
        Console.ReadLine();
    }
}