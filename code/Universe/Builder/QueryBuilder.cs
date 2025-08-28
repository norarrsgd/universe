using Universe.Response;
using Universe.Builder.Strategies;
using System.Diagnostics;

namespace Universe.Builder;

internal class UniverseBuilder(bool recordQueries)
{
	private readonly QueryTuner _queryTuner = new();
	private readonly QueryStrategySelector _strategySelector = new(new());

	public UniverseBuilder() : this(false)
	{
	}

	public UniverseBuilder(bool recordQueries, QueryTuner queryTuner) : this(recordQueries)
	{
		_queryTuner = queryTuner;
		_strategySelector = new(_queryTuner);
	}

	internal QueryDefinition CreateQuery(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> groups = null)
	{
		// Column Options Builder
		string columnsInQuery = "*";
		if (columnOptions is not null)
		{
			if (columnOptions.Value.Names is not null && columnOptions.Value.Names.Count > 0)
				columnsInQuery = string.Join(", ", columnOptions.Value.Names.Select(c => $"c.{c}"));

			if (columnOptions.Value.Top > 0)
				columnsInQuery = $"TOP {columnOptions.Value.Top} {columnsInQuery}";

			if (columnOptions.Value is { IsDistinct: true, Names.Count: > 0 })
				columnsInQuery = $"DISTINCT {columnsInQuery}";

			if (columnOptions.Value.Aggregates is not null && columnOptions.Value.Aggregates.Any())
			{
				if (columnOptions.Value.Names is null || !columnOptions.Value.Names.Any())
					throw new UniverseException("ColumnOption.Names must not be null or empty when using aggregates.");

				groups ??= [];
				List<string> formattedGroup = [];
				foreach (string group in groups)
				{
					if (!group.StartsWith("c."))
						formattedGroup.Add($"c.{group}");
					else
						formattedGroup.Add(group);
				}

				groups = [.. formattedGroup.Distinct()];

				groups = [.. groups.Concat(columnOptions.Value.Names.Select(n => $"c.{n}")).Distinct()];

				if (columnOptions.Value.Aggregates.Any(ag => string.IsNullOrWhiteSpace(ag.Column)))
					throw new UniverseException("Aggregate columns must not be null or empty.");

				foreach (AggregationOption aggregate in columnOptions.Value.Aggregates)
				{
					string toAppend = aggregate.Aggregate switch
					{
						Q.Aggregate.Count => Q.Aggregate.Count.Value(),
						Q.Aggregate.Sum => string.Format(Q.Aggregate.Sum.Value(), "c", aggregate.Column),
						Q.Aggregate.Min => string.Format(Q.Aggregate.Min.Value(), "c", aggregate.Column),
						Q.Aggregate.Max => string.Format(Q.Aggregate.Max.Value(), "c", aggregate.Column),
						Q.Aggregate.Avg => string.Format(Q.Aggregate.Avg.Value(), "c", aggregate.Column),
						_ => throw new UniverseException($"Unrecognized aggregate function: {aggregate.Aggregate}")
					};

					if (columnsInQuery.Contains(toAppend))
						continue;

					columnsInQuery = string.IsNullOrWhiteSpace(columnsInQuery)
						? toAppend
						: $"{columnsInQuery}, {toAppend}";
				}

				groups = [.. groups.Distinct()];
			}
		}

		// Append VectorDistance to columnsInQuery if present
		if (clusters is not null && clusters.Any(c => c.Catalysts.Any(cat => cat.Operator is Q.Operator.VectorDistance)))
		{
			List<Catalyst> vectorDistanceCatalysts = [.. clusters.SelectMany(cluster => cluster.Catalysts).Where(catalyst => catalyst.Operator is Q.Operator.VectorDistance)];

			switch (vectorDistanceCatalysts.Count)
			{
				case > 1:
					columnsInQuery = vectorDistanceCatalysts.Aggregate(columnsInQuery, (current, catalyst)
						=> $"{current}, {catalyst.Operator.Value()}({catalyst.Alias}.{catalyst.Column}, @{catalyst.ParameterName()}) AS {catalyst.Column}Score{(vectorDistanceCatalysts.IndexOf(catalyst) > 0 ? catalyst.CatalystId[^8..] : string.Empty)}");
					break;
				case 1:
				{
					Catalyst catalyst = vectorDistanceCatalysts.First();
					columnsInQuery += $", {catalyst.Operator.Value()}({catalyst.Alias}.{catalyst.Column}, @{catalyst.ParameterName()}) AS {catalyst.Column}Score";
					break;
				}
			}
		}

		// Update Columns Builder with Group By
		if (columnsInQuery.Contains('*') && groups is not null && groups.Any())
			columnsInQuery = columnsInQuery.Replace("*", string.Join(", ", groups));

		StringBuilder queryBuilder = new($"SELECT {columnsInQuery} FROM c");

		if (columnOptions?.Join is not null)
		{
			JoinOptions join = columnOptions.Value.Join;
			queryBuilder = new($"SELECT {columnsInQuery} FROM c JOIN {join.Alias} IN c.{join.ArrayPath}");

			// Add join columns to the select if specified
			if (join.Columns?.Any() == true)
			{
				string joinColumns = string.Join(", ", join.Columns.Select(col => $"{join.Alias}.{col}"));
				columnsInQuery = columnsInQuery == "*" ? $"c.*, {joinColumns}" : $"{columnsInQuery}, {joinColumns}";
				queryBuilder = new($"SELECT {columnsInQuery} FROM c JOIN {join.Alias} IN c.{join.ArrayPath}");
			}

			// Handle join aggregates
			if (join.Aggregates?.Any() == true)
			{
				groups ??= [];

				foreach (AggregationOption aggregate in join.Aggregates)
				{
					string toAppend = aggregate.Aggregate switch
					{
						Q.Aggregate.Count => Q.Aggregate.Count.Value(),
						Q.Aggregate.Sum => string.Format(Q.Aggregate.Sum.Value(), join.Alias, aggregate.Column),
						Q.Aggregate.Min => string.Format(Q.Aggregate.Min.Value(), join.Alias, aggregate.Column),
						Q.Aggregate.Max => string.Format(Q.Aggregate.Max.Value(), join.Alias, aggregate.Column),
						Q.Aggregate.Avg => string.Format(Q.Aggregate.Avg.Value(), join.Alias, aggregate.Column),
						_ => throw new UniverseException($"Unrecognized aggregate function: {aggregate.Aggregate}")
					};

					// Only add if not already present
					if (!columnsInQuery.Contains(toAppend))
						columnsInQuery = string.IsNullOrWhiteSpace(columnsInQuery) ? toAppend : $"{columnsInQuery}, {toAppend}";
				}

				// Add join columns to GROUP BY
				if (join.Columns?.Any() == true)
					groups = [.. groups.Concat(join.Columns.Distinct().Select(col => $"{join.Alias}.{col}"))];

				queryBuilder = new($"SELECT {columnsInQuery} FROM c JOIN {join.Alias} IN c.{join.ArrayPath}");
			}
		}

		// Validate Clusters
		if (clusters is not null && clusters.Any(c => c.Catalysts is null || !c.Catalysts.Any()))
			throw new UniverseException("Catalysts inside of a Cluster must not be null or empty.");

		// Construct Where Clause by Clusters
		if (clusters is not null)
		{
			bool whereClauseStarted = false;
			foreach (Cluster cluster in clusters.Where(cs => cs.Catalysts.Any()))
			{
				// Validate Catalysts
				if (cluster.Catalysts.Any(c => c.RuleViolations().Any()))
				{
					List<IEnumerable<string>> violationsPerCatalyst = [.. cluster.Catalysts.Select(c => c.RuleViolations())];
					List<string> violations = [.. violationsPerCatalyst.SelectMany(v => v).Distinct()];
					throw new UniverseException(string.Join(Environment.NewLine, violations));
				}

				// ColumnOptions dependency check for VectorDistance or FTScore
				if (cluster.Catalysts.Any(c => c.Operator is Q.Operator.VectorDistance or Q.Operator.FTScore) && (columnOptions == null || columnOptions.GetValueOrDefault().Top <= 0))
					throw new UniverseException("ColumnOptions that specify a top value must be provided when using VectorDistance operator.");

				// Skip if all catalysts are of VectorDistance or FTScore operator
				if (cluster.Catalysts.All(c => c.Operator is Q.Operator.VectorDistance or Q.Operator.FTScore))
					continue;

				// Add the where statement if not yet present
				if (!whereClauseStarted)
				{
					queryBuilder.Append(" WHERE (");
					whereClauseStarted = true;
				}
				else
					queryBuilder.Append($" {cluster.Where.Value()} (");

				// Where Clause Builder
				foreach (Catalyst catalyst in cluster.Catalysts.Where(c => c.Operator is not Q.Operator.VectorDistance && c.Operator is not Q.Operator.FTScore))
				{
					if (cluster.Catalysts.ToList().IndexOf(catalyst) == 0)
						queryBuilder.Append(WhereClauseBuilder(catalyst));
					else
						queryBuilder.Append($" {catalyst.Where.Value()} {WhereClauseBuilder(catalyst)}");
				}

				queryBuilder.Append(')');
			}
		}

		// Sorting Builder
		if (sorting is not null && sorting.Any(s => s.Direction is not Sorting.Direction.WEIGHTED))
		{
			// Make sure that the clusters does not have VectorDistance or FTScore operator
			if (clusters is not null && clusters.Any(c => c.Catalysts.Any(cat => cat.Operator is Q.Operator.VectorDistance || cat.Operator is Q.Operator.FTScore)))
				throw new UniverseException("Sorting scalar fields is not supported in the presence of rank catalysts.");

			queryBuilder.Append($" ORDER BY c.{sorting[0].Column} {sorting[0].Direction.Value()}");
			foreach (Sorting.Option sort in sorting.Where(s => s.Column != sorting[0].Column))
				queryBuilder.Append($", c.{sort.Column} {sort.Direction.Value()}");
		}
		else if (clusters is not null && clusters.Any(c => c.Catalysts.Any(cat => cat.Operator is Q.Operator.VectorDistance || cat.Operator is Q.Operator.FTScore)))
		{
			List<Catalyst> rankCatalysts = [.. clusters.SelectMany(cluster => cluster.Catalysts).Where(catalyst => catalyst.Operator is Q.Operator.VectorDistance || catalyst.Operator is Q.Operator.FTScore)];

			if (rankCatalysts.Count > 1)
			{
				queryBuilder.Append(" ORDER BY RANK RRF(");

				foreach (Catalyst catalyst in rankCatalysts)
				{
					if (rankCatalysts.IndexOf(catalyst) > 0)
						queryBuilder.Append(", ");

					if (catalyst.Operator is Q.Operator.VectorDistance)
						queryBuilder.Append($"{catalyst.Operator.Value()}(c.{catalyst.Column}, @{catalyst.ParameterName()})");
					else if (catalyst.Operator is Q.Operator.FTScore)
					{
						queryBuilder.Append($"{catalyst.Operator.Value()}(c.{catalyst.Column}, ");
						if (catalyst.Value is IEnumerable<string> stringVals)
							queryBuilder.Append($"{string.Join(", ", stringVals.Select(v => $"'{v}'"))}");
						else
							throw new UniverseException("FullTextScore operator requires a string[] value.");
						queryBuilder.Append(')');
					}
				}

				if (sorting is not null && sorting.Count(s => s.Direction is Sorting.Direction.WEIGHTED) > 1)
					throw new UniverseException("Only one WEIGHT option is allowed.");

				if (sorting is not null && sorting.Any(s => s.Direction is not Sorting.Direction.WEIGHTED))
					throw new UniverseException("Sorting fields is not supported in the presence of multiple rank catalysts.");

				if (sorting is not null)
				{
					foreach (Sorting.Option sort in sorting.Where(s => s.Direction is Sorting.Direction.WEIGHTED))
					{
						string weightValue = sort.Column;
						if (!weightValue.StartsWith('['))
							weightValue = $"[{weightValue}";
						if (!weightValue.EndsWith(']'))
							weightValue = $"{weightValue}]";

						queryBuilder.Append($", {weightValue}");
					}
				}

				queryBuilder.Append(')');
			}
			else if (rankCatalysts.Count == 1)
			{
				Catalyst catalyst = rankCatalysts.First();
				if (catalyst.Operator is Q.Operator.FTScore)
				{
					queryBuilder.Append($" ORDER BY RANK {catalyst.Operator.Value()}(c.{catalyst.Column}, ");
					if (catalyst.Value is IEnumerable<string> stringVals)
						queryBuilder.Append($"{string.Join(", ", stringVals.Select(v => $"'{v}'"))}");

					queryBuilder.Append(')');
				}
				else
					queryBuilder.Append($" ORDER BY {catalyst.Operator.Value()}(c.{catalyst.Column}, @{catalyst.ParameterName()})");
			}
		}

		// Group By Builder
		if (groups is not null && groups.Any())
		{
			queryBuilder.Append($" GROUP BY {groups[0]}");
			foreach (string group in groups.Where(g => g != groups[0]))
				queryBuilder.Append($", {group}");
		}

		// This error blocks code execution since this is not yet supported by CosmosDb
		// if (queryBuilder.ToString().Contains("ORDER BY") && groups is not null && groups.Any())
		// 	throw new UniverseException("ORDER BY is not supported in presence of GROUP BY");

		// Parameters Builder
		QueryDefinition query = new(queryBuilder.ToString());

		if (clusters is not null && clusters.Any())
		{
			query = clusters.SelectMany(cluster => cluster.Catalysts)
				.Aggregate(query, (current, catalyst) =>
					current.WithParameter($"@{catalyst.ParameterName()}", catalyst.Value));
		}

		return query;
	}

