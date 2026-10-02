namespace OpenGarrison.Core;

/// <summary>
/// Explosion rules: mines (detonation, chains, splash), rocket explosions
/// (direct hit, splash, knockback, mortar redirects), danger-close blasts, and
/// explosion pushes on gibs and bodies. Geometry is in ExplosionGeometry.
/// </summary>
internal sealed partial class ExplosionRulesSystem
{
    private readonly IExplosionRulesHost _host;

    public ExplosionRulesSystem(IExplosionRulesHost host)
    {
        _host = host;
    }

    private float GetExplosionDistanceToPlayer(PlayerEntity player, float originX, float originY)
    {
        _host.GetCachedPlayerPresentationHitBounds(player, out var left, out var top, out var right, out var bottom);
        return ExplosionGeometry.GetDistanceToBounds(left, top, right, bottom, originX, originY);
    }
}
