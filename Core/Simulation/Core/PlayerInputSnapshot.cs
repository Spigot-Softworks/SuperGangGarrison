namespace OpenGarrison.Core;

public readonly record struct PlayerInputSnapshot(
    bool Left,
    bool Right,
    bool Up,
    bool Down,
    bool BuildSentry,
    bool DestroySentry,
    bool Taunt,
    bool FirePrimary,
    bool FireSecondary,
    float AimWorldX,
    float AimWorldY,
    bool DebugKill,
    bool DropIntel = false,
    bool UseAbility = false,
    bool InteractWeapon = false,
    bool IsUsingBinoculars = false,
    float BinocularsFocusX = 0f,
    float BinocularsFocusY = 0f,
    bool SwapWeapon = false,
    bool ReadyUp = false,
    bool IsTypingChatMessage = false);
