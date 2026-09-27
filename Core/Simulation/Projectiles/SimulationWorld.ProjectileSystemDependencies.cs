using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private ProjectileSystemDependencies CreateProjectileSystemDependencies()
    {
        return new ProjectileSystemDependencies
        {
            AllocateEntityId = AllocateEntityId,
            GetLevel = () => Level,
            Config = Config,
            CurrentFrame = () => Frame,
            GetGravityScale = () => _configuredGravityScale,
            IsClientPredictionMode = () => ClientPredictionMode,
            GetAuthoritativeLocalPlayerId = () => _authoritativeLocalPlayerId,
            GetLocalPlayerId = () => LocalPlayer.Id,
            LocalProjectileTerminationSuppressionTicks = LocalProjectileTerminationSuppressionTicks,
            ClientPredictedProjectileIds = _clientPredictedProjectileIds,
            SuppressProjectileRespawn = SuppressProjectileRespawn,
            EnumerateSimulatedPlayers = EnumerateSimulatedPlayers,
            FindPlayerById = FindPlayerById,
            CanTeamDamagePlayer = CanTeamDamagePlayer,
            GetSimulationTicksFromSourceTicks = sourceTicks => GetSimulationTicksFromSourceTicks(sourceTicks),
            NextRandomInt = maximumExclusive => _random.Next(maximumExclusive),
            NextRandomSingle = () => _random.NextSingle(),
            Sentries = _sentries,
            JumpPads = _jumpPads,
            Generators = _generators,
            MarkProjectileTerminated = _ => { },
            RegisterCombatTrace = (x, y, dx, dy, distance, hitCharacter, team, sniper, critical) =>
                RegisterCombatTrace(x, y, dx, dy, distance, hitCharacter, team, sniper, critical),
            RegisterBloodEffect = RegisterBloodEffect,
            RegisterVisualEffect = RegisterVisualEffect,
            RegisterWorldSoundEvent = RegisterWorldSoundEvent,
            RegisterImpactEffect = RegisterImpactEffect,
            RegisterStuckArrowEffect = RegisterStuckArrowEffect,
            GetNearestShotHit = GetNearestShotHit,
            GetNearestNeedleHit = GetNearestNeedleHit,
            GetNearestMedicHealNeedleHit = GetNearestMedicHealNeedleHit,
            GetNearestRevolverHit = GetNearestRevolverHit,
            GetNearestBladeHit = GetNearestBladeHit,
            GetNearestStabHit = GetNearestStabHit,
            GetNearestHealstabHit = GetNearestHealstabHit,
            HasStabChainLineOfSight = HasStabChainLineOfSight,
            GetNearestRocketHit = GetNearestRocketHit,
            GetNearestMineHit = GetNearestMineHit,
            GetNearestGrenadeEnvironmentHit = GetNearestGrenadeEnvironmentHit,
            GetNearestGrenadePlayerHit = GetNearestGrenadePlayerHit,
            TryGetGrenadeDamageableZoneContact = TryGetGrenadeDamageableZoneContact,
            GetNearestFlameHit = GetNearestFlameHit,
            GetNearestFlareHit = GetNearestFlareHit,
            GetNearestFlareHitWithPlayerFilter = (flare, dx, dy, distance, includePlayers) =>
                GeometryResolver.GetNearestFlareHit(flare, dx, dy, distance, includePlayers),
            GetNearestFlarePlayerHit = (flare, dx, dy, distance, blockingHit) =>
                GeometryResolver.GetNearestFlarePlayerHit(flare, dx, dy, distance, blockingHit),
            IsProjectilePathBlocked = IsProjectilePathBlocked,
            GetCachedPlayerPresentationHitBounds = (PlayerEntity player, out float left, out float top, out float right, out float bottom) =>
            {
                GetPlayerPresentationHitBounds(this, player, out left, out top, out right, out bottom);
            },
            TryInterceptWithCivilDefenseTurret = TryInterceptWithCivilDefenseTurret,
            TryHandleProjectileDamageableZoneHit = (hit, damage, team) => TryHandleProjectileDamageableZoneHit(in hit, damage, team),
            TryApplyDamageableZoneDamage = TryApplyDamageableZoneDamage,
            BlocksProjectileDamageableZone = BlocksProjectileDamageableZone,
            ApplyExperimentalAirshotDamageMultiplier = (owner, target, damage) =>
            {
                var adjustedDamage = ApplyExperimentalAirshotDamageMultiplier(owner, target, damage, out var flags);
                return (adjustedDamage, flags);
            },
            ApplyExperimentalSentryPlayerHit = (sentry, owner, target, damage, traits, criticalBoost, useLiveAttackerCriticalBoost, threatX, threatY, knockbackPayload, impactX, impactY) =>
                ApplyExperimentalSentryPlayerHit(
                    sentry,
                    owner,
                    target,
                    damage,
                    traits,
                    criticalBoost,
                    useLiveAttackerCriticalBoost,
                    threatX,
                    threatY,
                    knockbackPayload,
                    impactX,
                    impactY),
            ApplyExperimentalSentryDamageRewards = ApplyExperimentalSentryDamageRewards,
            TryApplyLastToDieSniperGuardian = TryApplyLastToDieSniperGuardian,
            ApplyMedicHealNeedleTeammateHit = ApplyMedicHealNeedleTeammateHit,
            ExplodeBoomstickPellet = ExplodeBoomstickPellet,
            KillPlayer = (player, gibbed, killer, weapon, animation) => KillPlayer(player, gibbed, killer, weapon, animation),
            DestroySentry = (sentry, attacker) => DestroySentry(sentry, attacker),
            TryDamageGenerator = TryDamageGenerator,
            ApplySentryDamage = ApplySentryDamage,
            ApplyGeneratorDamage = ApplyGeneratorDamage,
            ApplyJumpPadDamage = (jumpPad, damage) => jumpPad.TakeDamage(damage),
            ApplyHealingWithFeedback = ApplyHealingWithFeedback,
            ExplodeRocket = (rocket, player, sentry, generator, damageableZoneIndex) =>
                ExplodeRocket(rocket, player, sentry, generator, damageableZoneIndex),
            ApplyExplosiveDamageToJumpPads = ApplyExplosiveDamageToJumpPads,
            ApplyExplosiveDamageToDamageableZones = ApplyExplosiveDamageToDamageableZones,
            DestroyJumpPad = DestroyJumpPad,
            ApplyDeadBodyExplosionImpulse = ApplyDeadBodyExplosionImpulse,
            ApplyPlayerGibExplosionImpulse = ApplyPlayerGibExplosionImpulse,
            RegisterExplosionTraces = RegisterExplosionTraces,
            ShouldSkipFriendlyExplosionBoost = ShouldSkipFriendlyExplosionBoost,
            ShouldIgnoreFriendlyGroundedBlast = ShouldIgnoreFriendlyGroundedBlast,
            ApplyMineExplosionImpulse = ApplyMineExplosionImpulse,
            GetExplosionDistanceToPlayer = (player, x, y) => GetExplosionDistanceToPlayer(this, player, x, y),
            GetKillFeedWeaponSprite = GetKillFeedWeaponSprite,
            GetLastToDieGameplaySettings = GetLastToDieGameplaySettings,
            IsExperimentalPracticePowerOwner = IsExperimentalPracticePowerOwner,
            ResolveExperimentalEngineerRocketTrackingDirection = (rocket, player) =>
            {
                var success = TryResolveExperimentalEngineerRocketTrackingDirection(rocket, player, out var direction);
                return (success, direction);
            },
            GetExperimentalSoldierStingerTurnRateRadians = GetExperimentalSoldierStingerTurnRateRadians,
            GetExperimentalEngineerCaveatTurnRateRadians = GetExperimentalEngineerCaveatTurnRateRadians,
            CaptureLastToDieMedicKritzM2Payload = CaptureLastToDieMedicKritzM2Payload,
            TryApplyLastToDieSniperStatusPayload = TryApplyLastToDieSniperStatusPayload,
            TryApplyLastToDieStatusEffect = (targetId, sourceId, spec) => TryApplyLastToDieStatusEffect(targetId, sourceId, spec),
            TryExplodeLastToDieSniperArrow = (arrow, x, y) => TryExplodeLastToDieSniperArrow(arrow, x, y),
            TryExplodeLastToDieMedicJavelin = TryExplodeLastToDieMedicJavelin,
            TrySpawnExperimentalDemoknightDecapitationRemains = TrySpawnExperimentalDemoknightDecapitationRemains,
        };
    }
}
