namespace OpenGarrison.Core;

public sealed partial class SimulationWorld : ICombatGeometryHost
{
    SimpleLevel ICombatGeometryHost.Level => Level;
    WorldObjectStore ICombatGeometryHost.WorldObjects => WorldObjects;
    long ICombatGeometryHost.Frame => Frame;

    IEnumerable<PlayerEntity> ICombatGeometryHost.EnumerateSimulatedPlayers() => EnumerateSimulatedPlayers();
    bool ICombatGeometryHost.CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target) => CanTeamDamagePlayer(attackerTeam, attackerId, target);
    void ICombatGeometryHost.GetCachedPlayerPresentationHitBounds(PlayerEntity player, out float left, out float top, out float right, out float bottom) => GetCachedPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
    bool ICombatGeometryHost.BlocksProjectileDamageableZone(int roomObjectIndex) => BlocksProjectileDamageableZone(roomObjectIndex);
    float ICombatGeometryHost.GetDamageableZoneHealth(int roomObjectIndex) => GetDamageableZoneHealth(roomObjectIndex);
    void ICombatGeometryHost.SetProjectileSpawnBlockedDebug(float x, float y, float width, float height, string objectName) => SetProjectileSpawnBlockedDebug(x, y, width, height, objectName);
    bool ICombatGeometryHost.TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? damagingTeam) => TryApplyDamageableZoneDamage(roomObjectIndex, damage, damagingTeam);
}
