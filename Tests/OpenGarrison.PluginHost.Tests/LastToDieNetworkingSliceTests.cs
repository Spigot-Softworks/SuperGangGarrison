using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using OpenGarrison.Server.LastToDie;
using System.Collections;
using System.Net;
using System.Reflection;
using System.Text;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieNetworkingSliceTests
{
    private static readonly Guid RunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid HostId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid GuestId = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
    private static readonly Guid HostClientInstanceId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid GuestClientInstanceId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Fact]
    public void SemanticMessagesRoundTripThroughLegacyAndProtocol64Codecs()
    {
        Assert.Equal(86, ProtocolVersion.Current);
        var hello = new HelloMessage(
            "Host",
            ProtocolVersion.Current,
            0,
            ClientInstanceId: HostClientInstanceId);
        Assert.Equal(hello, LegacyRoundTrip<HelloMessage>(hello));
        Assert.Equal(hello, Protocol64RoundTrip(hello));
        var command = new LastToDieCommandMessage(
            CommandId: 7,
            RunId,
            ExpectedStructuralRevision: 12,
            LastToDieCommandKind.SelectReward,
            StageInstanceId: 1,
            OfferId: 4,
            SelectedId: LastToDiePerkIds.Spy.Blunderbuss1.Value);
        Assert.Equal(command, LegacyRoundTrip<LastToDieCommandMessage>(command));
        Assert.Equal(command, Protocol64RoundTrip(command));
        var retry = command with
        {
            CommandId = 8,
            Kind = LastToDieCommandKind.Retry,
            StageInstanceId = 0,
            OfferId = 0,
            SelectedId = string.Empty,
        };
        Assert.Equal(retry, LegacyRoundTrip<LastToDieCommandMessage>(retry));
        Assert.Equal(retry, Protocol64RoundTrip(retry));

        var result = new LastToDieCommandResultMessage(
            command.CommandId,
            LastToDieCommandResultKind.Accepted,
            AuthoritativeStructuralRevision: 13);
        Assert.Equal(result, LegacyRoundTrip<LastToDieCommandResultMessage>(result));
        Assert.Equal(result, Protocol64RoundTrip(result));

        var snapshot = CreateWireSnapshot();
        AssertSnapshotsEqual(snapshot, LegacyRoundTrip<LastToDieRunSnapshotMessage>(snapshot));
        AssertSnapshotsEqual(snapshot, Protocol64RoundTrip(snapshot));

        var acknowledgement = new LastToDieRunSnapshotAckMessage(
            snapshot.RunId,
            snapshot.StructuralRevision);
        Assert.Equal(
            acknowledgement,
            LegacyRoundTrip<LastToDieRunSnapshotAckMessage>(acknowledgement));
        Assert.Equal(acknowledgement, Protocol64RoundTrip(acknowledgement));
    }

    [Fact]
    public void TwoPeersConvergeThroughValidatedCommandsAndRecipientSpecificSnapshots()
    {
        var server = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort", "Conflict"],
            LastToDieDifficulty.Standard,
            seed: 1234,
            runId: RunId);
        var controller = new LastToDieProtocolController(server);
        Assert.True(controller.TryRegisterPlayer(slot: 1, HostId, out var hostRegistrationError), hostRegistrationError);
        Assert.True(controller.TryRegisterPlayer(slot: 2, GuestId, out var guestRegistrationError), guestRegistrationError);
        SetAllLobbyPlayersReady(controller);

        var hostState = new LastToDieReplicatedState();
        var guestState = new LastToDieReplicatedState();
        Publish(controller, hostState, guestState, serverTick: 10);
        Assert.Equal(hostState.Snapshot!.StructuralRevision, guestState.Snapshot!.StructuralRevision);
        Assert.True(GetPlayer(hostState, slot: 1).IsHost);
        Assert.False(GetPlayer(hostState, slot: 2).IsHost);

        var start = Handle(
            controller,
            slot: 1,
            commandId: 1,
            LastToDieCommandKind.RequestStart,
            expectedRevision: hostState.Snapshot.StructuralRevision);
        Assert.Equal(LastToDieCommandResultKind.Accepted, start.Result.Result);
        Publish(controller, hostState, guestState, serverTick: 11);
        Assert.Equal(LastToDieWirePhase.SurvivorChoice, hostState.Snapshot!.Phase);

        var hostSurvivor = Handle(
            controller,
            slot: 1,
            commandId: 2,
            LastToDieCommandKind.ChooseSurvivor,
            hostState.Snapshot.StructuralRevision,
            selectedId: LastToDieSurvivorCatalog.SpyId.Value);
        Assert.Equal(LastToDieCommandResultKind.Accepted, hostSurvivor.Result.Result);

        var guestSurvivor = Handle(
            controller,
            slot: 2,
            commandId: 1,
            LastToDieCommandKind.ChooseSurvivor,
            guestState.Snapshot!.StructuralRevision,
            selectedId: LastToDieSurvivorCatalog.MedicId.Value);
        Assert.Equal(LastToDieCommandResultKind.Accepted, guestSurvivor.Result.Result);
        Publish(controller, hostState, guestState, serverTick: 12);

        Assert.Equal(LastToDieWirePhase.RewardChoice, hostState.Snapshot!.Phase);
        Assert.Equal(hostState.Snapshot.StructuralRevision, guestState.Snapshot!.StructuralRevision);
        var hostViewOfHost = GetPlayer(hostState, slot: 1);
        var hostViewOfGuest = GetPlayer(hostState, slot: 2);
        var guestViewOfHost = GetPlayer(guestState, slot: 1);
        var guestViewOfGuest = GetPlayer(guestState, slot: 2);
        Assert.Equal(3, hostViewOfHost.ActiveOfferChoices.Count);
        Assert.Empty(hostViewOfGuest.ActiveOfferChoices);
        Assert.Empty(guestViewOfHost.ActiveOfferChoices);
        Assert.Equal(3, guestViewOfGuest.ActiveOfferChoices.Count);

        var hostReward = Handle(
            controller,
            slot: 1,
            commandId: 3,
            LastToDieCommandKind.SelectReward,
            hostState.Snapshot.StructuralRevision,
            hostViewOfHost.ActiveOfferId,
            hostViewOfHost.ActiveOfferChoices[0]);
        Assert.Equal(LastToDieCommandResultKind.Accepted, hostReward.Result.Result);
        var duplicateHostReward = controller.HandleCommand(
            slot: 1,
            new LastToDieCommandMessage(
                3,
                RunId,
                hostState.Snapshot.StructuralRevision,
                LastToDieCommandKind.SelectReward,
                hostState.Snapshot.StageInstanceId,
                hostViewOfHost.ActiveOfferId,
                hostViewOfHost.ActiveOfferChoices[0]));
        Assert.Equal(hostReward.Result, duplicateHostReward.Result);
        Assert.False(duplicateHostReward.StateChanged);

        var forgedReward = Handle(
            controller,
            slot: 2,
            commandId: 2,
            LastToDieCommandKind.SelectReward,
            guestState.Snapshot!.StructuralRevision,
            hostViewOfHost.ActiveOfferId,
            hostViewOfHost.ActiveOfferChoices[0]);
        Assert.Equal(LastToDieCommandResultKind.Rejected, forgedReward.Result.Result);
        Assert.Contains("stale or does not belong", forgedReward.Result.Reason, StringComparison.OrdinalIgnoreCase);

        var guestReward = Handle(
            controller,
            slot: 2,
            commandId: 3,
            LastToDieCommandKind.SelectReward,
            guestState.Snapshot.StructuralRevision,
            guestViewOfGuest.ActiveOfferId,
            guestViewOfGuest.ActiveOfferChoices[0]);
        Assert.Equal(LastToDieCommandResultKind.Accepted, guestReward.Result.Result);
        Publish(controller, hostState, guestState, serverTick: 14);
        Assert.Equal(LastToDieWirePhase.LoadingStage, hostState.Snapshot!.Phase);
        Assert.Equal(hostState.Snapshot.StructuralRevision, guestState.Snapshot!.StructuralRevision);

        var stageInstanceId = hostState.Snapshot.StageInstanceId;
        Assert.True(
            controller.TryOpenStageBarrier(stageInstanceId, baselineStartFrame: 500, out var barrierError),
            barrierError);
        Publish(controller, hostState, guestState, serverTick: 15);
        Assert.Equal(500UL, hostState.Snapshot!.BaselineStartFrame);

        var hostContentReady = Handle(
            controller,
            slot: 1,
            commandId: 4,
            LastToDieCommandKind.StageContentReady,
            hostState.Snapshot.StructuralRevision,
            selectedId: hostState.Snapshot.CurrentMap);
        Assert.Equal(LastToDieCommandResultKind.Accepted, hostContentReady.Result.Result);
        Assert.False(GetPlayer(controller.CreateSnapshot(1, serverTick: 15), slot: 1).IsReady);
        Assert.False(
            controller.TryAcknowledgeWorldBaseline(
                slot: 1,
                snapshotFrame: 499,
                out _,
                out _));
        Assert.True(
            controller.TryAcknowledgeWorldBaseline(
                slot: 1,
                snapshotFrame: 500,
                out var hostBarrierChanged,
                out var hostBaselineError),
            hostBaselineError);
        Assert.True(hostBarrierChanged);
        Assert.True(GetPlayer(controller.CreateSnapshot(1, serverTick: 16), slot: 1).IsReady);
        Assert.False(GetPlayer(controller.CreateSnapshot(2, serverTick: 16), slot: 2).IsReady);

        Assert.True(
            controller.TryAcknowledgeWorldBaseline(
                slot: 2,
                snapshotFrame: 505,
                out var guestBaselineChanged,
                out var guestBaselineError),
            guestBaselineError);
        Assert.True(guestBaselineChanged);
        Assert.False(GetPlayer(controller.CreateSnapshot(2, serverTick: 16), slot: 2).IsReady);
        var guestContentReady = Handle(
            controller,
            slot: 2,
            commandId: 4,
            LastToDieCommandKind.StageContentReady,
            controller.Director.StructuralRevision,
            selectedId: guestState.Snapshot!.CurrentMap);
        Assert.Equal(LastToDieCommandResultKind.Accepted, guestContentReady.Result.Result);
        Assert.True(GetPlayer(controller.CreateSnapshot(2, serverTick: 17), slot: 2).IsReady);
        Assert.True(controller.TrySetPlayerConnected(2, isConnected: false, out var disconnectError), disconnectError);
        Assert.True(GetPlayer(controller.CreateSnapshot(2, serverTick: 17), slot: 2).IsReady);
        Assert.True(controller.TrySetPlayerConnected(2, isConnected: true, out var reconnectError), reconnectError);
        Assert.False(GetPlayer(controller.CreateSnapshot(2, serverTick: 17), slot: 2).IsReady);
        Assert.True(controller.TryAcknowledgeWorldBaseline(
            2,
            snapshotFrame: 505,
            out _,
            out var reconnectBaselineError), reconnectBaselineError);
        var reconnectContentReady = Handle(
            controller,
            slot: 2,
            commandId: 5,
            LastToDieCommandKind.StageContentReady,
            controller.Director.StructuralRevision,
            selectedId: guestState.Snapshot.CurrentMap);
        Assert.Equal(LastToDieCommandResultKind.Accepted, reconnectContentReady.Result.Result);
        Assert.True(GetPlayer(controller.CreateSnapshot(2, serverTick: 18), slot: 2).IsReady);
        Assert.True(controller.Director.TryBeginStage(serverTick: 600, out var beginStageError), beginStageError);
        Assert.Equal(LastToDiePhase.Playing, controller.Director.Phase);

        var acknowledgement = hostState.CreateSnapshotAcknowledgement();
        Assert.NotNull(acknowledgement);
        Assert.True(controller.TryAcknowledgeSnapshot(1, acknowledgement!, out var acknowledgementError), acknowledgementError);
        Assert.Equal(
            hostState.Snapshot.StructuralRevision,
            controller.GetLastAcknowledgedStructuralRevision(1));
    }

    [Fact]
    public void LobbyReadyCommandsGateHostStartAndEscapeStyleUnlockClearsChoice()
    {
        var server = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 4321,
            runId: RunId);
        var controller = new LastToDieProtocolController(server);
        Assert.True(controller.TryRegisterPlayer(1, HostId, out var hostError), hostError);
        Assert.True(controller.TryRegisterPlayer(2, GuestId, out var guestError), guestError);

        var lobby = controller.CreateSnapshot(1, 1);
        Assert.Equal((byte)2, lobby.MaximumPlayers);
        Assert.Equal(
            LastToDieCommandResultKind.Rejected,
            Handle(controller, 1, 1, LastToDieCommandKind.RequestStart, lobby.StructuralRevision).Result.Result);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(controller, 1, 2, LastToDieCommandKind.Ready, controller.Director.StructuralRevision).Result.Result);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(controller, 2, 1, LastToDieCommandKind.Ready, controller.Director.StructuralRevision).Result.Result);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(controller, 1, 3, LastToDieCommandKind.RequestStart, controller.Director.StructuralRevision).Result.Result);

        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                1,
                4,
                LastToDieCommandKind.ChooseSurvivor,
                controller.Director.StructuralRevision,
                selectedId: LastToDieSurvivorCatalog.SpyId.Value).Result.Result);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(controller, 1, 5, LastToDieCommandKind.Unready, controller.Director.StructuralRevision).Result.Result);
        var unlocked = controller.CreateSnapshot(1, 2);
        Assert.Equal(LastToDieWirePhase.SurvivorChoice, unlocked.Phase);
        Assert.Equal(string.Empty, GetPlayer(unlocked, 1).SurvivorId);
    }

    [Fact]
    public void ClientStateRejectsOlderRevisionAndAnotherRun()
    {
        var state = new LastToDieReplicatedState();
        var current = CreateWireSnapshot();
        Assert.True(state.ApplySnapshot(current).Applied);
        Assert.Equal(
            LastToDieSnapshotApplyKind.Duplicate,
            state.ApplySnapshot(Protocol64RoundTrip(current)).Kind);

        var stale = current with
        {
            StructuralRevision = current.StructuralRevision - 1,
            ServerTick = current.ServerTick + 1,
        };
        Assert.Equal(LastToDieSnapshotApplyKind.Stale, state.ApplySnapshot(stale).Kind);

        var anotherRun = current with
        {
            RunId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            StructuralRevision = current.StructuralRevision + 1,
        };
        Assert.Equal(LastToDieSnapshotApplyKind.Rejected, state.ApplySnapshot(anotherRun).Kind);
        Assert.Equal(current.RunId, state.Snapshot!.RunId);
    }

    [Fact]
    public void Protocol64ClientRetriesTheSamePendingCommandId()
    {
        using var client = new NetworkGameClient();
        var transport = new RecordingClientTransport("ws64://127.0.0.1:8190");
        Assert.True(client.Connect(transport, "Tester", 0, out var error), error);
        Assert.True(client.Protocol64ModeEnabled);
        Assert.True(client.LastToDieState.ApplySnapshot(CreateWireSnapshot()).Applied);

        var commandId = client.SendLastToDieCommand(LastToDieCommandKind.RequestStart);
        Assert.NotEqual(0UL, commandId);
        var sentBeforeRetry = transport.SentPayloads.Count;

        var pendingField = typeof(NetworkGameClient).GetField(
            "_pendingLastToDieCommands",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var pendingCommands = Assert.IsAssignableFrom<IDictionary>(pendingField!.GetValue(client));
        var pending = pendingCommands[commandId];
        Assert.NotNull(pending);
        pending.GetType().GetProperty("LastSentAtMilliseconds")!
            .SetValue(pending, -1_000L);

        typeof(NetworkGameClient).GetMethod(
                "FlushLastToDieCommands",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(client, null);

        Assert.Equal(sentBeforeRetry + 1, transport.SentPayloads.Count);
    }

    [Fact]
    public void ClientReusesOneNonEmptyInstanceIdAcrossTransportReconnects()
    {
        using var client = new NetworkGameClient();
        var firstTransport = new RecordingClientTransport("127.0.0.1:8190");
        Assert.True(client.Connect(firstTransport, "Tester", 0, out var firstError), firstError);
        Assert.True(ProtocolCodec.TryDeserialize(Assert.Single(firstTransport.SentPayloads), out var firstMessage));
        var firstHello = Assert.IsType<HelloMessage>(firstMessage);
        Assert.NotEqual(Guid.Empty, firstHello.ClientInstanceId);

        var secondTransport = new RecordingClientTransport("127.0.0.1:8190");
        Assert.True(client.Connect(secondTransport, "Tester", 0, out var secondError), secondError);
        Assert.True(ProtocolCodec.TryDeserialize(Assert.Single(secondTransport.SentPayloads), out var secondMessage));
        var secondHello = Assert.IsType<HelloMessage>(secondMessage);

        Assert.Equal(firstHello.ClientInstanceId, secondHello.ClientInstanceId);
    }

    [Fact]
    public void DispatcherRebindsMatchingClientInstanceBeforeOrdinarySlotAllocation()
    {
        var world = new SimulationWorld();
        world.CompleteLocalPlayerJoin(PlayerClass.Spy);
        var oldPeer = new IPEndPoint(IPAddress.Loopback, 8191);
        var replacementPeer = new IPEndPoint(IPAddress.Loopback, 8291);
        var oldClient = new ClientSession(
            1,
            100,
            oldPeer,
            "Host",
            TimeSpan.Zero,
            HostClientInstanceId)
        {
            IsAuthorized = true,
        };
        var clients = new Dictionary<byte, ClientSession> { [1] = oldClient };
        var sent = new List<(ServerTransportPeer Peer, IProtocolMessage Message)>();
        var sessionManager = new ServerSessionManager(
            world,
            clients,
            maxPlayableClients: 2,
            maxTotalClients: 2,
            maxSpectatorClients: 0,
            nowProvider: () => TimeSpan.Zero,
            serverPassword: null,
            passwordRequired: false,
            clientTimeoutSeconds: 20,
            passwordTimeoutSeconds: 20,
            passwordRetrySeconds: 5,
            getPasswordRateLimitReason: static _ => null,
            recordPasswordFailure: static _ => { },
            clearPasswordFailures: static _ => { },
            sendMessage: (peer, message) => sent.Add((peer, message)),
            log: static _ => { });
        sessionManager.ConfigurePlayableClientLifecyclePolicy(
            retainPlayableSlotOnDisconnect: static slot => slot == 1,
            canAcceptPlayableInput: static _ => false);
        var dispatcher = new ServerIncomingMessageDispatcher(
            new SimulationConfig(),
            "Test Server",
            passwordRequired: false,
            maxPlayableClients: 2,
            maxTotalClients: 2,
            maxSpectatorClients: 0,
            clients,
            sessionManager,
            world,
            () => TimeSpan.Zero,
            static () => null,
            static () => 101,
            static _ => null,
            static _ => { },
            static () => (false, string.Empty, string.Empty),
            (peer, message) => sent.Add((peer, message)),
            static _ => { },
            static _ => { },
            static (_, _, _) => { },
            static (_, _) => { },
            static _ => { },
            isPlayableSlotAvailable: static _ => true,
            resolveLastToDieReconnectSlot: clientInstanceId =>
                clientInstanceId == HostClientInstanceId ? (byte)1 : (byte)0);

        dispatcher.Dispatch(
            new HelloMessage(
                "Host",
                ProtocolVersion.Current,
                0,
                ClientInstanceId: HostClientInstanceId),
            replacementPeer);

        var replacement = Assert.Single(clients).Value;
        Assert.Equal((byte)1, replacement.Slot);
        Assert.Equal(HostClientInstanceId, replacement.ClientInstanceId);
        Assert.Equal(replacementPeer, replacement.EndPoint);
        Assert.Contains(sent, item =>
            item.Peer == replacement.Peer
            && item.Message is WelcomeMessage { PlayerSlot: 1 });
    }

    [Fact]
    public void AuthoritativeRosterRejectsLateNewPlayersAndBoundsCommandLedger()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 91,
            runId: RunId,
            maximumPlayers: 1);
        var controller = new LastToDieProtocolController(serverDirector);
        var session = new LastToDieNetworkSession(
            controller,
            () => 1,
            (_, _) => { });
        var host = new ClientSession(
            slot: 1,
            userId: 100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            name: "Host",
            lastSeen: TimeSpan.Zero);
        var lateGuest = new ClientSession(
            slot: 2,
            userId: 101,
            new IPEndPoint(IPAddress.Parse("192.0.2.2"), 8192),
            name: "Late Guest",
            lastSeen: TimeSpan.Zero);

        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([host]));
        SetAllLobbyPlayersReady(controller);
        var snapshot = controller.CreateSnapshot(1, serverTick: 1);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                slot: 1,
                commandId: 1,
                LastToDieCommandKind.RequestStart,
                snapshot.StructuralRevision).Result.Result);

        var rejected = new List<(ClientSession Client, string Reason)>();
        Assert.Empty(session.SynchronizeAuthorizedClients(
            [host, lateGuest],
            (client, reason) => rejected.Add((client, reason))));
        var rejection = Assert.Single(rejected);
        Assert.Equal(lateGuest, rejection.Client);
        Assert.Contains("Lobby", rejection.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(controller.CreateSnapshot(1, serverTick: 2).Players);

        for (ulong commandId = 2; commandId <= 130; commandId += 1)
        {
            controller.HandleCommand(
                slot: 1,
                new LastToDieCommandMessage(
                    commandId,
                    RunId,
                    controller.Director.StructuralRevision,
                    LastToDieCommandKind.RequestStart));
        }

        var reusedEvictedId = controller.HandleCommand(
            slot: 1,
            new LastToDieCommandMessage(
                CommandId: 1,
                RunId,
                controller.Director.StructuralRevision,
                LastToDieCommandKind.ChooseSurvivor,
                SelectedId: LastToDieSurvivorCatalog.SpyId.Value));
        Assert.NotEqual(LastToDieCommandResultKind.Duplicate, reusedEvictedId.Result.Result);
    }

    [Fact]
    public void DuplicateCommandRetriesReplayCachedResultWithoutConsumingRateBudget()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 92,
            runId: RunId,
            maximumPlayers: 1);
        var controller = new LastToDieProtocolController(serverDirector);
        var sent = new List<IProtocolMessage>();
        var session = new LastToDieNetworkSession(
            controller,
            () => 1,
            (_, message) => sent.Add(message),
            ticksPerSecond: 30);
        var host = new ClientSession(
            slot: 1,
            userId: 100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            name: "Host",
            lastSeen: TimeSpan.Zero);
        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([host]));
        SetAllLobbyPlayersReady(controller);

        var command = new LastToDieCommandMessage(
            CommandId: 1,
            RunId,
            controller.Director.StructuralRevision,
            LastToDieCommandKind.RequestStart);
        session.HandleCommand(host, command);
        var accepted = Assert.Single(sent.OfType<LastToDieCommandResultMessage>());
        Assert.Equal(LastToDieCommandResultKind.Accepted, accepted.Result);

        sent.Clear();
        for (var retry = 0; retry < 40; retry += 1)
        {
            session.HandleCommand(host, command);
        }

        Assert.Equal(40, sent.Count);
        Assert.All(
            sent,
            message => Assert.Equal(accepted, Assert.IsType<LastToDieCommandResultMessage>(message)));

        sent.Clear();
        session.HandleCommand(
            host,
            new LastToDieCommandMessage(
                CommandId: 2,
                RunId,
                controller.Director.StructuralRevision,
                LastToDieCommandKind.ChooseSurvivor,
                SelectedId: LastToDieSurvivorCatalog.SpyId.Value));
        var freshResult = Assert.Single(sent.OfType<LastToDieCommandResultMessage>());
        Assert.DoesNotContain("rate limit", freshResult.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HostedDeveloperVictoryCommandAdvancesAuthoritativeRunAndBroadcastsRewardChoice()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort", "Conflict"],
            LastToDieDifficulty.Standard,
            seed: 93,
            ticksPerSecond: 30,
            runId: RunId,
            maximumPlayers: 1);
        var controller = new LastToDieProtocolController(serverDirector);
        var sent = new List<IProtocolMessage>();
        long serverTick = 1;
        var session = new LastToDieNetworkSession(
            controller,
            () => serverTick,
            (_, message) => sent.Add(message),
            ticksPerSecond: 30);
        var host = new ClientSession(
            slot: 1,
            userId: 100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            name: "Host",
            lastSeen: TimeSpan.Zero);
        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([host]));
        StartSinglePlayerStage(session, controller, host, ref serverTick);
        sent.Clear();

        Assert.True(session.TryTriggerStageVictoryForTesting(out var error), error);

        Assert.Equal(LastToDiePhase.RewardChoice, controller.Director.Phase);
        var snapshot = Assert.IsType<LastToDieRunSnapshotMessage>(
            Assert.Single(sent.OfType<LastToDieRunSnapshotMessage>()));
        Assert.Equal(LastToDieWirePhase.RewardChoice, snapshot.Phase);

        sent.Clear();
        var localPlayer = GetPlayer(snapshot, host.Slot);
        session.HandleCommand(
            host,
            new LastToDieCommandMessage(
                CommandId: 5,
                RunId,
                snapshot.StructuralRevision,
                LastToDieCommandKind.SelectReward,
                snapshot.StageInstanceId,
                localPlayer.ActiveOfferId,
                localPlayer.ActiveOfferChoices[0]));

        var result = Assert.Single(sent.OfType<LastToDieCommandResultMessage>());
        Assert.Equal(LastToDieCommandResultKind.Accepted, result.Result);
        Assert.Equal(LastToDiePhase.LoadingStage, controller.Director.Phase);
    }

    [Theory]
    [InlineData(LastToDiePhase.Lobby, true)]
    [InlineData(LastToDiePhase.SurvivorChoice, true)]
    [InlineData(LastToDiePhase.RewardChoice, true)]
    [InlineData(LastToDiePhase.Won, true)]
    [InlineData(LastToDiePhase.Lost, true)]
    [InlineData(LastToDiePhase.LoadingStage, false)]
    [InlineData(LastToDiePhase.Playing, false)]
    public void LastToDieStageEnemiesExistOnlyDuringLoadingAndPlaying(
        LastToDiePhase phase,
        bool expected)
    {
        Assert.Equal(expected, GameServer.ShouldDeactivateLastToDieStageWorld(phase));
    }

    [Fact]
    public void DirectCoopDisconnectPolicyRetainsThePlayableEntityAndBuild()
    {
        var world = new SimulationWorld();
        world.CompleteLocalPlayerJoin(PlayerClass.Spy);
        world.LocalPlayer.ForceSetHealth(42);
        var client = new ClientSession(
            slot: SimulationWorld.LocalPlayerSlot,
            userId: 100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            name: "Host",
            lastSeen: TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession> { [client.Slot] = client };
        var sessionManager = new ServerSessionManager(
            world,
            clients,
            maxPlayableClients: 2,
            maxTotalClients: 2,
            maxSpectatorClients: 0,
            nowProvider: () => TimeSpan.Zero,
            serverPassword: null,
            passwordRequired: false,
            clientTimeoutSeconds: 20,
            passwordTimeoutSeconds: 20,
            passwordRetrySeconds: 5,
            getPasswordRateLimitReason: static _ => null,
            recordPasswordFailure: static _ => { },
            clearPasswordFailures: static _ => { },
            sendMessage: static (_, _) => { },
            log: static _ => { });
        sessionManager.ConfigurePlayableClientLifecyclePolicy(
            retainPlayableSlotOnDisconnect: static _ => true,
            canAcceptPlayableInput: static _ => false);

        sessionManager.RemoveClient(client.Slot, "test disconnect");

        Assert.Empty(clients);
        Assert.False(world.IsNetworkPlayerAwaitingJoin(client.Slot));
        Assert.Equal(PlayerClass.Spy, world.LocalPlayer.ClassId);
        Assert.Equal(42, world.LocalPlayer.Health);
    }

    [Fact]
    public void UnacknowledgedRunSnapshotsRetryAndStopAfterAcknowledgement()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 93,
            runId: RunId);
        var controller = new LastToDieProtocolController(serverDirector);
        var sent = new List<IProtocolMessage>();
        var session = new LastToDieNetworkSession(
            controller,
            () => 1,
            (_, message) => sent.Add(message));
        var host = new ClientSession(
            slot: 1,
            userId: 100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            name: "Host",
            lastSeen: TimeSpan.Zero);
        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([host]));

        sent.Clear();
        session.ResendUnacknowledgedSnapshots();
        var retry = Assert.Single(sent);
        var snapshot = Assert.IsType<LastToDieRunSnapshotMessage>(retry);
        Assert.True(controller.TryAcknowledgeSnapshot(
            1,
            new LastToDieRunSnapshotAckMessage(snapshot.RunId, snapshot.StructuralRevision),
            out var error), error);

        sent.Clear();
        session.ResendUnacknowledgedSnapshots();
        Assert.Empty(sent);
    }

    [Fact]
    public void AuthorizedRosterSynchronizationBroadcastsJoinAndDisconnectState()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 42,
            ticksPerSecond: 30,
            runId: RunId);
        var controller = new LastToDieProtocolController(serverDirector);
        long serverTick = 1;
        var sent = new List<(ServerTransportPeer Peer, IProtocolMessage Message)>();
        var session = new LastToDieNetworkSession(
            controller,
            () => serverTick,
            (peer, message) => sent.Add((peer, message)),
            ticksPerSecond: 30);
        var host = new ClientSession(
            slot: 1,
            userId: 100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            name: "Host",
            lastSeen: TimeSpan.Zero);
        var guest = new ClientSession(
            slot: 2,
            userId: 101,
            new IPEndPoint(IPAddress.Parse("192.0.2.2"), 8192),
            name: "Guest",
            lastSeen: TimeSpan.FromMilliseconds(1));

        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([host]));
        sent.Clear();
        serverTick += 1;
        Assert.Equal([(byte)2], session.SynchronizeAuthorizedClients([host, guest]));

        var hostJoinSnapshot = Assert.Single(
            sent,
            item => item.Peer == host.Peer
                && item.Message is LastToDieRunSnapshotMessage).Message;
        Assert.Equal(
            2,
            Assert.IsType<LastToDieRunSnapshotMessage>(hostJoinSnapshot).Players.Count);

        sent.Clear();
        serverTick += 1;
        Assert.Empty(session.SynchronizeAuthorizedClients([host]));

        var disconnectSnapshot = Assert.IsType<LastToDieRunSnapshotMessage>(
            Assert.Single(sent, item => item.Peer == host.Peer).Message);
        Assert.False(GetPlayer(disconnectSnapshot, slot: 2).IsConnected);
    }

    [Fact]
    public void PeerReplacementInvalidatesLoadingProofsAndAliveReconnectDoesNotBecomeAGhost()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 77,
            ticksPerSecond: 30,
            runId: RunId,
            maximumPlayers: 1);
        var controller = new LastToDieProtocolController(serverDirector);
        long serverTick = 1;
        var session = new LastToDieNetworkSession(
            controller,
            () => serverTick,
            (_, _) => { },
            ticksPerSecond: 30);
        var original = new ClientSession(
            slot: 1,
            userId: 100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            name: "Host",
            lastSeen: TimeSpan.Zero);
        var replacement = new ClientSession(
            slot: 1,
            userId: 100,
            new IPEndPoint(IPAddress.Loopback, 8291),
            name: "Host",
            lastSeen: TimeSpan.FromMilliseconds(1));

        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([original]));
        SetAllLobbyPlayersReady(controller);
        var snapshot = controller.CreateSnapshot(1, serverTick);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(controller, 1, 1, LastToDieCommandKind.RequestStart, snapshot.StructuralRevision).Result.Result);
        snapshot = controller.CreateSnapshot(1, ++serverTick);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                1,
                2,
                LastToDieCommandKind.ChooseSurvivor,
                snapshot.StructuralRevision,
                selectedId: LastToDieSurvivorCatalog.SpyId.Value).Result.Result);
        snapshot = controller.CreateSnapshot(1, ++serverTick);
        var offer = GetPlayer(snapshot, 1);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                1,
                3,
                LastToDieCommandKind.SelectReward,
                snapshot.StructuralRevision,
                offer.ActiveOfferId,
                offer.ActiveOfferChoices[0]).Result.Result);
        snapshot = controller.CreateSnapshot(1, ++serverTick);
        Assert.Equal(LastToDieWirePhase.LoadingStage, snapshot.Phase);
        Assert.True(controller.TryOpenStageBarrier(snapshot.StageInstanceId, 500, out var barrierError), barrierError);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                1,
                4,
                LastToDieCommandKind.StageContentReady,
                controller.Director.StructuralRevision,
                selectedId: snapshot.CurrentMap).Result.Result);
        Assert.True(controller.TryAcknowledgeWorldBaseline(1, 500, out _, out var baselineError), baselineError);
        Assert.True(GetPlayer(controller.CreateSnapshot(1, ++serverTick), 1).IsReady);

        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([replacement]));
        var reboundLoadingPlayer = GetPlayer(controller.CreateSnapshot(1, ++serverTick), 1);
        Assert.True(reboundLoadingPlayer.IsConnected);
        Assert.False(reboundLoadingPlayer.IsReady);

        Assert.True(controller.TryAcknowledgeWorldBaseline(1, 500, out _, out baselineError), baselineError);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                1,
                5,
                LastToDieCommandKind.StageContentReady,
                controller.Director.StructuralRevision,
                selectedId: snapshot.CurrentMap).Result.Result);
        Assert.True(controller.Director.TryBeginStage(++serverTick, out var beginError), beginError);
        Assert.True(GetPlayer(controller.CreateSnapshot(1, serverTick), 1).IsAlive);

        Assert.Empty(session.SynchronizeAuthorizedClients([]));
        var disconnectedPlayer = GetPlayer(controller.CreateSnapshot(1, ++serverTick), 1);
        Assert.False(disconnectedPlayer.IsConnected);
        Assert.False(disconnectedPlayer.IsAlive);
        Assert.True(session.ShouldRetainPlayableSlot(1));

        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([replacement]));
        var restoredPlayer = GetPlayer(controller.CreateSnapshot(1, ++serverTick), 1);
        Assert.True(restoredPlayer.IsConnected);
        Assert.True(restoredPlayer.IsAlive);
        Assert.False(session.CanAcceptGameplayInput(1));
        Assert.True(
            controller.TryAcknowledgeSnapshot(
                1,
                new LastToDieRunSnapshotAckMessage(
                    RunId,
                    controller.Director.StructuralRevision),
                out var semanticAckError),
            semanticAckError);
        Assert.False(session.CanAcceptGameplayInput(1));
        typeof(ClientSession).GetProperty(nameof(ClientSession.LastAcknowledgedSnapshotFrame))!
            .SetValue(replacement, 500UL);
        Assert.True(session.CanAcceptGameplayInput(1));
    }

    [Fact]
    public void StableClientIdentityReclaimsOnlyItsBoundLogicalSlot()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 78,
            runId: RunId);
        var controller = new LastToDieProtocolController(serverDirector);
        var session = new LastToDieNetworkSession(controller, () => 1, (_, _) => { });
        var original = new ClientSession(
            1,
            100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            "Host",
            TimeSpan.Zero,
            HostClientInstanceId);
        var replacement = new ClientSession(
            1,
            101,
            new IPEndPoint(IPAddress.Loopback, 8291),
            "Host",
            TimeSpan.FromMilliseconds(1),
            HostClientInstanceId);
        var impostor = new ClientSession(
            1,
            102,
            new IPEndPoint(IPAddress.Loopback, 8391),
            "Impostor",
            TimeSpan.FromMilliseconds(2),
            GuestClientInstanceId);

        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([original]));
        Assert.Equal((byte)1, session.ResolveReconnectSlot(HostClientInstanceId));
        Assert.Equal((byte)0, session.ResolveReconnectSlot(GuestClientInstanceId));

        Assert.Empty(session.SynchronizeAuthorizedClients([]));
        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([replacement]));

        var rejected = new List<string>();
        Assert.Empty(session.SynchronizeAuthorizedClients(
            [impostor],
            (_, reason) => rejected.Add(reason)));
        Assert.Contains(
            "identity",
            Assert.Single(rejected),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal((byte)1, session.ResolveReconnectSlot(HostClientInstanceId));
        Assert.Equal((byte)0, session.ResolveReconnectSlot(GuestClientInstanceId));
    }

    [Fact]
    public void ReconnectGraceRestoresInsideWindowButExpiresWithoutCreatingAGhost()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 79,
            ticksPerSecond: 30,
            runId: RunId,
            maximumPlayers: 1);
        var controller = new LastToDieProtocolController(serverDirector);
        long serverTick = 1;
        var session = new LastToDieNetworkSession(
            controller,
            () => serverTick,
            (_, _) => { },
            ticksPerSecond: 30);
        var original = new ClientSession(
            1,
            100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            "Host",
            TimeSpan.Zero,
            HostClientInstanceId);
        var replacement = new ClientSession(
            1,
            101,
            new IPEndPoint(IPAddress.Loopback, 8291),
            "Host",
            TimeSpan.FromMilliseconds(1),
            HostClientInstanceId);

        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([original]));
        StartSinglePlayerStage(session, controller, original, ref serverTick);

        Assert.Empty(session.SynchronizeAuthorizedClients([]));
        Assert.True(session.HasActiveReconnectGrace());
        var graceSnapshot = session.CreateSnapshot(1);
        Assert.True(GetPlayer(graceSnapshot, 1).ReconnectGraceEndServerTick > serverTick);
        Assert.False(GetPlayer(controller.CreateSnapshot(1, serverTick), 1).IsAlive);

        serverTick += 1;
        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([replacement]));
        Assert.True(GetPlayer(controller.CreateSnapshot(1, serverTick), 1).IsAlive);

        Assert.Empty(session.SynchronizeAuthorizedClients([]));
        serverTick += (30 * 30) + 1;
        session.Tick();
        Assert.False(session.HasActiveReconnectGrace());
        Assert.Equal([(byte)1], session.SynchronizeAuthorizedClients([replacement]));
        Assert.False(GetPlayer(controller.CreateSnapshot(1, serverTick), 1).IsAlive);

        Assert.True(controller.Director.TryAdvancePlayingState(
            serverTick,
            redObjectiveWon: false,
            blueObjectiveWon: false,
            anyAfterlifeWindowActive: session.HasActiveReconnectGrace(),
            out var advanceError), advanceError);
        Assert.Equal(LastToDiePhase.Lost, controller.Director.Phase);
    }

    [Fact]
    public void SelectionPhasesWaitIndefinitelyForEveryPlayer()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 80,
            ticksPerSecond: 30,
            runId: RunId);
        var controller = new LastToDieProtocolController(serverDirector);
        long serverTick = 1;
        var session = new LastToDieNetworkSession(
            controller,
            () => serverTick,
            (_, _) => { },
            ticksPerSecond: 30);
        var host = new ClientSession(
            1,
            100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            "Host",
            TimeSpan.Zero,
            HostClientInstanceId);
        var guest = new ClientSession(
            2,
            101,
            new IPEndPoint(IPAddress.Loopback, 8192),
            "Guest",
            TimeSpan.Zero,
            GuestClientInstanceId);
        Assert.Equal([(byte)1, (byte)2], session.SynchronizeAuthorizedClients([host, guest]));
        SetAllLobbyPlayersReady(controller);
        var participants = session.GetParticipants().ToDictionary(participant => participant.Slot);
        var lobby = controller.CreateSnapshot(1, serverTick);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(controller, 1, 1, LastToDieCommandKind.RequestStart, lobby.StructuralRevision).Result.Result);

        Assert.True(controller.Director.TrySelectSurvivor(
            participants[1].PlayerId,
            LastToDieSurvivorCatalog.SpyId,
            out var hostSurvivorError), hostSurvivorError);
        serverTick += 30 * 600;
        session.Tick();
        var survivorSnapshot = controller.CreateSnapshot(1, serverTick);
        Assert.Equal(LastToDiePhase.SurvivorChoice, controller.Director.Phase);
        Assert.Equal(LastToDieSurvivorCatalog.SpyId.Value, GetPlayer(survivorSnapshot, 1).SurvivorId);
        Assert.True(string.IsNullOrWhiteSpace(GetPlayer(survivorSnapshot, 2).SurvivorId));

        Assert.True(controller.Director.TrySelectSurvivor(
            participants[2].PlayerId,
            LastToDieSurvivorCatalog.MedicId,
            out var guestSurvivorError), guestSurvivorError);
        Assert.Equal(LastToDiePhase.RewardChoice, controller.Director.Phase);

        var rewardSnapshot = controller.CreateSnapshot(1, serverTick);
        var hostOffer = GetPlayer(rewardSnapshot, 1);
        Assert.True(controller.Director.TrySelectReward(
            participants[1].PlayerId,
            hostOffer.ActiveOfferId,
            new LastToDiePerkId(hostOffer.ActiveOfferChoices[0]),
            out var hostRewardError), hostRewardError);
        serverTick += 30 * 600;
        session.Tick();

        rewardSnapshot = controller.CreateSnapshot(1, serverTick);
        var guestRewardSnapshot = controller.CreateSnapshot(2, serverTick);
        Assert.Equal(LastToDiePhase.RewardChoice, controller.Director.Phase);
        Assert.Equal(0UL, GetPlayer(rewardSnapshot, 1).ActiveOfferId);
        Assert.Single(GetPlayer(rewardSnapshot, 1).OwnedPerkIds);
        Assert.NotEqual(0UL, GetPlayer(guestRewardSnapshot, 2).ActiveOfferId);
        Assert.Empty(GetPlayer(guestRewardSnapshot, 2).OwnedPerkIds);
    }

    [Fact]
    public void LoadingDeadlineDropsOnlyUnreadyGuestAndLetsReadyHostContinue()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 84,
            ticksPerSecond: 30,
            runId: RunId);
        var controller = new LastToDieProtocolController(serverDirector);
        long serverTick = 1;
        var disconnected = new List<ClientSession>();
        var session = new LastToDieNetworkSession(
            controller,
            () => serverTick,
            (_, _) => { },
            ticksPerSecond: 30,
            disconnectClient: (client, _) => disconnected.Add(client));
        var host = new ClientSession(
            1,
            100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            "Host",
            TimeSpan.Zero,
            HostClientInstanceId);
        var guest = new ClientSession(
            2,
            101,
            new IPEndPoint(IPAddress.Loopback, 8192),
            "Guest",
            TimeSpan.FromMilliseconds(1),
            GuestClientInstanceId);
        Assert.Equal([(byte)1, (byte)2], session.SynchronizeAuthorizedClients([host, guest]));
        SetAllLobbyPlayersReady(controller);

        var snapshot = controller.CreateSnapshot(1, serverTick);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(controller, 1, 1, LastToDieCommandKind.RequestStart, snapshot.StructuralRevision).Result.Result);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                1,
                2,
                LastToDieCommandKind.ChooseSurvivor,
                controller.Director.StructuralRevision,
                selectedId: LastToDieSurvivorCatalog.SpyId.Value).Result.Result);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                2,
                1,
                LastToDieCommandKind.ChooseSurvivor,
                controller.Director.StructuralRevision,
                selectedId: LastToDieSurvivorCatalog.MedicId.Value).Result.Result);
        var hostOffer = GetPlayer(controller.CreateSnapshot(1, serverTick), 1);
        var guestOffer = GetPlayer(controller.CreateSnapshot(2, serverTick), 2);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                1,
                3,
                LastToDieCommandKind.SelectReward,
                controller.Director.StructuralRevision,
                hostOffer.ActiveOfferId,
                hostOffer.ActiveOfferChoices[0]).Result.Result);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                2,
                2,
                LastToDieCommandKind.SelectReward,
                controller.Director.StructuralRevision,
                guestOffer.ActiveOfferId,
                guestOffer.ActiveOfferChoices[0]).Result.Result);
        snapshot = controller.CreateSnapshot(1, serverTick);
        Assert.Equal(LastToDieWirePhase.LoadingStage, snapshot.Phase);
        Assert.True(
            session.TryOpenStageBarrier(snapshot.StageInstanceId, baselineStartFrame: 500, out var barrierError),
            barrierError);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                1,
                4,
                LastToDieCommandKind.StageContentReady,
                controller.Director.StructuralRevision,
                selectedId: snapshot.CurrentMap).Result.Result);
        Assert.True(
            controller.TryAcknowledgeWorldBaseline(1, 500, out _, out var baselineError),
            baselineError);

        session.Tick();
        serverTick += 30 * 60;
        Assert.True(session.Tick());

        Assert.Equal(LastToDiePhase.Playing, controller.Director.Phase);
        Assert.Equal(guest, Assert.Single(disconnected));
        var playing = session.CreateSnapshot(1);
        Assert.True(GetPlayer(playing, 1).IsAlive);
        Assert.False(GetPlayer(playing, 2).IsConnected);
        Assert.False(GetPlayer(playing, 2).IsAlive);
        Assert.True(session.HasActiveReconnectGrace());

        var replacementGuest = new ClientSession(
            2,
            102,
            new IPEndPoint(IPAddress.Loopback, 8292),
            "Guest",
            TimeSpan.FromMilliseconds(2),
            GuestClientInstanceId);
        serverTick += 1;
        Assert.Equal([(byte)2], session.SynchronizeAuthorizedClients([host, replacementGuest]));
        Assert.True(GetPlayer(session.CreateSnapshot(2), 2).IsAlive);
    }

    [Fact]
    public void LeaveReleasesIdentityAndPromotesTheRemainingLobbyPlayer()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 81,
            runId: RunId);
        var controller = new LastToDieProtocolController(serverDirector);
        var sent = new List<(ServerTransportPeer Peer, IProtocolMessage Message)>();
        var disconnected = new List<ClientSession>();
        var session = new LastToDieNetworkSession(
            controller,
            () => 1,
            (peer, message) => sent.Add((peer, message)),
            disconnectClient: (client, _) => disconnected.Add(client));
        var host = new ClientSession(
            1,
            100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            "Host",
            TimeSpan.Zero,
            HostClientInstanceId);
        var guest = new ClientSession(
            2,
            101,
            new IPEndPoint(IPAddress.Loopback, 8192),
            "Guest",
            TimeSpan.Zero,
            GuestClientInstanceId);
        Assert.Equal([(byte)1, (byte)2], session.SynchronizeAuthorizedClients([host, guest]));

        session.HandleCommand(
            host,
            new LastToDieCommandMessage(
                1,
                RunId,
                controller.Director.StructuralRevision,
                LastToDieCommandKind.Leave));

        var result = Assert.Single(
            sent.Where(item => item.Peer == host.Peer)
                .Select(item => item.Message)
                .OfType<LastToDieCommandResultMessage>());
        Assert.Equal(LastToDieCommandResultKind.Accepted, result.Result);
        Assert.Equal(host, Assert.Single(disconnected));
        Assert.False(session.ShouldRetainPlayableSlot(1));
        Assert.Equal((byte)0, session.ResolveReconnectSlot(HostClientInstanceId));
        var remaining = controller.CreateSnapshot(2, 2);
        var remainingGuest = Assert.Single(remaining.Players);
        Assert.Equal((byte)2, remainingGuest.Slot);
        Assert.True(remainingGuest.IsHost);
    }

    [Fact]
    public void DisconnectedLobbyHostExpiresAndPromotesRemainingPlayer()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 83,
            ticksPerSecond: 30,
            runId: RunId);
        var controller = new LastToDieProtocolController(serverDirector);
        long serverTick = 1;
        var session = new LastToDieNetworkSession(
            controller,
            () => serverTick,
            (_, _) => { },
            ticksPerSecond: 30);
        var host = new ClientSession(
            1,
            100,
            new IPEndPoint(IPAddress.Loopback, 8191),
            "Host",
            TimeSpan.Zero,
            HostClientInstanceId);
        var guest = new ClientSession(
            2,
            101,
            new IPEndPoint(IPAddress.Loopback, 8192),
            "Guest",
            TimeSpan.FromMilliseconds(1),
            GuestClientInstanceId);

        Assert.Equal([(byte)1, (byte)2], session.SynchronizeAuthorizedClients([host, guest]));
        Assert.Empty(session.SynchronizeAuthorizedClients([guest]));
        Assert.True(session.HasActiveReconnectGrace());

        serverTick += (30 * 30) + 1;
        Assert.True(session.Tick());

        var snapshot = session.CreateSnapshot(2);
        var remaining = Assert.Single(snapshot.Players);
        Assert.Equal((byte)2, remaining.Slot);
        Assert.True(remaining.IsHost);
        Assert.Equal((byte)0, session.ResolveReconnectSlot(HostClientInstanceId));
        Assert.True(controller.Director.TrySetLobbyReady(remaining.PlayerId, true, out var readyError), readyError);
        var rejectedStart = controller.HandleCommand(
            2,
            new LastToDieCommandMessage(
                1,
                RunId,
                controller.Director.StructuralRevision,
                LastToDieCommandKind.RequestStart));
        Assert.Equal(LastToDieCommandResultKind.Rejected, rejectedStart.Result.Result);
        Assert.Contains("seat", rejectedStart.Result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MaximumProgressionSnapshotFitsConservativeUdpBudgetWhenCompressed()
    {
        var definitions = LastToDieExpansionPerkCatalog.CreateDefinitions();
        var spyPerks = definitions
            .Where(definition => definition.SurvivorId == LastToDieSurvivorCatalog.SpyId)
            .Take(9)
            .Select(definition => definition.Id.Value)
            .ToArray();
        var medicPerks = definitions
            .Where(definition => definition.SurvivorId == LastToDieSurvivorCatalog.MedicId)
            .Take(9)
            .Select(definition => definition.Id.Value)
            .ToArray();
        var snapshot = CreateWireSnapshot() with
        {
            Players =
            [
                new LastToDiePlayerSnapshotMessage(
                    Slot: 1,
                    HostId,
                    IsConnected: true,
                    SurvivorId: LastToDieSurvivorCatalog.SpyId.Value,
                    OwnedPerkIds: spyPerks,
                    ActiveOfferId: 0,
                    ActiveOfferOrdinal: 0,
                    ActiveOfferChoices: [],
                    IsReady: true,
                    IsAlive: true,
                    Kills: 100),
                new LastToDiePlayerSnapshotMessage(
                    Slot: 2,
                    GuestId,
                    IsConnected: true,
                    SurvivorId: LastToDieSurvivorCatalog.MedicId.Value,
                    OwnedPerkIds: medicPerks,
                    ActiveOfferId: 0,
                    ActiveOfferOrdinal: 0,
                    ActiveOfferChoices: [],
                    IsReady: true,
                    IsAlive: true,
                    Kills: 100,
                    ReconnectGraceEndServerTick: 800),
            ],
        };

        var uncompressed = ProtocolCodec.Serialize(snapshot, ProtocolCompressionSettings.Disabled);
        var serverSettings = ServerProtocolCompression.GetSettingsFor(snapshot);
        var compressed = ProtocolCodec.Serialize(snapshot, serverSettings);

        Assert.False(serverSettings.CompressOnlySnapshots);
        Assert.True(compressed.Length < uncompressed.Length);
        Assert.True(
            compressed.Length <= 1_200,
            $"Compressed LTD snapshot was {compressed.Length} bytes; expected no more than 1200.");
        Assert.True(ProtocolCodec.TryDeserialize(compressed, out var decoded));
        Assert.IsType<LastToDieRunSnapshotMessage>(decoded);
    }

    [Fact]
    public void CommandSchemaRejectsMalformedUtf8()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(1UL);
            writer.Write(RunId.ToByteArray());
            writer.Write(1UL);
            writer.Write((byte)LastToDieCommandKind.ChooseSurvivor);
            writer.Write(0UL);
            writer.Write(0UL);
            writer.Write((ushort)2);
            writer.Write((byte)0xC3);
            writer.Write((byte)0x28);
        }

        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        Assert.Throws<DecoderFallbackException>(() => new LastToDieCommandSchema().ReadBody(reader));
    }

    private static LastToDieCommandHandlingResult Handle(
        LastToDieProtocolController controller,
        byte slot,
        ulong commandId,
        LastToDieCommandKind kind,
        ulong expectedRevision,
        ulong offerId = 0,
        string selectedId = "")
        => controller.HandleCommand(
            slot,
            new LastToDieCommandMessage(
                commandId,
                RunId,
                expectedRevision,
                kind,
                controller.Director.CreateSnapshot().StageInstanceId,
                offerId,
                selectedId));

    private static void StartSinglePlayerStage(
        LastToDieNetworkSession session,
        LastToDieProtocolController controller,
        ClientSession client,
        ref long serverTick)
    {
        SetAllLobbyPlayersReady(controller);
        var snapshot = controller.CreateSnapshot(client.Slot, serverTick);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                client.Slot,
                1,
                LastToDieCommandKind.RequestStart,
                snapshot.StructuralRevision).Result.Result);
        snapshot = controller.CreateSnapshot(client.Slot, ++serverTick);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                client.Slot,
                2,
                LastToDieCommandKind.ChooseSurvivor,
                snapshot.StructuralRevision,
                selectedId: LastToDieSurvivorCatalog.SpyId.Value).Result.Result);
        snapshot = controller.CreateSnapshot(client.Slot, ++serverTick);
        var player = GetPlayer(snapshot, client.Slot);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                client.Slot,
                3,
                LastToDieCommandKind.SelectReward,
                snapshot.StructuralRevision,
                player.ActiveOfferId,
                player.ActiveOfferChoices[0]).Result.Result);
        snapshot = controller.CreateSnapshot(client.Slot, ++serverTick);
        Assert.True(
            controller.TryOpenStageBarrier(snapshot.StageInstanceId, 500, out var barrierError),
            barrierError);
        typeof(ClientSession).GetProperty(nameof(ClientSession.LastAcknowledgedSnapshotFrame))!
            .SetValue(client, 500UL);
        Assert.Equal(
            LastToDieCommandResultKind.Accepted,
            Handle(
                controller,
                client.Slot,
                4,
                LastToDieCommandKind.StageContentReady,
                controller.Director.StructuralRevision,
                selectedId: snapshot.CurrentMap).Result.Result);
        Assert.True(session.Tick());
        Assert.Equal(LastToDiePhase.Playing, controller.Director.Phase);
    }

    private static void SetAllLobbyPlayersReady(LastToDieProtocolController controller)
    {
        foreach (var player in controller.Director.CreateSnapshot().Players)
        {
            Assert.True(
                controller.Director.TrySetLobbyReady(player.PlayerId, true, out var error),
                error);
        }
    }

    private static void Publish(
        LastToDieProtocolController controller,
        LastToDieReplicatedState hostState,
        LastToDieReplicatedState guestState,
        long serverTick)
    {
        var hostSnapshot = Protocol64RoundTrip(controller.CreateSnapshot(1, serverTick));
        var guestSnapshot = Protocol64RoundTrip(controller.CreateSnapshot(2, serverTick));
        Assert.True(hostState.ApplySnapshot(hostSnapshot).Applied);
        Assert.True(guestState.ApplySnapshot(guestSnapshot).Applied);
    }

    private static LastToDiePlayerSnapshotMessage GetPlayer(
        LastToDieReplicatedState state,
        byte slot)
        => Assert.Single(state.Snapshot!.Players, player => player.Slot == slot);

    private static LastToDiePlayerSnapshotMessage GetPlayer(
        LastToDieRunSnapshotMessage snapshot,
        byte slot)
        => Assert.Single(snapshot.Players, player => player.Slot == slot);

    private static LastToDieRunSnapshotMessage CreateWireSnapshot()
        => new(
            RunId,
            StructuralRevision: 12,
            Seed: 42,
            RulesetVersion: 1,
            LastToDieWireDifficulty.Standard,
            LastToDieWirePhase.RewardChoice,
            ServerTick: 100,
            StageNumber: 1,
            StageInstanceId: 1,
            CurrentMap: "Truefort",
            EnemyCount: 2,
            StageEndServerTick: 5_500,
            RunEndServerTick: 54_100,
            Players:
            [
                new LastToDiePlayerSnapshotMessage(
                    Slot: 1,
                    HostId,
                    IsConnected: true,
                    SurvivorId: LastToDieSurvivorCatalog.SpyId.Value,
                    OwnedPerkIds:
                    [
                        LastToDiePerkIds.Spy.Grounded.Value,
                        LastToDiePerkIds.Spy.Acrobat.Value,
                        LastToDiePerkIds.Spy.Vampire.Value,
                        LastToDiePerkIds.Spy.Rejuvenation.Value,
                        LastToDiePerkIds.Spy.ChameleonShell.Value,
                        LastToDiePerkIds.Spy.Shroud.Value,
                    ],
                    ActiveOfferId: 9,
                    ActiveOfferOrdinal: 1,
                    ActiveOfferChoices:
                    [
                        LastToDiePerkIds.Spy.Blunderbuss1.Value,
                        LastToDiePerkIds.Spy.Agent.Value,
                        LastToDiePerkIds.Spy.Deadly.Value,
                    ],
                    IsReady: false,
                    IsAlive: true,
                    Kills: 3,
                    ConquistadorStacks: 37,
                    ReconnectGraceEndServerTick: 130),
            ],
            BaselineStartFrame: 90);

    private static T LegacyRoundTrip<T>(T message)
        where T : class, IProtocolMessage
    {
        var payload = ProtocolCodec.Serialize(message);
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var decoded));
        return Assert.IsType<T>(decoded);
    }

    private static T Protocol64RoundTrip<T>(T value)
        where T : class
    {
        var registry = Protocol64SchemaRegistryFactory.CreateDefault();
        var encoded = Protocol64FrameCodec.EncodeObject(
            registry,
            value,
            connectionEpoch: 1,
            frameId: 1);
        Assert.True(encoded.Succeeded, encoded.Fault?.Message);
        var decoded = Protocol64FrameCodec.Decode(encoded.Payload!, registry);
        Assert.True(decoded.Succeeded, decoded.Fault?.Message);
        return Assert.IsType<T>(decoded.Event);
    }

    private static void AssertSnapshotsEqual(
        LastToDieRunSnapshotMessage expected,
        LastToDieRunSnapshotMessage actual)
    {
        Assert.Equal(expected.RunId, actual.RunId);
        Assert.Equal(expected.StructuralRevision, actual.StructuralRevision);
        Assert.Equal(expected.ServerTick, actual.ServerTick);
        Assert.Equal(expected.StageInstanceId, actual.StageInstanceId);
        Assert.Equal(expected.BaselineStartFrame, actual.BaselineStartFrame);
        Assert.Equal(expected.MaximumPlayers, actual.MaximumPlayers);
        Assert.Equal(expected.CurrentMap, actual.CurrentMap);
        Assert.Equal(expected.Players.Count, actual.Players.Count);
        for (var index = 0; index < expected.Players.Count; index += 1)
        {
            Assert.Equal(expected.Players[index].Slot, actual.Players[index].Slot);
            Assert.Equal(expected.Players[index].PlayerId, actual.Players[index].PlayerId);
            Assert.Equal(expected.Players[index].IsHost, actual.Players[index].IsHost);
            Assert.Equal(expected.Players[index].ConquistadorStacks, actual.Players[index].ConquistadorStacks);
            Assert.Equal(
                expected.Players[index].ReconnectGraceEndServerTick,
                actual.Players[index].ReconnectGraceEndServerTick);
            Assert.Equal(expected.Players[index].OwnedPerkIds, actual.Players[index].OwnedPerkIds);
            Assert.Equal(expected.Players[index].ActiveOfferChoices, actual.Players[index].ActiveOfferChoices);
        }
    }

    private sealed class RecordingClientTransport(string remoteDescription)
        : INetworkClientMessageTransport
    {
        public List<byte[]> SentPayloads { get; } = [];

        public bool HasPendingMessages => false;

        public bool IsLoopbackConnection => true;

        public string RemoteDescription { get; } = remoteDescription;

        public bool TryReceive(out byte[] payload)
        {
            payload = [];
            return false;
        }

        public bool TryConsumeDisconnectReason(out string reason)
        {
            reason = string.Empty;
            return false;
        }

        public void Send(byte[] payload)
            => SentPayloads.Add(payload.ToArray());

        public void Dispose()
        {
        }
    }
}
