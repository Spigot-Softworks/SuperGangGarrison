namespace OpenGarrison.Core;

/// <summary>
/// Swept raycasts against barrier rectangles so fast projectiles do not tunnel through thin walls.
/// </summary>
public static class BarrierProjectileRaycast
{
    public const float DefaultMaxStepLength = 6f;

    public static bool TryRaycastMarker(
        in BarrierConfiguration configuration,
        PlayerTeam shotTeam,
        RoomObjectMarker marker,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance,
        out float hitDistance,
        float maxStepLength = DefaultMaxStepLength)
    {
        hitDistance = 0f;
        if (!BarrierCollision.BlocksProjectile(configuration, shotTeam))
        {
            return false;
        }

        if (IsPointInsideRectangle(marker.Left, marker.Top, marker.Right, marker.Bottom, originX, originY)
            && IsPointInsideRectangle(
                marker.Left,
                marker.Top,
                marker.Right,
                marker.Bottom,
                originX + (directionX * maxDistance),
                originY + (directionY * maxDistance)))
        {
            return false;
        }

        return TryRaycastRectangleWithSubsteps(
            marker.Left,
            marker.Top,
            marker.Right,
            marker.Bottom,
            originX,
            originY,
            directionX,
            directionY,
            maxDistance,
            out hitDistance,
            maxStepLength);
    }

    public static bool TryRaycastLevelBarriers(
        SimpleLevel level,
        PlayerTeam shotTeam,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance,
        out float hitDistance,
        float maxStepLength = DefaultMaxStepLength)
    {
        hitDistance = 0f;
        var found = false;
        var nearest = maxDistance;
        for (var index = 0; index < level.RoomObjects.Count; index += 1)
        {
            if (!level.IsRoomObjectActive(index))
            {
                continue;
            }

            var marker = level.RoomObjects[index];
            if (marker.Type != RoomObjectType.Barrier)
            {
                continue;
            }

            if (!TryRaycastMarker(
                    marker.Barrier,
                    shotTeam,
                    marker,
                    originX,
                    originY,
                    directionX,
                    directionY,
                    nearest,
                    out var distance,
                    maxStepLength))
            {
                continue;
            }

            if (!found || distance < nearest)
            {
                found = true;
                nearest = distance;
            }
        }

        if (!found)
        {
            return false;
        }

        hitDistance = nearest;
        return true;
    }

    public static bool TryRaycastRectangleWithSubsteps(
        float left,
        float top,
        float right,
        float bottom,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance,
        out float hitDistance,
        float maxStepLength = DefaultMaxStepLength)
    {
        hitDistance = 0f;
        if (maxDistance <= 0f || maxStepLength <= 0f)
        {
            return false;
        }

        var stepLength = MathF.Max(1f, maxStepLength);
        var traveled = 0f;
        while (traveled < maxDistance - 0.0001f)
        {
            var segmentLength = MathF.Min(stepLength, maxDistance - traveled);
            if (TryRaycastRectangle(
                    left,
                    top,
                    right,
                    bottom,
                    originX,
                    originY,
                    directionX,
                    directionY,
                    segmentLength,
                    out var segmentHit))
            {
                hitDistance = traveled + segmentHit;
                return true;
            }

            originX += directionX * segmentLength;
            originY += directionY * segmentLength;
            traveled += segmentLength;
        }

        return false;
    }

    public static bool TryRaycastRectangle(
        float left,
        float top,
        float right,
        float bottom,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance,
        out float hitDistance)
    {
        hitDistance = 0f;
        const float epsilon = 0.0001f;
        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;

        if (MathF.Abs(directionX) < epsilon)
        {
            if (originX < left || originX > right)
            {
                return false;
            }
        }
        else
        {
            var t1 = (left - originX) / directionX;
            var t2 = (right - originX) / directionX;
            tMin = MathF.Max(tMin, MathF.Min(t1, t2));
            tMax = MathF.Min(tMax, MathF.Max(t1, t2));
        }

        if (MathF.Abs(directionY) < epsilon)
        {
            if (originY < top || originY > bottom)
            {
                return false;
            }
        }
        else
        {
            var t3 = (top - originY) / directionY;
            var t4 = (bottom - originY) / directionY;
            tMin = MathF.Max(tMin, MathF.Min(t3, t4));
            tMax = MathF.Min(tMax, MathF.Max(t3, t4));
        }

        if (tMax < 0f || tMin > tMax)
        {
            return false;
        }

        var distance = tMin >= 0f ? tMin : tMax;
        if (distance < 0f || distance > maxDistance)
        {
            return false;
        }

        hitDistance = distance;
        return true;
    }

    public static bool IsPointInsideRectangle(float left, float top, float right, float bottom, float x, float y)
    {
        return x >= left && x <= right && y >= top && y <= bottom;
    }
}
