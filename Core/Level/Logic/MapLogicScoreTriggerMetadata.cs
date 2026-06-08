using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public enum MapLogicScoreTeamTarget
{
    Red,
    Blue,
    Both,
}

public enum MapLogicScoreChangeMode
{
    Add,
    Subtract,
}

public static class MapLogicScoreTriggerMetadata
{
    public const string ScoreTriggerEntityType = "logicScore";
    public const string ScoreTeamPropertyKey = "scoreTeam";
    public const string ChangePropertyKey = "change";
    public const string ValuePropertyKey = "value";

    public const string ScoreTeamRedPropertyValue = "red";
    public const string ScoreTeamBluePropertyValue = "blue";
    public const string ScoreTeamBothPropertyValue = "both";

    public const string ChangeAddPropertyValue = "add";
    public const string ChangeSubtractPropertyValue = "subtract";

    public const int DefaultValue = 1;
    public const int MinValue = 0;
    public const int MaxValue = 999;

    public static bool IsScoreTriggerEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(ScoreTriggerEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static MapLogicScoreTeamTarget ParseScoreTeam(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(ScoreTeamPropertyKey, out var value))
        {
            return MapLogicScoreTeamTarget.Red;
        }

        return ParseScoreTeam(value);
    }

    public static MapLogicScoreTeamTarget ParseScoreTeam(string? value)
    {
        if (value?.Equals(ScoreTeamBluePropertyValue, StringComparison.OrdinalIgnoreCase) == true)
        {
            return MapLogicScoreTeamTarget.Blue;
        }

        if (value?.Equals(ScoreTeamBothPropertyValue, StringComparison.OrdinalIgnoreCase) == true)
        {
            return MapLogicScoreTeamTarget.Both;
        }

        return MapLogicScoreTeamTarget.Red;
    }

    public static string ToScoreTeamPropertyValue(MapLogicScoreTeamTarget team)
    {
        return team switch
        {
            MapLogicScoreTeamTarget.Blue => ScoreTeamBluePropertyValue,
            MapLogicScoreTeamTarget.Both => ScoreTeamBothPropertyValue,
            _ => ScoreTeamRedPropertyValue,
        };
    }

    public static string CycleScoreTeamPropertyValue(string? current)
    {
        var next = (MapLogicScoreTeamTarget)(((int)ParseScoreTeam(current) + 1) % 3);
        return ToScoreTeamPropertyValue(next);
    }

    public static string GetScoreTeamDisplayLabel(string? value)
    {
        return ParseScoreTeam(value) switch
        {
            MapLogicScoreTeamTarget.Blue => "Blue",
            MapLogicScoreTeamTarget.Both => "Both",
            _ => "Red",
        };
    }

    public static MapLogicScoreChangeMode ParseChangeMode(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is not null
            && properties.TryGetValue(ChangePropertyKey, out var value)
            && value.Equals(ChangeSubtractPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapLogicScoreChangeMode.Subtract;
        }

        return MapLogicScoreChangeMode.Add;
    }

    public static string ToChangePropertyValue(MapLogicScoreChangeMode mode)
    {
        return mode == MapLogicScoreChangeMode.Subtract
            ? ChangeSubtractPropertyValue
            : ChangeAddPropertyValue;
    }

    public static string CycleChangePropertyValue(string? current)
    {
        return ParseChangeModeFromValue(current) == MapLogicScoreChangeMode.Add
            ? ChangeSubtractPropertyValue
            : ChangeAddPropertyValue;
    }

    public static string GetChangeDisplayLabel(string? value)
    {
        return ParseChangeModeFromValue(value) == MapLogicScoreChangeMode.Subtract
            ? "Subtract"
            : "Add";
    }

    public static int ParseValue(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(ValuePropertyKey, out var rawValue)
            || !int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return DefaultValue;
        }

        return Math.Clamp(parsed, MinValue, MaxValue);
    }

    public static int StepValueProperty(string? current, int delta)
    {
        if (!int.TryParse(current?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            parsed = DefaultValue;
        }

        return Math.Clamp(parsed + delta, MinValue, MaxValue);
    }

    public static string ToValuePropertyValue(int value)
    {
        return Math.Clamp(value, MinValue, MaxValue).ToString(CultureInfo.InvariantCulture);
    }

    public static string FormatValueSliderDisplay(string? value)
    {
        return $"< {StepValueProperty(value, 0)} >";
    }

    private static MapLogicScoreChangeMode ParseChangeModeFromValue(string? value)
    {
        return value?.Equals(ChangeSubtractPropertyValue, StringComparison.OrdinalIgnoreCase) == true
            ? MapLogicScoreChangeMode.Subtract
            : MapLogicScoreChangeMode.Add;
    }
}
