using Microsoft.Xna.Framework;
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

    void RegisterTextureAtlasAsset(string assetId, string relativePath, int frameWidth, int frameHeight);

    bool TryGetTextureAtlasAsset(string assetId, out ClientPluginTextureAtlas atlas);

    void RegisterTextureRegionAsset(string assetId, string textureAssetId, Rectangle sourceRectangle);

    bool TryGetTextureRegionAsset(string assetId, out ClientPluginTextureRegion region);

    void RegisterSoundAsset(string assetId, string relativePath);

    bool TryGetSoundAsset(string assetId, out SoundEffect sound);
}

public readonly record struct ClientPluginTextureAtlas(
    Texture2D Texture,
    int FrameWidth,
    int FrameHeight,
    int Columns,
    int Rows,
    int FrameCount)
{
    public bool TryGetFrameSourceRectangle(int frameIndex, out Rectangle sourceRectangle)
    {
        if (frameIndex < 0 || frameIndex >= FrameCount || Columns <= 0 || Rows <= 0)
        {
            sourceRectangle = default;
            return false;
        }

        var column = frameIndex % Columns;
        var row = frameIndex / Columns;
        sourceRectangle = new Rectangle(column * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight);
        return true;
    }
}

public readonly record struct ClientPluginTextureRegion(
    Texture2D Texture,
    Rectangle SourceRectangle);

public interface IOpenGarrisonClientPluginHotkeys
{
    Keys RegisterHotkey(string hotkeyId, string displayName, Keys defaultKey);

    bool WasHotkeyPressed(string hotkeyId);
}
