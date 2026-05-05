using System.Collections.Generic;

namespace OpenGarrison.Core;

/// <summary>
/// Client-side string cache that resolves cache IDs to strings.
/// Receives cache updates from server snapshots.
/// </summary>
public sealed class ClientSnapshotStringCache
{
    private readonly Dictionary<ushort, string> _cache = new();

    public void Clear()
    {
        _cache.Clear();
    }

    public void ApplyCacheUpdates(IReadOnlyDictionary<ushort, string>? updates)
    {
        if (updates == null || updates.Count == 0)
        {
            return;
        }

        foreach (var (id, value) in updates)
        {
            _cache[id] = value;
        }
    }

    public string Resolve(ushort cacheId, string fallbackValue)
    {
        if (cacheId == 0)
        {
            return fallbackValue;
        }

        return _cache.TryGetValue(cacheId, out var cached) ? cached : fallbackValue;
    }
}
