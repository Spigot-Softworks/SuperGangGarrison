namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public bool DebugHasLastRocketCollision { get; private set; }
    public float DebugLastRocketCollisionX { get; private set; }
    public float DebugLastRocketCollisionY { get; private set; }
    public string DebugLastRocketCollisionObjectName { get; private set; } = string.Empty;
    public string DebugLastRocketCollisionReason { get; private set; } = string.Empty;

    public bool DebugHasProjectileSpawnBlocked { get; private set; }
    public float DebugProjectileSpawnBlockedX { get; private set; }
    public float DebugProjectileSpawnBlockedY { get; private set; }
    public float DebugProjectileSpawnBlockedWidth { get; private set; }
    public float DebugProjectileSpawnBlockedHeight { get; private set; }
    public string DebugProjectileSpawnBlockedObjectName { get; private set; } = string.Empty;

    private void SetLastRocketCollisionDebug(float x, float y, string objectName, string reason)
    {
        DebugHasLastRocketCollision = true;
        DebugLastRocketCollisionX = x;
        DebugLastRocketCollisionY = y;
        DebugLastRocketCollisionObjectName = objectName;
        DebugLastRocketCollisionReason = reason;
    }

    private void SetProjectileSpawnBlockedDebug(float x, float y, float width, float height, string objectName)
    {
        DebugHasProjectileSpawnBlocked = true;
        DebugProjectileSpawnBlockedX = x;
        DebugProjectileSpawnBlockedY = y;
        DebugProjectileSpawnBlockedWidth = width;
        DebugProjectileSpawnBlockedHeight = height;
        DebugProjectileSpawnBlockedObjectName = objectName;
    }

    private string ResolveRocketEnvironmentCollisionName(float hitX, float hitY)
    {
        const float epsilon = 0.25f;
        for (var index = 0; index < Level.RoomObjects.Count; index += 1)
        {
            var roomObject = Level.RoomObjects[index];
            var blocksProjectiles = roomObject.Type switch
            {
                RoomObjectType.TeamGate => true,
                RoomObjectType.ControlPointSetupGate => Level.ControlPointSetupGatesActive,
                RoomObjectType.BulletWall => true,
                _ => false,
            };
            if (!blocksProjectiles)
            {
                continue;
            }

            if (hitX >= roomObject.Left - epsilon
                && hitX <= roomObject.Right + epsilon
                && hitY >= roomObject.Top - epsilon
                && hitY <= roomObject.Bottom + epsilon)
            {
                return $"RoomObject:{roomObject.Type}";
            }
        }

        for (var index = 0; index < Level.Solids.Count; index += 1)
        {
            var solid = Level.Solids[index];
            if (hitX >= solid.Left - epsilon
                && hitX <= solid.Right + epsilon
                && hitY >= solid.Top - epsilon
                && hitY <= solid.Bottom + epsilon)
            {
                return "LevelSolid";
            }
        }

        return "Environment";
    }

    private void AdvanceRockets()
    {
        RocketProjectileSystem.Advance(this);
    }

    private void RemoveRocketAt(int rocketIndex)
    {
        var rocket = _rockets[rocketIndex];
        _entities.Remove(rocket.Id);
        MarkProjectileTerminated(rocket.Id);
        _rockets.RemoveAt(rocketIndex);
    }

    private static class RocketProjectileSystem
    {
        public static void Advance(SimulationWorld world)
        {
            var deltaSeconds = (float)world.Config.FixedDeltaSeconds;
            for (var rocketIndex = world._rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                AdvanceRocket(world, rocketIndex, deltaSeconds);
            }
        }

        public static void AdvancePendingForOwner(SimulationWorld world, int ownerId)
        {
            var deltaSeconds = (float)world.Config.FixedDeltaSeconds;
            for (var pendingIndex = world._pendingNewRocketIds.Count - 1; pendingIndex >= 0; pendingIndex -= 1)
            {
                var rocketId = world._pendingNewRocketIds[pendingIndex];
                var rocketIndex = FindRocketIndex(world, rocketId);
                if (rocketIndex < 0)
                {
                    world._pendingNewRocketIds.RemoveAt(pendingIndex);
                    continue;
                }

                if (world._rockets[rocketIndex].OwnerId != ownerId)
                {
                    continue;
                }

                world._pendingNewRocketIds.RemoveAt(pendingIndex);
                AdvanceRocket(world, rocketIndex, deltaSeconds);
            }
        }

        private static void AdvanceRocket(SimulationWorld world, int rocketIndex, float deltaSeconds)
        {
            if (rocketIndex < 0 || rocketIndex >= world._rockets.Count)
            {
                return;
            }

            var rocket = world._rockets[rocketIndex];
            if (world.FindPlayerById(rocket.RangeAnchorOwnerId) is { } rangeAnchorPlayer)
            {
                rocket.RefreshRangeOrigin(rangeAnchorPlayer.X, rangeAnchorPlayer.Y);
                if (rocket.EnableExperimentalStingerTracking
                    && world.ExperimentalGameplaySettings.EnableSoldierStingerRockets
                    && rangeAnchorPlayer.ClassId == PlayerClass.Soldier
                    && world.IsExperimentalPracticePowerOwner(rangeAnchorPlayer))
                {
                    rocket.TrackExperimentalStingerTarget(
                        rangeAnchorPlayer.AimDirectionDegrees * (MathF.PI / 180f),
                        SimulationWorld.GetExperimentalSoldierStingerTurnRateRadians());
                }
            }

            if (rocket.IsFading)
            {
                rocket.AdvanceFade(deltaSeconds);
                if (rocket.IsExpired)
                {
                    world.RemoveRocketAt(rocketIndex);
                    return;
                }
            }
            else
            {
                rocket.TryBeginFadeFromSourceRange();
            }

            if (rocket.ExplodeImmediately)
            {
                var explodeReason = string.IsNullOrWhiteSpace(rocket.DelayedExplosionReason)
                    ? "Unknown"
                    : rocket.DelayedExplosionReason;
                world.SetLastRocketCollisionDebug(rocket.X, rocket.Y, "ExplodeImmediately", explodeReason);
                rocket.ClearDelayedExplosion();
                if (rocket.IsFading)
                {
                    world.RemoveRocketAt(rocketIndex);
                }
                else
                {
                    world.ExplodeRocket(rocket, directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
                }

                return;
            }

            rocket.AdvanceOneTick(deltaSeconds);
            var movementX = rocket.X - rocket.PreviousX;
            var movementY = rocket.Y - rocket.PreviousY;
            var movementDistance = MathF.Sqrt((movementX * movementX) + (movementY * movementY));
            if (movementDistance <= 0.0001f)
            {
                if (rocket.IsExpired)
                {
                    world.RemoveRocketAt(rocketIndex);
                }

                return;
            }

            var directionX = movementX / movementDistance;
            var directionY = movementY / movementDistance;
            var hit = world.GetNearestRocketHit(rocket, directionX, directionY, movementDistance);
            if (hit.HasValue)
            {
                var hitResult = hit.Value;
                var hitX = hitResult.HitX;
                var hitY = hitResult.HitY;
                var collisionObjectName = "Environment";
                if (hitResult.HitPlayer is not null)
                {
                    collisionObjectName = $"Player:{hitResult.HitPlayer.Id}";
                }
                else if (hitResult.HitSentry is not null)
                {
                    collisionObjectName = $"Sentry:{hitResult.HitSentry.Id}";
                }
                else if (hitResult.HitGenerator is not null)
                {
                    collisionObjectName = $"Generator:{hitResult.HitGenerator.Team}";
                }

                if (hitResult.HitPlayer is null && hitResult.HitSentry is null && hitResult.HitGenerator is null)
                {
                    // The legacy GameMaker rocket uses a collision mask, so it explodes a few pixels
                    // before the projectile origin would mathematically touch the wall.
                    var backoffDistance = MathF.Min(hitResult.Distance, RocketProjectileEntity.EnvironmentCollisionBackoffDistance);
                    hitX -= directionX * backoffDistance;
                    hitY -= directionY * backoffDistance;
                    collisionObjectName = world.ResolveRocketEnvironmentCollisionName(hitResult.HitX, hitResult.HitY);
                }

                rocket.MoveTo(hitX, hitY);
                world.SetLastRocketCollisionDebug(hitX, hitY, collisionObjectName, "CollisionHit");
                world.RegisterCombatTrace(rocket.PreviousX, rocket.PreviousY, directionX, directionY, hitResult.Distance, hitResult.HitPlayer is not null);
                if (rocket.IsFading
                    && hitResult.HitPlayer is null
                    && hitResult.HitSentry is null
                    && hitResult.HitGenerator is null)
                {
                    world.RemoveRocketAt(rocketIndex);
                }
                else
                {
                    world.ExplodeRocket(rocket, hitResult.HitPlayer, hitResult.HitSentry, hitResult.HitGenerator);
                }
            }
            else
            {
                RegisterFriendlyPassThroughs(world, rocket, directionX, directionY, movementDistance);
                if (rocket.IsExpired)
                {
                    world.RemoveRocketAt(rocketIndex);
                }
            }
        }

        private static int FindRocketIndex(SimulationWorld world, int rocketId)
        {
            for (var rocketIndex = world._rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                if (world._rockets[rocketIndex].Id == rocketId)
                {
                    return rocketIndex;
                }
            }

            return -1;
        }

        private static void RegisterFriendlyPassThroughs(SimulationWorld world, RocketProjectileEntity rocket, float directionX, float directionY, float maxDistance)
        {
            if (rocket.IsFading || maxDistance <= 0.0001f)
            {
                return;
            }

            var endX = rocket.PreviousX + (directionX * maxDistance);
            var endY = rocket.PreviousY + (directionY * maxDistance);
            List<(int PlayerId, float Distance)>? passThroughs = null;
            foreach (var player in world.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Team != rocket.Team || player.Id == rocket.OwnerId)
                {
                    continue;
                }

                var distance = SimulationWorld.GetLineIntersectionDistanceToPlayer(
                    rocket.PreviousX,
                    rocket.PreviousY,
                    endX,
                    endY,
                    player,
                    maxDistance);
                if (!distance.HasValue)
                {
                    continue;
                }

                passThroughs ??= [];
                passThroughs.Add((player.Id, distance.Value));
            }

            if (passThroughs is null)
            {
                return;
            }

            passThroughs.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
            for (var index = 0; index < passThroughs.Count; index += 1)
            {
                rocket.TryRegisterFriendlyPassThrough(passThroughs[index].PlayerId);
            }
        }
    }
}
