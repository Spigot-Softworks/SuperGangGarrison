using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Services and state exposed to a server plugin by the plugin host.
/// </summary>
public interface IOpenGarrisonServerPluginContext : IOpenGarrisonPluginHostContext
{
    /// <summary>
    /// Gets the directory server maps are loaded from.
    /// </summary>
    string MapsDirectory { get; }

    /// <summary>
    /// Gets read-only server state such as the match, players, and objectives.
    /// </summary>
    IOpenGarrisonServerReadOnlyState ServerState { get; }

    /// <summary>
    /// Gets the admin operations available to the plugin (kick, ban, bots, and so on).
    /// </summary>
    IOpenGarrisonServerAdminOperations AdminOperations { get; }

    /// <summary>
    /// Gets the server cvar registry.
    /// </summary>
    IOpenGarrisonServerCvarRegistry Cvars { get; }

    /// <summary>
    /// Gets the server task scheduler.
    /// </summary>
    IOpenGarrisonServerScheduler Scheduler { get; }

    /// <summary>
    /// Sends a plugin message to a client-side plugin on the given slot.
    /// </summary>
    /// <param name="slot">The target client slot.</param>
    /// <param name="targetPluginId">The client plugin identifier that should receive the message.</param>
    /// <param name="messageType">The message type understood by the receiving plugin.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="payloadFormat">The payload format (text or JSON).</param>
    /// <param name="schemaVersion">The message schema version.</param>
    void SendMessageToClient(
        byte slot,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion);

    /// <summary>
    /// Broadcasts a plugin message to the matching client-side plugin on all clients.
    /// </summary>
    /// <param name="targetPluginId">The client plugin identifier that should receive the message.</param>
    /// <param name="messageType">The message type understood by the receiving plugin.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="payloadFormat">The payload format (text or JSON).</param>
    /// <param name="schemaVersion">The message schema version.</param>
    void BroadcastMessageToClients(
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion);

    /// <summary>
    /// Sends a text plugin message to a client-side plugin on the given slot using schema version 1.
    /// </summary>
    /// <param name="slot">The target client slot.</param>
    /// <param name="targetPluginId">The client plugin identifier that should receive the message.</param>
    /// <param name="messageType">The message type understood by the receiving plugin.</param>
    /// <param name="payload">The text message payload.</param>
    void SendMessageToClient(byte slot, string targetPluginId, string messageType, string payload)
    {
        SendMessageToClient(slot, targetPluginId, messageType, payload, PluginMessagePayloadFormat.Text, schemaVersion: 1);
    }

    /// <summary>
    /// Broadcasts a text plugin message to the matching client-side plugin on all clients using schema version 1.
    /// </summary>
    /// <param name="targetPluginId">The client plugin identifier that should receive the message.</param>
    /// <param name="messageType">The message type understood by the receiving plugin.</param>
    /// <param name="payload">The text message payload.</param>
    void BroadcastMessageToClients(string targetPluginId, string messageType, string payload)
    {
        BroadcastMessageToClients(targetPluginId, messageType, payload, PluginMessagePayloadFormat.Text, schemaVersion: 1);
    }

    /// <summary>
    /// Sets an integer replicated state value for the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value was set.</returns>
    bool SetPlayerReplicatedStateInt(byte slot, string stateKey, int value);

    /// <summary>
    /// Sets a float replicated state value for the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value was set.</returns>
    bool SetPlayerReplicatedStateFloat(byte slot, string stateKey, float value);

    /// <summary>
    /// Sets a boolean replicated state value for the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value was set.</returns>
    bool SetPlayerReplicatedStateBool(byte slot, string stateKey, bool value);

    /// <summary>
    /// Clears a replicated state value for the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="stateKey">The state key.</param>
    /// <returns>True when the value was cleared.</returns>
    bool ClearPlayerReplicatedState(byte slot, string stateKey);

