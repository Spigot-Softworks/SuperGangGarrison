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

        RecordResolvedSnapshotPredictionError(resolvedSnapshot);
        QueueResolvedSnapshotVisualEvents(resolvedSnapshot);
        QueueResolvedSnapshotSoundEvents(resolvedSnapshot);
        QueueResolvedSnapshotDamageEvents(resolvedSnapshot);

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
        if (_networkDiagnosticsEnabled && localSnapshotPlayer is not null && _hasPredictedLocalPlayerPosition)
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

            _pendingNetworkVisualEvents.Add(visualEvent);
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
                soundEvent.SourceFrame,
                soundEvent.SourcePlayerId));
        }
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
            CaptureRemoteInterpolationTargets(entry.RawSnapshot, entry.ResolvedSnapshot);
            EnqueueAuthoritativeSnapshot(entry.ResolvedSnapshot, isServerFullSnapshot);
        }

        RecordSnapshotAckAhead(latestResolvedSnapshot.Frame, _lastAppliedSnapshotFrame, _queuedAuthoritativeSnapshots.Count);
        _networkClient.AcknowledgeSnapshot(latestResolvedSnapshot.Frame);
    }

    private static bool IsFullEquivalentNetworkSnapshot(SnapshotMessage snapshot)
    {
        return !snapshot.IsDelta || snapshot.BaselineFrame == 0;
    }

    private void EnqueueAuthoritativeSnapshot(SnapshotMessage snapshot, bool isServerFullSnapshot)
    {
        if (snapshot.Frame <= _lastBufferedSnapshotFrame)
        {
            return;
        }

        _queuedAuthoritativeSnapshots.Enqueue(snapshot);
        if (isServerFullSnapshot)
        {
            _authoritativeFullSnapshotFrames.Add(snapshot.Frame);
        }
        else
        {
            _authoritativeFullSnapshotFrames.Remove(snapshot.Frame);
        }

        _lastBufferedSnapshotFrame = snapshot.Frame;
        var frameBacklog = _lastAppliedSnapshotFrame > 0 && snapshot.Frame > _lastAppliedSnapshotFrame
            ? snapshot.Frame - _lastAppliedSnapshotFrame
            : 0UL;
        RecordQueuedAuthoritativeSnapshot(_queuedAuthoritativeSnapshots.Count, frameBacklog);
        while (_queuedAuthoritativeSnapshots.Count > MaxQueuedAuthoritativeSnapshots)
        {
            var droppedSnapshot = _queuedAuthoritativeSnapshots.Dequeue();
            _authoritativeFullSnapshotFrames.Remove(droppedSnapshot.Frame);
            RecordDroppedQueuedAuthoritativeSnapshot();
        }
    }

    private void ApplyNextQueuedAuthoritativeSnapshot()
    {
        if (_queuedAuthoritativeSnapshots.Count == 0)
        {
            return;
        }

        var snapshot = _queuedAuthoritativeSnapshots.Dequeue();
        var isServerFullSnapshot = _authoritativeFullSnapshotFrames.Remove(snapshot.Frame);
        var applySnapshotStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var previousLevelName = _world.Level.Name;
        var previousMapAreaIndex = _world.Level.MapAreaIndex;
        var previousLocalPlayerId = _lastAppliedSnapshotLocalPlayerId;
        var wasAwaitingJoin = _world.LocalPlayerAwaitingJoin;
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

        var mapChanged = !string.Equals(previousLevelName, _world.Level.Name, StringComparison.OrdinalIgnoreCase)
            || previousMapAreaIndex != _world.Level.MapAreaIndex;
        var currentLocalPlayerId = GetResolvedLocalPlayerId();
        var localPlayerIdentityChanged = previousLocalPlayerId.HasValue
            && previousLocalPlayerId.Value != currentLocalPlayerId;
        if (isServerFullSnapshot || mapChanged || localPlayerIdentityChanged)
        {
            ResetAndSeedSnapshotPresentationHistories(snapshot);
        }
        else
        {
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

        _lastAppliedSnapshotLocalPlayerId = currentLocalPlayerId;
        ObserveAppliedNetworkWorldSnapshot(snapshot, isServerFullSnapshot);
        ProcessOnlineCivvieMoneyTrailPresentation(snapshot);

        if (!_classSelectOpen)
        {
            _pendingClassSelectTeam = null;
        }
        var reconcileStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        _networkClient.AcknowledgeProcessedInput(snapshot.LastProcessedInputSequence);
        ReconcileLocalPrediction(snapshot.LastProcessedInputSequence);
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
            return;
        }

        var currentVisibleEnemySpyIds = new HashSet<int>();
        var explicitDeathOrRemovalSpyIds = new HashSet<int>(snapshot.RemovedPlayerIds);
        for (var playerIndex = 0; playerIndex < snapshot.Players.Count; playerIndex += 1)
        {
            var player = snapshot.Players[playerIndex];
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
                continue;
            }

            if (IsSpyHiddenFromLocalViewer(player.PlayerId, (PlayerTeam)player.Team, player.X))
            {
                continue;
            }

            currentVisibleEnemySpyIds.Add(player.PlayerId);
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

            SpawnFrozenSpyVisual(lastSpyId);
        }

        _lastVisibleEnemySpyIds.Clear();
        foreach (var spyId in currentVisibleEnemySpyIds)
        {
            _lastVisibleEnemySpyIds.Add(spyId);
        }
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
