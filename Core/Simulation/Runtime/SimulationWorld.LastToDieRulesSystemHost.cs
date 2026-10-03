namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ILastToDieHost
{
    bool ILastToDieHost.ClientPredictionMode => ClientPredictionMode;
    CombatSystem ILastToDieHost.Combat => Combat;
    IReadOnlyList<ControlPointState> ILastToDieHost.ControlPoints => ControlPoints;
    DamageRulesSystem ILastToDieHost.DamageRules => DamageRules;
    ExperimentalGameplaySettings ILastToDieHost.ExperimentalGameplaySettings => ExperimentalGameplaySettings;
    ExperimentalRulesSystem ILastToDieHost.ExperimentalRules => ExperimentalRules;
    CombatResolver ILastToDieHost.GeometryResolver => GeometryResolver;
    LastToDieState ILastToDieHost.LastToDieState => LastToDieState;
    MatchRules ILastToDieHost.MatchRules => MatchRules;
    MatchSettingsState ILastToDieHost.MatchSettings => MatchSettings;
    IReadOnlyList<NeedleProjectileEntity> ILastToDieHost.Needles => Needles;
    NetworkPlayerSystem ILastToDieHost.NetworkPlayers => NetworkPlayers;
    IReadOnlyList<byte> ILastToDieHost.NetworkPlayerSlots => NetworkPlayerSlots;
    ObjectiveRulesSystem ILastToDieHost.ObjectiveRules => ObjectiveRules;
    ObjectiveStateStore ILastToDieHost.Objectives => Objectives;
    PlayerDeathSystem ILastToDieHost.PlayerDeaths => PlayerDeaths;
    PlayerPresentationBoundsSystem ILastToDieHost.PresentationBounds => PresentationBounds;
    void ILastToDieHost.RemoveNeedleAt(int index) => Projectiles.RemoveNeedleAt(index);
    PlayerDamageResolution ILastToDieHost.ResolvePlayerDamageWithContext(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        float spyRevealAlpha,
        DamageEventFlags damageFlags,
        bool allowOsmosisHealOwnedSentries,
        bool allowCivvieUmbrellaShield,
        float? civvieUmbrellaThreatSourceX,
        float? civvieUmbrellaThreatSourceY,
        int? civvieUmbrellaDrainTicks,
        bool civvieUmbrellaCriticalBoost,
        bool civvieUmbrellaUseLiveAttackerCriticalBoost,
        PlayerDamageTraits additionalTraits,
        bool? attackerWasGrounded,
        bool? targetWasGrounded,
        int sourceEntityId,
        ulong attackId,
        int attackerPlayerIdOverride)
        => ResolvePlayerDamageWithContext(
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
    ScorekeepingSystem ILastToDieHost.Scorekeeping => Scorekeeping;
    ServerTuningSystem ILastToDieHost.ServerTuning => ServerTuning;
    SupportRulesSystem ILastToDieHost.SupportRules => SupportRules;
}
