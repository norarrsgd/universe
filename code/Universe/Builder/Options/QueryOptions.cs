namespace Universe.Builder.Options;

/// <summary>Query Options</summary>
public struct Q
{
    /// <summary>Query Limits</summary>
    public struct Limits
    {
        /// <summary>Maximum number of items return for vector search queries</summary>
        public const int MaxVectorItems = 50;

        /// <summary>Maximum number of items to return</summary>
        public const int MaxItems = 1000;

        /// <summary>Maximum number of RU per query</summary>
        public const int MaxRU = 1000;

        /// <summary>Maximum response continuation token size in KB</summary>
        public const int MaxContinuationTokenKb = 16;
    }

    /// <summary>Page definition for paginated queries</summary>
    public readonly record struct Page
    {
        /// <summary>Number of items requested for the page.</summary>
        public int Size { get; init; }

        /// <summary>Continuation token returned by Cosmos DB.</summary>
        public string ContinuationToken { get; init; }

        /// <summary>Create a paginated query request.</summary>
        public Page(int Size, string ContinuationToken = null)
        {
            if (Size < 1 || Size > Limits.MaxItems)
                throw new UniverseException($"Page size must be between 1 and {Limits.MaxItems}.");

            this.Size = Size;
            this.ContinuationToken = ContinuationToken;
        }

        /// <summary>Deconstruct a page request.</summary>
        public void Deconstruct(out int Size, out string ContinuationToken)
        {
            Size = this.Size;
            ContinuationToken = this.ContinuationToken;
        }
    }

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
        /// <summary>Equality check. Generates: c.[Column] = @param. Value: scalar to compare against.</summary>
        Eq,

        /// <summary>Inequality check. Generates: c.[Column] != @param. Value: scalar to compare against.</summary>
        NotEq,

        /// <summary>Greater than comparison. Generates: c.[Column] &gt; @param. Value: scalar to compare against.</summary>
        Gt,

        /// <summary>Greater than or equal comparison. Generates: c.[Column] &gt;= @param. Value: scalar to compare against.</summary>
        Gte,

        /// <summary>Less than comparison. Generates: c.[Column] &lt; @param. Value: scalar to compare against.</summary>
        Lt,

        /// <summary>Less than or equal comparison. Generates: c.[Column] &lt;= @param. Value: scalar to compare against.</summary>
        Lte,

        /// <summary>Checks if a document's array field contains a scalar value. Generates: ARRAY_CONTAINS(c.[Column], @param). Value: the scalar to search for.</summary>
        In,

        /// <summary>Checks if a document's array field does NOT contain a scalar value. Generates: NOT ARRAY_CONTAINS(c.[Column], @param). Value: the scalar to search for.</summary>
        NotIn,

        /// <summary>Checks if a user-provided collection contains the document's scalar field value (SQL IN equivalent). Generates: ARRAY_CONTAINS(@param, c.[Column]). Value: an IEnumerable collection (not string) of values to match against.</summary>
        Contains,

        /// <summary>Checks if a user-provided collection does NOT contain the document's scalar field value (SQL NOT IN equivalent). Generates: NOT ARRAY_CONTAINS(@param, c.[Column]). Value: an IEnumerable collection (not string) of values to match against.</summary>
        NotContains,

        /// <summary>Checks the length of a document's array field. Generates: ARRAY_LENGTH(c.[Column]) = @param. Value: integer length to compare against.</summary>
        Len,

        /// <summary>Pattern matching with wildcards. Generates: c.[Column] LIKE @param. Value: string pattern with '%' wildcard(s).</summary>
        Like,

        /// <summary>Negated pattern matching with wildcards. Generates: c.[Column] NOT LIKE @param. Value: string pattern with '%' wildcard(s).</summary>
        NotLike,

        /// <summary>Checks if a property is defined on the document. Generates: IS_DEFINED(c.[Column]). Value: not required (must be null).</summary>
        Defined,

        /// <summary>Checks if a property is NOT defined on the document. Generates: NOT IS_DEFINED(c.[Column]). Value: not required (must be null).</summary>
        NotDefined,

        /// <summary>Vector similarity search. Generates: VectorDistance(c.[Column], @param) in SELECT and ORDER BY. Value: float[] representing the query vector.</summary>
        VectorDistance,

        /// <summary>Full-text contains check for a single keyword. Generates: FullTextContains(c.[Column], @param). Value: string keyword to search for.</summary>
        FTContains,

        /// <summary>Negated full-text contains check. Generates: NOT FullTextContains(c.[Column], @param). Value: string keyword to search for.</summary>
        NotFTContains,

        /// <summary>Full-text check that ALL keywords are present. Generates: FullTextContainsAll(c.[Column], @param). Value: string[] of keywords that must all be present.</summary>
        FTContainsAll,

        /// <summary>Negated full-text check for all keywords. Generates: NOT FullTextContainsAll(c.[Column], @param). Value: string[] of keywords.</summary>
        NotFTContainsAll,

        /// <summary>Full-text check that ANY keyword is present. Generates: FullTextContainsAny(c.[Column], @param). Value: string[] of keywords where at least one must be present.</summary>
        FTContainsAny,

        /// <summary>Negated full-text check for any keyword. Generates: NOT FullTextContainsAny(c.[Column], @param). Value: string[] of keywords.</summary>
        NotFTContainsAny,

        /// <summary>Full-text relevance scoring for ORDER BY RANK. Generates: FullTextScore(c.[Column], @param) in ORDER BY. Value: string[] of search terms. Requires ColumnOptions.Top.</summary>
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
        Q.Operator.Contains => "ARRAY_CONTAINS",
        Q.Operator.NotContains => "NOT ARRAY_CONTAINS",
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
        Q.Aggregate.Sum => "SUM({0}) AS {1}_Sum",
        Q.Aggregate.Min => "MIN({0}) AS {1}_Min",
        Q.Aggregate.Max => "MAX({0}) AS {1}_Max",
        Q.Aggregate.Avg => "AVG({0}) AS {1}_Avg",
        _ => throw new UniverseException("Unrecognized AGGREGATE keyword")
    };
}
