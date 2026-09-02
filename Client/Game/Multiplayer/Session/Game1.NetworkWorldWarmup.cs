#nullable enable

using OpenGarrison.Protocol;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void BeginNetworkWorldWarmup(string levelName)
    {
        if (!_networkClient.IsConnected || _networkClient.IsReplayConnection)
        {
            _networkWorldWarmupActive = false;
            return;
        }

        _networkWorldWarmupActive = true;
        _networkWorldWarmupFullSnapshotApplied = false;
        _networkWorldWarmupAppliedSnapshotsAfterFull = 0;
        _networkWorldWarmupStartedClockSeconds = _networkInterpolationClockSeconds;
        ShowJoiningServerLoadingOverlay();
    }

    private void CancelNetworkWorldWarmup()
    {
        _networkWorldWarmupActive = false;
        _networkWorldWarmupFullSnapshotApplied = false;
        _networkWorldWarmupAppliedSnapshotsAfterFull = 0;
        _networkWorldWarmupStartedClockSeconds = -1d;
    }

    private bool IsNetworkWorldWarmupBlockingGameplay()
    {
        return _networkWorldWarmupActive
            && _networkClient.IsConnected
            && !_networkClient.IsReplayConnection;
    }

    private bool IsNetworkWorldWarmupBlockingPresentation()
        => ShouldBlockNetworkWorldWarmupPresentation(
            IsNetworkWorldWarmupBlockingGameplay(),
            _networkClient.LastToDieState.Snapshot?.Phase);

    // Hosted LTD begins in semantic lobby and selection phases before the
    // server creates an authoritative gameplay entity for the local player.
    // Those full-screen menus are safe to present without a warmed gameplay
    // world. Keeping them behind the ordinary online warmup gate would create
    // a deadlock: the guest cannot choose a survivor, so the entity that would
    // release warmup is never spawned.
    internal static bool ShouldBlockNetworkWorldWarmupPresentation(
        bool gameplayWarmupBlocking,
        LastToDieWirePhase? lastToDiePhase)
        => gameplayWarmupBlocking
            && lastToDiePhase is not (
                LastToDieWirePhase.Lobby
                or LastToDieWirePhase.SurvivorChoice
                or LastToDieWirePhase.RewardChoice
                or LastToDieWirePhase.LoadingStage
                or LastToDieWirePhase.Won
                or LastToDieWirePhase.Lost);

    // The world warmup is the visibility gate for a newly joined online session.
    // It must not release while interpolation is still seeding its presentation
    // histories; otherwise the first rendered frames can expose uninitialized
    // remote-player presentation state even though an authoritative snapshot has
    // already been applied.
    internal static bool ShouldReleaseNetworkWorldWarmup(
        bool hasAuthoritativeLocalPlayer,
        bool fullSnapshotApplied,
        int appliedSnapshotsAfterFull,
        bool hasFreshRemotePlayerHistories,
        bool hasQueuedAuthoritativeSnapshots,
        bool interpolationWarmupActive)
    {
        return hasAuthoritativeLocalPlayer
            && fullSnapshotApplied
            && appliedSnapshotsAfterFull >= NetworkWorldWarmupMinimumAppliedSnapshotsAfterFull
            && hasFreshRemotePlayerHistories
            && !hasQueuedAuthoritativeSnapshots
            && !interpolationWarmupActive;
    }

    private void ObserveAppliedNetworkWorldSnapshot(SnapshotMessage snapshot, bool isServerFullSnapshot)
    {
        if (!IsNetworkWorldWarmupBlockingGameplay())
        {
            return;
        }

        if (isServerFullSnapshot)
        {
            _networkWorldWarmupFullSnapshotApplied = true;
            _networkWorldWarmupAppliedSnapshotsAfterFull = 1;
        }
        else if (_networkWorldWarmupFullSnapshotApplied)
        {
            _networkWorldWarmupAppliedSnapshotsAfterFull += 1;
        }

        var hasAuthoritativeLocalPlayer = HasAuthoritativeLocalPlayerForNetworkWorldWarmup();
        var hasFreshRemotePlayerHistories = hasAuthoritativeLocalPlayer
            && HasFreshRemotePlayerHistoriesForCurrentWorld();
        if (!ShouldReleaseNetworkWorldWarmup(
                hasAuthoritativeLocalPlayer,
                _networkWorldWarmupFullSnapshotApplied,
                _networkWorldWarmupAppliedSnapshotsAfterFull,
                hasFreshRemotePlayerHistories,
                _queuedAuthoritativeSnapshots.Count > 0,
                IsNetworkInterpolationWarmupActive()))
        {
            ShowJoiningServerLoadingOverlay();
            return;
        }

        HideLoadingOverlay();
        CancelNetworkWorldWarmup();
    }

    private bool HasAuthoritativeLocalPlayerForNetworkWorldWarmup()
    {
        return _networkClient.IsSpectator || _localPlayerSnapshotEntityId.HasValue;
    }

    private bool HasFreshRemotePlayerHistoriesForCurrentWorld()
    {
        if (_latestSnapshotServerTimeSeconds < 0d)
        {
            return false;
        }

        if (!HasAuthoritativeLocalPlayerForNetworkWorldWarmup())
        {
            return false;
        }

        foreach (var player in _world.RemoteSnapshotPlayers)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            if (!HasFreshRemotePlayerRenderHistory(player.Id))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasFreshRemotePlayerRenderHistory(int playerId)
    {
        if (!_remotePlayerSnapshotHistories.TryGetValue(playerId, out var history) || history.Count == 0)
        {
            return false;
        }

        return _latestSnapshotServerTimeSeconds < 0d
            || _latestSnapshotServerTimeSeconds - history[^1].TimeSeconds <= NetworkWorldWarmupFreshPlayerHistorySeconds;
    }

    private bool HasFreshPlayerRenderHistory(PlayerEntity player)
    {
        if (!_networkClient.IsConnected || _networkClient.IsReplayConnection)
        {
            return true;
        }

        if (IsNetworkWorldWarmupBlockingGameplay())
        {
            return false;
        }

        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            return true;
        }

        if (!_remotePlayerSnapshotHistories.TryGetValue(player.Id, out var history) || history.Count == 0)
        {
            return true;
        }

        return _latestSnapshotServerTimeSeconds < 0d
            || _latestSnapshotServerTimeSeconds - history[^1].TimeSeconds <= StaleRemotePlayerSnapshotHistoryPruneSeconds;
    }
}
