#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1
{
    public const int MaxPendingPredictedInputs = 256;

    private LocalPredictionState _localPredictionState => _gameplayManager.LocalPrediction;

    private void RecordPredictedInput(
        uint sequence,
        PlayerInputSnapshot input,
        bool jumpPressed,
        bool primaryPressed,
        bool secondaryAbilityPressed,
        bool secondaryAbilityReleased,
        bool abilityPressed,
        bool swapWeaponPressed,
        bool toggleSecondaryWeaponPressed,
        bool tauntPressed,
        bool abilityReleased)
    {
        _localPredictionState.LatestPredictedLocalInput = input;

        if (!CanUseLocalPrediction() || sequence == 0 || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            ClearLocalPredictionState(clearPendingInputs: true);
            return;
        }

        _localPredictionState.PendingPredictedInputs.Add(new PredictedLocalInput(
            sequence,
            input,
            jumpPressed,
            primaryPressed,
            secondaryAbilityPressed,
            secondaryAbilityReleased,
            abilityPressed,
            swapWeaponPressed,
            toggleSecondaryWeaponPressed,
            tauntPressed,
            abilityReleased));
        if (_localPredictionState.PendingPredictedInputs.Count > MaxPendingPredictedInputs)
        {
            _localPredictionState.PendingPredictedInputs.RemoveRange(0, _localPredictionState.PendingPredictedInputs.Count - MaxPendingPredictedInputs);
        }

        RebuildLocalPrediction(preserveRenderContinuity: true, advancedOneTick: true);
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
        return _gameplayManager.RuntimeSettings.EnablePrediction
            && _localPredictionState.ServerLocalPredictionEnabled
            && _networkClient.IsConnected
            && !_networkClient.IsAwaitingWelcome
            && !_networkClient.IsReplayConnection
            && !_networkClient.IsLegacyGg2Connection
            && !_networkClient.IsSpectator
            && _localPlayerSnapshotEntityId.HasValue
            && _world.LocalPlayer.IsAlive
            && !_world.LocalPlayerAwaitingJoin;
    }

    private bool TryGetPredictedLocalPlayerCameraPosition(out Vector2 position)
    {
        if (CanUseLocalPrediction() && _localPredictionState.HasPredictedLocalPlayerPosition)
        {
            // Follow the same interpolated, corrected position as the predicted
            // player sprite so the view neither steps at the tick rate nor
            // jumps around the player during online reconciliation.
            position = GetPredictedLocalPlayerRenderPosition();
            return true;
        }

        position = default;
        return false;
    }

    private void ClearLocalPredictionState(bool clearPendingInputs)
    {
        _localPredictionState.HasPredictedLocalPlayerPosition = false;
        _localPredictionState.HasPredictedLocalPlayerTickStartPosition = false;
        _localPredictionState.HasSmoothedLocalPlayerRenderPosition = false;
        _localPredictionState.HasPredictedLocalActionState = false;
        _localPredictionState.PredictedLocalPlayerShadow = null;
        _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
        _localPredictionState.PredictedLocalPlayerVelocity = Vector2.Zero;
        _localPredictionState.PredictedLocalPlayerGrounded = false;
        _localPredictionState.PredictedSniperRifleChargePendingCount = 0;
        _localPredictionState.PredictedSniperBowChargePendingCount = 0;
        _localPredictionState.LastPredictedRenderSmoothingTimeSeconds = -1d;
        if (clearPendingInputs)
        {
            _localPredictionState.PendingPredictedInputs.Clear();
            // Clear presentation-only edges with the prediction queue.  The
            // Protocol64 path can transition to dead/awaiting-join without
            // applying a legacy snapshot, so leaving this latch alive would
            // replay a stale recoil on the next respawn.
            ClearPendingPredictedInputEdges();
        }
    }

    private void ResetLocalPredictionForAuthorityTransition()
    {
        ClearLocalPredictionState(clearPendingInputs: true);
        ClearPendingPredictedInputEdges();
        _latchedJumpPressSequence = 0;
        _localPredictionState.LastProtocol64PredictionStateSequence = 0;
    }

    private void ReconcileProtocol64PredictionState()
    {
        if (!_networkClient.Protocol64ModeEnabled)
        {
            _localPredictionState.LastProtocol64PredictionStateSequence = 0;
            return;
        }

        var stateSequence = _networkClient.Protocol64State.PlayerStateSequence;
        if (stateSequence == 0 || stateSequence == _localPredictionState.LastProtocol64PredictionStateSequence)
        {
            return;
        }

        if (!_networkClient.TryGetProtocol64PlayerState(_networkClient.LocalPlayerSlot, out var localPlayer))
        {
            return;
        }

        _localPredictionState.LastProtocol64PredictionStateSequence = stateSequence;
        _networkClient.AcknowledgeProcessedInput(localPlayer.LastProcessedInputSequence);
        ReconcileLocalPrediction(localPlayer.LastProcessedInputSequence);
    }

    private void RemoveAcknowledgedPredictedInputs(uint lastProcessedInputSequence)
    {
        if (lastProcessedInputSequence == 0 || _localPredictionState.PendingPredictedInputs.Count == 0)
        {
            return;
        }

        var removeCount = 0;
        while (removeCount < _localPredictionState.PendingPredictedInputs.Count
            && IsInputSequenceAcknowledged(_localPredictionState.PendingPredictedInputs[removeCount].Sequence, lastProcessedInputSequence))
        {
            removeCount += 1;
        }

        if (removeCount > 0)
        {
            _localPredictionState.PendingPredictedInputs.RemoveRange(0, removeCount);
        }
    }

    private static bool IsInputSequenceAcknowledged(uint sequence, uint lastProcessedInputSequence)
    {
        return sequence == lastProcessedInputSequence
            || unchecked((int)(lastProcessedInputSequence - sequence)) > 0;
    }

    private void RebuildLocalPrediction(bool preserveRenderContinuity, bool advancedOneTick = false)
    {
        var hadRenderPositionBeforeRebuild = preserveRenderContinuity
            && CanUseLocalPrediction()
            && _localPredictionState.HasPredictedLocalPlayerPosition;
        var tickEndBeforeRebuild = _localPredictionState.PredictedLocalPlayerPosition;
        var tickStartBeforeRebuild = _localPredictionState.HasPredictedLocalPlayerTickStartPosition
            ? _localPredictionState.PredictedLocalPlayerTickStartPosition
            : tickEndBeforeRebuild;

        if (!CanUseLocalPrediction() || !_world.LocalPlayer.IsAlive || _world.LocalPlayerAwaitingJoin)
        {
            ClearLocalPredictionState(clearPendingInputs: false);
            return;
        }

        var player = _world.LocalPlayer;
        if (_gameplayManager.InputUpdate.HasLatestLocalAimWorldPosition)
        {
            // Keep LocalPlayer aim on the cursor so Capture/HUD/arc do not wait on snapshot aim.
            player.ApplyPredictionAimWorld(_gameplayManager.InputUpdate.LatestLocalAimWorldX, _gameplayManager.InputUpdate.LatestLocalAimWorldY);
        }

        var hadPredictedState = _localPredictionState.HasPredictedLocalActionState;
        var previousRifleCharge = hadPredictedState
            ? _localPredictionState.PredictedLocalActionState.SniperChargeTicks
            : player.SniperChargeTicks;
        var previousBowCharge = hadPredictedState
            ? _localPredictionState.PredictedLocalActionState.SniperBowChargeTicks
            : player.SniperBowChargeTicks;
        var previousRiflePending = _localPredictionState.PredictedSniperRifleChargePendingCount;
        var previousBowPending = _localPredictionState.PredictedSniperBowChargePendingCount;
        var previousScoped = hadPredictedState && _localPredictionState.PredictedLocalActionState.IsSniperScoped;

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
        // With no pending input the authoritative sample is both endpoints.
        _localPredictionState.PredictedLocalPlayerTickStartPosition = _localPredictionState.PredictedLocalPlayerPosition;
        _localPredictionState.HasPredictedLocalPlayerTickStartPosition = true;

        var lastPendingIndex = _localPredictionState.PendingPredictedInputs.Count - 1;
        for (var index = 0; index < _localPredictionState.PendingPredictedInputs.Count; index += 1)
        {
            if (index == lastPendingIndex)
            {
                // Presentation blends across the newest predicted tick only.
                _localPredictionState.PredictedLocalPlayerTickStartPosition = _localPredictionState.PredictedLocalPlayerPosition;
            }

            ApplyPredictedInputStep(predictedPlayer, _localPredictionState.PendingPredictedInputs[index]);
        }

        _localPredictionState.PredictedSniperRifleChargePendingCount = _localPredictionState.PendingPredictedInputs.Count;
        _localPredictionState.PredictedSniperBowChargePendingCount = CountPendingBowChargingInputs();

        if (!_localPredictionState.HasSmoothedLocalPlayerRenderPosition)
        {
            _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            _localPredictionState.SmoothedLocalPlayerRenderPosition = _localPredictionState.PredictedLocalPlayerPosition;
            _localPredictionState.HasSmoothedLocalPlayerRenderPosition = true;
            return;
        }

        if (hadRenderPositionBeforeRebuild)
        {
            // Carry only the misprediction into the correction spring. Advancing
            // to a new tick is ordinary motion and must not become an offset.
            _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset += LocalPlayerRenderInterpolation.ComputeContinuityOffsetDelta(
                tickStartBeforeRebuild,
                tickEndBeforeRebuild,
                _localPredictionState.PredictedLocalPlayerTickStartPosition,
                _localPredictionState.PredictedLocalPlayerPosition,
                GetPredictedLocalPlayerInterpolationAlpha(),
                advancedOneTick);
            var correctionDistance = _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset.Length();
            if (correctionDistance >= PredictedRenderCorrectionTeleportSnapDistance)
            {
                RecordPredictedRenderCorrection(correctionDistance, hardSnap: true);
                _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
            }
        }

        _localPredictionState.SmoothedLocalPlayerRenderPosition = GetPredictedLocalPlayerRenderPosition();
    }

    private static void SeedPredictedSniperRifleCharge(
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

    private static void SeedPredictedSniperBowCharge(
        PlayerEntity predictedPlayer,
        PlayerEntity authorityPlayer,
        int previousPredictedCharge,
        int previousPendingCount)
    {
        if (!predictedPlayer.IsSniperBowEquipped
            && !authorityPlayer.IsSniperBowEquipped
            && !predictedPlayer.IsMortarLauncherEquipped
            && !authorityPlayer.IsMortarLauncherEquipped)
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
        for (var index = 0; index < _localPredictionState.PendingPredictedInputs.Count; index += 1)
        {
            if (_localPredictionState.PendingPredictedInputs[index].Input.FirePrimary)
            {
                count += 1;
            }
        }

        return count;
    }

    private bool TryGetCurrentPredictedRenderPosition(out Vector2 renderPosition)
    {
        if (CanUseLocalPrediction() && _localPredictionState.HasPredictedLocalPlayerPosition)
        {
            renderPosition = GetPredictedLocalPlayerRenderPosition();
            return true;
        }

        renderPosition = default;
        return false;
    }

    /// <summary>Fraction of the current input-tick interval that has elapsed.</summary>
    private float GetPredictedLocalPlayerInterpolationAlpha()
        => LocalPlayerRenderInterpolation.ComputeAlpha(_networkInputAccumulatorSeconds, _config.FixedDeltaSeconds);

    /// <summary>
    /// Predicted local position for presentation: blended across the newest
    /// predicted tick, plus the decaying misprediction correction. Gameplay
    /// inputs keep using the unblended tick sample.
    /// </summary>
    private Vector2 GetPredictedLocalPlayerRenderPosition()
    {
        var tickEnd = _localPredictionState.PredictedLocalPlayerPosition;
        var position = _localPredictionState.HasPredictedLocalPlayerTickStartPosition
            ? LocalPlayerRenderInterpolation.Interpolate(
                _localPredictionState.PredictedLocalPlayerTickStartPosition,
                tickEnd,
                GetPredictedLocalPlayerInterpolationAlpha())
            : tickEnd;
        return position + _localPredictionState.PredictedLocalPlayerRenderCorrectionOffset;
    }

    private PlayerEntity GetPredictedLocalPlayerShadow(PlayerEntity player)
    {
        if (_localPredictionState.PredictedLocalPlayerShadow is null
            || _localPredictionState.PredictedLocalPlayerShadow.Id != player.Id
            || _localPredictionState.PredictedLocalPlayerShadow.ClassId != player.ClassId)
        {
            _localPredictionState.PredictedLocalPlayerShadow = new PlayerEntity(player.Id, player.ClassDefinition, player.DisplayName);
        }

        return _localPredictionState.PredictedLocalPlayerShadow;
    }

    private void SyncPredictedLocalPlayerState(PlayerEntity player)
    {
        _localPredictionState.PredictedLocalPlayerPosition = new Vector2(player.X, player.Y);
        _localPredictionState.PredictedLocalPlayerVelocity = new Vector2(player.HorizontalSpeed, player.VerticalSpeed);
        _localPredictionState.PredictedLocalPlayerGrounded = player.IsGrounded;
        _localPredictionState.HasPredictedLocalPlayerPosition = true;
        _localPredictionState.PredictedLocalActionState = new PredictedLocalActionState
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
            StrongDrinkChargeTicks = player.StrongDrinkChargeTicks,
            StrongDrinkChargeDirectionDegrees = player.StrongDrinkChargeDirectionDegrees,
            IsUsingBinoculars = player.IsUsingBinoculars,
            IsSpyCloaked = player.IsSpyCloaked,
            SpyCloakAlpha = player.SpyCloakAlpha,
            LastToDieSpyCloakMeterUnits = player.LastToDieSpyCloakMeterUnits,
            LastToDieSpyCloakMeterMaximumUnits = player.LastToDieSpyCloakMeterMaximumUnits,
            LastToDieSpyRogueRampStacks = player.LastToDieSpyRogueRampStacks,
            SpySuperjumpChargeTicks = player.SpySuperjumpChargeTicks,
            SpySuperjumpChargeDirectionDegrees = player.SpySuperjumpChargeDirectionDegrees,
            IsSpySuperjumping = player.IsSpySuperjumping,
            SpySuperjumpHorizontalVelocity = player.SpySuperjumpHorizontalVelocity,
            SpySuperjumpCooldownTicksRemaining = player.SpySuperjumpCooldownTicksRemaining,
            SpySuperjumpAvailableCharges = player.SpySuperjumpAvailableCharges,
            SpySuperjumpMaximumCharges = player.SpySuperjumpMaximumCharges,
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
            MedicUberDeliveryMode = player.MedicUberPresentationMode,
            MedicNeedleCooldownTicks = player.MedicNeedleCooldownTicks,
            MedicNeedleRefillTicks = player.MedicNeedleRefillTicks,
            MedicHealDartCooldownTicks = player.MedicHealDartCooldownTicks,
            CurrentShells = player.CurrentShells,
            PrimaryCooldownTicks = player.PrimaryCooldownTicks,
            ReloadTicksUntilNextShell = player.ReloadTicksUntilNextShell,
            ExperimentalOffhandCurrentShells = player.ExperimentalOffhandCurrentShells,
            ExperimentalOffhandCooldownTicks = player.ExperimentalOffhandCooldownTicks,
            ExperimentalOffhandReloadTicksUntilNextShell = player.ExperimentalOffhandReloadTicksUntilNextShell,
            BuffBannerChargeDamage = player.BuffBannerChargeDamage,
            BuffBannerMaxChargeDamage = player.BuffBannerMaxChargeDamage,
            BuffBannerDeployTicksRemaining = player.BuffBannerDeployTicksRemaining,
            BuffBannerDeployDurationTicks = player.BuffBannerDeployDurationTicks,
            BuffBannerActiveTicksRemaining = player.BuffBannerActiveTicksRemaining,
            BuffBannerActiveDurationTicks = player.BuffBannerActiveDurationTicks,
            BuffBannerRadius = player.BuffBannerRadius,
            BuffBannerDamageMultiplier = player.BuffBannerDamageMultiplier,
            BuffBannerHealthRegenPerSecond = player.BuffBannerHealthRegenPerSecond,
            AcquiredWeaponCurrentShells = player.AcquiredWeaponCurrentShells,
            AcquiredWeaponCooldownTicks = player.AcquiredWeaponCooldownTicks,
            AcquiredWeaponReloadTicksUntilNextShell = player.AcquiredWeaponReloadTicksUntilNextShell,
            PyroFlareCooldownTicks = player.PyroFlareCooldownTicks,
            IsCivvieUmbrellaActive = player.IsCivvieUmbrellaActive,
            IsCivvieUmbrellaBroken = player.IsCivvieUmbrellaBroken,
            CivvieUmbrellaChargeTicks = player.CivvieUmbrellaChargeTicks,
            IsCivviePogoActive = player.IsCivviePogoActive,
            CivviePogoCrunchTicksRemaining = player.CivviePogoCrunchTicksRemaining,
            CivviePogoTrickTicksRemaining = player.CivviePogoTrickTicksRemaining,
            CivviePogoTrickDurationAtStart = player.CivviePogoTrickDurationAtStart,
        };
        _localPredictionState.HasPredictedLocalActionState = true;
    }

    private void ApplyPredictedInputStep(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        var previousBottom = player.Bottom;
        player.ObserveSpySuperjumpAbilityInput(predictedInput.Input.UseAbility);
        player.SyncCivvieUmbrellaSecondaryInput(predictedInput.Input.FireSecondary);
        player.SyncCivviePogoSuperJumpInput(predictedInput.Input.Up);
        player.ObserveTauntInput(
            predictedInput.Input.Taunt
                || (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.CivvieTaunt)
                    && predictedInput.Input.UseAbility));
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
        if (!player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.WhippingCord))
        {
            player.ReleaseWhippingCord();
        }
        else if (!movementInput.FirePrimary)
        {
            _ = player.ReleaseWhippingCordWithPull();
        }
        ApplyPredictedPrimaryFire(player, predictedInput);
        if (!wasSpyBackstabAnimating && player.IsSpyBackstabAnimating)
        {
            movementInput = ResetMovementInput(movementInput);
            jumpPressed = false;
            _localPredictionState.LatestPredictedLocalInput = ResetMovementInput(_localPredictionState.LatestPredictedLocalInput);
        }

        ApplyPredictedRoomForces(player);
        if (player.ClassId == PlayerClass.Spy
            && jumpPressed
            && predictedInput.Input.UseAbility
            && player.SpySuperjumpChargeTicks > 0)
        {
            player.CancelSpySuperjumpCharge(blockRestartUntilAbilityRelease: true);
            jumpPressed = false;
            movementInput = movementInput with { Up = false };
        }
        ApplyPredictedTaunt(player, predictedInput);
        var startedGrounded = player.PrepareMovement(
            movementInput,
            _world.Level,
            player.Team,
            _config.FixedDeltaSeconds,
            out var canMove,
            isSupportedByOneWayPlatform: _world.Movement.HasLandedArrowGroundSupport(player, movementInput.Down));
        var jumped = player.TryJumpIfPossible(canMove, jumpPressed);
        if (jumped)
        {
            _world.Movement.TryApplyJumpPadJumpBoostForPrediction(player, jumped);
        }
        ApplyPredictedSecondaryFire(player, predictedInput);
        ApplyPredictedUtilityAbility(player, predictedInput);
        if (!player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.WhippingCord))
        {
            player.ReleaseWhippingCord();
        }
        player.CompleteMovement(_world.Level, player.Team, _config.FixedDeltaSeconds, startedGrounded, jumped, movementInput.Down);
        _world.Movement.ResolveLandedArrowLanding(player, previousBottom, movementInput.Down);
        player.AdvanceLastToDieSpyCloakMeter(_config.TicksPerSecond);
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

    internal struct PredictedLocalActionState
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
        public int StrongDrinkChargeTicks;
        public float StrongDrinkChargeDirectionDegrees;
        public bool IsUsingBinoculars;
        public bool IsSpyCloaked;
        public float SpyCloakAlpha;
        public int LastToDieSpyCloakMeterUnits;
        public int LastToDieSpyCloakMeterMaximumUnits;
        public int LastToDieSpyRogueRampStacks;
        public int SpySuperjumpChargeTicks;
        public float SpySuperjumpChargeDirectionDegrees;
        public bool IsSpySuperjumping;
        public float SpySuperjumpHorizontalVelocity;
        public int SpySuperjumpCooldownTicksRemaining;
        public int SpySuperjumpAvailableCharges;
        public int SpySuperjumpMaximumCharges;
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
        public MedicUberDeliveryMode MedicUberDeliveryMode;
        public int MedicNeedleCooldownTicks;
        public int MedicNeedleRefillTicks;
        public int MedicHealDartCooldownTicks;
        public int CurrentShells;
        public int PrimaryCooldownTicks;
        public int ReloadTicksUntilNextShell;
        public int ExperimentalOffhandCurrentShells;
        public int ExperimentalOffhandCooldownTicks;
        public int ExperimentalOffhandReloadTicksUntilNextShell;
        public int BuffBannerChargeDamage;
        public int BuffBannerMaxChargeDamage;
        public int BuffBannerDeployTicksRemaining;
        public int BuffBannerDeployDurationTicks;
        public int BuffBannerActiveTicksRemaining;
        public int BuffBannerActiveDurationTicks;
        public float BuffBannerRadius;
        public float BuffBannerDamageMultiplier;
        public float BuffBannerHealthRegenPerSecond;
        public int AcquiredWeaponCurrentShells;
        public int AcquiredWeaponCooldownTicks;
        public int AcquiredWeaponReloadTicksUntilNextShell;
        public int PyroFlareCooldownTicks;
        public bool IsCivvieUmbrellaActive;
        public bool IsCivvieUmbrellaBroken;
        public int CivvieUmbrellaChargeTicks;
        public bool IsCivviePogoActive;
        public int CivviePogoCrunchTicksRemaining;
        public int CivviePogoTrickTicksRemaining;
        public int CivviePogoTrickDurationAtStart;
    }

    public readonly record struct PredictedLocalInput(
        uint Sequence,
        PlayerInputSnapshot Input,
        bool JumpPressed,
        bool PrimaryPressed,
        bool SecondaryAbilityPressed,
        bool SecondaryAbilityReleased,
        bool AbilityPressed,
        bool SwapWeaponPressed,
        bool ToggleSecondaryWeaponPressed,
        bool TauntPressed,
        bool AbilityReleased);
}
