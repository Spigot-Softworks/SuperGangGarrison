namespace OpenGarrison.Core;

public sealed record AirborneVelocityReachDefinition(
    float BonusPerExcessBaseline,
    float MaxReachMultiplier);

public sealed record PlayerKnockbackDefinition(
    float ImpulsePerUse,
    float AirborneVerticalScale,
    float GroundedVerticalScale);

public readonly record struct BulletKnockbackPayload(
    float Impulse,
    float AirborneVerticalScale,
    float GroundedVerticalScale)
{
    public static BulletKnockbackPayload None { get; } = new(0f, 0f, 0f);
}
