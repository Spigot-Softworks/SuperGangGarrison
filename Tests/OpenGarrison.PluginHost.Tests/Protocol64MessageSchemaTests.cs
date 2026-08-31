using System;
using System.Collections.Generic;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Protocol64MessageSchemaTests
{
    private static readonly IReadOnlyDictionary<Protocol64EventId, (Type EventType, Protocol64Direction Direction, Protocol64DeliveryKind Delivery, ChannelType? Channel)> ExpectedSchemas =
        new Dictionary<Protocol64EventId, (Type, Protocol64Direction, Protocol64DeliveryKind, ChannelType?)>
        {
            [Protocol64EventId.Hello] = (typeof(HelloMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.Welcome] = (typeof(WelcomeMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.InputState] = (typeof(InputStateMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.LastWins, ChannelType.Input),
            [Protocol64EventId.Snapshot] = (typeof(SnapshotMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.LastWins, ChannelType.State),
            [Protocol64EventId.ControlCommand] = (typeof(ControlCommandMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.ControlAck] = (typeof(ControlAckMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.ConnectionDenied] = (typeof(ConnectionDeniedMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.SessionSlotChanged] = (typeof(SessionSlotChangedMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.ServerStatusRequest] = (typeof(ServerStatusRequestMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.ServerStatusResponse] = (typeof(ServerStatusResponseMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.PasswordRequest] = (typeof(PasswordRequestMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.PasswordSubmit] = (typeof(PasswordSubmitMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.PasswordResult] = (typeof(PasswordResultMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.AutoBalanceNotice] = (typeof(AutoBalanceNoticeMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.GameplayEvents),
            [Protocol64EventId.ChatSubmit] = (typeof(ChatSubmitMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Chat),
            [Protocol64EventId.ChatRelay] = (typeof(ChatRelayMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Chat),
            [Protocol64EventId.SnapshotAck] = (typeof(SnapshotAckMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.PlayerProfileUpdate] = (typeof(PlayerProfileUpdateMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableUnordered, ChannelType.Social),
            [Protocol64EventId.ClientPluginMessage] = (typeof(ClientPluginMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableUnordered, ChannelType.Plugin),
            [Protocol64EventId.ServerPluginMessage] = (typeof(ServerPluginMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableUnordered, ChannelType.Plugin),
            [Protocol64EventId.PlayerSocialProfileUpdate] = (typeof(PlayerSocialProfileUpdateMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableUnordered, ChannelType.Social),
            [Protocol64EventId.ServerDetailsRequest] = (typeof(ServerDetailsRequestMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.ServerDetailsResponse] = (typeof(ServerDetailsResponseMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.CustomBubbleUpload] = (typeof(CustomBubbleUploadMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableUnordered, ChannelType.Social),
            [Protocol64EventId.CustomBubbleState] = (typeof(CustomBubbleStateMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableUnordered, ChannelType.Social),
            [Protocol64EventId.CustomBubbleClear] = (typeof(CustomBubbleClearMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableUnordered, ChannelType.Social),
            [Protocol64EventId.PingRequest] = (typeof(PingRequestMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.PingResponse] = (typeof(PingResponseMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.InputCommand] = (typeof(Protocol64InputCommand), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Input),
            [Protocol64EventId.InputCommandResult] = (typeof(Protocol64InputCommandResult), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Input),
            [Protocol64EventId.InputCommandResultAck] = (typeof(Protocol64InputCommandResultAck), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Input),
            [Protocol64EventId.PlayerStateBatch] = (typeof(Protocol64PlayerStateBatch), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableUnordered, ChannelType.State),
            [Protocol64EventId.RosterState] = (typeof(Protocol64RosterState), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableUnordered, ChannelType.State),
            [Protocol64EventId.ProjectileState] = (typeof(Protocol64ProjectileState), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.LastWins, ChannelType.State),
            [Protocol64EventId.ProjectileLifecycle] = (typeof(Protocol64ProjectileLifecycle), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableUnordered, ChannelType.GameplayEvents),
            [Protocol64EventId.StateResyncRequest] = (typeof(Protocol64StateResyncRequest), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.StateResyncResponse] = (typeof(Protocol64StateResyncResponse), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.RetransmitRequest] = (typeof(Protocol64RetransmitRequest), Protocol64Direction.Bidirectional, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.RetransmitResponse] = (typeof(Protocol64RetransmitResponse), Protocol64Direction.Bidirectional, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.LastToDieCommand] = (typeof(LastToDieCommandMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.LastToDieCommandResult] = (typeof(LastToDieCommandResultMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
            [Protocol64EventId.LastToDieRunSnapshot] = (typeof(LastToDieRunSnapshotMessage), Protocol64Direction.ServerToClient, Protocol64DeliveryKind.ReliableOrdered, ChannelType.GameplayEvents),
            [Protocol64EventId.LastToDieRunSnapshotAck] = (typeof(LastToDieRunSnapshotAckMessage), Protocol64Direction.ClientToServer, Protocol64DeliveryKind.ReliableOrdered, ChannelType.Control),
        };

    [Fact]
    public void DefaultRegistryContainsEveryCurrentMessageFamilyWithStableIds()
    {
        var registry = Protocol64SchemaRegistryFactory.CreateDefault();

        Assert.Equal(43, registry.Count);
        Assert.Equal(43, ExpectedSchemas.Count);

        foreach (var (eventId, expected) in ExpectedSchemas)
        {
            var expectedRevision = GetExpectedRevision(eventId);
            Assert.True(
                registry.TryGet((ushort)eventId, revision: expectedRevision, out var schema),
                $"Schema {eventId} was not registered.");
            Assert.NotNull(schema);
            Assert.Equal((ushort)eventId, schema!.Descriptor.Key.SchemaId);
            Assert.Equal(expectedRevision, schema.Descriptor.Key.Revision);
            Assert.Equal(expected.EventType, schema.EventType);
            Assert.True(schema.Descriptor.MaxBodyBytes > 0);
        }
    }

    [Fact]
    public void DefaultRegistryExposesBlueprintDeliveryAndDirectionMetadata()
    {
        var registry = Protocol64SchemaRegistryFactory.CreateDefault();

        foreach (var (eventId, expected) in ExpectedSchemas)
        {
            var expectedRevision = GetExpectedRevision(eventId);
            var schema = registry.Get((ushort)eventId, revision: expectedRevision);

            Assert.Equal(expected.Direction, schema.Descriptor.Direction);
            Assert.Equal(expected.Delivery, schema.Descriptor.Delivery.Kind);
            Assert.Equal(expected.Channel, schema.Descriptor.Delivery.Channel);
        }
    }

    private static ushort GetExpectedRevision(Protocol64EventId eventId)
    {
        return eventId switch
        {
            Protocol64EventId.Hello => 2,
            Protocol64EventId.PlayerStateBatch => 23,
            Protocol64EventId.StateResyncResponse => 26,
            Protocol64EventId.Snapshot => 4,
            Protocol64EventId.ProjectileState
                or Protocol64EventId.ProjectileLifecycle => 10,
            Protocol64EventId.LastToDieRunSnapshot => 4,
            Protocol64EventId.InputCommand => 3,
            Protocol64EventId.InputCommandResult => 2,
            _ => 1,
        };
    }

    [Fact]
    public void RepresentativeClientAndServerMessagesRoundTripThroughProtocol64Frames()
    {
        var registry = Protocol64SchemaRegistryFactory.CreateDefault();

        var hello = new HelloMessage(
            Name: "Ada",
            Version: 63,
            BadgeMask: 0x12,
            FriendCode: "friend",
            PlayerCardJson: "{\"theme\":\"blue\"}",
            Intent: ConnectionIntent.Join,
            ClientInstanceId: Guid.Parse("12345678-1234-1234-1234-123456789abc"));
        var decodedHello = RoundTrip<HelloMessage>(registry, hello, frameId: 1);
        Assert.Equal(hello, decodedHello);

        var input = new InputStateMessage(
            Sequence: 77,
            Buttons: InputButtons.Right | InputButtons.Up,
            AimRelX: 0.75f,
            AimRelY: -0.25f,
            ChatBubbleFrameIndex: 3,
            IsUsingBinoculars: true,
            BinocularsFocusX: 128f,
            BinocularsFocusY: 64f,
            PingMilliseconds: 91);
        var decodedInput = RoundTrip<InputStateMessage>(registry, input, frameId: 2);
        Assert.Equal(input, decodedInput);

        var chat = new ChatRelayMessage(
            Team: 1,
            PlayerName: "Ada",
            Text: "hello, team",
            TeamOnly: true,
            PlayerSlot: 2);
        var decodedChat = RoundTrip<ChatRelayMessage>(registry, chat, frameId: 3);
        Assert.Equal(chat, decodedChat);

        var snapshot = CreateMinimalSnapshot();
        var decodedSnapshot = RoundTrip<SnapshotMessage>(registry, snapshot, frameId: 4);
        Assert.Equal(snapshot.Frame, decodedSnapshot.Frame);
        Assert.Equal(snapshot.LevelName, decodedSnapshot.LevelName);
        Assert.Equal(snapshot.RedIntel, decodedSnapshot.RedIntel);
        Assert.Equal(snapshot.Players, decodedSnapshot.Players);
        Assert.Equal(snapshot.EntityCollectionCompletenessFlags, decodedSnapshot.EntityCollectionCompletenessFlags);
    }

    [Fact]
    public void LegacyAdapterRejectsAProtocolMessageFromTheWrongSchema()
    {
        var registry = Protocol64SchemaRegistryFactory.CreateDefault();
        var encoded = Protocol64FrameCodec.Encode(
            registry,
            new ChatSubmitMessage("hello"),
            connectionEpoch: 1,
            frameId: 1,
            options: new Protocol64FrameEncodeOptions
            {
                Compression = Protocol64Compression.None,
            });

        Assert.True(encoded.Succeeded, encoded.Fault?.Message);

        // The body adapter is intentionally legacy-compatible, but the schema ID
        // remains authoritative. Presenting a ChatSubmit body as a ChatRelay event
        // must fail rather than dispatching by the embedded legacy MessageType.
        var tampered = encoded.Payload!.ToArray();
        BitConverter.GetBytes((ushort)Protocol64EventId.ChatRelay).CopyTo(tampered, 8);
        BitConverter.GetBytes(Protocol64FrameCodec.ComputeIntegrity(tampered)).CopyTo(
            tampered,
            Protocol64FrameHeader.IntegrityOffset);

        var result = Protocol64FrameCodec.Decode(tampered, registry);

        Assert.False(result.Succeeded);
        Assert.Equal(Protocol64FaultKind.ValidationFailed, result.Fault!.Kind);
    }

    [Fact]
    public void UntypedFrameEncodingUsesTheRegisteredConcreteSchema()
    {
        var registry = Protocol64SchemaRegistryFactory.CreateDefault();
        var encoded = Protocol64FrameCodec.EncodeObject(
            registry,
            new Protocol64StateResyncRequest(9, 3, 4, 5, Protocol64StateResyncReason.ClientRequested),
            connectionEpoch: 1,
            frameId: 8);

        Assert.True(encoded.Succeeded, encoded.Fault?.Message);
        Assert.Equal((ushort)Protocol64EventId.StateResyncRequest, encoded.Header!.SchemaId);
        var decoded = Protocol64FrameCodec.Decode(encoded.Payload!, registry);
        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        Assert.IsType<Protocol64StateResyncRequest>(decoded.Event);
    }

    [Fact]
    public void RetransmitRequestIsACompleteBidirectionalControlEvent()
    {
        var registry = Protocol64SchemaRegistryFactory.CreateDefault();
        var value = new Protocol64RetransmitRequest(
            Guid.NewGuid(),
            9,
            Protocol64TransportRepairReason.StreamReset,
            12,
            ChannelType.State,
            Protocol64DeliveryKind.ReliableUnordered,
            4,
            7,
            42,
            true);

        var encoded = Protocol64FrameCodec.Encode(registry, value, 9, 2);
        Assert.True(encoded.Succeeded, encoded.Fault?.Message);
        var decoded = Protocol64FrameCodec.Decode<Protocol64RetransmitRequest>(encoded.Payload!, registry);

        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        Assert.Equal(value, decoded.Event);
    }

    [Fact]
    public void RetransmitResponseCarriesDedicatedStreamOrderingMetadata()
    {
        var registry = Protocol64SchemaRegistryFactory.CreateDefault();
        var value = new Protocol64RetransmitResponse(
            Guid.NewGuid(),
            9,
            true,
            24,
            ChannelType.GameplayEvents,
            Protocol64DeliveryKind.ReliableUnordered,
            Lane: 2,
            SequenceFrom: 4,
            SequenceTo: 7);

        var encoded = Protocol64FrameCodec.Encode(registry, value, 9, 3);
        Assert.True(encoded.Succeeded, encoded.Fault?.Message);
        var decoded = Protocol64FrameCodec.Decode<Protocol64RetransmitResponse>(encoded.Payload!, registry);

        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        Assert.Equal(value, decoded.Event);
    }

    private static TMessage RoundTrip<TMessage>(
        Protocol64SchemaRegistry registry,
        TMessage message,
        ulong frameId)
        where TMessage : class, IProtocolMessage
    {
        var encoded = Protocol64FrameCodec.Encode(
            registry,
            message,
            connectionEpoch: 9,
            frameId,
            options: new Protocol64FrameEncodeOptions
            {
                Compression = Protocol64Compression.None,
            });

        Assert.True(encoded.Succeeded, encoded.Fault?.Message);

        var decoded = Protocol64FrameCodec.Decode<TMessage>(
            encoded.Payload!,
            registry);

        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        return decoded.Event!;
    }

    private static SnapshotMessage CreateMinimalSnapshot()
        => new(
            Frame: 42,
            TickRate: 60,
            LevelName: "ctf_test",
            MapAreaIndex: 0,
            MapAreaCount: 1,
            GameMode: 1,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 600,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 17,
            RedIntel: new SnapshotIntelState(1, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(2, 0f, 0f, true, false, 0),
            Players: Array.Empty<SnapshotPlayerState>(),
            CombatTraces: Array.Empty<SnapshotCombatTraceState>(),
            SniperAimIndicators: Array.Empty<SnapshotSniperAimIndicatorState>(),
            Sentries: Array.Empty<SnapshotSentryState>(),
            Shots: Array.Empty<SnapshotShotState>(),
            Bubbles: Array.Empty<SnapshotShotState>(),
            Blades: Array.Empty<SnapshotShotState>(),
            Needles: Array.Empty<SnapshotShotState>(),
            RevolverShots: Array.Empty<SnapshotShotState>(),
            Rockets: Array.Empty<SnapshotRocketState>(),
            Flames: Array.Empty<SnapshotFlameState>(),
            Flares: Array.Empty<SnapshotShotState>(),
            Mines: Array.Empty<SnapshotMineState>(),
            DeadBodies: Array.Empty<SnapshotDeadBodyState>(),
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: Array.Empty<SnapshotControlPointState>(),
            Generators: Array.Empty<SnapshotGeneratorState>(),
            LocalDeathCam: null,
            KillFeed: Array.Empty<SnapshotKillFeedEntry>(),
            VisualEvents: Array.Empty<SnapshotVisualEvent>(),
            DamageEvents: Array.Empty<SnapshotDamageEvent>(),
            SoundEvents: Array.Empty<SnapshotSoundEvent>());
}
