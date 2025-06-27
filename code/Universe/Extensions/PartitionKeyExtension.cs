using System.Reflection;

namespace Universe.Extensions;

/// <summary> Extensions for ICosmicEntity </summary>
public static class CosmicEntityExtensions
{
    /// <summary>Maximum allowed partition key levels in Cosmos DB</summary>
    public const int MaxPartitionKeyLevels = 3;

    /// <summary>Builds the partition key for the entity.</summary>
    public static PartitionKey BuildPartitionKey(this Type entity)
    {
        if (entity != typeof(ICosmicEntity))
            throw new UniverseException($"Type {entity.Name} does not implement ICosmicEntity. Please ensure the type is a valid ICosmicEntity.");

        PartitionKeyBuilder builder = new();

        // Reflect on this type to find the PartitionKey properties
        IEnumerable<PropertyInfo> partitionKeyProperties = entity.GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(PartitionKeyAttribute), false).Any());

        if (!partitionKeyProperties.Any())
            throw new UniverseException($"No PartitionKey properties found in {entity.Name}. Please add a PartitionKeyAttribute to the ICosmicEntity object.");

        if (partitionKeyProperties.Count() > MaxPartitionKeyLevels)
            throw new UniverseException($"Only up to {MaxPartitionKeyLevels} PartitionKey properties are allowed in {entity.Name}.");

        // Validate for duplicate sequences
        List<int> sequences = [.. partitionKeyProperties.Select(p => p.GetCustomAttribute<PartitionKeyAttribute>()?.Sequence ?? 1)];

        IEnumerable<int> duplicateSequences = sequences
            .GroupBy(s => s)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        if (duplicateSequences.Any())
            throw new UniverseException($"Duplicate PartitionKey Sequence values found in {entity.Name}: {string.Join(", ", duplicateSequences)}. Each PartitionKey property must have a unique Sequence value.");

        // Order by sequence and build partition key
        IOrderedEnumerable<PropertyInfo> orderedProperties = partitionKeyProperties
            .OrderBy(p => p.GetCustomAttribute<PartitionKeyAttribute>()?.Sequence ?? 1);

        foreach (PropertyInfo property in partitionKeyProperties)
            builder.Add(property.Name);

        return builder.Build();
    }

    /// <summary>Builds the partition key for the entity.</summary>
    public static PartitionKey BuildPartitionKey(this ICosmicEntity entity)
    {
        if (entity is null)
            throw new ArgumentNullException(nameof(entity), "Entity cannot be null.");

        Type entityType = entity.GetType();
        PartitionKeyBuilder builder = new();

        // Reflect on this type to find the PartitionKey properties
        IEnumerable<PropertyInfo> partitionKeyProperties = entityType.GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(PartitionKeyAttribute), false).Any());

        foreach (PropertyInfo property in partitionKeyProperties)
        {
            object value = property.GetValue(entity) ?? throw new UniverseException($"PartitionKey property '{property.Name}' cannot be null in {entityType.Name}.");
            builder.Add(value.ToString());
        }

        return builder.Build();
    }

    /// <summary>Builds the partition key for the entity.</summary>
    public static IEnumerable<string> PartitionKeys(this ICosmicEntity entity)
    {
        if (entity is null)
            throw new ArgumentNullException(nameof(entity), "Entity cannot be null.");

        Type entityType = entity.GetType();

        return [.. entityType.GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(PartitionKeyAttribute), false).Any())
            .OrderBy(p => p.GetCustomAttribute<PartitionKeyAttribute>()?.Sequence ?? 1)
            .Select(p =>
            {
                object value = p.GetValue(entity) ?? throw new UniverseException($"PartitionKey property '{p.Name}' cannot be null in {entityType.Name}.");
                return value.ToString();
            })];
    }
}