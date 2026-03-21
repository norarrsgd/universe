using Universe.Builder.Options;
using Universe.Response;

namespace Universe.Builder;

/// <summary>
/// Fluent query builder for Galaxy queries. Create via galaxy.Query() extension method.
/// </summary>
public sealed class Orbit<T> where T : class, ICosmicEntity
{
	private readonly IGalaxy<T> _galaxy;
	private readonly List<(Action<ClusterBuilder> configure, Q.Where where)> _clusterConfigs = [];
	private readonly List<string> _columns = [];
	private readonly List<Sorting.Option> _sortingOptions = [];
	private readonly List<AggregationOption> _aggregates = [];
	private readonly List<string> _groupByColumns = [];
	private Q.Page? _page;
	private int _top;
	private bool _isDistinct;
	private JoinOptions _join;
	private QueryHints? _hints;
	private Q.Where _nextClusterWhere = Q.Where.And;

	internal Orbit(IGalaxy<T> galaxy)
		=> _galaxy = galaxy;

	/// <summary>Add a cluster of filter conditions using a builder lambda.</summary>
	public Orbit<T> Cluster(Action<ClusterBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_clusterConfigs.Add((configure, _nextClusterWhere));
		_nextClusterWhere = Q.Where.And;
		return this;
	}

	/// <summary>The next cluster will be joined with OR.</summary>
	public Orbit<T> Or()
	{
		_nextClusterWhere = Q.Where.Or;
		return this;
	}

	/// <summary>The next cluster will be joined with AND.</summary>
	public Orbit<T> And()
	{
		_nextClusterWhere = Q.Where.And;
		return this;
	}

	/// <summary>Specify which columns to select.</summary>
	public Orbit<T> Select(params string[] columns)
	{
		ArgumentNullException.ThrowIfNull(columns);
		_columns.AddRange(columns);
		return this;
	}

	/// <summary>Return only distinct results.</summary>
	public Orbit<T> Distinct()
	{
		_isDistinct = true;
		return this;
	}

	/// <summary>Limit the number of results returned.</summary>
	public Orbit<T> Top(int count)
	{
		if (count < 0)
			throw new UniverseException("Top count must be a non-negative value.");
		_top = count;
		return this;
	}

	/// <summary>Add a sort order.</summary>
	public Orbit<T> OrderBy(string column, Sorting.Direction direction = Sorting.Direction.ASC, string alias = "c")
	{
		_sortingOptions.Add(new(column, direction, alias));
		return this;
	}

	/// <summary>Add descending sort order on a column.</summary>
	public Orbit<T> OrderByDescending(string column, string alias = "c")
	{
		_sortingOptions.Add(new(column, Sorting.Direction.DESC, alias));
		return this;
	}

	/// <summary>Add weighted sorting for RRF ranking.</summary>
	public Orbit<T> WithWeights(string weights)
	{
		_sortingOptions.Add(new(weights, Sorting.Direction.WEIGHTED));
		return this;
	}

	/// <summary>Add an aggregation function.</summary>
	public Orbit<T> Aggregate(string column, Q.Aggregate aggregate)
	{
		_aggregates.Add(new(column, aggregate));
		return this;
	}

	/// <summary>Add GROUP BY columns.</summary>
	public Orbit<T> GroupBy(params string[] columns)
	{
		ArgumentNullException.ThrowIfNull(columns);
		_groupByColumns.AddRange(columns);
		return this;
	}

	/// <summary>Enable pagination with specified page size.</summary>
	public Orbit<T> Paged(int size, string continuationToken = null)
	{
		_page = new Q.Page(size, continuationToken);
		return this;
	}

	/// <summary>Configure a JOIN on a sub-collection array.</summary>
	public Orbit<T> Join(string arrayPath, string alias, IReadOnlyList<string> columns = null,
		IReadOnlyList<AggregationOption> aggregates = null)
	{
		_join = new(alias, arrayPath, columns, aggregates);
		return this;
	}

	/// <summary>Provide query optimization hints.</summary>
	public Orbit<T> WithHints(QueryHints hints)
	{
		_hints = hints;
		return this;
	}

