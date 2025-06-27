namespace Universe.Interfaces;

/// <summary>Base interface for Cosmos Entities</summary>
public interface ICosmicEntity
{
    /// <summary>Unique GUID</summary>
#pragma warning disable IDE1006 // Naming Styles
    public string id { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    /// <summary>UTC Date document was added</summary>
    public DateTime AddedOn { get; set; }

    /// <summary>UTC Date document was modified.</summary>
    public DateTime? ModifiedOn { get; set; }

    /// <summary>Set the value for the PartitionKey field</summary>
    [Obsolete("Use 'PartitionKeyAttribute' instead. This is no longer used. Will be removed in future versions.")]
    [JsonIgnore]
    public abstract string PartitionKey { get; }

    /// <summary>Represents the count of items in a query result.</summary>
    public long CountAggregate { get; set; }
}

/// <summary>Base record for Cosmos Entities</summary>
public abstract record CosmicEntity : IDisposable, ICosmicEntity
{
    /// <inheritdoc />
    public string id { get; set; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTime AddedOn { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime? ModifiedOn { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    [Obsolete("Use 'PartitionKeyAttribute' instead. This is no longer used. Will be removed in future versions.")]
    [JsonIgnore]
    public abstract string PartitionKey { get; }

    /// <inheritdoc />
    public long CountAggregate { get; set; }

    #region Dispose Pattern
    [JsonIgnore] private bool disposedValue;
    /// <summary></summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
            }
            disposedValue = true;
        }
    }

    /// <summary></summary>
    ~CosmicEntity() => Dispose(disposing: false);

    /// <summary></summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}