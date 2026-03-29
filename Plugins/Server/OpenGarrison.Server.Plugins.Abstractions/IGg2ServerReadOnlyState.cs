using OpenGarrison.Core;

namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerReadOnlyState
{
    string ServerName { get; }

    string LevelName { get; }

    int MapAreaIndex { get; }

    int MapAreaCount { get; }

    GameModeKind GameMode { get; }

    MatchPhase MatchPhase { get; }

    int RedCaps { get; }

    int BlueCaps { get; }

    IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers();
}
