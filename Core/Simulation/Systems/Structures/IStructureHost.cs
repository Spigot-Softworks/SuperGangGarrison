using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by <see cref="StructureSystem"/>.
/// </summary>
internal interface IStructureHost
{
    AirblastRulesSystem AirblastRules { get; }
    WorldBounds Bounds { get; }
    bool ClientPredictionMode { get; }
    SimulationConfig Config { get; }
    DamageRulesSystem DamageRules { get; }
    EntityStore EntityStore { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    CombatResolver GeometryResolver { get; }
    LastToDieState LastToDieState { get; }
    SimpleLevel Level { get; }
    PlayerEntity LocalPlayer { get; }
    MapLogicSystem MapLogic { get; }
    MatchState MatchState { get; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    NetworkPlayerSystem NetworkPlayers { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    ScorekeepingSystem Scorekeeping { get; }
    WorldEffectsSystem WorldEffects { get; }
    WorldObjectStore WorldObjects { get; }

    int AllocateEntityId();
    void ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse);
    bool ApplyPlayerDamage(PlayerEntity target, int damage, PlayerEntity? attacker, float spyRevealAlpha = 0f, DamageEventFlags damageFlags = DamageEventFlags.None, bool allowOsmosisHealOwnedSentries = true, bool allowCivvieUmbrellaShield = true, float? civvieUmbrellaThreatSourceX = null, float? civvieUmbrellaThreatSourceY = null, int? civvieUmbrellaDrainTicks = null, bool civvieUmbrellaCriticalBoost = false);
    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    PlayerEntity? FindPlayerById(int playerId);
    float GetExplosionImpulseMagnitude(PlayerEntity player, float originX, float originY, float knockbackPerTick, float distanceFactor, bool useMineVectorProfile);
}
