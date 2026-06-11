namespace OpenGarrison.Core;

internal static class SpritesheetPlaybackRuntime
{
    public static void Reset(SimulationWorld world)
    {
        world.SpritesheetPlaybackState.ResetFromConfiguration(world.Level.SpritesheetPlaybackSet);
    }

    public static void ApplyControlSignals(
        SimulationWorld world,
        MapLogicGraph graph,
        SpritesheetPlaybackSet playbackSet,
        SpritesheetPlaybackRuntimeState runtimeState)
    {
        if (!playbackSet.HasEntries)
        {
            return;
        }

        runtimeState.EnsureSignalCount(playbackSet.Entries.Count * 3);
        for (var index = 0; index < playbackSet.Entries.Count; index += 1)
        {
            var entry = playbackSet.Entries[index];
            if (entry.RoomObjectIndex < 0 || entry.RoomObjectIndex >= world.SpritesheetPlaybackState.IsPlaying.Length)
            {
                continue;
            }

            if (!entry.Configuration.Autostart
                && entry.StartInputNodeIndex >= 0
                && runtimeState.RecordSignalTransition(index * 3, graph.GetOutput(entry.StartInputNodeIndex)))
            {
                StartPlayback(world, entry);
            }

            if (entry.StopInputNodeIndex >= 0
                && runtimeState.RecordSignalTransition((index * 3) + 1, graph.GetOutput(entry.StopInputNodeIndex)))
            {
                StopPlayback(world, entry.RoomObjectIndex);
            }

            if (!entry.Configuration.Autoplay
                && entry.NextFrameInputNodeIndex >= 0
                && runtimeState.RecordSignalTransition((index * 3) + 2, graph.GetOutput(entry.NextFrameInputNodeIndex)))
            {
                AdvanceManualFrame(world, entry);
            }
        }
    }

    public static void TickAutoplay(SimulationWorld world, float deltaSeconds)
    {
        var playbackSet = world.Level.SpritesheetPlaybackSet;
        if (!playbackSet.HasEntries)
        {
            return;
        }

        for (var index = 0; index < playbackSet.Entries.Count; index += 1)
        {
            var entry = playbackSet.Entries[index];
            if (!entry.Configuration.Autoplay
                || entry.RoomObjectIndex < 0
                || entry.RoomObjectIndex >= world.SpritesheetPlaybackState.IsPlaying.Length)
            {
                continue;
            }

            if (!world.SpritesheetPlaybackState.IsPlaying[entry.RoomObjectIndex]
                || world.SpritesheetPlaybackState.Completed[entry.RoomObjectIndex])
            {
                continue;
            }

            var state = world.SpritesheetPlaybackState;
            var ticksPerSecond = Math.Max(1, entry.Configuration.Framerate);
            state.FrameAccumulator[entry.RoomObjectIndex] += deltaSeconds * ticksPerSecond;
            while (state.FrameAccumulator[entry.RoomObjectIndex] >= 1f)
            {
                state.FrameAccumulator[entry.RoomObjectIndex] -= 1f;
                if (!TryAdvanceAutoplayFrame(world, entry))
                {
                    break;
                }
            }
        }
    }

    private static void StartPlayback(SimulationWorld world, SpritesheetPlaybackEntry entry)
    {
        var state = world.SpritesheetPlaybackState;
        state.IsPlaying[entry.RoomObjectIndex] = true;
        state.Completed[entry.RoomObjectIndex] = false;
        state.FrameAccumulator[entry.RoomObjectIndex] = 0f;
        if (entry.Configuration.LoopingMode == SpritesheetLoopingMode.PlayOnce)
        {
            state.CurrentFrame[entry.RoomObjectIndex] = 0;
            state.PlaybackDirection[entry.RoomObjectIndex] = 1;
        }
    }

    private static void StopPlayback(SimulationWorld world, int roomObjectIndex)
    {
        world.SpritesheetPlaybackState.IsPlaying[roomObjectIndex] = false;
        world.SpritesheetPlaybackState.FrameAccumulator[roomObjectIndex] = 0f;
    }

    private static void AdvanceManualFrame(SimulationWorld world, SpritesheetPlaybackEntry entry)
    {
        var roomObjectIndex = entry.RoomObjectIndex;
        var state = world.SpritesheetPlaybackState;
        if (state.Completed[roomObjectIndex])
        {
            return;
        }

        state.IsPlaying[roomObjectIndex] = true;
        _ = TryAdvanceFrame(
            state,
            entry.Configuration,
            roomObjectIndex,
            manualAdvance: true);
    }

    private static bool TryAdvanceAutoplayFrame(SimulationWorld world, SpritesheetPlaybackEntry entry)
    {
        return TryAdvanceFrame(
            world.SpritesheetPlaybackState,
            entry.Configuration,
            entry.RoomObjectIndex,
            manualAdvance: false);
    }

    private static bool TryAdvanceFrame(
        SpritesheetPlaybackState state,
        SpritesheetConfiguration configuration,
        int roomObjectIndex,
        bool manualAdvance)
    {
        if (configuration.LoopingMode == SpritesheetLoopingMode.PlayOnce
            && state.Completed[roomObjectIndex])
        {
            return false;
        }

        var frameCount = configuration.FrameCount;
        var current = state.CurrentFrame[roomObjectIndex];
        var direction = state.PlaybackDirection[roomObjectIndex];
        var next = current + direction;
        if (configuration.LoopingMode == SpritesheetLoopingMode.Loop)
        {
            if (next < 0)
            {
                next = frameCount - 1;
            }
            else if (next >= frameCount)
            {
                next = 0;
            }
        }
        else if (configuration.LoopingMode == SpritesheetLoopingMode.Reverse)
        {
            if (next >= frameCount)
            {
                next = frameCount - 2;
                direction = -1;
            }
            else if (next < 0)
            {
                next = 1;
                direction = 1;
            }
        }
        else
        {
            if (next >= frameCount)
            {
                next = frameCount - 1;
                state.Completed[roomObjectIndex] = true;
                state.IsPlaying[roomObjectIndex] = false;
            }
            else if (next < 0)
            {
                next = 0;
            }
        }

        state.CurrentFrame[roomObjectIndex] = Math.Clamp(next, 0, frameCount - 1);
        state.PlaybackDirection[roomObjectIndex] = direction >= 0 ? 1 : -1;
        return !manualAdvance || !state.Completed[roomObjectIndex];
    }
}
