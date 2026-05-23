using System;

namespace OpenGarrison.Core;

public static class GameplayAbilityConstants
{
    // These categories have stock simulation dispatch hooks today.
    public const string SecondaryCategory = "secondary";
    public const string UtilityCategory = "utility";
    public const string PassiveCategory = "passive";
    public const string TauntCategory = "taunt";

    // These names are reserved semantic categories; callers must add a dispatch hook
    // before ability definitions in these categories can execute automatically.
    public const string MovementCategory = "movement";
    public const string PrimaryAltCategory = "primary_alt";
    public const string StatusCategory = "status";

    public const string PressedActivation = "pressed";
    public const string HeldActivation = "held";
    public const string ReleasedActivation = "released";
    public const string PassiveTickActivation = "passive_tick";

    public const string CoreAbilityReplicatedStateOwnerId = "core.ability";
    public const string CoreSecondaryInputTag = "core_m2";

    public static bool IsBuiltInDispatchedCategory(string? category)
    {
        return string.Equals(category, SecondaryCategory, StringComparison.Ordinal)
            || string.Equals(category, UtilityCategory, StringComparison.Ordinal)
            || string.Equals(category, PassiveCategory, StringComparison.Ordinal)
            || string.Equals(category, TauntCategory, StringComparison.Ordinal);
    }

    public static bool IsReservedCategory(string? category)
    {
        return string.Equals(category, MovementCategory, StringComparison.Ordinal)
            || string.Equals(category, PrimaryAltCategory, StringComparison.Ordinal)
            || string.Equals(category, StatusCategory, StringComparison.Ordinal);
    }
}
