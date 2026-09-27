namespace OpenGarrison.GameplayModding;

/// <summary>
/// The full definition of a gameplay item in a mod pack.
/// </summary>
/// <param name="Id">The item id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Slot">The equipment slot.</param>
/// <param name="BehaviorId">The behavior id.</param>
/// <param name="Ammo">The ammo definition.</param>
/// <param name="Presentation">The presentation definition.</param>
/// <param name="Combat">The combat definition, if any.</param>
/// <param name="Ownership">The ownership definition, if any.</param>
/// <param name="Description">The description definition, if any.</param>
/// <param name="Ability">The ability definition, if any.</param>
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
    /// <summary>
    /// Gets the item kind.
    /// </summary>
    public GameplayItemKind Kind { get; init; } = GameplayItemKind.Unspecified;

    /// <summary>
    /// Gets the weapon slot, when the item is a weapon.
    /// </summary>
    public GameplayWeaponSlot? WeaponSlot { get; init; }

    /// <summary>
    /// Gets the ability item ids granted with this item.
    /// </summary>
    public IReadOnlyList<string> GrantedAbilityItemIds { get; init; } = [];
}
