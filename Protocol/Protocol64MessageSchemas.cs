using System;
using System.IO;

namespace OpenGarrison.Protocol;

/// <summary>
/// Protocol-64 migration adapter for the existing OG2 message codec.
///
/// The legacy codec owns the body bytes for this slice. The protocol-64 schema
/// still owns the stable event ID, direction, delivery contract, size limit, and
/// complete-body validation. This lets backends select a schema by event ID and
/// concrete event type without switching on <see cref="MessageType"/>.
/// </summary>
public abstract class Protocol64LegacyMessageSchema<TMessage>
    : Protocol64EventSchema<TMessage>
    where TMessage : class, IProtocolMessage
{
    protected Protocol64LegacyMessageSchema(
        Protocol64EventId eventId,
        Protocol64Direction direction,
        int maxBodyBytes,
        ushort revision = 1)
        : base(
            schemaId: (ushort)eventId,
            revision,
            direction,
            maxBodyBytes)
    {
        EventId = eventId;
    }

    public Protocol64EventId EventId { get; }

    public override void WriteBody(TMessage eventValue, BinaryWriter writer)
    {
        var legacyPayload = ProtocolCodec.Serialize(eventValue);
        writer.Write(legacyPayload);
    }

    public override TMessage ReadBody(BinaryReader reader)
    {
        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (remaining < 0 || remaining > int.MaxValue)
        {
            throw new Protocol64SchemaValidationException(
                $"Legacy protocol body length {remaining} is invalid.");
        }

        var legacyPayload = reader.ReadBytes((int)remaining);
        if (legacyPayload.Length != remaining ||
            !ProtocolCodec.TryDeserialize(legacyPayload, out var message) ||
            message is not TMessage typedMessage)
        {
            throw new Protocol64SchemaValidationException(
                $"Legacy protocol payload did not decode as {typeof(TMessage).Name}.");
        }

        return typedMessage;
    }

    public override void Validate(TMessage eventValue)
    {
        if (eventValue.Type != (MessageType)EventId)
        {
            throw new Protocol64SchemaValidationException(
                $"Schema event ID {EventId} does not match legacy message type {eventValue.Type}.");
        }
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class HelloMessageSchema
    : Protocol64LegacyMessageSchema<HelloMessage>
{
    public const int MaxBodyBytes = 4 * 1024;

    public HelloMessageSchema()
        : base(Protocol64EventId.Hello, Protocol64Direction.ClientToServer, MaxBodyBytes, revision: 2)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class WelcomeMessageSchema
    : Protocol64LegacyMessageSchema<WelcomeMessage>
{
    public const int MaxBodyBytes = 4 * 1024;

    public WelcomeMessageSchema()
        : base(Protocol64EventId.Welcome, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[LastWins(ChannelType.Input)]
public sealed class InputStateMessageSchema
    : Protocol64LegacyMessageSchema<InputStateMessage>
{
    public const int MaxBodyBytes = 256;

    public InputStateMessageSchema()
        : base(Protocol64EventId.InputState, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[LastWins(ChannelType.State)]
public sealed class SnapshotMessageSchema
    : Protocol64LegacyMessageSchema<SnapshotMessage>
{
    public const int MaxBodyBytes = 4 * 1024 * 1024;

    public SnapshotMessageSchema()
        : base(
            Protocol64EventId.Snapshot,
            Protocol64Direction.ServerToClient,
            MaxBodyBytes,
            revision: 4)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class ControlCommandMessageSchema
    : Protocol64LegacyMessageSchema<ControlCommandMessage>
{
    public const int MaxBodyBytes = 512;

    public ControlCommandMessageSchema()
        : base(Protocol64EventId.ControlCommand, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class ControlAckMessageSchema
    : Protocol64LegacyMessageSchema<ControlAckMessage>
{
    public const int MaxBodyBytes = 128;

    public ControlAckMessageSchema()
        : base(Protocol64EventId.ControlAck, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class ConnectionDeniedMessageSchema
    : Protocol64LegacyMessageSchema<ConnectionDeniedMessage>
{
    public const int MaxBodyBytes = 512;

    public ConnectionDeniedMessageSchema()
        : base(Protocol64EventId.ConnectionDenied, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class SessionSlotChangedMessageSchema
    : Protocol64LegacyMessageSchema<SessionSlotChangedMessage>
{
    public const int MaxBodyBytes = 64;

    public SessionSlotChangedMessageSchema()
        : base(Protocol64EventId.SessionSlotChanged, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class ServerStatusRequestMessageSchema
    : Protocol64LegacyMessageSchema<ServerStatusRequestMessage>
{
    public const int MaxBodyBytes = 64;

    public ServerStatusRequestMessageSchema()
        : base(Protocol64EventId.ServerStatusRequest, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class ServerStatusResponseMessageSchema
    : Protocol64LegacyMessageSchema<ServerStatusResponseMessage>
{
    public const int MaxBodyBytes = 2 * 1024;

    public ServerStatusResponseMessageSchema()
        : base(Protocol64EventId.ServerStatusResponse, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class PasswordRequestMessageSchema
    : Protocol64LegacyMessageSchema<PasswordRequestMessage>
{
    public const int MaxBodyBytes = 64;

    public PasswordRequestMessageSchema()
        : base(Protocol64EventId.PasswordRequest, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class PasswordSubmitMessageSchema
    : Protocol64LegacyMessageSchema<PasswordSubmitMessage>
{
    public const int MaxBodyBytes = 256;

    public PasswordSubmitMessageSchema()
        : base(Protocol64EventId.PasswordSubmit, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class PasswordResultMessageSchema
    : Protocol64LegacyMessageSchema<PasswordResultMessage>
{
    public const int MaxBodyBytes = 512;

    public PasswordResultMessageSchema()
        : base(Protocol64EventId.PasswordResult, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.GameplayEvents)]
public sealed class AutoBalanceNoticeMessageSchema
    : Protocol64LegacyMessageSchema<AutoBalanceNoticeMessage>
{
    public const int MaxBodyBytes = 512;

    public AutoBalanceNoticeMessageSchema()
        : base(Protocol64EventId.AutoBalanceNotice, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Chat)]
public sealed class ChatSubmitMessageSchema
    : Protocol64LegacyMessageSchema<ChatSubmitMessage>
{
    public const int MaxBodyBytes = 512;

    public ChatSubmitMessageSchema()
        : base(Protocol64EventId.ChatSubmit, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Chat)]
public sealed class ChatRelayMessageSchema
    : Protocol64LegacyMessageSchema<ChatRelayMessage>
{
    public const int MaxBodyBytes = 1 * 1024;

    public ChatRelayMessageSchema()
        : base(Protocol64EventId.ChatRelay, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class SnapshotAckMessageSchema
    : Protocol64LegacyMessageSchema<SnapshotAckMessage>
{
    public const int MaxBodyBytes = 128;

    public SnapshotAckMessageSchema()
        : base(Protocol64EventId.SnapshotAck, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Social)]
public sealed class PlayerProfileUpdateMessageSchema
    : Protocol64LegacyMessageSchema<PlayerProfileUpdateMessage>
{
    public const int MaxBodyBytes = 2 * 1024;

    public PlayerProfileUpdateMessageSchema()
        : base(Protocol64EventId.PlayerProfileUpdate, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Plugin)]
public sealed class ClientPluginMessageSchema
    : Protocol64LegacyMessageSchema<ClientPluginMessage>
{
    public const int MaxBodyBytes = 4 * 1024;

    public ClientPluginMessageSchema()
        : base(Protocol64EventId.ClientPluginMessage, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Plugin)]
public sealed class ServerPluginMessageSchema
    : Protocol64LegacyMessageSchema<ServerPluginMessage>
{
    public const int MaxBodyBytes = 4 * 1024;

    public ServerPluginMessageSchema()
        : base(Protocol64EventId.ServerPluginMessage, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Social)]
public sealed class PlayerSocialProfileUpdateMessageSchema
    : Protocol64LegacyMessageSchema<PlayerSocialProfileUpdateMessage>
{
    public const int MaxBodyBytes = 32 * 1024;

    public PlayerSocialProfileUpdateMessageSchema()
        : base(Protocol64EventId.PlayerSocialProfileUpdate, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class ServerDetailsRequestMessageSchema
    : Protocol64LegacyMessageSchema<ServerDetailsRequestMessage>
{
    public const int MaxBodyBytes = 64;

    public ServerDetailsRequestMessageSchema()
        : base(Protocol64EventId.ServerDetailsRequest, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class ServerDetailsResponseMessageSchema
    : Protocol64LegacyMessageSchema<ServerDetailsResponseMessage>
{
    public const int MaxBodyBytes = 32 * 1024;

    public ServerDetailsResponseMessageSchema()
        : base(Protocol64EventId.ServerDetailsResponse, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Social)]
public sealed class CustomBubbleUploadMessageSchema
    : Protocol64LegacyMessageSchema<CustomBubbleUploadMessage>
{
    public const int MaxBodyBytes = 64 * 1024;

    public CustomBubbleUploadMessageSchema()
        : base(Protocol64EventId.CustomBubbleUpload, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Social)]
public sealed class CustomBubbleStateMessageSchema
    : Protocol64LegacyMessageSchema<CustomBubbleStateMessage>
{
    public const int MaxBodyBytes = 64 * 1024;

    public CustomBubbleStateMessageSchema()
        : base(Protocol64EventId.CustomBubbleState, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Social)]
public sealed class CustomBubbleClearMessageSchema
    : Protocol64LegacyMessageSchema<CustomBubbleClearMessage>
{
    public const int MaxBodyBytes = 64;

    public CustomBubbleClearMessageSchema()
        : base(Protocol64EventId.CustomBubbleClear, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class PingRequestMessageSchema
    : Protocol64LegacyMessageSchema<PingRequestMessage>
{
    public const int MaxBodyBytes = 64;

    public PingRequestMessageSchema()
        : base(Protocol64EventId.PingRequest, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
public sealed class PingResponseMessageSchema
    : Protocol64LegacyMessageSchema<PingResponseMessage>
{
    public const int MaxBodyBytes = 64;

    public PingResponseMessageSchema()
        : base(Protocol64EventId.PingResponse, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

public static class Protocol64SchemaRegistryFactory
{
    /// <summary>
    /// Creates the complete protocol-64 registry for the current OG2 message set.
    /// Every current <see cref="IProtocolMessage"/> family receives one stable
    /// event ID and one concrete schema type.
    /// </summary>
    public static Protocol64SchemaRegistry CreateDefault()
    {
        var registry = new Protocol64SchemaRegistry();

        registry.Register(new HelloMessageSchema());
        registry.Register(new WelcomeMessageSchema());
        registry.Register(new InputStateMessageSchema());
        registry.Register(new SnapshotMessageSchema());
        registry.Register(new ControlCommandMessageSchema());
        registry.Register(new ControlAckMessageSchema());
        registry.Register(new ConnectionDeniedMessageSchema());
        registry.Register(new SessionSlotChangedMessageSchema());
        registry.Register(new ServerStatusRequestMessageSchema());
        registry.Register(new ServerStatusResponseMessageSchema());
        registry.Register(new PasswordRequestMessageSchema());
        registry.Register(new PasswordSubmitMessageSchema());
        registry.Register(new PasswordResultMessageSchema());
        registry.Register(new AutoBalanceNoticeMessageSchema());
        registry.Register(new ChatSubmitMessageSchema());
        registry.Register(new ChatRelayMessageSchema());
        registry.Register(new SnapshotAckMessageSchema());
        registry.Register(new PlayerProfileUpdateMessageSchema());
        registry.Register(new ClientPluginMessageSchema());
        registry.Register(new ServerPluginMessageSchema());
        registry.Register(new PlayerSocialProfileUpdateMessageSchema());
        registry.Register(new ServerDetailsRequestMessageSchema());
        registry.Register(new ServerDetailsResponseMessageSchema());
        registry.Register(new CustomBubbleUploadMessageSchema());
        registry.Register(new CustomBubbleStateMessageSchema());
        registry.Register(new CustomBubbleClearMessageSchema());
        registry.Register(new PingRequestMessageSchema());
        registry.Register(new PingResponseMessageSchema());
        registry.Register(new Protocol64InputCommandSchema());
        registry.Register(new Protocol64InputCommandResultSchema());
        registry.Register(new Protocol64InputCommandResultAckSchema());
        registry.Register(new Protocol64PlayerStateBatchSchema());
        registry.Register(new Protocol64RosterStateSchema());
        registry.Register(new Protocol64ProjectileStateSchema());
        registry.Register(new Protocol64ProjectileLifecycleSchema());
        registry.Register(new Protocol64StateResyncRequestSchema());
        registry.Register(new Protocol64StateResyncResponseSchema());
        registry.Register(new Protocol64RetransmitRequestSchema());
        registry.Register(new Protocol64RetransmitResponseSchema());
        registry.Register(new LastToDieCommandSchema());
        registry.Register(new LastToDieCommandResultSchema());
        registry.Register(new LastToDieRunSnapshotSchema());
        registry.Register(new LastToDieRunSnapshotAckSchema());

        return registry;
    }
}
