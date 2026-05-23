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
        return CreatePlayerLoadoutState(
            GetRequiredClassBinding(playerClass).ClassId,
            loadoutId,
            equippedSlot,
            secondaryItemOverrideId,
            acquiredItemId);
    }

    public GameplayPlayerLoadoutState CreatePlayerLoadoutState(
        string gameplayClassId,
        string? loadoutId = null,
        GameplayEquipmentSlot equippedSlot = GameplayEquipmentSlot.Primary,
        string? secondaryItemOverrideId = null,
        string? acquiredItemId = null)
    {
        if (TryCreateValidatedPlayerLoadoutState(gameplayClassId, loadoutId, equippedSlot, secondaryItemOverrideId, acquiredItemId, out var loadoutState))
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
        var binding = GetRequiredClassBinding(gameplayClassId);
        var loadout = ResolveValidatedLoadout(gameplayClassId, loadoutId);
        var primaryItemId = loadout.PrimaryItemId;
        var secondaryItemId = ResolveValidatedSecondaryItemId(gameplayClassId, loadout, secondaryItemOverrideId);
        var utilityItemId = loadout.UtilityItemId;
        var validatedAcquiredItemId = ResolveValidatedAcquiredItemId(gameplayClassId, acquiredItemId);
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
                && acquiredItem.Slot == GameplayEquipmentSlot.Primary);
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
        var utilityItemId = loadout.UtilityItemId;
        var validatedAcquiredItemId = ResolveValidatedAcquiredItemId(gameplayClassId, acquiredItemId);
        return ResolveValidatedEquippedSlot(
            equippedSlot,
            loadout.PrimaryItemId,
            secondaryItemId,
            utilityItemId,
            validatedAcquiredItemId) == equippedSlot;
    }

    private bool CanUseSecondaryOverrideItem(string gameplayClassId, GameplayClassLoadoutDefinition loadout, string? secondaryItemId)
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

        var binding = GetRequiredClassBinding(gameplayClassId);
        return (binding.SupportsExperimentalAcquiredWeapon
            || binding.PlayerClass == PlayerClass.Engineer)
            && TryGetItem(secondaryItemId, out var secondaryItem)
            && secondaryItem.Slot == GameplayEquipmentSlot.Primary;
    }

    private GameplayPlayerLoadoutState CreateFallbackPlayerLoadoutState(PlayerClass playerClass)
    {
        return CreateFallbackPlayerLoadoutState(GetRequiredClassBinding(playerClass).ClassId);
    }

    private GameplayPlayerLoadoutState CreateFallbackPlayerLoadoutState(string gameplayClassId)
    {
        var binding = GetRequiredClassBinding(gameplayClassId);
        var loadout = GetDefaultLoadout(gameplayClassId);
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
            ? (string.IsNullOrWhiteSpace(secondaryItemOverrideId) ? loadout.SecondaryItemId : secondaryItemOverrideId)
            : loadout.SecondaryItemId;
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
