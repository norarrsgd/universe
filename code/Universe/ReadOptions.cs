namespace Universe;

/// <summary>Per-operation read consistency. An explicit level bypasses document caching.</summary>
public sealed record ReadOptions(ConsistencyLevel? ConsistencyLevel = null)
{
    internal void Validate()
    {
        if (ConsistencyLevel.HasValue && !Enum.IsDefined(ConsistencyLevel.Value))
            throw new UniverseException("Unrecognized read consistency level.");
    }
}
