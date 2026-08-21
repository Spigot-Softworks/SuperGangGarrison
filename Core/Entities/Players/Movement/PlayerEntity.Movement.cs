namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private const int MaxCollisionResolutionIterations = 10;
    private const float CollisionResolutionEpsilon = 0.1f;
    private const float CollisionSubpixelPrecision = 8f;
    private static readonly bool MovementCollisionDiagnosticsEnabled =
        Environment.GetEnvironmentVariable("OG_CLIENT_PERF_SIM_TRACE") is "1" or "true" or "TRUE";
    private int _movementCollisionContactIterations;
    private int _movementCollisionOccupyChecks;
    private int _movementCollisionResolutionIterations;

    internal int MovementCollisionContactIterations => _movementCollisionContactIterations;

    internal int MovementCollisionOccupyChecks => _movementCollisionOccupyChecks;

    internal int MovementCollisionResolutionIterations => _movementCollisionResolutionIterations;


    private void BeginMovementCollisionDiagnostics()
    {
        if (!MovementCollisionDiagnosticsEnabled)
        {
            return;
        }

        _movementCollisionContactIterations = 0;
        _movementCollisionOccupyChecks = 0;
        _movementCollisionResolutionIterations = 0;
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
                ResetCivvieUmbrellaAirLift();
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

        return ShouldCancelGravityForSourceSpinjump(level, Team, GetServerScaledAirborneGravityPerTick(MovementState));
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
        => IsInsideBlockingTeamGate(level, team, IsCarryingIntel);

    public bool IsInsideBlockingTeamGate(SimpleLevel level, PlayerTeam team, bool carryingIntel)
    {
        foreach (var gate in level.GetBlockingTeamGates(team, carryingIntel))
        {
            if (Intersects(gate))
            {
                return true;
            }
        }

        GetCollisionBounds(out var left, out var top, out var right, out var bottom);
        if (SimpleLevelBarrierCollision.BlocksPlayerAt(
                level,
                team,
                IsCarryingIntel,
                left,
                right,
                top,
                bottom,
                left,
                top,
                right,
                bottom))
        {
            return true;
        }

        return false;
    }

    public void AddImpulse(float velocityX, float velocityY)
    {
        if (ClassId == PlayerClass.Heavy && IsExperimentalGhostDashing)
        {
            return;
        }

        HorizontalSpeed += velocityX;
        VerticalSpeed += velocityY;
        // Any explosive vertical impulse invalidates the grounded state. The
        // downward/side-biased edge case used to leave MovementState airborne
        // while IsGrounded stayed true until the player supplied movement input.
        if (MathF.Abs(velocityY) > 0.0001f)
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
