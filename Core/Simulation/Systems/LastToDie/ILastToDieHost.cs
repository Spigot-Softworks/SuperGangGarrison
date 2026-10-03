namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="LastToDieRulesSystem"/> needs from the world coordinator.
/// Wide because Last-To-Die perks touch damage, healing, network slots and
/// objectives; each member is a capability, never a back-reference to the world.
/// </summary>
internal interface ILastToDieHost : ISimulationWorldState, ISimulationPlayerDirectory, ISimulationPresentationEvents
{
    bool ClientPredictionMode { get; }
    CombatSystem Combat { get; }
    IReadOnlyList<ControlPointState> ControlPoints { get; }
    DamageRulesSystem DamageRules { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    CombatResolver GeometryResolver { get; }
    LastToDieState LastToDieState { get; }
    MatchRules MatchRules { get; }
    MatchSettingsState MatchSettings { get; }
    IReadOnlyList<NeedleProjectileEntity> Needles { get; }
    NetworkPlayerSystem NetworkPlayers { get; }
    IReadOnlyList<byte> NetworkPlayerSlots { get; }
    ObjectiveRulesSystem ObjectiveRules { get; }
    ObjectiveStateStore Objectives { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    PlayerPresentationBoundsSystem PresentationBounds { get; }
    ScorekeepingSystem Scorekeeping { get; }
    ServerTuningSystem ServerTuning { get; }
    SupportRulesSystem SupportRules { get; }

    // Network players.

    // Damage, death and healing.
    PlayerDamageResolution ResolvePlayerDamageWithContext(
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

    // Geometry, projectiles and objectives.
    void RemoveNeedleAt(int index);
}
