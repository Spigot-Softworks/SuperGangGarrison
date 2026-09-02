using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenGarrison.Protocol;

/// <summary>
/// Stable IDs for the disjoint authoritative-state slice. These IDs are not
/// added to the legacy/default registry until a canonical backend opts in.
/// </summary>
public enum Protocol64StateEventId : ushort
{
    PlayerStateBatch = 32,
    RosterState = 33,
    ProjectileState = 34,
    ProjectileLifecycle = 35,
    StateResyncRequest = 36,
    StateResyncResponse = 37,
}

public static class Protocol64StateSchemaIds
{
    public const ushort PlayerStateBatch = (ushort)Protocol64StateEventId.PlayerStateBatch;
    public const ushort RosterState = (ushort)Protocol64StateEventId.RosterState;
    public const ushort ProjectileState = (ushort)Protocol64StateEventId.ProjectileState;
    public const ushort ProjectileLifecycle = (ushort)Protocol64StateEventId.ProjectileLifecycle;
    public const ushort StateResyncRequest = (ushort)Protocol64StateEventId.StateResyncRequest;
    public const ushort StateResyncResponse = (ushort)Protocol64StateEventId.StateResyncResponse;
}

public enum Protocol64ProjectileKind : byte
{
    Bullet = 1,
    Blade = 2,
    Needle = 3,
    RevolverShot = 4,
    Rocket = 5,
    Flame = 6,
    Flare = 7,
    Mine = 8,
    Grenade = 9,
    Custom = 10,
    Arrow = 11,
}

public enum Protocol64ProjectileLifecycleKind : byte
{
    Spawn = 1,
    Despawn = 2,
    Replaced = 3,
}

public enum Protocol64StateResyncReason : byte
{
    InitialState = 1,
    MissingState = 2,
    InvalidState = 3,
    StaleState = 4,
    ClientRequested = 5,
}

/// <summary>Stable identity for a player slot, including reuse generation.</summary>
public sealed record Protocol64PlayerIdentity(
    ushort Slot,
    ulong PlayerId,
    uint Generation);

public sealed record Protocol64LastToDieSniperVolleyState(
    byte QueuedArrowCount,
    byte DueArrowCount,
    byte SourceTicksUntilNextArrow,
    float VelocityX,
    float VelocityY,
    int Damage,
    float FakeSpeedMultiplier,
    byte PayloadFlags,
    float PoisonDamagePerSecond,
    float GhostDamageMultiplier,
    float CriticalDamageMultiplier = 1f);

/// <summary>
/// A complete player record. Class identity and health are deliberately inline;
/// applying this record never depends on a separately populated string cache.
/// </summary>
public sealed record Protocol64PlayerState(
    ushort Slot,
    ulong PlayerId,
    uint Generation,
    string GameplayClassId,
    int Health,
    int MaxHealth,
    byte Team,
    bool IsAlive,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    byte ActiveWeapon,
    uint AbilityState,
    uint StateTick,
    // This is scoped to the player record rather than the batch because the
    // batch contains every player while each client has its own input stream.
    // A zero value means that no input has been authoritatively consumed yet.
    uint LastProcessedInputSequence = 0,
    bool IsGrounded = false,
    int RemainingAirJumps = 0,
    // Stock Medic's M2 needlegun is a secondary ability that consumes the
    // primary weapon's PlayerEntity.CurrentShells value.
    int CurrentAmmo = 0,
    int MaxAmmo = 0,
    int OffhandAmmo = 0,
    int OffhandMaxAmmo = 0,
    int OffhandCooldownTicks = 0,
    int OffhandReloadTicks = 0,
    // Compact class-specific Last to Die weapon profile. Spy uses bits 0-7
    // plus Lucky Strike progress in bits 8-9; Sniper uses bits 4-15. The
    // Sniper profile has exhausted this field and future perks need a second word.
    ushort LastToDieSpyRevolverState = 0,
    // Cloak is authoritative gameplay state. Alpha remains presentation state,
    // quantized to one byte by the wire codec.
    bool IsSpyCloaked = false,
    float SpyCloakAlpha = 1f,
    ushort LastToDieSpyCloakMeterUnits = 0,
    byte LastToDieSpyRogueRampStacks = 0,
    ushort LastToDieSpyRogueRampTicks = 0,
    bool IsSpySuperjumping = false,
    float SpySuperjumpHorizontalVelocity = 0f,
    ushort SpySuperjumpCooldownTicksRemaining = 0,
    byte SpySuperjumpAvailableCharges = 1,
    byte SpySuperjumpMaximumCharges = 1,
    ushort SpySuperjumpChargeTicks = 0,
    float SpySuperjumpChargeDirectionDegrees = 0f,
    byte SpySuperjumpChargeStartMovementButtons = 0,
    bool SpySuperjumpChargeStartBlockedUntilAbilityRelease = false,
    // Sniper source-owned runtime: marked target slot in bits 0-6 and
    // Conquistador stacks in bits 7-13.
    ushort LastToDieSniperRuntimeState = 0,
    // Effective Medic heal-link state on this player. Bit 0 is Stimulant Drip,
    // bit 1 is Agility Drive, bit 2 is Martyr-protected, and bit 3 is the
    // active Martyr protector role.
    byte LastToDieMedicLinkState = 0,
    // The acquired weapon is inventory-owned elsewhere, but its authoritative
    // ammo and timing still need to survive support effects and reconciliation.
    int AcquiredAmmo = 0,
    int AcquiredMaxAmmo = 0,
    int AcquiredCooldownTicks = 0,
    int AcquiredReloadTicks = 0,
    // Stock Medigun M2 has dedicated cadence/refill timers even though its
    // ammunition is carried in CurrentAmmo.
    int MedicNeedleCooldownTicks = 0,
    int MedicNeedleRefillTicks = 0,
    // Pyro fuel has tenth-unit precision and cannot be reconstructed exactly
    // from the integer CurrentAmmo field.
    int PyroPrimaryFuelScaled = 0,
    // Second compact Sniper word. Bits 0-1 are Ghost/Overkiller profile,
    // bit 2 is active Ghost cloak, bits 3-11 are cooldown ticks, and bits
    // 12-14 are Decapitator/Menage A Trois/Explosive Tip profiles.
    ushort LastToDieSniperExtensionState = 0,
    // Spy Infiltrate runtime. Bits 0-15 are cooldown ticks, bits 16-23 are
    // active dash ticks, and bit 24 captures a leftward dash.
    uint LastToDieSpyInfiltrateState = 0,
    // Spy Afterlife runtime. Bits 0-15 are cooldown ticks and bits 16-31 are
    // the remaining active ghost-window ticks.
    uint LastToDieSpyAfterlifeState = 0,
    // Authoritative release-time state for delayed Menage A Trois arrows.
    Protocol64LastToDieSniperVolleyState? LastToDieSniperVolleyState = null,
    // Bits 0-1 carry the Medic delivery mode and bit 7 marks active drain.
    byte MedicUberDeliveryState = 0,
    int MedicHealTargetId = -1,
    float MedicUberCharge = 0f,
    ushort LastToDieMedicHailMaryTicksRemaining = 0,
    ushort ServerStunTicksRemaining = 0,
    int KritzCritBoostTicksRemaining = 0,
    int KritzCritBoostProviderPlayerId = 0,
    int KritzCritBoostProviderSlot = int.MaxValue,
    float KritzCritBoostDamageMultiplier = 1f,
    // 0 = inactive, 1 = M2 held without a shot, 2 = a cloaked shot consumed the hold.
    byte LastToDieProfessionalFireChordState = 0,
    // Normal gameplay buff state. Constructor defaults preserve source
    // compatibility; the enclosing wire schemas carry bumped revisions.
    bool IsDispenserBuffed = false,
    float DispenserAttackReloadSpeedMultiplier = 1f,
    // Authoritative Last to Die Rage meter/state. These fields are kept on
    // every player record so the local HUD does not depend on client-side
    // damage prediction.
    float RageCharge = 0f,
    bool IsRageReady = false,
    int RageTicksRemaining = 0,
    // Primary weapon timing is authoritative just like the compact offhand
    // timing above.  Keeping it on the protocol-64 baseline is required for
    // client prediction reconciliation: without it a rebuild seeds a weapon
    // from a stale ready state and the render animation briefly starts then
    // snaps back to idle/reload.
    int PrimaryCooldownTicks = 0,
    int PrimaryReloadTicks = 0,
    int BuffBannerChargeDamage = 0,
    int BuffBannerDeployTicksRemaining = 0,
    int BuffBannerActiveTicksRemaining = 0);

