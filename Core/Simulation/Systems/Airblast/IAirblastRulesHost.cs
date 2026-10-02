namespace OpenGarrison.Core;

/// <summary>
/// What <see cref="AirblastRulesSystem"/> needs from the world coordinator.
/// </summary>
internal interface IAirblastRulesHost : ISimulationWorldState, ISimulationPlayerDirectory
{
    ExperimentalGameplaySettings ExperimentalGameplaySettings { get; }
    IReadOnlyList<FlareProjectileEntity> Flares { get; }
    IReadOnlyList<GrenadeProjectileEntity> Grenades { get; }
    IReadOnlyList<MineProjectileEntity> Mines { get; }
    IReadOnlyList<NeedleProjectileEntity> Needles { get; }
    IReadOnlyList<RevolverProjectileEntity> RevolverShots { get; }
    IReadOnlyList<RocketProjectileEntity> Rockets { get; }
    IReadOnlyList<ShotProjectileEntity> Shots { get; }
    WeaponFireHandler WeaponHandler { get; }
    WorldObjectStore WorldObjects { get; }

    bool HasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam);
    void RegisterImpactEffect(float x, float y, float directionDegrees);
    void RegisterSoundEvent(PlayerEntity attacker, string soundName);
    void RegisterVisualEffect(
        string effectName,
        float x,
        float y,
        float directionDegrees = 0f,
        int count = 1,
        bool normalizeDirection = true);
    void RemoveMineAt(int index);
    void RemoveNeedleAt(int index);
    void RemoveRevolverShotAt(int index);
    void RemoveRocketAt(int index);
    void RemoveShotAt(int index);
    void ResolveDragonRageProjectileOutcome(FlareProjectileEntity flare, bool hitTarget);
    void SpawnAirblastExtinguishFlames(PlayerEntity attacker, PlayerEntity target, float aimRadians);
}
