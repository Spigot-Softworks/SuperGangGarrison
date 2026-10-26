#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal static class HudLayoutDefaults
{
    internal static HudElementLayout CreateConquestScoreCounter(HudElementLayout template, bool blueTeam) =>
        template with { Anchor = HudAnchor.TopCenter, Offset = new Vector2(blueTeam ? 162f : -162f, 30f) };

    private const float SourceHudWidth = 800f;
    private const float SourceHudHeight = 600f;
    private const float AbilitySourceX = 730f;
    private const float AbilitySourceY = 515f;
    private const float DefaultAbilityWidgetTopY = 494f;
    private const float AbilityWidgetGap = 10f;
    private const float EngineerSentrySourceX = 696f;
    private const float EngineerSentryWidth = 86f;
    private const float EngineerSentryHeight = 64f;
    private const float EngineerSentrySourceY = DefaultAbilityWidgetTopY - AbilityWidgetGap - EngineerSentryHeight;
    private const float EngineerDispenserSourceY = EngineerSentrySourceY - AbilityWidgetGap - EngineerSentryHeight;
    private const float EngineerBuildMenuWidth = 201f;
    private const float EngineerBuildMenuHeight = 201f;
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
                Layer: 5,
                Scale: 1f),

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
                Vector2.Zero,
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
                HudAnchor.Center,
                Vector2.Zero,
                new Vector2(EngineerBuildMenuWidth, EngineerBuildMenuHeight),
                new Vector2(-100f, -100f),
                Layer: 60),
        };
    }

    /// <summary>
    /// GG2 mode starts these elements where GG2 Modern's Draw events put them (800x600
    /// view numbers, anchored to the same screen edge). They take precedence over the SGG
    /// defaults and runtime defaults while GG2 presentation is on; the player's own moves
    /// in the HUD editor still apply on top.
    /// </summary>
    public static IReadOnlyDictionary<string, HudElementLayout> CreateGg2()
    {
        return new Dictionary<string, HudElementLayout>(StringComparer.Ordinal)
        {
            // KillLog: rows right-aligned to the view edge, first row centred 24 px down.
            [HudElementId.MatchKillFeed] = new(
                HudElementId.MatchKillFeed,
                HudAnchor.TopRight,
                Vector2.Zero,
                new Vector2(340f, 104f),
                new Vector2(-340f, 14f),
                Layer: 5,
                Scale: 1f),

            // KothHUD / DKothHUD: small timers at y 36, 43 * 1.15 either side of centre.
            [HudElementId.MatchKothRedTimer] = new(
                HudElementId.MatchKothRedTimer,
                HudAnchor.TopCenter,
                new Vector2(-43f * 1.15f, 36f),
                new Vector2(92f, 36f),
                new Vector2(-46f, -18f),
                Layer: 8),

            [HudElementId.MatchKothBlueTimer] = new(
                HudElementId.MatchKothBlueTimer,
                HudAnchor.TopCenter,
                new Vector2(43f * 1.15f, 36f),
                new Vector2(92f, 36f),
                new Vector2(-46f, -18f),
                Layer: 9),

            // UberHud: icon at (w - 80, h - 85).
            [HudElementId.ClassMedicUber] = new(
                HudElementId.ClassMedicUber,
                HudAnchor.BottomRight,
                new Vector2(-80f, -85f),
                new Vector2(134f, 56f),
                new Vector2(-58f, -20f),
                Layer: 30),

            // NutsNBoltsHud: icon at (w - 70, h - 85).
            [HudElementId.ClassEngineerMetal] = new(
                HudElementId.ClassEngineerMetal,
                HudAnchor.BottomRight,
                new Vector2(-70f, -85f),
                new Vector2(88f, 48f),
                new Vector2(-54f, -18f),
                Layer: 50),

            // SentryHealthHud: the sentry panel above the health panel, at (5, h - 145).
            [HudElementId.ClassEngineerSentry] = new(
                HudElementId.ClassEngineerSentry,
                HudAnchor.BottomLeft,
                new Vector2(5f, -145f),
                new Vector2(EngineerSentryWidth, EngineerSentryHeight),
                Vector2.Zero,
                Layer: 51),
        };
    }
}
