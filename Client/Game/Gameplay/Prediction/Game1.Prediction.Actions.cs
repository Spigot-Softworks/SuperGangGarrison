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

    private void AdvancePredictedActionState(PlayerEntity player)
    {
        AdvancePredictedWeaponState(player);
        AdvancePredictedEngineerState(player);
        AdvancePredictedHeavyState(player);
        AdvancePredictedSniperState(player);
        AdvancePredictedMedicState(player);
        AdvancePredictedSpyState(player);
    }

    private void AdvancePredictedEngineerState(PlayerEntity player)
    {
        var maxMetal = player.MaxMetal;
        _predictedLocalActionState.Metal = float.Min(_predictedLocalActionState.Metal, maxMetal);
        if (player.ClassId != PlayerClass.Engineer || _predictedLocalActionState.Metal >= maxMetal)
        {
            return;
        }

        _predictedLocalActionState.Metal = float.Min(maxMetal, _predictedLocalActionState.Metal + player.PassiveMetalRegenerationPerTick);
    }

    private void AdvancePredictedWeaponState(PlayerEntity player)
    {
        var weapon = player.PrimaryWeapon;
        if (weapon.AmmoRegenPerTick > 0 && _predictedLocalActionState.CurrentShells < weapon.MaxAmmo)
        {
            _predictedLocalActionState.CurrentShells = int.Min(weapon.MaxAmmo, _predictedLocalActionState.CurrentShells + weapon.AmmoRegenPerTick);
        }

        if (_predictedLocalActionState.PrimaryCooldownTicks > 0)
        {
            _predictedLocalActionState.PrimaryCooldownTicks -= 1;
            return;
        }

        if (!weapon.AutoReloads)
        {
            _predictedLocalActionState.ReloadTicksUntilNextShell = 0;
            return;
        }

        if (_predictedLocalActionState.CurrentShells >= weapon.MaxAmmo)
        {
            _predictedLocalActionState.ReloadTicksUntilNextShell = 0;
            return;
        }

        if (_predictedLocalActionState.ReloadTicksUntilNextShell > 0)
        {
            _predictedLocalActionState.ReloadTicksUntilNextShell -= 1;
            return;
        }

        if (weapon.RefillsAllAtOnce)
        {
            _predictedLocalActionState.CurrentShells = weapon.MaxAmmo;
            _predictedLocalActionState.ReloadTicksUntilNextShell = 0;
            return;
        }

        _predictedLocalActionState.CurrentShells += 1;
        if (_predictedLocalActionState.CurrentShells < weapon.MaxAmmo)
        {
            _predictedLocalActionState.ReloadTicksUntilNextShell = weapon.AmmoReloadTicks;
        }
    }

    private void AdvancePredictedHeavyState(PlayerEntity player)
    {
        if (_predictedLocalActionState.HeavyEatCooldownTicksRemaining > 0
            && (!_predictedLocalActionState.IsHeavyEating || player.Health >= player.MaxHealth))
        {
            _predictedLocalActionState.HeavyEatCooldownTicksRemaining -= 1;
            if (_predictedLocalActionState.HeavyEatCooldownTicksRemaining <= 0)
            {
                _predictedLocalActionState.HeavyEatCooldownDurationTicks = PlayerEntity.HeavySandvichCooldownTicks;
            }
        }

        if (!_predictedLocalActionState.IsHeavyEating)
        {
            return;
        }

        if (player.Health < player.MaxHealth)
        {
            _predictedLocalActionState.HeavyEatCooldownTicksRemaining =
                Math.Max(1, _predictedLocalActionState.HeavyEatCooldownDurationTicks);
        }

        _predictedLocalActionState.HeavyEatTicksRemaining = Math.Max(0, _predictedLocalActionState.HeavyEatTicksRemaining - 1);
        if (_predictedLocalActionState.HeavyEatTicksRemaining > 0)
        {
            return;
        }

        _predictedLocalActionState.IsHeavyEating = false;
        _predictedLocalActionState.HeavyEatTicksRemaining = 0;
    }

    private void AdvancePredictedSniperState(PlayerEntity player)
    {
        if (!player.HasScopedSniperWeaponEquipped || !_predictedLocalActionState.IsSniperScoped || _predictedLocalActionState.PrimaryCooldownTicks > 0)
        {
            _predictedLocalActionState.SniperChargeTicks = 0;
            return;
        }

        if (_predictedLocalActionState.SniperChargeTicks < PlayerEntity.SniperChargeMaxTicks)
        {
            _predictedLocalActionState.SniperChargeTicks += 1;
        }
    }

    private void AdvancePredictedMedicState(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Medic)
        {
            return;
        }

        if (_predictedLocalActionState.IsMedicUbering)
        {
            if (player.MedicUberUsesFixedDuration && player.MedicUberCommittedCharge > 0f)
            {
                _predictedLocalActionState.MedicUberCharge = player.MedicUberCharge;
            }
            else
            {
                _predictedLocalActionState.MedicUberCharge = float.Max(0f, _predictedLocalActionState.MedicUberCharge - PlayerEntity.MedicUberChargeDrainPerSourceTick);
            }

            if (_predictedLocalActionState.MedicUberCharge <= 0f)
            {
                _predictedLocalActionState.MedicUberCharge = 0f;
                _predictedLocalActionState.IsMedicUbering = false;
            }
        }

        if (_predictedLocalActionState.MedicNeedleCooldownTicks > 0)
        {
            _predictedLocalActionState.MedicNeedleCooldownTicks -= 1;
        }

        var maxShells = player.MaxShells;
        if (_predictedLocalActionState.CurrentShells >= maxShells)
        {
            _predictedLocalActionState.MedicNeedleRefillTicks = 0;
            return;
        }

        if (_predictedLocalActionState.MedicNeedleRefillTicks <= 0)
        {
            _predictedLocalActionState.MedicNeedleRefillTicks = PlayerEntity.MedicNeedleRefillTicksDefault;
            return;
        }

        _predictedLocalActionState.MedicNeedleRefillTicks -= 1;
        if (_predictedLocalActionState.MedicNeedleRefillTicks <= 0)
        {
            _predictedLocalActionState.CurrentShells = maxShells;
            _predictedLocalActionState.MedicNeedleRefillTicks = 0;
        }
    }

    private void AdvancePredictedSpyState(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Spy)
        {
            _predictedLocalActionState.SpyCloakAlpha = 1f;
            _predictedLocalActionState.IsSpyVisibleToEnemies = false;
            _predictedLocalActionState.SpySuperjumpChargeTicks = 0;
            _predictedLocalActionState.IsSpySuperjumping = false;
            _predictedLocalActionState.SpySuperjumpHorizontalVelocity = 0f;
            _predictedLocalActionState.SpySuperjumpCooldownTicksRemaining = 0;
            _predictedLocalActionState.SpyBackstabWindupTicksRemaining = 0;
            _predictedLocalActionState.SpyBackstabRecoveryTicksRemaining = 0;
            _predictedLocalActionState.SpyBackstabVisualTicksRemaining = 0;
            return;
        }

        if (_predictedLocalActionState.SpySuperjumpCooldownTicksRemaining > 0)
        {
            _predictedLocalActionState.SpySuperjumpCooldownTicksRemaining -= 1;
        }
        if (_predictedLocalActionState.IsSpySuperjumping && player.IsGrounded)
        {
            _predictedLocalActionState.IsSpySuperjumping = false;
            _predictedLocalActionState.SpySuperjumpHorizontalVelocity = 0f;
        }

        _predictedLocalActionState.SpyCloakAlpha = _predictedLocalActionState.IsSpyCloaked
            ? float.Max(0f, _predictedLocalActionState.SpyCloakAlpha - PlayerEntity.SpyCloakFadePerTick)
            : float.Min(1f, _predictedLocalActionState.SpyCloakAlpha + PlayerEntity.SpyCloakFadePerTick);

        if (_predictedLocalActionState.SpyBackstabVisualTicksRemaining > 0)
        {
            _predictedLocalActionState.SpyBackstabVisualTicksRemaining -= 1;
        }

        if (_predictedLocalActionState.SpyBackstabWindupTicksRemaining > 0)
        {
            _predictedLocalActionState.SpyBackstabWindupTicksRemaining -= 1;
            if (_predictedLocalActionState.SpyBackstabWindupTicksRemaining == 0)
            {
                _predictedLocalActionState.SpyBackstabRecoveryTicksRemaining = PlayerEntity.SpyBackstabRecoveryTicksDefault;
            }
        }
        else if (_predictedLocalActionState.SpyBackstabRecoveryTicksRemaining > 0)
        {
            _predictedLocalActionState.SpyBackstabRecoveryTicksRemaining -= 1;
        }

        _predictedLocalActionState.IsSpyVisibleToEnemies = _predictedLocalActionState.IsSpyCloaked
            && (_predictedLocalActionState.SpyCloakAlpha > 0f
                || _predictedLocalActionState.SpyBackstabVisualTicksRemaining > 0);
    }

    private void ApplyPredictedPrimaryFire(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (player.IsTaunting)
        {
            return;
        }

        if (player.ClassId == PlayerClass.Heavy && player.IsExperimentalGhostDashing)
        {
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
            if (IsPredictedSpyBackstabReady())
            {
                _ = TryPredictedStartSpyBackstab(player);
            }

            return;
        }

        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Blade))
        {
            if (player.TryFireQuoteBubble())
            {
                TriggerLocalWeaponFireHudRumble(0.4f);
                SyncPredictedLocalPlayerState(player);
            }

            return;
        }

        TryPredictedFirePrimaryWeapon(player);
    }

    private bool TryPredictedFireExperimentalOffhandPrimaryWeapon(PlayerEntity player, bool firePrimary)
    {
        if (!firePrimary
            || !player.IsExperimentalOffhandSelected)
        {
            return false;
        }

        if (!player.TryFireExperimentalOffhandWeapon())
        {
            return true;
        }

        TriggerLocalWeaponFireHudRumble(GetPredictedWeaponFireHudRumbleIntensity(player));
        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private void ApplyPredictedSecondaryFire(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (ApplyPredictedSecondaryAbility(player, predictedInput))
        {
            return;
        }

        var swappedWeaponThisTick = ApplyPredictedWeaponSwap(player, predictedInput);
        ApplyPredictedSecondaryWeaponFire(player, predictedInput, swappedWeaponThisTick);
    }

    private bool ApplyPredictedWeaponSwap(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (player.IsTaunting || !predictedInput.SwapWeaponPressed)
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

        if (player.ClassId == PlayerClass.Medic)
        {
            if (!predictedInput.Input.FireSecondary)
            {
                return false;
            }

            if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.MedigunCrit))
            {
                if (predictedInput.Input.FirePrimary
                    && _predictedLocalActionState.IsMedicUberReady
                    && player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber))
                {
                    TryPredictedStartMedicUber(player);
                }
                else if (!predictedInput.Input.FirePrimary
                    && player.TryFireMedicKritzHealNeedle())
                {
                    TriggerLocalWeaponFireHudRumble(0.35f);
                    SyncPredictedLocalPlayerState(player);
                }

                return true;
            }

            if (TryPredictedFireMedicNeedle(player))
            {
                return true;
            }

            if (_predictedLocalActionState.IsMedicUberReady && predictedInput.Input.FirePrimary)
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

        if (player.ClassId == PlayerClass.Heavy)
        {
            TryPredictedStartHeavySelfHeal(player);
            return true;
        }

        if (player.ClassId == PlayerClass.Pyro)
        {
            if (player.TryFirePyroAirblast())
            {
                if (predictedInput.Input.FirePrimary)
                {
                    player.TryFirePyroFlare();
                }

                SyncPredictedLocalPlayerState(player);
            }

            return true;
        }

        if (player.HasScopedSniperWeaponEquipped)
        {
            TryPredictedToggleSniperScope(player);
            return true;
        }

        if (player.ClassId == PlayerClass.Spy)
        {
            if (!predictedInput.Input.FirePrimary)
            {
                TryPredictedToggleSpyCloak(player);
            }

            return true;
        }

        if (player.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella))
        {
            if (player.TryActivateCivvieUmbrella())
            {
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

    private bool TryPredictedPyroSelfAirblast(PlayerEntity player, bool fireFlare)
    {
        if (!player.TryFirePyroAirblast(
                PlayerEntity.PyroAirburstCost,
                PlayerEntity.PyroAirblastReloadTicks,
                PlayerEntity.PyroAirburstNoFlameTicks))
        {
            return false;
        }

        if (fireFlare)
        {
            player.TryFirePyroFlare();
        }

        TriggerLocalWeaponFireHudRumble(0.55f);
        var aimRadians = player.AimDirectionDegrees * (MathF.PI / 180f);
        player.AddImpulse(
            -MathF.Cos(aimRadians) * PredictedPyroSelfAirblastImpulse,
            -MathF.Sin(aimRadians) * PredictedPyroSelfAirblastImpulse + PredictedPyroSelfAirblastLift);
        player.SetMovementState(LegacyMovementState.Airblast);
        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private void ApplyPredictedSecondaryWeaponFire(PlayerEntity player, PredictedLocalInput predictedInput, bool swappedWeaponThisTick)
    {
        if (player.IsTaunting
            || !predictedInput.AbilityPressed
            || swappedWeaponThisTick)
        {
            return;
        }

        if (IsPredictedPyroSelfAirblastInput(player, predictedInput))
        {
            TryPredictedPyroSelfAirblast(player, predictedInput.Input.FirePrimary);
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

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.ScoutUtility))

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUtility)
            || player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber))
        {
            TryPredictedToggleMedicMedigun(player);
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
            return;
        }

        if (!predictedInput.Input.SwapWeapon && TryPredictedToggleSecondaryWeapon(player))
        {
            return;
        }

        if (!player.HasExperimentalOffhandWeapon)
        {
            return;
        }

        if (predictedInput.Input.SwapWeapon)
        {
            return;
        }

        TryPredictedToggleSecondaryWeapon(player);
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

        TriggerLocalWeaponFireHudRumble(GetPredictedWeaponFireHudRumbleIntensity(player));
        SyncPredictedLocalPlayerState(player);
        return true;
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
        return CharacterClassCatalog.RuntimeRegistry.TryGetGameplayAbilityDefinition(
                player.GameplayLoadoutState.UtilityItemId,
                out _,
                out var ability)
            ? ability
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
        return string.Equals(player.GameplayLoadoutState.UtilityItemId, StockGameplayModCatalog.HeavyUtilityItemId, StringComparison.Ordinal)
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

        TriggerLocalWeaponFireHudRumble(0.35f);
        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private static float GetPredictedWeaponFireHudRumbleIntensity(PlayerEntity player)
    {
        var weaponKind = player.IsExperimentalOffhandSelected && player.ExperimentalOffhandWeapon is not null
            ? player.ExperimentalOffhandWeapon.Kind
            : player.IsAcquiredWeaponPresented && player.AcquiredWeapon is not null
                ? player.AcquiredWeapon.Kind
                : player.PrimaryWeapon.Kind;

        return weaponKind switch
        {
            PrimaryWeaponKind.Minigun or PrimaryWeaponKind.FlameThrower => 0.35f,
            PrimaryWeaponKind.Medigun => 0.25f,
            PrimaryWeaponKind.RocketLauncher or PrimaryWeaponKind.GrenadeLauncher or PrimaryWeaponKind.MineLauncher => 0.9f,
            PrimaryWeaponKind.Rifle => 0.85f,
            PrimaryWeaponKind.Revolver => 0.65f,
            PrimaryWeaponKind.Blade => 0.45f,
            _ => 0.55f,
        };
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

    private bool TryPredictedToggleMedicMedigun(PlayerEntity player)
    {
        var targetSlot = player.GameplayLoadoutState.EquippedSlot == GameplayEquipmentSlot.Secondary
            ? GameplayEquipmentSlot.Primary
            : GameplayEquipmentSlot.Secondary;
        if (!player.TrySelectGameplayEquippedSlot(targetSlot))
        {
            return false;
        }

        SyncPredictedLocalPlayerState(player);
        return true;
    }

    private bool IsPredictedSpyBackstabAnimating()
    {
        return _predictedLocalActionState.SpyBackstabVisualTicksRemaining > 0;
    }

    private bool IsPredictedSpyBackstabReady()
    {
        return _predictedLocalActionState.SpyBackstabWindupTicksRemaining <= 0
            && _predictedLocalActionState.SpyBackstabRecoveryTicksRemaining <= 0;
    }
}
