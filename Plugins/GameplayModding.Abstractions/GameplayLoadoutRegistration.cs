using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

/// <summary>
/// Registers a loadout for a class.
/// </summary>
/// <param name="ClassId">The class id.</param>
/// <param name="LoadoutId">The loadout id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="PrimaryItemId">The primary item id.</param>
/// <param name="SecondaryItemId">The secondary item id, if any.</param>
/// <param name="UtilityItemId">The utility item id, if any.</param>
/// <param name="AbilityItemIds">The ability item ids, if any.</param>
/// <param name="ModPackId">The owning mod pack id, if any.</param>
public sealed record GameplayLoadoutRegistration(
    string ClassId,
    string LoadoutId,
    string DisplayName,
    string PrimaryItemId,
    string? SecondaryItemId = null,
    string? UtilityItemId = null,
    IReadOnlyList<string>? AbilityItemIds = null,
    string? ModPackId = null);

/// <summary>
/// Registers an item into an equipment slot of a class loadout.
/// </summary>
/// <param name="ClassId">The class id.</param>
/// <param name="Slot">The equipment slot.</param>
/// <param name="ItemId">The item id.</param>
/// <param name="LoadoutId">The loadout id, if any.</param>
/// <param name="DisplayName">The display name, if any.</param>
/// <param name="BaseLoadoutId">The base loadout id, if any.</param>
/// <param name="ModPackId">The owning mod pack id, if any.</param>
public sealed record GameplaySlotItemRegistration(
    string ClassId,
    GameplayEquipmentSlot Slot,
    string ItemId,
    string? LoadoutId = null,
    string? DisplayName = null,
    string? BaseLoadoutId = null,
    string? ModPackId = null);
