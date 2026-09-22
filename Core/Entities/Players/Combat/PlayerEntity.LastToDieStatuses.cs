namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const string LastToDieStatusReplicatedStateOwnerId = "ltd.status";
    public const string LastToDieStatusMovementSpeedMultiplierReplicatedStateKey = "movescale";
    public const string LastToDieGuardianEvasionChanceReplicatedStateKey = "guardian_evasion";
    public const string LastToDieStatusOutgoingDamageMultiplierReplicatedStateKey = "outgoing_damage";
    public const string LastToDieMedicHailMaryTicksReplicatedStateKey = "hail_ticks";
    public const string LastToDieStatusFireSpeedMultiplierReplicatedStateKey = "fire_scale";
    public const string LastToDieStatusReloadSpeedMultiplierReplicatedStateKey = "reload_scale";
    public const string LastToDieSecondChanceInvulnerabilityTicksReplicatedStateKey = "second_chance_ticks";

    private float LastToDieStatusMovementSpeedMultiplierValue { get; set; } = 1f;
    private float LastToDieGuardianEvasionChanceValue { get; set; }
    private float LastToDieStatusOutgoingDamageMultiplierValue { get; set; } = 1f;
    private int LastToDieMedicHailMaryTicksRemainingValue { get; set; }
    private float LastToDieStatusFireSpeedMultiplierValue { get; set; } = 1f;
    private float LastToDieStatusReloadSpeedMultiplierValue { get; set; } = 1f;
    private int LastToDieSecondChanceInvulnerabilityTicksRemainingValue { get; set; }
    private bool LastToDieSecondChanceConsumedValue { get; set; }

    public float LastToDieStatusMovementSpeedMultiplier => LastToDieStatusMovementSpeedMultiplierValue;

    public float LastToDieGuardianEvasionChance => LastToDieGuardianEvasionChanceValue;

    public float LastToDieStatusOutgoingDamageMultiplier =>
        LastToDieStatusOutgoingDamageMultiplierValue;

    public int LastToDieMedicHailMaryTicksRemaining =>
        LastToDieMedicHailMaryTicksRemainingValue;

    internal float LastToDieStatusFireSpeedMultiplier => LastToDieStatusFireSpeedMultiplierValue;

    internal float LastToDieStatusReloadSpeedMultiplier => LastToDieStatusReloadSpeedMultiplierValue;

    internal bool IsLastToDieSecondChanceInvulnerable =>
        IsAlive && LastToDieSecondChanceInvulnerabilityTicksRemainingValue > 0;

    internal bool LastToDieSecondChanceConsumed => LastToDieSecondChanceConsumedValue;

    public bool IsLastToDieMedicHailMaryInvulnerable =>
        IsAlive && LastToDieMedicHailMaryTicksRemainingValue > 0;

    internal bool SetLastToDieStatusMovementSpeedMultiplier(float multiplier)
    {
        LastToDieStatusMovementSpeedMultiplierValue = Math.Clamp(multiplier, 0.05f, 1f);
        if (LastToDieStatusMovementSpeedMultiplierValue >= 0.9999f)
        {
            LastToDieStatusMovementSpeedMultiplierValue = 1f;
            ClearReplicatedState(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieStatusMovementSpeedMultiplierReplicatedStateKey);
            return true;
        }

        return SetReplicatedStateFloat(
            LastToDieStatusReplicatedStateOwnerId,
            LastToDieStatusMovementSpeedMultiplierReplicatedStateKey,
            LastToDieStatusMovementSpeedMultiplierValue);
    }

    internal void ClearLastToDieStatusRuntimeState()
    {
        SetLastToDieStatusMovementSpeedMultiplier(1f);
        SetLastToDieStatusOutgoingDamageMultiplier(1f);
        SetLastToDieStatusFireSpeedMultiplier(1f);
        SetLastToDieStatusReloadSpeedMultiplier(1f);
        SetLastToDieGuardianEvasionChance(0f);
        SetServerStunTicks(0);
        SetLastToDieMedicHailMaryTicks(0);
        RefreshLastToDieSecondChanceInvulnerability(0);
    }

    internal bool RefreshLastToDieMedicHailMaryInvulnerability(int ticks)
    {
        if (!IsAlive || ticks <= 0)
        {
            return false;
        }

        return SetLastToDieMedicHailMaryTicks(
            Math.Max(LastToDieMedicHailMaryTicksRemainingValue, ticks));
    }

    internal void AdvanceLastToDieMedicHailMaryState()
    {
        if (LastToDieMedicHailMaryTicksRemainingValue > 0)
        {
            SetLastToDieMedicHailMaryTicks(
                LastToDieMedicHailMaryTicksRemainingValue - 1);
        }
    }

    internal void HydrateProtocol64LastToDieMedicHailMaryTicks(int ticks)
    {
        LastToDieMedicHailMaryTicksRemainingValue = Math.Max(0, ticks);
    }

    internal void SetLastToDieSecondChanceConsumed(bool consumed)
    {
        LastToDieSecondChanceConsumedValue = consumed;
    }

    internal bool ActivateLastToDieSecondChance(int ticks)
    {
        if (LastToDieSecondChanceConsumedValue || ticks <= 0)
        {
            return false;
        }

        LastToDieSecondChanceConsumedValue = true;
        RefreshLastToDieSecondChanceInvulnerability(ticks);
        return true;
    }

    internal void AdvanceLastToDieSecondChanceState()
    {
        if (LastToDieSecondChanceInvulnerabilityTicksRemainingValue > 0)
        {
            RefreshLastToDieSecondChanceInvulnerability(
                LastToDieSecondChanceInvulnerabilityTicksRemainingValue - 1);
        }
    }

    private void RefreshLastToDieSecondChanceInvulnerability(int ticks)
    {
        LastToDieSecondChanceInvulnerabilityTicksRemainingValue = Math.Max(0, ticks);
        if (LastToDieSecondChanceInvulnerabilityTicksRemainingValue == 0)
        {
            ClearReplicatedState(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieSecondChanceInvulnerabilityTicksReplicatedStateKey);
            return;
        }

        SetReplicatedStateInt(
            LastToDieStatusReplicatedStateOwnerId,
            LastToDieSecondChanceInvulnerabilityTicksReplicatedStateKey,
            LastToDieSecondChanceInvulnerabilityTicksRemainingValue);
    }

    private bool SetLastToDieMedicHailMaryTicks(int ticks)
    {
        LastToDieMedicHailMaryTicksRemainingValue = Math.Max(0, ticks);
        if (LastToDieMedicHailMaryTicksRemainingValue == 0)
        {
            ClearReplicatedState(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieMedicHailMaryTicksReplicatedStateKey);
            return true;
        }

        return SetReplicatedStateInt(
            LastToDieStatusReplicatedStateOwnerId,
            LastToDieMedicHailMaryTicksReplicatedStateKey,
            LastToDieMedicHailMaryTicksRemainingValue);
    }

    internal bool SetLastToDieStatusOutgoingDamageMultiplier(float multiplier)
    {
        LastToDieStatusOutgoingDamageMultiplierValue = Math.Clamp(multiplier, 0.05f, 1f);
        if (LastToDieStatusOutgoingDamageMultiplierValue >= 0.9999f)
        {
            LastToDieStatusOutgoingDamageMultiplierValue = 1f;
            ClearReplicatedState(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieStatusOutgoingDamageMultiplierReplicatedStateKey);
            return true;
        }

        return SetReplicatedStateFloat(
            LastToDieStatusReplicatedStateOwnerId,
            LastToDieStatusOutgoingDamageMultiplierReplicatedStateKey,
            LastToDieStatusOutgoingDamageMultiplierValue);
    }

    internal bool SetLastToDieStatusFireSpeedMultiplier(float multiplier)
    {
        LastToDieStatusFireSpeedMultiplierValue = Math.Clamp(multiplier, 0.05f, 4f);
        if (LastToDieStatusFireSpeedMultiplierValue >= 0.9999f)
        {
            LastToDieStatusFireSpeedMultiplierValue = 1f;
            ClearReplicatedState(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieStatusFireSpeedMultiplierReplicatedStateKey);
            return true;
        }

        return SetReplicatedStateFloat(
            LastToDieStatusReplicatedStateOwnerId,
            LastToDieStatusFireSpeedMultiplierReplicatedStateKey,
            LastToDieStatusFireSpeedMultiplierValue);
    }

    internal bool SetLastToDieStatusReloadSpeedMultiplier(float multiplier)
    {
        LastToDieStatusReloadSpeedMultiplierValue = Math.Clamp(multiplier, 0.05f, 4f);
        if (LastToDieStatusReloadSpeedMultiplierValue >= 0.9999f)
        {
            LastToDieStatusReloadSpeedMultiplierValue = 1f;
            ClearReplicatedState(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieStatusReloadSpeedMultiplierReplicatedStateKey);
            return true;
        }

        return SetReplicatedStateFloat(
            LastToDieStatusReplicatedStateOwnerId,
            LastToDieStatusReloadSpeedMultiplierReplicatedStateKey,
            LastToDieStatusReloadSpeedMultiplierValue);
    }

    internal bool SetLastToDieGuardianEvasionChance(float chance)
    {
        LastToDieGuardianEvasionChanceValue = Math.Clamp(chance, 0f, 0.95f);
        if (LastToDieGuardianEvasionChanceValue <= 0.0001f)
        {
            LastToDieGuardianEvasionChanceValue = 0f;
            ClearReplicatedState(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieGuardianEvasionChanceReplicatedStateKey);
            return true;
        }

        return SetReplicatedStateFloat(
            LastToDieStatusReplicatedStateOwnerId,
            LastToDieGuardianEvasionChanceReplicatedStateKey,
            LastToDieGuardianEvasionChanceValue);
    }

    private void RefreshLastToDieStatusRuntimeFromReplicatedStateEntries()
    {
        LastToDieStatusMovementSpeedMultiplierValue =
            TryGetReplicatedStateFloat(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieStatusMovementSpeedMultiplierReplicatedStateKey,
                out var movementSpeedMultiplier)
                ? Math.Clamp(movementSpeedMultiplier, 0.05f, 1f)
                : 1f;
        LastToDieGuardianEvasionChanceValue =
            TryGetReplicatedStateFloat(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieGuardianEvasionChanceReplicatedStateKey,
                out var guardianEvasionChance)
                ? Math.Clamp(guardianEvasionChance, 0f, 0.95f)
                : 0f;
        LastToDieStatusOutgoingDamageMultiplierValue =
            TryGetReplicatedStateFloat(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieStatusOutgoingDamageMultiplierReplicatedStateKey,
                out var outgoingDamageMultiplier)
                ? Math.Clamp(outgoingDamageMultiplier, 0.05f, 1f)
                : 1f;
        LastToDieMedicHailMaryTicksRemainingValue =
            TryGetReplicatedStateInt(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieMedicHailMaryTicksReplicatedStateKey,
                out var hailMaryTicks)
                ? Math.Max(0, hailMaryTicks)
                : 0;
        LastToDieStatusFireSpeedMultiplierValue =
            TryGetReplicatedStateFloat(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieStatusFireSpeedMultiplierReplicatedStateKey,
                out var fireSpeedMultiplier)
                ? Math.Clamp(fireSpeedMultiplier, 0.05f, 4f)
                : 1f;
        LastToDieStatusReloadSpeedMultiplierValue =
            TryGetReplicatedStateFloat(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieStatusReloadSpeedMultiplierReplicatedStateKey,
                out var reloadSpeedMultiplier)
                ? Math.Clamp(reloadSpeedMultiplier, 0.05f, 4f)
                : 1f;
        LastToDieSecondChanceInvulnerabilityTicksRemainingValue =
            TryGetReplicatedStateInt(
                LastToDieStatusReplicatedStateOwnerId,
                LastToDieSecondChanceInvulnerabilityTicksReplicatedStateKey,
                out var secondChanceTicks)
                ? Math.Max(0, secondChanceTicks)
                : 0;
    }
}
