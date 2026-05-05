namespace OpenGarrison.Core;

public sealed class FixedStepSimulator
{
    private readonly SimulationWorld _world;
    private double _accumulatorSeconds;

    public SimulationWorld World => _world;

    public FixedStepSimulator(SimulationWorld world)
    {
        _world = world;
    }

    public int Step(double elapsedSeconds, Action? onTickAdvanced = null)
    {
        return Step(elapsedSeconds, beforeTickAdvanced: null, onTickAdvanced);
    }

    public int Step(double elapsedSeconds, Action? beforeTickAdvanced, Action? onTickAdvanced)
    {
        return Step(elapsedSeconds, beforeTickAdvanced, onTickAdvanced, maxTicksPerAdvance: null);
    }

    public int Step(
        double elapsedSeconds,
        Action? beforeTickAdvanced,
        Action? onTickAdvanced,
        int? maxTicksPerAdvance)
    {
        var frameDelta = _world.Config.FixedDeltaSeconds;
        _accumulatorSeconds += elapsedSeconds;

        var ticks = 0;

        while (_accumulatorSeconds >= frameDelta
            && (!maxTicksPerAdvance.HasValue || ticks < maxTicksPerAdvance.Value))
        {
            beforeTickAdvanced?.Invoke();
            _world.AdvanceOneTick();
            _accumulatorSeconds -= frameDelta;
            ticks += 1;
            onTickAdvanced?.Invoke();
        }

        if (maxTicksPerAdvance.HasValue
            && ticks >= maxTicksPerAdvance.Value
            && _accumulatorSeconds >= frameDelta)
        {
            _accumulatorSeconds = 0d;
        }

        return ticks;
    }
}
