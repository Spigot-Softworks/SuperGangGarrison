namespace OpenGarrison.GameplayModding;

public sealed record GameplayItemDefinition(
    string Id,
    string DisplayName,
    GameplayEquipmentSlot Slot,
    string BehaviorId,
    GameplayItemAmmoDefinition Ammo,
    GameplayItemPresentationDefinition Presentation);
