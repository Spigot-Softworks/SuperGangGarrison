#nullable enable

using Microsoft.Xna.Framework;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>Tracks offline-only presentation boundaries and smoothing rules.</summary>
internal sealed class OfflinePresentationController
{
    private readonly Dictionary<int, int> _playerDeathCounts = new();
    private object? _worldIdentity;
    private object? _levelIdentity;
    private int _localPlayerId;
    private bool _hasObservedFrame;
    private bool _wasConnected;
    private bool _wasMatchEnded;
    private readonly Dictionary<int, PlayerTickStart> _playerTickStarts = new();

    public bool ObserveFrame(
        bool isConnected,
        object world,
        object level,
        int localPlayerId,
        bool isMatchEnded)
    {
        var roundStateChanged = !isConnected
            && _hasObservedFrame
            && !_wasConnected
            && _wasMatchEnded != isMatchEnded;
        var sessionChanged = !_hasObservedFrame
            || _wasConnected != isConnected
            || !ReferenceEquals(_worldIdentity, world)
            || !ReferenceEquals(_levelIdentity, level)
            || _localPlayerId != localPlayerId
            || roundStateChanged;

        if (sessionChanged)
        {
            _playerTickStarts.Clear();
        }

        _hasObservedFrame = true;
        _wasConnected = isConnected;
        _worldIdentity = world;
        _levelIdentity = level;
        _localPlayerId = localPlayerId;
        _wasMatchEnded = isMatchEnded;

        if (isConnected || !sessionChanged)
        {
            return false;
        }

        _playerDeathCounts.Clear();
        return true;
    }

    public bool HasPlayerRespawned(int playerId, int deathCount)
    {
        if (!_playerDeathCounts.TryGetValue(playerId, out var previousDeathCount))
        {
            _playerDeathCounts[playerId] = deathCount;
            return false;
        }

        _playerDeathCounts[playerId] = deathCount;
        return previousDeathCount != deathCount;
    }

    public static bool ShouldInterpolatePlayer(bool isLocalPlayer) => !isLocalPlayer;

    /// <summary>Starts a new capture pass; players not captured again are drawn at their tick sample.</summary>
    public void BeginPlayerTickCapture()
    {
        _playerTickStarts.Clear();
    }

    /// <summary>Records a player's sample immediately before a fixed simulation tick.</summary>
    public void CapturePlayerTickStart(int playerKey, Vector2 position, int deathCount, bool isAlive)
    {
        if (!isAlive)
        {
            _playerTickStarts.Remove(playerKey);
            return;
        }

        _playerTickStarts[playerKey] = new PlayerTickStart(position, deathCount);
    }

    /// <summary>Single-player convenience used by callers and tests that only track the local player.</summary>
    public void CaptureLocalPlayerTickStart(int playerId, Vector2 position, int deathCount, bool isAlive)
    {
        CapturePlayerTickStart(playerId, position, deathCount, isAlive);
    }

    /// <summary>
    /// Blends a player across the latest fixed tick using the simulator's own
    /// accumulator phase. Unlike a chasing track restarted whenever a new tick
    /// is observed, this advances by an even amount every frame regardless of
    /// frame-time jitter, and adds at most one tick of presentation delay.
    /// </summary>
    public Vector2 GetPlayerRenderPosition(
        int playerKey,
        Vector2 tickEndPosition,
        int deathCount,
        Vector2 velocity,
        float interpolationAlpha)
    {
        if (!_playerTickStarts.TryGetValue(playerKey, out var tickStart)
            || tickStart.DeathCount != deathCount
            || ShouldSnapStoppedPlayer(tickStart.Position, tickEndPosition, velocity))
        {
            return tickEndPosition;
        }

        return LocalPlayerRenderInterpolation.Interpolate(
            tickStart.Position,
            tickEndPosition,
            interpolationAlpha);
    }

    public Vector2 GetLocalPlayerRenderPosition(int playerId, Vector2 tickEndPosition, int deathCount, float interpolationAlpha)
    {
        return GetPlayerRenderPosition(playerId, tickEndPosition, deathCount, Vector2.One, interpolationAlpha);
    }

    public static bool ShouldSnapStoppedPlayer(
        Vector2 currentRenderPosition,
        Vector2 targetPosition,
        Vector2 velocity)
    {
        const float stoppedTeleportSnapDistance = 48f;
        return velocity.LengthSquared() <= 0.01f
            && Vector2.DistanceSquared(currentRenderPosition, targetPosition)
                >= stoppedTeleportSnapDistance * stoppedTeleportSnapDistance;
    }

    public static Vector2 EvaluateTrack(
        Vector2 start,
        Vector2 target,
        double startTimeSeconds,
        float durationSeconds,
        double currentTimeSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return target;
        }

        var alpha = float.Clamp(
            (float)((currentTimeSeconds - startTimeSeconds) / durationSeconds),
            0f,
            1f);
        return Vector2.Lerp(start, target, alpha);
    }

    private readonly record struct PlayerTickStart(Vector2 Position, int DeathCount);
}
