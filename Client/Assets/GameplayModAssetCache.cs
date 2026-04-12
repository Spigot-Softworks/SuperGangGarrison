using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public sealed class GameplayModAssetCache(GraphicsDevice graphicsDevice) : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice = graphicsDevice;
    private readonly Dictionary<string, LoadedGameMakerSprite> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RegisteredGameplaySprite> _registeredSprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GameplayPackSpriteAssetService> _browserFallbackServices = [];
    private readonly Dictionary<string, Task<LoadedGameMakerSprite?>> _pendingBrowserSprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedBrowserSprites = new(StringComparer.OrdinalIgnoreCase);
    private BrowserAtlasTextureCache? _browserAtlasTextureCache;
    private BrowserGameplayAtlasSpriteResolver? _browserStockAtlasResolver;
    private bool _disposed;

    public void LoadRegisteredPacks(ClientRuntimeComposition runtimeComposition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(runtimeComposition);

        DisposeLoadedSprites();
        _registeredSprites.Clear();
        _browserFallbackServices.Clear();
        _pendingBrowserSprites.Clear();
        _failedBrowserSprites.Clear();
        _browserStockAtlasResolver = null;
        foreach (var service in runtimeComposition.GameplayPackSpriteAssets.Services.Values)
        {
            _browserFallbackServices.Add(service);
        }

        if (OperatingSystem.IsBrowser())
        {
            _browserAtlasTextureCache ??= new BrowserAtlasTextureCache(_graphicsDevice);
            var stockAtlasManifest = ClientRuntimeBootstrap.GetBrowserStockGameplayAtlasManifest();
            if (stockAtlasManifest is not null)
            {
                _browserStockAtlasResolver = new BrowserGameplayAtlasSpriteResolver(stockAtlasManifest, _browserAtlasTextureCache);
            }
        }

        foreach (var modPack in runtimeComposition.GameplayModPacks)
        {
            if (!runtimeComposition.GameplayPackSpriteAssets.TryGet(modPack.Id, out var assetService))
            {
                continue;
            }

            foreach (var sprite in modPack.Assets.Sprites.Values)
            {
                _registeredSprites[sprite.Id] = new RegisteredGameplaySprite(assetService, sprite);
            }
        }
    }

    public LoadedGameMakerSprite? GetSprite(string spriteId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_sprites.TryGetValue(spriteId, out var sprite))
        {
            return sprite;
        }

        if (!_registeredSprites.TryGetValue(spriteId, out var registeredSprite))
        {
            return OperatingSystem.IsBrowser()
                ? TryGetBrowserSprite(spriteId, null)
                : null;
        }

        if (OperatingSystem.IsBrowser())
        {
            return TryGetBrowserSprite(spriteId, registeredSprite);
        }

        sprite = LoadSprite(registeredSprite.AssetService.LoadRegisteredSprite(registeredSprite.Definition));
        _sprites[spriteId] = sprite;
        return sprite;
    }

    public IReadOnlyList<string> GetBrowserAtlasPagePaths()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return Array.Empty<string>();
        }

        var atlasManifest = ClientRuntimeBootstrap.GetBrowserStockGameplayAtlasManifest();
        return atlasManifest?.Manifest.Atlases
            .Select(static page => page.ImagePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();
    }

    public async Task<bool> WarmBrowserAtlasPageAsync(string relativePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (!OperatingSystem.IsBrowser())
        {
            return true;
        }

        _browserAtlasTextureCache ??= new BrowserAtlasTextureCache(_graphicsDevice);
        var page = await _browserAtlasTextureCache.GetPageAsync(relativePath).ConfigureAwait(false);
        return page is not null;
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
        _registeredSprites.Clear();
        _browserFallbackServices.Clear();
        _pendingBrowserSprites.Clear();
        _failedBrowserSprites.Clear();
        _browserStockAtlasResolver = null;
        _browserAtlasTextureCache?.Dispose();
        _browserAtlasTextureCache = null;
    }

    private LoadedGameMakerSprite LoadSprite(LoadedGameplaySpriteAsset spriteAsset)
    {
        var frames = new List<LoadedSpriteFrame>();
        var spriteDefinition = spriteAsset.Definition.Definition;
        var sourceImages = spriteAsset.SourceSet;
        foreach (var sourceImage in sourceImages.SourceImages)
        {
            if (OperatingSystem.IsBrowser())
            {
                frames.AddRange(TextureDecodeUtility.LoadFrameTextures(
                    _graphicsDevice,
                    sourceImage.Bytes,
                    spriteDefinition.FrameWidth,
                    spriteDefinition.FrameHeight));
                continue;
            }

            using var stream = new MemoryStream(sourceImage.Bytes, writable: false);
            using var sourceTexture = Texture2D.FromStream(_graphicsDevice, stream);
            AppendFramesFromSourceTexture(frames, sourceTexture, spriteDefinition);
        }

        return new LoadedGameMakerSprite(frames.ToArray(), new Point(spriteDefinition.OriginX, spriteDefinition.OriginY));
    }

    private void AppendFramesFromSourceTexture(List<LoadedSpriteFrame> frames, Texture2D sourceTexture, GameplaySpriteAssetDefinition spriteDefinition)
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
                frames.Add(new LoadedSpriteFrame(frameTexture));
            }
        }
    }

    private LoadedGameMakerSprite? TryGetBrowserSprite(string spriteId, RegisteredGameplaySprite? registeredSprite)
    {
        if (_failedBrowserSprites.Contains(spriteId))
        {
            return null;
        }

        if (!_pendingBrowserSprites.TryGetValue(spriteId, out var pendingTask))
        {
            pendingTask = StartBrowserSpriteLoadAsync(spriteId, registeredSprite);
            _pendingBrowserSprites[spriteId] = pendingTask;
        }

        if (!pendingTask.IsCompleted)
        {
            return null;
        }

        _pendingBrowserSprites.Remove(spriteId);
        try
        {
            var sprite = pendingTask.GetAwaiter().GetResult();
            if (sprite is null)
            {
                _failedBrowserSprites.Add(spriteId);
                return null;
            }

            _sprites[spriteId] = sprite;
            return sprite;
        }
        catch (Exception ex)
        {
            if (_failedBrowserSprites.Add(spriteId))
            {
                Console.WriteLine($"Browser gameplay sprite load failed for \"{spriteId}\": {ex.Message}");
            }

            return null;
        }
    }

    private Task<LoadedGameMakerSprite?> StartBrowserSpriteLoadAsync(string spriteId, RegisteredGameplaySprite? registeredSprite)
    {
        if (_browserStockAtlasResolver?.CanResolve(spriteId) == true)
        {
            return _browserStockAtlasResolver.LoadSpriteAsync(spriteId);
        }

        return registeredSprite is null
            ? LoadLazyBrowserSpriteAsync(spriteId)
            : LoadBrowserSpriteAsync(registeredSprite);
    }

    private async Task<LoadedGameMakerSprite?> LoadBrowserSpriteAsync(RegisteredGameplaySprite registeredSprite)
    {
        var spriteAsset = await registeredSprite.AssetService.LoadRegisteredSpriteAsync(registeredSprite.Definition)
            .ConfigureAwait(false);
        await Task.Yield();
        return LoadSprite(spriteAsset);
    }

    private async Task<LoadedGameMakerSprite?> LoadLazyBrowserSpriteAsync(string spriteId)
    {
        foreach (var assetService in _browserFallbackServices)
        {
            try
            {
                var spriteAsset = await assetService.TryLoadAsync(spriteId).ConfigureAwait(false);
                if (spriteAsset is not null)
                {
                    await Task.Yield();
                    return LoadSprite(spriteAsset);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Browser gameplay sprite probe failed for \"{spriteId}\" in pack \"{assetService.PackId}\": {ex.Message}");
            }
        }

        return null;
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

        _sprites.Clear();
    }

    private sealed record RegisteredGameplaySprite(GameplayPackSpriteAssetService AssetService, GameplaySpriteAssetDefinition Definition);
}
