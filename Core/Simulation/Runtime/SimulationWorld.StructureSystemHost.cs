using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IStructureHost
{
    AirblastRulesSystem IStructureHost.AirblastRules => AirblastRules;
    int IStructureHost.AllocateEntityId() => AllocateEntityId();
    void IStructureHost.ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse) => ExplosionGeometry.ApplyExplosionImpulse(player, originX, originY, impulse);
    bool IStructureHost.ApplyPlayerDamage(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha, DamageEventFlags damageFlags, bool allowOsmosisHealOwnedSentries, bool allowCivvieUmbrellaShield, float? civvieUmbrellaThreatSourceX, float? civvieUmbrellaThreatSourceY, int? civvieUmbrellaDrainTicks, bool civvieUmbrellaCriticalBoost) => ApplyPlayerDamage(target, damage, attacker, spyRevealAlpha, damageFlags, allowOsmosisHealOwnedSentries, allowCivvieUmbrellaShield, civvieUmbrellaThreatSourceX, civvieUmbrellaThreatSourceY, civvieUmbrellaDrainTicks, civvieUmbrellaCriticalBoost);
    WorldBounds IStructureHost.Bounds => Bounds;
    bool IStructureHost.ClientPredictionMode => ClientPredictionMode;
    SimulationConfig IStructureHost.Config => Config;
    DamageRulesSystem IStructureHost.DamageRules => DamageRules;
    EntityStore IStructureHost.EntityStore => EntityStore;
    IEnumerable<PlayerEntity> IStructureHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    ExperimentalRulesSystem IStructureHost.ExperimentalRules => ExperimentalRules;
    PlayerEntity? IStructureHost.FindPlayerById(int playerId) => FindPlayerById(playerId);
    CombatResolver IStructureHost.GeometryResolver => GeometryResolver;
    float IStructureHost.GetExplosionImpulseMagnitude(PlayerEntity player, float originX, float originY, float knockbackPerTick, float distanceFactor, bool useMineVectorProfile) => GetExplosionImpulseMagnitude(player, originX, originY, knockbackPerTick, distanceFactor, useMineVectorProfile);
    LastToDieState IStructureHost.LastToDieState => LastToDieState;
    SimpleLevel IStructureHost.Level => Level;
    PlayerEntity IStructureHost.LocalPlayer => LocalPlayer;
    MapLogicSystem IStructureHost.MapLogic => MapLogic;
    MatchState IStructureHost.MatchState => MatchState;
    IReadOnlyList<MineProjectileEntity> IStructureHost.Mines => Mines;
    NetworkPlayerSystem IStructureHost.NetworkPlayerRules => NetworkPlayerRules;
    PlayerDeathSystem IStructureHost.PlayerDeaths => PlayerDeaths;
    ScorekeepingSystem IStructureHost.Scorekeeping => Scorekeeping;
    WorldEffectsSystem IStructureHost.WorldEffects => WorldEffects;
    WorldObjectStore IStructureHost.WorldObjects => WorldObjects;
}
