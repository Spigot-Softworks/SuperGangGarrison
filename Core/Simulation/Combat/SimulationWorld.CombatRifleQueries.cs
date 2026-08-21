namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class CombatResolver
    {
        private const int MaximumOrderedRiflePlayerHits = 64;
        private const float FriendlyOverlapContactEpsilon = 8f;

        private struct RifleHitState
        {
            public RifleHitState(float nearestDistance)
            {
                NearestDistance = nearestDistance;
                HitPlayer = null;
                HitSentry = null;
                HitGenerator = null;
                HitJumpPad = null;
                HasTerminalContact = false;
            }

            public float NearestDistance;
            public PlayerEntity? HitPlayer;
            public SentryEntity? HitSentry;
            public GeneratorState? HitGenerator;
            public JumpPadEntity? HitJumpPad;
            public bool HasTerminalContact;
        }

        public RifleHitResult ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance)
            => ResolveRifleHit(attacker, attacker.X, attacker.Y, directionX, directionY, maxDistance);

        public bool IsFriendlyPlayerFirstRifleContact(
            PlayerEntity attacker,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float maxDistance)
        {
            var result = ResolveOrderedRifleHits(
                attacker,
                originX,
                originY,
                directionX,
                directionY,
                maxDistance,
                new RifleTracePolicy(
                    IgnoreOrdinaryGeometry: false,
                    AllowFriendlySupport: true,
                    MaximumEnemyPlayerHits: 1));
            return result.PlayerHits.Count > 0 && result.PlayerHits[0].IsFriendlySupport;
        }

        public RifleHitResult ResolveRifleHit(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance)
        {
            var orderedResult = ResolveOrderedRifleHits(
                attacker,
                originX,
                originY,
                directionX,
                directionY,
                maxDistance,
                new RifleTracePolicy(
                    IgnoreOrdinaryGeometry: false,
                    AllowFriendlySupport: false,
                    MaximumEnemyPlayerHits: 1));
            if (orderedResult.PlayerHits.Count > 0)
            {
                var playerHit = orderedResult.PlayerHits[0];
                return new RifleHitResult(
                    playerHit.Distance,
                    playerHit.Player,
                    HitSentry: null,
                    HitGenerator: null);
            }

            return new RifleHitResult(
                orderedResult.Distance,
                HitPlayer: null,
                HitSentry: orderedResult.HitSentry,
                HitGenerator: orderedResult.HitGenerator)
            {
                HitJumpPad = orderedResult.HitJumpPad,
            };
        }

        public OrderedRifleHitResult ResolveOrderedRifleHits(
            PlayerEntity attacker,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float maxDistance,
            RifleTracePolicy policy)
        {
            var hitState = new RifleHitState(maxDistance);

            UpdateNearestRifleHitFromWorldBounds(
                ref hitState,
                originX,
                originY,
                directionX,
                directionY);
            if (!policy.IgnoreOrdinaryGeometry)
            {
                UpdateNearestRifleHitFromSolids(ref hitState, originX, originY, directionX, directionY);
            }
            UpdateNearestRifleHitFromRoomObjects(
                ref hitState,
                attacker,
                originX,
                originY,
                directionX,
                directionY,
                policy.IgnoreOrdinaryGeometry);
            UpdateNearestRifleHitFromGenerators(ref hitState, attacker, originX, originY, directionX, directionY);
            UpdateNearestRifleHitFromSentries(ref hitState, attacker, originX, originY, directionX, directionY);
            UpdateNearestRifleHitFromJumpPads(ref hitState, attacker, originX, originY, directionX, directionY);

            var playerHits = ResolveOrderedRiflePlayerHits(
                ref hitState,
                attacker,
                originX,
                originY,
                directionX,
                directionY,
                policy);
            return new OrderedRifleHitResult(
                hitState.NearestDistance,
                playerHits,
                hitState.HitSentry,
                hitState.HitGenerator)
            {
                HitJumpPad = hitState.HitJumpPad,
            };
        }

        private void UpdateNearestRifleHitFromWorldBounds(
            ref RifleHitState hitState,
            float originX,
            float originY,
            float directionX,
            float directionY)
        {
            var boundaryDistance = hitState.NearestDistance;
            if (directionX > 0.0001f)
            {
                boundaryDistance = Math.Min(
                    boundaryDistance,
                    (Level.Bounds.Width - originX) / directionX);
            }
            else if (directionX < -0.0001f)
            {
                boundaryDistance = Math.Min(boundaryDistance, -originX / directionX);
            }

            if (directionY > 0.0001f)
            {
                boundaryDistance = Math.Min(
                    boundaryDistance,
                    (Level.Bounds.Height - originY) / directionY);
            }
            else if (directionY < -0.0001f)
            {
                boundaryDistance = Math.Min(boundaryDistance, -originY / directionY);
            }

            if (boundaryDistance >= 0f
                && boundaryDistance < hitState.NearestDistance)
            {
                UpdateNearestRifleObstacleHit(ref hitState, boundaryDistance);
            }
        }

        private void UpdateNearestRifleHitFromSolids(ref RifleHitState hitState, float originX, float originY, float directionX, float directionY)
        {
            var rayBounds = GetRayBounds(originX, originY, directionX, directionY, hitState.NearestDistance);
            foreach (var solid in GetPotentialSolidRaycastCandidates(rayBounds))
            {
                if (!RayBoundsMayIntersectRectangle(rayBounds, solid.Left, solid.Top, solid.Right, solid.Bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, hitState.NearestDistance);
                if (distance.HasValue) { UpdateNearestRifleObstacleHit(ref hitState, distance.Value); }
            }
        }

        private void UpdateNearestRifleHitFromRoomObjects(
            ref RifleHitState hitState,
            PlayerEntity attacker,
            float originX,
            float originY,
            float directionX,
            float directionY,
            bool ignoreOrdinaryGeometry)
        {
            var rayBounds = GetRayBounds(originX, originY, directionX, directionY, hitState.NearestDistance);
            foreach (var indexedRoomObject in GetPotentialRoomObjectRaycastCandidates(rayBounds))
            {
                if (!Level.IsRoomObjectActive(indexedRoomObject.Index))
                {
                    continue;
                }

                var roomObject = indexedRoomObject.Marker;
                if (!RayBoundsMayIntersectRectangle(rayBounds, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom))
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom, hitState.NearestDistance);
                if (!distance.HasValue)
                {
                    continue;
                }

                if (ignoreOrdinaryGeometry
                    && roomObject.Type is RoomObjectType.BulletWall or RoomObjectType.DirectionalWall)
                {
                    continue;
                }

                if (roomObject.Type == RoomObjectType.Barrier)
                {
                    var hitX = originX + (directionX * distance.Value);
                    var hitY = originY + (directionY * distance.Value);
                    if (!BarrierCollision.BlocksHitscan(
                            roomObject.Barrier,
                            attacker.Team,
                            attacker.IsCarryingIntel,
                            roomObject,
                            originX,
                            originY,
                            hitX,
                            hitY))
                    {
                        continue;
                    }
                }
                else if (roomObject.Type == RoomObjectType.DirectionalWall)
                {
                    var hitX = originX + (directionX * distance.Value);
                    var hitY = originY + (directionY * distance.Value);
                    if (!DirectionalWallCollision.BlocksHitscan(
                            roomObject.DirectionalWall,
                            attacker.Team,
                            attacker.IsCarryingIntel,
                            roomObject,
                            originX,
                            originY,
                            hitX,
                            hitY))
                    {
                        continue;
                    }
                }
                else if (!IsBlockingHitscanRoomObject(roomObject, attacker.Team, attacker.IsCarryingIntel))
                {
                    continue;
                }

                UpdateNearestRifleObstacleHit(ref hitState, distance.Value);
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

        private IReadOnlyList<OrderedRiflePlayerHit> ResolveOrderedRiflePlayerHits(
            ref RifleHitState hitState,
            PlayerEntity attacker,
            float originX,
            float originY,
            float directionX,
            float directionY,
            RifleTracePolicy policy)
        {
            var candidates = new List<OrderedRiflePlayerHit>();
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Id == attacker.Id) { continue; }
                var bodyDistance = GetRayIntersectionDistanceWithPlayer(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    _world,
                    player,
                    hitState.NearestDistance);
                var headDistance = policy.DetectLastToDieHeadshots
                    ? GetRayIntersectionDistanceWithLastToDieDecapitatorHeadZone(
                        originX,
                        originY,
                        directionX,
                        directionY,
                        _world,
                        player,
                        hitState.NearestDistance)
                    : null;
                var isHeadshot = headDistance.HasValue
                    && (!bodyDistance.HasValue || headDistance.Value <= bodyDistance.Value);
                var distance = isHeadshot ? headDistance : bodyDistance;
                if (!distance.HasValue
                    || distance.Value > hitState.NearestDistance
                    || (hitState.HasTerminalContact
                        && distance.Value == hitState.NearestDistance))
                {
                    continue;
                }

                candidates.Add(new OrderedRiflePlayerHit(
                    distance.Value,
                    player,
                    IsFriendlySupport: player.Team == attacker.Team,
                    IsLastToDieHeadshot: isHeadshot));
            }

            candidates.Sort(static (left, right) =>
            {
                var distanceOrder = left.Distance.CompareTo(right.Distance);
                return distanceOrder != 0
                    ? distanceOrder
                    : left.Player.Id.CompareTo(right.Player.Id);
            });

            // Allocation order must not decide whether an overlapping friendly
            // body blocks an ordinary rifle trace.
            if (!policy.AllowFriendlySupport)
            {
                var firstEnemyDistance = candidates
                    .Where(static candidate => !candidate.IsFriendlySupport)
                    .Select(static candidate => candidate.Distance)
                    .DefaultIfEmpty(float.PositiveInfinity)
                    .Min();
                var firstFriendlyDistance = candidates
                    .Where(static candidate => candidate.IsFriendlySupport)
                    .Select(static candidate => candidate.Distance)
                    .DefaultIfEmpty(float.PositiveInfinity)
                    .Min();
                if (candidates.Count > 0
                    && firstFriendlyDistance <= firstEnemyDistance + FriendlyOverlapContactEpsilon)
                {
                    UpdateNearestRifleObstacleHit(ref hitState, firstFriendlyDistance);
                    return [];
                }
            }

            var maximumEnemyHits = Math.Clamp(
                policy.MaximumEnemyPlayerHits,
                1,
                MaximumOrderedRiflePlayerHits);
            var orderedHits = new List<OrderedRiflePlayerHit>(
                Math.Min(candidates.Count, maximumEnemyHits));
            var enemyHitCount = 0;
            foreach (var candidate in candidates)
            {
                if (candidate.IsFriendlySupport)
                {
                    if (policy.AllowFriendlySupport)
                    {
                        orderedHits.Add(candidate);
                    }

                    UpdateNearestRifleObstacleHit(ref hitState, candidate.Distance);
                    break;
                }

                orderedHits.Add(candidate);
                enemyHitCount += 1;
                if (enemyHitCount >= maximumEnemyHits)
                {
                    UpdateNearestRifleObstacleHit(ref hitState, candidate.Distance);
                    break;
                }
            }

            return orderedHits;
        }

        private static void UpdateNearestRifleObstacleHit(ref RifleHitState hitState, float distance)
        {
            hitState.NearestDistance = distance;
            hitState.HitPlayer = null;
            hitState.HitSentry = null;
            hitState.HitGenerator = null;
            hitState.HitJumpPad = null;
            hitState.HasTerminalContact = true;
        }

        private static void UpdateNearestRifleSentryHit(ref RifleHitState hitState, float distance, SentryEntity sentry)
        {
            hitState.NearestDistance = distance;
            hitState.HitPlayer = null;
            hitState.HitSentry = sentry;
            hitState.HitGenerator = null;
            hitState.HitJumpPad = null;
            hitState.HasTerminalContact = true;
        }

        private static void UpdateNearestRifleGeneratorHit(ref RifleHitState hitState, float distance, GeneratorState generator)
        {
            hitState.NearestDistance = distance;
            hitState.HitPlayer = null;
            hitState.HitSentry = null;
            hitState.HitGenerator = generator;
            hitState.HitJumpPad = null;
            hitState.HasTerminalContact = true;
        }

        private void UpdateNearestRifleHitFromJumpPads(ref RifleHitState hitState, PlayerEntity attacker, float originX, float originY, float directionX, float directionY)
        {
            foreach (var pad in _jumpPads)
            {
                if (pad.IsNeutral || pad.Team == attacker.Team || !pad.IsBuilt || pad.IsDead) { continue; }
                var distance = GetRayIntersectionDistanceWithJumpPad(originX, originY, directionX, directionY, pad, hitState.NearestDistance);
                if (distance.HasValue)
                {
                    hitState.NearestDistance = distance.Value;
                    hitState.HitPlayer = null;
                    hitState.HitSentry = null;
                    hitState.HitGenerator = null;
                    hitState.HitJumpPad = pad;
                    hitState.HasTerminalContact = true;
                }
            }
        }
    }
}
