using System.Collections.Generic;

/// <summary>
/// Manages string-to-ID caching for snapshot compression.
/// Reduces bandwidth by sending frequently-used strings (like gameplay item IDs) once,
/// then referencing them by cache ID in subsequent snapshots.
/// </summary>
internal sealed class SnapshotStringCache
{
    private readonly Dictionary<string, ushort> _stringToId = new();
    private readonly Dictionary<ushort, string> _idToString = new();
    private ushort _nextId = 1; // 0 reserved for "not cached"

    public void Clear()
    {
        _stringToId.Clear();
        _idToString.Clear();
        _nextId = 1;
    }

    public ushort GetOrAddCacheId(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        if (_stringToId.TryGetValue(value, out var existingId))
        {
            return existingId;
        }

        var newId = _nextId++;
        _stringToId[value] = newId;
        _idToString[newId] = value;
        return newId;
    }

    public bool TryGetCacheId(string value, out ushort id)
    {
        if (string.IsNullOrEmpty(value))
        {
            id = 0;
            return false;
        }

        return _stringToId.TryGetValue(value, out id);
    }

    public bool TryGetString(ushort id, out string value)
    {
        return _idToString.TryGetValue(id, out value!);
    }

    public int Count => _stringToId.Count;
}

/// <summary>
/// Tracks which cache IDs have been sent to a specific client.
/// Builds cache update dictionaries containing only new strings for that client.
/// </summary>
internal sealed class ClientStringCacheTracker
{
    private readonly SnapshotStringCache _globalCache;
    private readonly HashSet<ushort> _sentCacheIds = new();

    public ClientStringCacheTracker(SnapshotStringCache globalCache)
    {
        _globalCache = globalCache;
    }

    public void Clear()
    {
        _sentCacheIds.Clear();
    }

    public Dictionary<ushort, string>? BuildCacheUpdatesForSnapshot(
        IReadOnlyList<(string value, ushort cacheId)> referencedStrings)
    {
        Dictionary<ushort, string>? updates = null;

        foreach (var (value, cacheId) in referencedStrings)
        {
            if (cacheId == 0 || _sentCacheIds.Contains(cacheId))
            {
                continue;
            }

            if (_globalCache.TryGetString(cacheId, out var cachedValue))
            {
                updates ??= new Dictionary<ushort, string>();
                updates[cacheId] = cachedValue;
                _sentCacheIds.Add(cacheId);
            }
        }

        return updates;
    }
}
