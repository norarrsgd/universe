using Universe.Response;
using DarkMatter.Models;
using Universe.Interfaces;
using Universe.Builder.Options;
using DarkMatter.Helpers;
using Universe.Extensions;

namespace DarkMatter.Examples;

/// <summary>
/// Example demonstrating VectorDistance search functionality in Cosmos DB
/// Shows single vector search, multi-vector search with RRF, hybrid search, and category-specific search
/// Uses the VectorDataGenerator to create sample data and predefined query vectors for realistic testing
/// </summary>
public class Example9_VectorSearch(IGalaxy<MyObjectVector> galaxy)
{
    private readonly IGalaxy<MyObjectVector> galaxy = galaxy;
    private double ruUsed = 0;

    public async Task<double> RunAsync()
    {
        Console.WriteLine("=== Vector Search Examples ===\n");

        // First, ensure we have sample data
        await EnsureSampleDataExists();

        // Example 1: Single Vector Search - Find similar products by description embedding
        await SingleVectorSearchExample();

        // Example 2: Multi-Vector Search with RRF - Search by both title and description embeddings
        await MultiVectorRRFExample();

        // Example 2b: Weighted Multi-Vector Search with RRF - Demonstrate custom weights
        await WeightedMultiVectorRRFExample();

        // Example 3: Hybrid Search - Combine vector search with traditional filtering
        await HybridVectorSearchExample();

        // Example 4: Furniture-specific Vector Search - Demonstrate category-specific search
        await FurnitureVectorSearchExample();

        Console.WriteLine($"\nTotal RU Used: {ruUsed:F2}");
        return ruUsed;
    }

    /// <summary>
    /// Ensures sample vector data exists in the database
    /// </summary>
    private async Task EnsureSampleDataExists()
    {
        Console.WriteLine("Ensuring sample vector data exists...");

        // Check if data already exists
        (Gravity checkGravity, IList<MyObjectVector> existingData) = await galaxy.Query()
            .Select(nameof(MyObjectVector.id))
            .Top(1)
            .ToListAsync();

        ruUsed += checkGravity.RU;

        if (existingData.Count == 0)
        {
            Console.WriteLine("No data found. Inserting sample vector data...");

            // Generate and insert sample data
            List<MyObjectVector> sampleData = VectorDataGenerator.GenerateSampleVectorData();

            Gravity insertGravity = await galaxy.Create(sampleData);
            ruUsed += insertGravity.RU;

            Console.WriteLine($"Inserted {sampleData.Count} sample items.\n");
        }
        else
        {
            Console.WriteLine("Sample data already exists.\n");
        }
    }

    /// <summary>
    /// Single vector search - finds products most similar to a query vector
    /// </summary>
    private async Task SingleVectorSearchExample()
    {
        Console.WriteLine("1. Single Vector Search Example");
        Console.WriteLine("Finding products similar to 'gaming laptop' using predefined query vector...\n");

        // Use the predefined gaming laptop query vector from VectorDataGenerator
        float[] queryEmbedding = VectorDataGenerator.SampleQueryVectors.GamingLaptopQuery;

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.Query()
            .Select(
                nameof(MyObjectVector.Code),
                nameof(MyObjectVector.Name),
                nameof(MyObjectVector.Description),
                nameof(MyObjectVector.Price))
            .Top(5) // Required for VectorDistance - get top 5 most similar
            .Cluster(c => c.VectorDistance(nameof(MyObjectVector.DescriptionEmbedding), queryEmbedding))
            .ToListAsync();

        ruUsed += gravity.RU;
        PrintVectorQueryResults(gravity, results, "Single Vector Search");
    }

    /// <summary>
    /// Multi-vector search using Reciprocal Rank Fusion (RRF)
    /// Combines similarity from multiple vector fields
    /// </summary>
    private async Task MultiVectorRRFExample()
    {
        Console.WriteLine("\n2a. Multi-Vector RRF Search Example");
        Console.WriteLine("Finding products using both title and description embeddings with RRF (business laptop query)...\n");

        // Use the predefined business laptop query vector for both title and description
        float[] titleEmbedding = VectorDataGenerator.SampleQueryVectors.BusinessLaptopQuery;
        float[] descriptionEmbedding = VectorDataGenerator.SampleQueryVectors.BusinessLaptopQuery;

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.Query()
            .Select(
                nameof(MyObjectVector.Code),
                nameof(MyObjectVector.Name),
                nameof(MyObjectVector.Category),
                nameof(MyObjectVector.Price))
            .Top(3) // Get top 3 results from RRF ranking
            .Cluster(c => c
                .VectorDistance(nameof(MyObjectVector.TitleEmbedding), titleEmbedding)
                .VectorDistance(nameof(MyObjectVector.DescriptionEmbedding), descriptionEmbedding))
            .ToListAsync();

        ruUsed += gravity.RU;
        PrintVectorQueryResults(gravity, results, "Multi-Vector RRF Search");
    }

