namespace OpenGarrison.Core;

internal static class SpritesheetPlaybackRuntime
{
    public static void Reset(SimpleLevel level)
    {
        level.SpritesheetPlaybackState.ResetFromConfiguration(level.SpritesheetPlaybackSet);
    }

    public static void ApplyControlSignals(
        SimpleLevel level,
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
            if (entry.RoomObjectIndex < 0 || entry.RoomObjectIndex >= level.SpritesheetPlaybackState.IsPlaying.Length)
            {
                continue;
            }

            if (!entry.Configuration.Autostart
                && entry.StartInputNodeIndex >= 0
                && runtimeState.RecordSignalTransition(index * 3, graph.GetOutput(entry.StartInputNodeIndex)))
            {
                StartPlayback(level, entry);
            }

            if (entry.StopInputNodeIndex >= 0
                && runtimeState.RecordSignalTransition((index * 3) + 1, graph.GetOutput(entry.StopInputNodeIndex)))
            {
                StopPlayback(level, entry.RoomObjectIndex);
            }

            if (!entry.Configuration.Autoplay
                && entry.NextFrameInputNodeIndex >= 0
                && runtimeState.RecordSignalTransition((index * 3) + 2, graph.GetOutput(entry.NextFrameInputNodeIndex)))
            {
                AdvanceManualFrame(level, entry);
            }
        }
    }

    public static void TickAutoplay(SimpleLevel level, float deltaSeconds)
    {
        var playbackSet = level.SpritesheetPlaybackSet;
        if (!playbackSet.HasEntries)
        {
            return;
        }

        for (var index = 0; index < playbackSet.Entries.Count; index += 1)
        {
            var entry = playbackSet.Entries[index];
            if (!entry.Configuration.Autoplay
                || entry.RoomObjectIndex < 0
                || entry.RoomObjectIndex >= level.SpritesheetPlaybackState.IsPlaying.Length)
            {
                continue;
            }

            if (!level.SpritesheetPlaybackState.IsPlaying[entry.RoomObjectIndex]
                || level.SpritesheetPlaybackState.Completed[entry.RoomObjectIndex])
            {
                continue;
            }

            var state = level.SpritesheetPlaybackState;
            var ticksPerSecond = Math.Max(1, entry.Configuration.Framerate);
            state.FrameAccumulator[entry.RoomObjectIndex] += deltaSeconds * ticksPerSecond;
            while (state.FrameAccumulator[entry.RoomObjectIndex] >= 1f)
            {
                state.FrameAccumulator[entry.RoomObjectIndex] -= 1f;
                if (!TryAdvanceAutoplayFrame(level, entry))
                {
                    break;
                }
            }
        }
    }

    private static void StartPlayback(SimpleLevel level, SpritesheetPlaybackEntry entry)
    {
        var state = level.SpritesheetPlaybackState;
        state.IsPlaying[entry.RoomObjectIndex] = true;
        state.Completed[entry.RoomObjectIndex] = false;
        state.FrameAccumulator[entry.RoomObjectIndex] = 0f;
        if (entry.Configuration.LoopingMode == SpritesheetLoopingMode.PlayOnce)
        {
            state.CurrentFrame[entry.RoomObjectIndex] = 0;
            state.PlaybackDirection[entry.RoomObjectIndex] = 1;
        }
    }

    private static void StopPlayback(SimpleLevel level, int roomObjectIndex)
    {
        level.SpritesheetPlaybackState.IsPlaying[roomObjectIndex] = false;
        level.SpritesheetPlaybackState.FrameAccumulator[roomObjectIndex] = 0f;
    }

    private static void AdvanceManualFrame(SimpleLevel level, SpritesheetPlaybackEntry entry)
    {
        var roomObjectIndex = entry.RoomObjectIndex;
        var state = level.SpritesheetPlaybackState;
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

    private static bool TryAdvanceAutoplayFrame(SimpleLevel level, SpritesheetPlaybackEntry entry)
    {
        return TryAdvanceFrame(
            level.SpritesheetPlaybackState,
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
