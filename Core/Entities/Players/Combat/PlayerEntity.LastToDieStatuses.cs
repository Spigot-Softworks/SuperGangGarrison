namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const string LastToDieStatusReplicatedStateOwnerId = "ltd.status";
    public const string LastToDieStatusMovementSpeedMultiplierReplicatedStateKey = "movescale";
    public const string LastToDieGuardianEvasionChanceReplicatedStateKey = "guardian_evasion";
    public const string LastToDieStatusOutgoingDamageMultiplierReplicatedStateKey = "outgoing_damage";
    public const string LastToDieMedicHailMaryTicksReplicatedStateKey = "hail_ticks";

    private float LastToDieStatusMovementSpeedMultiplierValue { get; set; } = 1f;
    private float LastToDieGuardianEvasionChanceValue { get; set; }
    private float LastToDieStatusOutgoingDamageMultiplierValue { get; set; } = 1f;
    private int LastToDieMedicHailMaryTicksRemainingValue { get; set; }

    public float LastToDieStatusMovementSpeedMultiplier => LastToDieStatusMovementSpeedMultiplierValue;

    public float LastToDieGuardianEvasionChance => LastToDieGuardianEvasionChanceValue;

    public float LastToDieStatusOutgoingDamageMultiplier =>
        LastToDieStatusOutgoingDamageMultiplierValue;

    public int LastToDieMedicHailMaryTicksRemaining =>
        LastToDieMedicHailMaryTicksRemainingValue;

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
        SetLastToDieGuardianEvasionChance(0f);
        SetServerStunTicks(0);
        SetLastToDieMedicHailMaryTicks(0);
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
    }
}