    /// <summary>
    /// Tries to apply a gameplay velocity impulse to a player.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="velocityX">The horizontal velocity.</param>
    /// <param name="velocityY">The vertical velocity.</param>
    /// <returns>True when the impulse was applied.</returns>
    bool TryApplyGameplayImpulse(int playerId, float velocityX, float velocityY)
    {
        return false;
    }

    /// <summary>
    /// Tries to set a gameplay ability cooldown for a player.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="cooldownKey">The cooldown key.</param>
    /// <param name="ticks">The cooldown in ticks.</param>
    /// <returns>True when the cooldown was set.</returns>
    bool TrySetGameplayAbilityCooldown(int playerId, string cooldownKey, int ticks)
    {
        return false;
    }

    /// <summary>
    /// Tries to apply gameplay damage to a player.
    /// </summary>
    /// <param name="targetPlayerId">The target player id.</param>
    /// <param name="amount">The damage amount.</param>
    /// <param name="attackerPlayerId">The attacking player id, if any.</param>
    /// <param name="weaponSpriteName">The weapon sprite name, if any.</param>
    /// <returns>True when the damage was applied.</returns>
    bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId = null, string? weaponSpriteName = null)
    {
        return false;
    }

    /// <summary>
    /// Tries to apply gameplay healing to a player.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="amount">The heal amount.</param>
    /// <returns>True when the healing was applied.</returns>
    bool TryApplyGameplayHealing(int playerId, float amount)
    {
        return false;
    }

    /// <summary>
    /// Tries to apply a gameplay status effect to a player.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="statusEffectId">The status effect id.</param>
    /// <param name="ticks">The duration in ticks.</param>
    /// <param name="value">The effect value.</param>
    /// <returns>True when the status effect was applied.</returns>
    bool TryApplyGameplayStatusEffect(int playerId, string statusEffectId, int ticks, float value = 0f)
    {
        return false;
    }

    /// <summary>
    /// Tries to spawn a gameplay projectile.
    /// </summary>
    /// <param name="request">The projectile spawn request.</param>
    /// <param name="projectileId">The spawned projectile id.</param>
    /// <returns>True when the projectile was spawned.</returns>
    bool TrySpawnGameplayProjectile(GameplayProjectileSpawnRequest request, out int projectileId)
    {
        projectileId = 0;
        return false;
    }

    /// <summary>
    /// Tries to register a gameplay ability with the host.
    /// </summary>
    /// <param name="registration">The ability registration.</param>
    /// <param name="errorMessage">The error message when registration fails.</param>
    /// <returns>True when the ability was registered.</returns>
    bool TryRegisterGameplayAbility(GameplayAbilityRegistration registration, out string errorMessage);

    /// <summary>
    /// Tries to override a registered gameplay ability with a patch.
    /// </summary>
    /// <param name="itemId">The ability item id.</param>
    /// <param name="patch">The ability patch.</param>
    /// <param name="errorMessage">The error message when the override fails.</param>
    /// <returns>True when the ability was overridden.</returns>
    bool TryOverrideGameplayAbility(string itemId, GameplayAbilityPatch patch, out string errorMessage);

    /// <summary>
    /// Tries to register a gameplay ability executor with the host.
    /// </summary>
    /// <param name="executorId">The executor id.</param>
    /// <param name="executor">The executor.</param>
    /// <param name="errorMessage">The error message when registration fails.</param>
    /// <returns>True when the executor was registered.</returns>
    bool TryRegisterGameplayAbilityExecutor(string executorId, IGameplayAbilityExecutor executor, out string errorMessage)
    {
        errorMessage = "Gameplay ability executor registration is not supported by this host.";
        return false;
    }

    /// <summary>
    /// Tries to register a primary weapon behavior with the host.
    /// </summary>
    /// <param name="behaviorId">The behavior id.</param>
    /// <param name="executor">The weapon executor.</param>
    /// <param name="fireSoundName">The fire sound name, if any.</param>
    /// <param name="errorMessage">The error message when registration fails.</param>
    /// <returns>True when the behavior was registered.</returns>
    bool TryRegisterGameplayPrimaryWeaponBehavior(string behaviorId, IGameplayPrimaryWeaponExecutor executor, string? fireSoundName, out string errorMessage)
    {
        errorMessage = "Gameplay primary weapon behavior registration is not supported by this host.";
        return false;
    }

    /// <summary>
    /// Tries to register a gameplay weapon item with the host.
    /// </summary>
    /// <param name="registration">The weapon item registration.</param>
    /// <param name="errorMessage">The error message when registration fails.</param>
    /// <returns>True when the weapon item was registered.</returns>
    bool TryRegisterGameplayWeaponItem(GameplayWeaponItemRegistration registration, out string errorMessage)
    {
        errorMessage = "Gameplay weapon item registration is not supported by this host.";
        return false;
    }

    /// <summary>
    /// Tries to register a gameplay loadout with the host.
    /// </summary>
    /// <param name="registration">The loadout registration.</param>
    /// <param name="errorMessage">The error message when registration fails.</param>
    /// <returns>True when the loadout was registered.</returns>
    bool TryRegisterGameplayLoadout(GameplayLoadoutRegistration registration, out string errorMessage)
    {
        errorMessage = "Gameplay loadout registration is not supported by this host.";
        return false;
    }

    /// <summary>
    /// Tries to register a gameplay slot item with the host.
    /// </summary>
    /// <param name="registration">The slot item registration.</param>
    /// <param name="errorMessage">The error message when registration fails.</param>
    /// <returns>True when the slot item was registered.</returns>
    bool TryRegisterGameplaySlotItem(GameplaySlotItemRegistration registration, out string errorMessage)
    {
        errorMessage = "Gameplay slot item registration is not supported by this host.";
        return false;
    }

    /// <summary>
    /// Tries to register a native vote kind with the host.
    /// </summary>
    /// <param name="registration">The vote registration.</param>
    /// <param name="errorMessage">The error message when registration fails.</param>
    /// <returns>True when the vote kind was registered.</returns>
    bool TryRegisterVoteKind(OpenGarrisonServerVoteRegistration registration, out string errorMessage)
    {
        errorMessage = "Native vote registration is not supported by this host.";
        return false;
    }

    /// <summary>
    /// Tries to start a native vote.
    /// </summary>
    /// <param name="voteKindId">The vote kind id.</param>
    /// <param name="initiatorSlot">The initiator's slot.</param>
    /// <param name="argument">The vote argument.</param>
    /// <param name="errorMessage">The error message when the vote cannot start.</param>
    /// <returns>True when the vote was started.</returns>
    bool TryStartVote(string voteKindId, byte initiatorSlot, string argument, out string errorMessage)
    {
        errorMessage = "Native voting is not supported by this host.";
        return false;
    }

    /// <summary>
    /// Registers a server command requiring the given permissions.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="requiredPermissions">The permissions required to run it.</param>
    void RegisterCommand(IOpenGarrisonServerCommand command, OpenGarrisonServerAdminPermissions requiredPermissions);

    /// <summary>
    /// Registers a server command with aliases, requiring the given permissions.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="requiredPermissions">The permissions required to run it.</param>
    /// <param name="aliases">The command aliases.</param>
    void RegisterCommand(IOpenGarrisonServerCommand command, OpenGarrisonServerAdminPermissions requiredPermissions, IReadOnlyList<string> aliases)
    {
        RegisterCommand(command, requiredPermissions);
    }

    /// <summary>
    /// Registers a server command with no required permissions.
    /// </summary>
    /// <param name="command">The command.</param>
    void RegisterCommand(IOpenGarrisonServerCommand command)
    {
        RegisterCommand(command, OpenGarrisonServerAdminPermissions.None);
    }
}
