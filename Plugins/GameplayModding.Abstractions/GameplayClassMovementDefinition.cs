namespace OpenGarrison.GameplayModding;

/// <summary>
/// Movement characteristics of a class.
/// </summary>
/// <param name="MaxHealth">The maximum health.</param>
/// <param name="CollisionLeft">The collision box left edge.</param>
/// <param name="CollisionTop">The collision box top edge.</param>
/// <param name="CollisionRight">The collision box right edge.</param>
/// <param name="CollisionBottom">The collision box bottom edge.</param>
/// <param name="RunPower">The run power.</param>
/// <param name="JumpStrength">The jump strength.</param>
/// <param name="MaxAirJumps">The maximum air jumps.</param>
/// <param name="TauntLengthFrames">The taunt length in frames.</param>
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
