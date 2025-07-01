using Universe.Response;
using DarkMatter.Models;
using Universe.Interfaces;
using Universe.Builder.Options;

namespace DarkMatter.Examples;

/// <summary>
/// Example demonstrating VectorDistance search functionality in Cosmos DB
/// Shows single vector search, multi-vector search with RRF, and hybrid search
/// </summary>
public class Example9_VectorSearch(IGalaxy<MyObjectVector> galaxy)
{
    private readonly IGalaxy<MyObjectVector> galaxy = galaxy;
    private double ruUsed = 0;

    public async Task<double> RunAsync()
    {
        Console.WriteLine("=== Vector Search Examples ===\n");

        // Example 1: Single Vector Search - Find similar products by description embedding
        await SingleVectorSearchExample();

        // Example 2: Multi-Vector Search with RRF - Search by both title and description embeddings
        await MultiVectorRRFExample();

        // Example 3: Hybrid Search - Combine vector search with traditional filtering
        await HybridVectorSearchExample();

        Console.WriteLine($"\nTotal RU Used: {ruUsed:F2}");
        return ruUsed;
    }

    /// <summary>
    /// Single vector search - finds products most similar to a query vector
    /// </summary>
    private async Task SingleVectorSearchExample()
    {
        Console.WriteLine("1. Single Vector Search Example");
        Console.WriteLine("Finding products similar to 'gaming laptop' based on description embeddings...\n");

        // Simulated embedding for "gaming laptop" query
        float[] queryEmbedding = [0.1f, 0.8f, 0.3f, 0.9f, 0.2f, 0.7f, 0.4f, 0.6f];

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.List(
            clusters: [
                new(Catalysts: [
                    new(nameof(MyObjectVector.DescriptionEmbedding), queryEmbedding, Operator: Q.Operator.VectorDistance)
                ])
            ],
            columnOptions: new(
                Names: [
                    nameof(MyObjectVector.Code),
                    nameof(MyObjectVector.Name),
                    nameof(MyObjectVector.Description),
                    nameof(MyObjectVector.Price)
                ],
                Top: 5 // Required for VectorDistance - get top 5 most similar
            )
        );

        ruUsed += gravity.RU;
        PrintVectorQueryResults(gravity, results, "Single Vector Search");
    }

    /// <summary>
    /// Multi-vector search using Reciprocal Rank Fusion (RRF)
    /// Combines similarity from multiple vector fields
    /// </summary>
    private async Task MultiVectorRRFExample()
    {
        Console.WriteLine("\n2. Multi-Vector RRF Search Example");
        Console.WriteLine("Finding products using both title and description embeddings with RRF...\n");

        // Different embeddings for title vs description
        float[] titleEmbedding = [0.2f, 0.9f, 0.1f, 0.8f, 0.3f, 0.7f, 0.5f, 0.4f];
        float[] descriptionEmbedding = [0.3f, 0.7f, 0.2f, 0.9f, 0.4f, 0.6f, 0.8f, 0.1f];

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.List(
            clusters: [
                new(Catalysts: [
                    new(nameof(MyObjectVector.TitleEmbedding), titleEmbedding, Operator: Q.Operator.VectorDistance),
                    new(nameof(MyObjectVector.DescriptionEmbedding), descriptionEmbedding, Operator: Q.Operator.VectorDistance)
                ])
            ],
            columnOptions: new(
                Names: [
                    nameof(MyObjectVector.Code),
                    nameof(MyObjectVector.Name),
                    nameof(MyObjectVector.Category),
                    nameof(MyObjectVector.Price)
                ],
                Top: 3 // Get top 3 results from RRF ranking
            )
        );

        ruUsed += gravity.RU;
        PrintVectorQueryResults(gravity, results, "Multi-Vector RRF Search");
    }

    /// <summary>
    /// Hybrid search - combines vector similarity with traditional filtering
    /// </summary>
    private async Task HybridVectorSearchExample()
    {
        Console.WriteLine("\n3. Hybrid Vector + Filter Search Example");
        Console.WriteLine("Finding similar electronics under $1000...\n");

        float[] queryEmbedding = [0.4f, 0.6f, 0.8f, 0.2f, 0.9f, 0.1f, 0.7f, 0.3f];

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.List(
            clusters: [
                // First cluster: Vector similarity
                new(Catalysts: [
                    new(nameof(MyObjectVector.DescriptionEmbedding), queryEmbedding, Operator: Q.Operator.VectorDistance)
                ]),
                // Second cluster: Traditional filters (AND logic)
                new(
                    Where: Q.Where.And,
                    Catalysts: [
                        new(nameof(MyObjectVector.Category), "Electronics", Operator: Q.Operator.Eq),
                        new(nameof(MyObjectVector.Price), 1000.0, Operator: Q.Operator.Lt)
                    ]
                )
            ],
            columnOptions: new(
                Names: [
                    nameof(MyObjectVector.Code),
                    nameof(MyObjectVector.Name),
                    nameof(MyObjectVector.Category),
                    nameof(MyObjectVector.Price),
                    nameof(MyObjectVector.Description)
                ],
                Top: 4 // Get top 4 filtered and ranked results
            )
        );

        ruUsed += gravity.RU;
        PrintVectorQueryResults(gravity, results, "Hybrid Vector + Filter Search");
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
            Console.WriteLine($"    Description: {item.Description?[..Math.Min(item.Description.Length, 60)]}...");

            // Note: Vector similarity scores would be included in the SELECT if we included them in column names
        }

        Console.WriteLine();
    }
}
