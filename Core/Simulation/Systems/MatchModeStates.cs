namespace OpenGarrison.Core;

/// <summary>Owns VIP mode assignment and warmup state; the rules that mutate it stay in the world partials.</summary>
internal sealed class VipState
{
    public Dictionary<PlayerTeam, byte> SlotsByTeam { get; } = new();

    public Dictionary<PlayerTeam, byte> PreferredSlotsByTeam { get; } = new();

    public bool PracticeRulesEnabled { get; set; }

    public bool AllowDuplicateClasses { get; set; }

    public int WarmupTicksRemaining { get; set; }

    public int AssignmentVersion { get; set; }

    public int RoundStartVersion { get; set; }
}

/// <summary>Owns competitive ready-up and setup-phase state.</summary>
internal sealed class CompetitiveReadyUpState(int defaultSetupSeconds)
{
    public HashSet<byte> ReadySlots { get; } = new();

    public bool Enabled { get; set; }

    public int SetupSeconds { get; set; } = defaultSetupSeconds;

    public CompetitiveReadyUpPhase Phase { get; set; } = CompetitiveReadyUpPhase.Disabled;

    public int TicksRemaining { get; set; }

    public bool SuppressSkirmishOnNextRoundRestart { get; set; }
}
