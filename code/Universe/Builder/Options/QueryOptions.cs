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

        /// <summary>Like</summary>
        Like,

        /// <summary>Not Like</summary>
        NotLike,

        /// <summary>IS_DEFINED</summary>
        Defined,

        /// <summary>NOT IS_DEFINED</summary>
        NotDefined
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
        Q.Operator.In => "IN",
        Q.Operator.NotIn => "NOT IN",
        Q.Operator.Like => "LIKE",
        Q.Operator.NotLike => "NOT LIKE",
        Q.Operator.Defined => "IS_DEFINED",
        Q.Operator.NotDefined => "NOT IS_DEFINED",
        _ => throw new UniverseException("Unrecognized OPERATOR keyword")
    };
}

/// <summary></summary>
public static class AggregateExtension
{
    /// <summary></summary>
    public static string Value(this Q.Aggregate aggregate) => aggregate switch
    {
        Q.Aggregate.Count => "COUNT(1)",
        Q.Aggregate.Sum => "SUM({0}) AS {0}_Sum",
        Q.Aggregate.Min => "MIN({0}) AS {0}_Min",
        Q.Aggregate.Max => "MAX({0}) AS {0}_Max",
        Q.Aggregate.Avg => "AVG({0}) AS {0}_Avg",
        _ => throw new UniverseException("Unrecognized AGGREGATE keyword")
    };
}
