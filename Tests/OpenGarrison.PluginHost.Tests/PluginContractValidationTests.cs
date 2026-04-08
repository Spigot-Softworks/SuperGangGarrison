using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PluginContractValidationTests
{
    [Fact]
    public void ClientPluginHostRejectsInvalidOutgoingPluginMessageMetadata()
    {
        var logLines = new List<string>();
        var state = new FakeClientPluginHostState();
        var rootPath = CreateTempRoot();
        var host = new ClientPluginHost(
            state,
            null!,
            Path.Combine(rootPath, "plugins"),
            Path.Combine(rootPath, "config"),
            Path.Combine(rootPath, "state.json"),
            logLines.Add);

        InvokeClientSendMessage(host, "sample.client.plugin", " target.plugin ", " score.update ", "{}", PluginMessagePayloadFormat.Json, 0);
        InvokeClientSendMessage(host, "sample.client.plugin", " ", "score.update", "{}", PluginMessagePayloadFormat.Json, 1);
        InvokeClientSendMessage(host, "sample.client.plugin", "target.plugin", "score.update", new string('x', ProtocolCodec.MaxPluginPayloadBytes + 1), PluginMessagePayloadFormat.Json, 1);

        Assert.Empty(state.SentMessages);
        Assert.Contains(logLines, line => line.Contains("sample.client.plugin", StringComparison.Ordinal) && line.Contains("Schema version must be greater than zero.", StringComparison.Ordinal));
        Assert.Contains(logLines, line => line.Contains("Target plugin id and message type must be non-empty.", StringComparison.Ordinal));
        Assert.Contains(logLines, line => line.Contains($"Payload exceeds protocol byte limit of {ProtocolCodec.MaxPluginPayloadBytes} bytes.", StringComparison.Ordinal));

        InvokeClientSendMessage(host, "sample.client.plugin", " target.plugin ", " score.update ", "{}", PluginMessagePayloadFormat.Json, 1);

        var sentMessage = Assert.Single(state.SentMessages);
        Assert.Equal("sample.client.plugin", sentMessage.SourcePluginId);
        Assert.Equal("target.plugin", sentMessage.TargetPluginId);
        Assert.Equal("score.update", sentMessage.MessageType);
        Assert.Equal("{}", sentMessage.Payload);
        Assert.Equal(PluginMessagePayloadFormat.Json, sentMessage.PayloadFormat);
        Assert.Equal((ushort)1, sentMessage.SchemaVersion);
    }

    [Fact]
    public void ServerPluginHostRejectsInvalidOutgoingPluginMessageMetadata()
    {
        var logLines = new List<string>();
        var sentMessages = new List<(byte Slot, string SourcePluginId, string TargetPluginId, string MessageType, string Payload, PluginMessagePayloadFormat PayloadFormat, ushort SchemaVersion)>();
        var broadcastMessages = new List<(string SourcePluginId, string TargetPluginId, string MessageType, string Payload, PluginMessagePayloadFormat PayloadFormat, ushort SchemaVersion)>();
        var rootPath = CreateTempRoot();
        var host = new OpenGarrison.Server.PluginHost(
            static () => throw new InvalidOperationException("Unexpected world access during message validation test."),
            new PluginCommandRegistry(),
            new FakeServerReadOnlyState(),
            new FakeServerAdminOperations(),
            new FakeServerCvarRegistry(),
            new FakeServerScheduler(),
            static slot => OpenGarrisonServerAdminIdentity.CreateUnauthenticated(slot),
            (slot, sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion) =>
                sentMessages.Add((slot, sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion)),
            (sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion) =>
                broadcastMessages.Add((sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion)),
            Path.Combine(rootPath, "plugins"),
            Path.Combine(rootPath, "config"),
            Path.Combine(rootPath, "maps"),
            logLines.Add);
        var plugin = new FakeServerPlugin("sample.server.plugin");
        var context = CreateServerContext(host, plugin, Path.Combine(rootPath, "plugins", plugin.Id));

        context.SendMessageToClient(3, " target.plugin ", " score.update ", "{}", PluginMessagePayloadFormat.Json, 0);
        context.BroadcastMessageToClients(" ", "score.update", "{}", PluginMessagePayloadFormat.Json, 1);
        context.BroadcastMessageToClients("target.plugin", "score.update", new string('x', ProtocolCodec.MaxPluginPayloadBytes + 1), PluginMessagePayloadFormat.Json, 1);

        Assert.Empty(sentMessages);
        Assert.Empty(broadcastMessages);
        Assert.Contains(logLines, line => line.Contains("sample.server.plugin", StringComparison.Ordinal) && line.Contains("Schema version must be greater than zero.", StringComparison.Ordinal));
        Assert.Contains(logLines, line => line.Contains("Target plugin id and message type must be non-empty.", StringComparison.Ordinal));
        Assert.Contains(logLines, line => line.Contains($"Payload exceeds protocol byte limit of {ProtocolCodec.MaxPluginPayloadBytes} bytes.", StringComparison.Ordinal));

        context.SendMessageToClient(3, " target.plugin ", " score.update ", "{}", PluginMessagePayloadFormat.Json, 1);
        context.BroadcastMessageToClients(" target.plugin ", " score.update ", "{}", PluginMessagePayloadFormat.Json, 1);

        var sentMessage = Assert.Single(sentMessages);
        Assert.Equal((byte)3, sentMessage.Slot);
        Assert.Equal("sample.server.plugin", sentMessage.SourcePluginId);
        Assert.Equal("target.plugin", sentMessage.TargetPluginId);
        Assert.Equal("score.update", sentMessage.MessageType);

        var broadcastMessage = Assert.Single(broadcastMessages);
        Assert.Equal("sample.server.plugin", broadcastMessage.SourcePluginId);
        Assert.Equal("target.plugin", broadcastMessage.TargetPluginId);
        Assert.Equal("score.update", broadcastMessage.MessageType);
    }

    [Fact]
    public void ClientPluginHostRoutesValidatedInboundMessagesToTargetPluginOnly()
    {
        InboundClientReceiverPlugin.Reset();
        InboundClientObserverPlugin.Reset();

        var logLines = new List<string>();
        var state = new FakeClientPluginHostState();
        var rootPath = CreateTempRoot();
        var host = new ClientPluginHost(
            state,
            null!,
            Path.Combine(rootPath, "plugins"),
            Path.Combine(rootPath, "config"),
            Path.Combine(rootPath, "state.json"),
            logLines.Add);
        host.LoadPlugins([typeof(InboundClientReceiverPlugin).Assembly]);

        host.NotifyServerPluginMessage(new ClientPluginMessageEnvelope(
            "   ",
            "tests.client.receiver",
            "score.update",
            "{}",
            PluginMessagePayloadFormat.Json,
            1));

        Assert.Empty(InboundClientReceiverPlugin.ReceivedMessages);
        Assert.Empty(InboundClientObserverPlugin.ReceivedMessages);
        Assert.Contains(logLines, line => line.Contains("rejected inbound plugin message from server", StringComparison.Ordinal)
            && line.Contains("Source plugin id must be non-empty.", StringComparison.Ordinal));

        host.NotifyServerPluginMessage(new ClientPluginMessageEnvelope(
            " server.scoreboard ",
            " tests.client.receiver ",
            " score.update ",
            "{}",
            PluginMessagePayloadFormat.Json,
            2));

        var message = Assert.Single(InboundClientReceiverPlugin.ReceivedMessages);
        Assert.Equal("server.scoreboard", message.SourcePluginId);
        Assert.Equal("tests.client.receiver", message.TargetPluginId);
        Assert.Equal("score.update", message.MessageType);
        Assert.Equal("{}", message.Payload);
        Assert.Equal(PluginMessagePayloadFormat.Json, message.PayloadFormat);
        Assert.Equal((ushort)2, message.SchemaVersion);
        Assert.Empty(InboundClientObserverPlugin.ReceivedMessages);
    }

    [Fact]
    public void ServerPluginHostRoutesValidatedInboundMessagesToTargetPluginOnly()
    {
        InboundServerReceiverPlugin.Reset();
        InboundServerObserverPlugin.Reset();

        var logLines = new List<string>();
        var rootPath = CreateTempRoot();
        var host = new OpenGarrison.Server.PluginHost(
            static () => throw new InvalidOperationException("Unexpected world access during inbound message routing test."),
            new PluginCommandRegistry(),
            new FakeServerReadOnlyState(),
            new FakeServerAdminOperations(),
            new FakeServerCvarRegistry(),
            new FakeServerScheduler(),
            static slot => OpenGarrisonServerAdminIdentity.CreateUnauthenticated(slot),
            static (_, _, _, _, _, _, _) => { },
            static (_, _, _, _, _, _) => { },
            Path.Combine(rootPath, "plugins"),
            Path.Combine(rootPath, "config"),
            Path.Combine(rootPath, "maps"),
            logLines.Add);
        host.LoadPlugins([typeof(InboundServerReceiverPlugin).Assembly]);

        host.NotifyClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            3,
            "Player",
            "   ",
            "tests.server.receiver",
            "score.update",
            "{}",
            PluginMessagePayloadFormat.Json,
            1));

        Assert.Empty(InboundServerReceiverPlugin.ReceivedMessages);
        Assert.Empty(InboundServerObserverPlugin.ReceivedMessages);
        Assert.Contains(logLines, line => line.Contains("rejected inbound client plugin message from slot 3", StringComparison.Ordinal)
            && line.Contains("Source plugin id must be non-empty.", StringComparison.Ordinal));

        host.NotifyClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            3,
            "Player",
            " client.scoreboard ",
            " tests.server.receiver ",
            " score.update ",
            "{}",
            PluginMessagePayloadFormat.Json,
            2));

        var message = Assert.Single(InboundServerReceiverPlugin.ReceivedMessages);
        Assert.Equal((byte)3, message.SourceSlot);
        Assert.Equal("Player", message.SourcePlayerName);
        Assert.Equal("client.scoreboard", message.SourcePluginId);
        Assert.Equal("tests.server.receiver", message.TargetPluginId);
        Assert.Equal("score.update", message.MessageType);
        Assert.Equal("{}", message.Payload);
        Assert.Equal(PluginMessagePayloadFormat.Json, message.PayloadFormat);
        Assert.Equal((ushort)2, message.SchemaVersion);
        Assert.Empty(InboundServerObserverPlugin.ReceivedMessages);
    }

    [Fact]
    public void CompatibilityHeaderRejectsMessagesOutsideDeclaredContract()
    {
        var header = new PluginMessageCompatibilityHeader(
            "server.scoreboard",
            "tests.client.receiver",
            "score.update",
            PluginMessagePayloadFormat.Json,
            2);

        Assert.False(PluginMessageContract.TryValidateAgainstCompatibilityContract(
            header,
            new PluginMessageCompatibilityContract(
                "tests.client.receiver",
                "score.update",
                PluginMessagePayloadFormat.Json,
                MinimumSchemaVersion: 1,
                MaximumSchemaVersion: 1),
            out var error));
        Assert.Contains("outside the supported range 1-1", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientSerializerUsesCompatibilityHeaderForJsonContractValidation()
    {
        var envelope = new ClientPluginMessageEnvelope(
            "server.scoreboard",
            "tests.client.receiver",
            "score.update",
            """{"Score":4}""",
            PluginMessagePayloadFormat.Json,
            1);

        Assert.True(ClientPluginMessageSerializer.TryDeserializeCompatibleJsonPayload<ScorePayload>(
            envelope,
            new PluginMessageCompatibilityContract(
                "tests.client.receiver",
                "score.update",
                PluginMessagePayloadFormat.Json,
                MinimumSchemaVersion: 1,
                MaximumSchemaVersion: 1),
            out var payload,
            out var error));
        Assert.Equal(string.Empty, error);
        Assert.NotNull(payload);
        Assert.Equal(4, payload!.Score);

        Assert.False(ClientPluginMessageSerializer.TryDeserializeCompatibleJsonPayload<ScorePayload>(
            envelope,
            new PluginMessageCompatibilityContract(
                "tests.client.receiver",
                "score.patch",
                PluginMessagePayloadFormat.Json,
                MinimumSchemaVersion: 1,
                MaximumSchemaVersion: 1),
            out _,
            out error));
        Assert.Contains("did not match expected message type", error, StringComparison.Ordinal);
    }

    private static void InvokeClientSendMessage(
        ClientPluginHost host,
        string sourcePluginId,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion)
    {
        var method = typeof(ClientPluginHost).GetMethod("SendMessageToServer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(host, [sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion]);
    }

    private static IOpenGarrisonServerPluginContext CreateServerContext(OpenGarrison.Server.PluginHost host, IOpenGarrisonServerPlugin plugin, string pluginDirectory)
    {
        var method = typeof(OpenGarrison.Server.PluginHost).GetMethod("CreateContext", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IOpenGarrisonServerPluginContext>(method!.Invoke(host,
        [
            plugin,
            OpenGarrisonPluginManifest.CreateClr(plugin.Id, plugin.DisplayName, plugin.Version, OpenGarrisonPluginType.Server, "Plugin.dll", plugin.GetType().FullName),
            pluginDirectory,
        ]));
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenGarrison.PluginHost.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeClientPluginHostState : IOpenGarrisonClientReadOnlyState, ClientPluginMessageSink
    {
        public List<(string SourcePluginId, string TargetPluginId, string MessageType, string Payload, PluginMessagePayloadFormat PayloadFormat, ushort SchemaVersion)> SentMessages { get; } = [];

        public bool IsConnected => true;
        public bool IsMainMenuOpen => false;
        public bool IsGameplayActive => true;
        public bool IsGameplayInputBlocked => false;
        public bool IsSpectator => false;
        public bool IsDeathCamActive => false;
        public ulong WorldFrame => 0;
        public int TickRate => 60;
        public int LocalPingMilliseconds => 0;
        public string LevelName => "test";
        public float LevelWidth => 0f;
        public float LevelHeight => 0f;
        public int ViewportWidth => 0;
        public int ViewportHeight => 0;
        public int? LocalPlayerId => null;
        public ClientPluginTeam LocalPlayerTeam => default;
        public ClientPluginClass LocalPlayerClass => default;
        public bool IsLocalPlayerAlive => false;
        public bool IsLocalPlayerScoped => false;
        public bool IsLocalPlayerHealing => false;
        public Vector2 CameraTopLeft => Vector2.Zero;

        public void SendPluginMessage(string sourcePluginId, string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
        {
            SentMessages.Add((sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion));
        }

        public IReadOnlyList<ClientPlayerMarker> GetPlayerMarkers() => [];

        public IReadOnlyList<ClientSentryMarker> GetSentryMarkers() => [];

        public IReadOnlyList<ClientObjectiveMarker> GetObjectiveMarkers() => [];

        public bool IsPlayerCloaked(int playerId) => false;

        public bool IsPlayerVisibleToLocalViewer(int playerId) => false;

        public bool TryGetLocalPlayerHealth(out int health, out int maxHealth)
        {
            health = 0;
            maxHealth = 0;
            return false;
        }

        public bool TryGetLocalPlayerWorldPosition(out Vector2 position)
        {
            position = Vector2.Zero;
            return false;
        }

        public bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value)
        {
            value = false;
            return false;
        }

        public bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value)
        {
            value = 0f;
            return false;
        }

        public bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value)
        {
            value = 0;
            return false;
        }

        public bool TryGetPlayerWorldPosition(int playerId, out Vector2 position)
        {
            position = Vector2.Zero;
            return false;
        }

        public bool WasKeyPressedThisFrame(Keys key) => false;
    }

    private sealed class FakeServerPlugin(string pluginId) : IOpenGarrisonServerPlugin
    {
        public string Id { get; } = pluginId;

        public string DisplayName => "Test Server Plugin";

        public Version Version => new(1, 0, 0);

        public void Initialize(IOpenGarrisonServerPluginContext context)
        {
        }

        public void Shutdown()
        {
        }
    }

    private sealed class FakeServerReadOnlyState : IOpenGarrisonServerReadOnlyState
    {
        public string ServerName => "test";
        public string LevelName => "test";
        public int MapAreaIndex => 1;
        public int MapAreaCount => 1;
        public GameModeKind GameMode => default;
        public MatchPhase MatchPhase => default;
        public int RedCaps => 0;
        public int BlueCaps => 0;

        public IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers() => [];

        public IReadOnlyList<OpenGarrisonServerGameplayModPackInfo> GetGameplayModPacks() => [];

        public IReadOnlyList<OpenGarrisonServerGameplayClassInfo> GetGameplayClasses(string? modPackId = null) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetGameplayItems(string? modPackId = null) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetOwnedGameplayItems(byte slot) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetGameplayLoadoutsForClass(string classId) => [];

        public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplaySecondaryItems(byte slot) => [];

        public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplayAcquiredItems(byte slot) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetAvailableGameplayLoadouts(byte slot) => [];

        public bool TryGetPlayerReplicatedStateInt(byte slot, string ownerPluginId, string stateKey, out int value)
        {
            value = 0;
            return false;
        }

        public bool TryGetPlayerReplicatedStateFloat(byte slot, string ownerPluginId, string stateKey, out float value)
        {
            value = 0f;
            return false;
        }

        public bool TryGetPlayerReplicatedStateBool(byte slot, string ownerPluginId, string stateKey, out bool value)
        {
            value = false;
            return false;
        }
    }

    private sealed class FakeServerAdminOperations : IOpenGarrisonServerAdminOperations
    {
        public void BroadcastSystemMessage(string text)
        {
        }

        public void SendSystemMessage(byte slot, string text)
        {
        }

        public bool TryDisconnect(byte slot, string reason) => true;

        public bool TryMoveToSpectator(byte slot) => true;

        public bool TrySetTeam(byte slot, PlayerTeam team) => true;

        public bool TrySetClass(byte slot, PlayerClass playerClass) => true;

        public bool TrySetGameplayLoadout(byte slot, string loadoutId) => true;

        public bool TrySetGameplaySecondaryItem(byte slot, string? itemId) => true;

        public bool TrySetGameplayAcquiredItem(byte slot, string? itemId) => true;

        public bool TryGrantGameplayItem(byte slot, string itemId) => true;

        public bool TryRevokeGameplayItem(byte slot, string itemId) => true;

        public bool TrySetGameplayEquippedSlot(byte slot, GameplayEquipmentSlot equippedSlot) => true;

        public bool TryForceKill(byte slot) => true;

        public bool TrySetCapLimit(int capLimit) => true;

        public bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false) => true;

        public bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1) => true;
    }

    private sealed class FakeServerCvarRegistry : IOpenGarrisonServerCvarRegistry
    {
        public IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll() => [];

        public bool TryGet(string name, out OpenGarrisonServerCvarInfo cvar)
        {
            cvar = default;
            return false;
        }

        public bool TrySet(string name, string value, out OpenGarrisonServerCvarInfo cvar, out string errorMessage)
        {
            cvar = default;
            errorMessage = "unsupported";
            return false;
        }
    }

    private sealed class FakeServerScheduler : IOpenGarrisonServerScheduler
    {
        public TimeSpan Uptime => TimeSpan.Zero;

        public Guid ScheduleOnce(TimeSpan delay, Action callback, string? description = null) => Guid.NewGuid();

        public Guid ScheduleRepeating(TimeSpan interval, Action callback, string? description = null, bool runImmediately = false) => Guid.NewGuid();

        public bool Cancel(Guid timerId) => false;

        public bool IsScheduled(Guid timerId) => false;

        public IReadOnlyList<OpenGarrisonServerScheduledTaskInfo> GetScheduledTasks() => [];
    }

    private sealed record ScorePayload(int Score);
}

