using System.Text.Json;

namespace Universe.Builder.Strategies.Storage;

/// <summary>
/// File-based storage (JSON persistence)
/// </summary>
public sealed class FileStatisticsStorage : IQueryStatisticsStorage
{
	private readonly string _filePath;
	private readonly SemaphoreSlim _lock = new(1, 1);

	/// <summary>
	/// Create a new file-based statistics storage
	/// </summary>
	/// <param name="filePath">Optional custom file path. If null, uses default location in the current working directory</param>
	public FileStatisticsStorage(string filePath = null)
	{
		_filePath = filePath ?? Path.Combine(
			Directory.GetCurrentDirectory(),
			"query-statistics.json"
		);

		// Ensure directory exists
		string directory = Path.GetDirectoryName(_filePath)!;
		if (!Directory.Exists(directory))
			Directory.CreateDirectory(directory);
	}

	/// <summary>
	/// Save a query execution statistic
	/// </summary>
	public async Task SaveAsync(QueryExecutionStatistics stats)
	{
		await _lock.WaitAsync();
		try
		{
			List<QueryExecutionStatistics> existing = await LoadAllAsync();
			existing.Add(stats);

			// Keep only last 1000 entries
			List<QueryExecutionStatistics> toSave = [.. existing
				.OrderByDescending(s => s.Timestamp)
				.Take(1000)];

			string json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions
			{
				WriteIndented = true,
				IncludeFields = true
			});

			await File.WriteAllTextAsync(_filePath, json);
		}
		catch
		{
			// Silently fail to not break query execution
			// File persistence is a best-effort feature
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// Load recent statistics
	/// </summary>
	public async Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count)
	{
		List<QueryExecutionStatistics> all = await LoadAllAsync();
		return [.. all.OrderByDescending(s => s.Timestamp).Take(count)];
	}

	/// <summary>
	/// Load statistics for a specific query hash within a time window
	/// </summary>
	public async Task<IList<QueryExecutionStatistics>> GetByQueryHashAsync(string queryHash, TimeSpan window)
	{
		DateTime cutoff = DateTime.UtcNow - window;
		List<QueryExecutionStatistics> all = await LoadAllAsync();
		return [.. all.Where(s => s.QueryHash == queryHash && s.Timestamp >= cutoff).OrderByDescending(s => s.Timestamp)];
	}

	/// <summary>
	/// Clear old statistics (older than specified timespan)
	/// </summary>
	public async Task ClearOldAsync(TimeSpan olderThan)
	{
		await _lock.WaitAsync();
		try
		{
			DateTime cutoff = DateTime.UtcNow - olderThan;
			List<QueryExecutionStatistics> existing = await LoadAllAsync();
			List<QueryExecutionStatistics> toKeep = [.. existing.Where(s => s.Timestamp >= cutoff)];

			string json = JsonSerializer.Serialize(toKeep, new JsonSerializerOptions
			{
				WriteIndented = true
			});

			await File.WriteAllTextAsync(_filePath, json);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// Load all statistics from file
	/// </summary>
	private async Task<List<QueryExecutionStatistics>> LoadAllAsync()
	{
		if (!File.Exists(_filePath))
			return [];

		try
		{
			string json = await File.ReadAllTextAsync(_filePath);
			return JsonSerializer.Deserialize<List<QueryExecutionStatistics>>(json)
				   ?? [];
		}
		catch (JsonException)
		{
			// If file is corrupted, return empty list
			return [];
		}
	}
}
