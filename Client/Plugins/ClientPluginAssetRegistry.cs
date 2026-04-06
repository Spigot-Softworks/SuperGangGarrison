using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

internal sealed class ClientPluginAssetRegistry(
    string pluginId,
    string pluginDirectory,
    GraphicsDevice graphicsDevice) : IDisposable
{
    private sealed record TextureAtlasEntry(string TextureAssetId, int FrameWidth, int FrameHeight, int Columns, int Rows, int FrameCount);
    private sealed record TextureRegionEntry(string TextureAssetId, Rectangle SourceRectangle);

    private readonly string _pluginDirectory = Path.GetFullPath(pluginDirectory);
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextureAtlasEntry> _textureAtlases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextureRegionEntry> _textureRegions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public void RegisterTextureAsset(string assetId, string relativePath)
    {
        var path = ResolveRegisteredPath(relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Texture asset not found for plugin '{pluginId}'.", path);
        }

        using var stream = File.OpenRead(path);
        var texture = Texture2D.FromStream(graphicsDevice, stream);
        if (_textures.TryGetValue(assetId, out var existing))
        {
            existing.Dispose();
        }

        _textures[assetId] = texture;
    }

    public bool TryGetTextureAsset(string assetId, out Texture2D texture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_textures.TryGetValue(assetId, out var existing))
        {
            texture = existing;
            return true;
        }

        texture = null!;
        return false;
    }

    public void RegisterTextureAtlasAsset(string assetId, string relativePath, int frameWidth, int frameHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameHeight);

        RegisterTextureAsset(assetId, relativePath);
        if (!_textures.TryGetValue(assetId, out var texture))
        {
            throw new InvalidOperationException($"Texture atlas source texture was not available for plugin '{pluginId}'.");
        }

        var columns = texture.Width / frameWidth;
        var rows = texture.Height / frameHeight;
        if (columns <= 0 || rows <= 0)
        {
            throw new InvalidOperationException($"Texture atlas frame size is larger than the texture for plugin '{pluginId}': {assetId}");
        }

        _textureAtlases[assetId] = new TextureAtlasEntry(assetId, frameWidth, frameHeight, columns, rows, columns * rows);
    }

    public bool TryGetTextureAtlasAsset(string assetId, out ClientPluginTextureAtlas atlas)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_textureAtlases.TryGetValue(assetId, out var entry)
            && _textures.TryGetValue(entry.TextureAssetId, out var texture))
        {
            atlas = new ClientPluginTextureAtlas(texture, entry.FrameWidth, entry.FrameHeight, entry.Columns, entry.Rows, entry.FrameCount);
            return true;
        }

        atlas = default;
        return false;
    }

    public void RegisterTextureRegionAsset(string assetId, string textureAssetId, Rectangle sourceRectangle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(textureAssetId);
        if (!_textures.TryGetValue(textureAssetId, out var texture))
        {
            throw new InvalidOperationException($"Texture region source texture is not registered for plugin '{pluginId}': {textureAssetId}");
        }

        ValidateTextureRegion(texture, sourceRectangle);
        _textureRegions[assetId] = new TextureRegionEntry(textureAssetId, sourceRectangle);
    }

    public bool TryGetTextureRegionAsset(string assetId, out ClientPluginTextureRegion region)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_textureRegions.TryGetValue(assetId, out var entry)
            && _textures.TryGetValue(entry.TextureAssetId, out var texture))
        {
            region = new ClientPluginTextureRegion(texture, entry.SourceRectangle);
            return true;
        }

        region = default;
        return false;
    }

    public void RegisterSoundAsset(string assetId, string relativePath)
    {
        var path = ResolveRegisteredPath(relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Sound asset not found for plugin '{pluginId}'.", path);
        }

        using var stream = File.OpenRead(path);
        var sound = SoundEffect.FromStream(stream);
        _sounds[assetId] = sound;
    }

    public bool TryGetSoundAsset(string assetId, out SoundEffect sound)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_sounds.TryGetValue(assetId, out var existing))
        {
            sound = existing;
            return true;
        }

        sound = null!;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var texture in _textures.Values)
        {
            try
            {
                texture.Dispose();
            }
            catch
            {
            }
        }

        _textures.Clear();
        _textureAtlases.Clear();
        _textureRegions.Clear();
        _sounds.Clear();
    }

    private static void ValidateTextureRegion(Texture2D texture, Rectangle sourceRectangle)
    {
        if (sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRectangle), "Texture region dimensions must be positive.");
        }

        if (sourceRectangle.X < 0
            || sourceRectangle.Y < 0
            || sourceRectangle.Right > texture.Width
            || sourceRectangle.Bottom > texture.Height)
        {
            throw new InvalidOperationException("Texture region source rectangle must stay within the source texture bounds.");
        }
    }

    private string ResolveRegisteredPath(string relativePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var combinedPath = Path.GetFullPath(Path.Combine(_pluginDirectory, relativePath));
        if (!combinedPath.StartsWith(_pluginDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Plugin asset path escapes plugin directory for '{pluginId}': {relativePath}");
        }

        return combinedPath;
    }
}
