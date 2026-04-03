using System;

namespace OpenGarrison.Core;

public enum HealthPackSize : byte
{
    Small = 1,
    Large = 2,
}

public sealed class HealthPackEntity : SimulationEntity
{
    public const int LifetimeTicks = 360;
    public const int FadeTicks = 45;
    public const float Width = 12f;
    public const float Height = 12f;
    public const float PickupWidth = 18f;
    public const float PickupHeight = 18f;
    public const float GravityPerTick = 0.45f;
    public const float MaxFallSpeed = 8f;
    public const float StopHorizontalSpeed = 0.08f;
    private const float GroundFrictionDivisor = 1.18f;
    public const int SmallBaseHealAmount = 25;
    public const float LargeBonusMaxHealthFraction = 0.1f;
    public const int LargeBonusRoundIncrement = 5;

    public HealthPackEntity(
        int id,
        float x,
        float y,
        HealthPackSize size,
        float horizontalSpeed,
        float verticalSpeed) : base(id)
    {
        X = x;
        Y = y;
        Size = size;
        HorizontalSpeed = horizontalSpeed;
        VerticalSpeed = verticalSpeed;
        TicksRemaining = LifetimeTicks;
    }

    public float X { get; private set; }

    public float Y { get; private set; }

    public HealthPackSize Size { get; }

    public float HorizontalSpeed { get; private set; }

    public float VerticalSpeed { get; private set; }

    public bool HasLanded { get; private set; }

    public int TicksRemaining { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public float Alpha => TicksRemaining > FadeTicks
        ? 1f
        : Math.Clamp(TicksRemaining / (float)FadeTicks, 0f, 1f);

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

        if (HasLanded)
        {
            HorizontalSpeed /= GroundFrictionDivisor;
            if (MathF.Abs(HorizontalSpeed) < StopHorizontalSpeed)
            {
                HorizontalSpeed = 0f;
            }
        }

        MoveHorizontally(level, bounds);
        MoveVertically(level, bounds);
    }

    public int GetHealAmount(PlayerEntity player)
    {
        var amount = SmallBaseHealAmount;
        if (Size == HealthPackSize.Large)
        {
            amount += RoundToNearestIncrement(player.MaxHealth * LargeBonusMaxHealthFraction, LargeBonusRoundIncrement);
        }

        return Math.Max(0, amount);
    }

    private static int RoundToNearestIncrement(float value, int increment)
    {
        if (increment <= 0)
        {
            return (int)MathF.Round(value);
        }

        return (int)MathF.Round(value / increment) * increment;
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
            break;
        }

        var clampedX = bounds.ClampX(X, Width);
        if (clampedX != X)
        {
            X = clampedX;
            HorizontalSpeed = 0f;
        }
    }

    private void MoveVertically(SimpleLevel level, WorldBounds bounds)
    {
        var wasFalling = VerticalSpeed >= 0f;
        var landedThisTick = false;
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
                landedThisTick = true;
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
            landedThisTick = clampedY > Y || clampedY >= bounds.Height - (Height / 2f);
            Y = clampedY;
            VerticalSpeed = 0f;
        }

        HasLanded = landedThisTick || (HasLanded && VerticalSpeed == 0f);
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
