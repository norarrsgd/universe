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
        => HashParts(["scope", clientNamespace, database, container, sourceType.AssemblyQualifiedName]);

    internal DocumentCacheKey CreatePointKey(
        string clientNamespace,
        string database,
        string container,
        Type sourceType,
        Type resultType,
        string id,
        IReadOnlyList<string> partitionKeys)
        => new(HashParts([
            "point",
            clientNamespace,
            database,
            container,
            sourceType.AssemblyQualifiedName,
            resultType.AssemblyQualifiedName,
            id,
            .. partitionKeys
        ]));

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
        List<string> parts =
        [
            "query",
            clientNamespace,
            database,
            container,
            sourceType.AssemblyQualifiedName,
            resultType.AssemblyQualifiedName,
            normalizedQuery
        ];

        Dictionary<string, object> bindings = parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Value, StringComparer.Ordinal);
        foreach (string name in parameterNames)
        {
            if (!bindings.TryGetValue(name, out object value))
                throw new InvalidOperationException("Cannot cache a query with an unresolved parameter binding.");
            parts.Add(value?.GetType().AssemblyQualifiedName);
            parts.Add(SerializeForKey(value));
        }

        return new(HashParts(parts));
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
            value = CloneIfNeeded<T>(entry.Value, entry.Serializer);
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

    internal void Set<T>(
        DocumentCacheKey key,
        DocumentCacheOperation operation,
        string scopeHash,
        T value,
        string eTag = null,
        long? expectedGeneration = null,
        CosmosSerializer serializer = null,
        bool normalize = false)
    {
        object valueToStore;
        int serializedBytes;
        try
        {
            byte[] json = Serialize(value, serializer);
            serializedBytes = json.Length;
            valueToStore = (options.CloneDocuments || normalize) && value is not null && serializedBytes <= options.MaxEntryBytes && serializedBytes <= options.MaxTotalBytes
                ? Deserialize(json, value, serializer)
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

            DocumentCacheEntry entry = new(key, valueToStore, DateTimeOffset.UtcNow, operation, scopeHash, eTag, serializedBytes, serializer);
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

    private T CloneIfNeeded<T>(object value, CosmosSerializer serializer)
    {
        if (!options.CloneDocuments || value is null)
            return (T)value;

        if (serializer is not null)
        {
            byte[] payload = Serialize((T)value, serializer);
            return Deserialize<T>(payload, serializer);
        }

        Type valueType = value.GetType();
        string json = JsonSerializer.Serialize(value, valueType, JsonOptions);
        return (T)JsonSerializer.Deserialize(json, valueType, JsonOptions);
    }

    private static byte[] Serialize<T>(T value, CosmosSerializer serializer)
    {
        if (serializer is null)
            return JsonSerializer.SerializeToUtf8Bytes(value, value is null ? typeof(T) : value.GetType(), JsonOptions);

        using Stream payload = serializer.ToStream(value) ?? throw new InvalidOperationException("The Cosmos serializer returned no document stream.");
        using MemoryStream copy = new();
        payload.CopyTo(copy);
        return copy.ToArray();
    }

    private static object Deserialize<T>(byte[] payload, T value, CosmosSerializer serializer)
    {
        if (serializer is not null)
            return Deserialize<T>(payload, serializer);

        return JsonSerializer.Deserialize(payload, value.GetType(), JsonOptions);
    }

    private static T Deserialize<T>(byte[] payload, CosmosSerializer serializer)
    {
        using MemoryStream stream = new(payload, writable: false);
        return serializer.FromStream<T>(stream);
    }

    private static string SerializeForKey(object value)
    {
        if (value is null)
            return "null";

        // Preserve exact UTF-16 code units; JSON serialization replaces unpaired surrogates.
        if (value is string text)
            return Convert.ToHexString(MemoryMarshal.AsBytes(text.AsSpan()));

        // Object/collection serialization can differ from Cosmos parameter serialization; bypass caching rather than risk a collision.
        if (value is not (bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal))
            throw new NotSupportedException("Query caching supports only null, string, boolean, and built-in numeric parameter values.");

        return JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
    }

    private static string HashParts(IEnumerable<string> parts)
    {
        string payload = JsonSerializer.Serialize(parts, JsonOptions);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record DocumentCacheEntry(
        DocumentCacheKey Key,
        object Value,
        DateTimeOffset CreatedOn,
        DocumentCacheOperation Operation,
        string ScopeHash,
        string ETag,
        int SerializedBytes,
        CosmosSerializer Serializer);
}