public sealed record Protocol64PlayerStateBatch(
    ulong StateSequence,
    uint StateTick,
    IReadOnlyList<Protocol64PlayerState> Players);

/// <summary>
/// A complete roster view for the event's scope. Removal is explicit and also
/// carries the generation so a late event cannot remove a replacement player.
/// </summary>
public sealed record Protocol64RosterState(
    ulong StateSequence,
    uint StateTick,
    IReadOnlyList<Protocol64PlayerIdentity> Players,
    IReadOnlyList<Protocol64PlayerIdentity> RemovedPlayers);

public sealed record Protocol64ProjectileState(
    ulong EntityId,
    uint Generation,
    Protocol64ProjectileKind EntityKind,
    uint StateTick,
    ushort OwnerSlot,
    uint OwnerGeneration,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    float Rotation,
    bool IsActive,
    uint RemainingLifetimeTicks,
    float Damage,
    bool IsCritical = false,
    byte LastToDieSpyRevolverProfile = 0,
    bool AppliesLastToDieLuckyStrikeStun = false,
    float ArrowFakeSpeedMultiplier = 1f,
    bool IsArrowLanded = false,
    bool AppliesLastToDieGuardian = false,
    bool PiercesPlayers = false,
    bool AppliesLastToDieTranqDarts = false,
    float LastToDiePoisonDamagePerSecond = 0f,
    float LastToDieGhostDamageMultiplier = 1f,
    bool AppliesLastToDieDecapitator = false,
    bool IsLastToDieDecapitatorFullyCharged = false,
    byte LastToDieAttachedHeadClassId = 0,
    byte LastToDieAttachedHeadTeam = 0,
    bool AppliesLastToDieExplosiveTip = false,
    byte LastToDieMedicKritzM2Payload = 0,
    int LastToDieMedicJavelinOwnerPlayerId = 0,
    byte LastToDieMedicJavelinTeam = 0,
    bool IsLastToDieMedicJavelinAnchored = false,
    ushort LastToDieMedicJavelinFuseTicksRemaining = 0,
    bool HasLastToDieMedicJavelinExploded = false,
    float CriticalDamageMultiplier = 1f,
    float PlayerKnockbackImpulse = 0f,
    float PlayerKnockbackAirborneVerticalScale = 1f,
    float PlayerKnockbackGroundedVerticalScale = 1f);

public sealed record Protocol64ProjectileIdentity(
    ulong EntityId,
    uint Generation,
    Protocol64ProjectileKind EntityKind);

/// <summary>
/// Lifecycle records repeat the complete identity and current state needed to
/// apply a spawn/replacement atomically. Despawn records keep the same identity
/// fields while their motion values are ignored by the receiver.
/// </summary>
public sealed record Protocol64ProjectileLifecycle(
    Protocol64ProjectileLifecycleKind Lifecycle,
    ulong EntityId,
    uint Generation,
    Protocol64ProjectileKind EntityKind,
    uint StateTick,
    ushort OwnerSlot,
    uint OwnerGeneration,
    float X,
    float Y,
    float VelocityX,
    float VelocityY,
    float Rotation,
    bool IsActive,
    uint RemainingLifetimeTicks,
    float Damage,
    bool IsCritical = false,
    byte LastToDieSpyRevolverProfile = 0,
    bool AppliesLastToDieLuckyStrikeStun = false,
    float ArrowFakeSpeedMultiplier = 1f,
    bool IsArrowLanded = false,
    bool AppliesLastToDieGuardian = false,
    bool PiercesPlayers = false,
    bool AppliesLastToDieTranqDarts = false,
    float LastToDiePoisonDamagePerSecond = 0f,
    float LastToDieGhostDamageMultiplier = 1f,
    bool AppliesLastToDieDecapitator = false,
    bool IsLastToDieDecapitatorFullyCharged = false,
    byte LastToDieAttachedHeadClassId = 0,
    byte LastToDieAttachedHeadTeam = 0,
    bool AppliesLastToDieExplosiveTip = false,
    byte LastToDieMedicKritzM2Payload = 0,
    int LastToDieMedicJavelinOwnerPlayerId = 0,
    byte LastToDieMedicJavelinTeam = 0,
    bool IsLastToDieMedicJavelinAnchored = false,
    ushort LastToDieMedicJavelinFuseTicksRemaining = 0,
    bool HasLastToDieMedicJavelinExploded = false,
    float CriticalDamageMultiplier = 1f,
    float PlayerKnockbackImpulse = 0f,
    float PlayerKnockbackAirborneVerticalScale = 1f,
    float PlayerKnockbackGroundedVerticalScale = 1f);

public sealed record Protocol64StateResyncRequest(
    ulong RequestId,
    ulong LastPlayerStateSequence,
    ulong LastProjectileStateSequence,
    uint LastStateTick,
    Protocol64StateResyncReason Reason);

/// <summary>
/// A complete replacement view. It is intentionally usable without any prior
/// player/projectile baseline; removed identities make replacement explicit.
/// </summary>
public sealed record Protocol64StateResyncResponse(
    ulong RequestId,
    ulong StateSequence,
    uint StateTick,
    IReadOnlyList<Protocol64PlayerState> Players,
    IReadOnlyList<Protocol64PlayerIdentity> RemovedPlayers,
    IReadOnlyList<Protocol64ProjectileState> Projectiles,
    IReadOnlyList<Protocol64ProjectileIdentity> RemovedProjectiles);

[ReliableUnordered(ChannelType.State)]
public sealed class Protocol64PlayerStateBatchSchema
    : Protocol64EventSchema<Protocol64PlayerStateBatch>
{
    public const int MaxBodyBytes = 64 * 1024;

    public Protocol64PlayerStateBatchSchema()
        : base(Protocol64StateSchemaIds.PlayerStateBatch, 24, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }

    public override void WriteBody(Protocol64PlayerStateBatch value, BinaryWriter writer)
    {
        Protocol64StateValidation.Validate(value);
        writer.Write(value.StateSequence);
        writer.Write(value.StateTick);
        Protocol64StateBinary.WriteCount(writer, value.Players, Protocol64StateValidation.MaxPlayers);
        foreach (var player in value.Players)
        {
            Protocol64StateBinary.WritePlayer(writer, player);
        }
    }

    public override Protocol64PlayerStateBatch ReadBody(BinaryReader reader)
    {
        var sequence = reader.ReadUInt64();
        var tick = reader.ReadUInt32();
        var players = Protocol64StateBinary.ReadList(reader, Protocol64StateValidation.MaxPlayers, Protocol64StateBinary.ReadPlayer);
        return new Protocol64PlayerStateBatch(sequence, tick, players);
    }

    public override void Validate(Protocol64PlayerStateBatch value)
        => Protocol64StateValidation.Validate(value);
}

[ReliableUnordered(ChannelType.State)]
public sealed class Protocol64RosterStateSchema
    : Protocol64EventSchema<Protocol64RosterState>
{
    public const int MaxBodyBytes = 16 * 1024;

    public Protocol64RosterStateSchema()
        : base(Protocol64StateSchemaIds.RosterState, 1, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }

    public override void WriteBody(Protocol64RosterState value, BinaryWriter writer)
    {
        Protocol64StateValidation.Validate(value);
        writer.Write(value.StateSequence);
        writer.Write(value.StateTick);
        Protocol64StateBinary.WriteCount(writer, value.Players, Protocol64StateValidation.MaxPlayers);
        foreach (var player in value.Players)
        {
            Protocol64StateBinary.WritePlayerIdentity(writer, player);
        }

        Protocol64StateBinary.WriteCount(writer, value.RemovedPlayers, Protocol64StateValidation.MaxPlayers);
        foreach (var player in value.RemovedPlayers)
        {
            Protocol64StateBinary.WritePlayerIdentity(writer, player);
        }
    }

    public override Protocol64RosterState ReadBody(BinaryReader reader)
    {
        var sequence = reader.ReadUInt64();
        var tick = reader.ReadUInt32();
        var players = Protocol64StateBinary.ReadList(reader, Protocol64StateValidation.MaxPlayers, Protocol64StateBinary.ReadPlayerIdentity);
        var removed = Protocol64StateBinary.ReadList(reader, Protocol64StateValidation.MaxPlayers, Protocol64StateBinary.ReadPlayerIdentity);
        return new Protocol64RosterState(sequence, tick, players, removed);
    }

    public override void Validate(Protocol64RosterState value)
        => Protocol64StateValidation.Validate(value);
}

