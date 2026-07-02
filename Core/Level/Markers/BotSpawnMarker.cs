using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public enum BotSpawnKind
{
    Bot,
    Dummy,
}

public enum BotSpawnRespawnMode
{
    NormalSpawn,
    Node,
}

public enum BotSpawnNameMode
{
    Random,
    Manual,
}

public readonly record struct BotSpawnMarker(
    float X,
    float Y,
    string TriggerRef,
    PlayerTeam Team,
    PlayerClass? ClassId,
    BotSpawnKind Kind,
    bool Respawn,
    BotSpawnRespawnMode RespawnMode,
    BotSpawnNameMode NameMode,
    string Name,
    bool ForceNameplate,
    bool ForceHealthBar,
    string DeathTriggerRef,
    int DeathTriggerNodeIndex = -1,
    int TriggerNodeIndex = -1)
{
    public bool UsesTrigger => TriggerNodeIndex >= 0;

    public bool UsesDeathTrigger => DeathTriggerNodeIndex >= 0;

    public BotSpawnMarker WithTriggerNodeIndex(int triggerNodeIndex) =>
        this with { TriggerNodeIndex = triggerNodeIndex };

    public BotSpawnMarker WithLogicNodeIndices(int triggerNodeIndex, int deathTriggerNodeIndex) =>
        this with { TriggerNodeIndex = triggerNodeIndex, DeathTriggerNodeIndex = deathTriggerNodeIndex };
}

public static class BotSpawnMetadata
{
    public const string BotSpawnEntityType = "botSpawn";
    public const string TriggerPropertyKey = "trigger";
    public const string TeamPropertyKey = "team";
    public const string ClassPropertyKey = "class";
    public const string KindPropertyKey = "kind";
    public const string RespawnPropertyKey = "respawn";
    public const string RespawnAtPropertyKey = "respawnAt";
    public const string NameModePropertyKey = "nameMode";
    public const string NamePropertyKey = "name";
    public const string ForceNameplatePropertyKey = "forceNameplate";
    public const string ForceHealthBarPropertyKey = "forceHealthBar";
    public const string DeathTriggerPropertyKey = "onDeathTrigger";
    public const string VisualReplicatedStateOwnerId = "mapbot";
    public const string ForceNameplateReplicatedStateKey = "force_nameplate";
    public const string ForceHealthBarReplicatedStateKey = "force_health_bar";
    public const string DeathTriggerNodeReplicatedStateKey = "death_node";
    public const string RandomClassValue = "random";
    public const string BotKindValue = "bot";
    public const string DummyKindValue = "dummy";
    public const string RespawnAtSpawnValue = "spawn";
    public const string RespawnAtNodeValue = "node";
    public const string RandomNameModeValue = "random";
    public const string ManualNameModeValue = "manual";

    public static bool IsBotSpawnEntityType(string type) =>
        type.Equals(BotSpawnEntityType, StringComparison.OrdinalIgnoreCase);

    public static BotSpawnMarker FromProperties(
        float x,
        float y,
        IReadOnlyDictionary<string, string> properties) => new(
        x,
        y,
        ReadProperty(properties, TriggerPropertyKey, string.Empty),
        ParseTeam(properties),
        ParseClass(properties),
        ParseKind(properties),
        ParseRespawn(properties),
        ParseRespawnMode(properties),
        ParseNameMode(properties),
        ParseName(properties),
        ParseForceNameplate(properties),
        ParseForceHealthBar(properties),
        ParseDeathTriggerRef(properties));

