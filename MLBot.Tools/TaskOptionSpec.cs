using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Tools;

internal sealed record TaskOptionSpec(
    string ModelPath,
    MLBotTaskPhase TaskPhase,
    float EngageDistance,
    string? LevelNameFilter,
    PlayerTeam? TeamFilter,
    PlayerClass? ClassFilter,
    float? MinObjectiveRelativeX,
    float? MaxObjectiveRelativeX,
    float? MinObjectiveRelativeY,
    float? MaxObjectiveRelativeY);
