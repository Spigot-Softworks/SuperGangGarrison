namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="AirblastRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface IAirblastRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    CombatFeedbackSystem CombatFeedback { get; }
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; }
    IReadOnlyList<FlareProjectileEntity> Flares { get; }
    CombatResolver GeometryResolver { get; }
    IReadOnlyList<GrenadeProjectileEntity> Grenades { get; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    IReadOnlyList<NeedleProjectileEntity> Needles { get; }
    ProjectileSystem Projectiles { get; }
    IReadOnlyList<RevolverProjectileEntity> RevolverShots { get; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    IReadOnlyList<ShotProjectileEntity> Shots { get; }
    WeaponFireHandler WeaponHandler { get; }
    WorldEffectsSystem WorldEffects { get; }
    WorldObjectStore WorldObjects { get; }

    void RemoveMineAt(int index);
    void RemoveNeedleAt(int index);
    void RemoveRevolverShotAt(int index);
    void RemoveRocketAt(int index);
    void RemoveShotAt(int index);
}
