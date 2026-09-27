using Microsoft.Xna.Framework;
#if !BROWSER_KNI
using Microsoft.Xna.Framework.Input;
#endif

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// Read-only snapshot of client state exposed to plugins.
/// </summary>
public interface IOpenGarrisonClientReadOnlyState
{
    /// <summary>
    /// Gets whether the client is connected to a server.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets whether the main menu is currently open.
    /// </summary>
    bool IsMainMenuOpen { get; }

    /// <summary>
    /// Gets whether gameplay is currently active.
    /// </summary>
    bool IsGameplayActive { get; }

    /// <summary>
    /// Gets whether gameplay input is currently blocked (for example by a menu).
    /// </summary>
    bool IsGameplayInputBlocked { get; }

    /// <summary>
    /// Gets whether the local player is spectating.
    /// </summary>
    bool IsSpectator { get; }

    /// <summary>
    /// Gets whether the death camera is active.
    /// </summary>
    bool IsDeathCamActive { get; }

    /// <summary>
    /// Gets the current world frame number.
    /// </summary>
    ulong WorldFrame { get; }

    /// <summary>
    /// Gets the simulation tick rate in ticks per second.
    /// </summary>
    int TickRate { get; }

    /// <summary>
    /// Gets the local player's ping in milliseconds.
    /// </summary>
    int LocalPingMilliseconds { get; }

    /// <summary>
    /// Gets the current level name.
    /// </summary>
    string LevelName { get; }

    /// <summary>
    /// Gets the level width in world units.
    /// </summary>
    float LevelWidth { get; }

    /// <summary>
    /// Gets the level height in world units.
    /// </summary>
    float LevelHeight { get; }

    /// <summary>
    /// Gets the viewport width in pixels.
    /// </summary>
    int ViewportWidth { get; }

    /// <summary>
    /// Gets the viewport height in pixels.
    /// </summary>
    int ViewportHeight { get; }

    /// <summary>
    /// Gets the local player id, or null when there is no local player.
    /// </summary>
    int? LocalPlayerId { get; }

    /// <summary>
    /// Gets the local player's team.
    /// </summary>
    ClientPluginTeam LocalPlayerTeam { get; }

    /// <summary>
    /// Gets the local player's class.
    /// </summary>
    ClientPluginClass LocalPlayerClass { get; }

    /// <summary>
    /// Gets whether the local player is alive.
    /// </summary>
    bool IsLocalPlayerAlive { get; }

    /// <summary>
    /// Gets whether the local player is scoped (zoomed aiming).
    /// </summary>
    bool IsLocalPlayerScoped { get; }

    /// <summary>
    /// Gets whether the local player is currently healing.
    /// </summary>
    bool IsLocalPlayerHealing { get; }

    /// <summary>
    /// Gets the sound effects volume scale (0 to 1).
    /// </summary>
    float SoundEffectsVolumeScale { get; }

    /// <summary>
    /// Gets the camera's top-left position in world coordinates.
    /// </summary>
    Vector2 CameraTopLeft { get; }

    /// <summary>
    /// Tries to get the local player's current and maximum health.
    /// </summary>
    /// <param name="health">The current health.</param>
    /// <param name="maxHealth">The maximum health.</param>
    /// <returns>True when health information is available.</returns>
    bool TryGetLocalPlayerHealth(out int health, out int maxHealth);

    /// <summary>
    /// Tries to get the local player's world position.
    /// </summary>
    /// <param name="position">The world position.</param>
    /// <returns>True when the position is available.</returns>
    bool TryGetLocalPlayerWorldPosition(out Vector2 position);

    /// <summary>
    /// Tries to get a player's world position.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="position">The world position.</param>
    /// <returns>True when the position is available.</returns>
    bool TryGetPlayerWorldPosition(int playerId, out Vector2 position);

    /// <summary>
    /// Gets whether a player is visible to the local viewer.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <returns>True when the player is visible to the local viewer.</returns>
    bool IsPlayerVisibleToLocalViewer(int playerId);

    /// <summary>
    /// Gets whether a player is cloaked.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <returns>True when the player is cloaked.</returns>
    bool IsPlayerCloaked(int playerId);

    /// <summary>
    /// Tries to get an integer replicated state value published by a plugin for a player.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="ownerPluginId">The plugin that published the state.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value exists.</returns>
    bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value);

    /// <summary>
    /// Tries to get a float replicated state value published by a plugin for a player.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="ownerPluginId">The plugin that published the state.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value exists.</returns>
    bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value);

    /// <summary>
    /// Tries to get a boolean replicated state value published by a plugin for a player.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="ownerPluginId">The plugin that published the state.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value exists.</returns>
    bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value);

#if !BROWSER_KNI
    /// <summary>
    /// Gets whether a key was pressed during this frame.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True when the key was pressed this frame.</returns>
    bool WasKeyPressedThisFrame(Keys key);
#endif

    /// <summary>
    /// Gets the gameplay item ids currently held by the local player.
    /// </summary>
    /// <returns>The local player's gameplay item ids.</returns>
    IReadOnlyList<string> GetLocalGameplayItemIds();

    /// <summary>
    /// Gets the gameplay ability item ids currently held by the local player.
    /// </summary>
    /// <returns>The local player's gameplay ability item ids.</returns>
    IReadOnlyList<string> GetLocalGameplayAbilityItemIds();

    /// <summary>
    /// Gets the current player markers (for example nameplates or indicators).
    /// </summary>
    /// <returns>The player markers.</returns>
    IReadOnlyList<ClientPlayerMarker> GetPlayerMarkers();

    /// <summary>
    /// Gets the current sentry markers.
    /// </summary>
    /// <returns>The sentry markers.</returns>
    IReadOnlyList<ClientSentryMarker> GetSentryMarkers();

    /// <summary>
    /// Gets the current objective markers.
    /// </summary>
    /// <returns>The objective markers.</returns>
    IReadOnlyList<ClientObjectiveMarker> GetObjectiveMarkers();
}
