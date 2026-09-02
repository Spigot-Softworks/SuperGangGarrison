namespace OpenGarrison.Core;

public sealed class ShotProjectileEntity : SimulationEntity
{
    public const int LifetimeTicks = 40;
    public const int DamagePerHit = 8;
    public const float GravityPerTick = 0.15f;

    public ShotProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        float damagePerHit = DamagePerHit,
        bool forceGibOnKill = false,
        string? killFeedWeaponSpriteNameOverride = null,
        int? sourceSentryId = null,
        bool applyExperimentalEngineerSentryPerkEffects = false,
        float playerKnockbackScale = 1f,
        float? playerSlowMovementMultiplier = null,
        int playerSlowRefreshTicks = 0,
        float? playerKnockbackImpulse = null,
        float playerKnockbackAirborneVerticalScale = 1f,
        float playerKnockbackGroundedVerticalScale = 1f) : base(id)
    {
        Team = team;
        OwnerId = ownerId;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        DamageValue = damagePerHit;
        ForceGibOnKill = forceGibOnKill;
        KillFeedWeaponSpriteNameOverride = killFeedWeaponSpriteNameOverride;
        SourceSentryId = sourceSentryId;
        ApplyExperimentalEngineerSentryPerkEffects = applyExperimentalEngineerSentryPerkEffects;
        PlayerKnockbackScale = Math.Max(0f, playerKnockbackScale);
        PlayerKnockbackImpulse = Math.Max(
            0f,
            playerKnockbackImpulse
                ?? (BulletKnockbackRules.LegacyImpulsePerProjectile * PlayerKnockbackScale));
        PlayerKnockbackAirborneVerticalScale = Math.Clamp(playerKnockbackAirborneVerticalScale, 0f, 1f);
        PlayerKnockbackGroundedVerticalScale = Math.Clamp(playerKnockbackGroundedVerticalScale, 0f, 1f);
        PlayerSlowMovementMultiplier = playerSlowMovementMultiplier.HasValue
            ? Math.Clamp(playerSlowMovementMultiplier.Value, 0.05f, 1f)
            : null;
        PlayerSlowRefreshTicks = Math.Max(0, playerSlowRefreshTicks);
        TicksRemaining = LifetimeTicks;
    }

    public PlayerTeam Team { get; private set; }

    public int OwnerId { get; private set; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float PreviousX { get; private set; }

    public float PreviousY { get; private set; }

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public float DamageValue { get; private set; }

    public bool ForceGibOnKill { get; }

    public string? KillFeedWeaponSpriteNameOverride { get; }

    public int? SourceSentryId { get; }

    public bool ApplyExperimentalEngineerSentryPerkEffects { get; }

    public float PlayerKnockbackScale { get; private set; }

    public float PlayerKnockbackImpulse { get; private set; }

    public float PlayerKnockbackAirborneVerticalScale { get; private set; }

    public float PlayerKnockbackGroundedVerticalScale { get; private set; }

    public BulletKnockbackPayload PlayerKnockbackPayload => new(
        PlayerKnockbackImpulse,
        PlayerKnockbackAirborneVerticalScale,
        PlayerKnockbackGroundedVerticalScale);

    public float? PlayerSlowMovementMultiplier { get; private set; }

    public int PlayerSlowRefreshTicks { get; private set; }

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

    public bool IsExpired => TicksRemaining <= 0;

    public void AdvanceOneTick(float gravityScale = 1f)
    {
        PreviousX = X;
        PreviousY = Y;
        X += VelocityX;
        Y += VelocityY;
        VelocityY += GravityPerTick * gravityScale;
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
        VelocityX = MathF.Cos(directionRadians) * speed;
        VelocityY = MathF.Sin(directionRadians) * speed;
        PreviousX = X;
        PreviousY = Y;
        TicksRemaining = LifetimeTicks;
    }

    public void ApplyNetworkState(
        float x,
        float y,
        float velocityX,
        float velocityY,
        int ticksRemaining,
        float? damageValue = null,
        float? playerKnockbackImpulse = null,
        float? playerKnockbackAirborneVerticalScale = null,
        float? playerKnockbackGroundedVerticalScale = null)
    {
        PreviousX = X;
        PreviousY = Y;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = ticksRemaining;
        if (damageValue.HasValue)
        {
            DamageValue = Math.Max(0f, damageValue.Value);
        }
        if (playerKnockbackImpulse.HasValue)
        {
            PlayerKnockbackScale = BulletKnockbackRules.LegacyImpulsePerProjectile <= 0f
                ? 0f
                : Math.Max(0f, playerKnockbackImpulse.Value) / BulletKnockbackRules.LegacyImpulsePerProjectile;
            PlayerKnockbackImpulse = Math.Max(0f, playerKnockbackImpulse.Value);
        }
        if (playerKnockbackAirborneVerticalScale.HasValue)
        {
            PlayerKnockbackAirborneVerticalScale = Math.Clamp(playerKnockbackAirborneVerticalScale.Value, 0f, 1f);
        }
        if (playerKnockbackGroundedVerticalScale.HasValue)
        {
            PlayerKnockbackGroundedVerticalScale = Math.Clamp(playerKnockbackGroundedVerticalScale.Value, 0f, 1f);
        }
    }
}
