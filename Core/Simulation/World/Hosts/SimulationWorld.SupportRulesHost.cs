namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ISupportRulesHost
{
    bool ISupportRulesHost.ApplyPlayerContinuousDamage(PlayerEntity target, float damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost)
        => Combat.ApplyPlayerContinuousDamage(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost);
    bool ISupportRulesHost.ControlPointSetupActive => ControlPointSetupActive;
    DamageRulesSystem ISupportRulesHost.DamageRules => DamageRules;
    ExperimentalRulesSystem ISupportRulesHost.ExperimentalRules => ExperimentalRules;
    CombatResolver ISupportRulesHost.GeometryResolver => GeometryResolver;
    float? ISupportRulesHost.GetThickLineIntersectionDistanceToPlayer(float originX, float originY, float endX, float endY, PlayerEntity player, float maxDistance, float thicknessRadius)
        => GeometryResolver.GetThickLineIntersectionDistanceToPlayer(originX, originY, endX, endY, player, maxDistance, thicknessRadius);
    LastToDieRulesSystem ISupportRulesHost.LastToDieRules => LastToDieRules;
    PlayerEntity ISupportRulesHost.LocalPlayer => LocalPlayer;
    NetworkPlayerSystem ISupportRulesHost.NetworkPlayers => NetworkPlayers;
    PlayerDeathSystem ISupportRulesHost.PlayerDeaths => PlayerDeaths;
    PlayerPresentationBoundsSystem ISupportRulesHost.PresentationBounds => PresentationBounds;
    ScorekeepingSystem ISupportRulesHost.Scorekeeping => Scorekeeping;
    WorldEffectsSystem ISupportRulesHost.WorldEffects => WorldEffects;
}
