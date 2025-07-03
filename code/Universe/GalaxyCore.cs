namespace Universe;

/// <summary>
/// Base class for Universe implementations.
/// </summary>
public abstract class GalaxyCore : IDisposable
{
    internal readonly Container _container;

    internal readonly bool _recordQuery;
    internal readonly bool _allowBulk;

    /// <summary></summary>
    protected GalaxyCore(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey, IReadOnlyDictionary<string, VectorIndexType> vectorPolicy = null, bool recordQueries = false)
    {
        if (string.IsNullOrWhiteSpace(container))
            throw new UniverseException("Container name is required");

        if (string.IsNullOrWhiteSpace(database))
            throw new UniverseException("Database name is required");

        client.CreateDatabaseIfNotExistsAsync(database).GetAwaiter().GetResult();

        ContainerProperties containerProps = new(container, partitionKey);

        if (vectorPolicy is not null && vectorPolicy.Any())
        {
            IndexingPolicy policy = new();
            foreach (var vp in vectorPolicy)
                policy.VectorIndexes.Add(new VectorIndexPath
                {
                    Path = $"/{vp.Key}",
                    Type = vp.Value
                });

            containerProps.IndexingPolicy = policy;
        }

        _recordQuery = recordQueries;
        if (client.ClientOptions is not null)
            _allowBulk = client.ClientOptions.AllowBulkExecution;
        _container = client.GetDatabase(database).CreateContainerIfNotExistsAsync(containerProps).GetAwaiter().GetResult();
    }

    #region Dispose Pattern
    private bool _disposedValue;

    /// <summary></summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
        }

        _disposedValue = true;
    }

    /// <summary></summary>
    ~GalaxyCore() => Dispose(disposing: false);

    /// <summary></summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}