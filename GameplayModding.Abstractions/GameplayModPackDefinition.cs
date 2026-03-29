using System;
using System.Collections.Generic;

namespace OpenGarrison.GameplayModding;

public sealed record GameplayModPackDefinition(
    string Id,
    string DisplayName,
    Version Version,
    IReadOnlyDictionary<string, GameplayItemDefinition> Items,
    IReadOnlyDictionary<string, GameplayClassDefinition> Classes);
