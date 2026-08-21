namespace OpenGarrison.Core.LastToDie;

/// <summary>
/// Immutable, per-trigger Spy revolver behavior for Last to Die. The profile is
/// copied onto each projectile so later build changes cannot mutate a shot in flight.
/// </summary>
public sealed record LastToDieSpyRevolverProfile(
    int BlunderbussRank = 0,
    bool AgentEnabled = false,
    bool RubberBulletsEnabled = false,
    bool DeadlyEnabled = false,
    bool ExecutionerEnabled = false,
    bool RicochetEnabled = false,
    bool LuckyStrikeEnabled = false)
{
    public const int AgentClipSize = 9;
    public const int BlunderbussRankOneClipSize = 1;
    public const int BlunderbussRankTwoClipSize = 2;
    public const int BlunderbussBasePelletCount = 13;
    public const int BlunderbussRankThreePelletCount = 26;
    public const float BlunderbussBaseSpreadDegrees = 24f;
    public const float BlunderbussRankThreeSpreadMultiplier = 1.4f;
    public const float BlunderbussBasePelletDamage = 8f;
    public const float BlunderbussRankTwoDamageMultiplier = 1.4f;
    public const float BlunderbussBaseReloadSpeedMultiplier = 1.30f;
    public const float BlunderbussRankThreeReloadSpeedMultiplier =
        BlunderbussBaseReloadSpeedMultiplier * 1.50f;
    public const float BlunderbussBaseBleedDamagePerSecond = 5f;
    public const float BlunderbussRankTwoBleedDamagePerSecond = 8f;
    public const int BlunderbussBleedDurationSourceTicks = 120;
    public const float BlunderbussBaseKnockbackScale = 1f;
    public const float BlunderbussRankTwoKnockbackScale = 1.4f;
    public const float DeadlyCriticalChance = 0.35f;
    public const float ExecutionerHealthThreshold = 0.40f;
    public const float RubberBulletsMovementMultiplier = 0.60f;
    public const int RubberBulletsSlowDurationSourceTicks = 30;
    public const float RubberBulletsUpwardImpulsePerSecond = -30f;
    public const int LuckyStrikeTriggerInterval = 3;
    public const int LuckyStrikeStunDurationSourceTicks = 30;
    public const int LuckyStrikeProgressBitShift = 8;
    public const int LuckyStrikeProgressBitMask = 0b11;
    public const int RicochetMaximumBounces = 3;
    public const float RicochetTargetRadius = 160f;

    public static LastToDieSpyRevolverProfile Stock { get; } = new();

    public bool IsActive =>
        BlunderbussRank > 0
        || AgentEnabled
        || RubberBulletsEnabled
        || DeadlyEnabled
        || ExecutionerEnabled
        || RicochetEnabled
        || LuckyStrikeEnabled;

    public int PelletCount => BlunderbussRank switch
    {
        >= 3 => BlunderbussRankThreePelletCount,
        >= 1 => BlunderbussBasePelletCount,
        _ => 1,
    };

    public float BleedDamagePerSecond => BlunderbussRank switch
    {
        >= 2 => BlunderbussRankTwoBleedDamagePerSecond,
        1 => BlunderbussBaseBleedDamagePerSecond,
        _ => 0f,
    };

    public float KnockbackScale => BlunderbussRank switch
    {
        >= 2 => BlunderbussRankTwoKnockbackScale,
        1 => BlunderbussBaseKnockbackScale,
        _ => 0f,
    };

    public PrimaryWeaponDefinition ApplyTo(PrimaryWeaponDefinition baseWeapon)
    {
        ArgumentNullException.ThrowIfNull(baseWeapon);
        if (BlunderbussRank <= 0)
        {
            return AgentEnabled
                ? baseWeapon with { MaxAmmo = AgentClipSize }
                : baseWeapon;
        }

        var reloadSpeedMultiplier = BlunderbussRank >= 3
            ? BlunderbussRankThreeReloadSpeedMultiplier
            : BlunderbussBaseReloadSpeedMultiplier;
        return baseWeapon with
        {
            MaxAmmo = BlunderbussRank >= 2
                ? BlunderbussRankTwoClipSize
                : BlunderbussRankOneClipSize,
            ProjectilesPerShot = PelletCount,
            AmmoReloadTicks = Math.Max(
                1,
                (int)MathF.Ceiling(baseWeapon.AmmoReloadTicks / reloadSpeedMultiplier)),
            SpreadDegrees = BlunderbussBaseSpreadDegrees
                * (BlunderbussRank >= 3 ? BlunderbussRankThreeSpreadMultiplier : 1f),
            DirectHitDamage = BlunderbussBasePelletDamage
                * (BlunderbussRank >= 2 ? BlunderbussRankTwoDamageMultiplier : 1f),
            PlayerKnockbackScale = KnockbackScale,
        };
    }

    public int Encode()
    {
        var encoded = Math.Clamp(BlunderbussRank, 0, 3);
        if (AgentEnabled) encoded |= 1 << 2;
        if (RubberBulletsEnabled) encoded |= 1 << 3;
        if (DeadlyEnabled) encoded |= 1 << 4;
        if (ExecutionerEnabled) encoded |= 1 << 5;
        if (RicochetEnabled) encoded |= 1 << 6;
        if (LuckyStrikeEnabled) encoded |= 1 << 7;
        return encoded;
    }

    public int EncodeReplicatedState(int luckyStrikeTriggerProgress)
    {
        var progress = LuckyStrikeEnabled
            ? Math.Clamp(luckyStrikeTriggerProgress, 0, LuckyStrikeTriggerInterval - 1)
            : 0;
        return Encode() | (progress << LuckyStrikeProgressBitShift);
    }

    public static int DecodeLuckyStrikeTriggerProgress(int encoded)
    {
        var progress = (encoded >> LuckyStrikeProgressBitShift) & LuckyStrikeProgressBitMask;
        return progress < LuckyStrikeTriggerInterval ? progress : 0;
    }

    public static LastToDieSpyRevolverProfile Decode(int encoded)
    {
        var rank = Math.Clamp(encoded & 0b11, 0, 3);
        return Normalize(
            rank,
            agentEnabled: (encoded & (1 << 2)) != 0,
            rubberBulletsEnabled: (encoded & (1 << 3)) != 0,
            deadlyEnabled: (encoded & (1 << 4)) != 0,
            executionerEnabled: (encoded & (1 << 5)) != 0,
            ricochetEnabled: (encoded & (1 << 6)) != 0,
            luckyStrikeEnabled: (encoded & (1 << 7)) != 0);
    }

    public static LastToDieSpyRevolverProfile FromPerks(IReadOnlySet<LastToDiePerkId> owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        var rank = 0;
        if (owned.Contains(LastToDiePerkIds.Spy.Blunderbuss1))
        {
            rank = 1;
            if (owned.Contains(LastToDiePerkIds.Spy.Blunderbuss2))
            {
                rank = 2;
                if (owned.Contains(LastToDiePerkIds.Spy.Blunderbuss3))
                {
                    rank = 3;
                }
            }
        }

        return Normalize(
            rank,
            owned.Contains(LastToDiePerkIds.Spy.Agent),
            owned.Contains(LastToDiePerkIds.Spy.RubberBullets),
            owned.Contains(LastToDiePerkIds.Spy.Deadly),
            owned.Contains(LastToDiePerkIds.Spy.Executioner),
            owned.Contains(LastToDiePerkIds.Spy.Ricochet),
            owned.Contains(LastToDiePerkIds.Spy.LuckyStrike));
    }

    private static LastToDieSpyRevolverProfile Normalize(
        int rank,
        bool agentEnabled,
        bool rubberBulletsEnabled,
        bool deadlyEnabled,
        bool executionerEnabled,
        bool ricochetEnabled,
        bool luckyStrikeEnabled)
    {
        rank = Math.Clamp(rank, 0, 3);
        if (rank > 0)
        {
            agentEnabled = false;
            rubberBulletsEnabled = false;
        }

        return new LastToDieSpyRevolverProfile(
            rank,
            agentEnabled,
            rubberBulletsEnabled,
            deadlyEnabled,
            executionerEnabled,
            ricochetEnabled,
            luckyStrikeEnabled);
    }
}
