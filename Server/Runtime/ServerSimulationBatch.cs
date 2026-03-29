using System;
using OpenGarrison.Core;

internal static class ServerSimulationBatch
{
    public static int Advance(
        FixedStepSimulator simulator,
        double elapsedSeconds,
        Action beforeTickAdvanced,
        Action onTickAdvanced,
        Action onSnapshotBatchReady)
    {
        var ticks = simulator.Step(elapsedSeconds, beforeTickAdvanced, onTickAdvanced);
        if (ticks > 0)
        {
            // If the server catches up multiple simulation ticks in one loop,
            // sending only the newest snapshot avoids burst-delivering stale frames.
            onSnapshotBatchReady();
        }

        return ticks;
    }
}
