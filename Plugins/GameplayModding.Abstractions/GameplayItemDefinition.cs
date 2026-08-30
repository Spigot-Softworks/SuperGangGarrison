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
    GameplayItemDescriptionDefinition? Description = null,
    GameplayAbilityDefinition? Ability = null)
{
    public GameplayItemKind Kind { get; init; } = GameplayItemKind.Unspecified;

    public GameplayWeaponSlot? WeaponSlot { get; init; }

    public IReadOnlyList<string> GrantedAbilityItemIds { get; init; } = [];
}
