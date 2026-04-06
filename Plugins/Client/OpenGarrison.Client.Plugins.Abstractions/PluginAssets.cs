using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client.Plugins;

public interface IOpenGarrisonClientHudOrderHooks
{
    int GameplayHudOrder { get; }
}

public interface IOpenGarrisonClientPluginAssets
{
    void RegisterTextureAsset(string assetId, string relativePath);

    bool TryGetTextureAsset(string assetId, out Texture2D texture);

    void RegisterSoundAsset(string assetId, string relativePath);

    bool TryGetSoundAsset(string assetId, out SoundEffect sound);
}

public interface IOpenGarrisonClientPluginHotkeys
{
    Keys RegisterHotkey(string hotkeyId, string displayName, Keys defaultKey);

    bool WasHotkeyPressed(string hotkeyId);
}
