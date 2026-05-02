namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public void SetExperimentalPrimaryWeaponOverride(PrimaryWeaponDefinition? weaponDefinition)
    {
        if (weaponDefinition == ExperimentalPrimaryWeaponOverride)
        {
            if (weaponDefinition is not null)
            {
                CurrentShells = int.Clamp(CurrentShells, 0, weaponDefinition.MaxAmmo);
                PrimaryCooldownTicks = Math.Max(0, PrimaryCooldownTicks);
                ReloadTicksUntilNextShell = Math.Max(0, ReloadTicksUntilNextShell);
            }

            return;
        }

        ExperimentalPrimaryWeaponOverride = weaponDefinition;
        CurrentShells = int.Clamp(CurrentShells, 0, PrimaryWeapon.MaxAmmo);
        if (CurrentShells >= PrimaryWeapon.MaxAmmo)
        {
            ReloadTicksUntilNextShell = 0;
        }
        else if (PrimaryWeapon.AutoReloads && ReloadTicksUntilNextShell <= 0)
        {
            ReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(PrimaryWeapon.AmmoReloadTicks);
        }

        RefreshGameplayLoadoutState();
    }
}
