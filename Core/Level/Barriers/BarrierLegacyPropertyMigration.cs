using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

/// <summary>
/// Converts pre-granular barrier property bags into the modern per-target block/allow schema.
/// </summary>
public static class BarrierLegacyPropertyMigration
{
    public static IReadOnlyDictionary<string, string> EnsureModernBarrierProperties(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (UsesModernBarrierSchema(properties))
        {
            return StripDeprecatedBarrierProperties(properties);
        }

        if (!UsesLegacyBarrierSchema(properties))
        {
            return StripDeprecatedBarrierProperties(properties);
        }

        var migrated = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
        ApplyLegacyBarrierSemantics(migrated);
        return StripDeprecatedBarrierProperties(migrated);
    }

    public static IReadOnlyDictionary<string, string> EnsureModernDirectionalWallProperties(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (UsesModernDirectionalWallSchema(properties))
        {
            return StripDeprecatedDirectionalWallProperties(properties);
        }

        if (!UsesLegacyDirectionalWallSchema(properties))
        {
            return StripDeprecatedDirectionalWallProperties(properties);
        }

        var migrated = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
        ApplyLegacyDirectionalWallSemantics(migrated);
        return StripDeprecatedDirectionalWallProperties(migrated);
    }

    public static bool UsesModernDirectionalWallSchema(IReadOnlyDictionary<string, string> properties)
    {
        return properties.ContainsKey(DirectionalWallConfiguration.PassDirectionPropertyKey)
            || (properties.ContainsKey(DirectionalWallConfiguration.PlayersPropertyKey)
                && !properties.ContainsKey(BarrierTargetFilterMetadata.RedPlayersPropertyKey));
    }

    public static bool UsesLegacyDirectionalWallSchema(IReadOnlyDictionary<string, string> properties)
    {
        return properties.ContainsKey("axis")
            || properties.ContainsKey("fromLeft")
            || properties.ContainsKey("fromRight")
            || properties.ContainsKey("fromTop")
            || properties.ContainsKey("fromBottom")
            || properties.ContainsKey(BarrierTargetFilterMetadata.RedPlayersPropertyKey);
    }

