using DarkMatter.Models;

namespace DarkMatter.Helpers;

/// <summary>
/// Helper class to generate various types of test data for examples 1-8
/// Provides standardized data generation methods to keep examples focused on query patterns
/// 
/// This class centralizes all data initialization logic that was previously scattered across
/// individual example files, improving maintainability and consistency.
/// 
/// Available data generators:
/// - CreateSingleTestItem: For single item operations (Example 5)
/// - CreateBulkTestItems: For bulk operations (Example 6) 
/// - CreateCategoryTestItems: For aggregation scenarios (Example 7)
/// - CreateSalesTestData: For sales analysis scenarios (Example 8)
/// - CreateFilteringTestData: For complex filtering scenarios
/// - CreateGeneralTestData: For general querying and paging scenarios
/// - CreatePredefinedTestData: For precise testing with known values
/// - SeedInitialTestDataAsync: Optional database seeding for examples that query existing data
/// </summary>
public static class TestDataGenerator
{
	private static readonly Random _random = new();

	/// <summary>
	/// Generates a single test item for single item operations (Example 5)
	/// </summary>
	public static MyObject CreateSingleTestItem() => new()
	{
		Code = "NEW-ITEM-" + DateTime.Now.Ticks,
		Name = "New Test Item",
		Description = "Created via Universe API",
		Links = ["link1", "link2"],
		Price = 29.99,
		Quantity = 10,
		Category = "Electronics",
	};

	/// <summary>
	/// Generates a collection of items for bulk operations (Example 6)
	/// </summary>
	/// <param name="count">Number of items to generate</param>
	public static List<MyObject> CreateBulkTestItems(int count = 3)
	{
		List<MyObject> bulkItems = [];

		for (int i = 1; i <= count; i++)
		{
			bulkItems.Add(new MyObject
			{
				Code = "BULK-" + i,
				Name = $"Bulk Item {i}",
				Description = "Part of bulk operation",
				Links = [$"bulk-link-{i}"],
				Price = 10.99 * i,
				Quantity = i * 5,
				Category = i % 2 == 0 ? "Electronics" : "Books"
			});
		}

		return bulkItems;
	}

	/// <summary>
	/// Generates items with random categories for aggregation testing (Example 7)
	/// </summary>
	/// <param name="count">Number of items to generate</param>
	public static List<MyObject> CreateCategoryTestItems(int count = 10)
	{
		List<MyObject> categoryItems = [];
		string[] categories = ["Electronics", "Books", "Clothing", "Toys"];

		for (int i = 1; i <= count; i++)
		{
			string category = categories[_random.Next(categories.Length)];
			categoryItems.Add(new MyObject
			{
				Code = $"CAT-ITEM-{i}",
				Name = $"Category Item {i}",
				Description = $"Category example item {i}",
				Links = [$"cat-link-{i}"],
				Price = Math.Round(_random.NextDouble() * 100, 2),
				Quantity = _random.Next(1, 50),
				Category = category
			});
		}

		return categoryItems;
	}

	/// <summary>
	/// Generates sales data with regions, products, and date ranges for analysis (Example 8)
	/// </summary>
	/// <param name="count">Number of sales records to generate</param>
	/// <param name="dayRange">Range of days in the past to generate data for</param>
	public static List<MyObject> CreateSalesTestData(int count = 20, int dayRange = 30)
	{
		List<MyObject> salesData = [];
		string[] regions = ["North", "South", "East", "West"];
		string[] products = ["Laptop", "Phone", "Tablet", "Desktop"];
		DateTime today = DateTime.Today;

		for (int i = 1; i <= count; i++)
		{
			// Create data for the specified day range
			int daysAgo = _random.Next(0, dayRange);
			string region = regions[_random.Next(regions.Length)];
			string product = products[_random.Next(products.Length)];

			salesData.Add(new MyObject
			{
				Code = $"SALE-{i}",
				Name = $"{product} Sale",
				Description = $"Sale in {region} region",
				Links = [$"sale-{i}"],
				Price = Math.Round(_random.NextDouble() * 1000, 2),
				Quantity = _random.Next(1, 10),
				Category = region,
				AddedOn = today.AddDays(-daysAgo)
			});
		}

		return salesData;
	}

