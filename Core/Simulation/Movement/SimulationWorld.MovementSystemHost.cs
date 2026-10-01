using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    IEnumerable<ArrowProjectileEntity> IMovementSystemHost.EnumerateArrowProjectiles()
        => Needles.OfType<ArrowProjectileEntity>();

    bool IMovementSystemHost.TryFindWhippingCordTerrainContact(
        PlayerEntity player,
        GameplayItemDefinition item,
        float aimWorldX,
        float aimWorldY,
        out float contactX,
        out float contactY)
        => TryFindWhippingCordTerrainContact(player, item, aimWorldX, aimWorldY, out contactX, out contactY);

    bool IMovementSystemHost.IsWhippingCordTerrainLatchPathClear(
        PlayerEntity player,
        float originX,
        float originY,
        float directionX,
        float directionY,
        float distance)
    {
        var firstHit = GeometryResolver.ResolveRifleHit(
            player,
            originX,
            originY,
            directionX,
            directionY,
            distance);
        return firstHit.HitPlayer is null
            && firstHit.HitSentry is null
            && firstHit.HitGenerator is null
            && firstHit.HitJumpPad is null;
    }
}
