namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="ExplosionRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface IExplosionRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    IReadOnlyList<BubbleProjectileEntity> Bubbles { get; }
    bool ClientPredictionMode { get; }
    CombatSystem Combat { get; }
    CombatRuntimeState CombatRuntime { get; }
    DamageRulesSystem DamageRules { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    MapLogicSystem MapLogic { get; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    ObjectiveRulesSystem ObjectiveRules { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    PlayerPresentationBoundsSystem PresentationBounds { get; }
    SimulationRandomStreams Randoms { get; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    ScorekeepingSystem Scorekeeping { get; }
    StructureSystem Structures { get; }
    WorldEffectsSystem WorldEffects { get; }
    WorldObjectStore WorldObjects { get; }

    bool ApplyPlayerContinuousDamageWithContext(
        PlayerEntity target,
        float damage,
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
        bool? targetWasGrounded = null);
    bool ApplyPlayerDamageWithContext(
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
        int attackerPlayerIdOverride = -1);
    void RemoveBubbleAt(int index);
    void RemoveMineAt(int index);
    void RemoveRocketAt(int index);
}
