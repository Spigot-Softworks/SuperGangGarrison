#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using System.Globalization;

namespace OpenGarrison.Client;

internal static class GameplayLoadoutMenuTextBuilder
{
    private static readonly Color TitleColor = new(245, 240, 232);
    private static readonly Color PositiveColor = new(109, 214, 106);
    private static readonly Color NegativeColor = new(214, 93, 86);
    private static readonly Color NeutralColor = new(186, 186, 186);

    public static GameplayLoadoutMenuBoardLine[] BuildBoardLines(
        PlayerClass viewedClass,
        GameplayItemDefinition item,
        int ticksPerSecond)
    {
        var lines = new List<GameplayLoadoutMenuBoardLine>();
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;

        if (item.Slot != GameplayEquipmentSlot.Primary || !runtimeRegistry.TryGetPrimaryWeaponBinding(item.BehaviorId, out _))
        {
            lines.Add(new GameplayLoadoutMenuBoardLine(item.DisplayName, TitleColor));
            foreach (var line in BuildAbilityLines(item))
            {
                lines.Add(new GameplayLoadoutMenuBoardLine(line, NeutralColor));
            }

            return lines.ToArray();
        }

        var resolvedItem = runtimeRegistry.CreatePrimaryWeaponDefinition(item);
        lines.Add(new GameplayLoadoutMenuBoardLine(item.DisplayName, TitleColor));

        var stockItem = GetStockComparisonItem(viewedClass, item.Slot, runtimeRegistry);
        if (stockItem is null || string.Equals(stockItem.Id, item.Id, StringComparison.Ordinal))
        {
            foreach (var line in BuildStockStatLines(resolvedItem, ticksPerSecond))
            {
                lines.Add(new GameplayLoadoutMenuBoardLine(line, TitleColor));
            }

            return lines.ToArray();
        }

        var resolvedStockItem = runtimeRegistry.CreatePrimaryWeaponDefinition(stockItem);
        foreach (var line in BuildDeltaLines(resolvedItem, resolvedStockItem, positive: true))
        {
            lines.Add(new GameplayLoadoutMenuBoardLine(line, PositiveColor));
        }

        foreach (var line in BuildDeltaLines(resolvedItem, resolvedStockItem, positive: false))
        {
            lines.Add(new GameplayLoadoutMenuBoardLine(line, NegativeColor));
        }

        foreach (var line in BuildNeutralLines(item, resolvedItem, resolvedStockItem))
        {
            lines.Add(new GameplayLoadoutMenuBoardLine(line, NeutralColor));
        }

        return lines.ToArray();
    }

    private static GameplayItemDefinition? GetStockComparisonItem(
        PlayerClass viewedClass,
        GameplayEquipmentSlot slot,
        GameplayRuntimeRegistry runtimeRegistry)
    {
        var stockLoadout = GameplayLoadoutSelectionResolver
            .GetOrderedLoadouts(viewedClass)
            .FirstOrDefault(loadout => loadout.Id.EndsWith(".stock", StringComparison.OrdinalIgnoreCase)
                || string.Equals(loadout.DisplayName, "Stock", StringComparison.OrdinalIgnoreCase));

        if (stockLoadout is null)
        {
            return null;
        }

        var stockItemId = GameplayLoadoutMenuModel.ResolveLoadoutItemId(stockLoadout, slot);
        if (string.IsNullOrWhiteSpace(stockItemId))
        {
            return null;
        }

        return runtimeRegistry.GetRequiredItem(stockItemId);
    }

    private static IEnumerable<string> BuildAbilityLines(GameplayItemDefinition item)
    {
        if (item.Description?.Notes is { Count: > 0 } notes)
        {
            foreach (var note in notes)
            {
                yield return note;
            }

            yield break;
        }

        if (!string.IsNullOrWhiteSpace(item.Description?.Summary))
        {
            yield return item.Description.Summary!;
            yield break;
        }

        if (item.Slot == GameplayEquipmentSlot.Secondary)
        {
            yield return "Standard secondary ability";
            yield break;
        }

        if (item.Slot == GameplayEquipmentSlot.Utility)
        {
            yield return "Standard utility ability";
        }
    }

