using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class BadgeCatalog
{
    private static readonly string[] RewardNames =
    [
        "Badge_Trn1Orng",
        "Badge_Trn1Gold",
        "Badge_Trn2Orng",
        "Badge_Trn2Gold",
        "Badge_Trn3Orng",
        "Badge_Trn3Gold",
        "Badge_RayBannDark",
        "Badge_RayBannLight",
        "Badge_CommunityGld",
        "Badge_CommunityGrn",
        "Badge_Dev",
        "Badge_Admn",
        "Badge_Haxxy",
        "Badge_RunnerRbn",
        "Badge_FirebugRbn",
        "Badge_RocketmanRbn",
        "Badge_OverweightRbn",
        "Badge_DetonatorRbn",
        "Badge_HealerRbn",
        "Badge_ConstructorRbn",
        "Badge_InfiltratorRbn",
        "Badge_RiflemanRbn",
        "Badge_Runner",
        "Badge_Firebug",
        "Badge_Rocketman",
        "Badge_Overweight",
        "Badge_Detonator",
        "Badge_Healer",
        "Badge_Constructor",
        "Badge_Infiltrator",
        "Badge_Rifleman",
        "Badge_Moneybag",
        "Badge_ChessWhite",
        "Badge_ChessBlack",
        "Badge_B-BallBlitz",
        "Badge_Elton-Jump-athon",
        "Badge_2spooky4u",
        "Badge_Rebel-ution",
        "Badge_GloryDays",
        "Badge_DynamicDuo",
        "Badge_ArmyOfOne",
        "Badge_ThreeMajor",
        "Badge_ThreeMinor",
        "Badge_ThreeCTF",
        "Badge_ThreeEtc",
        "Badge_ThreeKoTH",
        "Badge_BBall2015",
        "Badge_Elton2015",
        "Badge_InevitableOne2015",
        "Badge_Warpacks2015",
        "Badge_BannsLawsOfRobotics",
    ];

    private static readonly Dictionary<string, int> RewardIndexByName = BuildRewardIndexByName();

    public const int MaxBadgeCount = 51;
    public const ulong SupportedMask = (1UL << MaxBadgeCount) - 1UL;

    public static ulong ParseRewardString(string? rewardString)
    {
        if (string.IsNullOrWhiteSpace(rewardString))
        {
            return 0UL;
        }

        var badgeMask = 0UL;
        var rewardNames = rewardString.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < rewardNames.Length; index += 1)
        {
            if (RewardIndexByName.TryGetValue(rewardNames[index], out var badgeIndex))
            {
                badgeMask |= 1UL << badgeIndex;
            }
        }

        return badgeMask;
    }

    public static ulong SanitizeBadgeMask(ulong badgeMask)
    {
        return badgeMask & SupportedMask;
    }

    public static IEnumerable<int> EnumerateBadgeIndices(ulong badgeMask)
    {
        var sanitizedMask = SanitizeBadgeMask(badgeMask);
        for (var badgeIndex = 0; badgeIndex < RewardNames.Length; badgeIndex += 1)
        {
            if ((sanitizedMask & (1UL << badgeIndex)) != 0)
            {
                yield return badgeIndex;
            }
        }
    }

    public static int CountBadges(ulong badgeMask)
    {
        var sanitizedMask = SanitizeBadgeMask(badgeMask);
        var count = 0;
        while (sanitizedMask != 0)
        {
            count += (int)(sanitizedMask & 1UL);
            sanitizedMask >>= 1;
        }

        return count;
    }

    private static Dictionary<string, int> BuildRewardIndexByName()
    {
        var rewardIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < RewardNames.Length; index += 1)
        {
            rewardIndexByName[RewardNames[index]] = index;
        }

        return rewardIndexByName;
    }
}
