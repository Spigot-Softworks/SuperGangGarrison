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
            var inverseX = MathF.Abs(directionX) < epsilon ? float.PositiveInfinity : 1f / directionX;
            var inverseY = MathF.Abs(directionY) < epsilon ? float.PositiveInfinity : 1f / directionY;

            var t1 = (left - originX) * inverseX;
            var t2 = (right - originX) * inverseX;
            var t3 = (top - originY) * inverseY;
            var t4 = (bottom - originY) * inverseY;

            var tMin = MathF.Max(MathF.Min(t1, t2), MathF.Min(t3, t4));
            var tMax = MathF.Min(MathF.Max(t1, t2), MathF.Max(t3, t4));
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
            PlayerEntity player,
            float maxDistance)
        {
            if (!player.IsAlive)
            {
                return null;
            }

            player.GetCollisionBounds(out var left, out var top, out var right, out var bottom);
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

        public static float? GetLineIntersectionDistanceToPlayer(
            float originX,
            float originY,
            float endX,
            float endY,
            PlayerEntity player,
            float maxDistance)
        {
            if (!player.IsAlive)
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
            return GetRayIntersectionDistanceWithPlayer(originX, originY, directionX, directionY, player, distance);
        }
    }
}
