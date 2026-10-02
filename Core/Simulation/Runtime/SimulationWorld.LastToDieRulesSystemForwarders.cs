using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

// Thin forwarders so remaining world code (and reflection-based tests) keep their call shape;
// callers should migrate to the system directly over time.
public sealed partial class SimulationWorld
{
    public bool TryApplyLastToDieStatusEffect(
        int targetPlayerId,
        int? sourcePlayerId,
        LastToDieStatusEffectSpec requestedSpec,
        int? assistingMedicPlayerId = null)
        => LastToDieRules.TryApplyLastToDieStatusEffect(targetPlayerId, sourcePlayerId, requestedSpec, assistingMedicPlayerId);

    public IReadOnlyList<LastToDieActiveStatusEffectSnapshot> GetLastToDieStatusEffects(int targetPlayerId)
        => LastToDieRules.GetLastToDieStatusEffects(targetPlayerId);

    internal bool TryGetLastToDieMartyrProtector(PlayerEntity protectedTarget, out PlayerEntity protector)
        => LastToDieRules.TryGetLastToDieMartyrProtector(protectedTarget, out protector);

    private void TryApplyLastToDieSniperStatusPayload(PlayerEntity source, PlayerEntity target, bool appliesTranqDarts, float poisonTipDamagePerSecond)
        => LastToDieRules.TryApplyLastToDieSniperStatusPayload(source, target, appliesTranqDarts, poisonTipDamagePerSecond);
    private bool TryApplyLastToDieSniperGuardian(PlayerEntity sniper, PlayerEntity target) => LastToDieRules.TryApplyLastToDieSniperGuardian(sniper, target);
    private void BeginLastToDieStatusEffectsTick() => LastToDieRules.BeginLastToDieStatusEffectsTick();
    private void EndLastToDieStatusEffectsTick() => LastToDieRules.EndLastToDieStatusEffectsTick();
    private void ClearLastToDieStatusEffectsForTarget(int targetPlayerId) => LastToDieRules.ClearLastToDieStatusEffectsForTarget(targetPlayerId);
    private void ClearLastToDieStatusEffectsForReleasedPlayer(int playerId) => LastToDieRules.ClearLastToDieStatusEffectsForReleasedPlayer(playerId);
    private void RefreshLastToDieMedicLinkProjections() => LastToDieRules.RefreshLastToDieMedicLinkProjections();
    private bool TryApplyLastToDieMedicSupportRelay(PlayerEntity medic, PlayerEntity target) => LastToDieRules.TryApplyLastToDieMedicSupportRelay(medic, target);
    private bool TryResolveLastToDieExsanguinationMedic(PlayerEntity attacker, out PlayerEntity medic) => LastToDieRules.TryResolveLastToDieExsanguinationMedic(attacker, out medic);
    private PlayerEntity? ResolveLastToDieMedicLinkedOnHit(PlayerEntity? attacker, PlayerEntity target, int appliedDamage, PlayerDamageTraits damageTraits)
        => LastToDieRules.ResolveLastToDieMedicLinkedOnHit(attacker, target, appliedDamage, damageTraits);
    private static int ResolveLastToDieMedicLinkedAssistPlayerId(PlayerEntity? attacker, PlayerEntity? medic)
        => LastToDieRulesSystem.ResolveLastToDieMedicLinkedAssistPlayerId(attacker, medic);
    private void ApplyLastToDieMedicLinkedOnHitEffects(PlayerEntity? attacker, PlayerEntity target, PlayerEntity? medic)
        => LastToDieRules.ApplyLastToDieMedicLinkedOnHitEffects(attacker, target, medic);

