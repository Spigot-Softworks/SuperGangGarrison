using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OpenGarrison.Core;

public enum TeleportTeamFilter
{
    All,
    Red,
    Blue,
}

public static class TeleportMetadata
{
    public const string TeleportEntityType = "teleport";
    public const string TeleportExitEntityType = "teleportExit";
    public const string TeamPropertyKey = "team";
    public const string TeleportExitPropertyKey = "teleportExit";
    public const string TeamAllPropertyValue = "all";
    public const float DefaultZoneWidth = 42f;
    public const float DefaultZoneHeight = 42f;
    public const float ExitMarkerSize = 42f;
    public const float MinZoneExtent = 12f;

    public static bool IsTeleportEntityType(string? type)
    {
        return type.Equals(TeleportEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(TeleportExitEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static (float Width, float Height) ResolveZoneDimensions(float xScale, float yScale)
    {
        var width = DefaultZoneWidth * MathF.Abs(xScale <= 0f ? 1f : xScale);
        var height = DefaultZoneHeight * MathF.Abs(yScale <= 0f ? 1f : yScale);
        return (MathF.Max(MinZoneExtent, width), MathF.Max(MinZoneExtent, height));
    }

    public static bool TryParseTeamFilter(string? value, out TeleportTeamFilter filter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals(TeamAllPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            filter = TeleportTeamFilter.All;
            return true;
        }

        if (value.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            filter = TeleportTeamFilter.Red;
            return true;
        }

        if (value.Equals("blue", StringComparison.OrdinalIgnoreCase))
        {
            filter = TeleportTeamFilter.Blue;
            return true;
        }

        filter = default;
        return false;
    }

    public static string ToTeamPropertyValue(TeleportTeamFilter filter)
    {
        return filter switch
        {
            TeleportTeamFilter.Red => "red",
            TeleportTeamFilter.Blue => "blue",
            _ => TeamAllPropertyValue,
        };
    }

    public static string CycleTeamPropertyValue(string? current)
    {
        if (!TryParseTeamFilter(current, out var filter))
        {
            filter = TeleportTeamFilter.All;
        }

        var next = (TeleportTeamFilter)(((int)filter + 1) % 3);
        return ToTeamPropertyValue(next);
    }

    public static string GetTeamDisplayLabel(string? value)
    {
        return TryParseTeamFilter(value, out var filter)
            ? filter switch
            {
                TeleportTeamFilter.Red => "Red",
                TeleportTeamFilter.Blue => "Blue",
                _ => "All",
            }
            : "All";
    }

    public static bool AllowsTeam(TeleportTeamFilter filter, PlayerTeam team)
    {
        return filter switch
        {
            TeleportTeamFilter.Red => team == PlayerTeam.Red,
            TeleportTeamFilter.Blue => team == PlayerTeam.Blue,
            _ => true,
        };
    }

    public static bool TryResolveExitPosition(
        IList<RoomObjectMarker> roomObjects,
        string? exitRef,
        out float exitX,
        out float exitY)
    {
        exitX = 0f;
        exitY = 0f;
        if (!MapLogicEntityReference.TryParseEntityRef(exitRef, out var entityType, out var x, out var y)
            || !entityType.Equals(TeleportExitEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (MapLogicEntityReference.TryResolveRoomObjectIndex(ToReadOnlyRoomObjects(roomObjects), exitRef, out var index))
        {
            var marker = roomObjects[index];
            exitX = marker.CenterX;
            exitY = marker.CenterY;
            return true;
        }

        exitX = x;
        exitY = y;
        return true;
    }

    public static TeleportZoneConfiguration ParseZoneConfiguration(
        IReadOnlyDictionary<string, string>? properties,
        IList<RoomObjectMarker> roomObjects)
    {
        var team = TryParseTeamFilter(
            properties is not null && properties.TryGetValue(TeamPropertyKey, out var teamValue) ? teamValue : null,
            out var filter)
            ? filter
            : TeleportTeamFilter.All;

        var exitX = 0f;
        var exitY = 0f;
        var hasExit = properties is not null
            && properties.TryGetValue(TeleportExitPropertyKey, out var exitRef)
            && TryResolveExitPosition(roomObjects, exitRef, out exitX, out exitY);

        return hasExit
            ? new TeleportZoneConfiguration(team, exitX, exitY, HasExit: true)
            : new TeleportZoneConfiguration(team, 0f, 0f, HasExit: false);
    }

    private static IReadOnlyList<RoomObjectMarker> ToReadOnlyRoomObjects(IList<RoomObjectMarker> roomObjects)
    {
        return roomObjects as IReadOnlyList<RoomObjectMarker> ?? new ReadOnlyCollection<RoomObjectMarker>(roomObjects);
    }
}

public readonly record struct TeleportZoneConfiguration(
    TeleportTeamFilter TeamFilter,
    float ExitX,
    float ExitY,
    bool HasExit);
