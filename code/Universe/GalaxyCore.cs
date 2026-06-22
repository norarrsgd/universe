using System.Text.Json;
using Universe.Builder.Caching;

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
    internal readonly string _databaseName;
    internal readonly string _containerName;
    internal readonly DocumentCacheOptions _documentCacheOptions;

    /// <summary>
    /// Initialize Galaxy core with Cosmos DB connection.
    /// </summary>
    /// <remarks>
    /// WARNING: Setting <paramref name="recordQueries"/> to <c>true</c> includes full query text and parameter values
    /// in Gravity responses. This may expose sensitive data (PII, filter values). Use only for debugging — never enable in production.
    /// </remarks>
    protected GalaxyCore(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey, bool recordQueries = false)
        : this(client, database, container, partitionKey, recordQueries, autoProvisionContainers: true)
    {
    }

    /// <summary>
    /// Initialize Galaxy core with Cosmos DB connection and custom Universe options.
    /// </summary>
    /// <remarks>
    /// WARNING: Setting <paramref name="recordQueries"/> to <c>true</c> includes full query text and parameter values
    /// in Gravity responses. This may expose sensitive data (PII, filter values). Use only for debugging — never enable in production.
    /// </remarks>
    protected GalaxyCore(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey, UniverseOptions options, bool recordQueries = false)
        : this(client, database, container, partitionKey, recordQueries, RequireOptions(options))
    {
    }

    private static UniverseOptions RequireOptions(UniverseOptions options) =>
        options ?? throw new UniverseException("Universe options are required.");

    private GalaxyCore(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey, bool recordQueries, bool autoProvisionContainers)
        : this(client, database, container, partitionKey, recordQueries, autoProvisionContainers, documentCacheOptions: null)
    {
    }

    private GalaxyCore(CosmosClient client, string database, string container, IReadOnlyList<string> partitionKey, bool recordQueries, UniverseOptions options)
        : this(client, database, container, partitionKey, recordQueries, options.AutoProvisionContainers, options.DocumentCache)
    {
    }

    private GalaxyCore(
        CosmosClient client,
        string database,
        string container,
        IReadOnlyList<string> partitionKey,
        bool recordQueries,
        bool autoProvisionContainers,
        DocumentCacheOptions documentCacheOptions)
    {
        if (string.IsNullOrWhiteSpace(container))
            throw new UniverseException("Container name is required");

        if (string.IsNullOrWhiteSpace(database))
            throw new UniverseException("Database name is required");

        _recordQuery = recordQueries;
        _databaseName = database;
        _containerName = container;
        _documentCacheOptions = documentCacheOptions;
        if (client.ClientOptions is not null)
        {
            _allowBulk = client.ClientOptions.AllowBulkExecution;
            if (client.ClientOptions.Serializer is UniverseSerializer universeSerializer)
                _namingPolicy = universeSerializer.NamingPolicy;
        }

        if (autoProvisionContainers)
        {
            client.CreateDatabaseIfNotExistsAsync(database).GetAwaiter().GetResult();

            ContainerProperties containerProps = new(container, partitionKey);
            _container = client.GetDatabase(database).CreateContainerIfNotExistsAsync(containerProps).GetAwaiter().GetResult();
        }
        else
        {
            _container = client.GetContainer(database, container);
        }
    }

    internal DocumentCache DocumentCache => _documentCacheOptions?.Cache;

    internal string DocumentCacheScopeHash(Type sourceType)
        => DocumentCache.CreateScopeHash(_databaseName, _containerName, sourceType);

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
