namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void TryHandleNetworkPrimaryFire(PlayerEntity player, PlayerInputSnapshot input, bool suppressPyroPrimaryThisTick)
    {
        if (player.IsTaunting)
        {
            return;
        }

        if (player.IsAcquiredWeaponEquipped)
        {
            if (player.AcquiredWeapon?.Kind == PrimaryWeaponKind.Medigun)
            {
                if (input.FirePrimary)
                {
                    TryTriggerAcquiredMedigunHealsplosion(player);
                }

                return;
            }

            if (player.AcquiredWeapon?.Kind == PrimaryWeaponKind.MineLauncher
                && input.FirePrimary
                && CountOwnedMines(player.Id) >= player.AcquiredWeaponMaxShells)
            {
                return;
            }

            if (player.AcquiredWeapon?.Kind == PrimaryWeaponKind.FlameThrower
                && suppressPyroPrimaryThisTick)
            {
                return;
            }

            if (!input.FirePrimary || !player.TryFireAcquiredWeapon())
            {
                return;
            }

            WeaponHandler.FireAcquiredWeapon(player, input.AimWorldX, input.AimWorldY);
            return;
        }

        if (player.HasExperimentalOffhandWeapon && input.FirePrimary)
        {
            player.StowExperimentalOffhandWeapon();
        }

        if (player.PrimaryWeapon.Kind == PrimaryWeaponKind.Medigun)
        {
            if (input.FirePrimary)
            {
                UpdateMedicHealing(player, input.AimWorldX, input.AimWorldY);
            }
            else
            {
                player.ClearMedicHealingTarget();
            }

            return;
        }

        if (player.IsExperimentalDemoknightEnabled)
        {
            if (!input.FirePrimary || !player.TryFireExperimentalDemoknightSword())
            {
                return;
            }

            WeaponHandler.FireExperimentalDemoknightSword(player, input.AimWorldX, input.AimWorldY);
            return;
        }

        if (input.FirePrimary && TryStartSpyBackstab(player, input.AimWorldX, input.AimWorldY))
        {
            return;
        }

        if (player.ClassId == PlayerClass.Quote)
        {
            if (input.FirePrimary && player.TryFireQuoteBubble())
            {
                FirePrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
            }

            return;
        }

        if (player.ClassId == PlayerClass.Pyro)
        {
            if (input.FirePrimary && !suppressPyroPrimaryThisTick)
            {
                WeaponHandler.TryFirePyroPrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
            }

            return;
        }

        if (player.ClassId == PlayerClass.Demoman && input.FirePrimary && CountOwnedMines(player.Id) >= player.PrimaryWeapon.MaxAmmo)
        {
            return;
        }

        if (!input.FirePrimary || !player.TryFirePrimaryWeapon())
        {
            return;
        }

        FirePrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
    }

    private void TryHandleNetworkSecondaryAbility(PlayerEntity player, PlayerInputSnapshot input, float sourceX, float sourceY)
    {
        if (player.IsTaunting)
        {
            return;
        }

        if (player.IsAcquiredWeaponEquipped
            && player.AcquiredWeapon?.Kind == PrimaryWeaponKind.Medigun)
        {
            if (player.TryFireAcquiredMedicNeedle())
            {
                WeaponHandler.FireAcquiredMedicNeedle(player, input.AimWorldX, input.AimWorldY);
            }

            return;
        }

        if (player.IsAcquiredWeaponEquipped
            && player.AcquiredWeapon?.Kind == PrimaryWeaponKind.FlameThrower)
        {
            if (player.TryFirePyroAirblast())
            {
                TriggerPyroAirblast(player, input.AimWorldX, input.AimWorldY, input.FirePrimary);
            }

            return;
        }

        if (player.IsAcquiredWeaponEquipped
            && player.AcquiredWeapon?.Kind == PrimaryWeaponKind.MineLauncher)
        {
            DetonateOwnedMines(player.Id);
            return;
        }

        if (player.HasScopedSniperWeaponEquipped)
        {
            player.TryToggleSniperScope();
            return;
        }

        if (player.ClassId == PlayerClass.Demoman)
        {
            if (player.IsExperimentalDemoknightEnabled)
            {
                if (player.IsExperimentalDemoknightCharging)
                {
                    player.CancelExperimentalDemoknightCharge(depleteMeter: true);
                }
                else if (player.TryStartExperimentalDemoknightCharge())
                {
                    RegisterWorldSoundEvent(ExperimentalDemoknightCatalog.ChargeStartSoundName, player.X, player.Y);
                }

                return;
            }

            DetonateOwnedMines(player.Id);
            return;
        }

        if (player.ClassId == PlayerClass.Engineer)
        {
            if (!TryDestroySentry(player))
            {
                TryBuildSentry(player);
            }

            return;
        }

        if (player.ClassId == PlayerClass.Heavy)
        {
            player.TryStartHeavySelfHeal();
            return;
        }

        if (player.ClassId == PlayerClass.Pyro)
        {
            if (player.TryFirePyroAirblast())
            {
                TriggerPyroAirblast(player, input.AimWorldX, input.AimWorldY, input.FirePrimary);
            }

            return;
        }

        if (player.ClassId == PlayerClass.Spy)
        {
            if (!input.FirePrimary)
            {
                player.TryToggleSpyCloak();
            }

            return;
        }

        if (player.ClassId == PlayerClass.Medic)
        {
            if (player.TryFireMedicNeedle())
            {
                FireMedicNeedle(player, input.AimWorldX, input.AimWorldY);
                return;
            }

            if (player.IsMedicUberReady && input.FirePrimary)
            {
                if (player.TryStartMedicUber())
                {
                    AwardMedicUberActivationPoints(player);
                }
            }
        }

        if (player.ClassId == PlayerClass.Quote && player.TryFireQuoteBlade())
        {
            WeaponHandler.FireQuoteBlade(player, input.AimWorldX, input.AimWorldY);
        }
    }

    private void TryHandleNetworkSecondaryWeaponFire(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (player.IsTaunting)
        {
            return;
        }

        if (player.ClassId != PlayerClass.Soldier
            || !player.HasExperimentalOffhandWeapon)
        {
            return;
        }

        if (!player.IsAcquiredWeaponEquipped)
        {
            player.EquipExperimentalOffhandWeapon();
        }

        if (!player.TryFireExperimentalOffhandWeapon())
        {
            return;
        }

        WeaponHandler.FireExperimentalSoldierShotgun(player, input.AimWorldX, input.AimWorldY);
    }

    private void TryHandleNetworkWeaponInteraction(PlayerEntity player)
    {
        TryHandleDroppedWeaponInteraction(player);
    }

    private bool TryStartSpyBackstab(PlayerEntity attacker, float aimWorldX, float aimWorldY)
    {
        if (attacker.ClassId != PlayerClass.Spy || !attacker.IsSpyCloaked)
        {
            return false;
        }

        var directionDegrees = PointDirectionDegrees(attacker.X, attacker.Y, aimWorldX, aimWorldY);
        if (!attacker.TryStartSpyBackstab(directionDegrees))
        {
            return false;
        }

        SpawnStabAnimation(attacker, directionDegrees);
        return true;
    }

    private static bool ShouldUseHeldSecondaryAbility(PlayerEntity player)
    {
        if (player.ClassId == PlayerClass.Demoman)
        {
            return !player.IsExperimentalDemoknightEnabled;
        }

        return player.ClassId == PlayerClass.Quote;
    }

    private void TryActivatePendingSpyBackstab(PlayerEntity player)
    {
        if (!player.TryConsumeSpyBackstabHitboxTrigger(out var directionDegrees))
        {
            return;
        }

        SpawnStabMask(player, directionDegrees);
    }
}
