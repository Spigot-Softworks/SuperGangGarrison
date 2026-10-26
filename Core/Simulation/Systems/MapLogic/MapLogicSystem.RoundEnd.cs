using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class MapLogicSystem
{
    private MapLogicGraph? _roundEndGraph;
    private int[] _roundEndRemainingTicks = [];
    private bool[] _roundEndPreviousSignals = [];

    private void EnsureRoundEndCountdowns()
    {
        if (ReferenceEquals(_roundEndGraph, _host.Level.LogicGraph)) return;
        _roundEndGraph = _host.Level.LogicGraph;
        _roundEndRemainingTicks = new int[_roundEndGraph.RoundEnds.Count];
        _roundEndPreviousSignals = new bool[_roundEndGraph.RoundEnds.Count];
    }

    private void ResetRoundEndCountdowns()
    {
        EnsureRoundEndCountdowns();
        Array.Clear(_roundEndRemainingTicks);
        Array.Clear(_roundEndPreviousSignals);
        foreach (var definition in _roundEndGraph!.RoundEnds)
            _roundEndGraph.ClearExternalOutput(definition.OnCompleteNodeIndex);
        PublishRoundEndOutputs();
    }

    private int RoundEndSecondsToTicks(float seconds) =>
        Math.Max(0, (int)Math.Ceiling(seconds * _host.Config.TicksPerSecond));

    private void TickRoundEndCountdowns()
    {
        EnsureRoundEndCountdowns();
        // Damage logic can change ownership before the normal objective graph refresh.
        // A win must use those current owners, including on the final countdown tick.
        if (_roundEndRemainingTicks.Length > 0 && !_host.ClientPredictionMode && !_host.MatchState.IsEnded
            && ComputeMapLogicControlPointInputSignature() != _host.MapRuntime.LogicControlPointInputSignature)
            RefreshMapLogicRuntimeIfControlPointInputsChanged();
        for (var index = 0; index < _roundEndRemainingTicks.Length; index++)
        {
            if (_host.MatchState.IsEnded)
            {
                _roundEndRemainingTicks[index] = 0;
                continue;
            }
            if (_host.ClientPredictionMode)
            {
                // Only snapshots may start/cancel a network countdown or award a win.
                if (_roundEndRemainingTicks[index] > 1) _roundEndRemainingTicks[index]--;
                continue;
            }
            var definition = _roundEndGraph!.RoundEnds[index];
            var signal = _roundEndGraph.GetOutput(_roundEndGraph.Nodes[definition.NodeIndex].InputNodeIndex);
            var rising = signal && !_roundEndPreviousSignals[index];
            _roundEndPreviousSignals[index] = signal;
            if (!signal && definition.RequireContinuousSignal)
            {
                _roundEndRemainingTicks[index] = 0;
                continue;
            }
            if (rising && _roundEndRemainingTicks[index] == 0)
            {
                _roundEndRemainingTicks[index] = RoundEndSecondsToTicks(definition.DelaySeconds)
                    + RoundEndSecondsToTicks(definition.CountdownSeconds);
                if (_roundEndRemainingTicks[index] > 0) continue;
                CompleteRoundEnd(definition);
            }
            else if (_roundEndRemainingTicks[index] > 0 && --_roundEndRemainingTicks[index] == 0)
            {
                CompleteRoundEnd(definition);
            }
        }
        PublishRoundEndOutputs();
    }

    private void PublishRoundEndOutputs()
    {
        for (var index = 0; index < _roundEndRemainingTicks.Length; index++)
        {
            var definition = _roundEndGraph!.RoundEnds[index];
            var ticks = _roundEndRemainingTicks[index];
            _roundEndGraph.SetRoundEndOutput(definition.NodeIndex,
                !_host.MatchState.IsEnded && ticks > 0 && ticks <= RoundEndSecondsToTicks(definition.CountdownSeconds));
        }
    }

    private void CompleteRoundEnd(MapRoundEndDefinition definition)
    {
        if (!_host.TryEndRound(definition.Winner, "logic_round_end")) return;
        _roundEndGraph!.PulseExternalOutput(definition.OnCompleteNodeIndex);
        ApplyDamageableZoneDestroyWhenSignals();
        ApplyMapLogicActivators();
        ApplyMapLogicPresentationSignals();
    }

    internal IReadOnlyList<SnapshotMapRoundEndState> CaptureRoundEndCountdowns()
    {
        EnsureRoundEndCountdowns();
        var states = new List<SnapshotMapRoundEndState>();
        if (!_host.MatchState.IsEnded)
            for (var index = 0; index < _roundEndRemainingTicks.Length; index++)
                if (_roundEndRemainingTicks[index] > 0)
                    states.Add(new(index, _roundEndRemainingTicks[index]));
        return states;
    }

    internal void ApplyAuthoritativeRoundEndCountdowns(IReadOnlyList<SnapshotMapRoundEndState> states)
    {
        EnsureRoundEndCountdowns();
        Array.Clear(_roundEndRemainingTicks);
        foreach (var state in states)
            if ((uint)state.Index < (uint)_roundEndRemainingTicks.Length)
            {
                var definition = _roundEndGraph!.RoundEnds[state.Index];
                var maximum = RoundEndSecondsToTicks(definition.DelaySeconds) + RoundEndSecondsToTicks(definition.CountdownSeconds);
                _roundEndRemainingTicks[state.Index] = Math.Clamp(state.RemainingTicks, 0, maximum);
            }
        PublishRoundEndOutputs();
    }

    internal (int Index, MapRoundEndDefinition Definition, float SecondsRemaining)? GetActiveRoundEndCountdown()
    {
        EnsureRoundEndCountdowns();
        if (_host.MatchState.IsEnded) return null;
        for (var index = 0; index < _roundEndRemainingTicks.Length; index++)
        {
            var definition = _roundEndGraph!.RoundEnds[index];
            var ticks = _roundEndRemainingTicks[index];
            if (ticks > 0 && ticks <= RoundEndSecondsToTicks(definition.CountdownSeconds))
                return (index, definition, ticks / (float)_host.Config.TicksPerSecond);
        }
        return null;
    }
}