	private static string WhereClauseBuilder(Catalyst catalyst)
	{
		string alias = catalyst.Alias ?? "c";
		return catalyst.Operator switch
		{
			Q.Operator.In or
				Q.Operator.NotIn => $"{catalyst.Operator.Value()}({alias}.{catalyst.Column}, @{catalyst.ParameterName()})",
			Q.Operator.Len => $"{catalyst.Operator.Value()}({alias}.{catalyst.Column}) = @{catalyst.ParameterName()}",
			Q.Operator.Defined or
				Q.Operator.NotDefined => $"{catalyst.Operator.Value()}({alias}.{catalyst.Column})",
			Q.Operator.FTContains or
				Q.Operator.NotFTContains or
				Q.Operator.FTContainsAll or
				Q.Operator.NotFTContainsAll or
				Q.Operator.FTContainsAny or
				Q.Operator.NotFTContainsAny => $"{catalyst.Operator.Value()}({alias}.{catalyst.Column}, @{catalyst.ParameterName()})",
			_ => $"{alias}.{catalyst.Column} {catalyst.Operator.Value()} @{catalyst.ParameterName()}",
		};
	}

	internal async Task<(Gravity, T)> GetOneFromQuery<T>(Container container, QueryDefinition query, QueryContext? context = null)
	{
		context ??= InferQueryContext(query);
		QueryContext singleContext = context.Value with { MaxItemCount = 1 };

		IQueryExecutionStrategy strategy = _strategySelector.SelectStrategy(query, singleContext);
		Stopwatch stopwatch = Stopwatch.StartNew();

		try
		{
			(Gravity gravity, IList<T> results) = await strategy.ExecuteAsync<T>(container, query, singleContext, recordQueries);
			stopwatch.Stop();

			// Record successful execution
			_queryTuner.RecordExecution(new(
				QueryHash: ComputeQueryHash(query.QueryText),
				Strategy: strategy.Name,
				RequestUnits: gravity.RU,
				ExecutionTime: stopwatch.Elapsed,
				ResultCount: results.Count,
				ExecutedAt: DateTime.UtcNow,
				QueryType: singleContext.Type,
				WasSuccessful: true));

			T result = results.Count != 0 ? results.First() : default(T);
			return (gravity, result);
		}
		catch (SystemException ex)
		{
			stopwatch.Stop();

			// Record failed execution
			_queryTuner.RecordExecution(new(
				QueryHash: ComputeQueryHash(query.QueryText),
				Strategy: strategy.Name,
				RequestUnits: 0,
				ExecutionTime: stopwatch.Elapsed,
				ResultCount: 0,
				ExecutedAt: DateTime.UtcNow,
				QueryType: singleContext.Type,
				WasSuccessful: false,
				ErrorMessage: ex.Message));

			throw;
		}
	}

