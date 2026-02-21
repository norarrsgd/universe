using System.Diagnostics;
using Universe.Builder.Strategies;
using Universe.Builder.Strategies.Storage;
using Universe.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Universe.Tests.Storage;

public sealed class FileStatisticsStorageTests : IDisposable
{
	private readonly string _filePath;
	private readonly FileStatisticsStorage _storage;
	private readonly ITestOutputHelper _output;

	public FileStatisticsStorageTests(ITestOutputHelper output)
	{
		_output = output;
		_filePath = Path.Combine(AppContext.BaseDirectory, $"test-{Guid.NewGuid()}.json");
		_storage = new FileStatisticsStorage(_filePath);
	}

	public void Dispose()
	{
		_storage.Dispose();
		try { if (File.Exists(_filePath)) File.Delete(_filePath); }
		catch { /* best effort cleanup */ }
	}

	[Fact]
	public async Task SaveAndLoad_RoundTrip_AllFieldsPreserved()
	{
		var hints = new Dictionary<string, object> { ["MaxItemCount"] = 50 };
		var stat = TestStatisticsFactory.Create(
			queryHash: "file-abc",
			type: QueryType.Aggregation,
			ru: 33.3,
			executionTime: TimeSpan.FromMilliseconds(200),
			resultCount: 42,
			success: false,
			timestamp: new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc),
			strategyUsed: "Gateway",
			hintsUsed: hints);

		await _storage.SaveAsync(stat);
		var loaded = await _storage.LoadRecentAsync(10);

		Assert.Single(loaded);
		var result = loaded[0];
		Assert.Equal("file-abc", result.QueryHash);
		Assert.Equal(QueryType.Aggregation, result.Type);
		Assert.Equal(33.3, result.RU);
		Assert.Equal(200, result.ExecutionTime.TotalMilliseconds);
		Assert.Equal(42, result.ResultCount);
		Assert.False(result.Success);
		Assert.Equal("Gateway", result.StrategyUsed);
	}

	[Fact]
	public async Task LoadRecentAsync_RespectsLimit()
	{
		for (int i = 0; i < 10; i++)
		{
			await _storage.SaveAsync(TestStatisticsFactory.Create(
				queryHash: $"limit-{i}",
				timestamp: DateTime.UtcNow.AddMinutes(-i)));
		}

		var loaded = await _storage.LoadRecentAsync(5);

		Assert.Equal(5, loaded.Count);
		// Most recent first
		Assert.True(loaded[0].Timestamp >= loaded[1].Timestamp);
	}

	[Fact]
	public async Task SaveAsync_TruncatesAt1000Entries()
	{
		// Save 1005 entries
		for (int i = 0; i < 1005; i++)
		{
			await _storage.SaveAsync(TestStatisticsFactory.Create(
				queryHash: $"trunc-{i}",
				timestamp: DateTime.UtcNow.AddSeconds(-i)));
		}

		var loaded = await _storage.LoadRecentAsync(2000);

		Assert.Equal(1000, loaded.Count);
		// Should keep the most recent 1000
		Assert.Equal("trunc-0", loaded[0].QueryHash);
	}

	[Fact]
	public async Task CorruptJson_RecoversGracefully()
	{
		// Write corrupt JSON directly to the file
		await File.WriteAllTextAsync(_filePath, "{ this is not valid json [[[");

		// LoadRecentAsync should return empty (not throw)
		var loaded = await _storage.LoadRecentAsync(10);
		Assert.Empty(loaded);

		// Should be able to save new data after recovery
		await _storage.SaveAsync(TestStatisticsFactory.Create(queryHash: "recovered"));
		var loadedAfter = await _storage.LoadRecentAsync(10);
		Assert.Single(loadedAfter);
		Assert.Equal("recovered", loadedAfter[0].QueryHash);
	}

	[Fact]
	public void CustomPath_OutsideAppDirectory_IsAllowed()
	{
		string tempPath = Path.Combine(Path.GetTempPath(), $"universe-test-{Guid.NewGuid()}.json");
		using var storage = new FileStatisticsStorage(tempPath);
		storage.Dispose();
		try { if (File.Exists(tempPath)) File.Delete(tempPath); }
		catch { /* best effort cleanup */ }
	}

	[Fact]
	public void ResolveDefaultPath_NonAzure_UsesAppBaseDirectory()
	{
		string original1 = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
		string original2 = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");

		try
		{
			Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);
			Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", null);

			string expected = Path.Combine(AppContext.BaseDirectory, "query-statistics.json");
			Assert.Equal(expected, FileStatisticsStorage.ResolveDefaultPath());
		}
		finally
		{
			Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", original1);
			Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", original2);
		}
	}

	[Fact]
	[Trait("Category", "Performance")]
	public async Task SaveAsync_PerformanceAtDifferentScales()
	{
		int[] scales = [10, 100, 500, 1000];

		foreach (int scale in scales)
		{
			string filePath = Path.Combine(AppContext.BaseDirectory, $"perf-file-{scale}-{Guid.NewGuid()}.json");
			using var storage = new FileStatisticsStorage(filePath);

			var sw = Stopwatch.StartNew();
			for (int i = 0; i < scale; i++)
			{
				await storage.SaveAsync(TestStatisticsFactory.Create(
					queryHash: $"perf-{i}",
					timestamp: DateTime.UtcNow.AddSeconds(-i)));
			}
			sw.Stop();

			_output.WriteLine($"FileStorage SaveAsync x{scale}: {sw.Elapsed.TotalMilliseconds:F2}ms ({sw.Elapsed.TotalMilliseconds / scale:F2}ms/call)");

			try { File.Delete(filePath); }
			catch { /* best effort */ }
		}

		// This test documents O(n) per-call growth (total is O(n^2))
		// since each SaveAsync loads all existing records, appends, and writes back
	}
}
