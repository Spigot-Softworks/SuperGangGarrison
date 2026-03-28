namespace OpenGarrison.Core;

public sealed class DeadBodyEntity : SimulationEntity
{
    public const int LifetimeTicks = 300;
    public const float GravityPerTick = 0.6f;
    public const float MaxFallSpeed = 10f;
    public const float StopHorizontalSpeed = 0.2f;

    public DeadBodyEntity(
        int id,
        PlayerClass classId,
        PlayerTeam team,
        float x,
        float y,
        float width,
        float height,
        float horizontalSpeed,
        float verticalSpeed,
        bool facingLeft) : base(id)
    {
        ClassId = classId;
        Team = team;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        HorizontalSpeed = horizontalSpeed;
        VerticalSpeed = verticalSpeed;
        FacingLeft = facingLeft;
        TicksRemaining = LifetimeTicks;
    }

    public PlayerClass ClassId { get; }

    public PlayerTeam Team { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float Width { get; }

    public float Height { get; }

    public float HorizontalSpeed { get; private set; }

    public float VerticalSpeed { get; private set; }

    public bool FacingLeft { get; }

    public int TicksRemaining { get; private set; }

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

    public void Advance(SimpleLevel level, WorldBounds bounds)
    {
        HorizontalSpeed /= 1.1f;
        if (MathF.Abs(HorizontalSpeed) < StopHorizontalSpeed)
        {
            HorizontalSpeed = 0f;
        }

        MoveHorizontally(level, bounds);
        MoveVertically(level, bounds);
        TicksRemaining -= 1;
    }

    public void AddImpulse(float velocityX, float velocityY)
    {
        HorizontalSpeed += velocityX;
        VerticalSpeed += velocityY;
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

    private void MoveVertically(SimpleLevel level, WorldBounds bounds)
    {
        var wasFalling = VerticalSpeed >= 0f;
        VerticalSpeed = MathF.Min(MaxFallSpeed, VerticalSpeed + GravityPerTick);
        Y += VerticalSpeed;

        foreach (var solid in level.Solids)
        {
            if (!IntersectsSolid(solid))
            {
                continue;
            }

            if (wasFalling)
            {
                Y = solid.Top - (Height / 2f);
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
            Y = clampedY;
            VerticalSpeed = 0f;
        }
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
