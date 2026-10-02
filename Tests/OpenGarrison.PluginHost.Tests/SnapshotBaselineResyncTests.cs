using System.Net;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SnapshotBaselineResyncTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void MissingBaselineSnapshotHandlerRequestsResyncThenAcceptsFreshFullAndFollowingDelta()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld();
        var networkClient = new NetworkGameClient();
        var transport = new RecordingTransport();
        var services = new ClientServiceContainer();
        SetGameField(game, "_world", world);
        SetGameField(game, "_config", world.Config);
        SetGameField(game, "_networkClient", networkClient);
        SetGameField(game, "_services", services);
        SetGameField(game, "_gameplaySessionState", new Game1.GameplaySessionState());
        SetGameField(game, "_consoleHistory", new List<string>());
        SetGameField(game, "_snapshotStatesByFrame", new Dictionary<ulong, SnapshotBaselineState>());
        SetGameField(game, "_snapshotStateFrameOrder", new Queue<ulong>());
        SetGameField(game, "_pinnedServerSnapshotBaseline", null);
        SetGameField(game, "_pinnedServerSnapshotBaselineFrame", 0UL);
        SetGameField(game, "_processedNetworkVisualEventIds", new HashSet<ulong>());
        SetGameField(game, "_processedNetworkVisualEventOrder", new Queue<ulong>());
        var gameplay = new GameplayManager((IGameplayContext)game);
        services.Register(gameplay);
        services.Register(new AudioManager((IAudioContext)game));
        services.Register(new PluginManager((IPluginContext)game));
        Assert.True(networkClient.Connect(transport, "Tester", 0, out var error), error);
        networkClient.SetLocalPlayerSlot(1);
        transport.SentPayloads.Clear();

        var baselineUnknownDelta = CreateMatchingMapSnapshot(world, 8) with
        {
            IsDelta = true,
            BaselineFrame = 7,
        };
        var entryType = typeof(Game1).GetNestedType("ResolvedSnapshotEntry", PrivateInstance)!;
        var batch = Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType))!;
        var baselinesByFrame = new Dictionary<ulong, SnapshotBaselineState>();
        var missingArguments = new object?[] { baselineUnknownDelta, 0UL, null, baselinesByFrame, batch };
        Assert.False(InvokeTryHandleSnapshot(game, missingArguments));
        Assert.Contains(
            transport.SentPayloads.Select(Deserialize),
            message => message is SnapshotAckMessage { Frame: 0 });

        var replacementFull = CreateMatchingMapSnapshot(world, 9);
        var fullArguments = new object?[] { replacementFull, 0UL, null, baselinesByFrame, batch };
        Assert.True(InvokeTryHandleSnapshot(game, fullArguments));
        Assert.False((bool)typeof(NetworkGameClient)
            .GetField("_snapshotResyncRequestOutstanding", PrivateInstance)!
            .GetValue(networkClient)!);

        var nextDelta = CreateMatchingMapSnapshot(world, 10) with
        {
            IsDelta = true,
            BaselineFrame = replacementFull.Frame,
            Players = [],
            Shots = [],
        };
        var nextArguments = new object?[] { nextDelta, fullArguments[1], fullArguments[2], baselinesByFrame, batch };
        Assert.True(InvokeTryHandleSnapshot(game, nextArguments));
    }

    [Fact]
    public void MissingBaselineRequestIsDedicatedThrottledAndRetried()
    {
        using var client = ConnectClient(out var transport);

        client.RequestSnapshotResync();
        Assert.Equal(0UL, Assert.IsType<SnapshotAckMessage>(Deserialize(Assert.Single(transport.SentPayloads))).Frame);

        client.RequestSnapshotResync();
        Assert.Single(transport.SentPayloads);

        // Model a lost request by aging only its retry timestamp.
        SetField(client, "_lastSnapshotResyncRequestAtMilliseconds", -1000L);
        client.ReceiveMessages();
        var resyncRequests = transport.SentPayloads
            .Select(Deserialize)
            .OfType<SnapshotAckMessage>()
            .Where(message => message.Frame == 0)
            .ToArray();
        Assert.Equal(2, resyncRequests.Length);

        SetField(client, "_pendingSnapshotAckFrame", 12UL);
        SetField(client, "_snapshotAckLastSentFrame", 11UL);
        Assert.Null(typeof(NetworkGameClient)
            .GetMethod("GetDueSnapshotAck", PrivateInstance)!
            .Invoke(client, [long.MaxValue]));
        Assert.Null(typeof(NetworkGameClient)
            .GetMethod("GetDueSnapshotAckFallback", PrivateInstance)!
            .Invoke(client, [long.MaxValue]));

        client.ObserveResolvedSnapshotBaseline(CreateSnapshot(3));
        Assert.False((bool)typeof(NetworkGameClient)
            .GetField("_snapshotResyncRequestOutstanding", PrivateInstance)!
            .GetValue(client)!);

        transport.SentPayloads.Clear();
        SetField(client, "_pendingSnapshotAckFrame", 13UL);
        SetField(client, "_snapshotAckLastSentFrame", 12UL);
        client.SendInput(default, 0f, 0f);
        var input = Assert.IsType<InputStateMessage>(Deserialize(Assert.Single(transport.SentPayloads)));
        Assert.Equal(13UL, input.SnapshotAckFrame);
    }

    [Fact]
    public void ServerResyncUsesFreshFullBaselineAndPreservesDeliveredCacheAndEvents()
    {
        var client = new ClientSession(
            1,
            101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var initialSnapshot = CreateSnapshot(1) with
        {
            StringCacheUpdates = new Dictionary<ushort, string> { [7] = "pack-a" },
            SoundEvents = [new SnapshotSoundEvent("shot", 32f, 48f, EventId: 77)],
        };
        client.RememberResolvedSnapshotState(initialSnapshot);
        client.AcknowledgeSnapshot(initialSnapshot.Frame);
        Assert.True(client.HasAcknowledgedStringCacheId(7));
        Assert.True(client.HasAcknowledgedSoundEvent(77));
        var delayedSnapshot = CreateSnapshot(19);
        client.RememberResolvedSnapshotState(delayedSnapshot);
        Assert.NotNull(TryGetSnapshotBaseline(client, CreateSnapshot(2)));

        // A zero carried in an input ACK is ignored; only a standalone
        // SnapshotAckMessage(0) is dispatched as a resync request.
        client.AcknowledgeSnapshot(0);
        Assert.False(client.SnapshotResyncPending);
        Assert.Equal(1UL, client.LastAcknowledgedSnapshotFrame);

        client.RequestSnapshotResync();
        Assert.True(client.SnapshotResyncPending);
        Assert.Equal(0UL, client.LastAcknowledgedSnapshotFrame);
        Assert.Null(TryGetSnapshotBaseline(client, CreateSnapshot(2)));
        Assert.True(client.HasAcknowledgedStringCacheId(7));
        Assert.True(client.HasAcknowledgedSoundEvent(77));

        var firstFullSnapshot = CreateSnapshot(20);
        var laterFullSnapshot = CreateSnapshot(21);
        client.RememberResolvedSnapshotState(firstFullSnapshot);
        client.MarkSnapshotResyncFullSnapshotSent(firstFullSnapshot.Frame);
        client.RequestSnapshotResync(); // duplicate requests must not move the ACK floor
        client.RememberResolvedSnapshotState(laterFullSnapshot);
        client.MarkSnapshotResyncFullSnapshotSent(laterFullSnapshot.Frame);

        client.AcknowledgeSnapshot(19); // delayed ACK for the pre-resync stream
        Assert.True(client.SnapshotResyncPending);
        Assert.Equal(0UL, client.LastAcknowledgedSnapshotFrame);

        client.AcknowledgeSnapshot(firstFullSnapshot.Frame);
        Assert.False(client.SnapshotResyncPending);
        Assert.Equal(firstFullSnapshot.Frame, client.LastAcknowledgedSnapshotFrame);
        var restoredBaseline = TryGetSnapshotBaseline(client, laterFullSnapshot);
        Assert.NotNull(restoredBaseline);
        Assert.Equal(firstFullSnapshot.Frame, restoredBaseline!.Frame);
        Assert.True(client.HasAcknowledgedStringCacheId(7));
        Assert.True(client.HasAcknowledgedSoundEvent(77));
    }

    [Fact]
    public void DispatcherReservesZeroAckForStandaloneResyncMessage()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(
            1,
            101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero)
        {
            IsAuthorized = true,
        };
        var clients = new Dictionary<byte, ClientSession> { [client.Slot] = client };
        var sessionManager = new ServerSessionManager(
            world,
            clients,
            maxPlayableClients: 24,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
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
        var dispatcher = new ServerIncomingMessageDispatcher(
            new SimulationConfig(),
            "Test Server",
            passwordRequired: false,
            maxPlayableClients: 24,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
            clients,
            sessionManager,
            world,
            () => TimeSpan.Zero,
            static () => null,
            static () => 999,
            static _ => null,
            static _ => { },
            static () => (false, string.Empty, string.Empty),
            static (_, _) => { },
            static _ => { },
            static _ => { },
            static (_, _, _) => { },
            static (_, _) => { },
            static _ => { });
        var firstSnapshot = CreateSnapshot(1);
        client.RememberResolvedSnapshotState(firstSnapshot);
        client.AcknowledgeSnapshot(firstSnapshot.Frame);

        dispatcher.Dispatch(
            new InputStateMessage(1, InputButtons.None, 0f, 0f, -1, SnapshotAckFrame: 0),
            client.EndPoint);
        Assert.Equal(firstSnapshot.Frame, client.LastAcknowledgedSnapshotFrame);
        Assert.False(client.SnapshotResyncPending);

        dispatcher.Dispatch(new SnapshotAckMessage(0), client.EndPoint);
        Assert.Equal(0UL, client.LastAcknowledgedSnapshotFrame);
        Assert.True(client.SnapshotResyncPending);
    }

    private static NetworkGameClient ConnectClient(out RecordingTransport transport)
    {
        var client = new NetworkGameClient();
        transport = new RecordingTransport();
        Assert.True(client.Connect(transport, "Tester", 0, out var error), error);
        client.SetLocalPlayerSlot(1);
        transport.SentPayloads.Clear();
        return client;
    }

    private static SnapshotMessage CreateMatchingMapSnapshot(SimulationWorld world, ulong frame)
        => CreateSnapshot(frame) with
        {
            LevelName = world.Level.Name,
            MapAreaIndex = checked((byte)world.Level.MapAreaIndex),
            IsDelta = false,
            BaselineFrame = 0,
        };

    private static bool InvokeTryHandleSnapshot(Game1 game, object?[] arguments)
        => (bool)typeof(Game1)
            .GetMethod("TryHandleSnapshotMessage", PrivateInstance)!
            .Invoke(game, arguments)!;

    private static IProtocolMessage Deserialize(byte[] payload)
    {
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var message));
        return message!;
    }

    private static void SetField(NetworkGameClient client, string name, object value)
        => typeof(NetworkGameClient).GetField(name, PrivateInstance)!.SetValue(client, value);

    private static void SetGameField(Game1 game, string name, object? value)
        => typeof(Game1).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(game, value);

    private static SnapshotBaselineState? TryGetSnapshotBaseline(ClientSession client, SnapshotMessage snapshot)
        => (SnapshotBaselineState?)typeof(SnapshotBroadcaster)
            .GetMethod("TryGetBaselineSnapshot", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [client, snapshot]);

    private static SnapshotMessage CreateSnapshot(ulong frame)
    {
        var builder = typeof(ClientSessionSnapshotHistoryTests).GetMethod(
            "CreateSnapshot",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (SnapshotMessage)builder.Invoke(null, [frame])!;
    }

    private sealed class RecordingTransport : INetworkClientMessageTransport
    {
        public List<byte[]> SentPayloads { get; } = [];
        public bool IsLoopbackConnection => false;
        public string RemoteDescription => "server.example:8190";
        public bool HasPendingMessages => false;
        public void Send(byte[] payload) => SentPayloads.Add(payload);
        public bool TryConsumeDisconnectReason(out string reason)
        {
            reason = string.Empty;
            return false;
        }
        public bool TryReceive(out byte[] payload)
        {
            payload = [];
            return false;
        }
        public void Dispose() { }
    }
}
