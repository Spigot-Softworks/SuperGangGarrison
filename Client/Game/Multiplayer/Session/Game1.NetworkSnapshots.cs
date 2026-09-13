#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly HashSet<int> _lastVisibleEnemySpyIds = new();
    private readonly Dictionary<int, byte> _lastVisibleEnemySpySlots = new();

    private readonly record struct ResolvedSnapshotEntry(
        SnapshotMessage RawSnapshot,
        SnapshotMessage ResolvedSnapshot);

    private bool TryHandleSnapshotMessage(
        SnapshotMessage snapshot,
        ref ulong latestBufferedSnapshotFrame,
        ref SnapshotMessage? latestResolvedSnapshot,
        ref Dictionary<ulong, SnapshotBaselineState>? resolvedBatchSnapshotsByFrame,
        ref List<ResolvedSnapshotEntry>? resolvedBatchSnapshots)
    {
        if ((!string.Equals(snapshot.LevelName, _world.Level.Name, StringComparison.OrdinalIgnoreCase)
                || snapshot.MapAreaIndex != _world.Level.MapAreaIndex)
            && TryEnsureNetworkMapAvailable(
                snapshot.LevelName,
                snapshot.IsCustomMap,
                snapshot.MapDownloadUrl,
                snapshot.MapContentHash,
                out var snapshotMapError) is not NetworkMapSyncStatus.Available)
        {
            if (string.IsNullOrWhiteSpace(snapshotMapError))
            {
                return false;
            }

            ReturnToMainMenuWithNetworkStatus(snapshotMapError, $"custom map sync failed: {snapshotMapError}");
            return false;
        }

        if (snapshot.Frame <= latestBufferedSnapshotFrame)
        {
            RecordStaleSnapshot();
            return false;
        }

        ISnapshotBaselineState? baselineSnapshot = null;
        if (snapshot.IsDelta && snapshot.BaselineFrame != 0)
        {
            if (resolvedBatchSnapshotsByFrame?.TryGetValue(snapshot.BaselineFrame, out var batchBaselineSnapshot) == true)
            {
                baselineSnapshot = batchBaselineSnapshot;
            }
            else if (TryGetSnapshotState(snapshot.BaselineFrame, out var storedBaselineSnapshot))
            {
                baselineSnapshot = storedBaselineSnapshot;
            }
            else
            {
                RecordMissingBaselineSnapshot();
                AddNetworkConsoleLine($"snapshot {snapshot.Frame} missing baseline {snapshot.BaselineFrame}");
                return false;
            }
        }

        SnapshotMessage resolvedSnapshot;
        try
        {
            resolvedSnapshot = SnapshotDelta.ToFullSnapshot(snapshot, baselineSnapshot);
        }
        catch (InvalidOperationException ex)
        {
            RecordRejectedSnapshot();
            AddNetworkConsoleLine($"snapshot {snapshot.Frame} rejected: {ex.Message}");
            return false;
        }

        if (!_replaySeekCatchUpActive)
        {
            RecordResolvedSnapshotPredictionError(resolvedSnapshot);
            QueueResolvedSnapshotVisualEvents(resolvedSnapshot);
            QueueResolvedSnapshotSoundEvents(resolvedSnapshot);
            QueueResolvedSnapshotDamageEvents(resolvedSnapshot);
        }

        resolvedBatchSnapshotsByFrame ??= new Dictionary<ulong, SnapshotBaselineState>();
        resolvedBatchSnapshotsByFrame[resolvedSnapshot.Frame] = SnapshotBaselineState.FromSnapshot(resolvedSnapshot);

        resolvedBatchSnapshots ??= new List<ResolvedSnapshotEntry>();
        resolvedBatchSnapshots.Add(new ResolvedSnapshotEntry(snapshot, resolvedSnapshot));
        latestResolvedSnapshot = resolvedSnapshot;
        latestBufferedSnapshotFrame = resolvedSnapshot.Frame;
        return true;
    }

    private void RecordResolvedSnapshotPredictionError(SnapshotMessage resolvedSnapshot)
    {
        var localSnapshotPlayer = resolvedSnapshot.Players.FirstOrDefault(player => player.Slot == _networkClient.LocalPlayerSlot);
        if (_networkDiagnosticsEnabled && localSnapshotPlayer is not null && CanUseLocalPrediction() && _hasPredictedLocalPlayerPosition)
        {
            RecordPredictionError(Vector2.Distance(_predictedLocalPlayerPosition, new Vector2(localSnapshotPlayer.X, localSnapshotPlayer.Y)));
        }

        _localPlayerSnapshotEntityId = localSnapshotPlayer?.PlayerId;
    }

    private void QueueResolvedSnapshotVisualEvents(SnapshotMessage resolvedSnapshot)
    {
        for (var visualIndex = 0; visualIndex < resolvedSnapshot.VisualEvents.Count; visualIndex += 1)
        {
            var visualEvent = resolvedSnapshot.VisualEvents[visualIndex];
            if (!ShouldProcessNetworkEvent(visualEvent.EventId, _processedNetworkVisualEventIds, _processedNetworkVisualEventOrder))
            {
                continue;
            }

            _pendingNetworkVisualEvents.Add(visualEvent with
            {
                SourceFrame = ResolveNetworkEventSourceFrame(visualEvent.SourceFrame, resolvedSnapshot.Frame),
            });
        }
    }

    private void QueueResolvedSnapshotSoundEvents(SnapshotMessage resolvedSnapshot)
    {
        for (var soundIndex = 0; soundIndex < resolvedSnapshot.SoundEvents.Count; soundIndex += 1)
        {
            var soundEvent = resolvedSnapshot.SoundEvents[soundIndex];
            if (HasProcessedNetworkEvent(soundEvent.EventId, _processedNetworkSoundEventIds))
            {
                continue;
            }

            _pendingNetworkSoundEvents.Add(new WorldSoundEvent(
                soundEvent.SoundName,
                soundEvent.X,
                soundEvent.Y,
                soundEvent.EventId,
                ResolveNetworkEventSourceFrame(soundEvent.SourceFrame, resolvedSnapshot.Frame),
                soundEvent.SourcePlayerId));
        }
    }

    internal static ulong ResolveNetworkEventSourceFrame(ulong sourceFrame, ulong snapshotFrame)
    {
        return sourceFrame == 0 ? snapshotFrame : sourceFrame;
    }

    private void FinalizeResolvedSnapshotBatch(SnapshotMessage latestResolvedSnapshot, List<ResolvedSnapshotEntry> resolvedBatchSnapshots)
    {
        UpdateSnapshotTiming(
            latestResolvedSnapshot.Frame,
            latestResolvedSnapshot.TickRate,
            resolvedBatchSnapshots.Count);
        for (var snapshotIndex = 0; snapshotIndex < resolvedBatchSnapshots.Count; snapshotIndex += 1)
        {
            var entry = resolvedBatchSnapshots[snapshotIndex];
            var isServerFullSnapshot = IsFullEquivalentNetworkSnapshot(entry.RawSnapshot);
            RememberSnapshotState(entry.ResolvedSnapshot);
            EnqueueAuthoritativeSnapshot(entry.RawSnapshot, entry.ResolvedSnapshot, isServerFullSnapshot);
        }

        RecordSnapshotAckAhead(latestResolvedSnapshot.Frame, _lastAppliedSnapshotFrame, _queuedAuthoritativeSnapshots.Count);
        _networkClient.AcknowledgeSnapshot(latestResolvedSnapshot.Frame);
    }

    private static bool IsFullEquivalentNetworkSnapshot(SnapshotMessage snapshot)
    {
        return !snapshot.IsDelta || snapshot.BaselineFrame == 0;
    }

    private void EnqueueAuthoritativeSnapshot(
        SnapshotMessage rawSnapshot,
        SnapshotMessage resolvedSnapshot,
        bool isServerFullSnapshot)
    {
        if (resolvedSnapshot.Frame <= _lastBufferedSnapshotFrame)
        {
            return;
        }

        _queuedAuthoritativeSnapshots.Enqueue(new QueuedAuthoritativeSnapshot(
            rawSnapshot,
            resolvedSnapshot,
            isServerFullSnapshot));
        _lastBufferedSnapshotFrame = resolvedSnapshot.Frame;
        var frameBacklog = _lastAppliedSnapshotFrame > 0 && resolvedSnapshot.Frame > _lastAppliedSnapshotFrame
            ? resolvedSnapshot.Frame - _lastAppliedSnapshotFrame
            : 0UL;
        RecordQueuedAuthoritativeSnapshot(_queuedAuthoritativeSnapshots.Count, frameBacklog);
        while (_queuedAuthoritativeSnapshots.Count > MaxQueuedAuthoritativeSnapshots)
        {
            var droppedSnapshot = _queuedAuthoritativeSnapshots.Dequeue();
            if (droppedSnapshot.IsServerFullSnapshot
                && IsNetworkWorldWarmupBlockingGameplay()
                && !_networkWorldWarmupFullSnapshotApplied)
            {
                // Every queued resolved delta already contains the complete state
                // reconstructed from this baseline. If burst trimming drops the
                // original full packet, promote the first retained resolved state
                // instead of leaving the visibility gate waiting forever.
                _networkWorldWarmupAcceptNextAppliedSnapshotAsBaseline = true;
            }

            RecordDroppedQueuedAuthoritativeSnapshot();
        }
    }

    private void ApplyNextQueuedAuthoritativeSnapshot()
    {
        if (_queuedAuthoritativeSnapshots.Count == 0)
        {
            return;
        }

        var queuedSnapshot = _queuedAuthoritativeSnapshots.Dequeue();
        var rawSnapshot = queuedSnapshot.RawSnapshot;
        var snapshot = queuedSnapshot.ResolvedSnapshot;
        var isServerFullSnapshot = queuedSnapshot.IsServerFullSnapshot;
        var applySnapshotStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var previousLevelName = _world.Level.Name;
        var previousMapAreaIndex = _world.Level.MapAreaIndex;
        var previousLocalPlayerId = _lastAppliedSnapshotLocalPlayerId;
        var wasAwaitingJoin = _world.LocalPlayerAwaitingJoin;
        var wasLocalPlayerAlive = _world.LocalPlayer.IsAlive;
        var previousLocalClassId = _world.LocalPlayer.ClassId;
        CaptureRemovedProjectilePresentationEntities(rawSnapshot, snapshot.Frame);
        if (!_world.ApplySnapshot(snapshot, _networkClient.LocalPlayerSlot))
        {
            if (_networkDiagnosticsEnabled)
            {
                RecordApplySnapshotDuration(GetDiagnosticsElapsedMilliseconds(applySnapshotStartTimestamp));
                RecordRejectedSnapshot();
            }

            AddNetworkConsoleLine($"snapshot rejected for slot {_networkClient.LocalPlayerSlot}");
            return;
        }

        _networkClient.NotifyWorldSnapshotApplied(snapshot);

        var mapChanged = !string.Equals(previousLevelName, _world.Level.Name, StringComparison.OrdinalIgnoreCase)
            || previousMapAreaIndex != _world.Level.MapAreaIndex;
        var restoredProtocol64Baseline = _networkClient.Protocol64ModeEnabled
            && !mapChanged
            && _networkClient.Protocol64State.RestoreLocalPlayerBaseline(_world, _networkClient.LocalPlayerSlot);
        if (_networkClient.Protocol64ModeEnabled && !mapChanged)
        {
            _networkClient.Protocol64State.RestoreNewerProjectilesAfterSnapshot(
                _world, snapshot.Frame, _networkClient.LocalPlayerSlot);
        }
        var currentLocalPlayerId = GetResolvedLocalPlayerId();
        var localPlayerIdentityChanged = previousLocalPlayerId.HasValue
            && previousLocalPlayerId.Value != currentLocalPlayerId;
        var localPlayerJoined = wasAwaitingJoin && !_world.LocalPlayerAwaitingJoin;
        var presentationEpochChanged = mapChanged
            || localPlayerIdentityChanged
            || localPlayerJoined;
        var localPlayerAuthorityChanged = mapChanged
            || localPlayerIdentityChanged
            || previousLocalClassId != _world.LocalPlayer.ClassId
            || wasLocalPlayerAlive != _world.LocalPlayer.IsAlive
            || wasAwaitingJoin != _world.LocalPlayerAwaitingJoin;
        if (presentationEpochChanged)
        {
            BeginNetworkWorldWarmupFromAppliedSnapshot(snapshot.LevelName);
        }

        if (ShouldResetSnapshotPresentationHistories(isServerFullSnapshot, presentationEpochChanged))
        {
            ResetAndSeedSnapshotPresentationHistories(snapshot);
        }
        else
        {
            CaptureRemoteInterpolationTargets(rawSnapshot, snapshot);
            RefreshRetainedInterpolationHistories(snapshot);
        }

        UpdateClientSniperAimIndicators();

        CaptureSmoothingTrackForLocalPlayer(snapshot);
        DetectFrozenSpyVisualsForMissingEnemySpies(snapshot);
        var previousAppliedSnapshotFrame = _lastAppliedSnapshotFrame;
        _lastAppliedSnapshotFrame = snapshot.Frame;
        if (_queuedAuthoritativeSnapshots.Count == 0)
        {
            _lastBufferedSnapshotFrame = _lastAppliedSnapshotFrame;
        }

        if (_networkDiagnosticsEnabled)
        {
            RecordApplySnapshotDuration(GetDiagnosticsElapsedMilliseconds(applySnapshotStartTimestamp));
            RecordAppliedSnapshot(snapshot.Frame, previousAppliedSnapshotFrame, _queuedAuthoritativeSnapshots.Count);
        }

        if (localPlayerAuthorityChanged)
        {
            ResetLocalPredictionForAuthorityTransition();
        }

        _lastAppliedSnapshotLocalPlayerId = currentLocalPlayerId;
        ObserveAppliedNetworkWorldSnapshot(
            snapshot,
            isServerFullSnapshot,
            presentationEpochChanged);

        if (!_classSelectOpen)
        {
            _pendingClassSelectTeam = null;
        }
        var reconcileStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        if (!_networkClient.Protocol64ModeEnabled)
        {
            _networkClient.AcknowledgeProcessedInput(snapshot.LastProcessedInputSequence);
            ReconcileLocalPrediction(snapshot.LastProcessedInputSequence);
        }
        else if (restoredProtocol64Baseline)
        {
            RebuildLocalPrediction(preserveRenderContinuity: true);
        }
        if (_networkDiagnosticsEnabled)
        {
            RecordReconcileDuration(GetDiagnosticsElapsedMilliseconds(reconcileStartTimestamp));
        }

        ReopenJoinMenusAfterMapTransition(previousLevelName, previousMapAreaIndex, wasAwaitingJoin);
    }

    private void CaptureSmoothingTrackForLocalPlayer(SnapshotMessage snapshot)
    {
        if (!_networkClient.IsConnected || _world.LocalPlayerAwaitingJoin || !_world.LocalPlayer.IsAlive)
        {
            return;
        }

        var localPlayerStateKey = GetResolvedLocalPlayerId();
        _entityInterpolationTracks.Remove(localPlayerStateKey);
        if (!_networkClient.IsReplayConnection)
        {
            return;
        }

        var currentRenderPosition = GetRenderPosition(localPlayerStateKey, _world.LocalPlayer.X, _world.LocalPlayer.Y, allowInterpolation: true);
        var targetPosition = new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
        if (Vector2.DistanceSquared(currentRenderPosition, targetPosition) <= 0.0001f)
        {
            return;
        }

        var tickDurationSeconds = snapshot.TickRate > 0
            ? 1f / snapshot.TickRate
            : 1f / SimulationConfig.DefaultTicksPerSecond;
        var durationSeconds = Math.Clamp(tickDurationSeconds, 1f / SimulationConfig.DefaultTicksPerSecond, 0.25f);
        _entityInterpolationTracks[localPlayerStateKey] = new InterpolationTrack(
            currentRenderPosition,
            targetPosition,
            _networkInterpolationClockSeconds,
            durationSeconds,
            Vector2.Zero,
            0f,
            0f);
    }

    private void DetectFrozenSpyVisualsForMissingEnemySpies(SnapshotMessage snapshot)
    {
        if (!_networkClient.IsConnected || _networkClient.IsSpectator || !_world.LocalPlayer.IsAlive)
        {
            _lastVisibleEnemySpyIds.Clear();
            _lastVisibleEnemySpySlots.Clear();
            _lastVisibleEnemySpyFrameStates.Clear();
            _lastVisibleEnemySpyObservationEpochs.Clear();
            _consumedFrozenSpyObservationEpochs.Clear();
            _frozenSpyVisuals.Clear();
            return;
        }

        var currentVisibleEnemySpyIds = new HashSet<int>();
        var removedPlayerSlots = new HashSet<int>(snapshot.RemovedPlayerIds);
        var explicitDeathOrRemovalSpyIds = new HashSet<int>();
        for (var playerIndex = 0; playerIndex < snapshot.Players.Count; playerIndex += 1)
        {
            var player = snapshot.Players[playerIndex];
            if (_lastVisibleEnemySpyIds.Contains(player.PlayerId)
                && (player.ClassId != (byte)PlayerClass.Spy
                    || (PlayerTeam)player.Team == _world.LocalPlayer.Team))
            {
                ResetFrozenSpyStateForPlayer(player.PlayerId);
            }

            if (player.Slot >= SimulationWorld.FirstSpectatorSlot
                || player.IsSpectator
                || player.ClassId != (byte)PlayerClass.Spy
                || (PlayerTeam)player.Team == _world.LocalPlayer.Team)
            {
                continue;
            }

            if (!player.IsAlive)
            {
                explicitDeathOrRemovalSpyIds.Add(player.PlayerId);
                continue;
            }

            if (player.IsSpyCloaked && player.SpyCloakAlpha > 0f && player.SpyCloakAlpha < 0.99f)
            {
                currentVisibleEnemySpyIds.Add(player.PlayerId);
                _lastVisibleEnemySpySlots[player.PlayerId] = player.Slot;
                continue;
            }

            if (IsSpyHiddenFromLocalViewer(player.PlayerId, (PlayerTeam)player.Team, player.X))
            {
                continue;
            }

            currentVisibleEnemySpyIds.Add(player.PlayerId);
            _lastVisibleEnemySpySlots[player.PlayerId] = player.Slot;
        }

        for (var playerIndex = 0; playerIndex < snapshot.ScoreboardPlayers.Count; playerIndex += 1)
        {
            var player = snapshot.ScoreboardPlayers[playerIndex];
            if (!removedPlayerSlots.Contains(player.Slot) || player.IsAlive)
            {
                continue;
            }

            if (player.ClassId == (byte)PlayerClass.Spy
                && (PlayerTeam)player.Team != _world.LocalPlayer.Team)
            {
                explicitDeathOrRemovalSpyIds.Add(player.PlayerId);
            }
        }

        // Reappearance starts a new observation epoch and immediately consumes
        // any old ghost. A snapshot omission or a facing change must not leave
        // the previous frame armed for another flash.
        foreach (var visibleSpyId in currentVisibleEnemySpyIds)
        {
            ResetFrozenSpyStateForPlayer(visibleSpyId);
        }

        foreach (var lastSpyId in _lastVisibleEnemySpyIds)
        {
            if (currentVisibleEnemySpyIds.Contains(lastSpyId))
            {
                continue;
            }

            if (explicitDeathOrRemovalSpyIds.Contains(lastSpyId))
            {
                ResetFrozenSpyStateForPlayer(lastSpyId);
                continue;
            }

            if (_lastVisibleEnemySpySlots.TryGetValue(lastSpyId, out var lastSpySlot)
                && removedPlayerSlots.Contains(lastSpySlot))
            {
                ResetFrozenSpyStateForPlayer(lastSpyId);
                continue;
            }

            SpawnFrozenSpyVisual(lastSpyId);
        }

        _lastVisibleEnemySpyIds.Clear();
        foreach (var spyId in currentVisibleEnemySpyIds)
        {
            _lastVisibleEnemySpyIds.Add(spyId);
        }
    }

    internal static bool ShouldResetSnapshotPresentationHistories(
        bool isServerFullSnapshot,
        bool presentationEpochChanged)
    {
        // A transport-level full snapshot is a recovery baseline, not a new
        // presentation world. Its complete entity list can safely refresh and
        // prune existing histories. Only an actual map/local-authority epoch
        // transition should discard those histories.
        _ = isServerFullSnapshot;
        return presentationEpochChanged;
    }

    private void ReopenJoinMenusAfterMapTransition(string previousLevelName, int previousMapAreaIndex, bool wasAwaitingJoin)
    {
        if (_networkClient.IsSpectator)
        {
            return;
        }

        var mapChanged = !string.Equals(previousLevelName, _world.Level.Name, StringComparison.OrdinalIgnoreCase)
            || previousMapAreaIndex != _world.Level.MapAreaIndex;
        if (!_world.LocalPlayerAwaitingJoin || (!mapChanged && wasAwaitingJoin))
        {
            return;
        }

        OpenOnlineTeamSelection(clearPendingSelections: true, statusMessage: string.Empty);
    }
}
