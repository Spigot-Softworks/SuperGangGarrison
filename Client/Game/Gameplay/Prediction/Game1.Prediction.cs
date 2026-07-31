#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int MaxPendingPredictedInputs = 256;

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
    private bool _serverLocalPredictionEnabled;
    private PlayerInputSnapshot _latestPredictedLocalInput;
    private PlayerInputSnapshot _previousPredictedLocalInput;
    private ulong _lastProtocol64PredictionStateSequence;
    private int _predictedSniperRifleChargePendingCount;
    private int _predictedSniperBowChargePendingCount;

    private void RecordPredictedInput(
        uint sequence,
        PlayerInputSnapshot input,
        bool jumpPressed,
        bool primaryPressed,
        bool secondaryAbilityPressed,
        bool abilityPressed,
        bool swapWeaponPressed,
        bool tauntPressed)
    {
        _latestPredictedLocalInput = input;

        if (!CanUseLocalPrediction() || sequence == 0 || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            ClearLocalPredictionState(clearPendingInputs: true);
            return;
        }

        _pendingPredictedInputs.Add(new PredictedLocalInput(
            sequence,
            input,
            jumpPressed,
            primaryPressed,
            secondaryAbilityPressed,
            abilityPressed,
            swapWeaponPressed,
            tauntPressed));
        if (_pendingPredictedInputs.Count > MaxPendingPredictedInputs)
        {
            _pendingPredictedInputs.RemoveRange(0, _pendingPredictedInputs.Count - MaxPendingPredictedInputs);
        }

        RebuildLocalPrediction(preserveRenderContinuity: true);
    }

    private void ReconcileLocalPrediction(uint lastProcessedInputSequence)
    {
        AcknowledgeLatchedPredictedInputs(lastProcessedInputSequence);

        if (!CanUseLocalPrediction() || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            ClearLocalPredictionState(clearPendingInputs: true);
            return;
        }

        RemoveAcknowledgedPredictedInputs(lastProcessedInputSequence);
        RebuildLocalPrediction(preserveRenderContinuity: true);
    }

    private bool CanUseLocalPrediction()
    {
        return _serverLocalPredictionEnabled
            && _networkClient.IsConnected
            && !_networkClient.IsAwaitingWelcome
            && !_networkClient.IsReplayConnection
            && !_networkClient.IsSpectator
            && _localPlayerSnapshotEntityId.HasValue
            && _world.LocalPlayer.IsAlive
            && !_world.LocalPlayerAwaitingJoin;
    }

    private bool TryGetPredictedLocalPlayerCameraPosition(out Vector2 position)
    {
        if (CanUseLocalPrediction() && _hasPredictedLocalPlayerPosition)
        {
            position = _hasSmoothedLocalPlayerRenderPosition
                ? _smoothedLocalPlayerRenderPosition
                : _predictedLocalPlayerPosition + _predictedLocalPlayerRenderCorrectionOffset;
            return true;
        }

        position = default;
        return false;
    }

    private void ClearLocalPredictionState(bool clearPendingInputs)
    {
        _hasPredictedLocalPlayerPosition = false;
        _hasSmoothedLocalPlayerRenderPosition = false;
        _hasPredictedLocalActionState = false;
        _predictedLocalPlayerShadow = null;
        _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
        _predictedLocalPlayerVelocity = Vector2.Zero;
        _predictedLocalPlayerGrounded = false;
        _predictedLocalPlayerRemainingAirJumps = 0;
        _predictedSniperRifleChargePendingCount = 0;
        _predictedSniperBowChargePendingCount = 0;
        _lastPredictedRenderSmoothingTimeSeconds = -1d;
        if (clearPendingInputs)
        {
            _pendingPredictedInputs.Clear();
        }
    }

    private void ResetLocalPredictionForAuthorityTransition()
    {
        ClearLocalPredictionState(clearPendingInputs: true);
        ClearPendingPredictedInputEdges();
        _latchedJumpPressSequence = 0;
        _lastProtocol64PredictionStateSequence = 0;
    }

    private void ReconcileProtocol64PredictionState()
    {
        if (!_networkClient.Protocol64ModeEnabled)
        {
            _lastProtocol64PredictionStateSequence = 0;
            return;
        }

        var stateSequence = _networkClient.Protocol64State.PlayerStateSequence;
        if (stateSequence == 0 || stateSequence == _lastProtocol64PredictionStateSequence)
        {
            return;
        }

        if (!_networkClient.TryGetProtocol64PlayerState(_networkClient.LocalPlayerSlot, out var localPlayer))
        {
            return;
        }

        _lastProtocol64PredictionStateSequence = stateSequence;
        _networkClient.AcknowledgeProcessedInput(localPlayer.LastProcessedInputSequence);
        ReconcileLocalPrediction(localPlayer.LastProcessedInputSequence);
    }

    private void RemoveAcknowledgedPredictedInputs(uint lastProcessedInputSequence)
    {
        if (lastProcessedInputSequence == 0 || _pendingPredictedInputs.Count == 0)
        {
            return;
        }

        var removeCount = 0;
        while (removeCount < _pendingPredictedInputs.Count
            && IsInputSequenceAcknowledged(_pendingPredictedInputs[removeCount].Sequence, lastProcessedInputSequence))
        {
            removeCount += 1;
        }

        if (removeCount > 0)
        {
            _pendingPredictedInputs.RemoveRange(0, removeCount);
        }
    }

    private static bool IsInputSequenceAcknowledged(uint sequence, uint lastProcessedInputSequence)
    {
        return sequence == lastProcessedInputSequence
            || unchecked((int)(lastProcessedInputSequence - sequence)) > 0;
    }

    private void RebuildLocalPrediction(bool preserveRenderContinuity)
    {
        var renderPositionBeforeRebuild = default(Vector2);
        var hadRenderPositionBeforeRebuild = preserveRenderContinuity
            && TryGetCurrentPredictedRenderPosition(out renderPositionBeforeRebuild);

        if (!CanUseLocalPrediction() || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            ClearLocalPredictionState(clearPendingInputs: false);
            return;
        }

        var player = _world.LocalPlayer;
        if (_hasLatestLocalAimWorldPosition)
        {
            // Keep LocalPlayer aim on the cursor so Capture/HUD/arc do not wait on snapshot aim.
            player.ApplyPredictionAimWorld(_latestLocalAimWorldX, _latestLocalAimWorldY);
        }

        var hadPredictedState = _hasPredictedLocalActionState;
        var previousRifleCharge = hadPredictedState
            ? _predictedLocalActionState.SniperChargeTicks
            : player.SniperChargeTicks;
        var previousBowCharge = hadPredictedState
            ? _predictedLocalActionState.SniperBowChargeTicks
            : player.SniperBowChargeTicks;
        var previousRiflePending = _predictedSniperRifleChargePendingCount;
        var previousBowPending = _predictedSniperBowChargePendingCount;
        var previousScoped = hadPredictedState && _predictedLocalActionState.IsSniperScoped;

        var predictedPlayer = GetPredictedLocalPlayerShadow(player);
        predictedPlayer.RestorePredictionState(player.CapturePredictionState());
        SeedPredictedSniperRifleCharge(
            predictedPlayer,
            player,
            previousRifleCharge,
            previousRiflePending,
            previousScoped);
        SeedPredictedSniperBowCharge(
            predictedPlayer,
            player,
            previousBowCharge,
            previousBowPending);
        SyncPredictedLocalPlayerState(predictedPlayer);

        for (var index = 0; index < _pendingPredictedInputs.Count; index += 1)
        {
            ApplyPredictedInputStep(predictedPlayer, _pendingPredictedInputs[index]);
        }

        _predictedSniperRifleChargePendingCount = _pendingPredictedInputs.Count;
        _predictedSniperBowChargePendingCount = CountPendingBowChargingInputs();

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

    private void SeedPredictedSniperRifleCharge(
        PlayerEntity predictedPlayer,
        PlayerEntity authorityPlayer,
        int previousPredictedCharge,
        int previousPendingCount,
        bool previousScoped)
    {
        if (!predictedPlayer.HasScopedSniperWeaponEquipped && !authorityPlayer.HasScopedSniperWeaponEquipped)
        {
            return;
        }

        var impliedBaseline = previousPredictedCharge - previousPendingCount;
        if (impliedBaseline < 0)
        {
            impliedBaseline = 0;
        }

        int seeded;
        if (!previousScoped && !predictedPlayer.IsSniperScoped)
        {
            seeded = authorityPlayer.SniperChargeTicks;
        }
        else if (authorityPlayer.SniperChargeTicks < impliedBaseline)
        {
            // Server reset/corrected downward (shot fired, unscoped, etc.).
            seeded = authorityPlayer.SniperChargeTicks;
        }
        else
        {
            // Network charge often lags; keep synthesizing from the last predicted value so
            // rebuild+replay still advances one tick per local input.
            seeded = Math.Max(impliedBaseline, authorityPlayer.SniperChargeTicks);
        }

        predictedPlayer.ApplyPredictionSniperChargeTicks(seeded);
    }

    private void SeedPredictedSniperBowCharge(
        PlayerEntity predictedPlayer,
        PlayerEntity authorityPlayer,
        int previousPredictedCharge,
        int previousPendingCount)
    {
        if (!predictedPlayer.IsSniperBowEquipped && !authorityPlayer.IsSniperBowEquipped)
        {
            return;
        }

        var impliedBaseline = previousPredictedCharge - previousPendingCount;
        if (impliedBaseline < 0)
        {
            impliedBaseline = 0;
        }

        int seeded;
        if (authorityPlayer.SniperBowChargeTicks < impliedBaseline)
        {
            seeded = authorityPlayer.SniperBowChargeTicks;
        }
        else
        {
            seeded = Math.Max(impliedBaseline, authorityPlayer.SniperBowChargeTicks);
        }

        predictedPlayer.ApplyPredictionSniperBowChargeTicks(seeded);
    }

    private int CountPendingBowChargingInputs()
    {
        var count = 0;
        for (var index = 0; index < _pendingPredictedInputs.Count; index += 1)
        {
            if (_pendingPredictedInputs[index].Input.FirePrimary)
            {
                count += 1;
            }
        }

        return count;
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
        if (_predictedLocalPlayerShadow is null
            || _predictedLocalPlayerShadow.Id != player.Id
            || _predictedLocalPlayerShadow.ClassId != player.ClassId)
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
            HeavyEatCooldownDurationTicks = player.HeavyEatCooldownDurationTicks,
            IsExperimentalGhostDashing = player.IsExperimentalGhostDashing,
            ExperimentalGhostDashEnablesTrail = player.ExperimentalGhostDashEnablesTrail,
            ExperimentalGhostDashCooldownTicksRemaining = player.ExperimentalGhostDashCooldownTicksRemaining,
            IsSniperScoped = player.IsSniperScoped,
            SniperChargeTicks = player.SniperChargeTicks,
            SniperBowChargeTicks = player.SniperBowChargeTicks,
            IsUsingBinoculars = player.IsUsingBinoculars,
            IsSpyCloaked = player.IsSpyCloaked,
            SpyCloakAlpha = player.SpyCloakAlpha,
            SpySuperjumpChargeTicks = player.SpySuperjumpChargeTicks,
            IsSpySuperjumping = player.IsSpySuperjumping,
            SpySuperjumpHorizontalVelocity = player.SpySuperjumpHorizontalVelocity,
            SpySuperjumpCooldownTicksRemaining = player.SpySuperjumpCooldownTicksRemaining,
            IsSpyVisibleToEnemies = player.IsSpyVisibleToEnemies,
            SpyBackstabWindupTicksRemaining = player.SpyBackstabWindupTicksRemaining,
            SpyBackstabRecoveryTicksRemaining = player.SpyBackstabRecoveryTicksRemaining,
            SpyBackstabVisualTicksRemaining = player.SpyBackstabVisualTicksRemaining,
            MedicUberCharge = player.MedicUberCharge,
            Metal = player.Metal,
            IntelRechargeTicks = player.IntelRechargeTicks,
            IsCarryingIntel = player.IsCarryingIntel,
            IsMedicUberReady = player.IsMedicUberReady,
            IsMedicUbering = player.IsMedicUbering,
            MedicNeedleCooldownTicks = player.MedicNeedleCooldownTicks,
            MedicNeedleRefillTicks = player.MedicNeedleRefillTicks,
            CurrentShells = player.CurrentShells,
            PrimaryCooldownTicks = player.PrimaryCooldownTicks,
            ReloadTicksUntilNextShell = player.ReloadTicksUntilNextShell,
            PyroFlareCooldownTicks = player.PyroFlareCooldownTicks,
            IsCivvieUmbrellaActive = player.IsCivvieUmbrellaActive,
            IsCivvieUmbrellaBroken = player.IsCivvieUmbrellaBroken,
            CivvieUmbrellaChargeTicks = player.CivvieUmbrellaChargeTicks,
            IsCivviePogoActive = player.IsCivviePogoActive,
            CivviePogoCrunchTicksRemaining = player.CivviePogoCrunchTicksRemaining,
            CivviePogoTrickTicksRemaining = player.CivviePogoTrickTicksRemaining,
            CivviePogoTrickDurationAtStart = player.CivviePogoTrickDurationAtStart,
        };
        _hasPredictedLocalActionState = true;
    }

    private void ApplyPredictedInputStep(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        player.SyncCivvieUmbrellaSecondaryInput(predictedInput.Input.FireSecondary);
        player.SyncCivviePogoSuperJumpInput(predictedInput.Input.Up);
        player.ObserveTauntInput(predictedInput.Input.Taunt);
        player.ObserveCivviePogoTrickInput(predictedInput.Input.Taunt);

        var afterburn = player.AdvanceTickState(predictedInput.Input, _config.FixedDeltaSeconds);
        if (afterburn.IsFatal)
        {
            player.Kill();
            SyncPredictedLocalPlayerState(player);
            return;
        }

        var movementInput = predictedInput.Input;
        var jumpPressed = predictedInput.JumpPressed;
        var wasSpyBackstabAnimating = player.IsSpyBackstabAnimating;
        ApplyPredictedPrimaryFire(player, predictedInput);
        if (!wasSpyBackstabAnimating && player.IsSpyBackstabAnimating)
        {
            movementInput = ResetMovementInput(movementInput);
            jumpPressed = false;
            _latestPredictedLocalInput = ResetMovementInput(_latestPredictedLocalInput);
        }

        ApplyPredictedRoomForces(player);
        ApplyPredictedTaunt(player, predictedInput);
        var startedGrounded = player.PrepareMovement(movementInput, _world.Level, player.Team, _config.FixedDeltaSeconds, out var canMove);
        var jumped = player.TryJumpIfPossible(canMove, jumpPressed);
        ApplyPredictedSecondaryFire(player, predictedInput);
        player.CompleteMovement(_world.Level, player.Team, _config.FixedDeltaSeconds, startedGrounded, jumped, movementInput.Down);
        SyncPredictedLocalPlayerState(player);
    }

    private static PlayerInputSnapshot ResetMovementInput(PlayerInputSnapshot input)
    {
        return input with
        {
            Left = false,
            Right = false,
            Up = false,
            Down = false,
        };
    }

    private void ApplyPredictedRoomForces(PlayerEntity player)
    {
        foreach (var roomObject in _world.Level.RoomObjects)
        {
            if (!roomObject.IsMoveBox())
            {
                continue;
            }

            if (!player.IntersectsMarker(
                roomObject.CenterX,
                roomObject.CenterY,
                roomObject.Width,
                roomObject.Height))
            {
                continue;
            }

            var impulse = roomObject.GetMoveBoxImpulse();
            if (impulse.X == 0f && impulse.Y == 0f)
            {
                continue;
            }

            player.SetMovementState(LegacyMovementState.None);
            player.AddImpulse(impulse.X, impulse.Y);
        }
    }

    private struct PredictedLocalActionState
    {
        public bool IsHeavyEating;
        public int HeavyEatTicksRemaining;
        public int HeavyEatCooldownTicksRemaining;
        public int HeavyEatCooldownDurationTicks;
        public bool IsExperimentalGhostDashing;
        public bool ExperimentalGhostDashEnablesTrail;
        public int ExperimentalGhostDashCooldownTicksRemaining;
        public bool IsSniperScoped;
        public int SniperChargeTicks;
        public int SniperBowChargeTicks;
        public bool IsUsingBinoculars;
        public bool IsSpyCloaked;
        public float SpyCloakAlpha;
        public int SpySuperjumpChargeTicks;
        public bool IsSpySuperjumping;
        public float SpySuperjumpHorizontalVelocity;
        public int SpySuperjumpCooldownTicksRemaining;
        public bool IsSpyVisibleToEnemies;
        public int SpyBackstabWindupTicksRemaining;
        public int SpyBackstabRecoveryTicksRemaining;
        public int SpyBackstabVisualTicksRemaining;
        public float MedicUberCharge;
        public float Metal;
        public float IntelRechargeTicks;
        public bool IsCarryingIntel;
        public bool IsMedicUberReady;
        public bool IsMedicUbering;
        public int MedicNeedleCooldownTicks;
        public int MedicNeedleRefillTicks;
        public int CurrentShells;
        public int PrimaryCooldownTicks;
        public int ReloadTicksUntilNextShell;
        public int PyroFlareCooldownTicks;
        public bool IsCivvieUmbrellaActive;
        public bool IsCivvieUmbrellaBroken;
        public int CivvieUmbrellaChargeTicks;
        public bool IsCivviePogoActive;
        public int CivviePogoCrunchTicksRemaining;
        public int CivviePogoTrickTicksRemaining;
        public int CivviePogoTrickDurationAtStart;
    }

    private readonly record struct PredictedLocalInput(
        uint Sequence,
        PlayerInputSnapshot Input,
        bool JumpPressed,
        bool PrimaryPressed,
        bool SecondaryAbilityPressed,
        bool AbilityPressed,
        bool SwapWeaponPressed,
        bool TauntPressed);
}
