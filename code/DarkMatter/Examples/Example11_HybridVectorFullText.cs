using Universe.Response;
using DarkMatter.Models;
using Universe.Interfaces;
using Universe.Builder.Options;
using DarkMatter.Helpers;
using Universe.Extensions;

namespace DarkMatter.Examples;

/// <summary>
/// Example demonstrating Hybrid Vector + Full-Text Search functionality
/// Shows how to combine VectorDistance with FTScore using RRF (Reciprocal Rank Fusion)
/// Demonstrates semantic search (vectors) combined with lexical search (full-text) for optimal results
/// This provides the best of both worlds: semantic understanding and exact text matching
/// </summary>
public class Example11_HybridVectorFullText(IGalaxy<MyObjectVector> galaxy)
{
    private readonly IGalaxy<MyObjectVector> galaxy = galaxy;
    private double ruUsed = 0;

    public async Task<double> RunAsync()
    {
        Console.WriteLine("=== Hybrid Vector + Full-Text Search Examples ===\n");

        // First, ensure we have sample data with both vectors and rich text
        await EnsureSampleDataExists();

        // Example 1: Basic Hybrid Search - Vector + Single Full-Text
        await BasicHybridVectorFullTextExample();

        // Example 2: Multi-Field Hybrid Search - Vector + Multiple Full-Text fields with RRF
        await MultiFieldHybridSearchExample();

        // Example 3: Weighted Hybrid Search - Custom weights for Vector vs Full-Text
        await WeightedHybridSearchExample();

        // Example 4: Complex Hybrid Search - Vector + Full-Text + Traditional Filters
        await ComplexHybridSearchExample();

        // Example 5: Category-Specific Hybrid Search
        await CategorySpecificHybridSearchExample();

        Console.WriteLine($"\nTotal RU Used: {ruUsed:F2}");
        return ruUsed;
    }

    /// <summary>
    /// Ensures sample vector data with rich text content exists in the database
    /// </summary>
    private async Task EnsureSampleDataExists()
    {
        Console.WriteLine("Ensuring sample vector data with rich text content exists...");

        // Check if data already exists
        (Gravity checkGravity, IList<MyObjectVector> existingData) = await galaxy.List(
            clusters: [],
            columnOptions: new(Names: [nameof(MyObjectVector.id).ToLowerCamelCase()], Top: 1)
        );

        ruUsed += checkGravity.RU;

        if (existingData.Count == 0)
        {
            Console.WriteLine("No data found. Inserting sample vector + text data...");

            // Generate and insert sample data with both vectors and rich text
            List<MyObjectVector> sampleData = VectorDataGenerator.GenerateSampleVectorData();

            Gravity insertGravity = await galaxy.Create(sampleData);
            ruUsed += insertGravity.RU;

            Console.WriteLine($"Inserted {sampleData.Count} sample items with vectors and rich text.\n");
        }
        else
        {
            Console.WriteLine("Sample data already exists.\n");
        }
    }

