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

        private bool IsBlockingProjectileRoomObject(RoomObjectMarker roomObject)
        {
            return roomObject.Type switch
            {
                RoomObjectType.TeamGate => true,
                RoomObjectType.ControlPointSetupGate => Level.ControlPointSetupGatesActive,
                RoomObjectType.BulletWall => true,
                _ => false,
            };
        }

        private bool IsBlockingHitscanRoomObject(RoomObjectMarker roomObject)
        {
            return roomObject.Type switch
            {
                RoomObjectType.TeamGate => true,
                RoomObjectType.ControlPointSetupGate => Level.ControlPointSetupGatesActive,
                RoomObjectType.BulletWall => true,
                RoomObjectType.IntelGate => true,
                _ => false,
            };
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

            foreach (var gate in Level.RoomObjects)
            {
                if (!IsBlockingHitscanRoomObject(gate))
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

        public bool IsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY)
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
                    return true;
                }
            }

            foreach (var roomObject in Level.RoomObjects)
            {
                if (!IsBlockingProjectileRoomObject(roomObject))
                {
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