	internal async Task<(Gravity, IList<T>)> GetListFromQuery<T>(Container container, QueryDefinition query, QueryContext? context = null)
	{
		context ??= InferQueryContext(query);

		IQueryExecutionStrategy strategy = _strategySelector.SelectStrategy(query, context.Value);
		Stopwatch stopwatch = Stopwatch.StartNew();

		try
		{
			(Gravity gravity, IList<T> results) = await strategy.ExecuteAsync<T>(container, query, context.Value, recordQueries);
			stopwatch.Stop();

			// Record successful execution
			_queryTuner.RecordExecution(new(
				QueryHash: ComputeQueryHash(query.QueryText),
				Strategy: strategy.Name,
				RequestUnits: gravity.RU,
				ExecutionTime: stopwatch.Elapsed,
				ResultCount: results.Count,
				ExecutedAt: DateTime.UtcNow,
				QueryType: context.Value.Type,
				WasSuccessful: true));

			return (gravity, results);
		}
		catch (SystemException ex)
		{
			stopwatch.Stop();

			// Record failed execution
			_queryTuner.RecordExecution(new(
				QueryHash: ComputeQueryHash(query.QueryText),
				Strategy: strategy.Name,
				RequestUnits: 0,
				ExecutionTime: stopwatch.Elapsed,
				ResultCount: 0,
				ExecutedAt: DateTime.UtcNow,
				QueryType: context.Value.Type,
				WasSuccessful: false,
				ErrorMessage: ex.Message));

			throw;
		}
	}

