namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="SupportRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface ISupportRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    bool ControlPointSetupActive { get; }
    DamageRulesSystem DamageRules { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    CombatResolver GeometryResolver { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    PlayerEntity LocalPlayer { get; }
    NetworkPlayerSystem NetworkPlayerRules { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    PlayerPresentationBoundsSystem PresentationBounds { get; }
    ScorekeepingSystem Scorekeeping { get; }
    WorldEffectsSystem WorldEffects { get; }

    bool ApplyPlayerContinuousDamage(
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
        bool civvieUmbrellaCriticalBoost = false);
    float? GetThickLineIntersectionDistanceToPlayer(
        float originX,
        float originY,
        float endX,
        float endY,
        PlayerEntity player,
        float maxDistance,
        float thicknessRadius);
}
