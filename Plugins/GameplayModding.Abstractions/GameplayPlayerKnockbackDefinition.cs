namespace OpenGarrison.GameplayModding;

/// <summary>
/// Player knockback behavior for a gameplay item.
/// </summary>
/// <param name="ImpulsePerUse">The impulse applied per use.</param>
/// <param name="AirborneVerticalScale">The vertical scale applied while airborne.</param>
/// <param name="GroundedVerticalScale">The vertical scale applied while grounded.</param>
public sealed record GameplayPlayerKnockbackDefinition(
    float ImpulsePerUse = 0f,
    float AirborneVerticalScale = 0.5f,
    float GroundedVerticalScale = 0.5f);
