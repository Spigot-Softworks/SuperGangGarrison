#nullable enable

namespace OpenGarrison.Client;

internal static class HudElementId
{
    public const string LocalHealth = "local.health";
    public const string LocalWeaponStack = "local.weapon.stack";
    public const string LocalAbilityStack = "local.ability.stack";
    public const string LocalAbilityPrefix = "local.ability.";
    public const string LocalAbilitySlotPrefix = "local.ability.slot.";
    public const string MatchKillFeed = "match.killfeed";
    public const string MatchCtfPanel = "match.ctf.panel";
    public const string MatchObjectiveStatus = "match.objective.status";
    public const string MatchKothRedTimer = "match.koth.red.timer";
    public const string MatchKothBlueTimer = "match.koth.blue.timer";
    public const string LastToDieRage = "last-to-die.rage";
    public const string LastToDieBuffIcon = "last-to-die.buff-icon";
    public const string ClassMedicUber = "class.medic.uber";
    public const string ClassMedicHealingTarget = "class.medic.healing-target";
    public const string ClassMedicHealer = "class.medic.healer";
    public const string ClassEngineerMetal = "class.engineer.metal";
    public const string ClassEngineerSentry = "class.engineer.sentry";

    public static string LocalAbility(string id) => LocalAbilityPrefix + id;

    public static string LocalAbilitySlot(int index) => $"{LocalAbilitySlotPrefix}{index + 1}";
}
