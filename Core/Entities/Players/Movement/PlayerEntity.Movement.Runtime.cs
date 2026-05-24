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

    public bool TryApplyLiveScale(float scale, SimpleLevel level, PlayerTeam team)
    {
        var previousScale = PlayerScale;
        var previousX = X;
        var previousY = Y;
        GetCollisionBoundsAt(previousX, previousY, out var previousLeft, out _, out var previousRight, out var previousBottom);
        var previousCollisionCenterX = (previousLeft + previousRight) * 0.5f;
        var previousHorizontalSpeed = HorizontalSpeed;
        var previousVerticalSpeed = VerticalSpeed;
        var previousGrounded = IsGrounded;
        var previousRemainingAirJumps = RemainingAirJumps;
        var previousMovementState = MovementState;
        var previousLegacyStateTickAccumulator = LegacyStateTickAccumulator;

        SetPlayerScale(scale);
        if (!IsAlive)
        {
            return true;
        }

        var targetX = previousCollisionCenterX - ((CollisionLeftOffset + CollisionRightOffset) * 0.5f);
        var targetY = previousBottom - CollisionBottomOffset;

        if (TryCanOccupyWithinBounds(level, team, targetX, targetY))
        {
            X = targetX;
            Y = targetY;
            RefreshGroundSupport(level, team, allowDropdownFallThrough: false);
            return true;
        }

        if (TryFindNearestOccupiablePosition(level, team, targetX, targetY, out var resolvedX, out var resolvedY))
        {
            X = resolvedX;
            Y = resolvedY;
            RefreshGroundSupport(level, team, allowDropdownFallThrough: false);
            return true;
        }

        SetPlayerScale(previousScale);
        X = previousX;
        Y = previousY;
        HorizontalSpeed = previousHorizontalSpeed;
        VerticalSpeed = previousVerticalSpeed;
        IsGrounded = previousGrounded;
        RemainingAirJumps = previousRemainingAirJumps;
        MovementState = previousMovementState;
        LegacyStateTickAccumulator = previousLegacyStateTickAccumulator;
        return false;
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
            AdvanceSpySuperjumpState();
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
            && !IsSpyBackstabAnimating
            && !IsExperimentalCryoFrozen;

        var isDemoknightChargeDriving = IsExperimentalDemoknightCharging && canMove;
        var allowChargeFullControl = isDemoknightChargeDriving && ExperimentalDemoknightChargeFullControlEnabled;
        var horizontalDirection = 0f;
        if (allowChargeFullControl)
        {
            horizontalDirection = GetExperimentalDemoknightChargeTurnDirection(input, canMove);
        }
        else if (!isDemoknightChargeDriving)
        {
            // During spy superjump charging, ignore movement buttons that were held when charging started
            var leftInput = input.Left;
            var rightInput = input.Right;

            if (SpySuperjumpChargeTicks > 0)
            {
                var heldButtons = SpySuperjumpChargeStartMovementButtons;
                if ((heldButtons & 0x01) != 0) leftInput = false;  // Left was held
                if ((heldButtons & 0x02) != 0) rightInput = false; // Right was held
            }

            if (canMove && leftInput)
            {
                horizontalDirection -= 1f;
            }
            if (canMove && rightInput)
            {
                horizontalDirection += 1f;
            }
        }
        else
        {
            // Vanilla charge still tracks facing from aim, but keeps weaker steering
            // and no jump control (those stay gated by Full Control).
            horizontalDirection = GetExperimentalDemoknightChargeTurnDirection(input, canMove);
        }

        if (horizontalDirection != 0f)
        {
            FacingDirectionX = horizontalDirection;
        }

        if (isDemoknightChargeDriving)
        {
            SyncExperimentalDemoknightChargeTurnVelocity(horizontalDirection);
            ExperimentalDemoknightChargeWantsLift = input.Up;
            ApplyExperimentalDemoknightChargeDrive(dt);
        }

        // Calculate hasHorizontalInput with filtering for spy superjump charging
        var hasHorizontalInput = canMove && (input.Left || input.Right);

        // During spy superjump charging, ignore movement buttons that were held when charging started
        if (SpySuperjumpChargeTicks > 0 && !isDemoknightChargeDriving)
        {
            var heldButtons = SpySuperjumpChargeStartMovementButtons;
            var leftInput = input.Left && (heldButtons & 0x01) == 0;  // Only if left wasn't held
            var rightInput = input.Right && (heldButtons & 0x02) == 0; // Only if right wasn't held
            hasHorizontalInput = canMove && (leftInput || rightInput);
        }

        if (isDemoknightChargeDriving)
        {
            hasHorizontalInput = allowChargeFullControl ? hasHorizontalInput : false;
            if (!allowChargeFullControl)
            {
                horizontalDirection = 0f;
            }
        }

        // During Heavy ghost dash, ignore all directional input so the burst decelerates naturally
        if (ClassId == PlayerClass.Heavy && IsExperimentalGhostDashing)
        {
            hasHorizontalInput = false;
            horizontalDirection = 0f;
        }

        // Special handling for spy superjump: preserve momentum but allow air control
        if (IsSpySuperjumping && !IsGrounded)
        {
            // Apply limited air control input directly to the base velocity
            if (hasHorizontalInput && horizontalDirection != 0f)
            {
                var airControlPower = RunPower * GetMovementScale(input) * 0.85f * dt; // 0.85 is base control factor
                SpySuperjumpHorizontalVelocity += horizontalDirection * airControlPower * LegacyMovementModel.SourceTicksPerSecond;
            }
            HorizontalSpeed = SpySuperjumpHorizontalVelocity;
        }
        else
        {
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

            MovementState = LegacyMovementState.None;
        }
        else
        {
            IsGrounded = false;
        }

        return startedGrounded;
    }

    public bool TryJumpIfPossible(bool canMove, bool jumpPressed)
    {
        // Disable jumping while using binoculars
        if (IsUsingBinoculars)
        {
            return false;
        }
        
        var allowHeldChargeJump = IsExperimentalDemoknightCharging
            && ExperimentalDemoknightChargeFullControlEnabled
            && ExperimentalDemoknightChargeWantsLift
            && IsGrounded;
        if (!IsAlive || !canMove || IsExperimentalCryoFrozen || (!jumpPressed && !allowHeldChargeJump))
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
        if (IsExperimentalGhostDashing && ExperimentalGhostDashDisablesGravity)
        {
            // Freeze vertically for the duration of the dash
            VerticalSpeed = 0f;
        }
        else if (!startedGrounded || jumped)
        {
            gravityPerTick = GetServerScaledAirborneGravityPerTick(MovementState);
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
        ApplyExperimentalGhostDashMovement(level, team, dt, allowDropdownFallThrough);
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
                    // Also zero superjump horizontal velocity when hitting a wall
                    if (IsSpySuperjumping)
                    {
                        SpySuperjumpHorizontalVelocity = 0f;
                    }
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

    private bool TryFindNearestOccupiablePosition(SimpleLevel level, PlayerTeam team, float originX, float originY, out float resolvedX, out float resolvedY)
    {
        resolvedX = originX;
        resolvedY = originY;
        var searchStep = MathF.Max(2f, MathF.Min(8f, MathF.Min(Width, Height) / 4f));
        var maxSearchRadius = MathF.Max(96f, MathF.Max(Width, Height) * 8f);

        for (var radius = searchStep; radius <= maxSearchRadius + 0.001f; radius += searchStep)
        {
            for (var offsetX = -radius; offsetX <= radius + 0.001f; offsetX += searchStep)
            {
                if (TryCanOccupyWithinBounds(level, team, originX + offsetX, originY - radius))
                {
                    resolvedX = originX + offsetX;
                    resolvedY = originY - radius;
                    return true;
                }
            }

            for (var offsetY = -radius + searchStep; offsetY <= radius - searchStep + 0.001f; offsetY += searchStep)
            {
                if (TryCanOccupyWithinBounds(level, team, originX - radius, originY + offsetY))
                {
                    resolvedX = originX - radius;
                    resolvedY = originY + offsetY;
                    return true;
                }

                if (TryCanOccupyWithinBounds(level, team, originX + radius, originY + offsetY))
                {
                    resolvedX = originX + radius;
                    resolvedY = originY + offsetY;
                    return true;
                }
            }

            for (var offsetX = -radius; offsetX <= radius + 0.001f; offsetX += searchStep)
            {
                if (TryCanOccupyWithinBounds(level, team, originX + offsetX, originY + radius))
                {
                    resolvedX = originX + offsetX;
                    resolvedY = originY + radius;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryCanOccupyWithinBounds(SimpleLevel level, PlayerTeam team, float x, float y)
    {
        GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);
        if (left < 0f
            || top < 0f
            || right > level.Bounds.Width
            || bottom > level.Bounds.Height)
        {
            return false;
        }

        return CanOccupy(level, team, x, y);
    }
}
