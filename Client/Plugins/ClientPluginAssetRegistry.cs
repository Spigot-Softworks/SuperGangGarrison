using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

internal sealed class ClientPluginAssetRegistry(
    string pluginId,
    string pluginDirectory,
    GraphicsDevice graphicsDevice) : IDisposable
{
    private readonly string _pluginDirectory = Path.GetFullPath(pluginDirectory);
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
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

    public void RegisterSoundAsset(string assetId, string relativePath)
    {
        var path = ResolveRegisteredPath(relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Sound asset not found for plugin '{pluginId}'.", path);
        }

        using var stream = File.OpenRead(path);
        var sound = SoundEffect.FromStream(stream);
        if (_sounds.TryGetValue(assetId, out var existing))
        {
            existing.Dispose();
        }

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
            texture.Dispose();
        }

        foreach (var sound in _sounds.Values)
        {
            sound.Dispose();
        }

        _textures.Clear();
        _sounds.Clear();
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
