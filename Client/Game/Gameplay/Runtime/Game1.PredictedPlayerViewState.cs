#nullable enable

using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool IsUsingPredictedLocalState(PlayerEntity player)
    {
        return CanUseLocalPrediction()
            && ReferenceEquals(player, _world.LocalPlayer)
            && _hasPredictedLocalActionState;
    }

    private PlayerEntity GetPlayerPredictedPresentationState(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player) && _predictedLocalPlayerShadow is not null
            ? _predictedLocalPlayerShadow
            : player;
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

    private int GetPlayerHeavyEatCooldownDurationTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _predictedLocalActionState.HeavyEatCooldownDurationTicks)
            : Math.Max(1, player.HeavyEatCooldownDurationTicks);
    }

    private bool GetPlayerIsExperimentalGhostDashing(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Heavy)
        {
            return false;
        }

        if (IsUsingPredictedLocalState(player))
        {
            return _predictedLocalActionState.IsExperimentalGhostDashing;
        }

        // For remote players, IsExperimentalGhostDashing is not serialized into snapshots.
        // Use the replicated toggle that the server sends for HUD and visual purposes.
        return player.TryGetReplicatedStateBool(
            GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
            GameplayAbilityReplicatedState.HeavyDashActiveKey,
            out var isDashing) && isDashing;
    }

    private bool GetPlayerExperimentalGhostDashEnablesTrail(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.ExperimentalGhostDashEnablesTrail
            : player.ExperimentalGhostDashEnablesTrail;
    }

    private int GetPlayerExperimentalGhostDashCooldownTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.ExperimentalGhostDashCooldownTicksRemaining
            : player.ExperimentalGhostDashCooldownTicksRemaining;
    }

    private int GetPlayerSpySuperjumpCooldownTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpySuperjumpCooldownTicksRemaining
            : player.SpySuperjumpCooldownTicksRemaining;
    }

    private int GetPlayerSpySuperjumpAvailableCharges(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpySuperjumpAvailableCharges
            : player.SpySuperjumpAvailableCharges;
    }

    private int GetPlayerSpySuperjumpMaximumCharges(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _predictedLocalActionState.SpySuperjumpMaximumCharges)
            : player.SpySuperjumpMaximumCharges;
    }

    private int GetPlayerSpySuperjumpChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpySuperjumpChargeTicks
            : player.SpySuperjumpChargeTicks;
    }

    private float GetPlayerSpySuperjumpChargeDirectionDegrees(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpySuperjumpChargeDirectionDegrees
            : player.SpySuperjumpChargeDirectionDegrees;
    }

    private bool GetPlayerIsSpySuperjumpActive(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpySuperjumpChargeTicks > 0 || _predictedLocalActionState.IsSpySuperjumping
            : player.SpySuperjumpChargeTicks > 0 || player.IsSpySuperjumping;
    }

    private bool GetPlayerIsCarryingIntel(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsCarryingIntel
            : player.IsCarryingIntel;
    }

    private bool GetPlayerIsSniperScoped(PlayerEntity player)
    {
        if (!player.HasScopedSniperWeaponEquipped
            || GetPlayerIsSniperBowEquipped(player)
            || player.LastToDieSniperProfile.LightMarksmanEnabled)
        {
            return false;
        }

        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsSniperScoped
            : player.IsSniperScoped;
    }

    private bool GetPlayerIsUsingBinoculars(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsUsingBinoculars
            : player.IsUsingBinoculars;
    }

    private int GetPlayerSniperChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SniperChargeTicks
            : player.SniperChargeTicks;
    }

    private int GetPlayerSniperBowChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SniperBowChargeTicks
            : player.SniperBowChargeTicks;
    }

    private bool GetPlayerIsSniperBowEquipped(PlayerEntity player)
    {
        if (IsUsingPredictedLocalState(player)
            && _predictedLocalPlayerShadow is not null
            && ReferenceEquals(player, _world.LocalPlayer))
        {
            return _predictedLocalPlayerShadow.IsSniperBowEquipped;
        }

        return player.IsSniperBowEquipped;
    }

    private int GetPlayerSniperRifleDamage(PlayerEntity player)
    {
        return player.GetSniperRifleDamageForCharge(
            GetPlayerSniperChargeTicks(player),
            GetPlayerIsSniperScoped(player));
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

    private float GetPlayerLastToDieSpyCloakMeterFraction(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.LastToDieSpyCloakMeterFraction;
        }

        var maximum = _predictedLocalActionState.LastToDieSpyCloakMeterMaximumUnits;
        return maximum <= 0
            ? 1f
            : Math.Clamp(_predictedLocalActionState.LastToDieSpyCloakMeterUnits / (float)maximum, 0f, 1f);
    }

    private int GetPlayerLastToDieSpyRogueRampStacks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.LastToDieSpyRogueRampStacks
            : player.LastToDieSpyRogueRampStacks;
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

    private int GetPlayerExperimentalOffhandCurrentShells(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.ExperimentalOffhandCurrentShells
            : player.ExperimentalOffhandCurrentShells;
    }

    private int GetPlayerExperimentalOffhandCooldownTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.ExperimentalOffhandCooldownTicks
            : player.ExperimentalOffhandCooldownTicks;
    }

    private int GetPlayerExperimentalOffhandReloadTicksUntilNextShell(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.ExperimentalOffhandReloadTicksUntilNextShell
            : player.ExperimentalOffhandReloadTicksUntilNextShell;
    }

    private int GetPlayerBuffBannerChargeKills(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.BuffBannerChargeKills
            : player.BuffBannerChargeKills;
    }

    private int GetPlayerBuffBannerMaxChargeKills(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _predictedLocalActionState.BuffBannerMaxChargeKills)
            : Math.Max(1, player.BuffBannerMaxChargeKills);
    }

    private int GetPlayerBuffBannerMissingChargeKills(PlayerEntity player)
    {
        return Math.Max(0, GetPlayerBuffBannerMaxChargeKills(player) - GetPlayerBuffBannerChargeKills(player));
    }

    private int GetPlayerBuffBannerDeployTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.BuffBannerDeployTicksRemaining
            : player.BuffBannerDeployTicksRemaining;
    }

    private int GetPlayerBuffBannerDeployDurationTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _predictedLocalActionState.BuffBannerDeployDurationTicks)
            : Math.Max(1, player.BuffBannerDeployDurationTicks);
    }

    private bool GetPlayerIsBuffBannerDeploying(PlayerEntity player)
    {
        return GetPlayerBuffBannerDeployTicksRemaining(player) > 0;
    }

    private bool GetPlayerIsBuffBannerActive(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.BuffBannerActiveTicksRemaining > 0
            : player.IsBuffBannerActive;
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

    private bool GetPlayerIsCivvieUmbrellaActive(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsCivvieUmbrellaActive
            : player.IsCivvieUmbrellaActive;
    }

    private bool GetPlayerIsCivvieUmbrellaBroken(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsCivvieUmbrellaBroken
            : player.IsCivvieUmbrellaBroken;
    }

    private int GetPlayerCivvieUmbrellaChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.CivvieUmbrellaChargeTicks
            : player.CivvieUmbrellaChargeTicks;
    }

    private bool GetPlayerIsCivviePogoActive(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsCivviePogoActive
            : player.IsCivviePogoActive;
    }

    private int GetPlayerCivviePogoCrunchTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.CivviePogoCrunchTicksRemaining
            : player.CivviePogoCrunchTicksRemaining;
    }

    private int GetPlayerCivviePogoTrickTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.CivviePogoTrickTicksRemaining
            : player.CivviePogoTrickTicksRemaining;
    }

    private int GetPlayerCivviePogoTrickDurationAtStart(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.CivviePogoTrickDurationAtStart
            : player.CivviePogoTrickDurationAtStart;
    }

    private bool GetPlayerIsCivviePogoTrickActive(PlayerEntity player)
    {
        return GetPlayerCivviePogoTrickTicksRemaining(player) > 0
            || _civviePogoTrickPresentationTicksByPlayerId.ContainsKey(player.Id);
    }

    private MedicUberDeliveryMode GetPlayerMedicUberDeliveryMode(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.MedicUberDeliveryMode
            : player.MedicUberPresentationMode;
    }

    private bool GetPlayerIsMedicUbering(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsMedicUbering
            : player.IsMedicUberDeliveryActive;
    }

    private PlayerEntity GetPlayerCivviePresentationSource(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            && _predictedLocalPlayerShadow is { } predictedPlayer
            && predictedPlayer.Id == player.Id
                ? predictedPlayer
                : player;
    }
}
