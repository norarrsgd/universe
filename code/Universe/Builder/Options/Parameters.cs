using System.Text.RegularExpressions;

namespace Universe.Builder.Options;

/// <summary>
/// Create a generic query string from a list of parameters. The first parameter created will be the first parameter in the where clause of the query string.
/// </summary>
/// <param name="Column">Column name</param>
/// <param name="Value">Value for the where clause associated with the Column</param>
/// <param name="Where">Where operator (eg AND / OR)</param>
/// <param name="Operator">Boolean expression operator</param>
/// <param name="Alias">Alias for the collection containing the column. Default is 'c'</param>
public readonly record struct Catalyst(
    string Column,
    object Value = null,
    Q.Where Where = Q.Where.And,
    Q.Operator Operator = Q.Operator.Eq,
    string Alias = "c")
{
    /// <summary>
    /// Unique identifier for the catalyst, used to differentiate between multiple catalysts in a query.
    /// </summary>
    public string CatalystId { get; init; } = Guid.CreateVersion7().ToString().Replace("-", string.Empty);

    /// <summary>Creates a list of rule violations when creating a CosmosDb query catalyst</summary>
    public IEnumerable<string> RuleViolations()
    {
        List<string> violations = [];

        // Column name cannot be null or empty
        if (string.IsNullOrWhiteSpace(Column))
            violations.Add("Column name is required");

        // Value should be null if using IS_DEFINED or NOT IS_DEFINED operators
        if (Value is not null && Operator is Q.Operator.Defined or Q.Operator.NotDefined)
            violations.Add("Value should not be supplied when using the Defined or NotDefined operators");

        // Value should have wildcard for Like and Not Like operators
        if (Value is not null && Operator is Q.Operator.Like or Q.Operator.NotLike)
        {
            string value = Value.ToString();
            if (string.IsNullOrWhiteSpace(value))
                violations.Add("Value is required when using the Like or NotLike operators");
            else if (!value.Contains('%'))
                violations.Add("Value should contain a wildcard (%) for Like and NotLike operators");
        }

        // Contains/NotContains operators require a collection value (not string)
        if (Operator is Q.Operator.Contains or Q.Operator.NotContains)
        {
            if (Value is null)
                violations.Add("Value is required for Contains and NotContains operators");
            else if (Value is not System.Collections.IEnumerable || Value is string)
                violations.Add("Value must be an array or collection (not a string) for Contains and NotContains operators");
        }

        // VectorDistance operator requires a value
        if (Operator is Q.Operator.VectorDistance && Value is null)
            violations.Add("Value is required when using the VectorDistance operator");

        // VectorDistance operator requires value to be a valid vector
        if (Operator is Q.Operator.VectorDistance && Value is not null)
        {
            if (Value is not float[] vector || vector.Length == 0)
                violations.Add("Value must be a non-empty array of floats for VectorDistance operator");
            else if (vector.Length > 4096)
                violations.Add("Vector dimension exceeds maximum allowed size of 4096 for VectorDistance operator");
            else if (vector.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                violations.Add("All elements in the vector must be finite numbers for VectorDistance operator");
        }

        // FullTextContains operators require a value
        if (Operator is Q.Operator.FTContains or Q.Operator.NotFTContains)
        {
            if (Value is null)
                violations.Add("Value is required for Full-text search operators");
            else if (Value is not string)
                violations.Add("Value must be a string for FullTextContains and NotFullTextContains operators");
        }

        // Other Full-text search operators require a string array as value
        if (Operator is Q.Operator.FTContainsAll or Q.Operator.NotFTContainsAll or
            Q.Operator.FTContainsAny or Q.Operator.NotFTContainsAny or
            Q.Operator.FTScore)
        {
            if (Value is null)
                violations.Add("Value is required for Full-text search operators");
            else if (Value is not string[] ftValues || ftValues.Length == 0)
                violations.Add("Value must be a non-empty array of strings for Full-text search operators");
            else if (ftValues.Any(string.IsNullOrWhiteSpace))
                violations.Add("All elements in the Full-text search value array must be non-empty strings");
        }

        // Value is required for all other operators
        if (Value is null && Operator is not Q.Operator.Defined and not Q.Operator.NotDefined)
            violations.Add("Value is required for all operators except Defined and NotDefined");

        return violations;
    }
}

/// <summary></summary>
public static class CatalystExtension
{
    /// <summary></summary>
    public static string ParameterName(this Catalyst catalyst) => $"{Regex.Replace(catalyst.Column, "[^\\w\\d]", string.Empty)}{catalyst.CatalystId}";
}