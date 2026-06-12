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
            var appliedLocalPlayerState = NormalizeAwaitingJoinSnapshotPlayerState(localPlayerState);
            _authoritativeLocalPlayerId = localPlayerState.PlayerId;
            var wasAlive = LocalPlayer.IsAlive;
            var previousGibDeaths = LocalPlayer.GibDeaths;

            SynchronizeNetworkGibDeathPresentationCount(LocalPlayer.Id, localPlayerState.GibDeaths);
            ApplySnapshotPlayer(LocalPlayer, appliedLocalPlayerState);
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
            ApplySnapshotNetworkPlayerPingMilliseconds(LocalPlayerSlot, localPlayerState.PingMilliseconds);
            TrySetNetworkPlayerConfiguredTeam(LocalPlayerSlot, LocalPlayer.Team);
            return;
        }

        TrySetNetworkPlayerAwaitingJoin(LocalPlayerSlot, true);
        TrySetNetworkPlayerRespawnTicks(LocalPlayerSlot, 0);
        ApplySnapshotNetworkPlayerPingMilliseconds(LocalPlayerSlot, -1);
        _authoritativeLocalPlayerId = null;
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

    private static SnapshotPlayerState NormalizeAwaitingJoinSnapshotPlayerState(SnapshotPlayerState snapshotPlayer)
    {
        return snapshotPlayer.IsAwaitingJoin && snapshotPlayer.IsAlive
            ? snapshotPlayer with
            {
                IsAlive = false,
                Health = 0,
                HorizontalSpeed = 0f,
                VerticalSpeed = 0f,
                IsMedicHealing = false,
                MedicHealTargetId = -1,
                IsCarryingIntel = false,
            }
            : snapshotPlayer;
    }
}
