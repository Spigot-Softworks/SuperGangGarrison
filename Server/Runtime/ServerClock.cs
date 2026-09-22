using System.Diagnostics;

namespace OpenGarrison.Server;

internal sealed class ServerClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan? _simulationTime;
    public TimeSpan Elapsed => _simulationTime ?? _stopwatch.Elapsed;
    public void UseSimulationTime() => _simulationTime = TimeSpan.Zero;
    public void Advance(double seconds)
    {
        if (_simulationTime.HasValue) _simulationTime += TimeSpan.FromSeconds(seconds);
    }
}
