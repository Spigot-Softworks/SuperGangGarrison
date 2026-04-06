using OpenGarrison.Client.Plugins;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

internal sealed class ClientPluginContext(
    string pluginId,
    string pluginDirectory,
    string configDirectory,
    OpenGarrisonPluginManifest manifest,
    OpenGarrisonPluginHostApi hostApi,
    GraphicsDevice graphicsDevice,
    IOpenGarrisonClientReadOnlyState clientState,
    ClientPluginAssetRegistry assetRegistry,
    Func<string, string, Keys, Keys> registerHotkey,
    Func<string, bool> wasHotkeyPressed,
    Action<string, string, ClientPluginMenuLocation, Action, int> registerMenuEntry,
    Action<string, int, bool> showNotice,
    Action<string, string, string, PluginMessagePayloadFormat, ushort> sendMessageToServer,
    Action<string> log) : IOpenGarrisonClientPluginContext, IDisposable
{
    public string PluginId { get; } = pluginId;

    public string PluginDirectory { get; } = pluginDirectory;

    public string ConfigDirectory { get; } = configDirectory;

    public OpenGarrisonPluginManifest Manifest { get; } = manifest;

    public OpenGarrisonPluginHostApi HostApi { get; } = hostApi;

    public GraphicsDevice GraphicsDevice { get; } = graphicsDevice;

    public IOpenGarrisonClientReadOnlyState ClientState { get; } = clientState;

    public IOpenGarrisonClientPluginAssets Assets { get; } = new AssetAccess(assetRegistry);

    public IOpenGarrisonClientPluginHotkeys Hotkeys { get; } = new HotkeyAccess(registerHotkey, wasHotkeyPressed);

    public IOpenGarrisonClientPluginUi Ui { get; } = new UiAccess(registerMenuEntry, showNotice);

    public void SendMessageToServer(
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion)
    {
        sendMessageToServer(targetPluginId, messageType, payload, payloadFormat, schemaVersion);
    }

    public void Log(string message)
    {
        log($"[plugin:{PluginId}] {message}");
    }

    public void Dispose()
    {
        assetRegistry.Dispose();
    }

    private sealed class AssetAccess(ClientPluginAssetRegistry assetRegistry) : IOpenGarrisonClientPluginAssets
    {
        public void RegisterTextureAsset(string assetId, string relativePath)
        {
            assetRegistry.RegisterTextureAsset(assetId, relativePath);
        }

        public bool TryGetTextureAsset(string assetId, out Texture2D texture)
        {
            return assetRegistry.TryGetTextureAsset(assetId, out texture);
        }

        public void RegisterTextureAtlasAsset(string assetId, string relativePath, int frameWidth, int frameHeight)
        {
            assetRegistry.RegisterTextureAtlasAsset(assetId, relativePath, frameWidth, frameHeight);
        }

        public bool TryGetTextureAtlasAsset(string assetId, out ClientPluginTextureAtlas atlas)
        {
            return assetRegistry.TryGetTextureAtlasAsset(assetId, out atlas);
        }

        public void RegisterTextureRegionAsset(string assetId, string textureAssetId, Rectangle sourceRectangle)
        {
            assetRegistry.RegisterTextureRegionAsset(assetId, textureAssetId, sourceRectangle);
        }

        public bool TryGetTextureRegionAsset(string assetId, out ClientPluginTextureRegion region)
        {
            return assetRegistry.TryGetTextureRegionAsset(assetId, out region);
        }

        public void RegisterSoundAsset(string assetId, string relativePath)
        {
            assetRegistry.RegisterSoundAsset(assetId, relativePath);
        }

        public bool TryGetSoundAsset(string assetId, out SoundEffect sound)
        {
            return assetRegistry.TryGetSoundAsset(assetId, out sound);
        }
    }

    private sealed class HotkeyAccess(
        Func<string, string, Keys, Keys> registerHotkey,
        Func<string, bool> wasHotkeyPressed) : IOpenGarrisonClientPluginHotkeys
    {
        public Keys RegisterHotkey(string hotkeyId, string displayName, Keys defaultKey)
        {
            return registerHotkey(hotkeyId, displayName, defaultKey);
        }

        public bool WasHotkeyPressed(string hotkeyId)
        {
            return wasHotkeyPressed(hotkeyId);
        }
    }

    private sealed class UiAccess(
        Action<string, string, ClientPluginMenuLocation, Action, int> registerMenuEntry,
        Action<string, int, bool> showNotice) : IOpenGarrisonClientPluginUi
    {
        public void RegisterMenuEntry(string menuEntryId, string label, ClientPluginMenuLocation location, Action activate, int order = 0)
        {
            registerMenuEntry(menuEntryId, label, location, activate, order);
        }

        public void ShowNotice(string text, int durationTicks = 200, bool playSound = true)
        {
            showNotice(text, durationTicks, playSound);
        }
    }
}
