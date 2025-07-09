using System.Net;
using System.Text.Json;
using Universe.Builder;
using Universe.Response;

namespace Universe;

/// <summary>Inherit repositories to implement the very basic Universe</summary>
public class GalaxyBasic<T> : GalaxyCore, IGalaxyBasic<T> where T : class, ICosmicEntity
{
    internal readonly UniverseBuilder _qBuilder;

    /// <summary></summary>
    protected GalaxyBasic(
        CosmosClient client,
        string database,
        string container,
        IReadOnlyList<string> partitionKey,
        bool recordQueries = false) : base(client, database, container, partitionKey, recordQueries) => _qBuilder = new(_recordQuery);

    async Task<(Gravity, string)> IGalaxyBasic<T>.Create(T model)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model.id))
                model.id = Guid.NewGuid().ToString();
            model.AddedOn = DateTime.UtcNow;

            ItemResponse<T> response = await _container.CreateItemAsync(
                model,
                model.BuildPartitionKey(),
                requestOptions: new()
                {
                    EnableContentResponseOnWrite = false
                });
            return (new(response.RequestCharge, null), model.id);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new UniverseException($"{typeof(T).Name} already exists.");
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.Conflict)
        {
            throw;
        }
    }

    async Task<Gravity> IGalaxyBasic<T>.Create(IList<T> models)
    {
        try
        {
            if (!_allowBulk)
                throw new UniverseException("Bulk create of documents is not configured properly.");

            string payload = JsonSerializer.Serialize(models);
            if (Encoding.UTF8.GetByteCount(payload) > 2 * 1024 * 1024)
                throw new UniverseException("Payload size exceeds the maximum allowed size of 2MB.");

            if (models.Count > 100)
                throw new UniverseException("Bulk create can only handle up to 100 items at a time.");

            List<Task<double>> tasks = new(models.Count);

            IEnumerable<IGrouping<PartitionKey, T>> partitionGroups = models.GroupBy(m => m.BuildPartitionKey());
            foreach (IGrouping<PartitionKey, T> group in partitionGroups)
            {
                TransactionalBatch batch = _container.CreateTransactionalBatch(group.Key);

                foreach (T model in group)
                {
                    if (string.IsNullOrWhiteSpace(model.id))
                        model.id = Guid.NewGuid().ToString();
                    model.AddedOn = DateTime.UtcNow;

                    batch.CreateItem(model, requestOptions: new()
                    {
                        EnableContentResponseOnWrite = false
                    });
                }

                tasks.Add(batch.ExecuteAsync().ContinueWith(t =>
                {
                    if (!t.IsCompletedSuccessfully)
                        throw new UniverseException(t.Exception?.Flatten().InnerException?.Message ?? "Oops! Something went wrong!");

                    if (t.Result.IsSuccessStatusCode)
                        return t.Result.RequestCharge;
                    else
                        throw new UniverseException($"Transaction batch failed with status code {t.Result.StatusCode}. Message: {t.Result.ErrorMessage}");
                }));
            }

            await Task.WhenAll(tasks);
            double totalRu = tasks.Sum(t => t.Result);

            return new(totalRu, string.Empty);
        }
        catch
        {
            throw;
        }
    }

    async Task<(Gravity, T)> IGalaxyBasic<T>.Modify(T model)
    {
        try
        {
            model.ModifiedOn = DateTime.UtcNow;

            ItemResponse<T> response = await _container.ReplaceItemAsync(model, model.id, model.BuildPartitionKey());
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

            string payload = JsonSerializer.Serialize(models);
            if (Encoding.UTF8.GetByteCount(payload) > 2 * 1024 * 1024)
                throw new UniverseException("Payload size exceeds the maximum allowed size of 2MB.");

            if (models.Count > 100)
                throw new UniverseException("Bulk create can only handle up to 100 items at a time.");

            List<Task<double>> tasks = new(models.Count);

            IEnumerable<IGrouping<PartitionKey, T>> partitionGroups = models.GroupBy(m => m.BuildPartitionKey());
            foreach (IGrouping<PartitionKey, T> group in partitionGroups)
            {
                TransactionalBatch batch = _container.CreateTransactionalBatch(group.Key);

                foreach (T model in group)
                {
                    model.ModifiedOn = DateTime.UtcNow;

                    batch.ReplaceItem(model.id, model, requestOptions: new()
                    {
                        EnableContentResponseOnWrite = false
                    });
                }

                tasks.Add(batch.ExecuteAsync().ContinueWith(t =>
                {
                    if (!t.IsCompletedSuccessfully)
                        throw new UniverseException(t.Exception?.Flatten().InnerException?.Message ?? "Oops! Something went wrong!");

                    if (t.Result.IsSuccessStatusCode)
                        return t.Result.RequestCharge;
                    else
                        throw new UniverseException($"Transaction batch failed with status code {t.Result.StatusCode}. Message: {t.Result.ErrorMessage}");
                }));
            }

            await Task.WhenAll(tasks);
            double totalRu = tasks.Sum(t => t.Result);

            return new(totalRu, string.Empty);
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

    private static PartitionKey BuildPartitionKey(string[] partitionKey)
    {
        PartitionKeyBuilder partitionKeyBuilder = new();
        foreach (string key in partitionKey)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new UniverseException("Partition key cannot be null or empty.");

            partitionKeyBuilder.Add(key);
        }

        return partitionKeyBuilder.Build();
    }

    async Task<Gravity> IGalaxyBasic<T>.Remove(string id, params string[] partitionKey)
    {
        try
        {
            ItemResponse<T> response = await _container.DeleteItemAsync<T>(id, BuildPartitionKey(partitionKey), requestOptions: new()
            {
                EnableContentResponseOnWrite = false
            });
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

    async Task<Gravity> IGalaxyBasic<T>.Remove(string id, string partitionKey)
    {
        try
        {
            ItemResponse<T> response = await _container.DeleteItemAsync<T>(id, new PartitionKey(partitionKey), requestOptions: new()
            {
                EnableContentResponseOnWrite = false
            });
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

    async Task<(Gravity g, T T)> IGalaxyBasic<T>.Get(string id, params string[] partitionKey)
    {
        try
        {
            ItemResponse<T> response = await _container.ReadItemAsync<T>(id, BuildPartitionKey(partitionKey));
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

    async Task<(Gravity, T)> IGalaxyBasic<T>.Get(string id, string partitionKey)
    {
        try
        {
            ItemResponse<T> response = await _container.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
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
}