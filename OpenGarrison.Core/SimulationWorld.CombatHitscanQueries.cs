namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class CombatResolver
    {
        private delegate void UpdateProjectileHit<TProjectile>(
            ref ShotHitResult? nearestHit,
            TProjectile projectile,
            float directionX,
            float directionY,
            float distance,
            PlayerEntity? player,
            SentryEntity? sentry,
            GeneratorState? generator);

        public ShotHitResult? GetNearestShotHit(ShotProjectileEntity shot, float directionX, float directionY, float maxDistance)
        {
            ShotHitResult? nearestHit = null;
            UpdateNearestProjectileHitFromSolids(ref nearestHit, shot, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestHit);
            UpdateNearestProjectileHitFromGates(ref nearestHit, shot, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestHit);
            UpdateNearestProjectileHitFromSentries(ref nearestHit, shot, shot.Team, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestHit);
            UpdateNearestProjectileHitFromGenerators(ref nearestHit, shot, shot.Team, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestHit);
            UpdateNearestProjectileHitFromPlayers(ref nearestHit, shot, shot.Team, shot.OwnerId, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestHit);
            return nearestHit;
        }

        public ShotHitResult? GetNearestNeedleHit(NeedleProjectileEntity needle, float directionX, float directionY, float maxDistance)
        {
            ShotHitResult? nearestHit = null;
            UpdateNearestProjectileHitFromSolids(ref nearestHit, needle, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance, UpdateNearestNeedleHit);
            UpdateNearestProjectileHitFromGates(ref nearestHit, needle, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance, UpdateNearestNeedleHit);
            UpdateNearestProjectileHitFromSentries(ref nearestHit, needle, needle.Team, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance, UpdateNearestNeedleHit);
            UpdateNearestProjectileHitFromGenerators(ref nearestHit, needle, needle.Team, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance, UpdateNearestNeedleHit);
            UpdateNearestProjectileHitFromPlayers(ref nearestHit, needle, needle.Team, needle.OwnerId, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance, UpdateNearestNeedleHit);
            return nearestHit;
        }

        public ShotHitResult? GetNearestRevolverHit(RevolverProjectileEntity shot, float directionX, float directionY, float maxDistance)
        {
            ShotHitResult? nearestHit = null;
            UpdateNearestProjectileHitFromSolids(ref nearestHit, shot, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestRevolverHit);
            UpdateNearestProjectileHitFromGates(ref nearestHit, shot, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestRevolverHit);
            UpdateNearestProjectileHitFromSentries(ref nearestHit, shot, shot.Team, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestRevolverHit);
            UpdateNearestProjectileHitFromGenerators(ref nearestHit, shot, shot.Team, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestRevolverHit);
            UpdateNearestProjectileHitFromPlayers(ref nearestHit, shot, shot.Team, shot.OwnerId, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestRevolverHit);
            return nearestHit;
        }

        private void UpdateNearestProjectileHitFromSolids<TProjectile>(
            ref ShotHitResult? nearestHit,
            TProjectile projectile,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            UpdateProjectileHit<TProjectile> updateHit)
        {
            var rayBounds = GetRayBounds(previousX, previousY, directionX, directionY, maxDistance);
            foreach (var solid in Level.Solids)
            {
                if (!RayBoundsMayIntersectRectangle(rayBounds, solid.Left, solid.Top, solid.Right, solid.Bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithRectangle(previousX, previousY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, maxDistance);
                if (distance.HasValue) { updateHit(ref nearestHit, projectile, directionX, directionY, distance.Value, null, null, null); }
            }
        }

        private void UpdateNearestProjectileHitFromGates<TProjectile>(
            ref ShotHitResult? nearestHit,
            TProjectile projectile,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            UpdateProjectileHit<TProjectile> updateHit)
        {
            var rayBounds = GetRayBounds(previousX, previousY, directionX, directionY, maxDistance);
            foreach (var roomObject in Level.RoomObjects)
            {
                if (!IsBlockingProjectileRoomObject(roomObject)) { continue; }
                if (!RayBoundsMayIntersectRectangle(rayBounds, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithRectangle(previousX, previousY, directionX, directionY, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom, maxDistance);
                if (distance.HasValue) { updateHit(ref nearestHit, projectile, directionX, directionY, distance.Value, null, null, null); }
            }
        }

        private void UpdateNearestProjectileHitFromPlayers<TProjectile>(
            ref ShotHitResult? nearestHit,
            TProjectile projectile,
            PlayerTeam projectileTeam,
            int ownerId,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            UpdateProjectileHit<TProjectile> updateHit)
        {
            var rayBounds = GetRayBounds(previousX, previousY, directionX, directionY, maxDistance);
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Team == projectileTeam || player.Id == ownerId) { continue; }
                player.GetCollisionBounds(out var left, out var top, out var right, out var bottom);
                if (!RayBoundsMayIntersectRectangle(
                    rayBounds,
                    left,
                    top,
                    right,
                    bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithPlayer(previousX, previousY, directionX, directionY, player, maxDistance);
                if (distance.HasValue) { updateHit(ref nearestHit, projectile, directionX, directionY, distance.Value, player, null, null); }
            }
        }

        private void UpdateNearestProjectileHitFromSentries<TProjectile>(
            ref ShotHitResult? nearestHit,
            TProjectile projectile,
            PlayerTeam projectileTeam,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            UpdateProjectileHit<TProjectile> updateHit)
        {
            var rayBounds = GetRayBounds(previousX, previousY, directionX, directionY, maxDistance);
            foreach (var sentry in _sentries)
            {
                if (sentry.Team == projectileTeam) { continue; }
                var sentryHalfWidth = SentryEntity.Width / 2f;
                var sentryHalfHeight = SentryEntity.Height / 2f;
                if (!RayBoundsMayIntersectRectangle(
                    rayBounds,
                    sentry.X - sentryHalfWidth,
                    sentry.Y - sentryHalfHeight,
                    sentry.X + sentryHalfWidth,
                    sentry.Y + sentryHalfHeight))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithSentry(previousX, previousY, directionX, directionY, sentry, maxDistance);
                if (distance.HasValue) { updateHit(ref nearestHit, projectile, directionX, directionY, distance.Value, null, sentry, null); }
            }
        }

        private void UpdateNearestProjectileHitFromGenerators<TProjectile>(
            ref ShotHitResult? nearestHit,
            TProjectile projectile,
            PlayerTeam projectileTeam,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            UpdateProjectileHit<TProjectile> updateHit)
        {
            var rayBounds = GetRayBounds(previousX, previousY, directionX, directionY, maxDistance);
            for (var index = 0; index < _generators.Count; index += 1)
            {
                var generator = _generators[index];
                if (generator.Team == projectileTeam || generator.IsDestroyed)
                {
                    continue;
                }

                if (!RayBoundsMayIntersectRectangle(
                    rayBounds,
                    generator.Marker.Left,
                    generator.Marker.Top,
                    generator.Marker.Right,
                    generator.Marker.Bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithGenerator(previousX, previousY, directionX, directionY, generator, maxDistance);
                if (distance.HasValue) { updateHit(ref nearestHit, projectile, directionX, directionY, distance.Value, null, null, generator); }
            }
        }

        private static void UpdateNearestHit(ref ShotHitResult? nearestHit, ShotProjectileEntity shot, float directionX, float directionY, float distance, PlayerEntity? player, SentryEntity? sentry = null, GeneratorState? generator = null)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new ShotHitResult(distance, shot.PreviousX + directionX * distance, shot.PreviousY + directionY * distance, player, sentry, generator);
        }

        private static void UpdateNearestNeedleHit(ref ShotHitResult? nearestHit, NeedleProjectileEntity needle, float directionX, float directionY, float distance, PlayerEntity? player, SentryEntity? sentry = null, GeneratorState? generator = null)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new ShotHitResult(distance, needle.PreviousX + directionX * distance, needle.PreviousY + directionY * distance, player, sentry, generator);
        }

        private static void UpdateNearestRevolverHit(ref ShotHitResult? nearestHit, RevolverProjectileEntity shot, float directionX, float directionY, float distance, PlayerEntity? player, SentryEntity? sentry = null, GeneratorState? generator = null)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new ShotHitResult(distance, shot.PreviousX + directionX * distance, shot.PreviousY + directionY * distance, player, sentry, generator);
        }
    }
}
