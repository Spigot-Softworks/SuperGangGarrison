using OpenGarrison.Core;

namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerPlayerInfo(
    byte Slot,
    string Name,
    bool IsSpectator,
    bool IsAuthorized,
    PlayerTeam? Team,
    PlayerClass? PlayerClass,
    string EndPoint);
