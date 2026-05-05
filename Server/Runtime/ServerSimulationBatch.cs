using System;
using OpenGarrison.Core;

internal static class ServerSimulationBatch
{
    public static int Advance(
        FixedStepSimulator simulator,
        double elapsedSeconds,
        Action beforeTickAdvanced,
        Action onTickAdvanced,
        Action onSnapshotBatchReady,
        int? maxTicksPerAdvance = null)
    {
        var ticks = simulator.Step(elapsedSeconds, beforeTickAdvanced, onTickAdvanced, maxTicksPerAdvance);
        if (ticks > 0)
        {
            // If the server catches up multiple simulation ticks in one loop,
            // sending only the newest snapshot avoids burst-delivering stale frames.
            onSnapshotBatchReady();
        }

        return ticks;
    }
}
