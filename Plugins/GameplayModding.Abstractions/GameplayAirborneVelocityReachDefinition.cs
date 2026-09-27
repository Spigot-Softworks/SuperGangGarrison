namespace OpenGarrison.GameplayModding;

/// <summary>
/// Airborne velocity reach behavior for a gameplay item.
/// </summary>
/// <param name="Baseline">The baseline.</param>
/// <param name="BonusPerExcessBaseline">The bonus per excess baseline.</param>
/// <param name="MaxReachMultiplier">The maximum reach multiplier.</param>
public sealed record GameplayAirborneVelocityReachDefinition(
    string Baseline = "classRunJump",
    float BonusPerExcessBaseline = 0.5f,
    float MaxReachMultiplier = 1.5f);