public sealed class InboundClientReceiverPlugin : IOpenGarrisonClientPlugin, IOpenGarrisonClientPluginMessageHooks
{
    public static List<ClientPluginMessageEnvelope> ReceivedMessages { get; } = [];

    public string Id => "tests.client.receiver";

    public string DisplayName => "Inbound Client Receiver";

    public Version Version => new(1, 0, 0);

    public static void Reset()
    {
        ReceivedMessages.Clear();
    }

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
    }

    public void OnServerPluginMessage(ClientPluginMessageEnvelope e)
    {
        ReceivedMessages.Add(e);
    }

    public void Shutdown()
    {
    }
}

public sealed class InboundClientObserverPlugin : IOpenGarrisonClientPlugin, IOpenGarrisonClientPluginMessageHooks
{
    public static List<ClientPluginMessageEnvelope> ReceivedMessages { get; } = [];

    public string Id => "tests.client.observer";

    public string DisplayName => "Inbound Client Observer";

    public Version Version => new(1, 0, 0);

    public static void Reset()
    {
        ReceivedMessages.Clear();
    }

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
    }

    public void OnServerPluginMessage(ClientPluginMessageEnvelope e)
    {
        ReceivedMessages.Add(e);
    }

    public void Shutdown()
    {
    }
}

public sealed class InboundServerReceiverPlugin : IOpenGarrisonServerPlugin, IOpenGarrisonServerPluginMessageHooks
{
    public static List<OpenGarrisonServerPluginMessageEnvelope> ReceivedMessages { get; } = [];

    public string Id => "tests.server.receiver";

    public string DisplayName => "Inbound Server Receiver";

    public Version Version => new(1, 0, 0);

    public static void Reset()
    {
        ReceivedMessages.Clear();
    }

    public void Initialize(IOpenGarrisonServerPluginContext context)
    {
    }

    public void OnClientPluginMessage(OpenGarrisonServerPluginMessageEnvelope e)
    {
        ReceivedMessages.Add(e);
    }

    public void Shutdown()
    {
    }
}

public sealed class InboundServerObserverPlugin : IOpenGarrisonServerPlugin, IOpenGarrisonServerPluginMessageHooks
{
    public static List<OpenGarrisonServerPluginMessageEnvelope> ReceivedMessages { get; } = [];

    public string Id => "tests.server.observer";

    public string DisplayName => "Inbound Server Observer";

    public Version Version => new(1, 0, 0);

    public static void Reset()
    {
        ReceivedMessages.Clear();
    }

    public void Initialize(IOpenGarrisonServerPluginContext context)
    {
    }

    public void OnClientPluginMessage(OpenGarrisonServerPluginMessageEnvelope e)
    {
        ReceivedMessages.Add(e);
    }

    public void Shutdown()
    {
    }
}
