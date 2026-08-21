namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const string LastToDieSniperVolleyMetadataReplicatedStateKey = "sniper_volley";
    public const string LastToDieSniperVolleyVelocityXReplicatedStateKey = "sniper_volley_vx";
    public const string LastToDieSniperVolleyVelocityYReplicatedStateKey = "sniper_volley_vy";
    public const string LastToDieSniperVolleyDamageReplicatedStateKey = "sniper_volley_damage";
    public const string LastToDieSniperVolleyFakeSpeedReplicatedStateKey = "sniper_volley_speed";
    public const string LastToDieSniperVolleyPayloadFlagsReplicatedStateKey = "sniper_volley_flags";
    public const string LastToDieSniperVolleyPoisonReplicatedStateKey = "sniper_volley_poison";
    public const string LastToDieSniperVolleyGhostReplicatedStateKey = "sniper_volley_ghost";
    public const string LastToDieSniperVolleyCriticalMultiplierReplicatedStateKey = "sniper_volley_crit";

    private const int GuardianPayloadBit = 1 << 0;
    private const int PiercesPlayersPayloadBit = 1 << 1;
    private const int TranqDartsPayloadBit = 1 << 2;
    private const int DecapitatorPayloadBit = 1 << 3;
    private const int DecapitatorFullyChargedPayloadBit = 1 << 4;
    private const int ExplosiveTipPayloadBit = 1 << 5;
    private const int CriticalPayloadBit = 1 << 6;
    private const int KnownPayloadBits = (1 << 7) - 1;

    public LastToDieSniperVolleyState LastToDieSniperVolleyState { get; private set; }

    internal void BeginLastToDieSniperVolley(
        float velocityX,
        float velocityY,
        int damage,
        float fakeSpeedMultiplier,
        in LastToDieSniperArrowPayload payload)
    {
        LastToDieSniperVolleyState = new LastToDieSniperVolleyState(
            QueuedArrowCount: checked((byte)(global::OpenGarrison.Core.LastToDie.LastToDieSniperProfile.MenageATroisArrowCount - 1)),
            DueArrowCount: 0,
            SourceTicksUntilNextArrow: checked((byte)global::OpenGarrison.Core.LastToDie.LastToDieSniperProfile.MenageATroisArrowIntervalSourceTicks),
            velocityX,
            velocityY,
            Math.Max(0, damage),
            MathF.Max(0.0001f, fakeSpeedMultiplier),
            payload);
        WriteLastToDieSniperVolleyReplicatedState();
    }

    internal bool TryTakeDueLastToDieSniperVolleyArrow(out LastToDieSniperVolleyState state)
    {
        state = LastToDieSniperVolleyState;
        if (!state.IsActive || state.DueArrowCount == 0)
        {
            return false;
        }

        LastToDieSniperVolleyState = state with
        {
            DueArrowCount = checked((byte)(state.DueArrowCount - 1)),
        };
        WriteLastToDieSniperVolleyReplicatedState();
        return true;
    }

    internal void AdvanceLastToDieSniperVolleyState()
    {
        var state = LastToDieSniperVolleyState;
        if (!state.IsActive)
        {
            return;
        }

        if (!IsAlive || ClassId != PlayerClass.Sniper || !IsSniperBowEquipped)
        {
            CancelLastToDieSniperVolley();
            return;
        }

        if (state.QueuedArrowCount == 0)
        {
            return;
        }

        var ticks = state.SourceTicksUntilNextArrow > 0
            ? state.SourceTicksUntilNextArrow - 1
            : 0;
        if (ticks > 0)
        {
            LastToDieSniperVolleyState = state with
            {
                SourceTicksUntilNextArrow = checked((byte)ticks),
            };
            WriteLastToDieSniperVolleyReplicatedState();
            return;
        }

        var queued = checked((byte)(state.QueuedArrowCount - 1));
        LastToDieSniperVolleyState = state with
        {
            QueuedArrowCount = queued,
            DueArrowCount = checked((byte)(state.DueArrowCount + 1)),
            SourceTicksUntilNextArrow = queued > 0
                ? checked((byte)global::OpenGarrison.Core.LastToDie.LastToDieSniperProfile.MenageATroisArrowIntervalSourceTicks)
                : (byte)0,
        };
        WriteLastToDieSniperVolleyReplicatedState();
    }

    internal void CancelLastToDieSniperVolley()
    {
        if (!LastToDieSniperVolleyState.IsActive)
        {
            return;
        }

        LastToDieSniperVolleyState = default;
        WriteLastToDieSniperVolleyReplicatedState();
    }

    internal void HydrateProtocol64LastToDieSniperVolleyState(LastToDieSniperVolleyState state)
    {
        LastToDieSniperVolleyState = NormalizeLastToDieSniperVolleyState(state);
        WriteLastToDieSniperVolleyReplicatedState();
    }

    private void RefreshLastToDieSniperVolleyFromReplicatedStateEntries()
    {
        if (ClassId != PlayerClass.Sniper
            || !TryGetReplicatedStateInt(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperVolleyMetadataReplicatedStateKey,
                out var metadata)
            || metadata <= 0)
        {
            LastToDieSniperVolleyState = default;
            return;
        }

        _ = TryGetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyVelocityXReplicatedStateKey, out var velocityX);
        _ = TryGetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyVelocityYReplicatedStateKey, out var velocityY);
        _ = TryGetReplicatedStateInt(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyDamageReplicatedStateKey, out var damage);
        _ = TryGetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyFakeSpeedReplicatedStateKey, out var fakeSpeedMultiplier);
        _ = TryGetReplicatedStateInt(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyPayloadFlagsReplicatedStateKey, out var flags);
        _ = TryGetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyPoisonReplicatedStateKey, out var poisonDamagePerSecond);
        _ = TryGetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyGhostReplicatedStateKey, out var ghostDamageMultiplier);
        _ = TryGetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyCriticalMultiplierReplicatedStateKey, out var criticalDamageMultiplier);

        LastToDieSniperVolleyState = NormalizeLastToDieSniperVolleyState(new LastToDieSniperVolleyState(
            checked((byte)(metadata & 0x03)),
            checked((byte)((metadata >> 2) & 0x03)),
            checked((byte)((metadata >> 4) & 0x0f)),
            velocityX,
            velocityY,
            damage,
            fakeSpeedMultiplier,
            DecodeLastToDieSniperArrowPayload(
                flags,
                poisonDamagePerSecond,
                ghostDamageMultiplier,
                criticalDamageMultiplier)));
    }

    private void WriteLastToDieSniperVolleyReplicatedState()
    {
        var state = LastToDieSniperVolleyState;
        if (!state.IsActive)
        {
            ClearLastToDieSniperVolleyReplicatedState();
            return;
        }

        var metadata = state.QueuedArrowCount
            | (state.DueArrowCount << 2)
            | (state.SourceTicksUntilNextArrow << 4);
        SetReplicatedStateInt(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyMetadataReplicatedStateKey, metadata);
        SetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyVelocityXReplicatedStateKey, state.VelocityX);
        SetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyVelocityYReplicatedStateKey, state.VelocityY);
        SetReplicatedStateInt(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyDamageReplicatedStateKey, state.Damage);
        SetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyFakeSpeedReplicatedStateKey, state.FakeSpeedMultiplier);
        SetReplicatedStateInt(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyPayloadFlagsReplicatedStateKey, EncodeLastToDieSniperArrowPayload(state.Payload));
        SetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyPoisonReplicatedStateKey, state.Payload.PoisonDamagePerSecond);
        SetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyGhostReplicatedStateKey, state.Payload.GhostDamageMultiplier);
        SetReplicatedStateFloat(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyCriticalMultiplierReplicatedStateKey, state.Payload.CriticalDamageMultiplier);
    }

    private void ClearLastToDieSniperVolleyReplicatedState()
    {
        ClearReplicatedState(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyMetadataReplicatedStateKey);
        ClearReplicatedState(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyVelocityXReplicatedStateKey);
        ClearReplicatedState(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyVelocityYReplicatedStateKey);
        ClearReplicatedState(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyDamageReplicatedStateKey);
        ClearReplicatedState(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyFakeSpeedReplicatedStateKey);
        ClearReplicatedState(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyPayloadFlagsReplicatedStateKey);
        ClearReplicatedState(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyPoisonReplicatedStateKey);
        ClearReplicatedState(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyGhostReplicatedStateKey);
        ClearReplicatedState(LastToDieWeaponReplicatedStateOwnerId, LastToDieSniperVolleyCriticalMultiplierReplicatedStateKey);
    }

    public static int EncodeLastToDieSniperArrowPayload(in LastToDieSniperArrowPayload payload)
    {
        var encoded = 0;
        if (payload.AppliesGuardian) encoded |= GuardianPayloadBit;
        if (payload.PiercesPlayers) encoded |= PiercesPlayersPayloadBit;
        if (payload.AppliesTranqDarts) encoded |= TranqDartsPayloadBit;
        if (payload.AppliesDecapitator) encoded |= DecapitatorPayloadBit;
        if (payload.IsDecapitatorFullyCharged) encoded |= DecapitatorFullyChargedPayloadBit;
        if (payload.AppliesExplosiveTip) encoded |= ExplosiveTipPayloadBit;
        if (payload.IsCritical) encoded |= CriticalPayloadBit;
        return encoded;
    }

    public static LastToDieSniperArrowPayload DecodeLastToDieSniperArrowPayload(
        int encoded,
        float poisonDamagePerSecond,
        float ghostDamageMultiplier,
        float criticalDamageMultiplier = 1f)
    {
        encoded &= KnownPayloadBits;
        return new LastToDieSniperArrowPayload(
            (encoded & GuardianPayloadBit) != 0,
            (encoded & PiercesPlayersPayloadBit) != 0,
            (encoded & TranqDartsPayloadBit) != 0,
            MathF.Max(0f, poisonDamagePerSecond),
            MathF.Max(1f, ghostDamageMultiplier),
            (encoded & DecapitatorPayloadBit) != 0,
            (encoded & DecapitatorFullyChargedPayloadBit) != 0,
            (encoded & ExplosiveTipPayloadBit) != 0,
            (encoded & CriticalPayloadBit) != 0,
            (encoded & CriticalPayloadBit) != 0
                ? ExperimentalGameplaySettings.NormalizeCriticalDamageMultiplier(
                    criticalDamageMultiplier)
                : 1f);
    }

    private static LastToDieSniperVolleyState NormalizeLastToDieSniperVolleyState(LastToDieSniperVolleyState state)
    {
        var queued = Math.Clamp(state.QueuedArrowCount, (byte)0, (byte)2);
        var due = Math.Clamp(state.DueArrowCount, (byte)0, (byte)2);
        if (queued + due == 0)
        {
            return default;
        }

        return state with
        {
            QueuedArrowCount = queued,
            DueArrowCount = due,
            SourceTicksUntilNextArrow = queued > 0
                ? Math.Clamp(state.SourceTicksUntilNextArrow, (byte)1, (byte)3)
                : (byte)0,
            Damage = Math.Max(0, state.Damage),
            FakeSpeedMultiplier = MathF.Max(0.0001f, state.FakeSpeedMultiplier),
            Payload = state.Payload with
            {
                PoisonDamagePerSecond = MathF.Max(0f, state.Payload.PoisonDamagePerSecond),
                GhostDamageMultiplier = MathF.Max(1f, state.Payload.GhostDamageMultiplier),
                CriticalDamageMultiplier = state.Payload.IsCritical
                    ? ExperimentalGameplaySettings.NormalizeCriticalDamageMultiplier(
                        state.Payload.CriticalDamageMultiplier)
                    : 1f,
            },
        };
    }
}
