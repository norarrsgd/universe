namespace Universe.Builder.Options;

/// <summary>Query Options</summary>
public struct Q
{
	/// <summary>Query Limits</summary>
	public struct Limits
	{
		/// <summary>Maximum number of items to return</summary>
		public const int MaxItems = 1000;

		/// <summary>Maximum number of RU per query</summary>
		public const int MaxRU = 1000;
	}

	/// <summary>Page definition for paginated queries</summary>
	public record struct Page(int Size, string ContinuationToken = null);

	/// <summary>AND / OR where clause operators</summary>
	public enum Where
	{
		/// <summary></summary>
		And,

		/// <summary></summary>
		Or
	}

	/// <summary>Equality operator</summary>
	public enum Operator
	{
		/// <summary>Equal</summary>
		Eq,

		/// <summary>Not Equal</summary>
		NotEq,

		/// <summary>Greater Than</summary>
		Gt,

		/// <summary>Greater Than Or Equal</summary>
		Gte,

		/// <summary>Lower Than</summary>
		Lt,

		/// <summary>Lower Than Or Equal</summary>
		Lte,

		/// <summary>In</summary>
		In,

		/// <summary>Not In</summary>
		NotIn,

		/// <summary>Array Length</summary>
		Len,

		/// <summary>Like</summary>
		Like,

		/// <summary>Not Like</summary>
		NotLike,

		/// <summary>IS_DEFINED</summary>
		Defined,

		/// <summary>NOT IS_DEFINED</summary>
		NotDefined,

		/// <summary>VectorDistance</summary>
		VectorDistance,

		/// <summary>FullTextContains</summary>
		FTContains,

		/// <summary>NOT FullTextContains</summary>
		NotFTContains,

		/// <summary>FullTextContainsAll</summary>
		FTContainsAll,

		/// <summary>NOT FullTextContainsAll</summary>
		NotFTContainsAll,

		/// <summary>FullTextContainsAny</summary>
		FTContainsAny,

		/// <summary>NOT FullTextContainsAny</summary>
		NotFTContainsAny,

		/// <summary>FullTextScore</summary>
		FTScore
	}

	/// <summary>Aggregation functions</summary>
	public enum Aggregate
	{
		/// <summary>Count</summary>
		Count,

		/// <summary>Sum</summary>
		Sum,

		/// <summary>Min</summary>
		Min,

		/// <summary>Max</summary>
		Max,

		/// <summary>Avg</summary>
		Avg
	}
}

/// <summary></summary>
public static class WhereExtension
{
	/// <summary></summary>
	public static string Value(this Q.Where where) => where switch
	{
		Q.Where.And => "AND",
		Q.Where.Or => "OR",
		_ => throw new UniverseException("Unrecognized WHERE keyword")
	};
}

/// <summary></summary>
public static class OperatorExtension
{
	/// <summary></summary>
	public static string Value(this Q.Operator opr) => opr switch
	{
		Q.Operator.Eq => "=",
		Q.Operator.NotEq => "!=",
		Q.Operator.Gt => ">",
		Q.Operator.Gte => ">=",
		Q.Operator.Lt => "<",
		Q.Operator.Lte => "<=",
		Q.Operator.In => "ARRAY_CONTAINS",
		Q.Operator.Len => "ARRAY_LENGTH",
		Q.Operator.NotIn => "NOT ARRAY_CONTAINS",
		Q.Operator.Like => "LIKE",
		Q.Operator.NotLike => "NOT LIKE",
		Q.Operator.Defined => "IS_DEFINED",
		Q.Operator.NotDefined => "NOT IS_DEFINED",
		Q.Operator.VectorDistance => "VectorDistance",
		Q.Operator.FTContains => "FullTextContains",
		Q.Operator.NotFTContains => "NOT FullTextContains",
		Q.Operator.FTContainsAll => "FullTextContainsAll",
		Q.Operator.NotFTContainsAll => "NOT FullTextContainsAll",
		Q.Operator.FTContainsAny => "FullTextContainsAny",
		Q.Operator.NotFTContainsAny => "NOT FullTextContainsAny",
		Q.Operator.FTScore => "FullTextScore",
		_ => throw new UniverseException("Unrecognized OPERATOR keyword")
	};
}

/// <summary></summary>
public static class AggregateExtension
{
	/// <summary></summary>
	public static string Value(this Q.Aggregate aggregate) => aggregate switch
	{
		Q.Aggregate.Count => $"COUNT(1) AS {nameof(ICosmicEntity.CountAggregate)}",
		Q.Aggregate.Sum => "SUM({0}.{1}) AS {1}_Sum",
		Q.Aggregate.Min => "MIN({0}.{1}) AS {1}_Min",
		Q.Aggregate.Max => "MAX({0}.{1}) AS {1}_Max",
		Q.Aggregate.Avg => "AVG({0}.{1}) AS {1}_Avg",
		_ => throw new UniverseException("Unrecognized AGGREGATE keyword")
	};
}