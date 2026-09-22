namespace OpenGarrison.Core.LastToDie;

public sealed record LastToDieStageDefinition(
    int StageNumber,
    int EnemyCount,
    int DurationTicks);

public sealed record LastToDieRuleset
{
    public const int CurrentVersion = 4;
    public const int SoloStartingEnemyCount = 2;
    public const int CoopStartingEnemyCount = 3;
    public const int MaximumEnemyCount = 16;
    public const int SniperStartingStage = 9;
    public const int EnemyScalingStartingStage = 10;
    public const float EnemyScalingPerStage = 0.05f;

    public int Version { get; init; } = CurrentVersion;

    public int TicksPerSecond { get; init; } = 30;

    public int MaximumPlayers { get; init; } = 2;

    public int StageCount { get; init; } = 9;

    public bool Endless { get; init; } = true;

    public int StartingEnemyCount { get; init; } = SoloStartingEnemyCount;

    public int EnemyCountIncrement { get; init; } = 1;

    public int StartingStageMinutes { get; init; } = 1;

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

        if (MaximumPlayers is < 1 or > 4)
        {
            throw new InvalidOperationException("Last to Die supports one to four players.");
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
        if (stageNumber < 1 || (!Endless && stageNumber > StageCount))
        {
            throw new ArgumentOutOfRangeException(nameof(stageNumber));
        }

        // Endless rounds continue increasing the enemy wave until the fixed
        // population cap. Stage time retains its former final-stage duration.
        var enemyCount = (int)Math.Clamp(
            (long)StartingEnemyCount + ((long)(stageNumber - 1) * EnemyCountIncrement),
            1,
            MaximumEnemyCount);
        var durationOffset = Math.Min(stageNumber - 1, StageCount - 1);
        var durationMinutes = checked(StartingStageMinutes + (durationOffset * StageMinuteIncrement));
        var durationTicks = checked(durationMinutes * 60 * TicksPerSecond);
        return new LastToDieStageDefinition(stageNumber, enemyCount, durationTicks);
    }

    public static bool CanSpawnSniper(int stageNumber)
        => stageNumber >= SniperStartingStage;

    public static float GetEnemyStatMultiplier(int stageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stageNumber, 1);
        return 1f + (Math.Max(0, stageNumber - (EnemyScalingStartingStage - 1)) * EnemyScalingPerStage);
    }

    public static float GetEnemyDamageMultiplier(int stageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stageNumber, 1);
        return 1f + (Math.Max(0, stageNumber - EnemyScalingStartingStage) * EnemyScalingPerStage);
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
