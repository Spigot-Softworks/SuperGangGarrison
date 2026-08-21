namespace OpenGarrison.Core;

public class NeedleProjectileEntity : SimulationEntity
{
    public const int LifetimeTicks = 40;
    public const int DamagePerHit = 4;
    public const float GravityPerTick = 0.2f;

    public virtual int Damage => DamagePerHit;

    protected virtual float ProjectileGravityPerTick => GravityPerTick;

    public NeedleProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int lifetimeTicks = LifetimeTicks) : base(id)
    {
        Team = team;
        OwnerId = ownerId;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = lifetimeTicks;
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

    public bool IsCritical { get; private set; }

    public float CriticalDamageMultiplier { get; private set; } = 1f;

    public virtual float HitProbeForwardOffset => 0f;

    internal float RaycastPreviousX { get; private set; }

    internal float RaycastPreviousY { get; private set; }

    public void SetCritical(float damageMultiplier = ExperimentalGameplaySettings.KritzCriticalDamageMultiplier)
        => HydrateCritical(true, damageMultiplier);

    public void HydrateCritical(bool isCritical, float damageMultiplier)
    {
        IsCritical = isCritical;
        CriticalDamageMultiplier = isCritical
            ? ExperimentalGameplaySettings.NormalizeCriticalDamageMultiplier(damageMultiplier)
            : 1f;
    }

    public void PrepareRaycastProbe()
    {
        GetForwardProbePosition(PreviousX, PreviousY, out var probeX, out var probeY);
        RaycastPreviousX = probeX;
        RaycastPreviousY = probeY;
    }

    public void GetForwardProbePosition(float baseX, float baseY, out float probeX, out float probeY)
    {
        var offset = HitProbeForwardOffset;
        if (offset <= 0f)
        {
            probeX = baseX;
            probeY = baseY;
            return;
        }

        var speed = MathF.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));
        if (speed <= 0.0001f)
        {
            probeX = baseX;
            probeY = baseY;
            return;
        }

        probeX = baseX + ((VelocityX / speed) * offset);
        probeY = baseY + ((VelocityY / speed) * offset);
    }

    public void GetBasePositionFromProbeHit(float probeHitX, float probeHitY, float directionX, float directionY, out float baseX, out float baseY)
    {
        var offset = HitProbeForwardOffset;
        baseX = probeHitX - (directionX * offset);
        baseY = probeHitY - (directionY * offset);
    }

    public virtual void AdvanceOneTick(float gravityScale = 1f)
    {
        PreviousX = X;
        PreviousY = Y;
        X += VelocityX;
        Y += VelocityY;
        VelocityY += ProjectileGravityPerTick * gravityScale;
        TicksRemaining -= 1;
    }

    public void MoveTo(float x, float y)
    {
        X = x;
        Y = y;
    }

    public virtual void Destroy()
    {
        TicksRemaining = 0;
    }

    public virtual void Reflect(int ownerId, PlayerTeam team, float directionRadians)
    {
        var speed = MathF.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));
        OwnerId = ownerId;
        Team = team;
        VelocityX = MathF.Cos(directionRadians) * speed;
        VelocityY = MathF.Sin(directionRadians) * speed;
        PreviousX = X;
        PreviousY = Y;
        TicksRemaining = LifetimeTicks;
    }

    protected void SetPreviousPositionToCurrent()
    {
        PreviousX = X;
        PreviousY = Y;
    }

    protected void SetVelocity(float velocityX, float velocityY)
    {
        VelocityX = velocityX;
        VelocityY = velocityY;
    }

    protected void AdvanceLifetimeOneTick()
    {
        TicksRemaining -= 1;
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