    /// <summary>
    /// Multi-vector search using Reciprocal Rank Fusion (RRF) with custom weights
    /// Demonstrates how to weight different vector fields differently in the ranking
    /// </summary>
    private async Task WeightedMultiVectorRRFExample()
    {
        Console.WriteLine("\n2b. Weighted Multi-Vector RRF Search Example");
        Console.WriteLine("Finding products using weighted title and description embeddings (business laptop query)...\n");
        Console.WriteLine("Title weight: 0.8, Description weight: 1.2 (description has higher importance)\n");

        // Use the predefined business laptop query vector for both title and description
        float[] titleEmbedding = VectorDataGenerator.SampleQueryVectors.BusinessLaptopQuery;
        float[] descriptionEmbedding = VectorDataGenerator.SampleQueryVectors.BusinessLaptopQuery;

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.Query()
            .Select(
                nameof(MyObjectVector.Code).ToLowerCamelCase(),
                nameof(MyObjectVector.Name).ToLowerCamelCase(),
                nameof(MyObjectVector.Category).ToLowerCamelCase(),
                nameof(MyObjectVector.Price).ToLowerCamelCase())
            .Top(3) // Get top 3 results from weighted RRF ranking
            .Cluster(c => c
                .VectorDistance(nameof(MyObjectVector.TitleEmbedding).ToLowerCamelCase(), titleEmbedding)
                .VectorDistance(nameof(MyObjectVector.DescriptionEmbedding).ToLowerCamelCase(), descriptionEmbedding))
            .WithWeights("[1, 2]")
            .ToListAsync();

        ruUsed += gravity.RU;
        PrintVectorQueryResults(gravity, results, "Weighted Multi-Vector RRF Search");
    }

    /// <summary>
    /// Hybrid search - combines vector similarity with traditional filtering
    /// </summary>
    private async Task HybridVectorSearchExample()
    {
        Console.WriteLine("\n3. Hybrid Vector + Filter Search Example");
        Console.WriteLine("Finding affordable electronics using vector similarity...\n");

        // Use the predefined affordable electronics query vector
        float[] queryEmbedding = VectorDataGenerator.SampleQueryVectors.AffordableElectronicsQuery;

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.Query()
            .Select(
                nameof(MyObjectVector.Code).ToLowerCamelCase(),
                nameof(MyObjectVector.Name).ToLowerCamelCase(),
                nameof(MyObjectVector.Category).ToLowerCamelCase(),
                nameof(MyObjectVector.Price).ToLowerCamelCase(),
                nameof(MyObjectVector.Description).ToLowerCamelCase())
            .Top(4) // Get top 4 filtered and ranked results
            .Cluster(c => c.VectorDistance(nameof(MyObjectVector.DescriptionEmbedding).ToLowerCamelCase(), queryEmbedding))
            .Cluster(c => c
                .Eq(nameof(MyObjectVector.Category).ToLowerCamelCase(), "Electronics")
                .And().Lt(nameof(MyObjectVector.Price).ToLowerCamelCase(), 1000.0))
            .ToListAsync();

        ruUsed += gravity.RU;
        PrintVectorQueryResults(gravity, results, "Hybrid Vector + Filter Search");
    }

    /// <summary>
    /// Furniture-specific vector search - demonstrates category-specific vector queries
    /// </summary>
    private async Task FurnitureVectorSearchExample()
    {
        Console.WriteLine("\n4. Furniture Vector Search Example");
        Console.WriteLine("Finding furniture items using furniture-specific query vector...\n");

        // Use the predefined furniture query vector
        float[] queryEmbedding = VectorDataGenerator.SampleQueryVectors.FurnitureQuery;

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.Query()
            .Select(
                nameof(MyObjectVector.Code).ToLowerCamelCase(),
                nameof(MyObjectVector.Name).ToLowerCamelCase(),
                nameof(MyObjectVector.Category).ToLowerCamelCase(),
                nameof(MyObjectVector.Price).ToLowerCamelCase(),
                nameof(MyObjectVector.Description).ToLowerCamelCase())
            .Top(3) // Get top 3 furniture-related results
            .Cluster(c => c.VectorDistance(nameof(MyObjectVector.DescriptionEmbedding).ToLowerCamelCase(), queryEmbedding))
            .ToListAsync();

        ruUsed += gravity.RU;
        PrintVectorQueryResults(gravity, results, "Furniture Vector Search");
    }

    /// <summary>
    /// Enhanced result printer for vector search results
    /// </summary>
    private void PrintVectorQueryResults<T>(Gravity gravity, IList<T> results, string searchType) where T : MyObjectVector
    {
        Console.WriteLine($"=== {searchType} Results ===");
        Console.WriteLine($"RU Spent: {gravity.RU:F2}");

        // Display generated query
        if (gravity.Query != default)
        {
            Console.WriteLine($"Generated Query: {gravity.Query.Text}");
            Console.WriteLine("Parameters:");
            foreach ((string name, object value) in gravity.Query.Parameters)
            {
                if (value is float[] vector)
                    Console.WriteLine($"  {name} = [{string.Join(", ", vector.Take(4))}...] (vector of {vector.Length} dimensions)");
                else
                    Console.WriteLine($"  {name} = {value}");
            }
        }

        Console.WriteLine($"Results Found: {results.Count}");

        // Show detailed results
        for (int i = 0; i < results.Count; i++)
        {
            T item = results[i];
            Console.WriteLine($"\n  Result {i + 1}:");
            Console.WriteLine($"    Code: {item.Code}");
            Console.WriteLine($"    Name: {item.Name}");
            Console.WriteLine($"    Category: {item.Category}");
            Console.WriteLine($"    Price: ${item.Price:F2}");
            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                string desc = item.Description.Length > 60
                    ? $"{item.Description[..60]}..."
                    : item.Description;
                Console.WriteLine($"    Description: {desc}");
            }

            // show the scores
            if (item.TitleEmbeddingScore != 0.0f)
                Console.WriteLine($"    Title Similarity Score: {item.TitleEmbeddingScore:F4}");
            if (item.DescriptionEmbeddingScore != 0.0f)
                Console.WriteLine($"    Description Similarity Score: {item.DescriptionEmbeddingScore:F4}");
            if (item.CombinedEmbeddingScore != 0.0f)
                Console.WriteLine($"    Combined Similarity Score: {item.CombinedEmbeddingScore:F4}");

            // Note: Vector similarity scores would be included in the SELECT if we included them in column names
        }

        Console.WriteLine();
    }
}
