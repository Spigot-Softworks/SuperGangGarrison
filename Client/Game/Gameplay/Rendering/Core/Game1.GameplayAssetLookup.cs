#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private LoadedGameMakerSprite? GetResolvedSprite(string spriteName)
    {
        if (OperatingSystem.IsBrowser())
        {
            var browserGameplaySprite = _gameplayModAssets?.GetSprite(spriteName, allowBrowserLazyFallback: false);
            if (browserGameplaySprite is not null || _clientSettings.DisableLegacyGameplaySpriteFallback)
            {
                return browserGameplaySprite;
            }

            var browserRuntimeSprite = _runtimeAssets?.GetSprite(spriteName);
            if (browserRuntimeSprite is not null || _runtimeAssets?.ContainsSprite(spriteName) == true)
            {
                return browserRuntimeSprite;
            }

            return _gameplayModAssets?.GetSprite(spriteName);
        }

        var gameplaySprite = _gameplayModAssets?.GetSprite(spriteName);
        if (gameplaySprite is not null || _clientSettings.DisableLegacyGameplaySpriteFallback)
        {
            return gameplaySprite;
        }

        return _runtimeAssets?.GetSprite(spriteName);
    }
}
