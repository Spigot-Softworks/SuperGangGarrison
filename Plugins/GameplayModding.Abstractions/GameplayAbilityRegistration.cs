using System.Text.Json;

namespace OpenGarrison.GameplayModding;

public sealed record GameplayAbilityRegistration(
    string ItemId,
    string DisplayName,
    GameplayEquipmentSlot Slot,
    string BehaviorId,
    GameplayAbilityDefinition Ability,
    string? ModPackId = null,
    GameplayItemPresentationDefinition? Presentation = null);

public sealed record GameplayAbilityPatch(
    string? Category = null,
    string? Activation = null,
    string? ExecutorId = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyDictionary<string, JsonElement>? Parameters = null);
