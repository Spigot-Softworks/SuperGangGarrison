using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public static class BotNavigationClasses
{
    public static IReadOnlyList<PlayerClass> All { get; } =
    [
        PlayerClass.Scout,
        PlayerClass.Engineer,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Demoman,
        PlayerClass.Heavy,
        PlayerClass.Sniper,
        PlayerClass.Medic,
        PlayerClass.Spy,
        PlayerClass.Quote,
    ];

    public static CharacterClassDefinition GetDefinition(PlayerClass classId)
    {
        return CharacterClassCatalog.GetDefinition(classId);
    }

    public static IReadOnlyList<PlayerClass> ResolveApplicableClasses(
        IReadOnlyList<PlayerClass> classes,
        IReadOnlyList<BotNavigationProfile>? legacyProfiles)
    {
        if (classes.Count > 0)
        {
            return classes;
        }

        if (legacyProfiles is not null && legacyProfiles.Count > 0)
        {
            return legacyProfiles
                .SelectMany(GetClassesForProfile)
                .Distinct()
                .OrderBy(static classId => (int)classId)
                .ToArray();
        }

        return Array.Empty<PlayerClass>();
    }

    public static bool AppliesToClass(
        IReadOnlyList<PlayerClass> classes,
        IReadOnlyList<BotNavigationProfile>? legacyProfiles,
        PlayerClass classId)
    {
        var applicableClasses = ResolveApplicableClasses(classes, legacyProfiles);
        return applicableClasses.Count == 0 || applicableClasses.Contains(classId);
    }

    public static bool AppliesToProfile(
        IReadOnlyList<PlayerClass> classes,
        IReadOnlyList<BotNavigationProfile>? legacyProfiles,
        BotNavigationProfile profile)
    {
        var applicableClasses = ResolveApplicableClasses(classes, legacyProfiles);
        if (applicableClasses.Count == 0)
        {
            return true;
        }

        for (var index = 0; index < applicableClasses.Count; index += 1)
        {
            if (BotNavigationProfiles.GetProfileForClass(applicableClasses[index]) == profile)
            {
                return true;
            }
        }

        return false;
    }

    public static string GetDisplayName(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Engineer => "Engineer",
            PlayerClass.Pyro => "Pyro",
            PlayerClass.Soldier => "Soldier",
            PlayerClass.Demoman => "Demoman",
            PlayerClass.Heavy => "Heavy",
            PlayerClass.Sniper => "Sniper",
            PlayerClass.Medic => "Medic",
            PlayerClass.Spy => "Spy",
            PlayerClass.Quote => "Quote",
            _ => "Scout",
        };
    }

    public static string GetShortLabel(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Engineer => "Engi",
            PlayerClass.Soldier => "Soldier",
            PlayerClass.Demoman => "Demo",
            PlayerClass.Heavy => "Heavy",
            PlayerClass.Sniper => "Sniper",
            PlayerClass.Medic => "Medic",
            PlayerClass.Quote => "Quote",
            _ => GetDisplayName(classId),
        };
    }

    public static string GetFileToken(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Engineer => "engineer",
            PlayerClass.Pyro => "pyro",
            PlayerClass.Soldier => "soldier",
            PlayerClass.Demoman => "demoman",
            PlayerClass.Heavy => "heavy",
            PlayerClass.Sniper => "sniper",
            PlayerClass.Medic => "medic",
            PlayerClass.Spy => "spy",
            PlayerClass.Quote => "quote",
            _ => "scout",
        };
    }

    public static IReadOnlyList<PlayerClass> GetClassesForProfile(BotNavigationProfile profile)
    {
        return profile switch
        {
            BotNavigationProfile.Light =>
            [
                PlayerClass.Scout,
                PlayerClass.Sniper,
                PlayerClass.Spy,
            ],
            BotNavigationProfile.Heavy =>
            [
                PlayerClass.Heavy,
            ],
            _ =>
            [
                PlayerClass.Engineer,
                PlayerClass.Pyro,
                PlayerClass.Soldier,
                PlayerClass.Demoman,
                PlayerClass.Medic,
                PlayerClass.Quote,
            ],
        };
    }
}
