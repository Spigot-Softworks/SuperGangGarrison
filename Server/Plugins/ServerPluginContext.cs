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

internal delegate bool RegisterGameplayClassDelegate(string pluginId, GameplayClassRegistration registration, out string errorMessage);

internal delegate bool RegisterGameplaySlotItemDelegate(string pluginId, GameplaySlotItemRegistration registration, out string errorMessage);

internal delegate bool RegisterServerVoteKindDelegate(string pluginId, OpenGarrisonServerVoteRegistration registration, out string errorMessage);

internal delegate bool StartServerVoteDelegate(string pluginId, string voteKindId, byte initiatorSlot, string argument, out string errorMessage);

internal delegate bool ApplyGameplayImpulseDelegate(int playerId, float velocityX, float velocityY);

internal delegate bool SetGameplayVelocityDelegate(int playerId, float? velocityX, float? velocityY);
internal delegate bool SetGameplayPoseDelegate(int playerId, string? suffix, int ticksPerFrame, bool loop, int durationTicks);
internal delegate bool PlayGameplaySoundDelegate(int playerId, string soundName);
internal delegate bool SpawnGameplayBloodDelegate(int playerId, float directionDegrees, int count);
internal delegate bool SpawnGameplaySpriteEffectDelegate(string spriteName, float x, float y, float rotationDegrees, bool flipX, int ticksPerFrame);
internal delegate bool PlayGameplayScreenEffectDelegate(string effect, float x, float y, int durationTicks);
internal delegate bool FreezeGameplayProjectilesDelegate(float x, float y, float radius, int ticks, float travelPixels);
internal delegate bool TeleportGameplayPlayerDelegate(int playerId, float x, float y);

internal delegate bool SetGameplayAbilityCooldownDelegate(string pluginId, int playerId, string cooldownKey, int ticks);

internal delegate bool ApplyGameplayDamageDelegate(int targetPlayerId, float amount, int? attackerPlayerId, string? weaponSpriteName, string? deathType);

internal delegate bool ApplyGameplayHealingDelegate(int playerId, float amount, bool silent);

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
    RegisterGameplayClassDelegate registerGameplayClass,
    RegisterGameplaySlotItemDelegate registerGameplaySlotItem,
    RegisterServerVoteKindDelegate registerServerVoteKind,
    StartServerVoteDelegate startServerVote,
    ApplyGameplayImpulseDelegate applyGameplayImpulse,
    SetGameplayVelocityDelegate setGameplayVelocity,
    SetGameplayPoseDelegate setGameplayPose,
    PlayGameplaySoundDelegate playGameplaySound,
    SpawnGameplayBloodDelegate spawnGameplayBlood,
    SpawnGameplaySpriteEffectDelegate spawnGameplaySpriteEffect,
    PlayGameplayScreenEffectDelegate playGameplayScreenEffect,
    FreezeGameplayProjectilesDelegate freezeGameplayProjectiles,
    TeleportGameplayPlayerDelegate teleportGameplayPlayer,
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

    public bool TrySetGameplayVelocity(int playerId, float? velocityX, float? velocityY)
    {
        return setGameplayVelocity(playerId, velocityX, velocityY);
    }

    public bool TrySetGameplayPose(int playerId, string? suffix, int ticksPerFrame, bool loop, int durationTicks)
    {
        return setGameplayPose(playerId, suffix, ticksPerFrame, loop, durationTicks);
    }

    public bool TryPlayGameplaySound(int playerId, string soundName)
    {
        return playGameplaySound(playerId, soundName);
    }

    public bool TrySpawnGameplayBlood(int playerId, float directionDegrees, int count)
    {
        return spawnGameplayBlood(playerId, directionDegrees, count);
    }

    public bool TrySpawnGameplaySpriteEffect(string spriteName, float x, float y, float rotationDegrees, bool flipX, int ticksPerFrame)
    {
        return spawnGameplaySpriteEffect(spriteName, x, y, rotationDegrees, flipX, ticksPerFrame);
    }

    public bool TryPlayGameplayScreenEffect(string effect, float x, float y, int durationTicks)
    {
        return playGameplayScreenEffect(effect, x, y, durationTicks);
    }

    public bool TryFreezeGameplayProjectiles(float x, float y, float radius, int ticks)
    {
        return freezeGameplayProjectiles(x, y, radius, ticks, 0f);
    }

    public bool TryFreezeGameplayProjectiles(float x, float y, float radius, int ticks, float travelPixels)
    {
        return freezeGameplayProjectiles(x, y, radius, ticks, travelPixels);
    }

    public bool TryTeleportGameplayPlayer(int playerId, float x, float y)
    {
        return teleportGameplayPlayer(playerId, x, y);
    }

    public bool TrySetGameplayAbilityCooldown(int playerId, string cooldownKey, int ticks)
    {
        return setGameplayAbilityCooldown(PluginId, playerId, cooldownKey, ticks);
    }

    public bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId = null, string? weaponSpriteName = null)
    {
        return applyGameplayDamage(targetPlayerId, amount, attackerPlayerId, weaponSpriteName, null);
    }

    public bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId, string? weaponSpriteName, string? deathType)
    {
        return applyGameplayDamage(targetPlayerId, amount, attackerPlayerId, weaponSpriteName, deathType);
    }

    public bool TryApplyGameplayHealing(int playerId, float amount)
    {
        return applyGameplayHealing(playerId, amount, false);
    }

    public bool TryApplyGameplayHealing(int playerId, float amount, bool silent)
    {
        return applyGameplayHealing(playerId, amount, silent);
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

    public bool TryRegisterGameplayClass(GameplayClassRegistration registration, out string errorMessage)
    {
        return registerGameplayClass(PluginId, registration, out errorMessage);
    }

    public bool TryRegisterGameplaySlotItem(GameplaySlotItemRegistration registration, out string errorMessage)
    {
        return registerGameplaySlotItem(PluginId, registration, out errorMessage);
    }

    public bool TryRegisterVoteKind(OpenGarrisonServerVoteRegistration registration, out string errorMessage)
    {
        return registerServerVoteKind(PluginId, registration, out errorMessage);
    }

    public bool TryStartVote(string voteKindId, byte initiatorSlot, string argument, out string errorMessage)
    {
        return startServerVote(PluginId, voteKindId, initiatorSlot, argument, out errorMessage);
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
