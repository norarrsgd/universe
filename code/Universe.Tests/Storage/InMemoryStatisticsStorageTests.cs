using Universe.Builder.Strategies;
using Universe.Builder.Strategies.Storage;
using Universe.Tests.Helpers;
using Xunit;

namespace Universe.Tests.Storage;

public sealed class InMemoryStatisticsStorageTests
{
    [Fact]
    public async Task SaveAsync_ReturnsCompletedTask()
    {
        InMemoryStatisticsStorage storage = new InMemoryStatisticsStorage();
        Task task = storage.SaveAsync(TestStatisticsFactory.Create());

        Assert.True(task.IsCompleted);
        await task; // should not throw
    }

    [Fact]
    public async Task LoadRecentAsync_ReturnsEmpty()
    {
        InMemoryStatisticsStorage storage = new InMemoryStatisticsStorage();
        IList<QueryExecutionStatistics> result = await storage.LoadRecentAsync(100);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByQueryHashAsync_ReturnsEmpty()
    {
        InMemoryStatisticsStorage storage = new InMemoryStatisticsStorage();
        IList<QueryExecutionStatistics> result = await storage.GetByQueryHashAsync("anyhash", TimeSpan.FromHours(1));

        Assert.Empty(result);
    }

    [Fact]
    public async Task ClearOldAsync_Completes()
    {
        InMemoryStatisticsStorage storage = new InMemoryStatisticsStorage();
        await storage.ClearOldAsync(TimeSpan.FromDays(1)); // should not throw
    }
}
