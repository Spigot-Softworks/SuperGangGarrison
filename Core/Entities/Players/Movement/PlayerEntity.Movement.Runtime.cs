namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public void TeleportTo(float x, float y)
    {
        X = x;
        Y = y;
        HorizontalSpeed = 0f;
        VerticalSpeed = 0f;
        IsGrounded = false;
        LegacyStateTickAccumulator = 0f;
        MovementState = LegacyMovementState.None;
        ResetExperimentalDemoknightChargeMovementState();
    }

    public void ResolveBlockingOverlap(SimpleLevel level, PlayerTeam team)
    {
        if (!IsAlive)
        {
            return;
        }

        NudgeOutsideBlockingGeometry(level, team);
        ClampTo(level.Bounds);
        if (CanOccupy(level, team, X, Y))
        {
            RefreshGroundSupport(level, team, allowDropdownFallThrough: false);
        }
    }

    public bool Advance(PlayerInputSnapshot input, bool jumpPressed, SimpleLevel level, PlayerTeam team, double deltaSeconds)
    {
        var afterburn = AdvanceTickState(input, deltaSeconds);
        if (afterburn.IsFatal)
        {
            Kill();
            return false;
        }

        var startedGrounded = PrepareMovement(input, level, team, deltaSeconds, out var canMove);
        var jumped = TryJumpIfPossible(canMove, jumpPressed);
        CompleteMovement(level, team, deltaSeconds, startedGrounded, jumped, input.Down);
        return jumped;
    }

    public AfterburnTickResult AdvanceTickState(PlayerInputSnapshot input, double deltaSeconds)
    {
        var dt = (float)deltaSeconds;
        UpdateAimDirection(input);
        UpdatePyroPrimaryHoldState(input.FirePrimary);
        if (HealingCabinetSoundCooldownSecondsRemaining > 0f)
        {
            HealingCabinetSoundCooldownSecondsRemaining = float.Max(0f, HealingCabinetSoundCooldownSecondsRemaining - dt);
        }

        if (!IsAlive)
        {
            return default;
        }

        AdvanceAssistTracking();
        AdvanceCombatPerformanceTracking();
        var legacyStateTicks = ConsumeLegacyStateTicks(dt);
        for (var tick = 0; tick < legacyStateTicks; tick += 1)
        {
            if (IntelPickupCooldownTicks > 0)
            {
                IntelPickupCooldownTicks -= 1;
            }

            AdvanceEngineerResources();
            AdvanceExperimentalPowerState();
            AdvanceWeaponState();
            AdvanceHeavyState();
            AdvanceTauntState();
            AdvanceSniperState();
            AdvanceUberState();
            AdvanceMedicState();
            AdvanceSpyState();
            AdvanceIntelCarryState();
        }

        return AdvanceAfterburn(dt);
    }

    public bool PrepareMovement(PlayerInputSnapshot input, SimpleLevel level, PlayerTeam team, double deltaSeconds, out bool canMove, bool isHumiliated = false)
    {
        var dt = (float)deltaSeconds;
        if (!IsAlive)
        {
            canMove = false;
            return false;
        }

        canMove = !IsHeavyEating
            && (!IsTaunting || IsRaging)
            && !IsSpyBackstabAnimating;
        var preserveHorizontalMomentum = ClassId == PlayerClass.Spy && IsSpyBackstabAnimating;

        var isDemoknightChargeDriving = IsExperimentalDemoknightCharging && canMove;
        var horizontalDirection = 0f;
        if (!isDemoknightChargeDriving)
        {
            if (canMove && input.Left)
            {
                horizontalDirection -= 1f;
            }
            if (canMove && input.Right)
            {
                horizontalDirection += 1f;
            }
        }
        else
        {
            ExperimentalDemoknightChargeWantsLift = input.Up;
            ApplyExperimentalDemoknightChargeDrive(dt);
        }

        if (!isDemoknightChargeDriving && horizontalDirection != 0f)
        {
            FacingDirectionX = horizontalDirection;
        }

        if (!preserveHorizontalMomentum)
        {
            var hasHorizontalInput = canMove && (input.Left || input.Right);
            if (isDemoknightChargeDriving)
            {
                hasHorizontalInput = false;
                horizontalDirection = 0f;
            }

            HorizontalSpeed = LegacyMovementModel.AdvanceHorizontalSpeed(
                HorizontalSpeed,
                RunPower,
                GetMovementScale(input),
                hasHorizontalInput,
                horizontalDirection,
                MovementState,
                IsCarryingIntel,
                dt,
                isHumiliated);
        }

        ClampMovementSpeedsToMovementMaximum();

        if (!CanOccupy(level, team, X, Y))
        {
            NudgeOutsideBlockingGeometry(level, team);
            ClampTo(level.Bounds);
        }

        var startedGrounded = !CanOccupy(level, team, X, Y + 1f)
            || IsStandingOnDropdownPlatform(level, input.Down);
        if (startedGrounded)
        {
            IsGrounded = true;
            RemainingAirJumps = MaxAirJumps;
            if (VerticalSpeed > 0f)
            {
                VerticalSpeed = 0f;
            }

            if (TryGetSupportingDropdownPlatform(level, input.Down, out var dropdownPlatform)
                && dropdownPlatform.ResetsMovementState())
            {
                MovementState = LegacyMovementState.None;
            }
        }
        else
        {
            IsGrounded = false;
        }

        return startedGrounded;
    }

    public bool TryJumpIfPossible(bool canMove, bool jumpPressed)
    {
        if (!IsAlive || !canMove || !jumpPressed)
        {
            return false;
        }

        return TryJump();
    }

    public void CompleteMovement(SimpleLevel level, PlayerTeam team, double deltaSeconds, bool startedGrounded, bool jumped, bool allowDropdownFallThrough)
    {
        var dt = (float)deltaSeconds;
        if (!IsAlive)
        {
            return;
        }

        GetCollisionBounds(out _, out _, out _, out var previousBottom);

        var gravityPerTick = 0f;
        if (!startedGrounded || jumped)
        {
            gravityPerTick = LegacyMovementModel.GetAirborneGravityPerTick(MovementState);
            if (ShouldCancelGravityForSourceSpinjump(level, team, gravityPerTick))
            {
                gravityPerTick = 0f;
            }

            VerticalSpeed = LegacyMovementModel.AdvanceVerticalSpeedHalfStep(VerticalSpeed, gravityPerTick, dt);
        }

        MoveWithCollisions(level, team, HorizontalSpeed * dt, VerticalSpeed * dt, allowDropdownFallThrough);
        if (gravityPerTick > 0f && CanOccupy(level, team, X, Y + 1f))
        {
            VerticalSpeed = LegacyMovementModel.AdvanceVerticalSpeedHalfStep(VerticalSpeed, gravityPerTick, dt);
        }

        ResolveDropdownPlatformContact(level, allowDropdownFallThrough, previousBottom);
        if (TryApplySourceStepDown(level, team))
        {
            RefreshGroundSupport(level, team, allowDropdownFallThrough);
        }
        else
        {
            RefreshGroundSupport(level, team, allowDropdownFallThrough);
        }

        ClampTo(level.Bounds);
        AdvanceSourceFacingDirectionForNextStep();
    }

    private void MoveWithCollisions(SimpleLevel level, PlayerTeam team, float moveX, float moveY, bool allowDropdownFallThrough)
    {
        if (!float.IsFinite(moveX) || !float.IsFinite(moveY))
        {
            HorizontalSpeed = 0f;
            VerticalSpeed = 0f;
            return;
        }

        NudgeOutsideBlockingGeometry(level, team);

        var remainingX = moveX;
        var remainingY = moveY;
        IsGrounded = false;

        for (var iteration = 0;
            iteration < MaxCollisionResolutionIterations && (MathF.Abs(remainingX) > CollisionResolutionEpsilon || MathF.Abs(remainingY) > CollisionResolutionEpsilon);
            iteration += 1)
        {
            var previousX = X;
            var previousY = Y;
            MoveContact(level, team, remainingX, remainingY);
            remainingX -= X - previousX;
            remainingY -= Y - previousY;

            var collisionRectified = false;
            if (remainingY != 0f && !CanOccupy(level, team, X, Y + MathF.Sign(remainingY)))
            {
                if (remainingY > 0f)
                {
                    IsGrounded = true;
                    RemainingAirJumps = MaxAirJumps;
                    MovementState = LegacyMovementState.None;
                }

                VerticalSpeed = 0f;
                remainingY = 0f;
                collisionRectified = true;
            }

            if (remainingX != 0f && !CanOccupy(level, team, X + MathF.Sign(remainingX), Y))
            {
                if (TryHandleExperimentalDemoknightChargeHorizontalCollision(level, team, MathF.Sign(remainingX), ref remainingX))
                {
                    collisionRectified = true;
                }
                else if (TryStepUpForObstacle(level, team, MathF.Sign(remainingX)))
                {
                    MovementState = LegacyMovementState.None;
                    collisionRectified = true;
                }
                else if (TryStepDownForCeilingSlope(level, team, MathF.Sign(remainingX)))
                {
                    MovementState = LegacyMovementState.None;
                    collisionRectified = true;
                }
                else
                {
                    HorizontalSpeed = 0f;
                    remainingX = 0f;
                    collisionRectified = true;
                }
            }

            if (!collisionRectified && (MathF.Abs(remainingX) >= 1f || MathF.Abs(remainingY) >= 1f))
            {
                VerticalSpeed = 0f;
                remainingY = 0f;
            }
        }

        RefreshGroundSupport(level, team, allowDropdownFallThrough);
        RefreshExperimentalDemoknightChargeFlightState(level, allowDropdownFallThrough);
    }
}
