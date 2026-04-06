using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
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

        var directionX = deltaX / maxDistance;
        var directionY = deltaY / maxDistance;

        var totalMoved = 0f;
        while (totalMoved < maxDistance)
        {
            var remainingDistance = MathF.Min(1f, maxDistance - totalMoved);
            var movedThisIteration = false;
            for (var subpixel = CollisionSubpixelPrecision; subpixel >= 1f; subpixel -= 1f)
            {
                var fraction = subpixel / CollisionSubpixelPrecision;
                var stepDistance = remainingDistance * fraction;
                var nextX = X + (directionX * stepDistance);
                var nextY = Y + (directionY * stepDistance);
                if (!CanOccupy(level, team, nextX, nextY))
                {
                    continue;
                }

                var advancedX = nextX - X;
                var advancedY = nextY - Y;
                if (advancedX == 0f && advancedY == 0f)
                {
                    continue;
                }

                X = nextX;
                Y = nextY;
                totalMoved += MathF.Sqrt((advancedX * advancedX) + (advancedY * advancedY));
                movedThisIteration = true;
                break;
            }

            if (!movedThisIteration)
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

    private bool IsStandingOnDropdownPlatform(SimpleLevel level, bool allowDropdownFallThrough)
    {
        return TryGetSupportingDropdownPlatform(level, allowDropdownFallThrough, out _);
    }

    private bool TryGetSupportingDropdownPlatform(SimpleLevel level, bool allowDropdownFallThrough, out RoomObjectMarker platform)
    {
        platform = default;
        if (allowDropdownFallThrough)
        {
            return false;
        }

        GetCollisionBounds(out var left, out var top, out var right, out var bottom);
        left += StepSupportEpsilon;
        right -= StepSupportEpsilon;
        if (right <= left)
        {
            left = X + CollisionLeftOffset;
            right = X + CollisionRightOffset;
        }

        RoomObjectMarker? bestPlatform = null;
        foreach (var candidate in level.GetRoomObjects(RoomObjectType.DropdownPlatform))
        {
            if (right <= candidate.Left
                || left >= candidate.Right
                || top >= candidate.Bottom
                || bottom < candidate.Top - 1.5f
                || bottom > candidate.Top + 1f)
            {
                continue;
            }

            if (bestPlatform is null || candidate.Top < bestPlatform.Value.Top)
            {
                bestPlatform = candidate;
            }
        }

        if (!bestPlatform.HasValue)
        {
            return false;
        }

        platform = bestPlatform.Value;
        return true;
    }

    private void ResolveDropdownPlatformContact(SimpleLevel level, bool allowDropdownFallThrough, float previousBottom)
    {
        if (allowDropdownFallThrough || VerticalSpeed < 0f)
        {
            return;
        }

        RoomObjectMarker? bestPlatform = null;
        foreach (var platform in level.GetRoomObjects(RoomObjectType.DropdownPlatform))
        {
            if (!Intersects(platform))
            {
                continue;
            }

            if (previousBottom > platform.Top + 0.1f)
            {
                continue;
            }

            if (bestPlatform is null || platform.Top < bestPlatform.Value.Top)
            {
                bestPlatform = platform;
            }
        }

        if (!bestPlatform.HasValue)
        {
            return;
        }

        Y = bestPlatform.Value.Top - CollisionBottomOffset;
        IsGrounded = true;
        RemainingAirJumps = MaxAirJumps;
        VerticalSpeed = 0f;
        if (bestPlatform.Value.ResetsMovementState())
        {
            MovementState = LegacyMovementState.None;
        }
    }

    private void RefreshGroundSupport(SimpleLevel level, PlayerTeam team, bool allowDropdownFallThrough)
    {
        if (VerticalSpeed < 0f || !CanOccupy(level, team, X, Y))
        {
            return;
        }

        if (CanOccupy(level, team, X, Y + 1f) && !IsStandingOnDropdownPlatform(level, allowDropdownFallThrough))
        {
            return;
        }

        if (!TrySnapToGroundBelow(level, team)
            && !TrySnapToDropdownPlatformBelow(level, allowDropdownFallThrough))
        {
            return;
        }

        IsGrounded = true;
        RemainingAirJumps = MaxAirJumps;
        VerticalSpeed = 0f;
    }

    private bool TrySnapToGroundBelow(SimpleLevel level, PlayerTeam team)
    {
        var obstacleTop = FindBlockingObstacleTop(level, team, X, Y + 1f);
        if (!obstacleTop.HasValue)
        {
            return false;
        }

        var targetY = obstacleTop.Value - CollisionBottomOffset;
        if (targetY < Y || !CanOccupy(level, team, X, targetY))
        {
            return false;
        }

        Y = targetY;
        return true;
    }

    private bool TrySnapToDropdownPlatformBelow(SimpleLevel level, bool allowDropdownFallThrough)
    {
        if (!TryGetSupportingDropdownPlatform(level, allowDropdownFallThrough, out var platform))
        {
            return false;
        }

        var targetY = platform.Top - CollisionBottomOffset;
        if (targetY < Y)
        {
            return false;
        }

        Y = targetY;
        if (platform.ResetsMovementState())
        {
            MovementState = LegacyMovementState.None;
        }

        return true;
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
        left += StepSupportEpsilon;
        right -= StepSupportEpsilon;
        if (right <= left)
        {
            left = x + CollisionLeftOffset;
            right = x + CollisionRightOffset;
        }

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

        foreach (var gate in level.GetBlockingTeamGates(team, IsCarryingIntel))
        {
            if (left < gate.Right && right > gate.Left && top < gate.Bottom && bottom > gate.Top)
            {
                obstacleTop = obstacleTop.HasValue ? MathF.Min(obstacleTop.Value, gate.Top) : gate.Top;
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
