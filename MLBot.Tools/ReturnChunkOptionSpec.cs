using OpenGarrison.Core;

namespace OpenGarrison.MLBot.Tools;

internal sealed record ReturnChunkOptionSpec(
    string ModelPath,
    float EngageDistance,
    int CommitTicks,
    string? LevelNameFilter,
    PlayerTeam? TeamFilter,
    PlayerClass? ClassFilter,
    float? MinObjectiveRelativeX,
    float? MaxObjectiveRelativeX,
    float? MinObjectiveRelativeY,
    float? MaxObjectiveRelativeY);
