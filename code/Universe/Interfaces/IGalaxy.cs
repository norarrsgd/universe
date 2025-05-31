using Universe.Response;

namespace Universe.Interfaces;

/// <summary></summary>
public interface IGalaxy<T> : IGalaxyBasic<T> where T : ICosmicEntity
{
    /// <summary>
    /// Get one model from the database
    /// </summary>
    Task<(Gravity g, T T)> Get(IList<Cluster> clusters, IList<string> columns = null);

    /// <summary>
    /// Get one model from the database with a different type
    /// </summary>
    Task<(Gravity g, S S)> Get<S>(IList<Cluster> clusters, IList<string> columns = null) where S : ICosmicEntity;

    /// <summary>
    /// Get list from the database
    /// </summary>
    Task<(Gravity g, IList<T> T)> List(IList<Cluster> clusters, ColumnOptions? columnOptions = null, IList<Sorting.Option> sorting = null, IList<string> group = null);


    /// <summary>
    /// Get list from the database with a different type
    /// </summary>
    Task<(Gravity g, IList<S> T)> List<S>(IList<Cluster> clusters, ColumnOptions? columnOptions = null, IList<Sorting.Option> sorting = null, IList<string> group = null) where S : ICosmicEntity;

    /// <summary>
    /// Get a paginated list from the database
    /// </summary>
    Task<(Gravity g, IList<T> T)> Paged(Q.Page page, IList<Cluster> clusters, ColumnOptions? columnOptions = null, IList<Sorting.Option> sorting = null, IList<string> group = null);

    /// <summary>
    /// Get a paginated list from the database
    /// </summary>
    Task<(Gravity g, IList<S> T)> Paged<S>(Q.Page page, IList<Cluster> clusters, ColumnOptions? columnOptions = null, IList<Sorting.Option> sorting = null, IList<string> group = null) where S : ICosmicEntity;
}
