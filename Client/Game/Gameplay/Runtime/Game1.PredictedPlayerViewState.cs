#nullable enable

using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public bool IsUsingPredictedLocalState(PlayerEntity player)
    {
        return CanUseLocalPrediction()
            && ReferenceEquals(player, _world.LocalPlayer)
            && _hasPredictedLocalActionState;
    }

    public PlayerEntity GetPlayerPredictedPresentationState(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player) && _predictedLocalPlayerShadow is not null
            ? _predictedLocalPlayerShadow
            : player;
    }

    public bool GetPlayerIsHeavyEating(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsHeavyEating
            : player.IsHeavyEating;
    }

    public int GetPlayerHeavyEatTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.HeavyEatTicksRemaining
            : player.HeavyEatTicksRemaining;
    }

    public int GetPlayerHeavyEatCooldownTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.HeavyEatCooldownTicksRemaining
            : player.HeavyEatCooldownTicksRemaining;
    }

    public int GetPlayerHeavyEatCooldownDurationTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _predictedLocalActionState.HeavyEatCooldownDurationTicks)
            : Math.Max(1, player.HeavyEatCooldownDurationTicks);
    }

    public bool GetPlayerIsExperimentalGhostDashing(PlayerEntity player)
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

    public bool GetPlayerExperimentalGhostDashEnablesTrail(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.ExperimentalGhostDashEnablesTrail
            : player.ExperimentalGhostDashEnablesTrail;
    }

    public int GetPlayerExperimentalGhostDashCooldownTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.ExperimentalGhostDashCooldownTicksRemaining
            : player.ExperimentalGhostDashCooldownTicksRemaining;
    }

    public int GetPlayerSpySuperjumpCooldownTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpySuperjumpCooldownTicksRemaining
            : player.SpySuperjumpCooldownTicksRemaining;
    }

    public int GetPlayerSpySuperjumpAvailableCharges(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpySuperjumpAvailableCharges
            : player.SpySuperjumpAvailableCharges;
    }

    public int GetPlayerSpySuperjumpMaximumCharges(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _predictedLocalActionState.SpySuperjumpMaximumCharges)
            : player.SpySuperjumpMaximumCharges;
    }

    public int GetPlayerSpySuperjumpChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpySuperjumpChargeTicks
            : player.SpySuperjumpChargeTicks;
    }

    public float GetPlayerSpySuperjumpChargeDirectionDegrees(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpySuperjumpChargeDirectionDegrees
            : player.SpySuperjumpChargeDirectionDegrees;
    }

    public bool GetPlayerIsSpySuperjumpActive(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SpySuperjumpChargeTicks > 0 || _predictedLocalActionState.IsSpySuperjumping
            : player.SpySuperjumpChargeTicks > 0 || player.IsSpySuperjumping;
    }

    public bool GetPlayerIsCarryingIntel(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsCarryingIntel
            : player.IsCarryingIntel;
    }

    public bool GetPlayerIsSniperScoped(PlayerEntity player)
    {
        if (!player.HasScopedSniperWeaponEquipped
            || GetPlayerIsSniperBowEquipped(player))
        {
            return false;
        }

        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsSniperScoped
            : player.IsSniperScoped;
    }

    public bool GetPlayerIsUsingBinoculars(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.IsUsingBinoculars
            : player.IsUsingBinoculars;
    }

    public int GetPlayerSniperChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SniperChargeTicks
            : player.SniperChargeTicks;
    }

    public int GetPlayerSniperBowChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.SniperBowChargeTicks
            : player.SniperBowChargeTicks;
    }

    public int GetPlayerStrongDrinkChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.StrongDrinkChargeTicks
            : player.StrongDrinkChargeTicks;
    }

    public float GetPlayerStrongDrinkChargeDirectionDegrees(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.StrongDrinkChargeDirectionDegrees
            : player.StrongDrinkChargeDirectionDegrees;
    }

    public bool GetPlayerIsSniperBowEquipped(PlayerEntity player)
    {
        if (IsUsingPredictedLocalState(player)
            && _predictedLocalPlayerShadow is not null
            && ReferenceEquals(player, _world.LocalPlayer))
        {
            return _predictedLocalPlayerShadow.IsSniperBowEquipped;
        }

        return player.IsSniperBowEquipped;
    }

    public bool GetPlayerIsMortarLauncherEquipped(PlayerEntity player)
    {
        if (IsUsingPredictedLocalState(player)
            && _predictedLocalPlayerShadow is not null
            && ReferenceEquals(player, _world.LocalPlayer))
        {
            return _predictedLocalPlayerShadow.IsMortarLauncherEquipped;
        }

        return player.IsMortarLauncherEquipped;
    }

    public int GetPlayerSniperRifleDamage(PlayerEntity player)
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

    public float GetPlayerMedicUberCharge(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.MedicUberCharge
            : player.MedicUberCharge;
    }

    public float GetPlayerMetal(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.Metal
            : player.Metal;
    }







    public int GetPlayerBuffBannerChargeDamage(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.BuffBannerChargeDamage
            : player.BuffBannerChargeDamage;
    }

    public int GetPlayerMedicHealDartCooldownTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.MedicHealDartCooldownTicks
            : player.MedicHealDartCooldownTicks;
    }

    public int GetPlayerBuffBannerMaxChargeDamage(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _predictedLocalActionState.BuffBannerMaxChargeDamage)
            : Math.Max(1, player.BuffBannerMaxChargeDamage);
    }

    public int GetPlayerBuffBannerMissingChargeDamage(PlayerEntity player)
    {
        return Math.Max(0, GetPlayerBuffBannerMaxChargeDamage(player) - GetPlayerBuffBannerChargeDamage(player));
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

    public bool GetPlayerIsBuffBannerDeploying(PlayerEntity player)
    {
        return GetPlayerBuffBannerDeployTicksRemaining(player) > 0;
    }

    public bool GetPlayerIsBuffBannerActive(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.BuffBannerActiveTicksRemaining > 0
            : player.IsBuffBannerActive;
    }

    public int GetPlayerPyroFlareCooldownTicks(PlayerEntity player)
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

    public int GetPlayerMedicNeedleRefillTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.MedicNeedleRefillTicks
            : player.MedicNeedleRefillTicks;
    }

    public bool GetPlayerIsCivvieUmbrellaActive(PlayerEntity player)
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

    public MedicUberDeliveryMode GetPlayerMedicUberDeliveryMode(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _predictedLocalActionState.MedicUberDeliveryMode
            : player.MedicUberPresentationMode;
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
