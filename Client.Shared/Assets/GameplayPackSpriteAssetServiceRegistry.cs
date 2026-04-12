#nullable enable

using System.Net.Http;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.ClientShared;

public sealed class GameplayPackSpriteAssetServiceRegistry
{
    private readonly Dictionary<string, GameplayPackSpriteAssetService> _services;

    public GameplayPackSpriteAssetServiceRegistry(IEnumerable<KeyValuePair<string, GameplayPackSpriteAssetService>> services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = new Dictionary<string, GameplayPackSpriteAssetService>(StringComparer.OrdinalIgnoreCase);
        foreach (var (packId, service) in services)
        {
            _services[packId] = service;
        }
    }

    public IReadOnlyDictionary<string, GameplayPackSpriteAssetService> Services => _services;

    public bool TryGet(string packId, out GameplayPackSpriteAssetService service)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        return _services.TryGetValue(packId, out service!);
    }

    public GameplayPackSpriteAssetService GetRequired(string packId)
    {
        if (!TryGet(packId, out var service))
        {
            throw new KeyNotFoundException($"No gameplay pack sprite asset service was registered for pack \"{packId}\".");
        }

        return service;
    }

    public static GameplayPackSpriteAssetServiceRegistry Create(IEnumerable<GameplayModPackDefinition> modPacks, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(modPacks);

        if (OperatingSystem.IsBrowser())
        {
            httpClient ??= ClientRuntimeBootstrap.GetBrowserHttpClient();
        }

        var services = new Dictionary<string, GameplayPackSpriteAssetService>(StringComparer.OrdinalIgnoreCase);
        foreach (var modPack in modPacks)
        {
            var packDirectory = OperatingSystem.IsBrowser()
                ? null
                : GameplayModPackDirectoryLoader.FindPackDirectory(modPack.Id);
            var service = ClientRuntimeBootstrap.CreateGameplayPackSpriteAssetService(modPack.Id, httpClient, packDirectory);
            if (service is not null)
            {
                services[modPack.Id] = service;
            }
        }

        return new GameplayPackSpriteAssetServiceRegistry(services);
    }
}
