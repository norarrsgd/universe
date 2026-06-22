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

    internal Builder.Caching.DocumentCache Cache { get; }

    /// <summary>Create document cache options.</summary>
    public DocumentCacheOptions(TimeSpan? timeToLive = null, int maxEntries = DefaultMaxEntries, bool cloneDocuments = true)
    {
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
