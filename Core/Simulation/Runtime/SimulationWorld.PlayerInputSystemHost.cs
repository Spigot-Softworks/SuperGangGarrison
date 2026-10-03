namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IPlayerInputHost
{
    GameplayAbilitySystem IPlayerInputHost.Abilities => Abilities;
    AirblastRulesSystem IPlayerInputHost.AirblastRules => AirblastRules;
    bool IPlayerInputHost.ClientPredictionMode => ClientPredictionMode;
    ExperimentalGameplaySettings IPlayerInputHost.ExperimentalGameplaySettings => ExperimentalGameplaySettings;
    ExperimentalRulesSystem IPlayerInputHost.ExperimentalRules => ExperimentalRules;
    bool IPlayerInputHost.IsPlayerHumiliated(PlayerEntity player)
        => IsPlayerHumiliated(player);
    LastToDieRulesSystem IPlayerInputHost.LastToDieRules => LastToDieRules;
    LastToDieState IPlayerInputHost.LastToDieState => LastToDieState;
    MovementSystem IPlayerInputHost.Movement => Movement;
    NetworkPlayerSystem IPlayerInputHost.NetworkPlayers => NetworkPlayers;
    ObjectiveRulesSystem IPlayerInputHost.ObjectiveRules => ObjectiveRules;
    PickupSystem IPlayerInputHost.Pickups => Pickups;
    PlayerDeathSystem IPlayerInputHost.PlayerDeaths => PlayerDeaths;
    PracticeDummySystem IPlayerInputHost.PracticeDummies => PracticeDummies;
    ProjectileSystem IPlayerInputHost.Projectiles => Projectiles;
    void IPlayerInputHost.RegisterDamageEvent(PlayerEntity? attacker, DamageTargetKind targetKind, int targetEntityId, float x, float y, int amount, bool wasFatal, PlayerEntity? playerTarget, DamageEventFlags flags, int assistPlayerIdOverride, int attackerPlayerIdOverride)
        => Combat.RegisterDamageEvent(attacker, targetKind, targetEntityId, x, y, amount, wasFatal, playerTarget, flags, assistPlayerIdOverride, attackerPlayerIdOverride);
    IReadOnlyList<RocketProjectileEntity> IPlayerInputHost.Rockets => Rockets;
    RoomEffectsSystem IPlayerInputHost.RoomEffects => RoomEffects;
    StructureSystem IPlayerInputHost.Structures => Structures;
    SupportRulesSystem IPlayerInputHost.SupportRules => SupportRules;
    bool IPlayerInputHost.TryAbsorbCivvieUmbrellaDamage(PlayerEntity target, PlayerEntity? attacker, DamageEventFlags damageFlags, float? threatSourceX, float? threatSourceY, int? drainTicks, bool criticalBoost, bool useLiveAttackerCriticalBoost)
        => Combat.TryAbsorbCivvieUmbrellaDamage(target, attacker, damageFlags, threatSourceX, threatSourceY, drainTicks, criticalBoost, useLiveAttackerCriticalBoost);
    void IPlayerInputHost.TryRegisterCivvieMoneyTrail(PlayerEntity player)
        => TryRegisterCivvieMoneyTrail(player);
    WeaponFireHandler IPlayerInputHost.WeaponHandler => WeaponHandler;
    WorldEffectsSystem IPlayerInputHost.WorldEffects => WorldEffects;
}
