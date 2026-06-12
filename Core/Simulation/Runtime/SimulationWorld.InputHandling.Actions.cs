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
            && ExperimentalGameplaySettings.EnableSoldierInfiniteAmmoDuringRage;
        if (!input.FirePrimary || !player.TryFirePrimaryWeapon(ignorePrimaryAmmoCost))
        {
            return;
        }

        FirePrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
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
            UpdateMedicHealing(player, input.AimWorldX, input.AimWorldY);
            return true;
        }

        if (!player.TryFireExperimentalOffhandWeapon())
        {
            return true;
        }

        if (player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.GrenadeLauncher))
        {
            WeaponHandler.FireGrenadeLauncher(player, input.AimWorldX, input.AimWorldY);
            return true;
        }

        var offhandBehaviorId = player.EquippedBehaviorId ?? player.SecondaryBehaviorId ?? player.UtilityBehaviorId;
        if (CharacterClassCatalog.RuntimeRegistry.TryGetPrimaryWeaponBinding(offhandBehaviorId, out var secondaryBinding)
            && secondaryBinding.Executor is not null)
        {
            WeaponHandler.FireExperimentalOffhandWeapon(player, offhandBehaviorId, input.AimWorldX, input.AimWorldY);
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
        if (TryHandleExperimentalEngineerBeamPrimaryFire(player, input))
        {
            return true;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Medigun))
        {
            if (input.FirePrimary)
            {
                UpdateMedicHealing(player, input.AimWorldX, input.AimWorldY);
            }
            else if (!input.FireSecondary || !player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit))
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
            var activeProjectileLimit = player.PrimaryWeapon.ActiveProjectileLimit ?? PlayerEntity.QuoteBubbleLimit;
            if (input.FirePrimary && player.TryFireQuoteBubble(activeProjectileLimit))
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

    private bool TryHandleExperimentalEngineerBeamPrimaryFire(PlayerEntity player, PlayerInputSnapshot input)
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

        return false;
    }

    private GameplayAbilityResult TryHandleNetworkSecondaryAbility(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        GameplayAbilityInputPhase phase,
        float sourceX,
        float sourceY)
    {
        var dispatchResult = TryDispatchGameplayAbility(
            player,
            input,
            previousInput,
            phase,
            GameplayAbilityConstants.SecondaryCategory,
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

    private void HandleEngineerPdaSentryCommand(PlayerEntity player)
    {
        var maxOwnedSentries = GetExperimentalMaxOwnedSentries(player);
        if (maxOwnedSentries > 1 && GetExperimentalOwnedSentryCount(player.Id) < maxOwnedSentries)
        {
            TryBuildSentry(player);
            return;
        }

        var destroyResult = TryDestroySentryForOwnerCommand(player);
        if (destroyResult == OwnedSentryDestroyResult.NoOwnedSentry)
        {
            TryBuildSentry(player);
        }
    }

    private static bool TryHandleSecondaryWeaponToggle(PlayerEntity player)
    {
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
        if (!ExperimentalGameplaySettings.EnableSecondaryAbilities
            || player.IsTaunting
            || player.IsExperimentalCryoFrozen)
        {
            return false;
        }

        return TryHandleSecondaryWeaponToggle(player);
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

    private bool TryHandleNetworkAbilityInput(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        GameplayAbilityInputPhase phase,
        bool swappedWeaponThisTick)
    {
        if (player.IsTaunting && !CanUseUtilityAbilityWhileTaunting(player, phase))
        {
            return false;
        }

        if (!ExperimentalGameplaySettings.EnableSecondaryAbilities)
        {
            return false;
        }

        if (swappedWeaponThisTick)
        {
            return true;
        }

        // Backward compatibility for clients that still send UseAbility for weapon swapping.
        if (!input.SwapWeapon && TryHandleNetworkWeaponSwap(player))
        {
            return true;
        }

        var dispatchResult = TryDispatchGameplayAbility(
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

        // Backward-compatible fallback for sessions where a secondary weapon is enabled
        // but utility loadout behaviors are unavailable.
        if (!input.SwapWeapon)
        {
            TryHandleLegacyNetworkSecondaryWeaponToggle(player, input);
        }

        return false;
    }

    private static bool CanUseUtilityAbilityWhileTaunting(PlayerEntity player, GameplayAbilityInputPhase phase)
    {
        _ = phase;
        return player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.DemomanUtility);
    }

    private static void TryHandleLegacyNetworkSecondaryWeaponToggle(PlayerEntity player, PlayerInputSnapshot input)
    {
        _ = input;
        TryHandleSecondaryWeaponToggle(player);
    }

    private void TryHandleNetworkWeaponInteraction(PlayerEntity player)
    {
        if (ExperimentalGameplaySettings.EnableSecondaryAbilities
            && TryHandleExperimentalEngineerAlternateWeaponInteraction(player))
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

        if (player.IsExperimentalOffhandSelected)
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

        if (!player.IsExperimentalOffhandSelected)
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
        if (!TryResolveSecondaryGameplayAbilityItem(player, out var item)
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
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        return runtimeRegistry.TryGetGameplayAbilityDefinition(player.GameplayLoadoutState.UtilityItemId, out _, out var ability)
            && string.Equals(ability.Activation, GameplayAbilityConstants.HeldActivation, StringComparison.Ordinal);
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