	/// <summary>Execute the query and return a list of results.</summary>
	public async Task<(Gravity g, IList<T> T)> ToListAsync()
	{
		ValidateQueryConstraints();
		(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = Build();

		if (_page.HasValue)
			return await _galaxy.Paged(_page.Value, clusters, columnOptions, sorting, groups);

		return await _galaxy.List(clusters, columnOptions, sorting, groups, _hints);
	}

	/// <summary>Execute the query and return a list of results projected to a different type.</summary>
	public async Task<(Gravity g, IList<TS> T)> ToListAsync<TS>() where TS : ICosmicEntity
	{
		ValidateQueryConstraints();
		(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = Build();

		if (_page.HasValue)
			return await _galaxy.Paged<TS>(_page.Value, clusters, columnOptions, sorting, groups);

		return await _galaxy.List<TS>(clusters, columnOptions, sorting, groups, _hints);
	}

	private void ValidateQueryConstraints()
	{
		if (_galaxy is null)
			throw new UniverseException("Cannot execute query without a Galaxy instance. Create Orbit via galaxy.Query() extension.");

		if (_page.HasValue && _hints.HasValue)
			throw new UniverseException("QueryHints are not supported with paginated queries. Use either Paged() or WithHints(), not both.");
	}

	/// <summary>Execute the query and return the first matching result.</summary>
	/// <remarks>
	/// Only filter conditions (clusters) and column selection (Select) are applied.
	/// Top, Distinct, Aggregate, GroupBy, sorting, Join, Paged, and WithHints options are ignored. Use ToListAsync for those features.
	/// </remarks>
	public async Task<(Gravity g, T T)> GetAsync()
	{
		ValidateQueryConstraints();
		(IReadOnlyList<Cluster> clusters, _, _, _) = Build();
		IReadOnlyList<string> columns = _columns.Count > 0 ? _columns : null;
		return await _galaxy.Get(clusters, columns);
	}

	/// <summary>Execute the query and return the first matching result projected to a different type.</summary>
	/// <remarks>
	/// Only filter conditions (clusters) and column selection (Select) are applied.
	/// Top, Distinct, Aggregate, GroupBy, sorting, Join, Paged, and WithHints options are ignored. Use ToListAsync for those features.
	/// </remarks>
	public async Task<(Gravity g, TS S)> GetAsync<TS>() where TS : ICosmicEntity
	{
		ValidateQueryConstraints();
		(IReadOnlyList<Cluster> clusters, _, _, _) = Build();
		IReadOnlyList<string> columns = _columns.Count > 0 ? _columns : null;
		return await _galaxy.Get<TS>(clusters, columns);
	}

	/// <summary>Generate the SQL query without executing it.</summary>
	public Gravity GenerateQuery()
	{
		ValidateQueryConstraints();
		(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) = Build();
		return _galaxy.GenerateQuery(clusters, columnOptions, sorting, groups);
	}

	internal (IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions,
		IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups) Build()
	{
		List<Cluster> clusters = [];
		foreach ((Action<ClusterBuilder> configure, Q.Where where) in _clusterConfigs)
		{
			ClusterBuilder cb = new();
			configure(cb);
			clusters.Add(cb.Build(where));
		}

		ColumnOptions? columnOptions = null;
		if (_columns.Count > 0 || _top > 0 || _isDistinct || _aggregates.Count > 0 || _join is not null)
		{
			columnOptions = new ColumnOptions(
				Names: _columns.Count > 0 ? _columns : null,
				IsDistinct: _isDistinct,
				Top: _top,
				Aggregates: _aggregates.Count > 0 ? _aggregates : null,
				Join: _join
			);
		}

		IReadOnlyList<Sorting.Option> sorting = _sortingOptions.Count > 0 ? _sortingOptions : null;
		IReadOnlyList<string> groups = _groupByColumns.Count > 0 ? _groupByColumns : null;

		return (clusters.Count > 0 ? clusters : null, columnOptions, sorting, groups);
	}
}