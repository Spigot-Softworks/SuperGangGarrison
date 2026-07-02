using OpenGarrison.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.BotAI;

public static class BotNavigationScoreRouteStore
{
    public const int CurrentFormatVersion = 2;
    private const string RoutesRelativeDirectory = "Core/Content/BotNavScoreRoutes";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };
    private static readonly object OverrideLock = new();
    private static readonly Dictionary<string, BotNavigationScoreRouteAsset?> RuntimeOverridesByLevelKey = new(StringComparer.OrdinalIgnoreCase);

    static BotNavigationScoreRouteStore()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static BotNavigationScoreRouteAsset? Load(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var levelKey = BuildLevelKey(level.Name, level.MapAreaIndex);
        lock (OverrideLock)
        {
            if (RuntimeOverridesByLevelKey.TryGetValue(levelKey, out var overrideAsset))
            {
                return overrideAsset;
            }
        }

        var path = ResolvePath(level.Name, level.MapAreaIndex);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var asset = JsonSerializer.Deserialize<BotNavigationScoreRouteAsset>(json, SerializerOptions);
            if (asset is null
                || asset.FormatVersion != CurrentFormatVersion
                || !string.Equals(asset.LevelName, level.Name, StringComparison.OrdinalIgnoreCase)
                || asset.MapAreaIndex != level.MapAreaIndex)
            {
                return null;
            }

            return asset;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string? ResolvePath(string levelName, int mapAreaIndex)
    {
        var fileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.botnavscore.json";
        var projectPath = ProjectSourceLocator.FindFile($"{RoutesRelativeDirectory}/{fileName}");
        if (!string.IsNullOrWhiteSpace(projectPath) && File.Exists(projectPath))
        {
            return projectPath;
        }

        var runtimePath = ContentRoot.GetPath("BotNavScoreRoutes", fileName);
        return File.Exists(runtimePath) ? runtimePath : null;
    }

    public static string ResolveWritablePath(string levelName, int mapAreaIndex)
    {
        var fileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.botnavscore.json";
        var existingPath = ResolvePath(levelName, mapAreaIndex);
        if (!string.IsNullOrWhiteSpace(existingPath))
        {
            return existingPath;
        }

        var projectDirectory = ProjectSourceLocator.FindDirectory(RoutesRelativeDirectory);
        if (!string.IsNullOrWhiteSpace(projectDirectory))
        {
            return Path.Combine(projectDirectory, fileName);
        }

        return ContentRoot.GetPath("BotNavScoreRoutes", fileName);
    }

    public static void Save(BotNavigationScoreRouteAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var path = ResolveWritablePath(asset.LevelName, asset.MapAreaIndex);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(asset, SerializerOptions);
        File.WriteAllText(path, json);
    }

    public static void SetRuntimeOverride(string levelName, int mapAreaIndex, BotNavigationScoreRouteAsset? asset)
    {
        var levelKey = BuildLevelKey(levelName, mapAreaIndex);
        lock (OverrideLock)
        {
            RuntimeOverridesByLevelKey[levelKey] = asset;
        }
    }

    public static void ClearRuntimeOverride(string levelName, int mapAreaIndex)
    {
        var levelKey = BuildLevelKey(levelName, mapAreaIndex);
        lock (OverrideLock)
        {
            RuntimeOverridesByLevelKey.Remove(levelKey);
        }
    }

    private static string BuildLevelKey(string levelName, int mapAreaIndex)
    {
        return $"{levelName}|{Math.Max(1, mapAreaIndex)}";
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
}
