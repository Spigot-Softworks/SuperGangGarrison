using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const string LastToDieSpyInfiltrateReplicatedStateOwnerId = "ltd.infiltrate";

    public const string LastToDieSpyInfiltrateReplicatedStateKey = "state";

    private const uint LastToDieSpyInfiltrateCooldownMask = 0xffffu;

    private const int LastToDieSpyInfiltrateDashTicksShift = 16;

    private const uint LastToDieSpyInfiltrateDashTicksMask = 0xffu;

    private const uint LastToDieSpyInfiltrateLeftDirectionFlag = 1u << 24;

    public const uint LastToDieSpyInfiltrateKnownStateBits = 0x01ff_ffffu;

    private bool LastToDieSpyInfiltrateEnabledValue { get; set; }

    private int LastToDieSpyInfiltrateDashTicksRemainingValue { get; set; }

    private int LastToDieSpyInfiltrateCooldownTicksRemainingValue { get; set; }

    private int LastToDieSpyInfiltrateDurationTicksValue { get; set; } =
        GetLastToDieSpyInfiltrateDurationTicks(SimulationConfig.DefaultTicksPerSecond);

    private int LastToDieSpyInfiltrateCooldownTicksValue { get; set; } =
        GetLastToDieSpyInfiltrateCooldownTicks(SimulationConfig.DefaultTicksPerSecond);

    private float LastToDieSpyInfiltrateDirectionXValue { get; set; } = 1f;

    public bool LastToDieSpyInfiltrateEnabled => LastToDieSpyInfiltrateEnabledValue;

    public int LastToDieSpyInfiltrateDashTicksRemaining =>
        LastToDieSpyInfiltrateDashTicksRemainingValue;

    public int LastToDieSpyInfiltrateCooldownTicksRemaining =>
        LastToDieSpyInfiltrateCooldownTicksRemainingValue;

    public int LastToDieSpyInfiltrateDurationTicks => LastToDieSpyInfiltrateDurationTicksValue;

    public int LastToDieSpyInfiltrateCooldownTicks => LastToDieSpyInfiltrateCooldownTicksValue;

    public float LastToDieSpyInfiltrateCooldownFraction =>
        LastToDieSpyInfiltrateCooldownTicksValue <= 0
            ? 0f
            : Math.Clamp(
                LastToDieSpyInfiltrateCooldownTicksRemainingValue
                    / (float)LastToDieSpyInfiltrateCooldownTicksValue,
                0f,
                1f);

    public float LastToDieSpyInfiltrateDirectionX =>
        LastToDieSpyInfiltrateDirectionXValue < 0f ? -1f : 1f;

    public bool IsLastToDieSpyInfiltrateDashing =>
        IsAlive
        && ClassId == PlayerClass.Spy
        && LastToDieSpyInfiltrateDashTicksRemainingValue > 0;

    internal bool IsLastToDieSpyInfiltrateProjectileImmune =>
        IsLastToDieSpyInfiltrateDashing;

    public uint LastToDieSpyInfiltrateState => EncodeLastToDieSpyInfiltrateState();

    internal void ConfigureLastToDieSpyInfiltrate(
        bool enabled,
        int ticksPerSecond,
        bool resetDynamicState)
    {
        LastToDieSpyInfiltrateEnabledValue = enabled;
        LastToDieSpyInfiltrateDurationTicksValue =
            GetLastToDieSpyInfiltrateDurationTicks(ticksPerSecond);
        LastToDieSpyInfiltrateCooldownTicksValue =
            GetLastToDieSpyInfiltrateCooldownTicks(ticksPerSecond);

        if (!enabled || resetDynamicState)
        {
            ResetLastToDieSpyInfiltrateDynamicState();
        }
        else
        {
            LastToDieSpyInfiltrateDashTicksRemainingValue = Math.Clamp(
                LastToDieSpyInfiltrateDashTicksRemainingValue,
                0,
                LastToDieSpyInfiltrateDurationTicksValue);
            LastToDieSpyInfiltrateCooldownTicksRemainingValue = Math.Clamp(
                LastToDieSpyInfiltrateCooldownTicksRemainingValue,
                0,
                LastToDieSpyInfiltrateCooldownTicksValue);
        }
    }

    internal bool TryStartLastToDieSpyInfiltrate(int ticksPerSecond)
    {
        if (!IsAlive
            || ClassId != PlayerClass.Spy
            || !LastToDieSpyInfiltrateEnabledValue
            || LastToDieSpyInfiltrateDashTicksRemainingValue > 0
            || LastToDieSpyInfiltrateCooldownTicksRemainingValue > 0
            || IsServerFrozen
            || IsServerStunned
            || IsExperimentalCryoFrozen
            || IsTaunting
            || IsSpyBackstabAnimating)
        {
            return false;
        }

        LastToDieSpyInfiltrateDurationTicksValue =
            GetLastToDieSpyInfiltrateDurationTicks(ticksPerSecond);
        LastToDieSpyInfiltrateCooldownTicksValue =
            GetLastToDieSpyInfiltrateCooldownTicks(ticksPerSecond);
        LastToDieSpyInfiltrateDirectionXValue = FacingDirectionX < 0f ? -1f : 1f;
        LastToDieSpyInfiltrateDashTicksRemainingValue =
            LastToDieSpyInfiltrateDurationTicksValue;
        LastToDieSpyInfiltrateCooldownTicksRemainingValue =
            LastToDieSpyInfiltrateCooldownTicksValue;
        HorizontalSpeed = LastToDieSpyInfiltrateDirectionXValue
            * LastToDieDerivedModifiers.SpyInfiltrateHorizontalSpeed;
        WriteLastToDieSpyInfiltrateReplicatedState();
        return true;
    }

    internal void AdvanceLastToDieSpyInfiltrateState()
    {
        if (LastToDieSpyInfiltrateDashTicksRemainingValue <= 0
            && LastToDieSpyInfiltrateCooldownTicksRemainingValue <= 0)
        {
            return;
        }

        if (!IsAlive || ClassId != PlayerClass.Spy)
        {
            ResetLastToDieSpyInfiltrateDynamicState();
            return;
        }

        if (LastToDieSpyInfiltrateDashTicksRemainingValue > 0)
        {
            LastToDieSpyInfiltrateDashTicksRemainingValue -= 1;
            if (LastToDieSpyInfiltrateDashTicksRemainingValue == 0)
            {
                HorizontalSpeed = 0f;
            }
        }

        if (LastToDieSpyInfiltrateCooldownTicksRemainingValue > 0)
        {
            LastToDieSpyInfiltrateCooldownTicksRemainingValue -= 1;
        }

        WriteLastToDieSpyInfiltrateReplicatedState();
    }

    internal void ResetLastToDieSpyInfiltrateDynamicState()
    {
        if (LastToDieSpyInfiltrateDashTicksRemainingValue > 0)
        {
            HorizontalSpeed = 0f;
        }

        LastToDieSpyInfiltrateDashTicksRemainingValue = 0;
        LastToDieSpyInfiltrateCooldownTicksRemainingValue = 0;
        LastToDieSpyInfiltrateDirectionXValue = 1f;
        ClearReplicatedState(
            LastToDieSpyInfiltrateReplicatedStateOwnerId,
            LastToDieSpyInfiltrateReplicatedStateKey);
    }

    internal void HydrateProtocol64LastToDieSpyInfiltrateState(
        uint encoded,
        int ticksPerSecond)
    {
        LastToDieSpyInfiltrateDurationTicksValue =
            GetLastToDieSpyInfiltrateDurationTicks(ticksPerSecond);
        LastToDieSpyInfiltrateCooldownTicksValue =
            GetLastToDieSpyInfiltrateCooldownTicks(ticksPerSecond);
        ApplyLastToDieSpyInfiltrateState(encoded, writeReplicatedState: true);
    }

    private void RefreshLastToDieSpyInfiltrateFromReplicatedStateEntries()
    {
        var encoded = TryGetReplicatedStateInt(
            LastToDieSpyInfiltrateReplicatedStateOwnerId,
            LastToDieSpyInfiltrateReplicatedStateKey,
            out var replicatedState)
                ? unchecked((uint)replicatedState)
                : 0u;
        ApplyLastToDieSpyInfiltrateState(encoded, writeReplicatedState: false);
    }

    private void ApplyLastToDieSpyInfiltrateState(uint encoded, bool writeReplicatedState)
    {
        encoded &= LastToDieSpyInfiltrateKnownStateBits;
        var cooldownTicks = (int)(encoded & LastToDieSpyInfiltrateCooldownMask);
        var dashTicks = (int)((encoded >> LastToDieSpyInfiltrateDashTicksShift)
            & LastToDieSpyInfiltrateDashTicksMask);
        LastToDieSpyInfiltrateCooldownTicksRemainingValue = Math.Clamp(
            cooldownTicks,
            0,
            LastToDieSpyInfiltrateCooldownTicksValue);
        LastToDieSpyInfiltrateDashTicksRemainingValue = Math.Clamp(
            Math.Min(dashTicks, LastToDieSpyInfiltrateCooldownTicksRemainingValue),
            0,
            LastToDieSpyInfiltrateDurationTicksValue);
        LastToDieSpyInfiltrateDirectionXValue =
            (encoded & LastToDieSpyInfiltrateLeftDirectionFlag) != 0 ? -1f : 1f;

        if (writeReplicatedState)
        {
            WriteLastToDieSpyInfiltrateReplicatedState();
        }
    }

    private uint EncodeLastToDieSpyInfiltrateState()
    {
        var cooldownTicks = (uint)Math.Clamp(
            LastToDieSpyInfiltrateCooldownTicksRemainingValue,
            0,
            ushort.MaxValue);
        var dashTicks = (uint)Math.Clamp(
            LastToDieSpyInfiltrateDashTicksRemainingValue,
            0,
            byte.MaxValue);
        var encoded = cooldownTicks
            | (dashTicks << LastToDieSpyInfiltrateDashTicksShift);
        if (dashTicks > 0 && LastToDieSpyInfiltrateDirectionXValue < 0f)
        {
            encoded |= LastToDieSpyInfiltrateLeftDirectionFlag;
        }

        return encoded;
    }

    private void WriteLastToDieSpyInfiltrateReplicatedState()
    {
        var encoded = LastToDieSpyInfiltrateState;
        if (encoded == 0)
        {
            ClearReplicatedState(
                LastToDieSpyInfiltrateReplicatedStateOwnerId,
                LastToDieSpyInfiltrateReplicatedStateKey);
            return;
        }

        SetReplicatedStateInt(
            LastToDieSpyInfiltrateReplicatedStateOwnerId,
            LastToDieSpyInfiltrateReplicatedStateKey,
            unchecked((int)encoded));
    }

    private static int GetLastToDieSpyInfiltrateDurationTicks(int ticksPerSecond) =>
        Math.Clamp(
            (int)MathF.Ceiling(
                LastToDieDerivedModifiers.SpyInfiltrateDurationSeconds
                    * Math.Max(1, ticksPerSecond)),
            1,
            byte.MaxValue);

    private static int GetLastToDieSpyInfiltrateCooldownTicks(int ticksPerSecond) =>
        Math.Clamp(
            (int)MathF.Ceiling(
                LastToDieDerivedModifiers.SpyInfiltrateCooldownSeconds
                    * Math.Max(1, ticksPerSecond)),
            1,
            ushort.MaxValue);
}
