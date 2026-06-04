using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class BarrierTargetFilterMetadata
{
    public const string BlockValue = "block";
    public const string AllowValue = "allow";

    public const string RedPlayersPropertyKey = "redPlayers";
    public const string BluePlayersPropertyKey = "bluePlayers";
    public const string RedShotsPropertyKey = "redShots";
    public const string BlueShotsPropertyKey = "blueShots";
    public const string RedIntelPropertyKey = "redIntel";
    public const string BlueIntelPropertyKey = "blueIntel";

    public static readonly string[] TargetPropertyKeys =
    [
        RedPlayersPropertyKey,
        BluePlayersPropertyKey,
        RedShotsPropertyKey,
        BlueShotsPropertyKey,
        RedIntelPropertyKey,
        BlueIntelPropertyKey,
    ];

    public static BarrierTargetFilter Parse(string? value, BarrierTargetFilter fallback = BarrierTargetFilter.Allow)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().Equals(BlockValue, StringComparison.OrdinalIgnoreCase)
            ? BarrierTargetFilter.Block
            : value.Trim().Equals(AllowValue, StringComparison.OrdinalIgnoreCase)
                ? BarrierTargetFilter.Allow
                : fallback;
    }

    public static string ToPropertyValue(BarrierTargetFilter filter)
    {
        return filter == BarrierTargetFilter.Block ? BlockValue : AllowValue;
    }

    public static string CyclePropertyValue(string current)
    {
        return Parse(current) == BarrierTargetFilter.Block ? AllowValue : BlockValue;
    }

    public static bool IsTargetPropertyKey(string key)
    {
        foreach (var targetKey in TargetPropertyKeys)
        {
            if (key.Equals(targetKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static BarrierTargetFilter ReadTarget(
        IReadOnlyDictionary<string, string> properties,
        string key,
        BarrierTargetFilter fallback = BarrierTargetFilter.Allow)
    {
        return properties.TryGetValue(key, out var value)
            ? Parse(value, fallback)
            : fallback;
    }

    public static void WriteTargets(Dictionary<string, string> properties, in BarrierTargetFilters filters)
    {
        properties[RedPlayersPropertyKey] = ToPropertyValue(filters.RedPlayers);
        properties[BluePlayersPropertyKey] = ToPropertyValue(filters.BluePlayers);
        properties[RedShotsPropertyKey] = ToPropertyValue(filters.RedShots);
        properties[BlueShotsPropertyKey] = ToPropertyValue(filters.BlueShots);
        properties[RedIntelPropertyKey] = ToPropertyValue(filters.RedIntel);
        properties[BlueIntelPropertyKey] = ToPropertyValue(filters.BlueIntel);
    }
}

public readonly record struct BarrierTargetFilters(
    BarrierTargetFilter RedPlayers,
    BarrierTargetFilter BluePlayers,
    BarrierTargetFilter RedShots,
    BarrierTargetFilter BlueShots,
    BarrierTargetFilter RedIntel,
    BarrierTargetFilter BlueIntel)
{
    public static BarrierTargetFilters Default => new(
        BarrierTargetFilter.Allow,
        BarrierTargetFilter.Allow,
        BarrierTargetFilter.Allow,
        BarrierTargetFilter.Allow,
        BarrierTargetFilter.Allow,
        BarrierTargetFilter.Allow);

    /// <summary>
    /// Standard solid wall: blocks both teams and intel carriers; projectiles pass unless configured otherwise.
    /// </summary>
    public static BarrierTargetFilters SolidWall => new(
        BarrierTargetFilter.Block,
        BarrierTargetFilter.Block,
        BarrierTargetFilter.Allow,
        BarrierTargetFilter.Allow,
        BarrierTargetFilter.Block,
        BarrierTargetFilter.Block);

    public static BarrierTargetFilters FromProperties(IReadOnlyDictionary<string, string>? properties)
    {
        properties ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return new BarrierTargetFilters(
            BarrierTargetFilterMetadata.ReadTarget(properties, BarrierTargetFilterMetadata.RedPlayersPropertyKey),
            BarrierTargetFilterMetadata.ReadTarget(properties, BarrierTargetFilterMetadata.BluePlayersPropertyKey),
            BarrierTargetFilterMetadata.ReadTarget(properties, BarrierTargetFilterMetadata.RedShotsPropertyKey),
            BarrierTargetFilterMetadata.ReadTarget(properties, BarrierTargetFilterMetadata.BlueShotsPropertyKey),
            BarrierTargetFilterMetadata.ReadTarget(properties, BarrierTargetFilterMetadata.RedIntelPropertyKey),
            BarrierTargetFilterMetadata.ReadTarget(properties, BarrierTargetFilterMetadata.BlueIntelPropertyKey));
    }

    public bool Blocks(BarrierTargetKind target)
    {
        return Get(target) == BarrierTargetFilter.Block;
    }

    public BarrierTargetFilter Get(BarrierTargetKind target)
    {
        return target switch
        {
            BarrierTargetKind.RedPlayers => RedPlayers,
            BarrierTargetKind.BluePlayers => BluePlayers,
            BarrierTargetKind.RedShots => RedShots,
            BarrierTargetKind.BlueShots => BlueShots,
            BarrierTargetKind.RedIntel => RedIntel,
            BarrierTargetKind.BlueIntel => BlueIntel,
            _ => BarrierTargetFilter.Allow,
        };
    }

    public Dictionary<string, string> ToProperties()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        BarrierTargetFilterMetadata.WriteTargets(properties, this);
        return properties;
    }
}

public enum BarrierTargetKind
{
    RedPlayers,
    BluePlayers,
    RedShots,
    BlueShots,
    RedIntel,
    BlueIntel,
}
