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
    public const float BounceVelocityRetention = 0.55f; // Grenades lose 45% velocity on bounce
    public const float HorizontalAirFriction = 0.985f; // Per-tick horizontal velocity damping (stronger than vertical)
    public const float AirFriction = 0.998f; // Per-tick vertical velocity damping (very slight)
    public const float RotationFriction = 0.88f; // Per-tick rotation speed damping
    public const int FuseTicksRemaining = 60; // 2 seconds at 30 ticks/second
    public const float ReflectedSpeedFloor = 10f; // Minimum speed applied to a reflected grenade

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
        RotationAngle = MathF.Atan2(velocityY, velocityX);
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

    public float CriticalDamageMultiplier => IsCritical ? ExperimentalGameplaySettings.DefaultCriticalDamageMultiplier : 1f;

    public void SetCritical() { IsCritical = true; }

    public void AdvanceOneTick(float gravityScale = 1f)
    {
        PreviousX = X;
        PreviousY = Y;

        VelocityX *= HorizontalAirFriction;
        VelocityY = float.Min(MaxFallSpeed, VelocityY + GravityPerTick * gravityScale);
        VelocityY *= AirFriction;
        X += VelocityX;
        Y += VelocityY;

        RotationAngle += RotationSpeed;
        RotationSpeed *= RotationFriction;

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
        VelocityX = MathF.Cos(directionRadians) * reflectedSpeed;
        VelocityY = MathF.Sin(directionRadians) * reflectedSpeed;
        PreviousX = X;
        PreviousY = Y;
        FuseTicksLeft = FuseTicksRemaining;
        KillFeedWeaponSpriteNameOverride = "ReflectedGrenadeKL";
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
        RotationAngle = MathF.Atan2(velocityY, velocityX);
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
        RotationAngle = MathF.Atan2(velocityY, velocityX);
        RotationSpeed = 0f;
    }
}
