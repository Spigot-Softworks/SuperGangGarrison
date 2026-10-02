namespace OpenGarrison.Core;

public sealed class BladeProjectileEntity : SimulationEntity
{
    public BladeProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int hitDamage,
        int ticksRemaining = PlayerEntity.QuoteBladeLifetimeTicks,
        int lifetimeSimulationTicks = PlayerEntity.QuoteBladeLifetimeTicks,
        long ownerAmmoGeneration = 0) : base(id)
    {
        Team = team;
        OwnerId = ownerId;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        HitDamage = hitDamage;
        LifetimeSimulationTicks = Math.Max(1, lifetimeSimulationTicks);
        TicksRemaining = Math.Max(1, ticksRemaining);
        OwnerAmmoGeneration = ownerAmmoGeneration;
    }

    public PlayerTeam Team { get; }

    public int OwnerId { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float PreviousX { get; private set; }

    public float PreviousY { get; private set; }

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public int HitDamage { get; private set; }

    public bool IsCritical { get; private set; }

    public float CriticalDamageMultiplier { get; private set; } = 1f;

    public void SetCritical(float damageMultiplier = ExperimentalGameplaySettings.KritzCriticalDamageMultiplier)
        => HydrateCritical(true, damageMultiplier);

    public void HydrateCritical(bool isCritical, float damageMultiplier)
    {
        IsCritical = isCritical;
        CriticalDamageMultiplier = isCritical
            ? ExperimentalGameplaySettings.NormalizeCriticalDamageMultiplier(damageMultiplier)
            : 1f;
    }

    public int TicksRemaining { get; private set; }

    public int LifetimeSimulationTicks { get; private set; }

    internal long OwnerAmmoGeneration { get; private set; }

    public int AmmoDrainedSourceTicks { get; private set; }

    private float AmmoDrainSourceTickAccumulator { get; set; }

    internal void HydrateLifetimeSimulationTicks(int lifetimeSimulationTicks)
    {
        LifetimeSimulationTicks = Math.Max(1, lifetimeSimulationTicks);
    }

    internal void HydrateOwnerAmmoGeneration(long ownerAmmoGeneration)
    {
        OwnerAmmoGeneration = ownerAmmoGeneration;
    }

    public bool IsExpired => TicksRemaining <= 0;

    public void AdvanceOneTick()
    {
        PreviousX = X;
        PreviousY = Y;
        X += VelocityX;
        Y += VelocityY;
        TicksRemaining -= 1;
    }

    internal int AdvanceAmmoDrain(float sourceTicksPerSimulationTick)
    {
        if (!float.IsFinite(sourceTicksPerSimulationTick) || sourceTicksPerSimulationTick <= 0f)
        {
            return 0;
        }

        AmmoDrainSourceTickAccumulator += sourceTicksPerSimulationTick;
        var drainedThisAdvance = 0;
        while (AmmoDrainSourceTickAccumulator >= 1f)
        {
            AmmoDrainSourceTickAccumulator -= 1f;
            AmmoDrainedSourceTicks += 1;
            drainedThisAdvance += 1;
        }

        return drainedThisAdvance;
    }

    internal void HydrateAmmoDrainProgress(int ticksRemaining, float sourceTicksPerSimulationTick)
    {
        if (!float.IsFinite(sourceTicksPerSimulationTick) || sourceTicksPerSimulationTick <= 0f)
        {
            return;
        }

        var elapsedSimulationTicks = Math.Clamp(
            LifetimeSimulationTicks - Math.Max(0, ticksRemaining),
            0,
            LifetimeSimulationTicks);
        var elapsedSourceTicks = elapsedSimulationTicks * sourceTicksPerSimulationTick;
        var trackedSourceTicks = AmmoDrainedSourceTicks + AmmoDrainSourceTickAccumulator;
        if (elapsedSourceTicks <= trackedSourceTicks)
        {
            return;
        }

        AmmoDrainedSourceTicks = (int)MathF.Floor(elapsedSourceTicks);
        AmmoDrainSourceTickAccumulator = elapsedSourceTicks - AmmoDrainedSourceTicks;
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

    public void ApplyNetworkState(float x, float y, float velocityX, float velocityY, int ticksRemaining, int hitDamage)
    {
        PreviousX = X;
        PreviousY = Y;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = ticksRemaining;
        HitDamage = hitDamage;
    }
}
