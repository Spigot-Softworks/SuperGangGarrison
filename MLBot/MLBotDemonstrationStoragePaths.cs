using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;
using System.Globalization;

namespace OpenGarrison.MLBot;

internal static class MLBotDemonstrationStoragePaths
{
    public const string DataRootEnvironmentVariable = "OG_MLBOT_DATA_ROOT";

    public static string ResolveWritablePath(string demonstrationDirectoryName, MLBotDemonstrationMetadata metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(demonstrationDirectoryName);
        ArgumentNullException.ThrowIfNull(metadata);

        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"{metadata.RecordedAtUtc.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}_{SanitizePathSegment(metadata.LevelName)}_{metadata.Team.ToString().ToLowerInvariant()}_{metadata.ClassId.ToString().ToLowerInvariant()}_{DescribePhase(metadata.RequestedPhase)}.json");
        var directoryPath = Path.Combine(
            ResolveDataRootDirectory(),
            demonstrationDirectoryName,
            SanitizePathSegment(metadata.LevelName));
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath, fileName);
    }

    private static string ResolveDataRootDirectory()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(DataRootEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            return RuntimePaths.ConfigDirectory;
        }

        var resolvedRoot = Path.IsPathRooted(configuredRoot)
            ? Path.GetFullPath(configuredRoot)
            : Path.GetFullPath(Path.Combine(RuntimePaths.ApplicationRoot, configuredRoot));
        Directory.CreateDirectory(resolvedRoot);
        return resolvedRoot;
    }

    private static string DescribePhase(MLBotTaskPhase phase)
    {
        return phase switch
        {
            MLBotTaskPhase.AttackIntel => "attack",
            MLBotTaskPhase.ReturnIntel => "return",
            MLBotTaskPhase.CaptureObjective => "capture",
            MLBotTaskPhase.DefendObjective => "defend",
            _ => "auto",
        };
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "demo";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        for (var index = 0; index < value.Length; index += 1)
        {
            var character = value[index];
            if (invalidChars.Contains(character) || char.IsWhiteSpace(character))
            {
                buffer[length++] = '_';
                continue;
            }

            buffer[length++] = character;
        }

        return new string(buffer[..length]).Trim('_');
    }
}
