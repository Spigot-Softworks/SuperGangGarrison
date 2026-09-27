namespace OpenGarrison.GameplayModding;

/// <summary>
/// Registers a weapon item with the host.
/// </summary>
/// <param name="ItemId">The weapon item id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Slot">The equipment slot.</param>
/// <param name="BehaviorId">The behavior id.</param>
/// <param name="Ammo">The ammo definition.</param>
/// <param name="ModPackId">The owning mod pack id, if any.</param>
/// <param name="Presentation">The presentation definition, if any.</param>
/// <param name="Combat">The combat definition, if any.</param>
/// <param name="Ownership">The ownership definition, if any.</param>
/// <param name="Description">The description definition, if any.</param>
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
    GameplayItemDescriptionDefinition? Description = null)
{
    /// <summary>
    /// Gets the ability item ids granted with this weapon.
    /// </summary>
    public IReadOnlyList<string> GrantedAbilityItemIds { get; init; } = [];
}
