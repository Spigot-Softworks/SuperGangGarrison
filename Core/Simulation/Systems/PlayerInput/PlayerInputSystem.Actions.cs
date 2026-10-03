using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

internal sealed partial class PlayerInputSystem
{
    internal void TryHandleNetworkPrimaryFire(PlayerEntity player, PlayerInputSnapshot input, PlayerInputSnapshot previousInput, bool primaryPressed, bool suppressPyroPrimaryThisTick)
    {
        if (player.IsTaunting || player.IsBuffBannerDeploying)
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

        if (TryHandleSniperBowPrimaryFire(player, input, previousInput))
        {
            return;
        }

        if (TryHandleMortarLauncherPrimaryFire(player, input, previousInput))
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

        if (primaryPressed && TryHandleExperimentalSoldierStingerPrimaryBurst(player))
        {
            return;
        }

        var ignorePrimaryAmmoCost = player.ClassId == PlayerClass.Soldier
            && player.IsRaging
            && _host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableSoldierInfiniteAmmoDuringRage;
        var isLastToDieProfessionalFireChord = input.FireSecondary
            && player.CanFireLastToDieProfessionalRevolverWhileCloaked;
        if (!input.FirePrimary || !player.TryFirePrimaryWeapon(ignorePrimaryAmmoCost))
        {
            return;
        }

        if (isLastToDieProfessionalFireChord)
        {
            _ = player.MarkLastToDieProfessionalFireChordConsumed();
        }

        _host.WeaponHandler.FirePrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
    }

    private bool TryHandleSniperBowPrimaryFire(PlayerEntity player, PlayerInputSnapshot input, PlayerInputSnapshot previousInput)
    {
        if (!player.IsSniperBowEquipped)
        {
            return false;
        }

        var directionDegrees = SimulationMath.PointDirectionDegrees(player.X, player.Y, input.AimWorldX, input.AimWorldY);
        if (!input.FirePrimary && previousInput.FirePrimary)
        {
            if (player.TryReleaseSniperBowCharge(out var velocityX, out var velocityY, out var damage, out var fakeSpeedMultiplier)
                && player.TryFirePrimaryWeapon())
            {
                _host.WeaponHandler.FireSniperBow(
                    player,
                    player.PrimaryWeapon,
                    input.AimWorldX,
                    input.AimWorldY,
                    velocityX,
                    velocityY,
                    damage,
                    fakeSpeedMultiplier,
                    "BowKL");
            }
            else
            {
                player.CancelSniperBowCharge();
            }

            return true;
        }

        if (input.FirePrimary)
        {
            if (player.SniperBowChargeTicks == 0)
            {
                _ = player.TryStartSniperBowCharge(directionDegrees);
            }
            else
            {
                player.IncrementSniperBowCharge(directionDegrees);
            }

            return true;
        }

        return false;
    }

    private bool TryHandleMortarLauncherPrimaryFire(PlayerEntity player, PlayerInputSnapshot input, PlayerInputSnapshot previousInput)
    {
        if (!player.IsMortarLauncherEquipped)
        {
            return false;
        }

        var directionDegrees = SimulationMath.PointDirectionDegrees(player.X, player.Y, input.AimWorldX, input.AimWorldY);
        if (!input.FirePrimary && previousInput.FirePrimary)
        {
            if (player.TryReleaseMortarLauncherCharge(out var chargeFraction, out var directionRadians)
                && player.TryFirePrimaryWeapon())
            {
                _host.WeaponHandler.FireMortarLauncher(
                    player,
                    player.PrimaryWeapon,
                    directionRadians,
                    chargeFraction);
            }
            else
            {
                player.CancelMortarLauncherCharge();
            }

            return true;
        }

        if (input.FirePrimary)
        {
            if (player.MortarLauncherChargeTicks == 0)
            {
                _ = player.TryStartMortarLauncherCharge(directionDegrees);
            }
            else
            {
                player.IncrementMortarLauncherCharge(directionDegrees);
            }

            return true;
        }

        return false;
    }

