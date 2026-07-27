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
    uint StateTick);

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
    int Damage);

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
    int Damage);

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
        : base(Protocol64StateSchemaIds.PlayerStateBatch, 1, Protocol64Direction.ServerToClient, MaxBodyBytes)
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
        : base(Protocol64StateSchemaIds.ProjectileState, 1, Protocol64Direction.ServerToClient, MaxBodyBytes)
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
        : base(Protocol64StateSchemaIds.ProjectileLifecycle, 1, Protocol64Direction.ServerToClient, MaxBodyBytes)
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
        : base(Protocol64StateSchemaIds.StateResyncResponse, 1, Protocol64Direction.ServerToClient, MaxBodyBytes)
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
            !float.IsFinite(value.VelocityX) || !float.IsFinite(value.VelocityY))
        {
            throw new Protocol64SchemaValidationException("Player position and velocity must be finite.");
        }

        if (value.Team > 2)
        {
            throw new Protocol64SchemaValidationException("Player team is outside the protocol range.");
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
        foreach (var player in value.Players)
        {
            Validate(player);
            if (!identities.Add((player.Slot, player.PlayerId, player.Generation)))
            {
                throw new Protocol64SchemaValidationException("Player state batch contains duplicate identities.");
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
    }

    public static void Validate(Protocol64ProjectileState value)
    {
        if (value is null)
        {
            throw new Protocol64SchemaValidationException("Projectile state cannot be null.");
        }

        ValidateProjectileIdentity(value.EntityId, value.Generation, value.EntityKind);
        ValidateOwner(value.OwnerSlot, value.OwnerGeneration);
        ValidateProjectileMotion(value.X, value.Y, value.VelocityX, value.VelocityY, value.Rotation);
        if (value.Damage < 0)
        {
            throw new Protocol64SchemaValidationException("Projectile damage cannot be negative.");
        }
    }

    public static void Validate(Protocol64ProjectileLifecycle value)
    {
        if (value is null || !Enum.IsDefined(value.Lifecycle))
        {
            throw new Protocol64SchemaValidationException("Projectile lifecycle kind is invalid.");
        }

        ValidateProjectileIdentity(value.EntityId, value.Generation, value.EntityKind);
        ValidateOwner(value.OwnerSlot, value.OwnerGeneration);
        ValidateProjectileMotion(value.X, value.Y, value.VelocityX, value.VelocityY, value.Rotation);
        if (value.Damage < 0)
        {
            throw new Protocol64SchemaValidationException("Projectile damage cannot be negative.");
        }
    }

    public static void Validate(Protocol64StateResyncRequest value)
    {
        if (value is null || value.RequestId == 0 || !Enum.IsDefined(value.Reason))
        {
            throw new Protocol64SchemaValidationException("State resync request ID or reason is invalid.");
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
        foreach (var player in value.Players) Validate(player);
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

    private static void ValidateOwner(ushort slot, uint generation)
    {
        if (slot >= MaxPlayers || generation == 0)
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
            reader.ReadUInt32());

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
            reader.ReadInt32());

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
            reader.ReadInt32());
}
