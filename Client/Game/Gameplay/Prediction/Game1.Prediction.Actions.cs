#nullable enable

using System;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float PredictedPyroSelfAirblastBaseImpulse = 15f * LegacyMovementModel.SourceTicksPerSecond;
    private const float PredictedPyroSelfAirblastBaseLift = -2f * LegacyMovementModel.SourceTicksPerSecond;
    private const float PredictedPyroSelfAirblastHorizontalStrengthScale = 1f / 3f;
    private const float PredictedPyroSelfAirblastVerticalStrengthScale = 1f / 3f;
    private const float PredictedPyroSelfAirblastImpulse = PredictedPyroSelfAirblastBaseImpulse * PredictedPyroSelfAirblastHorizontalStrengthScale;
    private const float PredictedPyroSelfAirblastLift = PredictedPyroSelfAirblastBaseLift * PredictedPyroSelfAirblastVerticalStrengthScale;


    private bool ApplyPredictedSniperBowPrimaryFire(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (!player.IsSniperBowEquipped)
        {
            _localPredictionState.PredictedLocalActionState.SniperBowChargeTicks = 0;
            return false;
        }

        var input = predictedInput.Input;
        if (input.FirePrimary)
        {
            var directionDegrees = player.AimDirectionDegrees;
            if (player.SniperBowChargeTicks == 0)
            {
                _ = player.TryStartSniperBowCharge(directionDegrees);
            }
            else
            {
                player.IncrementSniperBowCharge(directionDegrees);
            }
        }
        else if (player.SniperBowChargeTicks > 0)
        {
            // Match server release: clear local charge; the authoritative arrow comes from the snapshot.
            player.CancelSniperBowCharge();
        }

        _localPredictionState.PredictedLocalActionState.SniperBowChargeTicks = player.SniperBowChargeTicks;
        return true;
    }

    private bool ApplyPredictedMortarLauncherPrimaryFire(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (!player.IsMortarLauncherEquipped)
        {
            return false;
        }

        var input = predictedInput.Input;
        if (input.FirePrimary)
        {
            var directionDegrees = player.AimDirectionDegrees;
            if (player.MortarLauncherChargeTicks == 0)
            {
                _ = player.TryStartMortarLauncherCharge(directionDegrees);
            }
            else
            {
                player.IncrementMortarLauncherCharge(directionDegrees);
            }
        }
        else if (player.MortarLauncherChargeTicks > 0)
        {
            // Consume the predicted round on release so the loaded rocket and
            // reload animation change immediately. Projectile authority stays
            // with the server and reconciles through its snapshot.
            if (player.TryReleaseMortarLauncherCharge(out _, out _))
            {
                _ = player.TryFirePrimaryWeapon();
            }
        }

        _localPredictionState.PredictedLocalActionState.SniperBowChargeTicks = player.SniperBowChargeTicks;
        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private void ApplyPredictedPrimaryFire(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (player.IsTaunting || player.IsBuffBannerDeploying)
        {
            return;
        }

        if (player.ClassId == PlayerClass.Heavy && player.IsExperimentalGhostDashing)
        {
            return;
        }

        if (player.IsExperimentalDemoknightEnabled)
        {
            if (predictedInput.Input.FirePrimary
                && player.TryFireExperimentalDemoknightSword())
            {
                SyncPredictedLocalPlayerState(player);
            }

            return;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.WhippingCord))
        {
            if (predictedInput.Input.FirePrimary
                && player.TryFireWhippingCord())
            {
                var recoilTicks = WhippingCordCatalog.ResolveRecoilTicks(player);
                player.BeginWhippingCordWindup(
                    WhippingCordCatalog.ResolveWindupTicks(recoilTicks),
                    WhippingCordCatalog.ResolveSwingTicks(recoilTicks),
                    WhippingCordCatalog.ResolveBackswingTicks(recoilTicks));
            }

            if (player.TryEnterWhippingCordDamageWindow())
            {
                if (predictedInput.Input.FirePrimary)
                {
                    _ = _world.TryLatchWhippingCordToTerrain(
                        player, predictedInput.Input.AimWorldX, predictedInput.Input.AimWorldY);
                }

                player.AdvanceWhippingCordSwingTimer();
            }
            else if (player.IsWhippingCordBackswingActive)
            {
                player.AdvanceWhippingCordBackswingTimer();
            }

            SyncPredictedLocalPlayerState(player);

            return;
        }

        if (player.IsAcquiredWeaponEquipped)
        {
            // Acquired weapons use their own cooldown/ammo state. Predict the
            // state transition here as well; projectile creation remains on
            // the authoritative simulation path.
            if (!predictedInput.Input.FirePrimary
                || player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.Medigun))
            {
                return;
            }

            if (player.TryFireAcquiredWeapon())
            {
                SyncPredictedLocalPlayerState(player);
            }

            return;
        }

        if (player.PrimaryWeapon.Kind == PrimaryWeaponKind.Medigun)
        {
            if (!predictedInput.Input.FirePrimary)
            {
                player.ClearMedicHealingTarget();
                SyncPredictedLocalPlayerState(player);
            }

            return;
        }

        if (ApplyPredictedSniperBowPrimaryFire(player, predictedInput))
        {
            return;
        }

        if (ApplyPredictedMortarLauncherPrimaryFire(player, predictedInput))
        {
            return;
        }

        if (!predictedInput.Input.FirePrimary)
        {
            return;
        }

        if (TryPredictedFireExperimentalOffhandPrimaryWeapon(player, predictedInput.Input.FirePrimary))
        {
            return;
        }

        if (player.ClassId == PlayerClass.Spy
            && player.IsSpyCloaked
            && predictedInput.Input.FirePrimary)
        {
            var isLastToDieProfessionalFireChord = predictedInput.Input.FireSecondary
                && player.CanFireLastToDieProfessionalRevolverWhileCloaked;
            if (!isLastToDieProfessionalFireChord && IsPredictedSpyBackstabReady())
            {
                _ = TryPredictedStartSpyBackstab(player);
                return;
            }

            if (!isLastToDieProfessionalFireChord)
            {
                return;
            }
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Blade))
        {
            if (player.TryFireQuoteBubble())
            {
                SyncPredictedLocalPlayerState(player);
            }

            return;
        }

        if (TryPredictedFirePrimaryWeapon(player)
            && predictedInput.Input.FireSecondary
            && player.IsSpyCloaked
            && player.LastToDieProfessionalEnabled)
        {
            _ = player.MarkLastToDieProfessionalFireChordConsumed();
            SyncPredictedLocalPlayerState(player);
        }
    }

    private bool TryPredictedFireExperimentalOffhandPrimaryWeapon(PlayerEntity player, bool firePrimary)
    {
        if (!firePrimary
            || !player.IsExperimentalOffhandSelected)
        {
            return false;
        }

        if (player.IsSniperBowEquipped)
        {
            return true;
        }

        if (!player.TryFireExperimentalOffhandWeapon())
        {
            return true;
        }

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private void ApplyPredictedSecondaryFire(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (ApplyPredictedSecondaryAbility(player, predictedInput))
        {
            return;
        }

        var swappedWeaponThisTick = ApplyPredictedSecondaryWeaponToggle(player, predictedInput)
            || ApplyPredictedWeaponSwap(player, predictedInput);
        ApplyPredictedSecondaryWeaponFire(player, predictedInput, swappedWeaponThisTick);
    }

    private void ApplyPredictedUtilityAbility(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (player.IsTaunting
            || predictedInput.Input.FireSecondary
            || !_world.ExperimentalGameplaySettings.EnableSecondaryAbilities)
        {
            return;
        }

        if (TryPredictedFireMedicHealDart(player, predictedInput))
        {
            return;
        }

        if (TryPredictedStartBuffBanner(player, predictedInput))
        {
            return;
        }

        if (!player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SpyUtility))
        {
            return;
        }

        var ability = GetPredictedSpySuperjumpAbility(player);
        var maxChargeTicks = ability is null
            ? PlayerEntity.SpySuperjumpMaxChargeTicks
            : GameplayAbilityParameterReader.GetInt(
                ability,
                "maxChargeTicks",
                PlayerEntity.SpySuperjumpMaxChargeTicks,
                minValue: 1);
        maxChargeTicks = player.ResolveSpySuperjumpMaxChargeTicks(maxChargeTicks);
        var cooldownTicks = ability is null
            ? PlayerEntity.SpySuperjumpCooldownTicks
            : GameplayAbilityParameterReader.GetTicks(
                ability,
                "cooldownTicks",
                "cooldownSeconds",
                PlayerEntity.SpySuperjumpCooldownTicks,
                _config.TicksPerSecond);
        var minVelocity = ability is null
            ? PlayerEntity.SpySuperjumpMinVelocity
            : GameplayAbilityParameterReader.GetFloat(
                ability,
                "minVelocity",
                PlayerEntity.SpySuperjumpMinVelocity,
                minValue: 0f);
        var maxVelocity = ability is null
            ? PlayerEntity.SpySuperjumpMaxVelocity
            : GameplayAbilityParameterReader.GetFloat(
                ability,
                "maxVelocity",
                PlayerEntity.SpySuperjumpMaxVelocity,
                minValue: minVelocity);

        var directionDegrees = GetPredictedAimDirectionDegrees(player, predictedInput.Input);
        if (predictedInput.AbilityReleased)
        {
            if (player.TryReleaseSpySuperjump(
                out var velocityX,
                out var velocityY,
                maxChargeTicks,
                minVelocity,
                maxVelocity,
                cooldownTicks))
            {
                player.ApplyVelocityImpulse(velocityX, velocityY);
            }

            SyncPredictedLocalPlayerState(player);
            return;
        }

        if (!predictedInput.AbilityPressed && !predictedInput.Input.UseAbility)
        {
            return;
        }

        if (player.SpySuperjumpChargeTicks == 0)
        {
            player.TryStartSpySuperjumpCharge(
                directionDegrees,
                predictedInput.Input.Left,
                predictedInput.Input.Right,
                predictedInput.Input.Up,
                predictedInput.Input.Down);
            SyncPredictedLocalPlayerState(player);
            return;
        }

        if (player.SpySuperjumpChargeTicks <= 0)
        {
            return;
        }

        var heldButtons = player.SpySuperjumpChargeStartMovementButtons;
        var leftWasHeld = (heldButtons & 0x01) != 0;
        var rightWasHeld = (heldButtons & 0x02) != 0;
        var upWasHeld = (heldButtons & 0x04) != 0;
        var downWasHeld = (heldButtons & 0x08) != 0;
        var newButtonPressed = (predictedInput.Input.Left && !leftWasHeld)
            || (predictedInput.Input.Right && !rightWasHeld)
            || (predictedInput.Input.Up && !upWasHeld)
            || (predictedInput.Input.Down && !downWasHeld);
        if (newButtonPressed || player.IsSpyBackstabAnimating || player.IsCarryingIntel)
        {
            player.CancelSpySuperjumpCharge();
        }
        else
        {
            player.IncrementSpySuperjumpCharge(directionDegrees, maxChargeTicks);
        }

        SyncPredictedLocalPlayerState(player);
    }

    private bool TryPredictedFireMedicHealDart(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (!predictedInput.AbilityPressed
            || !player.TryGetGameplayAbilityItem(
                GameplayAbilityConstants.UtilityChannel,
                BuiltInGameplayBehaviorIds.MedicKritzHealNeedles,
                out var abilityItem)
            || abilityItem.Ability is not { } ability)
        {
            return false;
        }

        var cooldownTicks = GameplayAbilityParameterReader.GetTicks(
            ability,
            "cooldownTicks",
            "cooldownSeconds",
            PlayerEntity.MedicHealDartDefaultCooldownTicks,
            _config.TicksPerSecond);
        _ = player.TryFireMedicHealDart(cooldownTicks);
        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private bool TryPredictedStartBuffBanner(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (!predictedInput.AbilityPressed
            || !player.TryGetGameplayAbilityItem(
                GameplayAbilityConstants.UtilityChannel,
                BuiltInGameplayBehaviorIds.SoldierBuffBanner,
                out var abilityItem)
            || abilityItem.Ability is not { } ability)
        {
            return false;
        }

        var maxChargeDamage = GameplayAbilityParameterReader.GetInt(
            ability,
            "maxChargeDamage",
            PlayerEntity.BuffBannerDefaultMaxChargeDamage,
            minValue: 1);
        var deployTicks = GameplayAbilityParameterReader.GetTicks(
            ability,
            "deployTicks",
            "deploySeconds",
            PlayerEntity.BuffBannerDefaultDeployTicks,
            _config.TicksPerSecond);
        var activeTicks = GameplayAbilityParameterReader.GetTicks(
            ability,
            "activeTicks",
            "activeSeconds",
            PlayerEntity.BuffBannerDefaultActiveTicks,
            _config.TicksPerSecond);
        var radius = GameplayAbilityParameterReader.GetFloat(
            ability,
            "radius",
            PlayerEntity.BuffBannerDefaultRadius,
            minValue: 1f);
        var damageMultiplier = GameplayAbilityParameterReader.GetFloat(
            ability,
            "damageMultiplier",
            PlayerEntity.BuffBannerDefaultDamageMultiplier,
            minValue: 1f);
        var healthRegenPerSecond = GameplayAbilityParameterReader.GetFloat(
            ability,
            "healthRegenPerSecond",
            PlayerEntity.BuffBannerDefaultHealthRegenPerSecond,
            minValue: 0f);

        _ = player.TryStartBuffBanner(
            maxChargeDamage,
            deployTicks,
            activeTicks,
            radius,
            damageMultiplier,
            healthRegenPerSecond);
        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private static GameplayAbilityDefinition? GetPredictedSpySuperjumpAbility(PlayerEntity player)
    {
        return player.TryGetGameplayAbilityItem(
                GameplayAbilityConstants.UtilityChannel,
                BuiltInGameplayBehaviorIds.SpyUtility,
                out var abilityItem)
            ? abilityItem.Ability
            : null;
    }

    private static float GetPredictedAimDirectionDegrees(PlayerEntity player, PlayerInputSnapshot input)
    {
        var degrees = MathF.Atan2(input.AimWorldY - player.Y, input.AimWorldX - player.X) * (180f / MathF.PI);
        degrees %= 360f;
        return degrees < 0f ? degrees + 360f : degrees;
    }

    private bool ApplyPredictedWeaponSwap(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (player.IsTaunting || !predictedInput.SwapWeaponPressed)
        {
            return false;
        }

        if (player.HasAlternatePrimaryWeapons)
        {
            if (_world.IsNearPrimaryWeaponSwapStation(player))
            {
                if (!player.TryCycleGameplayPrimaryItem())
                {
                    return false;
                }

                SyncPredictedLocalPlayerState(player);
                return true;
            }
        }

        return TryPredictedToggleSecondaryWeapon(player);
    }

    private bool ApplyPredictedSecondaryWeaponToggle(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (player.IsTaunting
            || player.IsExperimentalCryoFrozen
            || !predictedInput.ToggleSecondaryWeaponPressed)
        {
            return false;
        }

        return TryPredictedToggleSecondaryWeapon(player);
    }

    private bool ApplyPredictedSecondaryAbility(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (player.IsTaunting)
        {
            return false;
        }

        if (predictedInput.SecondaryAbilityReleased
            && player.TryReleaseLastToDieProfessionalFireChord(out var shouldDecloakFromProfessionalChord))
        {
            if (shouldDecloakFromProfessionalChord)
            {
                player.ForceDecloak();
                SyncPredictedLocalPlayerState(player);
            }
            else
            {
                SyncPredictedLocalPlayerState(player);
            }

            return true;
        }

        var specialAbilityBehaviorId = player.SpecialAbilityBehaviorId;
        var hasMedicNeedlegun = string.Equals(
            specialAbilityBehaviorId,
            BuiltInGameplayBehaviorIds.MedicNeedlegun,
            StringComparison.Ordinal);
        var hasMedicKritzBeam = string.Equals(
            specialAbilityBehaviorId,
            BuiltInGameplayBehaviorIds.MedicKritzBeam,
            StringComparison.Ordinal);
        var hasMedicUber = string.Equals(
            specialAbilityBehaviorId,
            BuiltInGameplayBehaviorIds.MedicUber,
            StringComparison.Ordinal);
        if (player.ClassId == PlayerClass.Medic
            && (hasMedicNeedlegun || hasMedicKritzBeam || hasMedicUber))
        {
            if (!predictedInput.Input.FireSecondary)
            {
                return false;
            }

            if (hasMedicUber)
            {
                if (predictedInput.Input.FirePrimary && _localPredictionState.PredictedLocalActionState.IsMedicUberReady)
                {
                    TryPredictedStartMedicUber(player);
                }

                return true;
            }

            if (hasMedicKritzBeam)
            {
                return true;
            }

            if (hasMedicNeedlegun && TryPredictedFireMedicNeedle(player))
            {
                return true;
            }

            if (_localPredictionState.PredictedLocalActionState.IsMedicUberReady && predictedInput.Input.FirePrimary)
            {
                TryPredictedStartMedicUber(player);
            }

            return true;
        }

        var useHeldSecondary = player.ClassId is PlayerClass.Demoman or PlayerClass.Quote;
        if ((!useHeldSecondary && !predictedInput.SecondaryAbilityPressed)
            || (useHeldSecondary && !predictedInput.Input.FireSecondary))
        {
            return false;
        }

        if (player.ClassId == PlayerClass.Demoman)
        {
            return true;
        }

        if (player.ClassId == PlayerClass.Heavy
            && string.Equals(
                specialAbilityBehaviorId,
                BuiltInGameplayBehaviorIds.HeavySandvich,
                StringComparison.Ordinal))
        {
            TryPredictedStartHeavySelfHeal(player);
            return true;
        }

        if (player.ClassId == PlayerClass.Pyro
            && string.Equals(
                specialAbilityBehaviorId,
                BuiltInGameplayBehaviorIds.PyroAirblast,
                StringComparison.Ordinal))
        {
            if (player.TryFirePyroAirblast())
            {
                PresentPredictedAirBlastVisual(player, predictedInput.Sequence);
                SyncPredictedLocalPlayerState(player);
            }

            return true;
        }

        if (string.Equals(
                specialAbilityBehaviorId,
                BuiltInGameplayBehaviorIds.SniperScope,
                StringComparison.Ordinal)
            && player.HasScopedSniperWeaponEquipped
            && !player.IsSniperBowEquipped)
        {
            TryPredictedToggleSniperScope(player);
            return true;
        }

        if (player.ClassId == PlayerClass.Spy)
        {
            if (player.TryBeginLastToDieProfessionalFireChord())
            {
                SyncPredictedLocalPlayerState(player);
                return true;
            }

            if (!predictedInput.Input.FirePrimary)
            {
                TryPredictedToggleSpyCloak(player);
            }

            return true;
        }

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella))
        {
            var ability = player.TryGetGameplayAbilityItem(GameplayAbilityConstants.SpecialChannel,
                BuiltInGameplayBehaviorIds.CivvieUmbrella, out var umbrellaItem) ? umbrellaItem.Ability : null;
            var maxCharge = ability is null ? PlayerEntity.CivvieUmbrellaMaxChargeTicks
                : GameplayAbilityParameterReader.GetInt(ability, "maxChargeTicks", PlayerEntity.CivvieUmbrellaMaxChargeTicks, minValue: 1);
            var chargeCost = ability is null ? PlayerEntity.CivvieUmbrellaOpeningChargeCost
                : GameplayAbilityParameterReader.GetInt(ability, "openingChargeCost", PlayerEntity.CivvieUmbrellaOpeningChargeCost, minValue: 0);
            if (player.TryActivateCivvieUmbrella(maxCharge))
            {
                player.TrySpendCivvieUmbrellaOpeningCharge(chargeCost);
                SyncPredictedLocalPlayerState(player);
            }

            return true;
        }

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.QuoteBladeThrow) && player.TryFireQuoteBlade())
        {
            SyncPredictedLocalPlayerState(player);
            return true;
        }

        return player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.QuoteBladeThrow);
    }

    private static bool IsPredictedPyroSelfAirblastInput(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        return player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.PyroUtility)
            && predictedInput.AbilityPressed;
    }

    private bool TryPredictedToggleSecondaryWeapon(PlayerEntity player)
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

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private bool TryPredictedPyroSelfAirblast(PlayerEntity player, uint inputSequence)
    {
        if (!player.TryFirePyroAirblast(
                PlayerEntity.PyroAirburstCost,
                PlayerEntity.PyroAirblastReloadTicks,
                PlayerEntity.PyroAirburstNoFlameTicks))
        {
            return false;
        }

        var aimRadians = player.AimDirectionDegrees * (MathF.PI / 180f);
        player.AddImpulse(
            -MathF.Cos(aimRadians) * PredictedPyroSelfAirblastImpulse,
            -MathF.Sin(aimRadians) * PredictedPyroSelfAirblastImpulse + PredictedPyroSelfAirblastLift);
        player.SetMovementState(LegacyMovementState.Airblast);
        PresentPredictedAirBlastVisual(player, inputSequence);
        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private void ApplyPredictedSecondaryWeaponFire(PlayerEntity player, PredictedLocalInput predictedInput, bool swappedWeaponThisTick)
    {
        if (player.IsTaunting || swappedWeaponThisTick)
        {
            if (player.StrongDrinkChargeTicks > 0)
            {
                player.CancelStrongDrinkCharge();
                SyncPredictedLocalPlayerState(player);
            }

            return;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SniperStrongDrink))
        {
            TryPredictedChargeSniperStrongDrink(player, predictedInput);
            return;
        }

        if (!predictedInput.AbilityPressed)
        {
            return;
        }

        if (IsPredictedPyroSelfAirblastInput(player, predictedInput))
        {
            TryPredictedPyroSelfAirblast(player, predictedInput.Sequence);
            return;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.CivviePogo))
        {
            if (predictedInput.AbilityPressed && player.TryToggleCivviePogo())
            {
                SyncPredictedLocalPlayerState(player);
            }

            return;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.HeavyUtility))
        {
            TryPredictedStartHeavyGhostDash(player);
            return;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SniperBinoculars))
        {
            TryPredictedToggleBinoculars(player);
        }
    }

    private bool TryPredictedChargeSniperStrongDrink(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (player.ClassId != PlayerClass.Sniper || !player.IsAlive)
        {
            if (player.StrongDrinkChargeTicks > 0)
            {
                player.CancelStrongDrinkCharge();
                SyncPredictedLocalPlayerState(player);
            }

            return false;
        }

        if (player.TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.SniperStrongDrinkCooldownTicksKey,
                out var remainingCooldown)
            && remainingCooldown > 0)
        {
            if (player.StrongDrinkChargeTicks > 0)
            {
                player.CancelStrongDrinkCharge();
                SyncPredictedLocalPlayerState(player);
            }

            return false;
        }

        var ability = GetPredictedSniperStrongDrinkAbility(player);
        var cooldownTicks = ability is null
            ? 450
            : GameplayAbilityParameterReader.GetTicks(
                ability,
                "cooldownTicks",
                "cooldownSeconds",
                450,
                _config.TicksPerSecond);
        var directionDegrees = GetPredictedAimDirectionDegrees(player, predictedInput.Input);

        if (predictedInput.AbilityReleased)
        {
            if (player.TryReleaseStrongDrinkCharge(out _, out _))
            {
                // Match server release: clear local aim preview and arm cooldown; the
                // authoritative bottle comes from the snapshot.
                player.SetGameplayAbilityCooldownReplicatedState(
                    GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                    GameplayAbilityReplicatedState.SniperStrongDrinkCooldownTicksKey,
                    cooldownTicks);
            }

            SyncPredictedLocalPlayerState(player);
            return true;
        }

        if (!predictedInput.AbilityPressed && !predictedInput.Input.UseAbility)
        {
            return false;
        }

        if (player.IsHeavyEating)
        {
            player.CancelStrongDrinkCharge();
            SyncPredictedLocalPlayerState(player);
            return false;
        }

        if (player.StrongDrinkChargeTicks == 0)
        {
            player.TryStartStrongDrinkCharge(directionDegrees);
            SyncPredictedLocalPlayerState(player);
            return true;
        }

        player.IncrementStrongDrinkCharge(directionDegrees, maxChargeTicks: 1);
        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private static GameplayAbilityDefinition? GetPredictedSniperStrongDrinkAbility(PlayerEntity player)
    {
        return player.TryGetGameplayAbilityItem(
                GameplayAbilityConstants.UtilityChannel,
                BuiltInGameplayBehaviorIds.SniperStrongDrink,
                out var abilityItem)
            ? abilityItem.Ability
            : null;
    }

    private bool TryPredictedFirePrimaryWeapon(PlayerEntity player)
    {
        if (player.ClassId == PlayerClass.Demoman && CountPredictedLocalOwnedMines(player.Id) >= player.PrimaryWeapon.MaxAmmo)
        {
            return false;
        }

        if (!player.TryFirePrimaryWeapon())
        {
            return false;
        }

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private void ApplyPredictedTaunt(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (!predictedInput.TauntPressed)
        {
            return;
        }

        if (player.ClassId == PlayerClass.Quote && player.IsCivviePogoActive)
        {
            if (TryPredictedStartCivviePogoTrick(player))
            {
                SyncPredictedLocalPlayerState(player);
            }

            return;
        }

        if (player.TryStartTaunt())
        {
            if (player.ClassId == PlayerClass.Quote)
            {
                player.BeginPendingCivvieTauntHeal();
            }

            SyncPredictedLocalPlayerState(player);
        }
    }

    private bool TryPredictedStartCivviePogoTrick(PlayerEntity player)
    {
        var ability = GetPredictedCivvieTauntAbility(player);
        var trickFrameCount = ability is null
            ? PlayerEntity.CivviePogoTrickFrameCountDefault
            : GameplayAbilityParameterReader.GetInt(
                ability,
                "pogoTrickFrameCount",
                PlayerEntity.CivviePogoTrickFrameCountDefault,
                minValue: 1);
        var requestedDurationTicks = ability is null
            ? PlayerEntity.CivviePogoTrickDurationTicksDefault
            : GameplayAbilityParameterReader.GetInt(
                ability,
                "pogoTrickDurationTicks",
                PlayerEntity.CivviePogoTrickDurationTicksDefault,
                minValue: 1);
        var trickDurationTicks = PlayerEntity.ResolveCivviePogoTrickDurationTicks(
            requestedDurationTicks,
            _config.TicksPerSecond);
        return player.TryStartCivviePogoTrick(trickFrameCount, trickDurationTicks);
    }

    private static GameplayAbilityDefinition? GetPredictedCivvieTauntAbility(PlayerEntity player)
    {
        return player.TryGetGameplayAbilityItem(
                GameplayAbilityConstants.TauntChannel,
                BuiltInGameplayBehaviorIds.CivvieTaunt,
                out var abilityItem)
            ? abilityItem.Ability
            : null;
    }

    private int CountPredictedLocalOwnedMines(int ownerId)
    {
        var count = 0;
        foreach (var mine in _world.Mines)
        {
            if (mine.OwnerId == ownerId)
            {
                count += 1;
            }
        }

        return count;
    }

    private bool TryPredictedStartHeavySelfHeal(PlayerEntity player)
    {
        if (!player.TryStartHeavySelfHeal())
        {
            return false;
        }

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private bool TryPredictedStartHeavyGhostDash(PlayerEntity player)
    {
        var ability = GetPredictedHeavyGhostDashAbility(player);
        var useMomentum = GetPredictedHeavyGhostDashUseMomentum(player, ability);
        if (!player.TryStartExperimentalGhostDash(
            GetPredictedHeavyGhostDashDurationTicks(ability),
            GetPredictedHeavyGhostDashCooldownTicks(ability),
            GetPredictedHeavyGhostDashNextAttackDamageMultiplier(ability),
            useMomentum ? GetPredictedHeavyGhostDashImpulse(ability) : 0f,
            requireExperimentalDemoknight: false,
            useMomentum: useMomentum,
            movementTicks: useMomentum ? GetPredictedHeavyGhostDashMovementDurationTicks(ability) : 0,
            slideVelocityPerTick: GetPredictedHeavyGhostDashSlideVelocityPerTick(ability),
            burstSpeedMultiplier: GetPredictedHeavyGhostDashBurstSpeedMultiplier(ability),
            disableGravity: GetPredictedHeavyGhostDashDisableGravity(ability),
            enableGhostTrail: GetPredictedHeavyGhostDashEnableGhostTrail(ability)))
        {
            return false;
        }

        if (!useMomentum && player.ExperimentalGhostDashBurstSpeedMultiplier > 0f)
        {
            var burstSpeed = LegacyMovementModel.GetMaxRunSpeed(player.RunPower) * player.ExperimentalGhostDashBurstSpeedMultiplier;
            player.ApplyVelocityImpulse(
                player.FacingDirectionX >= 0f ? burstSpeed : -burstSpeed,
                velocityY: 0f);
        }

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private static GameplayAbilityDefinition? GetPredictedHeavyGhostDashAbility(PlayerEntity player)
    {
        return player.TryGetGameplayAbilityItem(
                GameplayAbilityConstants.UtilityChannel,
                BuiltInGameplayBehaviorIds.HeavyGhostDash,
                out var abilityItem)
            ? abilityItem.Ability
            : null;
    }

    private int GetPredictedHeavyGhostDashDurationTicks(GameplayAbilityDefinition? ability)
    {
        return ability is null
            ? Math.Max(1, (int)MathF.Round(_config.TicksPerSecond * ExperimentalGameplaySettings.HeavyGhostDashDurationSeconds))
            : GameplayAbilityParameterReader.GetTicks(
                ability,
                "durationTicks",
                "durationSeconds",
                Math.Max(1, (int)MathF.Round(_config.TicksPerSecond * ExperimentalGameplaySettings.HeavyGhostDashDurationSeconds)),
                _config.TicksPerSecond);
    }

    private int GetPredictedHeavyGhostDashMovementDurationTicks(GameplayAbilityDefinition? ability)
    {
        return ability is null
            ? Math.Max(1, (int)MathF.Round(_config.TicksPerSecond * ExperimentalGameplaySettings.HeavyGhostDashMovementDurationSeconds))
            : GameplayAbilityParameterReader.GetTicks(
                ability,
                "movementDurationTicks",
                "movementDurationSeconds",
                Math.Max(1, (int)MathF.Round(_config.TicksPerSecond * ExperimentalGameplaySettings.HeavyGhostDashMovementDurationSeconds)),
                _config.TicksPerSecond);
    }

    private int GetPredictedHeavyGhostDashCooldownTicks(GameplayAbilityDefinition? ability)
    {
        return ability is null
            ? Math.Max(1, (int)MathF.Round(_config.TicksPerSecond * ExperimentalGameplaySettings.HeavyGhostDashCooldownSeconds))
            : GameplayAbilityParameterReader.GetTicks(
                ability,
                "cooldownTicks",
                "cooldownSeconds",
                Math.Max(1, (int)MathF.Round(_config.TicksPerSecond * ExperimentalGameplaySettings.HeavyGhostDashCooldownSeconds)),
                _config.TicksPerSecond);
    }

    private static float GetPredictedHeavyGhostDashImpulse(GameplayAbilityDefinition? ability)
    {
        return ability is null
            ? 75f * ExperimentalGameplaySettings.HeavyGhostDashImpulseScale
            : GameplayAbilityParameterReader.GetFloat(
                ability,
                "impulse",
                75f * ExperimentalGameplaySettings.HeavyGhostDashImpulseScale,
                minValue: 0f);
    }

    private static float GetPredictedHeavyGhostDashNextAttackDamageMultiplier(GameplayAbilityDefinition? ability)
    {
        return ability is null
            ? ExperimentalGameplaySettings.DefaultGhostDashNextAttackDamageMultiplier
            : GameplayAbilityParameterReader.GetFloat(
                ability,
                "nextAttackDamageMultiplier",
                ExperimentalGameplaySettings.DefaultGhostDashNextAttackDamageMultiplier,
                minValue: 1.0001f);
    }

    private static bool GetPredictedHeavyGhostDashUseMomentum(GameplayAbilityDefinition? ability)
    {
        if (ability is null)
        {
            return false;
        }

        var hasBurstParameters = HasPredictedHeavyGhostDashBurstParameters(ability);
        return GameplayAbilityParameterReader.GetBool(ability, "useMomentum", defaultValue: !hasBurstParameters);
    }

    private static bool GetPredictedHeavyGhostDashUseMomentum(PlayerEntity player, GameplayAbilityDefinition? ability)
    {
        if (IsPredictedStockHeavyGhostDashUtility(player, ability))
        {
            return false;
        }

        return GetPredictedHeavyGhostDashUseMomentum(ability);
    }

    private static bool IsPredictedStockHeavyGhostDashUtility(PlayerEntity player, GameplayAbilityDefinition? ability)
    {
        return player.TryGetGameplayAbilityItem(
                GameplayAbilityConstants.UtilityChannel,
                BuiltInGameplayBehaviorIds.HeavyGhostDash,
                out var abilityItem)
            && string.Equals(abilityItem.Id, StockGameplayModCatalog.HeavyUtilityItemId, StringComparison.Ordinal)
            && (ability is null || string.Equals(ability.ExecutorId, BuiltInGameplayBehaviorIds.HeavyGhostDash, StringComparison.Ordinal));
    }

    private static float GetPredictedHeavyGhostDashSlideVelocityPerTick(GameplayAbilityDefinition? ability)
    {
        return ability is null
            ? ExperimentalGameplaySettings.HeavyGhostDashSlideVelocityPerTick
            : GameplayAbilityParameterReader.GetFloat(
                ability,
                "slideVelocityPerTick",
                ExperimentalGameplaySettings.HeavyGhostDashSlideVelocityPerTick,
                minValue: 0f);
    }

    private static float GetPredictedHeavyGhostDashBurstSpeedMultiplier(GameplayAbilityDefinition? ability)
    {
        return ability is null
            ? ExperimentalGameplaySettings.HeavyGhostDashBurstSpeedMultiplier
            : GameplayAbilityParameterReader.GetFloat(
                ability,
                "burstSpeedMultiplier",
                ExperimentalGameplaySettings.HeavyGhostDashBurstSpeedMultiplier,
                minValue: 0f);
    }

    private static bool GetPredictedHeavyGhostDashDisableGravity(GameplayAbilityDefinition? ability)
    {
        return ability is null
            ? ExperimentalGameplaySettings.HeavyGhostDashDisableGravityDefault
            : GameplayAbilityParameterReader.GetBool(
                ability,
                "disableGravity",
                ExperimentalGameplaySettings.HeavyGhostDashDisableGravityDefault);
    }

    private static bool GetPredictedHeavyGhostDashEnableGhostTrail(GameplayAbilityDefinition? ability)
    {
        return ability is null
            ? ExperimentalGameplaySettings.HeavyGhostDashEnableGhostTrailDefault
            : GameplayAbilityParameterReader.GetBool(
                ability,
                "enableGhostTrail",
                ExperimentalGameplaySettings.HeavyGhostDashEnableGhostTrailDefault);
    }

    private static bool HasPredictedHeavyGhostDashBurstParameters(GameplayAbilityDefinition? ability)
    {
        return ability is not null
            && (ability.Parameters.ContainsKey("burstSpeedMultiplier")
                || ability.Parameters.ContainsKey("disableGravity")
                || ability.Parameters.ContainsKey("enableGhostTrail"));
    }

    private bool TryPredictedToggleSniperScope(PlayerEntity player)
    {
        if (!player.TryToggleSniperScope())
        {
            return false;
        }

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private bool TryPredictedToggleSpyCloak(PlayerEntity player)
    {
        if (!player.TryToggleSpyCloak())
        {
            return false;
        }

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private bool TryPredictedToggleBinoculars(PlayerEntity player)
    {
        if (!player.TryToggleBinoculars())
        {
            return false;
        }

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private bool TryPredictedStartSpyBackstab(PlayerEntity player)
    {
        if (!player.TryStartSpyBackstab(player.AimDirectionDegrees))
        {
            return false;
        }

        var backstabOwnerId = ReferenceEquals(player, _world.LocalPlayer)
            ? GetResolvedLocalPlayerId()
            : player.Id;
        SpawnBackstabVisual(backstabOwnerId, player.Team, player.X, player.Y, player.AimDirectionDegrees);
        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private bool TryPredictedFireMedicNeedle(PlayerEntity player)
    {
        if (!player.TryFireMedicNeedle())
        {
            return false;
        }

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private bool TryPredictedStartMedicUber(PlayerEntity player)
    {
        if (!player.TryStartMedicUber())
        {
            return false;
        }

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private bool IsPredictedSpyBackstabAnimating()
    {
        return _localPredictionState.PredictedLocalActionState.SpyBackstabVisualTicksRemaining > 0;
    }

    private bool IsPredictedSpyBackstabReady()
    {
        return _localPredictionState.PredictedLocalActionState.SpyBackstabWindupTicksRemaining <= 0
            && _localPredictionState.PredictedLocalActionState.SpyBackstabRecoveryTicksRemaining <= 0;
    }
}
