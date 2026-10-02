namespace OpenGarrison.Core;

/// <summary>
/// Owns the lists of map and gameplay objects that live in the world but are not
/// players or projectiles: buildings, pickups, corpses, gibs, blood, and generators.
/// Projectiles are owned by <see cref="ProjectileSystem"/> and all entity lifetimes are
/// registered in <see cref="EntityStore"/>.
/// </summary>
internal sealed class WorldObjectStore
{
    private readonly EntityStore _entities;

    public WorldObjectStore(EntityStore entities)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
    }

    // Buildings and map structures
    public List<SentryEntity> Sentries { get; } = new();
    public List<JumpPadEntity> JumpPads { get; } = new();
    public List<CivilDefenseTurretEntity> CivilDefenseTurrets { get; } = new();
    public List<GeneratorState> Generators { get; } = new();

    // Pickups
    public List<HealthPackEntity> HealthPacks { get; } = new();
    public List<int> HealthPackSpawnRespawnTicks { get; } = new();
    public List<DroppedWeaponEntity> DroppedWeapons { get; } = new();

    // Corpses, gibs, and blood
    public List<DeadBodyEntity> DeadBodies { get; } = new();
    public List<PlayerGibEntity> PlayerGibs { get; } = new();
    public List<SentryGibEntity> SentryGibs { get; } = new();
    public List<JumpPadGibEntity> JumpPadGibs { get; } = new();
    public List<BloodDropEntity> BloodDrops { get; } = new();

    /// <summary>
    /// Unregisters every entity in <paramref name="entities"/> from the entity store and
    /// empties the list.
    /// </summary>
    public void RemoveAll<T>(List<T> entities) where T : SimulationEntity
    {
        for (var index = 0; index < entities.Count; index += 1)
        {
            _entities.RemoveIfSame(entities[index]);
        }

        entities.Clear();
    }

    /// <summary>
    /// Removes the objects that must not survive a round or map reset. Dropped weapons,
    /// jump pad gibs, generators, and health pack respawn timers are handled by their
    /// own reset paths.
    /// </summary>
    public void RemoveRoundScopedObjects()
    {
        RemoveAll(Sentries);
        RemoveAll(JumpPads);
        RemoveAll(CivilDefenseTurrets);
        RemoveAll(PlayerGibs);
        RemoveAll(BloodDrops);
        RemoveAll(HealthPacks);
        RemoveAll(DeadBodies);
        RemoveAll(SentryGibs);
    }
}
