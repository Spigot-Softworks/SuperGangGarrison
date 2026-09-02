namespace OpenGarrison.Core;

internal static class BulletKnockbackRules
{
    public const float LegacyImpulsePerProjectile = 0.5f;
    public const float SentryImpulsePerUse = 1f;
    private const float GroundedVerticalDirectionDeadZone = 0.1f;

    public static BulletKnockbackPayload ResolvePayload(
        PrimaryWeaponDefinition weapon,
        int actualProjectileCount)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        var scale = MathF.Max(0f, weapon.PlayerKnockbackScale);
        if (weapon.PlayerKnockback is not { } definition)
        {
            return new BulletKnockbackPayload(
                LegacyImpulsePerProjectile * scale,
                AirborneVerticalScale: 1f,
                GroundedVerticalScale: 1f);
        }

        var projectileCount = Math.Max(1, actualProjectileCount);
        return new BulletKnockbackPayload(
            MathF.Max(0f, definition.ImpulsePerUse) * scale / projectileCount,
            Math.Clamp(definition.AirborneVerticalScale, 0f, 1f),
            Math.Clamp(definition.GroundedVerticalScale, 0f, 1f));
    }

    public static BulletKnockbackPayload CreateSentryPayload()
        => new(
            SentryImpulsePerUse,
            AirborneVerticalScale: 0.5f,
            GroundedVerticalScale: 0.5f);

    public static void Apply(
        PlayerEntity target,
        float directionX,
        float directionY,
        in BulletKnockbackPayload payload)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.IsUbered || payload.Impulse <= 0f)
        {
            return;
        }

        var directionLength = MathF.Sqrt((directionX * directionX) + (directionY * directionY));
        if (!float.IsFinite(directionLength) || directionLength <= 0.0001f)
        {
            return;
        }

        directionX /= directionLength;
        directionY /= directionLength;
        var verticalScale = target.IsGrounded
            ? payload.GroundedVerticalScale
            : payload.AirborneVerticalScale;
        var verticalImpulse = directionY * payload.Impulse * Math.Clamp(verticalScale, 0f, 1f);
        if (target.IsGrounded
            && (directionY >= 0f || MathF.Abs(directionY) < GroundedVerticalDirectionDeadZone))
        {
            verticalImpulse = 0f;
        }

        target.AddImpulse(
            directionX * payload.Impulse * LegacyMovementModel.SourceTicksPerSecond,
            verticalImpulse * LegacyMovementModel.SourceTicksPerSecond);
    }
}
