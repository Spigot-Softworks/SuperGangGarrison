using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    internal GameplayPrimaryWeaponResult ExecuteWhippingCordPrimaryWeapon(GameplayPrimaryWeaponContext context)
    {
        WeaponHandler.StartWhippingCordSwing(context.Player, context.AimWorldX, context.AimWorldY);
        return GameplayPrimaryWeaponResult.HandledResult;
    }
}
