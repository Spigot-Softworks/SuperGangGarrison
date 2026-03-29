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
                        continue;
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

                    continue;
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

                    continue;
                }

                var directionX = movementX / movementDistance;
                var directionY = movementY / movementDistance;
                var hit = world.GetNearestRocketHit(rocket, directionX, directionY, movementDistance);
                if (hit.HasValue)
                {
                    var hitResult = hit.Value;
                    rocket.MoveTo(hitResult.HitX, hitResult.HitY);
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
