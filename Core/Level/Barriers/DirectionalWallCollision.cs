namespace OpenGarrison.Core;

public static class DirectionalWallCollision
{
    public static bool BlocksPlayerMovement(
        in DirectionalWallConfiguration configuration,
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
        _ = team;
        _ = isCarryingIntel;
        if (!configuration.AffectsPlayers)
        {
            return false;
        }

        if (!BarrierCollision.Intersects(marker, nextLeft, nextTop, nextRight, nextBottom))
        {
            return false;
        }

        return BlocksWrongWayTraversal(
            configuration.PassDirection,
            marker,
            previousLeft,
            previousRight,
            previousTop,
            previousBottom,
            nextLeft,
            nextRight,
            nextTop,
            nextBottom);
    }

    public static bool BlocksProjectilePath(
        in DirectionalWallConfiguration configuration,
        PlayerTeam shotTeam,
        RoomObjectMarker marker,
        float previousX,
        float previousY,
        float nextX,
        float nextY)
    {
        _ = shotTeam;
        if (!configuration.AffectsProjectiles)
        {
            return false;
        }

        const float projectileSpan = 1f;
        var previousLeft = previousX;
        var previousRight = previousX + projectileSpan;
        var previousTop = previousY;
        var previousBottom = previousY + projectileSpan;
        var nextLeft = nextX;
        var nextRight = nextX + projectileSpan;
        var nextTop = nextY;
        var nextBottom = nextY + projectileSpan;

        if (!BarrierCollision.Intersects(marker, nextLeft, nextTop, nextRight, nextBottom))
        {
            return false;
        }

        return BlocksWrongWayTraversal(
            configuration.PassDirection,
            marker,
            previousLeft,
            previousRight,
            previousTop,
            previousBottom,
            nextLeft,
            nextRight,
            nextTop,
            nextBottom);
    }

    public static bool BlocksHitscan(
        in DirectionalWallConfiguration configuration,
        PlayerTeam shooterTeam,
        bool isCarryingIntel,
        RoomObjectMarker marker,
        float originX,
        float originY,
        float targetX,
        float targetY)
    {
        const float span = 1f;
        if (!BarrierCollision.Intersects(
                marker,
                MathF.Min(originX, targetX),
                MathF.Min(originY, targetY),
                MathF.Max(originX, targetX) + span,
                MathF.Max(originY, targetY) + span))
        {
            return false;
        }

        if (configuration.AffectsPlayers
            && BlocksWrongWayTraversal(
                configuration.PassDirection,
                marker,
                originX,
                originX + span,
                originY,
                originY + span,
                targetX,
                targetX + span,
                targetY,
                targetY + span))
        {
            _ = shooterTeam;
            _ = isCarryingIntel;
            return true;
        }

        return BlocksProjectilePath(configuration, shooterTeam, marker, originX, originY, targetX, targetY);
    }

    /// <summary>
    /// Blocks movement from the side opposite the pass direction (wrong-way entry).
    /// </summary>
    public static bool BlocksWrongWayTraversal(
        DirectionalWallPassDirection passDirection,
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
        const float edgeTolerance = 2f;
        return passDirection switch
        {
            DirectionalWallPassDirection.Right => nextLeft < previousLeft && previousLeft >= marker.Right - edgeTolerance,
            DirectionalWallPassDirection.Left => nextLeft > previousLeft && previousRight <= marker.Left + edgeTolerance,
            DirectionalWallPassDirection.Down => nextTop < previousTop && previousTop >= marker.Bottom - edgeTolerance,
            _ => nextBottom > previousBottom && previousBottom <= marker.Top + edgeTolerance,
        };
    }
}
