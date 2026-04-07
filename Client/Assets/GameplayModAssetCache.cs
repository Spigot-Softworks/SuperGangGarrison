using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public sealed class GameplayModAssetCache(GraphicsDevice graphicsDevice) : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice = graphicsDevice;
    private readonly Dictionary<string, LoadedGameMakerSprite> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public void LoadRegisteredPacks(IEnumerable<GameplayModPackDefinition> modPacks)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(modPacks);

        DisposeLoadedSprites();
        foreach (var modPack in modPacks)
        {
            var packDirectory = GameplayModPackDirectoryLoader.FindPackDirectory(modPack.Id);
            if (string.IsNullOrWhiteSpace(packDirectory))
            {
                continue;
            }

            foreach (var sprite in modPack.Assets.Sprites.Values)
            {
                _sprites[sprite.Id] = LoadSprite(packDirectory, sprite);
            }
        }
    }

    public LoadedGameMakerSprite? GetSprite(string spriteId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _sprites.TryGetValue(spriteId, out var sprite) ? sprite : null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeLoadedSprites();
        _sprites.Clear();
    }

    private LoadedGameMakerSprite LoadSprite(string packDirectory, GameplaySpriteAssetDefinition spriteDefinition)
    {
        var frames = new Texture2D[spriteDefinition.FramePaths.Count];
        for (var frameIndex = 0; frameIndex < spriteDefinition.FramePaths.Count; frameIndex += 1)
        {
            var framePath = Path.GetFullPath(Path.Combine(packDirectory, spriteDefinition.FramePaths[frameIndex]));
            using var stream = File.OpenRead(framePath);
            frames[frameIndex] = Texture2D.FromStream(_graphicsDevice, stream);
        }

        return new LoadedGameMakerSprite(frames, new Point(spriteDefinition.OriginX, spriteDefinition.OriginY));
    }

    private void DisposeLoadedSprites()
    {
        foreach (var sprite in _sprites.Values)
        {
            foreach (var frame in sprite.Frames)
            {
                frame.Dispose();
            }
        }
    }
}
