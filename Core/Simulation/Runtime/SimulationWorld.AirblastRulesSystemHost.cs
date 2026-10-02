namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IAirblastRulesHost
{
    ExperimentalGameplaySettings IAirblastRulesHost.ExperimentalGameplaySettings => ExperimentalGameplaySettings;
    IReadOnlyList<FlareProjectileEntity> IAirblastRulesHost.Flares => Flares;
    IReadOnlyList<GrenadeProjectileEntity> IAirblastRulesHost.Grenades => Grenades;
    bool IAirblastRulesHost.HasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam)
        => HasDirectLineOfSight(originX, originY, targetX, targetY, targetTeam);
    IReadOnlyList<MineProjectileEntity> IAirblastRulesHost.Mines => Mines;
    IReadOnlyList<NeedleProjectileEntity> IAirblastRulesHost.Needles => Needles;
    void IAirblastRulesHost.RegisterImpactEffect(float x, float y, float directionDegrees)
        => WorldEffects.RegisterImpactEffect(x, y, directionDegrees);
    void IAirblastRulesHost.RegisterSoundEvent(PlayerEntity attacker, string soundName)
        => WorldEffects.RegisterSoundEvent(attacker, soundName);
    void IAirblastRulesHost.RegisterVisualEffect(string effectName, float x, float y, float directionDegrees, int count, bool normalizeDirection)
        => WorldEffects.RegisterVisualEffect(effectName, x, y, directionDegrees, count, normalizeDirection);
    void IAirblastRulesHost.RemoveMineAt(int index)
        => RemoveMineAt(index);
    void IAirblastRulesHost.RemoveNeedleAt(int index)
        => RemoveNeedleAt(index);
    void IAirblastRulesHost.RemoveRevolverShotAt(int index)
        => RemoveRevolverShotAt(index);
    void IAirblastRulesHost.RemoveRocketAt(int index)
        => RemoveRocketAt(index);
    void IAirblastRulesHost.RemoveShotAt(int index)
        => RemoveShotAt(index);
    void IAirblastRulesHost.ResolveDragonRageProjectileOutcome(FlareProjectileEntity flare, bool hitTarget)
        => ResolveDragonRageProjectileOutcome(flare, hitTarget);
    IReadOnlyList<RevolverProjectileEntity> IAirblastRulesHost.RevolverShots => RevolverShots;
    IReadOnlyList<RocketProjectileEntity> IAirblastRulesHost.Rockets => Rockets;
    IReadOnlyList<ShotProjectileEntity> IAirblastRulesHost.Shots => Shots;
    void IAirblastRulesHost.SpawnAirblastExtinguishFlames(PlayerEntity attacker, PlayerEntity target, float aimRadians)
        => CombatFeedback.SpawnAirblastExtinguishFlames(attacker, target, aimRadians);
    WeaponFireHandler IAirblastRulesHost.WeaponHandler => WeaponHandler;
    WorldObjectStore IAirblastRulesHost.WorldObjects => WorldObjects;
}
