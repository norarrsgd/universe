using Universe.Response;
using DarkMatter.Models;
using Universe.Interfaces;
using Universe.Builder.Options;
using Universe.Extensions;

namespace DarkMatter.Examples;

/// <summary>
/// Example demonstrating Full-Text Search functionality in Cosmos DB
/// Shows basic text search, relevance scoring, multi-field search with RRF, 
/// hybrid search combining text and traditional filters, negated searches, and aggregation
/// Uses realistic product data for demonstration
/// </summary>
public class Example10_FullTextSearch(IGalaxy<MyObject> galaxy)
{
    private readonly IGalaxy<MyObject> galaxy = galaxy;
    private double ruUsed = 0;

    public async Task<double> RunAsync()
    {
        Console.WriteLine("=== Full-Text Search Examples ===\n");

        // First, ensure we have sample data with rich text content
        await EnsureSampleDataExists();

        // Example 1: Basic Full-Text Contains Search
        await BasicFullTextContainsExample();

        // Example 2: Full-Text Search with Relevance Scoring
        await FullTextScoringExample();

        // Example 3: Multi-Field Full-Text Search with RRF
        await MultiFieldFullTextRRFExample();

        // Example 4: Weighted Multi-Field Full-Text Search
        await WeightedMultiFieldFullTextExample();

        // Example 5: Full-Text Contains All Terms
        await FullTextContainsAllExample();

        // Example 6: Full-Text Contains Any Terms
        await FullTextContainsAnyExample();

        // Example 7: Negated Full-Text Search
        await NegatedFullTextSearchExample();

        // Example 8: Hybrid Search - Full-Text + Traditional Filters
        await HybridFullTextSearchExample();

        // Example 9: Complex Multi-Cluster Full-Text Search
        await ComplexMultiClusterFullTextExample();

        // Example 10: Full-Text Search with Aggregation
        await FullTextSearchWithAggregationExample();

        // Example 11: Boolean Full-Text Logic
        await BooleanFullTextLogicExample();

        Console.WriteLine($"\nTotal RU Used: {ruUsed:F2}");
        return ruUsed;
    }

    /// <summary>
    /// Ensures sample data with rich text content exists in the database
    /// </summary>
    private async Task EnsureSampleDataExists()
    {
        Console.WriteLine("Ensuring sample data with rich text content exists...");

        // Check if data already exists
        (Gravity checkGravity, IList<MyObject> existingData) = await galaxy.List(
            clusters: [],
            columnOptions: new(Names: [nameof(MyObject.id).ToLowerCamelCase()], Top: 1)
        );

        ruUsed += checkGravity.RU;

        if (existingData.Count == 0)
        {
            Console.WriteLine("No data found. Inserting sample text-rich data...");

            // Generate and insert sample data with rich text content
            List<MyObject> sampleData = GenerateTextRichSampleData();

            Gravity insertGravity = await galaxy.Create(sampleData);
            ruUsed += insertGravity.RU;

            Console.WriteLine($"Inserted {sampleData.Count} sample items with rich text content.\n");
        }
        else
        {
            Console.WriteLine("Sample data already exists.\n");
        }
    }

