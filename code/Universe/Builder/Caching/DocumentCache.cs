using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Universe.Builder.Caching;

internal readonly record struct DocumentCacheKey(string Value);

internal enum DocumentCacheOperation
{
    PointRead,
    SingleQuery
}

internal sealed class DocumentCache(DocumentCacheOptions options)
{
    internal const int MaxQueryParameterSignatureBytes = 64 * 1024;
    private const string ParameterTypeDomain = "parameter-type";
    private const string ParameterValueDomain = "parameter-value";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true
    };

    private readonly object _sync = new();
    private readonly Dictionary<DocumentCacheKey, LinkedListNode<DocumentCacheEntry>> _entries = [];
    private readonly LinkedList<DocumentCacheEntry> _insertionOrder = [];
    private readonly ConditionalWeakTable<CosmosClient, string> _clientNamespaces = new();

    // Fixed stripes bound mutation metadata; hash collisions only suppress an unrelated read fill.
    private readonly long[] _generations = new long[256];
    private long _serializedBytes;

    internal long CaptureGeneration(string scopeHash)
    {
        lock (_sync)
            return _generations[GenerationIndex(scopeHash)];
    }

    private static int GenerationIndex(string scopeHash) => (int)((uint)StringComparer.Ordinal.GetHashCode(scopeHash) % 256);

    internal string GetClientNamespace(CosmosClient client)
        => _clientNamespaces.GetValue(client, static _ => Guid.NewGuid().ToString("N"));

    internal string CreateScopeHash(string clientNamespace, string database, string container, Type sourceType)
    {
        using IncrementalHash hasher = CreateKeyHasher("scope");
        AppendStringComponent(hasher, "client", clientNamespace);
        AppendStringComponent(hasher, "database", database);
        AppendStringComponent(hasher, "container", container);
        AppendStringComponent(hasher, "source-type", sourceType.AssemblyQualifiedName);
        return FinishHash(hasher);
    }

    internal DocumentCacheKey CreatePointKey(
        string clientNamespace,
        string database,
        string container,
        Type sourceType,
        Type resultType,
        string id,
        IReadOnlyList<string> partitionKeys)
    {
        using IncrementalHash hasher = CreateKeyHasher("point");
        AppendStringComponent(hasher, "client", clientNamespace);
        AppendStringComponent(hasher, "database", database);
        AppendStringComponent(hasher, "container", container);
        AppendStringComponent(hasher, "source-type", sourceType.AssemblyQualifiedName);
        AppendStringComponent(hasher, "result-type", resultType.AssemblyQualifiedName);
        AppendStringComponent(hasher, "id", id);
        foreach (string partitionKey in partitionKeys)
            AppendStringComponent(hasher, "partition-key", partitionKey);
        return new(FinishHash(hasher));
    }

    internal DocumentCacheKey CreateQueryKey(
        string clientNamespace,
        string database,
        string container,
        Type sourceType,
        Type resultType,
        string queryText,
        IReadOnlyList<(string Name, object Value)> parameters)
    {
        (string normalizedQuery, IReadOnlyList<string> parameterNames) = QueryParameterNormalizer.Normalize(queryText);
        Dictionary<string, object> bindings = parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Value, StringComparer.Ordinal);
        long signatureBytes = 0;
        foreach (string name in parameterNames)
        {
            if (!bindings.TryGetValue(name, out object value))
                throw new InvalidOperationException("Cannot cache a query with an unresolved parameter binding.");

            long parameterBytes = GetParameterSignatureByteCount(value);
            if (parameterBytes > MaxQueryParameterSignatureBytes - signatureBytes)
                throw new NotSupportedException($"Query parameter signatures larger than {MaxQueryParameterSignatureBytes} bytes are not cached.");
            signatureBytes += parameterBytes;
        }

        using IncrementalHash hasher = CreateKeyHasher("query");
        AppendStringComponent(hasher, "client", clientNamespace);
        AppendStringComponent(hasher, "database", database);
        AppendStringComponent(hasher, "container", container);
        AppendStringComponent(hasher, "source-type", sourceType.AssemblyQualifiedName);
        AppendStringComponent(hasher, "result-type", resultType.AssemblyQualifiedName);
        AppendStringComponent(hasher, "query-text", normalizedQuery);
        foreach (string name in parameterNames)
            AppendParameter(hasher, bindings[name]);

        return new(FinishHash(hasher));
    }

    internal bool TryGet<T>(DocumentCacheKey key, out T value)
        => TryGet(key, out value, out _);

    internal bool TryGet<T>(DocumentCacheKey key, out T value, out string eTag)
    {
        value = default;
        eTag = null;

        DocumentCacheEntry entry;
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out LinkedListNode<DocumentCacheEntry> node))
                return false;

            entry = node.Value;
            if (DateTimeOffset.UtcNow - entry.CreatedOn >= options.TimeToLive)
            {
                RemoveNode(node);
                return false;
            }
        }

        try
        {
            value = CloneIfNeeded<T>(entry.Value);
            eTag = entry.ETag;
            return true;
        }
        catch (SystemException)
        {
            lock (_sync)
            {
                if (_entries.TryGetValue(key, out LinkedListNode<DocumentCacheEntry> node) && ReferenceEquals(node.Value, entry))
                    RemoveNode(node);
            }
            value = default;
            return false;
        }
    }

    internal void Set(DocumentCacheKey key, DocumentCacheOperation operation, string scopeHash, object value, string eTag = null, long? expectedGeneration = null)
    {
        object valueToStore;
        int serializedBytes;
        try
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, value?.GetType() ?? typeof(object), JsonOptions);
            serializedBytes = json.Length;
            valueToStore = options.CloneDocuments && value is not null && serializedBytes <= options.MaxEntryBytes && serializedBytes <= options.MaxTotalBytes
                ? JsonSerializer.Deserialize(json, value.GetType(), JsonOptions)
                : value;
        }
        catch (SystemException)
        {
            return;
        }

        lock (_sync)
        {
            int generationIndex = GenerationIndex(scopeHash);
            if (expectedGeneration.HasValue && expectedGeneration.Value != _generations[generationIndex])
                return;
            if (!expectedGeneration.HasValue)
                _generations[generationIndex]++;

            _entries.TryGetValue(key, out LinkedListNode<DocumentCacheEntry> node);
            if (serializedBytes > options.MaxEntryBytes || serializedBytes > options.MaxTotalBytes)
            {
                if (node is not null)
                    RemoveNode(node);
                return;
            }

            DocumentCacheEntry entry = new(key, valueToStore, DateTimeOffset.UtcNow, operation, scopeHash, eTag, serializedBytes);
            if (node is not null)
            {
                _serializedBytes -= node.Value.SerializedBytes;
                node.Value = entry;
            }
            else
                _entries.Add(key, _insertionOrder.AddLast(entry));
            _serializedBytes += serializedBytes;
            while (_entries.Count > options.MaxEntries || _serializedBytes > options.MaxTotalBytes)
                RemoveNode(_insertionOrder.First!);
        }
    }

    internal void Remove(DocumentCacheKey key, string scopeHash = null)
    {
        lock (_sync)
        {
            if (scopeHash is not null)
                _generations[GenerationIndex(scopeHash)]++;
            else if (_entries.TryGetValue(key, out LinkedListNode<DocumentCacheEntry> existing))
                _generations[GenerationIndex(existing.Value.ScopeHash)]++;
            if (_entries.TryGetValue(key, out LinkedListNode<DocumentCacheEntry> node))
                RemoveNode(node);
        }
    }

    internal void ClearQueries(string scopeHash)
    {
        lock (_sync)
        {
            _generations[GenerationIndex(scopeHash)]++;
            LinkedListNode<DocumentCacheEntry> node = _insertionOrder.First;
            while (node is not null)
            {
                LinkedListNode<DocumentCacheEntry> next = node.Next;
                if (node.Value.ScopeHash == scopeHash && node.Value.Operation is DocumentCacheOperation.SingleQuery)
                    RemoveNode(node);
                node = next;
            }
        }
    }

    private void RemoveNode(LinkedListNode<DocumentCacheEntry> node)
    {
        _serializedBytes -= node.Value.SerializedBytes;
        _entries.Remove(node.Value.Key);
        _insertionOrder.Remove(node);
    }

    private T CloneIfNeeded<T>(object value)
    {
        if (!options.CloneDocuments || value is null)
            return (T)value;

        Type valueType = value.GetType();
        string json = JsonSerializer.Serialize(value, valueType, JsonOptions);
        return (T)JsonSerializer.Deserialize(json, valueType, JsonOptions);
    }

    private static long GetParameterSignatureByteCount(object value)
    {
        Type type = value?.GetType();
        string typeName = type?.AssemblyQualifiedName ?? type?.FullName ?? type?.Name;
        long valueBytes = value switch
        {
            null => 0,
            string text => (long)text.Length * sizeof(char),
            bool or byte or sbyte => sizeof(byte),
            short or ushort => sizeof(short),
            int or uint or float => sizeof(int),
            long or ulong or double => sizeof(long),
            decimal => sizeof(decimal),
            _ => throw new NotSupportedException("Query caching supports only null, string, boolean, and built-in numeric parameter values.")
        };

        long typeNameBytes = (long)(typeName?.Length ?? 0) * sizeof(char);
        return GetComponentByteCount(ParameterTypeDomain, typeNameBytes)
            + GetComponentByteCount(ParameterValueDomain, valueBytes);
    }

    private static long GetComponentByteCount(string domain, long valueBytes)
        => ((long)domain.Length * sizeof(char)) + valueBytes + (2 * sizeof(long));

    private static IncrementalHash CreateKeyHasher(string operation)
    {
        IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendStringComponent(hasher, "operation", operation);
        return hasher;
    }

    private static void AppendParameter(IncrementalHash hasher, object value)
    {
        Type type = value?.GetType();
        AppendStringComponent(hasher, ParameterTypeDomain, type?.AssemblyQualifiedName ?? type?.FullName ?? type?.Name);

        switch (value)
        {
            case null:
                AppendStringComponent(hasher, ParameterValueDomain, null);
                break;
            case string text:
                // Hash exact UTF-16 code units so unpaired surrogates remain distinct.
                AppendStringComponent(hasher, ParameterValueDomain, text);
                break;
            case bool number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case byte number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case sbyte number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case short number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case ushort number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case int number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case uint number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case long number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case ulong number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case float number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case double number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            case decimal number:
                AppendUnmanagedComponent(hasher, ParameterValueDomain, number);
                break;
            default:
                throw new NotSupportedException("Query caching supports only null, string, boolean, and built-in numeric parameter values.");
        }
    }

    private static void AppendStringComponent(IncrementalHash hasher, string domain, string value)
    {
        AppendUtf16(hasher, domain);
        AppendUtf16(hasher, value);
    }

    private static void AppendUnmanagedComponent<T>(IncrementalHash hasher, string domain, T value) where T : unmanaged
    {
        AppendUtf16(hasher, domain);
        Span<T> values = stackalloc T[1];
        values[0] = value;
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(values);
        AppendLength(hasher, bytes.Length);
        hasher.AppendData(bytes);
    }

    private static void AppendUtf16(IncrementalHash hasher, string value)
    {
        if (value is null)
        {
            AppendLength(hasher, -1);
            return;
        }

        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(value.AsSpan());
        AppendLength(hasher, bytes.Length);
        hasher.AppendData(bytes);
    }

    private static void AppendLength(IncrementalHash hasher, long length)
    {
        Span<long> values = stackalloc long[1];
        values[0] = length;
        hasher.AppendData(MemoryMarshal.AsBytes(values));
    }

    private static string FinishHash(IncrementalHash hasher)
    {
        byte[] hash = hasher.GetHashAndReset();
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record DocumentCacheEntry(
        DocumentCacheKey Key,
        object Value,
        DateTimeOffset CreatedOn,
        DocumentCacheOperation Operation,
        string ScopeHash,
        string ETag,
        int SerializedBytes);
}
