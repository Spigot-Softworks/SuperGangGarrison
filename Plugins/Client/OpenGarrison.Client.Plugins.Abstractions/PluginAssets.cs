using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// Hooks a client plugin can implement to control its HUD draw order.
/// </summary>
public interface IOpenGarrisonClientHudOrderHooks
{
    /// <summary>
    /// Gets the plugin's gameplay HUD draw order (lower draws first).
    /// </summary>
    int GameplayHudOrder { get; }
}

/// <summary>
/// Asset registry plugins use to register and look up textures and sounds.
/// </summary>
public interface IOpenGarrisonClientPluginAssets
{
    /// <summary>
    /// Registers a texture asset from a path relative to the plugin directory.
    /// </summary>
    /// <param name="assetId">The asset id.</param>
    /// <param name="relativePath">The path relative to the plugin directory.</param>
    void RegisterTextureAsset(string assetId, string relativePath);

    /// <summary>
    /// Tries to get a registered texture asset.
    /// </summary>
    /// <param name="assetId">The asset id.</param>
    /// <param name="texture">The texture.</param>
    /// <returns>True when the asset is registered.</returns>
    bool TryGetTextureAsset(string assetId, out Texture2D texture);

    /// <summary>
    /// Registers a texture atlas asset from a path relative to the plugin directory.
    /// </summary>
    /// <param name="assetId">The asset id.</param>
    /// <param name="relativePath">The path relative to the plugin directory.</param>
    /// <param name="frameWidth">The frame width in pixels.</param>
    /// <param name="frameHeight">The frame height in pixels.</param>
    void RegisterTextureAtlasAsset(string assetId, string relativePath, int frameWidth, int frameHeight);

    /// <summary>
    /// Tries to get a registered texture atlas asset.
    /// </summary>
    /// <param name="assetId">The asset id.</param>
    /// <param name="atlas">The atlas.</param>
    /// <returns>True when the asset is registered.</returns>
    bool TryGetTextureAtlasAsset(string assetId, out ClientPluginTextureAtlas atlas);

    /// <summary>
    /// Registers a texture region asset referencing a sub-rectangle of a texture asset.
    /// </summary>
    /// <param name="assetId">The asset id.</param>
    /// <param name="textureAssetId">The source texture asset id.</param>
    /// <param name="sourceRectangle">The source rectangle within the texture.</param>
    void RegisterTextureRegionAsset(string assetId, string textureAssetId, Rectangle sourceRectangle);

    /// <summary>
    /// Tries to get a registered texture region asset.
    /// </summary>
    /// <param name="assetId">The asset id.</param>
    /// <param name="region">The region.</param>
    /// <returns>True when the asset is registered.</returns>
    bool TryGetTextureRegionAsset(string assetId, out ClientPluginTextureRegion region);

    /// <summary>
    /// Registers a sound asset from a path relative to the plugin directory.
    /// </summary>
    /// <param name="assetId">The asset id.</param>
    /// <param name="relativePath">The path relative to the plugin directory.</param>
    void RegisterSoundAsset(string assetId, string relativePath);

    /// <summary>
    /// Tries to get a registered sound asset.
    /// </summary>
    /// <param name="assetId">The asset id.</param>
    /// <param name="sound">The sound.</param>
    /// <returns>True when the asset is registered.</returns>
    bool TryGetSoundAsset(string assetId, out SoundEffect sound);
}

/// <summary>
/// A texture atlas whose frames are laid out in a uniform grid.
/// </summary>
/// <param name="Texture">The atlas texture.</param>
/// <param name="FrameWidth">The frame width in pixels.</param>
/// <param name="FrameHeight">The frame height in pixels.</param>
/// <param name="Columns">The number of columns.</param>
/// <param name="Rows">The number of rows.</param>
/// <param name="FrameCount">The number of frames.</param>
public readonly record struct ClientPluginTextureAtlas(
    Texture2D Texture,
    int FrameWidth,
    int FrameHeight,
    int Columns,
    int Rows,
    int FrameCount)
{
    /// <summary>
    /// Tries to get the source rectangle for a frame index.
    /// </summary>
    /// <param name="frameIndex">The frame index.</param>
    /// <param name="sourceRectangle">The source rectangle.</param>
    /// <returns>True when the frame index is valid.</returns>
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

/// <summary>
/// A sub-rectangle of a texture asset.
/// </summary>
/// <param name="Texture">The source texture.</param>
/// <param name="SourceRectangle">The source rectangle within the texture.</param>
public readonly record struct ClientPluginTextureRegion(
    Texture2D Texture,
    Rectangle SourceRectangle);

/// <summary>
/// Hotkey registry plugins use to register and poll hotkeys.
/// </summary>
public interface IOpenGarrisonClientPluginHotkeys
{
    /// <summary>
    /// Registers a hotkey and returns the currently bound key.
    /// </summary>
    /// <param name="hotkeyId">The hotkey id.</param>
    /// <param name="displayName">The display name shown in key bindings.</param>
    /// <param name="defaultKey">The default key binding.</param>
    /// <returns>The currently bound key.</returns>
    Keys RegisterHotkey(string hotkeyId, string displayName, Keys defaultKey);

    /// <summary>
    /// Gets whether a hotkey was pressed this frame.
    /// </summary>
    /// <param name="hotkeyId">The hotkey id.</param>
    /// <returns>True when the hotkey was pressed this frame.</returns>
    bool WasHotkeyPressed(string hotkeyId);

    /// <summary>
    /// Enables or disables hotkey capture for key rebinding.
    /// </summary>
    /// <param name="enabled">Whether key capture is enabled.</param>
    void SetHotkeyCaptureEnabled(bool enabled);
}
