namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class CombatResolver
    {
        private bool IsBlockingGate(RoomObjectMarker roomObject)
        {
            return roomObject.Type switch
            {
                RoomObjectType.TeamGate => true,
                RoomObjectType.ControlPointSetupGate => Level.ControlPointSetupGatesActive,
                _ => false,
            };
        }

        private bool IsBlockingProjectileRoomObject(RoomObjectMarker roomObject, PlayerTeam shotTeam)
        {
            return roomObject.Type switch
            {
                RoomObjectType.TeamGate => true,
                RoomObjectType.ControlPointSetupGate => Level.ControlPointSetupGatesActive,
                RoomObjectType.BulletWall => true,
                RoomObjectType.Barrier => BarrierCollision.BlocksProjectile(roomObject.Barrier, shotTeam),
                RoomObjectType.DirectionalWall => roomObject.DirectionalWall.AffectsProjectiles,
                RoomObjectType.DamageableZone => false,
                _ => false,
            };
        }

        private bool IsBlockingProjectileRoomObject(int roomObjectIndex, RoomObjectMarker roomObject, PlayerTeam shotTeam)
        {
            if (roomObject.Type == RoomObjectType.DamageableZone)
            {
                return _world.BlocksProjectileDamageableZone(roomObjectIndex);
            }

            return IsBlockingProjectileRoomObject(roomObject, shotTeam);
        }

        private bool IsBlockingHitscanRoomObject(RoomObjectMarker roomObject, PlayerTeam shooterTeam, bool carryingIntel)
        {
            return roomObject.Type switch
            {
                RoomObjectType.TeamGate => true,
                RoomObjectType.ControlPointSetupGate => Level.ControlPointSetupGatesActive,
                RoomObjectType.BulletWall => true,
                RoomObjectType.IntelGate => true,
                RoomObjectType.Barrier => false,
                _ => false,
            };
        }

        private bool IsBlockingProjectileRoomObjectForAnyTeam(RoomObjectMarker roomObject)
        {
            if (roomObject.Type == RoomObjectType.Barrier)
            {
                return BarrierCollision.BlocksProjectile(roomObject.Barrier, PlayerTeam.Red)
                    || BarrierCollision.BlocksProjectile(roomObject.Barrier, PlayerTeam.Blue);
            }

            if (roomObject.Type == RoomObjectType.DirectionalWall)
            {
                return roomObject.DirectionalWall.AffectsProjectiles;
            }

            return IsBlockingProjectileRoomObject(roomObject, PlayerTeam.Red);
        }

        private bool IsBlockingHitscanRoomObjectForAnyTeam(RoomObjectMarker roomObject)
        {
            if (roomObject.Type == RoomObjectType.Barrier)
            {
                return false;
            }

            return IsBlockingHitscanRoomObject(roomObject, PlayerTeam.Red, carryingIntel: false);
        }

        private bool IsBlockingGateForTeam(RoomObjectMarker roomObject, PlayerTeam team)
        {
            if (roomObject.Type == RoomObjectType.TeamGate)
            {
                return roomObject.Team.HasValue && roomObject.Team.Value != team;
            }

            if (roomObject.Type == RoomObjectType.ControlPointSetupGate)
            {
                return Level.ControlPointSetupGatesActive;
            }

            return false;
        }

        public bool HasLineOfSight(PlayerEntity attacker, PlayerEntity target)
        {
            var deltaX = target.X - attacker.X;
            var deltaY = (target.Y - target.Height / 4f) - attacker.Y;
            var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance <= 0.0001f)
            {
                return true;
            }

            var directionX = deltaX / distance;
            var directionY = deltaY / distance;
            foreach (var solid in Level.Solids)
            {
                var hitDistance = GetRayIntersectionDistanceWithRectangle(
                    attacker.X,
                    attacker.Y,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance);
                if (hitDistance.HasValue)
                {
                    return false;
                }
            }

            foreach (var gate in Level.GetBlockingTeamGates(attacker.Team, attacker.IsCarryingIntel))
            {
                var hitDistance = GetRayIntersectionDistanceWithRectangle(
                    attacker.X,
                    attacker.Y,
                    directionX,
                    directionY,
                    gate.Left,
                    gate.Top,
                    gate.Right,
                    gate.Bottom,
                    distance);
                if (hitDistance.HasValue)
                {
                    return false;
                }
            }

            foreach (var barrier in Level.GetRoomObjects(RoomObjectType.Barrier))
            {
                var hitDistance = GetRayIntersectionDistanceWithRectangle(
                    attacker.X,
                    attacker.Y,
                    directionX,
                    directionY,
                    barrier.Left,
                    barrier.Top,
                    barrier.Right,
                    barrier.Bottom,
                    distance);
                if (!hitDistance.HasValue)
                {
                    continue;
                }

                var hitX = attacker.X + (directionX * hitDistance.Value);
                var hitY = attacker.Y + (directionY * hitDistance.Value);
                if (!BarrierCollision.BlocksHitscan(
                        barrier.Barrier,
                        attacker.Team,
                        attacker.IsCarryingIntel,
                        barrier,
                        attacker.X,
                        attacker.Y,
                        hitX,
                        hitY))
                {
                    continue;
                }

                return false;
            }

            foreach (var wall in Level.GetRoomObjects(RoomObjectType.DirectionalWall))
            {
                var hitDistance = GetRayIntersectionDistanceWithRectangle(
                    attacker.X,
                    attacker.Y,
                    directionX,
                    directionY,
                    wall.Left,
                    wall.Top,
                    wall.Right,
                    wall.Bottom,
                    distance);
                if (!hitDistance.HasValue)
                {
                    continue;
                }

                var hitX = attacker.X + (directionX * hitDistance.Value);
                var hitY = attacker.Y + (directionY * hitDistance.Value);
                if (!DirectionalWallCollision.BlocksHitscan(
                        wall.DirectionalWall,
                        attacker.Team,
                        attacker.IsCarryingIntel,
                        wall,
                        attacker.X,
                        attacker.Y,
                        hitX,
                        hitY))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        public bool HasSentryLineOfSight(SentryEntity sentry, PlayerEntity target)
        {
            var distance = DistanceBetween(sentry.X, sentry.Y, target.X, target.Y);
            if (distance <= 0.0001f)
            {
                return true;
            }

            var directionX = (target.X - sentry.X) / distance;
            var directionY = (target.Y - sentry.Y) / distance;
            foreach (var solid in Level.Solids)
            {
                var hitDistance = GetRayIntersectionDistanceWithRectangle(
                    sentry.X,
                    sentry.Y,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance);
                if (hitDistance.HasValue)
                {
                    return false;
                }
            }

            foreach (var barrier in Level.GetRoomObjects(RoomObjectType.Barrier))
            {
                var hitDistance = GetRayIntersectionDistanceWithRectangle(
                    sentry.X,
                    sentry.Y,
                    directionX,
                    directionY,
                    barrier.Left,
                    barrier.Top,
                    barrier.Right,
                    barrier.Bottom,
                    distance);
                if (!hitDistance.HasValue)
                {
                    continue;
                }

                var hitX = sentry.X + (directionX * hitDistance.Value);
                var hitY = sentry.Y + (directionY * hitDistance.Value);
                if (BarrierCollision.BlocksHitscan(barrier.Barrier, PlayerTeam.Red, false, barrier, sentry.X, sentry.Y, hitX, hitY)
                    || BarrierCollision.BlocksHitscan(barrier.Barrier, PlayerTeam.Blue, false, barrier, sentry.X, sentry.Y, hitX, hitY))
                {
                    return false;
                }
            }

            foreach (var wall in Level.GetRoomObjects(RoomObjectType.DirectionalWall))
            {
                var hitDistance = GetRayIntersectionDistanceWithRectangle(
                    sentry.X,
                    sentry.Y,
                    directionX,
                    directionY,
                    wall.Left,
                    wall.Top,
                    wall.Right,
                    wall.Bottom,
                    distance);
                if (!hitDistance.HasValue)
                {
                    continue;
                }

                var hitX = sentry.X + (directionX * hitDistance.Value);
                var hitY = sentry.Y + (directionY * hitDistance.Value);
                if (DirectionalWallCollision.BlocksHitscan(wall.DirectionalWall, PlayerTeam.Red, false, wall, sentry.X, sentry.Y, hitX, hitY)
                    || DirectionalWallCollision.BlocksHitscan(wall.DirectionalWall, PlayerTeam.Blue, false, wall, sentry.X, sentry.Y, hitX, hitY))
                {
                    return false;
                }
            }

            foreach (var gate in Level.RoomObjects)
            {
                if (!IsBlockingHitscanRoomObjectForAnyTeam(gate))
                {
                    continue;
                }

                var hitDistance = GetRayIntersectionDistanceWithRectangle(
                    sentry.X,
                    sentry.Y,
                    directionX,
                    directionY,
                    gate.Left,
                    gate.Top,
                    gate.Right,
                    gate.Bottom,
                    distance);
                if (hitDistance.HasValue)
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam)
        {
            var distance = DistanceBetween(originX, originY, targetX, targetY);
            if (distance <= 0.0001f)
            {
                return true;
            }

            var directionX = (targetX - originX) / distance;
            var directionY = (targetY - originY) / distance;
            foreach (var solid in Level.Solids)
            {
                if (GetRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance).HasValue)
                {
                    return false;
                }
            }

            foreach (var gate in Level.RoomObjects)
            {
                if (!IsBlockingGateForTeam(gate, targetTeam))
                {
                    continue;
                }

                if (GetRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    gate.Left,
                    gate.Top,
                    gate.Right,
                    gate.Bottom,
                    distance).HasValue)
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        {
            var distance = DistanceBetween(originX, originY, targetX, targetY);
            if (distance <= 0.0001f)
            {
                return true;
            }

            var directionX = (targetX - originX) / distance;
            var directionY = (targetY - originY) / distance;
            foreach (var solid in Level.Solids)
            {
                if (GetRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance).HasValue)
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY, PlayerTeam shotTeam)
        {
            var distance = DistanceBetween(originX, originY, targetX, targetY);
            if (distance <= 0.0001f)
            {
                return false;
            }

            var directionX = (targetX - originX) / distance;
            var directionY = (targetY - originY) / distance;
            foreach (var solid in Level.Solids)
            {
                if (GetRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance).HasValue)
                {
                    _world.SetProjectileSpawnBlockedDebug(solid.Left, solid.Top, solid.Width, solid.Height, "LevelSolid");
                    return true;
                }
            }

            for (var roomObjectIndex = 0; roomObjectIndex < Level.RoomObjects.Count; roomObjectIndex += 1)
            {
                if (!Level.IsRoomObjectActive(roomObjectIndex))
                {
                    continue;
                }

                var roomObject = Level.RoomObjects[roomObjectIndex];
                if (!IsBlockingProjectileRoomObject(roomObjectIndex, roomObject, shotTeam))
                {
                    continue;
                }

                if (roomObject.Type == RoomObjectType.Barrier)
                {
                    if (BarrierProjectileRaycast.TryRaycastMarker(
                            roomObject.Barrier,
                            shotTeam,
                            roomObject,
                            originX,
                            originY,
                            directionX,
                            directionY,
                            distance,
                            out _))
                    {
                        _world.SetProjectileSpawnBlockedDebug(roomObject.Left, roomObject.Top, roomObject.Width, roomObject.Height, $"RoomObject:{roomObject.Type}");
                        return true;
                    }

                    continue;
                }

                if (GetRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    roomObject.Left,
                    roomObject.Top,
                    roomObject.Right,
                    roomObject.Bottom,
                    distance).HasValue)
                {
                    _world.SetProjectileSpawnBlockedDebug(roomObject.Left, roomObject.Top, roomObject.Width, roomObject.Height, $"RoomObject:{roomObject.Type}");
                    return true;
                }
            }

            return false;
        }

        public bool IsFlameSpawnBlocked(float originX, float originY, float spawnX, float spawnY, PlayerTeam team)
        {
            var distance = DistanceBetween(originX, originY, spawnX, spawnY);
            if (distance <= 0.0001f)
            {
                return false;
            }

            var directionX = (spawnX - originX) / distance;
            var directionY = (spawnY - originY) / distance;
            foreach (var solid in Level.Solids)
            {
                if (GetRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance).HasValue)
                {
                    return true;
                }
            }

            foreach (var gate in Level.RoomObjects)
            {
                if (!IsBlockingGateForTeam(gate, team))
                {
                    continue;
                }

                if (GetRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    gate.Left,
                    gate.Top,
                    gate.Right,
                    gate.Bottom,
                    distance).HasValue)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
