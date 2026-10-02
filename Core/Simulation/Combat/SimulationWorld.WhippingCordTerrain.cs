using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool TryFindWhippingCordTerrainContact(PlayerEntity player, float aimWorldX, float aimWorldY, out float contactX, out float contactY)
        => GeometryResolver.TryFindWhippingCordTerrainContact(player, aimWorldX, aimWorldY, out contactX, out contactY);
    private bool TryFindWhippingCordTerrainContact(PlayerEntity player, GameplayItemDefinition item, float aimWorldX, float aimWorldY, out float contactX, out float contactY)
        => GeometryResolver.TryFindWhippingCordTerrainContact(player, item, aimWorldX, aimWorldY, out contactX, out contactY);
}
