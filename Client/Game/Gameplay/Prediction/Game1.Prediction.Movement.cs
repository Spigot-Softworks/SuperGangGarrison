#nullable enable

using Microsoft.Xna.Framework;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float PredictedRenderCorrectionTeleportSnapDistance = 128f;
    private const float PredictedRenderCorrectionIdleCatchUpRate = 10f;
    private const float PredictedRenderCorrectionActiveCatchUpRate = 16f;
    private const float PredictedRenderCorrectionDistanceRateScale = 2.5f;
    private const float PredictedRenderCorrectionMaxRateBonus = 120f;
    private const float PredictedRenderMaxLeadTicks = 1.25f;
    private const float PredictedRenderIdleCatchUpRate = 28f;

    private void UpdateLocalPredictedRenderPosition()
    {
        if (!CanUseLocalPrediction() || !_hasPredictedLocalPlayerPosition || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            ClearLocalPredictionState(clearPendingInputs: false);
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
        var targetRenderPosition = _predictedLocalPlayerPosition + _predictedLocalPlayerRenderCorrectionOffset;
        var renderDistance = Vector2.Distance(_smoothedLocalPlayerRenderPosition, targetRenderPosition);
        if (renderDistance >= PredictedRenderCorrectionTeleportSnapDistance)
        {
            RecordPredictedRenderCorrection(distance, hardSnap: true);
            _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _smoothedLocalPlayerRenderPosition = _predictedLocalPlayerPosition;
            return;
        }

        if (distance <= 0.01f)
        {
            _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            targetRenderPosition = _predictedLocalPlayerPosition;
            distance = 0f;
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

        targetRenderPosition = _predictedLocalPlayerPosition + _predictedLocalPlayerRenderCorrectionOffset;
        _smoothedLocalPlayerRenderPosition = AdvancePredictedLocalPlayerRenderPosition(
            _smoothedLocalPlayerRenderPosition,
            targetRenderPosition,
            _predictedLocalPlayerVelocity,
            deltaSeconds);
        RecordPredictedRenderCorrection(_predictedLocalPlayerRenderCorrectionOffset.Length(), hardSnap: false);
    }

    private Vector2 AdvancePredictedLocalPlayerRenderPosition(
        Vector2 current,
        Vector2 target,
        Vector2 velocity,
        float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return current;
        }

        var maxHorizontalLead = MathF.Max(
            1f,
            MathF.Abs(velocity.X) * (float)_config.FixedDeltaSeconds * PredictedRenderMaxLeadTicks);
        var nextX = current.X;
        if (MathF.Abs(velocity.X) > 0.01f)
        {
            nextX += velocity.X * deltaSeconds;
            var leadX = nextX - target.X;
            if (MathF.Abs(leadX) > maxHorizontalLead)
            {
                nextX = target.X + (MathF.Sign(leadX) * maxHorizontalLead);
            }
        }
        else
        {
            var catchUp = 1f - MathF.Exp(-PredictedRenderIdleCatchUpRate * deltaSeconds);
            nextX = MathHelper.Lerp(nextX, target.X, catchUp);
        }

        var verticalCatchUp = 1f - MathF.Exp(-PredictedRenderIdleCatchUpRate * deltaSeconds);
        var nextY = MathHelper.Lerp(current.Y, target.Y, verticalCatchUp);
        return new Vector2(nextX, nextY);
    }
}
