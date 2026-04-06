using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed record GameplayPlayerLoadoutState(
    string ModPackId,
    string ClassId,
    string LoadoutId,
    string PrimaryItemId,
    string? SecondaryItemId,
    string? UtilityItemId,
    GameplayEquipmentSlot EquippedSlot,
    string EquippedItemId,
    string? AcquiredItemId = null);
