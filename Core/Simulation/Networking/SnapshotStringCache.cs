using System.Collections.Generic;

namespace OpenGarrison.Core;

/// <summary>
/// Manages string-to-ID caching for snapshot compression.
/// Reduces bandwidth by sending frequently-used strings (like gameplay item IDs) once,
/// then referencing them by cache ID in subsequent snapshots.
/// </summary>
public sealed class SnapshotStringCache
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


    public bool TryGetString(ushort id, out string value)
    {
        return _idToString.TryGetValue(id, out value!);
    }

    public int Count => _stringToId.Count;
}
