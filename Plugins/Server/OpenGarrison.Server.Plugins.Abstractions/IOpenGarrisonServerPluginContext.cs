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
    /// Tries to set a player's velocity, keeping the current speed on any axis passed as null.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="velocityX">The horizontal velocity, or null to keep it.</param>
    /// <param name="velocityY">The vertical velocity, or null to keep it.</param>
    /// <returns>True when the velocity was set.</returns>
    bool TrySetGameplayVelocity(int playerId, float? velocityX, float? velocityY)
    {
        return false;
    }

    /// <summary>
    /// Tries to play a move pose on a player, one of its class presentation's pose suffixes.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="suffix">The pose suffix, or null to clear the pose.</param>
    /// <param name="ticksPerFrame">Source ticks each frame is shown.</param>
    /// <param name="loop">Whether the frames loop; otherwise the last frame holds.</param>
    /// <param name="durationTicks">How long the pose shows, in source ticks; 0 shows it until cleared.</param>
    /// <returns>True when the pose was set or cleared.</returns>
    bool TrySetGameplayPose(int playerId, string? suffix, int ticksPerFrame, bool loop, int durationTicks)
    {
        return false;
    }

    /// <summary>
    /// Tries to play a sound at a player's position for everyone nearby: a stock sound name, or
    /// one from a gameplay pack's sounds (clients without the plugin download it).
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="soundName">The sound name (at most 64 UTF-8 bytes).</param>
    /// <returns>True when the sound event was recorded.</returns>
    bool TryPlayGameplaySound(int playerId, string soundName)
    {
        return false;
    }

    /// <summary>
    /// Tries to spray blood from a player, the effect weapon hits make.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="directionDegrees">The direction the blood flies, in degrees (0 is right, 90 is up).</param>
    /// <param name="count">How much blood, 1 to 8.</param>
    /// <returns>True when the effect was recorded.</returns>
    bool TrySpawnGameplayBlood(int playerId, float directionDegrees, int count)
    {
        return false;
    }

    /// <summary>
    /// Tries to play a gameplay pack sprite once at a world position on every client, like a hit spark.
    /// </summary>
    /// <param name="spriteName">The sprite, from a loaded gameplay pack.</param>
    /// <param name="x">The world X coordinate of the sprite's origin.</param>
    /// <param name="y">The world Y coordinate of the sprite's origin.</param>
    /// <param name="rotationDegrees">The rotation in degrees.</param>
    /// <param name="flipX">True to mirror the sprite horizontally.</param>
    /// <param name="ticksPerFrame">Source ticks each frame shows, 1 to 30.</param>
    /// <returns>True when the effect was recorded.</returns>
    bool TrySpawnGameplaySpriteEffect(string spriteName, float x, float y, float rotationDegrees, bool flipX, int ticksPerFrame)
    {
        return false;
    }

    /// <summary>
    /// Tries to play a screen effect over every client's view, centred on a world position.
    /// </summary>
    /// <param name="effect">The screen effect id; currently <c>time_stop</c>.</param>
    /// <param name="x">The world X coordinate the effect starts from.</param>
    /// <param name="y">The world Y coordinate the effect starts from.</param>
    /// <param name="durationTicks">How long the effect lasts, in source ticks.</param>
    /// <returns>True when the effect was recorded.</returns>
    bool TryPlayGameplayScreenEffect(string effect, float x, float y, int durationTicks)
    {
        return false;
    }

    /// <summary>
    /// Tries to hold every projectile inside a circle still (no movement, lifetime or fuse) for a
    /// while, as a time stop does; they carry on as they were afterwards.
    /// </summary>
    /// <param name="x">The world X coordinate of the circle's centre.</param>
    /// <param name="y">The world Y coordinate of the circle's centre.</param>
    /// <param name="radius">The circle's radius in world units.</param>
    /// <param name="ticks">How long projectiles are held, in ticks.</param>
    /// <returns>True when the freeze was applied.</returns>
    bool TryFreezeGameplayProjectiles(float x, float y, float radius, int ticks)
    {
        return false;
    }

    /// <summary>
    /// Tries to hold every projectile inside a circle still, as above, letting blades thrown inside
    /// it fly travelPixels first (so knives leave the hand before they stop).
    /// </summary>
    /// <param name="x">The world X coordinate of the circle's centre.</param>
    /// <param name="y">The world Y coordinate of the circle's centre.</param>
    /// <param name="radius">The circle's radius in world units.</param>
    /// <param name="ticks">How long projectiles are held, in ticks.</param>
    /// <param name="travelPixels">How far a blade thrown inside it flies before it is held.</param>
    /// <returns>True when the freeze was applied.</returns>
    bool TryFreezeGameplayProjectiles(float x, float y, float radius, int ticks, float travelPixels)
    {
        return TryFreezeGameplayProjectiles(x, y, radius, ticks);
    }

    /// <summary>
    /// Tries to move a player straight toward a world position at once, stopping short where
    /// walls, gates or player walls would block them.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="x">The world X coordinate to move them toward.</param>
    /// <param name="y">The world Y coordinate to move them toward.</param>
    /// <returns>True when the player moved.</returns>
    bool TryTeleportGameplayPlayer(int playerId, float x, float y)
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
    /// Tries to apply gameplay damage to a player, choosing how a kill leaves the body.
    /// </summary>
    /// <param name="targetPlayerId">The target player id.</param>
    /// <param name="amount">The damage amount.</param>
    /// <param name="attackerPlayerId">The attacking player id, if any.</param>
    /// <param name="weaponSpriteName">The weapon sprite name, if any.</param>
    /// <param name="deathType">"gib", "decapitated", "bisected", "severe", "rifle", or null for the usual corpse.</param>
    /// <returns>True when the damage was applied.</returns>
    bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId, string? weaponSpriteName, string? deathType)
    {
        return TryApplyGameplayDamage(targetPlayerId, amount, attackerPlayerId, weaponSpriteName);
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
    /// Tries to apply gameplay healing to a player, optionally silently: no healing particles or
    /// numbers (a passive regen).
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="amount">The heal amount.</param>
    /// <param name="silent">True to heal without the healing feedback.</param>
    /// <returns>True when the healing was applied.</returns>
    bool TryApplyGameplayHealing(int playerId, float amount, bool silent)
    {
        return TryApplyGameplayHealing(playerId, amount);
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
    /// Tries to register a plugin class on the neutral Custom player class slot.
    /// </summary>
    /// <param name="registration">The class registration, including its default loadout.</param>
    /// <param name="errorMessage">The error message when registration fails.</param>
    /// <returns>True when the class was registered.</returns>
    bool TryRegisterGameplayClass(GameplayClassRegistration registration, out string errorMessage)
    {
        errorMessage = "Gameplay class registration is not supported by this host.";
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
