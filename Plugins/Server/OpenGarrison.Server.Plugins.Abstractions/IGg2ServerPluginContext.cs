using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerPluginContext : IOpenGarrisonPluginHostContext
{
    string MapsDirectory { get; }

    IOpenGarrisonServerReadOnlyState ServerState { get; }

    IOpenGarrisonServerAdminOperations AdminOperations { get; }

    IOpenGarrisonServerCvarRegistry Cvars { get; }

    IOpenGarrisonServerScheduler Scheduler { get; }

    void SendMessageToClient(
        byte slot,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion);

    void BroadcastMessageToClients(
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion);

    void SendMessageToClient(byte slot, string targetPluginId, string messageType, string payload)
    {
        SendMessageToClient(slot, targetPluginId, messageType, payload, PluginMessagePayloadFormat.Text, schemaVersion: 1);
    }

    void BroadcastMessageToClients(string targetPluginId, string messageType, string payload)
    {
        BroadcastMessageToClients(targetPluginId, messageType, payload, PluginMessagePayloadFormat.Text, schemaVersion: 1);
    }

    bool SetPlayerReplicatedStateInt(byte slot, string stateKey, int value);

    bool SetPlayerReplicatedStateFloat(byte slot, string stateKey, float value);

    bool SetPlayerReplicatedStateBool(byte slot, string stateKey, bool value);

    bool ClearPlayerReplicatedState(byte slot, string stateKey);

    bool TryApplyGameplayImpulse(int playerId, float velocityX, float velocityY)
    {
        return false;
    }

    bool TrySetGameplayAbilityCooldown(int playerId, string cooldownKey, int ticks)
    {
        return false;
    }

    bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId = null, string? weaponSpriteName = null)
    {
        return false;
    }

    bool TryApplyGameplayHealing(int playerId, float amount)
    {
        return false;
    }

    bool TryApplyGameplayStatusEffect(int playerId, string statusEffectId, int ticks, float value = 0f)
    {
        return false;
    }

    bool TrySpawnGameplayProjectile(GameplayProjectileSpawnRequest request, out int projectileId)
    {
        projectileId = 0;
        return false;
    }

    bool TryRegisterGameplayAbility(GameplayAbilityRegistration registration, out string errorMessage);

    bool TryOverrideGameplayAbility(string itemId, GameplayAbilityPatch patch, out string errorMessage);

    bool TryRegisterGameplayAbilityExecutor(string executorId, IGameplayAbilityExecutor executor, out string errorMessage)
    {
        errorMessage = "Gameplay ability executor registration is not supported by this host.";
        return false;
    }

    bool TryRegisterGameplayPrimaryWeaponBehavior(string behaviorId, IGameplayPrimaryWeaponExecutor executor, string? fireSoundName, out string errorMessage)
    {
        errorMessage = "Gameplay primary weapon behavior registration is not supported by this host.";
        return false;
    }

    bool TryRegisterGameplayWeaponItem(GameplayWeaponItemRegistration registration, out string errorMessage)
    {
        errorMessage = "Gameplay weapon item registration is not supported by this host.";
        return false;
    }

    bool TryRegisterGameplayLoadout(GameplayLoadoutRegistration registration, out string errorMessage)
    {
        errorMessage = "Gameplay loadout registration is not supported by this host.";
        return false;
    }

    bool TryRegisterGameplaySlotItem(GameplaySlotItemRegistration registration, out string errorMessage)
    {
        errorMessage = "Gameplay slot item registration is not supported by this host.";
        return false;
    }

    void RegisterCommand(IOpenGarrisonServerCommand command, OpenGarrisonServerAdminPermissions requiredPermissions);

    void RegisterCommand(IOpenGarrisonServerCommand command, OpenGarrisonServerAdminPermissions requiredPermissions, IReadOnlyList<string> aliases)
    {
        RegisterCommand(command, requiredPermissions);
    }

    void RegisterCommand(IOpenGarrisonServerCommand command)
    {
        RegisterCommand(command, OpenGarrisonServerAdminPermissions.None);
    }
}
