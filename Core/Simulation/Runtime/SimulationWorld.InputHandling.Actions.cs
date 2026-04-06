using OpenGarrison.GameplayModding;

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
            if (player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.Medigun))
            {
                if (input.FirePrimary)
                {
                    TryTriggerAcquiredMedigunHealsplosion(player);
                }

                return;
            }

            if (player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MineLauncher)
                && input.FirePrimary
                && CountOwnedMines(player.Id) >= player.AcquiredWeaponMaxShells)
            {
                return;
            }

            if (player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.Flamethrower)
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

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Medigun))
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

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Blade))
        {
            if (input.FirePrimary && player.TryFireQuoteBubble())
            {
                FirePrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
            }

            return;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Flamethrower))
        {
            if (input.FirePrimary && !suppressPyroPrimaryThisTick)
            {
                WeaponHandler.TryFirePyroPrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
            }

            return;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.MineLauncher) && input.FirePrimary && CountOwnedMines(player.Id) >= player.PrimaryWeapon.MaxAmmo)
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
            && player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.Medigun))
        {
            if (player.TryFireAcquiredMedicNeedle())
            {
                WeaponHandler.FireAcquiredMedicNeedle(player, input.AimWorldX, input.AimWorldY);
            }

            return;
        }

        if (player.IsAcquiredWeaponEquipped
            && player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.Flamethrower))
        {
            if (player.TryFirePyroAirblast())
            {
                TriggerPyroAirblast(player, input.AimWorldX, input.AimWorldY, input.FirePrimary);
            }

            return;
        }

        if (player.IsAcquiredWeaponEquipped
            && player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MineLauncher))
        {
            DetonateOwnedMines(player.Id);
            return;
        }

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.SniperScope)
            || player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.Rifle))
        {
            player.TryToggleSniperScope();
            return;
        }

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.DemomanDetonate))
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

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.EngineerPda))
        {
            if (!TryDestroySentry(player))
            {
                TryBuildSentry(player);
            }

            return;
        }

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.HeavySandvich))
        {
            player.TryStartHeavySelfHeal();
            return;
        }

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.PyroAirblast))
        {
            if (player.TryFirePyroAirblast())
            {
                TriggerPyroAirblast(player, input.AimWorldX, input.AimWorldY, input.FirePrimary);
            }

            return;
        }

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.SpyCloak))
        {
            if (!input.FirePrimary)
            {
                player.TryToggleSpyCloak();
            }

            return;
        }

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.MedicNeedlegun)
            || player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber))
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

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.QuoteBladeThrow) && player.TryFireQuoteBlade())
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
        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.DemomanDetonate))
        {
            return !player.IsExperimentalDemoknightEnabled;
        }

        return player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.QuoteBladeThrow);
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
