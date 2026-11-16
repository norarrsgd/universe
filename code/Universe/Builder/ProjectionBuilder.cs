namespace Universe.Builder;

/// <summary>Builds a structured JSON projection without accepting SQL fragments.</summary>
public sealed class ProjectionBuilder
{
    private readonly List<ProjectionItem> _items = [];

    /// <summary>Map a source property to an explicit output name.</summary>
    public ProjectionBuilder Field(string name, string column, string alias = "c")
    {
        _items.Add(new(name, column, alias));
        return this;
    }

    /// <summary>Build a nested output object.</summary>
    public ProjectionBuilder Object(string name, Action<ProjectionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ProjectionBuilder nested = new();
        configure(nested);
        _items.Add(new(name, Object: nested.Build()));
        return this;
    }

    /// <summary>Map an aggregate to an explicit output name. Count uses COUNT(1).</summary>
    public ProjectionBuilder Aggregate(string name, string column, Q.Aggregate aggregate, string alias = "c")
    {
        _items.Add(new(name, column, alias, aggregate));
        return this;
    }

    /// <summary>Capture the current projection independently of later builder changes.</summary>
    public QueryProjection Build() => new(Array.AsReadOnly(_items.ToArray()));
}
