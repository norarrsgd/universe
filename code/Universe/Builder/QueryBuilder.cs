using System.Text.Json;
using Universe.Response;
using Universe.Builder.Strategies;

namespace Universe.Builder;

internal class UniverseBuilder : IDisposable
{
    private static readonly HashSet<string> ReservedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND",
        "AS",
        "ASC",
        "BETWEEN",
        "BY",
        "DESC",
        "DISTINCT",
        "FROM",
        "GROUP",
        "IN",
        "JOIN",
        "NOT",
        "OR",
        "ORDER",
        "RANK",
        "SELECT",
        "TOP",
        "WHERE"
    };

    private readonly bool _recordQueries;
    private readonly QueryTuner _queryTuner;
    private readonly QueryStrategySelector _strategySelector;
    private readonly JsonNamingPolicy _namingPolicy;

    public UniverseBuilder() : this(false)
    {
    }

    public UniverseBuilder(bool recordQueries, JsonNamingPolicy namingPolicy = null)
    {
        _recordQueries = recordQueries;
        _namingPolicy = namingPolicy;
        _queryTuner = new();
        _strategySelector = new(_queryTuner);
    }

    public UniverseBuilder(bool recordQueries, QueryTuner queryTuner, JsonNamingPolicy namingPolicy = null)
    {
        _recordQueries = recordQueries;
        _namingPolicy = namingPolicy;
        _queryTuner = queryTuner;
        _strategySelector = new(_queryTuner);
    }

    public void Dispose() => _queryTuner?.Dispose();

    internal QueryDefinition CreateQuery(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> groups = null)
    {
        // Sanitize and validate input identifiers
        SanitizeInputs(clusters, columnOptions, sorting, groups);

        // Column Options Builder
        string columnsInQuery = "*";
        if (columnOptions is not null)
        {
            if (columnOptions.Value.Names is not null && columnOptions.Value.Names.Count > 0)
                columnsInQuery = string.Join(", ", columnOptions.Value.Names.Select(c => FormatProperty("c", c)));

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
                formattedGroup.AddRange(groups.Select(group => FormatProperty("c", group)));

                groups = [.. formattedGroup.Distinct()];

                groups = [.. groups.Concat(columnOptions.Value.Names.Select(n => FormatProperty("c", n))).Distinct()];

                if (columnOptions.Value.Aggregates.Any(ag => string.IsNullOrWhiteSpace(ag.Column)))
                    throw new UniverseException("Aggregate columns must not be null or empty.");

                foreach (AggregationOption aggregate in columnOptions.Value.Aggregates)
                {
                    string aliasBase = BuildSqlAliasBase(aggregate.Column);
                    string toAppend = aggregate.Aggregate switch
                    {
                        Q.Aggregate.Count => $"COUNT(1) AS {BuildSqlAliasBase(nameof(ICosmicEntity.CountAggregate))}",
                        Q.Aggregate.Sum => string.Format(Q.Aggregate.Sum.Value(), FormatProperty("c", aggregate.Column), aliasBase),
                        Q.Aggregate.Min => string.Format(Q.Aggregate.Min.Value(), FormatProperty("c", aggregate.Column), aliasBase),
                        Q.Aggregate.Max => string.Format(Q.Aggregate.Max.Value(), FormatProperty("c", aggregate.Column), aliasBase),
                        Q.Aggregate.Avg => string.Format(Q.Aggregate.Avg.Value(), FormatProperty("c", aggregate.Column), aliasBase),
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
                    columnsInQuery = vectorDistanceCatalysts.Aggregate(columnsInQuery, (current, catalyst) =>
                    {
                        string alias = catalyst.Alias ?? "c";
                        return $"{current}, {catalyst.Operator.Value()}({FormatProperty(alias, catalyst.Column)}, @{catalyst.ParameterName()}) AS {BuildVectorScoreAlias(catalyst, vectorDistanceCatalysts.IndexOf(catalyst))}";
                    });
                    break;
                case 1:
                    {
                        Catalyst catalyst = vectorDistanceCatalysts.First();
                        string alias = catalyst.Alias ?? "c";
                        columnsInQuery += $", {catalyst.Operator.Value()}({FormatProperty(alias, catalyst.Column)}, @{catalyst.ParameterName()}) AS {BuildVectorScoreAlias(catalyst)}";
                        break;
                    }
            }
        }

        // Update Columns Builder with Group By
        if (columnsInQuery.Contains('*') && groups is not null && groups.Any())
            columnsInQuery = columnsInQuery.Replace("*", string.Join(", ", groups));

        // nosemgrep: sql-injection -- columnsInQuery is built from validated identifiers only:
        // column names pass through ValidateIdentifier() (rejects ;, --, /*, */, ", ], control chars)
        // and FormatProperty() (bracket-wraps segments); Top is an int; aggregates use enum values
        StringBuilder queryBuilder = new($"SELECT {columnsInQuery} FROM c");

        if (columnOptions?.Join is not null)
        {
            // SQL Injection mitigation: join.Alias and join.ArrayPath are validated by SanitizeInputs() at line 24
            // ValidateIdentifier() rejects SQL keywords (;, --, /*, */), brackets (]), quotes ("), and control characters
            JoinOptions join = columnOptions.Value.Join;
            queryBuilder = new($"SELECT {columnsInQuery} FROM c JOIN {join.Alias} IN {FormatProperty("c", join.ArrayPath)}");

            // Add join columns to the select if specified
            if (join.Columns?.Any() == true)
            {
                // SQL Injection mitigation: join.Alias and join.ArrayPath are validated by SanitizeInputs() at line 24
                // ValidateIdentifier() rejects SQL keywords (;, --, /*, */), brackets (]), quotes ("), and control characters
                string joinColumns = string.Join(", ", join.Columns.Select(col => FormatProperty(join.Alias, col)));
                columnsInQuery = columnsInQuery == "*" ? $"c.*, {joinColumns}" : $"{columnsInQuery}, {joinColumns}";
                queryBuilder = new($"SELECT {columnsInQuery} FROM c JOIN {join.Alias} IN {FormatProperty("c", join.ArrayPath)}");
            }

            // Handle join aggregates
            if (join.Aggregates?.Any() == true)
            {
                groups ??= [];

                foreach (AggregationOption aggregate in join.Aggregates)
                {
                    string aliasBase = BuildSqlAliasBase(aggregate.Column);
                    string toAppend = aggregate.Aggregate switch
                    {
                        Q.Aggregate.Count => $"COUNT(1) AS {BuildSqlAliasBase(nameof(ICosmicEntity.CountAggregate))}",
                        Q.Aggregate.Sum => string.Format(Q.Aggregate.Sum.Value(), FormatProperty(join.Alias, aggregate.Column), aliasBase),
                        Q.Aggregate.Min => string.Format(Q.Aggregate.Min.Value(), FormatProperty(join.Alias, aggregate.Column), aliasBase),
                        Q.Aggregate.Max => string.Format(Q.Aggregate.Max.Value(), FormatProperty(join.Alias, aggregate.Column), aliasBase),
                        Q.Aggregate.Avg => string.Format(Q.Aggregate.Avg.Value(), FormatProperty(join.Alias, aggregate.Column), aliasBase),
                        _ => throw new UniverseException($"Unrecognized aggregate function: {aggregate.Aggregate}")
                    };

                    // Only add if not already present
                    if (!columnsInQuery.Contains(toAppend))
                        columnsInQuery = string.IsNullOrWhiteSpace(columnsInQuery) ? toAppend : $"{columnsInQuery}, {toAppend}";
                }

                // Add join columns to GROUP BY
                if (join.Columns?.Any() == true)
                    groups = [.. groups.Concat(join.Columns.Distinct().Select(col => FormatProperty(join.Alias, col)))];

                // SQL Injection mitigation: join.Alias and join.ArrayPath are validated by SanitizeInputs() at line 24
                // ValidateIdentifier() rejects SQL keywords (;, --, /*, */), brackets (]), quotes ("), and control characters
                queryBuilder = new($"SELECT {columnsInQuery} FROM c JOIN {join.Alias} IN {FormatProperty("c", join.ArrayPath)}");
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
                if (cluster.Catalysts.Any(c => c.Operator is Q.Operator.VectorDistance or Q.Operator.FTScore) && !HasValidRankTop(columnOptions))
                    throw new UniverseException($"ColumnOptions.Top must be between 1 and {Q.Limits.MaxVectorItems} when using rank catalysts.");

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
            // Make sure that the clusters do not have VectorDistance or FTScore operator
            if (clusters is not null && clusters.Any(c => c.Catalysts.Any(cat => cat.Operator is Q.Operator.VectorDistance or Q.Operator.FTScore)))
                throw new UniverseException("Sorting scalar fields is not supported in the presence of rank catalysts.");

            queryBuilder.Append($" ORDER BY {FormatProperty("c", sorting[0].Column)} {sorting[0].Direction.Value()}");
            foreach (Sorting.Option sort in sorting.Where(s => s.Column != sorting[0].Column))
                queryBuilder.Append($", {FormatProperty("c", sort.Column)} {sort.Direction.Value()}");
        }
        else if (clusters is not null && clusters.Any(c => c.Catalysts.Any(cat => cat.Operator is Q.Operator.VectorDistance or Q.Operator.FTScore)))
        {
            List<Catalyst> rankCatalysts = [.. clusters.SelectMany(cluster => cluster.Catalysts).Where(catalyst => catalyst.Operator is Q.Operator.VectorDistance or Q.Operator.FTScore)];

            if (rankCatalysts.Count > 1)
            {
                queryBuilder.Append(" ORDER BY RANK RRF(");

                foreach (Catalyst catalyst in rankCatalysts)
                {
                    if (rankCatalysts.IndexOf(catalyst) > 0)
                        queryBuilder.Append(", ");

                    if (catalyst.Operator is Q.Operator.VectorDistance)
                        queryBuilder.Append($"{catalyst.Operator.Value()}({FormatProperty("c", catalyst.Column)}, @{catalyst.ParameterName()})");
                    else if (catalyst.Operator is Q.Operator.FTScore)
                    {
                        queryBuilder.Append($"{catalyst.Operator.Value()}({FormatProperty("c", catalyst.Column)}, ");
                        if (catalyst.Value is IEnumerable<string> stringVals)
                        {
                            string[] vals = stringVals.ToArray();
                            queryBuilder.Append(string.Join(", ", vals.Select((_, i) => $"@{catalyst.ParameterName()}_ft{i}")));
                        }
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
                    queryBuilder.Append($" ORDER BY RANK {catalyst.Operator.Value()}({FormatProperty("c", catalyst.Column)}, ");
                    if (catalyst.Value is IEnumerable<string> stringVals)
                    {
                        string[] vals = stringVals.ToArray();
                        queryBuilder.Append(string.Join(", ", vals.Select((_, i) => $"@{catalyst.ParameterName()}_ft{i}")));
                    }

                    queryBuilder.Append(')');
                }
                else
                    queryBuilder.Append($" ORDER BY {catalyst.Operator.Value()}({FormatProperty("c", catalyst.Column)}, @{catalyst.ParameterName()})");
            }
        }

        // Group By Builder
        if (groups is not null && groups.Any())
        {
            // Groups pre-formatted by the aggregate path already contain bracket notation (e.g., c["category"]).
            // Raw groups (GROUP BY without aggregates) need FormatProperty applied.
            IReadOnlyList<string> formattedGroups = groups[0].Contains('[')
                ? groups
                : [.. groups.Select(g => FormatProperty("c", g)).Distinct()];

            queryBuilder.Append($" GROUP BY {formattedGroups[0]}");
            foreach (string group in formattedGroups.Where(g => g != formattedGroups[0]))
                queryBuilder.Append($", {group}");
        }

        // This error blocks code execution since this is not yet supported by CosmosDb
        // if (queryBuilder.ToString().Contains("ORDER BY") && groups is not null && groups.Any())
        // 	throw new UniverseException("ORDER BY is not supported in presence of GROUP BY");

        // Parameters Builder
        QueryDefinition query = new(queryBuilder.ToString());

        if (clusters is not null && clusters.Any())
        {
            foreach (Catalyst catalyst in clusters.SelectMany(cluster => cluster.Catalysts))
            {
                if (catalyst.Operator is Q.Operator.FTScore && catalyst.Value is IEnumerable<string> ftValues)
                {
                    string[] vals = ftValues.ToArray();
                    for (int i = 0; i < vals.Length; i++)
                    {
                        query = query.WithParameter($"@{catalyst.ParameterName()}_ft{i}", vals[i]);
                    }
                }
                else
                {
                    query = query.WithParameter($"@{catalyst.ParameterName()}", catalyst.Value);
                }
            }
        }

        return query;
    }

    private static void SanitizeInputs(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions, IReadOnlyList<Sorting.Option> sorting, IReadOnlyList<string> groups)
    {
        // Validate column options
        if (columnOptions.HasValue)
        {
            ValidateTop(columnOptions.Value.Top, "ColumnOptions.Top");

            if (columnOptions.Value.Names is not null)
            {
                foreach (string columnName in columnOptions.Value.Names)
                    ValidateIdentifier(columnName, "ColumnOption.Names");
            }

            if (columnOptions.Value.Aggregates is not null)
            {
                foreach (AggregationOption aggregate in columnOptions.Value.Aggregates)
                    ValidateIdentifier(aggregate.Column, "AggregationOption.Column");
            }

            if (columnOptions.Value.Join is not null)
            {
                JoinOptions join = columnOptions.Value.Join;
                ValidateSqlAlias(join.Alias, "JoinOptions.Alias");
                ValidateIdentifier(join.ArrayPath, "JoinOptions.ArrayPath");

                if (join.Columns is not null)
                {
                    foreach (string column in join.Columns)
                        ValidateIdentifier(column, "JoinOptions.Columns");
                }

                if (join.Aggregates is not null)
                {
                    foreach (AggregationOption aggregate in join.Aggregates)
                        ValidateIdentifier(aggregate.Column, "JoinOptions.Aggregates.Column");
                }
            }
        }

        // Validate cluster catalysts
        if (clusters is not null)
        {
            foreach (Cluster cluster in clusters.Where(c => c.Catalysts is not null))
            {
                foreach (Catalyst catalyst in cluster.Catalysts)
                {
                    ValidateIdentifier(catalyst.Column, "Catalyst.Column");
                    if (!string.IsNullOrWhiteSpace(catalyst.Alias))
                        ValidateSqlAlias(catalyst.Alias, "Catalyst.Alias");
                }
            }
        }

        // Validate sorting columns
        if (sorting is not null)
        {
            foreach (Sorting.Option sort in sorting)
            {
                if (sort.Direction is Sorting.Direction.WEIGHTED)
                    ValidateWeightValue(sort.Column);
                else
                    ValidateIdentifier(sort.Column, "Sorting.Column");
            }
        }

        // Validate group columns
        if (groups is not null)
        {
            foreach (string group in groups)
                ValidateIdentifier(group, "Group");
        }
    }

    private static void ValidateTop(int top, string parameterName)
    {
        if (top < 0 || top > Q.Limits.MaxItems)
            throw new UniverseException($"{parameterName} must be between 0 and {Q.Limits.MaxItems}.");
    }

    private static bool HasValidRankTop(ColumnOptions? columnOptions)
        => columnOptions.HasValue
           && columnOptions.Value.Top >= 1
           && columnOptions.Value.Top <= Q.Limits.MaxVectorItems;

    private static void ValidateSqlAlias(string alias, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new UniverseException($"{parameterName} cannot be null or empty.");

        if (alias.Length > 128)
            throw new UniverseException($"{parameterName} exceeds maximum alias length of 128 characters.");

        if (!IsStrictSqlAlias(alias))
            throw new UniverseException($"{parameterName} must match ^[A-Za-z_][A-Za-z0-9_]*$.");

        if (ReservedAliases.Contains(alias))
            throw new UniverseException($"{parameterName} cannot be a SQL keyword.");
    }

    private static bool IsStrictSqlAlias(string alias)
    {
        if (alias.Length == 0 || !(char.IsAsciiLetter(alias[0]) || alias[0] == '_'))
            return false;

        return alias.Skip(1).All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
    }

    private static void ValidateIdentifier(string identifier, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new UniverseException($"{parameterName} cannot be null or empty.");

        // Reject identifiers that consist solely of dots (e.g., ".", "..", "...")
        // These would produce invalid SQL when processed by FormatProperty
        if (identifier.All(c => c == '.'))
            throw new UniverseException($"{parameterName} cannot consist solely of dots.");

        // Check for suspicious patterns that might indicate SQL injection attempts
        if (identifier.Contains(';') || identifier.Contains("--") || identifier.Contains("/*") || identifier.Contains("*/"))
            throw new UniverseException($"{parameterName} contains invalid characters. SQL injection attempt detected.");

        // Reject double-quote and closing bracket characters which can break bracketed identifiers.
        // Bracket escape patterns include:
        //   - c] OR 1=1 -- (closes bracket, adds condition, comments rest)
        //   - name"], [c.id (closes bracket+quote, opens new bracket)
        //   - items] OR 1=1 -- (closes bracket, adds SQL condition)
        // Both "] and ] characters must be rejected unconditionally to prevent all bracket escape attacks.
        if (identifier.Contains('"'))
            throw new UniverseException($"{parameterName} contains double-quote (\") characters, which are not allowed in identifiers.");
        if (identifier.Contains(']'))
            throw new UniverseException($"{parameterName} contains closing bracket (]) characters, which are not allowed in identifiers.");

        // Ensure the identifier doesn't exceed a reasonable length
        // Per Azure Cosmos DB documentation: database/container names are limited to 255 characters
        // Document properties have no practical limit, but we enforce this as a sanity check
        if (identifier.Length > 255)
            throw new UniverseException($"{parameterName} exceeds maximum identifier length of 255 characters.");

        // Reject control characters which could lead to query corruption
        if (identifier.Any(char.IsControl))
            throw new UniverseException($"{parameterName} contains control characters.");
    }

    /// <summary>
    /// Validates that a weight value for ORDER BY RANK RRF contains only safe numeric content.
    /// Weight values should be comma-separated decimal numbers, optionally bracket-wrapped (e.g., "0.7, 0.3").
    /// </summary>
    private static void ValidateWeightValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new UniverseException("Weight value cannot be null or empty.");

        // Strip optional surrounding brackets for validation
        string inner = value.Trim();
        if (inner.StartsWith('['))
            inner = inner[1..];
        if (inner.EndsWith(']'))
            inner = inner[..^1];

        if (string.IsNullOrWhiteSpace(inner))
            throw new UniverseException("Weight value cannot be empty.");

        // Defense-in-depth: reject invalid characters before parsing individual segments
        foreach (char c in inner)
        {
            if (!char.IsDigit(c) && c != '.' && c != ',' && !char.IsWhiteSpace(c))
                throw new UniverseException($"Weight value contains invalid character '{c}'. Only numeric values separated by commas are allowed.");
        }

        // Validate each segment is a parseable number
        string[] segments = inner.Split(',', StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            throw new UniverseException("Weight value must contain at least one numeric value.");

        foreach (string segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
                throw new UniverseException("Weight value contains an empty segment.");

            if (!double.TryParse(segment, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                throw new UniverseException($"Weight value '{segment}' is not a valid number.");
        }
    }

    private string ConvertName(string name) => _namingPolicy?.ConvertName(name) ?? name;

    private string BuildSqlAliasBase(string path)
    {
        string[] segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string convertedPath = string.Join("_", segments.Select(ConvertName));

        StringBuilder alias = new(convertedPath.Length);
        bool previousWasUnderscore = false;
        foreach (char c in convertedPath)
        {
            bool isAliasChar = char.IsAsciiLetterOrDigit(c) || c == '_';
            char next = isAliasChar ? c : '_';

            if (next == '_' && previousWasUnderscore)
                continue;

            alias.Append(next);
            previousWasUnderscore = next == '_';
        }

        string result = alias.ToString().Trim('_');
        if (string.IsNullOrEmpty(result))
            result = "value";

        if (!(char.IsAsciiLetter(result[0]) || result[0] == '_'))
            result = $"_{result}";

        if (ReservedAliases.Contains(result))
            result = $"{result}_alias";

        return result;
    }

    private string BuildVectorScoreAlias(Catalyst catalyst, int index = 0)
    {
        string suffix = string.Empty;
        if (index > 0 && !string.IsNullOrEmpty(catalyst.CatalystId))
        {
            int start = Math.Max(0, catalyst.CatalystId.Length - 8);
            suffix = new string(catalyst.CatalystId[start..]
                .Select(c => char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_')
                .ToArray());
        }

        return $"{BuildSqlAliasBase(catalyst.Column)}Score{suffix}";
    }

    private string WhereClauseBuilder(Catalyst catalyst)
    {
        string alias = catalyst.Alias ?? "c";
        string formattedProperty = FormatProperty(alias, catalyst.Column);
        return catalyst.Operator switch
        {
            Q.Operator.In or
                Q.Operator.NotIn => $"{catalyst.Operator.Value()}({formattedProperty}, @{catalyst.ParameterName()})",
            Q.Operator.Contains or
                Q.Operator.NotContains => $"{catalyst.Operator.Value()}(@{catalyst.ParameterName()}, {formattedProperty})",
            Q.Operator.Len => $"{catalyst.Operator.Value()}({formattedProperty}) = @{catalyst.ParameterName()}",
            Q.Operator.Defined or
                Q.Operator.NotDefined => $"{catalyst.Operator.Value()}({formattedProperty})",
            Q.Operator.FTContains or
                Q.Operator.NotFTContains or
                Q.Operator.FTContainsAll or
                Q.Operator.NotFTContainsAll or
                Q.Operator.FTContainsAny or
                Q.Operator.NotFTContainsAny => $"{catalyst.Operator.Value()}({formattedProperty}, @{catalyst.ParameterName()})",
            _ => $"{formattedProperty} {catalyst.Operator.Value()} @{catalyst.ParameterName()}",
        };
    }

    private string FormatProperty(string alias, string column)
    {
        // Handle nested JSON paths by splitting on '.' and wrapping each segment
        // Example: "metadata.sku" becomes alias["metadata"]["sku"]
        // Naming policy is applied to each segment to match serialized document field names

        string[] segments = column.Split('.');
        StringBuilder result = new(alias);

        foreach (string segment in segments)
        {
            if (!string.IsNullOrWhiteSpace(segment))
                result.Append($"[\"{ConvertName(segment)}\"]");
        }

        return result.ToString();
    }

    internal async Task<(Gravity, T)> GetOneFromQuery<T>(Container container, QueryDefinition query, QueryContext? context = null)
    {
        context ??= InferQueryContext(query);
        QueryContext singleContext = context.Value with { MaxItemCount = 1 };

        IQueryExecutionStrategy strategy = _strategySelector.SelectStrategy(query, singleContext);
        (Gravity gravity, IList<T> results) = await strategy.ExecuteAsync<T>(container, query, singleContext, _recordQueries, _queryTuner);
        T result = results.Count != 0 ? results.First() : default(T);
        return (gravity, result);
    }

    internal async Task<(Gravity, IList<T>)> GetListFromQuery<T>(Container container, QueryDefinition query, QueryContext? context = null)
    {
        context ??= InferQueryContext(query);
        IQueryExecutionStrategy strategy = _strategySelector.SelectStrategy(query, context.Value);
        return await strategy.ExecuteAsync<T>(container, query, context.Value, _recordQueries, _queryTuner);
    }

    internal QueryTuningRecommendations GetQueryRecommendations(QueryType queryType) => _queryTuner.GetRecommendations(queryType);

    private static QueryContext InferQueryContext(QueryDefinition query)
        => new(QueryTypeDetector.Infer(query));
}
