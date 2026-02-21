using System.Diagnostics;
using System.Text.Json;

namespace Universe.Builder.Strategies.Storage;

/// <summary>
/// File-based storage (JSON persistence)
/// </summary>
public sealed class FileStatisticsStorage : IQueryStatisticsStorage, IDisposable
{
	private readonly string _filePath;
	private readonly SemaphoreSlim _lock = new(1, 1);

	/// <summary>
	/// Create a new file-based statistics storage
	/// </summary>
	/// <param name="filePath">Optional custom file path. If null, uses a platform-aware default
	/// (temp directory on Azure, application directory otherwise).</param>
	public FileStatisticsStorage(string filePath = null)
	{
		_filePath = Path.GetFullPath(filePath ?? ResolveDefaultPath());

		// Ensure directory exists
		string directory = Path.GetDirectoryName(_filePath)!;
		if (!Directory.Exists(directory))
			Directory.CreateDirectory(directory);
	}

	/// <summary>
	/// Resolves the default file path based on the runtime environment.
	/// On Azure (Functions / App Service), uses local temp storage to avoid
	/// SMB-mounted paths where file locking can be unreliable.
	/// </summary>
	internal static string ResolveDefaultPath()
	{
		if (SqliteStatisticsStorage.IsAzureEnvironment())
		{
			string localTemp = Environment.GetEnvironmentVariable("TMP")
				?? Environment.GetEnvironmentVariable("TEMP")
				?? Path.GetTempPath();

			return Path.Combine(localTemp, "query-statistics.json");
		}

		return Path.Combine(AppContext.BaseDirectory, "query-statistics.json");
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
		catch (JsonException ex)
		{
			Trace.TraceWarning($"[UniverseQuery] File statistics serialization failed: {ex.Message}");
		}
		catch (SystemException ex)
		{
			Trace.TraceWarning($"[UniverseQuery] File statistics save failed: {ex.Message}");
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
		await _lock.WaitAsync();
		try
		{
			List<QueryExecutionStatistics> all = await LoadAllAsync();
			return [.. all.OrderByDescending(s => s.Timestamp).Take(count)];
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// Load statistics for a specific query hash within a time window
	/// </summary>
	public async Task<IList<QueryExecutionStatistics>> GetByQueryHashAsync(string queryHash, TimeSpan window)
	{
		await _lock.WaitAsync();
		try
		{
			DateTime cutoff = DateTime.UtcNow - window;
			List<QueryExecutionStatistics> all = await LoadAllAsync();
			return [.. all.Where(s => s.QueryHash == queryHash && s.Timestamp >= cutoff).OrderByDescending(s => s.Timestamp)];
		}
		finally
		{
			_lock.Release();
		}
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

	/// <inheritdoc/>
	public void Dispose() => _lock.Dispose();
}
