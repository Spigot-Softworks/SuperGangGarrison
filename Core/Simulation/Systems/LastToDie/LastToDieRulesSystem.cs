namespace OpenGarrison.Core;

/// <summary>
/// Last-To-Die mode rules: perk builds and prediction profiles, status effects
/// and medic links, the medic javelin and sniper explosive tip, the spy
/// afterlife, and survivor/stage objectives. State lives in
/// <see cref="LastToDieState"/>; this system owns the rules.
/// </summary>
internal sealed partial class LastToDieRulesSystem
{
    private readonly ILastToDieHost _host;

    public LastToDieRulesSystem(ILastToDieHost host)
    {
        _host = host;
    }

    private float GetExplosionDistanceToPlayer(PlayerEntity player, float originX, float originY)
    {
        _host.GetCachedPlayerPresentationHitBounds(player, out var left, out var top, out var right, out var bottom);
        return ExplosionGeometry.GetDistanceToBounds(left, top, right, bottom, originX, originY);
    }

    private static void GetExplosionDirection(PlayerEntity player, float originX, float originY, out float deltaX, out float deltaY, out float distance)
        => ExplosionGeometry.GetDirectionToPlayerCenter(player, originX, originY, out deltaX, out deltaY, out distance);
}
