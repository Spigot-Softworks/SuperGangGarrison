#nullable enable

using OpenGarrison.Protocol;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public void BeginNetworkWorldWarmup(string levelName)
        => StartNetworkWorldWarmup(acceptNextAppliedSnapshotAsBaseline: false);

    private void BeginNetworkWorldWarmupFromAppliedSnapshot(string levelName)
        => StartNetworkWorldWarmup(acceptNextAppliedSnapshotAsBaseline: false);

    private void BeginNetworkWorldWarmupFromNextAppliedSnapshot(string levelName)
    {
        StartNetworkWorldWarmup(acceptNextAppliedSnapshotAsBaseline: true);
        while (_queuedAuthoritativeSnapshots.Count > 0)
        {
            _queuedAuthoritativeSnapshots.Dequeue();
            RecordDroppedQueuedAuthoritativeSnapshot();
        }

        _gameplayManager.NetworkPresentation.LastBufferedSnapshotFrame = _gameplayManager.NetworkPresentation.LastAppliedSnapshotFrame;
        ResetSnapshotPresentationHistories();
    }

    private void StartNetworkWorldWarmup(bool acceptNextAppliedSnapshotAsBaseline)
    {
        if (!_networkClient.IsConnected || _networkClient.IsReplayConnection)
        {
            _gameplayManager.NetworkPresentation.NetworkWorldWarmupActive = false;
            return;
        }

        _gameplayManager.NetworkPresentation.NetworkWorldWarmupActive = true;
        _gameplayManager.NetworkPresentation.NetworkWorldWarmupFullSnapshotApplied = false;
        _gameplayManager.NetworkPresentation.NetworkWorldWarmupAppliedSnapshotsAfterFull = 0;
        _gameplayManager.NetworkPresentation.NetworkWorldWarmupAcceptNextAppliedSnapshotAsBaseline = acceptNextAppliedSnapshotAsBaseline;
        _gameplayManager.NetworkPresentation.NetworkInterpolationWarmupSnapshotsRemaining = Math.Max(
            _gameplayManager.NetworkPresentation.NetworkInterpolationWarmupSnapshotsRemaining,
            NetworkInterpolationWarmupSnapshotCount - 1);
        _gameplayManager.NetworkPresentation.NetworkInterpolationWarmupUntilClockSeconds = Math.Max(
            _gameplayManager.NetworkPresentation.NetworkInterpolationWarmupUntilClockSeconds,
            _networkInterpolationClockSeconds + NetworkInterpolationWarmupSeconds);
        _gameplayManager.NetworkPresentation.HasLocalPlayerRenderTime = false;
        _gameplayManager.NetworkPresentation.HasRemotePlayerRenderTime = false;
        ResetTransientPresentationEffects();
        ResetHealingCharacterEffects();
        ResetBackstabVisuals();
        ShowJoiningServerLoadingOverlay();
    }

    private void CancelNetworkWorldWarmup()
    {
        _gameplayManager.NetworkPresentation.NetworkWorldWarmupActive = false;
        _gameplayManager.NetworkPresentation.NetworkWorldWarmupFullSnapshotApplied = false;
        _gameplayManager.NetworkPresentation.NetworkWorldWarmupAppliedSnapshotsAfterFull = 0;
        _gameplayManager.NetworkPresentation.NetworkWorldWarmupAcceptNextAppliedSnapshotAsBaseline = false;
    }

    private bool IsNetworkWorldWarmupBlockingGameplay()
    {
        return _gameplayManager.NetworkPresentation.NetworkWorldWarmupActive
            && _networkClient.IsConnected
            && !_networkClient.IsReplayConnection;
    }

    public bool IsNetworkWorldWarmupBlockingPresentation()
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
        bool isServerFullSnapshot,
        bool isPresentationEpochBaselineSnapshot = false)
    {
        if (!IsNetworkWorldWarmupBlockingGameplay())
        {
            return;
        }

        var establishesPresentationBaseline = ShouldEstablishNetworkWorldWarmupBaseline(
            _networkClient.IsLegacyGg2Connection,
            _gameplayManager.NetworkPresentation.NetworkWorldWarmupFullSnapshotApplied,
            isServerFullSnapshot,
            isPresentationEpochBaselineSnapshot,
            _gameplayManager.NetworkPresentation.NetworkWorldWarmupAcceptNextAppliedSnapshotAsBaseline);
        if (establishesPresentationBaseline)
        {
            _gameplayManager.NetworkPresentation.NetworkWorldWarmupFullSnapshotApplied = true;
            _gameplayManager.NetworkPresentation.NetworkWorldWarmupAppliedSnapshotsAfterFull = 0;
            _gameplayManager.NetworkPresentation.NetworkWorldWarmupAcceptNextAppliedSnapshotAsBaseline = false;
        }
        else if (_gameplayManager.NetworkPresentation.NetworkWorldWarmupFullSnapshotApplied)
        {
            _gameplayManager.NetworkPresentation.NetworkWorldWarmupAppliedSnapshotsAfterFull += 1;
        }

        var hasAuthoritativeLocalPlayer = HasAuthoritativeLocalPlayerForNetworkWorldWarmup();
        var hasFreshRemotePlayerHistories = hasAuthoritativeLocalPlayer
            && HasFreshRemotePlayerHistoriesForCurrentWorld();
        if (!ShouldReleaseNetworkWorldWarmup(
                hasAuthoritativeLocalPlayer,
                _gameplayManager.NetworkPresentation.NetworkWorldWarmupFullSnapshotApplied,
                _gameplayManager.NetworkPresentation.NetworkWorldWarmupAppliedSnapshotsAfterFull,
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

    internal static bool ShouldEstablishNetworkWorldWarmupBaseline(
        bool isLegacyGg2,
        bool hasBaseline,
        bool isServerFullSnapshot,
        bool isPresentationEpochBaselineSnapshot,
        bool acceptNextAppliedSnapshotAsBaseline)
        => isPresentationEpochBaselineSnapshot
            || (isServerFullSnapshot && (!isLegacyGg2 || !hasBaseline))
            || (acceptNextAppliedSnapshotAsBaseline && !hasBaseline);

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
        if (_gameplayManager.NetworkPresentation.LatestSnapshotServerTimeSeconds < 0d)
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
            _gameplayManager.NetworkPresentation.LatestSnapshotServerTimeSeconds,
            history.Count > 0 ? history[^1].TimeSeconds : -1d,
            NetworkWorldWarmupFreshPlayerHistorySeconds);
    }

    public bool HasFreshPlayerRenderHistory(PlayerEntity player)
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
            _gameplayManager.NetworkPresentation.LatestSnapshotServerTimeSeconds,
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
            _gameplayManager.NetworkPresentation.NetworkPresentationObservedLastToDiePhase = null;
            return;
        }

        var currentPhase = _networkClient.LastToDieState.Snapshot?.Phase;
        if (ShouldRestartNetworkPresentationForLastToDiePhase(
                _gameplayManager.NetworkPresentation.NetworkPresentationObservedLastToDiePhase,
                currentPhase))
        {
            BeginNetworkWorldWarmupFromNextAppliedSnapshot(_world.Level.Name);
        }

        _gameplayManager.NetworkPresentation.NetworkPresentationObservedLastToDiePhase = currentPhase;
    }

    internal static bool ShouldRestartNetworkPresentationForLastToDiePhase(
        LastToDieWirePhase? previousPhase,
        LastToDieWirePhase? currentPhase)
        => previousPhase.HasValue
            && previousPhase != LastToDieWirePhase.Playing
            && currentPhase == LastToDieWirePhase.Playing;
}
