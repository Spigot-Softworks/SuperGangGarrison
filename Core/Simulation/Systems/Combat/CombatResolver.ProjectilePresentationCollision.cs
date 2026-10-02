namespace OpenGarrison.Core;

internal sealed partial class CombatResolver
{
    public float? GetProjectilePresentationEnvironmentHit(
        PlayerTeam team, float x, float y, float directionX, float directionY, float distance)
    {
        float? nearest = null;
        UpdateNearestEnvironmentProjectileHitFromSolids<byte, float>(
            ref nearest, 0, x, y, directionX, directionY, distance, false, RememberPresentationHit);
        UpdateNearestEnvironmentProjectileHitFromRoomObjects<byte, float>(
            ref nearest, team, 0, x, y, directionX, directionY, distance,
            ProjectileRoomObjectBlockerProfile.Standard, false, RememberPresentationHit, applyDamage: false);
        return nearest;
    }

    private static void RememberPresentationHit(
        ref float? nearest, byte unused, float directionX, float directionY, float distance, bool destroyOnHit)
    {
        if (!nearest.HasValue || distance < nearest.Value)
            nearest = distance;
    }
}
