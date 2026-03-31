namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvanceRockets()
    {
        RocketProjectileSystem.Advance(this);
    }

    private void RemoveRocketAt(int rocketIndex)
    {
        var rocket = _rockets[rocketIndex];
        _entities.Remove(rocket.Id);
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
                if (hitResult.HitPlayer is null && hitResult.HitSentry is null && hitResult.HitGenerator is null)
                {
                    // The legacy GameMaker rocket uses a collision mask, so it explodes a few pixels
                    // before the projectile origin would mathematically touch the wall.
                    var backoffDistance = MathF.Min(hitResult.Distance, RocketProjectileEntity.EnvironmentCollisionBackoffDistance);
                    hitX -= directionX * backoffDistance;
                    hitY -= directionY * backoffDistance;
                }

                rocket.MoveTo(hitX, hitY);
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
