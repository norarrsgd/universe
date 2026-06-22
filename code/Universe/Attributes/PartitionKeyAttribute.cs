namespace Universe.Attributes;

/// <summary>
/// Attribute to specify the partition key for a Cosmos DB entity.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class PartitionKeyAttribute : Attribute
{
    /// <summary>
    /// Gets the partition key sequence number.
    /// </summary>
    public int Sequence { get; }

    /// <summary>
    /// Gets the name of the partition key.
    /// </summary>
    public string KeyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PartitionKeyAttribute"/> class with the specified partition key.
    /// </summary>
    public PartitionKeyAttribute() => Sequence = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartitionKeyAttribute"/> class with the specified partition key.
    /// </summary>
    /// <param name="sequence">The partition key sequence number (1-3).</param>
    /// <param name="keyName">The name of the partition key.</param>
    public PartitionKeyAttribute(int sequence, string keyName = null)
    {
        if (sequence < 1 || sequence > 3)
            throw new ArgumentOutOfRangeException(nameof(sequence), "Partition key sequence must be between 1 and 3.");

        Sequence = sequence;
        KeyName = keyName;
    }
}