    private static IEnumerable<string> BuildStockStatLines(PrimaryWeaponDefinition item, int ticksPerSecond)
    {
        foreach (var line in BuildStockDamageLines(item))
        {
            yield return line;
        }

        if (item.ProjectilesPerShot > 0)
        {
            yield return BuildValueLine("Projectiles", item.ProjectilesPerShot);
        }

        if (item.MaxAmmo > 0)
        {
            yield return BuildValueLine("Clip", item.MaxAmmo);
        }

        if (item.ReloadDelayTicks > 0)
        {
            var seconds = item.ReloadDelayTicks / (float)ticksPerSecond;
            yield return BuildValueLine("Refire", seconds, " sec");
        }

        if (item.AmmoReloadTicks > 0)
        {
            var seconds = item.AmmoReloadTicks / (float)ticksPerSecond;
            yield return BuildValueLine("Reload", seconds, " sec");
        }

        if (item.SpreadDegrees > 0f)
        {
            yield return BuildValueLine("Spread", item.SpreadDegrees, " deg");
        }

        var projectileSpeed = item.MinShotSpeed + item.AdditionalRandomShotSpeed;
        if (projectileSpeed > 0f)
        {
            yield return BuildValueLine("Proj speed", projectileSpeed, string.Empty);
        }

        if (item.DirectHitHealAmount is float directHitHealAmount && directHitHealAmount > 0f)
        {
            yield return BuildValueLine("Direct-hit heal", directHitHealAmount, string.Empty);
        }
    }

    private static IEnumerable<string> BuildStockDamageLines(PrimaryWeaponDefinition item)
    {
        if (item.RocketCombat is { } rocket)
        {
            yield return BuildValueLine("Direct damage", rocket.DirectHitDamage);
            yield return BuildValueLine("Splash damage", rocket.ExplosionDamage, string.Empty);
            yield return BuildValueLine("Blast radius", rocket.BlastRadius, string.Empty);
            yield break;
        }

        if (item.DirectHitDamage is { } directDamage)
        {
            yield return BuildValueLine("Damage", directDamage, string.Empty);
        }

        if (item.DamagePerTick is { } damagePerTick)
        {
            yield return BuildValueLine("Damage / tick", damagePerTick, string.Empty);
        }
    }

    private static IEnumerable<string> BuildDeltaLines(PrimaryWeaponDefinition item, PrimaryWeaponDefinition stockItem, bool positive)
    {
        foreach (var line in BuildDamageDeltaLines(item, stockItem, positive))
        {
            yield return line;
        }

        if (TryBuildDeltaLine("Projectiles", item.ProjectilesPerShot, stockItem.ProjectilesPerShot, false, positive, out var projectiles))
        {
            yield return projectiles;
        }

        if (TryBuildDeltaLine("Clip", item.MaxAmmo, stockItem.MaxAmmo, false, positive, out var clip))
        {
            yield return clip;
        }

        if (TryBuildDeltaLine("Refire", item.ReloadDelayTicks, stockItem.ReloadDelayTicks, true, positive, out var refire))
        {
            yield return refire;
        }

        if (TryBuildDeltaLine("Reload", item.AmmoReloadTicks, stockItem.AmmoReloadTicks, true, positive, out var reload))
        {
            yield return reload;
        }

        if (TryBuildDeltaLine("Spread", item.SpreadDegrees, stockItem.SpreadDegrees, true, positive, out var spread))
        {
            yield return spread;
        }

        var projectileSpeed = item.MinShotSpeed + item.AdditionalRandomShotSpeed;
        var stockProjectileSpeed = stockItem.MinShotSpeed + stockItem.AdditionalRandomShotSpeed;
        if (TryBuildDeltaLine("Projectile speed", projectileSpeed, stockProjectileSpeed, false, positive, out var projectileSpeedLine))
        {
            yield return projectileSpeedLine;
        }

        if (TryBuildEffectDeltaLine(
            "health on direct hit",
            item.DirectHitHealAmount,
            stockItem.DirectHitHealAmount,
            positive,
            out var directHitHealLine))
        {
            yield return directHitHealLine;
        }
    }

