#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private LoadedGameMakerSprite? GetResolvedSprite(string spriteName)
    {
        var gameplaySprite = _gameplayModAssets?.GetSprite(spriteName);
        if (gameplaySprite is not null)
        {
            return gameplaySprite;
        }

        if (_clientSettings.DisableLegacyGameplaySpriteFallback)
        {
            return null;
        }

        return _runtimeAssets?.GetSprite(spriteName);
    }
}
