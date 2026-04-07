namespace OpenGarrison.Server;

internal interface IGameplayOwnershipRepository
{
    IReadOnlyList<string> LoadOwnedItemIds(GameplayOwnershipIdentity identity);

    void SaveOwnedItemIds(GameplayOwnershipIdentity identity, IReadOnlyCollection<string> itemIds);
}
