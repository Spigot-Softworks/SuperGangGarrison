namespace OpenGarrison.Core;

public sealed class FixedStepSimulator
{
    private readonly SimulationWorld _world;
    private double _accumulatorSeconds;

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
        var frameDelta = _world.Config.FixedDeltaSeconds;
        _accumulatorSeconds += elapsedSeconds;

        var ticks = 0;

        while (_accumulatorSeconds >= frameDelta)
        {
            beforeTickAdvanced?.Invoke();
            _world.AdvanceOneTick();
            _accumulatorSeconds -= frameDelta;
            ticks += 1;
            onTickAdvanced?.Invoke();
        }

        return ticks;
    }
}
