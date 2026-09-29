using System.Collections.Generic;
using OpenGarrison.Core;

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
