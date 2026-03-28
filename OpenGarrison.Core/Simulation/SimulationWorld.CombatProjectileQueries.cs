namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class CombatResolver
    {
        private enum ProjectileRoomObjectBlockerProfile
        {
            Standard,
            Flame,
        }

        private delegate void UpdateEnvironmentProjectileHit<TProjectile, THit>(
            ref THit? nearestHit,
            TProjectile projectile,
            float directionX,
            float directionY,
            float distance,
            bool destroyOnHit)
            where THit : struct;

        private delegate void UpdateTargetProjectileHit<TProjectile, THit>(
            ref THit? nearestHit,
            TProjectile projectile,
            float directionX,
            float directionY,
            float distance,
            PlayerEntity? player,
            SentryEntity? sentry,
            GeneratorState? generator)
            where THit : struct;

        public RocketHitResult? GetNearestRocketHit(RocketProjectileEntity rocket, float directionX, float directionY, float maxDistance)
        {
            RocketHitResult? nearestHit = null;
            UpdateNearestEnvironmentProjectileHitFromSolids(ref nearestHit, rocket, rocket.PreviousX, rocket.PreviousY, directionX, directionY, maxDistance, destroyOnHit: false, UpdateNearestRocketEnvironmentHit);
            UpdateNearestEnvironmentProjectileHitFromRoomObjects(ref nearestHit, rocket, rocket.PreviousX, rocket.PreviousY, directionX, directionY, maxDistance, ProjectileRoomObjectBlockerProfile.Standard, destroyOnHit: false, UpdateNearestRocketEnvironmentHit);
            UpdateNearestTargetProjectileHitFromSentries(ref nearestHit, rocket, rocket.Team, rocket.PreviousX, rocket.PreviousY, directionX, directionY, maxDistance, UpdateNearestRocketHit);
            UpdateNearestTargetProjectileHitFromGenerators(ref nearestHit, rocket, rocket.Team, rocket.PreviousX, rocket.PreviousY, directionX, directionY, maxDistance, UpdateNearestRocketHit);
            UpdateNearestTargetProjectileHitFromPlayers(ref nearestHit, rocket, rocket.Team, rocket.OwnerId, rocket.PreviousX, rocket.PreviousY, directionX, directionY, maxDistance, UpdateNearestRocketHit);
            return nearestHit;
        }

        public MineHitResult? GetNearestMineHit(MineProjectileEntity mine, float directionX, float directionY, float maxDistance)
        {
            MineHitResult? nearestHit = null;
            UpdateNearestEnvironmentProjectileHitFromSolids(ref nearestHit, mine, mine.PreviousX, mine.PreviousY, directionX, directionY, maxDistance, destroyOnHit: false, UpdateNearestMineHit);
            UpdateNearestTargetProjectileHitFromSentries(ref nearestHit, mine, mine.Team, mine.PreviousX, mine.PreviousY, directionX, directionY, maxDistance, UpdateNearestMineTargetHit);
            UpdateNearestMineHitFromGenerators(ref nearestHit, mine, mine.Team, mine.PreviousX, mine.PreviousY, directionX, directionY, maxDistance);
            UpdateNearestEnvironmentProjectileHitFromRoomObjects(ref nearestHit, mine, mine.PreviousX, mine.PreviousY, directionX, directionY, maxDistance, ProjectileRoomObjectBlockerProfile.Standard, destroyOnHit: true, UpdateNearestMineHit);
            return nearestHit;
        }

        public FlameHitResult? GetNearestFlameHit(FlameProjectileEntity flame, float directionX, float directionY, float maxDistance)
        {
            FlameHitResult? nearestHit = null;
            UpdateNearestEnvironmentProjectileHitFromSolids(ref nearestHit, flame, flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance, destroyOnHit: false, UpdateNearestFlameEnvironmentHit);
            UpdateNearestEnvironmentProjectileHitFromRoomObjects(ref nearestHit, flame, flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance, ProjectileRoomObjectBlockerProfile.Flame, destroyOnHit: false, UpdateNearestFlameEnvironmentHit);
            UpdateNearestTargetProjectileHitFromSentries(ref nearestHit, flame, flame.Team, flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlameHit);
            UpdateNearestTargetProjectileHitFromGenerators(ref nearestHit, flame, flame.Team, flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlameHit);
            UpdateNearestTargetProjectileHitFromPlayers(ref nearestHit, flame, flame.Team, flame.OwnerId, flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlameHit);
            return nearestHit;
        }

        public ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float maxDistance)
        {
            ShotHitResult? nearestHit = null;
            UpdateNearestEnvironmentProjectileHitFromSolids(ref nearestHit, flare, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance, destroyOnHit: false, UpdateNearestFlareEnvironmentHit);
            UpdateNearestEnvironmentProjectileHitFromRoomObjects(ref nearestHit, flare, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance, ProjectileRoomObjectBlockerProfile.Standard, destroyOnHit: false, UpdateNearestFlareEnvironmentHit);
            UpdateNearestTargetProjectileHitFromSentries(ref nearestHit, flare, flare.Team, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlareHit);
            UpdateNearestTargetProjectileHitFromGenerators(ref nearestHit, flare, flare.Team, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlareHit);
            UpdateNearestTargetProjectileHitFromPlayers(ref nearestHit, flare, flare.Team, flare.OwnerId, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlareHit);
            return nearestHit;
        }

        public ShotHitResult? GetNearestBladeHit(BladeProjectileEntity blade, float directionX, float directionY, float maxDistance)
        {
            ShotHitResult? nearestHit = null;
            UpdateNearestEnvironmentProjectileHitFromSolids(ref nearestHit, blade, blade.PreviousX, blade.PreviousY, directionX, directionY, maxDistance, destroyOnHit: false, UpdateNearestBladeEnvironmentHit);
            UpdateNearestEnvironmentProjectileHitFromRoomObjects(ref nearestHit, blade, blade.PreviousX, blade.PreviousY, directionX, directionY, maxDistance, ProjectileRoomObjectBlockerProfile.Standard, destroyOnHit: false, UpdateNearestBladeEnvironmentHit);
            UpdateNearestTargetProjectileHitFromSentries(ref nearestHit, blade, blade.Team, blade.PreviousX, blade.PreviousY, directionX, directionY, maxDistance, UpdateNearestBladeHit);
            UpdateNearestTargetProjectileHitFromGenerators(ref nearestHit, blade, blade.Team, blade.PreviousX, blade.PreviousY, directionX, directionY, maxDistance, UpdateNearestBladeHit);
            UpdateNearestTargetProjectileHitFromPlayers(ref nearestHit, blade, blade.Team, blade.OwnerId, blade.PreviousX, blade.PreviousY, directionX, directionY, maxDistance, UpdateNearestBladeHit);
            return nearestHit;
        }

        private void UpdateNearestEnvironmentProjectileHitFromSolids<TProjectile, THit>(
            ref THit? nearestHit,
            TProjectile projectile,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            bool destroyOnHit,
            UpdateEnvironmentProjectileHit<TProjectile, THit> updateHit)
            where THit : struct
        {
            var rayBounds = GetRayBounds(previousX, previousY, directionX, directionY, maxDistance);
            foreach (var solid in Level.Solids)
            {
                if (!RayBoundsMayIntersectRectangle(rayBounds, solid.Left, solid.Top, solid.Right, solid.Bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithRectangle(previousX, previousY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, maxDistance);
                if (distance.HasValue) { updateHit(ref nearestHit, projectile, directionX, directionY, distance.Value, destroyOnHit); }
            }
        }

        private void UpdateNearestEnvironmentProjectileHitFromRoomObjects<TProjectile, THit>(
            ref THit? nearestHit,
            TProjectile projectile,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            ProjectileRoomObjectBlockerProfile blockerProfile,
            bool destroyOnHit,
            UpdateEnvironmentProjectileHit<TProjectile, THit> updateHit)
            where THit : struct
        {
            var rayBounds = GetRayBounds(previousX, previousY, directionX, directionY, maxDistance);
            foreach (var roomObject in Level.RoomObjects)
            {
                if (!TryGetProjectileRoomObjectHitbox(roomObject, blockerProfile, out var hitbox)) { continue; }
                if (!RayBoundsMayIntersectRectangle(rayBounds, hitbox.Left, hitbox.Top, hitbox.Right, hitbox.Bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithRectangle(previousX, previousY, directionX, directionY, hitbox.Left, hitbox.Top, hitbox.Right, hitbox.Bottom, maxDistance);
                if (distance.HasValue) { updateHit(ref nearestHit, projectile, directionX, directionY, distance.Value, destroyOnHit); }
            }
        }

        private void UpdateNearestTargetProjectileHitFromPlayers<TProjectile, THit>(
            ref THit? nearestHit,
            TProjectile projectile,
            PlayerTeam projectileTeam,
            int ownerId,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            UpdateTargetProjectileHit<TProjectile, THit> updateHit)
            where THit : struct
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

        private void UpdateNearestTargetProjectileHitFromSentries<TProjectile, THit>(
            ref THit? nearestHit,
            TProjectile projectile,
            PlayerTeam projectileTeam,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            UpdateTargetProjectileHit<TProjectile, THit> updateHit)
            where THit : struct
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

        private void UpdateNearestTargetProjectileHitFromGenerators<TProjectile, THit>(
            ref THit? nearestHit,
            TProjectile projectile,
            PlayerTeam projectileTeam,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance,
            UpdateTargetProjectileHit<TProjectile, THit> updateHit)
            where THit : struct
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

        private void UpdateNearestMineHitFromGenerators(
            ref MineHitResult? nearestHit,
            MineProjectileEntity mine,
            PlayerTeam projectileTeam,
            float previousX,
            float previousY,
            float directionX,
            float directionY,
            float maxDistance)
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
                if (distance.HasValue)
                {
                    UpdateNearestMineHit(ref nearestHit, mine, directionX, directionY, distance.Value, destroyOnHit: false);
                }
            }
        }

        private bool TryGetProjectileRoomObjectHitbox(
            RoomObjectMarker roomObject,
            ProjectileRoomObjectBlockerProfile blockerProfile,
            out RectangleHitbox hitbox)
        {
            switch (blockerProfile)
            {
                case ProjectileRoomObjectBlockerProfile.Standard:
                    if (IsBlockingProjectileRoomObject(roomObject))
                    {
                        hitbox = new RectangleHitbox(roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom);
                        return true;
                    }
                    break;

                case ProjectileRoomObjectBlockerProfile.Flame:
                    switch (roomObject.Type)
                    {
                        case RoomObjectType.TeamGate:
                        case RoomObjectType.BulletWall:
                        case RoomObjectType.HealingCabinet:
                            hitbox = new RectangleHitbox(roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom);
                            return true;

                        case RoomObjectType.ControlPointSetupGate when Level.ControlPointSetupGatesActive:
                            hitbox = new RectangleHitbox(roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom);
                            return true;
                    }
                    break;
            }

            hitbox = default;
            return false;
        }

        private static void UpdateNearestRocketEnvironmentHit(ref RocketHitResult? nearestHit, RocketProjectileEntity rocket, float directionX, float directionY, float distance, bool destroyOnHit)
        {
            UpdateNearestRocketHit(ref nearestHit, rocket, directionX, directionY, distance, null);
        }

        private static void UpdateNearestBladeEnvironmentHit(ref ShotHitResult? nearestHit, BladeProjectileEntity blade, float directionX, float directionY, float distance, bool destroyOnHit)
        {
            UpdateNearestBladeHit(ref nearestHit, blade, directionX, directionY, distance, null);
        }

        private static void UpdateNearestFlameEnvironmentHit(ref FlameHitResult? nearestHit, FlameProjectileEntity flame, float directionX, float directionY, float distance, bool destroyOnHit)
        {
            UpdateNearestFlameHit(ref nearestHit, flame, directionX, directionY, distance, null);
        }

        private static void UpdateNearestFlareEnvironmentHit(ref ShotHitResult? nearestHit, FlareProjectileEntity flare, float directionX, float directionY, float distance, bool destroyOnHit)
        {
            UpdateNearestFlareHit(ref nearestHit, flare, directionX, directionY, distance, null);
        }

        private static void UpdateNearestMineTargetHit(ref MineHitResult? nearestHit, MineProjectileEntity mine, float directionX, float directionY, float distance, PlayerEntity? player, SentryEntity? sentry, GeneratorState? generator)
        {
            UpdateNearestMineHit(ref nearestHit, mine, directionX, directionY, distance, destroyOnHit: false);
        }

        private static void UpdateNearestFlameHit(ref FlameHitResult? nearestHit, FlameProjectileEntity flame, float directionX, float directionY, float distance, PlayerEntity? player, SentryEntity? sentry = null, GeneratorState? generator = null)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new FlameHitResult(distance, flame.PreviousX + directionX * distance, flame.PreviousY + directionY * distance, player, sentry, generator);
        }

        private static void UpdateNearestFlareHit(ref ShotHitResult? nearestHit, FlareProjectileEntity flare, float directionX, float directionY, float distance, PlayerEntity? player, SentryEntity? sentry = null, GeneratorState? generator = null)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new ShotHitResult(distance, flare.PreviousX + directionX * distance, flare.PreviousY + directionY * distance, player, sentry, generator);
        }

        private static void UpdateNearestBladeHit(ref ShotHitResult? nearestHit, BladeProjectileEntity blade, float directionX, float directionY, float distance, PlayerEntity? player, SentryEntity? sentry = null, GeneratorState? generator = null)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new ShotHitResult(distance, blade.PreviousX + directionX * distance, blade.PreviousY + directionY * distance, player, sentry, generator);
        }

        private static void UpdateNearestRocketHit(ref RocketHitResult? nearestHit, RocketProjectileEntity rocket, float directionX, float directionY, float distance, PlayerEntity? player, SentryEntity? sentry = null, GeneratorState? generator = null)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new RocketHitResult(distance, rocket.PreviousX + directionX * distance, rocket.PreviousY + directionY * distance, player, sentry, generator);
        }

        private static void UpdateNearestMineHit(ref MineHitResult? nearestHit, MineProjectileEntity mine, float directionX, float directionY, float distance, bool destroyOnHit)
        {
            if (nearestHit.HasValue && nearestHit.Value.Distance <= distance) { return; }
            nearestHit = new MineHitResult(distance, mine.PreviousX + directionX * distance, mine.PreviousY + directionY * distance, destroyOnHit);
        }
    }
}
