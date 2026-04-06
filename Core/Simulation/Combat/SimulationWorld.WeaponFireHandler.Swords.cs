namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        public void FireQuoteBlade(PlayerEntity attacker, float aimWorldX, float aimWorldY)
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
                hitDamage);
        }

        public void FireExperimentalDemoknightSword(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            const float swordOffsetDistance = 12f;

            RegisterSoundEvent(attacker, ExperimentalDemoknightCatalog.EyelanderSwingSoundName);
            var swordRange = attacker.GetExperimentalDemoknightSwordRange();
            var weaponOrigin = GetSourceWeaponOrigin(attacker);
            var originX = weaponOrigin.BaseX;
            var originY = weaponOrigin.BaseY + weaponOrigin.WeaponYOffset + weaponOrigin.EquipmentOffset;
            var aimDeltaX = aimWorldX - originX;
            var aimDeltaY = aimWorldY - originY;
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
            var perpendicularX = -directionY;
            var perpendicularY = directionX;
            ReflectExperimentalDemoknightProjectiles(
                attacker,
                originX,
                originY,
                directionX,
                directionY,
                perpendicularX,
                perpendicularY,
                swordRange,
                swordOffsetDistance);

            var centerResult = ResolveRifleHit(attacker, originX, originY, directionX, directionY, swordRange);
            var leftResult = ResolveRifleHit(
                attacker,
                originX + perpendicularX * swordOffsetDistance,
                originY + perpendicularY * swordOffsetDistance,
                directionX,
                directionY,
                swordRange);
            var rightResult = ResolveRifleHit(
                attacker,
                originX - perpendicularX * swordOffsetDistance,
                originY - perpendicularY * swordOffsetDistance,
                directionX,
                directionY,
                swordRange);
            var wideLeftResult = ResolveRifleHit(
                attacker,
                originX + perpendicularX * swordOffsetDistance * 2f,
                originY + perpendicularY * swordOffsetDistance * 2f,
                directionX,
                directionY,
                swordRange);
            var wideRightResult = ResolveRifleHit(
                attacker,
                originX - perpendicularX * swordOffsetDistance * 2f,
                originY - perpendicularY * swordOffsetDistance * 2f,
                directionX,
                directionY,
                swordRange);
            var result = GetNearestSwordHit(centerResult, leftResult, rightResult, wideLeftResult, wideRightResult);
            RegisterCombatTrace(originX, originY, directionX, directionY, result.Distance, result.HitPlayer is not null, attacker.Team);

            var damage = attacker.GetExperimentalDemoknightSwordDamage();
            if (damage <= 0)
            {
                return;
            }

            if (result.HitPlayer is not null)
            {
                RegisterBloodEffect(
                    result.HitPlayer.X,
                    result.HitPlayer.Y,
                    PointDirectionDegrees(originX, originY, result.HitPlayer.X, result.HitPlayer.Y) - 180f,
                    count: attacker.IsExperimentalDemoknightCharging ? 2 : 1);
                result.HitPlayer.AddImpulse(directionX * 2.5f * LegacyMovementModel.SourceTicksPerSecond, -1.5f * LegacyMovementModel.SourceTicksPerSecond);
                if (ApplyPlayerDamage(result.HitPlayer, damage, attacker, PlayerEntity.SpyDamageRevealAlpha))
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

            if (result.Distance < swordRange)
            {
                RegisterImpactEffect(
                    originX + directionX * result.Distance,
                    originY + directionY * result.Distance,
                    PointDirectionDegrees(0f, 0f, directionX, directionY));
            }
        }

        private void ReflectExperimentalDemoknightProjectiles(
            PlayerEntity attacker,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float perpendicularX,
            float perpendicularY,
            float swordRange,
            float swordOffsetDistance)
        {
            var directionRadians = MathF.Atan2(directionY, directionX);

            for (var rocketIndex = 0; rocketIndex < _world._rockets.Count; rocketIndex += 1)
            {
                var rocket = _world._rockets[rocketIndex];
                if (rocket.Team == attacker.Team
                    || !IsProjectileInsideSwordSwing(
                        rocket.X,
                        rocket.Y,
                        5f,
                        originX,
                        originY,
                        directionX,
                        directionY,
                        perpendicularX,
                        perpendicularY,
                        swordRange,
                        swordOffsetDistance))
                {
                    continue;
                }

                rocket.Reflect(attacker.Id, attacker.Team, directionRadians);
            }

            for (var flareIndex = 0; flareIndex < _world._flares.Count; flareIndex += 1)
            {
                var flare = _world._flares[flareIndex];
                if (flare.Team == attacker.Team
                    || !IsProjectileInsideSwordSwing(
                        flare.X,
                        flare.Y,
                        5f,
                        originX,
                        originY,
                        directionX,
                        directionY,
                        perpendicularX,
                        perpendicularY,
                        swordRange,
                        swordOffsetDistance))
                {
                    continue;
                }

                flare.Reflect(attacker.Id, attacker.Team, directionRadians);
            }

            for (var mineIndex = 0; mineIndex < _world._mines.Count; mineIndex += 1)
            {
                var mine = _world._mines[mineIndex];
                if (mine.Team == attacker.Team
                    || !IsProjectileInsideSwordSwing(
                        mine.X,
                        mine.Y,
                        5f,
                        originX,
                        originY,
                        directionX,
                        directionY,
                        perpendicularX,
                        perpendicularY,
                        swordRange,
                        swordOffsetDistance))
                {
                    continue;
                }

                mine.Reflect(attacker.Id, attacker.Team, directionRadians, PyroAirblastMineSpeedFloor);
            }
        }

        private static RifleHitResult GetNearestSwordHit(params RifleHitResult[] candidates)
        {
            var best = candidates[0];
            for (var index = 1; index < candidates.Length; index += 1)
            {
                if (candidates[index].Distance < best.Distance)
                {
                    best = candidates[index];
                }
            }

            return best;
        }

        private static bool IsProjectileInsideSwordSwing(
            float targetX,
            float targetY,
            float radius,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float perpendicularX,
            float perpendicularY,
            float swordRange,
            float swordOffsetDistance)
        {
            return IsPointNearSwordSwingSegment(targetX, targetY, radius, originX, originY, directionX, directionY, swordRange)
                || IsPointNearSwordSwingSegment(
                    targetX,
                    targetY,
                    radius,
                    originX + perpendicularX * swordOffsetDistance,
                    originY + perpendicularY * swordOffsetDistance,
                    directionX,
                    directionY,
                    swordRange)
                || IsPointNearSwordSwingSegment(
                    targetX,
                    targetY,
                    radius,
                    originX - perpendicularX * swordOffsetDistance,
                    originY - perpendicularY * swordOffsetDistance,
                    directionX,
                    directionY,
                    swordRange)
                || IsPointNearSwordSwingSegment(
                    targetX,
                    targetY,
                    radius,
                    originX + perpendicularX * swordOffsetDistance * 2f,
                    originY + perpendicularY * swordOffsetDistance * 2f,
                    directionX,
                    directionY,
                    swordRange)
                || IsPointNearSwordSwingSegment(
                    targetX,
                    targetY,
                    radius,
                    originX - perpendicularX * swordOffsetDistance * 2f,
                    originY - perpendicularY * swordOffsetDistance * 2f,
                    directionX,
                    directionY,
                    swordRange);
        }

        private static bool IsPointNearSwordSwingSegment(
            float targetX,
            float targetY,
            float radius,
            float startX,
            float startY,
            float directionX,
            float directionY,
            float distance)
        {
            var endX = startX + directionX * distance;
            var endY = startY + directionY * distance;
            var segmentX = endX - startX;
            var segmentY = endY - startY;
            var segmentLengthSquared = (segmentX * segmentX) + (segmentY * segmentY);
            if (segmentLengthSquared <= 0.0001f)
            {
                return DistanceBetween(startX, startY, targetX, targetY) <= radius;
            }

            var projection = ((targetX - startX) * segmentX + (targetY - startY) * segmentY) / segmentLengthSquared;
            projection = Math.Clamp(projection, 0f, 1f);
            var closestX = startX + segmentX * projection;
            var closestY = startY + segmentY * projection;
            return DistanceBetween(closestX, closestY, targetX, targetY) <= radius;
        }
    }
}
