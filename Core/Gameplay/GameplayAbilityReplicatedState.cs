namespace OpenGarrison.Core;

public static class GameplayAbilityReplicatedState
{
    public const string PyroAirblastCooldownTicksKey = "pyro_airblast_cooldown_ticks";
    public const string MedicUberChargeKey = "medic_uber_charge";
    public const string MedicUberReadyKey = "medic_uber_ready";
    public const string MedicNeedlegunCooldownTicksKey = "medic_needlegun_cooldown_ticks";
    public const string HeavyEatTicksRemainingKey = "heavy_eat_ticks_remaining";
    public const string HeavyEatCooldownTicksKey = "heavy_eat_cooldown_ticks";
    public const string SniperChargeTicksKey = "sniper_charge_ticks";
    public const string SniperBowChargeTicksKey = "sniper_bow_charge_ticks";
    public const string SpyCloakAlphaKey = "spy_cloak_alpha";
    public const string SpySuperjumpCooldownTicksKey = "spy_superjump_cooldown_ticks";
    public const string SpySuperjumpActiveKey = "spy_superjump_active";
    public const string SpySuperjumpDisabledKey = "spy_superjump_disabled";
    public const string CivvieUmbrellaCooldownTicksKey = "civvie_umbrella_cooldown_ticks";
    public const string CivvieUmbrellaActiveKey = "civvie_umbrella_active";
    public const string CivvieUmbrellaDisabledKey = "civvie_umbrella_disabled";
    public const string CivviePogoActiveKey = "civvie_pogo_active";
    public const string CivviePogoCrunchTicksKey = "civvie_pogo_crunch_ticks";
    public const string CivviePogoTrickTicksKey = "civvie_pogo_trick_ticks";
    public const string CivviePogoTrickDurationTicksKey = "civvie_pogo_trick_duration_ticks";
    public const string HeavyDashCooldownTicksKey = "heavy_dash_cooldown_ticks";
    public const string HeavyDashActiveKey = "heavy_dash_active";
    public const string HeavyDashVisibleKey = "heavy_dash_visible";
    public const string HeavyDashTrailAlphaKey = "heavy_dash_trail_alpha";
    public const string BuffBannerChargeKillsKey = "buff_banner_charge_kills";
    public const string BuffBannerMissingKillsKey = "buff_banner_missing_kills";
    public const string BuffBannerDeployTicksKey = "buff_banner_deploy_ticks";
    public const string BuffBannerActiveTicksKey = "buff_banner_active_ticks";
    public const string BuffBannerDeployingOrActiveKey = "buff_banner_deploying_or_active";

    public static IReadOnlyList<GameplayReplicatedStateEntry> CreateEntries(PlayerEntity player)
    {
        return player.ClassId switch
        {
            PlayerClass.Pyro =>
            [
                Whole(PyroAirblastCooldownTicksKey, player.PyroAirblastCooldownTicks),
            ],
            PlayerClass.Medic =>
            [
                Scalar(MedicUberChargeKey, player.MedicUberCharge),
                Toggle(MedicUberReadyKey, player.IsMedicUberReady),
                Whole(MedicNeedlegunCooldownTicksKey, player.MedicNeedleCooldownTicks),
            ],
            PlayerClass.Heavy =>
            [
                Whole(HeavyEatTicksRemainingKey, player.HeavyEatTicksRemaining),
                Whole(HeavyEatCooldownTicksKey, player.HeavyEatCooldownTicksRemaining),
                Whole(HeavyDashCooldownTicksKey, player.ExperimentalGhostDashCooldownTicksRemaining),
                Toggle(HeavyDashActiveKey, player.IsExperimentalGhostDashing),
                Toggle(HeavyDashVisibleKey, player.IsExperimentalGhostDashVisible),
                Scalar(HeavyDashTrailAlphaKey, player.ExperimentalGhostDashTrailAlpha),
            ],
            PlayerClass.Soldier =>
            [
                Whole(BuffBannerChargeKillsKey, player.BuffBannerChargeKills),
                Whole(BuffBannerMissingKillsKey, player.BuffBannerMissingChargeKills),
                Whole(BuffBannerDeployTicksKey, player.BuffBannerDeployTicksRemaining),
                Whole(BuffBannerActiveTicksKey, player.BuffBannerActiveTicksRemaining),
                Toggle(BuffBannerDeployingOrActiveKey, player.IsBuffBannerDeploying || player.IsBuffBannerActive),
            ],
            PlayerClass.Sniper =>
            [
                Whole(SniperChargeTicksKey, player.SniperChargeTicks),
                Whole(SniperBowChargeTicksKey, player.SniperBowChargeTicks),
            ],
            PlayerClass.Spy =>
            [
                Scalar(SpyCloakAlphaKey, player.SpyCloakAlpha),
                Whole(SpySuperjumpCooldownTicksKey, player.SpySuperjumpCooldownTicksRemaining),
                Toggle(SpySuperjumpActiveKey, player.SpySuperjumpChargeTicks > 0 || player.IsSpySuperjumping),
                Toggle(SpySuperjumpDisabledKey, player.IsCarryingIntel),
            ],
            PlayerClass.Quote =>
            [
                Whole(CivvieUmbrellaCooldownTicksKey, player.CivvieUmbrellaCooldownTicks),
                Toggle(CivvieUmbrellaActiveKey, player.IsCivvieUmbrellaActive),
                Toggle(CivvieUmbrellaDisabledKey, player.IsCivvieUmbrellaDisabled),
                Toggle(CivviePogoActiveKey, player.IsCivviePogoActive),
                Whole(CivviePogoCrunchTicksKey, player.CivviePogoCrunchTicksRemaining),
                Whole(CivviePogoTrickTicksKey, player.CivviePogoTrickTicksRemaining),
                Whole(CivviePogoTrickDurationTicksKey, player.CivviePogoTrickDurationAtStart),
            ],
            _ => Array.Empty<GameplayReplicatedStateEntry>(),
        };
    }

