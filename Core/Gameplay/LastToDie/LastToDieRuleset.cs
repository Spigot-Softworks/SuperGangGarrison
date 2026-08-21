namespace OpenGarrison.Core.LastToDie;

public sealed record LastToDieStageDefinition(
    int StageNumber,
    int EnemyCount,
    int DurationTicks);

public sealed record LastToDieRuleset
{
    public const int CurrentVersion = 1;
    public const int SoloStartingEnemyCount = 2;
    public const int CoopStartingEnemyCount = 3;

    public int Version { get; init; } = CurrentVersion;

    public int TicksPerSecond { get; init; } = 30;

    public int MaximumPlayers { get; init; } = 2;

    public int StageCount { get; init; } = 9;

    public int StartingEnemyCount { get; init; } = SoloStartingEnemyCount;

    public int EnemyCountIncrement { get; init; } = 1;

    public int StartingStageMinutes { get; init; } = 3;

    public int StageMinuteIncrement { get; init; } = 1;

    public int RunTimeLimitMinutes { get; init; } = 30;

    public int RewardChoiceCount { get; init; } = 3;

    public int KillTimerReductionSeconds { get; init; } = 3;

    public static LastToDieRuleset CreateDefault(int ticksPerSecond = 30)
        => new() { TicksPerSecond = ticksPerSecond };

    public void Validate()
    {
        if (Version <= 0)
        {
            throw new InvalidOperationException("Last to Die ruleset version must be positive.");
        }

        if (TicksPerSecond <= 0)
        {
            throw new InvalidOperationException("Last to Die tick rate must be positive.");
        }

        if (MaximumPlayers is < 1 or > 2)
        {
            throw new InvalidOperationException("Last to Die currently supports one or two players.");
        }

        if (StageCount <= 0 || StartingEnemyCount <= 0 || EnemyCountIncrement < 0)
        {
            throw new InvalidOperationException("Last to Die stage and enemy counts must be valid.");
        }

        if (StartingStageMinutes <= 0 || StageMinuteIncrement < 0 || RunTimeLimitMinutes <= 0)
        {
            throw new InvalidOperationException("Last to Die time limits must be positive.");
        }

        if (RewardChoiceCount <= 0 || KillTimerReductionSeconds < 0)
        {
            throw new InvalidOperationException("Last to Die reward and kill-timer settings must be valid.");
        }
    }

    public LastToDieStageDefinition GetStage(int stageNumber)
    {
        Validate();
        if (stageNumber < 1 || stageNumber > StageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(stageNumber));
        }

        var offset = stageNumber - 1;
        var enemyCount = checked(StartingEnemyCount + (offset * EnemyCountIncrement));
        var durationMinutes = checked(StartingStageMinutes + (offset * StageMinuteIncrement));
        var durationTicks = checked(durationMinutes * 60 * TicksPerSecond);
        return new LastToDieStageDefinition(stageNumber, enemyCount, durationTicks);
    }

    public int RunTimeLimitTicks
    {
        get
        {
            Validate();
            return checked(RunTimeLimitMinutes * 60 * TicksPerSecond);
        }
    }

    public int KillTimerReductionTicks
    {
        get
        {
            Validate();
            return checked(KillTimerReductionSeconds * TicksPerSecond);
        }
    }
}