[LastWins(ChannelType.State)]
public sealed class Protocol64ProjectileStateSchema
    : Protocol64EventSchema<Protocol64ProjectileState>
{
    public const int MaxBodyBytes = 128;

    public Protocol64ProjectileStateSchema()
        : base(Protocol64StateSchemaIds.ProjectileState, 11, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }

    public override void WriteBody(Protocol64ProjectileState value, BinaryWriter writer)
    {
        Protocol64StateValidation.Validate(value);
        Protocol64StateBinary.WriteProjectileState(writer, value);
    }

    public override Protocol64ProjectileState ReadBody(BinaryReader reader)
        => Protocol64StateBinary.ReadProjectileState(reader);

    public override void Validate(Protocol64ProjectileState value)
        => Protocol64StateValidation.Validate(value);
}

[ReliableUnordered(ChannelType.GameplayEvents)]
public sealed class Protocol64ProjectileLifecycleSchema
    : Protocol64EventSchema<Protocol64ProjectileLifecycle>
{
    public const int MaxBodyBytes = 160;

    public Protocol64ProjectileLifecycleSchema()
        : base(Protocol64StateSchemaIds.ProjectileLifecycle, 11, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }

    public override void WriteBody(Protocol64ProjectileLifecycle value, BinaryWriter writer)
    {
        Protocol64StateValidation.Validate(value);
        Protocol64StateBinary.WriteProjectileLifecycle(writer, value);
    }

    public override Protocol64ProjectileLifecycle ReadBody(BinaryReader reader)
        => Protocol64StateBinary.ReadProjectileLifecycle(reader);

    public override void Validate(Protocol64ProjectileLifecycle value)
        => Protocol64StateValidation.Validate(value);
}

[ReliableOrdered(ChannelType.Control)]
public sealed class Protocol64StateResyncRequestSchema
    : Protocol64EventSchema<Protocol64StateResyncRequest>
{
    public const int MaxBodyBytes = 64;

    public Protocol64StateResyncRequestSchema()
        : base(Protocol64StateSchemaIds.StateResyncRequest, 1, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }

    public override void WriteBody(Protocol64StateResyncRequest value, BinaryWriter writer)
    {
        Protocol64StateValidation.Validate(value);
        writer.Write(value.RequestId);
        writer.Write(value.LastPlayerStateSequence);
        writer.Write(value.LastProjectileStateSequence);
        writer.Write(value.LastStateTick);
        writer.Write((byte)value.Reason);
    }

    public override Protocol64StateResyncRequest ReadBody(BinaryReader reader)
        => new(
            reader.ReadUInt64(),
            reader.ReadUInt64(),
            reader.ReadUInt64(),
            reader.ReadUInt32(),
            (Protocol64StateResyncReason)reader.ReadByte());

    public override void Validate(Protocol64StateResyncRequest value)
        => Protocol64StateValidation.Validate(value);
}

[ReliableOrdered(ChannelType.Control)]
public sealed class Protocol64StateResyncResponseSchema
    : Protocol64EventSchema<Protocol64StateResyncResponse>
{
    public const int MaxBodyBytes = 256 * 1024;

    public Protocol64StateResyncResponseSchema()
        : base(Protocol64StateSchemaIds.StateResyncResponse, 27, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }

    public override void WriteBody(Protocol64StateResyncResponse value, BinaryWriter writer)
    {
        Protocol64StateValidation.Validate(value);
        writer.Write(value.RequestId);
        writer.Write(value.StateSequence);
        writer.Write(value.StateTick);

        Protocol64StateBinary.WriteCount(writer, value.Players, Protocol64StateValidation.MaxPlayers);
        foreach (var player in value.Players)
        {
            Protocol64StateBinary.WritePlayer(writer, player);
        }

        Protocol64StateBinary.WriteCount(writer, value.RemovedPlayers, Protocol64StateValidation.MaxPlayers);
        foreach (var player in value.RemovedPlayers)
        {
            Protocol64StateBinary.WritePlayerIdentity(writer, player);
        }

        Protocol64StateBinary.WriteCount(writer, value.Projectiles, Protocol64StateValidation.MaxProjectiles);
        foreach (var projectile in value.Projectiles)
        {
            Protocol64StateBinary.WriteProjectileState(writer, projectile);
        }

        Protocol64StateBinary.WriteCount(writer, value.RemovedProjectiles, Protocol64StateValidation.MaxProjectiles);
        foreach (var projectile in value.RemovedProjectiles)
        {
            Protocol64StateBinary.WriteProjectileIdentity(writer, projectile);
        }
    }

    public override Protocol64StateResyncResponse ReadBody(BinaryReader reader)
    {
        var requestId = reader.ReadUInt64();
        var sequence = reader.ReadUInt64();
        var tick = reader.ReadUInt32();
        var players = Protocol64StateBinary.ReadList(reader, Protocol64StateValidation.MaxPlayers, Protocol64StateBinary.ReadPlayer);
        var removedPlayers = Protocol64StateBinary.ReadList(reader, Protocol64StateValidation.MaxPlayers, Protocol64StateBinary.ReadPlayerIdentity);
        var projectiles = Protocol64StateBinary.ReadList(reader, Protocol64StateValidation.MaxProjectiles, Protocol64StateBinary.ReadProjectileState);
        var removedProjectiles = Protocol64StateBinary.ReadList(reader, Protocol64StateValidation.MaxProjectiles, Protocol64StateBinary.ReadProjectileIdentity);
        return new Protocol64StateResyncResponse(requestId, sequence, tick, players, removedPlayers, projectiles, removedProjectiles);
    }

    public override void Validate(Protocol64StateResyncResponse value)
        => Protocol64StateValidation.Validate(value);
}

internal static class Protocol64StateValidation
{
    public const int MaxPlayers = 64;
    public const int MaxProjectiles = 512;
    public const int MaxGameplayClassIdBytes = 64;

    public static void Validate(Protocol64PlayerIdentity value)
    {
        if (value is null)
        {
            throw new Protocol64SchemaValidationException("Player identity cannot be null.");
        }

        if (value.PlayerId == 0 || value.Generation == 0)
        {
            throw new Protocol64SchemaValidationException("Player ID and generation must be non-zero.");
        }

        if (value.Slot >= MaxPlayers)
        {
            throw new Protocol64SchemaValidationException($"Player slot must be less than {MaxPlayers}.");
        }
    }

