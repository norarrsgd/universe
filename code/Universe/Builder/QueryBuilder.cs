using Universe.Response;

namespace Universe.Builder;

internal class UniverseBuilder(bool recordQueries)
{
    internal QueryDefinition CreateQuery(IList<Cluster> clusters, ColumnOptions? columnOptions = null, IList<Sorting.Option> sorting = null, IList<string> groups = null)
    {
        // Column Options Builder
        string columnsInQuery = "*";
        if (columnOptions is not null)
        {
            if (columnOptions.Value.Names is not null && columnOptions.Value.Names.Count > 0)
                columnsInQuery = string.Join(", ", columnOptions.Value.Names.Select(c => $"c.{c}"));

            if (columnOptions.Value.Top > 0)
                columnsInQuery = $"TOP {columnOptions.Value.Top} {columnsInQuery}";

            if (columnOptions.Value.IsDistinct && columnOptions.Value.Names is not null && columnOptions.Value.Names.Count > 0)
                columnsInQuery = $"DISTINCT {columnsInQuery}";

            if (columnOptions.Value.Aggregates is not null && columnOptions.Value.Aggregates.Count() > 0)
            {
                if (columnOptions.Value.Names is null || !columnOptions.Value.Names.Any())
                    throw new UniverseException("ColumnOption.Names must not be null or empty when using aggregates.");

                groups ??= [];
                groups = [.. groups.Concat(columnOptions.Value.Names ?? []).Distinct()];

                if (columnOptions.Value.Aggregates.Any(ag => string.IsNullOrWhiteSpace(ag.Column)))
                    throw new UniverseException("Aggregate columns must not be null or empty.");

                foreach (AggregationOption aggregate in columnOptions.Value.Aggregates)
                {
                    string toAppend = aggregate.Aggregate switch
                    {
                        Q.Aggregate.Count => Q.Aggregate.Count.Value(),
                        Q.Aggregate.Sum => string.Format(Q.Aggregate.Sum.Value(), aggregate.Column),
                        Q.Aggregate.Min => string.Format(Q.Aggregate.Min.Value(), aggregate.Column),
                        Q.Aggregate.Max => string.Format(Q.Aggregate.Max.Value(), aggregate.Column),
                        Q.Aggregate.Avg => string.Format(Q.Aggregate.Avg.Value(), aggregate.Column),
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

            if (vectorDistanceCatalysts.Count > 1)
            {
                foreach (Catalyst catalyst in vectorDistanceCatalysts)
                    columnsInQuery += $", {catalyst.Operator.Value()}(c.{catalyst.Column}, @{catalyst.ParameterName()}) AS {catalyst.Column}Score";
            }
            else if (vectorDistanceCatalysts.Count == 1)
            {
                Catalyst catalyst = vectorDistanceCatalysts.First();
                columnsInQuery += $", {catalyst.Operator.Value()}(c.{catalyst.Column}, @{catalyst.ParameterName()}) AS {catalyst.Column}Score";
            }
        }

        // This error blocks code execution since this is not yet supported by CosmosDb
        if (sorting is not null && sorting.Any() && groups is not null && groups.Any())
            throw new UniverseException("ORDER BY is not supported in presence of GROUP BY");

        // Update Columns Builder with Group By
        if (columnsInQuery.Contains('*') && groups is not null && groups.Any())
            columnsInQuery = columnsInQuery.Replace("*", string.Join(", ", groups.Select(c => $"c.{c}")));

        StringBuilder queryBuilder = new($"SELECT {columnsInQuery} FROM c");

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

                // There should be unique combination of column names and operator
                if (cluster.Catalysts.GroupBy(c => (c.Column, c.Operator)).Any(g => g.Count() > 1))
                    throw new UniverseException("Each Catalyst in a Cluster must have a unique combination of Column and Operator.");

                // ColumnOptions dependency check for VectorDistance
                if (cluster.Catalysts.Any(c => c.Operator is Q.Operator.VectorDistance) && (columnOptions == null || columnOptions.GetValueOrDefault().Top <= 0))
                    throw new UniverseException("ColumnOptions that specify a top value must be provided when using VectorDistance operator.");

                // Skip if all catalysts are of VectorDistance operator
                if (cluster.Catalysts.All(c => c.Operator is Q.Operator.VectorDistance or Q.Operator.FTScore))
                    continue;

                // Add the where statement if not yet present
                if (!whereClauseStarted)
                {
                    queryBuilder.Append(" WHERE (");
                    whereClauseStarted = true;
                }
                else queryBuilder.Append($" {cluster.Where.Value()} (");

                // Where Clause Builder
                foreach (Catalyst catalyst in cluster.Catalysts.Where(c => c.Operator is not Q.Operator.VectorDistance or Q.Operator.FTScore))
                {
                    if (cluster.Catalysts.IndexOf(catalyst) == 0)
                        queryBuilder.Append(WhereClauseBuilder(catalyst));
                    else queryBuilder.Append($" {catalyst.Where.Value()} {WhereClauseBuilder(catalyst)}");
                }

                queryBuilder.Append(')');
            }
        }

        // Sorting Builder
        if (sorting is not null && sorting.Any(s => s.Direction is not Sorting.Direction.WEIGHTED))
        {
            // Make sure that the clusters does not have VectorDistance operator
            if (clusters is not null && clusters.Any(c => c.Catalysts.Any(cat => cat.Operator is Q.Operator.VectorDistance)))
                throw new UniverseException("Sorting scalar fields is not supported in the presence of the VectorDistance operator.");

            queryBuilder.Append($" ORDER BY c.{sorting[0].Column} {sorting[0].Direction.Value()}");
            foreach (Sorting.Option sort in sorting.Where(s => s.Column != sorting[0].Column))
                queryBuilder.Append($", c.{sort.Column} {sort.Direction.Value()}");
        }
        else if (clusters is not null && clusters.Any(c => c.Catalysts.Any(cat => cat.Operator is Q.Operator.VectorDistance or Q.Operator.FTScore)))
        {
            List<Catalyst> rankCatalysts = [.. clusters.SelectMany(cluster => cluster.Catalysts).Where(catalyst => catalyst.Operator is Q.Operator.VectorDistance or Q.Operator.FTScore)];

            if (rankCatalysts.Count > 1)
            {
                queryBuilder.Append(" ORDER BY RANK RRF(");
                queryBuilder.Append(string.Join(", ", rankCatalysts.Select(c => $"{c.Operator.Value()}(c.{c.Column}, @{c.ParameterName()})")));

                if (sorting is not null && sorting.Count(s => s.Direction is Sorting.Direction.WEIGHTED) > 1)
                    throw new UniverseException("Only one WEIGHT option is allowed.");

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
                    queryBuilder.Append($" ORDER BY RANK {catalyst.Operator.Value()}(c.{catalyst.Column}, @{catalyst.ParameterName()})");
                else
                    queryBuilder.Append($" ORDER BY {catalyst.Operator.Value()}(c.{catalyst.Column}, @{catalyst.ParameterName()})");
            }
        }

        // Group By Builder
        if (groups is not null && groups.Any())
        {
            queryBuilder.Append($" GROUP BY c.{groups[0]}");
            foreach (string group in groups.Where(g => g != groups[0]))
                queryBuilder.Append($", c.{group}");
        }

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

    internal static string WhereClauseBuilder(Catalyst catalyst) => catalyst.Operator switch
    {
        Q.Operator.In or
        Q.Operator.NotIn => $"{catalyst.Operator.Value()}(c.{catalyst.Column}, @{catalyst.ParameterName()})",
        Q.Operator.Len => $"{catalyst.Operator.Value()}(c.{catalyst.Column}) = @{catalyst.ParameterName()}",
        Q.Operator.Defined or
        Q.Operator.NotDefined => $"{catalyst.Operator.Value()}(c.{catalyst.Column})",
        Q.Operator.FTContains or
        Q.Operator.NotFTContains or
        Q.Operator.FTContainsAll or
        Q.Operator.NotFTContainsAll or
        Q.Operator.FTContainsAny or
        Q.Operator.NotFTContainsAny => $"{catalyst.Operator.Value()}(c.{catalyst.Column}, @{catalyst.ParameterName()})",
        _ => $"c.{catalyst.Column} {catalyst.Operator.Value()} @{catalyst.ParameterName()}",
    };

    async internal Task<(Gravity, T)> GetOneFromQuery<T>(Container container, QueryDefinition query)
    {
        using FeedIterator<T> queryResponse = container.GetItemQueryIterator<T>(query, requestOptions: new() { MaxItemCount = Q.Limits.MaxItems });
        if (queryResponse.HasMoreResults)
        {
            FeedResponse<T> next = await queryResponse.ReadNextAsync();
            return (
                new
                (
                    RU: next.RequestCharge,
                    ContinuationToken: null,
                    Query: recordQueries ? (query.QueryText, query.GetQueryParameters()) : default
                ),
                next.Count != 0 ? next.Resource.FirstOrDefault() : default);
        }
        else return new(new(0, null), default);
    }

    async internal Task<(Gravity, IList<T>)> GetListFromQuery<T>(Container container, QueryDefinition query)
    {
        double requestCharge = 0;
        List<T> collection = [];
        using FeedIterator<T> queryResponse = container.GetItemQueryIterator<T>(query, requestOptions: new() { MaxItemCount = Q.Limits.MaxItems });
        while (queryResponse.HasMoreResults)
        {
            FeedResponse<T> next = await queryResponse.ReadNextAsync();
            collection.AddRange(next);
            requestCharge += next.RequestCharge;
        }

        return (
            new
            (
                RU: requestCharge,
                ContinuationToken: null,
                Query: recordQueries ? (query.QueryText, query.GetQueryParameters()) : default
            ), collection);
    }
}