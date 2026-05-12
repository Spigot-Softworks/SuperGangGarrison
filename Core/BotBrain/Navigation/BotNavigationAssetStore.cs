using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.Core.BotBrain;

public static class BotNavigationAssetStore
{
    public const int CurrentFormatVersion = 75;

    private const string ShippedDirectoryName = "BotBrainNav";
    private const string RuntimeCacheDirectoryName = "botbrain-nav";

    private static readonly object AssetFileCacheSync = new();
    private static readonly Dictionary<string, CachedAssetFile> AssetFileCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object GraphCacheSync = new();
    private static readonly ConditionalWeakTable<SimpleLevel, CachedLevelGraph> GraphCache = new();
    private static readonly ConditionalWeakTable<SimpleLevel, CachedLevelFingerprint> FingerprintCache = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    static BotNavigationAssetStore()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static NavGraph LoadGraphOrBuild(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        lock (GraphCacheSync)
        {
            if (GraphCache.TryGetValue(level, out var cachedGraph)
                && string.Equals(cachedGraph.LevelFingerprint, ComputeLevelFingerprint(level), StringComparison.OrdinalIgnoreCase)
                && cachedGraph.FormatVersion == CurrentFormatVersion)
            {
                return cachedGraph.Graph;
            }

            return TryLoadShipped(level, out var asset)
                || TryLoadRuntimeCache(level, out asset)
                ? CacheGraph(level, asset, BotNavigationAssetBuilder.ToGraph(asset, level))
                : CacheGraph(level, BuildAndSaveRuntimeCache(level));
        }
    }

    public static bool TryLoadCachedGraph(SimpleLevel level, out NavGraph graph)
    {
        ArgumentNullException.ThrowIfNull(level);

        lock (GraphCacheSync)
        {
            if (GraphCache.TryGetValue(level, out var cachedGraph)
                && string.Equals(cachedGraph.LevelFingerprint, ComputeLevelFingerprint(level), StringComparison.OrdinalIgnoreCase)
                && cachedGraph.FormatVersion == CurrentFormatVersion)
            {
                graph = cachedGraph.Graph;
                return true;
            }

            if (TryLoadShipped(level, out var shippedAsset))
            {
                graph = CacheGraph(level, shippedAsset, BotNavigationAssetBuilder.ToGraph(shippedAsset, level));
                return true;
            }

            if (TryLoadRuntimeCache(level, out var cachedAsset))
            {
                graph = CacheGraph(level, cachedAsset, BotNavigationAssetBuilder.ToGraph(cachedAsset, level));
                return true;
            }
        }

        graph = null!;
        return false;
    }

    public static BotNavigationAsset LoadOrBuild(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        return TryLoadShipped(level, out var asset)
            || TryLoadRuntimeCache(level, out asset)
            ? asset
            : BuildAndSaveRuntimeCache(level);
    }

    public static BotNavigationAsset BuildAndSaveShippedSource(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var asset = BotNavigationAssetBuilder.BuildAsset(level);
        SaveShippedSource(asset);
        return asset;
    }

    public static BotNavigationAsset BuildAndSaveRuntimeCache(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var asset = BotNavigationAssetBuilder.BuildAsset(level);
        SaveRuntimeCache(asset);
        return asset;
    }

    public static bool TryLoadShipped(SimpleLevel level, out BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(level);
        return TryLoadFromPath(ResolveShippedPath(level), level, out asset);
    }

    public static bool TryLoadRuntimeCache(SimpleLevel level, out BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(level);
        return TryLoadFromPath(GetRuntimeCachePath(level), level, out asset);
    }

    public static BotNavigationAssetLoadDiagnostic GetLoadDiagnostic(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var expectedFingerprint = ComputeLevelFingerprint(level);
        var shippedPath = ResolveShippedPath(level);
        var runtimeCachePath = GetRuntimeCachePath(level);
        return new BotNavigationAssetLoadDiagnostic(
            ShippedPath: shippedPath,
            RuntimeCachePath: runtimeCachePath,
            ExpectedFingerprint: expectedFingerprint,
            ShippedStatus: InspectPath(shippedPath, level, expectedFingerprint),
            RuntimeCacheStatus: InspectPath(runtimeCachePath, level, expectedFingerprint));
    }

    public static void SaveRuntimeCache(BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        WriteAsset(GetRuntimeCachePath(asset), asset);
    }

    public static void SaveShippedSource(BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        WriteAsset(ResolveShippedPath(asset), asset);
    }

