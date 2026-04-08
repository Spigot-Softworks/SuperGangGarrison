#nullable enable

using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

internal static class GameplayLoadoutMenuModel
{
    public static GameplayLoadoutMenuEntry[] BuildEntries(
        PlayerClass classId,
        PlayerClass localPlayerClassId,
        string localLoadoutId,
        Func<string, bool> ownsGameplayItem)
    {
        ArgumentNullException.ThrowIfNull(ownsGameplayItem);

        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        return GameplayLoadoutSelectionResolver.GetOrderedLoadouts(classId)
            .Select(loadout => new GameplayLoadoutMenuEntry(
                loadout,
                GameplayRuntimeRegistry.LoadoutItemsAreOwned(loadout, ownsGameplayItem),
                classId == localPlayerClassId
                    && string.Equals(loadout.Id, localLoadoutId, StringComparison.Ordinal)))
            .ToArray();
    }

    public static (IReadOnlyList<GameplayLoadoutMenuSlotOption> Left, IReadOnlyList<GameplayLoadoutMenuSlotOption> Right) GetVisualColumns(
        GameplayLoadoutMenuEntry selectedLoadout,
        IReadOnlyList<GameplayLoadoutMenuEntry> loadouts)
    {
        var slotOptions = BuildSlotOptions(selectedLoadout, loadouts);
        var rightOptions = new List<GameplayLoadoutMenuSlotOption>(slotOptions[1].Count + slotOptions[2].Count);
        rightOptions.AddRange(slotOptions[1]);
        rightOptions.AddRange(slotOptions[2]);
        return (slotOptions[0], rightOptions);
    }

    public static string GetRmClassName(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Scout => "RUNNER",
            PlayerClass.Pyro => "FIREBUG",
            PlayerClass.Soldier => "ROCKETMAN",
            PlayerClass.Heavy => "OVERWEIGHT",
            PlayerClass.Demoman => "DETONATOR",
            PlayerClass.Medic => "HEALER",
            PlayerClass.Engineer => "CONSTRUCTOR",
            PlayerClass.Spy => "INFILTRATOR",
            PlayerClass.Sniper => "RIFLEMAN",
            _ => CharacterClassCatalog.GetDefinition(playerClass).DisplayName.ToUpperInvariant(),
        };
    }

    public static string? ResolveLoadoutItemId(GameplayClassLoadoutDefinition loadout, GameplayEquipmentSlot slot)
    {
        return slot switch
        {
            GameplayEquipmentSlot.Primary => loadout.PrimaryItemId,
            GameplayEquipmentSlot.Secondary => loadout.SecondaryItemId,
            GameplayEquipmentSlot.Utility => loadout.UtilityItemId,
            _ => null,
        };
    }

    private static List<GameplayLoadoutMenuSlotOption>[] BuildSlotOptions(
        GameplayLoadoutMenuEntry selectedLoadout,
        IReadOnlyList<GameplayLoadoutMenuEntry> loadouts)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var slots = new[]
        {
            GameplayEquipmentSlot.Primary,
            GameplayEquipmentSlot.Secondary,
            GameplayEquipmentSlot.Utility,
        };
        var options = new List<GameplayLoadoutMenuSlotOption>[slots.Length];
        for (var slotIndex = 0; slotIndex < slots.Length; slotIndex += 1)
        {
            var slot = slots[slotIndex];
            var seenItems = new HashSet<string>(StringComparer.Ordinal);
            var slotOptions = new List<GameplayLoadoutMenuSlotOption>();
            for (var loadoutIndex = 0; loadoutIndex < loadouts.Count; loadoutIndex += 1)
            {
                var candidate = ResolveLoadoutItemId(loadouts[loadoutIndex].Loadout, slot);
                if (string.IsNullOrWhiteSpace(candidate) || !seenItems.Add(candidate))
                {
                    continue;
                }

                var resolvedLoadout = ResolveLoadoutForSlotSelection(slot, candidate, selectedLoadout.Loadout, loadouts);
                if (resolvedLoadout is null)
                {
                    continue;
                }

                var item = runtimeRegistry.GetRequiredItem(candidate);
                slotOptions.Add(new GameplayLoadoutMenuSlotOption(
                    slot,
                    item,
                    resolvedLoadout.Value,
                    string.Equals(ResolveLoadoutItemId(selectedLoadout.Loadout, slot), candidate, StringComparison.Ordinal)));
            }

            slotOptions.Sort(static (left, right) => CompareSlotOptions(left, right));
            options[slotIndex] = slotOptions;
        }

        return options;
    }

    private static GameplayLoadoutMenuEntry? ResolveLoadoutForSlotSelection(
        GameplayEquipmentSlot targetSlot,
        string itemId,
        GameplayClassLoadoutDefinition selectedLoadout,
        IReadOnlyList<GameplayLoadoutMenuEntry> loadouts)
    {
        GameplayLoadoutMenuEntry? best = null;
        var bestScore = int.MinValue;
        for (var index = 0; index < loadouts.Count; index += 1)
        {
            var candidate = loadouts[index];
            if (!string.Equals(ResolveLoadoutItemId(candidate.Loadout, targetSlot), itemId, StringComparison.Ordinal))
            {
                continue;
            }

            var score = 0;
            if (string.Equals(candidate.Loadout.PrimaryItemId, selectedLoadout.PrimaryItemId, StringComparison.Ordinal))
            {
                score += targetSlot == GameplayEquipmentSlot.Primary ? 3 : 1;
            }

            if (string.Equals(candidate.Loadout.SecondaryItemId ?? string.Empty, selectedLoadout.SecondaryItemId ?? string.Empty, StringComparison.Ordinal))
            {
                score += targetSlot == GameplayEquipmentSlot.Secondary ? 3 : 1;
            }

            if (string.Equals(candidate.Loadout.UtilityItemId ?? string.Empty, selectedLoadout.UtilityItemId ?? string.Empty, StringComparison.Ordinal))
            {
                score += targetSlot == GameplayEquipmentSlot.Utility ? 3 : 1;
            }

            if (candidate.IsAvailable)
            {
                score += 2;
            }

            if (best is null || score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static int CompareSlotOptions(GameplayLoadoutMenuSlotOption left, GameplayLoadoutMenuSlotOption right)
    {
        var leftIsStock = IsStockOption(left);
        var rightIsStock = IsStockOption(right);
        if (leftIsStock != rightIsStock)
        {
            return leftIsStock ? -1 : 1;
        }

        return string.Compare(left.Item.DisplayName, right.Item.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStockOption(GameplayLoadoutMenuSlotOption option)
    {
        return option.Loadout.Loadout.Id.EndsWith(".stock", StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.Loadout.Loadout.DisplayName, "Stock", StringComparison.OrdinalIgnoreCase);
    }
}

internal readonly record struct GameplayLoadoutMenuEntry(
    GameplayClassLoadoutDefinition Loadout,
    bool IsAvailable,
    bool IsSelected);

internal readonly record struct GameplayLoadoutMenuSlotOption(
    GameplayEquipmentSlot Slot,
    GameplayItemDefinition Item,
    GameplayLoadoutMenuEntry Loadout,
    bool IsSelected);