    public static PlayerTeam ParseTeam(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, TeamPropertyKey, "blue")
            : "blue";
        return value.Equals("red", StringComparison.OrdinalIgnoreCase)
            ? PlayerTeam.Red
            : PlayerTeam.Blue;
    }

    public static PlayerClass? ParseClass(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, ClassPropertyKey, RandomClassValue)
            : RandomClassValue;
        if (value.Equals(RandomClassValue, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var playerClass)
            ? playerClass
            : null;
    }

    public static BotSpawnKind ParseKind(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, KindPropertyKey, BotKindValue)
            : BotKindValue;
        return value.Equals(DummyKindValue, StringComparison.OrdinalIgnoreCase)
            ? BotSpawnKind.Dummy
            : BotSpawnKind.Bot;
    }

    public static bool ParseRespawn(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, RespawnPropertyKey, "true")
            : "true";
        return DamageTriggerMetadata.ParseBoolProperty(value);
    }

    public static BotSpawnRespawnMode ParseRespawnMode(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, RespawnAtPropertyKey, RespawnAtSpawnValue)
            : RespawnAtSpawnValue;
        return value.Equals(RespawnAtNodeValue, StringComparison.OrdinalIgnoreCase)
            ? BotSpawnRespawnMode.Node
            : BotSpawnRespawnMode.NormalSpawn;
    }

    public static BotSpawnNameMode ParseNameMode(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, NameModePropertyKey, RandomNameModeValue)
            : RandomNameModeValue;
        return value.Equals(ManualNameModeValue, StringComparison.OrdinalIgnoreCase)
            ? BotSpawnNameMode.Manual
            : BotSpawnNameMode.Random;
    }

    public static string ParseName(IReadOnlyDictionary<string, string>? properties) =>
        properties is not null ? ReadProperty(properties, NamePropertyKey, string.Empty) : string.Empty;

    public static bool ParseForceNameplate(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, ForceNameplatePropertyKey, "false")
            : "false";
        return DamageTriggerMetadata.ParseBoolProperty(value);
    }

    public static bool ParseForceHealthBar(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, ForceHealthBarPropertyKey, "false")
            : "false";
        return DamageTriggerMetadata.ParseBoolProperty(value);
    }

    public static string ParseDeathTriggerRef(IReadOnlyDictionary<string, string>? properties) =>
        properties is not null ? ReadProperty(properties, DeathTriggerPropertyKey, string.Empty) : string.Empty;

    public static string ToTeamPropertyValue(PlayerTeam team) =>
        team == PlayerTeam.Red ? "red" : "blue";

    public static string ToClassPropertyValue(PlayerClass? playerClass) =>
        playerClass.HasValue ? playerClass.Value.ToString().ToLowerInvariant() : RandomClassValue;

    public static string ToKindPropertyValue(BotSpawnKind kind) =>
        kind == BotSpawnKind.Dummy ? DummyKindValue : BotKindValue;

    public static string ToRespawnPropertyValue(bool respawn) =>
        respawn ? "true" : "false";

    public static string ToRespawnModePropertyValue(BotSpawnRespawnMode mode) =>
        mode == BotSpawnRespawnMode.Node ? RespawnAtNodeValue : RespawnAtSpawnValue;

    public static string ToNameModePropertyValue(BotSpawnNameMode mode) =>
        mode == BotSpawnNameMode.Manual ? ManualNameModeValue : RandomNameModeValue;

    public static string ToForceNameplatePropertyValue(bool force) =>
        force ? "true" : "false";

    public static string ToForceHealthBarPropertyValue(bool force) =>
        force ? "true" : "false";

    public static string CycleTeamPropertyValue(string? value) =>
        ToTeamPropertyValue(value?.Trim().Equals("red", StringComparison.OrdinalIgnoreCase) == true
            ? PlayerTeam.Blue
            : PlayerTeam.Red);

    public static string CycleKindPropertyValue(string? value) =>
        value?.Trim().Equals(DummyKindValue, StringComparison.OrdinalIgnoreCase) == true
            ? BotKindValue
            : DummyKindValue;

    public static string CycleRespawnPropertyValue(string? value) =>
        ToRespawnPropertyValue(!DamageTriggerMetadata.ParseBoolProperty(value ?? "true"));

    public static string CycleRespawnModePropertyValue(string? value) =>
        ParseRespawnMode(new Dictionary<string, string>
        {
            [RespawnAtPropertyKey] = value ?? string.Empty,
        }) == BotSpawnRespawnMode.Node
            ? RespawnAtSpawnValue
            : RespawnAtNodeValue;

    public static string CycleNameModePropertyValue(string? value) =>
        ParseNameMode(new Dictionary<string, string>
        {
            [NameModePropertyKey] = value ?? string.Empty,
        }) == BotSpawnNameMode.Manual
            ? RandomNameModeValue
            : ManualNameModeValue;

    public static string CycleForceNameplatePropertyValue(string? value) =>
        ToForceNameplatePropertyValue(!DamageTriggerMetadata.ParseBoolProperty(value ?? "false"));

    public static string CycleForceHealthBarPropertyValue(string? value) =>
        ToForceHealthBarPropertyValue(!DamageTriggerMetadata.ParseBoolProperty(value ?? "false"));

    public static string CycleClassPropertyValue(string? value)
    {
        var classes = GetClassCycle();
        var current = ParseClass(new Dictionary<string, string>
        {
            [ClassPropertyKey] = value ?? string.Empty,
        });
        if (!current.HasValue)
        {
            return ToClassPropertyValue(classes[0]);
        }

        for (var index = 0; index < classes.Length; index += 1)
        {
            if (classes[index] == current.Value)
            {
                return index + 1 < classes.Length
                    ? ToClassPropertyValue(classes[index + 1])
                    : RandomClassValue;
            }
        }

        return RandomClassValue;
    }

    public static string GetTeamDisplayLabel(string? value) =>
        ParseTeam(new Dictionary<string, string>
        {
            [TeamPropertyKey] = value ?? string.Empty,
        }) == PlayerTeam.Red
            ? "Red"
            : "Blue";

    public static string GetClassDisplayLabel(string? value)
    {
        var playerClass = ParseClass(new Dictionary<string, string>
        {
            [ClassPropertyKey] = value ?? string.Empty,
        });
        return playerClass?.ToString() ?? "Random";
    }

    public static string GetKindDisplayLabel(string? value) =>
        ParseKind(new Dictionary<string, string>
        {
            [KindPropertyKey] = value ?? string.Empty,
        }) == BotSpawnKind.Dummy
            ? "Dummy"
            : "Bot";

    public static string GetRespawnDisplayLabel(string? value) =>
        DamageTriggerMetadata.ParseBoolProperty(value ?? "true") ? "On" : "Off";

    public static string GetRespawnModeDisplayLabel(string? value) =>
        ParseRespawnMode(new Dictionary<string, string>
        {
            [RespawnAtPropertyKey] = value ?? string.Empty,
        }) == BotSpawnRespawnMode.Node
            ? "Node"
            : "Spawn";

    public static string GetNameModeDisplayLabel(string? value) =>
        ParseNameMode(new Dictionary<string, string>
        {
            [NameModePropertyKey] = value ?? string.Empty,
        }) == BotSpawnNameMode.Manual
            ? "Manual"
            : "Random";

    public static string GetForceNameplateDisplayLabel(string? value) =>
        DamageTriggerMetadata.ParseBoolProperty(value ?? "false") ? "On" : "Off";

    public static string GetForceHealthBarDisplayLabel(string? value) =>
        DamageTriggerMetadata.ParseBoolProperty(value ?? "false") ? "On" : "Off";

    private static string ReadProperty(
        IReadOnlyDictionary<string, string> properties,
        string key,
        string fallback)
    {
        return properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }

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
