namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IExplosionRulesHost
{
    int IExplosionRulesHost.ApplyExperimentalAirshotDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, int baseDamage, out DamageEventFlags damageFlags)
        => ExperimentalRules.ApplyExperimentalAirshotDamageMultiplier(attacker, target, baseDamage, out damageFlags);
    void IExplosionRulesHost.ApplyExplosiveDamageToDamageableZones(float originX, float originY, float blastRadius, float damage, float splashThresholdFactor, int excludeRoomObjectIndex, PlayerTeam? damagingTeam, float minimumSplashDamage)
        => MapLogic.ApplyExplosiveDamageToDamageableZones(originX, originY, blastRadius, damage, splashThresholdFactor, excludeRoomObjectIndex, damagingTeam, minimumSplashDamage);
    int IExplosionRulesHost.ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName, float soundX, float soundY)
        => DamageRules.ApplyHealingWithFeedback(target, healing, soundName, soundX, soundY);
    bool IExplosionRulesHost.ApplyPlayerContinuousDamageWithContext(PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost, bool civvieUmbrellaUseLiveAttackerCriticalBoost, PlayerDamageTraits additionalTraits, bool? attackerWasGrounded, bool? targetWasGrounded)
        => ApplyPlayerContinuousDamageWithContext(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost, civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded, targetWasGrounded);
    bool IExplosionRulesHost.ApplyPlayerDamageWithContext(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost, bool civvieUmbrellaUseLiveAttackerCriticalBoost, PlayerDamageTraits additionalTraits, bool? attackerWasGrounded, bool? targetWasGrounded, int sourceEntityId, ulong attackId, int attackerPlayerIdOverride)
        => ApplyPlayerDamageWithContext(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost, civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded, targetWasGrounded, sourceEntityId, attackId, attackerPlayerIdOverride);
    bool IExplosionRulesHost.ApplySentryDamage(SentryEntity target, int damage, PlayerEntity? attacker)
        => ApplySentryDamage(target, damage, attacker);
    void IExplosionRulesHost.AwardHealingPoints(PlayerEntity healer, int healedAmount)
        => Scorekeeping.AwardHealingPoints(healer, healedAmount);
    IReadOnlyList<BubbleProjectileEntity> IExplosionRulesHost.Bubbles => Bubbles;
    bool IExplosionRulesHost.ClientPredictionMode => ClientPredictionMode;
    CombatRuntimeState IExplosionRulesHost.CombatRuntime => CombatRuntime;
    void IExplosionRulesHost.DestroyJumpPad(JumpPadEntity pad)
        => Structures.DestroyJumpPad(pad);
    void IExplosionRulesHost.DestroySentry(SentryEntity sentry, PlayerEntity? attacker)
        => Structures.DestroySentry(sentry, attacker);
    ExperimentalGameplaySettings IExplosionRulesHost.ExperimentalGameplaySettings => ExperimentalGameplaySettings;
    void IExplosionRulesHost.GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom)
        => PresentationBounds.GetCachedPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
    ExperimentalGameplaySettings IExplosionRulesHost.GetLastToDieGameplaySettings(PlayerEntity? player)
        => LastToDieRules.GetLastToDieGameplaySettings(player);
    bool IExplosionRulesHost.IsExperimentalPracticePowerOwner(PlayerEntity? player)
        => ExperimentalRules.IsExperimentalPracticePowerOwner(player);
    void IExplosionRulesHost.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerIdOverride, bool completingLastToDieSpyAfterlifeDeath)
        => PlayerDeaths.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerIdOverride, completingLastToDieSpyAfterlifeDeath);
    IReadOnlyList<MineProjectileEntity> IExplosionRulesHost.Mines => Mines;
    SimulationRandomStreams IExplosionRulesHost.Randoms => Randoms;
    void IExplosionRulesHost.RegisterBloodEffect(float x, float y, float directionDegrees, int count)
        => WorldEffects.RegisterBloodEffect(x, y, directionDegrees, count);
    void IExplosionRulesHost.RegisterCombatTrace(float originX, float originY, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team, bool isSniperTracer, bool isCritical)
        => WorldEffects.RegisterCombatTrace(originX, originY, directionX, directionY, distance, hitCharacter, team, isSniperTracer, isCritical);
    void IExplosionRulesHost.RegisterVisualEffect(string effectName, float x, float y, float directionDegrees, int count, bool normalizeDirection)
        => WorldEffects.RegisterVisualEffect(effectName, x, y, directionDegrees, count, normalizeDirection);
    void IExplosionRulesHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId)
        => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    void IExplosionRulesHost.RemoveBubbleAt(int index)
        => RemoveBubbleAt(index);
    void IExplosionRulesHost.RemoveMineAt(int index)
        => RemoveMineAt(index);
    void IExplosionRulesHost.RemoveRocketAt(int index)
        => RemoveRocketAt(index);
    IReadOnlyList<RocketProjectileEntity> IExplosionRulesHost.Rockets => Rockets;
    bool IExplosionRulesHost.TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? damagingTeam)
        => MapLogic.TryApplyDamageableZoneDamage(roomObjectIndex, damage, damagingTeam);
    void IExplosionRulesHost.TryApplyExperimentalSoldierRocketHitReloadReward(PlayerEntity? attacker, RocketProjectileEntity rocket, bool hitEnemyPlayer)
        => ExperimentalRules.TryApplyExperimentalSoldierRocketHitReloadReward(attacker, rocket, hitEnemyPlayer);
    bool IExplosionRulesHost.TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker)
        => ObjectiveRules.TryDamageGenerator(targetTeam, damage, attacker);
    WorldObjectStore IExplosionRulesHost.WorldObjects => WorldObjects;
}
