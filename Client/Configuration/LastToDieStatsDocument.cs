#nullable enable

using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class LastToDieStatsDocument
{
    public const string DefaultFileName = "last-to-die-stats.json";

    public int HighestRoundCompleted { get; set; }

    public int BestScoreUnits { get; set; }

    public int RunsPlayed { get; set; }

    public int MostDamageSingleRun { get; set; }

    public int MostHealingSingleRun { get; set; }

    public int LongestComboSingleRun { get; set; }

    public int TotalDamageLifetime { get; set; }

    public int LastRunRound { get; set; }

    public int LastRunScoreUnits { get; set; }

    public int LastRunElapsedTicks { get; set; }

    public bool HasRecordedRun { get; set; }

    public string LastRecordedAttemptId { get; set; } = string.Empty;

    public bool RecordRun(int scoreUnits, int completedRounds, Guid? attemptId = null)
    {
        var normalizedAttemptId = attemptId is { } value && value != Guid.Empty
            ? value.ToString("N")
            : string.Empty;
        if (normalizedAttemptId.Length > 0
            && string.Equals(LastRecordedAttemptId, normalizedAttemptId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        scoreUnits = Math.Max(0, scoreUnits);
        completedRounds = Math.Max(0, completedRounds);
        HasRecordedRun = true;
        RunsPlayed = RunsPlayed >= int.MaxValue ? int.MaxValue : RunsPlayed + 1;
        BestScoreUnits = Math.Max(BestScoreUnits, scoreUnits);
        HighestRoundCompleted = Math.Max(HighestRoundCompleted, completedRounds);
        LastRunScoreUnits = scoreUnits;
        LastRunRound = completedRounds;
        LastRecordedAttemptId = normalizedAttemptId;
        return true;
    }

    public static LastToDieStatsDocument Load(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            try
            {
                if (OpenGarrison.ClientShared.BrowserPreferenceStore.Read("ltd-stats-v1") is { } json
                    && System.Text.Json.JsonSerializer.Deserialize(json, BrowserPreferencesJsonContext.Default.LastToDieStatsDocument) is { } saved)
                    return saved;
            }
            catch (System.Text.Json.JsonException) { }
            return new LastToDieStatsDocument();
        }

        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        return JsonConfigurationFile.LoadOrCreate<LastToDieStatsDocument>(resolvedPath);
    }

    public void Save(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            OpenGarrison.ClientShared.BrowserPreferenceStore.Write("ltd-stats-v1",
                System.Text.Json.JsonSerializer.Serialize(this, BrowserPreferencesJsonContext.Default.LastToDieStatsDocument));
            return;
        }

        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        JsonConfigurationFile.Save(resolvedPath, this);
    }
}
