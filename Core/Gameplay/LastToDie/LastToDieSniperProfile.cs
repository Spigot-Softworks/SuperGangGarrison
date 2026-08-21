namespace OpenGarrison.Core.LastToDie;

/// <summary>
/// Static, per-player Sniper weapon and movement rules for Last to Die.
/// Charge remains expressed in source ticks, with the effective maxima carried
/// by the profile so authority, prediction, and presentation use one scale.
/// </summary>
public sealed record LastToDieSniperProfile(
    bool OverchargedEnabled = false,
    bool GreasedBoltEnabled = false,
    bool LightMarksmanEnabled = false,
    bool ExtremeConditioningEnabled = false,
    bool FiftyCalEnabled = false,
    bool FmjEnabled = false,
    bool GuardianEnabled = false,
    bool MechanicaEnabled = false,
    bool SpottedEnabled = false,
    bool ConquistadorEnabled = false,
    bool TranqDartsEnabled = false,
    bool PoisonTipEnabled = false,
    bool GhostEnabled = false,
    bool OverkillerEnabled = false,
    bool DecapitatorEnabled = false,
    bool MenageATroisEnabled = false,
    bool ExplosiveTipEnabled = false)
{
    // Protocol64 reuses one class-specific compact weapon-state field. Sniper
    // decoding is unambiguous because validation selects the mask by class.
    private const int OverchargedBit = 1 << 4;
    private const int GreasedBoltBit = 1 << 5;
    private const int LightMarksmanBit = 1 << 6;
    private const int ExtremeConditioningBit = 1 << 7;
    private const int FiftyCalBit = 1 << 8;
    private const int FmjBit = 1 << 9;
    private const int GuardianBit = 1 << 10;
    private const int MechanicaBit = 1 << 11;
    private const int SpottedBit = 1 << 12;
    private const int ConquistadorBit = 1 << 13;
    private const int TranqDartsBit = 1 << 14;
    private const int PoisonTipBit = 1 << 15;
    private const int GhostExtensionBit = 1 << 0;
    private const int OverkillerExtensionBit = 1 << 1;
    // Bits 2-11 in the shared extension word are Ghost runtime state.
    private const int DecapitatorExtensionBit = 1 << 12;
    private const int MenageATroisExtensionBit = 1 << 13;
    private const int ExplosiveTipExtensionBit = 1 << 14;

    public const int OverchargedRifleFullChargeTicks = 45;
    public const int OverchargedHuntsmanFullChargeTicks = 15;
    public const float GreasedBoltCycleSpeedBonus = 0.4f;
    public const float LightMarksmanCycleSpeedBonus = 1f;
    public const float FiftyCalCycleSpeedMultiplier = 0.4f;
    public const int LightMarksmanBaseDamage = 60;
    public const float ExtremeConditioningMovementSpeedMultiplier = 1.2f;
    public const int FiftyCalMaximumPlayerHits = 2;
    public const int GuardianDurationSeconds = 3;
    public const float GuardianHealingPerSecond = 12f;
    public const float GuardianEvasionChance = 0.3f;
    public const float SpottedDamageMultiplier = 2f;
    public const float ConquistadorDamageBonusPerStack = 0.02f;
    public const int ConquistadorMaximumStacks = 100;
    public const float TranqDartsDirectDamageMultiplier = 0.4f;
    public const int TranqDartsDurationSeconds = 4;
    public const float TranqDartsPoisonDamagePerSecond = 9f;
    public const float TranqDartsSlowPerStack = 0.1f;
    public const int TranqDartsMaximumSlowStacks = 5;
    public const float TranqDartsOutgoingDamageMultiplier = 0.6f;
    public const int PoisonTipDurationSeconds = 4;
    public const float PoisonTipMinimumDamagePerSecond = 9f;
    public const float PoisonTipMaximumDamagePerSecond = 20f;
    public const float GhostShotDamageMultiplier = 3f;
    public const int GhostCooldownSeconds = 10;
    public const float OverkillerChance = 0.3f;
    public const float DecapitatorHeadshotZoneSize = 2f;
    public const int MenageATroisArrowCount = 3;
    public const int MenageATroisArrowIntervalSourceTicks = 3;
    public const float ExplosiveTipBlastRadius = 96f;
    public const float ExplosiveTipCenterDamage = 80f;
    public const float ExplosiveTipEdgeDamage = 40f;
    public const float ExplosiveTipSelfDamageMultiplier = 0.5f;

    public static LastToDieSniperProfile Stock { get; } = new();

    public bool IsActive =>
        OverchargedEnabled
        || GreasedBoltEnabled
        || LightMarksmanEnabled
        || ExtremeConditioningEnabled
        || FiftyCalEnabled
        || FmjEnabled
        || GuardianEnabled
        || MechanicaEnabled
        || SpottedEnabled
        || ConquistadorEnabled
        || TranqDartsEnabled
        || PoisonTipEnabled
        || GhostEnabled
        || OverkillerEnabled
        || DecapitatorEnabled
        || MenageATroisEnabled
        || ExplosiveTipEnabled;

    public int RifleFullChargeTicks => OverchargedEnabled && !LightMarksmanEnabled
        ? OverchargedRifleFullChargeTicks
        : PlayerEntity.SniperChargeMaxTicks;

    public int HuntsmanFullChargeTicks => OverchargedEnabled
        ? OverchargedHuntsmanFullChargeTicks
        : PlayerEntity.SniperBowMaxChargeTicks;

    public float RifleCycleSpeedMultiplier =>
        (FiftyCalEnabled ? FiftyCalCycleSpeedMultiplier : 1f)
        * (1f
            + (GreasedBoltEnabled ? GreasedBoltCycleSpeedBonus : 0f)
            + (LightMarksmanEnabled ? LightMarksmanCycleSpeedBonus : 0f));

    public float MovementSpeedMultiplier => ExtremeConditioningEnabled
        ? ExtremeConditioningMovementSpeedMultiplier
        : 1f;

    public static float GetPoisonTipDamagePerSecond(float chargeFraction) =>
        PoisonTipMinimumDamagePerSecond
        + ((PoisonTipMaximumDamagePerSecond - PoisonTipMinimumDamagePerSecond)
            * Math.Clamp(chargeFraction, 0f, 1f));

    public int ScaleRifleCycleTicks(int ticks) => Math.Max(
        1,
        (int)MathF.Ceiling(Math.Max(1, ticks) / RifleCycleSpeedMultiplier));

    public int Encode()
    {
        var encoded = 0;
        if (OverchargedEnabled) encoded |= OverchargedBit;
        if (GreasedBoltEnabled) encoded |= GreasedBoltBit;
        if (LightMarksmanEnabled) encoded |= LightMarksmanBit;
        if (ExtremeConditioningEnabled) encoded |= ExtremeConditioningBit;
        if (FiftyCalEnabled) encoded |= FiftyCalBit;
        if (FmjEnabled) encoded |= FmjBit;
        if (GuardianEnabled) encoded |= GuardianBit;
        if (MechanicaEnabled) encoded |= MechanicaBit;
        if (SpottedEnabled) encoded |= SpottedBit;
        if (ConquistadorEnabled) encoded |= ConquistadorBit;
        if (TranqDartsEnabled) encoded |= TranqDartsBit;
        if (PoisonTipEnabled) encoded |= PoisonTipBit;
        return encoded;
    }

    public ushort EncodeExtensionProfile()
    {
        var encoded = 0;
        if (GhostEnabled) encoded |= GhostExtensionBit;
        if (OverkillerEnabled) encoded |= OverkillerExtensionBit;
        if (DecapitatorEnabled) encoded |= DecapitatorExtensionBit;
        if (MenageATroisEnabled) encoded |= MenageATroisExtensionBit;
        if (ExplosiveTipEnabled) encoded |= ExplosiveTipExtensionBit;
        return checked((ushort)encoded);
    }

    public static LastToDieSniperProfile Decode(int encoded) => new(
        OverchargedEnabled: (encoded & OverchargedBit) != 0,
        GreasedBoltEnabled: (encoded & GreasedBoltBit) != 0,
        LightMarksmanEnabled: (encoded & LightMarksmanBit) != 0,
        ExtremeConditioningEnabled: (encoded & ExtremeConditioningBit) != 0,
        FiftyCalEnabled: (encoded & FiftyCalBit) != 0,
        FmjEnabled: (encoded & FmjBit) != 0,
        GuardianEnabled: (encoded & GuardianBit) != 0,
        MechanicaEnabled: (encoded & MechanicaBit) != 0,
        SpottedEnabled: (encoded & SpottedBit) != 0,
        ConquistadorEnabled: (encoded & ConquistadorBit) != 0,
        TranqDartsEnabled: (encoded & TranqDartsBit) != 0,
        PoisonTipEnabled: (encoded & PoisonTipBit) != 0);

    public static LastToDieSniperProfile Decode(int encoded, int extensionEncoded)
    {
        var primary = Decode(encoded);
        return primary with
        {
            GhostEnabled = (extensionEncoded & GhostExtensionBit) != 0,
            OverkillerEnabled = (extensionEncoded & OverkillerExtensionBit) != 0,
            DecapitatorEnabled = (extensionEncoded & DecapitatorExtensionBit) != 0,
            MenageATroisEnabled = (extensionEncoded & MenageATroisExtensionBit) != 0,
            ExplosiveTipEnabled = (extensionEncoded & ExplosiveTipExtensionBit) != 0,
        };
    }

    public static LastToDieSniperProfile FromPerks(IReadOnlySet<LastToDiePerkId> owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        return new LastToDieSniperProfile(
            owned.Contains(LastToDiePerkIds.Sniper.Overcharged),
            owned.Contains(LastToDiePerkIds.Sniper.GreasedBolt),
            owned.Contains(LastToDiePerkIds.Sniper.LightMarksman),
            owned.Contains(LastToDiePerkIds.Sniper.ExtremeConditioning),
            owned.Contains(LastToDiePerkIds.Sniper.FiftyCal),
            owned.Contains(LastToDiePerkIds.Sniper.Fmj),
            owned.Contains(LastToDiePerkIds.Sniper.Guardian),
            owned.Contains(LastToDiePerkIds.Sniper.Mechanica),
            owned.Contains(LastToDiePerkIds.Sniper.Spotted),
            owned.Contains(LastToDiePerkIds.Sniper.Conquistador),
            owned.Contains(LastToDiePerkIds.Sniper.TranqDarts),
            owned.Contains(LastToDiePerkIds.Sniper.PoisonTip),
            owned.Contains(LastToDiePerkIds.Sniper.Ghost),
            owned.Contains(LastToDiePerkIds.Sniper.Overkiller),
            owned.Contains(LastToDiePerkIds.Sniper.Decapitator),
            owned.Contains(LastToDiePerkIds.Sniper.MenageATrois),
            owned.Contains(LastToDiePerkIds.Sniper.ExplosiveTip));
    }
}
