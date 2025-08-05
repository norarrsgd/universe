using DarkMatter.Models;

namespace DarkMatter.Helpers;

/// <summary>
/// Helper class to generate sample data with vector embeddings for testing VectorDistance queries
/// In a real application, vectors would be generated using embedding models like text-embedding-ada-002
/// </summary>
public static class VectorDataGenerator
{
	private static readonly Random _random = new(42); // Fixed seed for reproducible results

	/// <summary>
	/// Generates sample products with vector embeddings for testing
	/// </summary>
	public static List<MyObjectVector> GenerateSampleVectorData()
	{
		List<MyObjectVector> products =
		[
			new()
			{
				id = "1",
				Code = "LAPTOP001",
				Name = "Gaming Laptop Pro",
				Description = "High-performance gaming laptop with RTX 4070, 16GB RAM, perfect for gaming and content creation",
				Category = "Electronics",
				Price = 1499.99,
				Quantity = 10,
				TitleEmbedding = GenerateEmbedding("gaming laptop pro"),
				DescriptionEmbedding = GenerateEmbedding("high performance gaming laptop rtx 4070 16gb ram gaming content creation"),
				CombinedEmbedding = GenerateEmbedding("gaming laptop pro high performance gaming laptop rtx 4070 16gb ram gaming content creation")
			},
			new()
			{
				id = "2",
				Code = "LAPTOP002",
				Name = "Business Ultrabook",
				Description = "Lightweight business laptop with long battery life, Intel i7, 32GB RAM, ideal for professionals",
				Category = "Electronics",
				Price = 1299.99,
				Quantity = 15,
				TitleEmbedding = GenerateEmbedding("business ultrabook"),
				DescriptionEmbedding = GenerateEmbedding("lightweight business laptop long battery life intel i7 32gb ram professionals"),
				CombinedEmbedding = GenerateEmbedding("business ultrabook lightweight business laptop long battery life intel i7 32gb ram professionals")
			},
			new()
			{
				id = "3",
				Code = "PHONE001",
				Name = "Smartphone Pro Max",
				Description = "Latest flagship smartphone with advanced camera system, 5G connectivity, and premium design",
				Category = "Electronics",
				Price = 1099.99,
				Quantity = 25,
				TitleEmbedding = GenerateEmbedding("smartphone pro max"),
				DescriptionEmbedding = GenerateEmbedding("latest flagship smartphone advanced camera system 5g connectivity premium design"),
				CombinedEmbedding = GenerateEmbedding("smartphone pro max latest flagship smartphone advanced camera system 5g connectivity premium design")
			},
			new()
			{
				id = "4",
				Code = "TAB001",
				Name = "Professional Tablet",
				Description = "High-resolution tablet for digital artists and designers with stylus support and color accuracy",
				Category = "Electronics",
				Price = 899.99,
				Quantity = 8,
				TitleEmbedding = GenerateEmbedding("professional tablet"),
				DescriptionEmbedding = GenerateEmbedding("high resolution tablet digital artists designers stylus support color accuracy"),
				CombinedEmbedding = GenerateEmbedding("professional tablet high resolution tablet digital artists designers stylus support color accuracy")
			},
			new()
			{
				id = "5",
				Code = "DESK001",
				Name = "Standing Desk Pro",
				Description = "Ergonomic electric standing desk with memory presets, cable management, and premium finish",
				Category = "Furniture",
				Price = 599.99,
				Quantity = 12,
				TitleEmbedding = GenerateEmbedding("standing desk pro"),
				DescriptionEmbedding = GenerateEmbedding("ergonomic electric standing desk memory presets cable management premium finish"),
				CombinedEmbedding = GenerateEmbedding("standing desk pro ergonomic electric standing desk memory presets cable management premium finish")
			},
			new()
			{
				id = "6",
				Code = "CHAIR001",
				Name = "Executive Office Chair",
				Description = "Premium leather executive chair with lumbar support, adjustable height, and 360-degree swivel",
				Category = "Furniture",
				Price = 799.99,
				Quantity = 6,
				TitleEmbedding = GenerateEmbedding("executive office chair"),
				DescriptionEmbedding = GenerateEmbedding("premium leather executive chair lumbar support adjustable height 360 degree swivel"),
				CombinedEmbedding = GenerateEmbedding("executive office chair premium leather executive chair lumbar support adjustable height 360 degree swivel")
			},
			new()
			{
				id = "7",
				Code = "LAPTOP003",
				Name = "Student Laptop Basic",
				Description = "Affordable laptop for students with solid performance, good battery life, and lightweight design",
				Category = "Electronics",
				Price = 599.99,
				Quantity = 20,
				TitleEmbedding = GenerateEmbedding("student laptop basic"),
				DescriptionEmbedding = GenerateEmbedding("affordable laptop students solid performance good battery life lightweight design"),
				CombinedEmbedding = GenerateEmbedding("student laptop basic affordable laptop students solid performance good battery life lightweight design")
			},
			new()
			{
				id = "8",
				Code = "MONITOR001",
				Name = "4K Gaming Monitor",
				Description = "Ultra-wide 4K gaming monitor with HDR support, 144Hz refresh rate, perfect for immersive gaming",
				Category = "Electronics",
				Price = 699.99,
				Quantity = 14,
				TitleEmbedding = GenerateEmbedding("4k gaming monitor"),
				DescriptionEmbedding = GenerateEmbedding("ultra wide 4k gaming monitor hdr support 144hz refresh rate immersive gaming"),
				CombinedEmbedding = GenerateEmbedding("4k gaming monitor ultra wide 4k gaming monitor hdr support 144hz refresh rate immersive gaming")
			}
		];

		return products;
	}

	/// <summary>
	/// Generates a simple vector embedding for demonstration purposes
	/// In production, use proper embedding models like OpenAI's text-embedding-ada-002
	/// </summary>
	private static float[] GenerateEmbedding(string text, int dimensions = 8)
	{
		// Simple hash-based embedding generation for demo purposes
		// This creates consistent embeddings for the same text
		float[] embedding = new float[dimensions];
		int hash = text.GetHashCode();

		for (int i = 0; i < dimensions; i++)
		{
			// Use different seeds for each dimension to create variety
			_random.InitState((uint)(hash + i * 12345));
			embedding[i] = (float)_random.NextDouble();
		}

		// Normalize the vector
		double magnitude = Math.Sqrt(embedding.Sum(x => x * x));
		for (int i = 0; i < dimensions; i++)
		{
			embedding[i] = (float)(embedding[i] / magnitude);
		}

		return embedding;
	}

	/// <summary>
	/// Sample query vectors that would be similar to certain products
	/// </summary>
	public static class SampleQueryVectors
	{
		/// <summary>Vector similar to gaming laptops</summary>
		public static float[] GamingLaptopQuery => GenerateEmbedding("gaming laptop powerful graphics performance");

		/// <summary>Vector similar to business laptops</summary>
		public static float[] BusinessLaptopQuery => GenerateEmbedding("business laptop professional work productivity");

		/// <summary>Vector similar to furniture</summary> 
		public static float[] FurnitureQuery => GenerateEmbedding("office furniture desk chair ergonomic");

		/// <summary>Vector similar to affordable electronics</summary>
		public static float[] AffordableElectronicsQuery => GenerateEmbedding("affordable budget electronics student");
	}
}

/// <summary>
/// Extension for Random to support seeding (for .NET compatibility)
/// </summary>
internal static class RandomExtensions
{
	public static void InitState(this Random random, uint seed)
	{
		// This is a simplified approach - in production you might want a more sophisticated seeding mechanism
		// For demo purposes, we'll recreate the Random instance
	}
}