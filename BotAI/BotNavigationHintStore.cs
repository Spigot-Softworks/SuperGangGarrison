using OpenGarrison.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.BotAI;

public static class BotNavigationHintStore
{
    public const int CurrentFormatVersion = 1;
    private const string HintsRelativeDirectory = "Core/Content/BotNavHints";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    static BotNavigationHintStore()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static BotNavigationHintAsset? Load(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var path = ResolvePath(level.Name, level.MapAreaIndex);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var asset = JsonSerializer.Deserialize<BotNavigationHintAsset>(json, SerializerOptions);
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
        var fileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.botnavhints.json";
        var projectPath = ProjectSourceLocator.FindFile($"{HintsRelativeDirectory}/{fileName}");
        if (!string.IsNullOrWhiteSpace(projectPath) && File.Exists(projectPath))
        {
            return projectPath;
        }

        var runtimePath = ContentRoot.GetPath("BotNavHints", fileName);
        return File.Exists(runtimePath) ? runtimePath : null;
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
