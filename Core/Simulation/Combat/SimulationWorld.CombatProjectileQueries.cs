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
            UpdateNearestEnvironmentProjectileHitFromRoomObjects(ref nearestHit, rocket.Team, rocket, rocket.PreviousX, rocket.PreviousY, directionX, directionY, maxDistance, ProjectileRoomObjectBlockerProfile.Standard, destroyOnHit: false, UpdateNearestRocketEnvironmentHit);
            UpdateNearestTargetProjectileHitFromSentries(ref nearestHit, rocket, rocket.Team, rocket.PreviousX, rocket.PreviousY, directionX, directionY, maxDistance, UpdateNearestRocketHit);
            UpdateNearestTargetProjectileHitFromGenerators(ref nearestHit, rocket, rocket.Team, rocket.PreviousX, rocket.PreviousY, directionX, directionY, maxDistance, UpdateNearestRocketHit);
            UpdateNearestRocketHitFromJumpPads(ref nearestHit, rocket, rocket.Team, rocket.PreviousX, rocket.PreviousY, directionX, directionY, maxDistance);
            UpdateNearestTargetProjectileHitFromPlayers(ref nearestHit, rocket, rocket.Team, rocket.OwnerId, rocket.PreviousX, rocket.PreviousY, directionX, directionY, maxDistance, UpdateNearestRocketHit);
            return nearestHit;
        }

        public MineHitResult? GetNearestMineHit(MineProjectileEntity mine, float directionX, float directionY, float maxDistance)
        {
            MineHitResult? nearestHit = null;
            UpdateNearestEnvironmentProjectileHitFromSolids(ref nearestHit, mine, mine.PreviousX, mine.PreviousY, directionX, directionY, maxDistance, destroyOnHit: false, UpdateNearestMineHit);
            UpdateNearestTargetProjectileHitFromSentries(ref nearestHit, mine, mine.Team, mine.PreviousX, mine.PreviousY, directionX, directionY, maxDistance, UpdateNearestMineTargetHit);
            UpdateNearestMineHitFromGenerators(ref nearestHit, mine, mine.Team, mine.PreviousX, mine.PreviousY, directionX, directionY, maxDistance);
            UpdateNearestEnvironmentProjectileHitFromRoomObjects(ref nearestHit, mine.Team, mine, mine.PreviousX, mine.PreviousY, directionX, directionY, maxDistance, ProjectileRoomObjectBlockerProfile.Standard, destroyOnHit: true, UpdateNearestMineHit);
            return nearestHit;
        }

        public PlayerEntity? GetNearestGrenadePlayerHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float maxDistance)
        {
            if (maxDistance <= 0.0001f)
            {
                return null;
            }

            var rayBounds = GetRayBounds(grenade.PreviousX, grenade.PreviousY, directionX, directionY, maxDistance);
            PlayerEntity? nearestPlayer = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!_world.CanTeamDamagePlayer(grenade.Team, grenade.OwnerId, player) || player.Id == grenade.OwnerId)
                {
                    continue;
                }

                GetPlayerPresentationHitBounds(_world, player, out var left, out var top, out var right, out var bottom);
                if (!RayBoundsMayIntersectRectangle(rayBounds, left, top, right, bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithPlayer(grenade.PreviousX, grenade.PreviousY, directionX, directionY, _world, player, maxDistance);
                if (distance.HasValue && distance.Value < nearestDistance)
                {
                    nearestPlayer = player;
                    nearestDistance = distance.Value;
                }
            }

            return nearestPlayer;
        }

        public GrenadeEnvironmentHit? GetNearestGrenadeEnvironmentHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float maxDistance)
        {
            GrenadeEnvironmentHit? nearestHit = null;
            var rayBounds = GetRayBounds(grenade.PreviousX, grenade.PreviousY, directionX, directionY, maxDistance);

            foreach (var solid in GetPotentialSolidRaycastCandidates(rayBounds))
            {
                if (!RayBoundsMayIntersectRectangle(rayBounds, solid.Left, solid.Top, solid.Right, solid.Bottom)) { continue; }
                var result = GetRayIntersectionWithNormalWithRectangle(grenade.PreviousX, grenade.PreviousY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, maxDistance);
                if (result.HasValue && (!nearestHit.HasValue || result.Value.Distance < nearestHit.Value.Distance))
                {
                    nearestHit = new GrenadeEnvironmentHit(result.Value.Distance, grenade.PreviousX + directionX * result.Value.Distance, grenade.PreviousY + directionY * result.Value.Distance, result.Value.NormalX, result.Value.NormalY);
                }
            }

            for (var roomObjectIndex = 0; roomObjectIndex < Level.RoomObjects.Count; roomObjectIndex += 1)
            {
                var roomObject = Level.RoomObjects[roomObjectIndex];
                if (roomObject.Type == RoomObjectType.DamageableZone)
                {
                    continue;
                }

                if (!TryGetProjectileRoomObjectHitbox(roomObjectIndex, roomObject, grenade.Team, ProjectileRoomObjectBlockerProfile.Standard, out var hitbox)) { continue; }
                if (!RayBoundsMayIntersectRectangle(rayBounds, hitbox.Left, hitbox.Top, hitbox.Right, hitbox.Bottom)) { continue; }
                var result = GetRayIntersectionWithNormalWithRectangle(grenade.PreviousX, grenade.PreviousY, directionX, directionY, hitbox.Left, hitbox.Top, hitbox.Right, hitbox.Bottom, maxDistance);
                if (result.HasValue && (!nearestHit.HasValue || result.Value.Distance < nearestHit.Value.Distance))
                {
                    nearestHit = new GrenadeEnvironmentHit(result.Value.Distance, grenade.PreviousX + directionX * result.Value.Distance, grenade.PreviousY + directionY * result.Value.Distance, result.Value.NormalX, result.Value.NormalY);
                }
            }

            return nearestHit;
        }

        public bool TryGetGrenadeDamageableZoneContact(
            GrenadeProjectileEntity grenade,
            float directionX,
            float directionY,
            float maxDistance,
            out float hitX,
            out float hitY,
            out int roomObjectIndex)
        {
            hitX = 0f;
            hitY = 0f;
            roomObjectIndex = -1;
            if (maxDistance <= 0.0001f)
            {
                return false;
            }

            var rayBounds = GetRayBounds(grenade.PreviousX, grenade.PreviousY, directionX, directionY, maxDistance);
            float? nearestDistance = null;
            for (var index = 0; index < Level.RoomObjects.Count; index += 1)
            {
                if (!Level.IsRoomObjectActive(index))
                {
                    continue;
                }

                var roomObject = Level.RoomObjects[index];
                if (roomObject.Type != RoomObjectType.DamageableZone
                    || !_world.BlocksProjectileDamageableZone(index))
                {
                    continue;
                }

                if (!RayBoundsMayIntersectRectangle(
                        rayBounds,
                        roomObject.Left,
                        roomObject.Top,
                        roomObject.Right,
                        roomObject.Bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithRectangle(
                    grenade.PreviousX,
                    grenade.PreviousY,
                    directionX,
                    directionY,
                    roomObject.Left,
                    roomObject.Top,
                    roomObject.Right,
                    roomObject.Bottom,
                    maxDistance);
                if (!distance.HasValue || (nearestDistance.HasValue && distance.Value >= nearestDistance.Value))
                {
                    continue;
                }

                nearestDistance = distance.Value;
                roomObjectIndex = index;
                hitX = grenade.PreviousX + (directionX * distance.Value);
                hitY = grenade.PreviousY + (directionY * distance.Value);
            }

            return roomObjectIndex >= 0;
        }

        public FlameHitResult? GetNearestFlameHit(FlameProjectileEntity flame, float directionX, float directionY, float maxDistance)
        {
            FlameHitResult? nearestHit = null;
            UpdateNearestEnvironmentProjectileHitFromSolids(ref nearestHit, flame, flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance, destroyOnHit: false, UpdateNearestFlameEnvironmentHit);
            UpdateNearestEnvironmentProjectileHitFromRoomObjects(ref nearestHit, flame.Team, flame, flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance, ProjectileRoomObjectBlockerProfile.Flame, destroyOnHit: false, UpdateNearestFlameEnvironmentHit);
            UpdateNearestTargetProjectileHitFromSentries(ref nearestHit, flame, flame.Team, flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlameHit);
            UpdateNearestTargetProjectileHitFromGenerators(ref nearestHit, flame, flame.Team, flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlameHit);
            UpdateNearestFlameHitFromJumpPads(ref nearestHit, flame, flame.Team, flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance);
            UpdateNearestFlameHitFromPlayers(ref nearestHit, flame, directionX, directionY, maxDistance);
            return nearestHit;
        }

        public ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float maxDistance)
        {
            ShotHitResult? nearestHit = null;
            UpdateNearestEnvironmentProjectileHitFromSolids(ref nearestHit, flare, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance, destroyOnHit: false, UpdateNearestFlareEnvironmentHit);
            UpdateNearestEnvironmentProjectileHitFromRoomObjects(ref nearestHit, flare.Team, flare, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance, ProjectileRoomObjectBlockerProfile.Standard, destroyOnHit: false, UpdateNearestFlareEnvironmentHit);
            UpdateNearestTargetProjectileHitFromSentries(ref nearestHit, flare, flare.Team, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlareHit);
            UpdateNearestTargetProjectileHitFromGenerators(ref nearestHit, flare, flare.Team, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlareHit);
            UpdateNearestFlareHitFromJumpPads(ref nearestHit, flare, flare.Team, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance);
            UpdateNearestTargetProjectileHitFromPlayers(ref nearestHit, flare, flare.Team, flare.OwnerId, flare.PreviousX, flare.PreviousY, directionX, directionY, maxDistance, UpdateNearestFlareHit);
            return nearestHit;
        }

        public ShotHitResult? GetNearestBladeHit(BladeProjectileEntity blade, float directionX, float directionY, float maxDistance)
        {
            ShotHitResult? nearestHit = null;
            UpdateNearestEnvironmentProjectileHitFromSolids(ref nearestHit, blade, blade.PreviousX, blade.PreviousY, directionX, directionY, maxDistance, destroyOnHit: false, UpdateNearestBladeEnvironmentHit);
            UpdateNearestEnvironmentProjectileHitFromRoomObjects(ref nearestHit, blade.Team, blade, blade.PreviousX, blade.PreviousY, directionX, directionY, maxDistance, ProjectileRoomObjectBlockerProfile.Standard, destroyOnHit: false, UpdateNearestBladeEnvironmentHit);
            UpdateNearestTargetProjectileHitFromSentries(ref nearestHit, blade, blade.Team, blade.PreviousX, blade.PreviousY, directionX, directionY, maxDistance, UpdateNearestBladeHit);
            UpdateNearestTargetProjectileHitFromGenerators(ref nearestHit, blade, blade.Team, blade.PreviousX, blade.PreviousY, directionX, directionY, maxDistance, UpdateNearestBladeHit);
            UpdateNearestBladeHitFromJumpPads(ref nearestHit, blade, blade.Team, blade.PreviousX, blade.PreviousY, directionX, directionY, maxDistance);
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
            foreach (var solid in GetPotentialSolidRaycastCandidates(rayBounds))
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
            PlayerTeam projectileTeam,
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
            for (var roomObjectIndex = 0; roomObjectIndex < Level.RoomObjects.Count; roomObjectIndex += 1)
            {
                if (!Level.IsRoomObjectActive(roomObjectIndex))
                {
                    continue;
                }

                var roomObject = Level.RoomObjects[roomObjectIndex];
                if (!TryGetProjectileRoomObjectHitbox(roomObjectIndex, roomObject, projectileTeam, blockerProfile, out var hitbox)) { continue; }
                if (!RayBoundsMayIntersectRectangle(rayBounds, hitbox.Left, hitbox.Top, hitbox.Right, hitbox.Bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithRectangle(previousX, previousY, directionX, directionY, hitbox.Left, hitbox.Top, hitbox.Right, hitbox.Bottom, maxDistance);
                if (!distance.HasValue)
                {
                    continue;
                }

                if (roomObject.Type == RoomObjectType.Barrier)
                {
                    if (!BarrierProjectileRaycast.TryRaycastMarker(
                            roomObject.Barrier,
                            projectileTeam,
                            roomObject,
                            previousX,
                            previousY,
                            directionX,
                            directionY,
                            maxDistance,
                            out var barrierDistance))
                    {
                        continue;
                    }

                    updateHit(ref nearestHit, projectile, directionX, directionY, barrierDistance, destroyOnHit);
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

                if (roomObject.Type == RoomObjectType.DamageableZone)
                {
                    _world.TryApplyDamageableZoneDamage(
                        roomObjectIndex,
                        ResolveEnvironmentProjectileDamage(projectile));
                }

                updateHit(ref nearestHit, projectile, directionX, directionY, distance.Value, destroyOnHit);
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
                if (!_world.CanTeamDamagePlayer(projectileTeam, ownerId, player) || player.Id == ownerId) { continue; }
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
                if (distance.HasValue) { updateHit(ref nearestHit, projectile, directionX, directionY, distance.Value, player, null, null); }
            }
        }

        private void UpdateNearestFlameHitFromPlayers(
            ref FlameHitResult? nearestHit,
            FlameProjectileEntity flame,
            float directionX,
            float directionY,
            float maxDistance)
        {
            var rayBounds = GetRayBounds(flame.PreviousX, flame.PreviousY, directionX, directionY, maxDistance);
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive
                    || !_world.CanTeamDamagePlayer(flame.Team, flame.OwnerId, player)
                    || player.Id == flame.OwnerId
                    || flame.HasHitPlayer(player.Id))
                {
                    continue;
                }

                GetPlayerPresentationHitBounds(_world, player, out var left, out var top, out var right, out var bottom);
                if (!RayBoundsMayIntersectRectangle(rayBounds, left, top, right, bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithPlayer(
                    flame.PreviousX,
                    flame.PreviousY,
                    directionX,
                    directionY,
                    _world,
                    player,
                    maxDistance);
                if (distance.HasValue)
                {
                    UpdateNearestFlameHit(ref nearestHit, flame, directionX, directionY, distance.Value, player);
                }
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

        private static float ResolveEnvironmentProjectileDamage<TProjectile>(TProjectile projectile)
        {
            return projectile switch
            {
                BladeProjectileEntity blade => blade.HitDamage * blade.CriticalDamageMultiplier,
                FlameProjectileEntity flame => flame.DirectHitDamageValue * flame.CriticalDamageMultiplier,
                FlareProjectileEntity flare => FlareProjectileEntity.DamagePerHit * flare.CriticalDamageMultiplier,
                RocketProjectileEntity rocket => rocket.DirectHitDamageValue,
                MineProjectileEntity mine => mine.ExplosionDamage * mine.CriticalDamageMultiplier,
                GrenadeProjectileEntity grenade => GrenadeProjectileEntity.DirectHitDamage,
                _ => 1f,
            };
        }

        private bool TryGetProjectileRoomObjectHitbox(
            int roomObjectIndex,
            RoomObjectMarker roomObject,
            PlayerTeam projectileTeam,
            ProjectileRoomObjectBlockerProfile blockerProfile,
            out RectangleHitbox hitbox)
        {
            switch (blockerProfile)
            {
                case ProjectileRoomObjectBlockerProfile.Standard:
                    if (roomObject.Type == RoomObjectType.DamageableZone
                        && _world.BlocksProjectileDamageableZone(roomObjectIndex))
                    {
                        hitbox = new RectangleHitbox(roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom);
                        return true;
                    }

                    if (roomObject.Type == RoomObjectType.Barrier && BarrierCollision.BlocksProjectile(roomObject.Barrier, projectileTeam))
                    {
                        hitbox = new RectangleHitbox(roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom);
                        return true;
                    }

                    if (roomObject.Type == RoomObjectType.DirectionalWall
                        && roomObject.DirectionalWall.AffectsProjectiles)
                    {
                        hitbox = new RectangleHitbox(roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom);
                        return true;
                    }

                    if (IsBlockingProjectileRoomObject(roomObject, projectileTeam))
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

                        case RoomObjectType.Barrier when BarrierCollision.BlocksProjectile(roomObject.Barrier, projectileTeam):
                            hitbox = new RectangleHitbox(roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom);
                            return true;

                        case RoomObjectType.DirectionalWall when roomObject.DirectionalWall.AffectsProjectiles:
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

        private void UpdateNearestRocketHitFromJumpPads(
            ref RocketHitResult? nearestHit,
            RocketProjectileEntity rocket,
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
                    nearestHit = new RocketHitResult(distance.Value, rocket.PreviousX + directionX * distance.Value, rocket.PreviousY + directionY * distance.Value, null, null, null) { HitJumpPad = pad };
                }
            }
        }

        private void UpdateNearestFlameHitFromJumpPads(
            ref FlameHitResult? nearestHit,
            FlameProjectileEntity flame,
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
                    nearestHit = new FlameHitResult(distance.Value, flame.PreviousX + directionX * distance.Value, flame.PreviousY + directionY * distance.Value, null, null, null) { HitJumpPad = pad };
                }
            }
        }

        private void UpdateNearestFlareHitFromJumpPads(
            ref ShotHitResult? nearestHit,
            FlareProjectileEntity flare,
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
                    nearestHit = new ShotHitResult(distance.Value, flare.PreviousX + directionX * distance.Value, flare.PreviousY + directionY * distance.Value, null, null, null) { HitJumpPad = pad };
                }
            }
        }

        private void UpdateNearestBladeHitFromJumpPads(
            ref ShotHitResult? nearestHit,
            BladeProjectileEntity blade,
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
                    nearestHit = new ShotHitResult(distance.Value, blade.PreviousX + directionX * distance.Value, blade.PreviousY + directionY * distance.Value, null, null, null) { HitJumpPad = pad };
                }
            }
        }
    }
}
