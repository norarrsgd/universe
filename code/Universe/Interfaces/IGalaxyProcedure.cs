using Universe.Response;

namespace Universe.Interfaces;

/// <summary></summary>
public interface IGalaxyProcedure
{
    /// <summary>
    /// Execute a stored procedure
    /// </summary>
    Task<(Gravity g, T T)> ExecSProc<T>(string procedureName, string partitionKey, params object[] parameters);

    /// <summary>
    /// Create a stored procedure
    /// </summary>
    Task<Gravity> CreateSProc(string procedureName, string body);

    /// <summary>
    /// Read a stored procedure
    /// </summary>
    Task<(Gravity g, string body)> ReadSProc(string procedureName);

    /// <summary>
    /// Replace a stored procedure
    /// </summary>
    Task<Gravity> ReplaceSProc(string procedureName, string newBody);

    /// <summary>
    /// Delete a stored procedure
    /// </summary>
    Task<Gravity> DeleteSProc(string procedureName);

    /// <summary>
    /// List stored procedures (returns list of names)
    /// </summary>
    Task<(Gravity g, IList<string> names)> ListSProcs();
}
