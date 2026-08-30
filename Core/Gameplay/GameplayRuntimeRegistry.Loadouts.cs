using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class GameplayRuntimeRegistry
{
    public GameplayPlayerLoadoutState CreatePlayerLoadoutState(
        PlayerClass playerClass,
        string? loadoutId = null,
        GameplayEquipmentSlot equippedSlot = GameplayEquipmentSlot.Primary,
        string? secondaryItemOverrideId = null,
        string? acquiredItemId = null,
        string? selectedPrimaryItemId = null)
    {
        return CreatePlayerLoadoutState(
            GetRequiredClassBinding(playerClass).ClassId,
            loadoutId,
            equippedSlot,
            secondaryItemOverrideId,
            acquiredItemId,
            selectedPrimaryItemId);
    }

    public GameplayPlayerLoadoutState CreatePlayerLoadoutState(
        string gameplayClassId,
        string? loadoutId = null,
        GameplayEquipmentSlot equippedSlot = GameplayEquipmentSlot.Primary,
        string? secondaryItemOverrideId = null,
        string? acquiredItemId = null,
        string? selectedPrimaryItemId = null)
    {
        if (TryCreateValidatedPlayerLoadoutState(
                gameplayClassId,
                loadoutId,
                equippedSlot,
                secondaryItemOverrideId,
                acquiredItemId,
                selectedPrimaryItemId,
                out var loadoutState))
        {
            return loadoutState;
        }

        return CreateFallbackPlayerLoadoutState(gameplayClassId);
    }

    public bool TryCreateValidatedPlayerLoadoutState(
        PlayerClass playerClass,
        string? loadoutId,
        GameplayEquipmentSlot equippedSlot,
        string? secondaryItemOverrideId,
        string? acquiredItemId,
        out GameplayPlayerLoadoutState loadoutState)
    {
        return TryCreateValidatedPlayerLoadoutState(
            GetRequiredClassBinding(playerClass).ClassId,
            loadoutId,
            equippedSlot,
            secondaryItemOverrideId,
            acquiredItemId,
            selectedPrimaryItemId: null,
            out loadoutState);
    }

    public bool TryCreateValidatedPlayerLoadoutState(
        PlayerClass playerClass,
        string? loadoutId,
        GameplayEquipmentSlot equippedSlot,
        string? secondaryItemOverrideId,
        string? acquiredItemId,
        string? selectedPrimaryItemId,
        out GameplayPlayerLoadoutState loadoutState)
    {
        return TryCreateValidatedPlayerLoadoutState(
            GetRequiredClassBinding(playerClass).ClassId,
            loadoutId,
            equippedSlot,
            secondaryItemOverrideId,
            acquiredItemId,
            selectedPrimaryItemId,
            out loadoutState);
    }

    public bool TryCreateValidatedPlayerLoadoutState(
        string gameplayClassId,
        string? loadoutId,
        GameplayEquipmentSlot equippedSlot,
        string? secondaryItemOverrideId,
        string? acquiredItemId,
        out GameplayPlayerLoadoutState loadoutState)
    {
        return TryCreateValidatedPlayerLoadoutState(
            gameplayClassId,
            loadoutId,
            equippedSlot,
            secondaryItemOverrideId,
            acquiredItemId,
            selectedPrimaryItemId: null,
            out loadoutState);
    }

    public bool TryCreateValidatedPlayerLoadoutState(
        string gameplayClassId,
        string? loadoutId,
        GameplayEquipmentSlot equippedSlot,
        string? secondaryItemOverrideId,
        string? acquiredItemId,
        string? selectedPrimaryItemId,
        out GameplayPlayerLoadoutState loadoutState)
    {
        var binding = GetRequiredClassBinding(gameplayClassId);
        var loadout = ResolveValidatedLoadout(gameplayClassId, loadoutId);
        var primaryItemId = ResolveValidatedPrimaryItemId(loadout, selectedPrimaryItemId);
        var secondaryItemId = ResolveValidatedSecondaryItemId(gameplayClassId, loadout, secondaryItemOverrideId);
        var utilityItemId = loadout.UtilityItemId;
        var validatedAcquiredItemId = ResolveValidatedAcquiredItemId(gameplayClassId, acquiredItemId);
        var validatedEquippedSlot = ResolveValidatedEquippedSlot(
            equippedSlot,
            primaryItemId,
            secondaryItemId,
            validatedAcquiredItemId);

        var equippedItemId = validatedEquippedSlot switch
        {
            GameplayEquipmentSlot.Secondary => validatedAcquiredItemId ?? secondaryItemId ?? primaryItemId,
            _ => primaryItemId,
        };

        loadoutState = new GameplayPlayerLoadoutState(
            ModPackId: binding.ModPackId,
            ClassId: binding.ClassId,
            LoadoutId: loadout.Id,
            PrimaryItemId: primaryItemId,
            SecondaryItemId: secondaryItemId,
            UtilityItemId: utilityItemId,
            EquippedSlot: validatedEquippedSlot,
            EquippedItemId: equippedItemId,
            AcquiredItemId: validatedAcquiredItemId,
            AbilityItemIds: loadout.Abilities);
        return true;
    }

    public bool CanUsePrimaryItem(PlayerClass playerClass, string? loadoutId, string? primaryItemId)
    {
        return CanUsePrimaryItem(GetRequiredClassBinding(playerClass).ClassId, loadoutId, primaryItemId);
    }

    public bool CanUsePrimaryItem(string gameplayClassId, string? loadoutId, string? primaryItemId)
    {
        var loadout = ResolveValidatedLoadout(gameplayClassId, loadoutId);
        if (string.IsNullOrWhiteSpace(primaryItemId) || loadout.Primary is null)
        {
            return false;
        }

        var normalizedItemId = primaryItemId.Trim();
        return loadout.Primary.ItemIds.Contains(normalizedItemId, StringComparer.Ordinal)
            && TryGetItem(normalizedItemId, out var item)
            && item.Kind == GameplayItemKind.Weapon
            && item.WeaponSlot == GameplayWeaponSlot.Primary;
    }

    public bool CanUseSecondaryOverrideItem(PlayerClass playerClass, string? secondaryItemId)
    {
        return CanUseSecondaryOverrideItem(GetRequiredClassBinding(playerClass).ClassId, secondaryItemId);
    }

    public bool CanUseSecondaryOverrideItem(string gameplayClassId, string? secondaryItemId)
    {
        return CanUseSecondaryOverrideItem(gameplayClassId, GetDefaultLoadout(gameplayClassId), secondaryItemId);
    }

    public bool CanUseSecondaryOverrideItem(PlayerClass playerClass, string? loadoutId, string? secondaryItemId)
    {
        return CanUseSecondaryOverrideItem(GetRequiredClassBinding(playerClass).ClassId, loadoutId, secondaryItemId);
    }

    public bool CanUseSecondaryOverrideItem(string gameplayClassId, string? loadoutId, string? secondaryItemId)
    {
        return CanUseSecondaryOverrideItem(gameplayClassId, ResolveValidatedLoadout(gameplayClassId, loadoutId), secondaryItemId);
    }

    public bool CanUseAcquiredItem(PlayerClass playerClass, string? acquiredItemId)
    {
        return CanUseAcquiredItem(GetRequiredClassBinding(playerClass).ClassId, acquiredItemId);
    }

    public bool CanUseAcquiredItem(string gameplayClassId, string? acquiredItemId)
    {
        return string.IsNullOrWhiteSpace(acquiredItemId)
            || (SupportsExperimentalAcquiredWeapon(gameplayClassId)
                && TryGetItem(acquiredItemId, out var acquiredItem)
                && acquiredItem.Kind == GameplayItemKind.Weapon
                && acquiredItem.WeaponSlot == GameplayWeaponSlot.Primary);
    }

    public bool CanEquipSlot(
        PlayerClass playerClass,
        string? loadoutId,
        GameplayEquipmentSlot equippedSlot,
        string? secondaryItemOverrideId = null,
        string? acquiredItemId = null)
    {
        return CanEquipSlot(
            GetRequiredClassBinding(playerClass).ClassId,
            loadoutId,
            equippedSlot,
            secondaryItemOverrideId,
            acquiredItemId);
    }

    public bool CanEquipSlot(
        string gameplayClassId,
        string? loadoutId,
        GameplayEquipmentSlot equippedSlot,
        string? secondaryItemOverrideId = null,
        string? acquiredItemId = null)
    {
        var loadout = ResolveValidatedLoadout(gameplayClassId, loadoutId);
        var secondaryItemId = ResolveValidatedSecondaryItemId(gameplayClassId, loadout, secondaryItemOverrideId);
        var validatedAcquiredItemId = ResolveValidatedAcquiredItemId(gameplayClassId, acquiredItemId);
        return ResolveValidatedEquippedSlot(
            equippedSlot,
            loadout.Primary?.DefaultItemId ?? loadout.PrimaryItemId,
            secondaryItemId,
            validatedAcquiredItemId) == equippedSlot;
    }

    private bool CanUseSecondaryOverrideItem(string gameplayClassId, GameplayClassLoadoutDefinition loadout, string? secondaryItemId)
    {
        if (string.IsNullOrWhiteSpace(secondaryItemId))
        {
            return true;
        }

        var defaultSecondaryItemId = loadout.Secondary?.ItemId;
        if (string.Equals(defaultSecondaryItemId, secondaryItemId, StringComparison.Ordinal))
        {
            return true;
        }

        var binding = GetRequiredClassBinding(gameplayClassId);
        return (binding.SupportsExperimentalAcquiredWeapon
            || binding.PlayerClass == PlayerClass.Engineer)
            && TryGetItem(secondaryItemId, out var secondaryItem)
            && secondaryItem.Kind == GameplayItemKind.Weapon;
    }

    private GameplayPlayerLoadoutState CreateFallbackPlayerLoadoutState(PlayerClass playerClass)
    {
        return CreateFallbackPlayerLoadoutState(GetRequiredClassBinding(playerClass).ClassId);
    }

    private GameplayPlayerLoadoutState CreateFallbackPlayerLoadoutState(string gameplayClassId)
    {
        var binding = GetRequiredClassBinding(gameplayClassId);
        var loadout = GetDefaultLoadout(gameplayClassId);
        var primaryItemId = loadout.Primary?.DefaultItemId ?? loadout.PrimaryItemId;
        return new GameplayPlayerLoadoutState(
            ModPackId: binding.ModPackId,
            ClassId: binding.ClassId,
            LoadoutId: loadout.Id,
            PrimaryItemId: primaryItemId,
            SecondaryItemId: loadout.Secondary?.ItemId,
            UtilityItemId: loadout.UtilityItemId,
            EquippedSlot: GameplayEquipmentSlot.Primary,
            EquippedItemId: primaryItemId,
            AcquiredItemId: null,
            AbilityItemIds: loadout.Abilities);
    }

    private GameplayClassLoadoutDefinition ResolveValidatedLoadout(PlayerClass playerClass, string? loadoutId)
    {
        return ResolveValidatedLoadout(GetRequiredClassBinding(playerClass).ClassId, loadoutId);
    }

    private GameplayClassLoadoutDefinition ResolveValidatedLoadout(string gameplayClassId, string? loadoutId)
    {
        return TryGetLoadout(gameplayClassId, loadoutId, out var loadout)
            ? loadout
            : GetDefaultLoadout(gameplayClassId);
    }

    private string? ResolveValidatedSecondaryItemId(string gameplayClassId, GameplayClassLoadoutDefinition loadout, string? secondaryItemOverrideId)
    {
        return CanUseSecondaryOverrideItem(gameplayClassId, loadout, secondaryItemOverrideId)
            ? (string.IsNullOrWhiteSpace(secondaryItemOverrideId) ? loadout.Secondary?.ItemId : secondaryItemOverrideId.Trim())
            : loadout.Secondary?.ItemId;
    }

    private string ResolveValidatedPrimaryItemId(
        GameplayClassLoadoutDefinition loadout,
        string? selectedPrimaryItemId)
    {
        var defaultPrimaryItemId = loadout.Primary?.DefaultItemId ?? loadout.PrimaryItemId;
        if (string.IsNullOrWhiteSpace(selectedPrimaryItemId))
        {
            return defaultPrimaryItemId;
        }

        var normalizedItemId = selectedPrimaryItemId.Trim();
        return loadout.Primary is not null
            && loadout.Primary.ItemIds.Contains(normalizedItemId, StringComparer.Ordinal)
            && TryGetItem(normalizedItemId, out var item)
            && item.Kind == GameplayItemKind.Weapon
            && item.WeaponSlot == GameplayWeaponSlot.Primary
                ? normalizedItemId
                : defaultPrimaryItemId;
    }

    private string? ResolveValidatedAcquiredItemId(PlayerClass playerClass, string? acquiredItemId)
    {
        return ResolveValidatedAcquiredItemId(GetRequiredClassBinding(playerClass).ClassId, acquiredItemId);
    }

    private string? ResolveValidatedAcquiredItemId(string gameplayClassId, string? acquiredItemId)
    {
        return CanUseAcquiredItem(gameplayClassId, acquiredItemId) && !string.IsNullOrWhiteSpace(acquiredItemId)
            ? acquiredItemId
            : null;
    }

    private static GameplayEquipmentSlot ResolveValidatedEquippedSlot(
        GameplayEquipmentSlot requestedSlot,
        string primaryItemId,
        string? secondaryItemId,
        string? acquiredItemId)
    {
        return requestedSlot switch
        {
            GameplayEquipmentSlot.Secondary when !string.IsNullOrWhiteSpace(acquiredItemId) || !string.IsNullOrWhiteSpace(secondaryItemId)
                => GameplayEquipmentSlot.Secondary,
            _ => GameplayEquipmentSlot.Primary,
        };
    }
}
