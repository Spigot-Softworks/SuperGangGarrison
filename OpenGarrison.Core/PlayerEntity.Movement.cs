namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private const int MaxCollisionResolutionIterations = 10;
    private const float CollisionResolutionEpsilon = 0.1f;
    private const float CollisionSubpixelPrecision = 8f;

    public void TeleportTo(float x, float y)
    {
        X = x;
        Y = y;
        HorizontalSpeed = 0f;
        VerticalSpeed = 0f;
        IsGrounded = false;
        LegacyStateTickAccumulator = 0f;
        MovementState = LegacyMovementState.None;
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
            RefreshGroundSupport(level, team);
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
        CompleteMovement(level, team, deltaSeconds, startedGrounded, jumped);
        return jumped;
    }

    public AfterburnTickResult AdvanceTickState(PlayerInputSnapshot input, double deltaSeconds)
    {
        var dt = (float)deltaSeconds;
        UpdateAimDirection(input);

        if (!IsAlive)
        {
            return default;
        }

        var legacyStateTicks = ConsumeLegacyStateTicks(dt);
        for (var tick = 0; tick < legacyStateTicks; tick += 1)
        {
            if (IntelPickupCooldownTicks > 0)
            {
                IntelPickupCooldownTicks -= 1;
            }

            AdvanceEngineerResources();
            AdvanceWeaponState();
            AdvanceHeavyState();
            AdvanceTauntState();
            AdvanceSniperState();
            AdvanceUberState();
            AdvanceMedicState();
            AdvanceSpyState();
        }

        return AdvanceAfterburn(dt);
    }

    public bool PrepareMovement(PlayerInputSnapshot input, SimpleLevel level, PlayerTeam team, double deltaSeconds, out bool canMove)
    {
        var dt = (float)deltaSeconds;
        if (!IsAlive)
        {
            canMove = false;
            return false;
        }

        canMove = !IsHeavyEating && !IsTaunting;

        var horizontalDirection = 0f;
        if (canMove && input.Left)
        {
            horizontalDirection -= 1f;
        }
        if (canMove && input.Right)
        {
            horizontalDirection += 1f;
        }

        if (horizontalDirection != 0f)
        {
            FacingDirectionX = horizontalDirection;
        }

        HorizontalSpeed = LegacyMovementModel.AdvanceHorizontalSpeed(
            HorizontalSpeed,
            RunPower,
            GetMovementScale(input),
            canMove && (input.Left || input.Right),
            horizontalDirection,
            MovementState,
            IsCarryingIntel,
            dt);

        ClampMovementSpeedsToSourceStepMaximum();

        // Source wall-rub spinjump logic assumes we resolve incidental overlap
        // before checking whether the player is standing on something.
        if (!CanOccupy(level, team, X, Y))
        {
            NudgeOutsideBlockingGeometry(level, team);
            ClampTo(level.Bounds);
        }

        var startedGrounded = !CanOccupy(level, team, X, Y + 1f);
        if (startedGrounded)
        {
            IsGrounded = true;
            RemainingAirJumps = MaxAirJumps;
            if (VerticalSpeed > 0f)
            {
                VerticalSpeed = 0f;
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

    public void CompleteMovement(SimpleLevel level, PlayerTeam team, double deltaSeconds, bool startedGrounded, bool jumped)
    {
        var dt = (float)deltaSeconds;
        if (!IsAlive)
        {
            return;
        }

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

        MoveWithCollisions(level, team, HorizontalSpeed * dt, VerticalSpeed * dt);
        if (gravityPerTick > 0f && CanOccupy(level, team, X, Y + 1f))
        {
            VerticalSpeed = LegacyMovementModel.AdvanceVerticalSpeedHalfStep(VerticalSpeed, gravityPerTick, dt);
        }

        if (TryApplySourceStepDown(level, team))
        {
            RefreshGroundSupport(level, team);
        }

        ClampTo(level.Bounds);
        AdvanceSourceFacingDirectionForNextStep();
    }

    public void ClampTo(WorldBounds bounds)
    {
        var minX = -CollisionLeftOffset;
        var maxX = bounds.Width - CollisionRightOffset;
        var clampedX = float.Clamp(X, minX, maxX);
        if (clampedX != X)
        {
            HorizontalSpeed = 0f;
            X = clampedX;
        }

        var minY = -CollisionTopOffset;
        var maxY = bounds.Height - CollisionBottomOffset;
        var clampedY = float.Clamp(Y, minY, maxY);
        if (clampedY != Y)
        {
            if (VerticalSpeed > 0f)
            {
                IsGrounded = true;
            }

            Y = clampedY;
            VerticalSpeed = 0f;
            MovementState = LegacyMovementState.None;
        }
    }

    public bool IsSourceFacingLeft => SourceFacingDirectionX < 0f;

    public bool IsPerformingSourceSpinjump(SimpleLevel level)
    {
        if (!IsAlive)
        {
            return false;
        }

        return ShouldCancelGravityForSourceSpinjump(level, Team, LegacyMovementModel.GetAirborneGravityPerTick(MovementState));
    }

    private void MoveWithCollisions(SimpleLevel level, PlayerTeam team, float moveX, float moveY)
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
                if (TryStepUpForObstacle(level, team, MathF.Sign(remainingX)))
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

        RefreshGroundSupport(level, team);
    }

    private float GetMovementScale(PlayerInputSnapshot input)
    {
        if (IsHeavyEating || IsTaunting)
        {
            return 0f;
        }

        if (ClassId == PlayerClass.Spy && SpyBackstabVisualTicksRemaining > 0)
        {
            return 0f;
        }

        if (ClassId == PlayerClass.Sniper && IsSniperScoped)
        {
            return SniperScopedMoveScale;
        }

        if (ClassId == PlayerClass.Heavy && input.FirePrimary)
        {
            return HeavyPrimaryMoveScale;
        }

        return 1f;
    }

    private float GetJumpScale()
    {
        if (ClassId == PlayerClass.Sniper && IsSniperScoped)
        {
            return SniperScopedJumpScale;
        }

        if (ClassId == PlayerClass.Spy && SpyBackstabVisualTicksRemaining > 0)
        {
            return 0f;
        }

        return 1f;
    }

    public bool IntersectsMarker(float markerX, float markerY, float markerWidth, float markerHeight)
    {
        GetCollisionBounds(out var left, out var top, out var right, out var bottom);
        var markerLeft = markerX - (markerWidth / 2f);
        var markerRight = markerX + (markerWidth / 2f);
        var markerTop = markerY - (markerHeight / 2f);
        var markerBottom = markerY + (markerHeight / 2f);

        return left < markerRight
            && right > markerLeft
            && top < markerBottom
            && bottom > markerTop;
    }

    public bool IsInsideBlockingTeamGate(SimpleLevel level, PlayerTeam team)
    {
        foreach (var gate in level.GetBlockingTeamGates(team, IsCarryingIntel))
        {
            if (Intersects(gate))
            {
                return true;
            }
        }

        return false;
    }

    public void AddImpulse(float velocityX, float velocityY)
    {
        HorizontalSpeed += velocityX;
        VerticalSpeed += velocityY;
        if (velocityY < 0f)
        {
            IsGrounded = false;
        }
    }

    public void ScaleVelocity(float scale)
    {
        HorizontalSpeed *= scale;
        VerticalSpeed *= scale;
        if (VerticalSpeed < 0f)
        {
            IsGrounded = false;
        }
    }

    private void UpdateAimDirection(PlayerInputSnapshot input)
    {
        var aimDeltaX = input.AimWorldX - X;
        var aimDeltaY = input.AimWorldY - Y;
        if (MathF.Abs(aimDeltaX) <= 0.0001f && MathF.Abs(aimDeltaY) <= 0.0001f)
        {
            AimDirectionDegrees = FacingDirectionX < 0f ? 180f : 0f;
            return;
        }

        AimDirectionDegrees = NormalizeDegrees(MathF.Atan2(aimDeltaY, aimDeltaX) * (180f / MathF.PI));
    }

    private static float NormalizeDegrees(float degrees)
    {
        while (degrees < 0f)
        {
            degrees += 360f;
        }

        while (degrees >= 360f)
        {
            degrees -= 360f;
        }

        return degrees;
    }

    private static float GetSourceFacingDirectionX(float aimDirectionDegrees)
    {
        return NormalizeDegrees(aimDirectionDegrees + 270f) > 180f ? 1f : -1f;
    }

    private bool ShouldCancelGravityForSourceSpinjump(SimpleLevel level, PlayerTeam team, float gravityPerTick)
    {
        if (gravityPerTick <= 0f)
        {
            return false;
        }

        var horizontalDirection = MathF.Sign(HorizontalSpeed);
        if (horizontalDirection == 0f)
        {
            return false;
        }

        if (!DidSourceFacingSpinForHorizontalDirection(horizontalDirection))
        {
            return false;
        }

        if (!CanOccupy(level, team, X, Y))
        {
            return false;
        }

        if (CanOccupy(level, team, X + horizontalDirection, Y))
        {
            return false;
        }

        if (!CanOccupy(level, team, X, Y - gravityPerTick))
        {
            return false;
        }

        return CanOccupy(level, team, X, Y + 1f) || VerticalSpeed < 0f;
    }

    private bool DidSourceFacingSpinForHorizontalDirection(float horizontalDirection)
    {
        if (horizontalDirection > 0f)
        {
            return PreviousSourceFacingDirectionX > SourceFacingDirectionX;
        }

        return PreviousSourceFacingDirectionX < SourceFacingDirectionX;
    }

    private void AdvanceSourceFacingDirectionForNextStep()
    {
        PreviousSourceFacingDirectionX = SourceFacingDirectionX;
        SourceFacingDirectionX = GetSourceFacingDirectionX(AimDirectionDegrees);
    }

    private bool TryApplySourceStepDown(SimpleLevel level, PlayerTeam team)
    {
        if (VerticalSpeed != 0f || !CanOccupy(level, team, X, Y))
        {
            return false;
        }

        if (CanOccupy(level, team, X, Y + 6f) && !CanOccupy(level, team, X, Y + 7f))
        {
            Y += 6f;
            return true;
        }

        if (GetSourceMovementSpeedPerTick() > 6f
            && CanOccupy(level, team, X, Y + 12f)
            && !CanOccupy(level, team, X, Y + 13f))
        {
            Y += 12f;
            return true;
        }

        return false;
    }

    private float GetSourceMovementSpeedPerTick()
    {
        return MathF.Sqrt((HorizontalSpeed * HorizontalSpeed) + (VerticalSpeed * VerticalSpeed))
            / LegacyMovementModel.SourceTicksPerSecond;
    }

    private void ClampMovementSpeedsToSourceStepMaximum()
    {
        var maxSpeed = LegacyMovementModel.MaxStepSpeedPerTick * LegacyMovementModel.SourceTicksPerSecond;
        HorizontalSpeed = float.Clamp(HorizontalSpeed, -maxSpeed, maxSpeed);
        VerticalSpeed = float.Clamp(VerticalSpeed, -maxSpeed, maxSpeed);
    }

    private void MoveContact(SimpleLevel level, PlayerTeam team, float deltaX, float deltaY)
    {
        if (!float.IsFinite(deltaX) || !float.IsFinite(deltaY))
        {
            return;
        }

        var maxDistance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (maxDistance <= 0f)
        {
            return;
        }

        var scaleFactor = MathF.Max(MathF.Abs(deltaX), MathF.Abs(deltaY));
        if (scaleFactor <= 0f)
        {
            return;
        }

        var subpixel = CollisionSubpixelPrecision;
        var totalMoved = 0f;
        while (totalMoved < maxDistance && subpixel > 0f)
        {
            var remainingDistance = MathF.Min(1f, maxDistance - totalMoved);
            var fraction = subpixel / CollisionSubpixelPrecision;
            var moveX = (deltaX / scaleFactor) * fraction * remainingDistance;
            var moveY = (deltaY / scaleFactor) * fraction * remainingDistance;
            var nextX = X + (moveX * fraction);
            var nextY = Y + (moveY * fraction);
            if (!CanOccupy(level, team, nextX, nextY))
            {
                subpixel -= 1f;
                continue;
            }

            var advancedX = nextX - X;
            var advancedY = nextY - Y;
            if (advancedX == 0f && advancedY == 0f)
            {
                break;
            }

            totalMoved += MathF.Sqrt((advancedX * advancedX) + (advancedY * advancedY));
            X = nextX;
            Y = nextY;
            if (subpixel < CollisionSubpixelPrecision)
            {
                break;
            }
        }
    }

    private void NudgeOutsideBlockingGeometry(SimpleLevel level, PlayerTeam team)
    {
        if (CanOccupy(level, team, X, Y))
        {
            return;
        }

        var originalX = X;
        var originalY = Y;
        var bestDistance = float.PositiveInfinity;
        var bestX = X;
        var bestY = Y;

        ConsiderOutsideBlockingGeometry(level, team, 0f, -1f, Height / 2f, ref bestDistance, ref bestX, ref bestY);
        ConsiderOutsideBlockingGeometry(level, team, 0f, 1f, Height / 2f, ref bestDistance, ref bestX, ref bestY);
        ConsiderOutsideBlockingGeometry(level, team, 1f, 0f, Width / 2f, ref bestDistance, ref bestX, ref bestY);
        ConsiderOutsideBlockingGeometry(level, team, -1f, 0f, Width / 2f, ref bestDistance, ref bestX, ref bestY);

        if (bestDistance < float.PositiveInfinity)
        {
            X = bestX;
            Y = bestY;
            return;
        }

        X = originalX;
        Y = originalY;
    }

    private void ConsiderOutsideBlockingGeometry(
        SimpleLevel level,
        PlayerTeam team,
        float directionX,
        float directionY,
        float maxDistance,
        ref float bestDistance,
        ref float bestX,
        ref float bestY)
    {
        if (!TryFindOutsideBlockingPosition(level, team, directionX, directionY, maxDistance, out var candidateX, out var candidateY, out var distance))
        {
            return;
        }

        if (distance >= bestDistance)
        {
            return;
        }

        bestDistance = distance;
        bestX = candidateX;
        bestY = candidateY;
    }

    private bool TryFindOutsideBlockingPosition(
        SimpleLevel level,
        PlayerTeam team,
        float directionX,
        float directionY,
        float maxDistance,
        out float candidateX,
        out float candidateY,
        out float distance)
    {
        candidateX = X;
        candidateY = Y;
        distance = 0f;

        for (var offset = 1f; offset <= maxDistance + 0.001f; offset += 1f)
        {
            var nextX = X + (directionX * offset);
            var nextY = Y + (directionY * offset);
            if (CanOccupy(level, team, nextX, nextY))
            {
                candidateX = nextX;
                candidateY = nextY;
                distance = offset;
                return true;
            }
        }

        return false;
    }

    private bool Intersects(LevelSolid solid)
    {
        GetCollisionBounds(out var left, out var top, out var right, out var bottom);

        return left < solid.Right
            && right > solid.Left
            && top < solid.Bottom
            && bottom > solid.Top;
    }

    private bool Intersects(RoomObjectMarker roomObject)
    {
        GetCollisionBounds(out var left, out var top, out var right, out var bottom);
        var gateLeft = roomObject.Left;
        var gateRight = roomObject.Right;
        var gateTop = roomObject.Top;
        var gateBottom = roomObject.Bottom;

        return left < gateRight
            && right > gateLeft
            && top < gateBottom
            && bottom > gateTop;
    }

    private void RefreshGroundSupport(SimpleLevel level, PlayerTeam team)
    {
        if (VerticalSpeed < 0f || !CanOccupy(level, team, X, Y))
        {
            return;
        }

        if (CanOccupy(level, team, X, Y + 1f))
        {
            return;
        }

        IsGrounded = true;
        RemainingAirJumps = MaxAirJumps;
        VerticalSpeed = 0f;
    }

    private bool TryStepUpForObstacle(SimpleLevel level, PlayerTeam team, float horizontalDirection)
    {
        if (horizontalDirection == 0f || HorizontalSpeed == 0f)
        {
            return false;
        }

        var targetY = Y - StepUpHeight;
        if (!CanOccupy(level, team, X + horizontalDirection, targetY))
        {
            return false;
        }

        Y = targetY;
        return true;
    }

    private bool TryStepDownForCeilingSlope(SimpleLevel level, PlayerTeam team, float horizontalDirection)
    {
        if (horizontalDirection == 0f || MathF.Abs(HorizontalSpeed) < MathF.Abs(VerticalSpeed))
        {
            return false;
        }

        var targetY = Y + StepUpHeight;
        if (!CanOccupy(level, team, X + horizontalDirection, targetY))
        {
            return false;
        }

        Y = targetY;
        return true;
    }

    private float? FindBlockingObstacleTop(SimpleLevel level, PlayerTeam team, float x, float y)
    {
        GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);
        float? obstacleTop = null;

        foreach (var solid in level.Solids)
        {
            if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
            {
                obstacleTop = obstacleTop.HasValue ? MathF.Min(obstacleTop.Value, solid.Top) : solid.Top;
            }
        }

        foreach (var wall in level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (left < wall.Right && right > wall.Left && top < wall.Bottom && bottom > wall.Top)
            {
                obstacleTop = obstacleTop.HasValue ? MathF.Min(obstacleTop.Value, wall.Top) : wall.Top;
            }
        }

        return obstacleTop;
    }

    private bool TryJump()
    {
        var jumpSpeed = JumpSpeed * GetJumpScale();
        if (jumpSpeed <= 0f)
        {
            return false;
        }

        if (IsGrounded)
        {
            VerticalSpeed = -jumpSpeed;
            IsGrounded = false;
            return true;
        }

        if (RemainingAirJumps <= 0)
        {
            return false;
        }

        VerticalSpeed = -jumpSpeed;
        RemainingAirJumps -= 1;
        MovementState = LegacyMovementState.None;
        return true;
    }
}
