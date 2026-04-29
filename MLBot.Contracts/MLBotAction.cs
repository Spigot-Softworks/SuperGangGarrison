namespace OpenGarrison.MLBot.Contracts;

public readonly record struct MLBotAction(
    int MoveDirection,
    bool Jump,
    bool Crouch,
    bool FirePrimary,
    bool FireSecondary,
    bool DropIntel,
    float AimWorldX,
    float AimWorldY)
{
    public static MLBotAction Idle(float aimWorldX, float aimWorldY) => new(
        MoveDirection: 0,
        Jump: false,
        Crouch: false,
        FirePrimary: false,
        FireSecondary: false,
        DropIntel: false,
        AimWorldX: aimWorldX,
        AimWorldY: aimWorldY);
}
