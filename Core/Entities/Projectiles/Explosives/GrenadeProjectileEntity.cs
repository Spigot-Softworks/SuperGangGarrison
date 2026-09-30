using System;

namespace OpenGarrison.Core;

public sealed class GrenadeProjectileEntity : SimulationEntity
{
    public const float BlastRadius = 65f;
    public const float DirectHitDamage = 35f;
    public const float BaseExplosionDamage = 30f; // Base splash damage.
    public const float GravityPerTick = 0.8f;
    public const float MaxFallSpeed = 20f;
    public const float BlastImpulse = 8f;
    public const float EnvironmentCollisionBackoffDistance = 3f;
    public const float SelfDamageScale = 5f / 9f;
    public const float SplashThresholdFactor = 0.25f;
    public const float SentryDamageMultiplier = 1.5f;
    public const float BounceVelocityRetention = 0.77f; // About 40% more retained bounce speed than stock
    public const float HorizontalAirFriction = 0.985f; // Per-tick horizontal velocity damping (stronger than vertical)
    public const float AirFriction = 0.998f; // Per-tick vertical velocity damping (very slight)
    public const float RotationFriction = 0.88f; // Per-tick rotation speed damping
    public const int FuseTicksRemaining = 60; // 2 seconds at 30 ticks/second
    public const float ReflectedSpeedFloor = 10f; // Minimum speed applied to a reflected grenade

    // Strong Drink (Sniper utility bottle) — no bounce / fuse explode; shootable by friendlies.
    public const string StrongDrinkKillFeedSpriteName = "SniperBottleKL";
    public const string StrongDrinkFireKillFeedSpriteName = "SniperBottleFireKL";
    public const string StrongDrinkCrashSoundName = "BottleCrashSnd";
    public const float StrongDrinkDirectHitDamage = 30f;
    // Bottle sprite is 8x22; gameplay hitbox is a square on the longer edge.
    public const float StrongDrinkHitboxSize = 22f;
    public const float StrongDrinkHitboxHalfExtent = StrongDrinkHitboxSize * 0.5f;
    // Grounded flame puddle touch radius (separate from the bottle hitbox).
    public const float StrongDrinkCollisionRadius = StrongDrinkHitboxHalfExtent;
    public const int StrongDrinkDefaultFuseTicks = 180;
    public const float StrongDrinkDefaultMinThrowSpeed = 6f;
    public const float StrongDrinkDefaultMaxThrowSpeed = 11.5f;
    // Legacy alias for the charged max throw speed (was an uncharged flat 19).
    public const float StrongDrinkDefaultThrowSpeed = StrongDrinkDefaultMaxThrowSpeed;
    // Low gravity: slow flight with long lob range (stock grenades use 0.8).
    public const float StrongDrinkGravityPerTick = 0.18f;
    public const float StrongDrinkDefaultSpinSpeed = 0.28f;
    public const float StrongDrinkSpinSpeedMin = 0.18f;
    public const float StrongDrinkSpinSpeedMax = 0.42f;
    // Keep tumble visible through the slow lob (stock grenades use 0.88).
    public const float StrongDrinkRotationFriction = 0.985f;
    public const int StrongDrinkDefaultFireParticleCount = 20;
    // Flames erupt from the bottle blast (slight vertical jitter), then fall and settle.
    public const float StrongDrinkFireSpawnJitterY = 12f;
    public const float StrongDrinkFireHorizontalSpread = 100f;
    // 1.5x fall speed vs stock flames; horizontal burst is scaled up so spread stays similar.
    public const float StrongDrinkFireGravityScale = 1.5f;
    public const float StrongDrinkFireBurstSpeedMin = 1.8f;
    public const float StrongDrinkFireBurstSpeedMax = 5.1f;
    public const float StrongDrinkFireUpwardBurstMin = -4.5f;
    public const float StrongDrinkFireUpwardBurstMax = -1.5f;
    public const float StrongDrinkFireDriftSpeed = 1.2f;
    public const int StrongDrinkGroundedFlameLifetimeTicks = 90;
    // Airborne rain uses this as a pit/void failsafe only; puddle lifetime starts on ground hit.
    public const int StrongDrinkAirFailsafeLifetimeTicks = 450;

