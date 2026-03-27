using System.Text.Json.Serialization;
using DarkMatter.Models;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example16_ProjectionSelect(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
	public override async Task<double> RunAsync()
	{
		Console.WriteLine("\n=== EXAMPLE 16: Type-Based Projection Select ===\n");

		// ── 1. Basic projection with ToListAsync<T>() ───────────────────────
		Console.WriteLine("1. Basic Projection — Select<ProductSummary>().ToListAsync<ProductSummary>():");
		Console.WriteLine("   Query only the Name, Price, and Category columns.\n");

		(Gravity g1, IList<ProductSummary> products) = await galaxy.Query()
			.Select<ProductSummary>()
			.Top(5)
			.Cluster(c => c.Defined("Name"))
			.OrderByDescending("Price")
			.ToListAsync<ProductSummary>();

		ruUsed += g1.RU;
		PrintProjectionResults(g1, products);
		Console.WriteLine();

		// ── 2. Projection with [JsonIgnore] ─────────────────────────────────
		Console.WriteLine("2. Projection with [JsonIgnore] — computed property excluded from query:");
		Console.WriteLine("   InventoryView.IsLowStock is computed client-side from Quantity.\n");

		(Gravity g2, IList<InventoryView> inventory) = await galaxy.Query()
			.Select<InventoryView>()
			.Top(5)
			.Cluster(c => c.Gt("Quantity", 0))
			.ToListAsync<InventoryView>();

		ruUsed += g2.RU;
		PrintProjectionResults(g2, inventory);
		foreach (InventoryView item in inventory.Take(3))
			Console.WriteLine($"     {item.Name}: Qty={item.Quantity}, IsLowStock={item.IsLowStock}");
		Console.WriteLine();

		// ── 3. Combining projection + extra string column ───────────────────
		Console.WriteLine("3. Combining Select<T>() + Select(string) — additive columns:");
		Console.WriteLine("   ProductSummary columns + Description, deserialized as DetailedProductView.\n");

		(Gravity g3, IList<DetailedProductView> detailed) = await galaxy.Query()
			.Select<ProductSummary>()
			.Select("Description")
			.Top(3)
			.Cluster(c => c.Defined("Description"))
			.ToListAsync<DetailedProductView>();

		ruUsed += g3.RU;
		PrintProjectionResults(g3, detailed);
		Console.WriteLine();

		// ── 4. Pagination with projection ───────────────────────────────────
		Console.WriteLine("4. Paginated projection — first page of 3 items:");

		(Gravity g4, IList<ProductSummary> page1) = await galaxy.Query()
			.Select<ProductSummary>()
			.Paged(3)
			.Cluster(c => c.Defined("Name"))
			.OrderBy("Name")
			.ToListAsync<ProductSummary>();

		ruUsed += g4.RU;
		PrintProjectionResults(g4, page1);
		Console.WriteLine($"   Continuation token: {(string.IsNullOrEmpty(g4.ContinuationToken) ? "(none)" : g4.ContinuationToken[..Math.Min(50, g4.ContinuationToken.Length)] + "...")}");

		if (!string.IsNullOrEmpty(g4.ContinuationToken))
		{
			Console.WriteLine("\n   Fetching next page...");
			(Gravity g4b, IList<ProductSummary> page2) = await galaxy.Query()
				.Select<ProductSummary>()
				.Paged(3, g4.ContinuationToken)
				.Cluster(c => c.Defined("Name"))
				.OrderBy("Name")
				.ToListAsync<ProductSummary>();

			ruUsed += g4b.RU;
			PrintProjectionResults(g4b, page2);
		}

		Console.WriteLine();

		// ── 5. Single item with GetAsync<T>() ───────────────────────────────
		Console.WriteLine("5. Single item — Select<ProductSummary>().GetAsync<ProductSummary>():");

		(Gravity g5, ProductSummary single) = await galaxy.Query()
			.Select<ProductSummary>()
			.Cluster(c => c.Defined("Name"))
			.GetAsync<ProductSummary>();

		ruUsed += g5.RU;
		Console.WriteLine($"   RU: {g5.RU}");
		if (g5.Query != default)
			Console.WriteLine($"   Query: {g5.Query.Text}");
		Console.WriteLine($"   Result: {single}");
		Console.WriteLine();

		// ── 6. Side-by-side SQL comparison (GenerateQuery) ──────────────────
		Console.WriteLine("6. Side-by-side SQL — string Select vs typed Select:");

		Gravity manual = galaxy.Query()
			.Select("Name", "Price", "Category")
			.Cluster(c => c.Eq("Code", "ABC"))
			.GenerateQuery();

		Gravity typed = galaxy.Query()
			.Select<ProductSummary>()
			.Cluster(c => c.Eq("Code", "ABC"))
			.GenerateQuery();

		Console.WriteLine($"   String: {manual.Query.Text}");
		Console.WriteLine($"   Typed:  {typed.Query.Text}");
		Console.WriteLine();

		Console.WriteLine("Projection select examples completed successfully!");
		return ruUsed;
	}

	private static void PrintProjectionResults<T>(Gravity g, IList<T> results)
	{
		Console.WriteLine($"   RU: {g.RU}");
		if (g.Query != default)
			Console.WriteLine($"   Query: {g.Query.Text}");
		Console.WriteLine($"   Count: {results.Count}");
		foreach (T item in results.Take(3))
			Console.WriteLine($"     {item}");
	}
}

// ── Projection types used in examples ───────────────────────────────────────

/// <summary>Lightweight projection for product listing pages.</summary>
record ProductSummary
{
	public string Name { get; init; }
	public double Price { get; init; }
	public string Category { get; init; }
}

/// <summary>Inventory view — IsLowStock is computed client-side and excluded from SELECT.</summary>
record InventoryView
{
	public string Code { get; init; }
	public string Name { get; init; }
	public int Quantity { get; init; }
	[JsonIgnore] public bool IsLowStock => Quantity < 10;
}

/// <summary>Value-type projection for chart/graph data points.</summary>
record struct PricePoint
{
	public string Code { get; init; }
	public double Price { get; init; }
}

/// <summary>Extended projection inheriting base columns and adding Description.</summary>
record DetailedProductView : ProductSummary
{
	public string Description { get; init; }
}
