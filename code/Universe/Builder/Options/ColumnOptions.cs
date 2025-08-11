namespace Universe.Builder.Options;

/// <summary></summary>
/// <param name="Names">List of column names to be part of the query</param>
/// <param name="IsDistinct">Adds DISTINCT in the generated query</param>
/// <param name="Top">Only select the specified number of top rows</param>
/// <param name="Aggregates">
///         List of aggregates to be applied to the query.
///         If aggregates are specified, the query will be grouped by the <paramref name="Names"/> columns.
/// </param>
/// <param name="Join">Options for joining a sub-collection.</param>
public record struct ColumnOptions(
	IReadOnlyList<string> Names,
	bool IsDistinct = false,
	int Top = 0,
	IReadOnlyList<AggregationOption> Aggregates = null,
	JoinOptions Join = null);