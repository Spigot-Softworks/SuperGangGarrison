using System;

namespace OpenGarrison.Core;

public sealed class SimulationConfig
{
    public const int DefaultTicksPerSecond = 30;
    public const int MinimumTicksPerSecond = 30;
    public const int MaximumTicksPerSecond = 120;

    public int TicksPerSecond { get; init; } = DefaultTicksPerSecond;

    public double FixedDeltaSeconds => 1.0 / TicksPerSecond;

    public bool EnableLocalDummies { get; init; } = true;

    public bool EnableEnemyTrainingDummy { get; init; } = true;

    public bool EnableFriendlySupportDummy { get; init; } = true;

    public static int NormalizeTicksPerSecond(int ticksPerSecond)
    {
        return ticksPerSecond > 0
            ? Math.Clamp(ticksPerSecond, MinimumTicksPerSecond, MaximumTicksPerSecond)
            : DefaultTicksPerSecond;
    }
}
