using System.Text.Json;
using System.Text.RegularExpressions;
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
            PressedDigit: 2,
            QPressed: false));
        Assert.NotNull(pageSwitchResult);
        Assert.Equal(2, pageSwitchResult!.NewXPageIndex);
        Assert.True(pageSwitchResult.ClearBubbleSelection);

        var xDigitResult = bubbleMenuHooks.TryHandleBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.X,
            XPageIndex: 0,
            AimDirectionDegrees: 0f,
            DistanceFromCenter: 60f,
            LeftMousePressed: false,
            LeftMouseDown: false,
            LeftMouseReleased: false,
            PressedDigit: 3,
            QPressed: false));
        Assert.NotNull(xDigitResult);
        Assert.Equal(29, xDigitResult!.BubbleFrame);

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
    public void PackagedClientLuaGarrisonToolsEffectsLoadsAndAcceptsServerMessages()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadPackagedClientLuaPlugin("Lua.GarrisonToolsEffects", "open-garrison.client.lua-garrison-tools-effects", tempDirectory, logs);

        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonClientUpdateHooks>(loadedPlugin.Plugin);
        var messageHooks = Assert.IsAssignableFrom<IOpenGarrisonClientPluginMessageHooks>(loadedPlugin.Plugin);
        var hudHooks = Assert.IsAssignableFrom<IOpenGarrisonClientHudHooks>(loadedPlugin.Plugin);
        var cameraHooks = Assert.IsAssignableFrom<IOpenGarrisonClientCameraHooks>(loadedPlugin.Plugin);

        messageHooks.OnServerPluginMessage(new ClientPluginMessageEnvelope(
            "open-garrison.server.lua-garrison-tools",
            "open-garrison.client.lua-garrison-tools-effects",
            "seffect.apply",
            "blind|8.000|220|28",
            PluginMessagePayloadFormat.Text,
            1));
        messageHooks.OnServerPluginMessage(new ClientPluginMessageEnvelope(
            "open-garrison.server.lua-garrison-tools",
            "open-garrison.client.lua-garrison-tools-effects",
            "seffect.apply",
            "earthquake|6.000|12.000|18.000",
            PluginMessagePayloadFormat.Text,
            1));
        messageHooks.OnServerPluginMessage(new ClientPluginMessageEnvelope(
            "open-garrison.server.lua-garrison-tools",
            "open-garrison.client.lua-garrison-tools-effects",
            "announce.notice",
            "Server restart soon",
            PluginMessagePayloadFormat.Text,
            1));

        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 1, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));

        var canvas = new FakeHudCanvas();
        hudHooks.OnGameplayHudDraw(canvas);
        var offset = cameraHooks.GetCameraOffset();

        Assert.True(float.IsFinite(offset.X), string.Join(Environment.NewLine, logs));
        Assert.True(float.IsFinite(offset.Y), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.UiImpl.Notices, notice => notice.Text == "Server restart soon" && notice.DurationTicks == 300 && notice.PlaySound == false);

        messageHooks.OnServerPluginMessage(new ClientPluginMessageEnvelope(
            "open-garrison.server.lua-garrison-tools",
            "open-garrison.client.lua-garrison-tools-effects",
            "seffect.clear",
            "all",
            PluginMessagePayloadFormat.Text,
            1));

        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 2, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));

        var clearedCanvas = new FakeHudCanvas();
        hudHooks.OnGameplayHudDraw(clearedCanvas);

        Assert.Equal(Vector2.Zero, cameraHooks.GetCameraOffset());
        Assert.DoesNotContain(logs, log => log.Contains("disabled open-garrison.client.lua-garrison-tools-effects", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PackagedClientLuaGarrisonToolsMenuLoadsAndDrawsHud()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadPackagedClientLuaPlugin("Lua.GarrisonToolsMenu", "open-garrison.client.lua-garrison-tools-menu", tempDirectory, logs);

        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonClientUpdateHooks>(loadedPlugin.Plugin);
        var messageHooks = Assert.IsAssignableFrom<IOpenGarrisonClientPluginMessageHooks>(loadedPlugin.Plugin);
        var hudHooks = Assert.IsAssignableFrom<IOpenGarrisonClientHudHooks>(loadedPlugin.Plugin);

        Assert.Contains(loadedPlugin.Context.HotkeysImpl.RegisteredHotkeys, entry => entry.HotkeyId == "adminmenu-slot-1" && entry.DefaultKey == Keys.D1);
        Assert.Contains(loadedPlugin.Context.HotkeysImpl.RegisteredHotkeys, entry => entry.HotkeyId == "adminmenu-slot-6" && entry.DefaultKey == Keys.D6);
        Assert.Contains(loadedPlugin.Context.HotkeysImpl.RegisteredHotkeys, entry => entry.HotkeyId == "adminmenu-close" && entry.DefaultKey == Keys.Escape);

        messageHooks.OnServerPluginMessage(new ClientPluginMessageEnvelope(
            "open-garrison.server.lua-garrison-tools",
            "open-garrison.client.lua-garrison-tools-menu",
            "adminmenu.open",
            "am1|t=Admin Menu|s=Choose a branch.|b=Root|l=Server management|l=Game management|l=Player management|l=Fun|l=Close",
            PluginMessagePayloadFormat.Text,
            1));

        Assert.NotNull(loadedPlugin.Context.UiImpl.OverlayMenu);
        Assert.Equal("Admin Menu", loadedPlugin.Context.UiImpl.OverlayMenu!.Title);
        Assert.Equal("Choose a branch.", loadedPlugin.Context.UiImpl.OverlayMenu.Subtitle);
        Assert.Equal("Root", loadedPlugin.Context.UiImpl.OverlayMenu.Breadcrumb);
        Assert.Equal(5, loadedPlugin.Context.UiImpl.OverlayMenu.Entries.Count);
        Assert.True(loadedPlugin.Context.HotkeysImpl.CaptureEnabled, string.Join(Environment.NewLine, logs));

        loadedPlugin.Context.HotkeysImpl.PressedHotkeys.Add("adminmenu-slot-4");
        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 1, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));

        messageHooks.OnServerPluginMessage(new ClientPluginMessageEnvelope(
            "open-garrison.server.lua-garrison-tools",
            "open-garrison.client.lua-garrison-tools-menu",
            "adminmenu.close",
            string.Empty,
            PluginMessagePayloadFormat.Text,
            1));

        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 2, IsMainMenuOpen: false, IsGameplayActive: true, IsConnected: true, IsSpectator: false));

        Assert.Null(loadedPlugin.Context.UiImpl.OverlayMenu);
        Assert.DoesNotContain(logs, log => log.Contains("disabled open-garrison.client.lua-garrison-tools-menu", StringComparison.OrdinalIgnoreCase));
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
    public void ClientLuaHostRejectsSoundRegistrationAndUiMutationDuringHudDraw()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-draw-ui-mutation",
            "Lua Draw UI Mutation",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_gameplay_hud_draw(canvas)
                plugin.host.register_sound_asset("ding", "ding.wav")
                plugin.host.register_menu_entry("toggle-draw", "Toggle Draw", "InGameMenu", "toggle_draw")
                plugin.host.register_hotkey("draw-hotkey", "Draw Hotkey", "R")
                plugin.host.show_notice("draw notice", 60, false)
                plugin.host.list_files(".", "*.lua")
            end

            function plugin.toggle_draw()
            end

            return plugin
            """,
            tempDirectory,
            logs);

        var hudHooks = Assert.IsAssignableFrom<IOpenGarrisonClientHudHooks>(loadedPlugin.Plugin);
        hudHooks.OnGameplayHudDraw(new FakeHudCanvas());

        Assert.Empty(loadedPlugin.Context.AssetsImpl.RegisteredSounds);
        Assert.Empty(loadedPlugin.Context.UiImpl.MenuEntries);
        Assert.Empty(loadedPlugin.Context.HotkeysImpl.RegisteredHotkeys);
        Assert.Empty(loadedPlugin.Context.UiImpl.Notices);
        Assert.Contains(logs, log => log.Contains("register_sound_asset rejected", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("register_menu_entry rejected", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("register_hotkey rejected", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("show_notice rejected", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("list_files rejected", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, log => log.Contains("callback failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaHostRejectsConfigAccessDuringCameraQuery()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-query-config",
            "Lua Query Config",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.get_camera_offset()
                plugin.host.load_json_config("query-config.json", { enabled = true })
                plugin.host.save_json_config("query-config.json", { enabled = false })
                return plugin.host.vec2(4.0, -3.0)
            end

            return plugin
            """,
            tempDirectory,
            logs);

        var cameraHooks = Assert.IsAssignableFrom<IOpenGarrisonClientCameraHooks>(loadedPlugin.Plugin);
        Assert.Equal(new Vector2(4f, -3f), cameraHooks.GetCameraOffset());

        Assert.False(File.Exists(Path.Combine(loadedPlugin.ConfigDirectory, "query-config.json")));
        Assert.Contains(logs, log => log.Contains("load_json_config rejected", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("save_json_config rejected", StringComparison.Ordinal));
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
    public void ClientLuaHostReadsReplicatedAbilityState()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ClientLuaReplicatedAbilityState");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.client.lua-replicated-ability-state",
              "displayName": "Lua Client Replicated Ability State",
              "version": "1.0.0",
              "type": "Client",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), """
            local plugin = {}

            function plugin.initialize(host)
                local cooldown = host.get_player_replicated_state_int(1, "core.ability", "heavy_dash_cooldown_ticks")
                local charge = host.get_player_replicated_state_float(1, "core.ability", "medic_uber_charge")
                local ready = host.get_player_replicated_state_bool(1, "core.ability", "medic_uber_ready")
                local missing = host.get_player_replicated_state_int(1, "core.ability", "missing_key")
                host.save_json_config("replicated-ability-state.json", {
                    cooldown = cooldown,
                    charge = charge,
                    ready = ready,
                    missing_is_nil = missing == nil
                })
            end

            return plugin
            """);

        var logs = new List<string>();
        var configDirectory = tempDirectory.CreateSubdirectory("ClientConfig");
        Assert.True(
            OpenGarrisonPluginManifestLoader.TryLoadFromPath(Path.Combine(pluginDirectory, "plugin.json"), out var manifest, out var manifestError),
            manifestError);

        var fakeContext = new FakeClientPluginContext(manifest, pluginDirectory, configDirectory, logs);
        fakeContext.StateImpl.IntReplicatedStates[(1, "core.ability", "heavy_dash_cooldown_ticks")] = 12;
        fakeContext.StateImpl.FloatReplicatedStates[(1, "core.ability", "medic_uber_charge")] = 0.75f;
        fakeContext.StateImpl.BoolReplicatedStates[(1, "core.ability", "medic_uber_ready")] = true;

        var discoveredPlugins = ClientPluginLoader.DiscoverFromDirectory(tempDirectory.RootPath, logs.Add);
        var discoveredPlugin = Assert.Single(discoveredPlugins);
        var loadedPlugin = ClientPluginLoader.TryLoadDiscoveredPlugin(
            discoveredPlugin,
            (_, _, _) => fakeContext,
            logs.Add);

        Assert.NotNull(loadedPlugin);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(configDirectory, "replicated-ability-state.json")));
        Assert.Equal(12, json.RootElement.GetProperty("cooldown").GetInt32());
        Assert.Equal(0.75, json.RootElement.GetProperty("charge").GetDouble(), precision: 3);
        Assert.True(json.RootElement.GetProperty("ready").GetBoolean());
        Assert.True(json.RootElement.GetProperty("missing_is_nil").GetBoolean());
    }

    [Fact]
    public void ClientLuaHostRegistersStructuredHudAndScoreboardUi()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-structured-ui",
            "Lua Client Structured UI",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
                host.register_hud_widget({
                    id = "status",
                    anchor = "top_right",
                    order = 10,
                    draw = function(canvas, widget)
                        canvas.draw_bitmap_text("widget " .. widget.anchor, 8, 10, host.color(255, 255, 255, 255))
                    end
                })
                host.register_scoreboard_panel({
                    id = "summary",
                    location = "HeaderRight",
                    order = -5,
                    draw = function(canvas, state, panel)
                        canvas.draw_bitmap_text_right_aligned("panel " .. panel.location, 100, 20, host.color(255, 255, 255, 255))
                    end
                })
                host.register_scoreboard_player_action({
                    id = "inspect",
                    label = "Inspect",
                    order = 3,
                    activate = function(player)
                        host.save_json_config("scoreboard-action.json", {
                            playerName = player.playerName,
                            slot = player.slot
                        })
                    end
                })
                host.register_chat_filter({
                    id = "suffix",
                    order = 1,
                    handler = function(chat)
                        return {
                            text = chat.text .. " filtered",
                            teamOnly = true
                        }
                    end
                })
                host.register_chat_command({
                    name = "localtest",
                    aliases = { "lt" },
                    handler = function(command)
                        host.save_json_config("chat-command.json", {
                            name = command.name,
                            arguments = command.arguments
                        })
                    end
                })
                host.show_prompt({
                    title = "Prompt",
                    message = "Choose",
                    options = { "A", "B" }
                })
                host.show_overlay_panel({
                    title = "Panel",
                    subtitle = "Controls",
                    controls = {
                        { id = "apply", label = "Apply", kind = "Button" },
                        { id = "enabled", label = "Enabled", kind = "Toggle", value = "true" }
                    }
                })
            end

            return plugin
            """,
            tempDirectory,
            logs);

        Assert.NotNull(loadedPlugin.Context.UiImpl.OverlayMenu);
        Assert.Equal("Prompt", loadedPlugin.Context.UiImpl.OverlayMenu!.Title);
        Assert.Equal("Choose", loadedPlugin.Context.UiImpl.OverlayMenu.Subtitle);
        Assert.Equal(["A", "B"], loadedPlugin.Context.UiImpl.OverlayMenu.Entries);
        Assert.NotNull(loadedPlugin.Context.UiImpl.OverlayPanel);
        Assert.Equal("Panel", loadedPlugin.Context.UiImpl.OverlayPanel!.Title);
        Assert.Equal(2, loadedPlugin.Context.UiImpl.OverlayPanel.Controls.Count);

        var hudHooks = Assert.IsAssignableFrom<IOpenGarrisonClientHudHooks>(loadedPlugin.Plugin);
        var hudCanvas = new FakeHudCanvas();
        hudHooks.OnGameplayHudDraw(hudCanvas);
        Assert.Equal(1, hudCanvas.BitmapTextDrawCount);

        var scoreboardHooks = Assert.IsAssignableFrom<IOpenGarrisonClientScoreboardHooks>(loadedPlugin.Plugin);
        Assert.Equal(ClientScoreboardPanelLocation.HeaderRight, scoreboardHooks.ScoreboardPanelLocation);
        Assert.Equal(-5, scoreboardHooks.ScoreboardPanelOrder);

        var scoreboardCanvas = new FakeHudCanvas();
        scoreboardHooks.OnScoreboardDraw(
            scoreboardCanvas,
            new ClientScoreboardRenderState(
                new Rectangle(0, 0, 320, 160),
                1f,
                "Test Server",
                "ctf_test",
                RedPlayerCount: 1,
                BluePlayerCount: 2,
                RedCenterText: "RED",
                BlueCenterText: "BLU"));
        Assert.Equal(1, scoreboardCanvas.BitmapTextDrawCount);

        var scoreboardActionHooks = Assert.IsAssignableFrom<IOpenGarrisonClientScoreboardPlayerActionHooks>(loadedPlugin.Plugin);
        var action = Assert.Single(scoreboardActionHooks.GetScoreboardPlayerActions(new ClientScoreboardPlayerActionContext(
            4,
            44,
            "Target",
            ClientPluginTeam.Blue,
            ClientPluginClass.Soldier,
            IsLocalPlayer: false)));
        Assert.Equal("Inspect", action.Label);
        action.Activate(new ClientScoreboardPlayerActionContext(
            4,
            44,
            "Target",
            ClientPluginTeam.Blue,
            ClientPluginClass.Soldier,
            IsLocalPlayer: false));

        var filteredChat = Assert.IsAssignableFrom<IOpenGarrisonClientChatHooks>(loadedPlugin.Plugin)
            .BeforeChatSubmit(new ClientChatSubmitContext("hello", TeamOnly: false));
        Assert.Equal("hello filtered", filteredChat.Text);
        Assert.True(filteredChat.TeamOnly);

        Assert.True(Assert.IsAssignableFrom<IOpenGarrisonClientChatCommandHooks>(loadedPlugin.Plugin)
            .TryHandleChatCommand(new ClientChatSubmitContext("!lt one two", TeamOnly: false)));
        using var scoreboardActionJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(loadedPlugin.ConfigDirectory, "scoreboard-action.json")));
        Assert.Equal("Target", scoreboardActionJson.RootElement.GetProperty("playerName").GetString());
        Assert.Equal(4, scoreboardActionJson.RootElement.GetProperty("slot").GetInt32());
        using var chatCommandJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(loadedPlugin.ConfigDirectory, "chat-command.json")));
        Assert.Equal("lt", chatCommandJson.RootElement.GetProperty("name").GetString());
        Assert.Equal("one two", chatCommandJson.RootElement.GetProperty("arguments").GetString());
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientLuaHostRegistersGameplayAbilityScopedHudWidget()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadAdHocClientLuaPlugin(
            "tests.client.lua-ability-hud-widget",
            "Lua Client Ability HUD Widget",
            """
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
                host.register_gameplay_ability_hud_widget({
                    itemId = "ability.plugin-dash",
                    id = "plugin-dash-hud",
                    anchor = "bottom_right",
                    order = 25,
                    draw = function(canvas, ability)
                        canvas.draw_bitmap_text("ability " .. ability.itemId, 8, 10, host.color(255, 255, 255, 255))
                    end
                })
            end

            return plugin
            """,
            tempDirectory,
            logs);

        var hudHooks = Assert.IsAssignableFrom<IOpenGarrisonClientHudHooks>(loadedPlugin.Plugin);
        var hiddenCanvas = new FakeHudCanvas();
        hudHooks.OnGameplayHudDraw(hiddenCanvas);
        Assert.Equal(0, hiddenCanvas.BitmapTextDrawCount);

        loadedPlugin.Context.StateImpl.LocalGameplayItemIds.Add("ability.plugin-dash");
        var visibleCanvas = new FakeHudCanvas();
        hudHooks.OnGameplayHudDraw(visibleCanvas);
        Assert.Equal(1, visibleCanvas.BitmapTextDrawCount);
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
            new OpenGarrisonServerChatMessageContext(
                fakeContext.ServerState,
                fakeContext.AdminOperations,
                fakeContext.Cvars,
                fakeContext.Scheduler,
                OpenGarrisonServerAdminIdentity.CreateUnauthenticated(1)),
            new ChatReceivedEvent(1, "Tester", "!lua", Team: null, TeamOnly: false));
        Assert.True(handled);

        var configPath = Path.Combine(configDirectory, "server-smoke.json");
        Assert.True(File.Exists(configPath));
        var json = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(15, json.RootElement.GetProperty("count").GetInt32());
        Assert.Contains(logs, log => log.Contains("server initialized", StringComparison.Ordinal));
    }

    [Fact]
    public void ServerLuaHostRegistersCommandsThroughCommandRegistryWithPermissions()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaCommandApi");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-command-api",
              "displayName": "Lua Server Command API",
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
                host.register_command({
                    name = "lua_rename",
                    aliases = { "lr" },
                    usage = "lua_rename <name>",
                    description = "Rename test target.",
                    permission = "manage_players",
                    handler = function(context, arguments)
                        context.require_permission("ManagePlayers")
                        host.try_set_player_name(2, arguments)
                        return {
                            "renamed " .. arguments,
                            "source " .. context.source,
                            "slot " .. tostring(context.slot),
                            "sourceSlot " .. tostring(context.sourceSlot),
                            "source_slot " .. tostring(context.source_slot)
                        }
                    end
                })
            end

            return plugin
            """);

        var logs = new List<string>();
        Assert.True(
            OpenGarrisonPluginManifestLoader.TryLoadFromPath(Path.Combine(pluginDirectory, "plugin.json"), out var manifest, out var manifestError),
            manifestError);

        var fakeContext = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            tempDirectory.CreateSubdirectory("ServerConfig"),
            tempDirectory.CreateSubdirectory("Maps"),
            logs);

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, _, _) => fakeContext,
            logs.Add);

        Assert.True(loadedPlugins.Count == 1, string.Join(Environment.NewLine, logs));
        var registeredCommand = Assert.Single(fakeContext.RegisteredCommands);
        Assert.Equal("lua_rename", registeredCommand.Command.Name);
        Assert.Equal(OpenGarrisonServerAdminPermissions.ManagePlayers, registeredCommand.RequiredPermissions);
        Assert.Contains("lr", registeredCommand.Aliases);

        var unauthorizedContext = CreateCommandContext(fakeContext, OpenGarrisonServerAdminIdentity.CreateUnauthenticated(1));
        Assert.True(fakeContext.CommandRegistry.TryExecute("!lua_rename Blocked", unauthorizedContext, CancellationToken.None, out var unauthorizedLines));
        Assert.Contains("requires ManagePlayers", Assert.Single(unauthorizedLines), StringComparison.Ordinal);
        Assert.Empty(fakeContext.AdminImpl.RenameRequests);

        var authorizedContext = CreateCommandContext(
            fakeContext,
            new OpenGarrisonServerAdminIdentity(
                "Test Admin",
                OpenGarrisonServerAdminAuthority.RconSession,
                OpenGarrisonServerAdminPermissions.ManagePlayers,
                SourceSlot: 1));
        Assert.True(fakeContext.CommandRegistry.TryExecute("/lr Renamed", authorizedContext, CancellationToken.None, out var authorizedLines));
        Assert.Contains("renamed Renamed", authorizedLines);
        Assert.Contains("source PrivateChat", authorizedLines);
        Assert.Contains("slot 1", authorizedLines);
        Assert.Contains("sourceSlot 1", authorizedLines);
        Assert.Contains("source_slot 1", authorizedLines);
        Assert.Contains(fakeContext.AdminImpl.RenameRequests, request => request.Slot == 2 && request.NewName == "Renamed");
    }

    [Fact]
    public void ServerLuaDecisionHooksCanCancelSupportedMutations()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaBeforeChat");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-before-chat",
              "displayName": "Lua Server Before Chat",
              "version": "1.0.0",
              "type": "Server",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), """
            local plugin = {}

            function plugin.before_chat_message(e)
                if e.text == "blocked" then
                    return { cancel = true, reason = "blocked by lua" }
                end

                return { cancel = false }
            end

            function plugin.before_team_change(e)
                if e.team == "Blue" then
                    return "blue team blocked"
                end
            end

            function plugin.before_class_change(e)
                return e.playerClass == "Spy"
            end

            function plugin.before_loadout_change(e)
                if e.loadoutId == "restricted" then
                    return { cancelled = true, reason = "restricted loadout" }
                end
            end

            function plugin.before_map_change(e)
                if e.levelName == "ctf_blocked" then
                    return { cancel = true }
                end
            end

            function plugin.before_spawn(e)
                return e.playerName == "Blocked"
            end

            function plugin.before_damage(e)
                if e.targetPlayerId == 99 then
                    return { cancel = true, reason = "protected target" }
                end
            end

            function plugin.before_death(e)
                return e.victimName == "Immortal"
            end

            function plugin.before_pickup(e)
                return e.kind == "HealthPack" and e.pickupValue == "Large"
            end

            function plugin.before_score(e)
                return e.reason == "blocked_score"
            end

            function plugin.before_round_end(e)
                return e.reason == "blocked_round"
            end

            return plugin
            """);

        var logs = new List<string>();
        Assert.True(
            OpenGarrisonPluginManifestLoader.TryLoadFromPath(Path.Combine(pluginDirectory, "plugin.json"), out var manifest, out var manifestError),
            manifestError);

        var fakeContext = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            tempDirectory.CreateSubdirectory("ServerConfig"),
            tempDirectory.CreateSubdirectory("Maps"),
            logs);

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, _, _) => fakeContext,
            logs.Add);

        var loadedPlugin = Assert.Single(loadedPlugins);
        var decisionHooks = Assert.IsAssignableFrom<IOpenGarrisonServerDecisionHooks>(loadedPlugin.Plugin);

        var blockedDecision = decisionHooks.BeforeChatMessage(new ChatReceivedEvent(1, "Tester", "blocked", Team: null, TeamOnly: false));
        Assert.True(blockedDecision.IsCancelled);
        Assert.Equal("blocked by lua", blockedDecision.Reason);

        var allowedDecision = decisionHooks.BeforeChatMessage(new ChatReceivedEvent(1, "Tester", "allowed", Team: null, TeamOnly: false));
        Assert.False(allowedDecision.IsCancelled);

        var blockedTeamDecision = decisionHooks.BeforeTeamChange(new OpenGarrisonServerTeamChangeRequest(1, PlayerTeam.Blue));
        Assert.True(blockedTeamDecision.IsCancelled);
        Assert.Equal("blue team blocked", blockedTeamDecision.Reason);
        Assert.False(decisionHooks.BeforeTeamChange(new OpenGarrisonServerTeamChangeRequest(1, PlayerTeam.Red)).IsCancelled);

        Assert.True(decisionHooks.BeforeClassChange(new OpenGarrisonServerClassChangeRequest(1, PlayerClass.Spy)).IsCancelled);
        Assert.False(decisionHooks.BeforeClassChange(new OpenGarrisonServerClassChangeRequest(1, PlayerClass.Scout)).IsCancelled);

        var loadoutDecision = decisionHooks.BeforeLoadoutChange(new OpenGarrisonServerLoadoutChangeRequest(1, "restricted"));
        Assert.True(loadoutDecision.IsCancelled);
        Assert.Equal("restricted loadout", loadoutDecision.Reason);

        Assert.True(decisionHooks.BeforeMapChange(new OpenGarrisonServerMapChangeRequest("ctf_blocked", 1, PreservePlayerStats: false)).IsCancelled);
        Assert.False(decisionHooks.BeforeMapChange(new OpenGarrisonServerMapChangeRequest("ctf_allowed", 1, PreservePlayerStats: false)).IsCancelled);

        Assert.True(decisionHooks.BeforeSpawn(new OpenGarrisonServerSpawnRequest(1, 2, 22, "Blocked", PlayerTeam.Red, PlayerClass.Scout, 10f, 20f, IsRespawn: true)).IsCancelled);
        Assert.False(decisionHooks.BeforeSpawn(new OpenGarrisonServerSpawnRequest(1, 2, 22, "Allowed", PlayerTeam.Red, PlayerClass.Scout, 10f, 20f, IsRespawn: true)).IsCancelled);

        var damageDecision = decisionHooks.BeforeDamage(new OpenGarrisonServerDamageRequest(
            2,
            OpenGarrison.Core.DamageTargetKind.Player,
            99,
            99,
            PlayerTeam.Blue,
            22,
            PlayerTeam.Red,
            50,
            false,
            10f,
            20f));
        Assert.True(damageDecision.IsCancelled);
        Assert.Equal("protected target", damageDecision.Reason);
        Assert.False(decisionHooks.BeforeDeath(new OpenGarrisonServerDeathRequest(3, 2, 22, "Mortal", PlayerTeam.Red, PlayerClass.Scout, 99, "Killer", PlayerTeam.Blue, "weapon", Gibbed: false)).IsCancelled);
        Assert.True(decisionHooks.BeforeDeath(new OpenGarrisonServerDeathRequest(3, 2, 22, "Immortal", PlayerTeam.Red, PlayerClass.Scout, 99, "Killer", PlayerTeam.Blue, "weapon", Gibbed: false)).IsCancelled);
        Assert.True(decisionHooks.BeforePickup(new OpenGarrisonServerPickupRequest(4, "HealthPack", 2, 22, "Runner", PlayerTeam.Red, 10, "Large", 100f, 120f)).IsCancelled);
        Assert.False(decisionHooks.BeforePickup(new OpenGarrisonServerPickupRequest(4, "DroppedWeapon", 2, 22, "Runner", PlayerTeam.Red, 10, "Large", 100f, 120f)).IsCancelled);
        Assert.True(decisionHooks.BeforeScore(new OpenGarrisonServerScoreRequest(5, PlayerTeam.Red, Delta: 1, RedCaps: 0, BlueCaps: 0, ActorPlayerId: 22, Reason: "blocked_score")).IsCancelled);
        Assert.False(decisionHooks.BeforeScore(new OpenGarrisonServerScoreRequest(5, PlayerTeam.Red, Delta: 1, RedCaps: 0, BlueCaps: 0, ActorPlayerId: 22, Reason: "allowed_score")).IsCancelled);
        Assert.True(decisionHooks.BeforeRoundEnd(new OpenGarrisonServerRoundEndRequest(6, GameModeKind.CaptureTheFlag, WinnerTeam: PlayerTeam.Red, RedCaps: 2, BlueCaps: 1, Reason: "blocked_round")).IsCancelled);
        Assert.False(decisionHooks.BeforeRoundEnd(new OpenGarrisonServerRoundEndRequest(6, GameModeKind.CaptureTheFlag, WinnerTeam: PlayerTeam.Red, RedCaps: 2, BlueCaps: 1, Reason: "allowed_round")).IsCancelled);
    }

    [Fact]
    public void ServerLuaHostExposesStableReadModelQueries()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaReadModel");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-read-model",
              "displayName": "Lua Server Read Model",
              "version": "1.0.0",
              "type": "Server",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), """
            local plugin = {}

            local function list_count(value)
                if value == nil then
                    return 0
                end
                if value.length ~= nil then
                    return value.length
                end
                if value.Length ~= nil then
                    return value.Length
                end

                local count = 0
                for _, _ in pairs(value) do
                    count = count + 1
                end
                return count
            end

            function plugin.initialize(host)
                local match = host.get_match_state()
                local slot_player = host.get_player_state(1)
                local id_player = host.get_player_state({ playerId = 22 })
                local kind_player = host.get_player_state({ by = "player_id", value = 22 })
                local missing_player = host.get_player_state({ slot = 99 })
                local objectives = host.get_objectives()
                local buildables = host.get_buildables()
                local projectiles = host.get_projectiles()
                local recent_events = host.get_recent_events()
                local map_region = host.get_map_region({ x = 100, y = 200, radius = 64, limit = 4 })
                local visibility = host.has_line_of_sight({
                    originX = 1,
                    originY = 2,
                    targetX = 3,
                    targetY = 4,
                    team = "Red"
                })

                host.save_json_config("read-model.json", {
                    levelName = match.levelName,
                    playerCount = match.playerCount,
                    activePlayerCount = match.activePlayerCount,
                    spectatorCount = match.spectatorCount,
                    slotPlayerName = slot_player.name,
                    slotPlayerHealth = slot_player.health,
                    idPlayerName = id_player.name,
                    kindPlayerName = kind_player.name,
                    missingPlayerIsNil = missing_player == nil,
                    controlPointCount = list_count(objectives.controlPoints),
                    buildableCount = list_count(buildables),
                    projectileCount = list_count(projectiles),
                    recentEventCount = list_count(recent_events),
                    mapRegionRadius = map_region.radius,
                    mapSolidCount = list_count(map_region.solids),
                    hasLineOfSight = visibility.hasLineOfSight,
                    visibilityTeam = visibility.team
                })
            end

            return plugin
            """);

        var logs = new List<string>();
        Assert.True(
            OpenGarrisonPluginManifestLoader.TryLoadFromPath(Path.Combine(pluginDirectory, "plugin.json"), out var manifest, out var manifestError),
            manifestError);

        var configDirectory = tempDirectory.CreateSubdirectory("ServerConfig");
        var fakeContext = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            configDirectory,
            tempDirectory.CreateSubdirectory("Maps"),
            logs);
        fakeContext.StateImpl.Players.Add(new OpenGarrisonServerPlayerInfo(
            1,
            101,
            "Runner",
            IsSpectator: false,
            IsAuthorized: false,
            IsGagged: false,
            IsAlive: true,
            PlayerId: 11,
            Team: PlayerTeam.Red,
            PlayerClass: PlayerClass.Scout,
            PlayerScale: 1f,
            EndPoint: "127.0.0.1:8190",
            GameplayLoadoutId: "stock",
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: "weapon.scattergun",
            Health: 125,
            MaxHealth: 125));
        fakeContext.StateImpl.Players.Add(new OpenGarrisonServerPlayerInfo(
            2,
            202,
            "Rocketman",
            IsSpectator: false,
            IsAuthorized: false,
            IsGagged: false,
            IsAlive: true,
            PlayerId: 22,
            Team: PlayerTeam.Blue,
            PlayerClass: PlayerClass.Soldier,
            PlayerScale: 1f,
            EndPoint: "127.0.0.1:8191",
            GameplayLoadoutId: "stock",
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: "weapon.rocketlauncher"));
        fakeContext.StateImpl.Players.Add(new OpenGarrisonServerPlayerInfo(
            3,
            303,
            "Spectator",
            IsSpectator: true,
            IsAuthorized: false,
            IsGagged: false,
            IsAlive: false,
            PlayerId: null,
            Team: null,
            PlayerClass: null,
            PlayerScale: 1f,
            EndPoint: "127.0.0.1:8192",
            GameplayLoadoutId: string.Empty,
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: string.Empty));
        fakeContext.StateImpl.Objectives = new OpenGarrisonServerObjectiveStateInfo(
            [new OpenGarrisonServerControlPointInfo(0, 100f, 200f, 42f, 42f, PlayerTeam.Red, null, 0f, 120, 0, 0, 0, IsLocked: false, HasHealingAura: true)],
            [],
            []);
        fakeContext.StateImpl.Buildables.Add(new OpenGarrisonServerBuildableInfo(
            "sentry",
            500,
            22,
            PlayerTeam.Blue,
            128f,
            256f,
            100,
            100,
            IsBuilt: true,
            IsDead: false,
            HasLanded: true,
            HasActiveTarget: false));
        fakeContext.StateImpl.Projectiles.Add(new OpenGarrisonServerProjectileInfo(
            "rocket",
            600,
            22,
            PlayerTeam.Blue,
            140f,
            260f,
            130f,
            260f,
            VelocityX: null,
            VelocityY: null,
            DirectionRadians: 0f,
            Speed: 12f,
            TicksRemaining: 120,
            IsCritical: false,
            IsDestroyed: false));
        fakeContext.StateImpl.RecentEvents.Add(new OpenGarrisonServerRecentEventInfo(
            "damage",
            "Player",
            EventId: 700,
            SourceFrame: 10,
            WorldX: 140f,
            WorldY: 260f,
            Amount: 30,
            TargetEntityId: 11,
            TargetPlayerId: 11,
            AttackerPlayerId: 22,
            WasFatal: false));

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, _, _) => fakeContext,
            logs.Add);

        Assert.True(loadedPlugins.Count == 1, string.Join(Environment.NewLine, logs));

        var configPath = Path.Combine(configDirectory, "read-model.json");
        Assert.True(File.Exists(configPath));
        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal("ctf_test", json.RootElement.GetProperty("levelName").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("playerCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("activePlayerCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("spectatorCount").GetInt32());
        Assert.Equal("Runner", json.RootElement.GetProperty("slotPlayerName").GetString());
        Assert.Equal(125, json.RootElement.GetProperty("slotPlayerHealth").GetInt32());
        Assert.Equal("Rocketman", json.RootElement.GetProperty("idPlayerName").GetString());
        Assert.Equal("Rocketman", json.RootElement.GetProperty("kindPlayerName").GetString());
        Assert.True(json.RootElement.GetProperty("missingPlayerIsNil").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("controlPointCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("buildableCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("projectileCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("recentEventCount").GetInt32());
        Assert.Equal(64, json.RootElement.GetProperty("mapRegionRadius").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("mapSolidCount").GetInt32());
        Assert.True(json.RootElement.GetProperty("hasLineOfSight").GetBoolean());
        Assert.Equal("Red", json.RootElement.GetProperty("visibilityTeam").GetString());
    }

    [Fact]
    public void ServerLuaHostRunsDeferredActionsOnHeartbeat()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaDeferredActions");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-deferred-actions",
              "displayName": "Lua Server Deferred Actions",
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

            function plugin.on_chat_received(e)
                plugin.host.enqueue_action({
                    type = "send_system_message",
                    slot = e.slot,
                    text = "queued " .. e.text
                })
                plugin.host.enqueue_action("try_set_player_name", {
                    slot = e.slot,
                    name = "Deferred " .. e.slot
                })
            end

            return plugin
            """);

        var logs = new List<string>();
        Assert.True(
            OpenGarrisonPluginManifestLoader.TryLoadFromPath(Path.Combine(pluginDirectory, "plugin.json"), out var manifest, out var manifestError),
            manifestError);

        var fakeContext = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            tempDirectory.CreateSubdirectory("ServerConfig"),
            tempDirectory.CreateSubdirectory("Maps"),
            logs);

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, _, _) => fakeContext,
            logs.Add);

        var loadedPlugin = Assert.Single(loadedPlugins);
        var chatHooks = Assert.IsAssignableFrom<IOpenGarrisonServerChatHooks>(loadedPlugin.Plugin);
        chatHooks.OnChatReceived(new ChatReceivedEvent(1, "Tester", "hello", Team: null, TeamOnly: false));

        Assert.Empty(fakeContext.AdminImpl.SystemMessages);
        Assert.Empty(fakeContext.AdminImpl.RenameRequests);

        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonServerUpdateHooks>(loadedPlugin.Plugin);
        updateHooks.OnServerHeartbeat(TimeSpan.FromSeconds(5));

        Assert.Contains(fakeContext.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text == "queued hello");
        Assert.Contains(fakeContext.AdminImpl.RenameRequests, request => request.Slot == 1 && request.NewName == "Deferred 1");
    }

    [Fact]
    public void ServerLuaHostExposesAbilityCatalogStateAndStartupRegistration()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaAbilityApi");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-ability-api",
              "displayName": "Lua Server Ability API",
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
                local abilities = host.get_gameplay_abilities()
                local player_abilities = host.get_player_gameplay_abilities(10)
                local utility = host.get_player_gameplay_ability(10, "utility")
                local cooldown = host.get_player_replicated_state_int(10, "core.ability", "heavy_dash_cooldown_ticks")
                local ready = host.get_player_replicated_state_bool(10, "core.ability", "medic_uber_ready")
                local executor_id = "plugin.tests.server.lua-ability-api.dash"
                local executor_registered = host.register_gameplay_ability_executor(executor_id)
                local registered = host.register_gameplay_ability({
                    itemId = "ability.plugin-dash",
                    displayName = "Plugin Dash",
                    slot = "Utility",
                    behaviorId = executor_id,
                    ability = {
                        category = "utility",
                        activation = "pressed",
                        executorId = executor_id,
                        tags = { "movement", "dash" },
                        parameters = { duration = 0.5 }
                    },
                    presentation = {
                        hud = {
                            displayKind = "meter",
                            stackGroup = "ability",
                            stateProvider = "abilityCooldown",
                            stateOwner = "tests.server.lua-ability-api",
                            cooldownKey = "plugin_dash_cooldown",
                            maxCooldown = 42,
                            activeKey = "plugin_dash_active",
                            disabledKey = "plugin_dash_disabled",
                            widgetId = "plugin-dash-widget",
                            widgetOwner = "tests.client.lua-ability-api",
                            widgetCallback = "draw_plugin_dash",
                            anchor = "bottom_right"
                        }
                    }
                })
                local overridden = host.override_gameplay_ability("ability.heavy-utility", {
                    activation = "held",
                    tags = { "patched" },
                    parameters = { cooldown = 9 }
                })
                local loadout_registered = host.register_gameplay_loadout({
                    classId = "heavy",
                    loadoutId = "heavy.plugin-dash",
                    displayName = "Plugin Dash",
                    primaryItemId = "weapon.minigun",
                    secondaryItemId = "ability.heavy-sandvich",
                    utilityItemId = "ability.plugin-dash"
                })
                local slot_item_registered = host.register_gameplay_slot_item({
                    classId = "spy",
                    slot = "Utility",
                    itemId = "ability.plugin-dash",
                    loadoutId = "spy.plugin-dash",
                    displayName = "Plugin Dash",
                    baseLoadoutId = "spy.stock"
                })

                host.save_json_config("ability-api.json", {
                    ability_count = abilities[1] ~= nil and 1 or 0,
                    player_ability_count = player_abilities[1] ~= nil and 1 or 0,
                    utility_item_id = utility.itemId,
                    cooldown = cooldown,
                    ready = ready,
                    executor_registered = executor_registered,
                    registered = registered,
                    overridden = overridden,
                    loadout_registered = loadout_registered,
                    slot_item_registered = slot_item_registered
                })
            end

            function plugin.on_gameplay_ability_execute(e)
                local projectile_id = plugin.host.try_spawn_gameplay_projectile({
                    ownerPlayerId = e.playerId,
                    kind = "rocket",
                    x = e.sourceX,
                    y = e.sourceY,
                    speed = 4,
                    directionRadians = 0
                })
                plugin.host.try_apply_gameplay_impulse(e.playerId, 12, -4)
                plugin.host.try_set_gameplay_ability_cooldown(e.playerId, "plugin_dash_cooldown", 42)
                plugin.host.try_apply_gameplay_damage(e.playerId, 3, e.playerId, "DashKL")
                plugin.host.try_apply_gameplay_healing(e.playerId, 5)
                plugin.host.try_apply_gameplay_status_effect(e.playerId, "movement_boost", 9, 1.5)
                plugin.host.save_json_config("ability-execute.json", {
                    item_id = e.itemId,
                    executor_id = e.executorId,
                    parameter_duration = e.parameters.duration,
                    projectile_id = projectile_id
                })
                return { handled = true, consumedInput = true }
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
        fakeContext.StateImpl.Abilities.Add(new OpenGarrisonServerGameplayAbilityInfo(
            "stock.gg2",
            "ability.heavy-utility",
            "Heavy Utility",
            GameplayEquipmentSlot.Utility,
            BuiltInGameplayBehaviorIds.HeavyUtility,
            GameplayAbilityConstants.UtilityCategory,
            GameplayAbilityConstants.PressedActivation,
            BuiltInGameplayBehaviorIds.HeavyGhostDash,
            ["dash"],
            new Dictionary<string, string>(StringComparer.Ordinal)));
        fakeContext.StateImpl.IntReplicatedStates[(10, "core.ability", "heavy_dash_cooldown_ticks")] = 18;
        fakeContext.StateImpl.BoolReplicatedStates[(10, "core.ability", "medic_uber_ready")] = true;

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, _, _) => fakeContext,
            logs.Add);

        Assert.Single(loadedPlugins);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(configDirectory, "ability-api.json")));
        Assert.Equal(1, json.RootElement.GetProperty("ability_count").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("player_ability_count").GetInt32());
        Assert.Equal("ability.heavy-utility", json.RootElement.GetProperty("utility_item_id").GetString());
        Assert.Equal(18, json.RootElement.GetProperty("cooldown").GetInt32());
        Assert.True(json.RootElement.GetProperty("ready").GetBoolean());
        Assert.True(json.RootElement.GetProperty("executor_registered").GetBoolean());
        Assert.True(json.RootElement.GetProperty("registered").GetBoolean());
        Assert.True(json.RootElement.GetProperty("overridden").GetBoolean());
        Assert.True(json.RootElement.GetProperty("loadout_registered").GetBoolean());
        Assert.True(json.RootElement.GetProperty("slot_item_registered").GetBoolean());

        var registeredExecutor = Assert.Single(fakeContext.RegisteredGameplayAbilityExecutors);
        var registeredAbility = Assert.Single(fakeContext.RegisteredGameplayAbilities);
        Assert.Equal("ability.plugin-dash", registeredAbility.ItemId);
        Assert.Equal("utility", registeredAbility.Ability.Category);
        Assert.Contains("dash", registeredAbility.Ability.Tags);
        Assert.Equal(0.5, registeredAbility.Ability.Parameters["duration"].GetDouble(), precision: 3);
        var registeredAbilityHud = Assert.IsType<GameplayItemHudPresentationDefinition>(registeredAbility.Presentation?.Hud);
        Assert.Equal("tests.server.lua-ability-api", registeredAbilityHud.StateOwner);
        Assert.Equal("plugin_dash_cooldown", registeredAbilityHud.CooldownKey);
        Assert.Equal(42, registeredAbilityHud.MaxCooldown);
        Assert.Equal("plugin_dash_active", registeredAbilityHud.ActiveKey);
        Assert.Equal("plugin_dash_disabled", registeredAbilityHud.DisabledKey);
        Assert.Equal("plugin-dash-widget", registeredAbilityHud.WidgetId);
        Assert.Equal("tests.client.lua-ability-api", registeredAbilityHud.WidgetOwner);
        Assert.Equal("draw_plugin_dash", registeredAbilityHud.WidgetCallback);
        Assert.Equal("bottom_right", registeredAbilityHud.Anchor);

        var overrideEntry = Assert.Single(fakeContext.GameplayAbilityOverrides);
        Assert.Equal("ability.heavy-utility", overrideEntry.ItemId);
        Assert.Equal("held", overrideEntry.Patch.Activation);
        Assert.Contains("patched", overrideEntry.Patch.Tags!);
        Assert.Equal(9, overrideEntry.Patch.Parameters!["cooldown"].GetInt32());
        var registeredLoadout = Assert.Single(fakeContext.RegisteredGameplayLoadouts);
        Assert.Equal("heavy", registeredLoadout.ClassId);
        Assert.Equal("heavy.plugin-dash", registeredLoadout.LoadoutId);
        Assert.Equal("ability.plugin-dash", registeredLoadout.UtilityItemId);
        var registeredSlotItem = Assert.Single(fakeContext.RegisteredGameplaySlotItems);
        Assert.Equal("spy", registeredSlotItem.ClassId);
        Assert.Equal(GameplayEquipmentSlot.Utility, registeredSlotItem.Slot);
        Assert.Equal("ability.plugin-dash", registeredSlotItem.ItemId);

        var world = new SimulationWorld();
        var player = new PlayerEntity(10, CharacterClassCatalog.RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Heavy));
        var abilityItem = new GameplayItemDefinition(
            "ability.plugin-dash",
            "Plugin Dash",
            GameplayEquipmentSlot.Utility,
            registeredExecutor.ExecutorId,
            new GameplayItemAmmoDefinition(),
            new GameplayItemPresentationDefinition(),
            Ability: registeredAbility.Ability);
        var executionResult = registeredExecutor.Executor.Handle(new GameplayAbilityContext
        {
            World = world,
            Player = player,
            Item = abilityItem,
            Ability = registeredAbility.Ability,
            Phase = GameplayAbilityInputPhase.Pressed,
            Input = new PlayerInputSnapshot(false, false, false, false, false, false, false, false, false, 20f, 30f, false),
            PreviousInput = new PlayerInputSnapshot(false, false, false, false, false, false, false, false, false, 0f, 0f, false),
            SourceX = 5f,
            SourceY = 6f,
        });
        Assert.True(executionResult.Handled);
        Assert.True(executionResult.ConsumedInput);
        using var executeJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(configDirectory, "ability-execute.json")));
        Assert.Equal("ability.plugin-dash", executeJson.RootElement.GetProperty("item_id").GetString());
        Assert.Equal(registeredExecutor.ExecutorId, executeJson.RootElement.GetProperty("executor_id").GetString());
        Assert.Equal(0.5, executeJson.RootElement.GetProperty("parameter_duration").GetDouble(), precision: 3);
        Assert.Equal(1, executeJson.RootElement.GetProperty("projectile_id").GetInt32());
        Assert.Contains(fakeContext.GameplayImpulses, request => request.PlayerId == 10 && request.VelocityX == 12f && request.VelocityY == -4f);
        Assert.Contains(fakeContext.GameplayAbilityCooldowns, request => request.PlayerId == 10 && request.CooldownKey == "plugin_dash_cooldown" && request.Ticks == 42);
        Assert.Contains(fakeContext.GameplayDamageRequests, request => request.TargetPlayerId == 10 && request.Amount == 3f && request.AttackerPlayerId == 10 && request.WeaponSpriteName == "DashKL");
        Assert.Contains(fakeContext.GameplayHealingRequests, request => request.PlayerId == 10 && request.Amount == 5f);
        Assert.Contains(fakeContext.GameplayStatusEffectRequests, request => request.PlayerId == 10 && request.StatusEffectId == "movement_boost" && request.Ticks == 9 && request.Value == 1.5f);
        var projectileRequest = Assert.Single(fakeContext.GameplayProjectileSpawnRequests);
        Assert.Equal("rocket", projectileRequest.Kind);
        Assert.Equal(10, projectileRequest.OwnerPlayerId);
    }

    [Fact]
    public void ServerLuaHostRegistersAndExecutesPrimaryWeaponBehavior()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaPrimaryWeaponApi");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-primary-weapon",
              "displayName": "Lua Server Primary Weapon API",
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
                local behavior_id = "plugin.tests.server.lua-primary-weapon.nailgun"
                local behavior_registered = host.register_gameplay_primary_weapon_behavior({
                    behaviorId = behavior_id,
                    fireSoundName = "NeedleSnd"
                })
                local weapon_registered = host.register_gameplay_weapon_item({
                    itemId = "weapon.plugin-nailgun",
                    displayName = "Plugin Nailgun",
                    slot = "Secondary",
                    behaviorId = behavior_id,
                    ammo = {
                        maxAmmo = 30,
                        ammoPerUse = 1,
                        projectilesPerUse = 1,
                        useDelaySourceTicks = 4,
                        reloadSourceTicks = 2,
                        minProjectileSpeed = 9
                    },
                    presentation = {
                        hud = {
                            displayKind = "ammoPanel",
                            stackGroup = "weapon",
                            stateProvider = "secondaryAmmo"
                        }
                    }
                })
                local slot_item_registered = host.register_gameplay_slot_item({
                    classId = "scout",
                    slot = "Secondary",
                    itemId = "weapon.plugin-nailgun",
                    loadoutId = "scout.plugin-nailgun",
                    displayName = "Plugin Nailgun",
                    baseLoadoutId = "scout.stock"
                })

                host.save_json_config("primary-api.json", {
                    behavior_registered = behavior_registered,
                    weapon_registered = weapon_registered,
                    slot_item_registered = slot_item_registered
                })
            end

            function plugin.on_gameplay_primary_weapon_execute(e)
                local velocity_x = e.directionX * e.weapon.minShotSpeed
                local velocity_y = e.directionY * e.weapon.minShotSpeed
                local projectile_id = plugin.host.try_spawn_gameplay_projectile({
                    ownerPlayerId = e.playerId,
                    kind = "needle",
                    x = e.sourceX,
                    y = e.sourceY,
                    velocityX = velocity_x,
                    velocityY = velocity_y,
                    killFeedWeaponSpriteName = e.killFeedWeaponSpriteName
                })
                plugin.host.save_json_config("primary-execute.json", {
                    item_id = e.itemId,
                    behavior_id = e.behaviorId,
                    weapon_kind = e.weapon.kind,
                    weapon_max_ammo = e.weapon.maxAmmo,
                    projectile_id = projectile_id,
                    velocity_x = velocity_x,
                    velocity_y = velocity_y
                })
                return { handled = true }
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

        Assert.Single(loadedPlugins);
        using var apiJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(configDirectory, "primary-api.json")));
        Assert.True(apiJson.RootElement.GetProperty("behavior_registered").GetBoolean());
        Assert.True(apiJson.RootElement.GetProperty("weapon_registered").GetBoolean());
        Assert.True(apiJson.RootElement.GetProperty("slot_item_registered").GetBoolean());

        var registeredBehavior = Assert.Single(fakeContext.RegisteredGameplayPrimaryWeaponBehaviors);
        Assert.Equal("plugin.tests.server.lua-primary-weapon.nailgun", registeredBehavior.BehaviorId);
        Assert.Equal("NeedleSnd", registeredBehavior.FireSoundName);
        var registeredWeapon = Assert.Single(fakeContext.RegisteredGameplayWeaponItems);
        Assert.Equal("weapon.plugin-nailgun", registeredWeapon.ItemId);
        Assert.Equal(GameplayEquipmentSlot.Secondary, registeredWeapon.Slot);
        Assert.Equal(30, registeredWeapon.Ammo.MaxAmmo);
        Assert.Equal(9f, registeredWeapon.Ammo.MinProjectileSpeed);
        Assert.Equal(GameplayItemHudStateProviders.SecondaryAmmo, registeredWeapon.Presentation?.Hud?.StateProvider);
        var registeredSlotItem = Assert.Single(fakeContext.RegisteredGameplaySlotItems);
        Assert.Equal("scout", registeredSlotItem.ClassId);
        Assert.Equal("weapon.plugin-nailgun", registeredSlotItem.ItemId);

        var world = new SimulationWorld();
        var player = new PlayerEntity(10, CharacterClassCatalog.RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Scout));
        var weapon = new PrimaryWeaponDefinition(
            "Plugin Nailgun",
            PrimaryWeaponKind.Custom,
            MaxAmmo: 30,
            AmmoPerShot: 1,
            ProjectilesPerShot: 1,
            ReloadDelayTicks: 4,
            AmmoReloadTicks: 2,
            SpreadDegrees: 0f,
            MinShotSpeed: 9f,
            AdditionalRandomShotSpeed: 0f);
        var result = registeredBehavior.Executor.Handle(new GameplayPrimaryWeaponContext
        {
            World = world,
            Player = player,
            Weapon = weapon,
            ItemId = "weapon.plugin-nailgun",
            BehaviorId = registeredBehavior.BehaviorId,
            WeaponClassId = PlayerClass.Scout,
            SourceX = 5f,
            SourceY = 6f,
            AimWorldX = 100f,
            AimWorldY = 6f,
            DirectionX = 1f,
            DirectionY = 0f,
            DirectionRadians = 0f,
            KillFeedWeaponSpriteName = "NeedleKL",
        });

        Assert.True(result.Handled);
        using var executeJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(configDirectory, "primary-execute.json")));
        Assert.Equal("weapon.plugin-nailgun", executeJson.RootElement.GetProperty("item_id").GetString());
        Assert.Equal(registeredBehavior.BehaviorId, executeJson.RootElement.GetProperty("behavior_id").GetString());
        Assert.Equal("Custom", executeJson.RootElement.GetProperty("weapon_kind").GetString());
        Assert.Equal(30, executeJson.RootElement.GetProperty("weapon_max_ammo").GetInt32());
        Assert.Equal(1, executeJson.RootElement.GetProperty("projectile_id").GetInt32());
        Assert.Equal(9f, executeJson.RootElement.GetProperty("velocity_x").GetSingle());
        Assert.Equal(0f, executeJson.RootElement.GetProperty("velocity_y").GetSingle());
        var projectileRequest = Assert.Single(fakeContext.GameplayProjectileSpawnRequests);
        Assert.Equal(GameplayProjectileKinds.Needle, projectileRequest.Kind);
        Assert.Equal(10, projectileRequest.OwnerPlayerId);
        Assert.Equal(9f, projectileRequest.VelocityX);
        Assert.Equal("NeedleKL", projectileRequest.KillFeedWeaponSpriteName);
    }

    [Fact]
    public void ServerLuaGameplayAbilityTemplateRegistersAndExecutesAdvertisedFlows()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadServerLuaTemplate(
            "ServerLua.GameplayAbility",
            "sample.server.lua-gameplay-ability",
            tempDirectory,
            logs);

        var context = loadedPlugin.Context;
        var dashAbility = Assert.Single(context.RegisteredGameplayAbilities, ability => ability.ItemId == "ability.sample-force-dash");
        Assert.Equal(GameplayAbilityConstants.UtilityCategory, dashAbility.Ability.Category);
        Assert.Equal(GameplayAbilityConstants.PressedActivation, dashAbility.Ability.Activation);
        Assert.NotNull(dashAbility.Presentation?.Hud);
        Assert.Equal(GameplayItemHudStateProviders.AbilityCooldown, dashAbility.Presentation!.Hud!.StateProvider);
        Assert.Equal("sample.server.lua-gameplay-ability", dashAbility.Presentation.Hud.StateOwner);
        Assert.Equal("sample_force_dash_cooldown", dashAbility.Presentation.Hud.CooldownKey);

        var loadout = Assert.Single(context.RegisteredGameplayLoadouts);
        Assert.Equal("heavy", loadout.ClassId);
        Assert.Equal("ability.sample-force-dash", loadout.UtilityItemId);
        Assert.True(loadout.AbilityItemIds is null || loadout.AbilityItemIds.Count == 0);

        var dashExecutor = Assert.Single(context.RegisteredGameplayAbilityExecutors, executor => executor.ExecutorId == dashAbility.Ability.ExecutorId);
        var world = new SimulationWorld();
        var player = new PlayerEntity(10, CharacterClassCatalog.RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Heavy));
        var dashItem = CreateAbilityItemDefinition(dashAbility);

        var dashResult = dashExecutor.Executor.Handle(new GameplayAbilityContext
        {
            World = world,
            Player = player,
            Item = dashItem,
            Ability = dashAbility.Ability,
            Phase = GameplayAbilityInputPhase.Pressed,
            Input = new PlayerInputSnapshot(false, false, false, false, false, false, false, false, false, 20f, 30f, false),
            PreviousInput = new PlayerInputSnapshot(false, false, false, false, false, false, false, false, false, 0f, 0f, false),
            SourceX = 5f,
            SourceY = 6f,
        });
        Assert.True(dashResult.Handled);
        Assert.True(dashResult.ConsumedInput);
        Assert.Contains(context.GameplayImpulses, request => request.PlayerId == 10 && request.VelocityX == 12f && request.VelocityY == -4f);
        Assert.Contains(context.GameplayAbilityCooldowns, request => request.PlayerId == 10 && request.CooldownKey == "sample_force_dash_cooldown" && request.Ticks == 180);

        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ServerLuaPrimaryWeaponTemplateRegistersAndExecutesAdvertisedFlows()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadServerLuaTemplate(
            "ServerLua.PrimaryWeapon",
            "sample.server.lua-primary-weapon",
            tempDirectory,
            logs);

        var context = loadedPlugin.Context;
        var behavior = Assert.Single(context.RegisteredGameplayPrimaryWeaponBehaviors);
        Assert.Equal("plugin.sample.server.lua-primary-weapon.nailgun", behavior.BehaviorId);
        Assert.Equal("NeedleSnd", behavior.FireSoundName);
        var weaponItem = Assert.Single(context.RegisteredGameplayWeaponItems);
        Assert.Equal("weapon.sample-nailgun", weaponItem.ItemId);
        Assert.Equal(GameplayEquipmentSlot.Secondary, weaponItem.Slot);
        Assert.Equal(30, weaponItem.Ammo.MaxAmmo);
        Assert.Equal(9f, weaponItem.Ammo.MinProjectileSpeed);
        var toggleAbility = Assert.Single(context.RegisteredGameplayAbilities);
        Assert.Equal("ability.sample-nailgun-toggle", toggleAbility.ItemId);
        Assert.Equal(BuiltInGameplayBehaviorIds.SoldierSecondaryToggle, toggleAbility.Ability.ExecutorId);
        var loadout = Assert.Single(context.RegisteredGameplayLoadouts);
        Assert.Equal("scout", loadout.ClassId);
        Assert.Equal("weapon.sample-nailgun", loadout.SecondaryItemId);
        Assert.Equal("ability.sample-nailgun-toggle", loadout.UtilityItemId);

        var world = new SimulationWorld();
        var player = new PlayerEntity(10, CharacterClassCatalog.RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Scout));
        var result = behavior.Executor.Handle(new GameplayPrimaryWeaponContext
        {
            World = world,
            Player = player,
            Weapon = new PrimaryWeaponDefinition(
                "Sample Nailgun",
                PrimaryWeaponKind.Custom,
                MaxAmmo: 30,
                AmmoPerShot: 1,
                ProjectilesPerShot: 1,
                ReloadDelayTicks: 4,
                AmmoReloadTicks: 2,
                SpreadDegrees: 0f,
                MinShotSpeed: 9f,
                AdditionalRandomShotSpeed: 0f),
            ItemId = "weapon.sample-nailgun",
            BehaviorId = behavior.BehaviorId,
            WeaponClassId = PlayerClass.Scout,
            SourceX = 5f,
            SourceY = 6f,
            AimWorldX = 100f,
            AimWorldY = 6f,
            DirectionX = 1f,
            DirectionY = 0f,
            DirectionRadians = 0f,
            KillFeedWeaponSpriteName = "NeedleKL",
        });

        Assert.True(result.Handled);
        var projectileRequest = Assert.Single(context.GameplayProjectileSpawnRequests);
        Assert.Equal(GameplayProjectileKinds.Needle, projectileRequest.Kind);
        Assert.Equal(9f, projectileRequest.VelocityX);
    }

    [Fact]
    public void ServerLuaAbilityInputEventCanCancelAndUsedEventIsObservable()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaAbilityEvents");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-ability-events",
              "displayName": "Lua Server Ability Events",
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

            function plugin.on_gameplay_ability_input(e)
                plugin.host.save_json_config("ability-input.json", {
                    item_id = e.itemId,
                    category = e.abilityCategory,
                    phase = e.phase
                })
                return false
            end

            function plugin.on_gameplay_ability_used(e)
                plugin.host.save_json_config("ability-used.json", {
                    item_id = e.itemId,
                    handled = e.handled,
                    consumed_input = e.consumedInput
                })
            end

            function plugin.on_gameplay_ability_state_changed(e)
                plugin.host.save_json_config("ability-state.json", {
                    player_id = e.playerId,
                    owner_id = e.ownerId,
                    state_key = e.stateKey,
                    value_kind = e.valueKind,
                    current_int_value = e.currentIntValue
                })
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
        var hooks = Assert.IsAssignableFrom<IOpenGarrisonServerSemanticGameplayHooks>(loadedPlugin.Plugin);

        var inputEvent = new OpenGarrisonServerGameplayAbilityInputEvent(
            1,
            10,
            PlayerClass.Heavy,
            PlayerTeam.Red,
            "ability.heavy-utility",
            BuiltInGameplayBehaviorIds.HeavyUtility,
            GameplayAbilityConstants.UtilityCategory,
            GameplayAbilityConstants.PressedActivation,
            BuiltInGameplayBehaviorIds.HeavyGhostDash,
            GameplayAbilityInputPhase.Pressed.ToString(),
            ["dash"]);
        hooks.OnGameplayAbilityInput(inputEvent);
        hooks.OnGameplayAbilityUsed(new OpenGarrisonServerGameplayAbilityUsedEvent(
            1,
            10,
            PlayerClass.Heavy,
            PlayerTeam.Red,
            "ability.heavy-utility",
            BuiltInGameplayBehaviorIds.HeavyUtility,
            GameplayAbilityConstants.UtilityCategory,
            GameplayAbilityConstants.PressedActivation,
            BuiltInGameplayBehaviorIds.HeavyGhostDash,
            GameplayAbilityInputPhase.Pressed.ToString(),
            ["dash"],
            Handled: true,
            ConsumedInput: true));
        hooks.OnGameplayAbilityStateChanged(new OpenGarrisonServerGameplayAbilityStateChangedEvent(
            2,
            10,
            PlayerClass.Heavy,
            PlayerTeam.Red,
            GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
            GameplayAbilityReplicatedState.HeavyDashCooldownTicksKey,
            GameplayReplicatedStateValueKind.Whole,
            HasPreviousValue: true,
            PreviousIntValue: 0,
            CurrentIntValue: 360,
            PreviousFloatValue: 0f,
            CurrentFloatValue: 0f,
            PreviousBoolValue: false,
            CurrentBoolValue: false));

        Assert.True(inputEvent.IsCancelled);
        using var inputJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(configDirectory, "ability-input.json")));
        Assert.Equal("ability.heavy-utility", inputJson.RootElement.GetProperty("item_id").GetString());
        Assert.Equal("utility", inputJson.RootElement.GetProperty("category").GetString());
        Assert.Equal("Pressed", inputJson.RootElement.GetProperty("phase").GetString());

        using var usedJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(configDirectory, "ability-used.json")));
        Assert.Equal("ability.heavy-utility", usedJson.RootElement.GetProperty("item_id").GetString());
        Assert.True(usedJson.RootElement.GetProperty("handled").GetBoolean());
        Assert.True(usedJson.RootElement.GetProperty("consumed_input").GetBoolean());

        using var stateJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(configDirectory, "ability-state.json")));
        Assert.Equal(10, stateJson.RootElement.GetProperty("player_id").GetInt32());
        Assert.Equal("core.ability", stateJson.RootElement.GetProperty("owner_id").GetString());
        Assert.Equal("heavy_dash_cooldown_ticks", stateJson.RootElement.GetProperty("state_key").GetString());
        Assert.Equal("Whole", stateJson.RootElement.GetProperty("value_kind").GetString());
        Assert.Equal(360, stateJson.RootElement.GetProperty("current_int_value").GetInt32());
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
    public void ServerLuaHostAllowsChatCommandMutationsAndMessaging()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaChatCommandMutations");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-chat-command-mutations",
              "displayName": "Lua Server Chat Command Mutations",
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

            function plugin.try_handle_chat_message(context, e)
                plugin.host.broadcast_system_message("server-broadcast")
                plugin.host.send_system_message(e.slot, "private-message")
                plugin.host.send_message_to_client(e.slot, "tests.client.receiver", "sync", "{\"ok\":true}", "Json", 2)
                plugin.host.broadcast_message_to_clients("tests.client.receiver", "announce", "payload", "Text", 3)
                assert(plugin.host.set_player_replicated_state_int(e.slot, "score_bonus", 7))
                assert(plugin.host.set_player_replicated_state_float(e.slot, "aim_scale", 1.5))
                assert(plugin.host.set_player_replicated_state_bool(e.slot, "is_marked", true))
                assert(plugin.host.clear_player_replicated_state(e.slot, "is_marked"))
                return e.text == "!mutate"
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
        var chatHooks = Assert.IsAssignableFrom<IOpenGarrisonServerChatCommandHooks>(loadedPlugin.Plugin);
        var handled = chatHooks.TryHandleChatMessage(
            new OpenGarrisonServerChatMessageContext(
                fakeContext.ServerState,
                fakeContext.AdminOperations,
                fakeContext.Cvars,
                fakeContext.Scheduler,
                OpenGarrisonServerAdminIdentity.CreateUnauthenticated(1)),
            new ChatReceivedEvent(1, "Tester", "!mutate", Team: null, TeamOnly: false));

        Assert.True(handled);
        Assert.Contains("server-broadcast", fakeContext.AdminImpl.BroadcastSystemMessages);
        Assert.Contains(fakeContext.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text == "private-message");
        Assert.Collection(
            fakeContext.SentPluginMessages,
            message =>
            {
                Assert.Equal((byte)1, message.Slot);
                Assert.Equal("tests.client.receiver", message.TargetPluginId);
                Assert.Equal("sync", message.MessageType);
                Assert.Equal("{\"ok\":true}", message.Payload);
                Assert.Equal(PluginMessagePayloadFormat.Json, message.PayloadFormat);
                Assert.Equal((ushort)2, message.SchemaVersion);
            });
        Assert.Collection(
            fakeContext.BroadcastPluginMessages,
            message =>
            {
                Assert.Equal("tests.client.receiver", message.TargetPluginId);
                Assert.Equal("announce", message.MessageType);
                Assert.Equal("payload", message.Payload);
                Assert.Equal(PluginMessagePayloadFormat.Text, message.PayloadFormat);
                Assert.Equal((ushort)3, message.SchemaVersion);
            });
        Assert.Collection(
            fakeContext.ReplicatedStateWrites,
            state =>
            {
                Assert.Equal("int", state.ValueKind);
                Assert.Equal((byte)1, state.Slot);
                Assert.Equal("score_bonus", state.StateKey);
                Assert.Equal(7, Assert.IsType<int>(state.Value));
            },
            state =>
            {
                Assert.Equal("float", state.ValueKind);
                Assert.Equal((byte)1, state.Slot);
                Assert.Equal("aim_scale", state.StateKey);
                Assert.Equal(1.5f, Assert.IsType<float>(state.Value));
            },
            state =>
            {
                Assert.Equal("bool", state.ValueKind);
                Assert.Equal((byte)1, state.Slot);
                Assert.Equal("is_marked", state.StateKey);
                Assert.True(Assert.IsType<bool>(state.Value));
            });
        Assert.Collection(
            fakeContext.ClearedReplicatedStateKeys,
            state =>
            {
                Assert.Equal((byte)1, state.Slot);
                Assert.Equal("is_marked", state.StateKey);
            });
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ServerLuaHostExposesAdminIdentityCvarsAndScheduler()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaAdminFoundation");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-admin-foundation",
              "displayName": "Lua Server Admin Foundation",
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
                plugin.timer_id = host.schedule_once(0.1, function()
                    plugin.host.broadcast_system_message("scheduled-tick")
                end, "lua-once")
            end

            function plugin.try_handle_chat_message(context, e)
                local cvar = plugin.host.get_cvar("sv_test")
                local timers = plugin.host.get_scheduled_tasks()
                if context.identity ~= nil and context.identity.isAuthenticated and cvar ~= nil and cvar.currentValue == "false" and timers[1] ~= nil then
                    return plugin.host.set_cvar("sv_test", "true")
                end

                return false
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
        fakeContext.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_test",
            "Test cvar",
            OpenGarrisonServerCvarValueType.Boolean,
            "false",
            "false",
            IsProtected: false,
            IsReadOnly: false));

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(tempDirectory.RootPath, SearchOption.AllDirectories)],
            (_, _, _) => fakeContext,
            logs.Add);

        var loadedPlugin = Assert.Single(loadedPlugins);
        var chatHooks = Assert.IsAssignableFrom<IOpenGarrisonServerChatCommandHooks>(loadedPlugin.Plugin);
        var handled = chatHooks.TryHandleChatMessage(
            new OpenGarrisonServerChatMessageContext(
                fakeContext.ServerState,
                fakeContext.AdminOperations,
                fakeContext.Cvars,
                fakeContext.Scheduler,
                new OpenGarrisonServerAdminIdentity(
                    "Admin",
                    OpenGarrisonServerAdminAuthority.RconSession,
                    OpenGarrisonServerAdminPermissions.FullAccess,
                    SourceSlot: 1)),
            new ChatReceivedEvent(1, "Tester", "!admin", Team: null, TeamOnly: false));

        Assert.True(handled);
        Assert.True(fakeContext.CvarImpl.TryGet("sv_test", out var cvar));
        Assert.Equal("true", cvar.CurrentValue);

        fakeContext.SchedulerImpl.RunAll();

        Assert.Contains("scheduled-tick", fakeContext.AdminImpl.BroadcastSystemMessages);
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ServerLuaHostRejectsInvalidPluginMessageAndReplicatedStateMetadata()
    {
        using var tempDirectory = new TempDirectory();
        var pluginDirectory = tempDirectory.CreateSubdirectory("ServerLuaInvalidMessageMetadata");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "tests.server.lua-invalid-message-metadata",
              "displayName": "Lua Server Invalid Message Metadata",
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
                plugin.host.send_message_to_client(1, "   ", "sync", "payload")
                plugin.host.broadcast_message_to_clients("tests.client.receiver", "   ", "payload")
                plugin.host.send_message_to_client(1, "tests.client.receiver", "sync", string.rep("x", 1025))
                plugin.host.set_player_replicated_state_int(1, "   ", 4)
                plugin.host.clear_player_replicated_state(1, "")
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

        Assert.Empty(fakeContext.SentPluginMessages);
        Assert.Empty(fakeContext.BroadcastPluginMessages);
        Assert.Empty(fakeContext.ReplicatedStateWrites);
        Assert.Empty(fakeContext.ClearedReplicatedStateKeys);
        Assert.Contains(logs, log => log.Contains("send_message_to_client rejected", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("broadcast_message_to_clients rejected", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("Payload exceeds protocol byte limit", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains("Replicated state keys must be non-empty ASCII identifiers", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
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

                local foundStockPack = false
                for _, pack in pairs(packs) do
                    if pack ~= nil and pack.modPackId == "stock.gg2" then
                        foundStockPack = true
                    end
                end

                if foundStockPack then
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

    [Fact]
    public void PackagedServerLuaChatVotingStartsVoteForAuthorizedPlayerSlot()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadPackagedServerLuaPlugin("Lua.ChatVoting", "chat.voting", tempDirectory, logs);
        loadedPlugin.Context.StateImpl.Players.Add(CreateServerPlayer(slot: 1, userId: 101, name: "Voter"));

        var commandContext = CreateCommandContext(
            loadedPlugin.Context,
            new OpenGarrisonServerAdminIdentity(
                "Voter",
                OpenGarrisonServerAdminAuthority.None,
                OpenGarrisonServerAdminPermissions.None,
                SourceSlot: 1));

        Assert.True(loadedPlugin.Context.CommandRegistry.TryExecute("!votemap ctf_truefort", commandContext, CancellationToken.None, out var responseLines));

        Assert.Empty(responseLines);
        var systemMessages = string.Join(Environment.NewLine, loadedPlugin.Context.AdminImpl.SystemMessages.Select(message => message.Text));
        Assert.True(
            !systemMessages.Contains("could not find your slot", StringComparison.OrdinalIgnoreCase),
            systemMessages);
        Assert.Contains(
            loadedPlugin.Context.AdminImpl.BroadcastSystemMessages,
            message => message.Contains("Voter started votemap for Truefort", StringComparison.Ordinal));
    }

    [Fact]
    public void PackagedServerLuaChatVotingYesVotePassesMapVote()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadPackagedServerLuaPlugin("Lua.ChatVoting", "chat.voting", tempDirectory, logs);
        loadedPlugin.Context.StateImpl.Players.Add(CreateServerPlayer(slot: 1, userId: 101, name: "Starter"));
        loadedPlugin.Context.StateImpl.Players.Add(CreateServerPlayer(slot: 2, userId: 202, name: "Voter"));

        var starterContext = CreateCommandContext(
            loadedPlugin.Context,
            new OpenGarrisonServerAdminIdentity(
                "Starter",
                OpenGarrisonServerAdminAuthority.None,
                OpenGarrisonServerAdminPermissions.None,
                SourceSlot: 1));
        var voterContext = CreateCommandContext(
            loadedPlugin.Context,
            new OpenGarrisonServerAdminIdentity(
                "Voter",
                OpenGarrisonServerAdminAuthority.None,
                OpenGarrisonServerAdminPermissions.None,
                SourceSlot: 2));

        Assert.True(loadedPlugin.Context.CommandRegistry.TryExecute("!votemap ctf_truefort", starterContext, CancellationToken.None, out var startLines));
        Assert.True(loadedPlugin.Context.CommandRegistry.TryExecute("!yes", voterContext, CancellationToken.None, out var yesLines));

        Assert.True(startLines.Count == 0, string.Join(Environment.NewLine, startLines.Concat(logs)));
        Assert.True(yesLines.Count == 0, string.Join(Environment.NewLine, yesLines.Concat(logs)));
        Assert.Empty(loadedPlugin.Context.AdminImpl.MapChangeRequests);
        Assert.Contains(
            loadedPlugin.Context.AdminImpl.BroadcastSystemMessages,
            message => message.Contains("Vote passed for Truefort", StringComparison.Ordinal));

        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonServerUpdateHooks>(loadedPlugin.Plugin);
        updateHooks.OnServerHeartbeat(TimeSpan.FromSeconds(1));

        var mapChange = Assert.Single(loadedPlugin.Context.AdminImpl.MapChangeRequests);
        Assert.Equal("Truefort", mapChange.LevelName);
        Assert.Equal(1, mapChange.AreaIndex);
        Assert.False(mapChange.PreservePlayerStats);
    }

    [Fact]
    public void PackagedServerLuaGarrisonToolsHandlesHelpStatusAndCvars()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadPackagedServerLuaPlugin("Lua.GarrisonTools", "open-garrison.server.lua-garrison-tools", tempDirectory, logs);

        loadedPlugin.Context.StateImpl.Players.Add(new OpenGarrisonServerPlayerInfo(
            1,
            101,
            "Admin",
            IsSpectator: false,
            IsAuthorized: true,
            IsGagged: false,
            IsAlive: true,
            PlayerId: 11,
            Team: PlayerTeam.Red,
            PlayerClass: PlayerClass.Soldier,
            PlayerScale: 1f,
            EndPoint: "127.0.0.1:8190",
            GameplayLoadoutId: "stock",
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: "weapon.rocketlauncher"));
        loadedPlugin.Context.StateImpl.Players.Add(new OpenGarrisonServerPlayerInfo(
            2,
            202,
            "Spectator",
            IsSpectator: true,
            IsAuthorized: true,
            IsGagged: false,
            IsAlive: false,
            PlayerId: null,
            Team: null,
            PlayerClass: null,
            PlayerScale: 1f,
            EndPoint: "127.0.0.1:8191",
            GameplayLoadoutId: string.Empty,
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: string.Empty));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_timelimit",
            "Time limit",
            OpenGarrisonServerCvarValueType.Integer,
            "15",
            "15",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 1,
            MaximumNumericValue: 255));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_caplimit",
            "Capture limit",
            OpenGarrisonServerCvarValueType.Integer,
            "3",
            "3",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 1,
            MaximumNumericValue: 255));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_respawnseconds",
            "Respawn time",
            OpenGarrisonServerCvarValueType.Integer,
            "5",
            "5",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 0,
            MaximumNumericValue: 255));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_player_scale",
            "Player scale",
            OpenGarrisonServerCvarValueType.Float,
            "1",
            "1",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 0.25,
            MaximumNumericValue: 4));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_map_scale",
            "Map scale",
            OpenGarrisonServerCvarValueType.Float,
            "1",
            "1",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 0.25,
            MaximumNumericValue: 4));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_movement_speed_scale",
            "Movement speed scale",
            OpenGarrisonServerCvarValueType.Float,
            "1",
            "1",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 0.1,
            MaximumNumericValue: 4));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_projectile_speed_scale",
            "Projectile speed scale",
            OpenGarrisonServerCvarValueType.Float,
            "1",
            "1",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 0.1,
            MaximumNumericValue: 4));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_damage_scale",
            "Damage scale",
            OpenGarrisonServerCvarValueType.Float,
            "1",
            "1",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 0,
            MaximumNumericValue: 10));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_gravity_scale",
            "Gravity scale",
            OpenGarrisonServerCvarValueType.Float,
            "1",
            "1",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 0,
            MaximumNumericValue: 4));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_horizontal_speed_clamp",
            "Horizontal speed clamp",
            OpenGarrisonServerCvarValueType.Float,
            "15",
            "15",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 1,
            MaximumNumericValue: 60));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_vertical_speed_clamp",
            "Vertical speed clamp",
            OpenGarrisonServerCvarValueType.Float,
            "15",
            "15",
            IsProtected: false,
            IsReadOnly: false,
            MinimumNumericValue: 1,
            MaximumNumericValue: 60));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_roundendff",
            "Round-end friendly fire",
            OpenGarrisonServerCvarValueType.Boolean,
            "false",
            "false",
            IsProtected: false,
            IsReadOnly: false));
        loadedPlugin.Context.CvarImpl.Add(new OpenGarrisonServerCvarInfo(
            "sv_rcon_password",
            "RCON password",
            OpenGarrisonServerCvarValueType.String,
            string.Empty,
            "secret",
            IsProtected: true,
            IsReadOnly: false));

        var chatHooks = Assert.IsAssignableFrom<IOpenGarrisonServerChatCommandHooks>(loadedPlugin.Plugin);
        var context = CreateAdminChatContext(loadedPlugin.Context, slot: 1);

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_help", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("help | page=1/", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("!gt_cvar", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("@me", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_help protect", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("search=\"protect\"", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("!gt_cvar protect <name>", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_help 2", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("help | page=2/", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("!gt_kick <target> [reason]", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_status", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("status | server=Test Server", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("players | total=2 | active=1 | spectators=1 | authorized=2", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("admin | identity=Admin | authority=RconSession", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("userid=101 | slot=1 | name=Admin", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("userid=202 | slot=2 | name=Spectator", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvars sv_", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvars | count=12", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_caplimit | value=3", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_respawnseconds | value=5", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_player_scale | value=1", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_map_scale | value=1", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_movement_speed_scale | value=1", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_projectile_speed_scale | value=1", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_damage_scale | value=1", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_gravity_scale | value=1", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_horizontal_speed_clamp | value=15", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_roundendff | value=false", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_rcon_password | value=secret", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_caplimit", Team: null, TeamOnly: false)));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_caplimit | value=3", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_caplimit 5", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_caplimit", out var updatedCvar));
        Assert.Equal("5", updatedCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_caplimit set to 5.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_timelimit 20", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_timelimit", out var updatedTimeLimitCvar));
        Assert.Equal("20", updatedTimeLimitCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_timelimit set to 20.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_respawnseconds 9", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_respawnseconds", out var updatedRespawnCvar));
        Assert.Equal("9", updatedRespawnCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_respawnseconds set to 9.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_player_scale 1.5", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_player_scale", out var updatedPlayerScaleCvar));
        Assert.Equal("1.5", updatedPlayerScaleCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_player_scale set to 1.5.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_map_scale 1.25", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_map_scale", out var updatedMapScaleCvar));
        Assert.Equal("1.25", updatedMapScaleCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_map_scale set to 1.25.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_movement_speed_scale 1.5", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_movement_speed_scale", out var updatedMovementScaleCvar));
        Assert.Equal("1.5", updatedMovementScaleCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_movement_speed_scale set to 1.5.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_projectile_speed_scale 1.25", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_projectile_speed_scale", out var updatedProjectileScaleCvar));
        Assert.Equal("1.25", updatedProjectileScaleCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_projectile_speed_scale set to 1.25.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_damage_scale 2", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_damage_scale", out var updatedDamageScaleCvar));
        Assert.Equal("2", updatedDamageScaleCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_damage_scale set to 2.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_gravity_scale 0.5", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_gravity_scale", out var updatedGravityScaleCvar));
        Assert.Equal("0.5", updatedGravityScaleCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_gravity_scale set to 0.5.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_roundendff on", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_roundendff", out var updatedRoundEndFriendlyFireCvar));
        Assert.Equal("on", updatedRoundEndFriendlyFireCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_roundendff set to on.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar protect sv_caplimit", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_caplimit", out var maskedProtectedCaplimit));
        Assert.True(maskedProtectedCaplimit.IsProtected);
        Assert.Equal("<protected>", maskedProtectedCaplimit.CurrentValue);
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_caplimit", includeProtectedValue: true, out var revealedProtectedCaplimit));
        Assert.True(revealedProtectedCaplimit.IsProtected);
        Assert.Equal("5", revealedProtectedCaplimit.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("sv_caplimit is now protected", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_caplimit", Team: null, TeamOnly: false)));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("name=sv_caplimit | value=5", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("protected=yes", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_cvar sv_caplimit 6", Team: null, TeamOnly: false)));
        Assert.True(loadedPlugin.Context.CvarImpl.TryGet("sv_caplimit", includeProtectedValue: true, out var updatedProtectedCaplimitCvar));
        Assert.Equal("6", updatedProtectedCaplimitCvar.CurrentValue);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("cvar sv_caplimit updated.", StringComparison.Ordinal));
    }

    [Fact]
    public void PackagedServerLuaGarrisonToolsEnumeratesHelpAndMenuFromCommandSpecs()
    {
        var repoRoot = FindRepositoryRoot();
        var mainLuaPath = Path.Combine(repoRoot, "Plugins", "Packaged", "Server", "Lua.GarrisonTools", "main.lua");
        var mainLua = File.ReadAllText(mainLuaPath);
        var commandSpecs = ExtractGarrisonToolsCommandSpecs(mainLua);

        Assert.NotEmpty(commandSpecs);
        Assert.DoesNotContain("local command_catalog =", mainLua, StringComparison.Ordinal);
        Assert.DoesNotContain("local help_page_lines =", mainLua, StringComparison.Ordinal);
        Assert.DoesNotContain("local admin_menu_lines =", mainLua, StringComparison.Ordinal);

        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadPackagedServerLuaPlugin("Lua.GarrisonTools", "open-garrison.server.lua-garrison-tools", tempDirectory, logs);
        var chatHooks = Assert.IsAssignableFrom<IOpenGarrisonServerChatCommandHooks>(loadedPlugin.Plugin);
        var messageHooks = Assert.IsAssignableFrom<IOpenGarrisonServerPluginMessageHooks>(loadedPlugin.Plugin);
        var context = CreateAdminChatContext(loadedPlugin.Context, slot: 1);

        var helpPageCount = Math.Max(1, (int)Math.Ceiling(commandSpecs.Count / 6d));
        var helpMessages = new List<string>();
        for (var page = 1; page <= helpPageCount; page++)
        {
            Assert.True(chatHooks.TryHandleChatMessage(
                context,
                new ChatReceivedEvent(1, "Admin", $"!gt_help {page}", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
            helpMessages.AddRange(loadedPlugin.Context.AdminImpl.SystemMessages.Select(message => message.Text));
            loadedPlugin.Context.AdminImpl.SystemMessages.Clear();
        }

        var helpText = string.Join("\n", helpMessages);
        foreach (var spec in commandSpecs)
        {
            Assert.Contains(spec.Usage, helpText, StringComparison.Ordinal);
        }

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_help record", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Text.Contains("!gt_demo <status|start [path]|stop>", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        var menuPayloads = new List<string>();
        void CaptureMenuPayloads()
        {
            menuPayloads.AddRange(loadedPlugin.Context.SentPluginMessages
                .Where(message => message.MessageType == "adminmenu.open")
                .Select(message => message.Payload));
            loadedPlugin.Context.SentPluginMessages.Clear();
        }

        void OpenMenu()
        {
            Assert.True(chatHooks.TryHandleChatMessage(
                context,
                new ChatReceivedEvent(1, "Admin", "!gt_adminmenu", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
            CaptureMenuPayloads();
        }

        void SelectMenuToken(string token)
        {
            messageHooks.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
                1,
                "Admin",
                "open-garrison.client.lua-garrison-tools-menu",
                "open-garrison.server.lua-garrison-tools",
                "adminmenu.select",
                token,
                PluginMessagePayloadFormat.Text,
                1));
            CaptureMenuPayloads();
        }

        OpenMenu();
        SelectMenuToken("nav:server_management");
        SelectMenuToken("category:server_management|Reference");
        OpenMenu();
        SelectMenuToken("nav:server_management");
        SelectMenuToken("category:server_management|Communication");
        OpenMenu();
        SelectMenuToken("nav:game_management");
        OpenMenu();
        SelectMenuToken("nav:player_management");
        SelectMenuToken("page:2");
        SelectMenuToken("page:3");

        var menuText = string.Join("\n", menuPayloads);
        foreach (var spec in commandSpecs.Where(spec => !spec.Hidden && !string.IsNullOrWhiteSpace(spec.Branch)))
        {
            Assert.Contains("|l=" + spec.Label + "|", menuText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PackagedServerLuaGarrisonToolsHandlesAdminActions()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadPackagedServerLuaPlugin("Lua.GarrisonTools", "open-garrison.server.lua-garrison-tools", tempDirectory, logs);
        loadedPlugin.Context.StateImpl.Players.Add(new OpenGarrisonServerPlayerInfo(
            1,
            101,
            "Admin",
            IsSpectator: false,
            IsAuthorized: true,
            IsGagged: false,
            IsAlive: true,
            PlayerId: 11,
            Team: PlayerTeam.Red,
            PlayerClass: PlayerClass.Soldier,
            PlayerScale: 1f,
            EndPoint: "127.0.0.1:8190",
            GameplayLoadoutId: "stock",
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: "weapon.rocketlauncher"));
        loadedPlugin.Context.StateImpl.Players.Add(new OpenGarrisonServerPlayerInfo(
            3,
            303,
            "Target",
            IsSpectator: false,
            IsAuthorized: true,
            IsGagged: false,
            IsAlive: true,
            PlayerId: 33,
            Team: PlayerTeam.Blue,
            PlayerClass: PlayerClass.Medic,
            PlayerScale: 1f,
            MovementSpeedScale: 1.25f,
            HasMovementSpeedScaleOverride: true,
            GravityScale: 1f,
            HasGravityScaleOverride: false,
            EndPoint: "127.0.0.1:8192",
            GameplayLoadoutId: "stock",
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: "weapon.medigun"));
        var chatHooks = Assert.IsAssignableFrom<IOpenGarrisonServerChatCommandHooks>(loadedPlugin.Plugin);
        var messageHooks = Assert.IsAssignableFrom<IOpenGarrisonServerPluginMessageHooks>(loadedPlugin.Plugin);
        var context = CreateAdminChatContext(loadedPlugin.Context, slot: 1);

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_say Server restart soon", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(
            loadedPlugin.Context.BroadcastPluginMessages,
            message => message.TargetPluginId == "open-garrison.client.lua-garrison-tools-effects"
                && message.MessageType == "announce.notice"
                && message.Payload == "Server restart soon"
                && message.PayloadFormat == PluginMessagePayloadFormat.Text
                && message.SchemaVersion == 1);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("announcement sent", StringComparison.OrdinalIgnoreCase));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_psay #303 \"hello target\"", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 3 && message.Text == "hello target");
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("sent private message to Target (#303)", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_kick #303 griefing", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.DisconnectRequests, request => request.Slot == 3 && request.Reason == "griefing");
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("kicked Target (#303, slot 3)", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_ban #303 5 \"team griefing\"", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.BanPlayerRequests, request => request.Slot == 3 && request.Duration == TimeSpan.FromMinutes(5) && request.Reason == "team griefing");
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("banned Target (#303, ip 127.0.0.3) for 5 minute(s).", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_banip 127.0.0.44 0 routing", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.BanIpRequests, request => request.Address == "127.0.0.44" && request.Duration is null && request.Reason == "routing");
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("banned ip 127.0.0.44 permanently.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_unban 127.0.0.44", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains("127.0.0.44", loadedPlugin.Context.AdminImpl.UnbanIpRequests);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("unbanned ip 127.0.0.44.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_slay @blue", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.ForceKillRequests, slot => slot == 3);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("slayed 1 player(s)", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_burn @me 4", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.IgniteRequests, request => request.Slot == 1 && Math.Abs(request.DurationSeconds - 4f) < 0.001f);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("ignited 1 player(s) for 4s", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_gag #303", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.GagRequests, request => request.Slot == 3 && request.IsGagged);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 3 && message.Text.Contains("been gagged", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("gagged Target (#303)", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_rename #303 \"Renamed Medic\"", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.RenameRequests, request => request.Slot == 3 && request.NewName == "Renamed Medic");
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("renamed Target to Renamed Medic.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_bots fill 2 blue medic", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.FillBotTeamRequests, request =>
            request.Team == PlayerTeam.Blue && request.TargetCount == 2 && request.RequestedClass == PlayerClass.Medic);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("filled 2 bot slots on Blue team.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();
        loadedPlugin.Context.AdminImpl.FillBotTeamRequests.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_bots fill 2 blue", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.FillBotTeamRequests, request =>
            request.Team == PlayerTeam.Blue && request.TargetCount == 2 && request.RequestedClass is null);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("filled 2 bot slots on Blue team.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_bots fill 1 spy", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.FillBotRequests, request =>
            request.TargetPerTeam == 1 && request.RequestedClass == PlayerClass.Spy);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("filled 2 bot slots total (1 per team).", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_bots add 4 red scout ScoutBot", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.AddBotRequests, request =>
            request.Slot == 4
            && request.Team == PlayerTeam.Red
            && request.PlayerClass == PlayerClass.Scout
            && request.DisplayName == "ScoutBot");
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("bot added at slot 4.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_bots add 5 blue soldier", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.AddBotRequests, request =>
            request.Slot == 5
            && request.Team == PlayerTeam.Blue
            && request.PlayerClass == PlayerClass.Soldier
            && request.DisplayName == string.Empty);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("bot added at slot 5.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_bots remove 4", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains((byte)4, loadedPlugin.Context.AdminImpl.RemoveBotRequests);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("bot removed from slot 4.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_bots clear", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Equal(1, loadedPlugin.Context.AdminImpl.ClearBotsCallCount);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("removed 0 bots.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_kick @me regroup", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.DisconnectRequests, request => request.Slot == 1 && request.Reason == "regroup");
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_map ctf_avanti 2", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.MapChangeRequests, request => request.LevelName == "ctf_avanti" && request.AreaIndex == 2);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("changed map to ctf_avanti area 2.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_nextmap ctf_truefort", Team: null, TeamOnly: false)));
        Assert.Contains(loadedPlugin.Context.AdminImpl.NextRoundMapRequests, request => request.LevelName == "ctf_truefort" && request.AreaIndex == 1);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("next map set to ctf_truefort area 1.", StringComparison.Ordinal));
        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_adminmenu", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(
            loadedPlugin.Context.SentPluginMessages,
            message => message.Slot == 1
                && message.TargetPluginId == "open-garrison.client.lua-garrison-tools-menu"
                && message.MessageType == "adminmenu.open"
                && message.Payload.StartsWith("am1|t=Admin Menu|", StringComparison.Ordinal)
                && message.Payload.Contains("|l=Server management|", StringComparison.Ordinal)
                && message.Payload.Contains("|l=Player management|", StringComparison.Ordinal));

        loadedPlugin.Context.SentPluginMessages.Clear();
        messageHooks.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            1,
            "Admin",
            "open-garrison.client.lua-garrison-tools-menu",
            "open-garrison.server.lua-garrison-tools",
            "adminmenu.select",
            "select:3",
            PluginMessagePayloadFormat.Text,
            1));
        Assert.True(
            loadedPlugin.Context.SentPluginMessages.Any(
                message => message.Slot == 1
                    && message.TargetPluginId == "open-garrison.client.lua-garrison-tools-menu"
                    && message.MessageType == "adminmenu.open"
                    && message.Payload.Contains("|t=Player management|", StringComparison.Ordinal)
                    && message.Payload.Contains("|l=Kick|", StringComparison.Ordinal)
                    && message.Payload.Contains("|l=Ban|", StringComparison.Ordinal)
                    && message.Payload.Contains("|l=Ban IP|", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, logs.Concat(loadedPlugin.Context.SentPluginMessages.Select(message => message.Payload))));

        loadedPlugin.Context.SentPluginMessages.Clear();
        messageHooks.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            1,
            "Admin",
            "open-garrison.client.lua-garrison-tools-menu",
            "open-garrison.server.lua-garrison-tools",
            "adminmenu.select",
            "select:5",
            PluginMessagePayloadFormat.Text,
            1));
        Assert.True(
            loadedPlugin.Context.SentPluginMessages.Any(
                message => message.Slot == 1
                    && message.TargetPluginId == "open-garrison.client.lua-garrison-tools-menu"
                    && message.MessageType == "adminmenu.open"
                    && message.Payload.Contains("|t=Admin Menu|", StringComparison.Ordinal)
                    && message.Payload.Contains("|l=Server management|", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, logs.Concat(loadedPlugin.Context.SentPluginMessages.Select(message => message.Payload))));
        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_help", Team: null, TeamOnly: false)), string.Join(Environment.NewLine, logs));
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message => message.Slot == 1 && message.Text.Contains("[GT] help |", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, log => log.Contains("callback failed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, log => log.Contains("disabled open-garrison.server.lua-garrison-tools", StringComparison.OrdinalIgnoreCase));

        loadedPlugin.Context.SentPluginMessages.Clear();
        messageHooks.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            1,
            "Admin",
            "open-garrison.client.lua-garrison-tools-menu",
            "open-garrison.server.lua-garrison-tools",
            "adminmenu.select",
            "nav:fun",
            PluginMessagePayloadFormat.Text,
            1));
        messageHooks.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            1,
            "Admin",
            "open-garrison.client.lua-garrison-tools-menu",
            "open-garrison.server.lua-garrison-tools",
            "adminmenu.select",
            "nav:fun_player_effects",
            PluginMessagePayloadFormat.Text,
            1));
        messageHooks.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            1,
            "Admin",
            "open-garrison.client.lua-garrison-tools-menu",
            "open-garrison.server.lua-garrison-tools",
            "adminmenu.select",
            "nav:fun_player_modifiers",
            PluginMessagePayloadFormat.Text,
            1));
        messageHooks.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            1,
            "Admin",
            "open-garrison.client.lua-garrison-tools-menu",
            "open-garrison.server.lua-garrison-tools",
            "adminmenu.select",
            "action:scale",
            PluginMessagePayloadFormat.Text,
            1));
        messageHooks.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            1,
            "Admin",
            "open-garrison.client.lua-garrison-tools-menu",
            "open-garrison.server.lua-garrison-tools",
            "adminmenu.select",
            "target:#303",
            PluginMessagePayloadFormat.Text,
            1));
        messageHooks.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            1,
            "Admin",
            "open-garrison.client.lua-garrison-tools-menu",
            "open-garrison.server.lua-garrison-tools",
            "adminmenu.select",
            "value:0.5",
            PluginMessagePayloadFormat.Text,
            1));
        messageHooks.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
            1,
            "Admin",
            "open-garrison.client.lua-garrison-tools-menu",
            "open-garrison.server.lua-garrison-tools",
            "adminmenu.select",
            "duration:0",
            PluginMessagePayloadFormat.Text,
            1));

        Assert.True(
            loadedPlugin.Context.AdminImpl.ScaleRequests.Any(request => request.Slot == 3 && Math.Abs(request.Scale - 0.5f) < 0.001f),
            string.Join(Environment.NewLine, logs));
        Assert.Empty(loadedPlugin.Context.SchedulerImpl.Tasks);
        Assert.Contains(
            loadedPlugin.Context.AdminImpl.SystemMessages,
            message => message.Slot == 1 && message.Text.Contains("applied scale 0.5 to 1 target(s) until cleared.", StringComparison.Ordinal));
    }

    [Fact]
    public void PackagedServerLuaGarrisonToolsHandlesSpecialEffectsAndTimedClear()
    {
        using var tempDirectory = new TempDirectory();
        var logs = new List<string>();
        var loadedPlugin = LoadPackagedServerLuaPlugin("Lua.GarrisonTools", "open-garrison.server.lua-garrison-tools", tempDirectory, logs);
        loadedPlugin.Context.StateImpl.Players.Add(new OpenGarrisonServerPlayerInfo(
            1,
            101,
            "Admin",
            IsSpectator: false,
            IsAuthorized: true,
            IsGagged: false,
            IsAlive: true,
            PlayerId: 11,
            Team: PlayerTeam.Red,
            PlayerClass: PlayerClass.Soldier,
            PlayerScale: 1f,
            EndPoint: "127.0.0.1:8190",
            GameplayLoadoutId: "stock",
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: "weapon.rocketlauncher"));
        loadedPlugin.Context.StateImpl.Players.Add(new OpenGarrisonServerPlayerInfo(
            3,
            303,
            "Target",
            IsSpectator: false,
            IsAuthorized: true,
            IsGagged: false,
            IsAlive: true,
            PlayerId: 33,
            Team: PlayerTeam.Blue,
            PlayerClass: PlayerClass.Medic,
            PlayerScale: 1f,
            MovementSpeedScale: 1.25f,
            HasMovementSpeedScaleOverride: true,
            GravityScale: 1f,
            HasGravityScaleOverride: false,
            EndPoint: "127.0.0.1:8192",
            GameplayLoadoutId: "stock",
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: "weapon.medigun"));

        var chatHooks = Assert.IsAssignableFrom<IOpenGarrisonServerChatCommandHooks>(loadedPlugin.Plugin);
        var context = CreateAdminChatContext(loadedPlugin.Context, slot: 1);

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_seffect blind #303 5", Team: null, TeamOnly: false)),
            string.Join(Environment.NewLine, logs));

        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message =>
            message.Slot == 1 && message.Text.Contains("applied blind to 1 target(s) for 5s.", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.SentPluginMessages, message =>
            message.Slot == 3
            && message.TargetPluginId == "open-garrison.client.lua-garrison-tools-effects"
            && message.MessageType == "seffect.apply"
            && message.Payload.StartsWith("blind|5.000|", StringComparison.Ordinal));
        Assert.Single(loadedPlugin.Context.SchedulerImpl.Tasks);

        loadedPlugin.Context.SchedulerImpl.RunAll();

        Assert.Contains(loadedPlugin.Context.SentPluginMessages, message =>
            message.Slot == 3
            && message.TargetPluginId == "open-garrison.client.lua-garrison-tools-effects"
            && message.MessageType == "seffect.clear"
            && message.Payload == "blind");
        Assert.Empty(loadedPlugin.Context.SchedulerImpl.Tasks);

        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();
        loadedPlugin.Context.SentPluginMessages.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_seffect earthquake #303 4", Team: null, TeamOnly: false)),
            string.Join(Environment.NewLine, logs));
        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_seffect clear #303 earthquake", Team: null, TeamOnly: false)),
            string.Join(Environment.NewLine, logs));

        Assert.Contains(loadedPlugin.Context.SentPluginMessages, message =>
            message.Slot == 3
            && message.MessageType == "seffect.apply"
            && message.Payload.StartsWith("earthquake|4.000|", StringComparison.Ordinal));
        Assert.Contains(loadedPlugin.Context.SentPluginMessages, message =>
            message.Slot == 3
            && message.MessageType == "seffect.clear"
            && message.Payload == "earthquake");
        Assert.Empty(loadedPlugin.Context.SchedulerImpl.Tasks);

        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();
        loadedPlugin.Context.AdminImpl.ScaleRequests.Clear();
        loadedPlugin.Context.AdminImpl.MovementSpeedScaleRequests.Clear();
        loadedPlugin.Context.AdminImpl.ClearedMovementSpeedScaleSlots.Clear();
        loadedPlugin.Context.AdminImpl.GravityScaleRequests.Clear();
        loadedPlugin.Context.AdminImpl.ClearedGravityScaleSlots.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_seffect scale #303 0.5 7", Team: null, TeamOnly: false)),
            string.Join(Environment.NewLine, logs));

        Assert.Contains(loadedPlugin.Context.AdminImpl.ScaleRequests, request =>
            request.Slot == 3 && Math.Abs(request.Scale - 0.5f) < 0.001f);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message =>
            message.Slot == 1 && message.Text.Contains("applied scale 0.5 to 1 target(s) for 7s.", StringComparison.Ordinal));
        Assert.Single(loadedPlugin.Context.SchedulerImpl.Tasks);

        loadedPlugin.Context.SchedulerImpl.RunAll();

        Assert.Contains(loadedPlugin.Context.AdminImpl.ScaleRequests, request =>
            request.Slot == 3 && Math.Abs(request.Scale - 1f) < 0.001f);
        Assert.Empty(loadedPlugin.Context.SchedulerImpl.Tasks);

        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();
        loadedPlugin.Context.AdminImpl.MovementSpeedScaleRequests.Clear();
        loadedPlugin.Context.AdminImpl.ClearedMovementSpeedScaleSlots.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_seffect speed #303 2.5 6", Team: null, TeamOnly: false)),
            string.Join(Environment.NewLine, logs));

        Assert.Contains(loadedPlugin.Context.AdminImpl.MovementSpeedScaleRequests, request =>
            request.Slot == 3 && Math.Abs(request.Scale - 2.5f) < 0.001f);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message =>
            message.Slot == 1 && message.Text.Contains("applied speed 2.5 to 1 target(s) for 6s.", StringComparison.Ordinal));
        Assert.Single(loadedPlugin.Context.SchedulerImpl.Tasks);

        loadedPlugin.Context.SchedulerImpl.RunAll();

        Assert.Contains(loadedPlugin.Context.AdminImpl.MovementSpeedScaleRequests, request =>
            request.Slot == 3 && Math.Abs(request.Scale - 1.25f) < 0.001f);
        Assert.Empty(loadedPlugin.Context.AdminImpl.ClearedMovementSpeedScaleSlots);
        Assert.Empty(loadedPlugin.Context.SchedulerImpl.Tasks);

        loadedPlugin.Context.AdminImpl.SystemMessages.Clear();
        loadedPlugin.Context.AdminImpl.GravityScaleRequests.Clear();
        loadedPlugin.Context.AdminImpl.ClearedGravityScaleSlots.Clear();

        Assert.True(chatHooks.TryHandleChatMessage(
            context,
            new ChatReceivedEvent(1, "Admin", "!gt_seffect lowgrav #303 0.5 8", Team: null, TeamOnly: false)),
            string.Join(Environment.NewLine, logs));

        Assert.Contains(loadedPlugin.Context.AdminImpl.GravityScaleRequests, request =>
            request.Slot == 3 && Math.Abs(request.Scale - 0.5f) < 0.001f);
        Assert.Contains(loadedPlugin.Context.AdminImpl.SystemMessages, message =>
            message.Slot == 1 && message.Text.Contains("applied lowgrav 0.5 to 1 target(s) for 8s.", StringComparison.Ordinal));
        Assert.Single(loadedPlugin.Context.SchedulerImpl.Tasks);

        loadedPlugin.Context.SchedulerImpl.RunAll();

        Assert.Contains((byte)3, loadedPlugin.Context.AdminImpl.ClearedGravityScaleSlots);
        Assert.Empty(loadedPlugin.Context.SchedulerImpl.Tasks);
        Assert.DoesNotContain(logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
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

    private static LoadedServerLuaTemplate LoadServerLuaTemplate(
        string folderName,
        string pluginId,
        TempDirectory tempDirectory,
        List<string> logs)
    {
        var repoRoot = FindRepositoryRoot();
        var pluginDirectory = Path.Combine(repoRoot, "Plugins", "Templates", folderName);
        var manifestPath = Path.Combine(pluginDirectory, "plugin.json");
        Assert.True(OpenGarrisonPluginManifestLoader.TryLoadFromPath(manifestPath, out var manifest, out var manifestError), manifestError);

        var configDirectory = tempDirectory.CreateSubdirectory(pluginId.Replace('.', '_') + "_config");
        var context = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            configDirectory,
            tempDirectory.CreateSubdirectory(pluginId.Replace('.', '_') + "_maps"),
            logs);

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(pluginDirectory, SearchOption.TopDirectoryOnly)],
            (_, _, _) => context,
            logs.Add);

        Assert.True(loadedPlugins.Count == 1, string.Join(Environment.NewLine, logs));
        var loadedPlugin = loadedPlugins[0];
        Assert.Equal(pluginId, loadedPlugin.Plugin.Id);
        return new LoadedServerLuaTemplate(loadedPlugin.Plugin, context, configDirectory);
    }

    private static LoadedClientLuaTemplate LoadPackagedClientLuaPlugin(
        string folderName,
        string pluginId,
        TempDirectory tempDirectory,
        List<string> logs)
    {
        var repoRoot = FindRepositoryRoot();
        var pluginDirectory = Path.Combine(repoRoot, "Plugins", "Packaged", "Client", folderName);
        var discoveredPlugin = ClientPluginLoader.DiscoverFromDirectory(pluginDirectory, logs.Add)
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

    private static LoadedServerLuaTemplate LoadPackagedServerLuaPlugin(
        string folderName,
        string pluginId,
        TempDirectory tempDirectory,
        List<string> logs)
    {
        var repoRoot = FindRepositoryRoot();
        var pluginDirectory = Path.Combine(repoRoot, "Plugins", "Packaged", "Server", folderName);
        var manifestPath = Path.Combine(pluginDirectory, "plugin.json");
        Assert.True(OpenGarrisonPluginManifestLoader.TryLoadFromPath(manifestPath, out var manifest, out var manifestError), manifestError);

        var configDirectory = tempDirectory.CreateSubdirectory(pluginId.Replace('.', '_') + "_config");
        var context = new FakeServerPluginContext(
            manifest,
            pluginDirectory,
            configDirectory,
            tempDirectory.CreateSubdirectory(pluginId.Replace('.', '_') + "_maps"),
            logs);

        var loadedPlugins = PluginLoader.LoadFromSearchDirectories(
            [new PluginLoader.PluginSearchDirectory(pluginDirectory, SearchOption.TopDirectoryOnly)],
            (_, _, _) => context,
            logs.Add);

        Assert.True(loadedPlugins.Count == 1, string.Join(Environment.NewLine, logs));
        var loadedPlugin = loadedPlugins[0];
        Assert.Equal(pluginId, loadedPlugin.Plugin.Id);
        return new LoadedServerLuaTemplate(loadedPlugin.Plugin, context, configDirectory);
    }

    private static GameplayItemDefinition CreateAbilityItemDefinition(GameplayAbilityRegistration registration)
    {
        return new GameplayItemDefinition(
            registration.ItemId,
            registration.DisplayName,
            registration.Slot,
            registration.BehaviorId,
            new GameplayItemAmmoDefinition(),
            registration.Presentation ?? new GameplayItemPresentationDefinition(),
            Ability: registration.Ability);
    }

    private static OpenGarrisonServerChatMessageContext CreateAdminChatContext(FakeServerPluginContext context, byte slot)
    {
        return new OpenGarrisonServerChatMessageContext(
            context.ServerState,
            context.AdminOperations,
            context.Cvars,
            context.Scheduler,
            new OpenGarrisonServerAdminIdentity(
                "Admin",
                OpenGarrisonServerAdminAuthority.RconSession,
                OpenGarrisonServerAdminPermissions.FullAccess,
                slot));
    }

    private static OpenGarrisonServerCommandContext CreateCommandContext(
        FakeServerPluginContext context,
        OpenGarrisonServerAdminIdentity identity)
    {
        return new OpenGarrisonServerCommandContext(
            context.ServerState,
            context.AdminOperations,
            context.Cvars,
            context.Scheduler,
            identity,
            OpenGarrisonServerCommandSource.PrivateChat);
    }

    private static OpenGarrisonServerPlayerInfo CreateServerPlayer(
        byte slot,
        int userId,
        string name,
        bool isSpectator = false,
        bool isAuthorized = true,
        bool isAlive = true,
        PlayerTeam? team = PlayerTeam.Red,
        PlayerClass? playerClass = PlayerClass.Soldier)
    {
        return new OpenGarrisonServerPlayerInfo(
            slot,
            userId,
            name,
            IsSpectator: isSpectator,
            IsAuthorized: isAuthorized,
            IsGagged: false,
            IsAlive: isAlive,
            PlayerId: isSpectator ? null : slot,
            Team: isSpectator ? null : team,
            PlayerClass: isSpectator ? null : playerClass,
            PlayerScale: 1f,
            EndPoint: $"127.0.0.1:{8190 + slot}",
            GameplayLoadoutId: "stock",
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: "weapon.rocketlauncher");
    }

    private static List<GarrisonToolsCommandSpec> ExtractGarrisonToolsCommandSpecs(string mainLua)
    {
        var specs = new List<GarrisonToolsCommandSpec>();
        foreach (Match match in Regex.Matches(
            mainLua,
            @"append_command_spec\(\{(?<body>.*?)\}\)",
            RegexOptions.Singleline))
        {
            var body = match.Groups["body"].Value;
            specs.Add(new GarrisonToolsCommandSpec(
                GetLuaStringProperty(body, "name"),
                GetLuaStringProperty(body, "usage"),
                GetLuaStringProperty(body, "label"),
                GetOptionalLuaStringProperty(body, "branch"),
                body.Contains("hidden = true", StringComparison.Ordinal)));
        }

        return specs;
    }

    private static string GetLuaStringProperty(string body, string propertyName)
    {
        var value = GetOptionalLuaStringProperty(body, propertyName);
        Assert.False(string.IsNullOrWhiteSpace(value), $"Missing Lua command spec property '{propertyName}'.");
        return value!;
    }

    private static string? GetOptionalLuaStringProperty(string body, string propertyName)
    {
        var match = Regex.Match(body, propertyName + @"\s*=\s*""(?<value>[^""]*)""");
        return match.Success ? match.Groups["value"].Value : null;
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

    private sealed record LoadedServerLuaTemplate(
        IOpenGarrisonServerPlugin Plugin,
        FakeServerPluginContext Context,
        string ConfigDirectory);

    private sealed record GarrisonToolsCommandSpec(
        string Name,
        string Usage,
        string Label,
        string? Branch,
        bool Hidden);

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

        public List<(string TargetPluginId, string MessageType, string Payload, PluginMessagePayloadFormat PayloadFormat, ushort SchemaVersion)> SentMessages { get; } = [];

        public GraphicsDevice GraphicsDevice => null!;

        public IOpenGarrisonClientReadOnlyState ClientState => StateImpl;

        public IOpenGarrisonClientPluginAssets Assets => AssetsImpl;

        public IOpenGarrisonClientPluginHotkeys Hotkeys => HotkeysImpl;

        public IOpenGarrisonClientPluginUi Ui => UiImpl;

        public void Log(string message) => _logs.Add(message);

        public void SendMessageToServer(string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
        {
            SentMessages.Add((targetPluginId, messageType, payload, payloadFormat, schemaVersion));
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
        public float SoundEffectsVolumeScale { get; set; } = 1f;
        public Vector2 CameraTopLeft { get; set; } = Vector2.Zero;
        public Vector2 LocalPlayerPosition { get; set; } = new(10f, 20f);
        public List<string> LocalGameplayItemIds { get; } = [];
        public List<string> LocalGameplayAbilityItemIds { get; } = [];
        public List<ClientPlayerMarker> PlayerMarkers { get; set; } = [];
        public List<ClientSentryMarker> SentryMarkers { get; set; } = [];
        public List<ClientObjectiveMarker> ObjectiveMarkers { get; set; } = [];
        public Dictionary<(int PlayerId, string OwnerPluginId, string StateKey), int> IntReplicatedStates { get; } = [];
        public Dictionary<(int PlayerId, string OwnerPluginId, string StateKey), float> FloatReplicatedStates { get; } = [];
        public Dictionary<(int PlayerId, string OwnerPluginId, string StateKey), bool> BoolReplicatedStates { get; } = [];

        public IReadOnlyList<ClientPlayerMarker> GetPlayerMarkers() => PlayerMarkers;
        public IReadOnlyList<ClientSentryMarker> GetSentryMarkers() => SentryMarkers;
        public IReadOnlyList<ClientObjectiveMarker> GetObjectiveMarkers() => ObjectiveMarkers;
        public IReadOnlyList<string> GetLocalGameplayItemIds() => LocalGameplayItemIds;
        public IReadOnlyList<string> GetLocalGameplayAbilityItemIds() => LocalGameplayAbilityItemIds;
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
            return BoolReplicatedStates.TryGetValue((playerId, ownerPluginId, stateKey), out value);
        }

        public bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value)
        {
            return FloatReplicatedStates.TryGetValue((playerId, ownerPluginId, stateKey), out value);
        }

        public bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value)
        {
            return IntReplicatedStates.TryGetValue((playerId, ownerPluginId, stateKey), out value);
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

        public bool CaptureEnabled { get; private set; }

        public Keys RegisterHotkey(string hotkeyId, string displayName, Keys defaultKey)
        {
            RegisteredHotkeys.Add((hotkeyId, displayName, defaultKey));
            return defaultKey;
        }

        public bool WasHotkeyPressed(string hotkeyId) => PressedHotkeys.Remove(hotkeyId);

        public void SetHotkeyCaptureEnabled(bool enabled)
        {
            CaptureEnabled = enabled;
        }
    }

    private sealed class FakeClientUi : IOpenGarrisonClientPluginUi
    {
        public List<(string MenuEntryId, string Label, ClientPluginMenuLocation Location, int Order, Action Activate)> MenuEntries { get; } = [];

        public List<(string Text, int DurationTicks, bool PlaySound)> Notices { get; } = [];

        public FakeOverlayMenuState? OverlayMenu { get; private set; }

        public ClientPluginOverlayPanel? OverlayPanel { get; private set; }

        public void RegisterMenuEntry(string menuEntryId, string label, ClientPluginMenuLocation location, Action activate, int order = 0)
        {
            MenuEntries.Add((menuEntryId, label, location, order, activate));
        }

        public void ShowNotice(string text, int durationTicks = 200, bool playSound = true)
        {
            Notices.Add((text, durationTicks, playSound));
        }

        public void ShowOverlayMenu(string title, string subtitle, string breadcrumb, IReadOnlyList<string> entries)
        {
            OverlayMenu = new FakeOverlayMenuState(title, subtitle, breadcrumb, entries.ToArray());
        }

        public void ShowOverlayPanel(ClientPluginOverlayPanel panel)
        {
            OverlayPanel = panel;
        }

        public void HideOverlayMenu()
        {
            OverlayMenu = null;
            OverlayPanel = null;
        }
    }

    private sealed record FakeOverlayMenuState(
        string Title,
        string Subtitle,
        string Breadcrumb,
        IReadOnlyList<string> Entries);

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

        public FakeServerCvarRegistry CvarImpl { get; } = new();

        public FakeServerScheduler SchedulerImpl { get; } = new();

        public List<(byte Slot, string TargetPluginId, string MessageType, string Payload, PluginMessagePayloadFormat PayloadFormat, ushort SchemaVersion)> SentPluginMessages { get; } = [];

        public List<(string TargetPluginId, string MessageType, string Payload, PluginMessagePayloadFormat PayloadFormat, ushort SchemaVersion)> BroadcastPluginMessages { get; } = [];

        public List<(string ValueKind, byte Slot, string StateKey, object Value)> ReplicatedStateWrites { get; } = [];

        public List<(byte Slot, string StateKey)> ClearedReplicatedStateKeys { get; } = [];

        public List<(int PlayerId, float VelocityX, float VelocityY)> GameplayImpulses { get; } = [];

        public List<(int PlayerId, string CooldownKey, int Ticks)> GameplayAbilityCooldowns { get; } = [];

        public List<(int TargetPlayerId, float Amount, int? AttackerPlayerId, string? WeaponSpriteName)> GameplayDamageRequests { get; } = [];

        public List<(int PlayerId, float Amount)> GameplayHealingRequests { get; } = [];

        public List<(int PlayerId, string StatusEffectId, int Ticks, float Value)> GameplayStatusEffectRequests { get; } = [];

        public List<GameplayProjectileSpawnRequest> GameplayProjectileSpawnRequests { get; } = [];

        public List<GameplayAbilityRegistration> RegisteredGameplayAbilities { get; } = [];

        public List<(string ItemId, GameplayAbilityPatch Patch)> GameplayAbilityOverrides { get; } = [];

        public List<(string ExecutorId, IGameplayAbilityExecutor Executor)> RegisteredGameplayAbilityExecutors { get; } = [];

        public List<(string BehaviorId, IGameplayPrimaryWeaponExecutor Executor, string? FireSoundName)> RegisteredGameplayPrimaryWeaponBehaviors { get; } = [];

        public List<GameplayWeaponItemRegistration> RegisteredGameplayWeaponItems { get; } = [];

        public List<GameplayLoadoutRegistration> RegisteredGameplayLoadouts { get; } = [];

        public List<GameplaySlotItemRegistration> RegisteredGameplaySlotItems { get; } = [];

        public PluginCommandRegistry CommandRegistry { get; } = new();

        public List<(IOpenGarrisonServerCommand Command, OpenGarrisonServerAdminPermissions RequiredPermissions, IReadOnlyList<string> Aliases)> RegisteredCommands { get; } = [];

        public IOpenGarrisonServerReadOnlyState ServerState => StateImpl;

        public IOpenGarrisonServerAdminOperations AdminOperations => AdminImpl;

        public IOpenGarrisonServerCvarRegistry Cvars => CvarImpl;

        public IOpenGarrisonServerScheduler Scheduler => SchedulerImpl;

        public void BroadcastMessageToClients(string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
        {
            BroadcastPluginMessages.Add((targetPluginId, messageType, payload, payloadFormat, schemaVersion));
        }

        public bool ClearPlayerReplicatedState(byte slot, string stateKey)
        {
            ClearedReplicatedStateKeys.Add((slot, stateKey));
            return true;
        }

        public bool TryApplyGameplayImpulse(int playerId, float velocityX, float velocityY)
        {
            GameplayImpulses.Add((playerId, velocityX, velocityY));
            return true;
        }

        public bool TrySetGameplayAbilityCooldown(int playerId, string cooldownKey, int ticks)
        {
            GameplayAbilityCooldowns.Add((playerId, cooldownKey, ticks));
            return true;
        }

        public bool TryApplyGameplayDamage(int targetPlayerId, float amount, int? attackerPlayerId = null, string? weaponSpriteName = null)
        {
            GameplayDamageRequests.Add((targetPlayerId, amount, attackerPlayerId, weaponSpriteName));
            return true;
        }

        public bool TryApplyGameplayHealing(int playerId, float amount)
        {
            GameplayHealingRequests.Add((playerId, amount));
            return true;
        }

        public bool TryApplyGameplayStatusEffect(int playerId, string statusEffectId, int ticks, float value = 0f)
        {
            GameplayStatusEffectRequests.Add((playerId, statusEffectId, ticks, value));
            return true;
        }

        public bool TrySpawnGameplayProjectile(GameplayProjectileSpawnRequest request, out int projectileId)
        {
            GameplayProjectileSpawnRequests.Add(request);
            projectileId = GameplayProjectileSpawnRequests.Count;
            return true;
        }

        public void Log(string message) => _logs.Add(message);

        public void RegisterCommand(IOpenGarrisonServerCommand command, OpenGarrisonServerAdminPermissions requiredPermissions)
        {
            RegisterCommand(command, requiredPermissions, []);
        }

        public void RegisterCommand(IOpenGarrisonServerCommand command, OpenGarrisonServerAdminPermissions requiredPermissions, IReadOnlyList<string> aliases)
        {
            RegisteredCommands.Add((command, requiredPermissions, aliases));
            CommandRegistry.RegisterPluginCommand(command, PluginId, requiredPermissions, aliases);
        }

        public void SendMessageToClient(byte slot, string targetPluginId, string messageType, string payload, PluginMessagePayloadFormat payloadFormat, ushort schemaVersion)
        {
            SentPluginMessages.Add((slot, targetPluginId, messageType, payload, payloadFormat, schemaVersion));
        }

        public bool SetPlayerReplicatedStateBool(byte slot, string stateKey, bool value)
        {
            ReplicatedStateWrites.Add(("bool", slot, stateKey, value));
            return true;
        }

        public bool SetPlayerReplicatedStateFloat(byte slot, string stateKey, float value)
        {
            ReplicatedStateWrites.Add(("float", slot, stateKey, value));
            return true;
        }

        public bool SetPlayerReplicatedStateInt(byte slot, string stateKey, int value)
        {
            ReplicatedStateWrites.Add(("int", slot, stateKey, value));
            return true;
        }

        public bool TryRegisterGameplayAbility(GameplayAbilityRegistration registration, out string errorMessage)
        {
            RegisteredGameplayAbilities.Add(registration);
            errorMessage = string.Empty;
            return true;
        }

        public bool TryOverrideGameplayAbility(string itemId, GameplayAbilityPatch patch, out string errorMessage)
        {
            GameplayAbilityOverrides.Add((itemId, patch));
            errorMessage = string.Empty;
            return true;
        }

        public bool TryRegisterGameplayAbilityExecutor(string executorId, IGameplayAbilityExecutor executor, out string errorMessage)
        {
            RegisteredGameplayAbilityExecutors.Add((executorId, executor));
            errorMessage = string.Empty;
            return true;
        }

        public bool TryRegisterGameplayPrimaryWeaponBehavior(string behaviorId, IGameplayPrimaryWeaponExecutor executor, string? fireSoundName, out string errorMessage)
        {
            RegisteredGameplayPrimaryWeaponBehaviors.Add((behaviorId, executor, fireSoundName));
            errorMessage = string.Empty;
            return true;
        }

        public bool TryRegisterGameplayWeaponItem(GameplayWeaponItemRegistration registration, out string errorMessage)
        {
            RegisteredGameplayWeaponItems.Add(registration);
            errorMessage = string.Empty;
            return true;
        }

        public bool TryRegisterGameplayLoadout(GameplayLoadoutRegistration registration, out string errorMessage)
        {
            RegisteredGameplayLoadouts.Add(registration);
            errorMessage = string.Empty;
            return true;
        }

        public bool TryRegisterGameplaySlotItem(GameplaySlotItemRegistration registration, out string errorMessage)
        {
            RegisteredGameplaySlotItems.Add(registration);
            errorMessage = string.Empty;
            return true;
        }
    }

    private sealed class FakeServerReadOnlyState : IOpenGarrisonServerReadOnlyState
    {
        public string ServerName => "Test Server";
        public string LevelName => "ctf_test";
        public int MapAreaIndex => 1;
        public int MapAreaCount => 1;
        public float MapScale => 1f;
        public GameModeKind GameMode => GameModeKind.CaptureTheFlag;
        public MatchPhase MatchPhase => MatchPhase.Running;
        public int RedCaps => 0;
        public int BlueCaps => 0;
        public readonly List<OpenGarrisonServerPlayerInfo> Players = [];
        public readonly List<OpenGarrisonServerGameplayAbilityInfo> Abilities = [];
        public OpenGarrisonServerObjectiveStateInfo Objectives = new([], [], []);
        public readonly List<OpenGarrisonServerBuildableInfo> Buildables = [];
        public readonly List<OpenGarrisonServerProjectileInfo> Projectiles = [];
        public readonly List<OpenGarrisonServerRecentEventInfo> RecentEvents = [];
        public readonly Dictionary<(int PlayerId, string OwnerPluginId, string StateKey), int> IntReplicatedStates = [];
        public readonly Dictionary<(int PlayerId, string OwnerPluginId, string StateKey), float> FloatReplicatedStates = [];
        public readonly Dictionary<(int PlayerId, string OwnerPluginId, string StateKey), bool> BoolReplicatedStates = [];

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

        public IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetGameplayAbilities() => Abilities;

        public IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetPlayerGameplayAbilities(int playerId) => Abilities;

        public bool TryGetPlayerGameplayAbility(int playerId, string category, out OpenGarrisonServerGameplayAbilityInfo ability)
        {
            ability = Abilities.FirstOrDefault(candidate => string.Equals(candidate.Category, category, StringComparison.Ordinal));
            return !string.IsNullOrWhiteSpace(ability.ItemId);
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
                    IsSelected: false,
                    IsAvailableToPlayer: true))
                .ToArray();
        }

        public IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers() => Players;

        public OpenGarrisonServerObjectiveStateInfo GetObjectives() => Objectives;

        public IReadOnlyList<OpenGarrisonServerBuildableInfo> GetBuildables() => Buildables;

        public IReadOnlyList<OpenGarrisonServerProjectileInfo> GetProjectiles() => Projectiles;

        public IReadOnlyList<OpenGarrisonServerRecentEventInfo> GetRecentEvents() => RecentEvents;

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

        public bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value)
        {
            return BoolReplicatedStates.TryGetValue((playerId, ownerPluginId, stateKey), out value);
        }

        public bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value)
        {
            return FloatReplicatedStates.TryGetValue((playerId, ownerPluginId, stateKey), out value);
        }

        public bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value)
        {
            return IntReplicatedStates.TryGetValue((playerId, ownerPluginId, stateKey), out value);
        }
    }

    private sealed class FakeServerAdminOperations : IOpenGarrisonServerAdminOperations
    {
        public readonly List<(string SelectionKind, byte Slot, string? ItemId)> GameplayItemSelections = [];

        public readonly List<(string SelectionKind, byte Slot, string? ItemId)> GameplayItemSelectionAttempts = [];

        public readonly List<(string ChangeKind, byte Slot, string ItemId)> GameplayOwnershipChanges = [];

        public readonly List<string> BroadcastSystemMessages = [];

        public readonly List<(byte Slot, string Text)> SystemMessages = [];

        public readonly List<(byte Slot, string Reason)> DisconnectRequests = [];

        public readonly List<(byte Slot, TimeSpan? Duration, string Reason)> BanPlayerRequests = [];

        public readonly List<(string Address, TimeSpan? Duration, string Reason)> BanIpRequests = [];

        public readonly List<string> UnbanIpRequests = [];

        public readonly List<byte> ForceKillRequests = [];

        public readonly List<(byte Slot, float DurationSeconds)> IgniteRequests = [];

        public readonly List<(byte Slot, float Scale)> ScaleRequests = [];

        public readonly List<(byte Slot, float Scale)> MovementSpeedScaleRequests = [];

        public readonly List<byte> ClearedMovementSpeedScaleSlots = [];

        public readonly List<(byte Slot, float Scale)> GravityScaleRequests = [];

        public readonly List<byte> ClearedGravityScaleSlots = [];

        public readonly List<(byte Slot, bool IsGagged)> GagRequests = [];

        public readonly List<(byte Slot, string NewName)> RenameRequests = [];

        public readonly List<(string LevelName, int AreaIndex, bool PreservePlayerStats)> MapChangeRequests = [];

        public readonly List<(string LevelName, int AreaIndex)> NextRoundMapRequests = [];

        public readonly List<(byte Slot, PlayerTeam Team, PlayerClass PlayerClass, string DisplayName)> AddBotRequests = [];

        public readonly List<byte> RemoveBotRequests = [];

        public readonly List<(int TargetPerTeam, PlayerClass? RequestedClass)> FillBotRequests = [];

        public readonly List<(PlayerTeam Team, int TargetCount, PlayerClass? RequestedClass)> FillBotTeamRequests = [];

        public int ClearBotsCallCount { get; private set; }

        public void BroadcastSystemMessage(string text)
        {
            BroadcastSystemMessages.Add(text);
        }

        public void SendSystemMessage(byte slot, string text)
        {
            SystemMessages.Add((slot, text));
        }

        public bool TryRenamePlayer(byte slot, string newName)
        {
            RenameRequests.Add((slot, newName));
            return true;
        }

        public bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false)
        {
            MapChangeRequests.Add((levelName, mapAreaIndex, preservePlayerStats));
            return true;
        }

        public bool TryDisconnect(byte slot, string reason)
        {
            DisconnectRequests.Add((slot, reason));
            return true;
        }

        public OpenGarrisonServerBanActionResult TryBanPlayer(byte slot, TimeSpan? duration, string reason)
        {
            BanPlayerRequests.Add((slot, duration, reason));
            return new OpenGarrisonServerBanActionResult(true, $"127.0.0.{slot}", string.Empty, !duration.HasValue, duration.HasValue ? DateTimeOffset.UtcNow.Add(duration.Value).ToUnixTimeSeconds() : 0);
        }

        public OpenGarrisonServerBanActionResult TryBanIpAddress(string ipAddress, TimeSpan? duration, string reason)
        {
            BanIpRequests.Add((ipAddress, duration, reason));
            return new OpenGarrisonServerBanActionResult(true, ipAddress, string.Empty, !duration.HasValue, duration.HasValue ? DateTimeOffset.UtcNow.Add(duration.Value).ToUnixTimeSeconds() : 0);
        }

        public OpenGarrisonServerAddressActionResult TryUnbanIpAddress(string ipAddress)
        {
            UnbanIpRequests.Add(ipAddress);
            return new OpenGarrisonServerAddressActionResult(true, ipAddress, string.Empty);
        }

        public bool TrySetPlayerGagged(byte slot, bool isGagged)
        {
            GagRequests.Add((slot, isGagged));
            return true;
        }

        public bool TryForceKill(byte slot)
        {
            ForceKillRequests.Add(slot);
            return true;
        }

        public bool TryIgnitePlayer(byte slot, float durationSeconds)
        {
            IgniteRequests.Add((slot, durationSeconds));
            return true;
        }

        public bool TrySetPlayerScale(byte slot, float scale)
        {
            ScaleRequests.Add((slot, scale));
            return true;
        }

        public bool TrySetPlayerMovementSpeedScale(byte slot, float scale)
        {
            MovementSpeedScaleRequests.Add((slot, scale));
            return true;
        }

        public bool TryClearPlayerMovementSpeedScale(byte slot)
        {
            ClearedMovementSpeedScaleSlots.Add(slot);
            return true;
        }

        public bool TrySetPlayerGravityScale(byte slot, float scale)
        {
            GravityScaleRequests.Add((slot, scale));
            return true;
        }

        public bool TryClearPlayerGravityScale(byte slot)
        {
            ClearedGravityScaleSlots.Add(slot);
            return true;
        }

        public bool TrySetTimeLimit(int timeLimitMinutes) => true;

        public bool TryMoveToSpectator(byte slot) => true;

        public bool TrySetCapLimit(int capLimit) => true;

        public bool TrySetRespawnSeconds(int respawnSeconds) => true;

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

        public bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1)
        {
            NextRoundMapRequests.Add((levelName, mapAreaIndex));
            return true;
        }

        public bool TrySetTeam(byte slot, PlayerTeam team) => true;

        public bool TryAddBot(byte slot, PlayerTeam team, PlayerClass playerClass, string displayName)
        {
            AddBotRequests.Add((slot, team, playerClass, displayName));
            return true;
        }

        public bool TryRemoveBot(byte slot)
        {
            RemoveBotRequests.Add(slot);
            return true;
        }

        public bool TrySetBotTeam(byte slot, PlayerTeam team) => true;

        public bool TrySetBotClass(byte slot, PlayerClass playerClass) => true;

        public int TryFillBots(int targetPerTeam, PlayerClass? requestedClass)
        {
            FillBotRequests.Add((targetPerTeam, requestedClass));
            return targetPerTeam * 2;
        }

        public int TryFillBotTeam(PlayerTeam team, int targetCount, PlayerClass? requestedClass)
        {
            FillBotTeamRequests.Add((team, targetCount, requestedClass));
            return targetCount;
        }

        public IReadOnlyList<OpenGarrisonServerBotSlotInfo> GetBotSlots() => [];

        public int TryClearAllBots()
        {
            ClearBotsCallCount += 1;
            return 0;
        }

        public string GetDemoRecordingStatus() => "[server] demo | status=idle";

        public OpenGarrisonServerDemoRecordingResult TryStartDemoRecording(string? requestedPath) => new(true, "[server] demo recording started: test.ogdemo", string.Empty);

        public OpenGarrisonServerDemoRecordingResult TryStopDemoRecording() => new(true, "[server] demo recording stopped: test.ogdemo", string.Empty);
    }

    private sealed class FakeServerCvarRegistry : IOpenGarrisonServerCvarRegistry
    {
        private readonly Dictionary<string, OpenGarrisonServerCvarInfo> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _runtimeProtectedNames = new(StringComparer.OrdinalIgnoreCase);

        public void Add(OpenGarrisonServerCvarInfo cvar)
        {
            _entries[cvar.Name] = cvar;
        }

        public IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll(bool includeProtectedValues)
        {
            return _entries.Values
                .Select(cvar => includeProtectedValues ? MarkProtected(cvar) : MaskProtectedValue(cvar))
                .ToArray();
        }

        public IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll()
        {
            return GetAll(includeProtectedValues: false);
        }

        public bool TryGet(string name, bool includeProtectedValue, out OpenGarrisonServerCvarInfo cvar)
        {
            if (!_entries.TryGetValue(name, out cvar))
            {
                return false;
            }

            cvar = includeProtectedValue ? MarkProtected(cvar) : MaskProtectedValue(cvar);
            return true;
        }

        public bool TryGet(string name, out OpenGarrisonServerCvarInfo cvar)
        {
            return TryGet(name, includeProtectedValue: false, out cvar);
        }

        public bool TrySet(string name, string value, bool allowProtectedMutation, out OpenGarrisonServerCvarInfo cvar, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!_entries.TryGetValue(name, out cvar))
            {
                errorMessage = "unknown cvar";
                return false;
            }

            if (cvar.IsReadOnly)
            {
                errorMessage = "readonly";
                return false;
            }

            if (IsProtected(cvar.Name) && !allowProtectedMutation)
            {
                errorMessage = "protected";
                cvar = MaskProtectedValue(cvar);
                return false;
            }

            cvar = cvar with { CurrentValue = value };
            _entries[name] = cvar;
            cvar = allowProtectedMutation ? MarkProtected(cvar) : MaskProtectedValue(cvar);
            return true;
        }

        public bool TrySet(string name, string value, out OpenGarrisonServerCvarInfo cvar, out string errorMessage)
        {
            return TrySet(name, value, allowProtectedMutation: false, out cvar, out errorMessage);
        }

        public bool TryProtect(string name, out OpenGarrisonServerCvarInfo cvar, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!_entries.TryGetValue(name, out cvar))
            {
                errorMessage = "unknown cvar";
                return false;
            }

            _runtimeProtectedNames.Add(cvar.Name);
            cvar = MaskProtectedValue(cvar);
            return true;
        }

        private bool IsProtected(string name)
        {
            return _runtimeProtectedNames.Contains(name)
                || (_entries.TryGetValue(name, out var cvar) && cvar.IsProtected);
        }

        private OpenGarrisonServerCvarInfo MarkProtected(OpenGarrisonServerCvarInfo cvar)
        {
            return IsProtected(cvar.Name)
                ? cvar with { IsProtected = true }
                : cvar;
        }

        private OpenGarrisonServerCvarInfo MaskProtectedValue(OpenGarrisonServerCvarInfo cvar)
        {
            cvar = MarkProtected(cvar);
            return cvar.IsProtected
                ? cvar with { CurrentValue = "<protected>" }
                : cvar;
        }
    }

    private sealed class FakeServerScheduler : IOpenGarrisonServerScheduler
    {
        private readonly Dictionary<Guid, Action> _callbacks = [];
        public readonly List<OpenGarrisonServerScheduledTaskInfo> Tasks = [];

        public TimeSpan Uptime => TimeSpan.Zero;

        public Guid ScheduleOnce(TimeSpan delay, Action callback, string? description = null)
        {
            var timerId = Guid.NewGuid();
            Tasks.Add(new OpenGarrisonServerScheduledTaskInfo(timerId, description ?? string.Empty, false, delay, delay));
            _callbacks[timerId] = callback;
            return timerId;
        }

        public Guid ScheduleRepeating(TimeSpan interval, Action callback, string? description = null, bool runImmediately = false)
        {
            var timerId = Guid.NewGuid();
            Tasks.Add(new OpenGarrisonServerScheduledTaskInfo(timerId, description ?? string.Empty, true, interval, runImmediately ? TimeSpan.Zero : interval));
            _callbacks[timerId] = callback;
            return timerId;
        }

        public bool Cancel(Guid timerId)
        {
            _callbacks.Remove(timerId);
            return Tasks.RemoveAll(task => task.TimerId == timerId) > 0;
        }

        public bool IsScheduled(Guid timerId)
        {
            return Tasks.Any(task => task.TimerId == timerId);
        }

        public IReadOnlyList<OpenGarrisonServerScheduledTaskInfo> GetScheduledTasks() => Tasks;

        public void RunAll()
        {
            foreach (var task in Tasks.ToArray())
            {
                if (_callbacks.TryGetValue(task.TimerId, out var callback))
                {
                    callback();
                    if (!task.IsRepeating)
                    {
                        Cancel(task.TimerId);
                    }
                }
            }
        }
    }
}
