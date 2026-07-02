using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public enum SpawnClassBehaviorTeam
{
    Any,
    Red,
    Blue,
}

public readonly record struct SpawnClassBehaviorMarker(
    float X,
    float Y,
    SpawnClassBehaviorTeam Team,
    PlayerClass? ForcedClass,
    bool ManualSpawn,
    bool SkipTeamSelect,
    bool AllowTeamChange,
    bool AllowClassChange)
{
    public bool AppliesToTeam(PlayerTeam team) =>
        Team == SpawnClassBehaviorTeam.Any
        || (Team == SpawnClassBehaviorTeam.Red && team == PlayerTeam.Red)
        || (Team == SpawnClassBehaviorTeam.Blue && team == PlayerTeam.Blue);
}

public static class SpawnClassBehaviorMetadata
{
    public const string EntityType = "spawnClassBehavior";
    public const string TeamPropertyKey = "team";
    public const string ForceClassPropertyKey = "forceClass";
    public const string ManualSpawnPropertyKey = "manualSpawn";
    public const string SkipTeamSelectPropertyKey = "skipTeamSelect";
    public const string AllowTeamChangePropertyKey = "allowTeamChange";
    public const string AllowClassChangePropertyKey = "allowClassChange";
    public const string AnyTeamValue = "any";
    public const string NoneClassValue = "none";
    public const string DefaultProperties =
        "team=any;forceClass=none;manualSpawn=false;skipTeamSelect=false;allowTeamChange=true;allowClassChange=true";

    public static bool IsSpawnClassBehaviorEntityType(string type) =>
        type.Equals(EntityType, StringComparison.OrdinalIgnoreCase);

    public static SpawnClassBehaviorMarker FromProperties(
        float x,
        float y,
        IReadOnlyDictionary<string, string> properties) => new(
        x,
        y,
        ParseTeam(properties),
        ParseForcedClass(properties),
        ParseBool(properties, ManualSpawnPropertyKey, false),
        ParseBool(properties, SkipTeamSelectPropertyKey, false),
        ParseBool(properties, AllowTeamChangePropertyKey, true),
        ParseBool(properties, AllowClassChangePropertyKey, true));

    public static SpawnClassBehaviorTeam ParseTeam(IReadOnlyDictionary<string, string>? properties)
    {
        var value = ReadProperty(properties, TeamPropertyKey, AnyTeamValue);
        if (value.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            return SpawnClassBehaviorTeam.Red;
        }

        return value.Equals("blue", StringComparison.OrdinalIgnoreCase)
            ? SpawnClassBehaviorTeam.Blue
            : SpawnClassBehaviorTeam.Any;
    }

    public static PlayerClass? ParseForcedClass(IReadOnlyDictionary<string, string>? properties)
    {
        var value = ReadProperty(properties, ForceClassPropertyKey, NoneClassValue);
        if (value.Equals(NoneClassValue, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var playerClass)
            ? playerClass
            : null;
    }

    public static bool ParseBool(
        IReadOnlyDictionary<string, string>? properties,
        string key,
        bool fallback)
    {
        var value = ReadProperty(properties, key, fallback ? "true" : "false");
        return DamageTriggerMetadata.ParseBoolProperty(value);
    }

    public static string ToTeamPropertyValue(SpawnClassBehaviorTeam team) =>
        team switch
        {
            SpawnClassBehaviorTeam.Red => "red",
            SpawnClassBehaviorTeam.Blue => "blue",
            _ => AnyTeamValue,
        };

    public static string ToForcedClassPropertyValue(PlayerClass? playerClass) =>
        playerClass.HasValue ? playerClass.Value.ToString().ToLowerInvariant() : NoneClassValue;

    public static string CycleTeamPropertyValue(string? value)
    {
        return ParseTeam(ToDictionary(TeamPropertyKey, value)) switch
        {
            SpawnClassBehaviorTeam.Any => "red",
            SpawnClassBehaviorTeam.Red => "blue",
            _ => AnyTeamValue,
        };
    }

    public static string CycleForcedClassPropertyValue(string? value)
    {
        var classes = GetClassCycle();
        var current = ParseForcedClass(ToDictionary(ForceClassPropertyKey, value));
        if (!current.HasValue)
        {
            return ToForcedClassPropertyValue(classes[0]);
        }

        for (var index = 0; index < classes.Length; index += 1)
        {
            if (classes[index] == current.Value)
            {
                return index + 1 < classes.Length
                    ? ToForcedClassPropertyValue(classes[index + 1])
                    : NoneClassValue;
            }
        }

        return NoneClassValue;
    }

    public static string GetTeamDisplayLabel(string? value) =>
        ParseTeam(ToDictionary(TeamPropertyKey, value)) switch
        {
            SpawnClassBehaviorTeam.Red => "Red",
            SpawnClassBehaviorTeam.Blue => "Blue",
            _ => "Any",
        };

    public static string GetForcedClassDisplayLabel(string? value)
    {
        var playerClass = ParseForcedClass(ToDictionary(ForceClassPropertyKey, value));
        return playerClass switch
        {
            PlayerClass.Quote => "Civilian",
            { } forced => forced.ToString(),
            _ => "None",
        };
    }

    public static bool TryGetForcedGameplayClassId(PlayerClass? playerClass, out string gameplayClassId)
    {
        if (playerClass.HasValue
            && CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(playerClass.Value, out var binding))
        {
            gameplayClassId = binding.ClassId;
            return true;
        }

        gameplayClassId = string.Empty;
        return false;
    }

    private static string ReadProperty(IReadOnlyDictionary<string, string>? properties, string key, string fallback)
    {
        return properties is not null
            && properties.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }

    private static Dictionary<string, string> ToDictionary(string key, string? value) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [key] = value ?? string.Empty,
        };

    private static PlayerClass[] GetClassCycle() =>
    [
        PlayerClass.Scout,
        PlayerClass.Soldier,
        PlayerClass.Pyro,
        PlayerClass.Demoman,
        PlayerClass.Heavy,
        PlayerClass.Engineer,
        PlayerClass.Medic,
        PlayerClass.Sniper,
        PlayerClass.Spy,
        PlayerClass.Quote,
    ];
}
