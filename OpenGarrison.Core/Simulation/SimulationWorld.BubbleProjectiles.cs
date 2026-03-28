namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private readonly record struct BubbleHitResult(int BubbleIndex, float Distance, float HitX, float HitY);

    private void AdvanceBubbles()
    {
        BubbleProjectileSystem.Advance(this);
    }

    private void RemoveBubbleAt(int bubbleIndex)
    {
        BubbleProjectileSystem.RemoveAt(this, bubbleIndex);
    }

    private bool TryCutBubbleWithBlade(BladeProjectileEntity blade)
    {
        return BubbleProjectileSystem.TryCutWithBlade(this, blade);
    }

    private BubbleHitResult? GetNearestEnemyBubbleHit(float originX, float originY, float directionX, float directionY, float maxDistance, PlayerTeam projectileTeam)
    {
        return BubbleProjectileSystem.GetNearestEnemyHit(this, originX, originY, directionX, directionY, maxDistance, projectileTeam);
    }

    private static class BubbleProjectileSystem
    {
        public static void Advance(SimulationWorld world)
        {
            var deltaSeconds = (float)world.Config.FixedDeltaSeconds;
            for (var bubbleIndex = world._bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
            {
                var bubble = world._bubbles[bubbleIndex];
                var owner = world.FindPlayerById(bubble.OwnerId);
                if (owner is null || !owner.IsAlive)
                {
                    RemoveAt(world, bubbleIndex);
                    continue;
                }

                if (SimulationWorld.DistanceBetween(bubble.X, bubble.Y, owner.X, owner.Y) > BubbleProjectileEntity.MaxDistanceFromOwner)
                {
                    RemoveAt(world, bubbleIndex);
                    continue;
                }

                bubble.AdvanceOneTick(owner.X, owner.Y, owner.HorizontalSpeed, owner.VerticalSpeed, owner.AimDirectionDegrees, deltaSeconds);
                if (bubble.IsExpired || SimulationWorld.DistanceBetween(bubble.X, bubble.Y, owner.X, owner.Y) > BubbleProjectileEntity.MaxDistanceFromOwner)
                {
                    RemoveAt(world, bubbleIndex);
                    continue;
                }

                if (TryResolveEnvironmentCollision(world, bubble) || bubble.IsExpired)
                {
                    RemoveAt(world, bubbleIndex);
                    continue;
                }

                if (TryDamageEnemyPlayer(world, bubble, owner))
                {
                    RemoveAt(world, bubbleIndex);
                    continue;
                }

                ApplySameTeamRepulsion(world, bubble);

                if (TryHandleProjectileCollision(world, bubble) || TryDamageStructureTarget(world, bubble, owner))
                {
                    RemoveAt(world, bubbleIndex);
                    continue;
                }

                if (bubble.IsExpired)
                {
                    RemoveAt(world, bubbleIndex);
                }
            }
        }

        public static void RemoveAt(SimulationWorld world, int bubbleIndex)
        {
            var bubble = world._bubbles[bubbleIndex];
            if (world.FindPlayerById(bubble.OwnerId) is { } owner)
            {
                owner.DecrementQuoteBubbleCount();
            }

            world.RegisterVisualEffect("Pop", bubble.X, bubble.Y);
            world._entities.Remove(bubble.Id);
            world._bubbles.RemoveAt(bubbleIndex);
        }

        public static bool TryCutWithBlade(SimulationWorld world, BladeProjectileEntity blade)
        {
            for (var bubbleIndex = world._bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
            {
                var bubble = world._bubbles[bubbleIndex];
                if (bubble.Team == blade.Team && bubble.OwnerId != blade.OwnerId)
                {
                    continue;
                }

                if (SimulationWorld.DistanceBetween(blade.X, blade.Y, bubble.X, bubble.Y) > 10f)
                {
                    continue;
                }

                RemoveAt(world, bubbleIndex);
                return true;
            }

            return false;
        }

        public static BubbleHitResult? GetNearestEnemyHit(
            SimulationWorld world,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float maxDistance,
            PlayerTeam projectileTeam)
        {
            BubbleHitResult? nearestHit = null;
            for (var bubbleIndex = 0; bubbleIndex < world._bubbles.Count; bubbleIndex += 1)
            {
                var bubble = world._bubbles[bubbleIndex];
                if (bubble.Team == projectileTeam)
                {
                    continue;
                }

                var distance = GetRayIntersectionDistanceWithCircle(originX, originY, directionX, directionY, bubble.X, bubble.Y, BubbleProjectileEntity.Radius, maxDistance);
                if (!distance.HasValue)
                {
                    continue;
                }

                if (nearestHit.HasValue && nearestHit.Value.Distance <= distance.Value)
                {
                    continue;
                }

                nearestHit = new BubbleHitResult(
                    bubbleIndex,
                    distance.Value,
                    originX + directionX * distance.Value,
                    originY + directionY * distance.Value);
            }

            return nearestHit;
        }

        private static bool IsTouchingEnvironment(SimulationWorld world, BubbleProjectileEntity bubble)
        {
            return IsTouchingEnvironmentAt(world, bubble.X, bubble.Y);
        }

        private static bool IsTouchingEnvironmentAt(SimulationWorld world, float x, float y)
        {
            foreach (var solid in world.Level.Solids)
            {
                if (CircleIntersectsRectangle(x, y, BubbleProjectileEntity.Radius, solid.Left, solid.Top, solid.Right, solid.Bottom))
                {
                    return true;
                }
            }

            foreach (var roomObject in world.Level.RoomObjects)
            {
                if (!IsBlockingRoomObject(world, roomObject))
                {
                    continue;
                }

                if (CircleIntersectsRectangle(x, y, BubbleProjectileEntity.Radius, roomObject.Left, roomObject.Top, roomObject.Right, roomObject.Bottom))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveEnvironmentCollision(SimulationWorld world, BubbleProjectileEntity bubble)
        {
            if (!IsTouchingEnvironment(world, bubble))
            {
                return false;
            }

            if (!IsTouchingEnvironmentAt(world, bubble.X, bubble.Y + 6f))
            {
                bubble.MoveTo(bubble.X, bubble.Y + 6f);
                return false;
            }

            if (!IsTouchingEnvironmentAt(world, bubble.X, bubble.Y - 6f))
            {
                bubble.MoveTo(bubble.X, bubble.Y - 6f);
                return false;
            }

            var canKeepHorizontalMove = !IsTouchingEnvironmentAt(world, bubble.X, bubble.PreviousY);
            var canKeepVerticalMove = !IsTouchingEnvironmentAt(world, bubble.PreviousX, bubble.Y);
            if (canKeepHorizontalMove && !canKeepVerticalMove)
            {
                bubble.MoveTo(bubble.X, bubble.PreviousY);
                bubble.SetVelocity(bubble.VelocityX, 0f);
                return false;
            }

            if (canKeepVerticalMove && !canKeepHorizontalMove)
            {
                bubble.MoveTo(bubble.PreviousX, bubble.Y);
                bubble.SetVelocity(0f, bubble.VelocityY);
                return false;
            }

            bubble.Destroy();
            return true;
        }

        private static bool IsBlockingRoomObject(SimulationWorld world, RoomObjectMarker roomObject)
        {
            return roomObject.Type switch
            {
                RoomObjectType.TeamGate => true,
                RoomObjectType.ControlPointSetupGate => world.Level.ControlPointSetupGatesActive,
                RoomObjectType.BulletWall => true,
                _ => false,
            };
        }

        private static bool TryDamageEnemyPlayer(SimulationWorld world, BubbleProjectileEntity bubble, PlayerEntity owner)
        {
            foreach (var player in world.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.Team == bubble.Team || player.Id == bubble.OwnerId || !CircleIntersectsPlayer(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, player))
                {
                    continue;
                }

                if (world.ApplyPlayerContinuousDamage(player, BubbleProjectileEntity.DamagePerHit, owner))
                {
                    world.KillPlayer(player, killer: owner, weaponSpriteName: "BladeKL");
                }

                return true;
            }

            return false;
        }

        private static void ApplySameTeamRepulsion(SimulationWorld world, BubbleProjectileEntity bubble)
        {
            for (var bubbleIndex = 0; bubbleIndex < world._bubbles.Count; bubbleIndex += 1)
            {
                var otherBubble = world._bubbles[bubbleIndex];
                if (otherBubble.Id == bubble.Id || otherBubble.Team != bubble.Team)
                {
                    continue;
                }

                if (SimulationWorld.DistanceBetween(bubble.X, bubble.Y, otherBubble.X, otherBubble.Y) > BubbleProjectileEntity.Radius * 2f)
                {
                    continue;
                }

                bubble.AddRepulsionFrom(otherBubble.X, otherBubble.Y);
            }
        }

        private static bool TryDamageStructureTarget(SimulationWorld world, BubbleProjectileEntity bubble, PlayerEntity owner)
        {
            foreach (var sentry in world._sentries)
            {
                if (sentry.Team == bubble.Team || !CircleIntersectsRectangle(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, sentry.X - 12f, sentry.Y - 12f, sentry.X + 12f, sentry.Y + 12f))
                {
                    continue;
                }

                if (world.ApplySentryDamage(sentry, (int)MathF.Ceiling(BubbleProjectileEntity.DamagePerHit), owner))
                {
                    world.DestroySentry(sentry);
                }

                return true;
            }

            for (var index = 0; index < world._generators.Count; index += 1)
            {
                var generator = world._generators[index];
                if (generator.Team == bubble.Team
                    || generator.IsDestroyed
                    || !CircleIntersectsRectangle(bubble.X, bubble.Y, BubbleProjectileEntity.Radius, generator.Marker.Left, generator.Marker.Top, generator.Marker.Right, generator.Marker.Bottom))
                {
                    continue;
                }

                world.TryDamageGenerator(generator.Team, BubbleProjectileEntity.DamagePerHit, owner);
                return true;
            }

            return false;
        }

        private static bool TryHandleProjectileCollision(SimulationWorld world, BubbleProjectileEntity bubble)
        {
            for (var rocketIndex = world._rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                var rocket = world._rockets[rocketIndex];
                if (rocket.Team != bubble.Team && SimulationWorld.DistanceBetween(bubble.X, bubble.Y, rocket.X, rocket.Y) <= 10f)
                {
                    return true;
                }
            }

            for (var mineIndex = world._mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
            {
                var mine = world._mines[mineIndex];
                if (mine.Team != bubble.Team && SimulationWorld.DistanceBetween(bubble.X, bubble.Y, mine.X, mine.Y) <= 10f)
                {
                    if (!mine.IsStickied)
                    {
                        mine.SetVelocity(-mine.VelocityX, -mine.VelocityY);
                    }

                    bubble.HalveLifetimeOrDestroy(world.GetSimulationTicksFromSourceTicks(10f));
                    return bubble.IsExpired;
                }
            }

            for (var flameIndex = world._flames.Count - 1; flameIndex >= 0; flameIndex -= 1)
            {
                var flame = world._flames[flameIndex];
                if (flame.Team != bubble.Team && SimulationWorld.DistanceBetween(bubble.X, bubble.Y, flame.X, flame.Y) <= 8f)
                {
                    return true;
                }
            }

            for (var flareIndex = world._flares.Count - 1; flareIndex >= 0; flareIndex -= 1)
            {
                var flare = world._flares[flareIndex];
                if (flare.Team != bubble.Team && SimulationWorld.DistanceBetween(bubble.X, bubble.Y, flare.X, flare.Y) <= 8f)
                {
                    world.RemoveFlareAt(flareIndex);
                    return true;
                }
            }

            for (var shotIndex = world._shots.Count - 1; shotIndex >= 0; shotIndex -= 1)
            {
                if (world._shots[shotIndex].Team != bubble.Team && SimulationWorld.DistanceBetween(bubble.X, bubble.Y, world._shots[shotIndex].X, world._shots[shotIndex].Y) <= 8f)
                {
                    return true;
                }
            }

            for (var needleIndex = world._needles.Count - 1; needleIndex >= 0; needleIndex -= 1)
            {
                if (world._needles[needleIndex].Team != bubble.Team && SimulationWorld.DistanceBetween(bubble.X, bubble.Y, world._needles[needleIndex].X, world._needles[needleIndex].Y) <= 8f)
                {
                    return true;
                }
            }

            for (var revolverIndex = world._revolverShots.Count - 1; revolverIndex >= 0; revolverIndex -= 1)
            {
                if (world._revolverShots[revolverIndex].Team != bubble.Team && SimulationWorld.DistanceBetween(bubble.X, bubble.Y, world._revolverShots[revolverIndex].X, world._revolverShots[revolverIndex].Y) <= 8f)
                {
                    return true;
                }
            }

            for (var bladeIndex = world._blades.Count - 1; bladeIndex >= 0; bladeIndex -= 1)
            {
                var blade = world._blades[bladeIndex];
                if ((blade.Team != bubble.Team || blade.OwnerId == bubble.OwnerId)
                    && SimulationWorld.DistanceBetween(bubble.X, bubble.Y, blade.X, blade.Y) <= 10f)
                {
                    return true;
                }
            }

            for (var bubbleIndex = 0; bubbleIndex < world._bubbles.Count; bubbleIndex += 1)
            {
                var otherBubble = world._bubbles[bubbleIndex];
                if (otherBubble.Id == bubble.Id || otherBubble.Team == bubble.Team)
                {
                    continue;
                }

                if (SimulationWorld.DistanceBetween(bubble.X, bubble.Y, otherBubble.X, otherBubble.Y) <= BubbleProjectileEntity.Radius * 2f)
                {
                    return true;
                }
            }

            return false;
        }

        private static float? GetRayIntersectionDistanceWithCircle(float originX, float originY, float directionX, float directionY, float centerX, float centerY, float radius, float maxDistance)
        {
            var offsetX = originX - centerX;
            var offsetY = originY - centerY;
            var b = 2f * ((directionX * offsetX) + (directionY * offsetY));
            var c = (offsetX * offsetX) + (offsetY * offsetY) - (radius * radius);
            var discriminant = (b * b) - 4f * c;
            if (discriminant < 0f)
            {
                return null;
            }

            var sqrtDiscriminant = MathF.Sqrt(discriminant);
            var candidateA = (-b - sqrtDiscriminant) * 0.5f;
            if (candidateA >= 0f && candidateA <= maxDistance)
            {
                return candidateA;
            }

            var candidateB = (-b + sqrtDiscriminant) * 0.5f;
            return candidateB >= 0f && candidateB <= maxDistance
                ? candidateB
                : null;
        }
    }
}
