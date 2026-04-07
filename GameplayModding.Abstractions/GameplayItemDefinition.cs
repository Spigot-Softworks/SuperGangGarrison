namespace OpenGarrison.GameplayModding;

public sealed record GameplayItemDefinition(
    string Id,
    string DisplayName,
    GameplayEquipmentSlot Slot,
    string BehaviorId,
    GameplayItemAmmoDefinition Ammo,
    GameplayItemPresentationDefinition Presentation,
    GameplayItemCombatDefinition? Combat = null,
    GameplayItemOwnershipDefinition? Ownership = null,
    GameplayItemDescriptionDefinition? Description = null);
