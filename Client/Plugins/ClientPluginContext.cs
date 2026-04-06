using OpenGarrison.Client.Plugins;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

internal sealed class ClientPluginContext(
    string pluginId,
    string pluginDirectory,
    string configDirectory,
    GraphicsDevice graphicsDevice,
    IOpenGarrisonClientReadOnlyState clientState,
    ClientPluginAssetRegistry assetRegistry,
    Func<string, string, Keys, Keys> registerHotkey,
    Func<string, bool> wasHotkeyPressed,
    Action<string, string, ClientPluginMenuLocation, Action> registerMenuEntry,
    Action<string, int, bool> showNotice,
    Action<string, string, string> sendMessageToServer,
    Action<string> log) : IOpenGarrisonClientPluginContext, IDisposable
{
    public string PluginId { get; } = pluginId;

    public string PluginDirectory { get; } = pluginDirectory;

    public string ConfigDirectory { get; } = configDirectory;

    public GraphicsDevice GraphicsDevice { get; } = graphicsDevice;

    public IOpenGarrisonClientReadOnlyState ClientState { get; } = clientState;

    public IOpenGarrisonClientPluginAssets Assets { get; } = new AssetAccess(assetRegistry);

    public IOpenGarrisonClientPluginHotkeys Hotkeys { get; } = new HotkeyAccess(registerHotkey, wasHotkeyPressed);

    public IOpenGarrisonClientPluginUi Ui { get; } = new UiAccess(registerMenuEntry, showNotice);

    public void SendMessageToServer(string targetPluginId, string messageType, string payload)
    {
        sendMessageToServer(targetPluginId, messageType, payload);
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
        Action<string, string, ClientPluginMenuLocation, Action> registerMenuEntry,
        Action<string, int, bool> showNotice) : IOpenGarrisonClientPluginUi
    {
        public void RegisterMenuEntry(string menuEntryId, string label, ClientPluginMenuLocation location, Action activate)
        {
            registerMenuEntry(menuEntryId, label, location, activate);
        }

        public void ShowNotice(string text, int durationTicks = 200, bool playSound = true)
        {
            showNotice(text, durationTicks, playSound);
        }
    }
}
