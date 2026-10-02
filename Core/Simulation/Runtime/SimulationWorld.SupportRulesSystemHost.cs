namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ISupportRulesHost
{
    int ISupportRulesHost.ApplyHealingWithFeedback(PlayerEntity target, float healing, string? soundName, float soundX, float soundY)
        => DamageRules.ApplyHealingWithFeedback(target, healing, soundName, soundX, soundY);
    void ISupportRulesHost.ApplyLastToDieMedicHomeostasis(PlayerEntity medic, int appliedTargetHealing)
        => LastToDieRules.ApplyLastToDieMedicHomeostasis(medic, appliedTargetHealing);
    bool ISupportRulesHost.ApplyPlayerContinuousDamage(PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost)
        => ApplyPlayerContinuousDamage(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost);
    void ISupportRulesHost.AwardHealingPoints(PlayerEntity healer, int healedAmount)
        => Scorekeeping.AwardHealingPoints(healer, healedAmount);
    bool ISupportRulesHost.CanPlayerDamagePlayer(PlayerEntity attacker, PlayerEntity target)
        => DamageRules.CanPlayerDamagePlayer(attacker, target);
    bool ISupportRulesHost.ControlPointSetupActive => ControlPointSetupActive;
    void ISupportRulesHost.GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom)
        => PresentationBounds.GetCachedPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
    int ISupportRulesHost.GetExperimentalEngineerCryoFreezeDurationTicks()
        => ExperimentalRules.GetExperimentalEngineerCryoFreezeDurationTicks();
    int ISupportRulesHost.GetExperimentalEngineerEssenceExtractorDebuffTicks()
        => ExperimentalRules.GetExperimentalEngineerEssenceExtractorDebuffTicks();
    int ISupportRulesHost.GetExperimentalEngineerFreezeRayExposureWindowTicks()
        => ExperimentalRules.GetExperimentalEngineerFreezeRayExposureWindowTicks();
    int ISupportRulesHost.GetExperimentalEngineerFreezeRayFreezeThresholdTicks()
        => ExperimentalRules.GetExperimentalEngineerFreezeRayFreezeThresholdTicks();
    int ISupportRulesHost.GetExperimentalEngineerFreezeRaySlowTicks()
        => ExperimentalRules.GetExperimentalEngineerFreezeRaySlowTicks();
    float ISupportRulesHost.GetLastToDieMedicHealingMultiplier(PlayerEntity medic, PlayerEntity target)
        => LastToDieRules.GetLastToDieMedicHealingMultiplier(medic, target);
    float ISupportRulesHost.GetLastToDieMedicUberChargeGainMultiplier(PlayerEntity medic)
        => LastToDieRules.GetLastToDieMedicUberChargeGainMultiplier(medic);
    float? ISupportRulesHost.GetThickLineIntersectionDistanceToPlayer(float originX, float originY, float endX, float endY, PlayerEntity player, float maxDistance, float thicknessRadius)
        => GetThickLineIntersectionDistanceToPlayer(originX, originY, endX, endY, player, maxDistance, thicknessRadius);
    bool ISupportRulesHost.HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        => HasObstacleLineOfSight(originX, originY, targetX, targetY);
    void ISupportRulesHost.KillPlayer(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerIdOverride, bool completingLastToDieSpyAfterlifeDeath)
        => PlayerDeaths.KillPlayer(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerIdOverride, completingLastToDieSpyAfterlifeDeath);
    PlayerEntity ISupportRulesHost.LocalPlayer => LocalPlayer;
    void ISupportRulesHost.RegisterBloodEffect(float x, float y, float directionDegrees, int count)
        => WorldEffects.RegisterBloodEffect(x, y, directionDegrees, count);
    void ISupportRulesHost.RegisterHealingFeedbackOnly(PlayerEntity target, int amount)
        => DamageRules.RegisterHealingFeedbackOnly(target, amount);
    void ISupportRulesHost.RegisterVisualEffect(string effectName, float x, float y, float directionDegrees, int count, bool normalizeDirection)
        => WorldEffects.RegisterVisualEffect(effectName, x, y, directionDegrees, count, normalizeDirection);
    void ISupportRulesHost.RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId)
        => WorldEffects.RegisterWorldSoundEvent(soundName, x, y, sourcePlayerId);
    bool ISupportRulesHost.TryApplyLastToDieMedicSupportRelay(PlayerEntity medic, PlayerEntity target)
        => LastToDieRules.TryApplyLastToDieMedicSupportRelay(medic, target);
    bool ISupportRulesHost.TryGetPlayerNetworkSlot(PlayerEntity player, out byte slot)
        => NetworkPlayerRules.TryGetPlayerNetworkSlot(player, out slot);
}
