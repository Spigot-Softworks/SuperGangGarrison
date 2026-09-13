namespace OpenGarrison.Core;

public enum FlareProjectileStyle : byte
{
    Standard = 0,
    DragonRageSlug = 1,
}

public sealed class FlareProjectileEntity : SimulationEntity
{
    public const int LifetimeTicks = 40;
    public const int DragonRageLifetimeTicks = 16;
    public const float DragonRageVisualWidth = 21f;
    public const float DragonRageCoreVisualWidth = 16f;
    public const int DefaultDamagePerHit = 30;
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
        int ticksRemaining = LifetimeTicks,
        float damagePerHit = DefaultDamagePerHit,
        string? killFeedWeaponSpriteName = null,
        FlareProjectileStyle style = FlareProjectileStyle.Standard) : base(id)
    {
        Team = team;
        OwnerId = ownerId;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = ticksRemaining;
        DamagePerHit = Math.Max(0f, damagePerHit);
        Style = style;
        KillFeedWeaponSpriteName = string.IsNullOrWhiteSpace(killFeedWeaponSpriteName)
            ? style == FlareProjectileStyle.DragonRageSlug ? "DragonRageKL" : "FlareKL"
            : killFeedWeaponSpriteName.Trim();
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

    public float DamagePerHit { get; }

    public FlareProjectileStyle Style { get; }

    public bool IsDragonRageSlug => Style == FlareProjectileStyle.DragonRageSlug;

    public int InitialLifetimeTicks => IsDragonRageSlug ? DragonRageLifetimeTicks : LifetimeTicks;

    public float PresentationAlpha
    {
        get
        {
            if (!IsDragonRageSlug)
            {
                return 1f;
            }

            var fadeTicks = DragonRageLifetimeTicks / 2f;
            var normalized = Math.Clamp(TicksRemaining / fadeTicks, 0f, 1f);
            return normalized * normalized;
        }
    }

    public string KillFeedWeaponSpriteName { get; }

    public bool IsExpired => TicksRemaining <= 0;

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
        TicksRemaining = InitialLifetimeTicks;
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
