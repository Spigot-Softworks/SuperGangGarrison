namespace OpenGarrison.GameplayModding;

public sealed record GameplayWeaponItemRegistration(
    string ItemId,
    string DisplayName,
    GameplayEquipmentSlot Slot,
    string BehaviorId,
    GameplayItemAmmoDefinition Ammo,
    string? ModPackId = null,
    GameplayItemPresentationDefinition? Presentation = null,
    GameplayItemCombatDefinition? Combat = null,
    GameplayItemOwnershipDefinition? Ownership = null,
    GameplayItemDescriptionDefinition? Description = null);
