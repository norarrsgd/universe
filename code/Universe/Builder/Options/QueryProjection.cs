namespace Universe.Builder.Options;

/// <summary>An immutable snapshot of named fields, nested objects, and aggregates.</summary>
public sealed record QueryProjection
{
    internal IReadOnlyList<ProjectionItem> Items { get; }
    internal bool HasAggregates => Items.Any(item => item.Aggregate.HasValue || item.Object?.HasAggregates == true);
    internal QueryProjection(IReadOnlyList<ProjectionItem> items) => Items = items;
}

internal sealed record ProjectionItem(string Name, string Column = null, string Alias = "c", Q.Aggregate? Aggregate = null, QueryProjection Object = null);
