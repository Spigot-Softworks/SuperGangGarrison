using OpenGarrison.Server.Plugins;
using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;
using OpenGarrison.GameplayModding;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal delegate bool RegisterGameplayAbilityDelegate(string pluginId, GameplayAbilityRegistration registration, out string errorMessage);

internal delegate bool OverrideGameplayAbilityDelegate(string pluginId, string itemId, GameplayAbilityPatch patch, out string errorMessage);

internal delegate bool RegisterGameplayAbilityExecutorDelegate(string pluginId, string executorId, IGameplayAbilityExecutor executor, out string errorMessage);

internal delegate bool RegisterGameplayPrimaryWeaponBehaviorDelegate(string pluginId, string behaviorId, IGameplayPrimaryWeaponExecutor executor, string? fireSoundName, out string errorMessage);

internal delegate bool RegisterGameplayWeaponItemDelegate(string pluginId, GameplayWeaponItemRegistration registration, out string errorMessage);

internal delegate bool RegisterGameplayLoadoutDelegate(string pluginId, GameplayLoadoutRegistration registration, out string errorMessage);

internal delegate bool RegisterGameplaySlotItemDelegate(string pluginId, GameplaySlotItemRegistration registration, out string errorMessage);

internal delegate bool ApplyGameplayImpulseDelegate(int playerId, float velocityX, float velocityY);

internal delegate bool SetGameplayAbilityCooldownDelegate(string pluginId, int playerId, string cooldownKey, int ticks);

internal delegate bool ApplyGameplayDamageDelegate(int targetPlayerId, float amount, int? attackerPlayerId, string? weaponSpriteName);

internal delegate bool ApplyGameplayHealingDelegate(int playerId, float amount);

internal delegate bool ApplyGameplayStatusEffectDelegate(int playerId, string statusEffectId, int ticks, float value);

internal delegate bool SpawnGameplayProjectileDelegate(GameplayProjectileSpawnRequest request, out int projectileId);

