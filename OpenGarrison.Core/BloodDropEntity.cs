namespace OpenGarrison.Core;

public sealed class BloodDropEntity : SimulationEntity
{
    private const float BoundingSize = 2f;
    public const int LifetimeTicks = 250;
    public const int MaxMergedLifetimeTicks = LifetimeTicks * 2;
    public const float GravityPerTick = 0.4f;
    public const float MaxSpeed = 11f;
    public const float DefaultScale = 1f;
    public const float MaxScale = 2f;

    public BloodDropEntity(int id, float x, float y, float velocityX, float velocityY, float scale = DefaultScale) : base(id)
    {
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = LifetimeTicks;
        Scale = float.Clamp(scale, DefaultScale, MaxScale);
    }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public bool IsStuck { get; private set; }

    public int TicksRemaining { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public float Scale { get; private set; }

    public float Alpha => float.Clamp(TicksRemaining / (float)LifetimeTicks, 0f, 1f);

    public bool IsMergeable => IsStuck && !IsExpired;

    public void ApplyNetworkState(float x, float y, float velocityX, float velocityY, bool isStuck, int ticksRemaining, float scale)
    {
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        IsStuck = isStuck;
        TicksRemaining = ticksRemaining;
        Scale = float.Clamp(scale, DefaultScale, MaxScale);
    }

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

        if (!IsStuck)
        {
            VelocityY = float.Clamp(VelocityY + GravityPerTick, -MaxSpeed, MaxSpeed);
            VelocityX = float.Clamp(VelocityX, -MaxSpeed, MaxSpeed);
            X += VelocityX;
            Y += VelocityY;

            foreach (var solid in level.Solids)
            {
                if (!Intersects(solid))
                {
                    continue;
                }

                StickToSolid(solid);
                break;
            }

            var clampedX = bounds.ClampX(X, BoundingSize);
            var clampedY = bounds.ClampY(Y, BoundingSize);
            if (clampedX != X || clampedY != Y)
            {
                X = clampedX;
                Y = clampedY;
                VelocityX = 0f;
                VelocityY = 0f;
                IsStuck = true;
            }
        }
    }

    public bool CanMergeWith(BloodDropEntity other)
    {
        if (ReferenceEquals(this, other) || !IsMergeable || !other.IsMergeable)
        {
            return false;
        }

        var mergeDistance = 0.5f + Scale + other.Scale;
        var deltaX = X - other.X;
        var deltaY = Y - other.Y;
        return (deltaX * deltaX) + (deltaY * deltaY) <= mergeDistance * mergeDistance;
    }

    public void Absorb(BloodDropEntity other)
    {
        Scale = float.Clamp(Scale + (other.Scale * 0.4f), DefaultScale, MaxScale);
        TicksRemaining = int.Clamp(TicksRemaining + Math.Max(1, other.TicksRemaining / 3), 0, MaxMergedLifetimeTicks);
    }

    private void StickToSolid(LevelSolid solid)
    {
        if (VelocityY >= 0f)
        {
            Y = solid.Top - (BoundingSize / 2f);
        }
        else
        {
            Y = solid.Bottom + (BoundingSize / 2f);
        }

        VelocityX = 0f;
        VelocityY = 0f;
        IsStuck = true;
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
