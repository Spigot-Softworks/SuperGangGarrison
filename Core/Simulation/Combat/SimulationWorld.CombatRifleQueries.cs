namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class CombatResolver
    {
        private struct RifleHitState
        {
            public RifleHitState(float nearestDistance)
            {
                NearestDistance = nearestDistance;
                HitPlayer = null;
                HitSentry = null;
                HitGenerator = null;
            }

            public float NearestDistance;
            public PlayerEntity? HitPlayer;
            public SentryEntity? HitSentry;
            public GeneratorState? HitGenerator;
        }

        public RifleHitResult ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance)
            => ResolveRifleHit(attacker, attacker.X, attacker.Y, directionX, directionY, maxDistance);

        public RifleHitResult ResolveRifleHit(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance)
        {
            var hitState = new RifleHitState(maxDistance);

            UpdateNearestRifleHitFromSolids(ref hitState, originX, originY, directionX, directionY);
            UpdateNearestRifleHitFromRoomObjects(ref hitState, originX, originY, directionX, directionY);
            UpdateNearestRifleHitFromGenerators(ref hitState, attacker, originX, originY, directionX, directionY);
            UpdateNearestRifleHitFromSentries(ref hitState, attacker, originX, originY, directionX, directionY);
            UpdateNearestRifleHitFromPlayers(ref hitState, attacker, originX, originY, directionX, directionY);

            return new RifleHitResult(hitState.NearestDistance, hitState.HitPlayer, hitState.HitSentry, hitState.HitGenerator);
        }

        private void UpdateNearestRifleHitFromSolids(ref RifleHitState hitState, float originX, float originY, float directionX, float directionY)
        {
            foreach (var solid in Level.Solids)
            {
                var distance = GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, hitState.NearestDistance);
                if (distance.HasValue) { UpdateNearestRifleObstacleHit(ref hitState, distance.Value); }
            }
        }

        private void UpdateNearestRifleHitFromRoomObjects(ref RifleHitState hitState, float originX, float originY, float directionX, float directionY)
        {
            foreach (var roomObject in Level.RoomObjects)
            {
                if (!IsBlockingHitscanRoomObject(roomObject)) { continue; }
                var distance = GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom, hitState.NearestDistance);
                if (distance.HasValue) { UpdateNearestRifleObstacleHit(ref hitState, distance.Value); }
            }
        }

        private void UpdateNearestRifleHitFromSentries(ref RifleHitState hitState, PlayerEntity attacker, float originX, float originY, float directionX, float directionY)
        {
            foreach (var sentry in _sentries)
            {
                if (sentry.Team == attacker.Team) { continue; }
                var distance = GetRayIntersectionDistanceWithSentry(originX, originY, directionX, directionY, sentry, hitState.NearestDistance);
                if (distance.HasValue) { UpdateNearestRifleSentryHit(ref hitState, distance.Value, sentry); }
            }
        }

        private void UpdateNearestRifleHitFromGenerators(ref RifleHitState hitState, PlayerEntity attacker, float originX, float originY, float directionX, float directionY)
        {
            for (var index = 0; index < _generators.Count; index += 1)
            {
                var generator = _generators[index];
                if (generator.Team == attacker.Team || generator.IsDestroyed)
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithGenerator(originX, originY, directionX, directionY, generator, hitState.NearestDistance);
                if (distance.HasValue) { UpdateNearestRifleGeneratorHit(ref hitState, distance.Value, generator); }
            }
        }

        private void UpdateNearestRifleHitFromPlayers(ref RifleHitState hitState, PlayerEntity attacker, float originX, float originY, float directionX, float directionY)
        {
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Team == attacker.Team || player.Id == attacker.Id) { continue; }
                var distance = GetRayIntersectionDistanceWithPlayer(originX, originY, directionX, directionY, player, hitState.NearestDistance);
                if (distance.HasValue) { UpdateNearestRiflePlayerHit(ref hitState, distance.Value, player); }
            }
        }

        private static void UpdateNearestRifleObstacleHit(ref RifleHitState hitState, float distance)
        {
            hitState.NearestDistance = distance;
            hitState.HitPlayer = null;
            hitState.HitSentry = null;
            hitState.HitGenerator = null;
        }

        private static void UpdateNearestRifleSentryHit(ref RifleHitState hitState, float distance, SentryEntity sentry)
        {
            hitState.NearestDistance = distance;
            hitState.HitPlayer = null;
            hitState.HitSentry = sentry;
            hitState.HitGenerator = null;
        }

        private static void UpdateNearestRifleGeneratorHit(ref RifleHitState hitState, float distance, GeneratorState generator)
        {
            hitState.NearestDistance = distance;
            hitState.HitPlayer = null;
            hitState.HitSentry = null;
            hitState.HitGenerator = generator;
        }

        private static void UpdateNearestRiflePlayerHit(ref RifleHitState hitState, float distance, PlayerEntity player)
        {
            hitState.NearestDistance = distance;
            hitState.HitPlayer = player;
            hitState.HitSentry = null;
            hitState.HitGenerator = null;
        }
    }
}
