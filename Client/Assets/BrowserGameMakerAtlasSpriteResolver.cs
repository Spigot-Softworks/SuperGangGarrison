using Microsoft.Xna.Framework;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal sealed class BrowserGameMakerAtlasSpriteResolver(
    BrowserGameMakerAtlasManifest manifest,
    BrowserAtlasTextureCache atlasTextureCache)
{
    private readonly BrowserAtlasManifest _manifest = manifest.Manifest;
    private readonly BrowserAtlasTextureCache _atlasTextureCache = atlasTextureCache;
    private readonly Dictionary<string, BrowserAtlasPageManifest> _atlasPages = manifest.Manifest.Atlases
        .ToDictionary(static page => page.Id, StringComparer.OrdinalIgnoreCase);

    public bool CanResolve(string spriteId)
    {
        return _manifest.Sprites.ContainsKey(spriteId);
    }

    public async Task<LoadedGameMakerSprite?> LoadSpriteAsync(string spriteId)
    {
        if (!_manifest.Sprites.TryGetValue(spriteId, out var spriteManifest))
        {
            return null;
        }

        var frames = new List<LoadedSpriteFrame>(spriteManifest.Frames.Count);
        foreach (var frameManifest in spriteManifest.Frames)
        {
            if (!_atlasPages.TryGetValue(frameManifest.AtlasId, out var pageManifest))
            {
                return null;
            }

            var page = await _atlasTextureCache.GetPageAsync(pageManifest.ImagePath).ConfigureAwait(false);
            if (page is null)
            {
                return null;
            }

            var sourceRectangle = new Rectangle(
                frameManifest.X,
                frameManifest.Y,
                frameManifest.Width,
                frameManifest.Height);

            // The corner logo is also used by the startup/menu composition and has
            // historically been the first visible casualty when a driver samples
            // outside an atlas frame. Keep it on its own texture even though the
            // rest of the GameMaker sprites remain atlas-backed.
            if (string.Equals(spriteId, "OpenGarrisonLogoS", StringComparison.Ordinal))
            {
                frames.Add(new LoadedSpriteFrame(_atlasTextureCache.CreateFrameTexture(page, sourceRectangle)));
            }
            else
            {
                frames.Add(_atlasTextureCache.CreateFrame(page, sourceRectangle));
            }
        }

        return new LoadedGameMakerSprite(frames, new Point(spriteManifest.OriginX, spriteManifest.OriginY));
    }
}
