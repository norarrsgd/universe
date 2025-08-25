using Universe.Response;
using Universe.Builder.Strategies;

namespace Universe.Interfaces;

/// <summary></summary>
public interface IGalaxy<T> : IGalaxyBasic<T> where T : ICosmicEntity
{
	/// <summary>
	/// Get one model from the database
	/// </summary>
	Task<(Gravity g, T T)> Get(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns = null);

	/// <summary>
	/// Get one model from the database with a different type
	/// </summary>
	Task<(Gravity g, S S)> Get<S>(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns = null) where S : ICosmicEntity;

	/// <summary>
	/// Get list from the database with optional query optimization hints
	/// </summary>
	Task<(Gravity g, IList<T> T)> List(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null, QueryHints? hints = null);

	/// <summary>
	/// Get list from the database with a different type
	/// </summary>
	Task<(Gravity g, IList<S> T)> List<S>(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null) where S : ICosmicEntity;

	/// <summary>
	/// Get a paginated list from the database
	/// </summary>
	Task<(Gravity g, IList<T> T)> Paged(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null);

	/// <summary>
	/// Get a paginated list from the database
	/// </summary>
	Task<(Gravity g, IList<S> T)> Paged<S>(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null) where S : ICosmicEntity;

	/// <summary>
	/// Generate SQL query without executing it
	/// </summary>
	Gravity GenerateQuery(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null);

	/// <summary>
	/// Get query execution recommendations for optimization
	/// </summary>
	QueryTuningRecommendations GetQueryRecommendations(string queryPattern, QueryType queryType);
}