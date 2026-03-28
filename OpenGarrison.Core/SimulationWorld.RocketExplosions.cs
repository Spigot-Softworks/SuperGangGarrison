namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void ExplodeRocket(RocketProjectileEntity rocket, PlayerEntity? directHitPlayer, SentryEntity? directHitSentry, GeneratorState? directHitGenerator)
    {
        RocketExplosionSystem.Explode(this, rocket, directHitPlayer, directHitSentry, directHitGenerator);
    }

    private static class RocketExplosionSystem
    {
        public static void Explode(
            SimulationWorld world,
            RocketProjectileEntity rocket,
            PlayerEntity? directHitPlayer,
            SentryEntity? directHitSentry,
            GeneratorState? directHitGenerator)
        {
            var owner = world.FindPlayerById(rocket.OwnerId);
            RemoveAt(world, rocket.Id);
            ApplyDirectHitDamage(world, rocket, owner, directHitPlayer, directHitSentry, directHitGenerator);

            world.RegisterWorldSoundEvent("ExplosionSnd", rocket.X, rocket.Y);
            world.RegisterVisualEffect("Explosion", rocket.X, rocket.Y);
            world.ApplyDeadBodyExplosionImpulse(rocket.X, rocket.Y, RocketProjectileEntity.BlastRadius, 10f);
            world.ApplyPlayerGibExplosionImpulse(rocket.X, rocket.Y, RocketProjectileEntity.BlastRadius, 15f);
            world.RegisterExplosionTraces(rocket.X, rocket.Y);

            ApplySplashDamageToPlayers(world, rocket, owner);
            ApplySplashDamageToSentries(world, rocket, owner);
            ApplySplashDamageToGenerators(world, rocket, owner);
            TriggerMinesInBlast(world, rocket);
            DestroyBubblesInBlast(world, rocket);
        }

        private static void RemoveAt(SimulationWorld world, int rocketId)
        {
            for (var rocketIndex = world._rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                if (world._rockets[rocketIndex].Id == rocketId)
                {
                    world.RemoveRocketAt(rocketIndex);
                    break;
                }
            }
        }

        private static void ApplyDirectHitDamage(
            SimulationWorld world,
            RocketProjectileEntity rocket,
            PlayerEntity? owner,
            PlayerEntity? directHitPlayer,
            SentryEntity? directHitSentry,
            GeneratorState? directHitGenerator)
        {
            if (directHitPlayer is not null && !ReferenceEquals(directHitPlayer, owner))
            {
                if (world.ApplyPlayerDamage(directHitPlayer, RocketProjectileEntity.DirectHitDamage, owner, PlayerEntity.SpyDamageRevealAlpha))
                {
                    world.KillPlayer(directHitPlayer, gibbed: true, killer: owner, weaponSpriteName: "RocketKL");
                }
            }

            if (directHitSentry is not null)
            {
                if (world.ApplySentryDamage(directHitSentry, RocketProjectileEntity.DirectHitDamage, owner))
                {
                    world.DestroySentry(directHitSentry);
                }
            }

            if (directHitGenerator is not null)
            {
                world.TryDamageGenerator(directHitGenerator.Team, RocketProjectileEntity.DirectHitDamage, owner);
            }
        }

        private static void ApplySplashDamageToPlayers(SimulationWorld world, RocketProjectileEntity rocket, PlayerEntity? owner)
        {
            foreach (var player in world.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive)
                {
                    continue;
                }

                var distance = SimulationWorld.DistanceBetween(rocket.X, rocket.Y, player.X, player.Y);
                if (distance >= RocketProjectileEntity.BlastRadius)
                {
                    continue;
                }

                if (world.ShouldIgnoreFriendlyGroundedBlast(player, rocket.Team, rocket.OwnerId))
                {
                    continue;
                }

                var distanceFactor = 1f - (distance / RocketProjectileEntity.BlastRadius);
                if (distanceFactor <= RocketProjectileEntity.SplashThresholdFactor)
                {
                    continue;
                }

                ApplyPlayerImpulse(world, player, rocket, distanceFactor);
                ApplyMovementState(player, rocket);
                var receivedBlastLiftBonus = player.Id != rocket.OwnerId && ShouldApplyBlastLiftBonus(player, rocket.X, rocket.Y);
                if (receivedBlastLiftBonus)
                {
                    player.AddImpulse(0f, -4f * distanceFactor * LegacyMovementModel.SourceTicksPerSecond);
                }

                ApplySpeedAdjustments(player, rocket, receivedBlastLiftBonus);

                if (player.Team == rocket.Team && player.Id != rocket.OwnerId)
                {
                    continue;
                }

                var appliedDamage = RocketProjectileEntity.ExplosionDamage * distanceFactor;
                world.RegisterBloodEffect(player.X, player.Y, SimulationWorld.PointDirectionDegrees(rocket.X, rocket.Y, player.X, player.Y) - 180f, 3);
                if (world.ApplyPlayerContinuousDamage(player, appliedDamage, owner, PlayerEntity.SpyDamageRevealAlpha))
                {
                    world.KillPlayer(player, gibbed: true, killer: owner, weaponSpriteName: "RocketKL");
                }
            }
        }

        private static void ApplyPlayerImpulse(SimulationWorld world, PlayerEntity player, RocketProjectileEntity rocket, float distanceFactor)
        {
            var impulse = SimulationWorld.GetExplosionImpulseMagnitude(
                player,
                rocket.X,
                rocket.Y,
                rocket.CurrentKnockback,
                distanceFactor,
                useMineVectorProfile: false);
            SimulationWorld.ApplyExplosionImpulse(player, rocket.X, rocket.Y, impulse);
        }

        private static void ApplyMovementState(PlayerEntity player, RocketProjectileEntity rocket)
        {
            if (player.Id == rocket.OwnerId && player.Team == rocket.Team)
            {
                player.SetMovementState(LegacyMovementState.ExplosionRecovery);
                return;
            }

            player.SetMovementState(player.Team == rocket.Team
                ? LegacyMovementState.FriendlyJuggle
                : LegacyMovementState.RocketJuggle);
        }

        private static void ApplySpeedAdjustments(PlayerEntity player, RocketProjectileEntity rocket, bool receivedBlastLiftBonus)
        {
            if (player.Id == rocket.OwnerId && player.Team == rocket.Team)
            {
                player.ScaleVelocity(player.IsUbered ? 1.055f : 1.06f);
                return;
            }

            if (receivedBlastLiftBonus)
            {
                player.ScaleVelocity(1.3f);
            }
        }

        private static bool ShouldApplyBlastLiftBonus(PlayerEntity player, float originX, float originY)
        {
            var offsetAngle = ToGameMakerDegrees(SimulationWorld.PointDirectionDegrees(player.X, player.Y + 5f, originX, originY - 5f));
            var baseAngle = ToGameMakerDegrees(SimulationWorld.PointDirectionDegrees(player.X, player.Y, originX, originY));
            return offsetAngle > 210f && baseAngle < 330f;
        }

        private static float ToGameMakerDegrees(float worldDegrees)
        {
            return SimulationWorld.NormalizeAngleDegrees(360f - worldDegrees);
        }

        private static void ApplySplashDamageToSentries(SimulationWorld world, RocketProjectileEntity rocket, PlayerEntity? owner)
        {
            for (var sentryIndex = world._sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
            {
                var sentry = world._sentries[sentryIndex];
                var distance = SimulationWorld.DistanceBetween(rocket.X, rocket.Y, sentry.X, sentry.Y);
                if (distance >= RocketProjectileEntity.BlastRadius || sentry.Team == rocket.Team)
                {
                    continue;
                }

                var damage = RocketProjectileEntity.ExplosionDamage * (1f - (distance / RocketProjectileEntity.BlastRadius));
                if (world.ApplySentryDamage(sentry, (int)MathF.Ceiling(damage), owner))
                {
                    world.DestroySentry(sentry);
                }
            }
        }

        private static void ApplySplashDamageToGenerators(SimulationWorld world, RocketProjectileEntity rocket, PlayerEntity? owner)
        {
            for (var generatorIndex = 0; generatorIndex < world._generators.Count; generatorIndex += 1)
            {
                var generator = world._generators[generatorIndex];
                var distance = SimulationWorld.DistanceBetween(rocket.X, rocket.Y, generator.Marker.CenterX, generator.Marker.CenterY);
                if (distance >= RocketProjectileEntity.BlastRadius || generator.Team == rocket.Team || generator.IsDestroyed)
                {
                    continue;
                }

                var damage = RocketProjectileEntity.ExplosionDamage * (1f - (distance / RocketProjectileEntity.BlastRadius));
                world.TryDamageGenerator(generator.Team, damage, owner);
            }
        }

        private static void TriggerMinesInBlast(SimulationWorld world, RocketProjectileEntity rocket)
        {
            var queuedMineIds = new List<int>();
            foreach (var mine in world._mines)
            {
                if ((mine.Team == rocket.Team && mine.OwnerId != rocket.OwnerId)
                    || SimulationWorld.DistanceBetween(rocket.X, rocket.Y, mine.X, mine.Y) >= MineProjectileEntity.BlastRadius * 0.66f)
                {
                    continue;
                }

                queuedMineIds.Add(mine.Id);
            }

            for (var index = 0; index < queuedMineIds.Count; index += 1)
            {
                var mine = world.FindMineById(queuedMineIds[index]);
                if (mine is not null)
                {
                    world.ExplodeMine(mine);
                }
            }
        }

        private static void DestroyBubblesInBlast(SimulationWorld world, RocketProjectileEntity rocket)
        {
            for (var bubbleIndex = world._bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
            {
                if (SimulationWorld.DistanceBetween(rocket.X, rocket.Y, world._bubbles[bubbleIndex].X, world._bubbles[bubbleIndex].Y) < RocketProjectileEntity.BlastRadius * 0.66f)
                {
                    world.RemoveBubbleAt(bubbleIndex);
                }
            }
        }
    }
}
