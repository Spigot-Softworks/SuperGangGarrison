using System.Collections.Generic;
using OpenGarrison.Core;

/// <summary>
/// Repeats referenced cache IDs until a snapshot carrying the mapping is acknowledged.
/// </summary>
internal sealed class ClientStringCacheTracker
{
    private readonly SnapshotStringCache _globalCache;

    public ClientStringCacheTracker(SnapshotStringCache globalCache)
    {
        _globalCache = globalCache;
    }

    public Dictionary<ushort, string>? BuildCacheUpdatesForSnapshot(
        IReadOnlyList<(string value, ushort cacheId)> referencedStrings,
        ClientSession client)
    {
        Dictionary<ushort, string>? updates = null;

        foreach (var (value, cacheId) in referencedStrings)
        {
            if (cacheId == 0 || client.HasAcknowledgedStringCacheId(cacheId))
            {
                continue;
            }

            if (_globalCache.TryGetString(cacheId, out var cachedValue))
            {
                updates ??= new Dictionary<ushort, string>();
                updates[cacheId] = cachedValue;
            }
        }

        return updates;
    }
}
