using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

/// <summary>
/// A map-authored neutral jump pad. These pads use the normal jump-pad
/// simulation and presentation, but have no owning player or team.
/// </summary>
public readonly record struct JumpPadSpawnMarker(float X, float Y);

public static class JumpPadMetadata
{
    public const string EntityType = "jumpPad";
    public const string NeutralTeamPropertyKey = "team";
    public const string NeutralTeamPropertyValue = "neutral";
    public const string SpriteName = "JumpPadRed";
    public const string BuildSpriteName = "JumpPadRedBuild";

    public static bool IsJumpPadEntityType(string type) =>
        type.Equals(EntityType, StringComparison.OrdinalIgnoreCase);

    public static bool IsNeutral(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null
            || !properties.TryGetValue(NeutralTeamPropertyKey, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Trim().Equals(NeutralTeamPropertyValue, StringComparison.OrdinalIgnoreCase);
    }
}
