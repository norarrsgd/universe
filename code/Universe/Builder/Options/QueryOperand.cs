namespace Universe.Builder.Options;

/// <summary>A validated field, array length, or parameter used in a comparison.</summary>
public sealed record QueryOperand
{
    internal enum OperandKind { Field, ArrayLength, Parameter }
    internal OperandKind Kind { get; }
    internal string Path { get; }
    internal string Alias { get; }
    internal object Value { get; }

    private QueryOperand(OperandKind kind, string path = null, string alias = "c", object value = null)
        => (Kind, Path, Alias, Value) = (kind, path, alias, value);

    /// <summary>Reference a document property.</summary>
    public static QueryOperand Field(string path, string alias = "c") => new(OperandKind.Field, path, alias);

    /// <summary>Reference the length of a document array.</summary>
    public static QueryOperand ArrayLength(string path, string alias = "c") => new(OperandKind.ArrayLength, path, alias);

    /// <summary>Bind a non-null value. Use IsNull for null checks.</summary>
    public static QueryOperand Parameter(object value)
        => value is null ? throw new UniverseException("A comparison parameter cannot be null. Use IsNull instead.") : new(OperandKind.Parameter, value: value);
}
