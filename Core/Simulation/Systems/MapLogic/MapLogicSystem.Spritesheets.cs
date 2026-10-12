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
        TickMapLightLogic();
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

    private void ApplyMapLogicPresentationSignals()
    {
        // Damage impulses can be consumed by another graph evaluation in the same tick.
        // Latch their effects now, without advancing animation time a second time.
        var level = _host.Level;
        level.MapLightState.Tick(level.MapLights, level.LogicGraph, 0f);
        if (level.SpritesheetPlaybackSet.HasEntries)
            SpritesheetPlaybackRuntime.ApplyControlSignals(level, level.LogicGraph,
                level.SpritesheetPlaybackSet, _host.MapRuntime.SpritesheetPlaybackRuntimeState);
    }

    /// <summary>
    /// Placed lights listening to logic (power, colour) and their on/off transitions. Runs
    /// wherever spritesheet playback runs: on the server and on predicting clients.
    /// </summary>
    private void TickMapLightLogic()
    {
        var level = _host.Level;
        if (level.MapLights.Count == 0)
        {
            return;
        }

        level.MapLightState.Tick(level.MapLights, level.LogicGraph, (float)_host.Config.FixedDeltaSeconds);
    }

    private void ResetSpritesheetPlaybackRuntime()
    {
        _host.Level.MapLightState.Reset(_host.Level.MapLights);
        _host.MapRuntime.SpritesheetPlaybackRuntimeState.Reset();
        SpritesheetPlaybackRuntime.Reset(_host.Level);
    }
}