internal sealed class ServerPluginContext(
    string pluginId,
    string pluginDirectory,
    string configDirectory,
    OpenGarrisonPluginManifest manifest,
    OpenGarrisonPluginHostApi hostApi,
    string mapsDirectory,
    IOpenGarrisonServerReadOnlyState serverState,
    IOpenGarrisonServerAdminOperations adminOperations,
    IOpenGarrisonServerCvarRegistry cvars,
    IOpenGarrisonServerScheduler scheduler,
    Action<byte, string, string, string, PluginMessagePayloadFormat, ushort> sendMessageToClient,
    Action<string, string, string, PluginMessagePayloadFormat, ushort> broadcastMessageToClients,
    Func<string, byte, string, int, bool> setPlayerReplicatedStateInt,
    Func<string, byte, string, float, bool> setPlayerReplicatedStateFloat,
    Func<string, byte, string, bool, bool> setPlayerReplicatedStateBool,
    Func<string, byte, string, bool> clearPlayerReplicatedState,
    RegisterGameplayAbilityDelegate registerGameplayAbility,
    OverrideGameplayAbilityDelegate overrideGameplayAbility,
    RegisterGameplayAbilityExecutorDelegate registerGameplayAbilityExecutor,
    RegisterGameplayPrimaryWeaponBehaviorDelegate registerGameplayPrimaryWeaponBehavior,
    RegisterGameplayWeaponItemDelegate registerGameplayWeaponItem,
    RegisterGameplayLoadoutDelegate registerGameplayLoadout,
    RegisterGameplaySlotItemDelegate registerGameplaySlotItem,
    ApplyGameplayImpulseDelegate applyGameplayImpulse,
    SetGameplayAbilityCooldownDelegate setGameplayAbilityCooldown,
    ApplyGameplayDamageDelegate applyGameplayDamage,
    ApplyGameplayHealingDelegate applyGameplayHealing,
    ApplyGameplayStatusEffectDelegate applyGameplayStatusEffect,
    SpawnGameplayProjectileDelegate spawnGameplayProjectile,
    PluginCommandRegistry commandRegistry,
    Action<string> log) : IOpenGarrisonServerPluginContext
{
    public string PluginId { get; } = pluginId;

    public string PluginDirectory { get; } = pluginDirectory;

    public string ConfigDirectory { get; } = configDirectory;

    public OpenGarrisonPluginManifest Manifest { get; } = manifest;

    public OpenGarrisonPluginHostApi HostApi { get; } = hostApi;

    public string MapsDirectory { get; } = mapsDirectory;

    public IOpenGarrisonServerReadOnlyState ServerState { get; } = serverState;

    public IOpenGarrisonServerAdminOperations AdminOperations { get; } = adminOperations;

    public IOpenGarrisonServerCvarRegistry Cvars { get; } = cvars;

    public IOpenGarrisonServerScheduler Scheduler { get; } = scheduler;

    public void SendMessageToClient(byte slot, string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
    {
        sendMessageToClient(slot, targetPluginId, messageType, payload, payloadFormat, schemaVersion);
    }

    public void BroadcastMessageToClients(string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
    {
        broadcastMessageToClients(targetPluginId, messageType, payload, payloadFormat, schemaVersion);
    }

    public bool SetPlayerReplicatedStateInt(byte slot, string stateKey, int value)
    {
        return setPlayerReplicatedStateInt(PluginId, slot, stateKey, value);
    }

    public bool SetPlayerReplicatedStateFloat(byte slot, string stateKey, float value)
    {
        return setPlayerReplicatedStateFloat(PluginId, slot, stateKey, value);
    }

    public bool SetPlayerReplicatedStateBool(byte slot, string stateKey, bool value)
    {
        return setPlayerReplicatedStateBool(PluginId, slot, stateKey, value);
    }

    public bool ClearPlayerReplicatedState(byte slot, string stateKey)
    {
        return clearPlayerReplicatedState(PluginId, slot, stateKey);
    }

    public bool TryApplyGameplayImpulse(int playerId, float velocityX, float velocityY)
    {
        return applyGameplayImpulse(playerId, velocityX, velocityY);
    }

    public bool TrySetGameplayAbilityCooldown(int playerId, string cooldownKey, int ticks)
    {
        return setGameplayAbilityCooldown(PluginId, playerId, cooldownKey, ticks);
    }

    public bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId = null, string? weaponSpriteName = null)
    {
        return applyGameplayDamage(targetPlayerId, amount, attackerPlayerId, weaponSpriteName);
    }

    public bool TryApplyGameplayHealing(int playerId, float amount)
    {
        return applyGameplayHealing(playerId, amount);
    }

    public bool TryApplyGameplayStatusEffect(int playerId, string statusEffectId, int ticks, float value = 0f)
    {
        return applyGameplayStatusEffect(playerId, statusEffectId, ticks, value);
    }

    public bool TrySpawnGameplayProjectile(GameplayProjectileSpawnRequest request, out int projectileId)
    {
        return spawnGameplayProjectile(request, out projectileId);
    }

    public bool TryRegisterGameplayAbility(GameplayAbilityRegistration registration, out string errorMessage)
    {
        return registerGameplayAbility(PluginId, registration, out errorMessage);
    }

    public bool TryOverrideGameplayAbility(string itemId, GameplayAbilityPatch patch, out string errorMessage)
    {
        return overrideGameplayAbility(PluginId, itemId, patch, out errorMessage);
    }

    public bool TryRegisterGameplayAbilityExecutor(string executorId, IGameplayAbilityExecutor executor, out string errorMessage)
    {
        return registerGameplayAbilityExecutor(PluginId, executorId, executor, out errorMessage);
    }

    public bool TryRegisterGameplayPrimaryWeaponBehavior(string behaviorId, IGameplayPrimaryWeaponExecutor executor, string? fireSoundName, out string errorMessage)
    {
        return registerGameplayPrimaryWeaponBehavior(PluginId, behaviorId, executor, fireSoundName, out errorMessage);
    }

    public bool TryRegisterGameplayWeaponItem(GameplayWeaponItemRegistration registration, out string errorMessage)
    {
        return registerGameplayWeaponItem(PluginId, registration, out errorMessage);
    }

    public bool TryRegisterGameplayLoadout(GameplayLoadoutRegistration registration, out string errorMessage)
    {
        return registerGameplayLoadout(PluginId, registration, out errorMessage);
    }

    public bool TryRegisterGameplaySlotItem(GameplaySlotItemRegistration registration, out string errorMessage)
    {
        return registerGameplaySlotItem(PluginId, registration, out errorMessage);
    }

    public void RegisterCommand(IOpenGarrisonServerCommand command, OpenGarrisonServerAdminPermissions requiredPermissions)
    {
        commandRegistry.RegisterPluginCommand(command, PluginId, requiredPermissions);
    }

    public void RegisterCommand(IOpenGarrisonServerCommand command, OpenGarrisonServerAdminPermissions requiredPermissions, IReadOnlyList<string> aliases)
    {
        commandRegistry.RegisterPluginCommand(command, PluginId, requiredPermissions, aliases);
    }

    public void Log(string message)
    {
        log($"[plugin:{PluginId}] {message}");
    }
}
