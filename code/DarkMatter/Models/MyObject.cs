using Universe.Attributes;
using Universe.Interfaces;

namespace DarkMatter.Models;

public record MyObject : CosmicEntity
{
	// Custom properties
	[PartitionKey(2)] public string Code { get; set; }

	public string Name { get; set; }

	public string Description { get; set; }

	public string[] Links { get; set; }

	public double Price { get; set; }

	public int Quantity { get; set; }

	[PartitionKey(1)] public string Category { get; set; }
}