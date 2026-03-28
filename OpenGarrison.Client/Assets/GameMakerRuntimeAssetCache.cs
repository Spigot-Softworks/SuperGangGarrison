using System;
using System.Collections.Generic;
using System.IO;
using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

public sealed class GameMakerRuntimeAssetCache : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly GameMakerAssetManifest _manifest;
    private readonly Dictionary<string, LoadedGameMakerSprite> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _backgrounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public GameMakerRuntimeAssetCache(GraphicsDevice graphicsDevice, GameMakerAssetManifest manifest)
    {
        _graphicsDevice = graphicsDevice;
        _manifest = manifest;
    }

    public LoadedGameMakerSprite? GetSprite(string spriteName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_sprites.TryGetValue(spriteName, out var cached))
        {
            return cached;
        }

        if (!_manifest.Sprites.TryGetValue(spriteName, out var spriteAsset) || spriteAsset.FramePaths.Count == 0)
        {
            return null;
        }

        var frames = new Texture2D[spriteAsset.FramePaths.Count];
        for (var frameIndex = 0; frameIndex < spriteAsset.FramePaths.Count; frameIndex += 1)
        {
            var framePath = spriteAsset.FramePaths[frameIndex];
            if (!File.Exists(framePath))
            {
                return null;
            }

            using var stream = File.OpenRead(framePath);
            frames[frameIndex] = Texture2D.FromStream(_graphicsDevice, stream);
        }

        cached = new LoadedGameMakerSprite(frames, new Point(spriteAsset.OriginX, spriteAsset.OriginY));
        _sprites[spriteName] = cached;
        return cached;
    }

    public Texture2D? GetBackground(string backgroundName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_backgrounds.TryGetValue(backgroundName, out var cached))
        {
            return cached;
        }

        if (File.Exists(backgroundName))
        {
            using var directStream = File.OpenRead(backgroundName);
            cached = Texture2D.FromStream(_graphicsDevice, directStream);
            _backgrounds[backgroundName] = cached;
            return cached;
        }

        if (!_manifest.Backgrounds.TryGetValue(backgroundName, out var backgroundAsset)
            || !File.Exists(backgroundAsset.ImagePath))
        {
            return null;
        }

        using var stream = File.OpenRead(backgroundAsset.ImagePath);
        cached = Texture2D.FromStream(_graphicsDevice, stream);
        _backgrounds[backgroundName] = cached;
        return cached;
    }

    public SoundEffect? GetSound(string soundName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_sounds.TryGetValue(soundName, out var cached))
        {
            return cached;
        }

        if (!_manifest.Sounds.TryGetValue(soundName, out var soundAsset)
            || !File.Exists(soundAsset.AudioPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(soundAsset.AudioPath);
            cached = SoundEffect.FromStream(stream);
            _sounds[soundName] = cached;
            return cached;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var sprite in _sprites.Values)
        {
            foreach (var frame in sprite.Frames)
            {
                frame.Dispose();
            }
        }

        foreach (var background in _backgrounds.Values)
        {
            background.Dispose();
        }

        foreach (var sound in _sounds.Values)
        {
            sound.Dispose();
        }

        _sprites.Clear();
        _backgrounds.Clear();
        _sounds.Clear();
    }
}

public sealed record LoadedGameMakerSprite(IReadOnlyList<Texture2D> Frames, Point Origin);
