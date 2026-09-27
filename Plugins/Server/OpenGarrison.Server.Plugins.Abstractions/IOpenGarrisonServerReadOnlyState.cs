using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Read-only snapshot of server state exposed to plugins.
/// </summary>
public interface IOpenGarrisonServerReadOnlyState
{
    /// <summary>Gets the server name.</summary>
    string ServerName { get; }

    /// <summary>Gets the current level name.</summary>
    string LevelName { get; }

    /// <summary>Gets the current map area index.</summary>
    int MapAreaIndex { get; }

    /// <summary>Gets the number of map areas.</summary>
    int MapAreaCount { get; }

    /// <summary>Gets the map scale.</summary>
    float MapScale { get; }

    /// <summary>Gets the current game mode.</summary>
    GameModeKind GameMode { get; }

    /// <summary>Gets the current match phase.</summary>
    MatchPhase MatchPhase { get; }

    /// <summary>Gets the red team caps.</summary>
    int RedCaps { get; }

    /// <summary>Gets the blue team caps.</summary>
    int BlueCaps { get; }

    /// <summary>
    /// Gets snapshots of all players on the server.
    /// </summary>
    /// <returns>The player snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers();

    /// <summary>
    /// Gets the current objective state (control points, generators, intelligence).
    /// </summary>
    /// <returns>The objective state.</returns>
    OpenGarrisonServerObjectiveStateInfo GetObjectives()
        => new(Array.Empty<OpenGarrisonServerControlPointInfo>(), Array.Empty<OpenGarrisonServerGeneratorInfo>(), Array.Empty<OpenGarrisonServerIntelligenceInfo>());

    /// <summary>
    /// Gets snapshots of all buildables (sentries, dispensers, and so on).
    /// </summary>
    /// <returns>The buildable snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerBuildableInfo> GetBuildables()
        => Array.Empty<OpenGarrisonServerBuildableInfo>();

    /// <summary>
    /// Gets snapshots of all active projectiles.
    /// </summary>
    /// <returns>The projectile snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerProjectileInfo> GetProjectiles()
        => Array.Empty<OpenGarrisonServerProjectileInfo>();

    /// <summary>
    /// Gets snapshots of recent gameplay events.
    /// </summary>
    /// <returns>The recent events.</returns>
    IReadOnlyList<OpenGarrisonServerRecentEventInfo> GetRecentEvents()
        => Array.Empty<OpenGarrisonServerRecentEventInfo>();

    /// <summary>
    /// Gets map geometry (bounds, solids, room objects) around a center point.
    /// </summary>
    /// <param name="centerX">The region center X.</param>
    /// <param name="centerY">The region center Y.</param>
    /// <param name="radius">The region radius.</param>
    /// <param name="limit">The maximum number of entries per collection.</param>
    /// <returns>The map region.</returns>
    OpenGarrisonServerMapRegionInfo GetMapRegion(float centerX, float centerY, float radius, int limit = 128)
        => new(new OpenGarrisonServerMapBoundsInfo(0f, 0f), centerX, centerY, radius, Array.Empty<OpenGarrisonServerMapSolidInfo>(), Array.Empty<OpenGarrisonServerMapRoomObjectInfo>(), IsTruncated: false);

