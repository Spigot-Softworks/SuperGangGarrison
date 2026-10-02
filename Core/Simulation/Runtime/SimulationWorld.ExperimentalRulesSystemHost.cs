namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IExperimentalRulesHost
{
    int IExperimentalRulesHost.ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName, float soundX, float soundY)
        => DamageRules.ApplyHealingWithFeedback(target, healing, soundName, soundX, soundY);
    bool IExperimentalRulesHost.ApplySentryDamage(SentryEntity target, int damage, PlayerEntity? attacker)
        => ApplySentryDamage(target, damage, attacker);
    void IExperimentalRulesHost.DestroySentry(SentryEntity sentry, PlayerEntity? attacker)
        => Structures.DestroySentry(sentry, attacker);
    void IExperimentalRulesHost.FlushExperimentalEngineerEssenceExtractorHealing(PlayerEntity engineer)
        => SupportRules.FlushExperimentalEngineerEssenceExtractorHealing(engineer);
    bool IExperimentalRulesHost.IsLastToDieDroneSentry(SentryEntity sentry)
        => Structures.IsLastToDieDroneSentry(sentry);
    bool IExperimentalRulesHost.IsProjectileSpawnBlocked(float originX, float originY, float targetX, float targetY, PlayerTeam shotTeam)
        => IsProjectileSpawnBlocked(originX, originY, targetX, targetY, shotTeam);
    int IExperimentalRulesHost.ReflectEnemyExplosiveProjectiles(PlayerEntity player, float aimRadians, float poofX, float poofY, bool radial, float radialRadius)
        => AirblastRules.ReflectEnemyExplosiveProjectiles(player, aimRadians, poofX, poofY, radial, radialRadius);
    void IExperimentalRulesHost.RegisterBloodEffect(float x, float y, float directionDegrees, int count)
        => WorldEffects.RegisterBloodEffect(x, y, directionDegrees, count);
    void IExperimentalRulesHost.RegisterCombatTrace(float originX, float originY, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team, bool isSniperTracer, bool isCritical)
        => WorldEffects.RegisterCombatTrace(originX, originY, directionX, directionY, distance, hitCharacter, team, isSniperTracer, isCritical);
    void IExperimentalRulesHost.RegisterImpactEffect(float x, float y, float directionDegrees)
        => WorldEffects.RegisterImpactEffect(x, y, directionDegrees);
    void IExperimentalRulesHost.RegisterVisualEffect(string effectName, float x, float y, float directionDegrees, int count, bool normalizeDirection)
        => WorldEffects.RegisterVisualEffect(effectName, x, y, directionDegrees, count, normalizeDirection);
    void IExperimentalRulesHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId)
        => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    PlayerDamageResolution IExperimentalRulesHost.ResolvePlayerDamageWithContext(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost, bool civvieUmbrellaUseLiveAttackerCriticalBoost, PlayerDamageTraits additionalTraits, bool? attackerWasGrounded, bool? targetWasGrounded, int sourceEntityId, ulong attackId, int attackerPlayerIdOverride)
        => ResolvePlayerDamageWithContext(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost, civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded, targetWasGrounded, sourceEntityId, attackId, attackerPlayerIdOverride);
    RifleHitResult IExperimentalRulesHost.ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance)
        => ResolveRifleHit(attacker, directionX, directionY, maxDistance);
    RifleHitResult IExperimentalRulesHost.ResolveRifleHit(PlayerEntity attacker, float originX, float originY, float directionX, float directionY, float maxDistance)
        => ResolveRifleHit(attacker, originX, originY, directionX, directionY, maxDistance);
    void IExperimentalRulesHost.SpawnFlame(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float directHitDamage, float burnDamagePerTick)
        => SpawnFlame(owner, x, y, velocityX, velocityY, directHitDamage, burnDamagePerTick);
    void IExperimentalRulesHost.SpawnRocket(PlayerEntity owner, float x, float y, float speed, float directionRadians, RocketCombatDefinition? rocketCombat, float directHitHealAmount, bool explodeImmediately, bool canGrantExperimentalInstantReloadOnHit, float knockbackScale, bool canIgniteTargets, bool enableExperimentalStingerTracking, bool enableExperimentalCaveatTracking, float experimentalVisualScale, int experimentalTrackingLockTicksRemaining, bool isBallistic, float ballisticGravityPerTick, bool suppressSmokeTrail, string? killFeedWeaponSpriteNameOverride)
        => SpawnRocket(owner, x, y, speed, directionRadians, rocketCombat, directHitHealAmount, explodeImmediately, canGrantExperimentalInstantReloadOnHit, knockbackScale, canIgniteTargets, enableExperimentalStingerTracking, enableExperimentalCaveatTracking, experimentalVisualScale, experimentalTrackingLockTicksRemaining, isBallistic, ballisticGravityPerTick, suppressSmokeTrail, killFeedWeaponSpriteNameOverride);
    void IExperimentalRulesHost.SpawnShot(PlayerEntity owner, float x, float y, float velocityX, float velocityY, float damagePerHit, bool forceGibOnKill, string? killFeedWeaponSpriteNameOverride, int? sourceSentryId, bool applyExperimentalEngineerSentryPerkEffects, float playerKnockbackScale, float? playerSlowMovementMultiplier, int playerSlowRefreshTicks, float? playerKnockbackImpulse, float playerKnockbackAirborneVerticalScale, float playerKnockbackGroundedVerticalScale, bool isBoomstickPellet)
        => SpawnShot(owner, x, y, velocityX, velocityY, damagePerHit, forceGibOnKill, killFeedWeaponSpriteNameOverride, sourceSentryId, applyExperimentalEngineerSentryPerkEffects, playerKnockbackScale, playerSlowMovementMultiplier, playerSlowRefreshTicks, playerKnockbackImpulse, playerKnockbackAirborneVerticalScale, playerKnockbackGroundedVerticalScale, isBoomstickPellet);
    bool IExperimentalRulesHost.TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? damagingTeam)
        => MapLogic.TryApplyDamageableZoneDamage(roomObjectIndex, damage, damagingTeam);
    bool IExperimentalRulesHost.TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker)
        => ObjectiveRules.TryDamageGenerator(targetTeam, damage, attacker);
    void IExperimentalRulesHost.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerIdOverride, bool completingLastToDieSpyAfterlifeDeath)
        => PlayerDeaths.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerIdOverride, completingLastToDieSpyAfterlifeDeath);
    bool IExperimentalRulesHost.ApplyPlayerDamageWithContext(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost, bool civvieUmbrellaUseLiveAttackerCriticalBoost, PlayerDamageTraits additionalTraits, bool? attackerWasGrounded, bool? targetWasGrounded, int sourceEntityId, ulong attackId, int attackerPlayerIdOverride)
        => ApplyPlayerDamageWithContext(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost, civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded, targetWasGrounded, sourceEntityId, attackId, attackerPlayerIdOverride);
    ExperimentalGameplaySettings IExperimentalRulesHost.GetLastToDieGameplaySettings(PlayerEntity? player)
        => LastToDieRules.GetLastToDieGameplaySettings(player);
    bool IExperimentalRulesHost.IsLastToDieGameplaySettingEnabled(Func<ExperimentalGameplaySettings, bool> selector)
        => LastToDieRules.IsLastToDieGameplaySettingEnabled(selector);
    bool IExperimentalRulesHost.TryGetLastToDieLegacyGameplaySettings(byte slot, out ExperimentalGameplaySettings settings)
        => LastToDieRules.TryGetLastToDieLegacyGameplaySettings(slot, out settings);
    bool IExperimentalRulesHost.TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot)
        => NetworkPlayerRules.TryGetPlayerNetworkSlot(player, out slot);
    WorldBounds IExperimentalRulesHost.Bounds => Bounds;
    SimulationRandomStreams IExperimentalRulesHost.Randoms => Randoms;
    IReadOnlyList<RocketProjectileEntity> IExperimentalRulesHost.Rockets => Rockets;
    WorldObjectStore IExperimentalRulesHost.WorldObjects => WorldObjects;
    PlayerEntity IExperimentalRulesHost.LocalPlayer => LocalPlayer;
    CombatRuntimeState IExperimentalRulesHost.CombatRuntime => CombatRuntime;
    MatchSettingsState IExperimentalRulesHost.MatchSettings => MatchSettings;
    ObjectiveStateStore IExperimentalRulesHost.Objectives => Objectives;
    ExperimentalGameplaySettings IExperimentalRulesHost.ExperimentalGameplaySettings { get => ExperimentalGameplaySettings; set => ExperimentalGameplaySettings = value; }
    void IExperimentalRulesHost.ApplyNetworkPlayerMaxHealthOverride(byte slot, PlayerEntity player, bool refillHealth) => ServerTuning.ApplyNetworkPlayerMaxHealthOverride(slot, player, refillHealth);
    void IExperimentalRulesHost.ClearTemporaryHealthPacks() => Pickups.ClearTemporaryHealthPacks();
    void IExperimentalRulesHost.ClearDroppedWeapons() => Pickups.ClearDroppedWeapons();
    bool IExperimentalRulesHost.IsNetworkPlayerEnabled(byte slot) => NetworkPlayerRules.IsNetworkPlayerEnabled(slot);
    bool IExperimentalRulesHost.TryGetNetworkPlayer(byte slot, out PlayerEntity player) => NetworkPlayerRules.TryGetNetworkPlayer(slot, out player);
}
