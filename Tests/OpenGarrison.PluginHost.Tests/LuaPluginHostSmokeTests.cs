using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.PluginHost;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LuaPluginHostSmokeTests
{
    [Fact]
    public void ClientLuaDamageIndicatorTemplateBootstrapsAndDrawsHud()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadClientLuaTemplate("sample.client.lua-damage-indicator", tempDirectory, logs);

        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonClientUpdateHooks>(loadedPlugin.Plugin);
        var damageHooks = Assert.IsAssignableFrom<IOpenGarrisonClientDamageHooks>(loadedPlugin.Plugin);
        var semanticHooks = Assert.IsAssignableFrom<IOpenGarrisonClientSemanticGameplayHooks>(loadedPlugin.Plugin);
        var hudHooks = Assert.IsAssignableFrom<IOpenGarrisonClientHudHooks>(loadedPlugin.Plugin);
        var optionsHooks = Assert.IsAssignableFrom<IOpenGarrisonClientOptionsHooks>(loadedPlugin.Plugin);

        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 1, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));
        damageHooks.OnLocalDamage(new LocalDamageEvent(
            42,
            OpenGarrison.Client.Plugins.DamageTargetKind.Player,
            3,
            new Vector2(128f, 96f),
            TargetWasKilled: false,
            DealtByLocalPlayer: true,
            AssistedByLocalPlayer: false,
            ReceivedByLocalPlayer: false,
            AttackerPlayerId: 1,
            AssistedByPlayerId: 0,
            Flags: LocalDamageFlags.Airshot));
        semanticHooks.OnHeal(new ClientHealEvent(18, 110, 125, 2));
        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 2, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));

        var canvas = new FakeHudCanvas();
        hudHooks.OnGameplayHudDraw(canvas);

        Assert.NotEmpty(optionsHooks.GetOptionsSections());
        Assert.Contains(loadedPlugin.Context.AssetsImpl.RegisteredSounds, asset => asset.AssetId == "ding");
        Assert.True(File.Exists(Path.Combine(loadedPlugin.ConfigDirectory, "damageindicator.json")));
        Assert.True(canvas.BitmapTextDrawCount + canvas.BitmapTextCenteredDrawCount > 0);
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaBubbleWheelTemplateBootstrapsAndHandlesBubbleMenuInput()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadClientLuaTemplate("sample.client.lua-bubble-wheel", tempDirectory, logs);

        var lifecycleHooks = Assert.IsAssignableFrom<IOpenGarrisonClientLifecycleHooks>(loadedPlugin.Plugin);
        var bubbleMenuHooks = Assert.IsAssignableFrom<IOpenGarrisonClientBubbleMenuHooks>(loadedPlugin.Plugin);

        lifecycleHooks.OnClientStarted();

        var pageSwitchResult = bubbleMenuHooks.TryHandleBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.X,
            XPageIndex: 0,
            AimDirectionDegrees: 0f,
            DistanceFromCenter: 60f,
            LeftMousePressed: false,
            LeftMouseDown: false,
            LeftMouseReleased: false,
            PressedDigit: 3,
            QPressed: false));
        Assert.NotNull(pageSwitchResult);
        Assert.Equal(2, pageSwitchResult!.NewXPageIndex);
        Assert.True(pageSwitchResult.ClearBubbleSelection);

        var zMenuResult = bubbleMenuHooks.TryHandleBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.Z,
            XPageIndex: 0,
            AimDirectionDegrees: 0f,
            DistanceFromCenter: 60f,
            LeftMousePressed: false,
            LeftMouseDown: false,
            LeftMouseReleased: false,
            PressedDigit: null,
            QPressed: false));
        Assert.NotNull(zMenuResult);
        Assert.True(zMenuResult!.BubbleFrame.HasValue);
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaMoreAnimationsTemplateBootstraps()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadClientLuaTemplate("sample.client.lua-more-animations", tempDirectory, logs);

        Assert.Equal("sample.client.lua-more-animations", loadedPlugin.Plugin.Id);
        var deadBodyHooks = Assert.IsAssignableFrom<IOpenGarrisonClientDeadBodyHooks>(loadedPlugin.Plugin);
        Assert.False(deadBodyHooks.TryDrawDeadBody(
            new FakeHudCanvas(),
            new ClientDeadBodyRenderState(
                1,
                ClientPluginClass.Soldier,
                ClientPluginTeam.Red,
                new Vector2(128f, 96f),
                64f,
                64f,
                FacingLeft: false,
                TicksRemaining: 240,
                AnimationKind: ClientDeadBodyAnimationKind.Rifle)));
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaTeamOnlyMinimapTemplateBootstrapsAndDrawsHudImmediately()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadClientLuaTemplate("sample.client.lua-team-only-minimap", tempDirectory, logs);

        var hudHooks = Assert.IsAssignableFrom<IOpenGarrisonClientHudHooks>(loadedPlugin.Plugin);

        var state = loadedPlugin.Context.StateImpl;
        state.PlayerMarkers.Add(new ClientPlayerMarker(1, "Scout", ClientPluginTeam.Red, ClientPluginClass.Scout, new Vector2(64f, 64f), 100, 125, true, false, true));

        var canvas = new FakeHudCanvas();
        hudHooks.OnGameplayHudDraw(canvas);

        Assert.True(File.Exists(Path.Combine(loadedPlugin.ConfigDirectory, "teamonlyminimap.json")));
        Assert.Contains(loadedPlugin.Context.UiImpl.MenuEntries, entry => entry.MenuEntryId == "toggle-minimap");
        Assert.True(canvas.FilledRectangleCount > 0);
        Assert.True(canvas.OutlinedRectangleCount > 0);
        Assert.DoesNotContain(logs, log => log.Contains("failed to initialize", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaHostSupportsInitializeTimeConfigAndMenuRegistration()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-initialize-bootstrap",
            "Lua Initialize Bootstrap",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
                plugin.host.load_json_config("initialize-bootstrap.json", { enabled = true, zoomKey = "R" })
                plugin.host.register_menu_entry("toggle-bootstrap", "Toggle Bootstrap", "InGameMenu", "toggle_bootstrap")
            end

            function plugin.toggle_bootstrap()
            end

            return plugin
            """,
            tempDirectory,
            logs);

        Assert.True(File.Exists(Path.Combine(loadedPlugin.ConfigDirectory, "initialize-bootstrap.json")));
        Assert.Contains(loadedPlugin.Context.UiImpl.MenuEntries, entry => entry.MenuEntryId == "toggle-bootstrap");
        Assert.DoesNotContain(logs, log => log.Contains("failed to initialize", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaHostSupportsInitializeTimeHotkeyRegistration()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-initialize-hotkey",
            "Lua Initialize Hotkey",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
                plugin.host.register_hotkey("initialize-hotkey", "Initialize Hotkey", "R")
            end

            return plugin
            """,
            tempDirectory,
            logs);

        Assert.Contains(loadedPlugin.Context.HotkeysImpl.RegisteredHotkeys, entry => entry.HotkeyId == "initialize-hotkey");
        Assert.DoesNotContain(logs, log => log.Contains("failed to initialize", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaHostExposesPlayerMarkersInClientState()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-client-state-markers",
            "Lua Client State Markers",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_gameplay_hud_draw(canvas)
                local state = plugin.host.get_client_state()
                local marker = state.playerMarkers and state.playerMarkers[1]
                if marker ~= nil and marker.team == state.localPlayerTeam and marker.isLocalPlayer then
                    canvas.fill_screen_rectangle(0, 0, 1, 1, { r = 255, g = 255, b = 255, a = 255 })
                end
            end

            return plugin
            """,
            tempDirectory,
            logs);

        var hudHooks = Assert.IsAssignableFrom<IOpenGarrisonClientHudHooks>(loadedPlugin.Plugin);
        var state = loadedPlugin.Context.StateImpl;
        state.PlayerMarkers.Add(new ClientPlayerMarker(1, "Scout", ClientPluginTeam.Red, ClientPluginClass.Scout, new Vector2(64f, 64f), 100, 125, true, false, true));

        var canvas = new FakeHudCanvas();
        hudHooks.OnGameplayHudDraw(canvas);

        Assert.True(canvas.FilledRectangleCount > 0);
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaHostRejectsTextureRegistrationDuringHudDraw()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-hud-registration",
            "Lua HUD Registration",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_gameplay_hud_draw(canvas)
                plugin.host.register_texture_asset("draw-texture", "missing.png")
            end

            return plugin
            """,
            tempDirectory,
            logs);

        var hudHooks = Assert.IsAssignableFrom<IOpenGarrisonClientHudHooks>(loadedPlugin.Plugin);
        hudHooks.OnGameplayHudDraw(new FakeHudCanvas());

        Assert.Contains(logs, log => log.Contains("register_texture_asset rejected", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, log => log.Contains("callback failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaHostRejectsLegacyAnimationRegistrationDuringDeadBodyDraw()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-deadbody-registration",
            "Lua Dead Body Registration",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.try_draw_dead_body(canvas, dead_body)
                plugin.host.register_legacy_animation_asset("corpse", "missing.png", 1, true)
                return false
            end

            return plugin
            """,
            tempDirectory,
            logs);

        var deadBodyHooks = Assert.IsAssignableFrom<IOpenGarrisonClientDeadBodyHooks>(loadedPlugin.Plugin);
        Assert.False(deadBodyHooks.TryDrawDeadBody(
            new FakeHudCanvas(),
            new ClientDeadBodyRenderState(
                1,
                ClientPluginClass.Soldier,
                ClientPluginTeam.Red,
                new Vector2(128f, 96f),
                64f,
                64f,
                FacingLeft: false,
                TicksRemaining: 240,
                AnimationKind: ClientDeadBodyAnimationKind.Rifle)));

        Assert.Contains(logs, log => log.Contains("register_legacy_animation_asset rejected", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, log => log.Contains("callback failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaHostRejectsEscapingConfigPaths()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-config-escape",
            "Lua Config Escape",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_client_frame(e)
                plugin.host.save_json_config("../escape.json", { count = 1 })
            end

            return plugin
            """,
            tempDirectory,
            logs);

        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonClientUpdateHooks>(loadedPlugin.Plugin);
        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 1, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));

        Assert.Contains(logs, log => log.Contains("Plugin config path escapes config directory.", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(tempDirectory.RootPath, "escape.json")));
    }

    [Fact]
    public void ClientLuaHostRejectsEscapingPluginFileEnumeration()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-list-files-escape",
            "Lua List Files Escape",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_client_frame(e)
                plugin.host.list_files("../", "*")
            end

            return plugin
            """,
            tempDirectory,
            logs);

        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonClientUpdateHooks>(loadedPlugin.Plugin);
        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 1, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));

        Assert.Contains(logs, log => log.Contains("Plugin path escapes plugin directory.", StringComparison.Ordinal));
    }

    [Fact]
    public void ClientLuaHostDisablesPluginAfterCallbackBudgetExceeded()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-timeout",
            "Lua Timeout",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_client_frame(e)
                local sum = 0
                for i = 1, 100000000 do
                    sum = sum + i
                end
            end

            return plugin
            """,
            tempDirectory,
            logs);

        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonClientUpdateHooks>(loadedPlugin.Plugin);
        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 1, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));
        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 2, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));

        Assert.Contains(logs, log => log.Contains("disabled tests.client.lua-timeout", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("budget", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, logs.Count(log => log.Contains("disabled tests.client.lua-timeout", StringComparison.Ordinal)));
    }

    [Fact]
    public void ClientLuaPluginLoaderBootstrapsLuaPluginAndPersistsConfig()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ClientLuaSmoke");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.client.lua-smoke",
              "displayName": "Lua Client Smoke",
              "version": "1.0.0",
              "type": "Client",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), """
            local plugin = {}
            local camera_offset = nil

            function plugin.initialize(host)
                plugin.host = host
                local config = host.load_json_config("client-smoke.json", { count = 1 })
                config.count = (config.count or 0) + 1
                host.save_json_config("client-smoke.json", config)
                host.log("client initialized")
            end

            function plugin.on_client_frame(e)
                if e.isGameplayActive then
                    camera_offset = plugin.host.vec2(3.5, -2.25)
                end
            end

            function plugin.get_camera_offset()
                return camera_offset or plugin.host.vec2(0.0, 0.0)
            end

            return plugin
            """);

        var logs = new List<string>();
        var discoveredPlugins = ClientPluginLoader.DiscoverFromDirectory(tempDirectory.RootPath, logs.Add);
        var discoveredPlugin = Assert.Single(discoveredPlugins);
        Assert.Equal("tests.client.lua-smoke", discoveredPlugin.PluginId);
        Assert.Equal(OpenGarrisonPluginRuntimeKind.Lua, discoveredPlugin.Manifest.Runtime);

        var configDirectory = tempDirectory.CreateSubdirectory("ClientConfig");
        var context = new FakeClientPluginContext(discoveredPlugin.Manifest, pluginDirectory, configDirectory, logs);
        var loadedPlugin = ClientPluginLoader.TryLoadDiscoveredPlugin(
            discoveredPlugin,
            (_, manifest, directory) => context,
            logs.Add);

        Assert.NotNull(loadedPlugin);
        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonClientUpdateHooks>(loadedPlugin!.Plugin);
        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 1, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));

        var cameraHooks = Assert.IsAssignableFrom<IOpenGarrisonClientCameraHooks>(loadedPlugin.Plugin);
        Assert.Equal(new Vector2(3.5f, -2.25f), cameraHooks.GetCameraOffset());

        var configPath = Path.Combine(configDirectory, "client-smoke.json");
        Assert.True(File.Exists(configPath));
        var json = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(2, json.RootElement.GetProperty("count").GetInt32());
        Assert.Contains(logs, log => log.Contains("client initialized", StringComparison.Ordinal));
    }

    [Fact]
    public void ServerLuaPluginLoaderBootstrapsLuaPluginAndPersistsConfig()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaSmoke");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-smoke",
              "displayName": "Lua Server Smoke",
              "version": "1.0.0",
              "type": "Server",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
                local config = host.load_json_config("server-smoke.json", { count = 10 })
                config.count = (config.count or 0) + 5
                host.save_json_config("server-smoke.json", config)
                host.log("server initialized")
            end

            function plugin.on_server_heartbeat(seconds)
                plugin.last_heartbeat = seconds
            end

            function plugin.try_handle_chat_message(context, e)
                return e.text == "!lua"
            end

            return plugin
            """);

        var logs = new List<string>();
        var configDirectory = tempDirectory.CreateSubdirectory("ServerConfig");
        Assert.True(
            OpenGarrisonPluginManifestLoader.TryLoadFromPath(Path.Combine(pluginDirectory, "plugin.json"), out var manifest, out var manifestError),
            manifestError);

        var fakeContext = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            configDirectory,
            tempDirectory.CreateSubdirectory("Maps"),
            logs);

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, manifest, directory) => fakeContext,
            logs.Add);

        var loadedPlugin = Assert.Single(loadedPlugins);
        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonServerUpdateHooks>(loadedPlugin.Plugin);
        updateHooks.OnServerHeartbeat(TimeSpan.FromSeconds(42));

        var chatHooks = Assert.IsAssignableFrom<IOpenGarrisonServerChatCommandHooks>(loadedPlugin.Plugin);
        var handled = chatHooks.TryHandleChatMessage(
            new OpenGarrisonServerChatMessageContext(fakeContext.ServerState, fakeContext.AdminOperations),
            new ChatReceivedEvent(1, "Tester", "!lua", Team: null, TeamOnly: false));
        Assert.True(handled);

        var configPath = Path.Combine(configDirectory, "server-smoke.json");
        Assert.True(File.Exists(configPath));
        var json = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(15, json.RootElement.GetProperty("count").GetInt32());
        Assert.Contains(logs, log => log.Contains("server initialized", StringComparison.Ordinal));
    }

    [Fact]
    public void ServerLuaHostRejectsEscapingConfigPaths()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaEscape");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-config-escape",
              "displayName": "Lua Server Config Escape",
              "version": "1.0.0",
              "type": "Server",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_server_heartbeat(seconds)
                plugin.host.save_json_config("../escape.json", { count = 1 })
            end

            return plugin
            """);

        var logs = new List<string>();
        var configDirectory = tempDirectory.CreateSubdirectory("ServerConfig");
        Assert.True(
            OpenGarrisonPluginManifestLoader.TryLoadFromPath(Path.Combine(pluginDirectory, "plugin.json"), out var manifest, out var manifestError),
            manifestError);

        var fakeContext = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            configDirectory,
            tempDirectory.CreateSubdirectory("Maps"),
            logs);

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, manifest, directory) => fakeContext,
            logs.Add);

        var loadedPlugin = Assert.Single(loadedPlugins);
        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonServerUpdateHooks>(loadedPlugin.Plugin);
        updateHooks.OnServerHeartbeat(TimeSpan.FromSeconds(1));

        Assert.Contains(logs, log => log.Contains("Plugin config path escapes config directory.", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(tempDirectory.RootPath, "escape.json")));
    }

    [Fact]
    public void ServerLuaHostDisablesPluginAfterCallbackBudgetExceeded()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaTimeout");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-timeout",
              "displayName": "Lua Server Timeout",
              "version": "1.0.0",
              "type": "Server",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_server_heartbeat(seconds)
                local sum = 0
                for i = 1, 100000000 do
                    sum = sum + i
                end
            end

            return plugin
            """);

        var logs = new List<string>();
        var configDirectory = tempDirectory.CreateSubdirectory("ServerConfig");
        Assert.True(
            OpenGarrisonPluginManifestLoader.TryLoadFromPath(Path.Combine(pluginDirectory, "plugin.json"), out var manifest, out var manifestError),
            manifestError);

        var fakeContext = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            configDirectory,
            tempDirectory.CreateSubdirectory("Maps"),
            logs);

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, manifest, directory) => fakeContext,
            logs.Add);

        var loadedPlugin = Assert.Single(loadedPlugins);
        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonServerUpdateHooks>(loadedPlugin.Plugin);
        updateHooks.OnServerHeartbeat(TimeSpan.FromSeconds(1));
        updateHooks.OnServerHeartbeat(TimeSpan.FromSeconds(2));

        Assert.Contains(logs, log => log.Contains("disabled tests.server.lua-timeout", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("budget", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, logs.Count(log => log.Contains("disabled tests.server.lua-timeout", StringComparison.Ordinal)));
    }

    [Fact]
    public void ServerLuaHostExposesGameplayCatalog()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaGameplayCatalog");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-gameplay-catalog",
              "displayName": "Lua Server Gameplay Catalog",
              "version": "1.0.0",
              "type": "Server",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_server_heartbeat(seconds)
                local packs = plugin.host.get_gameplay_mod_packs()
                local classes = plugin.host.get_gameplay_classes("stock.gg2")
                local items = plugin.host.get_gameplay_items("stock.gg2")
                local ownedItems = plugin.host.get_owned_gameplay_items(1)
                local loadouts = plugin.host.get_gameplay_loadouts_for_class("soldier")
                local secondaryItems = plugin.host.get_available_gameplay_secondary_items(1)
                local acquiredItems = plugin.host.get_available_gameplay_acquired_items(1)

                if packs[1] ~= nil and packs[1].modPackId == "stock.gg2" then
                    plugin.host.log("gameplay-pack-ok")
                end

                if classes[1] ~= nil and classes[1].classId ~= nil then
                    plugin.host.log("gameplay-class-ok")
                end

                if items[1] ~= nil and items[1].itemId ~= nil then
                    plugin.host.log("gameplay-item-ok")
                end

                if ownedItems[1] ~= nil and ownedItems[1].itemId ~= nil then
                    plugin.host.log("gameplay-owned-ok")
                end

                local foundStockLoadout = false
                local foundDirectHitLoadout = false
                for _, loadout in pairs(loadouts) do
                    if loadout ~= nil and loadout.loadoutId == "soldier.stock" then
                        foundStockLoadout = true
                    end

                    if loadout ~= nil and loadout.loadoutId == "soldier.direct-hit" then
                        foundDirectHitLoadout = true
                    end
                end

                if foundStockLoadout and foundDirectHitLoadout then
                    plugin.host.log("gameplay-loadout-ok")
                end

                if secondaryItems[1] ~= nil and secondaryItems[1].itemId ~= nil and secondaryItems[1].isOwnedByPlayer ~= nil then
                    plugin.host.log("gameplay-secondary-ok")
                end

                if acquiredItems[1] ~= nil and acquiredItems[1].itemId ~= nil and acquiredItems[1].isOwnedByPlayer ~= nil then
                    plugin.host.log("gameplay-acquired-ok")
                end
            end

            return plugin
            """);

        var logs = new List<string>();
        var configDirectory = tempDirectory.CreateSubdirectory("ServerConfig");
        Assert.True(
            OpenGarrisonPluginManifestLoader.TryLoadFromPath(Path.Combine(pluginDirectory, "plugin.json"), out var manifest, out var manifestError),
            manifestError);

        var fakeContext = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            configDirectory,
            tempDirectory.CreateSubdirectory("Maps"),
            logs);

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, _, _) => fakeContext,
            logs.Add);

        var loadedPlugin = Assert.Single(loadedPlugins);
        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonServerUpdateHooks>(loadedPlugin.Plugin);
        updateHooks.OnServerHeartbeat(TimeSpan.FromSeconds(1));

        Assert.Contains(logs, log => log.Contains("gameplay-pack-ok", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("gameplay-class-ok", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("gameplay-item-ok", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("gameplay-owned-ok", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("gameplay-loadout-ok", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("gameplay-secondary-ok", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("gameplay-acquired-ok", StringComparison.Ordinal));
    }

    [Fact]
    public void ServerLuaHostSupportsGameplayItemSelectionWrites()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaGameplaySelectionWrites");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-gameplay-selection-writes",
              "displayName": "Lua Server Gameplay Selection Writes",
              "version": "1.0.0",
              "type": "Server",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_server_heartbeat(seconds)
                assert(plugin.host.try_grant_gameplay_item(1, "weapon.scattergun"))
                assert(plugin.host.try_set_gameplay_secondary_item(1, "weapon.scattergun"))
                assert(plugin.host.try_set_gameplay_secondary_item(1, nil))
                assert(plugin.host.try_grant_gameplay_item(1, "weapon.flamethrower"))
                assert(plugin.host.try_set_gameplay_acquired_item(1, "weapon.flamethrower"))
                assert(plugin.host.try_set_gameplay_acquired_item(1, nil))
                assert(plugin.host.try_revoke_gameplay_item(1, "weapon.scattergun"))
                assert(plugin.host.try_revoke_gameplay_item(1, "weapon.flamethrower"))
                plugin.host.log("gameplay-selection-write-ok")
            end

            return plugin
            """);

        Assert.True(
            OpenGarrisonPluginManifestLoader.TryLoadFromPath(Path.Combine(pluginDirectory, "plugin.json"), out var manifest, out var manifestError),
            manifestError);
        var configDirectory = tempDirectory.CreateSubdirectory("Config");
        var logs = new List<string>();
        var fakeContext = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            configDirectory,
            tempDirectory.CreateSubdirectory("Maps"),
            logs);

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, _, _) => fakeContext,
            logs.Add);

        var loadedPlugin = Assert.Single(loadedPlugins);
        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonServerUpdateHooks>(loadedPlugin.Plugin);
        updateHooks.OnServerHeartbeat(TimeSpan.FromSeconds(1));

        Assert.Contains(logs, log => log.Contains("gameplay-selection-write-ok", StringComparison.Ordinal));
        Assert.Equal(4, fakeContext.AdminImpl.GameplayOwnershipChanges.Count);
        Assert.Equal(4, fakeContext.AdminImpl.GameplayItemSelections.Count);
        Assert.Collection(
            fakeContext.AdminImpl.GameplayItemSelections,
            selection =>
            {
                Assert.Equal("secondary", selection.SelectionKind);
                Assert.Equal((byte)1, selection.Slot);
                Assert.Equal("weapon.scattergun", selection.ItemId);
            },
            selection =>
            {
                Assert.Equal("secondary", selection.SelectionKind);
                Assert.Equal((byte)1, selection.Slot);
                Assert.Null(selection.ItemId);
            },
            selection =>
            {
                Assert.Equal("acquired", selection.SelectionKind);
                Assert.Equal((byte)1, selection.Slot);
                Assert.Equal("weapon.flamethrower", selection.ItemId);
            },
            selection =>
            {
                Assert.Equal("acquired", selection.SelectionKind);
                Assert.Equal((byte)1, selection.Slot);
                Assert.Null(selection.ItemId);
            });
        Assert.Equal(4, fakeContext.AdminImpl.GameplayItemSelectionAttempts.Count);
        Assert.Contains(fakeContext.AdminImpl.GameplayItemSelectionAttempts, attempt =>
            attempt.SelectionKind == "secondary" && attempt.Slot == 1 && attempt.ItemId == "weapon.scattergun");
        Assert.Contains(fakeContext.AdminImpl.GameplayItemSelectionAttempts, attempt =>
            attempt.SelectionKind == "acquired" && attempt.Slot == 1 && attempt.ItemId == "weapon.flamethrower");
        Assert.Contains(fakeContext.AdminImpl.GameplayOwnershipChanges, change =>
            change.ChangeKind == "grant" && change.Slot == 1 && change.ItemId == "weapon.scattergun");
        Assert.Contains(fakeContext.AdminImpl.GameplayOwnershipChanges, change =>
            change.ChangeKind == "grant" && change.Slot == 1 && change.ItemId == "weapon.flamethrower");
        Assert.Contains(fakeContext.AdminImpl.GameplayOwnershipChanges, change =>
            change.ChangeKind == "revoke" && change.Slot == 1 && change.ItemId == "weapon.scattergun");
        Assert.Contains(fakeContext.AdminImpl.GameplayOwnershipChanges, change =>
            change.ChangeKind == "revoke" && change.Slot == 1 && change.ItemId == "weapon.flamethrower");
    }

    private static LoadedClientLuaTemplate LoadClientLuaTemplate(string pluginId, TempDirectory tempDirectory, List<string> logs)
    {
        var repoRoot = FindRepositoryRoot();
        var templatesDirectory = Path.Combine(repoRoot, "Plugins", "Templates");
        var discoveredPlugin = ClientPluginLoader.DiscoverFromDirectory(templatesDirectory, logs.Add)
            .Single(plugin => string.Equals(plugin.PluginId, pluginId, StringComparison.Ordinal));

        var configDirectory = tempDirectory.CreateSubdirectory(pluginId.Replace('.', '_'));
        var context = new FakeClientPluginContext(discoveredPlugin.Manifest, discoveredPlugin.PluginDirectory, configDirectory, logs);
        var loadedPlugin = ClientPluginLoader.TryLoadDiscoveredPlugin(
            discoveredPlugin,
            (_, manifest, directory) => context,
            logs.Add);

        Assert.True(loadedPlugin is not null, string.Join(Environment.NewLine, logs));
        return new LoadedClientLuaTemplate(loadedPlugin!.Plugin, context, configDirectory);
    }

    private static LoadedClientLuaTemplate LoadAdHocClientLuaPlugin(
        string pluginId,
        string displayName,
        string mainLua,
        TempDirectory tempDirectory,
        List<string> logs)
    {
        var pluginDirectory = tempDirectory.CreateSubdirectory(pluginId.Replace('.', '_'));
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{pluginId}}",
              "displayName": "{{displayName}}",
              "version": "1.0.0",
              "type": "Client",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), mainLua);

        var discoveredPlugin = ClientPluginLoader.DiscoverFromDirectory(tempDirectory.RootPath, logs.Add)
            .Single(plugin => string.Equals(plugin.PluginId, pluginId, StringComparison.Ordinal));
        var configDirectory = tempDirectory.CreateSubdirectory(pluginId.Replace('.', '_') + "_config");
        var context = new FakeClientPluginContext(discoveredPlugin.Manifest, discoveredPlugin.PluginDirectory, configDirectory, logs);
        var loadedPlugin = ClientPluginLoader.TryLoadDiscoveredPlugin(
            discoveredPlugin,
            (_, manifest, directory) => context,
            logs.Add);

        Assert.True(loadedPlugin is not null, string.Join(Environment.NewLine, logs));
        return new LoadedClientLuaTemplate(loadedPlugin!.Plugin, context, configDirectory);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenGarrison.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Failed to locate repository root from test output directory.");
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "OpenGarrison.PluginHost.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string CreateSubdirectory(string name)
        {
            var path = Path.Combine(RootPath, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch
            {
            }
        }
    }

    private sealed record LoadedClientLuaTemplate(
        IOpenGarrisonClientPlugin Plugin,
        FakeClientPluginContext Context,
        string ConfigDirectory);

    private sealed class FakeClientPluginContext : IOpenGarrisonClientPluginContext
    {
        private readonly List<string> _logs;

        public FakeClientPluginContext(
            OpenGarrisonPluginManifest manifest,
            string pluginDirectory,
            string configDirectory,
            List<string> logs)
        {
            Manifest = manifest;
            PluginDirectory = pluginDirectory;
            ConfigDirectory = configDirectory;
            _logs = logs;
            StateImpl = new FakeClientReadOnlyState();
            AssetsImpl = new FakeClientAssets();
            HotkeysImpl = new FakeClientHotkeys();
            UiImpl = new FakeClientUi();
        }

        public string PluginId => Manifest.Id;

        public string PluginDirectory { get; }

        public string ConfigDirectory { get; }

        public OpenGarrisonPluginManifest Manifest { get; }

        public OpenGarrisonPluginHostApi HostApi { get; } = OpenGarrisonPluginHostApi.CreateClientDefault();

        public FakeClientReadOnlyState StateImpl { get; }

        public FakeClientAssets AssetsImpl { get; }

        public FakeClientHotkeys HotkeysImpl { get; }

        public FakeClientUi UiImpl { get; }

        public GraphicsDevice GraphicsDevice => null!;

        public IOpenGarrisonClientReadOnlyState ClientState => StateImpl;

        public IOpenGarrisonClientPluginAssets Assets => AssetsImpl;

        public IOpenGarrisonClientPluginHotkeys Hotkeys => HotkeysImpl;

        public IOpenGarrisonClientPluginUi Ui => UiImpl;

        public void Log(string message) => _logs.Add(message);

        public void SendMessageToServer(string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
        {
        }
    }

    private sealed class FakeClientReadOnlyState : IOpenGarrisonClientReadOnlyState
    {
        public bool IsConnected { get; set; } = true;
        public bool IsMainMenuOpen { get; set; }
        public bool IsGameplayActive { get; set; } = true;
        public bool IsGameplayInputBlocked { get; set; }
        public bool IsSpectator { get; set; }
        public bool IsDeathCamActive { get; set; }
        public ulong WorldFrame { get; set; } = 1;
        public int TickRate { get; set; } = 60;
        public int LocalPingMilliseconds { get; set; } = 24;
        public string LevelName { get; set; } = "test_level";
        public float LevelWidth { get; set; } = 1024f;
        public float LevelHeight { get; set; } = 768f;
        public int ViewportWidth { get; set; } = 640;
        public int ViewportHeight { get; set; } = 480;
        public int? LocalPlayerId { get; set; } = 1;
        public ClientPluginTeam LocalPlayerTeam { get; set; } = ClientPluginTeam.Red;
        public ClientPluginClass LocalPlayerClass { get; set; } = ClientPluginClass.Scout;
        public bool IsLocalPlayerAlive { get; set; } = true;
        public bool IsLocalPlayerScoped { get; set; }
        public bool IsLocalPlayerHealing { get; set; }
        public Vector2 CameraTopLeft { get; set; } = Vector2.Zero;
        public Vector2 LocalPlayerPosition { get; set; } = new(10f, 20f);
        public List<ClientPlayerMarker> PlayerMarkers { get; set; } = [];
        public List<ClientSentryMarker> SentryMarkers { get; set; } = [];
        public List<ClientObjectiveMarker> ObjectiveMarkers { get; set; } = [];

        public IReadOnlyList<ClientPlayerMarker> GetPlayerMarkers() => PlayerMarkers;
        public IReadOnlyList<ClientSentryMarker> GetSentryMarkers() => SentryMarkers;
        public IReadOnlyList<ClientObjectiveMarker> GetObjectiveMarkers() => ObjectiveMarkers;
        public bool IsPlayerCloaked(int playerId) => false;
        public bool IsPlayerVisibleToLocalViewer(int playerId) => true;
        public bool TryGetLocalPlayerHealth(out int health, out int maxHealth)
        {
            health = 125;
            maxHealth = 125;
            return true;
        }

        public bool TryGetLocalPlayerWorldPosition(out Vector2 position)
        {
            position = LocalPlayerPosition;
            return true;
        }

        public bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value)
        {
            value = default;
            return false;
        }

        public bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value)
        {
            value = default;
            return false;
        }

        public bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value)
        {
            value = default;
            return false;
        }

        public bool TryGetPlayerWorldPosition(int playerId, out Vector2 position)
        {
            var marker = PlayerMarkers.FirstOrDefault(candidate => candidate.PlayerId == playerId);
            if (marker is not null)
            {
                position = marker.WorldPosition;
                return true;
            }

            position = new Vector2(50f, 50f);
            return true;
        }

        public bool WasKeyPressedThisFrame(Keys key) => false;
    }

    private sealed class FakeClientAssets : IOpenGarrisonClientPluginAssets
    {
        public List<(string AssetId, string RelativePath)> RegisteredSounds { get; } = [];
        public List<(string AssetId, string RelativePath)> RegisteredTextures { get; } = [];
        public List<(string AssetId, string RelativePath, int FrameWidth, int FrameHeight)> RegisteredTextureAtlases { get; } = [];
        public List<(string AssetId, string TextureAssetId, Rectangle SourceRectangle)> RegisteredTextureRegions { get; } = [];

        public void RegisterSoundAsset(string assetId, string relativePath)
        {
            RegisteredSounds.Add((assetId, relativePath));
        }

        public void RegisterTextureAsset(string assetId, string relativePath)
        {
            RegisteredTextures.Add((assetId, relativePath));
        }

        public void RegisterTextureAtlasAsset(string assetId, string relativePath, int frameWidth, int frameHeight)
        {
            RegisteredTextureAtlases.Add((assetId, relativePath, frameWidth, frameHeight));
        }

        public void RegisterTextureRegionAsset(string assetId, string textureAssetId, Rectangle sourceRectangle)
        {
            RegisteredTextureRegions.Add((assetId, textureAssetId, sourceRectangle));
        }

        public bool TryGetSoundAsset(string assetId, out Microsoft.Xna.Framework.Audio.SoundEffect sound)
        {
            sound = null!;
            return false;
        }

        public bool TryGetTextureAsset(string assetId, out Texture2D texture)
        {
            texture = null!;
            return false;
        }

        public bool TryGetTextureAtlasAsset(string assetId, out ClientPluginTextureAtlas atlas)
        {
            atlas = default;
            return false;
        }

        public bool TryGetTextureRegionAsset(string assetId, out ClientPluginTextureRegion region)
        {
            region = default;
            return false;
        }
    }

    private sealed class FakeClientHotkeys : IOpenGarrisonClientPluginHotkeys
    {
        public List<(string HotkeyId, string DisplayName, Keys DefaultKey)> RegisteredHotkeys { get; } = [];

        public HashSet<string> PressedHotkeys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Keys RegisterHotkey(string hotkeyId, string displayName, Keys defaultKey)
        {
            RegisteredHotkeys.Add((hotkeyId, displayName, defaultKey));
            return defaultKey;
        }

        public bool WasHotkeyPressed(string hotkeyId) => PressedHotkeys.Remove(hotkeyId);
    }

    private sealed class FakeClientUi : IOpenGarrisonClientPluginUi
    {
        public List<(string MenuEntryId, string Label, ClientPluginMenuLocation Location, int Order, Action Activate)> MenuEntries { get; } = [];

        public List<(string Text, int DurationTicks, bool PlaySound)> Notices { get; } = [];

        public void RegisterMenuEntry(string menuEntryId, string label, ClientPluginMenuLocation location, Action activate, int order = 0)
        {
            MenuEntries.Add((menuEntryId, label, location, order, activate));
        }

        public void ShowNotice(string text, int durationTicks = 200, bool playSound = true)
        {
            Notices.Add((text, durationTicks, playSound));
        }
    }

    private sealed class FakeHudCanvas : IOpenGarrisonClientScoreboardCanvas
    {
        public int ViewportWidth => 640;

        public int ViewportHeight => 480;

        public Vector2 CameraTopLeft => Vector2.Zero;

        public int BitmapTextDrawCount { get; private set; }

        public int BitmapTextCenteredDrawCount { get; private set; }

        public int FilledRectangleCount { get; private set; }

        public int OutlinedRectangleCount { get; private set; }

        public int ScreenSpriteDrawCount { get; private set; }

        public Vector2 WorldToScreen(Vector2 worldPosition) => worldPosition;

        public float MeasureBitmapTextWidth(string text, float scale) => text.Length * 8f * scale;

        public float MeasureBitmapTextHeight(float scale) => 8f * scale;

        public void DrawBitmapText(string text, Vector2 position, Color color, float scale = 1f)
        {
            BitmapTextDrawCount += 1;
        }

        public void DrawBitmapTextCentered(string text, Vector2 position, Color color, float scale = 1f)
        {
            BitmapTextCenteredDrawCount += 1;
        }

        public void FillScreenRectangle(Rectangle rectangle, Color color)
        {
            FilledRectangleCount += 1;
        }

        public void DrawScreenRectangleOutline(Rectangle rectangle, Color color, int thickness = 1)
        {
            OutlinedRectangleCount += 1;
        }

        public void DrawScreenLine(Vector2 start, Vector2 endPoint, Color color, float thickness = 1f)
        {
        }

        public bool TryDrawScreenSprite(string spriteName, int frameIndex, Vector2 position, Color tint, Vector2 scale)
        {
            ScreenSpriteDrawCount += 1;
            return true;
        }

        public bool TryDrawWorldSprite(string spriteName, int frameIndex, Vector2 worldPosition, Color tint, float rotation = 0f) => true;

        public bool TryGetLevelBackgroundTexture(out Texture2D texture)
        {
            texture = null!;
            return false;
        }

        public void DrawScreenTexture(
            Texture2D texture,
            Vector2 position,
            Color tint,
            Vector2 scale,
            Rectangle? sourceRectangle = null,
            float rotation = 0f,
            Vector2? origin = null)
        {
        }

        public void DrawWorldTexture(
            Texture2D texture,
            Vector2 worldPosition,
            Color tint,
            Vector2 scale,
            Rectangle? sourceRectangle = null,
            float rotation = 0f,
            Vector2? origin = null)
        {
        }

        public void DrawBitmapTextRightAligned(string text, Vector2 position, Color color, float scale = 1f)
        {
            BitmapTextDrawCount += 1;
        }
    }

    private sealed class FakeServerPluginContext : IOpenGarrisonServerPluginContext
    {
        private readonly List<string> _logs;

        public FakeServerPluginContext(
            OpenGarrisonPluginManifest manifest,
            string pluginDirectory,
            string configDirectory,
            string mapsDirectory,
            List<string> logs)
        {
            Manifest = manifest;
            PluginDirectory = pluginDirectory;
            ConfigDirectory = configDirectory;
            MapsDirectory = mapsDirectory;
            _logs = logs;
        }

        public string PluginId => Manifest.Id;

        public string PluginDirectory { get; }

        public string ConfigDirectory { get; }

        public OpenGarrisonPluginManifest Manifest { get; }

        public OpenGarrisonPluginHostApi HostApi { get; } = OpenGarrisonPluginHostApi.CreateServerDefault();

        public string MapsDirectory { get; }

        public FakeServerReadOnlyState StateImpl { get; } = new();

        public FakeServerAdminOperations AdminImpl { get; } = new();

        public IOpenGarrisonServerReadOnlyState ServerState => StateImpl;

        public IOpenGarrisonServerAdminOperations AdminOperations => AdminImpl;

        public void BroadcastMessageToClients(string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
        {
        }

        public bool ClearPlayerReplicatedState(byte slot, string stateKey) => true;

        public void Log(string message) => _logs.Add(message);

        public void RegisterCommand(IOpenGarrisonServerCommand command)
        {
        }

        public void SendMessageToClient(byte slot, string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
        {
        }

        public bool SetPlayerReplicatedStateBool(byte slot, string stateKey, bool value) => true;

        public bool SetPlayerReplicatedStateFloat(byte slot, string stateKey, float value) => true;

        public bool SetPlayerReplicatedStateInt(byte slot, string stateKey, int value) => true;
    }

    private sealed class FakeServerReadOnlyState : IOpenGarrisonServerReadOnlyState
    {
        public string ServerName => "Test Server";
        public string LevelName => "ctf_test";
        public int MapAreaIndex => 1;
        public int MapAreaCount => 1;
        public GameModeKind GameMode => GameModeKind.CaptureTheFlag;
        public MatchPhase MatchPhase => MatchPhase.Running;
        public int RedCaps => 0;
        public int BlueCaps => 0;

        public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetAvailableGameplayLoadouts(byte slot) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetOwnedGameplayItems(byte slot)
        {
            return CharacterClassCatalog.RuntimeRegistry.ModPacks
                .SelectMany(pack => pack.Items.Values
                    .Where(item => item.Ownership?.DefaultGranted ?? true)
                    .Select(item => new OpenGarrisonServerGameplayItemInfo(
                        pack.Id,
                        item.Id,
                        item.DisplayName,
                        item.Slot,
                        item.BehaviorId,
                        item.Ownership?.TrackOwnership ?? false,
                        item.Ownership?.DefaultGranted ?? true,
                        item.Ownership?.GrantOnAcquire ?? false,
                        item.Ownership?.GrantKey)))
                .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                .Take(4)
                .ToArray();
        }

        public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplaySecondaryItems(byte slot)
        {
            return CharacterClassCatalog.RuntimeRegistry.ModPacks
                .SelectMany(pack => pack.Items.Values)
                .Where(item => item.Slot is GameplayEquipmentSlot.Primary or GameplayEquipmentSlot.Secondary)
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Take(4)
                .Select(item => new OpenGarrisonServerGameplaySelectableItemInfo(
                    item.Id,
                    item.DisplayName,
                    item.Slot,
                    item.BehaviorId,
                    IsCurrentlySelected: false,
                    IsOwnedByPlayer: item.Ownership?.DefaultGranted ?? true,
                    item.Ownership?.TrackOwnership ?? false,
                    item.Ownership?.DefaultGranted ?? true,
                    item.Ownership?.GrantOnAcquire ?? false,
                    item.Ownership?.GrantKey))
                .ToArray();
        }

        public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplayAcquiredItems(byte slot)
        {
            return CharacterClassCatalog.RuntimeRegistry.ModPacks
                .SelectMany(pack => pack.Items.Values)
                .Where(item => item.Slot == GameplayEquipmentSlot.Primary)
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Take(4)
                .Select(item => new OpenGarrisonServerGameplaySelectableItemInfo(
                    item.Id,
                    item.DisplayName,
                    item.Slot,
                    item.BehaviorId,
                    IsCurrentlySelected: false,
                    IsOwnedByPlayer: item.Ownership?.DefaultGranted ?? true,
                    item.Ownership?.TrackOwnership ?? false,
                    item.Ownership?.DefaultGranted ?? true,
                    item.Ownership?.GrantOnAcquire ?? false,
                    item.Ownership?.GrantKey))
                .ToArray();
        }

        public IReadOnlyList<OpenGarrisonServerGameplayModPackInfo> GetGameplayModPacks()
        {
            return CharacterClassCatalog.RuntimeRegistry.ModPacks
                .OrderBy(pack => pack.Id, StringComparer.Ordinal)
                .Select(pack => new OpenGarrisonServerGameplayModPackInfo(
                    pack.Id,
                    pack.DisplayName,
                    pack.Version.ToString(),
                    pack.Items.Count,
                    pack.Classes.Count,
                    string.Equals(pack.Id, StockGameplayModCatalog.Definition.Id, StringComparison.Ordinal)))
                .ToArray();
        }

        public IReadOnlyList<OpenGarrisonServerGameplayClassInfo> GetGameplayClasses(string? modPackId = null)
        {
            return CharacterClassCatalog.RuntimeRegistry.ModPacks
                .Where(pack => string.IsNullOrWhiteSpace(modPackId) || string.Equals(pack.Id, modPackId, StringComparison.Ordinal))
                .SelectMany(pack => pack.Classes.Values.Select(gameplayClass => new OpenGarrisonServerGameplayClassInfo(
                    pack.Id,
                    gameplayClass.Id,
                    gameplayClass.DisplayName,
                    gameplayClass.DefaultLoadoutId,
                    gameplayClass.Loadouts.Count)))
                .OrderBy(gameplayClass => gameplayClass.ClassId, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetGameplayItems(string? modPackId = null)
        {
            return CharacterClassCatalog.RuntimeRegistry.ModPacks
                .Where(pack => string.IsNullOrWhiteSpace(modPackId) || string.Equals(pack.Id, modPackId, StringComparison.Ordinal))
                .SelectMany(pack => pack.Items.Values.Select(item => new OpenGarrisonServerGameplayItemInfo(
                    pack.Id,
                    item.Id,
                    item.DisplayName,
                    item.Slot,
                    item.BehaviorId,
                    item.Ownership?.TrackOwnership ?? false,
                    item.Ownership?.DefaultGranted ?? true,
                    item.Ownership?.GrantOnAcquire ?? false,
                    item.Ownership?.GrantKey)))
                .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetGameplayLoadoutsForClass(string classId)
        {
            var gameplayClass = CharacterClassCatalog.RuntimeRegistry.GetRequiredClass(classId);
            return gameplayClass.Loadouts.Values
                .OrderBy(loadout => loadout.Id, StringComparer.Ordinal)
                .Select(loadout => new OpenGarrisonServerGameplayLoadoutInfo(
                    loadout.Id,
                    loadout.DisplayName,
                    loadout.PrimaryItemId,
                    loadout.SecondaryItemId,
                    loadout.UtilityItemId,
                    IsSelected: false))
                .ToArray();
        }

        public IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers() => [];

        public bool TryGetPlayerReplicatedStateBool(byte slot, string ownerPluginId, string stateKey, out bool value)
        {
            value = default;
            return false;
        }

        public bool TryGetPlayerReplicatedStateFloat(byte slot, string ownerPluginId, string stateKey, out float value)
        {
            value = default;
            return false;
        }

        public bool TryGetPlayerReplicatedStateInt(byte slot, string ownerPluginId, string stateKey, out int value)
        {
            value = default;
            return false;
        }
    }

    private sealed class FakeServerAdminOperations : IOpenGarrisonServerAdminOperations
    {
        public List<(string SelectionKind, byte Slot, string? ItemId)> GameplayItemSelections { get; } = [];

        public List<(string SelectionKind, byte Slot, string? ItemId)> GameplayItemSelectionAttempts { get; } = [];

        public List<(string ChangeKind, byte Slot, string ItemId)> GameplayOwnershipChanges { get; } = [];

        public void BroadcastSystemMessage(string text)
        {
        }

        public void SendSystemMessage(byte slot, string text)
        {
        }

        public bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false) => true;

        public bool TryDisconnect(byte slot, string reason) => true;

        public bool TryForceKill(byte slot) => true;

        public bool TryMoveToSpectator(byte slot) => true;

        public bool TrySetCapLimit(int capLimit) => true;

        public bool TrySetClass(byte slot, PlayerClass playerClass) => true;

        public bool TryGrantGameplayItem(byte slot, string itemId)
        {
            GameplayOwnershipChanges.Add(("grant", slot, itemId));
            return itemId is "weapon.scattergun" or "weapon.flamethrower";
        }

        public bool TrySetGameplayAcquiredItem(byte slot, string? itemId)
        {
            GameplayItemSelectionAttempts.Add(("acquired", slot, itemId));
            if (!string.IsNullOrWhiteSpace(itemId) && !string.Equals(itemId, "weapon.flamethrower", StringComparison.Ordinal))
            {
                return false;
            }

            GameplayItemSelections.Add(("acquired", slot, itemId));
            return true;
        }

        public bool TrySetGameplayEquippedSlot(byte slot, GameplayEquipmentSlot equippedSlot) => true;

        public bool TrySetGameplayLoadout(byte slot, string loadoutId) => true;

        public bool TryRevokeGameplayItem(byte slot, string itemId)
        {
            GameplayOwnershipChanges.Add(("revoke", slot, itemId));
            return itemId is "weapon.scattergun" or "weapon.flamethrower";
        }

        public bool TrySetGameplaySecondaryItem(byte slot, string? itemId)
        {
            GameplayItemSelectionAttempts.Add(("secondary", slot, itemId));
            if (!string.IsNullOrWhiteSpace(itemId) && !string.Equals(itemId, "weapon.scattergun", StringComparison.Ordinal))
            {
                return false;
            }

            GameplayItemSelections.Add(("secondary", slot, itemId));
            return true;
        }

        public bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1) => true;

        public bool TrySetTeam(byte slot, PlayerTeam team) => true;
    }
}
