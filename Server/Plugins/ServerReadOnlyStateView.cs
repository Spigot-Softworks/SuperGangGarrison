using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Server.Plugins;
using static ServerHelpers;

namespace OpenGarrison.Server;

internal sealed class ServerReadOnlyStateView(
    Func<string> serverNameGetter,
    Func<SimulationWorld> worldGetter,
    Func<IReadOnlyDictionary<byte, ClientSession>> clientsGetter) : IOpenGarrisonServerReadOnlyState
{
    public string ServerName => serverNameGetter();

    public string LevelName => worldGetter().Level.Name;

    public int MapAreaIndex => worldGetter().Level.MapAreaIndex;

    public int MapAreaCount => worldGetter().Level.MapAreaCount;

    public GameModeKind GameMode => worldGetter().MatchRules.Mode;

    public MatchPhase MatchPhase => worldGetter().MatchState.Phase;

    public int RedCaps => worldGetter().RedCaps;

    public int BlueCaps => worldGetter().BlueCaps;

    public IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers()
    {
        var world = worldGetter();
        return clientsGetter()
            .Values
            .OrderBy(client => client.Slot)
            .Select(client =>
            {
                var isSpectator = IsSpectatorSlot(client.Slot);
                PlayerTeam? team = null;
                PlayerClass? playerClass = null;
                PlayerEntity? player = null;
                if (!isSpectator && world.TryGetNetworkPlayer(client.Slot, out var networkPlayer))
                {
                    player = networkPlayer;
                    team = networkPlayer.Team;
                    playerClass = networkPlayer.ClassId;
                }

                return new OpenGarrisonServerPlayerInfo(
                    client.Slot,
                    client.Name,
                    isSpectator,
                    client.IsAuthorized,
                    team,
                    playerClass,
                    client.EndPoint.ToString(),
                    player?.GameplayLoadoutState.LoadoutId ?? string.Empty,
                    player?.GameplayLoadoutState.SecondaryItemId ?? string.Empty,
                    player?.GameplayLoadoutState.AcquiredItemId ?? string.Empty,
                    player?.GameplayLoadoutState.EquippedSlot ?? GameplayEquipmentSlot.Primary,
                    player?.GameplayLoadoutState.EquippedItemId ?? string.Empty);
            })
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayModPackInfo> GetGameplayModPacks()
    {
        return CharacterClassCatalog.RuntimeRegistry.ModPacks
            .OrderBy(pack => pack.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pack => pack.Id, StringComparer.Ordinal)
            .Select(pack => new OpenGarrisonServerGameplayModPackInfo(
                pack.Id,
                pack.DisplayName,
                pack.Version.ToString(),
                pack.Items.Count,
                pack.Classes.Count,
                string.Equals(pack.Id, StockGameplayModCatalog.Definition.Id, StringComparison.Ordinal)))
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayClassInfo> GetGameplayClasses(string? modPackId = null)
    {
        return GetFilteredGameplayModPacks(modPackId)
            .SelectMany(static pack => pack.Classes.Values.Select(gameplayClass => new OpenGarrisonServerGameplayClassInfo(
                pack.Id,
                gameplayClass.Id,
                gameplayClass.DisplayName,
                gameplayClass.DefaultLoadoutId,
                gameplayClass.Loadouts.Count)))
            .OrderBy(gameplayClass => gameplayClass.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(gameplayClass => gameplayClass.ModPackId, StringComparer.Ordinal)
            .ThenBy(gameplayClass => gameplayClass.ClassId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetGameplayItems(string? modPackId = null)
    {
        return GetFilteredGameplayModPacks(modPackId)
            .SelectMany(static pack => pack.Items.Values.Select(item => new OpenGarrisonServerGameplayItemInfo(
                pack.Id,
                item.Id,
                item.DisplayName,
                item.Slot,
                item.BehaviorId,
                item.Ownership?.TrackOwnership ?? false,
                item.Ownership?.DefaultGranted ?? true,
                item.Ownership?.GrantOnAcquire ?? false,
                item.Ownership?.GrantKey)))
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ModPackId, StringComparer.Ordinal)
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetOwnedGameplayItems(byte slot)
    {
        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return Array.Empty<OpenGarrisonServerGameplayItemInfo>();
        }

        return player.GetOwnedGameplayItemIds()
            .Select(itemId => CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(itemId))
            .Select(item => new OpenGarrisonServerGameplayItemInfo(
                GetOwningModPackId(item.Id),
                item.Id,
                item.DisplayName,
                item.Slot,
                item.BehaviorId,
                item.Ownership?.TrackOwnership ?? false,
                item.Ownership?.DefaultGranted ?? true,
                item.Ownership?.GrantOnAcquire ?? false,
                item.Ownership?.GrantKey))
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetGameplayLoadoutsForClass(string classId)
    {
        if (string.IsNullOrWhiteSpace(classId))
        {
            return Array.Empty<OpenGarrisonServerGameplayLoadoutInfo>();
        }

        var gameplayClass = CharacterClassCatalog.RuntimeRegistry.GetRequiredClass(classId);
        return gameplayClass.Loadouts.Values
            .OrderBy(loadout => loadout.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(loadout => loadout.Id, StringComparer.Ordinal)
            .Select(static loadout => new OpenGarrisonServerGameplayLoadoutInfo(
                loadout.Id,
                loadout.DisplayName,
                loadout.PrimaryItemId,
                loadout.SecondaryItemId,
                loadout.UtilityItemId,
                IsSelected: false,
                IsAvailableToPlayer: false))
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplaySecondaryItems(byte slot)
    {
        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return Array.Empty<OpenGarrisonServerGameplaySelectableItemInfo>();
        }

        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var classDefinition = runtimeRegistry.GetClassDefinition(player.ClassId);
        var loadout = runtimeRegistry.TryGetLoadout(player.ClassId, player.GameplayLoadoutState.LoadoutId, out var selectedLoadout)
            ? selectedLoadout
            : classDefinition.Loadouts[classDefinition.DefaultLoadoutId];
        var items = new List<OpenGarrisonServerGameplaySelectableItemInfo>();
        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);

        AddSelectableItem(player, items, seenItemIds, loadout.SecondaryItemId, player.GameplayLoadoutState.SecondaryItemId);
        if (runtimeRegistry.SupportsExperimentalAcquiredWeapon(player.ClassId))
        {
            foreach (var modPack in runtimeRegistry.ModPacks.OrderBy(pack => pack.Id, StringComparer.Ordinal))
            {
                foreach (var item in modPack.Items.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
                {
                    if (item.Slot != GameplayEquipmentSlot.Primary)
                    {
                        continue;
                    }

                    AddSelectableItem(player, items, seenItemIds, item.Id, player.GameplayLoadoutState.SecondaryItemId);
                }
            }
        }

        return items;
    }

    public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplayAcquiredItems(byte slot)
    {
        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return Array.Empty<OpenGarrisonServerGameplaySelectableItemInfo>();
        }

        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        if (!runtimeRegistry.SupportsExperimentalAcquiredWeapon(player.ClassId))
        {
            return Array.Empty<OpenGarrisonServerGameplaySelectableItemInfo>();
        }

        var items = new List<OpenGarrisonServerGameplaySelectableItemInfo>();
        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var modPack in runtimeRegistry.ModPacks.OrderBy(pack => pack.Id, StringComparer.Ordinal))
        {
            foreach (var item in modPack.Items.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (item.Slot != GameplayEquipmentSlot.Primary)
                {
                    continue;
                }

                AddSelectableItem(player, items, seenItemIds, item.Id, player.GameplayLoadoutState.AcquiredItemId);
            }
        }

        return items;
    }

    public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetAvailableGameplayLoadouts(byte slot)
    {
        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return Array.Empty<OpenGarrisonServerGameplayLoadoutInfo>();
        }

        var gameplayClass = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(player.ClassId);
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        return gameplayClass.Loadouts
            .Values
            .OrderBy(loadout => loadout.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(loadout => loadout.Id, StringComparer.Ordinal)
            .Select(loadout => new OpenGarrisonServerGameplayLoadoutInfo(
                loadout.Id,
                loadout.DisplayName,
                loadout.PrimaryItemId,
                loadout.SecondaryItemId,
                loadout.UtilityItemId,
                string.Equals(loadout.Id, player.GameplayLoadoutState.LoadoutId, StringComparison.Ordinal),
                runtimeRegistry.LoadoutItemsAreOwned(loadout, player.OwnsGameplayItem)))
            .ToArray();
    }

    public bool TryGetPlayerReplicatedStateInt(byte slot, string ownerPluginId, string stateKey, out int value)
    {
        if (worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            return player.TryGetReplicatedStateInt(ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    public bool TryGetPlayerReplicatedStateFloat(byte slot, string ownerPluginId, string stateKey, out float value)
    {
        if (worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            return player.TryGetReplicatedStateFloat(ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    public bool TryGetPlayerReplicatedStateBool(byte slot, string ownerPluginId, string stateKey, out bool value)
    {
        if (worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            return player.TryGetReplicatedStateBool(ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    private static IReadOnlyList<GameplayModPackDefinition> GetFilteredGameplayModPacks(string? modPackId)
    {
        var modPacks = CharacterClassCatalog.RuntimeRegistry.ModPacks;
        if (string.IsNullOrWhiteSpace(modPackId))
        {
            return modPacks
                .OrderBy(pack => pack.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(pack => pack.Id, StringComparer.Ordinal)
                .ToArray();
        }

        return modPacks
            .Where(pack => string.Equals(pack.Id, modPackId, StringComparison.Ordinal))
            .ToArray();
    }

    private static string GetOwningModPackId(string itemId)
    {
        return CharacterClassCatalog.RuntimeRegistry.ModPacks
            .First(pack => pack.Items.ContainsKey(itemId))
            .Id;
    }

    private static void AddSelectableItem(
        PlayerEntity player,
        ICollection<OpenGarrisonServerGameplaySelectableItemInfo> items,
        ISet<string> seenItemIds,
        string? itemId,
        string? selectedItemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !seenItemIds.Add(itemId))
        {
            return;
        }

        var item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(itemId);
        items.Add(new OpenGarrisonServerGameplaySelectableItemInfo(
            item.Id,
            item.DisplayName,
            item.Slot,
            item.BehaviorId,
            string.Equals(item.Id, selectedItemId, StringComparison.Ordinal),
            player.OwnsGameplayItem(item.Id),
            item.Ownership?.TrackOwnership ?? false,
            item.Ownership?.DefaultGranted ?? true,
            item.Ownership?.GrantOnAcquire ?? false,
            item.Ownership?.GrantKey));
    }
}
