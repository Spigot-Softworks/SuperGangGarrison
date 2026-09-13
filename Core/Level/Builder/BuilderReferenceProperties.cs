namespace OpenGarrison.Core;

/// <summary>Reference properties shared by editing, cloning and validation.</summary>
public static class BuilderReferenceProperties
{
    public static IReadOnlyList<string> Logic { get; } = Array.AsReadOnly(new[]
    {
        MapLogicMetadata.LogicInputPropertyKey, MapLogicMetadata.LogicInput1PropertyKey, MapLogicMetadata.LogicInput2PropertyKey,
        MapLogicMetadata.StartWhenPropertyKey, MapLogicMetadata.EndWhenPropertyKey, MapLogicMetadata.LogicSignalPropertyKey,
        MapLogicMetadata.LockedWhenLogicPropertyKey, MapLogicMetadata.UnlockedWhenLogicPropertyKey,
        DamageableMetadata.HealWhenPropertyKey, BotSpawnMetadata.TriggerPropertyKey, BotSpawnMetadata.DeathTriggerPropertyKey,
    });
    public static IReadOnlyList<string> Entity { get; } = Array.AsReadOnly(new[]
    {
        MapLogicMetadata.ActivatorEntityPropertyKey, TeleportMetadata.TeleportExitPropertyKey,
        AreaExtensionMetadata.ExtendsPropertyKey, DamageTriggerMetadata.DamageableEntityPropertyKey,
        GameplayMessageMetadata.OnEndTeleportExitPropertyKey,
    });
    public static bool IsList(string key) => key.Equals(MapLogicMetadata.ActivatorEntityPropertyKey, StringComparison.OrdinalIgnoreCase);
}
