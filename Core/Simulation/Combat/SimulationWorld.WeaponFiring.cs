namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void FirePrimaryWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        => WeaponHandler.FirePrimaryWeapon(attacker, aimWorldX, aimWorldY);

    private void FireMedicNeedle(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        => WeaponHandler.FireMedicNeedle(attacker, aimWorldX, aimWorldY);

    private void FireMedicKritzHealNeedle(
        PlayerEntity attacker,
        float aimWorldX,
        float aimWorldY,
        int healPerHit = MedicHealNeedleProjectileEntity.DefaultHealPerHit,
        int enemyDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit,
        float projectileSpeed = MedicHealNeedleProjectileEntity.DefaultProjectileSpeed,
        float spreadDegrees = MedicHealNeedleProjectileEntity.DefaultSpreadDegrees)
        => WeaponHandler.FireMedicKritzHealNeedle(
            attacker,
            aimWorldX,
            aimWorldY,
            healPerHit,
            enemyDamagePerHit,
            projectileSpeed,
            spreadDegrees);
}
