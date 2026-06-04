namespace OpenGarrison.Core;

public sealed class DeadBodyEntity : SimulationEntity
{
    public const int LifetimeTicks = 300;
    public const float GravityPerTick = 0.6f;
    public const float MaxFallSpeed = 10f;
    public const float StopHorizontalSpeed = 0.2f;
    public const int ImpactSoundCooldownTicks = 18;

    public DeadBodyEntity(
        int id,
        int sourcePlayerId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        float x,
        float y,
        float width,
        float height,
        float horizontalSpeed,
        float verticalSpeed,
        bool facingLeft) : base(id)
    {
        SourcePlayerId = sourcePlayerId;
        ClassId = classId;
        Team = team;
        AnimationKind = animationKind;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        HorizontalSpeed = horizontalSpeed;
        VerticalSpeed = verticalSpeed;
        FacingLeft = facingLeft;
        TicksRemaining = LifetimeTicks;
    }

    public int SourcePlayerId { get; }

    public PlayerClass ClassId { get; }

    public PlayerTeam Team { get; }

    public DeadBodyAnimationKind AnimationKind { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float Width { get; }

    public float Height { get; }

    public float HorizontalSpeed { get; private set; }

    public float VerticalSpeed { get; private set; }

    public bool FacingLeft { get; }

    public int TicksRemaining { get; private set; }

    public int ImpactSoundCooldownTicksRemaining { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public void ApplyNetworkState(
        float x,
        float y,
        float horizontalSpeed,
        float verticalSpeed,
        int ticksRemaining)
    {
        X = x;
        Y = y;
        HorizontalSpeed = horizontalSpeed;
        VerticalSpeed = verticalSpeed;
        TicksRemaining = ticksRemaining;
    }

    public DeadBodyAdvanceResult Advance(SimpleLevel level, WorldBounds bounds)
    {
        if (ImpactSoundCooldownTicksRemaining > 0)
        {
            ImpactSoundCooldownTicksRemaining -= 1;
        }

        HorizontalSpeed /= 1.1f;
        if (MathF.Abs(HorizontalSpeed) < StopHorizontalSpeed)
        {
            HorizontalSpeed = 0f;
        }

        MoveHorizontally(level, bounds);
        var verticalResult = MoveVertically(level, bounds);
        TicksRemaining -= 1;
        return verticalResult;
    }

    public void AddImpulse(float velocityX, float velocityY)
    {
        HorizontalSpeed += velocityX;
        VerticalSpeed += velocityY;
    }

    public bool TryRestartImpactSoundCooldown()
    {
        if (ImpactSoundCooldownTicksRemaining > 0)
        {
            return false;
        }

        ImpactSoundCooldownTicksRemaining = ImpactSoundCooldownTicks;
        return true;
    }

    private void MoveHorizontally(SimpleLevel level, WorldBounds bounds)
    {
        X += HorizontalSpeed;
        foreach (var solid in level.Solids)
        {
            if (!IntersectsSolid(solid))
            {
                continue;
            }

            if (HorizontalSpeed > 0f)
            {
                X = solid.Left - (Width / 2f);
            }
            else if (HorizontalSpeed < 0f)
            {
                X = solid.Right + (Width / 2f);
            }

            HorizontalSpeed = 0f;
        }

        X = bounds.ClampX(X, Width);
    }

    private DeadBodyAdvanceResult MoveVertically(SimpleLevel level, WorldBounds bounds)
    {
        var wasFalling = VerticalSpeed >= 0f;
        VerticalSpeed = MathF.Min(MaxFallSpeed, VerticalSpeed + GravityPerTick);
        var impactSpeed = MathF.Max(0f, VerticalSpeed);
        Y += VerticalSpeed;
        var hitGround = false;

        foreach (var solid in level.Solids)
        {
            if (!IntersectsSolid(solid))
            {
                continue;
            }

            if (wasFalling)
            {
                Y = solid.Top - (Height / 2f);
                hitGround = true;
            }
            else
            {
                Y = solid.Bottom + (Height / 2f);
            }

            VerticalSpeed = 0f;
            break;
        }

        var clampedY = bounds.ClampY(Y, Height);
        if (clampedY != Y)
        {
            if (wasFalling && clampedY < Y)
            {
                hitGround = true;
            }

            Y = clampedY;
            VerticalSpeed = 0f;
        }

        return new DeadBodyAdvanceResult(hitGround, impactSpeed);
    }

    private bool IntersectsSolid(LevelSolid solid)
    {
        var left = X - (Width / 2f);
        var right = X + (Width / 2f);
        var top = Y - (Height / 2f);
        var bottom = Y + (Height / 2f);
        return left < solid.Right
            && right > solid.Left
            && top < solid.Bottom
            && bottom > solid.Top;
    }
}

public readonly record struct DeadBodyAdvanceResult(bool HitGround, float ImpactSpeed);
