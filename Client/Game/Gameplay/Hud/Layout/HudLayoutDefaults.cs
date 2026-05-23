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

    public static IReadOnlyDictionary<string, HudElementLayout> Create()
    {
        return new Dictionary<string, HudElementLayout>(StringComparer.Ordinal)
        {
            [HudElementId.LocalHealth] = new(
                HudElementId.LocalHealth,
                HudAnchor.BottomLeft,
                new Vector2(5f, -75f),
                new Vector2(176f, 78f),
                Vector2.Zero,
                Layer: 10),

            [HudElementId.LocalWeaponStack] = new(
                HudElementId.LocalWeaponStack,
                HudAnchor.BottomRight,
                new Vector2(MainAmmoSourceX - SourceHudWidth, MainAmmoSourceY - SourceHudHeight),
                new Vector2(220f, 116f),
                new Vector2(-150f, -72f),
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

            [HudElementId.ClassMedicUber] = new(
                HudElementId.ClassMedicUber,
                HudAnchor.BottomRight,
                new Vector2(-80f, -85f),
                new Vector2(134f, 56f),
                new Vector2(-58f, -20f),
                Layer: 30),

            [HudElementId.ClassEngineerMetal] = new(
                HudElementId.ClassEngineerMetal,
                HudAnchor.BottomRight,
                new Vector2(-70f, -85f),
                new Vector2(88f, 48f),
                new Vector2(-54f, -18f),
                Layer: 50),

            [HudElementId.ClassEngineerSentry] = new(
                HudElementId.ClassEngineerSentry,
                HudAnchor.BottomLeft,
                new Vector2(5f, -145f),
                new Vector2(96f, 72f),
                Vector2.Zero,
                Layer: 51),
        };
    }
}
