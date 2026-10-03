namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by CombatResolver for line-of-sight, raycast, and hit queries.
/// </summary>
internal interface ICombatGeometryHost
{
    DamageRulesSystem DamageRules { get; }
    long Frame { get; }
    SimpleLevel Level { get; }
    MapLogicSystem MapLogic { get; }
    PlayerPresentationBoundsSystem PresentationBounds { get; }
    ProjectileSystem Projectiles { get; }
    WorldObjectStore WorldObjects { get; }

    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
}