    /// <summary>
    /// Tests line of sight between two world points.
    /// </summary>
    /// <param name="originX">The origin X.</param>
    /// <param name="originY">The origin Y.</param>
    /// <param name="targetX">The target X.</param>
    /// <param name="targetY">The target Y.</param>
    /// <param name="team">The viewing team, if any.</param>
    /// <returns>The line-of-sight result.</returns>
    OpenGarrisonServerVisibilityInfo HasLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam? team = null)
        => new(originX, originY, targetX, targetY, team, HasLineOfSight: true);

    /// <summary>
    /// Gets a summary of the current match state.
    /// </summary>
    /// <returns>The match state summary.</returns>
    OpenGarrisonServerMatchStateInfo GetMatchState()
    {
        var players = GetPlayers();
        var spectatorCount = players.Count(player => player.IsSpectator);
        return new OpenGarrisonServerMatchStateInfo(
            ServerName,
            LevelName,
            MapAreaIndex,
            MapAreaCount,
            MapScale,
            GameMode,
            MatchPhase,
            RedCaps,
            BlueCaps,
            players.Count,
            players.Count - spectatorCount,
            spectatorCount);
    }

    /// <summary>
    /// Tries to get a player's snapshot by slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="player">The player snapshot.</param>
    /// <returns>True when a player occupies the slot.</returns>
    bool TryGetPlayerStateBySlot(byte slot, out OpenGarrisonServerPlayerInfo player)
    {
        foreach (var candidate in GetPlayers())
        {
            if (candidate.Slot == slot)
            {
                player = candidate;
                return true;
            }
        }

        player = default;
        return false;
    }

    /// <summary>
    /// Tries to get a player's snapshot by player id.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="player">The player snapshot.</param>
    /// <returns>True when the player exists.</returns>
    bool TryGetPlayerStateByPlayerId(int playerId, out OpenGarrisonServerPlayerInfo player)
    {
        foreach (var candidate in GetPlayers())
        {
            if (candidate.PlayerId == playerId)
            {
                player = candidate;
                return true;
            }
        }

        player = default;
        return false;
    }

    /// <summary>
    /// Gets the registered gameplay mod packs.
    /// </summary>
    /// <returns>The mod pack snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerGameplayModPackInfo> GetGameplayModPacks();

    /// <summary>
    /// Gets the gameplay classes, optionally filtered to a mod pack.
    /// </summary>
    /// <param name="modPackId">The mod pack id, or null for all.</param>
    /// <returns>The class snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerGameplayClassInfo> GetGameplayClasses(string? modPackId = null);

    /// <summary>
    /// Gets the gameplay items, optionally filtered to a mod pack.
    /// </summary>
    /// <param name="modPackId">The mod pack id, or null for all.</param>
    /// <returns>The item snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetGameplayItems(string? modPackId = null);

    /// <summary>
    /// Gets all gameplay abilities.
    /// </summary>
    /// <returns>The ability snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetGameplayAbilities();

    /// <summary>
    /// Gets the gameplay abilities available to a player.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <returns>The ability snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetPlayerGameplayAbilities(int playerId);

    /// <summary>
    /// Tries to get a player's gameplay ability in a category.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="category">The ability category.</param>
    /// <param name="ability">The ability snapshot.</param>
    /// <returns>True when the player has an ability in the category.</returns>
    bool TryGetPlayerGameplayAbility(int playerId, string category, out OpenGarrisonServerGameplayAbilityInfo ability);

    /// <summary>
    /// Gets the gameplay items owned by the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <returns>The owned item snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetOwnedGameplayItems(byte slot);

    /// <summary>
    /// Gets the gameplay loadouts available for a class.
    /// </summary>
    /// <param name="classId">The class id.</param>
    /// <returns>The loadout snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetGameplayLoadoutsForClass(string classId);

    /// <summary>
    /// Gets the secondary items available to the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <returns>The selectable item snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplaySecondaryItems(byte slot);

    /// <summary>
    /// Gets the acquired items available to the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <returns>The selectable item snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplayAcquiredItems(byte slot);

    /// <summary>
    /// Gets the loadouts available to the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <returns>The loadout snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetAvailableGameplayLoadouts(byte slot);

    /// <summary>
    /// Tries to get an integer replicated state value published by a plugin for the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="ownerPluginId">The plugin that published the state.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value exists.</returns>
    bool TryGetPlayerReplicatedStateInt(byte slot, string ownerPluginId, string stateKey, out int value);

    /// <summary>
    /// Tries to get a float replicated state value published by a plugin for the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="ownerPluginId">The plugin that published the state.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value exists.</returns>
    bool TryGetPlayerReplicatedStateFloat(byte slot, string ownerPluginId, string stateKey, out float value);

    /// <summary>
    /// Tries to get a boolean replicated state value published by a plugin for the player on a slot.
    /// </summary>
    /// <param name="slot">The player slot.</param>
    /// <param name="ownerPluginId">The plugin that published the state.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value exists.</returns>
    bool TryGetPlayerReplicatedStateBool(byte slot, string ownerPluginId, string stateKey, out bool value);

    /// <summary>
    /// Tries to get an integer replicated state value published by a plugin for a player id.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="ownerPluginId">The plugin that published the state.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value exists.</returns>
    bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value);

    /// <summary>
    /// Tries to get a float replicated state value published by a plugin for a player id.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="ownerPluginId">The plugin that published the state.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value exists.</returns>
    bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value);

    /// <summary>
    /// Tries to get a boolean replicated state value published by a plugin for a player id.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    /// <param name="ownerPluginId">The plugin that published the state.</param>
    /// <param name="stateKey">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>True when the value exists.</returns>
    bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value);
}
