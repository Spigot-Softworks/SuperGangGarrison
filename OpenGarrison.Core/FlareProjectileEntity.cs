namespace OpenGarrison.Core;

public sealed class FlareProjectileEntity : SimulationEntity
{
    public const int LifetimeTicks = 40;
    public const int DamagePerHit = 30;
    public const float BurnIntensityIncrease = 8f;
    public const float BurnDurationIncreaseSourceTicks = 35f;
    public const bool AfterburnFalloff = false;

    public FlareProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int ticksRemaining = LifetimeTicks) : base(id)
    {
        Team = team;
        OwnerId = ownerId;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = ticksRemaining;
    }

    public PlayerTeam Team { get; private set; }

    public int OwnerId { get; private set; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float PreviousX { get; private set; }

    public float PreviousY { get; private set; }

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public int TicksRemaining { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public void AdvanceOneTick()
    {
        PreviousX = X;
        PreviousY = Y;
        X += VelocityX;
        Y += VelocityY;
        TicksRemaining -= 1;
    }

    public void MoveTo(float x, float y)
    {
        X = x;
        Y = y;
    }

    public void Destroy()
    {
        TicksRemaining = 0;
    }

    public void Reflect(int ownerId, PlayerTeam team, float directionRadians)
    {
        var speed = MathF.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));
        OwnerId = ownerId;
        Team = team;
        PreviousX = X;
        PreviousY = Y;
        VelocityX = MathF.Cos(directionRadians) * speed;
        VelocityY = MathF.Sin(directionRadians) * speed;
        TicksRemaining = LifetimeTicks;
    }

    public void ApplyNetworkState(float x, float y, float velocityX, float velocityY, int ticksRemaining)
    {
        PreviousX = X;
        PreviousY = Y;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = ticksRemaining;
    }
}
