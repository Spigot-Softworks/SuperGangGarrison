namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        private void FireRifle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            FireRifle(attacker, attacker.ClassId, aimWorldX, aimWorldY, "RifleKL");
        }

        private void FireRifle(
            PlayerEntity attacker,
            PlayerClass weaponClassId,
            float aimWorldX,
            float aimWorldY,
            string killFeedWeaponSpriteNameOverride)
        {
            const float rifleDistance = 2000f;

            var weaponOrigin = GetSourceWeaponOrigin(attacker, weaponClassId);
            var aimDeltaX = aimWorldX - weaponOrigin.BaseX;
            var aimDeltaY = aimWorldY - weaponOrigin.BaseY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var distance = MathF.Sqrt((aimDeltaX * aimDeltaX) + (aimDeltaY * aimDeltaY));
            if (distance <= 0.0001f)
            {
                return;
            }

            // FireRifle is reached only after the weapon trigger has accepted
            // ammo/cycle state. Capture and spend Ghost exactly once here;
            // misses still count as fired shots, while dry/blocked triggers
            // never reach this seam.
            var ghostDamageMultiplier = attacker.ClassId == PlayerClass.Sniper
                ? attacker.CaptureLastToDieSniperGhostShot(Config.TicksPerSecond)
                : 1f;
            var capturedCriticalDamageMultiplier = attacker.ActiveKritzCritDamageMultiplier;
            var isCritical = PlayerEntity.IsCriticalDamageMultiplierBoosted(
                capturedCriticalDamageMultiplier);

            var directionX = aimDeltaX / distance;
            var directionY = aimDeltaY / distance;
            var sniperProfile = attacker.ClassId == PlayerClass.Sniper
                ? attacker.LastToDieSniperProfile
                : global::OpenGarrison.Core.LastToDie.LastToDieSniperProfile.Stock;
            var isFullyCharged = !sniperProfile.LightMarksmanEnabled
                && attacker.IsSniperScoped
                && attacker.SniperChargeTicks >= attacker.LastToDieSniperRifleFullChargeTicks;
            var maximumEnemyPlayerHits = sniperProfile.MechanicaEnabled && isFullyCharged
                ? 64
                : sniperProfile.FiftyCalEnabled
                    ? global::OpenGarrison.Core.LastToDie.LastToDieSniperProfile.FiftyCalMaximumPlayerHits
                    : 1;
            var result = ResolveOrderedRifleHits(
                attacker,
                weaponOrigin.BaseX,
                weaponOrigin.BaseY,
                directionX,
                directionY,
                rifleDistance,
                new RifleTracePolicy(
                    IgnoreOrdinaryGeometry: sniperProfile.FmjEnabled,
                    AllowFriendlySupport: sniperProfile.GuardianEnabled,
                    MaximumEnemyPlayerHits: maximumEnemyPlayerHits,
                    DetectLastToDieHeadshots: sniperProfile.DecapitatorEnabled));
            RegisterCombatTrace(
                weaponOrigin.BaseX,
                weaponOrigin.BaseY,
                directionX,
                directionY,
                result.Distance,
                result.PlayerHits.Count > 0,
                attacker.Team,
                isSniperTracer: true,
                isCritical: isCritical);
            var damage = attacker.GetSniperRifleDamage();
            if (sniperProfile.TranqDartsEnabled)
            {
                damage = Math.Max(
                    1,
                    (int)MathF.Round(
                        damage * global::OpenGarrison.Core.LastToDie.LastToDieSniperProfile.TranqDartsDirectDamageMultiplier));
            }
            damage = Math.Max(
                1,
                (int)MathF.Round(
                    damage
                        * ghostDamageMultiplier
                        * capturedCriticalDamageMultiplier));
            var enemyHitOrdinal = 0;
            foreach (var playerHit in result.PlayerHits)
            {
                if (playerHit.IsFriendlySupport)
                {
                    _world.TryApplyLastToDieSniperGuardian(attacker, playerHit.Player);
                    break;
                }

                var executesFromFiftyCal = sniperProfile.FiftyCalEnabled && enemyHitOrdinal == 0;
                var executesFromDecapitator = sniperProfile.DecapitatorEnabled
                    && isFullyCharged
                    && playerHit.IsLastToDieHeadshot;
                var executesTarget = executesFromFiftyCal || executesFromDecapitator;
                enemyHitOrdinal += 1;
                RegisterBloodEffect(
                    playerHit.Player.X,
                    playerHit.Player.Y,
                    PointDirectionDegrees(
                        weaponOrigin.BaseX,
                        weaponOrigin.BaseY,
                        playerHit.Player.X,
                        playerHit.Player.Y) - 180f);
                var resolution = ResolveRiflePlayerDamage(
                    playerHit.Player,
                    damage,
                    attacker,
                    weaponOrigin.BaseX,
                    weaponOrigin.BaseY,
                    executesTarget,
                    isCritical,
                    killFeedWeaponSpriteNameOverride);
                if (resolution.ShouldApplyOnHitEffects && sniperProfile.TranqDartsEnabled)
                {
                    _world.TryApplyLastToDieSniperStatusPayload(
                        attacker,
                        playerHit.Player,
                        appliesTranqDarts: true,
                        poisonTipDamagePerSecond: 0f);
                }

                if (!resolution.WasFatal)
                {
                    continue;
                }

                var deadBodyAnimationKind = executesFromDecapitator && !executesFromFiftyCal
                    ? DeadBodyAnimationKind.Decapitated
                    : damage > PlayerEntity.SniperBaseDamage
                        ? DeadBodyAnimationKind.Severe
                        : DeadBodyAnimationKind.Rifle;
                KillPlayer(
                    playerHit.Player,
                    gibbed: executesFromFiftyCal,
                    killer: attacker,
                    weaponSpriteName: killFeedWeaponSpriteNameOverride,
                    deadBodyAnimationKind: deadBodyAnimationKind);
                if (executesFromDecapitator
                    && !executesFromFiftyCal
                    && !playerHit.Player.IsAlive)
                {
                    _world.TrySpawnExperimentalDemoknightDecapitationRemains(
                        playerHit.Player,
                        directionX,
                        directionY);
                }
            }

            if (result.HitSentry is not null && ApplySentryDamage(result.HitSentry, damage, attacker))
            {
                DestroySentry(result.HitSentry, attacker);
            }
            else if (result.HitGenerator is not null)
            {
                TryDamageGenerator(result.HitGenerator.Team, damage, attacker);
            }
            else if (result.HitJumpPad is not null)
            {
                result.HitJumpPad.TakeDamage(damage);
            }
            else if (result.PlayerHits.Count == 0 && result.Distance < rifleDistance)
            {
                RegisterImpactEffect(
                    weaponOrigin.BaseX + directionX * result.Distance,
                    weaponOrigin.BaseY + directionY * result.Distance,
                    PointDirectionDegrees(0f, 0f, directionX, directionY));
            }
        }

        private PlayerDamageResolution ResolveRiflePlayerDamage(
            PlayerEntity target,
            int damage,
            PlayerEntity attacker,
            float originX,
            float originY,
            bool executeAfterDefenses,
            bool isCritical,
            string killFeedWeaponSpriteName)
        {
            var traits = PlayerDamageTraits.CanEvade
                | PlayerDamageTraits.CanApplyOnHitEffects
                | PlayerDamageTraits.CanReflect
                | PlayerDamageTraits.Bullet
                | PlayerDamageTraits.EstablishLastToDieSpotted
                | PlayerDamageTraits.BenefitFromLastToDieSpotted
                | PlayerDamageTraits.LastToDieOverkillerEligible;
            if (isCritical)
            {
                traits |= PlayerDamageTraits.Critical;
            }
            if (executeAfterDefenses)
            {
                traits |= PlayerDamageTraits.ExecuteAfterDefenses;
            }

            return _world.ResolvePlayerDamage(
                target,
                new PlayerDamageRequest(
                    PlayerDamageApplicationKind.Instant,
                    damage,
                    attacker,
                    PlayerEntity.SpySniperRevealAlpha,
                    DamageEventFlags.None,
                    traits,
                    AllowOsmosisHealOwnedSentries: true,
                    new PlayerDamageUmbrellaOptions(
                        AllowBlock: true,
                        ThreatSourceX: originX,
                        ThreatSourceY: originY,
                        CriticalBoost: isCritical,
                        UseLiveAttackerCriticalBoost: false),
                    AttackerWasGrounded: attacker.IsGrounded,
                    TargetWasGrounded: target.IsGrounded,
                    GibOnFatal: executeAfterDefenses,
                    FatalWeaponSpriteName: killFeedWeaponSpriteName));
        }
    }
}
