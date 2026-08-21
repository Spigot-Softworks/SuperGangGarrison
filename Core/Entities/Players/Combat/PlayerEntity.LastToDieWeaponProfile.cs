using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public const string LastToDieWeaponReplicatedStateOwnerId = "ltd.weapon";
    public const string LastToDieSpyRevolverProfileReplicatedStateKey = "spy";
    public const string LastToDieSniperProfileReplicatedStateKey = "sniper";
    public const string LastToDieSniperRuntimeReplicatedStateKey = "sniper_runtime";
    public const string LastToDieSniperExtensionReplicatedStateKey = "sniper_extension";

    private const int LastToDieSniperMarkedTargetSlotMask = 0x7f;
    private const int LastToDieSniperConquistadorStackShift = 7;
    private const int LastToDieSniperConquistadorStackMask = 0x7f;
    private const int LastToDieSniperGhostCloakedBit = 1 << 2;
    private const int LastToDieSniperGhostCooldownShift = 3;
    private const int LastToDieSniperGhostCooldownMask = 0x1ff;

    private PrimaryWeaponDefinition? LastToDiePrimaryWeaponOverride { get; set; }

    public LastToDieSpyRevolverProfile LastToDieSpyRevolverProfile { get; private set; }
        = LastToDieSpyRevolverProfile.Stock;

    public LastToDieSniperProfile LastToDieSniperProfile { get; private set; }
        = LastToDieSniperProfile.Stock;

    public int LastToDieSniperRifleFullChargeTicks => LastToDieSniperProfile.RifleFullChargeTicks;

    public int LastToDieSniperBowFullChargeTicks => LastToDieSniperProfile.HuntsmanFullChargeTicks;

    public byte LastToDieSniperMarkedTargetSlot { get; private set; }

    public int LastToDieSniperConquistadorStacks { get; private set; }

    public bool IsLastToDieSniperGhostCloaked { get; private set; }

    public int LastToDieSniperGhostCooldownTicksRemaining { get; private set; }

    public ushort LastToDieSniperRuntimeState => EncodeLastToDieSniperRuntimeState(
        LastToDieSniperMarkedTargetSlot,
        LastToDieSniperConquistadorStacks);

    public ushort LastToDieSniperExtensionState => EncodeLastToDieSniperExtensionState();

    public int LastToDieLuckyStrikeTriggerProgress { get; private set; }

    internal bool LastPrimaryShotAppliesLastToDieLuckyStrikeStun { get; private set; }

    internal void SetLastToDieSpyRevolverProfile(
        LastToDieSpyRevolverProfile? profile,
        bool refillAmmo)
    {
        profile ??= LastToDieSpyRevolverProfile.Stock;
        LastPrimaryShotAppliesLastToDieLuckyStrikeStun = false;
        var preserveLuckyStrikeProgress = LastToDieSpyRevolverProfile.LuckyStrikeEnabled
            && profile.LuckyStrikeEnabled;
        ApplyLastToDieSpyRevolverProfile(profile, refillAmmo);
        if (!profile.IsActive)
        {
            LastToDieLuckyStrikeTriggerProgress = 0;
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSpyRevolverProfileReplicatedStateKey);
            return;
        }

        if (!profile.LuckyStrikeEnabled || !preserveLuckyStrikeProgress)
        {
            LastToDieLuckyStrikeTriggerProgress = 0;
        }

        WriteLastToDieSpyRevolverReplicatedState();
    }

    internal void ClearLastToDieWeaponProfile()
    {
        SetLastToDieSpyRevolverProfile(null, refillAmmo: false);
        SetLastToDieSniperProfile(null);
        CancelLastToDieSniperVolley();
    }

    internal void SetLastToDieSniperProfile(LastToDieSniperProfile? profile)
    {
        profile ??= LastToDieSniperProfile.Stock;
        if (ClassId != PlayerClass.Sniper)
        {
            profile = LastToDieSniperProfile.Stock;
        }
        var preserveSpottedTarget = LastToDieSniperProfile.SpottedEnabled
            && profile.SpottedEnabled;
        var preserveConquistadorStacks = LastToDieSniperProfile.ConquistadorEnabled
            && profile.ConquistadorEnabled;
        var preserveGhostState = LastToDieSniperProfile.GhostEnabled
            && profile.GhostEnabled;
        ApplyLastToDieSniperProfile(
            profile,
            clearChargeOnProfileChange: profile != LastToDieSniperProfile);
        if (!preserveSpottedTarget)
        {
            LastToDieSniperMarkedTargetSlot = 0;
        }
        if (!preserveConquistadorStacks)
        {
            LastToDieSniperConquistadorStacks = 0;
        }
        if (!preserveGhostState)
        {
            IsLastToDieSniperGhostCloaked = false;
            LastToDieSniperGhostCooldownTicksRemaining = 0;
        }
        if (!profile.IsActive)
        {
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperProfileReplicatedStateKey);
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperRuntimeReplicatedStateKey);
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperExtensionReplicatedStateKey);
            return;
        }

        SetReplicatedStateInt(
            LastToDieWeaponReplicatedStateOwnerId,
            LastToDieSniperProfileReplicatedStateKey,
            profile.Encode());
        WriteLastToDieSniperRuntimeReplicatedState();
        WriteLastToDieSniperExtensionReplicatedState();
    }

    private void RefreshLastToDieWeaponProfileFromReplicatedStateEntries()
    {
        var hasReplicatedState = TryGetReplicatedStateInt(
            LastToDieWeaponReplicatedStateOwnerId,
            LastToDieSpyRevolverProfileReplicatedStateKey,
            out var encoded);
        var profile = hasReplicatedState
            ? LastToDieSpyRevolverProfile.Decode(encoded)
            : LastToDieSpyRevolverProfile.Stock;
        LastPrimaryShotAppliesLastToDieLuckyStrikeStun = false;
        ApplyLastToDieSpyRevolverProfile(profile, refillAmmo: false);
        LastToDieLuckyStrikeTriggerProgress = hasReplicatedState
            && profile.LuckyStrikeEnabled
            ? LastToDieSpyRevolverProfile.DecodeLuckyStrikeTriggerProgress(encoded)
            : 0;

        var encodedSniperProfile = 0;
        var hasSniperPrimaryReplicatedState = ClassId == PlayerClass.Sniper
            && TryGetReplicatedStateInt(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperProfileReplicatedStateKey,
                out encodedSniperProfile);
        var encodedSniperExtensionState = 0;
        var hasSniperExtensionReplicatedState = ClassId == PlayerClass.Sniper
            && TryGetReplicatedStateInt(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperExtensionReplicatedStateKey,
                out encodedSniperExtensionState);
        var hasSniperReplicatedState = hasSniperPrimaryReplicatedState
            || hasSniperExtensionReplicatedState;
        ApplyLastToDieSniperProfile(
            hasSniperReplicatedState
                ? LastToDieSniperProfile.Decode(encodedSniperProfile, encodedSniperExtensionState)
                : LastToDieSniperProfile.Stock,
            clearChargeOnProfileChange: false);
        var encodedSniperRuntimeState = 0;
        var hasSniperRuntimeState = hasSniperReplicatedState
            && TryGetReplicatedStateInt(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperRuntimeReplicatedStateKey,
                out encodedSniperRuntimeState);
        DecodeLastToDieSniperRuntimeState(
            hasSniperRuntimeState ? encodedSniperRuntimeState : 0,
            out var markedTargetSlot,
            out var conquistadorStacks);
        LastToDieSniperMarkedTargetSlot = LastToDieSniperProfile.SpottedEnabled
            ? markedTargetSlot
            : (byte)0;
        LastToDieSniperConquistadorStacks = LastToDieSniperProfile.ConquistadorEnabled
            ? conquistadorStacks
            : 0;
        IsLastToDieSniperGhostCloaked = LastToDieSniperProfile.GhostEnabled
            && (encodedSniperExtensionState & LastToDieSniperGhostCloakedBit) != 0;
        LastToDieSniperGhostCooldownTicksRemaining = LastToDieSniperProfile.GhostEnabled
            ? Math.Clamp(
                (encodedSniperExtensionState >> LastToDieSniperGhostCooldownShift)
                    & LastToDieSniperGhostCooldownMask,
                0,
                LastToDieSniperGhostCooldownMask)
            : 0;
        RefreshLastToDieSniperVolleyFromReplicatedStateEntries();
    }

    internal void SetLastToDieSniperMarkedTargetSlot(byte targetSlot)
    {
        LastToDieSniperMarkedTargetSlot = ClassId == PlayerClass.Sniper
            && LastToDieSniperProfile.SpottedEnabled
            ? checked((byte)Math.Clamp(
                (int)targetSlot,
                0,
                SimulationWorld.MaxPlayableNetworkPlayers))
            : (byte)0;
        WriteLastToDieSniperRuntimeReplicatedState();
    }

    internal void ClearLastToDieSniperMarkedTarget()
    {
        LastToDieSniperMarkedTargetSlot = 0;
        WriteLastToDieSniperRuntimeReplicatedState();
    }

    internal bool TryIncrementLastToDieSniperConquistadorStacks()
    {
        if (ClassId != PlayerClass.Sniper
            || !LastToDieSniperProfile.ConquistadorEnabled
            || LastToDieSniperConquistadorStacks >= LastToDieSniperProfile.ConquistadorMaximumStacks)
        {
            return false;
        }

        LastToDieSniperConquistadorStacks += 1;
        WriteLastToDieSniperRuntimeReplicatedState();
        return true;
    }

    internal void RestoreLastToDieSniperConquistadorStacks(int stacks)
    {
        LastToDieSniperConquistadorStacks = ClassId == PlayerClass.Sniper
            && LastToDieSniperProfile.ConquistadorEnabled
            ? Math.Clamp(stacks, 0, LastToDieSniperProfile.ConquistadorMaximumStacks)
            : 0;
        WriteLastToDieSniperRuntimeReplicatedState();
    }

    internal void ResetLastToDieSniperDynamicState()
    {
        LastToDieSniperMarkedTargetSlot = 0;
        LastToDieSniperConquistadorStacks = 0;
        IsLastToDieSniperGhostCloaked = false;
        LastToDieSniperGhostCooldownTicksRemaining = 0;
        WriteLastToDieSniperRuntimeReplicatedState();
        WriteLastToDieSniperExtensionReplicatedState();
    }

    internal bool TryActivateLastToDieSniperGhostCloak()
    {
        if (!IsAlive
            || ClassId != PlayerClass.Sniper
            || !LastToDieSniperProfile.GhostEnabled
            || IsLastToDieSniperGhostCloaked
            || LastToDieSniperGhostCooldownTicksRemaining > 0)
        {
            return false;
        }

        IsLastToDieSniperGhostCloaked = true;
        WriteLastToDieSniperExtensionReplicatedState();
        return true;
    }

    internal float CaptureLastToDieSniperGhostShot(int ticksPerSecond)
    {
        if (!IsAlive
            || ClassId != PlayerClass.Sniper
            || !LastToDieSniperProfile.GhostEnabled
            || !IsLastToDieSniperGhostCloaked)
        {
            return 1f;
        }

        IsLastToDieSniperGhostCloaked = false;
        LastToDieSniperGhostCooldownTicksRemaining = checked(
            global::OpenGarrison.Core.LastToDie.LastToDieSniperProfile.GhostCooldownSeconds
                * Math.Max(1, ticksPerSecond));
        WriteLastToDieSniperExtensionReplicatedState();
        return global::OpenGarrison.Core.LastToDie.LastToDieSniperProfile.GhostShotDamageMultiplier;
    }

    private void AdvanceLastToDieSniperGhostState()
    {
        if (LastToDieSniperGhostCooldownTicksRemaining <= 0)
        {
            return;
        }

        LastToDieSniperGhostCooldownTicksRemaining -= 1;
        WriteLastToDieSniperExtensionReplicatedState();
    }

    internal void HydrateProtocol64LastToDieSniperRuntimeState(ushort encoded)
    {
        DecodeLastToDieSniperRuntimeState(
            encoded,
            out var markedTargetSlot,
            out var conquistadorStacks);
        LastToDieSniperMarkedTargetSlot = ClassId == PlayerClass.Sniper
            && LastToDieSniperProfile.SpottedEnabled
            ? markedTargetSlot
            : (byte)0;
        LastToDieSniperConquistadorStacks = ClassId == PlayerClass.Sniper
            && LastToDieSniperProfile.ConquistadorEnabled
            ? conquistadorStacks
            : 0;
        WriteLastToDieSniperRuntimeReplicatedState();
    }

    internal void HydrateProtocol64LastToDieSniperExtensionState(ushort encoded)
    {
        if (ClassId == PlayerClass.Sniper && encoded != 0)
        {
            SetReplicatedStateInt(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperExtensionReplicatedStateKey,
                encoded);
        }
        else
        {
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperExtensionReplicatedStateKey);
        }

        RefreshLastToDieWeaponProfileFromReplicatedStateEntries();
    }

    private void WriteLastToDieSniperRuntimeReplicatedState()
    {
        var encoded = LastToDieSniperRuntimeState;
        if (encoded == 0)
        {
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperRuntimeReplicatedStateKey);
            return;
        }

        SetReplicatedStateInt(
            LastToDieWeaponReplicatedStateOwnerId,
            LastToDieSniperRuntimeReplicatedStateKey,
            encoded);
    }

    private void WriteLastToDieSniperExtensionReplicatedState()
    {
        var encoded = LastToDieSniperExtensionState;
        if (encoded == 0)
        {
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSniperExtensionReplicatedStateKey);
            return;
        }

        SetReplicatedStateInt(
            LastToDieWeaponReplicatedStateOwnerId,
            LastToDieSniperExtensionReplicatedStateKey,
            encoded);
    }

    private ushort EncodeLastToDieSniperExtensionState()
    {
        var encoded = LastToDieSniperProfile.EncodeExtensionProfile();
        if (IsLastToDieSniperGhostCloaked) encoded |= LastToDieSniperGhostCloakedBit;
        encoded |= checked((ushort)(
            Math.Clamp(
                LastToDieSniperGhostCooldownTicksRemaining,
                0,
                LastToDieSniperGhostCooldownMask)
            << LastToDieSniperGhostCooldownShift));
        return encoded;
    }

    private static ushort EncodeLastToDieSniperRuntimeState(
        byte markedTargetSlot,
        int conquistadorStacks)
    {
        var clampedTargetSlot = Math.Clamp(
            (int)markedTargetSlot,
            0,
            SimulationWorld.MaxPlayableNetworkPlayers);
        var clampedStacks = Math.Clamp(
            conquistadorStacks,
            0,
            LastToDieSniperProfile.ConquistadorMaximumStacks);
        return checked((ushort)(clampedTargetSlot
            | (clampedStacks << LastToDieSniperConquistadorStackShift)));
    }

    private static void DecodeLastToDieSniperRuntimeState(
        int encoded,
        out byte markedTargetSlot,
        out int conquistadorStacks)
    {
        markedTargetSlot = checked((byte)Math.Clamp(
            encoded & LastToDieSniperMarkedTargetSlotMask,
            byte.MinValue,
            SimulationWorld.MaxPlayableNetworkPlayers));
        conquistadorStacks = Math.Clamp(
            (encoded >> LastToDieSniperConquistadorStackShift)
                & LastToDieSniperConquistadorStackMask,
            0,
            LastToDieSniperProfile.ConquistadorMaximumStacks);
    }

    internal bool AdvanceLastToDieLuckyStrikeTrigger()
    {
        if (ClassId != PlayerClass.Spy || !LastToDieSpyRevolverProfile.LuckyStrikeEnabled)
        {
            return false;
        }

        LastToDieLuckyStrikeTriggerProgress =
            (LastToDieLuckyStrikeTriggerProgress + 1)
            % LastToDieSpyRevolverProfile.LuckyStrikeTriggerInterval;
        WriteLastToDieSpyRevolverReplicatedState();
        return LastToDieLuckyStrikeTriggerProgress == 0;
    }

    private void CommitLastToDieRevolverTrigger(
        PrimaryWeaponDefinition weaponDefinition,
        PlayerClass weaponClassId)
    {
        LastPrimaryShotAppliesLastToDieLuckyStrikeStun =
            ClassId == PlayerClass.Spy
            && weaponClassId == PlayerClass.Spy
            && weaponDefinition.Kind == PrimaryWeaponKind.Revolver
            && AdvanceLastToDieLuckyStrikeTrigger();
    }

    internal void ResetLastToDieLuckyStrikeTriggerProgress()
    {
        LastToDieLuckyStrikeTriggerProgress = 0;
        LastPrimaryShotAppliesLastToDieLuckyStrikeStun = false;
        if (LastToDieSpyRevolverProfile.IsActive)
        {
            WriteLastToDieSpyRevolverReplicatedState();
        }
        else
        {
            ClearReplicatedState(
                LastToDieWeaponReplicatedStateOwnerId,
                LastToDieSpyRevolverProfileReplicatedStateKey);
        }
    }

    private void WriteLastToDieSpyRevolverReplicatedState()
    {
        SetReplicatedStateInt(
            LastToDieWeaponReplicatedStateOwnerId,
            LastToDieSpyRevolverProfileReplicatedStateKey,
            LastToDieSpyRevolverProfile.EncodeReplicatedState(
                LastToDieLuckyStrikeTriggerProgress));
    }

    private void ApplyLastToDieSpyRevolverProfile(
        LastToDieSpyRevolverProfile profile,
        bool refillAmmo)
    {
        LastToDieSpyRevolverProfile = profile;
        RefreshLastToDieWeaponOverrideForClassDefinition();
        CurrentShells = refillAmmo
            ? MaxShells
            : int.Clamp(CurrentShells, 0, MaxShells);
        if (CurrentShells >= MaxShells)
        {
            ReloadTicksUntilNextShell = 0;
        }
        else if (PrimaryWeapon.AutoReloads && ReloadTicksUntilNextShell <= 0)
        {
            ReloadTicksUntilNextShell = ApplyExperimentalReloadMultiplier(PrimaryWeapon.AmmoReloadTicks);
        }

        RefreshGameplayLoadoutState();
    }

    private void ApplyLastToDieSniperProfile(
        LastToDieSniperProfile profile,
        bool clearChargeOnProfileChange)
    {
        var changed = profile != LastToDieSniperProfile;
        LastToDieSniperProfile = profile;
        if (changed && clearChargeOnProfileChange)
        {
            SniperChargeTicks = 0;
            CancelSniperBowCharge();
        }

        if (ClassId != PlayerClass.Sniper)
        {
            return;
        }

        if (profile.LightMarksmanEnabled)
        {
            IsSniperScoped = false;
            SniperChargeTicks = 0;
        }
        else
        {
            SniperChargeTicks = Math.Clamp(
                SniperChargeTicks,
                0,
                profile.RifleFullChargeTicks);
        }

        SniperBowChargeTicks = Math.Clamp(
            SniperBowChargeTicks,
            0,
            profile.HuntsmanFullChargeTicks);
    }

    private void RefreshLastToDieWeaponOverrideForClassDefinition()
    {
        LastToDiePrimaryWeaponOverride = ClassId == PlayerClass.Spy
            && LastToDieSpyRevolverProfile.IsActive
            ? LastToDieSpyRevolverProfile.ApplyTo(ClassDefinition.PrimaryWeapon)
            : null;
    }
}
