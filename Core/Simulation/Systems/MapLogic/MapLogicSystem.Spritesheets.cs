namespace OpenGarrison.Core;

internal sealed partial class MapLogicSystem
{

    internal SpritesheetPlaybackState SpritesheetPlaybackState => _host.Level.SpritesheetPlaybackState;

    internal int GetSpritesheetFrame(int roomObjectIndex)
    {
        if (roomObjectIndex < 0 || roomObjectIndex >= SpritesheetPlaybackState.CurrentFrame.Length)
        {
            return 0;
        }

        return SpritesheetPlaybackState.CurrentFrame[roomObjectIndex];
    }

    internal void TickSpritesheetPlayback()
    {
        if (!_host.Level.SpritesheetPlaybackSet.HasEntries)
        {
            return;
        }

        SpritesheetPlaybackRuntime.ApplyControlSignals(
            _host.Level,
            _host.Level.LogicGraph,
            _host.Level.SpritesheetPlaybackSet,
            _host.MapRuntime.SpritesheetPlaybackRuntimeState);
        SpritesheetPlaybackRuntime.TickAutoplay(_host.Level, (float)_host.Config.FixedDeltaSeconds);
    }

    private void ResetSpritesheetPlaybackRuntime()
    {
        _host.MapRuntime.SpritesheetPlaybackRuntimeState.Reset();
        SpritesheetPlaybackRuntime.Reset(_host.Level);
    }
}
