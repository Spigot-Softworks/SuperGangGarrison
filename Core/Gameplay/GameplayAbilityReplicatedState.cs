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
    public const string SpyCloakAlphaKey = "spy_cloak_alpha";
    public const string SpySuperjumpCooldownTicksKey = "spy_superjump_cooldown_ticks";
    public const string SpySuperjumpActiveKey = "spy_superjump_active";
    public const string SpySuperjumpDisabledKey = "spy_superjump_disabled";
    public const string HeavyDashCooldownTicksKey = "heavy_dash_cooldown_ticks";
    public const string HeavyDashActiveKey = "heavy_dash_active";
    public const string HeavyDashVisibleKey = "heavy_dash_visible";
    public const string HeavyDashTrailAlphaKey = "heavy_dash_trail_alpha";

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
            PlayerClass.Sniper =>
            [
                Whole(SniperChargeTicksKey, player.SniperChargeTicks),
            ],
            PlayerClass.Spy =>
            [
                Scalar(SpyCloakAlphaKey, player.SpyCloakAlpha),
                Whole(SpySuperjumpCooldownTicksKey, player.SpySuperjumpCooldownTicksRemaining),
                Toggle(SpySuperjumpActiveKey, player.SpySuperjumpChargeTicks > 0 || player.IsSpySuperjumping),
                Toggle(SpySuperjumpDisabledKey, player.IsCarryingIntel),
            ],
            _ => Array.Empty<GameplayReplicatedStateEntry>(),
        };
    }

    public static bool TryGetInt(PlayerEntity player, string key, out int value)
    {
        if (key == HeavyDashCooldownTicksKey
            && player.ClassId == PlayerClass.Heavy
            && player.TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                HeavyDashCooldownTicksKey,
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
            SpySuperjumpCooldownTicksKey when player.ClassId == PlayerClass.Spy => player.SpySuperjumpCooldownTicksRemaining,
            _ => default,
        };

        return key switch
        {
            PyroAirblastCooldownTicksKey => player.ClassId == PlayerClass.Pyro,
            MedicNeedlegunCooldownTicksKey => player.ClassId == PlayerClass.Medic,
            HeavyEatTicksRemainingKey or HeavyEatCooldownTicksKey or HeavyDashCooldownTicksKey => player.ClassId == PlayerClass.Heavy,
            SniperChargeTicksKey => player.ClassId == PlayerClass.Sniper,
            SpySuperjumpCooldownTicksKey => player.ClassId == PlayerClass.Spy,
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
        value = key switch
        {
            MedicUberReadyKey when player.ClassId == PlayerClass.Medic => player.IsMedicUberReady,
            HeavyDashActiveKey when player.ClassId == PlayerClass.Heavy => player.IsExperimentalGhostDashing,
            HeavyDashVisibleKey when player.ClassId == PlayerClass.Heavy => player.IsExperimentalGhostDashVisible,
            SpySuperjumpActiveKey when player.ClassId == PlayerClass.Spy => player.SpySuperjumpChargeTicks > 0 || player.IsSpySuperjumping,
            SpySuperjumpDisabledKey when player.ClassId == PlayerClass.Spy => player.IsCarryingIntel,
            _ => default,
        };

        return key switch
        {
            MedicUberReadyKey => player.ClassId == PlayerClass.Medic,
            HeavyDashActiveKey or HeavyDashVisibleKey => player.ClassId == PlayerClass.Heavy,
            SpySuperjumpActiveKey or SpySuperjumpDisabledKey => player.ClassId == PlayerClass.Spy,
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
