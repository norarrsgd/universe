using Universe.Response;

namespace Universe.Interfaces;

/// <summary>
/// Stored procedure lifecycle and execution operations.
/// </summary>
/// <remarks>
/// Stored procedure create, replace, and delete operations are administrative capabilities. Stored procedure body strings
/// must come only from trusted, version-controlled or operator-approved sources.
/// </remarks>
public interface IGalaxyProcedure
{
    /// <summary>
    /// Execute a stored procedure
    /// </summary>
    Task<(Gravity g, T T)> ExecSProc<T>(string procedureName, string partitionKey, params object[] parameters);

    /// <summary>
    /// Create a stored procedure.
    /// </summary>
    /// <remarks>
    /// Administrative operation. The <paramref name="body"/> value is raw JavaScript executed by Cosmos DB and must come
    /// only from trusted, version-controlled or operator-approved sources.
    /// </remarks>
    Task<Gravity> CreateSProc(string procedureName, string body);

    /// <summary>
    /// Read a stored procedure
    /// </summary>
    Task<(Gravity g, string body)> ReadSProc(string procedureName);

    /// <summary>
    /// Replace a stored procedure.
    /// </summary>
    /// <remarks>
    /// Administrative operation. The <paramref name="newBody"/> value is raw JavaScript executed by Cosmos DB and must come
    /// only from trusted, version-controlled or operator-approved sources.
    /// </remarks>
    Task<Gravity> ReplaceSProc(string procedureName, string newBody);

    /// <summary>
    /// Delete a stored procedure.
    /// </summary>
    /// <remarks>
    /// Administrative operation. Only trusted administrative code paths should call this method.
    /// </remarks>
    Task<Gravity> DeleteSProc(string procedureName);

    /// <summary>
    /// List stored procedures (returns list of names)
    /// </summary>
    Task<(Gravity g, IList<string> names)> ListSProcs();
}
