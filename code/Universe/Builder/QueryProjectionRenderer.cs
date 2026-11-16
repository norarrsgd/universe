using System.Text.Json;

namespace Universe.Builder;

internal partial class UniverseBuilder
{
    private string BuildProjectionColumns(ColumnOptions options, List<string> groups, IReadOnlyList<Cluster> clusters, Dictionary<Catalyst, string> parameters, IReadOnlyList<Sorting.Option> sorting)
    {
        if (options.Projection.Items.Count == 0)
            throw new UniverseException("A projection must contain at least one output field.");
        List<string> columns = [];
        List<string> scalarFields = [];
        HashSet<string> outputNames = new(StringComparer.Ordinal);
        bool hasAggregates = options.Projection.HasAggregates || options.Aggregates is { Count: > 0 } || options.Join?.Aggregates is { Count: > 0 };

        void AddOutput(string name, string expression, bool explicitAlias)
        {
            if (!outputNames.Add(name))
                throw new UniverseException($"Duplicate projection output name: {name}.");
            columns.Add(explicitAlias ? $"{expression} AS {name}" : expression);
        }

        void AddField(string alias, string path)
        {
            string expression = FormatProperty(alias, path);
            scalarFields.Add(expression);
            AddOutput(ConvertName(path.Split('.').Last(segment => !string.IsNullOrWhiteSpace(segment))), expression, false);
        }

        void AddAggregates(IReadOnlyList<AggregationOption> aggregates, string alias)
        {
            foreach (AggregationOption aggregate in aggregates ?? [])
            {
                string name = aggregate.Aggregate == Q.Aggregate.Count ? BuildSqlAliasBase(nameof(ICosmicEntity.CountAggregate)) : $"{BuildSqlAliasBase(aggregate.Column)}_{aggregate.Aggregate}";
                AddOutput(name, RenderAggregate(aggregate.Column, aggregate.Aggregate, alias), true);
            }
        }

        foreach (string path in options.Names ?? [])
            AddField("c", path);
        foreach (string path in options.Join?.Columns ?? [])
            AddField(options.Join.Alias, path);
        AddAggregates(options.Aggregates, "c");
        AddAggregates(options.Join?.Aggregates, options.Join?.Alias);
        foreach (ProjectionItem item in options.Projection.Items)
        {
            ValidateSqlAlias(item.Name, "Projection.Name");
            if (string.Equals(item.Name, "value", StringComparison.OrdinalIgnoreCase))
                throw new UniverseException("Projection.Name cannot be the SQL keyword VALUE.");
            AddOutput(item.Name, RenderProjectionItem(item, scalarFields), true);
        }

        List<Catalyst> vectorCatalysts = [.. (clusters ?? []).SelectMany(cluster => cluster.Catalysts ?? []).Where(catalyst => catalyst.Operator == Q.Operator.VectorDistance)];
        for (int index = 0; index < vectorCatalysts.Count; index++)
        {
            Catalyst catalyst = vectorCatalysts[index];
            string name = BuildVectorScoreAlias(catalyst, vectorCatalysts.Count > 1 ? index : 0);
            AddOutput(name, $"VectorDistance({FormatProperty(catalyst.Alias ?? "c", catalyst.Column)}, @{parameters[catalyst]})", true);
        }

        if (hasAggregates)
        {
            foreach (string field in scalarFields)
                if (!groups.Contains(field))
                    groups.Add(field);
        }
        else if (groups.Count > 0 && scalarFields.Any(field => !groups.Contains(field)))
            throw new UniverseException("Every projected field must be included in GROUP BY.");

        bool hasRanking = vectorCatalysts.Count > 0 || (clusters ?? []).Any(cluster => (cluster.Catalysts ?? []).Any(catalyst => catalyst.Operator == Q.Operator.FTScore));
        if ((hasAggregates || groups.Count > 0) && (sorting is { Count: > 0 } || hasRanking))
            throw new UniverseException("Cosmos does not support ordering or ranking grouped or aggregate projections.");

        string prefix = options.IsDistinct ? "DISTINCT " : "";
        if (options.Top > 0)
            prefix += $"TOP {options.Top} ";
        return prefix + string.Join(", ", columns);
    }

    private string RenderProjectionItem(ProjectionItem item, List<string> scalarFields)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
            throw new UniverseException("Projection output names cannot be empty.");
        if (item.Object is not null)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            List<string> fields = [];
            foreach (ProjectionItem child in item.Object.Items)
            {
                if (!names.Add(child.Name))
                    throw new UniverseException($"Duplicate projection output name: {child.Name}.");
                fields.Add($"{JsonSerializer.Serialize(child.Name)}: {RenderProjectionItem(child, scalarFields)}");
            }
            return $"{{{string.Join(", ", fields)}}}";
        }
        ValidateIdentifier(item.Column, "Projection.Column");
        ValidateSqlAlias(item.Alias, "Projection.Alias");
        if (item.Aggregate.HasValue)
            return RenderAggregate(item.Column, item.Aggregate.Value, item.Alias);
        string expression = FormatProperty(item.Alias, item.Column);
        scalarFields.Add(expression);
        return expression;
    }

    private string RenderAggregate(string column, Q.Aggregate aggregate, string alias)
    {
        ValidateIdentifier(column, "Projection.Aggregate.Column");
        ValidateSqlAlias(alias, "Projection.Aggregate.Alias");
        return aggregate switch
        {
            Q.Aggregate.Count => "COUNT(1)",
            Q.Aggregate.Sum => $"SUM({FormatProperty(alias, column)})",
            Q.Aggregate.Min => $"MIN({FormatProperty(alias, column)})",
            Q.Aggregate.Max => $"MAX({FormatProperty(alias, column)})",
            Q.Aggregate.Avg => $"AVG({FormatProperty(alias, column)})",
            _ => throw new UniverseException("Unrecognized aggregate function.")
        };
    }
}
