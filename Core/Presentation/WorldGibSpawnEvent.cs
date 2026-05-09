namespace OpenGarrison.Core;

public readonly record struct WorldGibSpawnEvent(
    string SpriteName,
    int FrameIndex,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    float RotationSpeedDegrees,
    float HorizontalFriction,
    float RotationFriction,
    int LifetimeTicks,
    float BloodChance);
