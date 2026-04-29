using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Tools;

internal sealed record TaskChunkOptionSpec(
    string ModelPath,
    MLBotTaskPhase TaskPhase,
    float EngageDistance,
    int CommitTicks,
    string? LevelNameFilter,
    PlayerTeam? TeamFilter,
    PlayerClass? ClassFilter,
    float? MinObjectiveRelativeX,
    float? MaxObjectiveRelativeX,
    float? MinObjectiveRelativeY,
    float? MaxObjectiveRelativeY,
    bool RequiresCarryingIntel,
    float MinEngageDistance,
    bool LatchAcrossChunks,
    bool? RequiredIsGrounded);
