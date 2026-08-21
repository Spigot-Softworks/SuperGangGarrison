using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

public sealed record GameplayLoadoutRegistration(
    string ClassId,
    string LoadoutId,
    string DisplayName,
    string PrimaryItemId,
    string? SecondaryItemId = null,
    string? UtilityItemId = null,
    IReadOnlyList<string>? AbilityItemIds = null,
    string? ModPackId = null);

public sealed record GameplaySlotItemRegistration(
    string ClassId,
    GameplayEquipmentSlot Slot,
    string ItemId,
    string? LoadoutId = null,
    string? DisplayName = null,
    string? BaseLoadoutId = null,
    string? ModPackId = null);
