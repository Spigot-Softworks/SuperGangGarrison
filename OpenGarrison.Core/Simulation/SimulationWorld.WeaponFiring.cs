namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void FirePrimaryWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        => WeaponHandler.FirePrimaryWeapon(attacker, aimWorldX, aimWorldY);

    private void FireMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        => WeaponHandler.FireMedicNeedle(attacker, aimWorldX, aimWorldY);
}
