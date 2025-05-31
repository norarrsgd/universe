using Universe.Interfaces;

namespace DarkMatter.Models;

public record MyObject : ICosmicEntity
{        // ICosmicEntity required properties
    public string id { get; set; } = Guid.NewGuid().ToString();
    public string PartitionKey => Code;
    public DateTime AddedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedOn { get; set; } = DateTime.UtcNow;
    public long CountAggregate { get; set; }

    // Custom properties
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string[] Links { get; set; }
    public double Price { get; set; }
    public int Quantity { get; set; }
    public string Category { get; set; }
}
