namespace Universe.Builder.Options;

/// <summary>
/// Represents a column aggregation.
/// </summary>
/// <param name="Column">The name of the column to aggregate.</param>
/// <param name="Aggregate">The aggregate function to apply to the column.</param>
public record struct AggregationOption(string Column, Q.Aggregate Aggregate);