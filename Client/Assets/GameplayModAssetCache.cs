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
        var frames = new List<Texture2D>();
        for (var frameIndex = 0; frameIndex < spriteDefinition.FramePaths.Count; frameIndex += 1)
        {
            var framePath = Path.GetFullPath(Path.Combine(packDirectory, spriteDefinition.FramePaths[frameIndex]));
            using var stream = File.OpenRead(framePath);
            using var sourceTexture = Texture2D.FromStream(_graphicsDevice, stream);
            AppendFramesFromSourceTexture(frames, sourceTexture, spriteDefinition);
        }

        return new LoadedGameMakerSprite(frames.ToArray(), new Point(spriteDefinition.OriginX, spriteDefinition.OriginY));
    }

    private void AppendFramesFromSourceTexture(List<Texture2D> frames, Texture2D sourceTexture, GameplaySpriteAssetDefinition spriteDefinition)
    {
        var frameWidth = spriteDefinition.FrameWidth ?? sourceTexture.Width;
        var frameHeight = spriteDefinition.FrameHeight ?? sourceTexture.Height;
        if (frameWidth <= 0
            || frameHeight <= 0
            || sourceTexture.Width % frameWidth != 0
            || sourceTexture.Height % frameHeight != 0)
        {
            throw new InvalidOperationException($"Gameplay sprite asset \"{spriteDefinition.Id}\" frame dimensions do not evenly divide the source texture.");
        }

        var columns = Math.Max(1, sourceTexture.Width / frameWidth);
        var rows = Math.Max(1, sourceTexture.Height / frameHeight);
        var sourcePixels = new Color[sourceTexture.Width * sourceTexture.Height];
        sourceTexture.GetData(sourcePixels);

        for (var row = 0; row < rows; row += 1)
        {
            for (var column = 0; column < columns; column += 1)
            {
                var framePixels = new Color[frameWidth * frameHeight];
                for (var y = 0; y < frameHeight; y += 1)
                {
                    var sourceIndex = ((row * frameHeight) + y) * sourceTexture.Width + (column * frameWidth);
                    Array.Copy(sourcePixels, sourceIndex, framePixels, y * frameWidth, frameWidth);
                }

                var frameTexture = new Texture2D(_graphicsDevice, frameWidth, frameHeight);
                frameTexture.SetData(framePixels);
                frames.Add(frameTexture);
            }
        }
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
