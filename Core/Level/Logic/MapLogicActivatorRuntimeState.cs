using System;

namespace OpenGarrison.Core;

/// <summary>
/// Per-round activator edge memory used to resolve competing activators on the same target.
/// </summary>
public sealed class MapLogicActivatorRuntimeState
{
    private bool[] _previousSignals = [];
    private long[] _lastRisingEdgeOrders = [];
    private bool[] _toggleNextIsDisable = [];
    private bool[] _toggleActivateOnStartPending = [];
    private bool[] _logicInputHasActivated = [];
    private bool[] _toggleTargetActive = [];
    private bool[] _toggleTargetActiveValid = [];
    private long _edgeSequence;
    private long _currentTickRisingEdgeOrder = -1L;

    public static MapLogicActivatorRuntimeState CreateForActivatorCount(int activatorCount)
    {
        var state = new MapLogicActivatorRuntimeState();
        state.EnsureActivatorCount(activatorCount);
        return state;
    }

    public void EnsureActivatorCount(int activatorCount)
    {
        if (activatorCount <= 0)
        {
            _previousSignals = [];
            _lastRisingEdgeOrders = [];
            _toggleNextIsDisable = [];
            _toggleActivateOnStartPending = [];
            _logicInputHasActivated = [];
            _toggleTargetActive = [];
            _toggleTargetActiveValid = [];
            return;
        }

        if (_previousSignals.Length != activatorCount)
        {
            _previousSignals = new bool[activatorCount];
            _lastRisingEdgeOrders = new long[activatorCount];
            _toggleNextIsDisable = new bool[activatorCount];
            _toggleActivateOnStartPending = new bool[activatorCount];
            _logicInputHasActivated = new bool[activatorCount];
            Array.Fill(_lastRisingEdgeOrders, -1L);
            Array.Fill(_toggleNextIsDisable, true);
            Array.Clear(_toggleActivateOnStartPending, 0, _toggleActivateOnStartPending.Length);
            Array.Clear(_logicInputHasActivated, 0, _logicInputHasActivated.Length);
        }
    }

    public void EnsureToggleTargetStateCount(int roomObjectCount)
    {
        if (roomObjectCount <= 0)
        {
            _toggleTargetActive = [];
            _toggleTargetActiveValid = [];
            return;
        }

        if (_toggleTargetActive.Length != roomObjectCount)
        {
            _toggleTargetActive = new bool[roomObjectCount];
            _toggleTargetActiveValid = new bool[roomObjectCount];
        }
    }

    public void Reset()
    {
        if (_previousSignals.Length > 0)
        {
            Array.Clear(_previousSignals, 0, _previousSignals.Length);
            Array.Fill(_lastRisingEdgeOrders, -1L);
            Array.Fill(_toggleNextIsDisable, true);
            Array.Clear(_toggleActivateOnStartPending, 0, _toggleActivateOnStartPending.Length);
            Array.Clear(_logicInputHasActivated, 0, _logicInputHasActivated.Length);
        }

        if (_toggleTargetActiveValid.Length > 0)
        {
            Array.Clear(_toggleTargetActive, 0, _toggleTargetActive.Length);
            Array.Clear(_toggleTargetActiveValid, 0, _toggleTargetActiveValid.Length);
        }

        _edgeSequence = 0L;
        _currentTickRisingEdgeOrder = -1L;
    }

    internal void SetToggleTargetActive(int targetRoomObjectIndex, bool activeValue)
    {
        if (targetRoomObjectIndex < 0 || targetRoomObjectIndex >= _toggleTargetActive.Length)
        {
            return;
        }

        _toggleTargetActive[targetRoomObjectIndex] = activeValue;
        _toggleTargetActiveValid[targetRoomObjectIndex] = true;
    }

    internal bool TryGetToggleTargetActive(int targetRoomObjectIndex, out bool activeValue)
    {
        activeValue = true;
        if (targetRoomObjectIndex < 0
            || targetRoomObjectIndex >= _toggleTargetActiveValid.Length
            || !_toggleTargetActiveValid[targetRoomObjectIndex])
        {
            return false;
        }

        activeValue = _toggleTargetActive[targetRoomObjectIndex];
        return true;
    }

    internal void BeginToggleActivateOnStart(int activatorIndex)
    {
        if (activatorIndex < 0 || activatorIndex >= _toggleNextIsDisable.Length)
        {
            return;
        }

        _toggleActivateOnStartPending[activatorIndex] = true;
        _toggleNextIsDisable[activatorIndex] = false;
    }

    internal void BeginToggleActivateOnStartTarget(int targetRoomObjectIndex)
    {
        SetToggleTargetActive(targetRoomObjectIndex, activeValue: false);
    }

    internal bool ResolveToggleRisingEdgeActiveValue(int activatorIndex)
    {
        if (activatorIndex < 0 || activatorIndex >= _toggleNextIsDisable.Length)
        {
            return true;
        }

        var nextIsDisable = _toggleNextIsDisable[activatorIndex];
        _toggleNextIsDisable[activatorIndex] = !nextIsDisable;
        _toggleActivateOnStartPending[activatorIndex] = false;
        return !nextIsDisable;
    }

    internal bool IsToggleActivateOnStartPending(int activatorIndex)
    {
        return activatorIndex >= 0
            && activatorIndex < _toggleActivateOnStartPending.Length
            && _toggleActivateOnStartPending[activatorIndex];
    }

    internal bool HasLogicInputActivated(int activatorIndex)
    {
        return activatorIndex >= 0
            && activatorIndex < _logicInputHasActivated.Length
            && _logicInputHasActivated[activatorIndex];
    }

    internal void MarkLogicInputActivated(int activatorIndex)
    {
        if (activatorIndex < 0 || activatorIndex >= _logicInputHasActivated.Length)
        {
            return;
        }

        _logicInputHasActivated[activatorIndex] = true;
    }

    internal void BeginEvaluationTick()
    {
        _currentTickRisingEdgeOrder = -1L;
    }

    internal void RecordSignalTransition(int activatorIndex, bool currentSignal)
    {
        if (activatorIndex < 0 || activatorIndex >= _previousSignals.Length)
        {
            return;
        }

        if (currentSignal && !_previousSignals[activatorIndex])
        {
            if (_currentTickRisingEdgeOrder < 0L)
            {
                _edgeSequence += 1L;
                _currentTickRisingEdgeOrder = _edgeSequence;
            }

            _lastRisingEdgeOrders[activatorIndex] = _currentTickRisingEdgeOrder;
        }

        _previousSignals[activatorIndex] = currentSignal;
    }

    internal bool TryGetLastRisingEdgeOrder(int activatorIndex, out long risingEdgeOrder)
    {
        risingEdgeOrder = -1L;
        if (activatorIndex < 0 || activatorIndex >= _lastRisingEdgeOrders.Length)
        {
            return false;
        }

        risingEdgeOrder = _lastRisingEdgeOrders[activatorIndex];
        return risingEdgeOrder >= 0L;
    }
}
