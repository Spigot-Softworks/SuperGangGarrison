namespace OpenGarrison.Core;

// Explosion helpers other world code and host implementations still call.
// The geometry lives in ExplosionGeometry; the rules in ExplosionRulesSystem.
public sealed partial class SimulationWorld
{
    private static void ApplyMineExplosionImpulse(PlayerEntity player, float originX, float originY, float distanceFactor)
        => ExplosionGeometry.ApplyMineExplosionImpulse(player, originX, originY, distanceFactor);

    private static float GetExplosionImpulseMagnitude(
        PlayerEntity player,
        float originX,
        float originY,
        float knockbackPerTick,
        float distanceFactor,
        bool useMineVectorProfile)
        => ExplosionGeometry.GetExplosionImpulseMagnitude(player, originX, originY, knockbackPerTick, distanceFactor, useMineVectorProfile);

    private static float GetExplosionDistanceToPlayer(SimulationWorld world, PlayerEntity player, float originX, float originY)
    {
        world.GetCachedPlayerPresentationHitBounds(player, out var left, out var top, out var right, out var bottom);
        return ExplosionGeometry.GetDistanceToBounds(left, top, right, bottom, originX, originY);
    }
}
