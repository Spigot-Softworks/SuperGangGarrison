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
            _localPredictionState.SmoothedLocalPlayerRenderPosition = GetPredictedLocalPlayerRenderPosition();
            return;
        }

        var deltaSeconds = (float)Math.Clamp(
            _networkInterpolationClockSeconds - _localPredictionState.LastPredictedRenderSmoothingTimeSeconds,
            0d,
            0.05d);
        _localPredictionState.LastPredictedRenderSmoothingTimeSeconds = _networkInterpolationClockSeconds;

        var distance = _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset.Length();
        var targetRenderPosition = GetPredictedLocalPlayerRenderPosition();
        var renderDistance = Vector2.Distance(_localPredictionState.SmoothedLocalPlayerRenderPosition, targetRenderPosition);
        if (renderDistance >= PredictedRenderCorrectionTeleportSnapDistance)
        {
            RecordPredictedRenderCorrection(distance, hardSnap: true);
            _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _localPredictionState.SmoothedLocalPlayerRenderPosition = GetPredictedLocalPlayerRenderPosition();
            return;
        }

        if (distance <= 0.01f)
        {
            _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            distance = 0f;
        }

        if (distance >= PredictedRenderCorrectionTeleportSnapDistance)
        {
            RecordPredictedRenderCorrection(distance, hardSnap: true);
            _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _localPredictionState.SmoothedLocalPlayerRenderPosition = GetPredictedLocalPlayerRenderPosition();
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

        // The drawn position is the tick-interpolated prediction plus the
        // decaying correction. Keep this mirror in step for diagnostics.
        _localPredictionState.SmoothedLocalPlayerRenderPosition = GetPredictedLocalPlayerRenderPosition();
        RecordPredictedRenderCorrection(_localPredictionState.PredictedLocalPlayerRenderCorrectionOffset.Length(), hardSnap: false);
    }
}
