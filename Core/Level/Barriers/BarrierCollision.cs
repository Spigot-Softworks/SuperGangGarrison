namespace OpenGarrison.Core;

public static class BarrierCollision
{
    public static bool Intersects(RoomObjectMarker marker, float left, float top, float right, float bottom)
    {
        return left < marker.Right && right > marker.Left && top < marker.Bottom && bottom > marker.Top;
    }

    public static bool BlocksPlayerMovement(
        in BarrierConfiguration configuration,
        PlayerTeam team,
        bool isCarryingIntel,
        RoomObjectMarker marker,
        float previousLeft,
        float previousRight,
        float previousTop,
        float previousBottom,
        float nextLeft,
        float nextRight,
        float nextTop,
        float nextBottom)
    {
        if (!MatchesPlayerTarget(configuration.Targets, team, isCarryingIntel))
        {
            return false;
        }

        return Intersects(marker, nextLeft, nextTop, nextRight, nextBottom);
    }

    public static bool BlocksProjectile(in BarrierTargetFilters targets, PlayerTeam shotTeam)
    {
        var target = shotTeam == PlayerTeam.Red
            ? BarrierTargetKind.RedShots
            : BarrierTargetKind.BlueShots;
        return targets.Blocks(target);
    }

    public static bool BlocksProjectile(in BarrierConfiguration configuration, PlayerTeam shotTeam)
    {
        return BlocksProjectile(configuration.Targets, shotTeam);
    }

    public static bool BlocksProjectilePath(
        in BarrierConfiguration configuration,
        PlayerTeam shotTeam,
        RoomObjectMarker marker,
        float previousX,
        float previousY,
        float nextX,
        float nextY)
    {
        if (!BlocksProjectile(configuration, shotTeam))
        {
            return false;
        }

        const float projectileSpan = 1f;
        return Intersects(
            marker,
            MathF.Min(previousX, nextX),
            MathF.Min(previousY, nextY),
            MathF.Max(previousX, nextX) + projectileSpan,
            MathF.Max(previousY, nextY) + projectileSpan);
    }

    public static bool BlocksHitscan(
        in BarrierConfiguration configuration,
        PlayerTeam shooterTeam,
        bool isCarryingIntel,
        RoomObjectMarker marker,
        float originX,
        float originY,
        float targetX,
        float targetY)
    {
        const float span = 1f;
        if (!Intersects(
                marker,
                MathF.Min(originX, targetX),
                MathF.Min(originY, targetY),
                MathF.Max(originX, targetX) + span,
                MathF.Max(originY, targetY) + span))
        {
            return false;
        }

        if (MatchesPlayerTarget(configuration.Targets, shooterTeam, isCarryingIntel))
        {
            return true;
        }

        return BlocksProjectilePath(configuration, shooterTeam, marker, originX, originY, targetX, targetY);
    }

    public static bool BlocksPlayerWithoutDirection(
        in BarrierConfiguration configuration,
        PlayerTeam team,
        bool isCarryingIntel)
    {
        return MatchesPlayerTarget(configuration.Targets, team, isCarryingIntel);
    }

    public static bool MatchesPlayerTarget(in BarrierTargetFilters targets, PlayerTeam team, bool isCarryingIntel)
    {
        if (team == PlayerTeam.Red)
        {
            if (targets.Blocks(BarrierTargetKind.RedPlayers))
            {
                return true;
            }

            if (isCarryingIntel && targets.Blocks(BarrierTargetKind.BlueIntel))
            {
                return true;
            }
        }
        else if (team == PlayerTeam.Blue)
        {
            if (targets.Blocks(BarrierTargetKind.BluePlayers))
            {
                return true;
            }

            if (isCarryingIntel && targets.Blocks(BarrierTargetKind.RedIntel))
            {
                return true;
            }
        }

        return false;
    }
}
