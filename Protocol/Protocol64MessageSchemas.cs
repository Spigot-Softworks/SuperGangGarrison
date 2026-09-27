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

    /// <summary>
    /// Gets the event id.
    /// </summary>
    public Protocol64EventId EventId { get; }

    /// <summary>
    /// Writes the message body using the legacy codec.
    /// </summary>
    public override void WriteBody(TMessage eventValue, BinaryWriter writer)
    {
        var legacyPayload = ProtocolCodec.Serialize(eventValue);
        writer.Write(legacyPayload);
    }

    /// <summary>
    /// Reads the message body using the legacy codec.
    /// </summary>
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

    /// <summary>
    /// Validates that the message type matches the schema's event id.
    /// </summary>
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
/// <summary>The protocol-64 schema for the hello message message.</summary>
public sealed class HelloMessageSchema
    : Protocol64LegacyMessageSchema<HelloMessage>
{
    /// <summary>The maximum body size in bytes (4 KiB).</summary>
    public const int MaxBodyBytes = 4 * 1024;

    /// <summary>Initializes a new instance of the <see cref="HelloMessageSchema"/> class.</summary>
    public HelloMessageSchema()
        : base(Protocol64EventId.Hello, Protocol64Direction.ClientToServer, MaxBodyBytes, revision: 2)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the welcome message message.</summary>
public sealed class WelcomeMessageSchema
    : Protocol64LegacyMessageSchema<WelcomeMessage>
{
    /// <summary>The maximum body size in bytes (4 KiB).</summary>
    public const int MaxBodyBytes = 4 * 1024;

    /// <summary>Initializes a new instance of the <see cref="WelcomeMessageSchema"/> class.</summary>
    public WelcomeMessageSchema()
        : base(Protocol64EventId.Welcome, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[LastWins(ChannelType.Input)]
/// <summary>The protocol-64 schema for the input state message message.</summary>
public sealed class InputStateMessageSchema
    : Protocol64LegacyMessageSchema<InputStateMessage>
{
    /// <summary>The maximum body size in bytes (256 bytes).</summary>
    public const int MaxBodyBytes = 256;

    /// <summary>Initializes a new instance of the <see cref="InputStateMessageSchema"/> class.</summary>
    public InputStateMessageSchema()
        : base(Protocol64EventId.InputState, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[LastWins(ChannelType.State)]
/// <summary>The protocol-64 schema for the snapshot message message.</summary>
public sealed class SnapshotMessageSchema
    : Protocol64LegacyMessageSchema<SnapshotMessage>
{
    /// <summary>The maximum body size in bytes (4 MiB).</summary>
    public const int MaxBodyBytes = 4 * 1024 * 1024;

    /// <summary>Initializes a new instance of the <see cref="SnapshotMessageSchema"/> class.</summary>
    public SnapshotMessageSchema()
        : base(
            Protocol64EventId.Snapshot,
            Protocol64Direction.ServerToClient,
            MaxBodyBytes,
            revision: 9)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the control command message message.</summary>
public sealed class ControlCommandMessageSchema
    : Protocol64LegacyMessageSchema<ControlCommandMessage>
{
    /// <summary>The maximum body size in bytes (512 bytes).</summary>
    public const int MaxBodyBytes = 512;

    /// <summary>Initializes a new instance of the <see cref="ControlCommandMessageSchema"/> class.</summary>
    public ControlCommandMessageSchema()
        : base(Protocol64EventId.ControlCommand, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the control ack message message.</summary>
public sealed class ControlAckMessageSchema
    : Protocol64LegacyMessageSchema<ControlAckMessage>
{
    /// <summary>The maximum body size in bytes (128 bytes).</summary>
    public const int MaxBodyBytes = 128;

    /// <summary>Initializes a new instance of the <see cref="ControlAckMessageSchema"/> class.</summary>
    public ControlAckMessageSchema()
        : base(Protocol64EventId.ControlAck, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the connection denied message message.</summary>
public sealed class ConnectionDeniedMessageSchema
    : Protocol64LegacyMessageSchema<ConnectionDeniedMessage>
{
    /// <summary>The maximum body size in bytes (512 bytes).</summary>
    public const int MaxBodyBytes = 512;

    /// <summary>Initializes a new instance of the <see cref="ConnectionDeniedMessageSchema"/> class.</summary>
    public ConnectionDeniedMessageSchema()
        : base(Protocol64EventId.ConnectionDenied, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the session slot changed message message.</summary>
public sealed class SessionSlotChangedMessageSchema
    : Protocol64LegacyMessageSchema<SessionSlotChangedMessage>
{
    /// <summary>The maximum body size in bytes (64 bytes).</summary>
    public const int MaxBodyBytes = 64;

    /// <summary>Initializes a new instance of the <see cref="SessionSlotChangedMessageSchema"/> class.</summary>
    public SessionSlotChangedMessageSchema()
        : base(Protocol64EventId.SessionSlotChanged, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the server status request message message.</summary>
public sealed class ServerStatusRequestMessageSchema
    : Protocol64LegacyMessageSchema<ServerStatusRequestMessage>
{
    /// <summary>The maximum body size in bytes (64 bytes).</summary>
    public const int MaxBodyBytes = 64;

    /// <summary>Initializes a new instance of the <see cref="ServerStatusRequestMessageSchema"/> class.</summary>
    public ServerStatusRequestMessageSchema()
        : base(Protocol64EventId.ServerStatusRequest, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the server status response message message.</summary>
public sealed class ServerStatusResponseMessageSchema
    : Protocol64LegacyMessageSchema<ServerStatusResponseMessage>
{
    /// <summary>The maximum body size in bytes (2 KiB).</summary>
    public const int MaxBodyBytes = 2 * 1024;

    /// <summary>Initializes a new instance of the <see cref="ServerStatusResponseMessageSchema"/> class.</summary>
    public ServerStatusResponseMessageSchema()
        : base(Protocol64EventId.ServerStatusResponse, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the password request message message.</summary>
public sealed class PasswordRequestMessageSchema
    : Protocol64LegacyMessageSchema<PasswordRequestMessage>
{
    /// <summary>The maximum body size in bytes (64 bytes).</summary>
    public const int MaxBodyBytes = 64;

    /// <summary>Initializes a new instance of the <see cref="PasswordRequestMessageSchema"/> class.</summary>
    public PasswordRequestMessageSchema()
        : base(Protocol64EventId.PasswordRequest, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the password submit message message.</summary>
public sealed class PasswordSubmitMessageSchema
    : Protocol64LegacyMessageSchema<PasswordSubmitMessage>
{
    /// <summary>The maximum body size in bytes (256 bytes).</summary>
    public const int MaxBodyBytes = 256;

    /// <summary>Initializes a new instance of the <see cref="PasswordSubmitMessageSchema"/> class.</summary>
    public PasswordSubmitMessageSchema()
        : base(Protocol64EventId.PasswordSubmit, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the password result message message.</summary>
public sealed class PasswordResultMessageSchema
    : Protocol64LegacyMessageSchema<PasswordResultMessage>
{
    /// <summary>The maximum body size in bytes (512 bytes).</summary>
    public const int MaxBodyBytes = 512;

    /// <summary>Initializes a new instance of the <see cref="PasswordResultMessageSchema"/> class.</summary>
    public PasswordResultMessageSchema()
        : base(Protocol64EventId.PasswordResult, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.GameplayEvents)]
/// <summary>The protocol-64 schema for the auto balance notice message message.</summary>
public sealed class AutoBalanceNoticeMessageSchema
    : Protocol64LegacyMessageSchema<AutoBalanceNoticeMessage>
{
    /// <summary>The maximum body size in bytes (512 bytes).</summary>
    public const int MaxBodyBytes = 512;

    /// <summary>Initializes a new instance of the <see cref="AutoBalanceNoticeMessageSchema"/> class.</summary>
    public AutoBalanceNoticeMessageSchema()
        : base(Protocol64EventId.AutoBalanceNotice, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Chat)]
/// <summary>The protocol-64 schema for the chat submit message message.</summary>
public sealed class ChatSubmitMessageSchema
    : Protocol64LegacyMessageSchema<ChatSubmitMessage>
{
    /// <summary>The maximum body size in bytes (512 bytes).</summary>
    public const int MaxBodyBytes = 512;

    /// <summary>Initializes a new instance of the <see cref="ChatSubmitMessageSchema"/> class.</summary>
    public ChatSubmitMessageSchema()
        : base(Protocol64EventId.ChatSubmit, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Chat)]
/// <summary>The protocol-64 schema for the chat relay message message.</summary>
public sealed class ChatRelayMessageSchema
    : Protocol64LegacyMessageSchema<ChatRelayMessage>
{
    /// <summary>The maximum body size in bytes (1 KiB).</summary>
    public const int MaxBodyBytes = 1 * 1024;

    /// <summary>Initializes a new instance of the <see cref="ChatRelayMessageSchema"/> class.</summary>
    public ChatRelayMessageSchema()
        : base(Protocol64EventId.ChatRelay, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the snapshot ack message message.</summary>
public sealed class SnapshotAckMessageSchema
    : Protocol64LegacyMessageSchema<SnapshotAckMessage>
{
    /// <summary>The maximum body size in bytes (128 bytes).</summary>
    public const int MaxBodyBytes = 128;

    /// <summary>Initializes a new instance of the <see cref="SnapshotAckMessageSchema"/> class.</summary>
    public SnapshotAckMessageSchema()
        : base(Protocol64EventId.SnapshotAck, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Social)]
/// <summary>The protocol-64 schema for the player profile update message message.</summary>
public sealed class PlayerProfileUpdateMessageSchema
    : Protocol64LegacyMessageSchema<PlayerProfileUpdateMessage>
{
    /// <summary>The maximum body size in bytes (2 KiB).</summary>
    public const int MaxBodyBytes = 2 * 1024;

    /// <summary>Initializes a new instance of the <see cref="PlayerProfileUpdateMessageSchema"/> class.</summary>
    public PlayerProfileUpdateMessageSchema()
        : base(Protocol64EventId.PlayerProfileUpdate, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Plugin)]
/// <summary>The protocol-64 schema for the client plugin message message.</summary>
public sealed class ClientPluginMessageSchema
    : Protocol64LegacyMessageSchema<ClientPluginMessage>
{
    /// <summary>The maximum body size in bytes (4 KiB).</summary>
    public const int MaxBodyBytes = 4 * 1024;

    /// <summary>Initializes a new instance of the <see cref="ClientPluginMessageSchema"/> class.</summary>
    public ClientPluginMessageSchema()
        : base(Protocol64EventId.ClientPluginMessage, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Plugin)]
/// <summary>The protocol-64 schema for the server plugin message message.</summary>
public sealed class ServerPluginMessageSchema
    : Protocol64LegacyMessageSchema<ServerPluginMessage>
{
    /// <summary>The maximum body size in bytes (4 KiB).</summary>
    public const int MaxBodyBytes = 4 * 1024;

    /// <summary>Initializes a new instance of the <see cref="ServerPluginMessageSchema"/> class.</summary>
    public ServerPluginMessageSchema()
        : base(Protocol64EventId.ServerPluginMessage, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Social)]
/// <summary>The protocol-64 schema for the player social profile update message message.</summary>
public sealed class PlayerSocialProfileUpdateMessageSchema
    : Protocol64LegacyMessageSchema<PlayerSocialProfileUpdateMessage>
{
    /// <summary>The maximum body size in bytes (32 KiB).</summary>
    public const int MaxBodyBytes = 32 * 1024;

    /// <summary>Initializes a new instance of the <see cref="PlayerSocialProfileUpdateMessageSchema"/> class.</summary>
    public PlayerSocialProfileUpdateMessageSchema()
        : base(Protocol64EventId.PlayerSocialProfileUpdate, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the server details request message message.</summary>
public sealed class ServerDetailsRequestMessageSchema
    : Protocol64LegacyMessageSchema<ServerDetailsRequestMessage>
{
    /// <summary>The maximum body size in bytes (64 bytes).</summary>
    public const int MaxBodyBytes = 64;

    /// <summary>Initializes a new instance of the <see cref="ServerDetailsRequestMessageSchema"/> class.</summary>
    public ServerDetailsRequestMessageSchema()
        : base(Protocol64EventId.ServerDetailsRequest, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the server details response message message.</summary>
public sealed class ServerDetailsResponseMessageSchema
    : Protocol64LegacyMessageSchema<ServerDetailsResponseMessage>
{
    /// <summary>The maximum body size in bytes (32 KiB).</summary>
    public const int MaxBodyBytes = 32 * 1024;

    /// <summary>Initializes a new instance of the <see cref="ServerDetailsResponseMessageSchema"/> class.</summary>
    public ServerDetailsResponseMessageSchema()
        : base(Protocol64EventId.ServerDetailsResponse, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Social)]
/// <summary>The protocol-64 schema for the custom bubble upload message message.</summary>
public sealed class CustomBubbleUploadMessageSchema
    : Protocol64LegacyMessageSchema<CustomBubbleUploadMessage>
{
    /// <summary>The maximum body size in bytes (64 KiB).</summary>
    public const int MaxBodyBytes = 64 * 1024;

    /// <summary>Initializes a new instance of the <see cref="CustomBubbleUploadMessageSchema"/> class.</summary>
    public CustomBubbleUploadMessageSchema()
        : base(Protocol64EventId.CustomBubbleUpload, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Social)]
/// <summary>The protocol-64 schema for the custom bubble state message message.</summary>
public sealed class CustomBubbleStateMessageSchema
    : Protocol64LegacyMessageSchema<CustomBubbleStateMessage>
{
    /// <summary>The maximum body size in bytes (64 KiB).</summary>
    public const int MaxBodyBytes = 64 * 1024;

    /// <summary>Initializes a new instance of the <see cref="CustomBubbleStateMessageSchema"/> class.</summary>
    public CustomBubbleStateMessageSchema()
        : base(Protocol64EventId.CustomBubbleState, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableUnordered(ChannelType.Social)]
/// <summary>The protocol-64 schema for the custom bubble clear message message.</summary>
public sealed class CustomBubbleClearMessageSchema
    : Protocol64LegacyMessageSchema<CustomBubbleClearMessage>
{
    /// <summary>The maximum body size in bytes (64 bytes).</summary>
    public const int MaxBodyBytes = 64;

    /// <summary>Initializes a new instance of the <see cref="CustomBubbleClearMessageSchema"/> class.</summary>
    public CustomBubbleClearMessageSchema()
        : base(Protocol64EventId.CustomBubbleClear, Protocol64Direction.Bidirectional, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the ping request message message.</summary>
public sealed class PingRequestMessageSchema
    : Protocol64LegacyMessageSchema<PingRequestMessage>
{
    /// <summary>The maximum body size in bytes (64 bytes).</summary>
    public const int MaxBodyBytes = 64;

    /// <summary>Initializes a new instance of the <see cref="PingRequestMessageSchema"/> class.</summary>
    public PingRequestMessageSchema()
        : base(Protocol64EventId.PingRequest, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the ping response message message.</summary>
public sealed class PingResponseMessageSchema
    : Protocol64LegacyMessageSchema<PingResponseMessage>
{
    /// <summary>The maximum body size in bytes (64 bytes).</summary>
    public const int MaxBodyBytes = 64;

    /// <summary>Initializes a new instance of the <see cref="PingResponseMessageSchema"/> class.</summary>
    public PingResponseMessageSchema()
        : base(Protocol64EventId.PingResponse, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the gameplay account attach request message message.</summary>
public sealed class GameplayAccountAttachRequestMessageSchema
    : Protocol64LegacyMessageSchema<GameplayAccountAttachRequestMessage>
{
    /// <summary>The maximum body size in bytes (512 bytes).</summary>
    public const int MaxBodyBytes = 512;

    /// <summary>Initializes a new instance of the <see cref="GameplayAccountAttachRequestMessageSchema"/> class.</summary>
    public GameplayAccountAttachRequestMessageSchema()
        : base(Protocol64EventId.GameplayAccountAttachRequest, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the gameplay account attach result message message.</summary>
public sealed class GameplayAccountAttachResultMessageSchema
    : Protocol64LegacyMessageSchema<GameplayAccountAttachResultMessage>
{
    /// <summary>The maximum body size in bytes (1 KiB).</summary>
    public const int MaxBodyBytes = 1024;

    /// <summary>Initializes a new instance of the <see cref="GameplayAccountAttachResultMessageSchema"/> class.</summary>
    public GameplayAccountAttachResultMessageSchema()
        : base(Protocol64EventId.GameplayAccountAttachResult, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Social)]
/// <summary>The protocol-64 schema for the player points state message message.</summary>
public sealed class PlayerPointsStateMessageSchema
    : Protocol64LegacyMessageSchema<PlayerPointsStateMessage>
{
    /// <summary>The maximum body size in bytes (128 bytes).</summary>
    public const int MaxBodyBytes = 128;

    /// <summary>Initializes a new instance of the <see cref="PlayerPointsStateMessageSchema"/> class.</summary>
    public PlayerPointsStateMessageSchema()
        : base(Protocol64EventId.PlayerPointsState, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the vote command message message.</summary>
public sealed class VoteCommandMessageSchema
    : Protocol64LegacyMessageSchema<VoteCommandMessage>
{
    /// <summary>The maximum body size in bytes (512 bytes).</summary>
    public const int MaxBodyBytes = 512;

    /// <summary>Initializes a new instance of the <see cref="VoteCommandMessageSchema"/> class.</summary>
    public VoteCommandMessageSchema()
        : base(Protocol64EventId.VoteCommand, Protocol64Direction.ClientToServer, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.GameplayEvents)]
/// <summary>The protocol-64 schema for the vote state message message.</summary>
public sealed class VoteStateMessageSchema
    : Protocol64LegacyMessageSchema<VoteStateMessage>
{
    /// <summary>The maximum body size in bytes (1 KiB).</summary>
    public const int MaxBodyBytes = 1024;

    /// <summary>Initializes a new instance of the <see cref="VoteStateMessageSchema"/> class.</summary>
    public VoteStateMessageSchema()
        : base(Protocol64EventId.VoteState, Protocol64Direction.ServerToClient, MaxBodyBytes)
    {
    }
}

[ReliableOrdered(ChannelType.Control)]
/// <summary>The protocol-64 schema for the vote menu message message.</summary>
public sealed class VoteMenuMessageSchema
    : Protocol64LegacyMessageSchema<VoteMenuMessage>
{
    /// <summary>The maximum body size in bytes (128 KiB).</summary>
    public const int MaxBodyBytes = 128 * 1024;

    /// <summary>Initializes a new instance of the <see cref="VoteMenuMessageSchema"/> class.</summary>
    public VoteMenuMessageSchema()
        : base(Protocol64EventId.VoteMenu, Protocol64Direction.ServerToClient, MaxBodyBytes)
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
        registry.Register(new GameplayAccountAttachRequestMessageSchema());
        registry.Register(new GameplayAccountAttachResultMessageSchema());
        registry.Register(new PlayerPointsStateMessageSchema());
        registry.Register(new VoteCommandMessageSchema());
        registry.Register(new VoteStateMessageSchema());
        registry.Register(new VoteMenuMessageSchema());
        registry.Register(new VoiceSubmitMessageSchema());
        registry.Register(new AudioRelayMessageSchema());
        registry.Register(new ServerAudioStateMessageSchema());
        registry.Register(new VoiceChannelMembershipMessageSchema());
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
