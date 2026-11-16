using Universe.Interfaces;
using Universe.Response;
using Universe.Builder.Options;
using Universe.Builder.Strategies;
using Universe.Extensions;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace Universe.Tests.Batch;

public sealed class LegacyInterfaceCompatibilityTests
{
    [Fact]
    public async Task LegacyImplementation_ExistingOperationsRemainCallable()
    {
        IGalaxyBasic<LegacyEntity> legacy = new LegacyGalaxy();
        LegacyEntity entity = new() { id = "legacy-id" };

        (Gravity gravity, string id) = await legacy.Create(entity);

        Assert.Equal("legacy-id", id);
        Assert.Equal(1, gravity.RU);
    }

    [Fact]
    public void LegacyImplementation_NewBatchOperationsFailClearly()
    {
        IGalaxyBasic<LegacyEntity> legacy = new LegacyGalaxy();

        NotSupportedException atomic = Assert.Throws<NotSupportedException>(() => legacy.Atomic("tenant-1"));
        NotSupportedException bulk = Assert.Throws<NotSupportedException>(() => legacy.Bulk());

        Assert.Contains("atomic batch operations", atomic.Message);
        Assert.Contains("bulk batch operations", bulk.Message);
    }

    [Fact]
    public async Task LegacyReadOverloads_FallBackOnlyWhenOptionsAreRepresentable()
    {
        IGalaxy<LegacyEntity> legacy = new LegacyGalaxy();
        (_, LegacyEntity point) = await legacy.Get("id", ["tenant"], new ReadOptions());
        Assert.Equal("id", point.id);
        await legacy.Get(null, null, null, null, null, null);
        await legacy.Get<LegacyEntity>(null, null, null, null, null, null);
        await legacy.List(null, null, null, null, null, null);
        await legacy.List<LegacyEntity>(null, null, null, null, null, null);
        await legacy.Paged(new Q.Page(1), null, null, null, null, null);
        await legacy.Paged<LegacyEntity>(new Q.Page(1), null, null, null, null, null);
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.Get(null, new(null, Top: -1), null, null, null, null));
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.Get("id", ["tenant"], new(ConsistencyLevel.Strong)));
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.Query().WithConsistency(ConsistencyLevel.Strong).GetAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.Query().Project(p => p.Field("id", "id")).GetAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.Query().Project(p => p.Field("id", "id")).ToListAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.Query().Project(p => p.Field("id", "id")).Paged(1).ToListAsync());
    }

    private sealed class LegacyGalaxy : IGalaxy<LegacyEntity>
    {
        public Task<(Gravity g, LegacyEntity T)> Get(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns = null)
            => Task.FromResult<(Gravity, LegacyEntity)>((new(1, null), new()));
        public Task<(Gravity g, TS S)> Get<TS>(IReadOnlyList<Cluster> clusters, IReadOnlyList<string> columns = null)
            => Task.FromResult<(Gravity, TS)>((new(1, null), default));
        public Task<(Gravity g, IList<LegacyEntity> T)> List(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null, QueryHints? hints = null)
            => Task.FromResult<(Gravity, IList<LegacyEntity>)>((new(1, null), []));
        public Task<(Gravity g, IList<TS> T)> List<TS>(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null, QueryHints? hints = null)
            => Task.FromResult<(Gravity, IList<TS>)>((new(1, null), []));
        public Task<(Gravity g, IList<LegacyEntity> T)> Paged(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null)
            => List(clusters, columnOptions, sorting, group);
        public Task<(Gravity g, IList<TS> T)> Paged<TS>(Q.Page page, IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null)
            => List<TS>(clusters, columnOptions, sorting, group);
        public Gravity GenerateQuery(IReadOnlyList<Cluster> clusters, ColumnOptions? columnOptions = null, IReadOnlyList<Sorting.Option> sorting = null, IReadOnlyList<string> group = null) => new(0, null);
        public QueryTuningRecommendations GetQueryRecommendations(string queryPattern, QueryType queryType) => default;

        public Task<(Gravity g, string t)> Create(LegacyEntity model)
            => Task.FromResult<(Gravity, string)>((new(1, null), model.id));

        public Task<Gravity> Create(IReadOnlyList<LegacyEntity> models)
            => Task.FromResult<Gravity>(new(models.Count, null));

        public Task<(Gravity g, LegacyEntity T)> Modify(LegacyEntity model)
            => Task.FromResult<(Gravity, LegacyEntity)>((new(1, null), model));

        public Task<Gravity> Modify(IReadOnlyList<LegacyEntity> models)
            => Task.FromResult<Gravity>(new(models.Count, null));

        public Task<Gravity> Remove(string id, string partitionKey)
            => Task.FromResult<Gravity>(new(1, null));

        public Task<Gravity> Remove(string id, params string[] partitionKey)
            => Task.FromResult<Gravity>(new(1, null));

        public Task<(Gravity g, LegacyEntity T)> Get(string id, string partitionKey)
            => Task.FromResult<(Gravity, LegacyEntity)>((new(1, null), new() { id = id }));

        public Task<(Gravity g, LegacyEntity T)> Get(string id, params string[] partitionKey)
            => Task.FromResult<(Gravity, LegacyEntity)>((new(1, null), new() { id = id }));
    }

    private sealed record LegacyEntity : CosmicEntity;
}
