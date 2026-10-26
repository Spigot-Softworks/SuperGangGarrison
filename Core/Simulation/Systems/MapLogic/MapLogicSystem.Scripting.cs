using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class MapLogicSystem
{
    private void ResetMapScript()
    {
        var level = _host.Level;
        level.ClearMapScriptOverrides();
        var state = _host.MapRuntime;
        state.MapScriptLevel = level;
        state.MapScriptSignals.Clear();
        state.MapScriptSignalEvents.Clear();
        state.AuthoritativeMapScriptError = null;
        state.MapScript = null;
        if (_host.ClientPredictionMode || string.IsNullOrWhiteSpace(level.MapScriptSource)) return;
        int NodeIndex(string name)
        {
            var key = level.MapScriptNodeNames.GetValueOrDefault(name, name);
            if (!level.LogicGraph.TryGetNodeIndex(key, out var index))
                throw new MoonSharp.Interpreter.ScriptRuntimeException($"Unknown logic node '{name}'. Give the node a name in properties.");
            return index;
        }
        state.MapScript = new MapScriptRuntime(level,
            owner: index => _host.Objectives.ControlPoints.Points.FirstOrDefault(point =>
                (ControlPointMarkerIndex.TryGetIndex(point.Marker, out var logicalIndex) ? logicalIndex : point.Index) == index)?.Team,
            pulse: name => PulseMapLogicNode(NodeIndex(name)),
            signal: name => level.LogicGraph.GetOutput(NodeIndex(name)),
            score: (team, amount) => _host.TryModifyTeamScore(team, amount, "map_script"),
            endRound: team => _host.TryEndRound(team, "map_script"));
        foreach (var (name, key) in level.MapScriptNodeNames)
            if (level.LogicGraph.TryGetNodeIndex(key, out var index)) state.MapScriptSignals[name] = level.LogicGraph.GetOutput(index);
        state.MapScript.Start();
    }

    private void TickMapScript()
    {
        if (_host.ClientPredictionMode) return;
        if (!ReferenceEquals(_host.MapRuntime.MapScriptLevel, _host.Level)) ResetMapScript();
        var runtime = _host.MapRuntime.MapScript;
        if (runtime is null || runtime.Error is not null) return;
        runtime.Tick(_host.Config.FixedDeltaSeconds);
        QueueMapScriptSignalChanges();
        var count = _host.MapRuntime.MapScriptSignalEvents.Count;
        for (var eventIndex = 0; eventIndex < count && runtime.Error is null; eventIndex++)
        {
            var signal = _host.MapRuntime.MapScriptSignalEvents.Dequeue();
            runtime.Signal(signal.Name, signal.Active);
        }
    }

    private void QueueMapScriptSignalChanges()
    {
        if (_host.ClientPredictionMode || !ReferenceEquals(_host.MapRuntime.MapScriptLevel, _host.Level)
            || _host.MapRuntime.MapScript is not { Error: null } runtime) return;
        foreach (var (name, key) in _host.Level.MapScriptNodeNames)
        {
            if (!_host.Level.LogicGraph.TryGetNodeIndex(key, out var index)) continue;
            var value = _host.Level.LogicGraph.GetOutput(index);
            if (_host.MapRuntime.MapScriptSignals.TryGetValue(name, out var previous) && previous == value) continue;
            _host.MapRuntime.MapScriptSignals[name] = value;
            if (_host.MapRuntime.MapScriptSignalEvents.Count >= 256)
            { runtime.Abort("Too many map node signal events in one tick (256 maximum)."); return; }
            _host.MapRuntime.MapScriptSignalEvents.Enqueue((name, value));
        }
    }

    internal void NotifyMapScriptCapture(ControlPointState point, PlayerTeam team)
    {
        if (_host.ClientPredictionMode) return;
        if (!ReferenceEquals(_host.MapRuntime.MapScriptLevel, _host.Level)) ResetMapScript();
        var index = ControlPointMarkerIndex.TryGetIndex(point.Marker, out var logicalIndex) ? logicalIndex : point.Index;
        _host.MapRuntime.MapScript?.Capture(index, team);
    }

    internal SnapshotMapScriptObjectState[] CaptureMapScriptObjects()
        => Enumerable.Range(0, _host.Level.MapScriptObjectOverrides.Length)
            .Where(index => _host.Level.MapScriptObjectOverrides[index].HasValue)
            .Select(index => new SnapshotMapScriptObjectState(index, _host.Level.MapScriptObjectOverrides[index]!.Value)).ToArray();

    internal void ApplyAuthoritativeMapScriptObjects(IReadOnlyList<SnapshotMapScriptObjectState> objects)
    {
        _host.Level.ClearMapScriptOverrides();
        foreach (var item in objects)
            if (item.Index >= 0 && item.Index < _host.Level.MapScriptObjectOverrides.Length)
                _host.Level.SetMapScriptObjectOverride(item.Index, item.Active);
    }

    internal void ApplyAuthoritativeMapScriptError(string error)
        => _host.MapRuntime.AuthoritativeMapScriptError = string.IsNullOrWhiteSpace(error) ? null : error;
}
