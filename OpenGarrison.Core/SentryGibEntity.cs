namespace OpenGarrison.Core;

public sealed class SentryGibEntity : SimulationEntity
{
    public const int LifetimeTicks = 150;
    public const float PickupRadius = 18f;
    public const float MetalValue = 50f;

    public SentryGibEntity(int id, PlayerTeam team, float x, float y) : base(id)
    {
        Team = team;
        X = x;
        Y = y;
        TicksRemaining = LifetimeTicks;
    }

    public PlayerTeam Team { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public int TicksRemaining { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public void AdvanceOneTick()
    {
        TicksRemaining -= 1;
    }

    public void ApplyNetworkState(float x, float y, int ticksRemaining)
    {
        X = x;
        Y = y;
        TicksRemaining = ticksRemaining;
    }
}
