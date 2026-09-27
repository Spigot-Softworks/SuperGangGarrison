using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

/// <summary>
/// The definition of a character class in a mod pack.
/// </summary>
/// <param name="Id">The class id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Movement">The movement definition.</param>
/// <param name="Loadouts">The loadouts keyed by loadout id.</param>
/// <param name="DefaultLoadoutId">The default loadout id.</param>
/// <param name="Presentation">The presentation definition, if any.</param>
/// <param name="Runtime">The runtime definition, if any.</param>
public sealed record GameplayClassDefinition(
    string Id,
    string DisplayName,
    GameplayClassMovementDefinition Movement,
    IReadOnlyDictionary<string, GameplayClassLoadoutDefinition> Loadouts,
    string DefaultLoadoutId,
    GameplayClassPresentationDefinition? Presentation = null,
    GameplayClassRuntimeDefinition? Runtime = null);

/// <summary>
/// Runtime bindings for a character class.
/// </summary>
/// <param name="PlayerClass">The player class name.</param>
/// <param name="BasePlayerClass">The base player class name.</param>
/// <param name="BotGraphPlayerClass">The bot graph player class name.</param>
/// <param name="SupportsExperimentalAcquiredWeapon">Whether the experimental acquired weapon is supported.</param>
/// <param name="PrimaryWeaponKillFeedSprite">The primary weapon kill feed sprite.</param>
public sealed record GameplayClassRuntimeDefinition(
    string PlayerClass = "",
    string BasePlayerClass = "",
    string BotGraphPlayerClass = "",
    bool SupportsExperimentalAcquiredWeapon = true,
    string PrimaryWeaponKillFeedSprite = "");

/// <summary>
/// Sprite suffix presentation for a character class.
/// </summary>
/// <param name="SpritePrefix">The sprite prefix.</param>
/// <param name="BaseSuffix">The base suffix.</param>
/// <param name="StandSuffix">The stand suffix, if any.</param>
/// <param name="WalkSuffix">The walk suffix, if any.</param>
/// <param name="RunSuffix">The run suffix, if any.</param>
/// <param name="JumpSuffix">The jump suffix, if any.</param>
/// <param name="LeanLeftSuffix">The lean-left suffix, if any.</param>
/// <param name="LeanRightSuffix">The lean-right suffix, if any.</param>
/// <param name="TauntSuffix">The taunt suffix, if any.</param>
/// <param name="PogoSuffix">The pogo suffix, if any.</param>
/// <param name="PogoTrickSuffix">The pogo trick suffix, if any.</param>
/// <param name="PogoIntelSuffix">The pogo intel suffix, if any.</param>
/// <param name="HumiliationSuffix">The humiliation suffix, if any.</param>
/// <param name="DeadSuffix">The dead suffix, if any.</param>
/// <param name="IntelSuffix">The intel suffix, if any.</param>
/// <param name="ScopedSuffix">The scoped suffix, if any.</param>
/// <param name="HeavyEatSuffix">The heavy eat suffix, if any.</param>
public sealed record GameplayClassPresentationDefinition(
    string SpritePrefix,
    string BaseSuffix = "S",
    string? StandSuffix = null,
    string? WalkSuffix = null,
    string? RunSuffix = null,
    string? JumpSuffix = null,
    string? LeanLeftSuffix = null,
    string? LeanRightSuffix = null,
    string? TauntSuffix = null,
    string? PogoSuffix = null,
    string? PogoTrickSuffix = null,
    string? PogoIntelSuffix = null,
    string? HumiliationSuffix = null,
    string? DeadSuffix = null,
    string? IntelSuffix = null,
    string? ScopedSuffix = null,
    string? HeavyEatSuffix = null);
