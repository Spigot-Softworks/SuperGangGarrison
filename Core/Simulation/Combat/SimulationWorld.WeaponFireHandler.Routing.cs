namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        public void FirePrimaryWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            if (attacker.PrimaryWeapon.Kind != PrimaryWeaponKind.FlameThrower)
            {
                RegisterWeaponFireSound(attacker, attacker.PrimaryWeapon);
            }

            switch (attacker.PrimaryWeapon.Kind)
            {
                case PrimaryWeaponKind.FlameThrower:
                    FireFlamethrower(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Blade:
                    FireBladeBubble(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Minigun:
                    FireMinigun(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.MineLauncher:
                    FireMineLauncher(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Revolver:
                    FireRevolver(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Rifle:
                    FireRifle(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.RocketLauncher:
                    FireRocketLauncher(attacker, aimWorldX, aimWorldY);
                    break;
                default:
                    FirePelletWeapon(attacker, attacker.PrimaryWeapon, aimWorldX, aimWorldY, attacker.ClassId);
                    break;
            }
        }

        public void FireExperimentalSoldierShotgun(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponDefinition = attacker.ExperimentalOffhandWeapon ?? CharacterClassCatalog.Shotgun;
            RegisterWeaponFireSound(attacker, weaponDefinition);
            FirePelletWeapon(
                attacker,
                weaponDefinition,
                aimWorldX,
                aimWorldY,
                PlayerClass.Engineer,
                killFeedWeaponSpriteNameOverride: "ShotgunKL");
        }

        public void FireAcquiredWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponClassId = attacker.AcquiredWeaponClassId;
            var weaponDefinition = attacker.AcquiredWeapon;
            if (!weaponClassId.HasValue || weaponDefinition is null)
            {
                return;
            }

            var killFeedWeaponSpriteNameOverride = CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(weaponClassId.Value);
            switch (weaponDefinition.Kind)
            {
                case PrimaryWeaponKind.FlameThrower:
                    FireFlamethrower(attacker, weaponClassId.Value, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Minigun:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FireMinigun(attacker, weaponDefinition, weaponClassId.Value, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.MineLauncher:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FireMineLauncher(attacker, weaponDefinition, weaponClassId.Value, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.Revolver:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FireRevolver(attacker, weaponDefinition, weaponClassId.Value, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.Rifle:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FireRifle(attacker, weaponClassId.Value, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.RocketLauncher:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FireRocketLauncher(attacker, weaponDefinition, weaponClassId.Value, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.PelletGun:
                    RegisterWeaponFireSound(attacker, weaponDefinition);
                    FirePelletWeapon(attacker, weaponDefinition, aimWorldX, aimWorldY, weaponClassId.Value, killFeedWeaponSpriteNameOverride);
                    break;
            }
        }

        public bool TryFirePyroPrimaryWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            if (!attacker.TryPreparePyroPrimaryFireAttempt())
            {
                return false;
            }

            var shouldStartLoopSound = attacker.PyroFlameLoopTicksRemaining <= 0;
            if (!FireFlamethrower(attacker, aimWorldX, aimWorldY))
            {
                return false;
            }

            attacker.CommitPyroPrimaryWeaponShot();
            if (shouldStartLoopSound)
            {
                RegisterSoundEvent(attacker, "FlamethrowerSnd");
            }

            return true;
        }
    }
}
