namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{

    public SpritesheetPlaybackState SpritesheetPlaybackState => Level.SpritesheetPlaybackState;

    public int GetSpritesheetFrame(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= SpritesheetPlaybackState.CurrentFrame.Length)
        {
            return 0;
        }

        return SpritesheetPlaybackState.CurrentFrame[roomObjectIndex];
    }

    public void TickSpritesheetPlayback()
    {
        if (!Level.SpritesheetPlaybackSet.HasEntries)
        {
            return;
        }

        SpritesheetPlaybackRuntime.ApplyControlSignals(
            this,
            Level.LogicGraph,
            Level.SpritesheetPlaybackSet,
            MapRuntime.SpritesheetPlaybackRuntimeState);
        SpritesheetPlaybackRuntime.TickAutoplay(this, (float)Config.FixedDeltaSeconds);
    }

    private void ResetSpritesheetPlaybackRuntime()
    {
        MapRuntime.SpritesheetPlaybackRuntimeState.Reset();
        SpritesheetPlaybackRuntime.Reset(this);
    }
}
