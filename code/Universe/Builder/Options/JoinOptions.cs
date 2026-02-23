namespace Universe.Builder.Options;

/// <summary>
/// Options for joining tables.
/// </summary>
/// <param name="Alias">The alias for the sub-collection.</param>
/// <param name="ArrayPath">The path to the array sub-collection.</param>
/// <param name="Columns">The columns to include in the join.</param>
/// <param name="Aggregates">The aggregation options for the join.</param>
public record JoinOptions(
    string Alias,
    string ArrayPath,
    IReadOnlyList<string> Columns,
    IReadOnlyList<AggregationOption> Aggregates = null);