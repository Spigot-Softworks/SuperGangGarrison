namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : IAirblastRulesHost
{
    CombatFeedbackSystem IAirblastRulesHost.CombatFeedback => CombatFeedback;
    ExperimentalGameplaySettings IAirblastRulesHost.ExperimentalGameplaySettings => ExperimentalGameplaySettings;
    IReadOnlyList<FlareProjectileEntity> IAirblastRulesHost.Flares => Flares;
    CombatResolver IAirblastRulesHost.GeometryResolver => GeometryResolver;
    IReadOnlyList<GrenadeProjectileEntity> IAirblastRulesHost.Grenades => Grenades;
    IReadOnlyList<MineProjectileEntity> IAirblastRulesHost.Mines => Mines;
    IReadOnlyList<NeedleProjectileEntity> IAirblastRulesHost.Needles => Needles;
    ProjectileSystem IAirblastRulesHost.Projectiles => Projectiles;
    void IAirblastRulesHost.RemoveMineAt(int index)
        => Projectiles.RemoveMineAt(index);
    void IAirblastRulesHost.RemoveNeedleAt(int index)
        => Projectiles.RemoveNeedleAt(index);
    void IAirblastRulesHost.RemoveRevolverShotAt(int index)
        => Projectiles.RemoveRevolverShotAt(index);
    void IAirblastRulesHost.RemoveRocketAt(int index)
        => Projectiles.RemoveRocketAt(index);
    void IAirblastRulesHost.RemoveShotAt(int index)
        => Projectiles.RemoveShotAt(index);
    IReadOnlyList<RevolverProjectileEntity> IAirblastRulesHost.RevolverShots => RevolverShots;
    IReadOnlyList<RocketProjectileEntity> IAirblastRulesHost.Rockets => Rockets;
    IReadOnlyList<ShotProjectileEntity> IAirblastRulesHost.Shots => Shots;
    WeaponFireHandler IAirblastRulesHost.WeaponHandler => WeaponHandler;
    WorldEffectsSystem IAirblastRulesHost.WorldEffects => WorldEffects;
    WorldObjectStore IAirblastRulesHost.WorldObjects => WorldObjects;
}