    public static void Validate(Protocol64PlayerState value)
    {
        if (value is null)
        {
            throw new Protocol64SchemaValidationException("Player state cannot be null.");
        }

        Validate(new Protocol64PlayerIdentity(value.Slot, value.PlayerId, value.Generation));
        ValidateString(value.GameplayClassId, MaxGameplayClassIdBytes, nameof(value.GameplayClassId));
        if (value.MaxHealth <= 0 || value.Health < 0 || value.Health > value.MaxHealth)
        {
            throw new Protocol64SchemaValidationException("Player health must be within 0 and positive max health.");
        }

        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) ||
            !float.IsFinite(value.VelocityX) || !float.IsFinite(value.VelocityY)
            || !float.IsFinite(value.SpyCloakAlpha)
            || value.SpyCloakAlpha < 0f
            || value.SpyCloakAlpha > 1f)
        {
            throw new Protocol64SchemaValidationException(
                "Player position, velocity, and Spy cloak alpha must be finite and valid.");
        }

        if (value.Team > 2)
        {
            throw new Protocol64SchemaValidationException("Player team is outside the protocol range.");
        }

        if (value.RemainingAirJumps < 0)
        {
            throw new Protocol64SchemaValidationException("Player remaining air jumps cannot be negative.");
        }

        if (!float.IsFinite(value.RageCharge)
            || value.RageCharge < 0f
            || value.RageTicksRemaining < 0)
        {
            throw new Protocol64SchemaValidationException("Player Rage state must be finite and non-negative.");
        }

        if (value.CurrentAmmo < 0
            || value.MaxAmmo < 0
            || value.PrimaryCooldownTicks < 0
            || value.PrimaryReloadTicks < 0
            || value.OffhandAmmo < 0
            || value.OffhandMaxAmmo < 0
            || value.OffhandCooldownTicks < 0
            || value.OffhandReloadTicks < 0
            || value.AcquiredAmmo < 0
            || value.AcquiredMaxAmmo < 0
            || value.AcquiredCooldownTicks < 0
            || value.AcquiredReloadTicks < 0
            || value.MedicNeedleCooldownTicks < 0
            || value.MedicNeedleRefillTicks < 0
            || value.PyroPrimaryFuelScaled < 0
            || value.CurrentAmmo > value.MaxAmmo
            || value.OffhandAmmo > value.OffhandMaxAmmo
            || value.AcquiredAmmo > value.AcquiredMaxAmmo)
        {
            throw new Protocol64SchemaValidationException("Player weapon ammunition or timing values are invalid.");
        }

        ValidateLastToDieWeaponState(
            value.GameplayClassId,
            value.LastToDieSpyRevolverState);
        ValidateLastToDieSniperRuntimeState(
            value.GameplayClassId,
            value.LastToDieSpyRevolverState,
            value.LastToDieSniperRuntimeState);
        ValidateLastToDieSniperExtensionState(
            value.GameplayClassId,
            value.LastToDieSniperExtensionState);
        ValidateLastToDieSpyInfiltrateState(
            value.GameplayClassId,
            value.LastToDieSpyInfiltrateState);
        ValidateLastToDieSpyAfterlifeState(
            value.GameplayClassId,
            value.LastToDieSpyAfterlifeState);
        ValidateLastToDieSniperVolleyState(
            value.GameplayClassId,
            value.LastToDieSniperVolleyState);
        ValidateMedicUberDeliveryState(value);
        if ((value.LastToDieMedicLinkState & ~0b1111) != 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Medic link state contains unknown bits.");
        }
        if (value.LastToDieSpyRogueRampStacks > 10)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Spy Rogue Commander ramp stacks cannot exceed 10.");
        }

        if (value.LastToDieProfessionalFireChordState > 2
            || (value.LastToDieProfessionalFireChordState != 0
                && (!string.Equals(value.GameplayClassId, "spy", StringComparison.Ordinal)
                    || !value.IsSpyCloaked)))
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Professional fire-chord state is invalid.");
        }

        if (!float.IsFinite(value.SpySuperjumpHorizontalVelocity)
            || !float.IsFinite(value.SpySuperjumpChargeDirectionDegrees)
            || value.SpySuperjumpMaximumCharges < 1
            || value.SpySuperjumpMaximumCharges > 2
            || value.SpySuperjumpAvailableCharges > value.SpySuperjumpMaximumCharges)
        {
            throw new Protocol64SchemaValidationException(
                "Spy jump-boot velocity, direction, and charge state are invalid.");
        }

        if (value.KritzCritBoostTicksRemaining < 0
            || value.KritzCritBoostProviderPlayerId < 0
            || value.KritzCritBoostProviderSlot < 0
            || !float.IsFinite(value.KritzCritBoostDamageMultiplier)
            || (value.KritzCritBoostTicksRemaining == 0
                && (value.KritzCritBoostProviderPlayerId != 0
                    || value.KritzCritBoostProviderSlot != int.MaxValue
                    || value.KritzCritBoostDamageMultiplier != 1f))
            || (value.KritzCritBoostTicksRemaining > 0
                && value.KritzCritBoostDamageMultiplier <= 1f))
        {
            throw new Protocol64SchemaValidationException(
                "Kritz critical boost source, lifetime, or multiplier is invalid.");
        }

        if (!float.IsFinite(value.DispenserAttackReloadSpeedMultiplier)
            || value.DispenserAttackReloadSpeedMultiplier < 1f
            || (!value.IsDispenserBuffed && value.DispenserAttackReloadSpeedMultiplier != 1f))
        {
            throw new Protocol64SchemaValidationException(
                "Dispenser buff state or multiplier is invalid.");
        }

        if (value.BuffBannerChargeDamage < 0
            || value.BuffBannerDeployTicksRemaining < 0
            || value.BuffBannerActiveTicksRemaining < 0
            || (value.BuffBannerDeployTicksRemaining > 0 && value.BuffBannerActiveTicksRemaining > 0))
        {
            throw new Protocol64SchemaValidationException(
                "Buff Banner charge, deployment, or active state is invalid.");
        }
    }

    private static void ValidateMedicUberDeliveryState(Protocol64PlayerState value)
    {
        const byte knownFlags = 0x83;
        var mode = value.MedicUberDeliveryState & 0x03;
        var active = (value.MedicUberDeliveryState & 0x80) != 0;
        if ((value.MedicUberDeliveryState & ~knownFlags) != 0
            || (active && mode == 0)
            || value.MedicHealTargetId < -1
            || !float.IsFinite(value.MedicUberCharge)
            || value.MedicUberCharge < 0f
            || value.MedicUberCharge > 2000f)
        {
            throw new Protocol64SchemaValidationException(
                "Medic Uber delivery state, target, or meter is invalid.");
        }

        if (!string.Equals(value.GameplayClassId, "medic", StringComparison.Ordinal)
            && (value.MedicUberDeliveryState != 0
                || value.MedicHealTargetId != -1
                || value.MedicUberCharge != 0f))
        {
            throw new Protocol64SchemaValidationException(
                "Only Medic players can carry Medic Uber runtime state.");
        }
    }

    public static void Validate(Protocol64PlayerStateBatch value)
    {
        if (value is null || value.StateSequence == 0)
        {
            throw new Protocol64SchemaValidationException("Player state batch sequence must be non-zero.");
        }

        ValidateCount(value.Players, MaxPlayers, nameof(value.Players));
        var identities = new HashSet<(ushort Slot, ulong PlayerId, uint Generation)>();
        var slots = new HashSet<ushort>();
        foreach (var player in value.Players)
        {
            Validate(player);
            if (!identities.Add((player.Slot, player.PlayerId, player.Generation))
                || !slots.Add(player.Slot))
            {
                throw new Protocol64SchemaValidationException("Player state batch contains duplicate player slots or identities.");
            }
        }
    }

    public static void Validate(Protocol64RosterState value)
    {
        if (value is null || value.StateSequence == 0)
        {
            throw new Protocol64SchemaValidationException("Roster state sequence must be non-zero.");
        }

        ValidateIdentities(value.Players, nameof(value.Players));
        ValidateIdentities(value.RemovedPlayers, nameof(value.RemovedPlayers));
        var slots = new HashSet<ushort>();
        foreach (var player in value.Players)
        {
            if (!slots.Add(player.Slot))
            {
                throw new Protocol64SchemaValidationException("Roster state contains duplicate player slots.");
            }
        }

        foreach (var removedPlayer in value.RemovedPlayers)
        {
            if (!slots.Add(removedPlayer.Slot))
            {
                throw new Protocol64SchemaValidationException("Roster state contains duplicate player slots.");
            }
        }
    }

    public static void Validate(Protocol64ProjectileState value)
    {
        if (value is null)
        {
            throw new Protocol64SchemaValidationException("Projectile state cannot be null.");
        }

        ValidateProjectileIdentity(value.EntityId, value.Generation, value.EntityKind);
        ValidateOwner(
            value.OwnerSlot,
            value.OwnerGeneration,
            allowMissing: value.LastToDieMedicJavelinOwnerPlayerId > 0);
        ValidateProjectileMotion(value.X, value.Y, value.VelocityX, value.VelocityY, value.Rotation);
        if (!float.IsFinite(value.Damage) || value.Damage < 0f)
        {
            throw new Protocol64SchemaValidationException("Projectile damage must be finite and non-negative.");
        }
        ValidatePlayerKnockbackPayload(
            value.PlayerKnockbackImpulse,
            value.PlayerKnockbackAirborneVerticalScale,
            value.PlayerKnockbackGroundedVerticalScale);
        ValidateCriticalDamageMultiplier(value.IsCritical, value.CriticalDamageMultiplier);

        ValidateLastToDieSpyRevolverProjectilePayload(
            value.EntityKind,
            value.LastToDieSpyRevolverProfile,
            value.AppliesLastToDieLuckyStrikeStun);
        ValidateLastToDieMedicKritzM2Payload(
            value.EntityKind,
            value.LastToDieMedicKritzM2Payload,
            value.LastToDieMedicJavelinOwnerPlayerId,
            value.LastToDieMedicJavelinTeam,
            value.IsLastToDieMedicJavelinAnchored,
            value.LastToDieMedicJavelinFuseTicksRemaining,
            value.HasLastToDieMedicJavelinExploded);
        ValidateArrowPayload(
            value.EntityKind,
            value.ArrowFakeSpeedMultiplier,
            value.IsArrowLanded,
            value.AppliesLastToDieGuardian,
            value.PiercesPlayers,
            value.AppliesLastToDieTranqDarts,
            value.LastToDiePoisonDamagePerSecond,
            value.LastToDieGhostDamageMultiplier,
            value.AppliesLastToDieDecapitator,
            value.IsLastToDieDecapitatorFullyCharged,
            value.LastToDieAttachedHeadClassId,
            value.LastToDieAttachedHeadTeam,
            value.AppliesLastToDieExplosiveTip);
    }

    public static void Validate(Protocol64ProjectileLifecycle value)
    {
        if (value is null || !Enum.IsDefined(value.Lifecycle))
        {
            throw new Protocol64SchemaValidationException("Projectile lifecycle kind is invalid.");
        }

        ValidateProjectileIdentity(value.EntityId, value.Generation, value.EntityKind);
        ValidateOwner(
            value.OwnerSlot,
            value.OwnerGeneration,
            allowMissing: value.LastToDieMedicJavelinOwnerPlayerId > 0);
        ValidateProjectileMotion(value.X, value.Y, value.VelocityX, value.VelocityY, value.Rotation);
        if (!float.IsFinite(value.Damage) || value.Damage < 0f)
        {
            throw new Protocol64SchemaValidationException("Projectile damage must be finite and non-negative.");
        }
        ValidatePlayerKnockbackPayload(
            value.PlayerKnockbackImpulse,
            value.PlayerKnockbackAirborneVerticalScale,
            value.PlayerKnockbackGroundedVerticalScale);
        ValidateCriticalDamageMultiplier(value.IsCritical, value.CriticalDamageMultiplier);

        ValidateLastToDieSpyRevolverProjectilePayload(
            value.EntityKind,
            value.LastToDieSpyRevolverProfile,
            value.AppliesLastToDieLuckyStrikeStun);
        ValidateLastToDieMedicKritzM2Payload(
            value.EntityKind,
            value.LastToDieMedicKritzM2Payload,
            value.LastToDieMedicJavelinOwnerPlayerId,
            value.LastToDieMedicJavelinTeam,
            value.IsLastToDieMedicJavelinAnchored,
            value.LastToDieMedicJavelinFuseTicksRemaining,
            value.HasLastToDieMedicJavelinExploded);
        ValidateArrowPayload(
            value.EntityKind,
            value.ArrowFakeSpeedMultiplier,
            value.IsArrowLanded,
            value.AppliesLastToDieGuardian,
            value.PiercesPlayers,
            value.AppliesLastToDieTranqDarts,
            value.LastToDiePoisonDamagePerSecond,
            value.LastToDieGhostDamageMultiplier,
            value.AppliesLastToDieDecapitator,
            value.IsLastToDieDecapitatorFullyCharged,
            value.LastToDieAttachedHeadClassId,
            value.LastToDieAttachedHeadTeam,
            value.AppliesLastToDieExplosiveTip);
    }

    public static void Validate(Protocol64StateResyncRequest value)
    {
        if (value is null || value.RequestId == 0 || !Enum.IsDefined(value.Reason))
        {
            throw new Protocol64SchemaValidationException("State resync request ID or reason is invalid.");
        }
    }

    private static void ValidateCriticalDamageMultiplier(bool isCritical, float multiplier)
    {
        if (!float.IsFinite(multiplier)
            || (isCritical ? multiplier < 1f : multiplier != 1f))
        {
            throw new Protocol64SchemaValidationException(
                "Projectile critical multiplier is inconsistent with its critical flag.");
        }
    }

    private static void ValidatePlayerKnockbackPayload(
        float impulse,
        float airborneVerticalScale,
        float groundedVerticalScale)
    {
        if (!float.IsFinite(impulse) || impulse < 0f)
        {
            throw new Protocol64SchemaValidationException(
                "Projectile player knockback impulse must be finite and non-negative.");
        }

        if (!float.IsFinite(airborneVerticalScale)
            || airborneVerticalScale is < 0f or > 1f
            || !float.IsFinite(groundedVerticalScale)
            || groundedVerticalScale is < 0f or > 1f)
        {
            throw new Protocol64SchemaValidationException(
                "Projectile player knockback vertical scales must be between zero and one.");
        }
    }

    public static void Validate(Protocol64StateResyncResponse value)
    {
        if (value is null || value.RequestId == 0 || value.StateSequence == 0)
        {
            throw new Protocol64SchemaValidationException("State resync response identifiers must be non-zero.");
        }

        ValidateCount(value.Players, MaxPlayers, nameof(value.Players));
        ValidateCount(value.RemovedPlayers, MaxPlayers, nameof(value.RemovedPlayers));
        ValidateCount(value.Projectiles, MaxProjectiles, nameof(value.Projectiles));
        ValidateCount(value.RemovedProjectiles, MaxProjectiles, nameof(value.RemovedProjectiles));
        var slots = new HashSet<ushort>();
        foreach (var player in value.Players)
        {
            Validate(player);
            if (!slots.Add(player.Slot))
            {
                throw new Protocol64SchemaValidationException("State resync contains duplicate player slots.");
            }
        }
        ValidateIdentities(value.RemovedPlayers, nameof(value.RemovedPlayers));
        foreach (var projectile in value.Projectiles) Validate(projectile);
        foreach (var projectile in value.RemovedProjectiles) Validate(projectile);
    }

    private static void ValidateIdentities(IReadOnlyList<Protocol64PlayerIdentity> values, string name)
    {
        ValidateCount(values, MaxPlayers, name);
        var identities = new HashSet<(ushort Slot, ulong PlayerId, uint Generation)>();
        foreach (var value in values)
        {
            Validate(value);
            if (!identities.Add((value.Slot, value.PlayerId, value.Generation)))
            {
                throw new Protocol64SchemaValidationException($"{name} contains duplicate identities.");
            }
        }
    }

    private static void ValidateCount<T>(IReadOnlyList<T>? values, int maximum, string name)
    {
        if (values is null || values.Count > maximum)
        {
            throw new Protocol64SchemaValidationException($"{name} exceeds the maximum count of {maximum}.");
        }
    }

    private static void ValidateProjectileIdentity(ulong entityId, uint generation, Protocol64ProjectileKind kind)
    {
        if (entityId == 0 || generation == 0 || !Enum.IsDefined(kind))
        {
            throw new Protocol64SchemaValidationException("Projectile ID, generation, or entity kind is invalid.");
        }
    }

    public static void Validate(Protocol64ProjectileIdentity value)
    {
        if (value is null)
        {
            throw new Protocol64SchemaValidationException("Projectile identity cannot be null.");
        }

        ValidateProjectileIdentity(value.EntityId, value.Generation, value.EntityKind);
    }

    private static void ValidateOwner(
        ushort slot,
        uint generation,
        bool allowMissing = false)
    {
        if (slot >= MaxPlayers
            || (generation == 0 && !(allowMissing && slot == 0)))
        {
            throw new Protocol64SchemaValidationException("Projectile owner identity is invalid.");
        }
    }

    private static void ValidateProjectileMotion(float x, float y, float velocityX, float velocityY, float rotation)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(velocityX) ||
            !float.IsFinite(velocityY) || !float.IsFinite(rotation))
        {
            throw new Protocol64SchemaValidationException("Projectile motion must be finite.");
        }
    }

    private static void ValidateLastToDieWeaponState(
        string gameplayClassId,
        ushort encoded)
    {
        if (string.Equals(gameplayClassId, "sniper", StringComparison.Ordinal))
        {
            const ushort KnownSniperBits = 0b1111_1111_1111_0000;
            if ((encoded & ~KnownSniperBits) != 0)
            {
                throw new Protocol64SchemaValidationException(
                    "Last to Die Sniper weapon state contains unknown bits.");
            }

            return;
        }

        ValidateLastToDieSpyRevolverState(encoded);
    }

    private static void ValidateLastToDieSniperRuntimeState(
        string gameplayClassId,
        ushort encodedProfile,
        ushort encodedRuntime)
    {
        const ushort RuntimeKnownBits = 0x3fff;
        const ushort MarkedTargetSlotMask = 0x007f;
        const int ConquistadorStackShift = 7;
        const ushort ConquistadorStackMask = 0x007f;
        const ushort SpottedProfileBit = 1 << 12;
        const ushort ConquistadorProfileBit = 1 << 13;
        if ((encodedRuntime & ~RuntimeKnownBits) != 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Sniper runtime state contains unknown bits.");
        }

        if (!string.Equals(gameplayClassId, "sniper", StringComparison.Ordinal))
        {
            if (encodedRuntime != 0)
            {
                throw new Protocol64SchemaValidationException(
                    "Only Snipers may carry Last to Die Sniper runtime state.");
            }

            return;
        }

        var markedTargetSlot = encodedRuntime & MarkedTargetSlotMask;
        var conquistadorStacks = (encodedRuntime >> ConquistadorStackShift)
            & ConquistadorStackMask;
        if (markedTargetSlot > 40
            || (markedTargetSlot > 0 && (encodedProfile & SpottedProfileBit) == 0))
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Spotted target slot is invalid or the perk is not active.");
        }

        if (conquistadorStacks > 100
            || (conquistadorStacks > 0 && (encodedProfile & ConquistadorProfileBit) == 0))
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Conquistador stacks are invalid or the perk is not active.");
        }
    }

    private static void ValidateLastToDieSpyRevolverState(ushort encoded)
    {
        const ushort KnownBits = 0b11_1111_1111;
        const ushort LuckyStrikeProfileBit = 1 << 7;
        const int LuckyStrikeProgressShift = 8;
        const int MaximumLuckyStrikeProgress = 2;
        if ((encoded & ~KnownBits) != 0)
        {
            throw new Protocol64SchemaValidationException("Last to Die Spy revolver state contains unknown bits.");
        }

        var progress = encoded >> LuckyStrikeProgressShift;
        if (progress > MaximumLuckyStrikeProgress
            || (progress > 0 && (encoded & LuckyStrikeProfileBit) == 0))
        {
            throw new Protocol64SchemaValidationException("Last to Die Lucky Strike progress is invalid.");
        }

        ValidateLastToDieSpyRevolverProfile((byte)encoded);
    }

    private static void ValidateLastToDieSniperExtensionState(
        string gameplayClassId,
        ushort encoded)
    {
        const ushort KnownBits = 0x7fff;
        const ushort GhostProfileBit = 1 << 0;
        const ushort GhostRuntimeMask = 0x0ffc;
        if ((encoded & ~KnownBits) != 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Sniper extension state contains unknown bits.");
        }

        if (!string.Equals(gameplayClassId, "sniper", StringComparison.Ordinal))
        {
            if (encoded != 0)
            {
                throw new Protocol64SchemaValidationException(
                    "Only Snipers may carry Last to Die Sniper extension state.");
            }

            return;
        }

        if ((encoded & GhostRuntimeMask) != 0 && (encoded & GhostProfileBit) == 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Ghost runtime requires the Ghost profile bit.");
        }
    }

    private static void ValidateLastToDieSpyInfiltrateState(
        string gameplayClassId,
        uint encoded)
    {
        const uint KnownBits = 0x01ff_ffffu;
        const uint CooldownMask = 0xffffu;
        const int DashTicksShift = 16;
        const uint DashTicksMask = 0xffu;
        const uint LeftDirectionFlag = 1u << 24;
        if ((encoded & ~KnownBits) != 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Spy Infiltrate state contains unknown bits.");
        }

        if (!string.Equals(gameplayClassId, "spy", StringComparison.Ordinal))
        {
            if (encoded != 0)
            {
                throw new Protocol64SchemaValidationException(
                    "Only Spies may carry Last to Die Spy Infiltrate state.");
            }

            return;
        }

        var cooldownTicks = encoded & CooldownMask;
        var dashTicks = (encoded >> DashTicksShift) & DashTicksMask;
        if (dashTicks > cooldownTicks
            || (dashTicks == 0 && (encoded & LeftDirectionFlag) != 0))
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Spy Infiltrate timers or direction are invalid.");
        }
    }

    private static void ValidateLastToDieSpyAfterlifeState(
        string gameplayClassId,
        uint encoded)
    {
        if (!string.Equals(gameplayClassId, "spy", StringComparison.Ordinal))
        {
            if (encoded != 0)
            {
                throw new Protocol64SchemaValidationException(
                    "Only Spies may carry Last to Die Spy Afterlife state.");
            }

            return;
        }

        var cooldownTicks = encoded & 0xffffu;
        var windowTicks = encoded >> 16;
        if (windowTicks > cooldownTicks)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Spy Afterlife timers are invalid.");
        }
    }

    private static void ValidateLastToDieSniperVolleyState(
        string gameplayClassId,
        Protocol64LastToDieSniperVolleyState? state)
    {
        if (state is null)
        {
            return;
        }

        if (!string.Equals(gameplayClassId, "sniper", StringComparison.Ordinal)
            || state.QueuedArrowCount > 2
            || state.DueArrowCount > 2
            || state.QueuedArrowCount + state.DueArrowCount is < 1 or > 2
            || (state.QueuedArrowCount > 0 && state.SourceTicksUntilNextArrow is < 1 or > 3)
            || (state.QueuedArrowCount == 0 && state.SourceTicksUntilNextArrow != 0)
            || state.Damage < 0
            || !float.IsFinite(state.VelocityX)
            || !float.IsFinite(state.VelocityY)
            || !float.IsFinite(state.FakeSpeedMultiplier)
            || state.FakeSpeedMultiplier <= 0f
            || !float.IsFinite(state.PoisonDamagePerSecond)
            || state.PoisonDamagePerSecond < 0f
            || !float.IsFinite(state.GhostDamageMultiplier)
            || state.GhostDamageMultiplier is < 1f or > 3f
            || !float.IsFinite(state.CriticalDamageMultiplier)
            || (((state.PayloadFlags & (1 << 6)) != 0)
                ? state.CriticalDamageMultiplier < 1f
                : state.CriticalDamageMultiplier != 1f)
            || (state.PayloadFlags & ~0x7f) != 0
            || ((state.PayloadFlags & (1 << 4)) != 0
                && (state.PayloadFlags & (1 << 3)) == 0))
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Sniper volley state is invalid.");
        }
    }

    private static void ValidateLastToDieSpyRevolverProjectilePayload(
        Protocol64ProjectileKind kind,
        byte profile,
        bool appliesLuckyStrikeStun)
    {
        if (kind != Protocol64ProjectileKind.RevolverShot
            && (profile != 0 || appliesLuckyStrikeStun))
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Spy revolver payload is only valid on revolver projectiles.");
        }

        ValidateLastToDieSpyRevolverProfile(profile);
        const byte LuckyStrikeProfileBit = 1 << 7;
        if (appliesLuckyStrikeStun && (profile & LuckyStrikeProfileBit) == 0)
        {
            throw new Protocol64SchemaValidationException(
                "A Lucky Strike projectile marker requires the Lucky Strike profile bit.");
        }
    }

    private static void ValidateLastToDieMedicKritzM2Payload(
        Protocol64ProjectileKind kind,
        byte encoded,
        int javelinOwnerPlayerId,
        byte javelinTeam,
        bool isJavelinAnchored,
        ushort javelinFuseTicksRemaining,
        bool hasJavelinExploded)
    {
        const byte MedicKritzM2Bit = 1 << 0;
        const byte JavelinBit = 1 << 3;
        const byte KnownBits = 0b1111;
        if ((encoded & ~KnownBits) != 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Medic Kritz M2 payload contains unknown bits.");
        }

        if (encoded != 0 && kind != Protocol64ProjectileKind.Needle)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Medic Kritz M2 payload is only valid on needle projectiles.");
        }

        if ((encoded & ~MedicKritzM2Bit) != 0
            && (encoded & MedicKritzM2Bit) == 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Medic Kritz M2 perk flags require the base Kritz M2 trait.");
        }

        var appliesJavelin = (encoded & JavelinBit) != 0;
        if (!appliesJavelin)
        {
            if (javelinOwnerPlayerId != 0
                || javelinTeam != 0
                || isJavelinAnchored
                || javelinFuseTicksRemaining != 0
                || hasJavelinExploded)
            {
                throw new Protocol64SchemaValidationException(
                    "Last to Die Medic Javelin state requires the Javelin payload bit.");
            }

            return;
        }

        if (javelinOwnerPlayerId <= 0)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Medic Javelin owner player ID must be positive.");
        }

        if (javelinTeam is < 1 or > 2)
        {
            throw new Protocol64SchemaValidationException(
                "Last to Die Medic Javelin team must be Red or Blue.");
        }

        if (!hasJavelinExploded && javelinFuseTicksRemaining == 0)
        {
            throw new Protocol64SchemaValidationException(
                "An unexploded Last to Die Medic Javelin must retain a positive fuse.");
        }
    }

    private static void ValidateLastToDieSpyRevolverProfile(byte encoded)
    {
        const byte BlunderbussRankMask = 0b11;
        const byte AgentProfileBit = 1 << 2;
        const byte RubberBulletsProfileBit = 1 << 3;
        var blunderbussRank = encoded & BlunderbussRankMask;
        if (blunderbussRank > 0
            && (encoded & (AgentProfileBit | RubberBulletsProfileBit)) != 0)
        {
            throw new Protocol64SchemaValidationException(
                "Blunderbuss cannot be combined with Agent or Rubber Bullets.");
        }
    }

    private static void ValidateArrowPayload(
        Protocol64ProjectileKind kind,
        float fakeSpeedMultiplier,
        bool isLanded,
        bool appliesGuardian,
        bool piercesPlayers,
        bool appliesTranqDarts,
        float poisonDamagePerSecond,
        float ghostDamageMultiplier,
        bool appliesDecapitator,
        bool isDecapitatorFullyCharged,
        byte attachedHeadClassId,
        byte attachedHeadTeam,
        bool appliesExplosiveTip)
    {
        if (!float.IsFinite(fakeSpeedMultiplier) || fakeSpeedMultiplier <= 0f)
        {
            throw new Protocol64SchemaValidationException(
                "Arrow fake-speed multiplier must be finite and positive.");
        }

        if (!float.IsFinite(poisonDamagePerSecond) || poisonDamagePerSecond < 0f)
        {
            throw new Protocol64SchemaValidationException(
                "Arrow poison damage must be finite and non-negative.");
        }

        if (!float.IsFinite(ghostDamageMultiplier)
            || ghostDamageMultiplier < 1f
            || ghostDamageMultiplier > 3f)
        {
            throw new Protocol64SchemaValidationException(
                "Arrow Ghost damage multiplier must be finite and between one and three.");
        }

        if (isDecapitatorFullyCharged && !appliesDecapitator)
        {
            throw new Protocol64SchemaValidationException(
                "A fully charged Decapitator marker requires its perk payload.");
        }

        var hasAttachedHead = attachedHeadClassId != 0 || attachedHeadTeam != 0;
        if (hasAttachedHead
            && (!appliesDecapitator
                || !isDecapitatorFullyCharged
                || attachedHeadClassId is < 1 or > 10
                || attachedHeadTeam is < 1 or > 2))
        {
            throw new Protocol64SchemaValidationException(
                "An attached Decapitator head requires valid class, team, perk, and charge payload.");
        }

        if ((attachedHeadClassId == 0) != (attachedHeadTeam == 0))
        {
            throw new Protocol64SchemaValidationException(
                "An attached Decapitator head must carry both class and team.");
        }

        if (kind != Protocol64ProjectileKind.Arrow
            && (fakeSpeedMultiplier != 1f
                || isLanded
                || appliesGuardian
                || piercesPlayers
                || appliesTranqDarts
                || poisonDamagePerSecond != 0f
                || ghostDamageMultiplier != 1f
                || appliesDecapitator
                || isDecapitatorFullyCharged
                || hasAttachedHead
                || appliesExplosiveTip))
        {
            throw new Protocol64SchemaValidationException(
                "Arrow gameplay payload is only valid on arrow projectiles.");
        }
    }

    internal static void ValidateString(string? value, int maximumBytes, string name)
    {
        if (value is null || value.Length == 0 || Encoding.UTF8.GetByteCount(value) > maximumBytes)
        {
            throw new Protocol64SchemaValidationException($"{name} is empty or exceeds {maximumBytes} UTF-8 bytes.");
        }
    }
}

