using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public readonly record struct ControlPointOwnershipContext(
    int PointIndex,
    int TotalPoints,
    bool ControlPointSetupMode,
    GameModeKind GameMode,
    bool OverrideInitialOwnership = false);

public static class ControlPointOwnershipResolver
{
    public static PlayerTeam? ResolveInitialTeam(RoomObjectMarker marker, in ControlPointOwnershipContext context)
    {
        if (context.OverrideInitialOwnership
            && marker.InitialOwnership != ControlPointInitialOwnership.ModeDefault)
        {
            return ControlPointInitialOwnershipMetadata.ToPlayerTeam(marker.InitialOwnership);
        }

        if (marker.SourceName.Equals("KothRedControlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerTeam.Red;
        }

        if (marker.SourceName.Equals("KothBlueControlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerTeam.Blue;
        }

        if (IsKothMode(context.GameMode))
        {
            return null;
        }

        return ResolveModeDefaultTeam(in context);
    }

    public static PlayerTeam? ResolveModeDefaultTeam(in ControlPointOwnershipContext context)
    {
        if (context.TotalPoints <= 0)
        {
            return null;
        }

        if (context.ControlPointSetupMode)
        {
            return PlayerTeam.Blue;
        }

        if (context.TotalPoints <= 1)
        {
            return null;
        }

        var middlePoint = context.TotalPoints / 2f;
        var middleCeiling = (int)MathF.Ceiling(middlePoint);
        PlayerTeam? team = context.PointIndex <= middlePoint ? PlayerTeam.Red : PlayerTeam.Blue;
        if (context.TotalPoints > 2 && context.PointIndex == middleCeiling)
        {
            team = null;
        }

        return team;
    }

    public static ControlPointInitialOwnership ParseMarkerInitialOwnership(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(ControlPointInitialOwnershipMetadata.PropertyKey, out var rawValue)
            || !ControlPointInitialOwnershipMetadata.TryParse(rawValue, out var ownership))
        {
            return ControlPointInitialOwnership.ModeDefault;
        }

        return ownership;
    }

    public static ControlPointInitialOwnership ParseEntityInitialOwnership(CustomMapBuilderEntity entity)
    {
        return ParseMarkerInitialOwnership(entity.Properties);
    }

    public static int ResolveControlPointIndex(CustomMapBuilderEntity entity)
    {
        var type = entity.Type.Trim();
        if (type.Equals("controlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return ControlPointIndexMetadata.ClampIndex(GetEntityInt(entity, ControlPointIndexMetadata.PropertyKey, ControlPointIndexMetadata.MinIndex));
        }

        if (type.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase)
            && type.Length > "controlPoint".Length
            && int.TryParse(type["controlPoint".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var legacyIndex))
        {
            return ControlPointIndexMetadata.ClampIndex(legacyIndex);
        }

        return ControlPointIndexMetadata.MinIndex;
    }

    public static List<CustomMapBuilderEntity> GetOrderedControlPointEntities(IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        var points = new List<(int Index, CustomMapBuilderEntity Entity)>();
        for (var entityIndex = 0; entityIndex < entities.Count; entityIndex += 1)
        {
            var entity = entities[entityIndex];
            if (!IsControlPointEntity(entity.Type))
            {
                continue;
            }

            points.Add((ResolveControlPointIndex(entity), entity));
        }

        return points
            .OrderBy(entry => entry.Index)
            .ThenBy(entry => entry.Entity.Y)
            .ThenBy(entry => entry.Entity.X)
            .Select(entry => entry.Entity)
            .ToList();
    }

    public static PlayerTeam? ResolveBuilderInitialTeam(
        CustomMapBuilderEntity entity,
        CustomMapBuilderGameMode builderMode,
        IReadOnlyList<CustomMapBuilderEntity> entities,
        bool controlPointSetupMode = false,
        bool overrideInitialOwnership = false)
    {
        var ownership = ParseEntityInitialOwnership(entity);
        if (overrideInitialOwnership && ownership != ControlPointInitialOwnership.ModeDefault)
        {
            return ControlPointInitialOwnershipMetadata.ToPlayerTeam(ownership);
        }

        var type = entity.Type.Trim();
        if (type.Equals("KothRedControlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerTeam.Red;
        }

        if (type.Equals("KothBlueControlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerTeam.Blue;
        }

        var ordered = GetOrderedControlPointEntities(entities);
        var pointIndex = Math.Max(1, ordered.FindIndex(candidate => ReferenceEquals(candidate, entity) || candidate == entity) + 1);
        if (pointIndex <= 0)
        {
            pointIndex = ResolveControlPointIndex(entity);
        }

        var context = new ControlPointOwnershipContext(
            pointIndex,
            Math.Max(1, ordered.Count),
            controlPointSetupMode,
            ToGameModeKind(builderMode),
            overrideInitialOwnership);
        if (IsKothMode(context.GameMode))
        {
            return null;
        }

        return ResolveModeDefaultTeam(in context);
    }

    public static string ResolveBuilderControlPointSpriteName(
        CustomMapBuilderEntity entity,
        CustomMapBuilderGameMode builderMode,
        IReadOnlyList<CustomMapBuilderEntity> entities,
        bool controlPointSetupMode = false,
        bool overrideInitialOwnership = false)
    {
        return ResolveBuilderInitialTeam(entity, builderMode, entities, controlPointSetupMode, overrideInitialOwnership) switch
        {
            PlayerTeam.Red => "ControlPointRedS",
            PlayerTeam.Blue => "ControlPointBlueS",
            _ => "ControlPointNeutralS",
        };
    }

    public static int ResolveBuilderControlPointSpriteFrame(CustomMapBuilderEntity entity, string spriteName)
    {
        if (!spriteName.Equals("ControlPointNeutralS", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return Math.Clamp(ResolveControlPointIndex(entity) - ControlPointIndexMetadata.MinIndex, 0, ControlPointIndexMetadata.MaxIndex);
    }

    public static bool IsControlPointEntity(string type)
    {
        var trimmed = type.Trim();
        if (trimmed.Equals("controlPoint", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("KothControlPoint", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("KothRedControlPoint", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("KothBlueControlPoint", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("ArenaControlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return trimmed.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase)
            && trimmed.Length > "controlPoint".Length
            && char.IsDigit(trimmed["controlPoint".Length]);
    }

    private static bool IsKothMode(GameModeKind mode)
    {
        return mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill;
    }

    private static GameModeKind ToGameModeKind(CustomMapBuilderGameMode builderMode)
    {
        return builderMode switch
        {
            CustomMapBuilderGameMode.CaptureTheFlag => GameModeKind.CaptureTheFlag,
            CustomMapBuilderGameMode.ControlPoint => GameModeKind.ControlPoint,
            CustomMapBuilderGameMode.AttackDefenseControlPoint => GameModeKind.ControlPoint,
            CustomMapBuilderGameMode.KingOfTheHill => GameModeKind.KingOfTheHill,
            CustomMapBuilderGameMode.DualKingOfTheHill => GameModeKind.DoubleKingOfTheHill,
            CustomMapBuilderGameMode.Arena => GameModeKind.Arena,
            CustomMapBuilderGameMode.Generator => GameModeKind.Generator,
            _ => GameModeKind.ControlPoint,
        };
    }

    private static int GetEntityInt(CustomMapBuilderEntity entity, string key, int fallback)
    {
        if (!entity.Properties.TryGetValue(key, out var rawValue)
            || !int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return fallback;
        }

        return value;
    }
}
