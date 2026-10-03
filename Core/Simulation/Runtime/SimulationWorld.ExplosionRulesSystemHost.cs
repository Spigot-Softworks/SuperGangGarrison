namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IExplosionRulesHost
{
    bool IExplosionRulesHost.ApplyPlayerContinuousDamageWithContext(PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost, bool civvieUmbrellaUseLiveAttackerCriticalBoost, PlayerDamageTraits additionalTraits, bool? attackerWasGrounded, bool? targetWasGrounded)
        => ApplyPlayerContinuousDamageWithContext(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost, civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded, targetWasGrounded);
    bool IExplosionRulesHost.ApplyPlayerDamageWithContext(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost, bool civvieUmbrellaUseLiveAttackerCriticalBoost, PlayerDamageTraits additionalTraits, bool? attackerWasGrounded, bool? targetWasGrounded, int sourceEntityId, ulong attackId, int attackerPlayerIdOverride)
        => ApplyPlayerDamageWithContext(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost, civvieUmbrellaUseLiveAttackerCriticalBoost, additionalTraits, attackerWasGrounded, targetWasGrounded, sourceEntityId, attackId, attackerPlayerIdOverride);
    IReadOnlyList<BubbleProjectileEntity> IExplosionRulesHost.Bubbles => Bubbles;
    bool IExplosionRulesHost.ClientPredictionMode => ClientPredictionMode;
    CombatSystem IExplosionRulesHost.Combat => Combat;
    CombatRuntimeState IExplosionRulesHost.CombatRuntime => CombatRuntime;
    DamageRulesSystem IExplosionRulesHost.DamageRules => DamageRules;
    ExperimentalGameplaySettings IExplosionRulesHost.ExperimentalGameplaySettings => ExperimentalGameplaySettings;
    ExperimentalRulesSystem IExplosionRulesHost.ExperimentalRules => ExperimentalRules;
    LastToDieRulesSystem IExplosionRulesHost.LastToDieRules => LastToDieRules;
    MapLogicSystem IExplosionRulesHost.MapLogic => MapLogic;
    IReadOnlyList<MineProjectileEntity> IExplosionRulesHost.Mines => Mines;
    ObjectiveRulesSystem IExplosionRulesHost.ObjectiveRules => ObjectiveRules;
    PlayerDeathSystem IExplosionRulesHost.PlayerDeaths => PlayerDeaths;
    PlayerPresentationBoundsSystem IExplosionRulesHost.PresentationBounds => PresentationBounds;
    SimulationRandomStreams IExplosionRulesHost.Randoms => Randoms;
    void IExplosionRulesHost.RemoveBubbleAt(int index)
        => Projectiles.RemoveBubbleAt(index);
    void IExplosionRulesHost.RemoveMineAt(int index)
        => Projectiles.RemoveMineAt(index);
    void IExplosionRulesHost.RemoveRocketAt(int index)
        => Projectiles.RemoveRocketAt(index);
    IReadOnlyList<RocketProjectileEntity> IExplosionRulesHost.Rockets => Rockets;
    ScorekeepingSystem IExplosionRulesHost.Scorekeeping => Scorekeeping;
    StructureSystem IExplosionRulesHost.Structures => Structures;
    WorldEffectsSystem IExplosionRulesHost.WorldEffects => WorldEffects;
    WorldObjectStore IExplosionRulesHost.WorldObjects => WorldObjects;
}
