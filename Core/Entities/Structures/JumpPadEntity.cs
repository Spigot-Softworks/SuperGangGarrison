namespace OpenGarrison.Core;

public sealed class JumpPadEntity : SimulationEntity
{
    public const float Width = 20f;
    public const float Height = 20f;
    public const float GravityPerTick = 0.6f;
    public const float MaxFallSpeed = 10f;
    public const int MaxHealth = 50;

    public JumpPadEntity(int id, int ownerPlayerId, PlayerTeam team, float x, float y) : base(id)
    {
        OwnerPlayerId = ownerPlayerId;
        Team = team;
        X = x;
        Y = y;
        Health = MaxHealth;
    }

    public int OwnerPlayerId { get; }

    public PlayerTeam Team { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float VerticalSpeed { get; private set; } = 0.001f;

    public bool HasLanded { get; private set; }

    public int Health { get; private set; }

    public bool IsDead => Health <= 0;

    public void Advance(SimpleLevel level, WorldBounds bounds)
    {
        if (!HasLanded)
        {
            VerticalSpeed = float.Min(MaxFallSpeed, VerticalSpeed + GravityPerTick);
            Y += VerticalSpeed;

            foreach (var solid in level.Solids)
            {
                if (!IntersectsSolid(solid))
                {
                    continue;
                }

                Y = solid.Top - (Height / 2f);
                VerticalSpeed = 0f;
                HasLanded = true;
                break;
            }

            var clampedY = bounds.ClampY(Y, Height);
            if (clampedY != Y)
            {
                Y = clampedY;
                VerticalSpeed = 0f;
                HasLanded = true;
            }
        }
    }

    public bool IsNear(float x, float y, float radius)
    {
        var deltaX = X - x;
        var deltaY = Y - y;
        return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
    }

    public void TakeDamage(int amount)
    {
        Health = int.Max(0, Health - amount);
    }

    public void ApplyNetworkState(float x, float y, int health, bool hasLanded)
    {
        X = x;
        Y = y;
        Health = health;
        HasLanded = hasLanded;
    }

    private bool IntersectsSolid(LevelSolid solid)
    {
        var left = X - (Width / 2f);
        var right = X + (Width / 2f);
        var top = Y - (Height / 2f);
        var bottom = Y + (Height / 2f);
        return left < solid.Right
            && right > solid.Left
            && top < solid.Bottom
            && bottom > solid.Top;
    }
}
