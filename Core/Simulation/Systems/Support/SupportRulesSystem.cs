namespace OpenGarrison.Core;

/// <summary>
/// Support beams and auras: medigun healing, uber and kritz, the experimental
/// engineer essence extractor and freeze ray, the acquired medigun healsplosion,
/// and the buff banner. State lives on players; this system owns the rules.
/// </summary>
internal sealed partial class SupportRulesSystem
{
    private readonly ISupportRulesHost _host;

    public SupportRulesSystem(ISupportRulesHost host)
    {
        _host = host;
    }

    private float GetExplosionDistanceToPlayer(PlayerEntity player, float originX, float originY)
    {
        _host.PresentationBounds.GetCachedPlayerPresentationHitBounds(player, out var left, out var top, out var right, out var bottom);
        return ExplosionGeometry.GetDistanceToBounds(left, top, right, bottom, originX, originY);
    }
}
