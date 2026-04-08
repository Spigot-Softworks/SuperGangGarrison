namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private sealed partial class WeaponFireHandler
    {
        public void FirePrimaryWeapon(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
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
            TryRegisterPrimaryWeaponFireSound(attacker, weaponDefinition, binding);
            DispatchPrimaryWeaponByKind(
                attacker,
                weaponDefinition,
                binding.WeaponKind,
                weaponClassId,
                aimWorldX,
                aimWorldY,
                killFeedWeaponSpriteNameOverride,
                resolvedKillFeedWeaponSpriteName);
        }

        private static GameplayPrimaryWeaponRuntimeBinding ResolvePrimaryWeaponRuntimeBinding(string? behaviorId, PrimaryWeaponDefinition weaponDefinition)
        {
            return CharacterClassCatalog.RuntimeRegistry.TryGetPrimaryWeaponBinding(behaviorId, out var binding)
                ? binding
                : new GameplayPrimaryWeaponRuntimeBinding(behaviorId ?? string.Empty, weaponDefinition.Kind);
        }

        private void TryRegisterPrimaryWeaponFireSound(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weaponDefinition,
            GameplayPrimaryWeaponRuntimeBinding binding)
        {
            var fireSoundName = weaponDefinition.FireSoundName ?? binding.FireSoundName;
            if (!string.IsNullOrWhiteSpace(fireSoundName))
            {
                RegisterSoundEvent(attacker, fireSoundName);
            }
        }

        private void DispatchPrimaryWeaponByKind(
            PlayerEntity attacker,
            PrimaryWeaponDefinition weaponDefinition,
            PrimaryWeaponKind weaponKind,
            PlayerClass weaponClassId,
            float aimWorldX,
            float aimWorldY,
            string? killFeedWeaponSpriteNameOverride,
            string resolvedKillFeedWeaponSpriteName)
        {
            switch (weaponKind)
            {
                case PrimaryWeaponKind.FlameThrower:
                    FireFlamethrower(attacker, weaponDefinition, weaponClassId, aimWorldX, aimWorldY);
                    return;
                case PrimaryWeaponKind.Blade:
                    FireBladeBubble(attacker, aimWorldX, aimWorldY);
                    return;
                case PrimaryWeaponKind.Minigun:
                    FireMinigun(attacker, weaponDefinition, weaponClassId, aimWorldX, aimWorldY, killFeedWeaponSpriteNameOverride);
                    return;
                case PrimaryWeaponKind.MineLauncher:
                    FireMineLauncher(attacker, weaponDefinition, weaponClassId, aimWorldX, aimWorldY, resolvedKillFeedWeaponSpriteName);
                    return;
                case PrimaryWeaponKind.Revolver:
                    FireRevolver(attacker, weaponDefinition, weaponClassId, aimWorldX, aimWorldY, resolvedKillFeedWeaponSpriteName);
                    return;
                case PrimaryWeaponKind.Rifle:
                    FireRifle(attacker, weaponClassId, aimWorldX, aimWorldY, resolvedKillFeedWeaponSpriteName);
                    return;
                case PrimaryWeaponKind.RocketLauncher:
                    FireRocketLauncher(attacker, weaponDefinition, weaponClassId, aimWorldX, aimWorldY, resolvedKillFeedWeaponSpriteName);
                    return;
                default:
                    FirePelletWeapon(attacker, weaponDefinition, aimWorldX, aimWorldY, weaponClassId, killFeedWeaponSpriteNameOverride);
                    return;
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
