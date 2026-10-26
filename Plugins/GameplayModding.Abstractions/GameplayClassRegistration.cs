namespace OpenGarrison.GameplayModding;

/// <summary>
/// Registers a plugin class on the neutral Custom player class slot.
/// </summary>
/// <param name="ClassId">The class id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Movement">The movement definition.</param>
/// <param name="DefaultLoadout">The default loadout. Its class id is ignored; the loadout is attached to <paramref name="ClassId"/>.</param>
/// <param name="Presentation">The presentation definition, if any.</param>
/// <param name="BotGraphPlayerClass">The stock player class whose bot graph bots use, or empty for Scout.</param>
/// <param name="PrimaryWeaponKillFeedSprite">The primary weapon kill feed sprite, if any.</param>
/// <param name="ModPackId">The owning mod pack id, if any.</param>
public sealed record GameplayClassRegistration(
    string ClassId,
    string DisplayName,
    GameplayClassMovementDefinition Movement,
    GameplayLoadoutRegistration DefaultLoadout,
    GameplayClassPresentationDefinition? Presentation = null,
    string BotGraphPlayerClass = "",
    string PrimaryWeaponKillFeedSprite = "",
    string? ModPackId = null);
