using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

public sealed record GameplayClassDefinition(
    string Id,
    string DisplayName,
    GameplayClassMovementDefinition Movement,
    IReadOnlyDictionary<string, GameplayClassLoadoutDefinition> Loadouts,
    string DefaultLoadoutId,
    GameplayClassPresentationDefinition? Presentation = null,
    GameplayClassRuntimeDefinition? Runtime = null);

public sealed record GameplayClassRuntimeDefinition(
    string PlayerClass = "",
    string BasePlayerClass = "",
    string BotGraphPlayerClass = "",
    bool SupportsExperimentalAcquiredWeapon = true,
    string PrimaryWeaponKillFeedSprite = "");

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
    string? HumiliationSuffix = null,
    string? DeadSuffix = null,
    string? IntelSuffix = null,
    string? ScopedSuffix = null,
    string? HeavyEatSuffix = null);
