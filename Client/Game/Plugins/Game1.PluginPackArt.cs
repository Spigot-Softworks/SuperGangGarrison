#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

// Plugin pack art: sprites and sounds of packs whose folder is known (plugins loaded here,
// practice, embedded rooms, or files downloaded from the server) are added to the sprite and
// sound caches at runtime, and dropped again when the pack goes away (plugin shutdown, disconnect).
public partial class Game1
{
    // Pack id -> (sound name -> file) currently registered with the sound cache.
    private readonly Dictionary<string, Dictionary<string, string>> _pluginPackSounds = new(StringComparer.Ordinal);
    private static readonly HttpClient PluginPackHttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };
    private GameplayPackSyncService? _pluginPackSyncService;

    private GameplayPackSyncService _pluginPackSync =>
        _pluginPackSyncService ??= new GameplayPackSyncService(PluginPackHttpClient, GameplayPackSyncService.DefaultCacheRoot);
    private readonly Dictionary<string, PendingPluginPackDownload> _pendingPluginPackDownloads = new(StringComparer.Ordinal);
    private int _pluginPackArtRegistryRevision = -1;
    private int _pluginPackArtCacheGeneration = -1;

    // Called after a server's gameplay content is applied.
    private void QueuePluginPackArtDownloads(GameplayReplicatedContent content)
    {
        var registry = CharacterClassCatalog.RuntimeRegistry;
        foreach (var assetPack in content.AssetPacks ?? [])
        {
            if (registry.TryGetPackDirectory(assetPack.PackId, out _))
            {
                // The server runs in this process (or the plugin is installed here): use its files.
                continue;
            }

            if (_pluginPackSync.TryGetCached(assetPack, out var cachedDirectory))
            {
                RegisterDownloadedPackDirectory(assetPack.PackId, cachedDirectory);
                continue;
            }

            var key = assetPack.PackId + "@" + assetPack.ContentHash;
            if (_pendingPluginPackDownloads.ContainsKey(key))
            {
                continue;
            }

            var cancellation = new CancellationTokenSource();
            var task = _pluginPackSync.EnsureAvailableAsync(assetPack, _networkClient.MapDownloadBaseUri, cancellation.Token);
            _pendingPluginPackDownloads[key] = new PendingPluginPackDownload(assetPack.PackId, _networkClient.ConnectionGeneration, task, cancellation);
            AddNetworkConsoleLine($"downloading plugin art for {assetPack.PackId}");
        }
    }

    public void UpdatePluginPackArt()
    {
        CompletePluginPackDownloads();
        if (_gameplayModAssets is null)
        {
            return;
        }

        var registry = CharacterClassCatalog.RuntimeRegistry;
        if (_pluginPackArtRegistryRevision == registry.ModPackRevision
            && _pluginPackArtCacheGeneration == _gameplayModAssets.PackLoadGeneration)
        {
            return;
        }

        _pluginPackArtRegistryRevision = registry.ModPackRevision;
        _pluginPackArtCacheGeneration = _gameplayModAssets.PackLoadGeneration;
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        var wantedSounds = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var modPack in registry.ModPacks)
        {
            if (!registry.TryGetPackDirectory(modPack.Id, out var directory))
            {
                continue;
            }

            if (modPack.Assets.Sprites.Count > 0)
            {
                wanted.Add(modPack.Id);
                if (!_gameplayModAssets.IsExternalPackRegistered(modPack.Id, directory)
                    && !_gameplayModAssets.RegisterExternalPack(modPack, directory))
                {
                    AddConsoleLine($"[plugin] could not load art for pack {modPack.Id} from {directory}");
                }
            }

            var sounds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sound in modPack.Assets.SoundsOrEmpty.Values)
            {
                if (GameplayPackAssetManifest.TryResolveContainedPath(directory, sound.Path, out var fullPath))
                {
                    sounds[sound.Id] = fullPath;
                }
            }

            if (sounds.Count > 0)
            {
                wantedSounds[modPack.Id] = sounds;
            }
        }

        foreach (var packId in _gameplayModAssets.ExternalPackIds.Where(packId => !wanted.Contains(packId)).ToArray())
        {
            _gameplayModAssets.UnregisterExternalPack(packId);
        }

        UpdatePluginPackSounds(wantedSounds);
    }

    private void UpdatePluginPackSounds(Dictionary<string, Dictionary<string, string>> wantedSounds)
    {
        foreach (var (packId, registered) in _pluginPackSounds.ToArray())
        {
            wantedSounds.TryGetValue(packId, out var wanted);
            foreach (var soundName in registered.Keys.Where(name => wanted is null || !wanted.ContainsKey(name)))
            {
                _runtimeAssets.UnregisterExternalSound(soundName);
            }

            if (wanted is null)
            {
                _pluginPackSounds.Remove(packId);
            }
        }

        foreach (var (packId, sounds) in wantedSounds)
        {
            foreach (var (soundName, fullPath) in sounds)
            {
                _runtimeAssets.RegisterExternalSound(soundName, fullPath);
            }

            _pluginPackSounds[packId] = sounds;
        }
    }

    private void CompletePluginPackDownloads()
    {
        if (_pendingPluginPackDownloads.Count == 0)
        {
            return;
        }

        foreach (var (key, download) in _pendingPluginPackDownloads.ToArray())
        {
            if (download.ConnectionGeneration != _networkClient.ConnectionGeneration)
            {
                // The connection that asked for this art is gone.
                download.Cancellation.Cancel();
                if (download.Task.IsCompleted)
                {
                    _pendingPluginPackDownloads.Remove(key);
                    download.Cancellation.Dispose();
                }

                continue;
            }

            if (!download.Task.IsCompleted)
            {
                continue;
            }

            _pendingPluginPackDownloads.Remove(key);
            download.Cancellation.Dispose();
            if (download.Task.IsCompletedSuccessfully)
            {
                RegisterDownloadedPackDirectory(download.PackId, download.Task.Result);
                AddNetworkConsoleLine($"plugin art ready for {download.PackId}");
            }
            else
            {
                AddNetworkConsoleLine($"plugin art for {download.PackId} failed: {download.Task.Exception?.GetBaseException().Message ?? "cancelled"}");
            }
        }
    }

    // Owned by the server's replicated content, so a disconnect forgets the folder again.
    private static void RegisterDownloadedPackDirectory(string packId, string directory)
    {
        var registry = CharacterClassCatalog.RuntimeRegistry;
        using (registry.BeginRegistrationScope(GameplayRuntimeRegistry.ReplicatedContentOwnerKey))
        {
            registry.RegisterPackDirectory(packId, directory);
        }
    }

    private sealed record PendingPluginPackDownload(
        string PackId,
        uint ConnectionGeneration,
        Task<string> Task,
        CancellationTokenSource Cancellation);
}
