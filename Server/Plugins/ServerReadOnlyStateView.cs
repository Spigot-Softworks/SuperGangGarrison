using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Server.Plugins;
using static ServerHelpers;

namespace OpenGarrison.Server;

internal sealed class ServerReadOnlyStateView(
    Func<string> serverNameGetter,
    Func<SimulationWorld> worldGetter,
    Func<IReadOnlyDictionary<byte, ClientSession>> clientsGetter) : IOpenGarrisonServerReadOnlyState
{
    public string ServerName => serverNameGetter();

    public string LevelName => worldGetter().Level.Name;

    public int MapAreaIndex => worldGetter().Level.MapAreaIndex;

    public int MapAreaCount => worldGetter().Level.MapAreaCount;

    public GameModeKind GameMode => worldGetter().MatchRules.Mode;

    public MatchPhase MatchPhase => worldGetter().MatchState.Phase;

    public int RedCaps => worldGetter().RedCaps;

    public int BlueCaps => worldGetter().BlueCaps;

    public IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers()
    {
        var world = worldGetter();
        return clientsGetter()
            .Values
            .OrderBy(client => client.Slot)
            .Select(client =>
            {
                var isSpectator = IsSpectatorSlot(client.Slot);
                PlayerTeam? team = null;
                PlayerClass? playerClass = null;
                PlayerEntity? player = null;
                if (!isSpectator && world.TryGetNetworkPlayer(client.Slot, out var networkPlayer))
                {
                    player = networkPlayer;
                    team = networkPlayer.Team;
                    playerClass = networkPlayer.ClassId;
                }

                return new OpenGarrisonServerPlayerInfo(
                    client.Slot,
                    client.Name,
                    isSpectator,
                    client.IsAuthorized,
                    team,
                    playerClass,
                    client.EndPoint.ToString(),
                    player?.GameplayLoadoutState.LoadoutId ?? string.Empty,
                    player?.GameplayLoadoutState.EquippedSlot ?? GameplayEquipmentSlot.Primary,
                    player?.GameplayLoadoutState.EquippedItemId ?? string.Empty);
            })
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetAvailableGameplayLoadouts(byte slot)
    {
        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return Array.Empty<OpenGarrisonServerGameplayLoadoutInfo>();
        }

        var gameplayClass = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(player.ClassId);
        return gameplayClass.Loadouts
            .Values
            .OrderBy(loadout => loadout.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(loadout => loadout.Id, StringComparer.Ordinal)
            .Select(loadout => new OpenGarrisonServerGameplayLoadoutInfo(
                loadout.Id,
                loadout.DisplayName,
                loadout.PrimaryItemId,
                loadout.SecondaryItemId,
                loadout.UtilityItemId,
                string.Equals(loadout.Id, player.GameplayLoadoutState.LoadoutId, StringComparison.Ordinal)))
            .ToArray();
    }

    public bool TryGetPlayerReplicatedStateInt(byte slot, string ownerPluginId, string stateKey, out int value)
    {
        if (worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            return player.TryGetReplicatedStateInt(ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    public bool TryGetPlayerReplicatedStateFloat(byte slot, string ownerPluginId, string stateKey, out float value)
    {
        if (worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            return player.TryGetReplicatedStateFloat(ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    public bool TryGetPlayerReplicatedStateBool(byte slot, string ownerPluginId, string stateKey, out bool value)
    {
        if (worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            return player.TryGetReplicatedStateBool(ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }
}
