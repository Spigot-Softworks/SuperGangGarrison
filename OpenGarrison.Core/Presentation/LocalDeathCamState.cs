namespace OpenGarrison.Core;

public sealed record LocalDeathCamState(
    float FocusX,
    float FocusY,
    string KillMessage,
    string KillerName,
    PlayerTeam? KillerTeam,
    int Health,
    int MaxHealth,
    int RemainingTicks,
    int InitialTicks = 0);
