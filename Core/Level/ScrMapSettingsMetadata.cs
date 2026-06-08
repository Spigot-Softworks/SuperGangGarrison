using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public enum ScrWinWhenScore
{
    MoreEqual,
    LessEqual,
}

public enum ScrRoundEndWin
{
    MorePoints,
    LessPoints,
    Red,
    Blue,
}

public static class ScrMapSettingsMetadata
{
    public const string ScoreToWinPropertyKey = "scoreToWin";
    public const string WinWhenScorePropertyKey = "winWhenScore";
    public const string RoundEndWinPropertyKey = "roundEndWin";
    public const string RedStartingScorePropertyKey = "redStartingScore";
    public const string BlueStartingScorePropertyKey = "blueStartingScore";
    public const string ShowControlPointsPropertyKey = "showCps";

    public const string WinWhenMoreEqualPropertyValue = "moreEqual";
    public const string WinWhenLessEqualPropertyValue = "lessEqual";

    public const string RoundEndMorePointsPropertyValue = "morePoints";
    public const string RoundEndLessPointsPropertyValue = "lessPoints";
    public const string RoundEndRedPropertyValue = "red";
    public const string RoundEndBluePropertyValue = "blue";

    public const int DefaultScoreToWin = 10;
    public const int MinScore = 0;
    public const int MaxScore = 999;

    public static bool IsEditableMapMetadataKey(string key)
    {
        return key.Equals(ScoreToWinPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(WinWhenScorePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(RoundEndWinPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(RedStartingScorePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(BlueStartingScorePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ShowControlPointsPropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsScrOnlyMapMetadataKey(string key)
    {
        return key.Equals(ScoreToWinPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(WinWhenScorePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(RoundEndWinPropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(RedStartingScorePropertyKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(BlueStartingScorePropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    public static CustomMapScrSettings ParseScrSettings(IReadOnlyDictionary<string, string>? metadata)
    {
        return new CustomMapScrSettings(
            ParseScoreToWin(metadata),
            ParseWinWhenScore(metadata),
            ParseRoundEndWin(metadata),
            ParseTeamStartingScore(metadata, RedStartingScorePropertyKey),
            ParseTeamStartingScore(metadata, BlueStartingScorePropertyKey));
    }

    public static bool ParseShowControlPoints(IReadOnlyDictionary<string, string>? metadata)
    {
        return metadata is not null
            && metadata.TryGetValue(ShowControlPointsPropertyKey, out var rawValue)
            && ParseBool(rawValue);
    }

    public static int ParseScoreToWin(IReadOnlyDictionary<string, string>? metadata)
    {
        return ParseBoundedScore(metadata, ScoreToWinPropertyKey, DefaultScoreToWin);
    }

    public static ScrWinWhenScore ParseWinWhenScore(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is not null
            && metadata.TryGetValue(WinWhenScorePropertyKey, out var value)
            && value.Equals(WinWhenLessEqualPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return ScrWinWhenScore.LessEqual;
        }

        return ScrWinWhenScore.MoreEqual;
    }

    public static ScrRoundEndWin ParseRoundEndWin(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue(RoundEndWinPropertyKey, out var value))
        {
            return ScrRoundEndWin.MorePoints;
        }

        if (value.Equals(RoundEndLessPointsPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return ScrRoundEndWin.LessPoints;
        }

        if (value.Equals(RoundEndRedPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return ScrRoundEndWin.Red;
        }

        if (value.Equals(RoundEndBluePropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return ScrRoundEndWin.Blue;
        }

        return ScrRoundEndWin.MorePoints;
    }

    public static string CycleWinWhenScorePropertyValue(string? current)
    {
        return ParseWinWhenScoreFromValue(current) == ScrWinWhenScore.MoreEqual
            ? WinWhenLessEqualPropertyValue
            : WinWhenMoreEqualPropertyValue;
    }

    public static string CycleRoundEndWinPropertyValue(string? current)
    {
        var next = (ScrRoundEndWin)(((int)ParseRoundEndWinFromValue(current) + 1) % 4);
        return ToRoundEndWinPropertyValue(next);
    }

    public static string GetWinWhenScoreDisplayLabel(string? value)
    {
        return ParseWinWhenScoreFromValue(value) == ScrWinWhenScore.LessEqual
            ? "Less/Equal"
            : "More/Equal";
    }

    public static string GetRoundEndWinDisplayLabel(string? value)
    {
        return ParseRoundEndWinFromValue(value) switch
        {
            ScrRoundEndWin.LessPoints => "Less points",
            ScrRoundEndWin.Red => "Red",
            ScrRoundEndWin.Blue => "Blue",
            _ => "More points",
        };
    }

    public static string ToShowControlPointsPropertyValue(bool enabled)
    {
        return enabled ? "true" : "false";
    }

    public static string ToWinWhenScorePropertyValue(ScrWinWhenScore mode)
    {
        return mode == ScrWinWhenScore.LessEqual
            ? WinWhenLessEqualPropertyValue
            : WinWhenMoreEqualPropertyValue;
    }

    public static string ToRoundEndWinPropertyValue(ScrRoundEndWin mode)
    {
        return mode switch
        {
            ScrRoundEndWin.LessPoints => RoundEndLessPointsPropertyValue,
            ScrRoundEndWin.Red => RoundEndRedPropertyValue,
            ScrRoundEndWin.Blue => RoundEndBluePropertyValue,
            _ => RoundEndMorePointsPropertyValue,
        };
    }

    public static int StepScorePropertyValue(string? current, int delta)
    {
        if (!int.TryParse(current?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            parsed = DefaultScoreToWin;
        }

        return Math.Clamp(parsed + delta, MinScore, MaxScore);
    }

    public static int ClampScore(int value)
    {
        return Math.Clamp(value, MinScore, MaxScore);
    }

    private static int ParseTeamStartingScore(IReadOnlyDictionary<string, string>? metadata, string key)
    {
        return ParseBoundedScore(metadata, key, 0);
    }

    private static int ParseBoundedScore(IReadOnlyDictionary<string, string>? metadata, string key, int defaultValue)
    {
        if (metadata is null
            || !metadata.TryGetValue(key, out var rawValue)
            || !int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return defaultValue;
        }

        return ClampScore(parsed);
    }

    private static ScrWinWhenScore ParseWinWhenScoreFromValue(string? value)
    {
        return value?.Equals(WinWhenLessEqualPropertyValue, StringComparison.OrdinalIgnoreCase) == true
            ? ScrWinWhenScore.LessEqual
            : ScrWinWhenScore.MoreEqual;
    }

    private static ScrRoundEndWin ParseRoundEndWinFromValue(string? value)
    {
        if (value?.Equals(RoundEndLessPointsPropertyValue, StringComparison.OrdinalIgnoreCase) == true)
        {
            return ScrRoundEndWin.LessPoints;
        }

        if (value?.Equals(RoundEndRedPropertyValue, StringComparison.OrdinalIgnoreCase) == true)
        {
            return ScrRoundEndWin.Red;
        }

        if (value?.Equals(RoundEndBluePropertyValue, StringComparison.OrdinalIgnoreCase) == true)
        {
            return ScrRoundEndWin.Blue;
        }

        return ScrRoundEndWin.MorePoints;
    }

    private static bool ParseBool(string? value)
    {
        return value?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true
            || value?.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) == true;
    }
}
