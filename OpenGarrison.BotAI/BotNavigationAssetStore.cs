using OpenGarrison.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.BotAI;

public static class BotNavigationAssetStore
{
    public const int CurrentFormatVersion = 2;
    private const string ShippedRelativeDirectory = "OpenGarrison.Core/Content/BotNav";
    private const string RuntimeCacheDirectoryName = "bot-nav";
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

    public static BotNavigationLoadResult LoadForLevel(SimpleLevel level, IReadOnlyList<BotNavigationProfile>? profiles = null)
    {
        ArgumentNullException.ThrowIfNull(level);

        var requestedProfiles = profiles ?? BotNavigationProfiles.All;
        var fingerprint = BotNavigationLevelFingerprint.Compute(level);
        var assets = new Dictionary<BotNavigationProfile, BotNavigationAsset>();
        var statuses = new List<BotNavigationAssetStatus>(requestedProfiles.Count);

        foreach (var profile in requestedProfiles.Distinct())
        {
            if (TryLoadAsset(level, profile, fingerprint, out var asset, out var status))
            {
                assets[profile] = asset!;
            }

            statuses.Add(status);
        }

        return new BotNavigationLoadResult(level.Name, level.MapAreaIndex, fingerprint, assets, statuses);
    }

    public static void SaveShipped(BotNavigationAsset asset, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, GetAssetFileName(asset.LevelName, asset.MapAreaIndex, asset.Profile));
        WriteAsset(outputPath, asset);
    }

    public static void SaveRuntimeCache(BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var cachePath = GetRuntimeCachePath(asset.LevelName, asset.MapAreaIndex, asset.Profile, asset.LevelFingerprint);
        WriteAsset(cachePath, asset);
    }

    public static string GetAssetFileName(string levelName, int mapAreaIndex, BotNavigationProfile profile)
    {
        return $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.{BotNavigationProfiles.GetFileToken(profile)}.botnav.json";
    }

    public static string GetRuntimeCachePath(string levelName, int mapAreaIndex, BotNavigationProfile profile, string levelFingerprint)
    {
        var cacheFileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.{BotNavigationProfiles.GetFileToken(profile)}.{TrimFingerprint(levelFingerprint)}.botnav.json";
        return RuntimePaths.GetConfigPath(Path.Combine(RuntimeCacheDirectoryName, cacheFileName));
    }

    public static string? ResolveShippedPath(string levelName, int mapAreaIndex, BotNavigationProfile profile)
    {
        var fileName = GetAssetFileName(levelName, mapAreaIndex, profile);
        var runtimePath = ContentRoot.GetPath("BotNav", fileName);
        if (File.Exists(runtimePath))
        {
            return runtimePath;
        }

        var projectPath = ProjectSourceLocator.FindFile($"{ShippedRelativeDirectory}/{fileName}");
        if (!string.IsNullOrWhiteSpace(projectPath) && File.Exists(projectPath))
        {
            return projectPath;
        }

        return null;
    }

    private static bool TryLoadAsset(
        SimpleLevel level,
        BotNavigationProfile profile,
        string fingerprint,
        out BotNavigationAsset? asset,
        out BotNavigationAssetStatus status)
    {
        var shippedPath = ResolveShippedPath(level.Name, level.MapAreaIndex, profile);
        if (!string.IsNullOrWhiteSpace(shippedPath)
            && TryReadAndValidate(shippedPath, level, profile, fingerprint, out asset, out var shippedMessage))
        {
            status = new BotNavigationAssetStatus(
                profile,
                IsLoaded: true,
                BotNavigationAssetSource.ShippedContent,
                shippedPath,
                shippedMessage,
                asset!.Nodes.Count,
                asset.Edges.Count);
            return true;
        }

        var runtimeCachePath = GetRuntimeCachePath(level.Name, level.MapAreaIndex, profile, fingerprint);
        if (File.Exists(runtimeCachePath)
            && TryReadAndValidate(runtimeCachePath, level, profile, fingerprint, out asset, out var cacheMessage))
        {
            status = new BotNavigationAssetStatus(
                profile,
                IsLoaded: true,
                BotNavigationAssetSource.RuntimeCache,
                runtimeCachePath,
                cacheMessage,
                asset!.Nodes.Count,
                asset.Edges.Count);
            return true;
        }

        asset = null;
        status = new BotNavigationAssetStatus(
            profile,
            IsLoaded: false,
            BotNavigationAssetSource.None,
            shippedPath ?? runtimeCachePath,
            shippedPath is null
                ? "no shipped asset found"
                : "shipped asset did not match current level fingerprint",
            NodeCount: 0,
            EdgeCount: 0);
        return false;
    }

    private static bool TryReadAndValidate(
        string path,
        SimpleLevel level,
        BotNavigationProfile profile,
        string fingerprint,
        out BotNavigationAsset? asset,
        out string message)
    {
        asset = null;
        message = string.Empty;

        try
        {
            var json = File.ReadAllText(path);
            asset = JsonSerializer.Deserialize<BotNavigationAsset>(json, SerializerOptions);
            if (asset is null)
            {
                message = "asset could not be deserialized";
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            message = ex.Message;
            return false;
        }

        if (asset.FormatVersion != CurrentFormatVersion)
        {
            message = $"format mismatch {asset.FormatVersion}";
            asset = null;
            return false;
        }

        if (!string.Equals(asset.LevelName, level.Name, StringComparison.OrdinalIgnoreCase)
            || asset.MapAreaIndex != level.MapAreaIndex
            || asset.Profile != profile)
        {
            message = "asset metadata mismatch";
            asset = null;
            return false;
        }

        if (!string.Equals(asset.LevelFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            message = "level fingerprint mismatch";
            asset = null;
            return false;
        }

        message = BuildSummary(asset);
        return true;
    }

    private static void WriteAsset(string path, BotNavigationAsset asset)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(asset, SerializerOptions);
        File.WriteAllText(path, json);
    }

    private static string BuildSummary(BotNavigationAsset asset)
    {
        return $"asset nodes={asset.Nodes.Count} edges={asset.Edges.Count} strategy={asset.BuildStrategy}";
    }

    private static string SanitizeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var sanitized = value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalid, '_');
        }

        return sanitized.Replace(' ', '-');
    }

    private static string TrimFingerprint(string fingerprint)
    {
        return string.IsNullOrWhiteSpace(fingerprint)
            ? "unknown"
            : fingerprint[..Math.Min(12, fingerprint.Length)].ToLowerInvariant();
    }
}
