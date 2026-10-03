namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    int ICombatDamageModifiers.ScaleConfiguredDamage(int damage) => DamageRules.ScaleConfiguredDamage(damage);
    float ICombatDamageModifiers.ScaleConfiguredDamage(float damage) => DamageRules.ScaleConfiguredDamage(damage);
    int ICombatDamageModifiers.ApplyExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, int damage)
        => ExperimentalRules.ApplyExperimentalOutgoingDamageMultiplier(attacker, target, damage);
    float ICombatDamageModifiers.ApplyExperimentalOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, float damage)
        => ExperimentalRules.ApplyExperimentalOutgoingDamageMultiplier(attacker, target, damage);
    int ICombatDamageModifiers.ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, PlayerEntity? attacker, int damage)
        => ExperimentalRules.ApplyExperimentalIncomingDamageMultiplier(target, attacker, damage);
    float ICombatDamageModifiers.ApplyExperimentalIncomingDamageMultiplier(PlayerEntity target, PlayerEntity? attacker, float damage)
        => ExperimentalRules.ApplyExperimentalIncomingDamageMultiplier(target, attacker, damage);
    int ICombatDamageModifiers.ApplyLastToDieOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, int damage, PlayerDamageTraits traits, bool? attackerWasGrounded, bool? targetWasGrounded)
        => LastToDieRules.ApplyLastToDieOutgoingDamageMultiplier(attacker, target, damage, traits, attackerWasGrounded, targetWasGrounded);
    float ICombatDamageModifiers.ApplyLastToDieOutgoingDamageMultiplier(PlayerEntity? attacker, PlayerEntity target, float damage, PlayerDamageTraits traits, bool? attackerWasGrounded, bool? targetWasGrounded)
        => LastToDieRules.ApplyLastToDieOutgoingDamageMultiplier(attacker, target, damage, traits, attackerWasGrounded, targetWasGrounded);
    int ICombatDamageModifiers.ApplyLastToDieIncomingDamageMultiplier(PlayerEntity target, int damage, PlayerDamageTraits traits)
        => LastToDieRules.ApplyLastToDieIncomingDamageMultiplier(target, damage, traits);
    float ICombatDamageModifiers.ApplyLastToDieIncomingDamageMultiplier(PlayerEntity target, float damage, PlayerDamageTraits traits)
        => LastToDieRules.ApplyLastToDieIncomingDamageMultiplier(target, damage, traits);
    int ICombatDamageModifiers.ApplyExperimentalIncomingSentryDamageMultiplier(SentryEntity target, int damage)
        => ExperimentalRules.ApplyExperimentalIncomingSentryDamageMultiplier(target, damage);
    float ICombatDamageModifiers.GetExperimentalTotalEvasionChance(PlayerEntity target) => ExperimentalRules.GetExperimentalTotalEvasionChance(target);
    float ICombatDamageModifiers.GetLastToDieEvasionChance(PlayerEntity target) => LastToDieRules.GetLastToDieEvasionChance(target);
    bool ICombatDamageModifiers.RollLastToDieEvasion(PlayerEntity target, float totalEvasionChance) => LastToDieRules.RollLastToDieEvasion(target, totalEvasionChance);

    bool ICombatDamageInterceptors.ShouldCancelDamage(DamageTargetKind targetKind, int targetEntityId, int targetPlayerId, PlayerTeam? targetTeam, PlayerEntity? attacker, int amount, bool wouldBeFatal, float x, float y)
        => ShouldCancelDamage(targetKind, targetEntityId, targetPlayerId, targetTeam, attacker, amount, wouldBeFatal, x, y);
    bool ICombatDamageInterceptors.ShouldCancelDeath(PlayerEntity player, bool gibbed, PlayerEntity? killer, string? weaponSpriteName)
        => ShouldCancelDeath(player, gibbed, killer, weaponSpriteName);
    bool ICombatDamageInterceptors.TryPreventExperimentalFatalDamage(PlayerEntity target, int damage)
        => ExperimentalRules.TryPreventExperimentalFatalDamage(target, damage);
    bool ICombatDamageInterceptors.TryConvertExperimentalSelfDamageToHealing(PlayerEntity target, PlayerEntity? attacker, float damage)
        => ExperimentalRules.TryConvertExperimentalSelfDamageToHealing(target, attacker, damage);
    bool ICombatDamageInterceptors.TryAbsorbPracticeCombatDummyDamage(PlayerEntity target, int damage, PlayerEntity? attacker, DamageEventFlags flags)
        => PracticeDummies.TryAbsorbPracticeCombatDummyDamage(target, damage, attacker, flags);
    bool ICombatDamageInterceptors.TryAbsorbPracticeCombatDummyContinuousDamage(PlayerEntity target, float damage, PlayerEntity? attacker, DamageEventFlags flags)
        => PracticeDummies.TryAbsorbPracticeCombatDummyContinuousDamage(target, damage, attacker, flags);

    void ICombatDamageConsequences.ApplyExperimentalDamageRewards(PlayerEntity? attacker, PlayerEntity target, int damage, bool allowOsmosisHealOwnedSentries)
        => ExperimentalRules.ApplyExperimentalDamageRewards(attacker, target, damage, allowOsmosisHealOwnedSentries);
    void ICombatDamageConsequences.ApplyExperimentalDamageTakenRewards(PlayerEntity target, PlayerEntity? attacker, int damage)
        => ExperimentalRules.ApplyExperimentalDamageTakenRewards(target, attacker, damage);
    void ICombatDamageConsequences.ApplyLastToDieDamageRewards(PlayerEntity? attacker, PlayerEntity target, int damage, PlayerDamageTraits traits)
        => LastToDieRules.ApplyLastToDieDamageRewards(attacker, target, damage, traits);
    void ICombatDamageConsequences.ApplyLastToDieDamageTakenEffects(PlayerEntity target, PlayerEntity? attacker, int damage, PlayerDamageTraits traits)
        => LastToDieRules.ApplyLastToDieDamageTakenEffects(target, attacker, damage, traits);
    PlayerEntity? ICombatDamageConsequences.ResolveLastToDieMedicLinkedOnHit(PlayerEntity? attacker, PlayerEntity target, int damage, PlayerDamageTraits traits)
        => LastToDieRules.ResolveLastToDieMedicLinkedOnHit(attacker, target, damage, traits);
    int ICombatDamageConsequences.ResolveLastToDieMedicLinkedAssistPlayerId(PlayerEntity? attacker, PlayerEntity? linkedMedic)
        => LastToDieRulesSystem.ResolveLastToDieMedicLinkedAssistPlayerId(attacker, linkedMedic);
    void ICombatDamageConsequences.ApplyLastToDieMedicLinkedOnHitEffects(PlayerEntity? attacker, PlayerEntity target, PlayerEntity? linkedMedic)
        => LastToDieRules.ApplyLastToDieMedicLinkedOnHitEffects(attacker, target, linkedMedic);
    void ICombatDamageConsequences.ApplyExperimentalEngineerFriendlyFireRetaliation(PlayerEntity attacker, PlayerEntity target, int damage)
        => ExperimentalRules.ApplyExperimentalEngineerFriendlyFireRetaliation(attacker, target, damage);
    void ICombatDamageConsequences.TryRegisterCombatComboHit(PlayerEntity? attacker, PlayerEntity target, int damage)
        => CombatFeedback.TryRegisterCombatComboHit(attacker, target, damage);
    void ICombatDamageConsequences.TryRegisterBuffBannerDamage(PlayerEntity attacker, PlayerEntity target, int damage)
        => SupportRules.TryRegisterBuffBannerDamage(attacker, target, damage);

    (float X, float Y) ICombatSystemHost.GetCivvieUmbrellaTip(PlayerEntity target, float aimWorldX, float aimWorldY)
    {
        var tip = WeaponHandler.GetCivvieUmbrellaTip(target, aimWorldX, aimWorldY);
        return (tip.X, tip.Y);
    }
}
