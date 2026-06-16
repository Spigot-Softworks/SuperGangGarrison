using OpenGarrison.Client.Plugins;
using OpenGarrison.Client.Plugins.BubbleWheel;
using OpenGarrison.Core;
using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BubbleWheelPluginTests
{
    private const float RedTeamBubbleAimDegrees = -70f;

    [Fact]
    public void DigitOneOnXMenuMainPageOpensRedTeamSubmenuWithoutImmediateClassSelection()
    {
        var plugin = CreatePlugin(BubbleWheelBehavior.HoldAndHover);

        var pageSwitchResult = plugin.TryHandleBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.X,
            XPageIndex: 0,
            AimDirectionDegrees: 160f,
            DistanceFromCenter: 60f,
            LeftMousePressed: false,
            LeftMouseDown: false,
            LeftMouseReleased: false,
            PressedDigit: 1,
            QPressed: false));
        Assert.NotNull(pageSwitchResult);
        Assert.Equal(1, pageSwitchResult!.NewXPageIndex);
        Assert.True(pageSwitchResult.ClearBubbleSelection);
        Assert.Null(pageSwitchResult.BubbleFrame);

        var hoverAfterPageSwitch = plugin.TryHandleBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.X,
            XPageIndex: 1,
            AimDirectionDegrees: 160f,
            DistanceFromCenter: 60f,
            LeftMousePressed: false,
            LeftMouseDown: false,
            LeftMouseReleased: false,
            PressedDigit: null,
            QPressed: false));
        Assert.Null(hoverAfterPageSwitch);
    }

    [Fact]
    public void HoveringTeamBubbleOnXMenuMainPageOpensSubmenuInHoldAndHoverMode()
    {
        var plugin = CreatePlugin(BubbleWheelBehavior.HoldAndHover);

        var pageSwitchResult = plugin.TryHandleBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.X,
            XPageIndex: 0,
            AimDirectionDegrees: RedTeamBubbleAimDegrees,
            DistanceFromCenter: 60f,
            LeftMousePressed: false,
            LeftMouseDown: false,
            LeftMouseReleased: false,
            PressedDigit: null,
            QPressed: false));
        Assert.NotNull(pageSwitchResult);
        Assert.Equal(1, pageSwitchResult!.NewXPageIndex);
        Assert.True(pageSwitchResult.ClearBubbleSelection);
    }

    [Fact]
    public void HoveringTeamBubbleOnXMenuMainPageRequiresClickInPressAndClickMode()
    {
        var plugin = CreatePlugin(BubbleWheelBehavior.PressAndClick);

        var hoverResult = plugin.TryHandleBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.X,
            XPageIndex: 0,
            AimDirectionDegrees: RedTeamBubbleAimDegrees,
            DistanceFromCenter: 60f,
            LeftMousePressed: false,
            LeftMouseDown: false,
            LeftMouseReleased: false,
            PressedDigit: null,
            QPressed: false));
        Assert.NotNull(hoverResult);
        Assert.Null(hoverResult!.NewXPageIndex);
        Assert.True(hoverResult.ClearBubbleSelection);

        var clickResult = plugin.TryHandleBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.X,
            XPageIndex: 0,
            AimDirectionDegrees: RedTeamBubbleAimDegrees,
            DistanceFromCenter: 60f,
            LeftMousePressed: true,
            LeftMouseDown: true,
            LeftMouseReleased: false,
            PressedDigit: null,
            QPressed: false));
        Assert.NotNull(clickResult);
        Assert.Equal(1, clickResult!.NewXPageIndex);
    }

    [Fact]
    public void DigitThreeOnXMenuSubmenuSelectsClassPortrait()
    {
        var plugin = CreatePlugin(BubbleWheelBehavior.HoldAndHover);
        _ = plugin.TryHandleBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.X,
            XPageIndex: 0,
            AimDirectionDegrees: RedTeamBubbleAimDegrees,
            DistanceFromCenter: 60f,
            LeftMousePressed: false,
            LeftMouseDown: false,
            LeftMouseReleased: false,
            PressedDigit: 1,
            QPressed: false));

        var classSelection = plugin.TryHandleBubbleMenuInput(new ClientBubbleMenuInputState(
            ClientBubbleMenuKind.X,
            XPageIndex: 1,
            AimDirectionDegrees: RedTeamBubbleAimDegrees,
            DistanceFromCenter: 60f,
            LeftMousePressed: false,
            LeftMouseDown: false,
            LeftMouseReleased: false,
            PressedDigit: 3,
            QPressed: false));
        Assert.NotNull(classSelection);
        Assert.Equal(2, classSelection!.BubbleFrame);
    }

    private static BubbleWheelPlugin CreatePlugin(BubbleWheelBehavior behavior)
    {
        var plugin = new BubbleWheelPlugin();
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var configPath = Path.Combine(tempDirectory, BubbleWheelPluginConfig.DefaultFileName);
        BubbleWheelPluginConfig.Save(configPath, new BubbleWheelPluginConfig { Behavior = behavior });

        plugin.Initialize(new TestClientPluginContext(tempDirectory));
        return plugin;
    }

    private sealed class TestClientPluginContext : IOpenGarrisonClientPluginContext
    {
        public TestClientPluginContext(string configDirectory)
        {
            ConfigDirectory = configDirectory;
            Assets = new NullClientPluginAssets();
            Hotkeys = new NullClientPluginHotkeys();
            Ui = new NullClientPluginUi();
        }

        public string PluginId => "bubblewheel";

        public string PluginDirectory => ConfigDirectory;

        public string ConfigDirectory { get; }

        public OpenGarrisonPluginManifest Manifest { get; } = new()
        {
            Id = "bubblewheel",
            DisplayName = "Bubble Wheel",
            Version = "1.0.0",
            Type = OpenGarrisonPluginType.Client,
        };

        public OpenGarrisonPluginHostApi HostApi { get; } = OpenGarrisonPluginHostApi.CreateClientDefault();

        public Microsoft.Xna.Framework.Graphics.GraphicsDevice GraphicsDevice => null!;

        public IOpenGarrisonClientReadOnlyState ClientState => null!;

        public IOpenGarrisonClientPluginAssets Assets { get; }

        public IOpenGarrisonClientPluginHotkeys Hotkeys { get; }

        public IOpenGarrisonClientPluginUi Ui { get; }

        public void Log(string message)
        {
        }

        public void SendMessageToServer(
            string targetPluginId,
            string messageType,
            string payload,
            PluginMessagePayloadFormat payloadFormat,
            ushort schemaVersion)
        {
        }
    }

    private sealed class NullClientPluginAssets : IOpenGarrisonClientPluginAssets
    {
        public void RegisterTextureAsset(string assetId, string relativePath)
        {
        }

        public void RegisterTextureAtlasAsset(string assetId, string relativePath, int frameWidth, int frameHeight)
        {
        }

        public void RegisterTextureRegionAsset(string assetId, string textureAssetId, Microsoft.Xna.Framework.Rectangle sourceRectangle)
        {
        }

        public void RegisterSoundAsset(string assetId, string relativePath)
        {
        }

        public bool TryGetTextureAsset(string assetId, out Microsoft.Xna.Framework.Graphics.Texture2D texture)
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

        public bool TryGetSoundAsset(string assetId, out Microsoft.Xna.Framework.Audio.SoundEffect sound)
        {
            sound = null!;
            return false;
        }
    }

    private sealed class NullClientPluginHotkeys : IOpenGarrisonClientPluginHotkeys
    {
        public Microsoft.Xna.Framework.Input.Keys RegisterHotkey(string hotkeyId, string displayName, Microsoft.Xna.Framework.Input.Keys defaultKey)
        {
            return defaultKey;
        }

        public bool WasHotkeyPressed(string hotkeyId)
        {
            return false;
        }

        public void SetHotkeyCaptureEnabled(bool enabled)
        {
        }
    }

    private sealed class NullClientPluginUi : IOpenGarrisonClientPluginUi
    {
        public void RegisterMenuEntry(string menuEntryId, string label, ClientPluginMenuLocation location, Action activate, int order = 0)
        {
        }

        public void ShowNotice(string text, int durationTicks = 200, bool playSound = true)
        {
        }

        public void ShowOverlayMenu(string title, string subtitle, string breadcrumb, IReadOnlyList<string> entries)
        {
        }

        public void ShowOverlayPanel(ClientPluginOverlayPanel panel)
        {
        }

        public void HideOverlayMenu()
        {
        }
    }
}