    public static string GetAssetFileName(string levelName, int mapAreaIndex)
    {
        return $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex).ToString(CultureInfo.InvariantCulture)}.botnav.json";
    }

    public static string ComputeLevelFingerprint(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        lock (GraphCacheSync)
        {
            if (FingerprintCache.TryGetValue(level, out var cached))
            {
                return cached.Fingerprint;
            }
        }

        var builder = new StringBuilder();
        Append(builder, level.Name);
        Append(builder, level.Mode.ToString());
        Append(builder, level.MapAreaIndex);
        Append(builder, level.MapScale);
        Append(builder, level.Bounds.Width);
        Append(builder, level.Bounds.Height);
        Append(builder, level.FloorY);
        Append(builder, level.Solids.Count);
        foreach (var solid in level.Solids.OrderBy(static solid => solid.Left).ThenBy(static solid => solid.Top))
        {
            Append(builder, solid.Left);
            Append(builder, solid.Top);
            Append(builder, solid.Width);
            Append(builder, solid.Height);
        }

        Append(builder, level.RoomObjects.Count);
        foreach (var roomObject in level.RoomObjects
                     .OrderBy(static roomObject => roomObject.Type)
                     .ThenBy(static roomObject => roomObject.Left)
                     .ThenBy(static roomObject => roomObject.Top))
        {
            Append(builder, roomObject.Type.ToString());
            Append(builder, roomObject.Left);
            Append(builder, roomObject.Top);
            Append(builder, roomObject.Width);
            Append(builder, roomObject.Height);
            Append(builder, roomObject.Team?.ToString() ?? string.Empty);
            Append(builder, roomObject.Value);
        }

        Append(builder, level.RedSpawns.Count);
        foreach (var spawn in level.RedSpawns.OrderBy(static spawn => spawn.X).ThenBy(static spawn => spawn.Y))
        {
            Append(builder, spawn.X);
            Append(builder, spawn.Y);
        }

        Append(builder, level.BlueSpawns.Count);
        foreach (var spawn in level.BlueSpawns.OrderBy(static spawn => spawn.X).ThenBy(static spawn => spawn.Y))
        {
            Append(builder, spawn.X);
            Append(builder, spawn.Y);
        }

        Append(builder, level.IntelBases.Count);
        foreach (var intelBase in level.IntelBases.OrderBy(static intel => intel.Team).ThenBy(static intel => intel.X).ThenBy(static intel => intel.Y))
        {
            Append(builder, intelBase.Team.ToString());
            Append(builder, intelBase.X);
            Append(builder, intelBase.Y);
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
        lock (GraphCacheSync)
        {
            FingerprintCache.Remove(level);
            FingerprintCache.Add(level, new CachedLevelFingerprint(fingerprint));
        }

        return fingerprint;
    }

    internal static bool IsCompatible(BotNavigationAsset asset, SimpleLevel level)
    {
        return asset.FormatVersion == CurrentFormatVersion
            && string.Equals(asset.LevelName, level.Name, StringComparison.OrdinalIgnoreCase)
            && asset.MapAreaIndex == level.MapAreaIndex
            && string.Equals(asset.LevelFingerprint, ComputeLevelFingerprint(level), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryLoadFromPath(string path, SimpleLevel level, out BotNavigationAsset asset)
    {
        asset = null!;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        var cacheKey = Path.GetFullPath(path);
        var fileInfo = new FileInfo(path);
        lock (AssetFileCacheSync)
        {
            if (AssetFileCache.TryGetValue(cacheKey, out var cached)
                && cached.LastWriteTicks == fileInfo.LastWriteTimeUtc.Ticks
                && cached.Length == fileInfo.Length
                && IsCompatible(cached.Asset, level))
            {
                asset = cached.Asset;
                return true;
            }
        }

        BotNavigationAsset? loaded;
        try
        {
            loaded = JsonSerializer.Deserialize<BotNavigationAsset>(File.ReadAllText(path), SerializerOptions);
        }
        catch
        {
            return false;
        }

        if (loaded is null || !IsCompatible(loaded, level))
        {
            return false;
        }

        lock (AssetFileCacheSync)
        {
            AssetFileCache[cacheKey] = new CachedAssetFile(fileInfo.LastWriteTimeUtc.Ticks, fileInfo.Length, loaded);
        }

        asset = loaded;
        return true;
    }

    private static string InspectPath(string path, SimpleLevel level, string expectedFingerprint)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "empty_path";
        }

        if (!File.Exists(path))
        {
            return "missing";
        }

        BotNavigationAsset? loaded;
        try
        {
            loaded = JsonSerializer.Deserialize<BotNavigationAsset>(File.ReadAllText(path), SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return $"read_error:{ex.GetType().Name}";
        }

        if (loaded is null)
        {
            return "empty_asset";
        }

        if (loaded.FormatVersion != CurrentFormatVersion)
        {
            return $"format_mismatch:{loaded.FormatVersion}!={CurrentFormatVersion}";
        }

        if (!string.Equals(loaded.LevelName, level.Name, StringComparison.OrdinalIgnoreCase))
        {
            return $"level_mismatch:{loaded.LevelName}!={level.Name}";
        }

        if (loaded.MapAreaIndex != level.MapAreaIndex)
        {
            return $"area_mismatch:{loaded.MapAreaIndex}!={level.MapAreaIndex}";
        }

        if (!string.Equals(loaded.LevelFingerprint, expectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return $"fingerprint_mismatch:{TrimFingerprint(loaded.LevelFingerprint)}!={TrimFingerprint(expectedFingerprint)}";
        }

        return $"compatible:nodes={loaded.Nodes.Count}:edges={loaded.Edges.Count}:anchors={loaded.Anchors.Count}";
    }

    private static NavGraph CacheGraph(SimpleLevel level, BotNavigationAsset asset)
    {
        return CacheGraph(level, asset, BotNavigationAssetBuilder.ToGraph(asset, level));
    }

    private static NavGraph CacheGraph(SimpleLevel level, BotNavigationAsset asset, NavGraph graph)
    {
        lock (GraphCacheSync)
        {
            GraphCache.Remove(level);
            GraphCache.Add(level, new CachedLevelGraph(asset.FormatVersion, asset.LevelFingerprint, graph));
        }

        return graph;
    }

    private static string ResolveShippedPath(SimpleLevel level)
    {
        return ContentRoot.GetPath(ShippedDirectoryName, GetAssetFileName(level.Name, level.MapAreaIndex));
    }

    private static string ResolveShippedPath(BotNavigationAsset asset)
    {
        return ContentRoot.GetPath(ShippedDirectoryName, GetAssetFileName(asset.LevelName, asset.MapAreaIndex));
    }

    private static string GetRuntimeCachePath(SimpleLevel level)
    {
        return GetRuntimeCachePath(level.Name, level.MapAreaIndex, ComputeLevelFingerprint(level));
    }

    private static string GetRuntimeCachePath(BotNavigationAsset asset)
    {
        return GetRuntimeCachePath(asset.LevelName, asset.MapAreaIndex, asset.LevelFingerprint);
    }

    private static string GetRuntimeCachePath(string levelName, int mapAreaIndex, string fingerprint)
    {
        var fileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex).ToString(CultureInfo.InvariantCulture)}.{TrimFingerprint(fingerprint)}.botnav.json";
        return RuntimePaths.GetConfigPath(Path.Combine(RuntimeCacheDirectoryName, fileName));
    }

    private static void WriteAsset(string path, BotNavigationAsset asset)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(asset, SerializerOptions));
        lock (AssetFileCacheSync)
        {
            AssetFileCache.Remove(Path.GetFullPath(path));
        }
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value);
        builder.Append('\n');
    }

    private static void Append(StringBuilder builder, int value)
        => Append(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder builder, float value)
        => Append(builder, value.ToString("R", CultureInfo.InvariantCulture));

    private static string SanitizeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unnamed";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? char.ToLowerInvariant(c) : '_');
        }

        return builder.Length == 0 ? "unnamed" : builder.ToString();
    }

    private static string TrimFingerprint(string fingerprint)
    {
        return string.IsNullOrWhiteSpace(fingerprint)
            ? "unknown"
            : fingerprint[..Math.Min(fingerprint.Length, 12)];
    }

    private readonly record struct CachedAssetFile(long LastWriteTicks, long Length, BotNavigationAsset Asset);

    private sealed record CachedLevelGraph(int FormatVersion, string LevelFingerprint, NavGraph Graph);

    private sealed record CachedLevelFingerprint(string Fingerprint);
}

public readonly record struct BotNavigationAssetLoadDiagnostic(
    string ShippedPath,
    string RuntimeCachePath,
    string ExpectedFingerprint,
    string ShippedStatus,
    string RuntimeCacheStatus);
