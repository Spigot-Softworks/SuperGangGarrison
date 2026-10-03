using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

/// <summary>
/// Resolves a player body sprite name to its sprite asset (origin and collision
/// mask) for <see cref="PlayerPresentationBoundsSystem"/>. Looks in the packaged
/// GameMaker assets, then in the registered gameplay mod packs, then in a fresh
/// read of the asset manifest.
/// </summary>
/// <remarks>
/// Process-wide on purpose: the packaged assets are immutable once loaded, and
/// loading them per world would cost every world (and every test) a manifest read.
/// Sharing is safe because a lookup depends only on the sprite name and on data
/// that is the same for every world in the process:
/// <list type="bullet">
/// <item>The cache is cleared whenever a mod pack is registered
/// (<see cref="GameplayRuntimeRegistry.ModPackRevision"/>), so a sprite that a later
/// mod pack adds is not stuck as missing.</item>
/// <item>One lock guards the resolved and missing sets, so worlds on different
/// threads (an embedded server beside the client) can share it.</item>
/// </list>
/// </remarks>
internal static class PresentationSpriteAssetCache
{
    private static readonly Lazy<GameMakerAssetManifest> PackagedAssets = new(GameMakerRuntimeAssetManifestLoader.LoadPackagedOrProjectAssets);
    private static readonly object Sync = new();
    private static readonly Dictionary<string, GameMakerSpriteAsset> Resolved = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Missing = new(StringComparer.OrdinalIgnoreCase);
    private static int _modPackRevision = -1;

    internal static bool TryGet(string spriteName, out GameMakerSpriteAsset sprite)
    {
        var normalizedSpriteName = spriteName.Trim();
        if (PackagedAssets.Value.Sprites.TryGetValue(normalizedSpriteName, out sprite!))
        {
            return true;
        }

        int revision;
        lock (Sync)
        {
            revision = ClearIfModPacksChanged();
            if (Resolved.TryGetValue(normalizedSpriteName, out sprite!))
            {
                return true;
            }

            if (Missing.Contains(normalizedSpriteName))
            {
                sprite = null!;
                return false;
            }
        }

        if (TryCreateGameplaySpriteAsset(normalizedSpriteName, out sprite!)
            || TryLoadFreshSpriteAsset(normalizedSpriteName, out sprite!))
        {
            lock (Sync)
            {
                // Do not store a result computed against mod packs that have since changed.
                if (revision == _modPackRevision)
                {
                    Resolved[normalizedSpriteName] = sprite;
                    Missing.Remove(normalizedSpriteName);
                }
            }

            return true;
        }

        lock (Sync)
        {
            if (revision == _modPackRevision)
            {
                Missing.Add(normalizedSpriteName);
            }
        }

        sprite = null!;
        return false;
    }

    // Caller holds Sync. Returns the mod pack revision the cache now reflects.
    private static int ClearIfModPacksChanged()
    {
        var revision = CharacterClassCatalog.RuntimeRegistry.ModPackRevision;
        if (revision != _modPackRevision)
        {
            Resolved.Clear();
            Missing.Clear();
            _modPackRevision = revision;
        }

        return revision;
    }

    private static bool TryLoadFreshSpriteAsset(string spriteName, out GameMakerSpriteAsset sprite)
    {
        try
        {
            var freshManifest = GameMakerRuntimeAssetManifestLoader.LoadPackagedOrProjectAssets();
            return freshManifest.Sprites.TryGetValue(spriteName, out sprite!);
        }
        catch (FileNotFoundException)
        {
            sprite = null!;
            return false;
        }
    }

    private static bool TryCreateGameplaySpriteAsset(string spriteName, out GameMakerSpriteAsset sprite)
    {
        foreach (var modPack in CharacterClassCatalog.RuntimeRegistry.ModPacks)
        {
            if (modPack.Assets.Sprites.TryGetValue(spriteName, out var definition)
                && TryCreateGameplaySpriteAsset(definition, out sprite!))
            {
                return true;
            }
        }

        if (StockGameplayModCatalog.Definition.Assets.Sprites.TryGetValue(spriteName, out var stockDefinition)
            && TryCreateGameplaySpriteAsset(stockDefinition, out sprite!))
        {
            return true;
        }

        sprite = null!;
        return false;
    }

    private static bool TryCreateGameplaySpriteAsset(
        GameplaySpriteAssetDefinition definition,
        out GameMakerSpriteAsset sprite)
    {
        var mask = definition.Mask;
        if (mask is null
            || !mask.Left.HasValue
            || !mask.Top.HasValue
            || !mask.Right.HasValue
            || !mask.Bottom.HasValue)
        {
            sprite = null!;
            return false;
        }

        sprite = new GameMakerSpriteAsset(
            definition.Id,
            MetadataPath: $"gameplay:{definition.Id}",
            FramePaths: definition.FramePaths,
            OriginX: definition.OriginX,
            OriginY: definition.OriginY,
            Preload: true,
            Transparent: true,
            Mask: new GameMakerSpriteMask(
                mask.Separate,
                mask.Shape,
                mask.BoundsMode,
                mask.Left,
                mask.Top,
                mask.Right,
                mask.Bottom));
        return true;
    }
}
