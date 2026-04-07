#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private LoadedGameMakerSprite? GetResolvedSprite(string spriteName)
    {
        return _gameplayModAssets?.GetSprite(spriteName)
            ?? _runtimeAssets.GetSprite(spriteName);
    }
}
