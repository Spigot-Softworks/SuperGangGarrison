using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.SessionRuntime;

public sealed record EmbeddedSessionOptions
{
    public bool LastToDie { get; init; } = true;
    public int MaximumPlayers { get; init; } = 4;
    public int TickRate { get; init; } = 30;
    public string Name { get; init; } = "Private room";
    public string Map { get; init; } = "Harvest";
    public bool PreferClassicMaps { get; init; }
    public int MapArea { get; init; } = 1;
    public int TimeLimitMinutes { get; init; } = 15;
    public int CaptureLimit { get; init; } = 5;
    public int RespawnSeconds { get; init; } = 5;
    public int RedBots { get; init; }
    public int BlueBots { get; init; }
    public bool SpecialAbilities { get; init; } = true;
    public LastToDieDifficulty Difficulty { get; init; }
    public ulong? Seed { get; init; }

    public void Validate()
    {
        if (MaximumPlayers is < 1 or > 4 || TickRate is not (30 or 60 or 120)
            || string.IsNullOrWhiteSpace(Map) || MapArea < 1
            || TimeLimitMinutes is < 5 or > 60 || CaptureLimit is < 1 or > 10
            || RespawnSeconds is not (0 or 3 or 5 or 10 or 15)
            || RedBots is < 0 or > 9 || BlueBots is < 0 or > 9
            || !Enum.IsDefined(Difficulty))
            throw new ArgumentException("Invalid private session settings.");
    }
}
