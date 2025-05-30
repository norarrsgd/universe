using Universe.Response;

namespace Universe.Interfaces;

/// <summary></summary>
public interface IGalaxyProcedure
{
    /// <summary>
    /// Execute a stored procedure
    /// </summary>
    Task<(Gravity g, T T)> ExecSProc<T>(string procedureName, string partitionKey, params object[] parameters);
}
