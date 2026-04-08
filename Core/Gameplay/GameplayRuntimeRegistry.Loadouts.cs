using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class GameplayRuntimeRegistry
{
    public GameplayPlayerLoadoutState CreatePlayerLoadoutState(
        PlayerClass playerClass,
        string? loadoutId = null,
        GameplayEquipmentSlot equippedSlot = GameplayEquipmentSlot.Primary,
        string? secondaryItemOverrideId = null,
        string? acquiredItemId = null)
    {
        if (TryCreateValidatedPlayerLoadoutState(playerClass, loadoutId, equippedSlot, secondaryItemOverrideId, acquiredItemId, out var loadoutState))
        {
            return loadoutState;
        }

        return CreateFallbackPlayerLoadoutState(playerClass);
    }

    public bool TryCreateValidatedPlayerLoadoutState(
        PlayerClass playerClass,
        string? loadoutId,
        GameplayEquipmentSlot equippedSlot,
        string? secondaryItemOverrideId,
        string? acquiredItemId,
        out GameplayPlayerLoadoutState loadoutState)
    {
        var binding = GetRequiredClassBinding(playerClass);
        var loadout = ResolveValidatedLoadout(playerClass, loadoutId);
        var primaryItemId = loadout.PrimaryItemId;
        var secondaryItemId = ResolveValidatedSecondaryItemId(playerClass, loadout, secondaryItemOverrideId);
        var utilityItemId = loadout.UtilityItemId;
        var validatedAcquiredItemId = ResolveValidatedAcquiredItemId(playerClass, acquiredItemId);
        var validatedEquippedSlot = ResolveValidatedEquippedSlot(
            equippedSlot,
            primaryItemId,
            secondaryItemId,
            utilityItemId,
            validatedAcquiredItemId);

        var equippedItemId = validatedEquippedSlot switch
        {
            GameplayEquipmentSlot.Secondary => validatedAcquiredItemId ?? secondaryItemId ?? primaryItemId,
            GameplayEquipmentSlot.Utility => utilityItemId ?? primaryItemId,
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
            AcquiredItemId: validatedAcquiredItemId);
        return true;
    }

    public bool CanUseSecondaryOverrideItem(PlayerClass playerClass, string? secondaryItemId)
    {
        return CanUseSecondaryOverrideItem(playerClass, GetDefaultLoadout(playerClass), secondaryItemId);
    }

    public bool CanUseSecondaryOverrideItem(PlayerClass playerClass, string? loadoutId, string? secondaryItemId)
    {
        return CanUseSecondaryOverrideItem(playerClass, ResolveValidatedLoadout(playerClass, loadoutId), secondaryItemId);
    }

    public bool CanUseAcquiredItem(PlayerClass playerClass, string? acquiredItemId)
    {
        return string.IsNullOrWhiteSpace(acquiredItemId)
            || (SupportsExperimentalAcquiredWeapon(playerClass)
                && TryGetItem(acquiredItemId, out var acquiredItem)
                && acquiredItem.Slot == GameplayEquipmentSlot.Primary);
    }

    public bool CanEquipSlot(
        PlayerClass playerClass,
        string? loadoutId,
        GameplayEquipmentSlot equippedSlot,
        string? secondaryItemOverrideId = null,
        string? acquiredItemId = null)
    {
        var loadout = ResolveValidatedLoadout(playerClass, loadoutId);
        var secondaryItemId = ResolveValidatedSecondaryItemId(playerClass, loadout, secondaryItemOverrideId);
        var utilityItemId = loadout.UtilityItemId;
        var validatedAcquiredItemId = ResolveValidatedAcquiredItemId(playerClass, acquiredItemId);
        return ResolveValidatedEquippedSlot(
            equippedSlot,
            loadout.PrimaryItemId,
            secondaryItemId,
            utilityItemId,
            validatedAcquiredItemId) == equippedSlot;
    }

    private bool CanUseSecondaryOverrideItem(PlayerClass playerClass, GameplayClassLoadoutDefinition loadout, string? secondaryItemId)
    {
        if (string.IsNullOrWhiteSpace(secondaryItemId))
        {
            return true;
        }

        var defaultSecondaryItemId = loadout.SecondaryItemId;
        if (string.Equals(defaultSecondaryItemId, secondaryItemId, StringComparison.Ordinal))
        {
            return true;
        }

        return SupportsExperimentalAcquiredWeapon(playerClass)
            && TryGetItem(secondaryItemId, out var secondaryItem)
            && secondaryItem.Slot == GameplayEquipmentSlot.Primary;
    }

    private GameplayPlayerLoadoutState CreateFallbackPlayerLoadoutState(PlayerClass playerClass)
    {
        var binding = GetRequiredClassBinding(playerClass);
        var loadout = GetDefaultLoadout(playerClass);
        return new GameplayPlayerLoadoutState(
            ModPackId: binding.ModPackId,
            ClassId: binding.ClassId,
            LoadoutId: loadout.Id,
            PrimaryItemId: loadout.PrimaryItemId,
            SecondaryItemId: loadout.SecondaryItemId,
            UtilityItemId: loadout.UtilityItemId,
            EquippedSlot: GameplayEquipmentSlot.Primary,
            EquippedItemId: loadout.PrimaryItemId,
            AcquiredItemId: null);
    }

    private GameplayClassLoadoutDefinition ResolveValidatedLoadout(PlayerClass playerClass, string? loadoutId)
    {
        return TryGetLoadout(playerClass, loadoutId, out var loadout)
            ? loadout
            : GetDefaultLoadout(playerClass);
    }

    private string? ResolveValidatedSecondaryItemId(PlayerClass playerClass, GameplayClassLoadoutDefinition loadout, string? secondaryItemOverrideId)
    {
        return CanUseSecondaryOverrideItem(playerClass, loadout, secondaryItemOverrideId)
            ? (string.IsNullOrWhiteSpace(secondaryItemOverrideId) ? loadout.SecondaryItemId : secondaryItemOverrideId)
            : loadout.SecondaryItemId;
    }

    private string? ResolveValidatedAcquiredItemId(PlayerClass playerClass, string? acquiredItemId)
    {
        return CanUseAcquiredItem(playerClass, acquiredItemId) && !string.IsNullOrWhiteSpace(acquiredItemId)
            ? acquiredItemId
            : null;
    }

    private static GameplayEquipmentSlot ResolveValidatedEquippedSlot(
        GameplayEquipmentSlot requestedSlot,
        string primaryItemId,
        string? secondaryItemId,
        string? utilityItemId,
        string? acquiredItemId)
    {
        return requestedSlot switch
        {
            GameplayEquipmentSlot.Secondary when !string.IsNullOrWhiteSpace(acquiredItemId) || !string.IsNullOrWhiteSpace(secondaryItemId)
                => GameplayEquipmentSlot.Secondary,
            GameplayEquipmentSlot.Utility when !string.IsNullOrWhiteSpace(utilityItemId)
                => GameplayEquipmentSlot.Utility,
            _ => GameplayEquipmentSlot.Primary,
        };
    }
}
