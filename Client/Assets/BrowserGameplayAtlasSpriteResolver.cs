using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal sealed class BrowserGameplayAtlasSpriteResolver(
    BrowserGameplayAtlasManifest manifest,
    BrowserAtlasTextureCache atlasTextureCache)
{
    private readonly BrowserGameplayAtlasManifest _manifest = manifest;
    private readonly BrowserAtlasTextureCache _atlasTextureCache = atlasTextureCache;
    private readonly Dictionary<string, BrowserAtlasPageManifest> _atlasPages = manifest.Manifest.Atlases
        .ToDictionary(static page => page.Id, StringComparer.OrdinalIgnoreCase);

    public bool CanResolve(string spriteId)
    {
        return _manifest.Manifest.Sprites.ContainsKey(spriteId);
    }

    public async Task<LoadedGameMakerSprite?> LoadSpriteAsync(string spriteId)
    {
        if (!_manifest.Manifest.Sprites.TryGetValue(spriteId, out var spriteManifest))
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

            frames.Add(_atlasTextureCache.CreateFrame(
                page,
                new Rectangle(frameManifest.X, frameManifest.Y, frameManifest.Width, frameManifest.Height)));
        }

        return new LoadedGameMakerSprite(frames, new Point(spriteManifest.OriginX, spriteManifest.OriginY));
    }
}
