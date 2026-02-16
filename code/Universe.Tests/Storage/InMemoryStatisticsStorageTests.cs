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
		var storage = new InMemoryStatisticsStorage();
		var task = storage.SaveAsync(TestStatisticsFactory.Create());

		Assert.True(task.IsCompleted);
		await task; // should not throw
	}

	[Fact]
	public async Task LoadRecentAsync_ReturnsEmpty()
	{
		var storage = new InMemoryStatisticsStorage();
		var result = await storage.LoadRecentAsync(100);

		Assert.Empty(result);
	}

	[Fact]
	public async Task GetByQueryHashAsync_ReturnsEmpty()
	{
		var storage = new InMemoryStatisticsStorage();
		var result = await storage.GetByQueryHashAsync("anyhash", TimeSpan.FromHours(1));

		Assert.Empty(result);
	}

	[Fact]
	public async Task ClearOldAsync_Completes()
	{
		var storage = new InMemoryStatisticsStorage();
		await storage.ClearOldAsync(TimeSpan.FromDays(1)); // should not throw
	}
}
