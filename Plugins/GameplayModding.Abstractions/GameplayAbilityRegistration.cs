using System.Text.Json;

namespace OpenGarrison.GameplayModding;

/// <summary>
/// Registers a gameplay ability with the host.
/// </summary>
/// <param name="ItemId">The ability item id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Slot">The equipment slot.</param>
/// <param name="BehaviorId">The behavior id.</param>
/// <param name="Ability">The ability definition.</param>
/// <param name="ModPackId">The owning mod pack id, if any.</param>
/// <param name="Presentation">The presentation definition, if any.</param>
public sealed record GameplayAbilityRegistration(
    string ItemId,
    string DisplayName,
    GameplayEquipmentSlot Slot,
    string BehaviorId,
    GameplayAbilityDefinition Ability,
    string? ModPackId = null,
    GameplayItemPresentationDefinition? Presentation = null);

/// <summary>
/// A patch applied to a registered gameplay ability's definition.
/// </summary>
/// <param name="Category">The ability category, or null to leave unchanged.</param>
/// <param name="Activation">The activation kind, or null to leave unchanged.</param>
/// <param name="ExecutorId">The executor id, or null to leave unchanged.</param>
/// <param name="Tags">The ability tags, or null to leave unchanged.</param>
/// <param name="Parameters">The ability parameters, or null to leave unchanged.</param>
public sealed record GameplayAbilityPatch(
    string? Category = null,
    string? Activation = null,
    string? ExecutorId = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyDictionary<string, JsonElement>? Parameters = null);
