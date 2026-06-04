using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

/// <summary>
/// Converts between legacy GG2 entity type names and simplified editor-facing types with property bags.
/// </summary>
public static class CustomMapBuilderEntityNormalization
{
    public static readonly HashSet<string> LegacyPaletteHiddenTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "redspawn", "bluespawn",
        "redspawn1", "redspawn2", "redspawn3", "redspawn4",
        "bluespawn1", "bluespawn2", "bluespawn3", "bluespawn4",
        "redteamgate", "blueteamgate", "redteamgate2", "blueteamgate2",
        "redintelgate", "blueintelgate", "redintelgate2", "blueintelgate2",
        "intelgatehorizontal", "intelgatevertical",
        "playerwall", "playerwall_horizontal", "bulletwall", "bulletwall_horizontal",
        "leftdoor", "rightdoor",
        "controlPoint1", "controlPoint2", "controlPoint3", "controlPoint4", "controlPoint5",
    };

    public static IReadOnlyList<CustomMapBuilderEntity> NormalizeForEditor(IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        var totalControlPoints = ForwardSpawnMetadata.CountMapControlPointsFromEditorEntities(entities);
        var result = new List<CustomMapBuilderEntity>(entities.Count);
        foreach (var entity in entities)
        {
            result.Add(NormalizeEntityForEditor(entity, totalControlPoints));
        }

        return result;
    }

    public static IReadOnlyList<CustomMapBuilderEntity> ResolveForExport(IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        var result = new List<CustomMapBuilderEntity>(entities.Count);
        foreach (var entity in entities)
        {
            result.Add(ResolveEntityForExport(entity));
        }

        return result;
    }

    public static CustomMapBuilderEntity NormalizeEntityForEditor(CustomMapBuilderEntity entity)
    {
        return NormalizeEntityForEditor(entity, totalControlPoints: 0);
    }

    public static CustomMapBuilderEntity NormalizeEntityForEditor(CustomMapBuilderEntity entity, int totalControlPoints)
    {
        var type = entity.Type.Trim();
        if (TryNormalizeSpawnFromLegacy(type, entity, out var spawn, totalControlPoints))
        {
            return spawn;
        }

        if (TryNormalizeDirectionalWallFromLegacy(type, entity, out var directionalWall))
        {
            return directionalWall;
        }

        if (TryNormalizeBarrierFromLegacy(type, entity, out var barrier))
        {
            return barrier;
        }

        if (TryNormalizeControlPointFromLegacy(type, entity, out var controlPoint))
        {
            return controlPoint;
        }

        return entity;
    }

    public static CustomMapBuilderEntity ResolveEntityForExport(CustomMapBuilderEntity entity)
    {
        var type = entity.Type.Trim();
        if (type.Equals("spawn", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveSpawnForExport(entity);
        }

        if (type.Equals("barrier", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveBarrierForExport(entity);
        }

        if (type.Equals("directionalWall", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveDirectionalWallForExport(entity);
        }

        if (type.Equals("controlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveControlPointForExport(entity);
        }

        return entity;
    }

    public static bool IsEditorOnlyType(string type)
    {
        return CustomMapEntityRuntimeRegistry.IsModernEntityType(type);
    }

    /// <summary>
    /// Optional upgrade path: convert legacy entities to modern editor types (does not run automatically on open).
    /// </summary>
    public static bool TryUpgradeLegacyToModern(CustomMapBuilderEntity entity, out CustomMapBuilderEntity upgraded)
    {
        var normalized = NormalizeEntityForEditor(entity);
        if (!entity.Type.Equals(normalized.Type, StringComparison.OrdinalIgnoreCase))
        {
            upgraded = normalized;
            return true;
        }

        upgraded = entity;
        return false;
    }

    public static bool CountsAsTeamSpawn(CustomMapBuilderEntity entity, string team)
    {
        var type = entity.Type.Trim();
        if (type.Equals("spawn", StringComparison.OrdinalIgnoreCase))
        {
            var entityTeam = GetProperty(entity, "team", "red");
            if (entityTeam.Equals("neutral", StringComparison.OrdinalIgnoreCase))
            {
                return !GetBoolProperty(entity, "forward", false);
            }

            if (!entityTeam.Equals(team, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !GetBoolProperty(entity, "forward", false);
        }

        return type.Equals(team.Equals("red", StringComparison.OrdinalIgnoreCase) ? "redspawn" : "bluespawn", StringComparison.OrdinalIgnoreCase);
    }

    public static int CountControlPointsNormalized(IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        var count = 0;
        foreach (var entity in entities)
        {
            var type = entity.Type.Trim();
            if (type.Equals("controlPoint", StringComparison.OrdinalIgnoreCase))
            {
                count += 1;
                continue;
            }

            if (type.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase)
                && type.Length > "controlPoint".Length
                && char.IsDigit(type["controlPoint".Length]))
            {
                count += 1;
            }
        }

        return count;
    }

    private static bool TryNormalizeSpawnFromLegacy(
        string type,
        CustomMapBuilderEntity entity,
        out CustomMapBuilderEntity spawn,
        int totalControlPoints = 0)
    {
        spawn = default!;
        string? team = null;
        var forward = false;
        var spawnSlotIndex = 0;

        if (type.Equals("redspawn", StringComparison.OrdinalIgnoreCase))
        {
            team = "red";
        }
        else if (type.Equals("bluespawn", StringComparison.OrdinalIgnoreCase))
        {
            team = "blue";
        }
        else if (type.StartsWith("redspawn", StringComparison.OrdinalIgnoreCase) && type.Length > "redspawn".Length)
        {
            team = "red";
            forward = true;
            spawnSlotIndex = ParseTrailingIndex(type, "redspawn");
        }
        else if (type.StartsWith("bluespawn", StringComparison.OrdinalIgnoreCase) && type.Length > "bluespawn".Length)
        {
            team = "blue";
            forward = true;
            spawnSlotIndex = ParseTrailingIndex(type, "bluespawn");
        }
        else
        {
            return false;
        }

        var properties = CopyEditableProperties(entity);
        properties["team"] = team!;
        properties["forward"] = forward ? "true" : "false";
        if (forward && spawnSlotIndex > 0)
        {
            var teamValue = team!.Equals("blue", StringComparison.OrdinalIgnoreCase) ? PlayerTeam.Blue : PlayerTeam.Red;
            var linkedControlPointIndex = ForwardSpawnMetadata.ResolveForwardSpawnControlPointIndex(
                teamValue,
                spawnSlotIndex,
                totalControlPoints);
            properties["linkObjective"] = $"controlPoint{linkedControlPointIndex}";
            properties[ForwardSpawnPriorityMetadata.PropertyKey] = ForwardSpawnPriorityMetadata.ToPropertyValue(spawnSlotIndex);
            properties.TryAdd(ForwardSpawnMetadata.UseWhenPropertyKey, ForwardSpawnMetadata.DefaultUseWhenValue);
        }

        spawn = CustomMapBuilderEntity.Create("spawn", entity.X, entity.Y, properties, entity.XScale, entity.YScale).NormalizeForEditing();
        return true;
    }

    private static CustomMapBuilderEntity ResolveSpawnForExport(CustomMapBuilderEntity entity)
    {
        var team = GetProperty(entity, "team", "red");
        var forward = GetBoolProperty(entity, "forward", false);
        if (!forward)
        {
            if (team.Equals("neutral", StringComparison.OrdinalIgnoreCase))
            {
                return entity;
            }

            var legacyType = team.Equals("blue", StringComparison.OrdinalIgnoreCase) ? "bluespawn" : "redspawn";
            return entity with { Type = legacyType };
        }

        var priority = ForwardSpawnMetadata.ParsePriority(entity.Properties);
        if (team.Equals("neutral", StringComparison.OrdinalIgnoreCase))
        {
            return entity;
        }

        priority = Math.Clamp(priority, 1, 4);
        var forwardType = team.Equals("blue", StringComparison.OrdinalIgnoreCase)
            ? $"bluespawn{priority}"
            : $"redspawn{priority}";
        var properties = CopyEditableProperties(entity);
        properties.Remove("team");
        properties.Remove("forward");
        properties.Remove("objectiveIndex");
        properties.Remove("linkObjective");
        properties.Remove(ForwardSpawnPriorityMetadata.PropertyKey);
        properties.Remove(ForwardSpawnMetadata.UseWhenPropertyKey);
        return CustomMapBuilderEntity.Create(forwardType, entity.X, entity.Y, properties, entity.XScale, entity.YScale).NormalizeForEditing();
    }

    private static bool TryNormalizeBarrierFromLegacy(string type, CustomMapBuilderEntity entity, out CustomMapBuilderEntity barrier)
    {
        barrier = default!;
        if (!TryDescribeLegacyBarrier(type, out var description))
        {
            return false;
        }

        var properties = CopyEditableProperties(entity);
        foreach (var pair in DescribeLegacyBarrierProperties(description))
        {
            properties[pair.Key] = pair.Value;
        }

        barrier = CustomMapBuilderEntity.Create("barrier", entity.X, entity.Y, properties, entity.XScale, entity.YScale).NormalizeForEditing();
        return true;
    }

    private static bool TryNormalizeDirectionalWallFromLegacy(string type, CustomMapBuilderEntity entity, out CustomMapBuilderEntity directionalWall)
    {
        directionalWall = default!;
        if (!TryDescribeLegacyDirectionalWall(type, out var description))
        {
            return false;
        }

        var properties = CopyEditableProperties(entity);
        foreach (var pair in DescribeLegacyDirectionalWallProperties(description))
        {
            properties[pair.Key] = pair.Value;
        }

        directionalWall = CustomMapBuilderEntity.Create("directionalWall", entity.X, entity.Y, properties, entity.XScale, entity.YScale).NormalizeForEditing();
        return true;
    }

    private static CustomMapBuilderEntity ResolveBarrierForExport(CustomMapBuilderEntity entity)
    {
        var configuration = BarrierConfiguration.FromProperties(entity.Properties);
        if (configuration.Targets.BlocksAnyProjectile())
        {
            return entity;
        }

        var properties = CopyEditableProperties(entity);
        RemoveBarrierFilterProperties(properties);

        var floor = BarrierConfiguration.IsFloorOrientation(entity.Properties);
        if (!TryResolveLegacyBarrierExport(configuration, floor, out var legacyType))
        {
            return entity;
        }

        return CustomMapBuilderEntity.Create(legacyType, entity.X, entity.Y, properties, entity.XScale, entity.YScale).NormalizeForEditing();
    }

    private static CustomMapBuilderEntity ResolveDirectionalWallForExport(CustomMapBuilderEntity entity)
    {
        var properties = CopyEditableProperties(entity);
        RemoveBarrierFilterProperties(properties);
        properties.Remove("axis");
        properties.Remove("fromLeft");
        properties.Remove("fromRight");
        properties.Remove("fromTop");
        properties.Remove("fromBottom");
        properties.Remove(DirectionalWallConfiguration.PassDirectionPropertyKey);
        properties.Remove(DirectionalWallConfiguration.PlayersPropertyKey);
        properties.Remove(DirectionalWallConfiguration.ProjectilesPropertyKey);

        var configuration = DirectionalWallConfiguration.FromProperties(entity.Properties);
        if (!TryResolveLegacyDirectionalWallExport(configuration, out var legacyType))
        {
            return entity;
        }

        return CustomMapBuilderEntity.Create(legacyType, entity.X, entity.Y, properties, entity.XScale, entity.YScale).NormalizeForEditing();
    }

    private static void RemoveBarrierFilterProperties(Dictionary<string, string> properties)
    {
        foreach (var key in new[]
                 {
                     "team", "orientation", "blockPlayers", "blockBullets", "blockIntel", "blockLeft", "blockRight", "allowTeam",
                     "mode", "shape",
                     BarrierTargetFilterMetadata.RedPlayersPropertyKey,
                     BarrierTargetFilterMetadata.BluePlayersPropertyKey,
                     BarrierTargetFilterMetadata.RedShotsPropertyKey,
                     BarrierTargetFilterMetadata.BlueShotsPropertyKey,
                     BarrierTargetFilterMetadata.RedIntelPropertyKey,
                     BarrierTargetFilterMetadata.BlueIntelPropertyKey,
                 })
        {
            properties.Remove(key);
        }
    }

    private static bool TryResolveLegacyBarrierExport(in BarrierConfiguration configuration, bool floor, out string legacyType)
    {
        legacyType = string.Empty;
        var targets = configuration.Targets;

        if (targets.Blocks(BarrierTargetKind.RedIntel) || targets.Blocks(BarrierTargetKind.BlueIntel))
        {
            if (targets.Blocks(BarrierTargetKind.RedIntel)
                && !targets.Blocks(BarrierTargetKind.BlueIntel)
                && !targets.Blocks(BarrierTargetKind.RedPlayers)
                && !targets.Blocks(BarrierTargetKind.BluePlayers))
            {
                legacyType = floor ? "redintelgate2" : "redintelgate";
                return true;
            }

            if (targets.Blocks(BarrierTargetKind.BlueIntel)
                && !targets.Blocks(BarrierTargetKind.RedIntel)
                && !targets.Blocks(BarrierTargetKind.RedPlayers)
                && !targets.Blocks(BarrierTargetKind.BluePlayers))
            {
                legacyType = floor ? "blueintelgate2" : "blueintelgate";
                return true;
            }

            if (targets.Blocks(BarrierTargetKind.RedIntel) && targets.Blocks(BarrierTargetKind.BlueIntel))
            {
                legacyType = floor ? "intelgatehorizontal" : "intelgatevertical";
                return true;
            }

            return false;
        }

        if (targets.Blocks(BarrierTargetKind.BluePlayers)
            && targets.Blocks(BarrierTargetKind.BlueShots)
            && !targets.Blocks(BarrierTargetKind.RedPlayers)
            && !targets.Blocks(BarrierTargetKind.RedShots))
        {
            legacyType = floor ? "redteamgate2" : "redteamgate";
            return true;
        }

        if (targets.Blocks(BarrierTargetKind.RedPlayers)
            && targets.Blocks(BarrierTargetKind.RedShots)
            && !targets.Blocks(BarrierTargetKind.BluePlayers)
            && !targets.Blocks(BarrierTargetKind.BlueShots))
        {
            legacyType = floor ? "blueteamgate2" : "blueteamgate";
            return true;
        }

        if (targets.Blocks(BarrierTargetKind.RedPlayers)
            && targets.Blocks(BarrierTargetKind.BluePlayers)
            && !targets.Blocks(BarrierTargetKind.RedShots)
            && !targets.Blocks(BarrierTargetKind.BlueShots))
        {
            legacyType = floor ? "playerwall_horizontal" : "playerwall";
            return true;
        }

        if (targets.Blocks(BarrierTargetKind.RedShots)
            && targets.Blocks(BarrierTargetKind.BlueShots)
            && !targets.Blocks(BarrierTargetKind.RedPlayers)
            && !targets.Blocks(BarrierTargetKind.BluePlayers))
        {
            legacyType = floor ? "bulletwall_horizontal" : "bulletwall";
            return true;
        }

        return false;
    }

    private static bool TryResolveLegacyDirectionalWallExport(in DirectionalWallConfiguration configuration, out string legacyType)
    {
        legacyType = string.Empty;
        if (!configuration.AffectsPlayers || configuration.AffectsProjectiles)
        {
            return false;
        }

        if (configuration.PassDirection == DirectionalWallPassDirection.Right)
        {
            legacyType = "leftdoor";
            return true;
        }

        if (configuration.PassDirection == DirectionalWallPassDirection.Left)
        {
            legacyType = "rightdoor";
            return true;
        }

        return false;
    }

    private static Dictionary<string, string> DescribeLegacyBarrierProperties(LegacyBarrierDescription description)
    {
        if (description.BlockLeft || description.BlockRight)
        {
            return DescribeLegacyDirectionalWallProperties(description);
        }

        var properties = BarrierTargetFilters.Default.ToProperties();
        properties["xscale"] = "1";
        properties["yscale"] = "1";

        switch (description.Team.ToLowerInvariant())
        {
            case "red":
                if (description.BlockPlayers)
                {
                    properties[BarrierTargetFilterMetadata.BluePlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                }

                if (description.BlockBullets)
                {
                    properties[BarrierTargetFilterMetadata.BlueShotsPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                }

                if (description.BlockIntel)
                {
                    properties[BarrierTargetFilterMetadata.RedIntelPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                }

                break;
            case "blue":
                if (description.BlockPlayers)
                {
                    properties[BarrierTargetFilterMetadata.RedPlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                }

                if (description.BlockBullets)
                {
                    properties[BarrierTargetFilterMetadata.RedShotsPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                }

                if (description.BlockIntel)
                {
                    properties[BarrierTargetFilterMetadata.BlueIntelPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                }

                break;
            default:
                if (description.BlockPlayers)
                {
                    properties[BarrierTargetFilterMetadata.RedPlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                    properties[BarrierTargetFilterMetadata.BluePlayersPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                }

                if (description.BlockBullets)
                {
                    properties[BarrierTargetFilterMetadata.RedShotsPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                    properties[BarrierTargetFilterMetadata.BlueShotsPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                }

                if (description.BlockIntel)
                {
                    properties[BarrierTargetFilterMetadata.RedIntelPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                    properties[BarrierTargetFilterMetadata.BlueIntelPropertyKey] = BarrierTargetFilterMetadata.BlockValue;
                }

                break;
        }

        if (description.Orientation.Equals("floor", StringComparison.OrdinalIgnoreCase))
        {
            properties["axis"] = "floor";
        }

        return properties;
    }

    private static Dictionary<string, string> DescribeLegacyDirectionalWallProperties(LegacyBarrierDescription description)
    {
        var floor = description.Orientation.Equals("floor", StringComparison.OrdinalIgnoreCase);
        var passDirection = floor
            ? description.BlockLeft
                ? DirectionalWallConfiguration.PassDirectionDownValue
                : description.BlockRight
                    ? DirectionalWallConfiguration.PassDirectionUpValue
                    : DirectionalWallConfiguration.PassDirectionRightValue
            : description.BlockLeft
                ? DirectionalWallConfiguration.PassDirectionRightValue
                : description.BlockRight
                    ? DirectionalWallConfiguration.PassDirectionLeftValue
                    : DirectionalWallConfiguration.PassDirectionRightValue;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [DirectionalWallConfiguration.PassDirectionPropertyKey] = passDirection,
            [DirectionalWallConfiguration.PlayersPropertyKey] = description.BlockPlayers
                ? DirectionalWallConfiguration.AffectValue
                : DirectionalWallConfiguration.IgnoreValue,
            [DirectionalWallConfiguration.ProjectilesPropertyKey] = description.BlockBullets
                ? DirectionalWallConfiguration.AffectValue
                : DirectionalWallConfiguration.IgnoreValue,
            ["xscale"] = "1",
            ["yscale"] = "1",
        };
    }

    private static bool TryDescribeLegacyDirectionalWall(string type, out LegacyBarrierDescription description)
    {
        description = default;
        return type.ToLowerInvariant() switch
        {
            "leftdoor" => AssignDescription(out description, new("neutral", "wall", true, false, false, true, false, null)),
            "rightdoor" => AssignDescription(out description, new("neutral", "wall", true, false, false, false, true, null)),
            _ => false,
        };
    }

    private static bool AssignDescription(out LegacyBarrierDescription description, LegacyBarrierDescription value)
    {
        description = value;
        return true;
    }

    private static bool TryNormalizeControlPointFromLegacy(string type, CustomMapBuilderEntity entity, out CustomMapBuilderEntity controlPoint)
    {
        controlPoint = default!;
        if (!type.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase)
            || type.Length <= "controlPoint".Length
            || !char.IsDigit(type["controlPoint".Length]))
        {
            return false;
        }

        var index = ParseTrailingIndex(type, "controlPoint");
        if (index <= 0)
        {
            return false;
        }

        var properties = CopyEditableProperties(entity);
        properties["index"] = index.ToString(CultureInfo.InvariantCulture);
        controlPoint = CustomMapBuilderEntity.Create("controlPoint", entity.X, entity.Y, properties, entity.XScale, entity.YScale).NormalizeForEditing();
        return true;
    }

    private static CustomMapBuilderEntity ResolveControlPointForExport(CustomMapBuilderEntity entity)
    {
        var index = Math.Clamp(GetIntProperty(entity, "index", 1), 1, 5);
        var properties = CopyEditableProperties(entity);
        properties.Remove("index");
        return CustomMapBuilderEntity.Create($"controlPoint{index}", entity.X, entity.Y, properties, entity.XScale, entity.YScale).NormalizeForEditing();
    }

    private static bool TryDescribeLegacyBarrier(string type, out LegacyBarrierDescription description)
    {
        description = default;
        switch (type.ToLowerInvariant())
        {
            case "redteamgate":
                description = new("red", "wall", true, true, false, false, false, null);
                return true;
            case "blueteamgate":
                description = new("blue", "wall", true, true, false, false, false, null);
                return true;
            case "redteamgate2":
                description = new("red", "floor", true, true, false, false, false, null);
                return true;
            case "blueteamgate2":
                description = new("blue", "floor", true, true, false, false, false, null);
                return true;
            case "redintelgate":
                description = new("red", "wall", false, false, true, false, false, "red");
                return true;
            case "blueintelgate":
                description = new("blue", "wall", false, false, true, false, false, "blue");
                return true;
            case "redintelgate2":
                description = new("red", "floor", false, false, true, false, false, "red");
                return true;
            case "blueintelgate2":
                description = new("blue", "floor", false, false, true, false, false, "blue");
                return true;
            case "intelgatevertical":
                description = new("neutral", "wall", false, false, true, false, false, null);
                return true;
            case "intelgatehorizontal":
                description = new("neutral", "floor", false, false, true, false, false, null);
                return true;
            case "playerwall":
                description = new("neutral", "wall", true, false, false, false, false, null);
                return true;
            case "playerwall_horizontal":
                description = new("neutral", "floor", true, false, false, false, false, null);
                return true;
            case "bulletwall":
                description = new("neutral", "wall", false, true, false, false, false, null);
                return true;
            case "bulletwall_horizontal":
                description = new("neutral", "floor", false, true, false, false, false, null);
                return true;
            default:
                return false;
        }
    }

    private static string ResolveTeamGateLegacyType(string team, bool floor)
    {
        if (team.Equals("blue", StringComparison.OrdinalIgnoreCase))
        {
            return floor ? "blueteamgate2" : "blueteamgate";
        }

        return floor ? "redteamgate2" : "redteamgate";
    }

    private static string ResolveIntelGateLegacyType(string team, bool floor)
    {
        if (team.Equals("blue", StringComparison.OrdinalIgnoreCase))
        {
            return floor ? "blueintelgate2" : "blueintelgate";
        }

        if (team.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            return floor ? "redintelgate2" : "redintelgate";
        }

        return floor ? "intelgatehorizontal" : "intelgatevertical";
    }

    private static Dictionary<string, string> CopyEditableProperties(CustomMapBuilderEntity entity)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in entity.Properties)
        {
            if (pair.Key.Equals("type", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("x", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            properties[pair.Key] = pair.Value;
        }

        return properties;
    }

    private static string GetProperty(CustomMapBuilderEntity entity, string key, string fallback)
    {
        return entity.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }

    private static bool GetBoolProperty(CustomMapBuilderEntity entity, string key, bool fallback)
    {
        if (!entity.Properties.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetIntProperty(CustomMapBuilderEntity entity, string key, int fallback)
    {
        if (entity.Properties.TryGetValue(key, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static int ParseTrailingIndex(string type, string prefix)
    {
        var suffix = type[prefix.Length..];
        return int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : 0;
    }

    private static int ParseControlPointIndex(string linkObjective)
    {
        if (string.IsNullOrWhiteSpace(linkObjective))
        {
            return 0;
        }

        if (linkObjective.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return ParseTrailingIndex(linkObjective, "controlPoint");
        }

        if (linkObjective.Equals("KothControlPoint", StringComparison.OrdinalIgnoreCase)
            || linkObjective.Equals("KothRedControlPoint", StringComparison.OrdinalIgnoreCase)
            || linkObjective.Equals("KothBlueControlPoint", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private readonly record struct LegacyBarrierDescription(
        string Team,
        string Orientation,
        bool BlockPlayers,
        bool BlockBullets,
        bool BlockIntel,
        bool BlockLeft,
        bool BlockRight,
        string? AllowTeam);
}
