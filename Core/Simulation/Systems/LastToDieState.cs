using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

internal readonly record struct LastToDieStatusRuntimeKey(
    int TargetPlayerId,
    LastToDieStatusEffectId EffectId,
    LastToDieStatusEffectKind Kind,
    int SourcePlayerId);

internal sealed class LastToDieStatusRuntime(
    LastToDieStatusEffectSpec spec,
    int? sourcePlayerId,
    int? assistingMedicPlayerId)
{
    public LastToDieStatusEffectSpec Spec { get; set; } = spec;

    public int? SourcePlayerId { get; } = sourcePlayerId;

    public int? AssistingMedicPlayerId { get; set; } = assistingMedicPlayerId;

    public int RemainingTicks { get; set; } = spec.DurationTicks;

    public double DamageAccumulator { get; set; }
}

internal sealed class LastToDiePlayerPerkRuntime(LastToDieDerivedModifiers modifiers)
{
    public LastToDieDerivedModifiers Modifiers { get; set; } = modifiers;

    public int BaseMaximumHealth { get; set; }

    public bool SecondChanceConsumed { get; set; }

    /// <summary>
    /// The original offline Last to Die perks are represented by the
    /// experimental gameplay settings model. This copy is per network
    /// slot; <see cref="SimulationWorld.ExperimentalGameplaySettings"/>
    /// remains the world/practice fallback.
    /// </summary>
    public ExperimentalGameplaySettings LegacySettings { get; set; } = new();

    public int DamageHealingRemainder { get; set; }

    public int ScopedDamageHealingRemainder { get; set; }

    public float ScopedHealingAccumulator { get; set; }

    public float CloakedHealingAccumulator { get; set; }

    public int MedicHomeostasisHealingRemainder { get; set; }

    public int MedicSpikedVestReflectionRemainder { get; set; }

    public int UniversalReflectionRemainder { get; set; }

    public float UniversalHealingAccumulator { get; set; }

    public float InfiniteSlayWorksDamageAccumulator { get; set; }

    public int? MedicSupportRelayActiveLinkTargetPlayerId { get; set; }

    public Dictionary<int, long> MedicSupportRelayCooldownUntilFrameByTargetPlayerId { get; } = [];

    public bool WasSpyCloaked { get; set; }

    public int ShroudGraceTicksRemaining { get; set; }

    public LastToDieRandom? RevolverCriticalRandom { get; set; }

    public LastToDieRandom? EvasionRandom { get; set; }

    public LastToDieRandom? OverkillerRandom { get; set; }
}

/// <summary>Owns Last to Die per-slot perk runtimes, status effects, combat seed, and survivor stage state.</summary>
internal sealed class LastToDieState
{
    public Dictionary<byte, LastToDiePlayerPerkRuntime> PerkRuntimesBySlot { get; } = [];

    // Client prediction receives the authoritative survivor/perk snapshot but
    // must not create the server-owned combat runtime. Keep that profile
    // separate so inactive online slots never become LTD owners.
    public Dictionary<byte, ExperimentalGameplaySettings> LegacyGameplaySettingsBySlot { get; } = [];

    public ulong CombatSeed { get; set; }

    public bool CombatSeedConfigured { get; set; }

    public Dictionary<LastToDieStatusRuntimeKey, LastToDieStatusRuntime> StatusRuntimes { get; } = [];

    public HashSet<LastToDieStatusRuntimeKey> StatusKeysAtTickStart { get; } = [];

    public Dictionary<int, double> GuardianHealingRemaindersByTargetId { get; } = [];

    public Dictionary<int, int> MartyrProtectorPlayerIdByProtectedTargetId { get; } = [];

    public HashSet<byte> SpyAfterlifeDisconnectFailureSlots { get; } = [];

    public HashSet<int> DroneSentryIds { get; } = new();

    public int StageNumber { get; set; }
}
