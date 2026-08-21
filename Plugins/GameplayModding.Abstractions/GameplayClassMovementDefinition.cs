namespace OpenGarrison.GameplayModding;

public sealed record GameplayClassMovementDefinition(
    int MaxHealth,
    float CollisionLeft,
    float CollisionTop,
    float CollisionRight,
    float CollisionBottom,
    float RunPower,
    float JumpStrength,
    int MaxAirJumps,
    int TauntLengthFrames);
