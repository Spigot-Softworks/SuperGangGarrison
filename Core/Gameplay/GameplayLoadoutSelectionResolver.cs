using OpenGarrison.GameplayModding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Core;

public static class GameplayLoadoutSelectionResolver
{
    public static IReadOnlyList<GameplayClassLoadoutDefinition> GetOrderedLoadouts(PlayerClass playerClass)
    {
        // Unbound slots (Custom) have no loadouts of their own; callers holding a player
        // should pass its class definition instead.
        return CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(playerClass, out var binding)
            ? GetOrderedLoadouts(CharacterClassCatalog.RuntimeRegistry.GetRequiredClass(binding.ClassId))
            : [];
    }

    public static IReadOnlyList<GameplayClassLoadoutDefinition> GetOrderedLoadouts(CharacterClassDefinition classDefinition)
    {
        return GetOrderedLoadouts(CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(classDefinition));
    }

    private static IReadOnlyList<GameplayClassLoadoutDefinition> GetOrderedLoadouts(GameplayClassDefinition gameplayClass)
    {
        return gameplayClass
            .Loadouts
            .Values
            .OrderBy(loadout => loadout.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(loadout => loadout.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool TryResolveLoadoutId(PlayerClass playerClass, string selection, out string loadoutId)
    {
        loadoutId = string.Empty;
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        if (!runtimeRegistry.TryGetClassBinding(playerClass, out var binding))
        {
            return false;
        }

        var gameplayClass = runtimeRegistry.GetRequiredClass(binding.ClassId);
        var candidates = GetOrderedLoadouts(gameplayClass);
        var trimmedSelection = selection.Trim();
        if (trimmedSelection.Length == 0)
        {
            return false;
        }

        if (gameplayClass.Loadouts.ContainsKey(trimmedSelection))
        {
            loadoutId = trimmedSelection;
            return true;
        }

        if (int.TryParse(trimmedSelection, out var parsedIndex)
            && parsedIndex >= 1
            && parsedIndex <= candidates.Count)
        {
            loadoutId = candidates[parsedIndex - 1].Id;
            return true;
        }

        var exactDisplayMatch = candidates.FirstOrDefault(loadout =>
            string.Equals(loadout.DisplayName, trimmedSelection, StringComparison.OrdinalIgnoreCase));
        if (exactDisplayMatch is not null)
        {
            loadoutId = exactDisplayMatch.Id;
            return true;
        }

        var prefixMatches = candidates
            .Where(loadout => loadout.DisplayName.StartsWith(trimmedSelection, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (prefixMatches.Length == 1)
        {
            loadoutId = prefixMatches[0].Id;
            return true;
        }

        return false;
    }
}
