#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// A stable, presentation-only description of a gameplay buff. The ID is
/// intentionally independent of the tooltip text so additional buffs can be
/// added without changing the HUD element or saved layout profile.
/// </summary>
internal readonly record struct GameplayBuffPresentation(
    string Id,
    IReadOnlyList<string> StatLines);

/// <summary>
/// Collects the normal-gameplay buffs shown by the shared buff icon. Keep
/// gameplay predicates here so the renderer remains a layout/drawing concern.
/// </summary>
internal static class GameplayBuffPresentationCatalog
{
    internal const string KritzCritTargetId = "gameplay-buff.kritz-crit-target";
    internal const string DispenserId = "gameplay-buff.dispenser";

    internal static IReadOnlyList<GameplayBuffPresentation> Collect(PlayerEntity player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Collect(player.IsKritzCritBoosted, player.IsDispenserBuffed, player.DispenserAttackReloadSpeedMultiplier);
    }

    internal static IReadOnlyList<GameplayBuffPresentation> Collect(
        bool isKritzCritBoosted,
        bool isDispenserBuffed,
        float dispenserAttackReloadSpeedMultiplier)
    {
        var presentations = new List<GameplayBuffPresentation>(2);
        if (isKritzCritBoosted)
        {
            presentations.Add(new GameplayBuffPresentation(
                KritzCritTargetId,
                ["Critical Rate: +100%"]));
        }

        if (isDispenserBuffed)
        {
            var bonus = FormatMultiplierBonusPercentage(dispenserAttackReloadSpeedMultiplier);
            presentations.Add(new GameplayBuffPresentation(
                DispenserId,
                [
                    $"Rate of Fire: {bonus}",
                    $"Reload Speed: {bonus}",
                ]));
        }

        return presentations;
    }

    internal static bool HasAny(PlayerEntity player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.IsKritzCritBoosted || player.IsDispenserBuffed;
    }

    internal static string FormatMultiplierBonusPercentage(float multiplier)
    {
        if (!float.IsFinite(multiplier))
        {
            return "+0%";
        }

        var percentage = MathF.Max(0f, (multiplier - 1f) * 100f);
        return $"+{percentage.ToString("0.##", CultureInfo.InvariantCulture)}%";
    }
}
