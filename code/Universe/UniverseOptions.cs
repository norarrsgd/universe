using Universe.Builder.Strategies.Storage;

namespace Universe;

/// <summary>
/// Configuration options for Universe query optimization
/// </summary>
public sealed class UniverseOptions
{
	/// <summary>
	/// Storage backend for query statistics. Defaults to InMemoryStatisticsStorage.
	/// </summary>
	public IQueryStatisticsStorage StatisticsStorage { get; set; }

	/// <summary>
	/// Creates default Universe options with in-memory storage
	/// </summary>
	public UniverseOptions()
	{
		StatisticsStorage = null; // Will default to InMemoryStatisticsStorage in QueryTuner
	}

	/// <summary>
	/// Creates Universe options with file-based persistence
	/// </summary>
	/// <param name="statisticsFilePath">Optional custom path for statistics file. If null, uses default location.</param>
	/// <returns>UniverseOptions configured for file persistence</returns>
	public static UniverseOptions WithFilePersistence(string statisticsFilePath = null) => new()
	{
		StatisticsStorage = new FileStatisticsStorage(statisticsFilePath)
	};
}
