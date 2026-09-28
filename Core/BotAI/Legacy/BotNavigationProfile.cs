using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

/// <summary>
/// Legacy profile mapping retained because the OG2 graph builder and navigation
/// diagnostics still use it as a shared class-profile compatibility helper.
/// Do not add new authoring behavior here.
/// </summary>
public enum BotNavigationProfile
{
    Light = 0,
    Standard = 1,
    Heavy = 2,
}

public static class BotNavigationProfiles
{
    public static IReadOnlyList<BotNavigationProfile> All { get; } =
    [
        BotNavigationProfile.Light,
        BotNavigationProfile.Standard,
        BotNavigationProfile.Heavy,
    ];

    public static CharacterClassDefinition GetRepresentativeClassDefinition(BotNavigationProfile profile)
    {
        return profile switch
        {
            BotNavigationProfile.Light => CharacterClassCatalog.Scout,
            BotNavigationProfile.Heavy => CharacterClassCatalog.Heavy,
            _ => CharacterClassCatalog.Soldier,
        };
    }

    public static BotNavigationProfile GetProfileForClass(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout or PlayerClass.Sniper or PlayerClass.Spy => BotNavigationProfile.Light,
            PlayerClass.Heavy => BotNavigationProfile.Heavy,
            _ => BotNavigationProfile.Standard,
        };
    }

    public static string GetFileToken(BotNavigationProfile profile)
    {
        return profile switch
        {
            BotNavigationProfile.Light => "light",
            BotNavigationProfile.Heavy => "heavy",
            _ => "standard",
        };
    }
}
