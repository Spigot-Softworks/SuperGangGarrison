using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public readonly record struct DamageableZoneConfiguration(
    float MaxHealth,
    int HealWhenNodeIndex,
    bool ShowHealthBar,
    bool BlockPlayers,
    bool DisableWhenDestroyed,
    bool SentryTarget,
    bool Stabbable)
{
    public bool IsConfigured => MaxHealth > 0f;
}

public static class DamageableMetadata
{
    public const string DamageableEntityType = "logicDamageable";

    public const string HealthPropertyKey = "health";
    public const string HealWhenPropertyKey = "healWhen";
    public const string ShowHealthBarPropertyKey = "showHealthBar";
    public const string BlockPlayersPropertyKey = "blockPlayers";
    public const string DisableWhenDestroyedPropertyKey = "disableWhenDestroyed";
    public const string SentryTargetPropertyKey = "sentryTarget";
    public const string StabbablePropertyKey = "stabbable";

    public const float DefaultZoneWidth = 42f;
    public const float DefaultZoneHeight = 42f;
    public const float MinZoneExtent = 12f;
    public const float DefaultHealth = 100f;
    public const float MinHealth = 1f;
    public const float MaxHealth = 10000f;

    public static bool IsDamageableEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(DamageableEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static (float Width, float Height) ResolveZoneDimensions(float xScale, float yScale)
    {
        var width = DefaultZoneWidth * MathF.Abs(xScale <= 0f ? 1f : xScale);
        var height = DefaultZoneHeight * MathF.Abs(yScale <= 0f ? 1f : yScale);
        return (MathF.Max(MinZoneExtent, width), MathF.Max(MinZoneExtent, height));
    }

    public static float ParseHealth(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(HealthPropertyKey, out var value)
            || !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return DefaultHealth;
        }

        return Math.Clamp(parsed, MinHealth, MaxHealth);
    }

    public static bool ParseShowHealthBar(IReadOnlyDictionary<string, string>? properties)
    {
        return properties is not null
            && properties.TryGetValue(ShowHealthBarPropertyKey, out var value)
            && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase));
    }

    public static bool ParseBlockPlayers(IReadOnlyDictionary<string, string>? properties)
    {
        return properties is not null
            && properties.TryGetValue(BlockPlayersPropertyKey, out var value)
            && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase));
    }

    public static bool ParseDisableWhenDestroyed(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(DisableWhenDestroyedPropertyKey, out var value))
        {
            return true;
        }

        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ParseSentryTarget(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(SentryTargetPropertyKey, out var value))
        {
            return true;
        }

        return ParseBoolProperty(value);
    }

    public static bool IsSentryTarget(in DamageableZoneConfiguration configuration, float currentHealth)
    {
        return configuration.SentryTarget
            && configuration.IsConfigured
            && currentHealth > 0f;
    }

    public static bool ParseStabbable(IReadOnlyDictionary<string, string>? properties)
    {
        return properties is not null
            && properties.TryGetValue(StabbablePropertyKey, out var value)
            && ParseBoolProperty(value);
    }

    public static bool IsStabbableTarget(in DamageableZoneConfiguration configuration, float currentHealth)
    {
        return configuration.Stabbable
            && configuration.IsConfigured
            && currentHealth > 0f;
    }

    public static string ToHealthPropertyValue(float health)
    {
        return Math.Clamp(health, MinHealth, MaxHealth).ToString(CultureInfo.InvariantCulture);
    }

    public static string CycleBooleanPropertyValue(string? current)
    {
        return ParseBoolProperty(current) ? "false" : "true";
    }

    public static string GetBooleanDisplayLabel(string? value)
    {
        return ParseBoolProperty(value) ? "on" : "off";
    }

    public static DamageableZoneConfiguration ParseZoneConfiguration(
        IReadOnlyDictionary<string, string>? properties,
        int healWhenNodeIndex = -1)
    {
        return new DamageableZoneConfiguration(
            ParseHealth(properties),
            healWhenNodeIndex,
            ParseShowHealthBar(properties),
            ParseBlockPlayers(properties),
            ParseDisableWhenDestroyed(properties),
            ParseSentryTarget(properties),
            ParseStabbable(properties));
    }

    public static bool TryResolveRoomObjectIndex(
        IReadOnlyList<RoomObjectMarker> roomObjects,
        string? entityRef,
        out int roomObjectIndex)
    {
        roomObjectIndex = -1;
        if (!MapLogicEntityReference.TryParseEntityRef(entityRef, out var entityType, out var x, out var y, out var mapEntityId))
        {
            return false;
        }

        if (!entityType.Equals(DamageableEntityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(mapEntityId))
        {
            for (var index = 0; index < roomObjects.Count; index += 1)
            {
                var marker = roomObjects[index];
                if (marker.Type != RoomObjectType.DamageableZone)
                {
                    continue;
                }

                if (marker.SourceName.Equals(mapEntityId, StringComparison.OrdinalIgnoreCase))
                {
                    roomObjectIndex = index;
                    return true;
                }
            }

            return false;
        }

        return MapLogicEntityReference.TryResolveRoomObjectIndex(roomObjects, entityRef, out roomObjectIndex);
    }

    public static bool BlocksProjectiles(
        in DamageableZoneConfiguration configuration,
        float currentHealth)
    {
        if (!configuration.IsConfigured)
        {
            return false;
        }

        if (currentHealth > 0f)
        {
            return true;
        }

        return !configuration.DisableWhenDestroyed;
    }

    public static bool BlocksPlayers(
        in DamageableZoneConfiguration configuration,
        float currentHealth)
    {
        if (!configuration.BlockPlayers)
        {
            return false;
        }

        return BlocksProjectiles(configuration, currentHealth);
    }

    private static bool ParseBoolProperty(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase));
    }
}
