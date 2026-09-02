namespace OpenGarrison.GameplayModding;

public sealed record GameplayAirborneVelocityReachDefinition(
    string Baseline = "classRunJump",
    float BonusPerExcessBaseline = 0.5f,
    float MaxReachMultiplier = 1.5f);