internal static class Protocol64StateBinary
{
    public static void WriteCount<T>(BinaryWriter writer, IReadOnlyList<T> values, int maximum)
    {
        if (values is null || values.Count > maximum || values.Count > ushort.MaxValue)
        {
            throw new Protocol64SchemaValidationException($"Collection count exceeds {maximum}.");
        }

        writer.Write((ushort)values.Count);
    }

    public static List<T> ReadList<T>(BinaryReader reader, int maximum, Func<BinaryReader, T> read)
    {
        var count = reader.ReadUInt16();
        if (count > maximum)
        {
            throw new Protocol64SchemaValidationException($"Collection count exceeds {maximum}.");
        }

        var values = new List<T>(count);
        for (var index = 0; index < count; index++)
        {
            values.Add(read(reader));
        }

        return values;
    }

    public static void WritePlayerIdentity(BinaryWriter writer, Protocol64PlayerIdentity value)
    {
        Protocol64StateValidation.Validate(value);
        writer.Write(value.Slot);
        writer.Write(value.PlayerId);
        writer.Write(value.Generation);
    }

    public static Protocol64PlayerIdentity ReadPlayerIdentity(BinaryReader reader)
        => new(reader.ReadUInt16(), reader.ReadUInt64(), reader.ReadUInt32());

