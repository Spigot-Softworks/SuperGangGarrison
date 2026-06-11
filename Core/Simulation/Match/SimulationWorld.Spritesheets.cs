namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private SpritesheetPlaybackRuntimeState _spritesheetPlaybackRuntimeState = new();

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
            _spritesheetPlaybackRuntimeState);
        SpritesheetPlaybackRuntime.TickAutoplay(this, (float)Config.FixedDeltaSeconds);
    }

    private void ResetSpritesheetPlaybackRuntime()
    {
        _spritesheetPlaybackRuntimeState.Reset();
        SpritesheetPlaybackRuntime.Reset(this);
    }
}
