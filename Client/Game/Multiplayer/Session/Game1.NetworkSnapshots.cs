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

    private bool TryHandleSnapshotMessage(
        SnapshotMessage snapshot,
        ref ulong latestBufferedSnapshotFrame,
        ref SnapshotMessage? latestResolvedSnapshot,
        ref Dictionary<ulong, SnapshotMessage>? resolvedBatchSnapshotsByFrame,
        ref List<SnapshotMessage>? resolvedBatchSnapshots)
    {
        if ((!string.Equals(snapshot.LevelName, _world.Level.Name, StringComparison.OrdinalIgnoreCase)
                || snapshot.MapAreaIndex != _world.Level.MapAreaIndex)
            && !CustomMapSyncService.EnsureMapAvailable(
                snapshot.LevelName,
                snapshot.IsCustomMap,
                snapshot.MapDownloadUrl,
                snapshot.MapContentHash,
                out var snapshotMapError))
        {
            ReturnToMainMenuWithNetworkStatus(snapshotMapError, $"custom map sync failed: {snapshotMapError}");
            return false;
        }

        if (snapshot.Frame <= latestBufferedSnapshotFrame)
        {
            RecordStaleSnapshot();
            return false;
        }

        SnapshotMessage? baselineSnapshot = null;
        if (snapshot.IsDelta && snapshot.BaselineFrame != 0
            && !(resolvedBatchSnapshotsByFrame?.TryGetValue(snapshot.BaselineFrame, out baselineSnapshot) ?? false)
            && !TryGetSnapshotState(snapshot.BaselineFrame, out baselineSnapshot))
        {
            RecordMissingBaselineSnapshot();
            AddNetworkConsoleLine($"snapshot {snapshot.Frame} missing baseline {snapshot.BaselineFrame}");
            return false;
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

        resolvedBatchSnapshotsByFrame ??= new Dictionary<ulong, SnapshotMessage>();
        resolvedBatchSnapshotsByFrame[resolvedSnapshot.Frame] = resolvedSnapshot;

        resolvedBatchSnapshots ??= new List<SnapshotMessage>();
        resolvedBatchSnapshots.Add(resolvedSnapshot);
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
            _pendingNetworkSoundEvents.Add(new WorldSoundEvent(
                soundEvent.SoundName,
                soundEvent.X,
                soundEvent.Y,
                soundEvent.EventId,
                soundEvent.SourceFrame));
        }
    }

    private void FinalizeResolvedSnapshotBatch(SnapshotMessage latestResolvedSnapshot, List<SnapshotMessage> resolvedBatchSnapshots)
    {
        UpdateSnapshotTiming(
            latestResolvedSnapshot.Frame,
            latestResolvedSnapshot.TickRate,
            resolvedBatchSnapshots.Count);
        for (var snapshotIndex = 0; snapshotIndex < resolvedBatchSnapshots.Count; snapshotIndex += 1)
        {
            var resolvedBatchSnapshot = resolvedBatchSnapshots[snapshotIndex];
            RememberSnapshotState(resolvedBatchSnapshot);
            CaptureRemoteInterpolationTargets(resolvedBatchSnapshot);
            EnqueueAuthoritativeSnapshot(resolvedBatchSnapshot);
        }

        _networkClient.AcknowledgeSnapshot(latestResolvedSnapshot.Frame);
    }

    private void EnqueueAuthoritativeSnapshot(SnapshotMessage snapshot)
    {
        if (snapshot.Frame <= _lastBufferedSnapshotFrame)
        {
            return;
        }

        _queuedAuthoritativeSnapshots.Enqueue(snapshot);
        _lastBufferedSnapshotFrame = snapshot.Frame;
        while (_queuedAuthoritativeSnapshots.Count > MaxQueuedAuthoritativeSnapshots)
        {
            _queuedAuthoritativeSnapshots.Dequeue();
        }
    }

    private void ApplyNextQueuedAuthoritativeSnapshot()
    {
        if (_queuedAuthoritativeSnapshots.Count == 0)
        {
            return;
        }

        var snapshot = _queuedAuthoritativeSnapshots.Dequeue();
        var applySnapshotStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var previousLevelName = _world.Level.Name;
        var previousMapAreaIndex = _world.Level.MapAreaIndex;
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

        CaptureSmoothingTrackForLocalPlayer(snapshot);
        DetectFrozenSpyVisualsForMissingEnemySpies(snapshot);
        _lastAppliedSnapshotFrame = snapshot.Frame;
        if (_queuedAuthoritativeSnapshots.Count == 0)
        {
            _lastBufferedSnapshotFrame = _lastAppliedSnapshotFrame;
        }

        if (_networkDiagnosticsEnabled)
        {
            RecordApplySnapshotDuration(GetDiagnosticsElapsedMilliseconds(applySnapshotStartTimestamp));
            RecordAppliedSnapshot();
        }

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
        if (!_networkClient.IsConnected || !_world.LocalPlayerAwaitingJoin || !_world.LocalPlayer.IsAlive)
        {
            return;
        }

        var localPlayerStateKey = GetResolvedLocalPlayerId();
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