    public static void WriteProjectileIdentity(BinaryWriter writer, Protocol64ProjectileIdentity value)
    {
        Protocol64StateValidation.Validate(value);
        writer.Write(value.EntityId);
        writer.Write(value.Generation);
        writer.Write((byte)value.EntityKind);
    }

    public static Protocol64ProjectileIdentity ReadProjectileIdentity(BinaryReader reader)
        => new(reader.ReadUInt64(), reader.ReadUInt32(), (Protocol64ProjectileKind)reader.ReadByte());

    public static void WritePlayer(BinaryWriter writer, Protocol64PlayerState value)
    {
        writer.Write(value.Slot);
        writer.Write(value.PlayerId);
        writer.Write(value.Generation);
        writer.Write(value.GameplayClassId);
        writer.Write(value.Health);
        writer.Write(value.MaxHealth);
        writer.Write(value.Team);
        writer.Write(value.IsAlive);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.VelocityX);
        writer.Write(value.VelocityY);
        writer.Write(value.ActiveWeapon);
        writer.Write(value.AbilityState);
        writer.Write(value.StateTick);
        writer.Write(value.LastProcessedInputSequence);
        writer.Write(value.IsGrounded);
        writer.Write(value.RemainingAirJumps);
        writer.Write(value.CurrentAmmo);
        writer.Write(value.MaxAmmo);
        writer.Write(value.OffhandAmmo);
        writer.Write(value.OffhandMaxAmmo);
        writer.Write(value.OffhandCooldownTicks);
        writer.Write(value.OffhandReloadTicks);
        writer.Write(value.LastToDieSpyRevolverState);
        writer.Write(value.IsSpyCloaked);
        writer.Write((byte)Math.Clamp(
            (int)MathF.Round(value.SpyCloakAlpha * byte.MaxValue),
            byte.MinValue,
            byte.MaxValue));
        writer.Write(value.LastToDieSpyCloakMeterUnits);
        writer.Write(value.LastToDieSpyRogueRampStacks);
        writer.Write(value.LastToDieSpyRogueRampTicks);
        writer.Write(value.IsSpySuperjumping);
        writer.Write(value.SpySuperjumpHorizontalVelocity);
        writer.Write(value.SpySuperjumpCooldownTicksRemaining);
        writer.Write(value.SpySuperjumpAvailableCharges);
        writer.Write(value.SpySuperjumpMaximumCharges);
        writer.Write(value.SpySuperjumpChargeTicks);
        writer.Write(value.SpySuperjumpChargeDirectionDegrees);
        writer.Write(value.SpySuperjumpChargeStartMovementButtons);
        writer.Write(value.SpySuperjumpChargeStartBlockedUntilAbilityRelease);
        writer.Write(value.LastToDieSniperRuntimeState);
        writer.Write(value.LastToDieMedicLinkState);
        writer.Write(value.AcquiredAmmo);
        writer.Write(value.AcquiredMaxAmmo);
        writer.Write(value.AcquiredCooldownTicks);
        writer.Write(value.AcquiredReloadTicks);
        writer.Write(value.MedicNeedleCooldownTicks);
        writer.Write(value.MedicNeedleRefillTicks);
        writer.Write(value.PyroPrimaryFuelScaled);
        writer.Write(value.LastToDieSniperExtensionState);
        writer.Write(value.LastToDieSpyInfiltrateState);
        writer.Write(value.LastToDieSpyAfterlifeState);
        WriteLastToDieSniperVolleyState(writer, value.LastToDieSniperVolleyState);
        writer.Write(value.MedicUberDeliveryState);
        writer.Write(value.MedicHealTargetId);
        writer.Write(value.MedicUberCharge);
        writer.Write(value.LastToDieMedicHailMaryTicksRemaining);
        writer.Write(value.ServerStunTicksRemaining);
        writer.Write(value.KritzCritBoostTicksRemaining);
        writer.Write(value.KritzCritBoostProviderPlayerId);
        writer.Write(value.KritzCritBoostProviderSlot);
        writer.Write(value.KritzCritBoostDamageMultiplier);
        writer.Write(value.LastToDieProfessionalFireChordState);
        writer.Write(value.IsDispenserBuffed);
        writer.Write(value.DispenserAttackReloadSpeedMultiplier);
        writer.Write(value.RageCharge);
        writer.Write(value.IsRageReady);
        writer.Write(value.RageTicksRemaining);
        writer.Write(value.PrimaryCooldownTicks);
        writer.Write(value.PrimaryReloadTicks);
        writer.Write(value.BuffBannerChargeDamage);
        writer.Write(value.BuffBannerDeployTicksRemaining);
        writer.Write(value.BuffBannerActiveTicksRemaining);
    }

    public static Protocol64PlayerState ReadPlayer(BinaryReader reader)
        => new(
            reader.ReadUInt16(),
            reader.ReadUInt64(),
            reader.ReadUInt32(),
            reader.ReadString(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadByte(),
            reader.ReadBoolean(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadByte(),
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            reader.ReadBoolean(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadUInt16(),
            reader.ReadBoolean(),
            reader.ReadByte() / (float)byte.MaxValue,
            reader.ReadUInt16(),
            reader.ReadByte(),
            reader.ReadUInt16(),
            reader.ReadBoolean(),
            reader.ReadSingle(),
            reader.ReadUInt16(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadUInt16(),
            reader.ReadSingle(),
            reader.ReadByte(),
            reader.ReadBoolean(),
            reader.ReadUInt16(),
            reader.ReadByte(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadUInt16(),
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            ReadLastToDieSniperVolleyState(reader),
            reader.ReadByte(),
            reader.ReadInt32(),
            reader.ReadSingle(),
            reader.ReadUInt16(),
            reader.ReadUInt16(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadSingle(),
            reader.ReadByte(),
            reader.ReadBoolean(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadBoolean(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());

    public static void WriteProjectileState(BinaryWriter writer, Protocol64ProjectileState value)
    {
        writer.Write(value.EntityId);
        writer.Write(value.Generation);
        writer.Write((byte)value.EntityKind);
        writer.Write(value.StateTick);
        writer.Write(value.OwnerSlot);
        writer.Write(value.OwnerGeneration);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.VelocityX);
        writer.Write(value.VelocityY);
        writer.Write(value.Rotation);
        writer.Write(value.IsActive);
        writer.Write(value.RemainingLifetimeTicks);
        writer.Write(value.Damage);
        writer.Write(value.IsCritical);
        writer.Write(value.LastToDieSpyRevolverProfile);
        writer.Write(value.AppliesLastToDieLuckyStrikeStun);
        writer.Write(value.ArrowFakeSpeedMultiplier);
        writer.Write(value.IsArrowLanded);
        writer.Write(value.AppliesLastToDieGuardian);
        writer.Write(value.PiercesPlayers);
        writer.Write(value.AppliesLastToDieTranqDarts);
        writer.Write(value.LastToDiePoisonDamagePerSecond);
        writer.Write(value.LastToDieGhostDamageMultiplier);
        writer.Write(value.AppliesLastToDieDecapitator);
        writer.Write(value.IsLastToDieDecapitatorFullyCharged);
        writer.Write(value.LastToDieAttachedHeadClassId);
        writer.Write(value.LastToDieAttachedHeadTeam);
        writer.Write(value.AppliesLastToDieExplosiveTip);
        writer.Write(value.LastToDieMedicKritzM2Payload);
        writer.Write(value.LastToDieMedicJavelinOwnerPlayerId);
        writer.Write(value.LastToDieMedicJavelinTeam);
        writer.Write(value.IsLastToDieMedicJavelinAnchored);
        writer.Write(value.LastToDieMedicJavelinFuseTicksRemaining);
        writer.Write(value.HasLastToDieMedicJavelinExploded);
        writer.Write(value.CriticalDamageMultiplier);
        writer.Write(value.PlayerKnockbackImpulse);
        writer.Write(value.PlayerKnockbackAirborneVerticalScale);
        writer.Write(value.PlayerKnockbackGroundedVerticalScale);
    }

    public static Protocol64ProjectileState ReadProjectileState(BinaryReader reader)
        => new(
            reader.ReadUInt64(),
            reader.ReadUInt32(),
            (Protocol64ProjectileKind)reader.ReadByte(),
            reader.ReadUInt32(),
            reader.ReadUInt16(),
            reader.ReadUInt32(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadBoolean(),
            reader.ReadUInt32(),
            reader.ReadSingle(),
            reader.ReadBoolean(),
            reader.ReadByte(),
            reader.ReadBoolean(),
            reader.ReadSingle(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadBoolean(),
            reader.ReadByte(),
            reader.ReadInt32(),
            reader.ReadByte(),
            reader.ReadBoolean(),
            reader.ReadUInt16(),
            reader.ReadBoolean(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle());

    public static void WriteProjectileLifecycle(BinaryWriter writer, Protocol64ProjectileLifecycle value)
    {
        writer.Write((byte)value.Lifecycle);
        writer.Write(value.EntityId);
        writer.Write(value.Generation);
        writer.Write((byte)value.EntityKind);
        writer.Write(value.StateTick);
        writer.Write(value.OwnerSlot);
        writer.Write(value.OwnerGeneration);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.VelocityX);
        writer.Write(value.VelocityY);
        writer.Write(value.Rotation);
        writer.Write(value.IsActive);
        writer.Write(value.RemainingLifetimeTicks);
        writer.Write(value.Damage);
        writer.Write(value.IsCritical);
        writer.Write(value.LastToDieSpyRevolverProfile);
        writer.Write(value.AppliesLastToDieLuckyStrikeStun);
        writer.Write(value.ArrowFakeSpeedMultiplier);
        writer.Write(value.IsArrowLanded);
        writer.Write(value.AppliesLastToDieGuardian);
        writer.Write(value.PiercesPlayers);
        writer.Write(value.AppliesLastToDieTranqDarts);
        writer.Write(value.LastToDiePoisonDamagePerSecond);
        writer.Write(value.LastToDieGhostDamageMultiplier);
        writer.Write(value.AppliesLastToDieDecapitator);
        writer.Write(value.IsLastToDieDecapitatorFullyCharged);
        writer.Write(value.LastToDieAttachedHeadClassId);
        writer.Write(value.LastToDieAttachedHeadTeam);
        writer.Write(value.AppliesLastToDieExplosiveTip);
        writer.Write(value.LastToDieMedicKritzM2Payload);
        writer.Write(value.LastToDieMedicJavelinOwnerPlayerId);
        writer.Write(value.LastToDieMedicJavelinTeam);
        writer.Write(value.IsLastToDieMedicJavelinAnchored);
        writer.Write(value.LastToDieMedicJavelinFuseTicksRemaining);
        writer.Write(value.HasLastToDieMedicJavelinExploded);
        writer.Write(value.CriticalDamageMultiplier);
        writer.Write(value.PlayerKnockbackImpulse);
        writer.Write(value.PlayerKnockbackAirborneVerticalScale);
        writer.Write(value.PlayerKnockbackGroundedVerticalScale);
    }

    public static Protocol64ProjectileLifecycle ReadProjectileLifecycle(BinaryReader reader)
        => new(
            (Protocol64ProjectileLifecycleKind)reader.ReadByte(),
            reader.ReadUInt64(),
            reader.ReadUInt32(),
            (Protocol64ProjectileKind)reader.ReadByte(),
            reader.ReadUInt32(),
            reader.ReadUInt16(),
            reader.ReadUInt32(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadBoolean(),
            reader.ReadUInt32(),
            reader.ReadSingle(),
            reader.ReadBoolean(),
            reader.ReadByte(),
            reader.ReadBoolean(),
            reader.ReadSingle(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadBoolean(),
            reader.ReadBoolean(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadBoolean(),
            reader.ReadByte(),
            reader.ReadInt32(),
            reader.ReadByte(),
            reader.ReadBoolean(),
            reader.ReadUInt16(),
            reader.ReadBoolean(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle());

    private static void WriteLastToDieSniperVolleyState(
        BinaryWriter writer,
        Protocol64LastToDieSniperVolleyState? state)
    {
        writer.Write(state is not null);
        if (state is null)
        {
            return;
        }

        writer.Write(state.QueuedArrowCount);
        writer.Write(state.DueArrowCount);
        writer.Write(state.SourceTicksUntilNextArrow);
        writer.Write(state.VelocityX);
        writer.Write(state.VelocityY);
        writer.Write(state.Damage);
        writer.Write(state.FakeSpeedMultiplier);
        writer.Write(state.PayloadFlags);
        writer.Write(state.PoisonDamagePerSecond);
        writer.Write(state.GhostDamageMultiplier);
        writer.Write(state.CriticalDamageMultiplier);
    }

    private static Protocol64LastToDieSniperVolleyState? ReadLastToDieSniperVolleyState(BinaryReader reader)
        => !reader.ReadBoolean()
            ? null
            : new Protocol64LastToDieSniperVolleyState(
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadInt32(),
                reader.ReadSingle(),
                reader.ReadByte(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
}
