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
        if (!CanUseLocalPrediction() || !_localPredictionState.HasPredictedLocalPlayerPosition || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            ClearLocalPredictionState(clearPendingInputs: false);
            return;
        }

        if (!_localPredictionState.HasSmoothedLocalPlayerRenderPosition)
        {
            _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _localPredictionState.SmoothedLocalPlayerRenderPosition = _localPredictionState.PredictedLocalPlayerPosition;
            _localPredictionState.HasSmoothedLocalPlayerRenderPosition = true;
            _localPredictionState.LastPredictedRenderSmoothingTimeSeconds = _networkInterpolationClockSeconds;
            RecordPredictedRenderCorrection(0f, hardSnap: false);
            return;
        }

        if (_localPredictionState.LastPredictedRenderSmoothingTimeSeconds < 0d)
        {
            _localPredictionState.LastPredictedRenderSmoothingTimeSeconds = _networkInterpolationClockSeconds;
            _localPredictionState.SmoothedLocalPlayerRenderPosition = _localPredictionState.PredictedLocalPlayerPosition + _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset;
            return;
        }

        var deltaSeconds = (float)Math.Clamp(
            _networkInterpolationClockSeconds - _localPredictionState.LastPredictedRenderSmoothingTimeSeconds,
            0d,
            0.05d);
        _localPredictionState.LastPredictedRenderSmoothingTimeSeconds = _networkInterpolationClockSeconds;

        var distance = _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset.Length();
        var targetRenderPosition = _localPredictionState.PredictedLocalPlayerPosition + _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset;
        var renderDistance = Vector2.Distance(_localPredictionState.SmoothedLocalPlayerRenderPosition, targetRenderPosition);
        if (renderDistance >= PredictedRenderCorrectionTeleportSnapDistance)
        {
            RecordPredictedRenderCorrection(distance, hardSnap: true);
            _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _localPredictionState.SmoothedLocalPlayerRenderPosition = _localPredictionState.PredictedLocalPlayerPosition;
            return;
        }

        if (distance <= 0.01f)
        {
            _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            targetRenderPosition = _localPredictionState.PredictedLocalPlayerPosition;
            distance = 0f;
        }

        if (distance >= PredictedRenderCorrectionTeleportSnapDistance)
        {
            RecordPredictedRenderCorrection(distance, hardSnap: true);
            _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _localPredictionState.SmoothedLocalPlayerRenderPosition = _localPredictionState.PredictedLocalPlayerPosition;
            return;
        }

        if (deltaSeconds <= 0f)
        {
            RecordPredictedRenderCorrection(distance, hardSnap: false);
            return;
        }

        var isActivelyMoving = _localPredictionState.LatestPredictedLocalInput.Left
            || _localPredictionState.LatestPredictedLocalInput.Right
            || _localPredictionState.LatestPredictedLocalInput.Up
            || MathF.Abs(_localPredictionState.PredictedLocalPlayerVelocity.X) > 20f
            || MathF.Abs(_localPredictionState.PredictedLocalPlayerVelocity.Y) > 20f;
        var catchUpRate = isActivelyMoving
            ? PredictedRenderCorrectionActiveCatchUpRate
            : PredictedRenderCorrectionIdleCatchUpRate;
        catchUpRate += MathF.Min(distance * PredictedRenderCorrectionDistanceRateScale, PredictedRenderCorrectionMaxRateBonus);

        var decayFactor = MathF.Exp(-catchUpRate * deltaSeconds);
        _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset *= decayFactor;
        if (_localPredictionState.PredictedLocalPlayerRenderCorrectionOffset.LengthSquared() <= 0.0001f)
        {
            _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
        }

        targetRenderPosition = _localPredictionState.PredictedLocalPlayerPosition + _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset;
        _localPredictionState.SmoothedLocalPlayerRenderPosition = AdvancePredictedLocalPlayerRenderPosition(
            _localPredictionState.SmoothedLocalPlayerRenderPosition,
            targetRenderPosition,
            _localPredictionState.PredictedLocalPlayerVelocity,
            deltaSeconds);
        RecordPredictedRenderCorrection(_localPredictionState.PredictedLocalPlayerRenderCorrectionOffset.Length(), hardSnap: false);
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
