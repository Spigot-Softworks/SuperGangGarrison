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
            && _localPredictionState.HasPredictedLocalActionState;
    }

    public PlayerEntity GetPlayerPredictedPresentationState(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player) && _localPredictionState.PredictedLocalPlayerShadow is not null
            ? _localPredictionState.PredictedLocalPlayerShadow
            : player;
    }

    public bool GetPlayerIsHeavyEating(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.IsHeavyEating
            : player.IsHeavyEating;
    }

    public int GetPlayerHeavyEatTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.HeavyEatTicksRemaining
            : player.HeavyEatTicksRemaining;
    }

    public int GetPlayerHeavyEatCooldownTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.HeavyEatCooldownTicksRemaining
            : player.HeavyEatCooldownTicksRemaining;
    }

    public int GetPlayerHeavyEatCooldownDurationTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _localPredictionState.PredictedLocalActionState.HeavyEatCooldownDurationTicks)
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
            return _localPredictionState.PredictedLocalActionState.IsExperimentalGhostDashing;
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
            ? _localPredictionState.PredictedLocalActionState.ExperimentalGhostDashEnablesTrail
            : player.ExperimentalGhostDashEnablesTrail;
    }

    public int GetPlayerExperimentalGhostDashCooldownTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.ExperimentalGhostDashCooldownTicksRemaining
            : player.ExperimentalGhostDashCooldownTicksRemaining;
    }

    public int GetPlayerSpySuperjumpCooldownTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.SpySuperjumpCooldownTicksRemaining
            : player.SpySuperjumpCooldownTicksRemaining;
    }

    public int GetPlayerSpySuperjumpAvailableCharges(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.SpySuperjumpAvailableCharges
            : player.SpySuperjumpAvailableCharges;
    }

    public int GetPlayerSpySuperjumpMaximumCharges(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _localPredictionState.PredictedLocalActionState.SpySuperjumpMaximumCharges)
            : player.SpySuperjumpMaximumCharges;
    }

    public int GetPlayerSpySuperjumpChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.SpySuperjumpChargeTicks
            : player.SpySuperjumpChargeTicks;
    }

    public float GetPlayerSpySuperjumpChargeDirectionDegrees(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.SpySuperjumpChargeDirectionDegrees
            : player.SpySuperjumpChargeDirectionDegrees;
    }

    public bool GetPlayerIsSpySuperjumpActive(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.SpySuperjumpChargeTicks > 0 || _localPredictionState.PredictedLocalActionState.IsSpySuperjumping
            : player.SpySuperjumpChargeTicks > 0 || player.IsSpySuperjumping;
    }

    public bool GetPlayerIsCarryingIntel(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.IsCarryingIntel
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
            ? _localPredictionState.PredictedLocalActionState.IsSniperScoped
            : player.IsSniperScoped;
    }

    public bool GetPlayerIsUsingBinoculars(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.IsUsingBinoculars
            : player.IsUsingBinoculars;
    }

    public int GetPlayerSniperChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.SniperChargeTicks
            : player.SniperChargeTicks;
    }

    public int GetPlayerSniperBowChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.SniperBowChargeTicks
            : player.SniperBowChargeTicks;
    }

    public int GetPlayerStrongDrinkChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.StrongDrinkChargeTicks
            : player.StrongDrinkChargeTicks;
    }

    public float GetPlayerStrongDrinkChargeDirectionDegrees(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.StrongDrinkChargeDirectionDegrees
            : player.StrongDrinkChargeDirectionDegrees;
    }

    public bool GetPlayerIsSniperBowEquipped(PlayerEntity player)
    {
        if (IsUsingPredictedLocalState(player)
            && _localPredictionState.PredictedLocalPlayerShadow is not null
            && ReferenceEquals(player, _world.LocalPlayer))
        {
            return _localPredictionState.PredictedLocalPlayerShadow.IsSniperBowEquipped;
        }

        return player.IsSniperBowEquipped;
    }

    public bool GetPlayerIsMortarLauncherEquipped(PlayerEntity player)
    {
        if (IsUsingPredictedLocalState(player)
            && _localPredictionState.PredictedLocalPlayerShadow is not null
            && ReferenceEquals(player, _world.LocalPlayer))
        {
            return _localPredictionState.PredictedLocalPlayerShadow.IsMortarLauncherEquipped;
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
            ? _localPredictionState.PredictedLocalActionState.IsSpyCloaked
            : player.IsSpyCloaked;
    }

    private float GetPlayerSpyCloakAlpha(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.SpyCloakAlpha
            : player.SpyCloakAlpha;
    }

    private float GetPlayerLastToDieSpyCloakMeterFraction(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.LastToDieSpyCloakMeterFraction;
        }

        var maximum = _localPredictionState.PredictedLocalActionState.LastToDieSpyCloakMeterMaximumUnits;
        return maximum <= 0
            ? 1f
            : Math.Clamp(_localPredictionState.PredictedLocalActionState.LastToDieSpyCloakMeterUnits / (float)maximum, 0f, 1f);
    }

    private int GetPlayerLastToDieSpyRogueRampStacks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.LastToDieSpyRogueRampStacks
            : player.LastToDieSpyRogueRampStacks;
    }

    private bool GetPlayerIsSpyVisibleToEnemies(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.IsSpyVisibleToEnemies
            : player.IsSpyVisibleToEnemies;
    }

    private bool GetPlayerIsSpyBackstabReady(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.IsSpyBackstabReady;
        }

        return _localPredictionState.PredictedLocalActionState.SpyBackstabWindupTicksRemaining <= 0
            && _localPredictionState.PredictedLocalActionState.SpyBackstabRecoveryTicksRemaining <= 0;
    }

    private bool GetPlayerIsSpyBackstabAnimating(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.IsSpyBackstabAnimating;
        }

        return _localPredictionState.PredictedLocalActionState.SpyBackstabVisualTicksRemaining > 0;
    }

    private int GetPlayerSpyBackstabVisualTicksRemaining(PlayerEntity player)
    {
        if (!IsUsingPredictedLocalState(player))
        {
            return player.SpyBackstabVisualTicksRemaining;
        }

        return _localPredictionState.PredictedLocalActionState.SpyBackstabVisualTicksRemaining;
    }

    public float GetPlayerMedicUberCharge(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.MedicUberCharge
            : player.MedicUberCharge;
    }

    public float GetPlayerMetal(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.Metal
            : player.Metal;
    }







    public int GetPlayerBuffBannerChargeDamage(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.BuffBannerChargeDamage
            : player.BuffBannerChargeDamage;
    }

    public int GetPlayerMedicHealDartCooldownTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.MedicHealDartCooldownTicks
            : player.MedicHealDartCooldownTicks;
    }

    public int GetPlayerBuffBannerMaxChargeDamage(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _localPredictionState.PredictedLocalActionState.BuffBannerMaxChargeDamage)
            : Math.Max(1, player.BuffBannerMaxChargeDamage);
    }

    public int GetPlayerBuffBannerMissingChargeDamage(PlayerEntity player)
    {
        return Math.Max(0, GetPlayerBuffBannerMaxChargeDamage(player) - GetPlayerBuffBannerChargeDamage(player));
    }

    private int GetPlayerBuffBannerDeployTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.BuffBannerDeployTicksRemaining
            : player.BuffBannerDeployTicksRemaining;
    }

    private int GetPlayerBuffBannerDeployDurationTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? Math.Max(1, _localPredictionState.PredictedLocalActionState.BuffBannerDeployDurationTicks)
            : Math.Max(1, player.BuffBannerDeployDurationTicks);
    }

    public bool GetPlayerIsBuffBannerDeploying(PlayerEntity player)
    {
        return GetPlayerBuffBannerDeployTicksRemaining(player) > 0;
    }

    public bool GetPlayerIsBuffBannerActive(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.BuffBannerActiveTicksRemaining > 0
            : player.IsBuffBannerActive;
    }

    public int GetPlayerPyroFlareCooldownTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.PyroFlareCooldownTicks
            : player.PyroFlareCooldownTicks;
    }

    private float GetPlayerIntelRechargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.IntelRechargeTicks
            : player.IntelRechargeTicks;
    }

    public int GetPlayerMedicNeedleRefillTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.MedicNeedleRefillTicks
            : player.MedicNeedleRefillTicks;
    }

    public bool GetPlayerIsCivvieUmbrellaActive(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.IsCivvieUmbrellaActive
            : player.IsCivvieUmbrellaActive;
    }

    private bool GetPlayerIsCivvieUmbrellaBroken(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.IsCivvieUmbrellaBroken
            : player.IsCivvieUmbrellaBroken;
    }

    private int GetPlayerCivvieUmbrellaChargeTicks(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.CivvieUmbrellaChargeTicks
            : player.CivvieUmbrellaChargeTicks;
    }

    private bool GetPlayerIsCivviePogoActive(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.IsCivviePogoActive
            : player.IsCivviePogoActive;
    }

    private int GetPlayerCivviePogoCrunchTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.CivviePogoCrunchTicksRemaining
            : player.CivviePogoCrunchTicksRemaining;
    }

    private int GetPlayerCivviePogoTrickTicksRemaining(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.CivviePogoTrickTicksRemaining
            : player.CivviePogoTrickTicksRemaining;
    }

    private int GetPlayerCivviePogoTrickDurationAtStart(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            ? _localPredictionState.PredictedLocalActionState.CivviePogoTrickDurationAtStart
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
            ? _localPredictionState.PredictedLocalActionState.MedicUberDeliveryMode
            : player.MedicUberPresentationMode;
    }


    private PlayerEntity GetPlayerCivviePresentationSource(PlayerEntity player)
    {
        return IsUsingPredictedLocalState(player)
            && _localPredictionState.PredictedLocalPlayerShadow is { } predictedPlayer
            && predictedPlayer.Id == player.Id
                ? predictedPlayer
                : player;
    }
}
