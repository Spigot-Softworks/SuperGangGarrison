using OpenGarrison.Core;

namespace OpenGarrison.MLBot;

public static class MLBotCaptureModelResolver
{
    public const string CaptureModelRootEnvironmentVariable = "OG_MLBOT_CAPTURE_MODEL_ROOT";

    public static string? ResolveBestModelPath(string levelName, PlayerTeam team, PlayerClass classId, MLBot.Contracts.MLBotTaskPhase phase)
    {
        if (phase == MLBot.Contracts.MLBotTaskPhase.None)
        {
            return null;
        }

        var modelRoot = Environment.GetEnvironmentVariable(CaptureModelRootEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(modelRoot) || !Directory.Exists(modelRoot))
        {
            return null;
        }

        var mapSegment = SanitizeSegment(levelName);
        var teamSegment = team.ToString().ToLowerInvariant();
        var classSegment = classId.ToString().ToLowerInvariant();
        var phaseSegment = DescribePhase(phase);

        var exactTeamSpecific = FindBestModelPath(modelRoot, $"{mapSegment}-{teamSegment}-{classSegment}-{phaseSegment}-v62-v*");
        if (exactTeamSpecific is not null)
        {
            return exactTeamSpecific;
        }

        var teamSpecificLegacy = FindBestModelPath(modelRoot, $"{mapSegment}-{teamSegment}-{classSegment}-{phaseSegment}-v*");
        if (teamSpecificLegacy is not null)
        {
            return teamSpecificLegacy;
        }

        var exactMixed = FindBestModelPath(modelRoot, $"{mapSegment}-{classSegment}-{phaseSegment}-mixedteams-v62-v*");
        if (exactMixed is not null)
        {
            return exactMixed;
        }

        return FindBestModelPath(modelRoot, $"{mapSegment}-{classSegment}-{phaseSegment}-mixedteams-v*");
    }

    private static string? FindBestModelPath(string rootDirectory, string pattern)
    {
        var directories = Directory.EnumerateDirectories(rootDirectory, pattern, SearchOption.TopDirectoryOnly)
            .Select(path => new
            {
                DirectoryPath = path,
                Version = TryParseVersionSuffix(Path.GetFileName(path)),
            })
            .OrderByDescending(item => item.Version)
            .ThenByDescending(item => item.DirectoryPath, StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in directories)
        {
            var modelPath = Path.Combine(candidate.DirectoryPath, "model.onnx");
            var modelDataPath = modelPath + ".data";
            if (File.Exists(modelPath) && File.Exists(modelDataPath))
            {
                return modelPath;
            }
        }

        return null;
    }

    private static int TryParseVersionSuffix(string? directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName))
        {
            return -1;
        }

        var markerIndex = directoryName.LastIndexOf("-v", StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return -1;
        }

        var suffix = directoryName[(markerIndex + 2)..];
        return int.TryParse(suffix, out var version) ? version : -1;
    }

    private static string DescribePhase(MLBot.Contracts.MLBotTaskPhase phase)
    {
        return phase switch
        {
            MLBot.Contracts.MLBotTaskPhase.AttackIntel => "attack",
            MLBot.Contracts.MLBotTaskPhase.ReturnIntel => "return",
            MLBot.Contracts.MLBotTaskPhase.CaptureObjective => "capture",
            MLBot.Contracts.MLBotTaskPhase.DefendObjective => "defend",
            _ => "auto",
        };
    }

    private static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "map";
        }

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        for (var index = 0; index < value.Length; index += 1)
        {
            var character = value[index];
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = char.ToLowerInvariant(character);
            }
        }

        return length == 0 ? "map" : new string(buffer[..length]);
    }
}
