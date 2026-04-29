using OpenGarrison.Core;

namespace OpenGarrison.MLBot.Contracts;

public readonly record struct MLBotEvaluationResult(
    string LevelName,
    PlayerTeam Team,
    PlayerClass ClassId,
    MLBotTaskPhase TaskPhase,
    bool Success,
    int TicksElapsed,
    float TotalReward,
    string Outcome);
