namespace OpenGarrison.Core;

internal enum PlayerDamageApplicationKind : byte
{
    Instant = 1,
    Continuous = 2,
}

[Flags]
internal enum PlayerDamageTraits : uint
{
    None = 0,
    CanEvade = 1 << 0,
    CanApplyOnHitEffects = 1 << 1,
    CanReflect = 1 << 2,
    Periodic = 1 << 3,
    Bleed = 1 << 4,
    Poison = 1 << 5,
    Reflected = 1 << 6,
    Critical = 1 << 7,
    Bullet = 1 << 8,
    Explosive = 1 << 9,
    Fire = 1 << 10,
    Melee = 1 << 11,
    LastToDieIncomingModifierPreApplied = 1 << 12,
    ExecuteAfterDefenses = 1 << 13,
    EstablishLastToDieSpotted = 1 << 14,
    BenefitFromLastToDieSpotted = 1 << 15,
    DirectProjectile = 1 << 16,
    LastToDieOverkillerEligible = 1 << 17,
    LastToDieOverkillerFollowUp = 1 << 18,
    MedicKritzM2 = 1 << 19,
}

internal readonly record struct PlayerDamageUmbrellaOptions(
    bool AllowBlock,
    float? ThreatSourceX = null,
    float? ThreatSourceY = null,
    int? DrainTicks = null,
    bool CriticalBoost = false,
    bool UseLiveAttackerCriticalBoost = true);

internal readonly record struct PlayerDamageRequest(
    PlayerDamageApplicationKind ApplicationKind,
    float Amount,
    PlayerEntity? Attacker,
    float SpyRevealAlpha,
    DamageEventFlags EventFlags,
    PlayerDamageTraits Traits,
    bool AllowOsmosisHealOwnedSentries,
    PlayerDamageUmbrellaOptions Umbrella,
    int SourceEntityId = 0,
    ulong AttackId = 0,
    bool? AttackerWasGrounded = null,
    bool? TargetWasGrounded = null,
    int AssistPlayerIdOverride = -1,
    int AttackerPlayerIdOverride = -1,
    bool GibOnFatal = false,
    string? FatalWeaponSpriteName = null);

internal enum PlayerDamageDisposition : byte
{
    Rejected = 0,
    UmbrellaBlocked = 1,
    FullyShielded = 2,
    ConvertedToHealing = 3,
    GhostEvaded = 4,
    Evaded = 5,
    FatalPrevented = 6,
    DamageCancelled = 7,
    PracticeDummyRecorded = 8,
    DeathCancelled = 9,
    Accumulated = 10,
    Invulnerable = 11,
    Applied = 12,
}

internal readonly record struct PlayerDamageResolution(
    PlayerDamageDisposition Disposition,
    float RequestedDamage,
    float DamageAfterOutgoingModifiers,
    float DamageAfterIncomingModifiers,
    float DamageAfterServerScaling,
    float DamageAfterShield,
    int HealthBefore,
    int HealthAfter,
    int AppliedHealthDamage,
    bool WasFatal,
    DamageEventFlags EventFlags,
    PlayerDamageTraits Traits)
{
    public bool WasEvaded => Disposition is
        PlayerDamageDisposition.GhostEvaded or PlayerDamageDisposition.Evaded;

    public bool WasBlocked => Disposition is
        PlayerDamageDisposition.UmbrellaBlocked
        or PlayerDamageDisposition.FullyShielded
        or PlayerDamageDisposition.DamageCancelled
        or PlayerDamageDisposition.DeathCancelled
        or PlayerDamageDisposition.Invulnerable;

    public bool WasResisted =>
        DamageAfterIncomingModifiers + 0.0001f < DamageAfterOutgoingModifiers;

    public bool ShouldApplyOnHitEffects =>
        Disposition is PlayerDamageDisposition.Applied or PlayerDamageDisposition.FatalPrevented
        && AppliedHealthDamage > 0
        && Traits.HasFlag(PlayerDamageTraits.CanApplyOnHitEffects);
}
