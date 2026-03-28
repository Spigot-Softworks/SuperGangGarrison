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
    private const float RemotePlayerExtrapolationDurationSeconds = 0.05f;
    private const float RemotePlayerMinimumInterpolationBackTimeSeconds = 0.12f;
    private const float RemotePlayerMaximumInterpolationBackTimeSeconds = 0.22f;
    private const float SnapshotHistoryRetentionSeconds = 0.5f;
    private const float ProjectileInterpolationExtrapolationCeilingSeconds = 0.25f;

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
    private readonly Dictionary<PlayerTeam, List<EntitySnapshotSample>> _intelSnapshotHistories = new();
    private readonly Dictionary<int, List<PlayerSnapshotSample>> _remotePlayerSnapshotHistories = new();
    private readonly HashSet<int> _activeInterpolatedEntityIds = new();
    private readonly List<int> _staleInterpolatedEntityIds = new();
    private readonly Dictionary<ulong, SnapshotMessage> _snapshotStatesByFrame = new();
    private readonly Queue<ulong> _snapshotStateFrameOrder = new();
    private readonly Queue<SnapshotMessage> _queuedAuthoritativeSnapshots = new();
    private readonly Stopwatch _networkInterpolationClock = Stopwatch.StartNew();
    private double _networkInterpolationClockSeconds;
    private float _networkSnapshotInterpolationDurationSeconds = 1f / SimulationConfig.DefaultTicksPerSecond;
    private float _smoothedSnapshotIntervalSeconds = 1f / SimulationConfig.DefaultTicksPerSecond;
    private float _smoothedSnapshotJitterSeconds;
    private float _remotePlayerInterpolationBackTimeSeconds = RemotePlayerMinimumInterpolationBackTimeSeconds;
    private double _remotePlayerRenderTimeSeconds;
    private double _lastRemotePlayerRenderTimeClockSeconds = -1d;
    private double _lastSnapshotReceivedTimeSeconds = -1d;
    private double _latestSnapshotServerTimeSeconds = -1d;
    private double _latestSnapshotReceivedClockSeconds = -1d;
    private double _lastPredictedRenderSmoothingTimeSeconds = -1d;
    private bool _hasReceivedSnapshot;
    private bool _hasRemotePlayerRenderTime;
    private ulong _lastAppliedSnapshotFrame;
    private ulong _lastBufferedSnapshotFrame;

    private Vector2 GetRenderPosition(int entityId, float x, float y, bool allowInterpolation = true)
    {
        if (!allowInterpolation || !_networkClient.IsConnected)
        {
            return new Vector2(x, y);
        }

        return _interpolatedEntityPositions.GetValueOrDefault(entityId, new Vector2(x, y));
    }

    private Vector2 GetRenderPosition(PlayerEntity player, bool allowInterpolation = true)
    {
        if (_networkClient.IsConnected && ReferenceEquals(player, _world.LocalPlayer) && _hasPredictedLocalPlayerPosition)
        {
            if (_hasSmoothedLocalPlayerRenderPosition)
            {
                return _smoothedLocalPlayerRenderPosition;
            }

            return _predictedLocalPlayerPosition;
        }

        if (_networkClient.IsConnected && !ReferenceEquals(player, _world.LocalPlayer))
        {
            return GetRenderPosition(player.Id, player.X, player.Y, allowInterpolation);
        }

        return GetRenderPosition(player.Id, player.X, player.Y, allowInterpolation);
    }

    private Vector2 GetRenderIntelPosition(TeamIntelligenceState intelState)
    {
        if (!_networkClient.IsConnected)
        {
            return new Vector2(intelState.X, intelState.Y);
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
        _lastBufferedSnapshotFrame = 0;
    }

    private void RememberSnapshotState(SnapshotMessage snapshot)
    {
        if (!_snapshotStatesByFrame.ContainsKey(snapshot.Frame))
        {
            _snapshotStateFrameOrder.Enqueue(snapshot.Frame);
        }

        _snapshotStatesByFrame[snapshot.Frame] = snapshot;
        while (_snapshotStateFrameOrder.Count > SnapshotStateHistoryLimit)
        {
            _snapshotStatesByFrame.Remove(_snapshotStateFrameOrder.Dequeue());
        }
    }

    private bool TryGetSnapshotState(ulong frame, out SnapshotMessage snapshot)
    {
        return _snapshotStatesByFrame.TryGetValue(frame, out snapshot!);
    }
}
