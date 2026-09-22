namespace OpenGarrison.Core;

/// <summary>Uses work limits instead of machine-speed limits while recording or replaying a match.</summary>
public sealed class DeterministicSimulationScope : IDisposable
{
    [ThreadStatic] private static int _depth;
    public static bool IsActive => _depth > 0;
    private bool _disposed;

    public DeterministicSimulationScope() => _depth++;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _depth--;
    }
}
