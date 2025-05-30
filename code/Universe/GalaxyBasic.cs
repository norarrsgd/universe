using System.Net;
using Universe.Builder;
using Universe.Response;

namespace Universe;

/// <summary>Inherit repositories to implement the very basic Universe</summary>
public class GalaxyBasic<T> : IDisposable, IGalaxyBasic<T> where T : class, ICosmicEntity
{
    internal readonly Container _container;
    internal readonly UniverseBuilder<T> _qBuilder;

    internal readonly bool _recordQuery;
    internal readonly bool _allowBulk;

    /// <summary></summary>
    protected GalaxyBasic(CosmosClient client, string database, string container, string partitionKey, bool recordQueries = false)
    {
        if (string.IsNullOrWhiteSpace(container) || string.IsNullOrWhiteSpace(partitionKey))
            throw new UniverseException("Container name and PartitionKey are required");

        _recordQuery = recordQueries;
        if (client.ClientOptions is not null)
            _allowBulk = client.ClientOptions.AllowBulkExecution;
        _container = client.GetDatabase(database).CreateContainerIfNotExistsAsync(container, partitionKey).GetAwaiter().GetResult();

        _qBuilder = new(_recordQuery);
    }

    async Task<(Gravity, string)> IGalaxyBasic<T>.Create(T model)
    {
        if (string.IsNullOrWhiteSpace(model.id))
            model.id = Guid.NewGuid().ToString();
        model.AddedOn = DateTime.UtcNow;

        ItemResponse<T> response = await _container.CreateItemAsync(model, new PartitionKey(model.PartitionKey));
        return (new(response.RequestCharge, null), model.id);
    }

    async Task<Gravity> IGalaxyBasic<T>.Create(IList<T> models)
    {
        if (!_allowBulk)
            throw new UniverseException("Bulk create of documents is not configured properly.");

        Gravity gravity = new(0, string.Empty);
        List<Task> tasks = new(models.Count);

        foreach (T model in models)
        {
            if (string.IsNullOrWhiteSpace(model.id))
                model.id = Guid.NewGuid().ToString();
            model.AddedOn = DateTime.UtcNow;

            tasks.Add(_container.CreateItemAsync(model, new PartitionKey(model.PartitionKey))
                .ContinueWith(response => gravity = new(gravity.RU + response.Result.RequestCharge, string.Empty)));
        }

        await Task.WhenAll(tasks);
        return gravity;
    }

    async Task<(Gravity, T)> IGalaxyBasic<T>.Modify(T model)
    {
        try
        {
            model.ModifiedOn = DateTime.UtcNow;

            ItemResponse<T> response = await _container.ReplaceItemAsync(model, model.id, new PartitionKey(model.PartitionKey));
            return (new(response.RequestCharge, null), response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UniverseException($"{typeof(T).Name} does not exist.");
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    async Task<Gravity> IGalaxyBasic<T>.Modify(IList<T> models)
    {
        try
        {
            if (!_allowBulk)
                throw new UniverseException("Bulk modify of documents is not configured properly.");

            Gravity gravity = new(0, string.Empty);
            List<Task> tasks = new(models.Count);

            foreach (T model in models)
            {
                model.ModifiedOn = DateTime.UtcNow;

                tasks.Add(_container.ReplaceItemAsync(model, model.id, new PartitionKey(model.PartitionKey))
                    .ContinueWith(response =>
                    {
                        if (!response.IsCompletedSuccessfully)
                            throw new UniverseException(response.Exception?.Flatten().InnerException?.Message ?? "Oops! Something went wrong!");

                        gravity = new(gravity.RU + response.Result.RequestCharge, string.Empty);
                    }));
            }

            await Task.WhenAll(tasks);
            return gravity;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UniverseException($"Something went wrong doing the bulk operation. See error: {ex.Message}");
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    async Task<Gravity> IGalaxyBasic<T>.Remove(string id, string partitionKey)
    {
        try
        {
            ItemResponse<T> response = await _container.DeleteItemAsync<T>(id, new PartitionKey(partitionKey));
            return new(response.RequestCharge, null);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UniverseException($"{typeof(T).Name} does not exist.");
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
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
    ~GalaxyBasic() => Dispose(disposing: false);

    /// <summary></summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}