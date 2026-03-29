namespace OpenGarrison.Core;

public sealed class SentryEntity : SimulationEntity
{
    public const float Width = 28f;
    public const float Height = 24f;
    public const float GravityPerTick = 0.6f;
    public const float MaxFallSpeed = 10f;
    public const int MaxHealth = 100;
    public const int InitialHealth = 25;
    public const float TargetRange = 375f;
    public const int ReloadTicks = 5;
    public const int HitDamage = 8;
    public const int ShotTraceTicks = 1;
    public const int RotationTicks = 14;

    public SentryEntity(int id, int ownerPlayerId, PlayerTeam team, float x, float y, float startDirectionX) : base(id)
    {
        OwnerPlayerId = ownerPlayerId;
        Team = team;
        X = x;
        Y = y;
        StartDirectionX = startDirectionX >= 0f ? 1f : -1f;
        FacingDirectionX = StartDirectionX;
        DesiredFacingDirectionX = StartDirectionX;
        RotationStartDirectionX = StartDirectionX;
        AimDirectionDegrees = StartDirectionX < 0f ? 180f : 0f;
        Health = InitialHealth;
    }

    public int OwnerPlayerId { get; }

    public PlayerTeam Team { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float VerticalSpeed { get; private set; } = 0.001f;

    public float StartDirectionX { get; }

    public float FacingDirectionX { get; private set; }

    public float DesiredFacingDirectionX { get; private set; }

    public float RotationStartDirectionX { get; private set; }

    public int RotationTicksRemaining { get; private set; }

    public bool IsRotating => RotationTicksRemaining > 0;

    public float RotationProgress => RotationTicks <= 0
        ? 1f
        : 1f - (RotationTicksRemaining / (float)RotationTicks);

    public float AimDirectionDegrees { get; private set; }

    public int Health { get; private set; }

    public int ReloadTicksRemaining { get; private set; }

    public int AlertTicksRemaining { get; private set; }

    public int ShotTraceTicksRemaining { get; private set; }

    public bool HasLanded { get; private set; }

    public bool IsBuilt { get; private set; }

    public bool HasActiveTarget { get; private set; }

    public int? CurrentTargetPlayerId { get; private set; }

    public float LastShotTargetX { get; private set; }

    public float LastShotTargetY { get; private set; }

    public bool IsShotTraceVisible => ShotTraceTicksRemaining > 0;

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
            if (Health < MaxHealth)
            {
                Health = int.Min(MaxHealth, Health + 1);
            }

            if (Health >= MaxHealth)
            {
                IsBuilt = true;
            }
        }

        if (ReloadTicksRemaining > 0)
        {
            ReloadTicksRemaining -= 1;
        }

        if (AlertTicksRemaining > 0)
        {
            AlertTicksRemaining -= 1;
        }

        if (ShotTraceTicksRemaining > 0)
        {
            ShotTraceTicksRemaining -= 1;
        }

        if (RotationTicksRemaining > 0)
        {
            RotationTicksRemaining -= 1;
            if (RotationTicksRemaining == 0)
            {
                FacingDirectionX = DesiredFacingDirectionX;
                if (!HasActiveTarget)
                {
                    AimDirectionDegrees = FacingDirectionX < 0f ? 180f : 0f;
                }
            }
        }
    }

    public bool IsNear(float x, float y, float radius)
    {
        var deltaX = X - x;
        var deltaY = Y - y;
        return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
    }

    public void SetTarget(int? playerId, float targetX, bool hasTarget = true)
    {
        HasActiveTarget = hasTarget;
        CurrentTargetPlayerId = playerId;
        var desiredFacingDirectionX = hasTarget
            ? (targetX < X ? -1f : 1f)
            : StartDirectionX;
        SetDesiredFacing(desiredFacingDirectionX);
    }

    public bool BeginTargetAlert()
    {
        if (AlertTicksRemaining > 0)
        {
            return false;
        }

        AlertTicksRemaining = ReloadTicks * 2;
        return true;
    }

    public bool CanFire()
    {
        return IsBuilt && AlertTicksRemaining == 0 && ReloadTicksRemaining == 0 && !IsRotating;
    }

    public void FireAt(float targetX, float targetY)
    {
        AimDirectionDegrees = MathF.Atan2(targetY - Y, targetX - X) * (180f / MathF.PI);
        LastShotTargetX = targetX;
        LastShotTargetY = targetY;
        ShotTraceTicksRemaining = ShotTraceTicks;
        ReloadTicksRemaining = ReloadTicks;
    }

    public bool ApplyDamage(int damage)
    {
        if (damage <= 0)
        {
            return false;
        }

        Health = int.Max(0, Health - damage);
        return Health == 0;
    }

    public void ApplyNetworkState(
        float x,
        float y,
        int health,
        bool isBuilt,
        float facingDirectionX,
        float desiredFacingDirectionX,
        float aimDirectionDegrees,
        int reloadTicksRemaining,
        int alertTicksRemaining,
        int shotTraceTicksRemaining,
        bool hasLanded,
        bool hasActiveTarget,
        int? currentTargetPlayerId,
        float lastShotTargetX,
        float lastShotTargetY)
    {
        X = x;
        Y = y;
        Health = health;
        IsBuilt = isBuilt;
        FacingDirectionX = facingDirectionX >= 0f ? 1f : -1f;
        DesiredFacingDirectionX = desiredFacingDirectionX >= 0f ? 1f : -1f;
        AimDirectionDegrees = aimDirectionDegrees;
        ReloadTicksRemaining = reloadTicksRemaining;
        AlertTicksRemaining = alertTicksRemaining;
        ShotTraceTicksRemaining = shotTraceTicksRemaining;
        HasLanded = hasLanded;
        HasActiveTarget = hasActiveTarget;
        CurrentTargetPlayerId = currentTargetPlayerId;
        LastShotTargetX = lastShotTargetX;
        LastShotTargetY = lastShotTargetY;
        RotationTicksRemaining = 0;
        RotationStartDirectionX = FacingDirectionX;
    }

    private void SetDesiredFacing(float desiredFacingDirectionX)
    {
        desiredFacingDirectionX = desiredFacingDirectionX >= 0f ? 1f : -1f;
        if (DesiredFacingDirectionX == desiredFacingDirectionX && RotationTicksRemaining > 0)
        {
            return;
        }

        DesiredFacingDirectionX = desiredFacingDirectionX;
        if (FacingDirectionX == desiredFacingDirectionX)
        {
            RotationTicksRemaining = 0;
            RotationStartDirectionX = FacingDirectionX;
            return;
        }

        RotationStartDirectionX = FacingDirectionX;
        RotationTicksRemaining = RotationTicks;
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
