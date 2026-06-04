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
            UpdateNearestProjectileHitFromGates(ref nearestHit, shot.Team, shot, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestHit);
            UpdateNearestProjectileHitFromSentries(ref nearestHit, shot, shot.Team, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestHit);
            UpdateNearestProjectileHitFromGenerators(ref nearestHit, shot, shot.Team, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestHit);
            UpdateNearestProjectileHitFromJumpPads(ref nearestHit, shot.Team, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance);
            UpdateNearestProjectileHitFromPlayers(ref nearestHit, shot, shot.Team, shot.OwnerId, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestHit);
            return nearestHit;
        }

        public ShotHitResult? GetNearestNeedleHit(NeedleProjectileEntity needle, float directionX, float directionY, float maxDistance)
        {
            ShotHitResult? nearestHit = null;
            UpdateNearestProjectileHitFromSolids(ref nearestHit, needle, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance, UpdateNearestNeedleHit);
            UpdateNearestProjectileHitFromGates(ref nearestHit, needle.Team, needle, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance, UpdateNearestNeedleHit);
            UpdateNearestProjectileHitFromSentries(ref nearestHit, needle, needle.Team, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance, UpdateNearestNeedleHit);
            UpdateNearestProjectileHitFromGenerators(ref nearestHit, needle, needle.Team, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance, UpdateNearestNeedleHit);
            UpdateNearestProjectileHitFromJumpPads(ref nearestHit, needle.Team, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance);
            UpdateNearestProjectileHitFromPlayers(ref nearestHit, needle, needle.Team, needle.OwnerId, needle.PreviousX, needle.PreviousY, directionX, directionY, maxDistance, UpdateNearestNeedleHit);
            return nearestHit;
        }

        public ShotHitResult? GetNearestRevolverHit(RevolverProjectileEntity shot, float directionX, float directionY, float maxDistance)
        {
            ShotHitResult? nearestHit = null;
            UpdateNearestProjectileHitFromSolids(ref nearestHit, shot, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestRevolverHit);
            UpdateNearestProjectileHitFromGates(ref nearestHit, shot.Team, shot, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestRevolverHit);
            UpdateNearestProjectileHitFromSentries(ref nearestHit, shot, shot.Team, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestRevolverHit);
            UpdateNearestProjectileHitFromGenerators(ref nearestHit, shot, shot.Team, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance, UpdateNearestRevolverHit);
            UpdateNearestProjectileHitFromJumpPads(ref nearestHit, shot.Team, shot.PreviousX, shot.PreviousY, directionX, directionY, maxDistance);
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
            foreach (var solid in GetPotentialSolidRaycastCandidates(rayBounds))
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
            PlayerTeam projectileTeam,
            TProjectile projectile,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            UpdateProjectileHit<TProjectile> updateHit)
        {
            var rayBounds = GetRayBounds(previousX, previousY, directionX, directionY, maxDistance);
            for (var roomObjectIndex = 0; roomObjectIndex < Level.RoomObjects.Count; roomObjectIndex += 1)
            {
                if (!Level.IsRoomObjectActive(roomObjectIndex))
                {
                    continue;
                }

                var roomObject = Level.RoomObjects[roomObjectIndex];
                if (!RayBoundsMayIntersectRectangle(rayBounds, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithRectangle(previousX, previousY, directionX, directionY, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom, maxDistance);
                if (!distance.HasValue)
                {
                    continue;
                }

                if (roomObject.Type == RoomObjectType.Barrier)
                {
                    var searchDistance = nearestHit.HasValue ? nearestHit.Value.Distance : maxDistance;
                    if (!BarrierProjectileRaycast.TryRaycastMarker(
                            roomObject.Barrier,
                            projectileTeam,
                            roomObject,
                            previousX,
                            previousY,
                            directionX,
                            directionY,
                            searchDistance,
                            out var barrierDistance))
                    {
                        continue;
                    }

                    updateHit(ref nearestHit, projectile, directionX, directionY, barrierDistance, null, null, null);
                    continue;
                }

                if (roomObject.Type == RoomObjectType.DirectionalWall)
                {
                    var hitX = previousX + (directionX * distance.Value);
                    var hitY = previousY + (directionY * distance.Value);
                    if (!DirectionalWallCollision.BlocksProjectilePath(
                            roomObject.DirectionalWall,
                            projectileTeam,
                            roomObject,
                            previousX,
                            previousY,
                            hitX,
                            hitY))
                    {
                        continue;
                    }
                }
                else if (!IsBlockingProjectileRoomObject(roomObject, projectileTeam))
                {
                    continue;
                }

                updateHit(ref nearestHit, projectile, directionX, directionY, distance.Value, null, null, null);
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
                if (!player.IsAlive || player.Id == ownerId) { continue; }
                GetPlayerPresentationHitBounds(_world, player, out var left, out var top, out var right, out var bottom);
                if (!RayBoundsMayIntersectRectangle(
                    rayBounds,
                    left,
                    top,
                    right,
                    bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithPlayer(previousX, previousY, directionX, directionY, _world, player, maxDistance);
                if (!distance.HasValue) { continue; }

                updateHit(
                    ref nearestHit,
                    projectile,
                    directionX,
                    directionY,
                    distance.Value,
                    _world.CanTeamDamagePlayer(projectileTeam, ownerId, player) ? player : null,
                    null,
                    null);
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

        private void UpdateNearestProjectileHitFromJumpPads(
            ref ShotHitResult? nearestHit,
            PlayerTeam projectileTeam,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance)
        {
            var rayBounds = GetRayBounds(previousX, previousY, directionX, directionY, maxDistance);
            var halfW = JumpPadEntity.HitboxWidth / 2f;
            var halfH = JumpPadEntity.HitboxHeight / 2f;
            foreach (var pad in _jumpPads)
            {
                if (pad.Team == projectileTeam || !pad.IsBuilt || pad.IsDead) { continue; }
                if (!RayBoundsMayIntersectRectangle(rayBounds, pad.X - halfW, pad.Y - halfH, pad.X + halfW, pad.Y + halfH)) { continue; }
                var distance = GetRayIntersectionDistanceWithJumpPad(previousX, previousY, directionX, directionY, pad, maxDistance);
                if (distance.HasValue && (!nearestHit.HasValue || distance.Value < nearestHit.Value.Distance))
                {
                    nearestHit = new ShotHitResult(distance.Value, previousX + directionX * distance.Value, previousY + directionY * distance.Value, null, null, null) { HitJumpPad = pad };
                }
            }
        }
    }
}
