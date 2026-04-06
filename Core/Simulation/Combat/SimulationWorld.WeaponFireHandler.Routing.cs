namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        public void FirePrimaryWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var binding = ResolvePrimaryWeaponRuntimeBinding(attacker.PrimaryBehaviorId, attacker.PrimaryWeapon);
            if (!string.IsNullOrWhiteSpace(binding.FireSoundName))
            {
                RegisterSoundEvent(attacker, binding.FireSoundName);
            }

            DispatchPrimaryWeaponFire(attacker, attacker.PrimaryWeapon, attacker.PrimaryBehaviorId, attacker.ClassId, aimWorldX, aimWorldY);
        }

        public void FireExperimentalSoldierShotgun(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var weaponDefinition = attacker.ExperimentalOffhandWeapon ?? CharacterClassCatalog.Shotgun;
            DispatchPrimaryWeaponFire(
                attacker,
                weaponDefinition,
                attacker.SecondaryBehaviorId,
                PlayerClass.Engineer,
                aimWorldX,
                aimWorldY,
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
            DispatchPrimaryWeaponFire(
                attacker,
                weaponDefinition,
                attacker.AcquiredBehaviorId,
                weaponClassId.Value,
                aimWorldX,
                aimWorldY,
                killFeedWeaponSpriteNameOverride);
        }

        private void DispatchPrimaryWeaponFire(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weaponDefinition,
            string? behaviorId,
            PlayerClass weaponClassId,
            float aimWorldX,
            float aimWorldY,
            string? killFeedWeaponSpriteNameOverride = null)
        {
            var binding = ResolvePrimaryWeaponRuntimeBinding(behaviorId, weaponDefinition);
            var resolvedKillFeedWeaponSpriteName = killFeedWeaponSpriteNameOverride ?? CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(weaponClassId);
            if (!string.IsNullOrWhiteSpace(binding.FireSoundName))
            {
                RegisterSoundEvent(attacker, binding.FireSoundName);
            }

            switch (binding.WeaponKind)
            {
                case PrimaryWeaponKind.FlameThrower:
                    FireFlamethrower(attacker, weaponClassId, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Blade:
                    FireBladeBubble(attacker, aimWorldX, aimWorldY);
                    break;
                case PrimaryWeaponKind.Minigun:
                    FireMinigun(attacker, weaponDefinition, weaponClassId, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    break;
                case PrimaryWeaponKind.MineLauncher:
                    FireMineLauncher(attacker, weaponDefinition, weaponClassId, aimWorldX, aimWorldY, resolvedKillFeedWeaponSpriteName);
                    break;
                case PrimaryWeaponKind.Revolver:
                    FireRevolver(attacker, weaponDefinition, weaponClassId, aimWorldX, aimWorldY, resolvedKillFeedWeaponSpriteName);
                    break;
                case PrimaryWeaponKind.Rifle:
                    FireRifle(attacker, weaponClassId, aimWorldX, aimWorldY, resolvedKillFeedWeaponSpriteName);
                    break;
                case PrimaryWeaponKind.RocketLauncher:
                    FireRocketLauncher(attacker, weaponDefinition, weaponClassId, aimWorldX, aimWorldY, resolvedKillFeedWeaponSpriteName);
                    break;
                default:
                    FirePelletWeapon(attacker, weaponDefinition, aimWorldX, aimWorldY, weaponClassId, killFeedWeaponSpriteNameOverride);
                    break;
            }
        }

        private static GameplayPrimaryWeaponRuntimeBinding ResolvePrimaryWeaponRuntimeBinding(string? behaviorId, PrimaryWeaponDefinition weaponDefinition)
        {
            return CharacterClassCatalog.RuntimeRegistry.TryGetPrimaryWeaponBinding(behaviorId, out var binding)
                ? binding
                : new GameplayPrimaryWeaponRuntimeBinding(behaviorId ?? string.Empty, weaponDefinition.Kind);
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
