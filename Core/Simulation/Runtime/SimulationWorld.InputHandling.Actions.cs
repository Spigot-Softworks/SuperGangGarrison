using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void TryHandleNetworkPrimaryFire(PlayerEntity player, PlayerInputSnapshot input, bool primaryPressed, bool suppressPyroPrimaryThisTick)
    {
        if (player.IsTaunting)
        {
            return;
        }

        if (TryHandleAcquiredPrimaryFire(player, input, suppressPyroPrimaryThisTick))
        {
            return;
        }

        if (TryHandleExperimentalOffhandPrimaryFire(player, input))
        {
            return;
        }

        if (TryHandleEquippedPrimaryFire(player, input, primaryPressed, suppressPyroPrimaryThisTick))
        {
            return;
        }

        // Mine launcher at limit: explode oldest mine to make room
        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.MineLauncher) && input.FirePrimary && CountOwnedMines(player.Id) >= player.PrimaryWeapon.MaxAmmo)
        {
            ExplodeOldestMine(player.Id);
        }

        if (primaryPressed && TryHandleExperimentalSoldierStingerPrimaryBurst(player))
        {
            return;
        }

        var ignorePrimaryAmmoCost = player.ClassId == PlayerClass.Soldier
            && player.IsRaging
            && ExperimentalGameplaySettings.EnableSoldierInfiniteAmmoDuringRage;
        if (!input.FirePrimary || !player.TryFirePrimaryWeapon(ignorePrimaryAmmoCost))
        {
            return;
        }

        FirePrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
    }

    private bool TryHandleExperimentalOffhandPrimaryFire(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (!input.FirePrimary
            || !player.IsExperimentalOffhandEquipped
            || !player.HasExperimentalOffhandWeapon)
        {
            return false;
        }

        if (!player.TryFireExperimentalOffhandWeapon())
        {
            return true;
        }

        WeaponHandler.FireSoldierShotgun(player, input.AimWorldX, input.AimWorldY);
        return true;
    }

    private bool TryHandleExperimentalSoldierStingerPrimaryBurst(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Soldier
            || !ExperimentalGameplaySettings.EnableSoldierStingerRockets
            || !IsExperimentalPracticePowerOwner(player))
        {
            return false;
        }

        for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            var rocket = _rockets[rocketIndex];
            if (rocket.OwnerId != player.Id
                || rocket.Team != player.Team
                || rocket.IsFading
                || !rocket.EnableExperimentalStingerTracking)
            {
                continue;
            }

            return rocket.TryApplyExperimentalStingerSpeedBurst(
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierStingerBurstSpeedMultiplier);
        }

        return false;
    }

    private bool TryHandleAcquiredPrimaryFire(PlayerEntity player, PlayerInputSnapshot input, bool suppressPyroPrimaryThisTick)
    {
        if (!player.IsAcquiredWeaponEquipped)
        {
            return false;
        }

        if (player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.Medigun))
        {
            if (input.FirePrimary)
            {
                TryTriggerAcquiredMedigunHealsplosion(player);
            }

            return true;
        }

        if (player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MineLauncher)
            && input.FirePrimary
            && CountOwnedMines(player.Id) >= player.AcquiredWeaponMaxShells)
        {
            return true;
        }

        if (player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.Flamethrower)
            && suppressPyroPrimaryThisTick)
        {
            return true;
        }

        if (!input.FirePrimary || !player.TryFireAcquiredWeapon())
        {
            return true;
        }

        WeaponHandler.FireAcquiredWeapon(player, input.AimWorldX, input.AimWorldY);
        return true;
    }

    private bool TryHandleEquippedPrimaryFire(PlayerEntity player, PlayerInputSnapshot input, bool primaryPressed, bool suppressPyroPrimaryThisTick)
    {
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

            return true;
        }

        if (player.IsExperimentalDemoknightEnabled)
        {
            if (!input.FirePrimary || !player.TryFireExperimentalDemoknightSword())
            {
                return true;
            }

            WeaponHandler.FireExperimentalDemoknightSword(player, input.AimWorldX, input.AimWorldY);
            return true;
        }

        if (primaryPressed && TryStartSpyBackstab(player, input.AimWorldX, input.AimWorldY))
        {
            return true;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Blade))
        {
            if (input.FirePrimary && player.TryFireQuoteBubble())
            {
                FirePrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
            }

            return true;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Flamethrower))
        {
            if (input.FirePrimary && !suppressPyroPrimaryThisTick)
            {
                WeaponHandler.TryFirePyroPrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
            }

            return true;
        }

        return false;
    }

    private void TryHandleNetworkSecondaryAbility(PlayerEntity player, PlayerInputSnapshot input, float sourceX, float sourceY)
    {
        // Allow demoman to detonate mines even while taunting
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        if (runtimeRegistry.TryGetSecondaryAbilityBinding(player.SecondaryBehaviorId, out var secondaryBinding)
            && secondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.DemomanDetonate
            && !player.IsExperimentalDemoknightEnabled)
        {
            DetonateOwnedMines(player.Id);
            return;
        }

        if (player.IsTaunting)
        {
            return;
        }

        if (TryHandleSoldierOffhandToggle(player))
        {
            return;
        }

        if (TryHandleExperimentalSoldierStingerDetonation(player))
        {
            return;
        }

        if (player.IsExperimentalDemoknightEnabled)
        {
            if (ExperimentalGameplaySettings.EnableDemoknightGhostDash)
            {
                if (player.TryStartExperimentalGhostDash(
                    GetExperimentalGhostDashDurationTicks(),
                    GetExperimentalGhostDashCooldownTicks(),
                    global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultGhostDashNextAttackDamageMultiplier,
                    GetExperimentalGhostDashImpulse()))
                {
                    RegisterWorldSoundEvent(ExperimentalDemoknightCatalog.ChargeStartSoundName, player.X, player.Y);
                }

                return;
            }

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

        runtimeRegistry.TryGetSecondaryAbilityBinding(player.EquippedBehaviorId, out var equippedBinding);
        runtimeRegistry.TryGetUtilityAbilityBinding(player.UtilityBehaviorId, out var utilityBinding);
        var resolvedSecondaryBinding = player.IsAcquiredWeaponEquipped ? equippedBinding : secondaryBinding;

        if (resolvedSecondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.SniperScope)
        {
            player.TryToggleSniperScope();
            return;
        }

        if (resolvedSecondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.EngineerPda)
        {
            if (!TryDestroySentry(player))
            {
                TryBuildSentry(player);
            }

            return;
        }

        if (resolvedSecondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.HeavySandvich)
        {
            player.TryStartHeavySelfHeal();
            return;
        }

        if (resolvedSecondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.PyroAirblast)
        {
            if (player.TryFirePyroAirblast())
            {
                TriggerPyroAirblast(player, input.AimWorldX, input.AimWorldY, input.FirePrimary);
            }

            return;
        }

        if (resolvedSecondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.SpyCloak)
        {
            if (!input.FirePrimary)
            {
                player.TryToggleSpyCloak();
            }

            return;
        }

        if (resolvedSecondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.MedicNeedlegun
            || utilityBinding.ActionKind == GameplayUtilityAbilityActionKind.MedicUber)
        {
            if (player.IsAcquiredWeaponEquipped)
            {
                if (player.TryFireAcquiredMedicNeedle())
                {
                    WeaponHandler.FireAcquiredMedicNeedle(player, input.AimWorldX, input.AimWorldY);
                    return;
                }
            }
            else if (player.TryFireMedicNeedle())
            {
                FireMedicNeedle(player, input.AimWorldX, input.AimWorldY);
                return;
            }

            if (player.IsMedicUberReady && input.FirePrimary && utilityBinding.ActionKind == GameplayUtilityAbilityActionKind.MedicUber)
            {
                if (player.TryStartMedicUber())
                {
                    AwardMedicUberActivationPoints(player);
                }
            }

            return;
        }

        if (resolvedSecondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.QuoteBladeThrow
            && player.TryFireQuoteBlade())
        {
            WeaponHandler.FireQuoteBlade(player, input.AimWorldX, input.AimWorldY);
        }
    }

    private static bool TryHandleSoldierOffhandToggle(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Soldier || !player.HasExperimentalOffhandWeapon)
        {
            return false;
        }

        if (player.IsAcquiredWeaponEquipped)
        {
            player.StowAcquiredWeapon();
        }

        if (player.IsExperimentalOffhandEquipped)
        {
            player.StowExperimentalOffhandWeapon();
        }
        else
        {
            player.EquipExperimentalOffhandWeapon();
        }

        return true;
    }

    private bool TryHandleExperimentalSoldierStingerDetonation(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Soldier
            || !ExperimentalGameplaySettings.EnableSoldierStingerRockets
            || !IsExperimentalPracticePowerOwner(player))
        {
            return false;
        }

        var detonatedAnyRocket = false;
        for (var rocketIndex = 0; rocketIndex < _rockets.Count; rocketIndex += 1)
        {
            var rocket = _rockets[rocketIndex];
            if (rocket.OwnerId != player.Id
                || rocket.Team != player.Team
                || rocket.IsFading
                || !rocket.EnableExperimentalStingerTracking)
            {
                continue;
            }

            rocket.ArmExperimentalManualDetonation();
            detonatedAnyRocket = true;
        }

        return detonatedAnyRocket;
    }

    private void TryHandleNetworkSecondaryWeaponFire(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (player.IsTaunting)
        {
            return;
        }

        if (!ExperimentalGameplaySettings.EnableSecondaryAbilities)
        {
            return;
        }

        if (TryHandleNetworkUtilityAbility(player, input))
        {
            return;
        }

        // Backward-compatible fallback for sessions where Soldier offhand is enabled
        // but utility loadout behaviors are unavailable.
        TryHandleLegacyNetworkSecondaryWeaponFire(player, input);
    }

    private bool TryHandleNetworkUtilityAbility(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.ScoutUtility))
        {
            player.TryStartTaunt();
            return true;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SoldierSecondaryWeapon))
        {
            TryHandleLegacyNetworkSecondaryWeaponFire(player, input);
            return true;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUtility)
            || player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber))
        {
            if (player.IsMedicUberReady && player.TryStartMedicUber())
            {
                AwardMedicUberActivationPoints(player);
            }

            return true;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.PyroUtility))
        {
            if (player.TryFirePyroAirblast())
            {
                TriggerPyroSelfAirblast(player, input.AimWorldX, input.AimWorldY, input.FirePrimary);
            }

            return true;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.EngineerUtility)
            || player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.DemomanUtility)
            || player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.HeavyUtility)
            || player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SniperUtility)
            || player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SpyUtility)
            || player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.QuoteUtility))
        {
            if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.EngineerUtility))
            {
                if (!TryDestroyJumpPad(player))
                {
                    TryBuildJumpPad(player);
                }

                return true;
            }

            TryHandleNetworkSecondaryAbility(player, input, player.X, player.Y);
            return true;
        }

        return false;
    }

    private static void TryHandleLegacyNetworkSecondaryWeaponFire(PlayerEntity player, PlayerInputSnapshot input)
    {
        _ = input;
        TryHandleSoldierOffhandToggle(player);
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
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        if (!runtimeRegistry.TryGetSecondaryAbilityBinding(player.SecondaryBehaviorId, out var secondaryBinding))
        {
            return false;
        }

        if (secondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.DemomanDetonate)
        {
            return !player.IsExperimentalDemoknightEnabled;
        }

        return secondaryBinding.UsesHeldInput;
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
