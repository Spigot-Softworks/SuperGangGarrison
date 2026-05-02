#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class LastToDieStatsDocument
{
    public const string DefaultFileName = "last-to-die-stats.json";

    public int HighestRoundCompleted { get; set; }

    public int MostDamageSingleRun { get; set; }

    public int MostHealingSingleRun { get; set; }

    public int LongestComboSingleRun { get; set; }

    public int TotalDamageLifetime { get; set; }

    public int LastRunRound { get; set; }

    public int LastRunElapsedTicks { get; set; }

    public bool HasRecordedRun { get; set; }

    public static LastToDieStatsDocument Load(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return new LastToDieStatsDocument();
        }

        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        return JsonConfigurationFile.LoadOrCreate<LastToDieStatsDocument>(resolvedPath);
    }

    public void Save(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        JsonConfigurationFile.Save(resolvedPath, this);
    }
}
