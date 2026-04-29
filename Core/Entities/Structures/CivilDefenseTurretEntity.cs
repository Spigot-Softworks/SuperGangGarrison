namespace OpenGarrison.Core;

public sealed class CivilDefenseTurretEntity : SimulationEntity
{
    public const float Width = 24f;
    public const float Height = 22f;
    public const float GravityPerTick = 0.6f;
    public const float MaxFallSpeed = 10f;
    public const int MaxHealth = 80;
    public const int InitialHealth = 25;
    public const float TargetRange = 210f;
    public const int ReloadTicks = SentryEntity.ReloadTicks;
    public const int ShotTraceTicks = 2;

    public CivilDefenseTurretEntity(int id, int ownerPlayerId, PlayerTeam team, float x, float y, float startDirectionX) : base(id)
    {
        OwnerPlayerId = ownerPlayerId;
        Team = team;
        X = x;
        Y = y;
        FacingDirectionX = startDirectionX >= 0f ? 1f : -1f;
        AimDirectionDegrees = FacingDirectionX < 0f ? 180f : 0f;
        Health = InitialHealth;
    }

    public int OwnerPlayerId { get; }

    public PlayerTeam Team { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float VerticalSpeed { get; private set; } = 0.001f;

    public float FacingDirectionX { get; private set; }

    public float AimDirectionDegrees { get; private set; }

    public int Health { get; private set; }

    public int ReloadTicksRemaining { get; private set; }

    public int ShotTraceTicksRemaining { get; private set; }

    public bool HasLanded { get; private set; }

    public bool IsBuilt { get; private set; }

    public float LastShotTargetX { get; private set; }

    public float LastShotTargetY { get; private set; }

    public bool IsShotTraceVisible => ShotTraceTicksRemaining > 0;

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

        if (HasLanded && !IsBuilt)
        {
            Health = int.Min(MaxHealth, Health + 2);
            IsBuilt = Health >= MaxHealth;
        }

        if (ReloadTicksRemaining > 0)
        {
            ReloadTicksRemaining -= 1;
        }

        if (ShotTraceTicksRemaining > 0)
        {
            ShotTraceTicksRemaining -= 1;
        }
    }

    public bool CanFire()
    {
        return IsBuilt && ReloadTicksRemaining == 0;
    }

    public void FireAt(float targetX, float targetY)
    {
        FacingDirectionX = targetX < X ? -1f : 1f;
        AimDirectionDegrees = MathF.Atan2(targetY - Y, targetX - X) * (180f / MathF.PI);
        LastShotTargetX = targetX;
        LastShotTargetY = targetY;
        ShotTraceTicksRemaining = ShotTraceTicks;
        ReloadTicksRemaining = ReloadTicks;
    }

    public bool IsNear(float x, float y, float radius)
    {
        var deltaX = X - x;
        var deltaY = Y - y;
        return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
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
