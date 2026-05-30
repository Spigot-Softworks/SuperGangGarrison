using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private static bool TryResolveSnapshotLocalPlayerState(
        SnapshotMessage snapshot,
        byte localPlayerSlot,
        out SnapshotPlayerState? localPlayerState,
        out bool isSpectatorSnapshot)
    {
        localPlayerState = snapshot.Players.FirstOrDefault(player => player.Slot == localPlayerSlot);
        isSpectatorSnapshot = localPlayerState?.IsSpectator ?? !IsPlayableNetworkPlayerSlot(localPlayerSlot);
        return localPlayerState is not null || isSpectatorSnapshot;
    }

    private void ApplySnapshotPlayerState(
        SnapshotMessage snapshot,
        byte localPlayerSlot,
        SnapshotPlayerState? localPlayerState,
        bool isSpectatorSnapshot)
    {
        ApplySnapshotLocalPlayerState(localPlayerState);
        ApplySnapshotRemotePlayerState(snapshot.Players, localPlayerSlot, localPlayerState, isSpectatorSnapshot);
    }

    private void ApplySnapshotLocalPlayerState(SnapshotPlayerState? localPlayerState)
    {
        if (localPlayerState is not null && !localPlayerState.IsSpectator)
        {
            var wasAlive = LocalPlayer.IsAlive;
            var previousGibDeaths = LocalPlayer.GibDeaths;

            SynchronizeNetworkGibDeathPresentationCount(LocalPlayer.Id, localPlayerState.GibDeaths);
            ApplySnapshotPlayer(LocalPlayer, localPlayerState);
            var diedThisSnapshot = wasAlive && !LocalPlayer.IsAlive;
            var wasGibbedDeath = localPlayerState.GibDeaths > previousGibDeaths;
            if (diedThisSnapshot
                && wasGibbedDeath
                && TryMarkNetworkGibDeathPresented(LocalPlayer.Id, localPlayerState.GibDeaths))
            {
                SpawnClientPlayerGibsFromNetworkDeath(LocalPlayer);
            }

            TrySetNetworkPlayerAwaitingJoin(LocalPlayerSlot, localPlayerState.IsAwaitingJoin);
            TrySetNetworkPlayerRespawnTicks(LocalPlayerSlot, localPlayerState.RespawnTicks);
            TrySetNetworkPlayerConfiguredTeam(LocalPlayerSlot, LocalPlayer.Team);
            return;
        }

        TrySetNetworkPlayerAwaitingJoin(LocalPlayerSlot, true);
        TrySetNetworkPlayerRespawnTicks(LocalPlayerSlot, 0);
        LocalDeathCam = null;
        LocalPlayer.ClearMedicHealingTarget();
        LocalPlayer.Kill();
    }

    private void ApplySnapshotRemotePlayerState(
        IReadOnlyList<SnapshotPlayerState> players,
        byte localPlayerSlot,
        SnapshotPlayerState? localPlayerState,
        bool isSpectatorSnapshot)
    {
        var remotePlayerStates = players
            .Where(player => !player.IsSpectator)
            .Where(player => IsPlayableNetworkPlayerSlot(player.Slot))
            .Where(player => isSpectatorSnapshot || player.Slot != localPlayerSlot)
            .OrderBy(player => player.Slot)
            .ToList();

        EnemyPlayerEnabled = false;
        _enemyDummyRespawnTicks = 0;
        ClearEnemyInputOverride();
        EnemyPlayer.Kill();
        FriendlyDummyEnabled = false;
        FriendlyDummy.Kill();
        SyncRemoteSnapshotPlayers(remotePlayerStates);

        if (localPlayerState is not null && !localPlayerState.IsSpectator)
        {
            TrySetNetworkPlayerConfiguredTeam(LocalPlayerSlot, LocalPlayer.Team);
        }
    }
}
