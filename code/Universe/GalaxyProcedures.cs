using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos.Scripts;
using Universe.Response;

namespace Universe;

/// <summary>Inherit repositories to implement stored procedures</summary>
public abstract class GalaxyProcedure(
    CosmosClient client,
    string database,
    string container,
    IReadOnlyList<string> partitionKey,
    IReadOnlyDictionary<string, VectorIndexType> vectorPolicy = null,
    bool recordQueries = false) : GalaxyCore(client, database, container, partitionKey, vectorPolicy, recordQueries), IGalaxyProcedure
{
    async Task<(Gravity g, T T)> IGalaxyProcedure.ExecSProc<T>(string procedureName, string partitionKey, params object[] parameters)
    {
        foreach (object param in parameters)
        {
            try
            {
                JsonSerializer.Serialize(param);
            }
            catch (System.Exception ex)
            {
                throw new UniverseException($"Stored procedure parameter is not serializable: {param?.GetType().Name}", ex);
            }
        }

        try
        {
            StoredProcedureExecuteResponse<T> response = await _container.Scripts.ExecuteStoredProcedureAsync<T>(
                procedureName,
                new PartitionKey(partitionKey),
                parameters
            );
            return (new(response.RequestCharge, null), response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UniverseException($"{procedureName} does not exist.");
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    async Task<Gravity> IGalaxyProcedure.CreateSProc(string procedureName, string body)
    {
        try
        {
            StoredProcedureProperties props = new()
            {
                Id = procedureName,
                Body = body
            };
            StoredProcedureResponse response = await _container.Scripts.CreateStoredProcedureAsync(props);
            return new(response.RequestCharge, null, ($"CreateStoredProcedure: {procedureName}", null));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new UniverseException($"Stored procedure '{procedureName}' already exists.");
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.Conflict)
        {
            throw;
        }
    }

    async Task<(Gravity g, string body)> IGalaxyProcedure.ReadSProc(string procedureName)
    {
        try
        {
            StoredProcedureResponse response = await _container.Scripts.ReadStoredProcedureAsync(procedureName);
            return (new(response.RequestCharge, null, ($"ReadStoredProcedure: {procedureName}", null)), response.Resource.Body);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UniverseException($"Stored procedure '{procedureName}' does not exist.");
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    async Task<Gravity> IGalaxyProcedure.ReplaceSProc(string procedureName, string newBody)
    {
        try
        {
            StoredProcedureProperties props = new()
            {
                Id = procedureName,
                Body = newBody
            };
            StoredProcedureResponse response = await _container.Scripts.ReplaceStoredProcedureAsync(props);
            return new(response.RequestCharge, null, ($"ReplaceStoredProcedure: {procedureName}", null));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UniverseException($"Stored procedure '{procedureName}' does not exist.");
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    async Task<Gravity> IGalaxyProcedure.DeleteSProc(string procedureName)
    {
        try
        {
            StoredProcedureResponse response = await _container.Scripts.DeleteStoredProcedureAsync(procedureName);
            return new(response.RequestCharge, null, ($"DeleteStoredProcedure: {procedureName}", null));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UniverseException($"Stored procedure '{procedureName}' does not exist.");
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    async Task<(Gravity g, IList<string> names)> IGalaxyProcedure.ListSProcs()
    {
        FeedIterator<StoredProcedureProperties> iterator = _container.Scripts.GetStoredProcedureQueryIterator<StoredProcedureProperties>();
        List<string> names = [];
        double totalRU = 0;
        while (iterator.HasMoreResults)
        {
            FeedResponse<StoredProcedureProperties> response = await iterator.ReadNextAsync();
            totalRU += response.RequestCharge;
            foreach (StoredProcedureProperties sProc in response)
                names.Add(sProc.Id);
        }
        return (new(totalRU, null, ("ListStoredProcedures", null)), names);
    }
}