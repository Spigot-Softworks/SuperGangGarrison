namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private readonly Random _botAwarenessRandom = new(0xB07A);

    /// <summary>
    /// Samples the one global Spy awareness reaction distribution in whole
    /// simulation ticks. Restricting the sample to tick durations that fit
    /// the authored interval keeps the effective delay within 150-350 ms at
    /// every supported simulation rate.
    /// </summary>
    internal int NextBotSpyAwarenessDelayTicks(int ticksPerSecond)
    {
        ticksPerSecond = Math.Max(1, ticksPerSecond);
        var minimumTicks = Math.Max(1, (int)MathF.Ceiling(150f * ticksPerSecond / 1000f));
        var maximumTicks = Math.Max(minimumTicks, (int)MathF.Floor(350f * ticksPerSecond / 1000f));
        return _botAwarenessRandom.Next(minimumTicks, maximumTicks + 1);
    }
}
