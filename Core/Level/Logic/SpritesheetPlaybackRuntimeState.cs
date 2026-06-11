namespace OpenGarrison.Core;

public sealed class SpritesheetPlaybackRuntimeState
{
    private bool[] _previousSignals = [];

    public void EnsureSignalCount(int signalCount)
    {
        if (signalCount <= 0)
        {
            _previousSignals = [];
            return;
        }

        if (_previousSignals.Length != signalCount)
        {
            _previousSignals = new bool[signalCount];
        }
    }

    public void Reset()
    {
        if (_previousSignals.Length > 0)
        {
            Array.Clear(_previousSignals, 0, _previousSignals.Length);
        }
    }

    public bool RecordSignalTransition(int signalIndex, bool currentSignal)
    {
        if (signalIndex < 0 || signalIndex >= _previousSignals.Length)
        {
            return false;
        }

        var risingEdge = currentSignal && !_previousSignals[signalIndex];
        _previousSignals[signalIndex] = currentSignal;
        return risingEdge;
    }
}
