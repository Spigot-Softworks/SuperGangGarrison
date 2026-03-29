namespace OpenGarrison.Core;

public sealed class StabMaskEntity : SimulationEntity
{
    public const int LifetimeTicks = 6;
    public const int DamagePerHit = 200;
    public const float StartOffset = 6f;
    public const float ReachLength = 33f;
    public const float Thickness = 12f;

    public StabMaskEntity(
        int id,
        int ownerId,
        PlayerTeam team,
        float x,
        float y,
        float directionDegrees) : base(id)
    {
        OwnerId = ownerId;
        Team = team;
        X = x;
        Y = y;
        DirectionDegrees = directionDegrees;
        TicksRemaining = LifetimeTicks;
    }

    public int OwnerId { get; }

    public PlayerTeam Team { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float DirectionDegrees { get; }

    public int TicksRemaining { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public void AdvanceOneTick(float ownerX, float ownerY)
    {
        X = ownerX;
        Y = ownerY;
        TicksRemaining -= 1;
    }

    public void Destroy()
    {
        TicksRemaining = 0;
    }
}