    /// <summary>
    /// Example 1: Basic Hybrid Search - Vector + Single Full-Text
    /// Combines semantic similarity (vector) with exact text matching (full-text) using RRF
    /// </summary>
    private async Task BasicHybridVectorFullTextExample()
    {
        Console.WriteLine("1. Basic Hybrid Vector + Full-Text Search");
        Console.WriteLine("Combining vector similarity for 'gaming laptop' with full-text search for 'machine learning'...\n");

        // Use predefined gaming laptop query vector
        float[] queryVector = VectorDataGenerator.SampleQueryVectors.GamingLaptopQuery;

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
					// Semantic similarity via vector
					new(nameof(MyObjectVector.DescriptionEmbedding).ToLowerCamelCase(), queryVector, Operator: Q.Operator.VectorDistance),
					// Lexical matching via full-text
					new(nameof(MyObjectVector.Name).ToLowerCamelCase(), new[] { "machine", "learning" }, Operator: Q.Operator.FTScore)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObjectVector.Code).ToLowerCamelCase(), nameof(MyObjectVector.Name).ToLowerCamelCase(), nameof(MyObjectVector.Description).ToLowerCamelCase()], Top: 10)
        );

        ruUsed += gravity.RU;
        PrintHybridQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 2: Multi-Field Hybrid Search - Vector + Multiple Full-Text fields with RRF
    /// Demonstrates combining vector similarity with multiple text fields for comprehensive search
    /// </summary>
    private async Task MultiFieldHybridSearchExample()
    {
        Console.WriteLine("2. Multi-Field Hybrid Search with RRF");
        Console.WriteLine("Vector similarity + full-text on name + full-text on description, all combined with RRF...\n");

        float[] queryVector = VectorDataGenerator.SampleQueryVectors.AffordableElectronicsQuery;

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
					// Semantic similarity
					new(nameof(MyObjectVector.TitleEmbedding).ToLowerCamelCase(), queryVector, Operator: Q.Operator.VectorDistance),
					// Lexical matching on multiple fields
					new(nameof(MyObjectVector.Name).ToLowerCamelCase(), new[] { "artificial", "intelligence" }, Operator: Q.Operator.FTScore),
                    new(nameof(MyObjectVector.Description).ToLowerCamelCase(), new[] { "neural", "networks", "deep", "learning" }, Operator: Q.Operator.FTScore)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObjectVector.Code).ToLowerCamelCase(), nameof(MyObjectVector.Name).ToLowerCamelCase(), nameof(MyObjectVector.Description).ToLowerCamelCase()], Top: 10)
        );

        ruUsed += gravity.RU;
        PrintHybridQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 3: Weighted Hybrid Search - Custom weights for Vector vs Full-Text
    /// Demonstrates how to balance semantic vs lexical search importance
    /// </summary>
    private async Task WeightedHybridSearchExample()
    {
        Console.WriteLine("3. Weighted Hybrid Search");
        Console.WriteLine("60% weight on vector similarity, 40% weight on full-text relevance...\n");

        float[] queryVector = VectorDataGenerator.SampleQueryVectors.GamingLaptopQuery;

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObjectVector.DescriptionEmbedding).ToLowerCamelCase(), queryVector, Operator: Q.Operator.VectorDistance),
                    new(nameof(MyObjectVector.Name).ToLowerCamelCase(), new[] { "gaming", "performance" }, Operator: Q.Operator.FTScore)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObjectVector.Code).ToLowerCamelCase(), nameof(MyObjectVector.Name).ToLowerCamelCase(), nameof(MyObjectVector.Price).ToLowerCamelCase()], Top: 10),
            sorting:
            [
                new(Column: "[0.6, 0.4]", Direction: Sorting.Direction.WEIGHTED) // 60% vector, 40% text
			]
        );

        ruUsed += gravity.RU;
        PrintHybridQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 4: Complex Hybrid Search - Vector + Full-Text + Traditional Filters
    /// Demonstrates the most comprehensive search combining all three approaches
    /// </summary>
    private async Task ComplexHybridSearchExample()
    {
        Console.WriteLine("4. Complex Hybrid Search - Vector + Full-Text + Traditional Filters");
        Console.WriteLine("Semantic search + text search + price filter + category filter...\n");

        float[] queryVector = VectorDataGenerator.SampleQueryVectors.AffordableElectronicsQuery;

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.List(
            clusters:
            [
				// Hybrid ranking cluster (Vector + Full-Text)
				new(Catalysts:
                [
                    new(nameof(MyObjectVector.DescriptionEmbedding).ToLowerCamelCase(), queryVector, Operator: Q.Operator.VectorDistance),
                    new(nameof(MyObjectVector.Description).ToLowerCamelCase(), new[] { "technology", "innovation" }, Operator: Q.Operator.FTScore)
                ]),
				// Traditional filter cluster
				new(Where: Q.Where.And, Catalysts:
                [
                    new(nameof(MyObjectVector.Category).ToLowerCamelCase(), "Electronics", Operator: Q.Operator.Eq),
                    new(nameof(MyObjectVector.Price).ToLowerCamelCase(), 2000.0, Operator: Q.Operator.Lt)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObjectVector.Code).ToLowerCamelCase(), nameof(MyObjectVector.Name).ToLowerCamelCase(), nameof(MyObjectVector.Category).ToLowerCamelCase(), nameof(MyObjectVector.Price).ToLowerCamelCase()], Top: 10)
        );

        ruUsed += gravity.RU;
        PrintHybridQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 5: Category-Specific Hybrid Search
    /// Demonstrates hybrid search within specific product categories
    /// </summary>
    private async Task CategorySpecificHybridSearchExample()
    {
        Console.WriteLine("5. Category-Specific Hybrid Search");
        Console.WriteLine("Hybrid search within Electronics category for laptop-related products...\n");

        float[] queryVector = VectorDataGenerator.SampleQueryVectors.GamingLaptopQuery;

        (Gravity gravity, IList<MyObjectVector> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
					// Category filter first
					new(nameof(MyObjectVector.Category).ToLowerCamelCase(), "Electronics", Operator: Q.Operator.Eq),
					// Then hybrid search
					new(nameof(MyObjectVector.TitleEmbedding).ToLowerCamelCase(), queryVector, Operator: Q.Operator.VectorDistance, Where: Q.Where.And),
                    new(nameof(MyObjectVector.Name).ToLowerCamelCase(), new[] { "laptop", "computer", "gaming" }, Operator: Q.Operator.FTScore, Where: Q.Where.And)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObjectVector.Code).ToLowerCamelCase(), nameof(MyObjectVector.Name).ToLowerCamelCase(), nameof(MyObjectVector.Category).ToLowerCamelCase(), nameof(MyObjectVector.Price).ToLowerCamelCase()], Top: 8)
        );

        ruUsed += gravity.RU;
        PrintHybridQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Helper method to print hybrid query results with additional vector score information
    /// </summary>
    private void PrintHybridQueryResults(Gravity gravity, IList<MyObjectVector> results)
    {
        Console.WriteLine($"RU Spent: {gravity.RU}");

        // Display query information if available
        if (gravity.Query != default)
        {
            Console.WriteLine($"Query: {gravity.Query.Text}");
            foreach ((string, object) p in gravity.Query.Parameters)
            {
                string valueDisplay = p.Item2 switch
                {
                    float[] floatArray => $"[vector with {floatArray.Length} dimensions]",
                    string[] strArray => $"[{string.Join(", ", strArray)}]",
                    _ => p.Item2?.ToString() ?? "null"
                };
                Console.WriteLine($"  Parameter: {p.Item1} = {valueDisplay}");
            }
        }

        Console.WriteLine($"Result Count: {results.Count}");

        // Print first few results if any
        int displayCount = Math.Min(results.Count, 3);
        for (int i = 0; i < displayCount; i++)
        {
            MyObjectVector item = results[i];
            Console.WriteLine($"  Item {i + 1}: {item.Code} - {item.Name}");
            Console.WriteLine($"    Category: {item.Category}, Price: ${item.Price:F2}");

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                string desc = item.Description.Length > 80
                    ? $"{item.Description[..80]}..."
                    : item.Description;
                Console.WriteLine($"    Description: {desc}");
            }

            // Show vector scores if available (these would be populated by the query results)
            if (item.TitleEmbeddingScore > 0)
                Console.WriteLine($"    Title Vector Score: {item.TitleEmbeddingScore:F4}");
            if (item.DescriptionEmbeddingScore > 0)
                Console.WriteLine($"    Description Vector Score: {item.DescriptionEmbeddingScore:F4}");
        }
    }
}