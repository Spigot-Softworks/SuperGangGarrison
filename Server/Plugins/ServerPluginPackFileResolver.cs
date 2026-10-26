using OpenGarrison.Core;

namespace OpenGarrison.Server;

/// <summary>
/// Decides which plugin pack files the server's HTTP endpoint may serve: only the sprite
/// frames listed in the pack's current asset manifest, and only for the content hash the
/// server advertised. Manifests are cached until the registry's packs change.
/// </summary>
internal sealed class ServerPluginPackFileResolver(GameplayRuntimeRegistry registry)
{
    private readonly object _sync = new();
    private readonly Dictionary<string, CachedManifest> _manifests = new(StringComparer.Ordinal);

    public bool TryGetManifest(string packId, string hashHex, out GameplayPackAssetManifest manifest, out string directory)
    {
        manifest = null!;
        directory = string.Empty;
        if (string.IsNullOrWhiteSpace(packId) || !GameplayPackAssetManifest.IsValidHashHex(hashHex))
        {
            return false;
        }

        lock (_sync)
        {
            var revision = registry.ModPackRevision;
            if (!_manifests.TryGetValue(packId, out var cached) || cached.Revision != revision)
            {
                cached = CreateManifest(packId, revision);
                _manifests[packId] = cached;
            }

            if (cached.Manifest is null
                || !string.Equals(GameplayPackAssetManifest.GetHashHex(cached.Manifest.ContentHash), hashHex, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            manifest = cached.Manifest;
            directory = cached.Directory;
            return true;
        }
    }

    public bool TryResolveFile(string packId, string hashHex, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (!TryGetManifest(packId, hashHex, out var manifest, out var directory)
            || !GameplayPackAssetManifest.TryNormalizeRelativePath(relativePath, out var normalizedPath)
            || !manifest.Files.Any(file => string.Equals(file.Path, normalizedPath, StringComparison.Ordinal)))
        {
            return false;
        }

        return GameplayPackAssetManifest.TryResolveContainedPath(directory, normalizedPath, out fullPath);
    }

    private CachedManifest CreateManifest(string packId, int revision)
    {
        var modPack = registry.ModPacks.FirstOrDefault(pack => string.Equals(pack.Id, packId, StringComparison.Ordinal));
        if (modPack is null
            || (modPack.Assets.Sprites.Count == 0 && modPack.Assets.SoundsOrEmpty.Count == 0)
            || !registry.TryGetPackDirectory(packId, out var directory)
            || !GameplayPackAssetManifest.TryCreate(packId, directory, modPack.Assets, out var manifest, out _))
        {
            return new CachedManifest(revision, null, string.Empty);
        }

        return new CachedManifest(revision, manifest, directory);
    }

    private sealed record CachedManifest(int Revision, GameplayPackAssetManifest? Manifest, string Directory);
}
