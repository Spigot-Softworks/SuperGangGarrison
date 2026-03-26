#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly List<PredictedLocalInput> _pendingPredictedInputs = new();
    private Vector2 _predictedLocalPlayerPosition;
    private Vector2 _smoothedLocalPlayerRenderPosition;
    private Vector2 _predictedLocalPlayerRenderCorrectionOffset;
    private Vector2 _predictedLocalPlayerVelocity;
    private bool _hasPredictedLocalPlayerPosition;
    private bool _hasSmoothedLocalPlayerRenderPosition;
    private bool _predictedLocalPlayerGrounded;
    private int _predictedLocalPlayerRemainingAirJumps;
    private PlayerEntity? _predictedLocalPlayerShadow;
    private PredictedLocalActionState _predictedLocalActionState;
    private bool _hasPredictedLocalActionState;
    private PlayerInputSnapshot _latestPredictedLocalInput;

    private void RecordPredictedInput(uint sequence, PlayerInputSnapshot input, bool jumpPressed, bool secondaryPressed)
    {
        _latestPredictedLocalInput = input;

        if (!_networkClient.IsConnected || sequence == 0 || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            return;
        }

        _pendingPredictedInputs.Add(new PredictedLocalInput(sequence, input, jumpPressed, secondaryPressed));
        RebuildLocalPrediction(preserveRenderContinuity: false);
    }

    private void ReconcileLocalPrediction(uint lastProcessedInputSequence)
    {
        if (!_networkClient.IsConnected || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            _hasPredictedLocalPlayerPosition = false;
            _hasSmoothedLocalPlayerRenderPosition = false;
            _hasPredictedLocalActionState = false;
            _predictedLocalPlayerShadow = null;
            _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _lastPredictedRenderSmoothingTimeSeconds = -1d;
            _pendingPredictedInputs.Clear();
            return;
        }

        AcknowledgeLatchedPredictedInputs(lastProcessedInputSequence);
        _pendingPredictedInputs.RemoveAll(input => input.Sequence <= lastProcessedInputSequence);
        RebuildLocalPrediction(preserveRenderContinuity: true);
    }

    private void RebuildLocalPrediction(bool preserveRenderContinuity)
    {
        var renderPositionBeforeRebuild = default(Vector2);
        var hadRenderPositionBeforeRebuild = preserveRenderContinuity
            && TryGetCurrentPredictedRenderPosition(out renderPositionBeforeRebuild);

        if (!_networkClient.IsConnected || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            _hasPredictedLocalPlayerPosition = false;
            _hasSmoothedLocalPlayerRenderPosition = false;
            _hasPredictedLocalActionState = false;
            _predictedLocalPlayerShadow = null;
            _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _lastPredictedRenderSmoothingTimeSeconds = -1d;
            return;
        }

        var player = _world.LocalPlayer;
        var predictedPlayer = GetPredictedLocalPlayerShadow(player);
        predictedPlayer.RestorePredictionState(player.CapturePredictionState());
        SyncPredictedLocalPlayerState(predictedPlayer);

        for (var index = 0; index < _pendingPredictedInputs.Count; index += 1)
        {
            ApplyPredictedInputStep(predictedPlayer, _pendingPredictedInputs[index]);
        }

        if (!_hasSmoothedLocalPlayerRenderPosition)
        {
            _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _smoothedLocalPlayerRenderPosition = _predictedLocalPlayerPosition;
            _hasSmoothedLocalPlayerRenderPosition = true;
            return;
        }

        if (hadRenderPositionBeforeRebuild)
        {
            _predictedLocalPlayerRenderCorrectionOffset = renderPositionBeforeRebuild - _predictedLocalPlayerPosition;
            var correctionDistance = _predictedLocalPlayerRenderCorrectionOffset.Length();
            if (correctionDistance >= PredictedRenderCorrectionTeleportSnapDistance)
            {
                RecordPredictedRenderCorrection(correctionDistance, hardSnap: true);
                _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            }
        }

        _smoothedLocalPlayerRenderPosition = _predictedLocalPlayerPosition + _predictedLocalPlayerRenderCorrectionOffset;
    }

    private bool TryGetCurrentPredictedRenderPosition(out Vector2 renderPosition)
    {
        if (_hasSmoothedLocalPlayerRenderPosition)
        {
            renderPosition = _smoothedLocalPlayerRenderPosition;
            return true;
        }

        if (_hasPredictedLocalPlayerPosition)
        {
            renderPosition = _predictedLocalPlayerPosition + _predictedLocalPlayerRenderCorrectionOffset;
            return true;
        }

        renderPosition = default;
        return false;
    }

    private PlayerEntity GetPredictedLocalPlayerShadow(PlayerEntity player)
    {
        if (_predictedLocalPlayerShadow is null || _predictedLocalPlayerShadow.Id != player.Id)
        {
            _predictedLocalPlayerShadow = new PlayerEntity(player.Id, player.ClassDefinition, player.DisplayName);
        }

        return _predictedLocalPlayerShadow;
    }

    private void SyncPredictedLocalPlayerState(PlayerEntity player)
    {
        _predictedLocalPlayerPosition = new Vector2(player.X, player.Y);
        _predictedLocalPlayerVelocity = new Vector2(player.HorizontalSpeed, player.VerticalSpeed);
        _predictedLocalPlayerGrounded = player.IsGrounded;
        _predictedLocalPlayerRemainingAirJumps = player.RemainingAirJumps;
        _hasPredictedLocalPlayerPosition = true;
        _predictedLocalActionState = new PredictedLocalActionState
        {
            IsHeavyEating = player.IsHeavyEating,
            HeavyEatTicksRemaining = player.HeavyEatTicksRemaining,
            HeavyEatCooldownTicksRemaining = player.HeavyEatCooldownTicksRemaining,
            IsSniperScoped = player.IsSniperScoped,
            SniperChargeTicks = player.SniperChargeTicks,
            IsSpyCloaked = player.IsSpyCloaked,
            SpyCloakAlpha = player.SpyCloakAlpha,
            IsSpyVisibleToEnemies = player.IsSpyVisibleToEnemies,
            SpyBackstabWindupTicksRemaining = player.SpyBackstabWindupTicksRemaining,
            SpyBackstabRecoveryTicksRemaining = player.SpyBackstabRecoveryTicksRemaining,
            SpyBackstabVisualTicksRemaining = player.SpyBackstabVisualTicksRemaining,
            MedicUberCharge = player.MedicUberCharge,
            Metal = player.Metal,
            IntelRechargeTicks = player.IntelRechargeTicks,
            IsMedicUberReady = player.IsMedicUberReady,
            IsMedicUbering = player.IsMedicUbering,
            MedicNeedleCooldownTicks = player.MedicNeedleCooldownTicks,
            MedicNeedleRefillTicks = player.MedicNeedleRefillTicks,
            CurrentShells = player.CurrentShells,
            PrimaryCooldownTicks = player.PrimaryCooldownTicks,
            ReloadTicksUntilNextShell = player.ReloadTicksUntilNextShell,
            PyroFlareCooldownTicks = player.PyroFlareCooldownTicks,
        };
        _hasPredictedLocalActionState = true;
    }

    private void ApplyPredictedInputStep(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        var afterburn = player.AdvanceTickState(predictedInput.Input, _config.FixedDeltaSeconds);
        if (afterburn.IsFatal)
        {
            player.Kill();
            SyncPredictedLocalPlayerState(player);
            return;
        }

        ApplyPredictedPrimaryFire(player, predictedInput);
        var startedGrounded = player.PrepareMovement(predictedInput.Input, _world.Level, player.Team, _config.FixedDeltaSeconds, out var canMove);
        var jumped = player.TryJumpIfPossible(canMove, predictedInput.JumpPressed);
        ApplyPredictedSecondaryFire(player, predictedInput);
        player.CompleteMovement(_world.Level, player.Team, _config.FixedDeltaSeconds, startedGrounded, jumped);
        SyncPredictedLocalPlayerState(player);
    }

    private struct PredictedLocalActionState
    {
        public bool IsHeavyEating;
        public int HeavyEatTicksRemaining;
        public int HeavyEatCooldownTicksRemaining;
        public bool IsSniperScoped;
        public int SniperChargeTicks;
        public bool IsSpyCloaked;
        public float SpyCloakAlpha;
        public bool IsSpyVisibleToEnemies;
        public int SpyBackstabWindupTicksRemaining;
        public int SpyBackstabRecoveryTicksRemaining;
        public int SpyBackstabVisualTicksRemaining;
        public float MedicUberCharge;
        public float Metal;
        public float IntelRechargeTicks;
        public bool IsMedicUberReady;
        public bool IsMedicUbering;
        public int MedicNeedleCooldownTicks;
        public int MedicNeedleRefillTicks;
        public int CurrentShells;
        public int PrimaryCooldownTicks;
        public int ReloadTicksUntilNextShell;
        public int PyroFlareCooldownTicks;
    }

    private readonly record struct PredictedLocalInput(
        uint Sequence,
        PlayerInputSnapshot Input,
        bool JumpPressed,
        bool SecondaryPressed);
}