	internal QueryTuningRecommendations GetQueryRecommendations(string queryPattern, QueryType queryType) => _queryTuner.GetRecommendations(queryPattern, queryType);

	private static QueryContext InferQueryContext(QueryDefinition query)
	{
		string queryText = query.QueryText.ToUpperInvariant();

		QueryType type = QueryType.Simple;

		if (queryText.Contains(Q.Operator.VectorDistance.Value().ToUpperInvariant()) && queryText.Contains(Q.Operator.FTScore.Value().ToUpperInvariant()))
			type = QueryType.HybridSearch;
		else if (queryText.Contains(Q.Operator.VectorDistance.Value().ToUpperInvariant()))
			type = QueryType.VectorSearch;
		else if (queryText.Contains(Q.Operator.FTScore.Value().ToUpperInvariant()))
			type = QueryType.FullTextSearch;
		else if (queryText.Contains("GROUP BY") || queryText.Contains("COUNT") || queryText.Contains("SUM"))
			type = QueryType.Aggregation;
		else if (queryText.Contains("JOIN"))
			type = QueryType.Join;
		else if (queryText.Contains("RRF") || queryText.Split(' ').Length > 20)
			type = QueryType.Complex;

		return new(type);
	}

	private static string ComputeQueryHash(string queryText)
	{
		using System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create();
		byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(queryText));
		return Convert.ToHexString(hashBytes)[..16];
	}
}