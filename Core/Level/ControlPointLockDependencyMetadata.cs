using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public readonly record struct ControlPointLockDependency(int ControlPointIndex, PlayerTeam Team)
{
    public bool IsConfigured => ControlPointIndex >= ControlPointIndexMetadata.MinIndex;
}

public readonly record struct ControlPointLockRules(
    ControlPointLockDependency LockedWhen,
    ControlPointLockDependency UnlockedWhen,
    int LockedWhenLogicNodeIndex = -1,
    int UnlockedWhenLogicNodeIndex = -1,
    bool InitialLocked = false)
{
    public static ControlPointLockRules Empty { get; } = default;

    public bool HasAnyRule =>
        LockedWhen.IsConfigured
        || UnlockedWhen.IsConfigured
        || LockedWhenLogicNodeIndex >= 0
        || UnlockedWhenLogicNodeIndex >= 0;
}

public static class ControlPointLockDependencyMetadata
{
    public const string LockedWhenCpPropertyKey = "lockedWhenCp";
    public const string LockedWhenTeamPropertyKey = "lockedWhenTeam";
    public const string UnlockedWhenCpPropertyKey = "unlockedWhenCp";
    public const string UnlockedWhenTeamPropertyKey = "unlockedWhenTeam";

    public const string LockedWhenSectionKey = "$section:lockedWhen";
    public const string UnlockedWhenSectionKey = "$section:unlockedWhen";

    public static ControlPointLockRules Parse(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return ControlPointLockRules.Empty;
        }

        return new ControlPointLockRules(
            ParseDependency(
                ReadProperty(properties, LockedWhenCpPropertyKey),
                ReadProperty(properties, LockedWhenTeamPropertyKey)),
            ParseDependency(
                ReadProperty(properties, UnlockedWhenCpPropertyKey),
                ReadProperty(properties, UnlockedWhenTeamPropertyKey)),
            InitialLocked: ControlPointInitialLockStateMetadata.ParseLocked(properties));
    }

    public static bool GetInitialLocked(in ControlPointLockRules rules)
    {
        return rules.InitialLocked;
    }

    public static void ApplyMapLockTriggers(
        in ControlPointLockRules rules,
        IReadOnlyList<ControlPointState> controlPoints,
        MapLogicGraph? logicGraph,
        ref bool isLocked)
    {
        if (rules.LockedWhen.IsConfigured
            && TryGetOwner(controlPoints, rules.LockedWhen.ControlPointIndex, out var lockedOwner)
            && lockedOwner == rules.LockedWhen.Team)
        {
            isLocked = true;
        }

        if (rules.UnlockedWhen.IsConfigured
            && TryGetOwner(controlPoints, rules.UnlockedWhen.ControlPointIndex, out var unlockedOwner)
            && unlockedOwner == rules.UnlockedWhen.Team)
        {
            isLocked = false;
        }

        if (logicGraph is null || !logicGraph.HasNodes)
        {
            return;
        }

        if (rules.LockedWhenLogicNodeIndex >= 0
            && logicGraph.GetOutput(rules.LockedWhenLogicNodeIndex))
        {
            isLocked = true;
        }

        if (rules.UnlockedWhenLogicNodeIndex >= 0
            && logicGraph.GetOutput(rules.UnlockedWhenLogicNodeIndex))
        {
            isLocked = false;
        }
    }

    public static bool TryParseTeam(string? value, out PlayerTeam team)
    {
        if (value is null)
        {
            team = default;
            return false;
        }

        if (value.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Red;
            return true;
        }

        if (value.Equals("blue", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Blue;
            return true;
        }

        team = default;
        return false;
    }

    public static string ToTeamPropertyValue(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? "blue" : "red";
    }

    public static string CycleTeamPropertyValue(string current)
    {
        return TryParseTeam(current, out var team) && team == PlayerTeam.Blue
            ? ToTeamPropertyValue(PlayerTeam.Red)
            : ToTeamPropertyValue(PlayerTeam.Blue);
    }

    public static string GetTeamDisplayLabel(string? value)
    {
        return TryParseTeam(value, out var team)
            ? team == PlayerTeam.Blue ? "Blue" : "Red"
            : "Red";
    }

    public static string FormatLinkDisplayLabel(string? linkValue, Func<string, string>? describeLink = null)
    {
        if (string.IsNullOrWhiteSpace(linkValue))
        {
            return "none (click to pick on map)";
        }

        return describeLink is null ? linkValue : describeLink(linkValue);
    }

    public static bool HasConflictingRules(in ControlPointLockRules rules)
    {
        if (!rules.LockedWhen.IsConfigured || !rules.UnlockedWhen.IsConfigured)
        {
            return false;
        }

        return rules.LockedWhen.ControlPointIndex == rules.UnlockedWhen.ControlPointIndex
            && rules.LockedWhen.Team == rules.UnlockedWhen.Team;
    }

    private static ControlPointLockDependency ParseDependency(string? linkValue, string? teamValue)
    {
        var index = ForwardSpawnMetadata.ParseControlPointIndexFromLink(linkValue);
        if (index < ControlPointIndexMetadata.MinIndex || !TryParseTeam(teamValue, out var team))
        {
            return default;
        }

        return new ControlPointLockDependency(index, team);
    }

    private static bool TryGetOwner(IReadOnlyList<ControlPointState> controlPoints, int controlPointIndex, out PlayerTeam? owner)
    {
        for (var index = 0; index < controlPoints.Count; index += 1)
        {
            var point = controlPoints[index];
            if (!TryResolveLogicalIndex(point.Marker, out var logicalIndex)
                || logicalIndex != controlPointIndex)
            {
                continue;
            }

            owner = point.Team;
            return true;
        }

        owner = null;
        return false;
    }

    private static bool TryResolveLogicalIndex(RoomObjectMarker marker, out int index)
    {
        if (ControlPointMarkerIndex.TryGetIndex(marker, out index))
        {
            return true;
        }

        index = 0;
        return false;
    }

    private static string ReadProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value : string.Empty;
    }
}
