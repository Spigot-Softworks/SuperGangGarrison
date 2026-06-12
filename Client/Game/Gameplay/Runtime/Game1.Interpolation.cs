#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int SnapshotStateHistoryLimit = 96;
    private const int MaxQueuedAuthoritativeSnapshots = 4;
    private const float RemotePlayerTeleportSnapDistance = 128f;
    private const float RemotePlayerExtrapolationDurationSeconds = 0.07f;
    private const float LocalPlayerMinimumInterpolationBackTimeSeconds = 0.050f;
    private const float LocalPlayerMaximumInterpolationBackTimeSeconds = 0.180f;
    private const float RemotePlayerMinimumInterpolationBackTimeSeconds = 0.050f;
    private const float RemotePlayerMaximumInterpolationBackTimeSeconds = 0.280f;
    private const float ProjectileMinimumInterpolationBackTimeSeconds = 0.075f;
    private const float ProjectileMaximumInterpolationBackTimeSeconds = 0.220f;
    private const float ReplayMinimumInterpolationBackTimeSeconds = 0.02f;
    private const float ReplayMaximumInterpolationBackTimeSeconds = 0.06f;
    private const float OfflineInterpolationTeleportSnapDistance = 128f;
    private const float SnapshotHistoryRetentionSeconds = 0.5f;
    private const float StaleEntitySnapshotHistoryPruneSeconds = 1.0f;
    private const float StaleRemotePlayerSnapshotHistoryPruneSeconds = 1.0f;
    private const float MaximumLocalProjectileInterpolationDistance = 256f;
    private const int NetworkInterpolationWarmupSnapshotCount = 4;
    private const float NetworkInterpolationWarmupSeconds = 0.35f;
    private const int NetworkWorldWarmupMinimumAppliedSnapshotsAfterFull = 2;
    private const float NetworkWorldWarmupFreshPlayerHistorySeconds = 0.25f;
    // Projectiles are updated every few ticks - enable extrapolation to smooth between updates
    // This prevents jittering when the camera/player moves around projectiles
    private static readonly float ProjectileInterpolationExtrapolationCeilingSeconds = 0.30f;
    private const int ExpectedProjectileUpdateIntervalTicks = 1;

    private int GetPlayerStateKey(PlayerEntity player)
    {
        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            return _localPlayerSnapshotEntityId ?? player.Id;
        }

        return player.Id;
    }

    private readonly Dictionary<int, Vector2> _interpolatedEntityPositions = new();
    private readonly Dictionary<PlayerTeam, Vector2> _interpolatedIntelPositions = new();
    private readonly Dictionary<int, InterpolationTrack> _entityInterpolationTracks = new();
    private readonly Dictionary<PlayerTeam, InterpolationTrack> _intelInterpolationTracks = new();
    private readonly Dictionary<int, List<EntitySnapshotSample>> _entitySnapshotHistories = new();
    private readonly Dictionary<int, NetworkDiagnosticEntityInterpolationKind> _entitySnapshotHistoryKinds = new();
    private readonly Dictionary<PlayerTeam, List<EntitySnapshotSample>> _intelSnapshotHistories = new();
    private readonly Dictionary<int, List<PlayerSnapshotSample>> _remotePlayerSnapshotHistories = new();
    private readonly HashSet<int> _activeInterpolatedEntityIds = new();
    private readonly List<int> _staleInterpolatedEntityIds = new();
    private readonly Dictionary<ulong, SnapshotBaselineState> _snapshotStatesByFrame = new();
    private readonly Queue<ulong> _snapshotStateFrameOrder = new();
    private readonly Queue<SnapshotMessage> _queuedAuthoritativeSnapshots = new();
    private readonly HashSet<ulong> _authoritativeFullSnapshotFrames = new();
    private readonly Stopwatch _networkInterpolationClock = Stopwatch.StartNew();
    private double _networkInterpolationClockSeconds;
    private float _networkSnapshotInterpolationDurationSeconds = 1f / SimulationConfig.DefaultTicksPerSecond;
    private float _smoothedSnapshotIntervalSeconds = 1f / SimulationConfig.DefaultTicksPerSecond;
    private float _smoothedSnapshotJitterSeconds;
    private float _localPlayerInterpolationBackTimeSeconds = LocalPlayerMinimumInterpolationBackTimeSeconds;
    private float _remotePlayerInterpolationBackTimeSeconds = RemotePlayerMinimumInterpolationBackTimeSeconds;
    private float _projectileInterpolationBackTimeSeconds = ProjectileMinimumInterpolationBackTimeSeconds;
    private double _localPlayerRenderTimeSeconds;
    private double _remotePlayerRenderTimeSeconds;
    private double _lastLocalPlayerRenderTimeClockSeconds = -1d;
    private double _lastRemotePlayerRenderTimeClockSeconds = -1d;
    private double _lastSnapshotReceivedTimeSeconds = -1d;
    private double _latestSnapshotServerTimeSeconds = -1d;
    private double _latestSnapshotReceivedClockSeconds = -1d;
    private double _lastPredictedRenderSmoothingTimeSeconds = -1d;
    private bool _hasReceivedSnapshot;
    private bool _hasLocalPlayerRenderTime;
    private bool _hasRemotePlayerRenderTime;
    private ulong _lastAppliedSnapshotFrame;
    private ulong _lastBufferedSnapshotFrame;
    private int? _lastAppliedSnapshotLocalPlayerId;
    private int _networkInterpolationWarmupSnapshotsRemaining;
    private double _networkInterpolationWarmupUntilClockSeconds = -1d;
    private bool _networkWorldWarmupActive;
    private bool _networkWorldWarmupFullSnapshotApplied;
    private int _networkWorldWarmupAppliedSnapshotsAfterFull;
    private double _networkWorldWarmupStartedClockSeconds = -1d;

    private bool IsPositionSmoothingActive()
    {
        return NetworkInterpolationPolicy.IsSnapshotInterpolationActive(
            _networkClient.IsConnected,
            _positionSmoothingEnabled,
            _networkClient.IsReplayConnection);
    }

    private float GetMinimumRemotePlayerInterpolationBackTimeSeconds()
    {
        return _networkClient.IsReplayConnection
            ? ReplayMinimumInterpolationBackTimeSeconds
            : RemotePlayerMinimumInterpolationBackTimeSeconds;
    }

    private float GetMaximumRemotePlayerInterpolationBackTimeSeconds()
    {
        return _networkClient.IsReplayConnection
            ? ReplayMaximumInterpolationBackTimeSeconds
            : RemotePlayerMaximumInterpolationBackTimeSeconds;
    }

    private float GetMinimumLocalPlayerInterpolationBackTimeSeconds()
    {
        return _networkClient.IsReplayConnection
            ? ReplayMinimumInterpolationBackTimeSeconds
            : LocalPlayerMinimumInterpolationBackTimeSeconds;
    }

    private float GetMaximumLocalPlayerInterpolationBackTimeSeconds()
    {
        return _networkClient.IsReplayConnection
            ? ReplayMaximumInterpolationBackTimeSeconds
            : LocalPlayerMaximumInterpolationBackTimeSeconds;
    }

    private Vector2 GetRenderPosition(int entityId, float x, float y, bool allowInterpolation = true)
    {
        if (!allowInterpolation)
        {
            return new Vector2(x, y);
        }

        if (!_networkClient.IsConnected)
        {
            return _interpolatedEntityPositions.GetValueOrDefault(entityId, new Vector2(x, y));
        }

        if (_entityInterpolationTracks.TryGetValue(entityId, out var track))
        {
            return EvaluateInterpolationTrack(track);
        }

        if (_entitySnapshotHistories.ContainsKey(entityId)
            || _remotePlayerSnapshotHistories.ContainsKey(entityId))
        {
            return _interpolatedEntityPositions.GetValueOrDefault(entityId, new Vector2(x, y));
        }

        if (!_networkClient.IsReplayConnection && IsLocallyAdvancedProjectileEntity(entityId))
        {
            return GetLocallyAdvancedProjectileRenderPosition(entityId, x, y);
        }

        if (IsPositionSmoothingActive())
        {
            return _interpolatedEntityPositions.GetValueOrDefault(entityId, new Vector2(x, y));
        }

        return new Vector2(x, y);
    }

    private bool IsLocallyAdvancedProjectileEntity(int entityId)
    {
        if (!_world.Entities.TryGetValue(entityId, out var entity))
        {
            return false;
        }

        return entity is ShotProjectileEntity
            or BubbleProjectileEntity
            or BladeProjectileEntity
            or NeedleProjectileEntity
            or RevolverProjectileEntity
            or FlameProjectileEntity
            or FlareProjectileEntity
            or RocketProjectileEntity
            or MineProjectileEntity
            or GrenadeProjectileEntity;
    }

    private Vector2 GetLocallyAdvancedProjectileRenderPosition(int entityId, float x, float y)
    {
        if (!_world.Entities.TryGetValue(entityId, out var entity))
        {
            return new Vector2(x, y);
        }

        return entity switch
        {
            ShotProjectileEntity shot => InterpolateLocalProjectilePosition(shot.PreviousX, shot.PreviousY, shot.X, shot.Y),
            BubbleProjectileEntity bubble => InterpolateLocalProjectilePosition(bubble.PreviousX, bubble.PreviousY, bubble.X, bubble.Y),
            BladeProjectileEntity blade => InterpolateLocalProjectilePosition(blade.PreviousX, blade.PreviousY, blade.X, blade.Y),
            NeedleProjectileEntity needle => InterpolateLocalProjectilePosition(needle.PreviousX, needle.PreviousY, needle.X, needle.Y),
            RevolverProjectileEntity shot => InterpolateLocalProjectilePosition(shot.PreviousX, shot.PreviousY, shot.X, shot.Y),
            FlameProjectileEntity flame => InterpolateLocalProjectilePosition(flame.PreviousX, flame.PreviousY, flame.X, flame.Y),
            FlareProjectileEntity flare => InterpolateLocalProjectilePosition(flare.PreviousX, flare.PreviousY, flare.X, flare.Y),
            RocketProjectileEntity rocket => InterpolateLocalProjectilePosition(rocket.PreviousX, rocket.PreviousY, rocket.X, rocket.Y),
            MineProjectileEntity mine => InterpolateLocalProjectilePosition(mine.PreviousX, mine.PreviousY, mine.X, mine.Y),
            GrenadeProjectileEntity grenade => InterpolateLocalProjectilePosition(grenade.PreviousX, grenade.PreviousY, grenade.X, grenade.Y),
            _ => new Vector2(x, y),
        };
    }

    private Vector2 InterpolateLocalProjectilePosition(float previousX, float previousY, float x, float y)
    {
        var current = new Vector2(x, y);
        if (!HasUsableLocalProjectilePreviousPosition(previousX, previousY, x, y))
        {
            return current;
        }

        return Vector2.Lerp(new Vector2(previousX, previousY), current, _simulator.InterpolationAlpha);
    }

    private static bool HasUsableLocalProjectilePreviousPosition(float previousX, float previousY, float x, float y)
    {
        if (!float.IsFinite(previousX)
            || !float.IsFinite(previousY)
            || !float.IsFinite(x)
            || !float.IsFinite(y))
        {
            return false;
        }

        var deltaX = x - previousX;
        var deltaY = y - previousY;
        return (deltaX * deltaX) + (deltaY * deltaY)
            <= MaximumLocalProjectileInterpolationDistance * MaximumLocalProjectileInterpolationDistance;
    }

    private Vector2 GetRenderPosition(PlayerEntity player, bool allowInterpolation = true)
    {
        if (_networkClient.IsConnected && ReferenceEquals(player, _world.LocalPlayer))
        {
            if (!IsPositionSmoothingActive())
            {
                return GetRenderPosition(GetResolvedLocalPlayerId(), player.X, player.Y, allowInterpolation);
            }

            if (_hasPredictedLocalPlayerPosition)
            {
                if (_hasSmoothedLocalPlayerRenderPosition)
                {
                    return _smoothedLocalPlayerRenderPosition;
                }

                return _predictedLocalPlayerPosition;
            }

            return GetRenderPosition(GetResolvedLocalPlayerId(), player.X, player.Y, allowInterpolation);
        }

        return GetRenderPosition(player.Id, player.X, player.Y, allowInterpolation);
    }

    private Vector2 GetRenderAimWorldPosition(PlayerEntity player)
    {
        if (!_networkClient.IsConnected)
        {
            return new Vector2(player.AimWorldX, player.AimWorldY);
        }

        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            if (_hasLatestLocalAimWorldPosition)
            {
                return new Vector2(_latestLocalAimWorldX, _latestLocalAimWorldY);
            }

            return new Vector2(player.AimWorldX, player.AimWorldY);
        }

        if (!IsPositionSmoothingActive())
        {
            return new Vector2(player.AimWorldX, player.AimWorldY);
        }

        if (_remotePlayerSnapshotHistories.TryGetValue(player.Id, out var history) && history.Count > 0)
        {
            return EvaluateRemotePlayerAimHistory(history, _remotePlayerRenderTimeSeconds);
        }

        return new Vector2(player.AimWorldX, player.AimWorldY);
    }

    private Vector2 GetRenderIntelPosition(TeamIntelligenceState intelState)
    {
        if (!_networkClient.IsConnected)
        {
            return _interpolatedIntelPositions.GetValueOrDefault(
                intelState.Team,
                new Vector2(intelState.X, intelState.Y));
        }

        return _interpolatedIntelPositions.GetValueOrDefault(intelState.Team, new Vector2(intelState.X, intelState.Y));
    }

    private readonly record struct InterpolationTrack(
        Vector2 Start,
        Vector2 Target,
        double StartTimeSeconds,
        float DurationSeconds,
        Vector2 Velocity,
        float ExtrapolationDurationSeconds,
        float MaxExtrapolationDistance);

    private readonly record struct PlayerSnapshotSample(
        Vector2 Position,
        Vector2 Velocity,
        Vector2 AimWorldPosition,
        double TimeSeconds,
        PlayerTeam Team,
        PlayerClass ClassId,
        bool IsAlive);

    private readonly record struct EntitySnapshotSample(
        Vector2 Position,
        Vector2 Velocity,
        double TimeSeconds,
        float ExtrapolationDurationSeconds,
        float MaxExtrapolationDistance);

    private void ResetSnapshotStateHistory()
    {
        _snapshotStatesByFrame.Clear();
        _snapshotStateFrameOrder.Clear();
        _queuedAuthoritativeSnapshots.Clear();
        _authoritativeFullSnapshotFrames.Clear();
        _lastBufferedSnapshotFrame = 0;
    }

    private void RememberSnapshotState(SnapshotMessage snapshot)
    {
        var baseline = SnapshotBaselineState.FromSnapshot(snapshot);
        if (!_snapshotStatesByFrame.ContainsKey(snapshot.Frame))
        {
            _snapshotStateFrameOrder.Enqueue(snapshot.Frame);
        }

        _snapshotStatesByFrame[snapshot.Frame] = baseline;
        while (_snapshotStateFrameOrder.Count > SnapshotStateHistoryLimit)
        {
            _snapshotStatesByFrame.Remove(_snapshotStateFrameOrder.Dequeue());
        }
    }

    private bool TryGetSnapshotState(ulong frame, out SnapshotBaselineState snapshot)
    {
        return _snapshotStatesByFrame.TryGetValue(frame, out snapshot!);
    }
}
