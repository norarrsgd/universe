using Universe.Response;
using Universe.Builder.Options;

namespace Universe.Builder;

internal class UniverseBuilder<T>(bool recordQueries) where T : class, ICosmicEntity
{
    internal QueryDefinition CreateQuery(IList<Cluster> clusters, ColumnOptions? columnOptions = null, IList<Sorting.Option> sorting = null, IList<string> groups = null)
    {
        // Column Options Builder
        string columnsInQuery = "*";
        if (columnOptions is not null)
        {
            if (columnOptions.Value.Names is not null && columnOptions.Value.Names.Count > 0)
                columnsInQuery = string.Join(", ", columnOptions.Value.Names.Select(c => $"c.{c}").ToList());

            if (columnOptions.Value.Top > 0)
                columnsInQuery = $"TOP {columnOptions.Value.Top} {columnsInQuery}";

            if (columnOptions.Value.IsDistinct && columnOptions.Value.Names is not null && columnOptions.Value.Names.Count > 0)
                columnsInQuery = $"DISTINCT {columnsInQuery}";

            if (columnOptions.Value.Aggregates is not null && columnOptions.Value.Aggregates.Count != 0)
            {
                groups ??= [];
                groups = [.. groups.Concat(columnOptions.Value.Names ?? []).Distinct()];

                if (columnOptions.Value.Aggregates.Any(ag => string.IsNullOrWhiteSpace(ag.Key)))
                    throw new UniverseException("Aggregate keys must not be null or empty.");

                foreach (KeyValuePair<string, Q.Aggregate> aggregate in columnOptions.Value.Aggregates)
                {
                    string toAppend = aggregate.Value switch
                    {
                        Q.Aggregate.Count => Q.Aggregate.Count.Value(),
                        Q.Aggregate.Sum => string.Format(Q.Aggregate.Sum.Value(), aggregate.Key),
                        Q.Aggregate.Min => string.Format(Q.Aggregate.Min.Value(), aggregate.Key),
                        Q.Aggregate.Max => string.Format(Q.Aggregate.Max.Value(), aggregate.Key),
                        Q.Aggregate.Avg => string.Format(Q.Aggregate.Avg.Value(), aggregate.Key),
                        _ => throw new UniverseException($"Unrecognized aggregate function: {aggregate.Value}")
                    };

                    columnsInQuery = string.IsNullOrWhiteSpace(columnsInQuery)
                        ? toAppend
                        : $"{columnsInQuery}, {toAppend}";
                }

                groups = [.. groups.Distinct()];
            }
        }

        // This error blocks code execution since this is not yet supported by CosmosDb
        if (sorting is not null && sorting.Any() && groups is not null && groups.Any())
            throw new UniverseException("ORDER BY is not supported in presence of GROUP BY");

        // Update Columns Builder with Group By
        if (columnsInQuery.Contains('*') && groups is not null && groups.Any())
            _ = columnsInQuery.Replace("*", string.Join(", ", groups.Select(c => $"c.{c}").ToList()));

        StringBuilder queryBuilder = new($"SELECT {columnsInQuery} FROM c");

        // Validate Clusters
        if (clusters is not null && clusters.Any(c => c.Catalysts is null || !c.Catalysts.Any()))
            throw new UniverseException("Catalysts inside of a Cluster must not be null or empty.");

        // Construct Where Clause by Clusters
        if (clusters is not null)
        {
            foreach (Cluster cluster in clusters)
            {
                // Validate Catalysts
                if (cluster.Catalysts.Any(c => c.RuleViolations().Any()))
                {
                    List<IEnumerable<string>> violationsPerCatalyst = [.. cluster.Catalysts.Select(c => c.RuleViolations())];
                    List<string> violations = [.. violationsPerCatalyst.SelectMany(v => v).ToList().Distinct()];
                    throw new UniverseException(string.Join(Environment.NewLine, violations));
                }

                // Add the where statement if not yet present
                if (clusters.IndexOf(cluster) == 0)
                    queryBuilder.Append(" WHERE (");
                else queryBuilder.Append($" {cluster.Where.Value()} (");

                // Where Clause Builder
                foreach (Catalyst catalyst in cluster.Catalysts)
                {
                    if (cluster.Catalysts.IndexOf(catalyst) == 0)
                        queryBuilder.Append(WhereClauseBuilder(catalyst));
                    else queryBuilder.Append($" {catalyst.Where.Value()} {WhereClauseBuilder(catalyst)}");
                }

                queryBuilder.Append(')');
            }
        }

        // Sorting Builder
        if (sorting is not null && sorting.Any())
        {
            queryBuilder.Append($" ORDER BY c.{sorting[0].Column} {sorting[0].Direction.Value()}");
            foreach (Sorting.Option sort in sorting.Where(s => s.Column != sorting[0].Column).ToList())
                queryBuilder.Append($", c.{sort.Column} {sort.Direction.Value()}");
        }

        // Group By Builder
        if (groups is not null && groups.Any())
        {
            queryBuilder.Append($" GROUP BY c.{groups[0]}");
            foreach (string group in groups.Where(g => g != groups[0]).ToList())
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

    internal string WhereClauseBuilder(Catalyst catalyst) => catalyst.Operator switch
    {
        Q.Operator.In => $"ARRAY_CONTAINS(c.{catalyst.Column}, @{catalyst.ParameterName()})",
        Q.Operator.NotIn => $"NOT ARRAY_CONTAINS(c.{catalyst.Column}, @{catalyst.ParameterName()})",
        Q.Operator.Defined => $"IS_DEFINED(c.{catalyst.Column})",
        Q.Operator.NotDefined => $"NOT IS_DEFINED(c.{catalyst.Column})",
        _ => $"c.{catalyst.Column} {catalyst.Operator.Value()} @{catalyst.ParameterName()}",
    };

    async internal Task<(Gravity, T)> GetOneFromQuery(Container container, QueryDefinition query)
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

    async internal Task<(Gravity, IList<T>)> GetListFromQuery(Container container, QueryDefinition query)
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