using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Tools;

internal sealed class MLBotEvaluationTrace
{
    public string LevelName { get; set; } = string.Empty;

    public PlayerTeam Team { get; set; }

    public PlayerClass ClassId { get; set; }

    public MLBotTaskPhase TaskPhase { get; set; }

    public bool Success { get; set; }

    public int TicksElapsed { get; set; }

    public float TotalReward { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public string TerminalReason { get; set; } = string.Empty;

    public int? PickupTick { get; set; }

    public int? ScoreTick { get; set; }

    public int? CaptureTick { get; set; }

    public float MaxStuckTicks { get; set; }

    public float MinObjectiveDistance { get; set; }

    public float FinalObjectiveDistance { get; set; }

    public float MinNavigationDistance { get; set; }

    public float FinalNavigationDistance { get; set; }

    public float MinWaypointDistance { get; set; }

    public float FinalWaypointDistance { get; set; }

    public MLBotTaskPhase FinalPhase { get; set; }

    public MLBotTraceEvent[] Events { get; set; } = [];
}

internal sealed class MLBotTraceEvent
{
    public int Tick { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