    private static IEnumerable<string> BuildDamageDeltaLines(PrimaryWeaponDefinition item, PrimaryWeaponDefinition stockItem, bool positive)
    {
        if (item.RocketCombat is { } rocket && stockItem.RocketCombat is { } stockRocket)
        {
            if (TryBuildDeltaLine("Direct damage", rocket.DirectHitDamage, stockRocket.DirectHitDamage, false, positive, out var direct))
            {
                yield return direct;
            }

            if (TryBuildDeltaLine("Splash damage", rocket.ExplosionDamage, stockRocket.ExplosionDamage, false, positive, out var splash))
            {
                yield return splash;
            }

            if (TryBuildDeltaLine("Blast radius", rocket.BlastRadius, stockRocket.BlastRadius, false, positive, out var radius))
            {
                yield return radius;
            }

            yield break;
        }

        if (item.DirectHitDamage is { } directDamage && stockItem.DirectHitDamage is { } stockDirectDamage)
        {
            if (TryBuildDeltaLine("Damage", directDamage, stockDirectDamage, false, positive, out var damage))
            {
                yield return damage;
            }
        }

        if (item.DamagePerTick is { } damagePerTick && stockItem.DamagePerTick is { } stockDamagePerTick)
        {
            if (TryBuildDeltaLine("Damage / tick", damagePerTick, stockDamagePerTick, false, positive, out var damageTick))
            {
                yield return damageTick;
            }
        }
    }

    private static IEnumerable<string> BuildNeutralLines(GameplayItemDefinition item, PrimaryWeaponDefinition itemStats, PrimaryWeaponDefinition stockItemStats)
    {
        if (itemStats.RefillsAllAtOnce != stockItemStats.RefillsAllAtOnce)
        {
            yield return itemStats.RefillsAllAtOnce ? "Reloads full clip at once" : "Shell-by-shell reload";
        }

        if (itemStats.AutoReloads != stockItemStats.AutoReloads)
        {
            yield return itemStats.AutoReloads ? "Auto reload enabled" : "Manual reload";
        }

        if (item.Description?.Notes is { Count: > 0 } notes)
        {
            foreach (var note in notes)
            {
                yield return note;
            }
        }
    }

    private static bool TryBuildDeltaLine(string label, int value, int stockValue, bool inverseIsBetter, bool positive, out string line)
    {
        return TryBuildDeltaLine(label, (float)value, stockValue, inverseIsBetter, positive, out line);
    }

    private static bool TryBuildDeltaLine(string label, float value, float stockValue, bool inverseIsBetter, bool positive, out string line)
    {
        line = string.Empty;
        if (stockValue <= 0f || NearlyEqual(value, stockValue))
        {
            return false;
        }

        var deltaPercent = ((value - stockValue) / stockValue) * 100f;
        var isPositive = inverseIsBetter ? deltaPercent < 0f : deltaPercent > 0f;
        if (isPositive != positive)
        {
            return false;
        }

        var sign = deltaPercent >= 0f ? "+" : "-";
        var deltaText = MathF.Abs(deltaPercent).ToString("0.#", CultureInfo.InvariantCulture);
        line = $"{sign}{deltaText}% {label}";
        return true;
    }

    private static bool TryBuildEffectDeltaLine(string label, float? value, float? stockValue, bool positive, out string line)
    {
        line = string.Empty;
        var resolvedValue = value ?? 0f;
        var resolvedStockValue = stockValue ?? 0f;
        if (NearlyEqual(resolvedValue, resolvedStockValue))
        {
            return false;
        }

        var isPositive = resolvedValue > resolvedStockValue;
        if (isPositive != positive)
        {
            return false;
        }

        var sign = isPositive ? "+" : "-";
        var magnitude = MathF.Abs(resolvedValue - resolvedStockValue);
        var magnitudeText = magnitude == MathF.Round(magnitude)
            ? ((int)MathF.Round(magnitude)).ToString(CultureInfo.InvariantCulture)
            : magnitude.ToString("0.#", CultureInfo.InvariantCulture);
        line = $"{sign}{magnitudeText} {label}";
        return true;
    }

    private static string BuildValueLine(string label, int value)
    {
        return BuildValueLine(label, (float)value, string.Empty);
    }

    private static string BuildValueLine(string label, float value, string suffix)
    {
        var valueText = value == MathF.Round(value)
            ? ((int)MathF.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
        return $"{label}: {valueText}{suffix}";
    }

    private static bool NearlyEqual(float left, float right)
    {
        return MathF.Abs(left - right) < 0.005f;
    }
}

internal readonly record struct GameplayLoadoutMenuBoardLine(
    string Text,
    Color Color);
