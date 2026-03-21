using Universe.Builder.Options;

namespace Universe.Builder;

/// <summary>
/// Builder for constructing a single cluster of Catalyst conditions.
/// Used inside Orbit.Cluster(c => ...) lambdas.
/// </summary>
public sealed class ClusterBuilder
{
    private readonly List<Catalyst> _catalysts = [];
    private Q.Where _nextWhere = Q.Where.And;

    /// <summary>Add a filter condition to this cluster.</summary>
    public ClusterBuilder Catalyst(string column, object value = null,
        Q.Operator op = Q.Operator.Eq, string alias = "c")
    {
        _catalysts.Add(new Catalyst(column, value, _nextWhere, op, alias));
        _nextWhere = Q.Where.And;
        return this;
    }

    /// <summary>The next condition will be joined with AND.</summary>
    public ClusterBuilder And()
    {
        _nextWhere = Q.Where.And;
        return this;
    }

    /// <summary>The next condition will be joined with OR.</summary>
    public ClusterBuilder Or()
    {
        _nextWhere = Q.Where.Or;
        return this;
    }

    /// <summary>Equality: column = value</summary>
    public ClusterBuilder Eq(string column, object value, string alias = "c")
        => Catalyst(column, value, Q.Operator.Eq, alias);

    /// <summary>Inequality: column != value</summary>
    public ClusterBuilder NotEq(string column, object value, string alias = "c")
        => Catalyst(column, value, Q.Operator.NotEq, alias);

    /// <summary>Greater than: column > value</summary>
    public ClusterBuilder Gt(string column, object value, string alias = "c")
        => Catalyst(column, value, Q.Operator.Gt, alias);

    /// <summary>Greater than or equal: column >= value</summary>
    public ClusterBuilder Gte(string column, object value, string alias = "c")
        => Catalyst(column, value, Q.Operator.Gte, alias);

    /// <summary>Less than: column &lt; value</summary>
    public ClusterBuilder Lt(string column, object value, string alias = "c")
        => Catalyst(column, value, Q.Operator.Lt, alias);

    /// <summary>Less than or equal: column &lt;= value</summary>
    public ClusterBuilder Lte(string column, object value, string alias = "c")
        => Catalyst(column, value, Q.Operator.Lte, alias);

    /// <summary>LIKE pattern matching</summary>
    public ClusterBuilder Like(string column, string pattern, string alias = "c")
        => Catalyst(column, pattern, Q.Operator.Like, alias);

    /// <summary>NOT LIKE pattern matching</summary>
    public ClusterBuilder NotLike(string column, string pattern, string alias = "c")
        => Catalyst(column, pattern, Q.Operator.NotLike, alias);

    /// <summary>Check if property is defined on the document</summary>
    public ClusterBuilder Defined(string column, string alias = "c")
        => Catalyst(column, null, Q.Operator.Defined, alias);

    /// <summary>Check if property is NOT defined on the document</summary>
    public ClusterBuilder NotDefined(string column, string alias = "c")
        => Catalyst(column, null, Q.Operator.NotDefined, alias);

    /// <summary>Check if document's array field contains a scalar value</summary>
    public ClusterBuilder In(string column, object value, string alias = "c")
        => Catalyst(column, value, Q.Operator.In, alias);

    /// <summary>Check if document's array field does NOT contain a scalar value</summary>
    public ClusterBuilder NotIn(string column, object value, string alias = "c")
        => Catalyst(column, value, Q.Operator.NotIn, alias);

    /// <summary>Check if a collection contains the document's scalar field value</summary>
    public ClusterBuilder Contains(string column, IEnumerable<object> values, string alias = "c")
        => Catalyst(column, values, Q.Operator.Contains, alias);

    /// <summary>Check if a collection does NOT contain the document's scalar field value</summary>
    public ClusterBuilder NotContains(string column, IEnumerable<object> values, string alias = "c")
        => Catalyst(column, values, Q.Operator.NotContains, alias);

    /// <summary>Array length check</summary>
    public ClusterBuilder Len(string column, int length, string alias = "c")
        => Catalyst(column, length, Q.Operator.Len, alias);

    /// <summary>Vector distance search</summary>
    public ClusterBuilder VectorDistance(string column, float[] vector, string alias = "c")
        => Catalyst(column, vector, Q.Operator.VectorDistance, alias);

    /// <summary>Full-text contains single keyword</summary>
    public ClusterBuilder FTContains(string column, string keyword, string alias = "c")
        => Catalyst(column, keyword, Q.Operator.FTContains, alias);

    /// <summary>Negated full-text contains</summary>
    public ClusterBuilder NotFTContains(string column, string keyword, string alias = "c")
        => Catalyst(column, keyword, Q.Operator.NotFTContains, alias);

    /// <summary>Full-text contains ALL keywords</summary>
    public ClusterBuilder FTContainsAll(string column, string[] keywords, string alias = "c")
        => Catalyst(column, keywords, Q.Operator.FTContainsAll, alias);

    /// <summary>Negated full-text contains all</summary>
    public ClusterBuilder NotFTContainsAll(string column, string[] keywords, string alias = "c")
        => Catalyst(column, keywords, Q.Operator.NotFTContainsAll, alias);

    /// <summary>Full-text contains ANY keyword</summary>
    public ClusterBuilder FTContainsAny(string column, string[] keywords, string alias = "c")
        => Catalyst(column, keywords, Q.Operator.FTContainsAny, alias);

    /// <summary>Negated full-text contains any</summary>
    public ClusterBuilder NotFTContainsAny(string column, string[] keywords, string alias = "c")
        => Catalyst(column, keywords, Q.Operator.NotFTContainsAny, alias);

    /// <summary>Full-text relevance scoring</summary>
    public ClusterBuilder FTScore(string column, string[] terms, string alias = "c")
        => Catalyst(column, terms, Q.Operator.FTScore, alias);

    internal Cluster Build(Q.Where where)
        => new(_catalysts, where);
}
