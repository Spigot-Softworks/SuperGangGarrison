using System.Collections.Generic;

namespace OpenGarrison.Core;

public readonly record struct MapImportedEntity(
    string Type,
    float X,
    float Y,
    IReadOnlyDictionary<string, string> Properties);
