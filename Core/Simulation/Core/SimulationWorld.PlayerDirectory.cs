namespace OpenGarrison.Core;

// The world's own player directory: the lookups behind ISimulationPlayerDirectory.
public sealed partial class SimulationWorld
{
    private bool IsNetworkPlayerActive(byte slot)
    {
        return NetworkPlayers.IsNetworkPlayerEnabled(slot);
    }

    private PlayerTeam GetNetworkPlayerTeam(byte slot)
    {
        return NetworkPlayers.TryGetNetworkPlayer(slot, out var player)
            ? player.Team
            : NetworkPlayers.GetNetworkPlayerConfiguredTeam(slot);
    }

    private PlayerEntity? FindPlayerById(int playerId)
    {
        if (PlayerRegistry.ActivePlayersById.TryGetValue(playerId, out var player))
        {
            return player;
        }

        if (EnemyPlayerEnabled && EnemyPlayer.Id == playerId)
        {
            return EnemyPlayer;
        }

        if (FriendlyDummyEnabled && FriendlyDummy.Id == playerId)
        {
            return FriendlyDummy;
        }

        return null;
    }

    // Includes debug dummy players when enabled.
    private IEnumerable<PlayerEntity> EnumerateSimulatedPlayers()
    {
        foreach (var slot in NetworkPlayers.EnumerateEnabledNetworkPlayerSlots())
        {
            if (!NetworkPlayers.IsNetworkPlayerEnabled(slot) || !NetworkPlayers.TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            yield return player;
        }

        if (EnemyPlayerEnabled)
        {
            yield return EnemyPlayer;
        }

        if (FriendlyDummyEnabled)
        {
            yield return FriendlyDummy;
        }
    }
}
