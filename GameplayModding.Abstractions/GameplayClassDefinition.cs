using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

public sealed record GameplayClassDefinition(
    string Id,
    string DisplayName,
    GameplayClassMovementDefinition Movement,
    IReadOnlyDictionary<string, GameplayClassLoadoutDefinition> Loadouts,
    string DefaultLoadoutId);
