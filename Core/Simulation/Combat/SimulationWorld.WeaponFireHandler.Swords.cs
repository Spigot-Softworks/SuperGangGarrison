namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        public void FireQuoteBlade(PlayerEntity attacker, float aimWorldX, float aimWorldY, int lifetimeTicks = PlayerEntity.QuoteBladeLifetimeTicks)
        {
            RegisterSoundEvent(attacker, "BladeSnd");
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var directionRadians = MathF.Atan2(aimDeltaY, aimDeltaX);
            var directionX = MathF.Cos(directionRadians);
            var directionY = MathF.Sin(directionRadians);
            var bladePower = attacker.CurrentShells;
            var bonusDamage = (int)MathF.Floor((15f / 100f) * bladePower + 3f);
            var hitDamage = 3 + bonusDamage;
            var inheritedVelocityX = attacker.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
            var inheritedVelocityY = attacker.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond;
            var (launchedVelocityX, launchedVelocityY) = _world.ApplyExperimentalProjectileSpeedMultiplier(
                attacker,
                directionX * 12f,
                directionY * 12f);
            SpawnBlade(
                attacker,
                weaponOrigin.BaseX + directionX * 5f,
                weaponOrigin.BaseY + directionY * 5f,
                launchedVelocityX + inheritedVelocityX,
                launchedVelocityY + inheritedVelocityY,
                hitDamage,
                lifetimeTicks);
        }

        public void StartExperimentalDemoknightSwordSwing(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            RegisterSoundEvent(attacker, ExperimentalDemoknightCatalog.EyelanderSwingSoundName);
            var swingTicks = ResolveExperimentalDemoknightSwordSwingTicks(attacker);
            attacker.BeginExperimentalDemoknightSwordSwing(swingTicks);
            AdvanceExperimentalDemoknightSwordSwing(attacker, aimWorldX, aimWorldY);
        }

        public void AdvanceExperimentalDemoknightSwordSwing(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            if (!attacker.IsExperimentalDemoknightSwordSwingActive)
            {
                return;
            }

            ProcessExperimentalDemoknightSwordSwingTick(attacker, aimWorldX, aimWorldY);
            attacker.AdvanceExperimentalDemoknightSwordSwingTimer();
        }

        private void ProcessExperimentalDemoknightSwordSwingTick(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var geometryScale = MathF.Max(0.1f, attacker.LastToDieUniversalModifiers.MeleeScale);
            var rangeScale = attacker.GetExperimentalDemoknightSwordRange() / PlayerEntity.ExperimentalDemoknightSwordBaseRange;
            var maskScale = geometryScale * MathF.Max(0.1f, rangeScale);
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var distance = MathF.Sqrt((aimDeltaX * aimDeltaX) + (aimDeltaY * aimDeltaY));
            if (distance <= 0.0001f)
            {
                return;
            }

            var directionX = aimDeltaX / distance;
            var directionY = aimDeltaY / distance;
            var facingLeft = MathF.Cos(attacker.AimDirectionDegrees * (MathF.PI / 180f)) < 0f;
            var hitboxSpriteName = ResolveExperimentalDemoknightMeleeHitboxSpriteName(attacker);
            var hitboxMask = MeleeHitboxMaskCatalog.GetOrLoad(hitboxSpriteName);
            if (hitboxMask is null)
            {
                return;
            }

            var anchorX = attacker.X;
            var anchorY = attacker.Y + MeleeHitboxMask.TorsoSitDownWorldOffset;
            ReflectExperimentalDemoknightProjectiles(
                attacker,
                hitboxMask,
                anchorX,
                anchorY,
                facingLeft,
                maskScale,
                directionX,
                directionY);

            var result = ResolveMeleeAreaHit(
                attacker,
                hitboxMask,
                anchorX,
                anchorY,
                facingLeft,
                maskScale);
            RegisterCombatTrace(
                anchorX,
                anchorY,
                directionX,
                directionY,
                result.Distance,
                result.HitPlayer is not null,
                attacker.Team);

            var damage = attacker.GetExperimentalDemoknightSwordDamage();
            if (damage <= 0)
            {
                return;
            }

            if (result.HitPlayer is not null)
            {
                var targetWasGrounded = result.HitPlayer.IsGrounded;
                RegisterBloodEffect(
                    result.HitPlayer.X,
                    result.HitPlayer.Y,
                    PointDirectionDegrees(anchorX, anchorY, result.HitPlayer.X, result.HitPlayer.Y) - 180f,
                    count: attacker.IsExperimentalDemoknightCharging ? 2 : 1);
                if (!result.HitPlayer.IsUbered)
                {
                    result.HitPlayer.AddImpulse(directionX * 2.5f * LegacyMovementModel.SourceTicksPerSecond, -1.5f * LegacyMovementModel.SourceTicksPerSecond);
                }
                if (ApplyPlayerDamage(
                        result.HitPlayer,
                        damage,
                        attacker,
                        PlayerEntity.SpyDamageRevealAlpha,
                        targetWasGrounded: targetWasGrounded))
                {
                    KillPlayer(
                        result.HitPlayer,
                        killer: attacker,
                        weaponSpriteName: ExperimentalDemoknightCatalog.EyelanderKillFeedSpriteName,
                        deadBodyAnimationKind: DeadBodyAnimationKind.Decapitated);
                    _world.TrySpawnExperimentalDemoknightDecapitationRemains(result.HitPlayer, directionX, directionY);
                }

                attacker.ConsumeExperimentalDemoknightChargeOnHit();
                return;
            }

            if (result.HitSentry is not null)
            {
                if (ApplySentryDamage(result.HitSentry, damage, attacker))
                {
                    DestroySentry(result.HitSentry, attacker);
                }

                attacker.ConsumeExperimentalDemoknightChargeOnHit();
                return;
            }

            if (result.HitGenerator is not null)
            {
                TryDamageGenerator(result.HitGenerator.Team, damage, attacker);
                attacker.ConsumeExperimentalDemoknightChargeOnHit();
                return;
            }

            if (!attacker.TryMarkExperimentalDemoknightSwordSwingImpact())
            {
                return;
            }

            var wallHit = ResolveRifleHit(attacker, anchorX, anchorY, directionX, directionY, hitboxMask.MaxReachFromOrigin * maskScale);
            if (wallHit.HitPlayer is null
                && wallHit.HitSentry is null
                && wallHit.HitGenerator is null
                && wallHit.Distance < hitboxMask.MaxReachFromOrigin * maskScale)
            {
                RegisterImpactEffect(
                    anchorX + directionX * wallHit.Distance,
                    anchorY + directionY * wallHit.Distance,
                    PointDirectionDegrees(0f, 0f, directionX, directionY));
            }
        }

        private static int ResolveExperimentalDemoknightSwordSwingTicks(PlayerEntity attacker)
        {
            var recoilTicks = 0;
            var itemId = attacker.GameplayLoadoutState.PrimaryItemId;
            if (!string.IsNullOrWhiteSpace(itemId)
                && CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item))
            {
                recoilTicks = item.Presentation.RecoilDurationSourceTicks;
            }

            return attacker.ResolveExperimentalDemoknightSwordSwingTicks(recoilTicks);
        }

        private static string ResolveExperimentalDemoknightMeleeHitboxSpriteName(PlayerEntity attacker)
        {
            var itemId = attacker.GameplayLoadoutState.PrimaryItemId;
            if (!string.IsNullOrWhiteSpace(itemId)
                && CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item)
                && !string.IsNullOrWhiteSpace(item.Presentation.MeleeHitboxSpriteName))
            {
                return item.Presentation.MeleeHitboxSpriteName!;
            }

            return ExperimentalDemoknightCatalog.EyelanderMeleeHitboxSpriteName;
        }

        private RifleHitResult ResolveMeleeAreaHit(
            PlayerEntity attacker,
            MeleeHitboxMask mask,
            float anchorX,
            float anchorY,
            bool facingLeft,
            float maskScale)
        {
            var maxDistance = mask.MaxReachFromOrigin * maskScale;
            PlayerEntity? nearestPlayer = null;
            var nearestPlayerDistance = maxDistance;
            foreach (var player in _world.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive
                    || player.Id == attacker.Id
                    || attacker.HasExperimentalDemoknightSwordHitPlayer(player.Id)
                    || !_world.CanTeamDamagePlayer(attacker.Team, attacker.Id, player))
                {
                    continue;
                }

                _world.GetCachedPlayerPresentationHitBounds(player, out var left, out var top, out var right, out var bottom);
                if (!mask.OverlapsRectangle(left, top, right, bottom, anchorX, anchorY, facingLeft, maskScale))
                {
                    continue;
                }

                var hitDistance = DistanceBetween(anchorX, anchorY, player.X, player.Y);
                if (hitDistance < nearestPlayerDistance)
                {
                    nearestPlayerDistance = hitDistance;
                    nearestPlayer = player;
                }
            }

            if (nearestPlayer is not null)
            {
                _ = attacker.TryMarkExperimentalDemoknightSwordHitPlayer(nearestPlayer.Id);
                return new RifleHitResult(nearestPlayerDistance, nearestPlayer, HitSentry: null, HitGenerator: null);
            }

            SentryEntity? nearestSentry = null;
            var nearestSentryDistance = maxDistance;
            for (var sentryIndex = 0; sentryIndex < _world._sentries.Count; sentryIndex += 1)
            {
                var sentry = _world._sentries[sentryIndex];
                if (sentry.Team == attacker.Team
                    || attacker.HasExperimentalDemoknightSwordHitSentry(sentry.Id))
                {
                    continue;
                }

                var left = sentry.X - (SentryEntity.Width / 2f);
                var top = sentry.Y - (SentryEntity.Height / 2f);
                var right = sentry.X + (SentryEntity.Width / 2f);
                var bottom = sentry.Y + (SentryEntity.Height / 2f);
                if (!mask.OverlapsRectangle(left, top, right, bottom, anchorX, anchorY, facingLeft, maskScale))
                {
                    continue;
                }

                var hitDistance = DistanceBetween(anchorX, anchorY, sentry.X, sentry.Y);
                if (hitDistance < nearestSentryDistance)
                {
                    nearestSentryDistance = hitDistance;
                    nearestSentry = sentry;
                }
            }

            if (nearestSentry is not null)
            {
                _ = attacker.TryMarkExperimentalDemoknightSwordHitSentry(nearestSentry.Id);
                return new RifleHitResult(nearestSentryDistance, HitPlayer: null, nearestSentry, HitGenerator: null);
            }

            GeneratorState? nearestGenerator = null;
            var nearestGeneratorDistance = maxDistance;
            for (var generatorIndex = 0; generatorIndex < _world._generators.Count; generatorIndex += 1)
            {
                var generator = _world._generators[generatorIndex];
                if (generator.Team == attacker.Team
                    || generator.IsDestroyed
                    || attacker.HasExperimentalDemoknightSwordHitGenerator(generator.Team))
                {
                    continue;
                }

                if (!mask.OverlapsRectangle(
                        generator.Marker.Left,
                        generator.Marker.Top,
                        generator.Marker.Right,
                        generator.Marker.Bottom,
                        anchorX,
                        anchorY,
                        facingLeft,
                        maskScale))
                {
                    continue;
                }

                var hitDistance = DistanceBetween(
                    anchorX,
                    anchorY,
                    (generator.Marker.Left + generator.Marker.Right) * 0.5f,
                    (generator.Marker.Top + generator.Marker.Bottom) * 0.5f);
                if (hitDistance < nearestGeneratorDistance)
                {
                    nearestGeneratorDistance = hitDistance;
                    nearestGenerator = generator;
                }
            }

            if (nearestGenerator is not null)
            {
                _ = attacker.TryMarkExperimentalDemoknightSwordHitGenerator(nearestGenerator.Team);
                return new RifleHitResult(nearestGeneratorDistance, HitPlayer: null, HitSentry: null, nearestGenerator);
            }

            return new RifleHitResult(maxDistance, HitPlayer: null, HitSentry: null, HitGenerator: null);
        }

        private void ReflectExperimentalDemoknightProjectiles(
            PlayerEntity attacker,
            MeleeHitboxMask mask,
            float anchorX,
            float anchorY,
            bool facingLeft,
            float maskScale,
            float directionX,
            float directionY)
        {
            var directionRadians = MathF.Atan2(directionY, directionX);

            for (var rocketIndex = 0; rocketIndex < _world._rockets.Count; rocketIndex += 1)
            {
                var rocket = _world._rockets[rocketIndex];
                if (rocket.Team == attacker.Team
                    || !mask.OverlapsCircle(rocket.X, rocket.Y, 5f, anchorX, anchorY, facingLeft, maskScale))
                {
                    continue;
                }

                rocket.Reflect(attacker.Id, attacker.Team, directionRadians);
            }

            for (var flareIndex = 0; flareIndex < _world._flares.Count; flareIndex += 1)
            {
                var flare = _world._flares[flareIndex];
                if (flare.Team == attacker.Team
                    || !mask.OverlapsCircle(flare.X, flare.Y, 5f, anchorX, anchorY, facingLeft, maskScale))
                {
                    continue;
                }

                _world.ResolveDragonRageProjectileOutcome(flare, hitTarget: false);
                flare.Reflect(attacker.Id, attacker.Team, directionRadians);
            }

            for (var mineIndex = 0; mineIndex < _world._mines.Count; mineIndex += 1)
            {
                var mine = _world._mines[mineIndex];
                if (mine.Team == attacker.Team
                    || !mask.OverlapsCircle(mine.X, mine.Y, 5f, anchorX, anchorY, facingLeft, maskScale))
                {
                    continue;
                }

                mine.Reflect(attacker.Id, attacker.Team, directionRadians, PyroAirblastMineSpeedFloor);
            }
        }
    }
}
