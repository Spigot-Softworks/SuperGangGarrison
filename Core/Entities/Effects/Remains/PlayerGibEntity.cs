namespace OpenGarrison.Core;

public sealed class PlayerGibEntity : SimulationEntity
{
    private const float BoundingSize = 6f;
    public const float GravityPerTick = 0.7f;
    public const float MaxFallSpeed = 11f;
    public const int FadeTicks = 10;
    public const float Scale = 2f;
    public const float DefaultBloodChance = 1.8f;

    public PlayerGibEntity(
        int id,
        string spriteName,
        int frameIndex,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float rotationSpeedDegrees,
        float horizontalFriction,
        float rotationFriction,
        int lifetimeTicks,
        float bloodChance = DefaultBloodChance) : base(id)
    {
        SpriteName = spriteName;
        FrameIndex = frameIndex;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        RotationSpeedDegrees = rotationSpeedDegrees;
        HorizontalFriction = horizontalFriction;
        RotationFriction = rotationFriction;
        TicksRemaining = lifetimeTicks;
        BloodChance = bloodChance;
    }

    public string SpriteName { get; }

    public int FrameIndex { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public float RotationDegrees { get; private set; }

    public float RotationSpeedDegrees { get; private set; }

    public float HorizontalFriction { get; }

    public float RotationFriction { get; }

    public int TicksRemaining { get; private set; }

    public float BloodChance { get; }

    public bool IsExpired => TicksRemaining <= 0;

    public float Alpha => TicksRemaining >= FadeTicks
        ? 1f
        : float.Max(0f, TicksRemaining / (float)FadeTicks);

    public void Advance(SimpleLevel level, WorldBounds bounds)
    {
        if (TicksRemaining > 0)
        {
            TicksRemaining -= 1;
        }

        if (TicksRemaining <= 0)
        {
            return;
        }

        RotationDegrees += RotationSpeedDegrees;
        VelocityY = float.Min(MaxFallSpeed, VelocityY + GravityPerTick);

        MoveHorizontally(level, bounds);
        MoveVertically(level, bounds);

        if (float.Abs(VelocityY) < 0.2f)
        {
            VelocityY = 0f;
        }

        if (float.Abs(RotationSpeedDegrees) < 0.2f)
        {
            RotationSpeedDegrees = 0f;
        }
    }

    public float Speed => MathF.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));

    public void AddImpulse(float velocityX, float velocityY, float rotationSpeedDegrees)
    {
        VelocityX += velocityX;
        VelocityY += velocityY;
        RotationSpeedDegrees += rotationSpeedDegrees;
    }

    public void ApplyNetworkState(
        float x,
        float y,
        float velocityX,
        float velocityY,
        float rotationDegrees,
        float rotationSpeedDegrees,
        int ticksRemaining)
    {
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        RotationDegrees = rotationDegrees;
        RotationSpeedDegrees = rotationSpeedDegrees;
        TicksRemaining = ticksRemaining;
    }

    private void MoveHorizontally(SimpleLevel level, WorldBounds bounds)
    {
        X += VelocityX;
        var hitSolid = false;
        foreach (var solid in level.Solids)
        {
            if (!Intersects(solid))
            {
                continue;
            }

            hitSolid = true;
            if (VelocityX > 0f)
            {
                X = solid.Left - (BoundingSize / 2f);
            }
            else if (VelocityX < 0f)
            {
                X = solid.Right + (BoundingSize / 2f);
            }

            VelocityX *= -0.4f;
            break;
        }

        var clampedX = bounds.ClampX(X, BoundingSize);
        if (clampedX != X)
        {
            X = clampedX;
            VelocityX *= -0.4f;
            hitSolid = true;
        }

        if (hitSolid)
        {
            VelocityX *= HorizontalFriction;
            RotationSpeedDegrees *= RotationFriction;
        }
    }

    private void MoveVertically(SimpleLevel level, WorldBounds bounds)
    {
        Y += VelocityY;
        var hitSolid = false;
        foreach (var solid in level.Solids)
        {
            if (!Intersects(solid))
            {
                continue;
            }

            hitSolid = true;
            if (VelocityY > 0f)
            {
                Y = solid.Top - (BoundingSize / 2f);
            }
            else if (VelocityY < 0f)
            {
                Y = solid.Bottom + (BoundingSize / 2f);
            }

            VelocityY *= -0.4f;
            break;
        }

        var clampedY = bounds.ClampY(Y, BoundingSize);
        if (clampedY != Y)
        {
            Y = clampedY;
            VelocityY *= -0.4f;
            hitSolid = true;
        }

        if (hitSolid)
        {
            VelocityX *= HorizontalFriction;
            RotationSpeedDegrees *= RotationFriction;
        }
    }

    private bool Intersects(LevelSolid solid)
    {
        var left = X - (BoundingSize / 2f);
        var right = X + (BoundingSize / 2f);
        var top = Y - (BoundingSize / 2f);
        var bottom = Y + (BoundingSize / 2f);
        return left < solid.Right
            && right > solid.Left
            && top < solid.Bottom
            && bottom > solid.Top;
    }
}
