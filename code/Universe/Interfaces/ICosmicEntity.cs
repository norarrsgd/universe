namespace Universe.Interfaces;

/// <summary>Base interface for Cosmos Entities</summary>
public interface ICosmicEntity
{
    /// <summary>Unique GUID</summary>
#pragma warning disable IDE1006 // Naming Styles
    [JsonPropertyOrder(0)]
    public string id { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    /// <summary>UTC Date document was added</summary>
    [JsonPropertyOrder(1)]
    public DateTime AddedOn { get; set; }

    /// <summary>UTC Date document was modified.</summary>
    [JsonPropertyOrder(2)]
    public DateTime? ModifiedOn { get; set; }

    /// <summary>Represents the count of items in a query result.</summary>
    [JsonPropertyOrder(3)]
    public long CountAggregate { get; set; }
}

/// <summary>Base record for Cosmos Entities</summary>
public abstract record CosmicEntity : IDisposable, ICosmicEntity
{
    /// <inheritdoc />
    [JsonPropertyOrder(0)]
    public string id { get; set; } = Guid.CreateVersion7().ToString();

    /// <inheritdoc />
    [JsonPropertyOrder(1)]
    public DateTime AddedOn { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    [JsonPropertyOrder(2)]
    public DateTime? ModifiedOn { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    [JsonPropertyOrder(3)]
    public long CountAggregate { get; set; }

    #region Dispose Pattern

    [JsonIgnore] private bool _disposedValue;

    /// <summary></summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
            }

            _disposedValue = true;
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