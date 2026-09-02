namespace OpenGarrison.GameplayModding;

public sealed record GameplayPlayerKnockbackDefinition(
    float ImpulsePerUse = 0f,
    float AirborneVerticalScale = 0.5f,
    float GroundedVerticalScale = 0.5f);
