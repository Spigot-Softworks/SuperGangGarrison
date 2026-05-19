namespace OpenGarrison.Core;

public readonly record struct SniperAimIndicator(
    int SniperPlayerId,
    float X,
    float Y,
    PlayerTeam Team,
    float Transparency);
