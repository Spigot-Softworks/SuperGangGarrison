namespace OpenGarrison.Core;

public static class SimpleLevelBarrierCollision
{
    public static bool BlocksPlayerAt(
        SimpleLevel level,
        PlayerTeam team,
        bool isCarryingIntel,
        float previousLeft,
        float previousRight,
        float previousTop,
        float previousBottom,
        float nextLeft,
        float nextTop,
        float nextRight,
        float nextBottom)
    {
        for (var index = 0; index < level.RoomObjects.Count; index += 1)
        {
            var barrier = level.RoomObjects[index];
            if (barrier.Type != RoomObjectType.Barrier || !level.IsRoomObjectActive(index))
            {
                continue;
            }

            if (BarrierCollision.BlocksPlayerMovement(
                    barrier.Barrier,
                    team,
                    isCarryingIntel,
                    barrier,
                    previousLeft,
                    previousRight,
                    previousTop,
                    previousBottom,
                    nextLeft,
                    nextRight,
                    nextTop,
                    nextBottom))
            {
                return true;
            }
        }

        for (var index = 0; index < level.RoomObjects.Count; index += 1)
        {
            var wall = level.RoomObjects[index];
            if (wall.Type != RoomObjectType.DirectionalWall || !level.IsRoomObjectActive(index))
            {
                continue;
            }

            if (DirectionalWallCollision.BlocksPlayerMovement(
                    wall.DirectionalWall,
                    team,
                    isCarryingIntel,
                    wall,
                    previousLeft,
                    previousRight,
                    previousTop,
                    previousBottom,
                    nextLeft,
                    nextRight,
                    nextTop,
                    nextBottom))
            {
                return true;
            }
        }

        return false;
    }

    public static bool BlocksPointForPlayer(SimpleLevel level, PlayerTeam team, bool isCarryingIntel, float x, float y)
    {
        for (var index = 0; index < level.RoomObjects.Count; index += 1)
        {
            var barrier = level.RoomObjects[index];
            if (barrier.Type != RoomObjectType.Barrier || !level.IsRoomObjectActive(index))
            {
                continue;
            }

            if (x < barrier.Left || x >= barrier.Right || y < barrier.Top || y >= barrier.Bottom)
            {
                continue;
            }

            if (BarrierCollision.BlocksPlayerWithoutDirection(barrier.Barrier, team, isCarryingIntel))
            {
                return true;
            }
        }

        return false;
    }

    public static bool BlocksPointForProjectile(SimpleLevel level, PlayerTeam shotTeam, float x, float y)
    {
        for (var index = 0; index < level.RoomObjects.Count; index += 1)
        {
            var barrier = level.RoomObjects[index];
            if (barrier.Type != RoomObjectType.Barrier || !level.IsRoomObjectActive(index))
            {
                continue;
            }

            if (x < barrier.Left || x >= barrier.Right || y < barrier.Top || y >= barrier.Bottom)
            {
                continue;
            }

            if (BarrierCollision.BlocksProjectile(barrier.Barrier, shotTeam))
            {
                return true;
            }
        }

        return false;
    }

    public static bool BlocksProjectileMovement(
        SimpleLevel level,
        PlayerTeam shotTeam,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance,
        out float hitDistance)
    {
        return BarrierProjectileRaycast.TryRaycastLevelBarriers(
            level,
            shotTeam,
            originX,
            originY,
            directionX,
            directionY,
            maxDistance,
            out hitDistance);
    }
}
