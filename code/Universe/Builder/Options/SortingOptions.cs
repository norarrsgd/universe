namespace Universe.Builder.Options;

/// <summary></summary>
public struct Sorting
{
    /// <summary></summary>
    public enum Direction
    {
        /// <summary>Ascending</summary>
        ASC,

        /// <summary>Descending</summary>
        DESC,

        /// <summary>Weighted sorting direction</summary>
        WEIGHTED
    }

    /// <summary></summary>
    public readonly record struct Option(string Column, Direction Direction = Direction.ASC);
}

/// <summary></summary>
public static class SortOptionDirectionExtension
{
    /// <summary></summary>
    public static string Value(this Sorting.Direction direction) => direction switch
    {
        Sorting.Direction.ASC => "ASC",
        Sorting.Direction.DESC => "DESC",
        Sorting.Direction.WEIGHTED => string.Empty,
        _ => throw new UniverseException("Unrecognized SORT DIRECTION keyword")
    };
}