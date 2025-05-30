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
    string partitionKey,
    bool recordQueries = false) : GalaxyCore(client, database, container, partitionKey, recordQueries), IGalaxyProcedure
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
}