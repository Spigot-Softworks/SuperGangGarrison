using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

/// <summary>
/// Creates runtime <see cref="RoomObjectMarker"/> instances from map entity type names.
/// Shared by legacy import and modern entity handlers.
/// </summary>
public static class CustomMapRoomObjectFactory
{
    public static bool TryCreate(
        string entityType,
        float x,
        float y,
        float xScale,
        float yScale,
        IReadOnlyDictionary<string, string> properties,
        out RoomObjectMarker marker,
        bool useCenterOrigin = false)
    {
        var normalizedXScale = MathF.Abs(xScale) <= 0f ? 1f : MathF.Abs(xScale);
        var normalizedYScale = MathF.Abs(yScale) <= 0f ? 1f : MathF.Abs(yScale);
        var resetMoveStatus = ResolveResetMoveStatus(properties);
        var moveBoxImpulse = ResolveMoveBoxImpulse(properties);

        marker = entityType.ToLowerInvariant() switch
        {
            "spawnroom" => CreateMarker(RoomObjectType.SpawnRoom, x, y, 42f, 42f, "sprite64", normalizedXScale, normalizedYScale, sourceName: entityType),
            "cabinets" or "healingcabinet" or "medcabinet" => CreateMarker(RoomObjectType.HealingCabinet, x, y, 32f, 48f, "sprite74", normalizedXScale, normalizedYScale, sourceName: entityType),
            "killbox" or "pitfall" => CreateMarker(RoomObjectType.KillBox, x, y, 42f, 42f, "sprite64", normalizedXScale, normalizedYScale, sourceName: entityType),
            "fragbox" => CreateMarker(RoomObjectType.FragBox, x, y, 42f, 42f, "sprite64", normalizedXScale, normalizedYScale, sourceName: entityType),
            "redteamgate" => CreateMarker(RoomObjectType.TeamGate, x, y, 6f, 60f, "sprite45", normalizedXScale, normalizedYScale, PlayerTeam.Red, entityType),
            "blueteamgate" => CreateMarker(RoomObjectType.TeamGate, x, y, 6f, 60f, "sprite45", normalizedXScale, normalizedYScale, PlayerTeam.Blue, entityType),
            "redteamgate2" => CreateMarker(RoomObjectType.TeamGate, x, y, 60f, 6f, "sprite44", normalizedXScale, normalizedYScale, PlayerTeam.Red, entityType),
            "blueteamgate2" => CreateMarker(RoomObjectType.TeamGate, x, y, 60f, 6f, "sprite44", normalizedXScale, normalizedYScale, PlayerTeam.Blue, entityType),
            "redintelgate" => CreateMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", normalizedXScale, normalizedYScale, PlayerTeam.Red, entityType),
            "blueintelgate" => CreateMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", normalizedXScale, normalizedYScale, PlayerTeam.Blue, entityType),
            "redintelgate2" => CreateMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", normalizedXScale, normalizedYScale, PlayerTeam.Red, entityType),
            "blueintelgate2" => CreateMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", normalizedXScale, normalizedYScale, PlayerTeam.Blue, entityType),
            "intelgatevertical" => CreateMarker(RoomObjectType.IntelGate, x, y, 6f, 60f, "sprite45", normalizedXScale, normalizedYScale, sourceName: entityType),
            "intelgatehorizontal" => CreateMarker(RoomObjectType.IntelGate, x, y, 60f, 6f, "sprite44", normalizedXScale, normalizedYScale, sourceName: entityType),
            "playerwall" => CreateMarker(RoomObjectType.PlayerWall, x, y, 6f, 60f, "sprite45", normalizedXScale, normalizedYScale, sourceName: entityType),
            "playerwallhorizontal" or "playerwall_horizontal" => CreateMarker(RoomObjectType.PlayerWall, x, y, 60f, 6f, "sprite44", normalizedXScale, normalizedYScale, sourceName: entityType),
            "leftdoor" => CreateMarker(RoomObjectType.PlayerWall, x, y, 6f, 60f, "sprite45", normalizedXScale, normalizedYScale, sourceName: entityType),
            "rightdoor" => CreateMarker(RoomObjectType.PlayerWall, x, y, 6f, 60f, "sprite45", normalizedXScale, normalizedYScale, sourceName: entityType),
            "dropdownplatform" => CreateMarker(RoomObjectType.DropdownPlatform, x, y, 60f, 6f, "sprite44", normalizedXScale, normalizedYScale, sourceName: entityType, value: resetMoveStatus),
            "bulletwall" => CreateMarker(RoomObjectType.BulletWall, x, y, 6f, 60f, "sprite45", normalizedXScale, normalizedYScale, sourceName: entityType),
            "bulletwallhorizontal" or "bulletwall_horizontal" => CreateMarker(RoomObjectType.BulletWall, x, y, 60f, 6f, "sprite44", normalizedXScale, normalizedYScale, sourceName: entityType),
            "moveboxup" => CreateMarker(RoomObjectType.MoveBoxUp, x, y, 42f, 42f, "sprite64", normalizedXScale, normalizedYScale, sourceName: entityType, value: moveBoxImpulse),
            "moveboxdown" => CreateMarker(RoomObjectType.MoveBoxDown, x, y, 42f, 42f, "sprite64", normalizedXScale, normalizedYScale, sourceName: entityType, value: moveBoxImpulse),
            "moveboxleft" => CreateMarker(RoomObjectType.MoveBoxLeft, x, y, 42f, 42f, "sprite64", normalizedXScale, normalizedYScale, sourceName: entityType, value: moveBoxImpulse),
            "moveboxright" => CreateMarker(RoomObjectType.MoveBoxRight, x, y, 42f, 42f, "sprite64", normalizedXScale, normalizedYScale, sourceName: entityType, value: moveBoxImpulse),
            "controlpoint" or "controlpoint1" or "controlpoint2" or "controlpoint3" or "controlpoint4" or "controlpoint5"
                => CreateControlPointMarker(RoomObjectType.ControlPoint, x, y, entityType, normalizedXScale, normalizedYScale, properties),
            "kothcontrolpoint" => CreateControlPointMarker(RoomObjectType.ControlPoint, x, y, entityType, normalizedXScale, normalizedYScale, properties),
            "kothredcontrolpoint" => CreateControlPointMarker(
                RoomObjectType.ControlPoint,
                x,
                y,
                entityType,
                normalizedXScale,
                normalizedYScale,
                properties,
                ControlPointInitialOwnership.Red),
            "kothbluecontrolpoint" => CreateControlPointMarker(
                RoomObjectType.ControlPoint,
                x,
                y,
                entityType,
                normalizedXScale,
                normalizedYScale,
                properties,
                ControlPointInitialOwnership.Blue),
            "capturepoint" => CreateMarker(RoomObjectType.CaptureZone, x, y, 42f, 42f, string.Empty, normalizedXScale, normalizedYScale, sourceName: entityType),
            "setupgate" => CreateMarker(RoomObjectType.ControlPointSetupGate, x, y, 60f, 6f, "sprite44", normalizedXScale, normalizedYScale, sourceName: entityType),
            "arenacontrolpoint" => CreateControlPointMarker(RoomObjectType.ArenaControlPoint, x, y, entityType, normalizedXScale, normalizedYScale, properties),
            "generatorred" => CreateMarker(RoomObjectType.Generator, x, y, 40f, 40f, "GeneratorS", normalizedXScale, normalizedYScale, PlayerTeam.Red, entityType),
            "generatorblue" => CreateMarker(RoomObjectType.Generator, x, y, 40f, 40f, "GeneratorS", normalizedXScale, normalizedYScale, PlayerTeam.Blue, entityType),
            _ => default,
        };

        if (marker.Type != default)
        {
            var (topLeftX, topLeftY) = CustomMapEntityPlacementAnchor.ToTopLeft(
                x,
                y,
                marker.Width,
                marker.Height,
                useCenterOrigin);
            marker = marker with { X = topLeftX, Y = topLeftY };
        }

        return marker.Type != default || entityType.Equals("spawnroom", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryCreateFromBuilderEntity(CustomMapBuilderEntity entity, out RoomObjectMarker marker)
    {
        return TryCreate(
            entity.Type,
            entity.X,
            entity.Y,
            entity.XScale,
            entity.YScale,
            entity.Properties,
            out marker,
            useCenterOrigin: false);
    }

    private static RoomObjectMarker CreateMarker(
        RoomObjectType type,
        float x,
        float y,
        float width,
        float height,
        string spriteName,
        float xScale,
        float yScale,
        PlayerTeam? team = null,
        string sourceName = "",
        float value = 0f,
        ControlPointInitialOwnership initialOwnership = ControlPointInitialOwnership.ModeDefault,
        ControlPointLockRules lockRules = default,
        float capTimeMultiplier = 1f,
        bool isCapTimeMultiplierCustom = false)
    {
        return new RoomObjectMarker(
            type,
            x,
            y,
            width * xScale,
            height * yScale,
            spriteName,
            team,
            sourceName,
            value,
            initialOwnership,
            lockRules,
            capTimeMultiplier,
            isCapTimeMultiplierCustom);
    }

    private static RoomObjectMarker CreateControlPointMarker(
        RoomObjectType type,
        float x,
        float y,
        string sourceName,
        float xScale,
        float yScale,
        IReadOnlyDictionary<string, string> properties,
        ControlPointInitialOwnership fallbackOwnership = ControlPointInitialOwnership.ModeDefault)
    {
        var ownership = ControlPointOwnershipResolver.ParseMarkerInitialOwnership(properties);
        if (ownership == ControlPointInitialOwnership.ModeDefault)
        {
            ownership = fallbackOwnership;
        }

        var resolvedSourceName = ResolveControlPointSourceName(sourceName, properties);
        var lockRules = ControlPointLockDependencyMetadata.Parse(properties);
        var (capTimeMultiplier, isCapTimeMultiplierCustom) = ControlPointCapTimeMultiplierMetadata.Parse(properties);
        return CreateMarker(
            type,
            x,
            y,
            42f,
            42f,
            "ControlPointNeutralS",
            xScale,
            yScale,
            sourceName: resolvedSourceName,
            initialOwnership: ownership,
            lockRules: lockRules,
            capTimeMultiplier: capTimeMultiplier,
            isCapTimeMultiplierCustom: isCapTimeMultiplierCustom);
    }

    private static string ResolveControlPointSourceName(string sourceName, IReadOnlyDictionary<string, string> properties)
    {
        if (ControlPointMarkerIndex.TryParseSourceName(sourceName, out _))
        {
            return sourceName;
        }

        if (properties.TryGetValue(ControlPointIndexMetadata.PropertyKey, out var indexRaw)
            && ControlPointIndexMetadata.TryParse(indexRaw, out var index))
        {
            return $"controlPoint{index}";
        }

        return sourceName;
    }

    private static float ResolveResetMoveStatus(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("resetMoveStatus", out var rawValue)
            && float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue != 0f ? 1f : 0f;
        }

        return 1f;
    }

    private static float ResolveMoveBoxImpulse(IReadOnlyDictionary<string, string> properties)
    {
        const float defaultMoveBoxPushPowerPerTick = 5f;
        if (properties.TryGetValue("speed", out var rawValue)
            && float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue)
            && parsedValue > 0f)
        {
            return parsedValue * LegacyMovementModel.SourceTicksPerSecond;
        }

        return defaultMoveBoxPushPowerPerTick * LegacyMovementModel.SourceTicksPerSecond;
    }
}
