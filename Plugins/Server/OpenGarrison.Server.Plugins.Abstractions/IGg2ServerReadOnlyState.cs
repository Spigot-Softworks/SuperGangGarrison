using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerReadOnlyState
{
    string ServerName { get; }

    string LevelName { get; }

    int MapAreaIndex { get; }

    int MapAreaCount { get; }

    float MapScale { get; }

    GameModeKind GameMode { get; }

    MatchPhase MatchPhase { get; }

    int RedCaps { get; }

    int BlueCaps { get; }

    IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers();

    OpenGarrisonServerObjectiveStateInfo GetObjectives()
        => new(Array.Empty<OpenGarrisonServerControlPointInfo>(), Array.Empty<OpenGarrisonServerGeneratorInfo>(), Array.Empty<OpenGarrisonServerIntelligenceInfo>());

    IReadOnlyList<OpenGarrisonServerBuildableInfo> GetBuildables()
        => Array.Empty<OpenGarrisonServerBuildableInfo>();

    IReadOnlyList<OpenGarrisonServerProjectileInfo> GetProjectiles()
        => Array.Empty<OpenGarrisonServerProjectileInfo>();

    IReadOnlyList<OpenGarrisonServerRecentEventInfo> GetRecentEvents()
        => Array.Empty<OpenGarrisonServerRecentEventInfo>();

    OpenGarrisonServerMapRegionInfo GetMapRegion(float centerX, float centerY, float radius, int limit = 128)
        => new(new OpenGarrisonServerMapBoundsInfo(0f, 0f), centerX, centerY, radius, Array.Empty<OpenGarrisonServerMapSolidInfo>(), Array.Empty<OpenGarrisonServerMapRoomObjectInfo>(), IsTruncated: false);

    OpenGarrisonServerVisibilityInfo HasLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam? team = null)
        => new(originX, originY, targetX, targetY, team, HasLineOfSight: true);

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

    IReadOnlyList<OpenGarrisonServerGameplayModPackInfo> GetGameplayModPacks();

    IReadOnlyList<OpenGarrisonServerGameplayClassInfo> GetGameplayClasses(string? modPackId = null);

    IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetGameplayItems(string? modPackId = null);

    IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetGameplayAbilities();

    IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetPlayerGameplayAbilities(int playerId);

    bool TryGetPlayerGameplayAbility(int playerId, string category, out OpenGarrisonServerGameplayAbilityInfo ability);

    IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetOwnedGameplayItems(byte slot);

    IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetGameplayLoadoutsForClass(string classId);

    IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplaySecondaryItems(byte slot);

    IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplayAcquiredItems(byte slot);

    IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetAvailableGameplayLoadouts(byte slot);

    bool TryGetPlayerReplicatedStateInt(byte slot, string ownerPluginId, string stateKey, out int value);

    bool TryGetPlayerReplicatedStateFloat(byte slot, string ownerPluginId, string stateKey, out float value);

    bool TryGetPlayerReplicatedStateBool(byte slot, string ownerPluginId, string stateKey, out bool value);

    bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value);

    bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value);

    bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value);
}
