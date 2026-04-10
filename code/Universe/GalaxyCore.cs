using System.Text.Json;

namespace Universe;

/// <summary>
/// Base class for Universe implementations.
/// </summary>
public abstract class GalaxyCore : IDisposable
{
    internal readonly Container _container;

    internal readonly bool _recordQuery;
    internal readonly bool _allowBulk;
    internal readonly JsonNamingPolicy _namingPolicy;

    /// <summary>
    /// Initialize Galaxy core with Cosmos DB connection.
    /// </summary>
    /// <remarks>
    /// WARNING: Setting <paramref name="recordQueries"/> to <c>true</c> includes full query text and parameter values
    /// in Gravity responses. This may expose sensitive data (PII, filter values). Use only for debugging — never enable in production.
    /// </remarks>
    protected GalaxyCore(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey, bool recordQueries = false)
    {
        if (string.IsNullOrWhiteSpace(container))
            throw new UniverseException("Container name is required");

        if (string.IsNullOrWhiteSpace(database))
            throw new UniverseException("Database name is required");

        client.CreateDatabaseIfNotExistsAsync(database).GetAwaiter().GetResult();

        ContainerProperties containerProps = new(container, partitionKey);

        _recordQuery = recordQueries;
        if (client.ClientOptions is not null)
        {
            _allowBulk = client.ClientOptions.AllowBulkExecution;
            if (client.ClientOptions.Serializer is UniverseSerializer universeSerializer)
                _namingPolicy = universeSerializer.NamingPolicy;
        }
        _container = client.GetDatabase(database).CreateContainerIfNotExistsAsync(containerProps).GetAwaiter().GetResult();
    }

    #region Dispose Pattern

    private bool _disposedValue;

    /// <summary></summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
            return;
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