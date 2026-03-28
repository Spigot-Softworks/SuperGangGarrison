using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Core;

public readonly record struct CustomMapDescriptor(
    string LevelName,
    string LocalFilePath,
    string SourceUrl,
    string ContentHash,
    string LegacyMd5Hash,
    string Sha256Hash);

public static class CustomMapDescriptorResolver
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, CachedDescriptor> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryResolve(string levelName, out CustomMapDescriptor descriptor)
    {
        descriptor = default;
        if (!CustomMapLocatorStore.TryNormalizeLevelName(levelName, out var normalizedLevelName))
        {
            return false;
        }

        var mapPath = CustomMapLocatorStore.GetMapPath(normalizedLevelName);
        if (!File.Exists(mapPath))
        {
            return false;
        }

        var lastWriteUtcTicks = File.GetLastWriteTimeUtc(mapPath).Ticks;
        var locatorPath = Path.Combine(RuntimePaths.MapsDirectory, $"{normalizedLevelName}.locator");
        var locatorLastWriteUtcTicks = File.Exists(locatorPath)
            ? File.GetLastWriteTimeUtc(locatorPath).Ticks
            : 0L;
        lock (Sync)
        {
            if (Cache.TryGetValue(normalizedLevelName, out var cached)
                && cached.LastWriteUtcTicks == lastWriteUtcTicks)
            {
                if (cached.LocatorLastWriteUtcTicks == locatorLastWriteUtcTicks)
                {
                    descriptor = cached.Descriptor;
                    return true;
                }
            }
        }

        var sha256Hash = CustomMapHashService.ComputeSha256(mapPath);
        var md5Hash = CustomMapHashService.ComputeMd5(mapPath);
        var metadata = CustomMapLocatorStore.TryReadMapMetadata(normalizedLevelName);
        var sourceUrl = metadata?.SourceUrl ?? string.Empty;
        if (sourceUrl.Length > 0
            && (!string.Equals(metadata?.Md5Hash, md5Hash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(metadata?.Sha256Hash, sha256Hash, StringComparison.OrdinalIgnoreCase)))
        {
            CustomMapLocatorStore.WriteMapMetadata(normalizedLevelName, new CustomMapLocatorMetadata(sourceUrl, md5Hash, sha256Hash));
            locatorLastWriteUtcTicks = File.GetLastWriteTimeUtc(locatorPath).Ticks;
        }

        var resolved = new CustomMapDescriptor(normalizedLevelName, mapPath, sourceUrl, md5Hash, md5Hash, sha256Hash);
        lock (Sync)
        {
            Cache[normalizedLevelName] = new CachedDescriptor(lastWriteUtcTicks, locatorLastWriteUtcTicks, resolved);
        }

        descriptor = resolved;
        return true;
    }

    private sealed record CachedDescriptor(long LastWriteUtcTicks, long LocatorLastWriteUtcTicks, CustomMapDescriptor Descriptor);
}
