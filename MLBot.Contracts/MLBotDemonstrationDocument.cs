using OpenGarrison.Core;

namespace OpenGarrison.MLBot.Contracts;

public enum MLBotDemonstrationCaptureKind
{
    Demonstration = 0,
    DaggerAssist = 1,
}

public sealed class MLBotDemonstrationDocument
{
    public MLBotDemonstrationMetadata Metadata { get; set; } = new();

    public MLBotDemonstrationSample[] Samples { get; set; } = [];
}

public sealed class MLBotDemonstrationMetadata
{
    public string SchemaVersion { get; set; } = "mlbot-demo-v6";

    public string LevelName { get; set; } = string.Empty;

    public int MapAreaIndex { get; set; } = 1;

    public GameModeKind Mode { get; set; }

    public PlayerTeam Team { get; set; }

    public PlayerClass ClassId { get; set; }

    public MLBotTaskPhase RequestedPhase { get; set; }

    public MLBotDemonstrationCaptureKind CaptureKind { get; set; }

    public string PolicyModelPath { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; set; }

    public int TickCount { get; set; }

    public int CaptureMaxTicks { get; set; }

    public bool ShortCapture { get; set; }

    public bool Success { get; set; }

    public string Outcome { get; set; } = string.Empty;
}

public sealed class MLBotDemonstrationSample
{
    public int Tick { get; set; }

    public MLBotTaskPhase ResolvedPhase { get; set; }

    public MLBotObservation Observation { get; set; }

    public MLBotAction Action { get; set; }

    public MLBotAction? HumanAction { get; set; }

    public MLBotAction? SuggestedAction { get; set; }

    public bool UsedHumanOverride { get; set; }

    public MLBotObservation NextObservation { get; set; }

    public bool PickedUpIntel { get; set; }

    public bool ScoredIntel { get; set; }

    public bool Died { get; set; }

    public bool EpisodeEnded { get; set; }
}
