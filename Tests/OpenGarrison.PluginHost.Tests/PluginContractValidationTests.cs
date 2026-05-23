using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
    public void ClientPluginHostEnforcesDeclaredOutgoingMessageContracts()
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
        var manifest = CreateMessageContractManifest(
            "sample.client.plugin",
            OpenGarrisonPluginType.Client,
            [new OpenGarrisonPluginManifestMessageContract
            {
                TargetPluginId = "target.plugin",
                MessageType = "allowed.update",
                PayloadFormat = "Json",
                SchemaVersion = 2,
                Direction = "ClientToServer",
            }]);

        InvokeClientSendMessage(host, "sample.client.plugin", manifest, "target.plugin", "blocked.update", "{}", PluginMessagePayloadFormat.Json, 2);
        InvokeClientSendMessage(host, "sample.client.plugin", manifest, "target.plugin", "allowed.update", "{}", PluginMessagePayloadFormat.Json, 2);

        var sentMessage = Assert.Single(state.SentMessages);
        Assert.Equal("allowed.update", sentMessage.MessageType);
        Assert.Contains(logLines, line => line.Contains("No manifest message contract allows", StringComparison.Ordinal));
    }

    [Fact]
    public void ServerPluginHostEnforcesDeclaredOutgoingMessageContracts()
    {
        var logLines = new List<string>();
        var sentMessages = new List<(byte Slot, string SourcePluginId, string TargetPluginId, string MessageType, string Payload, PluginMessagePayloadFormat PayloadFormat, ushort SchemaVersion)>();
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
            static (_, _, _, _, _, _) => { },
            Path.Combine(rootPath, "plugins"),
            Path.Combine(rootPath, "config"),
            Path.Combine(rootPath, "maps"),
            logLines.Add);
        var plugin = new FakeServerPlugin("sample.server.plugin");
        var manifest = CreateMessageContractManifest(
            plugin.Id,
            OpenGarrisonPluginType.Server,
            [new OpenGarrisonPluginManifestMessageContract
            {
                TargetPluginId = "target.plugin",
                MessageType = "allowed.update",
                PayloadFormat = "Json",
                SchemaVersion = 2,
                Direction = "ServerToClient",
            }]);
        var context = CreateServerContext(host, plugin, Path.Combine(rootPath, "plugins", plugin.Id), manifest);

        context.SendMessageToClient(3, "target.plugin", "blocked.update", "{}", PluginMessagePayloadFormat.Json, 2);
        context.SendMessageToClient(3, "target.plugin", "allowed.update", "{}", PluginMessagePayloadFormat.Json, 2);

        var sentMessage = Assert.Single(sentMessages);
        Assert.Equal("allowed.update", sentMessage.MessageType);
        Assert.Contains(logLines, line => line.Contains("No manifest message contract allows", StringComparison.Ordinal));
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
    public void ServerPluginHostRoutesChatThroughRegisteredCommandsBeforeLegacyHooks()
    {
        var logLines = new List<string>();
        var rootPath = CreateTempRoot();
        var adminOperations = new FakeServerAdminOperations();
        var host = new OpenGarrison.Server.PluginHost(
            static () => throw new InvalidOperationException("Unexpected world access during chat command routing test."),
            new PluginCommandRegistry(),
            new FakeServerReadOnlyState(),
            adminOperations,
            new FakeServerCvarRegistry(),
            new FakeServerScheduler(),
            static slot => new OpenGarrisonServerAdminIdentity(
                $"Player {slot}",
                OpenGarrisonServerAdminAuthority.RconSession,
                OpenGarrisonServerAdminPermissions.FullAccess,
                slot),
            static (_, _, _, _, _, _, _) => { },
            static (_, _, _, _, _, _) => { },
            Path.Combine(rootPath, "plugins"),
            Path.Combine(rootPath, "config"),
            Path.Combine(rootPath, "maps"),
            logLines.Add);
        var plugin = new FakeServerPlugin("sample.server.plugin");
        var context = CreateServerContext(host, plugin, Path.Combine(rootPath, "plugins", plugin.Id));
        context.RegisterCommand(
            new FakeServerCommand("!sample", static (commandContext, arguments) =>
            [
                $"slot {commandContext.Identity.SourceSlot}",
                $"args {arguments}",
            ]),
            OpenGarrisonServerAdminPermissions.None,
            ["!s"]);

        Assert.True(host.TryHandleChatMessage(new ChatReceivedEvent(7, "Tester", "!s alpha beta", Team: null, TeamOnly: false)));
        Assert.Equal(2, adminOperations.SystemMessages.Count);
        Assert.Equal((byte)7, adminOperations.SystemMessages[0].Slot);
        Assert.Equal("slot 7", adminOperations.SystemMessages[0].Text);
        Assert.Equal((byte)7, adminOperations.SystemMessages[1].Slot);
        Assert.Equal("args alpha beta", adminOperations.SystemMessages[1].Text);
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

    [Fact]
    public void PackagedClientPluginBootstrapperMirrorsPackagedPluginsWithoutDeletingCustomRuntimePlugins()
    {
        var rootPath = CreateTempRoot();
        var packagedSource = Path.Combine(rootPath, "Packaged");
        var runtimeDestination = Path.Combine(rootPath, "Runtime");

        var packagedPluginDirectory = Path.Combine(packagedSource, "Lua.GarrisonToolsEffects");
        Directory.CreateDirectory(packagedPluginDirectory);
        File.WriteAllText(Path.Combine(packagedPluginDirectory, "plugin.json"), """{"id":"open-garrison.client.lua-garrison-tools-effects"}""");
        File.WriteAllText(Path.Combine(packagedPluginDirectory, "main.lua"), "return {}");

        var nestedPackagedDirectory = Path.Combine(packagedPluginDirectory, "Resources");
        Directory.CreateDirectory(nestedPackagedDirectory);
        File.WriteAllText(Path.Combine(nestedPackagedDirectory, "effect.txt"), "blind");

        var staleRuntimeDirectory = Path.Combine(runtimeDestination, "Lua.GarrisonToolsEffects");
        Directory.CreateDirectory(staleRuntimeDirectory);
        File.WriteAllText(Path.Combine(staleRuntimeDirectory, "old.txt"), "stale");

        var customRuntimeDirectory = Path.Combine(runtimeDestination, "CustomPlugin");
        Directory.CreateDirectory(customRuntimeDirectory);
        File.WriteAllText(Path.Combine(customRuntimeDirectory, "custom.txt"), "keep");

        Assert.True(PackagedClientPluginBootstrapper.TryMirrorPackagedPlugins(packagedSource, runtimeDestination, out var error), error);
        Assert.Equal(string.Empty, error);

        Assert.False(File.Exists(Path.Combine(staleRuntimeDirectory, "old.txt")));
        Assert.True(File.Exists(Path.Combine(runtimeDestination, "Lua.GarrisonToolsEffects", "plugin.json")));
        Assert.True(File.Exists(Path.Combine(runtimeDestination, "Lua.GarrisonToolsEffects", "main.lua")));
        Assert.True(File.Exists(Path.Combine(runtimeDestination, "Lua.GarrisonToolsEffects", "Resources", "effect.txt")));
        Assert.True(File.Exists(Path.Combine(customRuntimeDirectory, "custom.txt")));
    }

    [Fact]
    public void ClientPluginStateDefaultsQuoteCurlyEnabled()
    {
        var rootPath = CreateTempRoot();
        var stateStore = new ClientPluginStateStore(
            Path.Combine(rootPath, "plugins.json"),
            _ => { });

        Assert.True(stateStore.IsPluginEnabled("quote-curly"));
    }

    [Fact]
    public void PluginPathContainmentRejectsSiblingPrefixEscapes()
    {
        var rootPath = CreateTempRoot();
        var pluginDirectory = Path.Combine(rootPath, "Plugin");
        var siblingDirectory = Path.Combine(rootPath, "PluginSibling");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(siblingDirectory);

        var siblingPath = Path.Combine(siblingDirectory, "main.lua");
        Assert.False(OpenGarrisonPluginPathContainment.IsPathContained(pluginDirectory, siblingPath));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            OpenGarrisonPluginPathContainment.ResolveContainedPath(
                pluginDirectory,
                Path.Combine("..", "PluginSibling", "main.lua"),
                "escape"));
        Assert.Equal("escape", ex.Message);
    }

    [Fact]
    public void ManifestLoaderRejectsEntryPointOutsidePluginDirectory()
    {
        var rootPath = CreateTempRoot();
        var pluginDirectory = Path.Combine(rootPath, "Plugin");
        var siblingDirectory = Path.Combine(rootPath, "PluginSibling");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(siblingDirectory);

        var manifest = CreateLuaManifest(
            OpenGarrisonPluginType.Client,
            Path.Combine("..", "PluginSibling", "main.lua"),
            hostApiVersion: "1.0");

        Assert.False(OpenGarrisonPluginManifestLoader.TryResolveEntryPointPath(manifest, pluginDirectory, out var entryPointPath, out var error));
        Assert.Equal(string.Empty, entryPointPath);
        Assert.Contains("escapes plugin directory", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManifestLoaderReadsEcosystemMetadataAndValidatesConfigSchema()
    {
        var rootPath = CreateTempRoot();
        var pluginDirectory = Path.Combine(rootPath, "Plugin");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(Path.Combine(pluginDirectory, "Gameplay", "example.gg2"));
        File.WriteAllText(Path.Combine(pluginDirectory, "config.schema.json"), """{"type":"object","properties":{}}""");
        File.WriteAllText(Path.Combine(pluginDirectory, "Gameplay", "example.gg2", "pack.json"), """
            {
              "id": "tests.example",
              "displayName": "Test Gameplay Pack",
              "version": "1.0.0"
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.lua-plugin",
              "displayName": "Test Lua Plugin",
              "version": "1.0.0",
              "type": "Client",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" },
              "configSchemaPath": "config.schema.json",
              "dependencies": [
                { "id": "required.plugin", "version": "1.2.3" }
              ],
              "optionalDependencies": [
                { "id": "optional.plugin" }
              ],
              "conflicts": [ "conflicting.plugin" ],
              "loadOrder": {
                "before": [ "late.plugin" ],
                "after": [ "early.plugin" ]
              },
              "permissions": [
                { "id": "client.ui", "description": "Draw HUD widgets.", "required": true }
              ],
              "messageContracts": [
                {
                  "targetPluginId": "server.plugin",
                  "messageType": "score.update",
                  "payloadFormat": "Json",
                  "schemaVersion": 1,
                  "direction": "ClientToServer"
                }
              ],
              "gameplayPacks": [
                {
                  "path": "Gameplay/example.gg2",
                  "allowRuntimeClassBindingOverride": true
                }
              ]
            }
            """);

        Assert.True(OpenGarrisonPluginManifestLoader.TryLoadFromPath(
            Path.Combine(pluginDirectory, "plugin.json"),
            out var manifest,
            out var error), error);

        Assert.Equal("required.plugin", Assert.Single(manifest.Dependencies).Id);
        Assert.Equal("optional.plugin", Assert.Single(manifest.OptionalDependencies).Id);
        Assert.Equal("conflicting.plugin", Assert.Single(manifest.Conflicts));
        Assert.Equal("late.plugin", Assert.Single(manifest.LoadOrder.Before));
        Assert.Equal("early.plugin", Assert.Single(manifest.LoadOrder.After));
        Assert.Equal("client.ui", Assert.Single(manifest.Permissions).Id);
        Assert.Equal("score.update", Assert.Single(manifest.MessageContracts).MessageType);
        var gameplayPack = Assert.Single(manifest.GameplayPacks);
        Assert.Equal("Gameplay/example.gg2", gameplayPack.Path);
        Assert.True(gameplayPack.AllowRuntimeClassBindingOverride);
    }

    [Fact]
    public void ManifestLoaderRejectsInvalidEcosystemMetadata()
    {
        Assert.False(OpenGarrisonPluginManifestLoader.TryLoadFromJson("""
            {
              "schemaVersion": 1,
              "id": "tests.lua-plugin",
              "displayName": "Test Lua Plugin",
              "version": "1.0.0",
              "type": "Client",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" },
              "conflicts": [ "tests.lua-plugin" ]
            }
            """, out _, out var error));
        Assert.Contains("conflicts", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestLoaderRejectsEscapingConfigSchemaPath()
    {
        var rootPath = CreateTempRoot();
        var pluginDirectory = Path.Combine(rootPath, "Plugin");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.lua-plugin",
              "displayName": "Test Lua Plugin",
              "version": "1.0.0",
              "type": "Client",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" },
              "configSchemaPath": "../config.schema.json"
            }
            """);

        Assert.False(OpenGarrisonPluginManifestLoader.TryLoadFromPath(
            Path.Combine(pluginDirectory, "plugin.json"),
            out _,
            out var error));
        Assert.Contains("configSchemaPath escapes plugin directory", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManifestLoaderRejectsEscapingGameplayPackPath()
    {
        var rootPath = CreateTempRoot();
        var pluginDirectory = Path.Combine(rootPath, "Plugin");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.lua-plugin",
              "displayName": "Test Lua Plugin",
              "version": "1.0.0",
              "type": "Client",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" },
              "gameplayPacks": [
                { "path": "../Gameplay/example.gg2" }
              ]
            }
            """);

        Assert.False(OpenGarrisonPluginManifestLoader.TryLoadFromPath(
            Path.Combine(pluginDirectory, "plugin.json"),
            out _,
            out var error));
        Assert.Contains("gameplayPacks path escapes plugin directory", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManifestLoaderRejectsUnsupportedHostApiVersion()
    {
        var manifest = CreateLuaManifest(
            OpenGarrisonPluginType.Client,
            "main.lua",
            hostApiVersion: "999.0");

        Assert.False(OpenGarrisonPluginManifestLoader.TryValidateHostApiCompatibility(
            manifest,
            OpenGarrisonPluginHostApi.CreateClientDefault(),
            out var error));
        Assert.Contains("requires host API version 999.0", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestPlannerAppliesDependenciesConflictsAndLoadOrderHints()
    {
        var plugins = new[]
        {
            CreatePlannedPlugin("dependent.plugin", dependencies: [new OpenGarrisonPluginManifestDependency { Id = "base.plugin" }]),
            CreatePlannedPlugin("conflicting.plugin", conflicts: ["base.plugin"]),
            CreatePlannedPlugin("late.plugin"),
            CreatePlannedPlugin("base.plugin", before: ["late.plugin"]),
            CreatePlannedPlugin("optional.plugin", optionalDependencies: [new OpenGarrisonPluginManifestDependency { Id = "base.plugin" }]),
            CreatePlannedPlugin("missing-dependency.plugin", dependencies: [new OpenGarrisonPluginManifestDependency { Id = "not.installed" }]),
        };

        var result = OpenGarrisonPluginManifestPlanner.PlanLoadOrder(plugins, static plugin => plugin.Manifest);

        Assert.Equal(
            ["base.plugin", "dependent.plugin", "late.plugin", "optional.plugin"],
            result.Plugins.Select(static plugin => plugin.Manifest.Id).ToArray());
        Assert.Contains(result.Warnings, warning => warning.Contains("conflicting.plugin", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("missing-dependency.plugin", StringComparison.Ordinal));
    }

    [Fact]
    public void ClientLuaHostApiAdvertisesCurrentRuntimeSurface()
    {
        var functions = GetLuaFunctions(OpenGarrisonPluginHostApi.CreateClientDefault(), OpenGarrisonPluginType.Client);

        AssertContainsAll(functions,
            "get_client_runtime_state",
            "get_player_markers",
            "get_sentry_markers",
            "get_objective_markers",
            "show_notice",
            "show_overlay_menu",
            "show_prompt",
            "register_hotkey",
            "register_hud_widget",
            "register_scoreboard_panel",
            "capture_hotkey_input",
            "send_message_to_server",
            "format_key_display_name",
            "try_get_local_player_world_position");
    }

    [Fact]
    public void ServerLuaHostApiAdvertisesCurrentRuntimeSurface()
    {
        var functions = GetLuaFunctions(OpenGarrisonPluginHostApi.CreateServerDefault(), OpenGarrisonPluginType.Server);

        AssertContainsAll(functions,
            "get_admin_summary",
            "resolve_targets",
            "get_cvars",
            "set_cvar",
            "register_command",
            "get_match_state",
            "get_player_state",
            "get_objectives",
            "get_buildables",
            "get_projectiles",
            "get_recent_events",
            "enqueue_action",
            "schedule_once",
            "cancel_scheduled_task",
            "try_ban_player",
            "try_set_player_scale",
            "get_bot_slots",
            "try_start_demo_recording",
            "register_gameplay_ability_executor");
    }

    [Fact]
    public void GeneratedLuaHostApiSurfaceMatchesHostBindings()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expectedClientFunctions = ScanLuaHostBindings(Path.Combine(repositoryRoot, "Client", "Plugins", "LuaClientPlugin.cs"));
        var expectedServerFunctions = ScanLuaHostBindings(Path.Combine(repositoryRoot, "Server", "Plugins", "LuaServerPlugin.cs"));

        Assert.Equal(expectedClientFunctions, OpenGarrisonLuaHostApiSurface.ClientFunctions);
        Assert.Equal(expectedServerFunctions, OpenGarrisonLuaHostApiSurface.ServerFunctions);
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
        var method = typeof(ClientPluginHost)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "SendMessageToServer" && method.GetParameters().Length == 6);
        method.Invoke(host, [sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion]);
    }

    private static void InvokeClientSendMessage(
        ClientPluginHost host,
        string sourcePluginId,
        OpenGarrisonPluginManifest sourceManifest,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion)
    {
        var method = typeof(ClientPluginHost)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "SendMessageToServer" && method.GetParameters().Length == 7);
        method.Invoke(host, [sourcePluginId, sourceManifest, targetPluginId, messageType, payload, payloadFormat, schemaVersion]);
    }

    private static IOpenGarrisonServerPluginContext CreateServerContext(OpenGarrison.Server.PluginHost host, IOpenGarrisonServerPlugin plugin, string pluginDirectory)
    {
        return CreateServerContext(
            host,
            plugin,
            pluginDirectory,
            OpenGarrisonPluginManifest.CreateClr(plugin.Id, plugin.DisplayName, plugin.Version, OpenGarrisonPluginType.Server, "Plugin.dll", plugin.GetType().FullName));
    }

    private static IOpenGarrisonServerPluginContext CreateServerContext(
        OpenGarrison.Server.PluginHost host,
        IOpenGarrisonServerPlugin plugin,
        string pluginDirectory,
        OpenGarrisonPluginManifest manifest)
    {
        var method = typeof(OpenGarrison.Server.PluginHost).GetMethod("CreateContext", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IOpenGarrisonServerPluginContext>(method!.Invoke(host,
        [
            plugin,
            manifest,
            pluginDirectory,
        ]));
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenGarrison.PluginHost.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static OpenGarrisonPluginManifest CreateLuaManifest(
        OpenGarrisonPluginType pluginType,
        string entryPoint,
        string hostApiVersion)
    {
        return new OpenGarrisonPluginManifest
        {
            Id = "tests.lua-plugin",
            DisplayName = "Test Lua Plugin",
            Version = "1.0.0",
            Type = pluginType,
            Runtime = OpenGarrisonPluginRuntimeKind.Lua,
            EntryPoint = entryPoint,
            Compatibility = new OpenGarrisonPluginManifestCompatibility(hostApiVersion),
        };
    }

    private static OpenGarrisonPluginManifest CreateMessageContractManifest(
        string pluginId,
        OpenGarrisonPluginType pluginType,
        IReadOnlyList<OpenGarrisonPluginManifestMessageContract> messageContracts)
    {
        return OpenGarrisonPluginManifest.CreateClr(
            pluginId,
            pluginId,
            new Version(1, 0),
            pluginType,
            "Plugin.dll",
            "Plugin") with
        {
            MessageContracts = messageContracts,
        };
    }

    private static PlannedPlugin CreatePlannedPlugin(
        string id,
        IReadOnlyList<OpenGarrisonPluginManifestDependency>? dependencies = null,
        IReadOnlyList<OpenGarrisonPluginManifestDependency>? optionalDependencies = null,
        IReadOnlyList<string>? conflicts = null,
        IReadOnlyList<string>? before = null,
        IReadOnlyList<string>? after = null)
    {
        return new PlannedPlugin(new OpenGarrisonPluginManifest
        {
            Id = id,
            DisplayName = id,
            Version = "1.0.0",
            Type = OpenGarrisonPluginType.Client,
            Runtime = OpenGarrisonPluginRuntimeKind.Lua,
            EntryPoint = "main.lua",
            Dependencies = dependencies ?? Array.Empty<OpenGarrisonPluginManifestDependency>(),
            OptionalDependencies = optionalDependencies ?? Array.Empty<OpenGarrisonPluginManifestDependency>(),
            Conflicts = conflicts ?? Array.Empty<string>(),
            LoadOrder = new OpenGarrisonPluginManifestLoadOrderHints
            {
                Before = before ?? Array.Empty<string>(),
                After = after ?? Array.Empty<string>(),
            },
        });
    }

    private static IReadOnlyList<string> GetLuaFunctions(OpenGarrisonPluginHostApi hostApi, OpenGarrisonPluginType expectedHostType)
    {
        Assert.Equal(expectedHostType, hostApi.HostType);
        var surface = Assert.Single(hostApi.RuntimeSurfaces, surface => surface.Runtime == OpenGarrisonPluginRuntimeKind.Lua);
        Assert.Equal(hostApi.ApiVersion, surface.ApiVersion);
        Assert.Equal(surface.Functions.Count, surface.Functions.Distinct(StringComparer.Ordinal).Count());
        return surface.Functions;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenGarrison.sln"))
                && File.Exists(Path.Combine(directory.FullName, "Client", "Plugins", "LuaClientPlugin.cs"))
                && File.Exists(Path.Combine(directory.FullName, "Server", "Plugins", "LuaServerPlugin.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private static IReadOnlyList<string> ScanLuaHostBindings(string sourcePath)
    {
        var source = File.ReadAllText(sourcePath);
        return Regex.Matches(source, """host\["(?<name>[A-Za-z0-9_]+)"\]\s*=""", RegexOptions.CultureInvariant)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertContainsAll(IReadOnlyCollection<string> values, params string[] expectedValues)
    {
        foreach (var expectedValue in expectedValues)
        {
            Assert.Contains(expectedValue, values);
        }
    }

    private sealed record PlannedPlugin(OpenGarrisonPluginManifest Manifest);

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
        public float SoundEffectsVolumeScale => 1f;
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

        public IReadOnlyList<string> GetLocalGameplayItemIds() => [];

        public IReadOnlyList<string> GetLocalGameplayAbilityItemIds() => [];

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

    private sealed class FakeServerCommand(
        string name,
        Func<OpenGarrisonServerCommandContext, string, IReadOnlyList<string>> execute) : IOpenGarrisonServerCommand
    {
        public string Name { get; } = name;

        public string Description => "Fake command";

        public string Usage => name;

        public Task<IReadOnlyList<string>> ExecuteAsync(
            OpenGarrisonServerCommandContext context,
            string arguments,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(execute(context, arguments));
        }
    }

    private sealed class FakeServerReadOnlyState : IOpenGarrisonServerReadOnlyState
    {
        public string ServerName => "test";
        public string LevelName => "test";
        public int MapAreaIndex => 1;
        public int MapAreaCount => 1;
        public float MapScale => 1f;
        public GameModeKind GameMode => default;
        public MatchPhase MatchPhase => default;
        public int RedCaps => 0;
        public int BlueCaps => 0;

        public IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers() => [];

        public IReadOnlyList<OpenGarrisonServerGameplayModPackInfo> GetGameplayModPacks() => [];

        public IReadOnlyList<OpenGarrisonServerGameplayClassInfo> GetGameplayClasses(string? modPackId = null) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetGameplayItems(string? modPackId = null) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetGameplayAbilities() => [];

        public IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetPlayerGameplayAbilities(int playerId) => [];

        public bool TryGetPlayerGameplayAbility(int playerId, string category, out OpenGarrisonServerGameplayAbilityInfo ability)
        {
            ability = default;
            return false;
        }

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

        public bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value)
        {
            value = 0;
            return false;
        }

        public bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value)
        {
            value = 0f;
            return false;
        }

        public bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value)
        {
            value = false;
            return false;
        }
    }

    private sealed class FakeServerAdminOperations : IOpenGarrisonServerAdminOperations
    {
        public List<(byte Slot, string Text)> SystemMessages { get; } = [];

        public void BroadcastSystemMessage(string text)
        {
        }

        public void SendSystemMessage(byte slot, string text)
        {
            SystemMessages.Add((slot, text));
        }

        public bool TryRenamePlayer(byte slot, string newName) => true;

        public bool TryDisconnect(byte slot, string reason) => true;

        public OpenGarrisonServerBanActionResult TryBanPlayer(byte slot, TimeSpan? duration, string reason) => new(true, "127.0.0.1", string.Empty, !duration.HasValue, 0);

        public OpenGarrisonServerBanActionResult TryBanIpAddress(string ipAddress, TimeSpan? duration, string reason) => new(true, ipAddress, string.Empty, !duration.HasValue, 0);

        public OpenGarrisonServerAddressActionResult TryUnbanIpAddress(string ipAddress) => new(true, ipAddress, string.Empty);

        public bool TrySetPlayerGagged(byte slot, bool isGagged) => true;

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

        public bool TryIgnitePlayer(byte slot, float durationSeconds) => true;

        public bool TrySetPlayerScale(byte slot, float scale) => true;

        public bool TrySetPlayerMovementSpeedScale(byte slot, float scale) => true;

        public bool TryClearPlayerMovementSpeedScale(byte slot) => true;

        public bool TrySetPlayerGravityScale(byte slot, float scale) => true;

        public bool TryClearPlayerGravityScale(byte slot) => true;

        public bool TrySetTimeLimit(int timeLimitMinutes) => true;

        public bool TrySetCapLimit(int capLimit) => true;

        public bool TrySetRespawnSeconds(int respawnSeconds) => true;

        public bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false) => true;

        public bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1) => true;

        public bool TryAddBot(byte slot, PlayerTeam team, PlayerClass playerClass, string displayName) => true;

        public bool TryRemoveBot(byte slot) => true;

        public bool TrySetBotTeam(byte slot, PlayerTeam team) => true;

        public bool TrySetBotClass(byte slot, PlayerClass playerClass) => true;

        public int TryFillBots(int targetPerTeam, PlayerClass? requestedClass) => 0;

        public int TryFillBotTeam(PlayerTeam team, int targetCount, PlayerClass? requestedClass) => 0;

        public IReadOnlyList<OpenGarrisonServerBotSlotInfo> GetBotSlots() => [];

        public int TryClearAllBots() => 0;

        public string GetDemoRecordingStatus() => "[server] demo | status=idle";

        public OpenGarrisonServerDemoRecordingResult TryStartDemoRecording(string? requestedPath) => new(true, "[server] demo recording started: test.ogdemo", string.Empty);

        public OpenGarrisonServerDemoRecordingResult TryStopDemoRecording() => new(true, "[server] demo recording stopped: test.ogdemo", string.Empty);
    }

    private sealed class FakeServerCvarRegistry : IOpenGarrisonServerCvarRegistry
    {
        public IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll(bool includeProtectedValues) => [];

        public bool TryGet(string name, bool includeProtectedValue, out OpenGarrisonServerCvarInfo cvar)
        {
            cvar = default;
            return false;
        }

        public bool TrySet(string name, string value, bool allowProtectedMutation, out OpenGarrisonServerCvarInfo cvar, out string errorMessage)
        {
            cvar = default;
            errorMessage = "unsupported";
            return false;
        }

        public bool TryProtect(string name, out OpenGarrisonServerCvarInfo cvar, out string errorMessage)
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
