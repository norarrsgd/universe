using Xunit;
using Universe.Attributes;
using Universe.Exception;
using Universe.Extensions;
using Universe.Interfaces;

namespace Universe.Tests.Extensions;

public sealed class PartitionKeyExtensionTests
{
    [Fact]
    public void BuildPartitionKey_UsesExplicitKeyName()
    {
        IReadOnlyList<string> paths = typeof(CustomNamedPartitionEntity).BuildPartitionKey();

        Assert.Equal(["/tenant_id", "/region_code"], paths);
    }

    [Fact]
    public void BuildPartitionKey_OrdersPathsBySequence()
    {
        IReadOnlyList<string> paths = typeof(OrderedPartitionEntity).BuildPartitionKey();

        Assert.Equal(["/tenantId", "/region", "/storeId"], paths);
    }

    [Fact]
    public void BuildPartitionKey_DuplicateSequence_ThrowsUniverseException()
    {
        UniverseException exception = Assert.Throws<UniverseException>(
            () => typeof(DuplicateSequencePartitionEntity).BuildPartitionKey());

        Assert.Contains("Duplicate PartitionKey Sequence values", exception.Message);
    }

    [Fact]
    public void BuildPartitionKey_MoreThanThreeLevels_ThrowsUniverseException()
    {
        UniverseException exception = Assert.Throws<UniverseException>(
            () => typeof(TooManyPartitionLevelsEntity).BuildPartitionKey());

        Assert.Contains("Only up to 3 PartitionKey properties are allowed", exception.Message);
    }

    [Fact]
    public void PartitionKeys_OrdersRuntimeValuesBySequence()
    {
        OrderedPartitionEntity entity = new()
        {
            TenantId = "tenant-1",
            Region = "west",
            StoreId = "store-7"
        };

        Assert.Equal(["tenant-1", "west", "store-7"], entity.PartitionKeys());
    }

    private sealed record CustomNamedPartitionEntity : CosmicEntity
    {
        [PartitionKey(1, "tenant_id")]
        public string TenantId { get; init; }

        [PartitionKey(2, "region_code")]
        public string Region { get; init; }
    }

    private sealed record OrderedPartitionEntity : CosmicEntity
    {
        [PartitionKey(3)]
        public string StoreId { get; init; }

        [PartitionKey(1)]
        public string TenantId { get; init; }

        [PartitionKey(2)]
        public string Region { get; init; }
    }

    private sealed record DuplicateSequencePartitionEntity : CosmicEntity
    {
        [PartitionKey(1)]
        public string TenantId { get; init; }

        [PartitionKey(1)]
        public string Region { get; init; }
    }

    private sealed record TooManyPartitionLevelsEntity : CosmicEntity
    {
        [PartitionKey(1)]
        public string TenantId { get; init; }

        [PartitionKey(2)]
        public string Region { get; init; }

        [PartitionKey(3)]
        public string StoreId { get; init; }

        [PartitionKey]
        public string DepartmentId { get; init; }
    }
}
