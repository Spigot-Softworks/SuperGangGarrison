using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    float IProjectileSpawnContext.GravityScale => MatchSettings.GravityScale;
    bool IProjectileSpawnContext.IsClientPredictionMode => ClientPredictionMode;
    int? IProjectileSpawnContext.AuthoritativeLocalPlayerId => ClientSnapshots.AuthoritativeLocalPlayerId;
    int IProjectileSpawnContext.LocalPlayerId => LocalPlayer.Id;
    int IProjectileSpawnContext.LocalProjectileTerminationSuppressionTicks => LocalProjectileTerminationSuppressionTicks;
    HashSet<int> IProjectileSpawnContext.ClientPredictedProjectileIds => ClientSnapshots.PredictedProjectileIds;
    int IProjectileSpawnContext.AllocateEntityId() => AllocateEntityId();
    void IProjectileSpawnContext.SuppressProjectileRespawn(int projectileId, int ticks) => SnapshotApply.SuppressProjectileRespawn(projectileId, ticks);

    ShotHitResult? IProjectileHitQueries.GetNearestShotHit(ShotProjectileEntity shot, float directionX, float directionY, float distance)
        => GetNearestShotHit(shot, directionX, directionY, distance);
    ShotHitResult? IProjectileHitQueries.GetNearestNeedleHit(NeedleProjectileEntity needle, float directionX, float directionY, float distance)
        => GetNearestNeedleHit(needle, directionX, directionY, distance);
    ShotHitResult? IProjectileHitQueries.GetNearestMedicHealNeedleHit(MedicHealNeedleProjectileEntity needle, float directionX, float directionY, float distance)
        => GetNearestMedicHealNeedleHit(needle, directionX, directionY, distance);
    ShotHitResult? IProjectileHitQueries.GetNearestRevolverHit(RevolverProjectileEntity shot, float directionX, float directionY, float distance)
        => GetNearestRevolverHit(shot, directionX, directionY, distance);
    ShotHitResult? IProjectileHitQueries.GetNearestBladeHit(BladeProjectileEntity blade, float directionX, float directionY, float distance)
        => GetNearestBladeHit(blade, directionX, directionY, distance);
    ShotHitResult? IProjectileHitQueries.GetNearestStabHit(StabMaskEntity mask, float directionX, float directionY)
        => GetNearestStabHit(mask, directionX, directionY);
    ShotHitResult? IProjectileHitQueries.GetNearestHealstabHit(StabMaskEntity mask, float directionX, float directionY)
        => GetNearestHealstabHit(mask, directionX, directionY);
    bool IProjectileHitQueries.HasStabChainLineOfSight(float x1, float y1, float x2, float y2)
        => HasStabChainLineOfSight(x1, y1, x2, y2);
    RocketHitResult? IProjectileHitQueries.GetNearestRocketHit(RocketProjectileEntity rocket, float directionX, float directionY, float distance)
        => GetNearestRocketHit(rocket, directionX, directionY, distance);
    MineHitResult? IProjectileHitQueries.GetNearestMineHit(MineProjectileEntity mine, float directionX, float directionY, float distance)
        => GetNearestMineHit(mine, directionX, directionY, distance);
    GrenadeEnvironmentHit? IProjectileHitQueries.GetNearestGrenadeEnvironmentHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float distance)
        => GetNearestGrenadeEnvironmentHit(grenade, directionX, directionY, distance);
    PlayerEntity? IProjectileHitQueries.GetNearestGrenadePlayerHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float distance)
        => GetNearestGrenadePlayerHit(grenade, directionX, directionY, distance);
    bool IProjectileHitQueries.TryGetGrenadeDamageableZoneContact(GrenadeProjectileEntity grenade, float directionX, float directionY, float maxDistance, out float hitX, out float hitY, out int roomObjectIndex)
        => TryGetGrenadeDamageableZoneContact(grenade, directionX, directionY, maxDistance, out hitX, out hitY, out roomObjectIndex);
    FlameHitResult? IProjectileHitQueries.GetNearestFlameHit(FlameProjectileEntity flame, float directionX, float directionY, float distance)
        => GetNearestFlameHit(flame, directionX, directionY, distance);
    ShotHitResult? IProjectileHitQueries.GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float distance)
        => GetNearestFlareHit(flare, directionX, directionY, distance);
    ShotHitResult? IProjectileHitQueries.GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float distance, bool includePlayers)
        => GeometryResolver.GetNearestFlareHit(flare, directionX, directionY, distance, includePlayers);
    ShotHitResult? IProjectileHitQueries.GetNearestFlarePlayerHit(FlareProjectileEntity flare, float directionX, float directionY, float distance, ShotHitResult? blockingHit)
        => GeometryResolver.GetNearestFlarePlayerHit(flare, directionX, directionY, distance, blockingHit);
    bool IProjectileHitQueries.IsProjectilePathBlocked(float x1, float y1, float x2, float y2, PlayerTeam team)
        => IsProjectilePathBlocked(x1, y1, x2, y2, team);
    bool IProjectileHitQueries.TryInterceptWithCivilDefenseTurret(PlayerTeam team, float x, float y, float directionX, float directionY, float distance)
        => Structures.TryInterceptWithCivilDefenseTurret(team, x, y, directionX, directionY, distance);
    void IProjectileHitQueries.GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom)
        => PresentationBounds.GetPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
    float IProjectileHitQueries.GetExplosionDistanceToPlayer(PlayerEntity player, float x, float y)
        => GetExplosionDistanceToPlayer(this, player, x, y);

    void IProjectileImpactTargets.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind)
        => PlayerDeaths.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind);
    void IProjectileImpactTargets.DestroySentry(SentryEntity sentry, PlayerEntity? attacker) => Structures.DestroySentry(sentry, attacker);
    bool IProjectileImpactTargets.ApplySentryDamage(SentryEntity sentry, int damage, PlayerEntity? attacker) => ApplySentryDamage(sentry, damage, attacker);
    bool IProjectileImpactTargets.ApplyGeneratorDamage(GeneratorState generator, float damage, PlayerEntity? attacker) => ApplyGeneratorDamage(generator, damage, attacker);
    bool IProjectileImpactTargets.TryDamageGenerator(PlayerTeam team, float damage, PlayerEntity? attacker) => ObjectiveRules.TryDamageGenerator(team, damage, attacker);
    void IProjectileImpactTargets.ApplyJumpPadDamage(JumpPadEntity jumpPad, int damage) => jumpPad.TakeDamage(damage);
    void IProjectileImpactTargets.ApplyExplosiveDamageToJumpPads(float x, float y, float radius, float damage, PlayerTeam team, float minimumDamage)
        => ExplosionRules.ApplyExplosiveDamageToJumpPads(x, y, radius, damage, team, minimumDamage);
    void IProjectileImpactTargets.ApplyExplosiveDamageToDamageableZones(float x, float y, float radius, float damage, float splashThresholdFactor, int excludeRoomObjectIndex, PlayerTeam? damagingTeam, float minimumSplashDamage)
        => MapLogic.ApplyExplosiveDamageToDamageableZones(x, y, radius, damage, splashThresholdFactor, excludeRoomObjectIndex, damagingTeam, minimumSplashDamage);
    bool IProjectileImpactTargets.TryHandleProjectileDamageableZoneHit(in ShotHitResult hit, float damage, PlayerTeam team)
        => MapLogic.TryHandleProjectileDamageableZoneHit(in hit, damage, team);
    bool IProjectileImpactTargets.TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? team)
        => MapLogic.TryApplyDamageableZoneDamage(roomObjectIndex, damage, team);
    bool IProjectileImpactTargets.BlocksProjectileDamageableZone(int roomObjectIndex) => MapLogic.BlocksProjectileDamageableZone(roomObjectIndex);
    int IProjectileImpactTargets.ApplyHealingWithFeedback(PlayerEntity player, float healing, string? soundName, float x, float y)
        => DamageRules.ApplyHealingWithFeedback(player, healing, soundName, x, y);

    void IProjectileExplosionEffects.ExplodeRocket(RocketProjectileEntity rocket, PlayerEntity? directHitPlayer, SentryEntity? directHitSentry, GeneratorState? directHitGenerator, int damageableZoneIndex)
        => ExplosionRules.ExplodeRocket(rocket, directHitPlayer, directHitSentry, directHitGenerator, damageableZoneIndex);
    void IProjectileExplosionEffects.ApplyDeadBodyExplosionImpulse(float x, float y, float radius, float impulse, float? falloff)
        => ExplosionRules.ApplyDeadBodyExplosionImpulse(x, y, radius, impulse, falloff);
    void IProjectileExplosionEffects.ApplyPlayerGibExplosionImpulse(float x, float y, float radius, float impulse, float? falloff)
        => ExplosionRules.ApplyPlayerGibExplosionImpulse(x, y, radius, impulse, falloff);
    void IProjectileExplosionEffects.ApplyMineExplosionImpulse(PlayerEntity player, float x, float y, float factor)
        => ApplyMineExplosionImpulse(player, x, y, factor);
    bool IProjectileExplosionEffects.ShouldSkipFriendlyExplosionBoost(PlayerEntity player, PlayerTeam team, int ownerId)
        => ExplosionRules.ShouldSkipFriendlyExplosionBoost(player, team, ownerId);
    bool IProjectileExplosionEffects.ShouldIgnoreFriendlyGroundedBlast(PlayerEntity player, PlayerTeam team, int ownerId)
        => ExplosionRules.ShouldIgnoreFriendlyGroundedBlast(player, team, ownerId);

    float IProjectileGameplayRules.ExperimentalSoldierStingerTurnRateRadians => ExperimentalRulesSystem.GetExperimentalSoldierStingerTurnRateRadians();
    float IProjectileGameplayRules.ExperimentalEngineerCaveatTurnRateRadians => ExperimentalRulesSystem.GetExperimentalEngineerCaveatTurnRateRadians();
    string? IProjectileGameplayRules.GetKillFeedWeaponSprite(PlayerEntity? owner) => KillFeedSystem.GetKillFeedWeaponSprite(owner);
    int IProjectileGameplayRules.ApplyExperimentalAirshotDamageMultiplier(PlayerEntity? owner, PlayerEntity target, int damage, out DamageEventFlags flags)
        => ExperimentalRules.ApplyExperimentalAirshotDamageMultiplier(owner, target, damage, out flags);
    void IProjectileGameplayRules.ApplyExperimentalSentryPlayerHit(SentryEntity sentry, PlayerEntity owner, PlayerEntity target, int damage, PlayerDamageTraits additionalTraits, bool criticalBoost, bool useLiveAttackerCriticalBoost, float? threatSourceX, float? threatSourceY, BulletKnockbackPayload? knockbackPayload, float? impactDirectionX, float? impactDirectionY)
        => ExperimentalRules.ApplyExperimentalSentryPlayerHit(sentry, owner, target, damage, additionalTraits, criticalBoost, useLiveAttackerCriticalBoost, threatSourceX, threatSourceY, knockbackPayload, impactDirectionX, impactDirectionY);
    void IProjectileGameplayRules.ApplyExperimentalSentryDamageRewards(SentryEntity sentry, PlayerEntity owner, int damage)
        => ExperimentalRules.ApplyExperimentalSentryDamageRewards(sentry, owner, damage);
    bool IProjectileGameplayRules.TryResolveExperimentalEngineerRocketTrackingDirection(RocketProjectileEntity rocket, PlayerEntity player, out float directionRadians)
        => ExperimentalRules.TryResolveExperimentalEngineerRocketTrackingDirection(rocket, player, out directionRadians);
    void IProjectileGameplayRules.TrySpawnExperimentalDemoknightDecapitationRemains(PlayerEntity player, float directionX, float directionY)
        => PlayerRemains.TrySpawnExperimentalDemoknightDecapitationRemains(player, directionX, directionY);
    void IProjectileGameplayRules.ApplyMedicHealNeedleTeammateHit(PlayerEntity? medic, PlayerEntity target, MedicHealNeedleProjectileEntity needle)
        => SupportRules.ApplyMedicHealNeedleTeammateHit(medic, target, needle);
    void IProjectileGameplayRules.ExplodeBoomstickPellet(ShotProjectileEntity shot) => ExplodeBoomstickPellet(shot);
    bool IProjectileGameplayRules.TryApplyLastToDieSniperGuardian(PlayerEntity sniper, PlayerEntity target)
        => LastToDieRules.TryApplyLastToDieSniperGuardian(sniper, target);
    void IProjectileGameplayRules.TryApplyLastToDieSniperStatusPayload(PlayerEntity owner, PlayerEntity target, bool tranquilize, float poison)
        => LastToDieRules.TryApplyLastToDieSniperStatusPayload(owner, target, tranquilize, poison);
    bool IProjectileGameplayRules.TryApplyLastToDieStatusEffect(int targetId, int sourceId, LastToDieStatusEffectSpec spec)
        => LastToDieRules.TryApplyLastToDieStatusEffect(targetId, sourceId, spec);
    LastToDieMedicKritzM2Payload IProjectileGameplayRules.CaptureLastToDieMedicKritzM2Payload(PlayerEntity owner)
        => LastToDieRules.CaptureLastToDieMedicKritzM2Payload(owner);
    bool IProjectileGameplayRules.TryExplodeLastToDieSniperArrow(ArrowProjectileEntity arrow, float x, float y)
        => LastToDieRules.TryExplodeLastToDieSniperArrow(arrow, x, y);
    bool IProjectileGameplayRules.TryExplodeLastToDieMedicJavelin(MedicHealNeedleProjectileEntity needle)
        => LastToDieRules.TryExplodeLastToDieMedicJavelin(needle);
}
