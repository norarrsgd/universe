namespace Universe;

/// <summary>
/// Optional in-memory document cache configuration.
/// </summary>
public sealed class DocumentCacheOptions
{
    /// <summary>Default cache entry time-to-live.</summary>
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromMinutes(5);

    /// <summary>Default maximum number of cached entries.</summary>
    public const int DefaultMaxEntries = 1000;

    /// <summary>How long cached documents remain valid.</summary>
    public TimeSpan TimeToLive { get; }

    /// <summary>Maximum number of entries retained by the cache.</summary>
    public int MaxEntries { get; }

    /// <summary>Whether cache reads and writes clone document instances.</summary>
    public bool CloneDocuments { get; }

    /// <summary>Default maximum serialized size of one entry (1 MiB).</summary>
    public const int DefaultMaxEntryBytes = 1024 * 1024;

    /// <summary>Default total serialized size budget (32 MiB).</summary>
    public const long DefaultMaxTotalBytes = 32L * 1024 * 1024;

    /// <summary>Maximum serialized UTF-8 size of one cached document.</summary>
    public int MaxEntryBytes { get; }

    /// <summary>Maximum total serialized UTF-8 size retained by the cache.</summary>
    public long MaxTotalBytes { get; }

    internal Builder.Caching.DocumentCache Cache { get; }

    /// <summary>Create document cache options.</summary>
    public DocumentCacheOptions(TimeSpan? timeToLive = null, int maxEntries = DefaultMaxEntries, bool cloneDocuments = true)
        : this(timeToLive, maxEntries, cloneDocuments, DefaultMaxEntryBytes, DefaultMaxTotalBytes)
    {
    }

    /// <summary>Create cache options with serialized byte budgets in addition to the entry limit.</summary>
    public DocumentCacheOptions(TimeSpan? timeToLive, int maxEntries, bool cloneDocuments, int maxEntryBytes, long maxTotalBytes)
    {
        if (maxEntryBytes <= 0 || maxTotalBytes <= 0)
            throw new UniverseException("Document cache byte limits must be greater than zero.");

        MaxEntryBytes = maxEntryBytes;
        MaxTotalBytes = maxTotalBytes;
        TimeSpan resolvedTimeToLive = timeToLive ?? DefaultTimeToLive;
        if (resolvedTimeToLive <= TimeSpan.Zero)
            throw new UniverseException("Document cache time-to-live must be greater than zero.");

        if (maxEntries <= 0)
            throw new UniverseException("Document cache max entries must be greater than zero.");

        TimeToLive = resolvedTimeToLive;
        MaxEntries = maxEntries;
        CloneDocuments = cloneDocuments;
        Cache = new(this);
    }
}
