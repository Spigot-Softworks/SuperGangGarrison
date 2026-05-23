using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Core;

public readonly record struct CustomMapDescriptor(
    string LevelName,
    string LocalFilePath,
    CustomMapSourceKind SourceKind,
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

        var sourceKind = CustomMapSourceKind.LegacyPng;
        var contentPath = CustomMapLocatorStore.GetMapPath(normalizedLevelName);
        if (!File.Exists(contentPath))
        {
            if (!CustomMapLocatorStore.TryGetPackageManifestPath(normalizedLevelName, out contentPath))
            {
                return false;
            }

            sourceKind = CustomMapSourceKind.Package;
        }

        var lastWriteUtcTicks = ResolveContentLastWriteTicks(contentPath, sourceKind);
        var locatorPath = CustomMapLocatorStore.GetLocatorPath(normalizedLevelName);
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

        var sha256Hash = sourceKind == CustomMapSourceKind.Package
            ? CustomMapHashService.ComputePackageSha256(contentPath)
            : CustomMapHashService.ComputeSha256(contentPath);
        if (sourceKind == CustomMapSourceKind.Package && string.IsNullOrWhiteSpace(sha256Hash))
        {
            return false;
        }

        var md5Hash = sourceKind == CustomMapSourceKind.LegacyPng
            ? CustomMapHashService.ComputeMd5(contentPath)
            : string.Empty;
        var metadata = CustomMapLocatorStore.TryReadMapMetadata(normalizedLevelName);
        var sourceUrl = metadata?.SourceUrl ?? string.Empty;
        if (sourceUrl.Length > 0
            && ((!string.IsNullOrWhiteSpace(md5Hash)
                    && !string.Equals(metadata?.Md5Hash, md5Hash, StringComparison.OrdinalIgnoreCase))
                || !string.Equals(metadata?.Sha256Hash, sha256Hash, StringComparison.OrdinalIgnoreCase)))
        {
            CustomMapLocatorStore.WriteMapMetadata(normalizedLevelName, new CustomMapLocatorMetadata(sourceUrl, md5Hash, sha256Hash));
            locatorLastWriteUtcTicks = File.GetLastWriteTimeUtc(locatorPath).Ticks;
        }

        var contentHash = sourceKind == CustomMapSourceKind.Package
            ? $"sha256:{sha256Hash}"
            : md5Hash;
        var resolved = new CustomMapDescriptor(normalizedLevelName, contentPath, sourceKind, sourceUrl, contentHash, md5Hash, sha256Hash);
        lock (Sync)
        {
            Cache[normalizedLevelName] = new CachedDescriptor(lastWriteUtcTicks, locatorLastWriteUtcTicks, resolved);
        }

        descriptor = resolved;
        return true;
    }

    private static long ResolveContentLastWriteTicks(string contentPath, CustomMapSourceKind sourceKind)
    {
        if (sourceKind != CustomMapSourceKind.Package)
        {
            return File.GetLastWriteTimeUtc(contentPath).Ticks;
        }

        var files = CustomMapPackageImporter.GetPackageContentFiles(contentPath);
        return files.Count == 0
            ? File.GetLastWriteTimeUtc(contentPath).Ticks
            : files.Max(file => File.GetLastWriteTimeUtc(file.FullPath).Ticks);
    }

    private sealed record CachedDescriptor(long LastWriteUtcTicks, long LocatorLastWriteUtcTicks, CustomMapDescriptor Descriptor);
}