    public static bool TryGetInt(PlayerEntity player, string key, out int value)
    {
        if (player.ClassId == PlayerClass.Soldier
            && key is BuffBannerChargeKillsKey
                or BuffBannerMissingKillsKey
                or BuffBannerDeployTicksKey
                or BuffBannerActiveTicksKey
            && player.TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                key,
                out value))
        {
            return true;
        }

        if (key == HeavyDashCooldownTicksKey
            && player.ClassId == PlayerClass.Heavy
            && player.TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                HeavyDashCooldownTicksKey,
                out value))
        {
            return true;
        }

        if (key == CivvieUmbrellaCooldownTicksKey
            && player.ClassId == PlayerClass.Quote
            && player.TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                CivvieUmbrellaCooldownTicksKey,
                out value))
        {
            return true;
        }

        if (player.ClassId == PlayerClass.Quote
            && key is CivviePogoTrickTicksKey or CivviePogoTrickDurationTicksKey
            && player.TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                key,
                out value))
        {
            return true;
        }

        value = key switch
        {
            PyroAirblastCooldownTicksKey when player.ClassId == PlayerClass.Pyro => player.PyroAirblastCooldownTicks,
            MedicNeedlegunCooldownTicksKey when player.ClassId == PlayerClass.Medic => player.MedicNeedleCooldownTicks,
            HeavyEatTicksRemainingKey when player.ClassId == PlayerClass.Heavy => player.HeavyEatTicksRemaining,
            HeavyEatCooldownTicksKey when player.ClassId == PlayerClass.Heavy => player.HeavyEatCooldownTicksRemaining,
            HeavyDashCooldownTicksKey when player.ClassId == PlayerClass.Heavy => player.ExperimentalGhostDashCooldownTicksRemaining,
            SniperChargeTicksKey when player.ClassId == PlayerClass.Sniper => player.SniperChargeTicks,
            SniperBowChargeTicksKey when player.ClassId == PlayerClass.Sniper => player.SniperBowChargeTicks,
            SpySuperjumpCooldownTicksKey when player.ClassId == PlayerClass.Spy => player.SpySuperjumpCooldownTicksRemaining,
            CivvieUmbrellaCooldownTicksKey when player.ClassId == PlayerClass.Quote => player.CivvieUmbrellaCooldownTicks,
            CivviePogoCrunchTicksKey when player.ClassId == PlayerClass.Quote => player.CivviePogoCrunchTicksRemaining,
            CivviePogoTrickTicksKey when player.ClassId == PlayerClass.Quote => player.CivviePogoTrickTicksRemaining,
            CivviePogoTrickDurationTicksKey when player.ClassId == PlayerClass.Quote => player.CivviePogoTrickDurationAtStart,
            BuffBannerChargeKillsKey when player.ClassId == PlayerClass.Soldier => player.BuffBannerChargeKills,
            BuffBannerMissingKillsKey when player.ClassId == PlayerClass.Soldier => player.BuffBannerMissingChargeKills,
            BuffBannerDeployTicksKey when player.ClassId == PlayerClass.Soldier => player.BuffBannerDeployTicksRemaining,
            BuffBannerActiveTicksKey when player.ClassId == PlayerClass.Soldier => player.BuffBannerActiveTicksRemaining,
            _ => default,
        };

        return key switch
        {
            PyroAirblastCooldownTicksKey => player.ClassId == PlayerClass.Pyro,
            MedicNeedlegunCooldownTicksKey => player.ClassId == PlayerClass.Medic,
            HeavyEatTicksRemainingKey or HeavyEatCooldownTicksKey or HeavyDashCooldownTicksKey => player.ClassId == PlayerClass.Heavy,
            SniperChargeTicksKey or SniperBowChargeTicksKey => player.ClassId == PlayerClass.Sniper,
            SpySuperjumpCooldownTicksKey => player.ClassId == PlayerClass.Spy,
            CivvieUmbrellaCooldownTicksKey
                or CivviePogoCrunchTicksKey
                or CivviePogoTrickTicksKey
                or CivviePogoTrickDurationTicksKey => player.ClassId == PlayerClass.Quote,
            BuffBannerChargeKillsKey
                or BuffBannerMissingKillsKey
                or BuffBannerDeployTicksKey
                or BuffBannerActiveTicksKey => player.ClassId == PlayerClass.Soldier,
            _ => false,
        };
    }

    public static bool TryGetFloat(PlayerEntity player, string key, out float value)
    {
        value = key switch
        {
            MedicUberChargeKey when player.ClassId == PlayerClass.Medic => player.MedicUberCharge,
            HeavyDashTrailAlphaKey when player.ClassId == PlayerClass.Heavy => player.ExperimentalGhostDashTrailAlpha,
            SpyCloakAlphaKey when player.ClassId == PlayerClass.Spy => player.SpyCloakAlpha,
            _ => default,
        };

        return key switch
        {
            MedicUberChargeKey => player.ClassId == PlayerClass.Medic,
            HeavyDashTrailAlphaKey => player.ClassId == PlayerClass.Heavy,
            SpyCloakAlphaKey => player.ClassId == PlayerClass.Spy,
            _ => false,
        };
    }

    public static bool TryGetBool(PlayerEntity player, string key, out bool value)
    {
        if (player.ClassId == PlayerClass.Soldier
            && key == BuffBannerDeployingOrActiveKey
            && player.TryGetReplicatedStateBool(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                key,
                out value))
        {
            return true;
        }

        if (player.ClassId == PlayerClass.Quote
            && key is CivvieUmbrellaActiveKey or CivvieUmbrellaDisabledKey or CivviePogoActiveKey
            && player.TryGetReplicatedStateBool(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                key,
                out value))
        {
            return true;
        }

        value = key switch
        {
            MedicUberReadyKey when player.ClassId == PlayerClass.Medic => player.IsMedicUberReady,
            HeavyDashActiveKey when player.ClassId == PlayerClass.Heavy => player.IsExperimentalGhostDashing,
            HeavyDashVisibleKey when player.ClassId == PlayerClass.Heavy => player.IsExperimentalGhostDashVisible,
            SpySuperjumpActiveKey when player.ClassId == PlayerClass.Spy => player.SpySuperjumpChargeTicks > 0 || player.IsSpySuperjumping,
            SpySuperjumpDisabledKey when player.ClassId == PlayerClass.Spy => player.IsCarryingIntel,
            CivvieUmbrellaActiveKey when player.ClassId == PlayerClass.Quote => player.IsCivvieUmbrellaActive,
            CivvieUmbrellaDisabledKey when player.ClassId == PlayerClass.Quote => player.IsCivvieUmbrellaDisabled,
            CivviePogoActiveKey when player.ClassId == PlayerClass.Quote => player.IsCivviePogoActive,
            BuffBannerDeployingOrActiveKey when player.ClassId == PlayerClass.Soldier => player.IsBuffBannerDeploying || player.IsBuffBannerActive,
            _ => default,
        };

        return key switch
        {
            MedicUberReadyKey => player.ClassId == PlayerClass.Medic,
            HeavyDashActiveKey or HeavyDashVisibleKey => player.ClassId == PlayerClass.Heavy,
            SpySuperjumpActiveKey or SpySuperjumpDisabledKey => player.ClassId == PlayerClass.Spy,
            CivvieUmbrellaActiveKey or CivvieUmbrellaDisabledKey or CivviePogoActiveKey => player.ClassId == PlayerClass.Quote,
            BuffBannerDeployingOrActiveKey => player.ClassId == PlayerClass.Soldier,
            _ => false,
        };
    }

    private static GameplayReplicatedStateEntry Whole(string key, int value)
    {
        return new GameplayReplicatedStateEntry(
            GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
            key,
            GameplayReplicatedStateValueKind.Whole,
            IntValue: value);
    }

    private static GameplayReplicatedStateEntry Scalar(string key, float value)
    {
        return new GameplayReplicatedStateEntry(
            GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
            key,
            GameplayReplicatedStateValueKind.Scalar,
            FloatValue: value);
    }

    private static GameplayReplicatedStateEntry Toggle(string key, bool value)
    {
        return new GameplayReplicatedStateEntry(
            GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
            key,
            GameplayReplicatedStateValueKind.Toggle,
            BoolValue: value);
    }
}