	/// <summary>
	/// Generates items with specific price ranges and dates for complex filtering scenarios
	/// </summary>
	/// <param name="count">Number of items to generate</param>
	public static List<MyObject> CreateFilteringTestData(int count = 15)
	{
		List<MyObject> items = [];
		string[] categories = ["Standard", "Premium", "Budget"];
		string[] specialCodes = ["SPECIAL", "REGULAR", "DISCOUNT"];

		for (int i = 1; i <= count; i++)
		{
			bool isTestItem = i % 3 == 0; // Every third item contains "Test" in name
			bool isRecentItem = i % 2 == 0; // Every second item is recent
			bool isSpecialCode = i % 4 == 0; // Every fourth item has special code

			items.Add(new MyObject
			{
				Code = isSpecialCode ? "SPECIAL-" + i : "REG-" + i,
				Name = isTestItem ? $"Test Item {i}" : $"Regular Item {i}",
				Description = $"Filtering test item {i}",
				Links = [$"filter-link-{i}"],
				Price = _random.NextDouble() * 100, // 0-100 range for price filtering
				Quantity = _random.Next(1, 20),
				Category = categories[_random.Next(categories.Length)],
				AddedOn = isRecentItem ? DateTime.Now.AddDays(-_random.Next(1, 15)) : DateTime.Now.AddDays(-_random.Next(31, 60))
			});
		}

		return items;
	}

	/// <summary>
	/// Generates a diverse set of items for general querying and paging scenarios
	/// </summary>
	/// <param name="count">Number of items to generate</param>
	public static List<MyObject> CreateGeneralTestData(int count = 25)
	{
		List<MyObject> items = [];
		string[] categories = ["Electronics", "Books", "Clothing", "Toys", "Sports", "Home"];
		string[] prefixes = ["PROD", "ITEM", "TEST", "DEMO"];

		for (int i = 1; i <= count; i++)
		{
			string prefix = prefixes[_random.Next(prefixes.Length)];
			string category = categories[_random.Next(categories.Length)];

			items.Add(new MyObject
			{
				Code = $"{prefix}-{i:D3}",
				Name = $"{category} Item {i}",
				Description = $"General test item {i} in {category} category",
				Links = [$"general-link-{i}", $"category-{category.ToLower()}"],
				Price = Math.Round(_random.NextDouble() * 200, 2),
				Quantity = _random.Next(1, 100),
				Category = category,
				AddedOn = DateTime.Now.AddDays(-_random.Next(1, 365))
			});
		}

		return items;
	}

	/// <summary>
	/// Creates a predefined set of items with known values for precise testing
	/// </summary>
	public static List<MyObject> CreatePredefinedTestData() =>
	[
		new MyObject
		{
			Code = "KNOWN-001",
			Name = "Known Test Item 1",
			Description = "Predefined item for testing",
			Links = ["known-link-1"],
			Price = 50.00,
			Quantity = 5,
			Category = "Electronics",
			AddedOn = DateTime.Now.AddDays(-10)
		},
		new MyObject
		{
			Code = "KNOWN-002",
			Name = "Known Test Item 2",
			Description = "Another predefined item",
			Links = ["known-link-2"],
			Price = 75.50,
			Quantity = 8,
			Category = "Books",
			AddedOn = DateTime.Now.AddDays(-5)
		},
		new MyObject
		{
			Code = "SPECIAL",
			Name = "Special Known Item",
			Description = "Special item with SPECIAL code",
			Links = ["special-link"],
			Price = 100.00,
			Quantity = 1,
			Category = "Premium",
			AddedOn = DateTime.Now.AddDays(-1)
		}
	];

	/// <summary>
	/// Seeds the database with initial test data for examples that query existing data
	/// This is optional and can be used to ensure examples have data to work with
	/// </summary>
	/// <param name="galaxy">The galaxy instance to use for data creation</param>
	/// <param name="includePredefined">Whether to include predefined test data</param>
	/// <param name="includeGeneral">Whether to include general test data</param>
	/// <param name="includeFiltering">Whether to include filtering test data</param>
	public static async Task<double> SeedInitialTestDataAsync<T>(
		Universe.Interfaces.IGalaxy<T> galaxy,
		bool includePredefined = true,
		bool includeGeneral = true,
		bool includeFiltering = true) where T : MyObject
	{
		double totalRU = 0;

		if (includePredefined)
		{
			List<MyObject> predefinedData = CreatePredefinedTestData();
			Universe.Response.Gravity g1 = await galaxy.Create(predefinedData.Cast<T>().ToList());
			totalRU += g1.RU;
		}

		if (includeGeneral)
		{
			List<MyObject> generalData = CreateGeneralTestData(10); // Smaller set for seeding
			Universe.Response.Gravity g2 = await galaxy.Create(generalData.Cast<T>().ToList());
			totalRU += g2.RU;
		}

		if (includeFiltering)
		{
			List<MyObject> filteringData = CreateFilteringTestData(8); // Smaller set for seeding
			Universe.Response.Gravity g3 = await galaxy.Create(filteringData.Cast<T>().ToList());
			totalRU += g3.RU;
		}

		return totalRU;
	}
}