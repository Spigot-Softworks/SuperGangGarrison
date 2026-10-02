namespace OpenGarrison.Core;

/// <summary>
/// Narrow view of the world used by CombatResolver for line-of-sight, raycast, and hit queries.
/// </summary>
internal interface ICombatGeometryHost
{
    SimpleLevel Level { get; }
    WorldObjectStore WorldObjects { get; }
    long Frame { get; }

    IEnumerable<PlayerEntity> EnumerateSimulatedPlayers();
    bool CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target);
    void GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom);
    bool BlocksProjectileDamageableZone(int roomObjectIndex);
    float GetDamageableZoneHealth(int roomObjectIndex);
    void SetProjectileSpawnBlockedDebug(float x, float y, float width, float height, string objectName);
    bool TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? damagingTeam = null);
}
