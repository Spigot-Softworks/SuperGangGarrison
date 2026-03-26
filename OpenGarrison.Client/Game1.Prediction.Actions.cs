#nullable enable

using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void AdvancePredictedActionState(PlayerEntity player)
    {
        AdvancePredictedWeaponState(player);
        AdvancePredictedEngineerState(player);
        AdvancePredictedHeavyState();
        AdvancePredictedSniperState(player);
        AdvancePredictedMedicState(player);
        AdvancePredictedSpyState(player);
    }

    private void AdvancePredictedEngineerState(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Engineer || _predictedLocalActionState.Metal >= player.MaxMetal)
        {
            return;
        }

        _predictedLocalActionState.Metal = float.Min(player.MaxMetal, _predictedLocalActionState.Metal + 0.1f);
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

    private void AdvancePredictedHeavyState()
    {
        if (!_predictedLocalActionState.IsHeavyEating)
        {
            return;
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
        if (player.ClassId != PlayerClass.Sniper || !_predictedLocalActionState.IsSniperScoped || _predictedLocalActionState.PrimaryCooldownTicks > 0)
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
            _predictedLocalActionState.MedicUberCharge = float.Max(0f, _predictedLocalActionState.MedicUberCharge - 10f);
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
            _predictedLocalActionState.SpyBackstabWindupTicksRemaining = 0;
            _predictedLocalActionState.SpyBackstabRecoveryTicksRemaining = 0;
            _predictedLocalActionState.SpyBackstabVisualTicksRemaining = 0;
            return;
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

        if (player.ClassId == PlayerClass.Spy && TryPredictedStartSpyBackstab(player))
        {
            return;
        }

        if (player.ClassId == PlayerClass.Quote)
        {
            if (player.TryFireQuoteBubble())
            {
                SyncPredictedLocalPlayerState(player);
            }

            return;
        }

        TryPredictedFirePrimaryWeapon(player);
    }

    private void ApplyPredictedSecondaryFire(PlayerEntity player, PredictedLocalInput predictedInput)
    {
        if (player.IsTaunting)
        {
            return;
        }

        if (player.ClassId == PlayerClass.Medic)
        {
            if (!predictedInput.Input.FireSecondary)
            {
                return;
            }

            if (TryPredictedFireMedicNeedle(player))
            {
                return;
            }

            if (_predictedLocalActionState.IsMedicUberReady && predictedInput.Input.FirePrimary)
            {
                TryPredictedStartMedicUber(player);
            }

            return;
        }

        var useHeldSecondary = player.ClassId is PlayerClass.Demoman or PlayerClass.Quote;
        if ((!useHeldSecondary && !predictedInput.SecondaryPressed)
            || (useHeldSecondary && !predictedInput.Input.FireSecondary))
        {
            return;
        }

        if (player.ClassId == PlayerClass.Demoman)
        {
            return;
        }

        if (player.ClassId == PlayerClass.Heavy)
        {
            TryPredictedStartHeavySelfHeal(player);
            return;
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

            return;
        }

        if (player.ClassId == PlayerClass.Sniper)
        {
            TryPredictedToggleSniperScope(player);
            return;
        }

        if (player.ClassId == PlayerClass.Spy)
        {
            if (!predictedInput.Input.FirePrimary)
            {
                TryPredictedToggleSpyCloak(player);
            }

            return;
        }

        if (player.ClassId == PlayerClass.Quote && player.TryFireQuoteBlade())
        {
            SyncPredictedLocalPlayerState(player);
        }
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

    private bool TryPredictedStartSpyBackstab(PlayerEntity player)
    {
        if (!player.TryStartSpyBackstab(player.AimDirectionDegrees))
        {
            return false;
        }

        SpawnBackstabVisual(player.Id, player.Team, player.X, player.Y, player.AimDirectionDegrees);
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
        return _predictedLocalActionState.SpyBackstabVisualTicksRemaining > 0;
    }

    private bool IsPredictedSpyBackstabReady()
    {
        return _predictedLocalActionState.SpyBackstabWindupTicksRemaining <= 0
            && _predictedLocalActionState.SpyBackstabRecoveryTicksRemaining <= 0;
    }
}
