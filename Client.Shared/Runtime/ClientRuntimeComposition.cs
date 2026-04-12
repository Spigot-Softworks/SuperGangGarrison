#nullable enable

using OpenGarrison.GameplayModding;

namespace OpenGarrison.ClientShared;

public sealed class ClientRuntimeComposition
{
    public ClientRuntimeComposition(
        IReadOnlyList<GameplayModPackDefinition> gameplayModPacks,
        GameplayPackSpriteAssetServiceRegistry gameplayPackSpriteAssets)
    {
        ArgumentNullException.ThrowIfNull(gameplayModPacks);
        ArgumentNullException.ThrowIfNull(gameplayPackSpriteAssets);

        GameplayModPacks = gameplayModPacks;
        GameplayPackSpriteAssets = gameplayPackSpriteAssets;
    }

    public IReadOnlyList<GameplayModPackDefinition> GameplayModPacks { get; }

    public GameplayPackSpriteAssetServiceRegistry GameplayPackSpriteAssets { get; }
}
