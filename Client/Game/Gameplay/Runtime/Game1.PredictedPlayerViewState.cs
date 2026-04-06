#nullable enable

using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsUsingPredictedLocalState(PlayerEntity player)
    {
        return _networkClient.IsConnected
            && ReferenceEquals(player, _world.LocalPlayer)
            && _hasPredictedLocalActionState;
    }

    private bool GetPlayerIsHeavyEating(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsHeavyEating
            : player.IsHeavyEating;
    }

    private int GetPlayerHeavyEatTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.HeavyEatTicksRemaining
            : player.HeavyEatTicksRemaining;
    }

    private int GetPlayerHeavyEatCooldownTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.HeavyEatCooldownTicksRemaining
            : player.HeavyEatCooldownTicksRemaining;
    }

    private bool GetPlayerIsSniperScoped(PlayerEntity player)
    {
        if (!player.HasScopedSniperWeaponEquipped)
        {
            return false;
        }

        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsSniperScoped
            : player.IsSniperScoped;
    }

    private int GetPlayerSniperChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SniperChargeTicks
            : player.SniperChargeTicks;
    }

    private int GetPlayerSniperRifleDamage(PlayerEntity player)
    {
        if (!player.HasScopedSniperWeaponEquipped || !GetPlayerIsSniperScoped(player))
        {
            return PlayerEntity.SniperBaseDamage;
        }

        var chargeTicks = GetPlayerSniperChargeTicks(player);
        return PlayerEntity.SniperBaseDamage + (int)MathF.Floor(MathF.Sqrt(chargeTicks * 125f / 6f));
    }

    private bool GetPlayerIsSpyCloaked(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsSpyCloaked
            : player.IsSpyCloaked;
    }

    private float GetPlayerSpyCloakAlpha(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpyCloakAlpha
            : player.SpyCloakAlpha;
    }

    private bool GetPlayerIsSpyVisibleToEnemies(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsSpyVisibleToEnemies
            : player.IsSpyVisibleToEnemies;
    }

    private bool GetPlayerIsSpyBackstabReady(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.IsSpyBackstabReady;
        }

        return _predictedLocalActionState.SpyBackstabWindupTicksRemaining <= 0
            && _predictedLocalActionState.SpyBackstabRecoveryTicksRemaining <= 0;
    }

    private bool GetPlayerIsSpyBackstabAnimating(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.IsSpyBackstabAnimating;
        }

        return _predictedLocalActionState.SpyBackstabVisualTicksRemaining > 0;
    }

    private int GetPlayerSpyBackstabVisualTicksRemaining(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.SpyBackstabVisualTicksRemaining;
        }

        return _predictedLocalActionState.SpyBackstabVisualTicksRemaining;
    }

    private float GetPlayerMedicUberCharge(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.MedicUberCharge
            : player.MedicUberCharge;
    }

    private float GetPlayerMetal(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.Metal
            : player.Metal;
    }

    private int GetPlayerCurrentShells(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.CurrentShells
            : player.CurrentShells;
    }

    private int GetPlayerPrimaryCooldownTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.PrimaryCooldownTicks
            : player.PrimaryCooldownTicks;
    }

    private int GetPlayerReloadTicksUntilNextShell(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.ReloadTicksUntilNextShell
            : player.ReloadTicksUntilNextShell;
    }

    private int GetPlayerPyroFlareCooldownTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.PyroFlareCooldownTicks
            : player.PyroFlareCooldownTicks;
    }

    private float GetPlayerIntelRechargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IntelRechargeTicks
            : player.IntelRechargeTicks;
    }

    private int GetPlayerMedicNeedleRefillTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.MedicNeedleRefillTicks
            : player.MedicNeedleRefillTicks;
    }
}
