using Universe.Response;

namespace Universe.Interfaces;

/// <summary></summary>
public interface IGalaxyBasic<T> where T : ICosmicEntity
{
    /// <summary>
    /// Create a new model in the database
    /// </summary>
    Task<(Gravity g, string t)> Create(T model);

    /// <summary>
    /// Bulk create new models in the database
    /// </summary>
    Task<Gravity> Create(IList<T> models);

    /// <summary>
    /// Modify a model in the database
    /// </summary>
    Task<(Gravity g, T T)> Modify(T model);

    /// <summary>
    /// Bulk modify models in the database
    /// </summary>
    Task<Gravity> Modify(IList<T> models);

    /// <summary>
    /// Remove one model from the database
    /// </summary>
    Task<Gravity> Remove(string id, string partitionKey);
}
