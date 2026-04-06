namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private const int MaxCollisionResolutionIterations = 10;
    private const float CollisionResolutionEpsilon = 0.1f;
    private const float CollisionSubpixelPrecision = 8f;

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

    public bool IsSourceFacingLeft => GetSourceFacingDirectionX(AimDirectionDegrees) < 0f;

    public bool IsPerformingSourceSpinjump(SimpleLevel level)
    {
        if (!IsAlive)
        {
            return false;
        }

        return ShouldCancelGravityForSourceSpinjump(level, Team, LegacyMovementModel.GetAirborneGravityPerTick(MovementState));
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
}
