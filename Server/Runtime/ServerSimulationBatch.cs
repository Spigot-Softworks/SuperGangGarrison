using System;
using OpenGarrison.Core;

internal static class ServerSimulationBatch
{
    public static int Advance(
        FixedStepSimulator simulator,
        double elapsedSeconds,
        Action beforeTickAdvanced,
        Action onTickAdvanced,
        Action onSnapshotTickReady,
        int? maxTicksPerAdvance = null)
    {
        var ticks = simulator.Step(
            elapsedSeconds,
            beforeTickAdvanced,
            () =>
            {
                onTickAdvanced();
                onSnapshotTickReady();
            },
            maxTicksPerAdvance);
        if (ticks == 0)
        {
            return 0;
        }

        return ticks;
    }
}
