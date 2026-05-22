using Universe.Builder;
using Universe.Builder.Options;
using Universe.Exception;
using Xunit;

namespace Universe.Tests.Builder;

public sealed class QueryLimitTests : IDisposable
{
    private readonly UniverseBuilder _builder = new(recordQueries: true);

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void OrbitTop_AboveMaxItems_Throws()
    {
        Orbit<TestEntity> orbit = new(null);

        Assert.Throws<UniverseException>(() => orbit.Top(Q.Limits.MaxItems + 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Q.Limits.MaxItems + 1)]
    public void Page_InvalidSize_Throws(int size)
    {
        Assert.Throws<UniverseException>(() => new Q.Page(size));
    }

    [Fact]
    public void Page_PropertiesDoNotExposePublicSetters()
    {
        Assert.Null(typeof(Q.Page).GetProperty(nameof(Q.Page.Size))?.SetMethod);
        Assert.Null(typeof(Q.Page).GetProperty(nameof(Q.Page.ContinuationToken))?.SetMethod);
    }

    [Fact]
    public void RankQuery_TopAboveMaxVectorItems_Throws()
    {
        float[] vector = [0.1f, 0.2f, 0.3f];
        List<Cluster> clusters = [new([new("embedding", vector, Operator: Q.Operator.VectorDistance)])];
        ColumnOptions colOpts = new(Names: ["name"], Top: Q.Limits.MaxVectorItems + 1);

        Assert.Throws<UniverseException>(() => _builder.CreateQuery(clusters, colOpts));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Q.Limits.MaxItems + 1)]
    public void QueryHints_InvalidMaxItemCount_Throws(int maxItemCount)
    {
        QueryHints hints = new(MaxItemCount: maxItemCount);

        Assert.Throws<UniverseException>(() => hints.ToContextHints());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Q.Limits.MaxItems + 1)]
    public void QueryHints_InvalidMaxBufferedItemCount_Throws(int maxBufferedItemCount)
    {
        QueryHints hints = new(MaxBufferedItemCount: maxBufferedItemCount);

        Assert.Throws<UniverseException>(() => hints.ToContextHints());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QueryHints_InvalidMaxConcurrency_Throws(int maxConcurrency)
    {
        QueryHints hints = new(MaxConcurrency: maxConcurrency);

        Assert.Throws<UniverseException>(() => hints.ToContextHints());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void QueryHints_InvalidResponseContinuationTokenLimit_Throws(int tokenLimit)
    {
        QueryHints hints = new(ResponseContinuationTokenLimitInKb: tokenLimit);

        Assert.Throws<UniverseException>(() => hints.ToContextHints());
    }
}