    // Perks, afterlife, javelin, sniper explosive tip, survivor/stage objectives.
    private void AdvanceLastToDiePassivePerks(byte slot, PlayerEntity player)
        => LastToDieRules.AdvanceLastToDiePassivePerks(slot, player);
    private void ApplyLastToDieDamageRewards(PlayerEntity? attacker, PlayerEntity target, int appliedDamage, PlayerDamageTraits damageTraits)
        => LastToDieRules.ApplyLastToDieDamageRewards(attacker, target, appliedDamage, damageTraits);
    private void ApplyLastToDieDamageTakenEffects(PlayerEntity target, PlayerEntity? attacker, int appliedDamage, PlayerDamageTraits damageTraits)
        => LastToDieRules.ApplyLastToDieDamageTakenEffects(target, attacker, appliedDamage, damageTraits);
    private int ApplyLastToDieIncomingDamageMultiplier(PlayerEntity target, int damage, PlayerDamageTraits damageTraits)
        => LastToDieRules.ApplyLastToDieIncomingDamageMultiplier(target, damage, damageTraits);
    private float ApplyLastToDieIncomingDamageMultiplier(PlayerEntity target, float damage, PlayerDamageTraits damageTraits)
        => LastToDieRules.ApplyLastToDieIncomingDamageMultiplier(target, damage, damageTraits);
    private void ApplyLastToDieKillRewards(PlayerEntity killer)
        => LastToDieRules.ApplyLastToDieKillRewards(killer);
    private void ApplyLastToDieMedicHomeostasis(PlayerEntity medic, int appliedTargetHealing)
        => LastToDieRules.ApplyLastToDieMedicHomeostasis(medic, appliedTargetHealing);
    private int ApplyLastToDieOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, int damage, PlayerDamageTraits damageTraits, bool? attackerWasGrounded, bool? targetWasGrounded)
        => LastToDieRules.ApplyLastToDieOutgoingDamageMultiplier(attacker, target, damage, damageTraits, attackerWasGrounded, targetWasGrounded);
    private float ApplyLastToDieOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, float damage, PlayerDamageTraits damageTraits, bool? attackerWasGrounded, bool? targetWasGrounded)
        => LastToDieRules.ApplyLastToDieOutgoingDamageMultiplier(attacker, target, damage, damageTraits, attackerWasGrounded, targetWasGrounded);
    public bool CanPlayerCaptureControlPointsWhileCloaked(PlayerEntity player)
        => LastToDieRules.CanPlayerCaptureControlPointsWhileCloaked(player);
    public bool CanPlayerCaptureControlPointsWhileUbered(PlayerEntity player)
        => LastToDieRules.CanPlayerCaptureControlPointsWhileUbered(player);
    public bool CanPlayerContributeToControlPoint(PlayerEntity player)
        => LastToDieRules.CanPlayerContributeToControlPoint(player);
    private LastToDieMedicKritzM2Payload CaptureLastToDieMedicKritzM2Payload(PlayerEntity owner)
        => LastToDieRules.CaptureLastToDieMedicKritzM2Payload(owner);
    public bool ClearLastToDiePlayerPredictionProfile(byte slot)
        => LastToDieRules.ClearLastToDiePlayerPredictionProfile(slot);
    private void ClearLastToDieSniperMarksTargeting(byte targetSlot)
        => LastToDieRules.ClearLastToDieSniperMarksTargeting(targetSlot);
    public void ConfigureLastToDieCombatSeed(ulong seed)
        => LastToDieRules.ConfigureLastToDieCombatSeed(seed);
    private static int GetLastToDieBaseMaximumHealth(PlayerEntity player)
        => LastToDieRulesSystem.GetLastToDieBaseMaximumHealth(player);
    private float GetLastToDieEvasionChance(PlayerEntity target)
        => LastToDieRules.GetLastToDieEvasionChance(target);
    private ExperimentalGameplaySettings GetLastToDieGameplaySettings(PlayerEntity? player)
        => LastToDieRules.GetLastToDieGameplaySettings(player);
    private float GetLastToDieMedicHealingMultiplier(PlayerEntity medic, PlayerEntity target)
        => LastToDieRules.GetLastToDieMedicHealingMultiplier(medic, target);
    private static float GetLastToDieMedicKritzCriticalDamageMultiplier(PlayerEntity medic)
        => LastToDieRulesSystem.GetLastToDieMedicKritzCriticalDamageMultiplier(medic);
    private float GetLastToDieMedicUberChargeGainMultiplier(PlayerEntity medic)
        => LastToDieRules.GetLastToDieMedicUberChargeGainMultiplier(medic);
    internal bool IsLastToDieGameplaySettingEnabled(Func<ExperimentalGameplaySettings, bool> selector)
        => LastToDieRules.IsLastToDieGameplaySettingEnabled(selector);
    public void ResetLastToDieClientSession()
        => LastToDieRules.ResetLastToDieClientSession();
    private void ResetLastToDiePerkRuntimeOnDeath(byte slot)
        => LastToDieRules.ResetLastToDiePerkRuntimeOnDeath(slot);
    private bool RollLastToDieEvasion(PlayerEntity target, float totalEvasionChance)
        => LastToDieRules.RollLastToDieEvasion(target, totalEvasionChance);
    private bool TryActivateLastToDieSecondChance(PlayerEntity player)
        => LastToDieRules.TryActivateLastToDieSecondChance(player);
    public bool TryApplyLastToDiePlayerPredictionProfile(byte slot, IEnumerable<string> ownedPerkIds, int runKills = 0, bool secondChanceConsumed = false)
        => LastToDieRules.TryApplyLastToDiePlayerPredictionProfile(slot, ownedPerkIds, runKills, secondChanceConsumed);
    public bool TryConfigureLastToDiePlayerBuild(byte slot, IEnumerable<LastToDiePerkId> perks, int? baseMaximumHealthOverride = null, bool refillHealth = false, bool resetDynamicState = false, int runKills = 0, bool secondChanceConsumed = false, bool runKillProgressionOwner = true)
        => LastToDieRules.TryConfigureLastToDiePlayerBuild(slot, perks, baseMaximumHealthOverride, refillHealth, resetDynamicState, runKills, secondChanceConsumed, runKillProgressionOwner);
    private bool TryGetLastToDieLegacyGameplaySettings(byte slot, out ExperimentalGameplaySettings settings)
        => LastToDieRules.TryGetLastToDieLegacyGameplaySettings(slot, out settings);
    public bool TryGetLastToDiePlayerModifiers(byte slot, out LastToDieDerivedModifiers modifiers)
        => LastToDieRules.TryGetLastToDiePlayerModifiers(slot, out modifiers);
    public bool TryGetLastToDieSecondChanceConsumed(byte slot, out bool consumed)
        => LastToDieRules.TryGetLastToDieSecondChanceConsumed(slot, out consumed);
    public bool TryGetLastToDieSniperConquistadorStacks(byte slot, out int stacks)
        => LastToDieRules.TryGetLastToDieSniperConquistadorStacks(slot, out stacks);
    private void TryRegisterLastToDieSniperConquistadorKill(PlayerEntity killer, PlayerEntity victim)
        => LastToDieRules.TryRegisterLastToDieSniperConquistadorKill(killer, victim);
    public bool TryRestoreLastToDieSniperConquistadorStacks(byte slot, int stacks)
        => LastToDieRules.TryRestoreLastToDieSniperConquistadorStacks(slot, stacks);
    internal bool TryRollLastToDieSpyDeadlyCritical(PlayerEntity attacker)
        => LastToDieRules.TryRollLastToDieSpyDeadlyCritical(attacker);
    public bool TrySetLastToDiePlayerRunKills(byte slot, int runKills)
        => LastToDieRules.TrySetLastToDiePlayerRunKills(slot, runKills);
    public bool ConsumeLastToDieSpyAfterlifeDisconnectFailure(byte slot)
        => LastToDieRules.ConsumeLastToDieSpyAfterlifeDisconnectFailure(slot);
    public bool IsLastToDieSpyAfterlifeWindowActive(byte slot)
        => LastToDieRules.IsLastToDieSpyAfterlifeWindowActive(slot);
    private bool TryCompleteExpiredLastToDieSpyAfterlife(PlayerEntity player)
        => LastToDieRules.TryCompleteExpiredLastToDieSpyAfterlife(player);
    private void TryCompleteLastToDieSpyAfterlifeSuccess(PlayerEntity killer, PlayerEntity victim)
        => LastToDieRules.TryCompleteLastToDieSpyAfterlifeSuccess(killer, victim);
    private bool TryFailLastToDieSpyAfterlifeOnDisconnect(byte slot, PlayerEntity player)
        => LastToDieRules.TryFailLastToDieSpyAfterlifeOnDisconnect(slot, player);
    private bool TryStartLastToDieSpyAfterlife(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName, DeadBodyAnimationKind deadBodyAnimationKind, string? deathCamMessage, SentryEntity? deathCamSentry, string? killFeedMessage, bool createDeathCam, bool spawnRemains, bool forceCorpseRemains, bool recordKillFeed, int assistingPlayerId)
        => LastToDieRules.TryStartLastToDieSpyAfterlife(player, gibbed, killer, weaponSpriteName, deadBodyAnimationKind, deathCamMessage, deathCamSentry, killFeedMessage, createDeathCam, spawnRemains, forceCorpseRemains, recordKillFeed, assistingPlayerId);
    private bool TryExplodeLastToDieMedicJavelin(MedicHealNeedleProjectileEntity needle)
        => LastToDieRules.TryExplodeLastToDieMedicJavelin(needle);
    private bool DetonateOwnedLastToDieSniperArrows(PlayerEntity owner)
        => LastToDieRules.DetonateOwnedLastToDieSniperArrows(owner);
    private bool HasOwnedLastToDieSniperExplosiveArrow(PlayerEntity owner)
        => LastToDieRules.HasOwnedLastToDieSniperExplosiveArrow(owner);
    private bool TryExplodeLastToDieSniperArrow(ArrowProjectileEntity arrow, float? explosionX = null, float? explosionY = null)
        => LastToDieRules.TryExplodeLastToDieSniperArrow(arrow, explosionX, explosionY);
    private bool TryExplodeLastToDieSniperRifleImpact(PlayerEntity owner, float x, float y, bool isCritical, float criticalDamageMultiplier)
        => LastToDieRules.TryExplodeLastToDieSniperRifleImpact(owner, x, y, isCritical, criticalDamageMultiplier);
    public void ConfigureLastToDieStage(int stageNumber)
        => LastToDieRules.ConfigureLastToDieStage(stageNumber);
    public bool TrySetLastToDieSurvivorBuff(byte slot, bool enabled)
        => LastToDieRules.TrySetLastToDieSurvivorBuff(slot, enabled);
    public int CountOwnedLastToDieSniperExplosiveArrows(PlayerEntity owner)
        => LastToDieRules.CountOwnedLastToDieSniperExplosiveArrows(owner);
    public bool CanCompleteLastToDieStageOnTimeout => LastToDieRules.CanCompleteLastToDieStageOnTimeout;
}