    public GrenadeProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        string? killFeedWeaponSpriteNameOverride = null) : base(id)
    {
        Team = team;
        OwnerId = ownerId;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        KillFeedWeaponSpriteNameOverride = killFeedWeaponSpriteNameOverride;
        FuseTicksLeft = FuseTicksRemaining;
        RotationAngle = DeterministicMath.Atan2(velocityY, velocityX);
    }

    public PlayerTeam Team { get; private set; }

    public int OwnerId { get; private set; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float PreviousX { get; private set; }

    public float PreviousY { get; private set; }

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public float RotationAngle { get; private set; }

    public float RotationSpeed { get; private set; }

    public string? KillFeedWeaponSpriteNameOverride { get; private set; }

    public bool IsDestroyed { get; private set; }

    public float ExplosionDamage { get; private set; } = BaseExplosionDamage;

    public bool IsCritical { get; private set; }

    public int FuseTicksLeft { get; private set; }

    public bool HasBounced { get; private set; }

    public bool IsStrongDrink { get; private set; }

    public float AppliedGravityPerTick { get; private set; } = GravityPerTick;

    public float CriticalDamageMultiplier { get; private set; } = 1f;

    public void ConfigureAsStrongDrink(int fuseTicks, float initialSpinSpeed, float gravityPerTick = StrongDrinkGravityPerTick)
    {
        IsStrongDrink = true;
        FuseTicksLeft = Math.Max(1, fuseTicks);
        RotationSpeed = initialSpinSpeed;
        AppliedGravityPerTick = MathF.Max(0f, gravityPerTick);
        ExplosionDamage = RocketProjectileEntity.ExplosionDamage;
        KillFeedWeaponSpriteNameOverride ??= StrongDrinkKillFeedSpriteName;
    }

    public void HydrateStrongDrink(bool isStrongDrink, float gravityPerTick = StrongDrinkGravityPerTick)
    {
        IsStrongDrink = isStrongDrink;
        if (isStrongDrink)
        {
            AppliedGravityPerTick = MathF.Max(0f, gravityPerTick);
            ExplosionDamage = RocketProjectileEntity.ExplosionDamage;
        }
        else
        {
            AppliedGravityPerTick = GravityPerTick;
        }
    }

    public void SetCritical(float damageMultiplier = ExperimentalGameplaySettings.KritzCriticalDamageMultiplier)
        => HydrateCritical(true, damageMultiplier);

    public void HydrateCritical(bool isCritical, float damageMultiplier)
    {
        IsCritical = isCritical;
        CriticalDamageMultiplier = isCritical
            ? ExperimentalGameplaySettings.NormalizeCriticalDamageMultiplier(damageMultiplier)
            : 1f;
    }

    public void AdvanceOneTick(float gravityScale = 1f)
    {
        PreviousX = X;
        PreviousY = Y;

        VelocityX *= HorizontalAirFriction;
        VelocityY = float.Min(MaxFallSpeed, VelocityY + AppliedGravityPerTick * gravityScale);
        VelocityY *= AirFriction;
        X += VelocityX;
        Y += VelocityY;

        RotationAngle += RotationSpeed;
        RotationSpeed *= IsStrongDrink ? StrongDrinkRotationFriction : RotationFriction;

        FuseTicksLeft -= 1;
    }

    public void MoveTo(float x, float y)
    {
        X = x;
        Y = y;
    }

    public void Bounce(float normalX, float normalY)
    {
        // Reflect velocity across surface normal and apply dampening
        var dotProduct = (VelocityX * normalX) + (VelocityY * normalY);
        VelocityX = (VelocityX - (2f * dotProduct * normalX)) * BounceVelocityRetention;
        VelocityY = (VelocityY - (2f * dotProduct * normalY)) * BounceVelocityRetention;
        HasBounced = true;
    }

    public void SetVelocity(float velocityX, float velocityY)
    {
        VelocityX = velocityX;
        VelocityY = velocityY;
    }

    public void ApplyRotationImpulse(float impulse)
    {
        RotationSpeed += impulse;
    }

    public void Destroy()
    {
        IsDestroyed = true;
    }

    public void Reflect(int ownerId, PlayerTeam team, float directionRadians)
    {
        OwnerId = ownerId;
        Team = team;
        var currentSpeed = MathF.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));
        var reflectedSpeed = MathF.Max(currentSpeed, ReflectedSpeedFloor);
        VelocityX = DeterministicMath.Cos(directionRadians) * reflectedSpeed;
        VelocityY = DeterministicMath.Sin(directionRadians) * reflectedSpeed;
        PreviousX = X;
        PreviousY = Y;
        FuseTicksLeft = FuseTicksRemaining;
        KillFeedWeaponSpriteNameOverride = "ReflectedGrenadeKL";
    }

    public void PushByAirblast(float directionRadians, float speedFloor)
    {
        var currentSpeed = MathF.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));
        var pushedSpeed = MathF.Max(currentSpeed, MathF.Max(0f, speedFloor));
        VelocityX = DeterministicMath.Cos(directionRadians) * pushedSpeed;
        VelocityY = DeterministicMath.Sin(directionRadians) * pushedSpeed;
        PreviousX = X;
        PreviousY = Y;
    }

    public void ApplyNetworkState(
        float x,
        float y,
        float velocityX,
        float velocityY,
        bool isDestroyed,
        float explosionDamage,
        int fuseTicksLeft)
    {
        PreviousX = X;
        PreviousY = Y;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        IsDestroyed = isDestroyed;
        ExplosionDamage = explosionDamage;
        FuseTicksLeft = fuseTicksLeft;
        RotationAngle = DeterministicMath.Atan2(velocityY, velocityX);
        RotationSpeed = 0f;
    }

    public void ApplyNetworkState(
        float x,
        float y,
        float previousX,
        float previousY,
        float velocityX,
        float velocityY,
        bool isDestroyed,
        float explosionDamage,
        int fuseTicksLeft)
    {
        PreviousX = previousX;
        PreviousY = previousY;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        IsDestroyed = isDestroyed;
        ExplosionDamage = explosionDamage;
        FuseTicksLeft = fuseTicksLeft;
        RotationAngle = DeterministicMath.Atan2(velocityY, velocityX);
        RotationSpeed = 0f;
    }
}
