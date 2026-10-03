namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="PlayerInputSystem"/> needs from the world coordinator.
/// </summary>
internal interface IPlayerInputHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    GameplayAbilitySystem Abilities { get; }
    AirblastRulesSystem AirblastRules { get; }
    bool ClientPredictionMode { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; }
    ExperimentalRulesSystem ExperimentalRules { get; }
    LastToDieRulesSystem LastToDieRules { get; }
    LastToDieState LastToDieState { get; }
    MovementSystem Movement { get; }
    NetworkPlayerSystem NetworkPlayers { get; }
    ObjectiveRulesSystem ObjectiveRules { get; }
    PickupSystem Pickups { get; }
    PlayerDeathSystem PlayerDeaths { get; }
    PracticeDummySystem PracticeDummies { get; }
    ProjectileSystem Projectiles { get; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    RoomEffectsSystem RoomEffects { get; }
    StructureSystem Structures { get; }
    SupportRulesSystem SupportRules { get; }
    WeaponFireHandler WeaponHandler { get; }
    WorldEffectsSystem WorldEffects { get; }

    bool IsPlayerHumiliated(PlayerEntity player);
    void RegisterDamageEvent(
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
        int attackerPlayerIdOverride = -1);
    bool TryAbsorbCivvieUmbrellaDamage(
        PlayerEntity target,
        PlayerEntity? attacker,
        DamageEventFlags damageFlags,
        float? threatSourceX = null,
        float? threatSourceY = null,
        int? drainTicks = null,
        bool criticalBoost = false,
        bool useLiveAttackerCriticalBoost = true);
    void TryRegisterCivvieMoneyTrail(PlayerEntity player);
}
