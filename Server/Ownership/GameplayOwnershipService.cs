using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal sealed class GameplayOwnershipService(
    Func<SimulationWorld> worldGetter,
    GameplayOwnershipIdentityResolver identityResolver,
    IGameplayOwnershipRepository repository,
    Action<string> log)
{
    private readonly Dictionary<byte, GameplayOwnershipIdentity> _identitiesBySlot = new();

    public bool PersistenceEnabled => identityResolver.PersistenceEnabled;

    public PersistentGameplayOwnershipIdentityMode IdentityMode => identityResolver.Mode;

    public void ApplyClientProfile(byte slot, string? playerName, ulong badgeMask)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            || !worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            _identitiesBySlot.Remove(slot);
            return;
        }

        player.ClearTrackedOwnedGameplayItems();
        if (!identityResolver.TryResolve(playerName, badgeMask, out var identity))
        {
            _identitiesBySlot.Remove(slot);
            return;
        }

        _identitiesBySlot[slot] = identity;
        if (!PersistenceEnabled)
        {
            return;
        }

        player.ReplaceOwnedGameplayItemIds(repository.LoadOwnedItemIds(identity));
    }

    public void ReleaseSlot(byte slot)
    {
        _identitiesBySlot.Remove(slot);
        if (SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            && worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            player.ClearTrackedOwnedGameplayItems();
        }
    }

    public bool TryGrantItem(byte slot, string itemId)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            || !worldGetter().TryGetNetworkPlayer(slot, out var player)
            || !player.TryGrantGameplayItem(itemId))
        {
            return false;
        }

        PersistTrackedOwnershipIfPossible(slot, player, itemId);
        return true;
    }

    public bool TryRevokeItem(byte slot, string itemId)
    {
        if (!SimulationWorld.IsPlayableNetworkPlayerSlot(slot)
            || !worldGetter().TryGetNetworkPlayer(slot, out var player)
            || !player.TryRevokeGameplayItem(itemId))
        {
            return false;
        }

        PersistTrackedOwnershipIfPossible(slot, player, itemId);
        return true;
    }

    public string DescribeConfiguration(string storePath)
    {
        return IdentityMode switch
        {
            PersistentGameplayOwnershipIdentityMode.Disabled => "[server] persistent gameplay ownership: disabled",
            _ => $"[server] persistent gameplay ownership: enabled mode={IdentityMode} store=\"{storePath}\"",
        };
    }

    private void PersistTrackedOwnershipIfPossible(byte slot, PlayerEntity player, string itemId)
    {
        if (!CharacterClassCatalog.RuntimeRegistry.RequiresTrackedOwnership(itemId))
        {
            return;
        }

        if (!_identitiesBySlot.TryGetValue(slot, out var identity))
        {
            log($"[ownership] tracked item \"{itemId}\" changed for slot {slot}, but no persistent identity is available.");
            return;
        }

        if (!PersistenceEnabled)
        {
            return;
        }

        repository.SaveOwnedItemIds(identity, player.GetTrackedOwnedGameplayItemIds());
    }
}
