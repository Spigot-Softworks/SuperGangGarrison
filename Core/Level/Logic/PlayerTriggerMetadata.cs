using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public enum PlayerTriggerTeamFilter
{
    Any,
    Red,
    Blue,
}

public readonly record struct PlayerTriggerZoneConfiguration(
    PlayerTriggerTeamFilter TeamFilter,
    bool IntelCarriersOnly = false);

public static class PlayerTriggerMetadata
{
    public const string PlayerTriggerEntityType = "logicPlayerTrigger";
    public const string TeamPropertyKey = "team";
    public const string IntelCarriersOnlyPropertyKey = "intelCarriersOnly";
    public const string TeamAnyPropertyValue = "any";
    public const float DefaultZoneWidth = 42f;
    public const float DefaultZoneHeight = 42f;
    public const float MinZoneExtent = 12f;

    public static (float Width, float Height) ResolveZoneDimensions(float xScale, float yScale)
    {
        var width = DefaultZoneWidth * MathF.Abs(xScale <= 0f ? 1f : xScale);
        var height = DefaultZoneHeight * MathF.Abs(yScale <= 0f ? 1f : yScale);
        return (MathF.Max(MinZoneExtent, width), MathF.Max(MinZoneExtent, height));
    }

    public static bool TryParseTeamFilter(string? value, out PlayerTriggerTeamFilter filter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals(TeamAnyPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            filter = PlayerTriggerTeamFilter.Any;
            return true;
        }

        if (value.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            filter = PlayerTriggerTeamFilter.Red;
            return true;
        }

        if (value.Equals("blue", StringComparison.OrdinalIgnoreCase))
        {
            filter = PlayerTriggerTeamFilter.Blue;
            return true;
        }

        filter = default;
        return false;
    }

    public static string ToTeamPropertyValue(PlayerTriggerTeamFilter filter)
    {
        return filter switch
        {
            PlayerTriggerTeamFilter.Red => "red",
            PlayerTriggerTeamFilter.Blue => "blue",
            _ => TeamAnyPropertyValue,
        };
    }

    public static string CycleTeamPropertyValue(string? current)
    {
        if (!TryParseTeamFilter(current, out var filter))
        {
            filter = PlayerTriggerTeamFilter.Any;
        }

        var next = (PlayerTriggerTeamFilter)(((int)filter + 1) % 3);
        return ToTeamPropertyValue(next);
    }

    public static string GetTeamDisplayLabel(string? value)
    {
        return TryParseTeamFilter(value, out var filter)
            ? filter switch
            {
                PlayerTriggerTeamFilter.Red => "Red",
                PlayerTriggerTeamFilter.Blue => "Blue",
                _ => "Any",
            }
            : "Any";
    }

    public static bool AllowsTeam(PlayerTriggerTeamFilter filter, PlayerTeam team)
    {
        return filter switch
        {
            PlayerTriggerTeamFilter.Red => team == PlayerTeam.Red,
            PlayerTriggerTeamFilter.Blue => team == PlayerTeam.Blue,
            _ => true,
        };
    }

    public static PlayerTriggerZoneConfiguration ParseZoneConfiguration(IReadOnlyDictionary<string, string>? properties)
    {
        var team = TryParseTeamFilter(
            properties is not null && properties.TryGetValue(TeamPropertyKey, out var teamValue) ? teamValue : null,
            out var filter)
            ? filter
            : PlayerTriggerTeamFilter.Any;

        var intelCarriersOnly = properties is not null
            && properties.TryGetValue(IntelCarriersOnlyPropertyKey, out var intelCarriersOnlyValue)
            && DamageTriggerMetadata.ParseBoolProperty(intelCarriersOnlyValue);

        return new PlayerTriggerZoneConfiguration(team, intelCarriersOnly);
    }

    public static bool AnyMatchingPlayerInside(
        in RoomObjectMarker zone,
        PlayerTriggerTeamFilter filter,
        IEnumerable<PlayerEntity> players,
        bool intelCarriersOnly = false)
    {
        foreach (var player in players)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            if (!AllowsTeam(filter, player.Team))
            {
                continue;
            }

            if (intelCarriersOnly && !player.IsCarryingIntel)
            {
                continue;
            }

            if (IsPointInsideZone(player.X, player.Y, zone))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPointInsideZone(float x, float y, in RoomObjectMarker zone)
    {
        return x >= zone.Left
            && x <= zone.Right
            && y >= zone.Top
            && y <= zone.Bottom;
    }

    public static int TryResolveRoomObjectIndex(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        string logicKey,
        float entityX,
        float entityY)
    {
        var bestIndex = -1;
        var bestDistanceSquared = float.MaxValue;
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            if (marker.Type != RoomObjectType.PlayerTriggerZone
                || !marker.SourceName.Equals(logicKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var deltaX = marker.CenterX - entityX;
            var deltaY = marker.CenterY - entityY;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestIndex = index;
        }

        return bestIndex;
    }
}
