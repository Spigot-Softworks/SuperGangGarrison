#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal static class HudLayoutDefaults
{
    private const float SourceHudWidth = 800f;
    private const float SourceHudHeight = 600f;
    private const float SourceAmmoHudBaseY = SourceHudHeight / 1.26f;
    private const float MainAmmoSourceX = 728f;
    private const float MainAmmoSourceY = SourceAmmoHudBaseY + 86f;
    private const float AbilitySourceX = 730f;
    private const float AbilitySourceY = 515f;
    private const float DefaultAbilityWidgetTopY = 494f;
    private const float AbilityWidgetGap = 10f;
    private const float EngineerSentrySourceX = 696f;
    private const float EngineerSentryWidth = 86f;
    private const float EngineerSentryHeight = 64f;
    private const float EngineerSentrySourceY = DefaultAbilityWidgetTopY - AbilityWidgetGap - EngineerSentryHeight;
    private const float EngineerDispenserSourceY = EngineerSentrySourceY - AbilityWidgetGap - EngineerSentryHeight;
    private const float EngineerBuildMenuWidth = 74f;
    private const float EngineerBuildMenuHeight = 244f;
    private const float EngineerBuildMenuSourceX = 37f;
    private const float ClassStatusSourceX = 580f;
    private const float LastToDieRageBarWidth = 170f;
    private const float LastToDieRageBarHeight = 18f;
    private const float LastToDieRageSourceX = 500f;
    private const float LastToDieRageSourceY = 420f;
    private const float LastToDieSpyCloakBarWidth = 170f;
    private const float LastToDieSpyCloakBarHeight = 14f;
    private const float LastToDieSpyCloakSourceX = 500f;
    private const float LastToDieSpyCloakSourceY = 344f;

    public static IReadOnlyDictionary<string, HudElementLayout> Create()
    {
        return new Dictionary<string, HudElementLayout>(StringComparer.Ordinal)
        {
            [HudElementId.LocalHealth] = new(
                HudElementId.LocalHealth,
                HudAnchor.BottomLeft,
                new Vector2(5f, -75f),
                new Vector2(96f, 72f),
                Vector2.Zero,
                Layer: 10),

            [HudElementId.LocalWeaponStack] = new(
                HudElementId.LocalWeaponStack,
                HudAnchor.BottomRight,
                new Vector2(MainAmmoSourceX - SourceHudWidth, MainAmmoSourceY - SourceHudHeight),
                new Vector2(82f, 48f),
                new Vector2(-39f, -48f),
                Layer: 20),

            [HudElementId.LocalAbilityStack] = new(
                HudElementId.LocalAbilityStack,
                HudAnchor.BottomRight,
                new Vector2(AbilitySourceX - SourceHudWidth, AbilitySourceY - SourceHudHeight),
                new Vector2(112f, 74f),
                new Vector2(-34f, -18f),
                Layer: 21),

            [HudElementId.MatchKillFeed] = new(
                HudElementId.MatchKillFeed,
                HudAnchor.TopRight,
                new Vector2(-4f, 59f),
                new Vector2(340f, 104f),
                new Vector2(-340f, -3f),
                Layer: 5),

            [HudElementId.MatchCtfPanel] = new(
                HudElementId.MatchCtfPanel,
                HudAnchor.BottomCenter,
                Vector2.Zero,
                new Vector2(360f, 78f),
                new Vector2(-180f, -72f),
                Layer: 6),

            [HudElementId.MatchObjectiveStatus] = new(
                HudElementId.MatchObjectiveStatus,
                HudAnchor.BottomCenter,
                new Vector2(0f, -40f),
                new Vector2(280f, 64f),
                new Vector2(-140f, -24f),
                Layer: 7),

            [HudElementId.MatchKothRedTimer] = new(
                HudElementId.MatchKothRedTimer,
                HudAnchor.BottomCenter,
                new Vector2(-132f, -28f),
                new Vector2(108f, 46f),
                new Vector2(-54f, -24f),
                Layer: 8),

            [HudElementId.MatchKothBlueTimer] = new(
                HudElementId.MatchKothBlueTimer,
                HudAnchor.BottomCenter,
                new Vector2(132f, -28f),
                new Vector2(108f, 46f),
                new Vector2(-54f, -24f),
                Layer: 9),

            [HudElementId.LastToDieRage] = new(
                HudElementId.LastToDieRage,
                HudAnchor.BottomRight,
                new Vector2(LastToDieRageSourceX - SourceHudWidth, LastToDieRageSourceY - SourceHudHeight),
                new Vector2(LastToDieRageBarWidth + 16f, LastToDieRageBarHeight + 48f),
                new Vector2(-8f, -28f),
                Layer: 9),

            [HudElementId.LastToDieSpyCloak] = new(
                HudElementId.LastToDieSpyCloak,
                HudAnchor.BottomRight,
                new Vector2(LastToDieSpyCloakSourceX - SourceHudWidth, LastToDieSpyCloakSourceY - SourceHudHeight),
                new Vector2(LastToDieSpyCloakBarWidth + 16f, LastToDieSpyCloakBarHeight + 48f),
                new Vector2(-8f, -28f),
                Layer: 9),

            [HudElementId.LastToDieBuffIcon] = new(
                HudElementId.LastToDieBuffIcon,
                HudAnchor.BottomLeft,
                new Vector2(96f, -83f),
                new Vector2(35f, 35f),
                new Vector2(96f, -83f),
                Layer: 11),

            [HudElementId.LastToDieActionStatus] = new(
                HudElementId.LastToDieActionStatus,
                HudAnchor.TopLeft,
                new Vector2(18f, 92f),
                new Vector2(285f, 144f),
                Vector2.Zero,
                Layer: 12),

            [HudElementId.ClassMedicUber] = new(
                HudElementId.ClassMedicUber,
                HudAnchor.BottomRight,
                new Vector2(ClassStatusSourceX - SourceHudWidth, -85f),
                new Vector2(134f, 56f),
                new Vector2(-58f, -20f),
                Layer: 30),

            [HudElementId.ClassEngineerMetal] = new(
                HudElementId.ClassEngineerMetal,
                HudAnchor.BottomRight,
                new Vector2(ClassStatusSourceX - SourceHudWidth, -85f),
                new Vector2(88f, 48f),
                new Vector2(-54f, -18f),
                Layer: 50),

            [HudElementId.ClassEngineerSentry] = new(
                HudElementId.ClassEngineerSentry,
                HudAnchor.BottomRight,
                new Vector2(EngineerSentrySourceX - SourceHudWidth, EngineerSentrySourceY - SourceHudHeight),
                new Vector2(EngineerSentryWidth, EngineerSentryHeight),
                Vector2.Zero,
                Layer: 51),

            [HudElementId.ClassEngineerDispenser] = new(
                HudElementId.ClassEngineerDispenser,
                HudAnchor.BottomRight,
                new Vector2(EngineerSentrySourceX - SourceHudWidth, EngineerDispenserSourceY - SourceHudHeight),
                new Vector2(EngineerSentryWidth, EngineerSentryHeight),
                Vector2.Zero,
                Layer: 52),

            [HudElementId.ClassEngineerBuildMenu] = new(
                HudElementId.ClassEngineerBuildMenu,
                HudAnchor.CenterLeft,
                new Vector2(EngineerBuildMenuSourceX, 0f),
                new Vector2(EngineerBuildMenuWidth, EngineerBuildMenuHeight),
                new Vector2(0f, -20f),
                Layer: 60),
        };
    }
}
