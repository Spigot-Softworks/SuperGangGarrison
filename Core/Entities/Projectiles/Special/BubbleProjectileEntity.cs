namespace OpenGarrison.Core;

public sealed class BubbleProjectileEntity : SimulationEntity
{
    public const int LifetimeTicks = 155;
    public const float DamagePerHit = 0.75f;
    public const float MaxDistanceFromOwner = 160f;
    public const float Radius = 6f;
    public const float SelfPopRadius = 4f;
    private const float OwnerVelocityCarryFactor = 0.2f;
    private const float AimAccelerationPerSourceTick = 2f;
    private const float HomeDistance = 90f;
    private const float HomeDistanceResponseDivisor = 4f;
    private const float StabilizeBoostPerSourceTick = 2.2f;
    private const float StabilizeFactor = 0.6f;
    private const float SameTeamRepelSpeed = 0.5f;

    public BubbleProjectileEntity(
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

    public PlayerTeam Team { get; }

    public int OwnerId { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float PreviousX { get; private set; }

    public float PreviousY { get; private set; }

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public int TicksRemaining { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public void AdvanceOneTick(float ownerX, float ownerY, float ownerHorizontalSpeed, float ownerVerticalSpeed, float aimDirectionDegrees, float deltaSeconds)
    {
        PreviousX = X;
        PreviousY = Y;
        var sourceDelta = MathF.Max(0f, deltaSeconds) * LegacyMovementModel.SourceTicksPerSecond;

        var aimRadians = aimDirectionDegrees * (MathF.PI / 180f);
        VelocityX += MathF.Cos(aimRadians) * AimAccelerationPerSourceTick * sourceDelta;
        VelocityY += MathF.Sin(aimRadians) * AimAccelerationPerSourceTick * sourceDelta;
        VelocityX += (ownerHorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond) * OwnerVelocityCarryFactor * sourceDelta;
        VelocityY += (ownerVerticalSpeed / LegacyMovementModel.SourceTicksPerSecond) * OwnerVelocityCarryFactor * sourceDelta;

        var deltaX = X - ownerX;
        var deltaY = Y - ownerY;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance > 0.001f)
        {
            var pull = -AimAccelerationPerSourceTick * MathF.Atan((distance - HomeDistance) / HomeDistanceResponseDivisor) * sourceDelta;
            VelocityX += (deltaX / distance) * pull;
            VelocityY += (deltaY / distance) * pull;
        }

        StabilizeSpeed(sourceDelta);
        X += VelocityX * sourceDelta;
        Y += VelocityY * sourceDelta;
        TicksRemaining -= 1;
    }

    public void AddRepulsionFrom(float otherX, float otherY)
    {
        var deltaX = otherX - X;
        var deltaY = otherY - Y;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0.001f)
        {
            return;
        }

        VelocityX += (deltaX / distance) * -SameTeamRepelSpeed;
        VelocityY += (deltaY / distance) * -SameTeamRepelSpeed;
    }

    public void SetVelocity(float velocityX, float velocityY)
    {
        VelocityX = velocityX;
        VelocityY = velocityY;
    }

    public void HalveLifetimeOrDestroy(int thresholdTicks)
    {
        if (TicksRemaining > thresholdTicks)
        {
            TicksRemaining /= 2;
            return;
        }

        Destroy();
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

    private void StabilizeSpeed(float sourceDelta)
    {
        var speed = MathF.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));
        if (speed <= 0.0001f)
        {
            return;
        }

        var stabilizedSpeed = (speed + (StabilizeBoostPerSourceTick * sourceDelta)) * GetDeltaMultiplier(StabilizeFactor, sourceDelta);
        var speedScale = stabilizedSpeed / speed;
        VelocityX *= speedScale;
        VelocityY *= speedScale;
    }

    private static float GetDeltaMultiplier(float factor, float sourceTickFraction)
    {
        return (factor * sourceTickFraction) + (1f - sourceTickFraction);
    }
}
