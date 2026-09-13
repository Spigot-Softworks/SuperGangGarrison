#nullable enable

using OpenGarrison.Protocol;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void BeginNetworkWorldWarmup(string levelName)
        => StartNetworkWorldWarmup(levelName, acceptNextAppliedSnapshotAsBaseline: false);

    private void BeginNetworkWorldWarmupFromAppliedSnapshot(string levelName)
        => StartNetworkWorldWarmup(levelName, acceptNextAppliedSnapshotAsBaseline: false);

    private void BeginNetworkWorldWarmupFromNextAppliedSnapshot(string levelName)
    {
        StartNetworkWorldWarmup(levelName, acceptNextAppliedSnapshotAsBaseline: true);
        while (_queuedAuthoritativeSnapshots.Count > 0)
        {
            _queuedAuthoritativeSnapshots.Dequeue();
            RecordDroppedQueuedAuthoritativeSnapshot();
        }

        _lastBufferedSnapshotFrame = _lastAppliedSnapshotFrame;
        ResetSnapshotPresentationHistories();
    }

    private void StartNetworkWorldWarmup(string levelName, bool acceptNextAppliedSnapshotAsBaseline)
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
        _networkWorldWarmupAcceptNextAppliedSnapshotAsBaseline = acceptNextAppliedSnapshotAsBaseline;
        _networkInterpolationWarmupSnapshotsRemaining = Math.Max(
            _networkInterpolationWarmupSnapshotsRemaining,
            NetworkInterpolationWarmupSnapshotCount - 1);
        _networkInterpolationWarmupUntilClockSeconds = Math.Max(
            _networkInterpolationWarmupUntilClockSeconds,
            _networkInterpolationClockSeconds + NetworkInterpolationWarmupSeconds);
        _hasLocalPlayerRenderTime = false;
        _hasRemotePlayerRenderTime = false;
        ResetTransientPresentationEffects();
        ResetHealingCharacterEffects();
        ResetBackstabVisuals();
        ShowJoiningServerLoadingOverlay();
    }

    private void CancelNetworkWorldWarmup()
    {
        _networkWorldWarmupActive = false;
        _networkWorldWarmupFullSnapshotApplied = false;
        _networkWorldWarmupAppliedSnapshotsAfterFull = 0;
        _networkWorldWarmupStartedClockSeconds = -1d;
        _networkWorldWarmupAcceptNextAppliedSnapshotAsBaseline = false;
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

    private void ObserveAppliedNetworkWorldSnapshot(
        SnapshotMessage snapshot,
        bool isServerFullSnapshot,
        bool isPresentationEpochBaselineSnapshot = false)
    {
        if (!IsNetworkWorldWarmupBlockingGameplay())
        {
            return;
        }

        var establishesPresentationBaseline = isServerFullSnapshot
            || isPresentationEpochBaselineSnapshot
            || (_networkWorldWarmupAcceptNextAppliedSnapshotAsBaseline
                && !_networkWorldWarmupFullSnapshotApplied);
        if (establishesPresentationBaseline)
        {
            _networkWorldWarmupFullSnapshotApplied = true;
            _networkWorldWarmupAppliedSnapshotsAfterFull = 0;
            _networkWorldWarmupAcceptNextAppliedSnapshotAsBaseline = false;
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
        return ShouldTreatLocalPlayerAsAuthoritativeForWarmup(
            _networkClient.IsSpectator,
            _localPlayerSnapshotEntityId.HasValue,
            _world.LocalPlayerAwaitingJoin);
    }

    internal static bool ShouldTreatLocalPlayerAsAuthoritativeForWarmup(
        bool isSpectator,
        bool hasSnapshotEntityId,
        bool isAwaitingJoin)
        => isSpectator || (hasSnapshotEntityId && !isAwaitingJoin);

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

        if (!_networkClient.IsSpectator
            && _world.LocalPlayer.IsAlive
            && !HasFreshRemotePlayerRenderHistory(GetPlayerStateKey(_world.LocalPlayer)))
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
        if (!_remotePlayerSnapshotHistories.TryGetValue(playerId, out var history))
        {
            return false;
        }

        return IsNetworkPlayerPresentationHistoryReady(
            history.Count,
            _latestSnapshotServerTimeSeconds,
            history.Count > 0 ? history[^1].TimeSeconds : -1d,
            NetworkWorldWarmupFreshPlayerHistorySeconds);
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

        var playerId = GetPlayerStateKey(player);
        if (!_remotePlayerSnapshotHistories.TryGetValue(playerId, out var history))
        {
            return false;
        }

        return IsNetworkPlayerPresentationHistoryRenderable(
            history.Count,
            _latestSnapshotServerTimeSeconds,
            history.Count > 0 ? history[^1].TimeSeconds : -1d,
            StaleRemotePlayerSnapshotHistoryPruneSeconds);
    }

    internal static bool IsNetworkPlayerPresentationHistoryReady(
        int sampleCount,
        double latestSnapshotServerTimeSeconds,
        double latestHistorySampleTimeSeconds,
        double freshnessSeconds)
        => IsNetworkPlayerPresentationHistoryUsable(
            sampleCount,
            NetworkPlayerPresentationMinimumSamples,
            latestSnapshotServerTimeSeconds,
            latestHistorySampleTimeSeconds,
            freshnessSeconds);

    internal static bool IsNetworkPlayerPresentationHistoryRenderable(
        int sampleCount,
        double latestSnapshotServerTimeSeconds,
        double latestHistorySampleTimeSeconds,
        double freshnessSeconds)
        => IsNetworkPlayerPresentationHistoryUsable(
            sampleCount,
            minimumSampleCount: 1,
            latestSnapshotServerTimeSeconds,
            latestHistorySampleTimeSeconds,
            freshnessSeconds);

    private static bool IsNetworkPlayerPresentationHistoryUsable(
        int sampleCount,
        int minimumSampleCount,
        double latestSnapshotServerTimeSeconds,
        double latestHistorySampleTimeSeconds,
        double freshnessSeconds)
    {
        if (sampleCount < minimumSampleCount
            || !double.IsFinite(latestSnapshotServerTimeSeconds)
            || !double.IsFinite(latestHistorySampleTimeSeconds)
            || latestSnapshotServerTimeSeconds < 0d
            || latestHistorySampleTimeSeconds < 0d)
        {
            return false;
        }

        var sampleAgeSeconds = latestSnapshotServerTimeSeconds - latestHistorySampleTimeSeconds;
        return sampleAgeSeconds >= -0.001d && sampleAgeSeconds <= freshnessSeconds;
    }

    private void ObserveNetworkPresentationPhaseTransition()
    {
        if (!_networkClient.IsConnected || _networkClient.IsReplayConnection)
        {
            _networkPresentationObservedLastToDiePhase = null;
            return;
        }

        var currentPhase = _networkClient.LastToDieState.Snapshot?.Phase;
        if (ShouldRestartNetworkPresentationForLastToDiePhase(
                _networkPresentationObservedLastToDiePhase,
                currentPhase))
        {
            BeginNetworkWorldWarmupFromNextAppliedSnapshot(_world.Level.Name);
        }

        _networkPresentationObservedLastToDiePhase = currentPhase;
    }

    internal static bool ShouldRestartNetworkPresentationForLastToDiePhase(
        LastToDieWirePhase? previousPhase,
        LastToDieWirePhase? currentPhase)
        => previousPhase.HasValue
            && previousPhase != LastToDieWirePhase.Playing
            && currentPhase == LastToDieWirePhase.Playing;
}
