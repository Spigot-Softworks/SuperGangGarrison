namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private CombatSystemDependencies CreateCombatSystemDependencies()
    {
        return new CombatSystemDependencies
        {
            CurrentFrame = () => Frame,
            NextSharedRandomDouble = () => _random.NextDouble(),
            EnumerateSimulatedPlayers = EnumerateSimulatedPlayers,
            FindPlayerById = FindPlayerById,
            CanDamagePlayer = CanTeamDamagePlayer,
            ScaleConfiguredDamage = ScaleConfiguredDamage,
            ScaleConfiguredFloatDamage = ScaleConfiguredDamage,
            ScaleConfiguredContinuousDamage = ScaleConfiguredDamage,
            GetSimulationTicksFromSourceTicks = GetSimulationTicksFromSourceTicks,
            ApplyExperimentalOutgoingDamageMultiplier = ApplyExperimentalOutgoingDamageMultiplier,
            ApplyExperimentalOutgoingDamageMultiplierContinuous = ApplyExperimentalOutgoingDamageMultiplier,
            ApplyExperimentalIncomingDamageMultiplier = ApplyExperimentalIncomingDamageMultiplier,
            ApplyExperimentalIncomingDamageMultiplierContinuous = ApplyExperimentalIncomingDamageMultiplier,
            ApplyLastToDieOutgoingDamageMultiplier = ApplyLastToDieOutgoingDamageMultiplier,
            ApplyLastToDieOutgoingDamageMultiplierContinuous = ApplyLastToDieOutgoingDamageMultiplier,
            ApplyLastToDieIncomingDamageMultiplier = ApplyLastToDieIncomingDamageMultiplier,
            ApplyLastToDieIncomingDamageMultiplierContinuous = ApplyLastToDieIncomingDamageMultiplier,
            ApplyExperimentalIncomingSentryDamageMultiplier = ApplyExperimentalIncomingSentryDamageMultiplier,
            TryPreventExperimentalFatalDamage = TryPreventExperimentalFatalDamage,
            TryConvertExperimentalSelfDamageToHealing = TryConvertExperimentalSelfDamageToHealing,
            TryAbsorbPracticeCombatDummyDamage = TryAbsorbPracticeCombatDummyDamage,
            TryAbsorbPracticeCombatDummyContinuousDamage = TryAbsorbPracticeCombatDummyContinuousDamage,
            GetExperimentalTotalEvasionChance = GetExperimentalTotalEvasionChance,
            GetLastToDieEvasionChance = GetLastToDieEvasionChance,
            RollLastToDieEvasion = RollLastToDieEvasion,
            ApplyExperimentalDamageRewards = ApplyExperimentalDamageRewards,
            ApplyExperimentalDamageTakenRewards = ApplyExperimentalDamageTakenRewards,
            ApplyLastToDieDamageRewards = ApplyLastToDieDamageRewards,
            ApplyLastToDieDamageTakenEffects = ApplyLastToDieDamageTakenEffects,
            ResolveLastToDieMedicLinkedOnHit = ResolveLastToDieMedicLinkedOnHit,
            ResolveLastToDieMedicLinkedAssistPlayerId = ResolveLastToDieMedicLinkedAssistPlayerId,
            ApplyLastToDieMedicLinkedOnHitEffects = ApplyLastToDieMedicLinkedOnHitEffects,
            ApplyExperimentalEngineerFriendlyFireRetaliation = ApplyExperimentalEngineerFriendlyFireRetaliation,
            TryRegisterCombatComboHit = TryRegisterCombatComboHit,
            TryRegisterBuffBannerDamage = TryRegisterBuffBannerDamage,
            RegisterImpactEffect = RegisterImpactEffect,
            GetCivvieUmbrellaTip = (target, x, y) =>
            {
                var tip = WeaponHandler.GetCivvieUmbrellaTip(target, x, y);
                return (tip.X, tip.Y);
            },
            ShouldCancelDamage = (frame, targetKind, targetEntityId, targetPlayerId, targetTeam, attacker, amount, wouldBeFatal, x, y) =>
                ShouldCancelDamage(targetKind, targetEntityId, targetPlayerId, targetTeam, attacker, amount, wouldBeFatal, x, y),
            ShouldCancelDeath = (frame, player, gibbed, killer, weaponSpriteName) =>
                ShouldCancelDeath(player, gibbed, killer, weaponSpriteName),
        };
    }

    private void RegisterDamageEvent(
        PlayerEntity? attacker,
        DamageTargetKind targetKind,
        int targetEntityId,
        float x,
        float y,
        int amount,
        bool wasFatal,
        PlayerEntity? playerTarget = null,
        DamageEventFlags flags = DamageEventFlags.None,
        int assistPlayerIdOverride = -1,
        int attackerPlayerIdOverride = -1)
        => Combat.RegisterDamageEvent(
            attacker, targetKind, targetEntityId, x, y, amount, wasFatal, playerTarget,
            flags, assistPlayerIdOverride, attackerPlayerIdOverride);

    private void MarkPendingFatalPlayerDamageEventGibbed(int playerId)
        => Combat.MarkPendingFatalPlayerDamageEventGibbed(playerId);

    private void MarkPendingFatalPlayerDamageEventPrevented(int playerId)
        => Combat.MarkPendingFatalPlayerDamageEventPrevented(playerId);

    private bool ApplyPlayerDamage(
        PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool allowOsmosisHealOwnedSentries = true,
        bool allowCivvieUmbrellaShield = true,
        float? civvieUmbrellaThreatSourceX = null,
        float? civvieUmbrellaThreatSourceY = null,
        int? civvieUmbrellaDrainTicks = null,
        bool civvieUmbrellaCriticalBoost = false)
        => Combat.ApplyPlayerDamage(
            target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries,
            allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY,
            civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost);

    private bool ApplyPlayerDamageWithContext(
        PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool allowOsmosisHealOwnedSentries = true,
        bool allowCivvieUmbrellaShield = true,
        float? civvieUmbrellaThreatSourceX = null,
        float? civvieUmbrellaThreatSourceY = null,
        int? civvieUmbrellaDrainTicks = null,
        bool civvieUmbrellaCriticalBoost = false,
        bool civvieUmbrellaUseLiveAttackerCriticalBoost = true,
        PlayerDamageTraits additionalTraits = PlayerDamageTraits.None,
        bool? attackerWasGrounded = null,
        bool? targetWasGrounded = null,
        int sourceEntityId = 0,
        ulong attackId = 0,
        int attackerPlayerIdOverride = -1)
        => Combat.ApplyPlayerDamageWithContext(
            target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries,
            allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY,
            civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost,
            civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded,
            targetWasGrounded, sourceEntityId, attackId, attackerPlayerIdOverride);

    internal PlayerDamageResolution ResolvePlayerDamage(PlayerEntity target, in PlayerDamageRequest request)
        => Combat.ResolvePlayerDamage(target, request);

    private PlayerDamageResolution ResolvePlayerDamageWithContext(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool allowOsmosisHealOwnedSentries = true,
        bool allowCivvieUmbrellaShield = true,
        float? civvieUmbrellaThreatSourceX = null,
        float? civvieUmbrellaThreatSourceY = null,
        int? civvieUmbrellaDrainTicks = null,
        bool civvieUmbrellaCriticalBoost = false,
        bool civvieUmbrellaUseLiveAttackerCriticalBoost = true,
        PlayerDamageTraits additionalTraits = PlayerDamageTraits.None,
        bool? attackerWasGrounded = null,
        bool? targetWasGrounded = null,
        int sourceEntityId = 0,
        ulong attackId = 0,
        int attackerPlayerIdOverride = -1)
        => Combat.ResolvePlayerDamageWithContext(
            target,
            damage,
            attacker,
            spyRevealAlpha,
            damageFlags,
            allowOsmosisHealOwnedSentries,
            allowCivvieUmbrellaShield,
            civvieUmbrellaThreatSourceX,
            civvieUmbrellaThreatSourceY,
            civvieUmbrellaDrainTicks,
            civvieUmbrellaCriticalBoost,
            civvieUmbrellaUseLiveAttackerCriticalBoost,
            additionalTraits,
            attackerWasGrounded,
            targetWasGrounded,
            sourceEntityId,
            attackId,
            attackerPlayerIdOverride);

    private bool ApplyPlayerContinuousDamage(
        PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool allowOsmosisHealOwnedSentries = true,
        bool allowCivvieUmbrellaShield = true,
        float? civvieUmbrellaThreatSourceX = null,
        float? civvieUmbrellaThreatSourceY = null,
        int? civvieUmbrellaDrainTicks = null,
        bool civvieUmbrellaCriticalBoost = false)
        => Combat.ApplyPlayerContinuousDamage(
            target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries,
            allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY,
            civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost);

    private bool ApplyPlayerContinuousDamageWithContext(
        PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha = 0f,
        DamageEventFlags damageFlags = DamageEventFlags.None,
        bool allowOsmosisHealOwnedSentries = true,
        bool allowCivvieUmbrellaShield = true,
        float? civvieUmbrellaThreatSourceX = null,
        float? civvieUmbrellaThreatSourceY = null,
        int? civvieUmbrellaDrainTicks = null,
        bool civvieUmbrellaCriticalBoost = false,
        bool civvieUmbrellaUseLiveAttackerCriticalBoost = true,
        PlayerDamageTraits additionalTraits = PlayerDamageTraits.None,
        bool? attackerWasGrounded = null,
        bool? targetWasGrounded = null)
        => Combat.ApplyPlayerContinuousDamageWithContext(
            target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries,
            allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY,
            civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost,
            civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded,
            targetWasGrounded);

    private bool ApplySentryDamage(SentryEntity target, int damage, PlayerEntity? attacker)
        => Combat.ApplySentryDamage(target, damage, attacker);

    private bool ApplyGeneratorDamage(GeneratorState target, float damage, PlayerEntity? attacker)
        => Combat.ApplyGeneratorDamage(target, damage, attacker);

    private bool TryAbsorbCivvieUmbrellaDamage(
        PlayerEntity target, PlayerEntity? attacker, DamageEventFlags damageFlags,
        float? threatSourceX = null, float? threatSourceY = null, int? drainTicks = null,
        bool criticalBoost = false, bool useLiveAttackerCriticalBoost = true)
        => Combat.TryAbsorbCivvieUmbrellaDamage(
            target, attacker, damageFlags, threatSourceX, threatSourceY, drainTicks,
            criticalBoost, useLiveAttackerCriticalBoost);

    private bool TryAbsorbCivvieUmbrellaProjectileContact(
        PlayerEntity target, int ownerId, float hitX, float hitY,
        DamageEventFlags damageFlags = DamageEventFlags.None, bool criticalBoost = false)
        => Combat.TryAbsorbCivvieUmbrellaProjectileContact(
            target, ownerId, hitX, hitY, damageFlags, criticalBoost);

    private PlayerEntity? ResolveAssistPlayer(PlayerEntity victim, PlayerEntity killer)
        => Combat.ResolveAssistPlayer(victim, killer);

    private int ResolveAssistPlayerId(PlayerEntity victim, PlayerEntity killer)
        => Combat.ResolveAssistPlayerId(victim, killer);

    private bool TryBeginPlayerDeath(
        PlayerEntity player,
        bool gibbed,
        PlayerEntity? killer,
        string? weaponSpriteName)
        => Combat.TryBeginPlayerDeath(player, gibbed, killer, weaponSpriteName);
}