    /// <summary>
    /// Example 1: Basic Full-Text Contains Search
    /// Demonstrates simple text matching in a specific field
    /// </summary>
    private async Task BasicFullTextContainsExample()
    {
        Console.WriteLine("1. Basic Full-Text Contains Search");
        Console.WriteLine("Searching for products with 'machine learning' in the name...\n");

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Name).ToLowerCamelCase(), "machine learning", Operator: Q.Operator.FTContains)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.Code).ToLowerCamelCase(), nameof(MyObject.Name).ToLowerCamelCase(), nameof(MyObject.Description).ToLowerCamelCase()], Top: 10)
        );

        ruUsed += gravity.RU;
        PrintQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 2: Full-Text Search with Relevance Scoring
    /// Demonstrates ranking results by text relevance
    /// </summary>
    private async Task FullTextScoringExample()
    {
        Console.WriteLine("2. Full-Text Search with Relevance Scoring");
        Console.WriteLine("Ranking products by relevance to 'artificial intelligence'...\n");

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Description).ToLowerCamelCase(), new[] { "artificial", "intelligence" }, Operator: Q.Operator.FTScore)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.Code).ToLowerCamelCase(), nameof(MyObject.Name).ToLowerCamelCase(), nameof(MyObject.Description).ToLowerCamelCase()], Top: 5)
        );

        ruUsed += gravity.RU;
        PrintQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 3: Multi-Field Full-Text Search with RRF
    /// Demonstrates searching across multiple fields with Reciprocal Rank Fusion
    /// </summary>
    private async Task MultiFieldFullTextRRFExample()
    {
        Console.WriteLine("3. Multi-Field Full-Text Search with RRF");
        Console.WriteLine("Searching 'machine learning' in name and 'neural networks' in description with RRF...\n");

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Name).ToLowerCamelCase(), new[] { "machine", "learning" }, Operator: Q.Operator.FTScore),
                    new(nameof(MyObject.Description).ToLowerCamelCase(), new[] { "neural", "networks" }, Operator: Q.Operator.FTScore)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.Code).ToLowerCamelCase(), nameof(MyObject.Name).ToLowerCamelCase(), nameof(MyObject.Description).ToLowerCamelCase()], Top: 10)
        );

        ruUsed += gravity.RU;
        PrintQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 4: Weighted Multi-Field Full-Text Search
    /// Demonstrates custom weighting of different text fields
    /// </summary>
    private async Task WeightedMultiFieldFullTextExample()
    {
        Console.WriteLine("4. Weighted Multi-Field Full-Text Search");
        Console.WriteLine("Searching with 80% weight on name, 20% weight on description...\n");

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Name).ToLowerCamelCase(), new[] { "machine", "learning" }, Operator: Q.Operator.FTScore),
                    new(nameof(MyObject.Description).ToLowerCamelCase(), new[] { "deep", "learning" }, Operator: Q.Operator.FTScore)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.Code).ToLowerCamelCase(), nameof(MyObject.Name).ToLowerCamelCase(), nameof(MyObject.Description).ToLowerCamelCase()], Top: 10),
            sorting:
            [
                new(Column: "[0.8, 0.2]", Direction: Sorting.Direction.WEIGHTED)
            ]
        );

        ruUsed += gravity.RU;
        PrintQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 5: Full-Text Contains All Terms
    /// Demonstrates requiring ALL specified terms to be present
    /// </summary>
    private async Task FullTextContainsAllExample()
    {
        Console.WriteLine("5. Full-Text Contains All Terms");
        Console.WriteLine("Finding products where description contains ALL terms: machine, learning, algorithms...\n");

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Description).ToLowerCamelCase(), new[] { "machine", "learning", "algorithms" }, Operator: Q.Operator.FTContainsAll)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.Code).ToLowerCamelCase(), nameof(MyObject.Name).ToLowerCamelCase(), nameof(MyObject.Description).ToLowerCamelCase()], Top: 10)
        );

        ruUsed += gravity.RU;
        PrintQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 6: Full-Text Contains Any Terms
    /// Demonstrates matching ANY of the specified terms
    /// </summary>
    private async Task FullTextContainsAnyExample()
    {
        Console.WriteLine("6. Full-Text Contains Any Terms");
        Console.WriteLine("Finding products in Electronics category with ANY of: AI, ML, DL, NLP...\n");

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Category).ToLowerCamelCase(), "Electronics", Operator: Q.Operator.Eq),
                    new(nameof(MyObject.Description).ToLowerCamelCase(), new[] { "AI", "ML", "DL", "NLP" }, Operator: Q.Operator.FTContainsAny, Where: Q.Where.And)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.Code).ToLowerCamelCase(), nameof(MyObject.Name).ToLowerCamelCase(), nameof(MyObject.Category).ToLowerCamelCase()], Top: 15)
        );

        ruUsed += gravity.RU;
        PrintQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 7: Negated Full-Text Search
    /// Demonstrates excluding documents containing specific terms
    /// </summary>
    private async Task NegatedFullTextSearchExample()
    {
        Console.WriteLine("7. Negated Full-Text Search");
        Console.WriteLine("Finding products NOT containing 'deprecated' in description and NOT containing 'obsolete' or 'legacy' in name...\n");

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Description).ToLowerCamelCase(), "deprecated", Operator: Q.Operator.NotFTContains),
                    new(nameof(MyObject.Name).ToLowerCamelCase(), new[] { "obsolete", "legacy" }, Operator: Q.Operator.NotFTContainsAny, Where: Q.Where.And)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.Code).ToLowerCamelCase(), nameof(MyObject.Name).ToLowerCamelCase(), nameof(MyObject.Description).ToLowerCamelCase()], Top: 10)
        );

        ruUsed += gravity.RU;
        PrintQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 8: Hybrid Search - Full-Text + Traditional Filters
    /// Demonstrates combining full-text search with regular filters
    /// </summary>
    private async Task HybridFullTextSearchExample()
    {
        Console.WriteLine("8. Hybrid Search: Full-Text + Traditional Filters");
        Console.WriteLine("Full-text search for 'machine learning' with price filter and category filter...\n");

        DateTime thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
				// Full-text search cluster
				new(Catalysts:
                [
                    new(nameof(MyObject.Description).ToLowerCamelCase(), new[] { "machine", "learning" }, Operator: Q.Operator.FTScore)
                ]),
				// Traditional filter cluster
				new(Where: Q.Where.And, Catalysts:
                [
                    new(nameof(MyObject.Category).ToLowerCamelCase(), "Electronics", Operator: Q.Operator.Eq),
                    new(nameof(MyObject.Price).ToLowerCamelCase(), 1000.0, Operator: Q.Operator.Lt)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.Code).ToLowerCamelCase(), nameof(MyObject.Name).ToLowerCamelCase(), nameof(MyObject.Category).ToLowerCamelCase(), nameof(MyObject.Price).ToLowerCamelCase()], Top: 10)
        );

        ruUsed += gravity.RU;
        PrintQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 9: Complex Multi-Cluster Full-Text Search
    /// Demonstrates multiple search clusters with different criteria
    /// </summary>
    private async Task ComplexMultiClusterFullTextExample()
    {
        Console.WriteLine("9. Complex Multi-Cluster Full-Text Search");
        Console.WriteLine("Primary search for 'artificial intelligence' in name, secondary search for ML terms in description or category...\n");

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
				// Primary content search
				new(Catalysts:
                [
                    new(nameof(MyObject.Name).ToLowerCamelCase(), new[] { "artificial", "intelligence" }, Operator: Q.Operator.FTScore)
                ]),
				// Secondary content search
				new(Where: Q.Where.And, Catalysts:
                [
                    new(nameof(MyObject.Description).ToLowerCamelCase(), new[] { "machine", "learning" }, Operator: Q.Operator.FTContainsAny),
                    new(nameof(MyObject.Category).ToLowerCamelCase(), new[] { "AI", "ML" }, Operator: Q.Operator.FTContainsAny, Where: Q.Where.Or)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.Code).ToLowerCamelCase(), nameof(MyObject.Name).ToLowerCamelCase(), nameof(MyObject.Description).ToLowerCamelCase(), nameof(MyObject.Category).ToLowerCamelCase()], Top: 20)
        );

        ruUsed += gravity.RU;
        PrintQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Example 10: Full-Text Search with Aggregation
    /// Demonstrates grouping results by category and counting
    /// </summary>
    private async Task FullTextSearchWithAggregationExample()
    {
        Console.WriteLine("10. Full-Text Search with Aggregation");
        Console.WriteLine("Group by category and count products containing 'technology' or 'innovation'...\n");

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Description).ToLowerCamelCase(), new[] { "technology", "innovation" }, Operator: Q.Operator.FTContainsAny)
                ])
            ],
            columnOptions: new(
                Names: [nameof(MyObject.Category).ToLowerCamelCase()],
                Aggregates:
                [
                    new(nameof(MyObject.id).ToLowerCamelCase(), Aggregate: Q.Aggregate.Count)
                ]
            )
        );

        ruUsed += gravity.RU;
        Console.WriteLine($"RU Spent: {gravity.RU}");

        // Display query information if available
        if (gravity.Query != default)
        {
            Console.WriteLine($"Query: {gravity.Query.Text}");
            foreach ((string, object) p in gravity.Query.Parameters)
            {
                string valueDisplay = p.Item2 switch
                {
                    string[] strArray => $"[{string.Join(", ", strArray)}]",
                    _ => p.Item2?.ToString() ?? "null"
                };
                Console.WriteLine($"  Parameter: {p.Item1} = {valueDisplay}");
            }
        }

        Console.WriteLine($"Aggregation Results: {results.Count} categories found");
        foreach (MyObject result in results.Take(5))
        {
            Console.WriteLine($"  Category: {result.Category}, Count: {result.CountAggregate}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example 11: Boolean Full-Text Logic
    /// Demonstrates complex boolean logic with full-text operators
    /// </summary>
    private async Task BooleanFullTextLogicExample()
    {
        Console.WriteLine("11. Boolean Full-Text Logic");
        Console.WriteLine("Must contain 'machine learning' AND any of the AI-related terms...\n");

        (Gravity gravity, IList<MyObject> results) = await galaxy.List(
            clusters:
            [
                new(Catalysts:
                [
                    new(nameof(MyObject.Description).ToLowerCamelCase(), "machine learning", Operator: Q.Operator.FTContains),
                    new(nameof(MyObject.Description).ToLowerCamelCase(), new[] { "AI", "artificial intelligence", "neural" }, Operator: Q.Operator.FTContainsAny, Where: Q.Where.And)
                ])
            ],
            columnOptions: new(Names: [nameof(MyObject.Code).ToLowerCamelCase(), nameof(MyObject.Name).ToLowerCamelCase(), nameof(MyObject.Description).ToLowerCamelCase()], Top: 10)
        );

        ruUsed += gravity.RU;
        PrintQueryResults(gravity, results);
        Console.WriteLine();
    }

    /// <summary>
    /// Helper method to print query results
    /// </summary>
    private void PrintQueryResults(Gravity gravity, IList<MyObject> results)
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
            Console.WriteLine($"  Item {i + 1}: {results[i].Code} - {results[i].Name}");
            if (!string.IsNullOrWhiteSpace(results[i].Description))
            {
                string desc = results[i].Description.Length > 100
                    ? $"{results[i].Description[..100]}..."
                    : results[i].Description;
                Console.WriteLine($"    Description: {desc}");
            }
        }
    }

    /// <summary>
    /// Generates sample data with rich text content for full-text search demonstrations
    /// </summary>
    private static List<MyObject> GenerateTextRichSampleData() => [
			// AI/ML Products
			new()
            {
                id = Guid.NewGuid().ToString(),
                Code = "AI-001",
                Name = "Advanced Machine Learning Toolkit",
                Description = "Comprehensive machine learning platform with artificial intelligence capabilities, neural networks, and deep learning algorithms for data science applications.",
                Category = "Electronics",
                Price = 899.99,
                Quantity = 50,
                Links = ["https://example.com/ml-toolkit"]
            },
            new()
            {
                id = Guid.NewGuid().ToString(),
                Code = "AI-002",
                Name = "Neural Network Processing Unit",
                Description = "High-performance processing unit designed for artificial intelligence workloads, machine learning inference, and neural network computations with GPU acceleration.",
                Category = "Electronics",
                Price = 1299.99,
                Quantity = 25,
                Links = ["https://example.com/neural-processor"]
            },
            new()
            {
                id = Guid.NewGuid().ToString(),
                Code = "AI-003",
                Name = "Deep Learning Framework",
                Description = "Open-source deep learning framework supporting machine learning algorithms, neural networks, and AI model training with distributed computing capabilities.",
                Category = "Software",
                Price = 0.0, // Free/Open source
				Quantity = 999,
                Links = ["https://example.com/deep-learning-framework"]
            },
            new()
            {
                id = Guid.NewGuid().ToString(),
                Code = "DATA-001",
                Name = "Big Data Analytics Platform",
                Description = "Enterprise-grade data analytics platform with machine learning integration, real-time processing, and artificial intelligence insights for business intelligence.",
                Category = "Software",
                Price = 2499.99,
                Quantity = 10,
                Links = ["https://example.com/big-data-platform"]
            },
            new()
            {
                id = Guid.NewGuid().ToString(),
                Code = "TECH-001",
                Name = "Innovation Hub Development Kit",
                Description = "Technology innovation platform enabling rapid prototyping, software development, and creative solutions for modern applications with cutting-edge features.",
                Category = "Electronics",
                Price = 599.99,
                Quantity = 75,
                Links = ["https://example.com/innovation-hub"]
            },

			// Non-AI Products for contrast
			new()
            {
                id = Guid.NewGuid().ToString(),
                Code = "FURN-001",
                Name = "Ergonomic Office Chair",
                Description = "Premium ergonomic office chair with lumbar support, adjustable height, and breathable mesh fabric for comfortable long-term sitting.",
                Category = "Furniture",
                Price = 299.99,
                Quantity = 30,
                Links = ["https://example.com/office-chair"]
            },
            new()
            {
                id = Guid.NewGuid().ToString(),
                Code = "FURN-002",
                Name = "Standing Desk Converter",
                Description = "Height-adjustable standing desk converter that transforms any workspace into an ergonomic standing workstation for better health and productivity.",
                Category = "Furniture",
                Price = 199.99,
                Quantity = 40,
                Links = ["https://example.com/standing-desk"]
            },
            new()
            {
                id = Guid.NewGuid().ToString(),
                Code = "BOOK-001",
                Name = "Machine Learning for Beginners",
                Description = "Comprehensive guide to machine learning fundamentals, covering algorithms, neural networks, and practical artificial intelligence applications with hands-on examples.",
                Category = "Books",
                Price = 49.99,
                Quantity = 100,
                Links = ["https://example.com/ml-book"]
            },
            new()
            {
                id = Guid.NewGuid().ToString(),
                Code = "LEGACY-001",
                Name = "Legacy Database System (Deprecated)",
                Description = "Older database management system marked as deprecated. No longer recommended for new projects due to obsolete architecture and outdated technology.",
                Category = "Software",
                Price = 99.99,
                Quantity = 5,
                Links = ["https://example.com/legacy-db"]
            },
            new()
            {
                id = Guid.NewGuid().ToString(),
                Code = "CLOUD-001",
                Name = "Cloud Computing Platform",
                Description = "Scalable cloud infrastructure platform with AI services, machine learning APIs, and innovative technology solutions for modern applications.",
                Category = "Software",
                Price = 199.99,
                Quantity = 999,
                Links = ["https://example.com/cloud-platform"]
            }
        ];
}