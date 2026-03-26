using System.Text.Json.Serialization;
using DarkMatter.Models;
using Universe.Extensions;
using Universe.Interfaces;
using Universe.Response;

namespace DarkMatter.Examples;

public class Example16_ProjectionSelect(IGalaxy<MyObject> galaxy) : ExampleBase(galaxy)
{
	public override Task<double> RunAsync()
	{
		Console.WriteLine("\n=== EXAMPLE 16: Type-Based Projection Select ===\n");

		// ── 1. Basic projection: extract columns from a record ──────────────
		Console.WriteLine("1. Basic Projection — Select<ProductSummary>():");
		Console.WriteLine("   Instead of .Select(\"Name\", \"Price\", \"Category\")");
		Console.WriteLine("   use the type's properties as the column list.\n");

		Gravity q1 = galaxy.Query()
			.Select<ProductSummary>()
			.Cluster(c => c.Gte("Price", 50.0))
			.GenerateQuery();

		Console.WriteLine($"   SQL: {q1.Query.Text}");
		Console.WriteLine();

		// ── 2. Projection with [JsonIgnore] ─────────────────────────────────
		Console.WriteLine("2. Projection with [JsonIgnore] — excluded properties:");
		Console.WriteLine("   InventoryView has a [JsonIgnore] on IsLowStock,");
		Console.WriteLine("   so it does NOT appear in the SELECT clause.\n");

		Gravity q2 = galaxy.Query()
			.Select<InventoryView>()
			.Cluster(c => c.Gt("Quantity", 0))
			.GenerateQuery();

		Console.WriteLine($"   SQL: {q2.Query.Text}");
		Console.WriteLine();

		// ── 3. Combining projection + extra string columns ──────────────────
		Console.WriteLine("3. Combining Select<T>() with Select(string) — additive:");
		Console.WriteLine("   Start with ProductSummary columns, then add Description.\n");

		Gravity q3 = galaxy.Query()
			.Select<ProductSummary>()
			.Select("Description")
			.Cluster(c => c.Like("Name", "%Widget%"))
			.GenerateQuery();

		Console.WriteLine($"   SQL: {q3.Query.Text}");
		Console.WriteLine();

		// ── 4. Projection with struct (value type) ──────────────────────────
		Console.WriteLine("4. Projection with record struct:");
		Console.WriteLine("   Select<T>() works with classes, records, structs, and record structs.\n");

		Gravity q4 = galaxy.Query()
			.Select<PricePoint>()
			.Top(10)
			.Cluster(c => c.Eq("Category", "Electronics"))
			.OrderBy("Price")
			.GenerateQuery();

		Console.WriteLine($"   SQL: {q4.Query.Text}");
		Console.WriteLine();

		// ── 5. Projection with inherited type ───────────────────────────────
		Console.WriteLine("5. Projection with inheritance — includes base + derived properties:");
		Console.WriteLine("   DetailedProductView inherits from ProductSummary and adds Description.\n");

		Gravity q5 = galaxy.Query()
			.Select<DetailedProductView>()
			.Cluster(c => c.Defined("Description"))
			.GenerateQuery();

		Console.WriteLine($"   SQL: {q5.Query.Text}");
		Console.WriteLine();

		// ── 6. Side-by-side: string Select vs projection Select ─────────────
		Console.WriteLine("6. Side-by-side comparison — identical SQL output:");

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
		return Task.FromResult(0.0);
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
