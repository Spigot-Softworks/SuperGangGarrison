using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class SnapshotApplySystem
{
    private static bool TryResolveSnapshotLocalPlayerState(
        SnapshotMessage snapshot,
        byte localPlayerSlot,
        out SnapshotPlayerState? localPlayerState,
        out bool isSpectatorSnapshot)
    {
        localPlayerState = snapshot.Players.FirstOrDefault(player => player.Slot == localPlayerSlot);
        isSpectatorSnapshot = localPlayerState?.IsSpectator ?? !NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(localPlayerSlot);
        return localPlayerState is not null || isSpectatorSnapshot;
    }

    private void ApplySnapshotPlayerState(
        SnapshotMessage snapshot,
        byte localPlayerSlot,
        SnapshotPlayerState? localPlayerState,
        bool isSpectatorSnapshot)
    {
        ApplySnapshotLocalPlayerState(localPlayerState);
        ApplySnapshotRemotePlayerState(
            snapshot.Players,
            snapshot.ScoreboardPlayers.Count == 0 ? snapshot.Players : snapshot.ScoreboardPlayers,
            localPlayerSlot,
            localPlayerState,
            isSpectatorSnapshot);
    }

    private void ApplySnapshotLocalPlayerState(SnapshotPlayerState? localPlayerState)
    {
        if (localPlayerState is not null && !localPlayerState.IsSpectator)
        {
            var appliedLocalPlayerState = NormalizeAwaitingJoinSnapshotPlayerState(localPlayerState);
            _host.ClientSnapshots.AuthoritativeLocalPlayerId = localPlayerState.PlayerId;
            var wasAlive = _host.LocalPlayer.IsAlive;
            var previousGibDeaths = _host.LocalPlayer.GibDeaths;

            SynchronizeNetworkGibDeathPresentationCount(_host.LocalPlayer.Id, localPlayerState.GibDeaths);
            ApplySnapshotPlayer(_host.LocalPlayer, appliedLocalPlayerState);
            var diedThisSnapshot = wasAlive && !_host.LocalPlayer.IsAlive;
            var wasGibbedDeath = localPlayerState.GibDeaths > previousGibDeaths;
            if (diedThisSnapshot
                && wasGibbedDeath
                && TryMarkNetworkGibDeathPresented(_host.LocalPlayer.Id, localPlayerState.GibDeaths))
            {
                _host.PlayerRemains.SpawnClientPlayerGibsFromNetworkDeath(_host.LocalPlayer);
            }

            _host.NetworkPlayerRules.TrySetNetworkPlayerAwaitingJoin(SimulationConstants.LocalPlayerSlot, localPlayerState.IsAwaitingJoin);
            _host.NetworkPlayerRules.TrySetNetworkPlayerRespawnTicks(SimulationConstants.LocalPlayerSlot, localPlayerState.RespawnTicks);
            _host.NetworkPlayerRules.ApplySnapshotNetworkPlayerPingMilliseconds(SimulationConstants.LocalPlayerSlot, localPlayerState.PingMilliseconds);
            _host.NetworkPlayerRules.TrySetNetworkPlayerConfiguredTeam(SimulationConstants.LocalPlayerSlot, _host.LocalPlayer.Team);
            return;
        }

        _host.NetworkPlayerRules.TrySetNetworkPlayerAwaitingJoin(SimulationConstants.LocalPlayerSlot, true);
        _host.NetworkPlayerRules.TrySetNetworkPlayerRespawnTicks(SimulationConstants.LocalPlayerSlot, 0);
        _host.NetworkPlayerRules.ApplySnapshotNetworkPlayerPingMilliseconds(SimulationConstants.LocalPlayerSlot, -1);
        _host.ClientSnapshots.AuthoritativeLocalPlayerId = null;
        _host.LocalDeathCam = null;
        _host.LocalPlayer.ClearMedicHealingTarget();
        _host.LocalPlayer.Kill();
    }

    private void ApplySnapshotRemotePlayerState(
        IReadOnlyList<SnapshotPlayerState> players,
        IReadOnlyList<SnapshotPlayerState> scoreboardPlayers,
        byte localPlayerSlot,
        SnapshotPlayerState? localPlayerState,
        bool isSpectatorSnapshot)
    {
        var remotePlayerStates = players
            .Where(player => !player.IsSpectator)
            .Where(player => NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(player.Slot))
            .Where(player => isSpectatorSnapshot || player.Slot != localPlayerSlot)
            .OrderBy(player => player.Slot)
            .ToList();
        var remoteScoreboardPlayerStates = scoreboardPlayers
            .Where(player => !player.IsSpectator)
            .Where(player => NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(player.Slot))
            .Where(player => isSpectatorSnapshot || player.Slot != localPlayerSlot)
            .OrderBy(player => player.Slot)
            .ToList();

        _host.EnemyPlayerEnabled = false;
        _host.DummyState.EnemyRespawnTicks = 0;
        _host.NetworkPlayerRules.ClearEnemyInputOverride();
        _host.EnemyPlayer.Kill();
        _host.FriendlyDummyEnabled = false;
        _host.FriendlyDummy.Kill();
        SyncRemoteSnapshotPlayers(remotePlayerStates);
        SyncRemoteSnapshotScoreboardPlayers(remoteScoreboardPlayerStates);

        if (localPlayerState is not null && !localPlayerState.IsSpectator)
        {
            _host.NetworkPlayerRules.TrySetNetworkPlayerConfiguredTeam(SimulationConstants.LocalPlayerSlot, _host.LocalPlayer.Team);
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
