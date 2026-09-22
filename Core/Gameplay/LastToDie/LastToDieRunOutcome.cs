namespace OpenGarrison.Core.LastToDie;

public sealed record LastToDieParticipantOutcome(byte Slot, Guid ClientId, string SurvivorId, int ScoreUnits);

public sealed record LastToDieRunOutcome(
    Guid AttemptId,
    int CompletedRounds,
    LastToDieDifficulty Difficulty,
    IReadOnlyList<LastToDieParticipantOutcome> Participants)
{
    public long CompletedFrame { get; init; }
}
