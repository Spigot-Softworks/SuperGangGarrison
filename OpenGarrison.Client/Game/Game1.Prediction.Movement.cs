#nullable enable

using Microsoft.Xna.Framework;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int MaxPredictedCollisionResolutionIterations = 10;
    private const float PredictedCollisionMoveStep = 1f;
    private const float PredictedStepUpHeight = 6f;
    private const float PredictedStepSupportEpsilon = 2f;
    private const float PredictedRenderCorrectionTeleportSnapDistance = 128f;
    private const float PredictedRenderCorrectionIdleCatchUpRate = 10f;
    private const float PredictedRenderCorrectionActiveCatchUpRate = 16f;
    private const float PredictedRenderCorrectionDistanceRateScale = 2.5f;
    private const float PredictedRenderCorrectionMaxRateBonus = 120f;

    private float GetPredictedMovementScale(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (_predictedLocalActionState.IsHeavyEating || player.IsTaunting)
        {
            return 0f;
        }

        if (player.ClassId == PlayerClass.Spy && IsPredictedSpyBackstabAnimating())
        {
            return 0f;
        }

        if (player.ClassId == PlayerClass.Sniper && _predictedLocalActionState.IsSniperScoped)
        {
            return PlayerEntity.SniperScopedMoveScale;
        }

        if (player.ClassId == PlayerClass.Heavy && input.FirePrimary)
        {
            return PlayerEntity.HeavyPrimaryMoveScale;
        }

        return 1f;
    }

    private float GetPredictedJumpScale(PlayerEntity player)
    {
        if (player.ClassId == PlayerClass.Sniper && _predictedLocalActionState.IsSniperScoped)
        {
            return PlayerEntity.SniperScopedJumpScale;
        }

        if (player.ClassId == PlayerClass.Spy && IsPredictedSpyBackstabAnimating())
        {
            return 0f;
        }

        return 1f;
    }

    private void UpdateLocalPredictedRenderPosition()
    {
        if (!_networkClient.IsConnected || !_hasPredictedLocalPlayerPosition || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            _hasSmoothedLocalPlayerRenderPosition = false;
            _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _lastPredictedRenderSmoothingTimeSeconds = -1d;
            return;
        }

        if (!_hasSmoothedLocalPlayerRenderPosition)
        {
            _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _smoothedLocalPlayerRenderPosition = _predictedLocalPlayerPosition;
            _hasSmoothedLocalPlayerRenderPosition = true;
            _lastPredictedRenderSmoothingTimeSeconds = _networkInterpolationClockSeconds;
            RecordPredictedRenderCorrection(0f, hardSnap: false);
            return;
        }

        if (_lastPredictedRenderSmoothingTimeSeconds < 0d)
        {
            _lastPredictedRenderSmoothingTimeSeconds = _networkInterpolationClockSeconds;
            _smoothedLocalPlayerRenderPosition = _predictedLocalPlayerPosition + _predictedLocalPlayerRenderCorrectionOffset;
            return;
        }

        var deltaSeconds = (float)Math.Clamp(
            _networkInterpolationClockSeconds - _lastPredictedRenderSmoothingTimeSeconds,
            0d,
            0.05d);
        _lastPredictedRenderSmoothingTimeSeconds = _networkInterpolationClockSeconds;

        var distance = _predictedLocalPlayerRenderCorrectionOffset.Length();
        if (distance <= 0.01f)
        {
            _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _smoothedLocalPlayerRenderPosition = _predictedLocalPlayerPosition;
            RecordPredictedRenderCorrection(0f, hardSnap: false);
            return;
        }

        if (distance >= PredictedRenderCorrectionTeleportSnapDistance)
        {
            RecordPredictedRenderCorrection(distance, hardSnap: true);
            _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _smoothedLocalPlayerRenderPosition = _predictedLocalPlayerPosition;
            return;
        }

        if (deltaSeconds <= 0f)
        {
            _smoothedLocalPlayerRenderPosition = _predictedLocalPlayerPosition + _predictedLocalPlayerRenderCorrectionOffset;
            RecordPredictedRenderCorrection(distance, hardSnap: false);
            return;
        }

        var isActivelyMoving = _latestPredictedLocalInput.Left
            || _latestPredictedLocalInput.Right
            || _latestPredictedLocalInput.Up
            || MathF.Abs(_predictedLocalPlayerVelocity.X) > 20f
            || MathF.Abs(_predictedLocalPlayerVelocity.Y) > 20f;
        var catchUpRate = isActivelyMoving
            ? PredictedRenderCorrectionActiveCatchUpRate
            : PredictedRenderCorrectionIdleCatchUpRate;
        catchUpRate += MathF.Min(distance * PredictedRenderCorrectionDistanceRateScale, PredictedRenderCorrectionMaxRateBonus);

        var decayFactor = MathF.Exp(-catchUpRate * deltaSeconds);
        _predictedLocalPlayerRenderCorrectionOffset *= decayFactor;
        if (_predictedLocalPlayerRenderCorrectionOffset.LengthSquared() <= 0.0001f)
        {
            _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
        }

        _smoothedLocalPlayerRenderPosition = _predictedLocalPlayerPosition + _predictedLocalPlayerRenderCorrectionOffset;
        RecordPredictedRenderCorrection(_predictedLocalPlayerRenderCorrectionOffset.Length(), hardSnap: false);
    }

    private void ApplyPredictedMovementStep(PredictedLocalInput predictedInput)
    {
        var player = _world.LocalPlayer;
        var dt = (float)_config.FixedDeltaSeconds;
        var moveScale = GetPredictedMovementScale(player, predictedInput.Input);
        var maxRunSpeed = player.MaxRunSpeed * moveScale;
        var groundAcceleration = player.GroundAcceleration * moveScale;
        var groundDeceleration = player.GroundDeceleration * moveScale;
        var canMove = !_predictedLocalActionState.IsHeavyEating
            && !player.IsTaunting
            && !IsPredictedSpyBackstabAnimating();
        if (IsPredictedSpyBackstabAnimating())
        {
            _predictedLocalPlayerVelocity.X = 0f;
        }

        var horizontalDirection = 0f;
        if (canMove && predictedInput.Input.Left)
        {
            horizontalDirection -= 1f;
        }

        if (canMove && predictedInput.Input.Right)
        {
            horizontalDirection += 1f;
        }

        if (horizontalDirection != 0f)
        {
            _predictedLocalPlayerVelocity.X += horizontalDirection * groundAcceleration * dt;
            _predictedLocalPlayerVelocity.X = float.Clamp(_predictedLocalPlayerVelocity.X, -maxRunSpeed, maxRunSpeed);
        }
        else
        {
            var deceleration = groundDeceleration * dt;
            if (_predictedLocalPlayerVelocity.X > 0f)
            {
                _predictedLocalPlayerVelocity.X = float.Max(0f, _predictedLocalPlayerVelocity.X - deceleration);
            }
            else if (_predictedLocalPlayerVelocity.X < 0f)
            {
                _predictedLocalPlayerVelocity.X = float.Min(0f, _predictedLocalPlayerVelocity.X + deceleration);
            }
        }

        if (canMove && predictedInput.JumpPressed)
        {
            TryPredictedJump(player);
        }

        _predictedLocalPlayerVelocity.Y += player.Gravity * dt;
        MovePredictedWithCollisions(player, _predictedLocalPlayerVelocity.X * dt, _predictedLocalPlayerVelocity.Y * dt);

        var clampedX = float.Clamp(
            _predictedLocalPlayerPosition.X,
            -player.CollisionLeftOffset,
            _world.Bounds.Width - player.CollisionRightOffset);
        if (clampedX != _predictedLocalPlayerPosition.X)
        {
            _predictedLocalPlayerPosition.X = clampedX;
            _predictedLocalPlayerVelocity.X = 0f;
        }

        var clampedY = float.Clamp(
            _predictedLocalPlayerPosition.Y,
            -player.CollisionTopOffset,
            _world.Bounds.Height - player.CollisionBottomOffset);
        if (clampedY != _predictedLocalPlayerPosition.Y)
        {
            if (_predictedLocalPlayerVelocity.Y > 0f)
            {
                _predictedLocalPlayerGrounded = true;
                _predictedLocalPlayerRemainingAirJumps = player.MaxAirJumps;
            }

            _predictedLocalPlayerPosition.Y = clampedY;
            _predictedLocalPlayerVelocity.Y = 0f;
        }
    }

    private void TryPredictedJump(PlayerEntity player)
    {
        var jumpScale = GetPredictedJumpScale(player);
        if (jumpScale <= 0f)
        {
            return;
        }

        if (_predictedLocalPlayerGrounded)
        {
            _predictedLocalPlayerVelocity.Y = -player.JumpSpeed * jumpScale;
            _predictedLocalPlayerGrounded = false;
            return;
        }

        if (_predictedLocalPlayerRemainingAirJumps <= 0)
        {
            return;
        }

        _predictedLocalPlayerVelocity.Y = -player.JumpSpeed * jumpScale;
        _predictedLocalPlayerRemainingAirJumps -= 1;
    }

    private void MovePredictedWithCollisions(PlayerEntity player, float moveX, float moveY)
    {
        if (!float.IsFinite(moveX) || !float.IsFinite(moveY))
        {
            _predictedLocalPlayerVelocity = Vector2.Zero;
            return;
        }

        NudgePredictedOutsideBlockingGeometry(player);
        var remainingX = moveX;
        var remainingY = moveY;
        _predictedLocalPlayerGrounded = false;

        for (var iteration = 0; iteration < MaxPredictedCollisionResolutionIterations && (MathF.Abs(remainingX) >= 1f || MathF.Abs(remainingY) >= 1f); iteration += 1)
        {
            var previousPosition = _predictedLocalPlayerPosition;
            MovePredictedContact(player, remainingX, remainingY);
            remainingX -= _predictedLocalPlayerPosition.X - previousPosition.X;
            remainingY -= _predictedLocalPlayerPosition.Y - previousPosition.Y;

            var collisionRectified = false;
            if (remainingY != 0f && !CanOccupyPredicted(player, _predictedLocalPlayerPosition.X, _predictedLocalPlayerPosition.Y + MathF.Sign(remainingY)))
            {
                if (remainingY > 0f)
                {
                    _predictedLocalPlayerGrounded = true;
                    _predictedLocalPlayerRemainingAirJumps = player.MaxAirJumps;
                }

                _predictedLocalPlayerVelocity.Y = 0f;
                remainingY = 0f;
                collisionRectified = true;
            }

            if (remainingX != 0f && !CanOccupyPredicted(player, _predictedLocalPlayerPosition.X + MathF.Sign(remainingX), _predictedLocalPlayerPosition.Y))
            {
                if (TryStepUpPredicted(player, MathF.Sign(remainingX)))
                {
                    collisionRectified = true;
                }
                else
                {
                    _predictedLocalPlayerVelocity.X = 0f;
                    remainingX = 0f;
                    collisionRectified = true;
                }
            }

            if (!collisionRectified && (MathF.Abs(remainingX) >= 1f || MathF.Abs(remainingY) >= 1f))
            {
                _predictedLocalPlayerVelocity.Y = 0f;
                remainingY = 0f;
            }
        }

        TryApplyPredictedResidualMovement(player, remainingX, remainingY);
        RefreshPredictedGroundSupport(player);
    }

    private void TryApplyPredictedResidualMovement(PlayerEntity player, float remainingX, float remainingY)
    {
        if (MathF.Abs(remainingX) <= 0f && MathF.Abs(remainingY) <= 0f)
        {
            return;
        }

        if (CanOccupyPredicted(player, _predictedLocalPlayerPosition.X + remainingX, _predictedLocalPlayerPosition.Y + remainingY))
        {
            _predictedLocalPlayerPosition.X += remainingX;
            _predictedLocalPlayerPosition.Y += remainingY;
            return;
        }

        if (MathF.Abs(remainingX) > 0f && MathF.Abs(remainingY) <= 0f && CanOccupyPredicted(player, _predictedLocalPlayerPosition.X + remainingX, _predictedLocalPlayerPosition.Y))
        {
            _predictedLocalPlayerPosition.X += remainingX;
            return;
        }

        if (MathF.Abs(remainingY) > 0f && MathF.Abs(remainingX) <= 0f && CanOccupyPredicted(player, _predictedLocalPlayerPosition.X, _predictedLocalPlayerPosition.Y + remainingY))
        {
            _predictedLocalPlayerPosition.Y += remainingY;
        }
    }

    private void RefreshPredictedGroundSupport(PlayerEntity player)
    {
        if (_predictedLocalPlayerVelocity.Y < 0f
            || !CanOccupyPredicted(player, _predictedLocalPlayerPosition.X, _predictedLocalPlayerPosition.Y))
        {
            return;
        }

        if (CanOccupyPredicted(player, _predictedLocalPlayerPosition.X, _predictedLocalPlayerPosition.Y + PredictedStepSupportEpsilon))
        {
            return;
        }

        _predictedLocalPlayerGrounded = true;
        _predictedLocalPlayerRemainingAirJumps = player.MaxAirJumps;
        _predictedLocalPlayerVelocity.Y = 0f;
    }

    private void MovePredictedContact(PlayerEntity player, float deltaX, float deltaY)
    {
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0f)
        {
            return;
        }

        var steps = Math.Max(1, (int)MathF.Ceiling(distance / PredictedCollisionMoveStep));
        var stepX = deltaX / steps;
        var stepY = deltaY / steps;
        for (var step = 0; step < steps; step += 1)
        {
            var nextX = _predictedLocalPlayerPosition.X + stepX;
            var nextY = _predictedLocalPlayerPosition.Y + stepY;
            if (!CanOccupyPredicted(player, nextX, nextY))
            {
                break;
            }

            _predictedLocalPlayerPosition = new Vector2(nextX, nextY);
        }
    }

    private void NudgePredictedOutsideBlockingGeometry(PlayerEntity player)
    {
        if (CanOccupyPredicted(player, _predictedLocalPlayerPosition.X, _predictedLocalPlayerPosition.Y))
        {
            return;
        }

        for (var offset = 1; offset <= 8; offset += 1)
        {
            if (CanOccupyPredicted(player, _predictedLocalPlayerPosition.X + offset, _predictedLocalPlayerPosition.Y))
            {
                _predictedLocalPlayerPosition.X += offset;
                return;
            }
        }

        for (var offset = 1; offset <= 16; offset += 1)
        {
            if (CanOccupyPredicted(player, _predictedLocalPlayerPosition.X - offset, _predictedLocalPlayerPosition.Y))
            {
                _predictedLocalPlayerPosition.X -= offset;
                return;
            }
        }
    }

    private bool CanOccupyPredicted(PlayerEntity player, float x, float y)
    {
        player.GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);

        foreach (var solid in _world.Level.Solids)
        {
            if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
            {
                return false;
            }
        }

        foreach (var gate in _world.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
        {
            if (left < gate.Right && right > gate.Left && top < gate.Bottom && bottom > gate.Top)
            {
                return false;
            }
        }

        foreach (var wall in _world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (left < wall.Right && right > wall.Left && top < wall.Bottom && bottom > wall.Top)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryStepUpPredicted(PlayerEntity player, float horizontalDirection)
    {
        if (horizontalDirection == 0f || _predictedLocalPlayerVelocity.X == 0f)
        {
            return false;
        }

        var obstacleTop = FindPredictedBlockingObstacleTop(player, _predictedLocalPlayerPosition.X + horizontalDirection, _predictedLocalPlayerPosition.Y);
        if (!obstacleTop.HasValue)
        {
            return false;
        }

        var bottom = _predictedLocalPlayerPosition.Y + player.CollisionBottomOffset;
        var stepDelta = bottom - obstacleTop.Value;
        if (stepDelta < 0f || stepDelta > PredictedStepUpHeight)
        {
            return false;
        }

        var targetY = _predictedLocalPlayerPosition.Y - stepDelta;
        if (!CanOccupyPredicted(player, _predictedLocalPlayerPosition.X, targetY))
        {
            return false;
        }

        _predictedLocalPlayerPosition.Y = targetY;
        return true;
    }

    private float? FindPredictedBlockingObstacleTop(PlayerEntity player, float x, float y)
    {
        player.GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);
        float? obstacleTop = null;

        foreach (var solid in _world.Level.Solids)
        {
            if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
            {
                obstacleTop = obstacleTop.HasValue ? MathF.Min(obstacleTop.Value, solid.Top) : solid.Top;
            }
        }

        foreach (var wall in _world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (left < wall.Right && right > wall.Left && top < wall.Bottom && bottom > wall.Top)
            {
                obstacleTop = obstacleTop.HasValue ? MathF.Min(obstacleTop.Value, wall.Top) : wall.Top;
            }
        }

        return obstacleTop;
    }
}
