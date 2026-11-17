using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Universe.Builder.Strategies.Storage;

/// <summary>
/// File-based storage with bounded, batched JSON persistence
/// </summary>
public sealed class FileStatisticsStorage : IQueryStatisticsStorage, IDisposable
{
    private sealed record WriteRequest(
        QueryExecutionStatistics Statistics,
        TaskCompletionSource<bool> Completion = null,
        bool IsPeriodicFlush = false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Channel<WriteRequest> _writeQueue;
    private readonly Task _writerTask;
    private readonly Timer _flushTimer;
    private int _disposed;
    private int _periodicFlushQueued;
    private long _droppedWriteCount;
    private const int MaxStoredExecutions = 1000;
    private const int WriteQueueCapacity = 1000;
    private const int WriteBatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    internal int PendingWriteCount => _writeQueue.Reader.Count;
    internal long DroppedWriteCount => Interlocked.Read(ref _droppedWriteCount);

    /// <summary>
    /// Create a new file-based statistics storage
    /// </summary>
    /// <param name="filePath">Optional custom file path. If null, uses a platform-aware default
    /// (temp directory on Azure, application directory otherwise).
    /// <b>Warning:</b> do not pass a value derived from untrusted user input; the path is used
    /// directly for file and directory creation after normalization via <see cref="Path.GetFullPath(string)"/>.</param>
    public FileStatisticsStorage(string filePath = null)
    {
        _filePath = PlatformDetection.ValidateStoragePath(filePath ?? ResolveDefaultPath());

        // Ensure directory exists with restrictive permissions
        string directory = Path.GetDirectoryName(_filePath)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            PlatformDetection.SetRestrictivePermissions(directory);
        }

        _writeQueue = Channel.CreateBounded<WriteRequest>(new BoundedChannelOptions(WriteQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _writerTask = ProcessWritesAsync();
        _flushTimer = new Timer(RequestPeriodicFlush, null, FlushInterval, FlushInterval);
    }

    /// <summary>
    /// Resolves the default file path based on the runtime environment.
    /// On Azure (Functions / App Service), uses local temp storage to avoid
    /// SMB-mounted paths where file locking can be unreliable.
    /// <para>
    /// <b>Azure caveat:</b> Temp directories are local to the VM instance and are cleared on
    /// cold starts, scale events, or platform updates. The tuner starts fresh in these cases.
    /// For durable persistence across restarts, provide an explicit <c>filePath</c> pointing to
    /// a writable local disk.
    /// </para>
    /// </summary>
    internal static string ResolveDefaultPath()
    {
        if (PlatformDetection.IsAzureEnvironment())
            return Path.Combine(PlatformDetection.GetLocalTempDirectory(), "query-statistics.json");

        return Path.Combine(AppContext.BaseDirectory, "query-statistics.json");
    }

    /// <summary>
    /// Queue a query execution statistic for best-effort persistence.
    /// Records are dropped when the bounded telemetry queue is full.
    /// </summary>
    public Task SaveAsync(QueryExecutionStatistics stats)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return Task.CompletedTask;

        if (!_writeQueue.Writer.TryWrite(new WriteRequest(stats)))
            Interlocked.Increment(ref _droppedWriteCount);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Load recent statistics
    /// </summary>
    public async Task<IList<QueryExecutionStatistics>> LoadRecentAsync(int count)
    {
        await FlushPendingAsync();
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
        await FlushPendingAsync();
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
        await FlushPendingAsync();
        await _lock.WaitAsync();
        try
        {
            DateTime cutoff = DateTime.UtcNow - olderThan;
            List<QueryExecutionStatistics> existing = await LoadAllAsync();
            List<QueryExecutionStatistics> toKeep = [.. existing.Where(s => s.Timestamp >= cutoff)];

            string json = JsonSerializer.Serialize(toKeep, JsonOptions);

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
        catch (JsonException ex)
        {
            Trace.TraceWarning($"[UniverseQuery] Statistics file may be corrupted or tampered with ({_filePath}): {ex.Message}");
            return [];
        }
    }

    private async Task ProcessWritesAsync()
    {
        List<QueryExecutionStatistics> batch = new(WriteBatchSize);

        await foreach (WriteRequest request in _writeQueue.Reader.ReadAllAsync())
        {
            if (request.Statistics is not null)
                batch.Add(request.Statistics);

            if (batch.Count >= WriteBatchSize || request.Statistics is null)
            {
                await PersistBatchAsync(batch);
                batch.Clear();
            }

            request.Completion?.TrySetResult(true);
            if (request.IsPeriodicFlush)
                Interlocked.Exchange(ref _periodicFlushQueued, 0);
        }

        await PersistBatchAsync(batch);
    }

    private async Task PersistBatchAsync(List<QueryExecutionStatistics> batch)
    {
        if (batch.Count == 0)
            return;

        await _lock.WaitAsync();
        try
        {
            List<QueryExecutionStatistics> existing = await LoadAllAsync();
            existing.AddRange(batch);

            List<QueryExecutionStatistics> toSave = [.. existing
                .OrderByDescending(s => s.Timestamp)
                .Take(MaxStoredExecutions)];

            string json = JsonSerializer.Serialize(toSave, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
            PlatformDetection.SetRestrictivePermissions(_filePath);
        }
        catch (JsonException ex)
        {
            Trace.TraceWarning($"[UniverseQuery] File statistics serialization failed: {ex.Message}");
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"[UniverseQuery] File statistics save failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Trace.TraceWarning($"[UniverseQuery] File statistics save failed (access denied): {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task FlushPendingAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await _writeQueue.Writer.WriteAsync(new WriteRequest(null, completion));
            await completion.Task;
        }
        catch (ChannelClosedException)
        {
            // Disposal drains every accepted write before closing the channel.
        }
    }

    private void RequestPeriodicFlush(object state)
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _periodicFlushQueued, 1) != 0)
            return;

        if (!_writeQueue.Writer.TryWrite(new WriteRequest(null, IsPeriodicFlush: true)))
            Interlocked.Exchange(ref _periodicFlushQueued, 0);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _flushTimer.Dispose();
        _writeQueue.Writer.TryComplete();

        bool writerStopped = false;
        try
        {
            writerStopped = _writerTask.Wait(TimeSpan.FromSeconds(10));
            if (!writerStopped)
                Trace.TraceWarning("[UniverseQuery] Timed out draining file statistics during disposal.");
        }
        catch (AggregateException ex)
        {
            Trace.TraceWarning($"[UniverseQuery] Failed to drain file statistics during disposal: {ex.Message}");
        }

        if (writerStopped)
            _lock.Dispose();
    }
}
