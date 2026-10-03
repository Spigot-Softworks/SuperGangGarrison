namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool HasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam)
        => GeometryResolver.HasDirectLineOfSight(originX, originY, targetX, targetY, targetTeam);

    private bool HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        => GeometryResolver.HasObstacleLineOfSight(originX, originY, targetX, targetY);

    private bool IsProjectilePathBlocked(float originX, float originY, float targetX, float targetY, PlayerTeam shotTeam)
        => GeometryResolver.IsProjectilePathBlocked(originX, originY, targetX, targetY, shotTeam);

    private float? GetThickLineIntersectionDistanceToPlayer(
        float originX,
        float originY,
        float endX,
        float endY,
        PlayerEntity player,
        float maxDistance,
        float thicknessRadius)
        => GeometryResolver.GetThickLineIntersectionDistanceToPlayer(originX, originY, endX, endY, player, maxDistance, thicknessRadius);

    private ShotHitResult? GetNearestShotHit(ShotProjectileEntity shot, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestShotHit(shot, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestNeedleHit(NeedleProjectileEntity needle, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestNeedleHit(needle, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestMedicHealNeedleHit(MedicHealNeedleProjectileEntity needle, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestMedicHealNeedleHit(needle, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestRevolverHit(RevolverProjectileEntity shot, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestRevolverHit(shot, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestBladeHit(BladeProjectileEntity blade, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestBladeHit(blade, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float directionX, float directionY)
        => GeometryResolver.GetNearestStabHit(mask, directionX, directionY);

    private ShotHitResult? GetNearestHealstabHit(StabMaskEntity mask, float directionX, float directionY)
        => GeometryResolver.GetNearestHealstabHit(mask, directionX, directionY);

    private bool HasStabChainLineOfSight(float originX, float originY, float targetX, float targetY)
        => GeometryResolver.HasStabChainLineOfSight(originX, originY, targetX, targetY);

    private RocketHitResult? GetNearestRocketHit(RocketProjectileEntity rocket, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestRocketHit(rocket, directionX, directionY, maxDistance);

    private MineHitResult? GetNearestMineHit(MineProjectileEntity mine, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestMineHit(mine, directionX, directionY, maxDistance);

    private GrenadeEnvironmentHit? GetNearestGrenadeEnvironmentHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestGrenadeEnvironmentHit(grenade, directionX, directionY, maxDistance);

    private bool TryGetGrenadeDamageableZoneContact(
        GrenadeProjectileEntity grenade,
        float directionX,
        float directionY,
        float maxDistance,
        out float hitX,
        out float hitY,
        out int roomObjectIndex)
        => GeometryResolver.TryGetGrenadeDamageableZoneContact(grenade, directionX, directionY, maxDistance, out hitX, out hitY, out roomObjectIndex);

    private PlayerEntity? GetNearestGrenadePlayerHit(GrenadeProjectileEntity grenade, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestGrenadePlayerHit(grenade, directionX, directionY, maxDistance);

    private FlameHitResult? GetNearestFlameHit(FlameProjectileEntity flame, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestFlameHit(flame, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestFlareHit(FlareProjectileEntity flare, float directionX, float directionY, float maxDistance)
        => GeometryResolver.GetNearestFlareHit(flare, directionX, directionY, maxDistance);

    private OrderedRifleHitResult ResolveOrderedRifleHits(
        PlayerEntity attacker,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float maxDistance,
        RifleTracePolicy policy)
        => GeometryResolver.ResolveOrderedRifleHits(
            attacker,
            originX,
            originY,
            directionX,
            directionY,
            maxDistance,
            policy);
}