    private bool TryHandleExperimentalOffhandPrimaryFire(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (TryHandleExperimentalEngineerBeamPrimaryFire(player, input))
        {
            return true;
        }

        if (!input.FirePrimary
            || !player.IsExperimentalOffhandSelected)
        {
            return false;
        }

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.Medigun)
            || player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.MedigunCrit))
        {
            _host.SupportRules.UpdateMedicHealing(player, input.AimWorldX, input.AimWorldY);
            return true;
        }

        if (!player.TryFireExperimentalOffhandWeapon())
        {
            return true;
        }

        if (player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.GrenadeLauncher))
        {
            _host.WeaponHandler.FireGrenadeLauncher(player, input.AimWorldX, input.AimWorldY);
            return true;
        }

        var offhandBehaviorId = player.EquippedBehaviorId ?? player.SecondaryBehaviorId ?? player.UtilityBehaviorId;
        if (player.ClassId == PlayerClass.Soldier
            && player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.PelletGun))
        {
            _host.WeaponHandler.FireSoldierShotgun(player, input.AimWorldX, input.AimWorldY);
            return true;
        }

        _host.WeaponHandler.FireExperimentalOffhandWeapon(player, offhandBehaviorId, input.AimWorldX, input.AimWorldY);
        return true;
    }

    private bool TryHandleExperimentalSoldierStingerPrimaryBurst(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Soldier
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableSoldierStingerRockets
            || !_host.ExperimentalRules.IsExperimentalPracticePowerOwner(player))
        {
            return false;
        }

        for (var rocketIndex = _host.Rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            var rocket = _host.Rockets[rocketIndex];
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
                _host.SupportRules.TryTriggerAcquiredMedigunHealsplosion(player);
            }

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

        _host.WeaponHandler.FireAcquiredWeapon(player, input.AimWorldX, input.AimWorldY);
        return true;
    }

    private bool TryHandleEquippedPrimaryFire(PlayerEntity player, PlayerInputSnapshot input, bool primaryPressed, bool suppressPyroPrimaryThisTick)
    {
        if (TryHandleExperimentalEngineerBeamPrimaryFire(player, input))
        {
            return true;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Medigun)
            || player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.MedigunCrit))
        {
            if (input.FirePrimary)
            {
                _host.SupportRules.UpdateMedicHealing(player, input.AimWorldX, input.AimWorldY);
            }
            else if (!input.FireSecondary || !player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit))
            {
                player.ClearMedicHealingTarget();
            }

            return true;
        }

        if (player.IsExperimentalDemoknightEnabled)
        {
            if (input.FirePrimary && player.TryFireExperimentalDemoknightSword())
            {
                _host.WeaponHandler.StartExperimentalDemoknightSwordSwing(player, input.AimWorldX, input.AimWorldY);
            }
            else
            {
                _host.WeaponHandler.AdvanceExperimentalDemoknightSwordSwing(player, input.AimWorldX, input.AimWorldY);
            }

            return true;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.WhippingCord))
        {
            if (input.FirePrimary && player.TryFireWhippingCord())
            {
                _host.WeaponHandler.StartWhippingCordSwing(player, input.AimWorldX, input.AimWorldY);
            }
            else
            {
                _host.WeaponHandler.AdvanceWhippingCordSwing(player, input.AimWorldX, input.AimWorldY, input.FirePrimary);
            }

            return true;
        }

        if (input.FirePrimary
            && player.ClassId == PlayerClass.Spy
            && player.IsSpyCloaked)
        {
            // A cloaked M1 is always the stock knife windup. The Professional
            // explicitly changes M1 to the revolver only while M2 is held.
            var isLastToDieProfessionalFireChord = input.FireSecondary
                && player.CanFireLastToDieProfessionalRevolverWhileCloaked;
            if (!isLastToDieProfessionalFireChord && player.IsSpyBackstabReady)
            {
                _ = TryStartSpyBackstab(player, input.AimWorldX, input.AimWorldY);
                return true;
            }

            return !isLastToDieProfessionalFireChord;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Blade))
        {
            var activeProjectileLimit = player.PrimaryWeapon.ActiveProjectileLimit ?? PlayerEntity.QuoteBubbleLimit;
            if (input.FirePrimary && player.TryFireQuoteBubble(activeProjectileLimit))
            {
                _host.WeaponHandler.FirePrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
            }

            return true;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Flamethrower))
        {
            if (input.FirePrimary && !suppressPyroPrimaryThisTick)
            {
                _host.WeaponHandler.TryFirePyroPrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
            }

            return true;
        }

        return false;
    }

    private bool TryHandleExperimentalEngineerBeamPrimaryFire(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (_host.ExperimentalRules.HasExperimentalEngineerFreezeRay(player))
        {
            if (input.FirePrimary)
            {
                _host.SupportRules.UpdateExperimentalEngineerFreezeRay(player, input.AimWorldX, input.AimWorldY);
            }
            else
            {
                player.ClearMedicHealingTarget();
            }

            return true;
        }

        if (_host.ExperimentalRules.HasExperimentalEngineerEssenceExtractor(player))
        {
            if (input.FirePrimary)
            {
                _host.SupportRules.UpdateExperimentalEngineerEssenceExtractor(player, input.AimWorldX, input.AimWorldY);
            }
            else
            {
                _host.SupportRules.FlushExperimentalEngineerEssenceExtractorHealing(player);
                player.ClearMedicHealingTarget();
            }

            return true;
        }

        return false;
    }

    internal GameplayAbilityResult TryHandleNetworkSecondaryAbility(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        GameplayAbilityInputPhase phase,
        float sourceX,
        float sourceY)
    {
        var dispatchResult = _host.Abilities.TryDispatchGameplayAbility(
            player,
            input,
            previousInput,
            phase,
            GameplayAbilityConstants.SpecialChannel,
            sourceX,
            sourceY);
        if (dispatchResult.ConsumedInput)
        {
            return dispatchResult;
        }

        if (player.IsTaunting)
        {
            return GameplayAbilityResult.Ignored;
        }

        if (player.IsExperimentalCryoFrozen)
        {
            return GameplayAbilityResult.Ignored;
        }

        return GameplayAbilityResult.Ignored;
    }

    internal void HandleEngineerPdaSentryCommand(PlayerEntity player)
    {
        var maxOwnedSentries = _host.ExperimentalRules.GetExperimentalMaxOwnedSentries(player);
        var ownedSentryCount = _host.ExperimentalRules.GetExperimentalOwnedSentryCount(player.Id);
        if (maxOwnedSentries > 1 && ownedSentryCount < maxOwnedSentries)
        {
            // A failed second placement must never turn into a destroy command.
            // This is especially important when the engineer is still standing
            // near the first sentry, or does not have enough metal yet.
            _ = _host.Structures.TryBuildSentry(player);
            return;
        }

        var destroyResult = _host.Structures.TryDestroySentryForOwnerCommand(player);
        if (destroyResult == OwnedSentryDestroyResult.NoOwnedSentry)
        {
            _host.Structures.TryBuildSentry(player);
        }
    }

    private bool TryHandleSecondaryWeaponToggle(PlayerEntity player)
    {
        // LTD Engineer beams share a medigun slot but need their own mode.
        // A generic Q equip leaves that mode at None, so the next passive
        // update immediately stows the weapon as invalid.
        if (_host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableSecondaryAbilities
            && TryHandleExperimentalEngineerAlternateWeaponInteraction(player))
        {
            return true;
        }

        if (!player.HasExperimentalOffhandWeapon)
        {
            return false;
        }

        var isOffhandSelected = player.IsExperimentalOffhandSelected;
        if (player.IsAcquiredWeaponEquipped)
        {
            player.StowAcquiredWeapon();
        }

        if (isOffhandSelected)
        {
            player.StowExperimentalOffhandWeapon();
        }
        else
        {
            player.EquipExperimentalOffhandWeapon();
        }

        return true;
    }

    private bool TryHandleNetworkWeaponSwap(PlayerEntity player)
    {
        if (player.IsTaunting
            || player.IsExperimentalCryoFrozen)
        {
            return false;
        }

        if (player.ClassId == PlayerClass.Sniper
            && _host.NetworkPlayers.TryGetPlayerNetworkSlot(player, out var sniperSlot)
            && (_host.LastToDieState.PerkRuntimesBySlot.ContainsKey(sniperSlot)
                || _host.LastToDieState.LegacyGameplaySettingsBySlot.ContainsKey(sniperSlot)))
        {
            return TryCycleLastToDieSniperWeapon(player);
        }

        if (player.HasAlternatePrimaryWeapons)
        {
            if (_host.Structures.IsNearPrimaryWeaponSwapStation(player))
            {
                return player.TryCycleGameplayPrimaryItem();
            }
        }

        return TryHandleSecondaryWeaponToggle(player);
    }

    private static bool TryCycleLastToDieSniperWeapon(PlayerEntity player)
    {
        if (!player.HasExperimentalOffhandWeapon)
        {
            return false;
        }

        if (player.IsExperimentalOffhandSelected)
        {
            player.StowExperimentalOffhandWeapon();
            return player.TrySelectGameplayPrimaryItem("weapon.rifle");
        }

        if (string.Equals(
                player.SelectedGameplayPrimaryItemId,
                "weapon.rifle",
                StringComparison.Ordinal))
        {
            return player.TrySelectGameplayPrimaryItem("weapon.bow");
        }

        player.EquipExperimentalOffhandWeapon();
        return player.IsExperimentalOffhandSelected;
    }

    internal bool TryHandleExperimentalSoldierStingerDetonation(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Soldier
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableSoldierStingerRockets
            || !_host.ExperimentalRules.IsExperimentalPracticePowerOwner(player))
        {
            return false;
        }

        var detonatedAnyRocket = false;
        for (var rocketIndex = 0; rocketIndex < _host.Rockets.Count; rocketIndex += 1)
        {
            var rocket = _host.Rockets[rocketIndex];
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

    internal bool TryHandleExperimentalSoldierCivilDefenseTurret(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Soldier
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableSoldierCivilDefenseTurret
            || !_host.ExperimentalRules.IsExperimentalPracticePowerOwner(player))
        {
            return false;
        }

        if (_host.ClientPredictionMode) return false;
        if (!_host.Structures.CanDeployCivilDefenseTurret(player))
        {
            _host.WorldEffects.RegisterWorldSoundEvent("FailureSnd", player.X, player.Y, player.Id);
            return false;
        }

        if (!player.TryDeployExperimentalSoldierCivilDefenseTurret(
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierCivilDefenseTurretDeployCooldownTicks))
        {
            return false;
        }

        return _host.Structures.TryDeployCivilDefenseTurret(player);
    }

    internal bool TryHandleExperimentalSoldierThundergunner(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (player.ClassId != PlayerClass.Soldier
            || !_host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableSoldierThundergunner
            || !_host.ExperimentalRules.IsExperimentalPracticePowerOwner(player)
            || !player.TryFireExperimentalSoldierThundergunner(
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultSoldierThundergunnerCooldownTicks))
        {
            return false;
        }

        _host.AirblastRules.TriggerExperimentalSoldierThundergunner(player, input.AimWorldX, input.AimWorldY);
        return true;
    }

    private bool TryHandleNetworkAbilityInput(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        GameplayAbilityInputPhase phase)
    {
        if (player.IsTaunting && !CanUseUtilityAbilityWhileTaunting(player, phase))
        {
            return false;
        }

        if (!_host.ExperimentalGameplaySettings.EnableSecondaryAbilities)
        {
            return false;
        }

        var dispatchResult = _host.Abilities.TryDispatchGameplayAbility(
            player,
            input,
            previousInput,
            phase,
            GameplayAbilityConstants.UtilityCategory,
            player.X,
            player.Y);
        if (dispatchResult.ConsumedInput)
        {
            return true;
        }

        return false;
    }

    private static bool CanUseUtilityAbilityWhileTaunting(PlayerEntity player, GameplayAbilityInputPhase phase)
    {
        _ = phase;
        return player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.DemomanUtility);
    }

    private void TryHandleNetworkWeaponInteraction(PlayerEntity player)
    {
        if (_host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableSecondaryAbilities
            && TryHandleExperimentalEngineerAlternateWeaponInteraction(player))
        {
            return;
        }

        _host.Pickups.TryHandleDroppedWeaponInteraction(player);
    }

    internal bool TryHandleExperimentalEngineerDestinyPunctuatorBlast(PlayerEntity player, PlayerInputSnapshot input)
    {
        if (!_host.ExperimentalRules.HasExperimentalEngineerDestinyPunctuator(player)
            || player.ClassId != PlayerClass.Engineer)
        {
            return false;
        }

        if (player.IsExperimentalOffhandSelected)
        {
            _host.ExperimentalRules.ClearExperimentalEngineerAlternateWeaponState(player);
            player.StowExperimentalOffhandWeapon();
        }

        if (!player.TryFireExperimentalEngineerDestinyPunctuatorBlast())
        {
            return true;
        }

        _host.WeaponHandler.FireExperimentalEngineerDestinyPunctuatorBlast(
            player,
            input.AimWorldX,
            input.AimWorldY,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorPelletMultiplier);

        var aimRadians = DeterministicMath.Atan2(input.AimWorldY - player.Y, input.AimWorldX - player.X);
        var blastOriginX = player.X + DeterministicMath.Cos(aimRadians) * 16f;
        var blastOriginY = player.Y + DeterministicMath.Sin(aimRadians) * 12f;
        ExplosionGeometry.ApplyExplosionImpulse(
            player,
            blastOriginX,
            blastOriginY,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerDestinyPunctuatorSelfKnockbackPerTick);
        player.SetMovementState(LegacyMovementState.ExplosionRecovery);
        return true;
    }

    internal bool TryHandleExperimentalEngineerAlternateWeaponInteraction(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Engineer)
        {
            return false;
        }

        var freezeAvailable = _host.ExperimentalRules.HasExperimentalEngineerFreezeRayAvailable(player);
        if (!_host.ExperimentalRules.HasExperimentalEngineerAlternateWeaponAvailable(player))
        {
            return false;
        }

        if (!player.HasExperimentalOffhandWeapon)
        {
            player.SetExperimentalOffhandWeapon(CharacterClassCatalog.Medigun);
        }

        if (!player.IsExperimentalOffhandSelected)
        {
            player.SetExperimentalEngineerAlternateWeaponMode(
                _host.ExperimentalRules.GetExperimentalEngineerDefaultAlternateWeaponMode(player));
            player.EquipExperimentalOffhandWeapon();
            return true;
        }

        if (player.ExperimentalEngineerAlternateWeaponMode == ExperimentalEngineerAlternateWeaponMode.EssenceExtractor
            && freezeAvailable)
        {
            _host.ExperimentalRules.ClearExperimentalEngineerAlternateWeaponState(player);
            player.SetExperimentalEngineerAlternateWeaponMode(ExperimentalEngineerAlternateWeaponMode.FreezeRay);
            return true;
        }

        _host.ExperimentalRules.ClearExperimentalEngineerAlternateWeaponState(player);
        player.SetExperimentalEngineerAlternateWeaponMode(ExperimentalEngineerAlternateWeaponMode.None);
        player.StowExperimentalOffhandWeapon();
        return true;
    }

    internal bool TryStartSpyBackstab(PlayerEntity attacker, float aimWorldX, float aimWorldY)
    {
        if (attacker.ClassId != PlayerClass.Spy || !attacker.IsSpyCloaked)
        {
            return false;
        }

        var directionDegrees = SimulationMath.PointDirectionDegrees(attacker.X, attacker.Y, aimWorldX, aimWorldY);
        if (!attacker.TryStartSpyBackstab(directionDegrees))
        {
            return false;
        }

        _host.Projectiles.SpawnStabAnimation(attacker, directionDegrees);
        return true;
    }

    private static bool ShouldUseHeldSecondaryAbility(PlayerEntity player)
    {
        if (!GameplayAbilitySystem.TryResolveSecondaryGameplayAbilityItem(player, out var item)
            || item.Ability is not { } ability)
        {
            return false;
        }

        return string.Equals(ability.Activation, GameplayAbilityConstants.HeldActivation, StringComparison.Ordinal)
            && !(string.Equals(ability.ExecutorId, BuiltInGameplayBehaviorIds.DemomanDetonate, StringComparison.Ordinal)
                && player.IsExperimentalDemoknightEnabled);
    }

    private static bool ShouldUseHeldUtilityAbility(PlayerEntity player)
    {
        foreach (var item in GameplayAbilitySystem.ResolveGameplayAbilityItems(player, GameplayAbilityConstants.UtilityChannel))
        {
            if (item.Ability is { } ability
                && string.Equals(ability.Activation, GameplayAbilityConstants.HeldActivation, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal void TryActivatePendingSpyBackstab(PlayerEntity player)
    {
        if (!player.TryConsumeSpyBackstabHitboxTrigger(out var directionDegrees))
        {
            return;
        }

        _host.Projectiles.SpawnStabMask(player, directionDegrees);
    }
}
