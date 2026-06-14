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
        int enemyDamagePerHit = MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit)
        => WeaponHandler.FireMedicKritzHealNeedle(
            attacker,
            attacker.ExperimentalOffhandWeapon ?? CharacterClassCatalog.Medigun,
            aimWorldX,
            aimWorldY,
            healPerHit,
            enemyDamagePerHit);
}
