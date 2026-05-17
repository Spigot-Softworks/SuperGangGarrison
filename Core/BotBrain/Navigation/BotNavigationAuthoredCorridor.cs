using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.Core.BotBrain;

public sealed class BotNavigationAuthoredCorridorAsset
{
    public int FormatVersion { get; set; } = 1;

    public string LevelName { get; set; } = string.Empty;

    public int MapAreaIndex { get; set; }

    public List<BotNavigationAuthoredCorridorEntry> Corridors { get; set; } = [];
}

public sealed class BotNavigationAuthoredCorridorEntry
{
    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlayerTeam Team { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlayerClass PlayerClass { get; set; }

    public List<BotNavigationAuthoredCorridorWaypoint> Waypoints { get; set; } = [];
}

public sealed class BotNavigationAuthoredCorridorWaypoint
{
    public float X { get; set; }

    public float Y { get; set; }

    public bool IsGrounded { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public static class BotNavigationAuthoredCorridorStore
{
    private const string AuthoredCorridorDirectoryName = "BotBrainCorridors";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static bool TryLoad(SimpleLevel level, out BotNavigationAuthoredCorridorAsset asset)
    {
        ArgumentNullException.ThrowIfNull(level);

        asset = null!;
        var path = ResolvePath(level.Name, level.MapAreaIndex);
        if (!BotBrainJsonAssetIO.TryResolveReadablePath(path, out var readablePath))
        {
            return false;
        }

        try
        {
            asset = BotBrainJsonAssetIO.Deserialize<BotNavigationAuthoredCorridorAsset>(readablePath, SerializerOptions)!;
        }
        catch
        {
            return false;
        }

        return asset is not null
            && string.Equals(asset.LevelName, level.Name, StringComparison.OrdinalIgnoreCase)
            && asset.MapAreaIndex == level.MapAreaIndex;
    }

    public static string UpsertCorridor(SimpleLevel level, BotNavigationAuthoredCorridorEntry corridor)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(corridor);

        var path = ResolvePath(level.Name, level.MapAreaIndex);
        BotNavigationAuthoredCorridorAsset asset;
        if (BotBrainJsonAssetIO.TryResolveReadablePath(path, out var readablePath))
        {
            asset = BotBrainJsonAssetIO.Deserialize<BotNavigationAuthoredCorridorAsset>(readablePath, SerializerOptions)
                ?? new BotNavigationAuthoredCorridorAsset();
        }
        else
        {
            asset = new BotNavigationAuthoredCorridorAsset();
        }

        asset.LevelName = level.Name;
        asset.MapAreaIndex = level.MapAreaIndex;
        var existingIndex = asset.Corridors.FindIndex(existing =>
            string.Equals(existing.Name, corridor.Name, StringComparison.OrdinalIgnoreCase)
            && existing.Team == corridor.Team
            && existing.PlayerClass == corridor.PlayerClass);
        if (existingIndex >= 0)
        {
            asset.Corridors[existingIndex] = corridor;
        }
        else
        {
            asset.Corridors.Add(corridor);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(asset, SerializerOptions));
        return path;
    }

    public static string ResolvePath(string levelName, int mapAreaIndex)
    {
        var fileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex).ToString(CultureInfo.InvariantCulture)}.botbrain-corridors.json";
        return ContentRoot.GetPath(AuthoredCorridorDirectoryName, fileName);
    }

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
}
