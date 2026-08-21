using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const string LastToDieSpyAfterlifeReplicatedStateOwnerId = "ltd.afterlife";

    public const string LastToDieSpyAfterlifeReplicatedStateKey = "state";

    private const uint LastToDieSpyAfterlifeTimerMask = 0xffffu;

    private const int LastToDieSpyAfterlifeWindowTicksShift = 16;

    private bool LastToDieSpyAfterlifeEnabledValue { get; set; }

    private int LastToDieSpyAfterlifeWindowTicksRemainingValue { get; set; }

    private int LastToDieSpyAfterlifeCooldownTicksRemainingValue { get; set; }

    private int LastToDieSpyAfterlifeWindowTicksValue { get; set; } =
        GetLastToDieSpyAfterlifeWindowTicks(SimulationConfig.DefaultTicksPerSecond);

    private int LastToDieSpyAfterlifeCooldownTicksValue { get; set; } =
        GetLastToDieSpyAfterlifeCooldownTicks(SimulationConfig.DefaultTicksPerSecond);

    private bool LastToDieSpyAfterlifeExpiryPendingValue { get; set; }

    public bool LastToDieSpyAfterlifeEnabled => LastToDieSpyAfterlifeEnabledValue;

    public int LastToDieSpyAfterlifeWindowTicksRemaining =>
        LastToDieSpyAfterlifeWindowTicksRemainingValue;

    public int LastToDieSpyAfterlifeCooldownTicksRemaining =>
        LastToDieSpyAfterlifeCooldownTicksRemainingValue;

    public int LastToDieSpyAfterlifeWindowTicks => LastToDieSpyAfterlifeWindowTicksValue;

    public int LastToDieSpyAfterlifeCooldownTicks => LastToDieSpyAfterlifeCooldownTicksValue;

    public float LastToDieSpyAfterlifeWindowFraction =>
        LastToDieSpyAfterlifeWindowTicksValue <= 0
            ? 0f
            : Math.Clamp(
                LastToDieSpyAfterlifeWindowTicksRemainingValue
                    / (float)LastToDieSpyAfterlifeWindowTicksValue,
                0f,
                1f);

    public float LastToDieSpyAfterlifeCooldownFraction =>
        LastToDieSpyAfterlifeCooldownTicksValue <= 0
            ? 0f
            : Math.Clamp(
                LastToDieSpyAfterlifeCooldownTicksRemainingValue
                    / (float)LastToDieSpyAfterlifeCooldownTicksValue,
                0f,
                1f);

    public bool IsLastToDieSpyAfterlifeActive =>
        IsAlive
        && ClassId == PlayerClass.Spy
        && LastToDieSpyAfterlifeWindowTicksRemainingValue > 0;

    internal bool IsLastToDieSpyAfterlifeIncomingDamageImmune =>
        IsLastToDieSpyAfterlifeActive;

    internal bool IsLastToDieSpyAfterlifeExpiryPending =>
        LastToDieSpyAfterlifeExpiryPendingValue;

    public uint LastToDieSpyAfterlifeState => EncodeLastToDieSpyAfterlifeState();

    internal void ConfigureLastToDieSpyAfterlife(
        bool enabled,
        int ticksPerSecond,
        bool resetDynamicState)
    {
        LastToDieSpyAfterlifeEnabledValue = enabled;
        LastToDieSpyAfterlifeWindowTicksValue =
            GetLastToDieSpyAfterlifeWindowTicks(ticksPerSecond);
        LastToDieSpyAfterlifeCooldownTicksValue =
            GetLastToDieSpyAfterlifeCooldownTicks(ticksPerSecond);

        if (!enabled || resetDynamicState)
        {
            ResetLastToDieSpyAfterlifeDynamicState();
            return;
        }

        LastToDieSpyAfterlifeWindowTicksRemainingValue = Math.Clamp(
            LastToDieSpyAfterlifeWindowTicksRemainingValue,
            0,
            LastToDieSpyAfterlifeWindowTicksValue);
        LastToDieSpyAfterlifeCooldownTicksRemainingValue = Math.Clamp(
            LastToDieSpyAfterlifeCooldownTicksRemainingValue,
            0,
            LastToDieSpyAfterlifeCooldownTicksValue);
        if (LastToDieSpyAfterlifeWindowTicksRemainingValue == 0)
        {
            LastToDieSpyAfterlifeExpiryPendingValue = false;
        }

        WriteLastToDieSpyAfterlifeReplicatedState();
    }

    internal bool TryStartLastToDieSpyAfterlife(int ticksPerSecond)
    {
        if (!IsAlive
            || ClassId != PlayerClass.Spy
            || !LastToDieSpyAfterlifeEnabledValue
            || LastToDieSpyAfterlifeWindowTicksRemainingValue > 0
            || LastToDieSpyAfterlifeCooldownTicksRemainingValue > 0)
        {
            return false;
        }

        LastToDieSpyAfterlifeWindowTicksValue =
            GetLastToDieSpyAfterlifeWindowTicks(ticksPerSecond);
        LastToDieSpyAfterlifeCooldownTicksValue =
            GetLastToDieSpyAfterlifeCooldownTicks(ticksPerSecond);
        LastToDieSpyAfterlifeWindowTicksRemainingValue =
            LastToDieSpyAfterlifeWindowTicksValue;
        LastToDieSpyAfterlifeCooldownTicksRemainingValue =
            LastToDieSpyAfterlifeCooldownTicksValue;
        LastToDieSpyAfterlifeExpiryPendingValue = false;
        ForceSetHealth(1);
        WriteLastToDieSpyAfterlifeReplicatedState();
        return true;
    }

    internal void AdvanceLastToDieSpyAfterlifeState()
    {
        if (LastToDieSpyAfterlifeWindowTicksRemainingValue <= 0
            && LastToDieSpyAfterlifeCooldownTicksRemainingValue <= 0)
        {
            return;
        }

        if (ClassId != PlayerClass.Spy || !LastToDieSpyAfterlifeEnabledValue)
        {
            ResetLastToDieSpyAfterlifeDynamicState();
            return;
        }

        if (LastToDieSpyAfterlifeWindowTicksRemainingValue > 0)
        {
            LastToDieSpyAfterlifeWindowTicksRemainingValue -= 1;
            if (LastToDieSpyAfterlifeWindowTicksRemainingValue == 0)
            {
                LastToDieSpyAfterlifeExpiryPendingValue = true;
            }
        }

        if (LastToDieSpyAfterlifeCooldownTicksRemainingValue > 0)
        {
            LastToDieSpyAfterlifeCooldownTicksRemainingValue -= 1;
        }

        WriteLastToDieSpyAfterlifeReplicatedState();
    }

    internal void CompleteLastToDieSpyAfterlifeSuccess()
    {
        LastToDieSpyAfterlifeWindowTicksRemainingValue = 0;
        LastToDieSpyAfterlifeExpiryPendingValue = false;
        ForceSetHealth(Math.Max(1, (MaxHealth * 3 + 4) / 5));
        WriteLastToDieSpyAfterlifeReplicatedState();
    }

    internal void PrepareLastToDieSpyAfterlifeFailure()
    {
        LastToDieSpyAfterlifeWindowTicksRemainingValue = 0;
        LastToDieSpyAfterlifeExpiryPendingValue = false;
        Health = 0;
        WriteLastToDieSpyAfterlifeReplicatedState();
    }

    internal void ResetLastToDieSpyAfterlifeDynamicState(bool preserveCooldown = false)
    {
        LastToDieSpyAfterlifeWindowTicksRemainingValue = 0;
        LastToDieSpyAfterlifeExpiryPendingValue = false;
        if (!preserveCooldown)
        {
            LastToDieSpyAfterlifeCooldownTicksRemainingValue = 0;
        }

        WriteLastToDieSpyAfterlifeReplicatedState();
    }

    internal void HydrateProtocol64LastToDieSpyAfterlifeState(
        uint encoded,
        int ticksPerSecond)
    {
        LastToDieSpyAfterlifeWindowTicksValue =
            GetLastToDieSpyAfterlifeWindowTicks(ticksPerSecond);
        LastToDieSpyAfterlifeCooldownTicksValue =
            GetLastToDieSpyAfterlifeCooldownTicks(ticksPerSecond);
        ApplyLastToDieSpyAfterlifeState(encoded, writeReplicatedState: true);
    }

    private void RefreshLastToDieSpyAfterlifeFromReplicatedStateEntries()
    {
        var encoded = TryGetReplicatedStateInt(
            LastToDieSpyAfterlifeReplicatedStateOwnerId,
            LastToDieSpyAfterlifeReplicatedStateKey,
            out var replicatedState)
                ? unchecked((uint)replicatedState)
                : 0u;
        ApplyLastToDieSpyAfterlifeState(encoded, writeReplicatedState: false);
    }

    private void ApplyLastToDieSpyAfterlifeState(uint encoded, bool writeReplicatedState)
    {
        var cooldownTicks = (int)(encoded & LastToDieSpyAfterlifeTimerMask);
        var windowTicks = (int)((encoded >> LastToDieSpyAfterlifeWindowTicksShift)
            & LastToDieSpyAfterlifeTimerMask);
        LastToDieSpyAfterlifeCooldownTicksRemainingValue = Math.Clamp(
            cooldownTicks,
            0,
            LastToDieSpyAfterlifeCooldownTicksValue);
        LastToDieSpyAfterlifeWindowTicksRemainingValue = Math.Clamp(
            Math.Min(windowTicks, LastToDieSpyAfterlifeCooldownTicksRemainingValue),
            0,
            LastToDieSpyAfterlifeWindowTicksValue);
        LastToDieSpyAfterlifeExpiryPendingValue = false;

        if (writeReplicatedState)
        {
            WriteLastToDieSpyAfterlifeReplicatedState();
        }
    }

    private uint EncodeLastToDieSpyAfterlifeState()
    {
        var cooldownTicks = (uint)Math.Clamp(
            LastToDieSpyAfterlifeCooldownTicksRemainingValue,
            0,
            ushort.MaxValue);
        var windowTicks = (uint)Math.Clamp(
            LastToDieSpyAfterlifeWindowTicksRemainingValue,
            0,
            ushort.MaxValue);
        return cooldownTicks | (windowTicks << LastToDieSpyAfterlifeWindowTicksShift);
    }

    private void WriteLastToDieSpyAfterlifeReplicatedState()
    {
        var encoded = LastToDieSpyAfterlifeState;
        if (encoded == 0)
        {
            ClearReplicatedState(
                LastToDieSpyAfterlifeReplicatedStateOwnerId,
                LastToDieSpyAfterlifeReplicatedStateKey);
            return;
        }

        SetReplicatedStateInt(
            LastToDieSpyAfterlifeReplicatedStateOwnerId,
            LastToDieSpyAfterlifeReplicatedStateKey,
            unchecked((int)encoded));
    }

    private static int GetLastToDieSpyAfterlifeWindowTicks(int ticksPerSecond) =>
        Math.Clamp(
            (int)MathF.Ceiling(
                LastToDieDerivedModifiers.SpyAfterlifeWindowSeconds
                    * Math.Max(1, ticksPerSecond)),
            1,
            ushort.MaxValue);

    private static int GetLastToDieSpyAfterlifeCooldownTicks(int ticksPerSecond) =>
        Math.Clamp(
            (int)MathF.Ceiling(
                LastToDieDerivedModifiers.SpyAfterlifeCooldownSeconds
                    * Math.Max(1, ticksPerSecond)),
            1,
            ushort.MaxValue);
}