    public static bool UsesModernBarrierSchema(IReadOnlyDictionary<string, string> properties)
    {
        foreach (var key in BarrierTargetFilterMetadata.TargetPropertyKeys)
        {
            if (properties.TryGetValue(key, out var value)
                && (value.Equals(BarrierTargetFilterMetadata.BlockValue, StringComparison.OrdinalIgnoreCase)
                    || value.Equals(BarrierTargetFilterMetadata.AllowValue, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool UsesLegacyBarrierSchema(IReadOnlyDictionary<string, string> properties)
    {
        return properties.ContainsKey("blockPlayers")
            || properties.ContainsKey("blockBullets")
            || properties.ContainsKey("blockIntel")
            || properties.ContainsKey("team")
            || properties.ContainsKey("mode")
            || properties.ContainsKey("shape")
            || properties.ContainsKey("blockLeft")
            || properties.ContainsKey("blockRight");
    }

    private static IReadOnlyDictionary<string, string> StripDeprecatedBarrierProperties(IReadOnlyDictionary<string, string> properties)
    {
        if (properties is Dictionary<string, string> mutable)
        {
            RemoveDeprecatedBarrierKeys(mutable);
            return mutable;
        }

        var copy = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
        RemoveDeprecatedBarrierKeys(copy);
        return copy;
    }

    private static void RemoveDeprecatedBarrierKeys(Dictionary<string, string> properties)
    {
        properties.Remove("mode");
        properties.Remove("shape");
        properties.Remove("orientation");
        properties.Remove("team");
        properties.Remove("blockPlayers");
        properties.Remove("blockBullets");
        properties.Remove("blockIntel");
        properties.Remove("blockLeft");
        properties.Remove("blockRight");
        properties.Remove("allowTeam");
        properties.Remove("fromLeft");
        properties.Remove("fromRight");
        properties.Remove("fromTop");
        properties.Remove("fromBottom");
    }

    private static IReadOnlyDictionary<string, string> StripDeprecatedDirectionalWallProperties(IReadOnlyDictionary<string, string> properties)
    {
        if (properties is Dictionary<string, string> mutable)
        {
            RemoveDeprecatedBarrierKeys(mutable);
            RemoveDeprecatedDirectionalWallKeys(mutable);
            return mutable;
        }

        var copy = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
        RemoveDeprecatedBarrierKeys(copy);
        RemoveDeprecatedDirectionalWallKeys(copy);
        return copy;
    }

    private static void RemoveDeprecatedDirectionalWallKeys(Dictionary<string, string> properties)
    {
        foreach (var key in BarrierTargetFilterMetadata.TargetPropertyKeys)
        {
            properties.Remove(key);
        }

        properties.Remove("fromLeft");
        properties.Remove("fromRight");
        properties.Remove("fromTop");
        properties.Remove("fromBottom");
        properties.Remove("axis");
        properties.Remove("mode");
        properties.Remove("shape");
    }

    private static void ApplyLegacyDirectionalWallSemantics(Dictionary<string, string> properties)
    {
        var floor = GetProperty(properties, "orientation", "wall").Equals("floor", StringComparison.OrdinalIgnoreCase)
            || GetProperty(properties, "axis", "wall").Equals("floor", StringComparison.OrdinalIgnoreCase);
        var fromLeft = ReadBool(properties, "fromLeft", false) || ReadBool(properties, "blockLeft", false);
        var fromRight = ReadBool(properties, "fromRight", false) || ReadBool(properties, "blockRight", false);
        var fromTop = ReadBool(properties, "fromTop", false);
        var fromBottom = ReadBool(properties, "fromBottom", false);

        properties[DirectionalWallConfiguration.PassDirectionPropertyKey] = ResolveLegacyPassDirection(floor, fromLeft, fromRight, fromTop, fromBottom);

        var blockPlayers = ReadBool(properties, BarrierTargetFilterMetadata.RedPlayersPropertyKey, false)
            || ReadBool(properties, BarrierTargetFilterMetadata.BluePlayersPropertyKey, false)
            || ReadBool(properties, "blockPlayers", false);
        var blockProjectiles = ReadBool(properties, BarrierTargetFilterMetadata.RedShotsPropertyKey, false)
            || ReadBool(properties, BarrierTargetFilterMetadata.BlueShotsPropertyKey, false)
            || ReadBool(properties, "blockBullets", false);

        properties[DirectionalWallConfiguration.PlayersPropertyKey] = blockPlayers
            ? DirectionalWallConfiguration.AffectValue
            : DirectionalWallConfiguration.IgnoreValue;
        properties[DirectionalWallConfiguration.ProjectilesPropertyKey] = blockProjectiles
            ? DirectionalWallConfiguration.AffectValue
            : DirectionalWallConfiguration.IgnoreValue;
    }

    private static string ResolveLegacyPassDirection(bool floor, bool fromLeft, bool fromRight, bool fromTop, bool fromBottom)
    {
        if (floor)
        {
            if (fromTop)
            {
                return DirectionalWallConfiguration.PassDirectionDownValue;
            }

            if (fromBottom)
            {
                return DirectionalWallConfiguration.PassDirectionUpValue;
            }
        }
        else
        {
            if (fromLeft)
            {
                return DirectionalWallConfiguration.PassDirectionRightValue;
            }

            if (fromRight)
            {
                return DirectionalWallConfiguration.PassDirectionLeftValue;
            }
        }

        if (fromTop)
        {
            return DirectionalWallConfiguration.PassDirectionDownValue;
        }

        if (fromBottom)
        {
            return DirectionalWallConfiguration.PassDirectionUpValue;
        }

        return DirectionalWallConfiguration.PassDirectionRightValue;
    }

    private static void ApplyLegacyBarrierSemantics(Dictionary<string, string> properties)
    {
        var team = GetProperty(properties, "team", "neutral");
        var blockPlayers = ReadBool(properties, "blockPlayers", false);
        var blockBullets = ReadBool(properties, "blockBullets", false);
        var blockIntel = ReadBool(properties, "blockIntel", false);
        var modeAllow = GetProperty(properties, "mode", "block").Equals("allow", StringComparison.OrdinalIgnoreCase);
        var redPlayers = ReadLegacyTargetSelection(properties, BarrierTargetFilterMetadata.RedPlayersPropertyKey, false);
        var bluePlayers = ReadLegacyTargetSelection(properties, BarrierTargetFilterMetadata.BluePlayersPropertyKey, false);
        var redShots = ReadLegacyTargetSelection(properties, BarrierTargetFilterMetadata.RedShotsPropertyKey, false);
        var blueShots = ReadLegacyTargetSelection(properties, BarrierTargetFilterMetadata.BlueShotsPropertyKey, false);
        var redIntel = ReadLegacyTargetSelection(properties, BarrierTargetFilterMetadata.RedIntelPropertyKey, false);
        var blueIntel = ReadLegacyTargetSelection(properties, BarrierTargetFilterMetadata.BlueIntelPropertyKey, false);

        if (!HasAnyLegacyTargetSelection(properties))
        {
            switch (team.ToLowerInvariant())
            {
                case "red":
                    bluePlayers = blockPlayers;
                    blueShots = blockBullets;
                    redIntel = blockIntel;
                    break;
                case "blue":
                    redPlayers = blockPlayers;
                    redShots = blockBullets;
                    blueIntel = blockIntel;
                    break;
                default:
                    redPlayers = blockPlayers;
                    bluePlayers = blockPlayers;
                    redShots = blockBullets;
                    blueShots = blockBullets;
                    redIntel = blockIntel;
                    blueIntel = blockIntel;
                    break;
            }
        }

        WriteLegacyTarget(properties, BarrierTargetFilterMetadata.RedPlayersPropertyKey, redPlayers, modeAllow);
        WriteLegacyTarget(properties, BarrierTargetFilterMetadata.BluePlayersPropertyKey, bluePlayers, modeAllow);
        WriteLegacyTarget(properties, BarrierTargetFilterMetadata.RedShotsPropertyKey, redShots, modeAllow);
        WriteLegacyTarget(properties, BarrierTargetFilterMetadata.BlueShotsPropertyKey, blueShots, modeAllow);
        WriteLegacyTarget(properties, BarrierTargetFilterMetadata.RedIntelPropertyKey, redIntel, modeAllow);
        WriteLegacyTarget(properties, BarrierTargetFilterMetadata.BlueIntelPropertyKey, blueIntel, modeAllow);
    }

    private static bool HasAnyLegacyTargetSelection(IReadOnlyDictionary<string, string> properties)
    {
        foreach (var key in BarrierTargetFilterMetadata.TargetPropertyKeys)
        {
            if (properties.ContainsKey(key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReadLegacyTargetSelection(IReadOnlyDictionary<string, string> properties, string key, bool fallback)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals(BarrierTargetFilterMetadata.BlockValue, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteLegacyTarget(Dictionary<string, string> properties, string key, bool selected, bool modeAllow)
    {
        var blocks = modeAllow ? !selected : selected;
        properties[key] = blocks
            ? BarrierTargetFilterMetadata.BlockValue
            : BarrierTargetFilterMetadata.AllowValue;
    }

    private static string GetProperty(IReadOnlyDictionary<string, string> properties, string key, string fallback)
    {
        return properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> properties, string key, bool fallback)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
