using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal sealed class BrowserBootstrapAtlasTextureResolver(
    BrowserAtlasManifest manifest,
    BrowserAtlasTextureCache atlasTextureCache)
{
    private readonly BrowserAtlasManifest _manifest = manifest;
    private readonly BrowserAtlasTextureCache _atlasTextureCache = atlasTextureCache;
    private readonly Dictionary<string, BrowserAtlasPageManifest> _atlasPages = manifest.Atlases
        .ToDictionary(static page => page.Id, StringComparer.OrdinalIgnoreCase);

    public bool CanResolve(string spriteId)
    {
        return _manifest.Sprites.ContainsKey(spriteId);
    }

    public async Task<LoadedSpriteFrame?> LoadFrameAsync(string spriteId)
    {
        if (!_manifest.Sprites.TryGetValue(spriteId, out var spriteManifest) || spriteManifest.Frames.Count == 0)
        {
            return null;
        }

        var frameManifest = spriteManifest.Frames[0];
        if (!_atlasPages.TryGetValue(frameManifest.AtlasId, out var pageManifest))
        {
            return null;
        }

        var page = await _atlasTextureCache.GetPageAsync(pageManifest.ImagePath).ConfigureAwait(false);
        return page is null
            ? null
            : _atlasTextureCache.CreateFrame(
                page,
                new Rectangle(frameManifest.X, frameManifest.Y, frameManifest.Width, frameManifest.Height));
    }
}
