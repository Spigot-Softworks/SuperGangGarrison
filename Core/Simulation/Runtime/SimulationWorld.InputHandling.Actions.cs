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

        if (player.IsExperimentalCryoFrozen)
        {
            player.ClearMedicHealingTarget();
            return;
        }

        if (TryHandleAcquiredPrimaryFire(player, input, suppressPyroPrimaryThisTick))
        {
            return;
        }

        if (player.HasExperimentalOffhandWeapon
            && input.FirePrimary
            && player.ClassId != PlayerClass.Engineer)
        {
            player.StowExperimentalOffhandWeapon();
        }

        if (TryHandleEquippedPrimaryFire(player, input, primaryPressed, suppressPyroPrimaryThisTick))
        {
            return;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.MineLauncher) && input.FirePrimary && CountOwnedMines(player.Id) >= player.PrimaryWeapon.MaxAmmo)
        {
            return;
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
        if (HasExperimentalEngineerFreezeRay(player))
        {
            if (input.FirePrimary)
            {
                UpdateExperimentalEngineerFreezeRay(player, input.AimWorldX, input.AimWorldY);
            }
            else
            {
                player.ClearMedicHealingTarget();
            }

            return true;
        }

        if (HasExperimentalEngineerEssenceExtractor(player))
        {
            if (input.FirePrimary)
            {
                UpdateExperimentalEngineerEssenceExtractor(player, input.AimWorldX, input.AimWorldY);
            }
            else
            {
                FlushExperimentalEngineerEssenceExtractorHealing(player);
                player.ClearMedicHealingTarget();
            }

            return true;
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
        if (player.IsTaunting)
        {
            return;
        }

        if (player.IsExperimentalCryoFrozen)
        {
            return;
        }

        if (TryHandleExperimentalSoldierStingerDetonation(player))
        {
            return;
        }

        if (TryHandleExperimentalSoldierCivilDefenseTurret(player))
        {
            return;
        }

        if (TryHandleExperimentalSoldierThundergunner(player, input))
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

        if (player.ClassId == PlayerClass.Engineer
            && HasExperimentalEngineerDestinyPunctuator(player))
        {
            TryHandleExperimentalEngineerDestinyPunctuatorBlast(player, input);
            return;
        }

        if (player.ClassId == PlayerClass.Engineer
            && player.IsExperimentalOffhandEquipped
            && HasExperimentalEngineerAlternateWeaponAvailable(player))
        {
            var maxOwnedSentries = GetExperimentalMaxOwnedSentries(player);
            if (maxOwnedSentries > 1)
            {
                if (GetExperimentalOwnedSentryCount(player.Id) < maxOwnedSentries)
                {
                    TryBuildSentry(player);
                    return;
                }
            }

            if (!TryDestroySentry(player))
            {
                TryBuildSentry(player);
            }

            return;
        }

        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        runtimeRegistry.TryGetSecondaryAbilityBinding(player.EquippedBehaviorId, out var equippedBinding);
        runtimeRegistry.TryGetSecondaryAbilityBinding(player.SecondaryBehaviorId, out var secondaryBinding);
        runtimeRegistry.TryGetUtilityAbilityBinding(player.UtilityBehaviorId, out var utilityBinding);
        var resolvedSecondaryBinding = player.IsAcquiredWeaponEquipped ? equippedBinding : secondaryBinding;

        if (resolvedSecondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.SniperScope)
        {
            player.TryToggleSniperScope();
            return;
        }

        if (resolvedSecondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.DemomanDetonate)
        {
            DetonateOwnedMines(player.Id);
            return;
        }

        if (resolvedSecondaryBinding.ActionKind == GameplaySecondaryAbilityActionKind.EngineerPda)
        {
            if (TryHandleExperimentalEngineerDestinyPunctuatorBlast(player, input))
            {
                return;
            }

            var maxOwnedSentries = GetExperimentalMaxOwnedSentries(player);
            if (maxOwnedSentries > 1)
            {
                if (GetExperimentalOwnedSentryCount(player.Id) < maxOwnedSentries)
                {
                    TryBuildSentry(player);
                    return;
                }
            }

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

    private bool TryHandleExperimentalSoldierCivilDefenseTurret(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Soldier
            || !ExperimentalGameplaySettings.EnableSoldierCivilDefenseTurret
            || !IsExperimentalPracticePowerOwner(player))
        {
            return false;
        }

        foreach (var turret in _civilDefenseTurrets)
        {
            if (turret.OwnerPlayerId == player.Id)
            {
                return false;
            }
        }

        if (!player.TryDeployExperimentalSoldierCivilDefenseTurret(
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierCivilDefenseTurretDeployCooldownTicks))
        {
            return false;
        }

        return TryDeployCivilDefenseTurret(player);
    }

    private bool TryHandleExperimentalSoldierThundergunner(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (player.ClassId != PlayerClass.Soldier
            || !ExperimentalGameplaySettings.EnableSoldierThundergunner
            || !IsExperimentalPracticePowerOwner(player)
            || !player.TryFireExperimentalSoldierThundergunner(
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierThundergunnerCooldownTicks))
        {
            return false;
        }

        TriggerExperimentalSoldierThundergunner(player, input.AimWorldX, input.AimWorldY);
        return true;
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

    private void TryHandleLegacyNetworkSecondaryWeaponFire(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (player.ClassId != PlayerClass.Soldier
            || !player.HasExperimentalOffhandWeapon)
        {
            return;
        }

        if (player.IsAcquiredWeaponEquipped)
        {
            player.StowAcquiredWeapon();
        }

        if (!player.IsExperimentalOffhandEquipped)
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
        if (TryHandleExperimentalEngineerAlternateWeaponInteraction(player))
        {
            return;
        }

        TryHandleDroppedWeaponInteraction(player);
    }

    private bool TryHandleExperimentalEngineerDestinyPunctuatorBlast(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (!HasExperimentalEngineerDestinyPunctuator(player)
            || player.ClassId != PlayerClass.Engineer)
        {
            return false;
        }

        if (player.IsExperimentalOffhandEquipped)
        {
            ClearExperimentalEngineerAlternateWeaponState(player);
            player.StowExperimentalOffhandWeapon();
        }

        if (!player.TryFireExperimentalEngineerDestinyPunctuatorBlast())
        {
            return true;
        }

        WeaponHandler.FireExperimentalEngineerDestinyPunctuatorBlast(
            player,
            input.AimWorldX,
            input.AimWorldY,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorPelletMultiplier);

        var aimRadians = MathF.Atan2(input.AimWorldY - player.Y, input.AimWorldX - player.X);
        var blastOriginX = player.X + MathF.Cos(aimRadians) * 16f;
        var blastOriginY = player.Y + MathF.Sin(aimRadians) * 12f;
        ApplyExplosionImpulse(
            player,
            blastOriginX,
            blastOriginY,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorSelfKnockbackPerTick);
        player.SetMovementState(LegacyMovementState.ExplosionRecovery);
        return true;
    }

    private bool TryHandleExperimentalEngineerAlternateWeaponInteraction(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Engineer)
        {
            return false;
        }

        var essenceAvailable = HasExperimentalEngineerEssenceExtractorAvailable(player);
        var freezeAvailable = HasExperimentalEngineerFreezeRayAvailable(player);
        if (!HasExperimentalEngineerAlternateWeaponAvailable(player))
        {
            return false;
        }

        if (!player.HasExperimentalOffhandWeapon)
        {
            player.SetExperimentalOffhandWeapon(CharacterClassCatalog.Medigun);
        }

        if (!player.IsExperimentalOffhandEquipped)
        {
            player.SetExperimentalEngineerAlternateWeaponMode(
                GetExperimentalEngineerDefaultAlternateWeaponMode(player));
            player.EquipExperimentalOffhandWeapon();
            return true;
        }

        if (player.ExperimentalEngineerAlternateWeaponMode == ExperimentalEngineerAlternateWeaponMode.EssenceExtractor
            && freezeAvailable)
        {
            ClearExperimentalEngineerAlternateWeaponState(player);
            player.SetExperimentalEngineerAlternateWeaponMode(ExperimentalEngineerAlternateWeaponMode.FreezeRay);
            return true;
        }

        ClearExperimentalEngineerAlternateWeaponState(player);
        player.SetExperimentalEngineerAlternateWeaponMode(ExperimentalEngineerAlternateWeaponMode.None);
        player.StowExperimentalOffhandWeapon();
        return true;
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
