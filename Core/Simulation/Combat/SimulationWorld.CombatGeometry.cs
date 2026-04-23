namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class CombatResolver
    {
        private static float DistanceBetween(float x1, float y1, float x2, float y2)
        {
            return SimulationWorld.DistanceBetween(x1, y1, x2, y2);
        }

        private static float GetStabOriginX(StabMaskEntity mask, float directionX)
        {
            return SimulationWorld.GetStabOriginX(mask, directionX);
        }

        private static float GetStabOriginY(StabMaskEntity mask, float directionY)
        {
            return SimulationWorld.GetStabOriginY(mask, directionY);
        }

        private static float? GetRayIntersectionDistanceWithRectangle(
            float originX,
            float originY,
            float directionX,
            float directionY,
            float left,
            float top,
            float right,
            float bottom,
            float maxDistance)
        {
            const float epsilon = 0.0001f;
            var tMin = float.NegativeInfinity;
            var tMax = float.PositiveInfinity;

            if (MathF.Abs(directionX) < epsilon)
            {
                if (originX < left || originX > right)
                {
                    return null;
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
                    return null;
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
                return null;
            }

            var distance = tMin >= 0f ? tMin : tMax;
            if (distance < 0f || distance > maxDistance)
            {
                return null;
            }

            return distance;
        }

        private static RectangleHitbox GetRayBounds(
            float originX,
            float originY,
            float directionX,
            float directionY,
            float maxDistance,
            float padding = 0f)
        {
            var endX = originX + (directionX * maxDistance);
            var endY = originY + (directionY * maxDistance);
            return new RectangleHitbox(
                MathF.Min(originX, endX) - padding,
                MathF.Min(originY, endY) - padding,
                MathF.Max(originX, endX) + padding,
                MathF.Max(originY, endY) + padding);
        }

        private static bool RayBoundsMayIntersectRectangle(
            RectangleHitbox rayBounds,
            float left,
            float top,
            float right,
            float bottom)
        {
            return rayBounds.Left <= right
                && rayBounds.Right >= left
                && rayBounds.Top <= bottom
                && rayBounds.Bottom >= top;
        }

        private static float? GetThickRayIntersectionDistanceWithRectangle(
            float originX,
            float originY,
            float directionX,
            float directionY,
            float left,
            float top,
            float right,
            float bottom,
            float maxDistance,
            float thicknessRadius)
        {
            return GetRayIntersectionDistanceWithRectangle(
                originX,
                originY,
                directionX,
                directionY,
                left - thicknessRadius,
                top - thicknessRadius,
                right + thicknessRadius,
                bottom + thicknessRadius,
                maxDistance);
        }

        private static float? GetRayIntersectionDistanceWithPlayer(
            float originX,
            float originY,
            float directionX,
            float directionY,
            SimulationWorld world,
            PlayerEntity player,
            float maxDistance)
        {
            if (!player.IsAlive || player.IsExperimentalGhostDashing)
            {
                return null;
            }

            GetPlayerPresentationHitBounds(world, player, out var left, out var top, out var right, out var bottom);
            return GetRayIntersectionDistanceWithRectangle(
                originX,
                originY,
                directionX,
                directionY,
                left,
                top,
                right,
                bottom,
                maxDistance);
        }

        private static float? GetRayIntersectionDistanceWithSentry(
            float originX,
            float originY,
            float directionX,
            float directionY,
            SentryEntity sentry,
            float maxDistance)
        {
            return GetRayIntersectionDistanceWithRectangle(
                originX,
                originY,
                directionX,
                directionY,
                sentry.X - (SentryEntity.Width / 2f),
                sentry.Y - (SentryEntity.Height / 2f),
                sentry.X + (SentryEntity.Width / 2f),
                sentry.Y + (SentryEntity.Height / 2f),
                maxDistance);
        }

        private static float? GetRayIntersectionDistanceWithGenerator(
            float originX,
            float originY,
            float directionX,
            float directionY,
            GeneratorState generator,
            float maxDistance)
        {
            return GetRayIntersectionDistanceWithRectangle(
                originX,
                originY,
                directionX,
                directionY,
                generator.Marker.Left,
                generator.Marker.Top,
                generator.Marker.Right,
                generator.Marker.Bottom,
                maxDistance);
        }

        private static float? GetThickRayIntersectionDistanceWithSentry(
            float originX,
            float originY,
            float directionX,
            float directionY,
            SentryEntity sentry,
            float maxDistance,
            float thicknessRadius)
        {
            return GetThickRayIntersectionDistanceWithRectangle(
                originX,
                originY,
                directionX,
                directionY,
                sentry.X - (SentryEntity.Width / 2f),
                sentry.Y - (SentryEntity.Height / 2f),
                sentry.X + (SentryEntity.Width / 2f),
                sentry.Y + (SentryEntity.Height / 2f),
                maxDistance,
                thicknessRadius);
        }

        public float? GetLineIntersectionDistanceToPlayer(
            float originX,
            float originY,
            float endX,
            float endY,
            PlayerEntity player,
            float maxDistance)
        {
            if (!player.IsAlive || player.IsExperimentalGhostDashing)
            {
                return null;
            }

            var distance = DistanceBetween(originX, originY, endX, endY);
            if (distance <= 0.0001f || distance > maxDistance)
            {
                return null;
            }

            var directionX = (endX - originX) / distance;
            var directionY = (endY - originY) / distance;
            return GetRayIntersectionDistanceWithPlayer(originX, originY, directionX, directionY, _world, player, distance);
        }

        public float? GetThickLineIntersectionDistanceToPlayer(
            float originX,
            float originY,
            float endX,
            float endY,
            PlayerEntity player,
            float maxDistance,
            float thicknessRadius)
        {
            if (!player.IsAlive || player.IsExperimentalGhostDashing)
            {
                return null;
            }

            var distance = DistanceBetween(originX, originY, endX, endY);
            if (distance <= 0.0001f || distance > maxDistance)
            {
                return null;
            }

            var directionX = (endX - originX) / distance;
            var directionY = (endY - originY) / distance;
            GetPlayerPresentationHitBounds(_world, player, out var left, out var top, out var right, out var bottom);
            return GetThickRayIntersectionDistanceWithRectangle(
                originX,
                originY,
                directionX,
                directionY,
                left,
                top,
                right,
                bottom,
                distance,
                thicknessRadius);
        }
    }
}
