using OpenGarrison.GameplayModding;

namespace OpenGarrison.Tools.BrowserAssetBuilder.Atlas;

internal static class AtlasGroupingPolicy
{
    public static string GetBootstrapGroup(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return normalized.Contains("/Menu/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/Title/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/Fonts/", StringComparison.OrdinalIgnoreCase)
            ? "menu-ui"
            : "hud-ui";
    }

    public static string GetStockGameplayGroup(GameplaySpriteAssetDefinition sprite)
    {
        var id = sprite.Id;
        if (id.Contains("Red", StringComparison.Ordinal))
        {
            return "characters-red";
        }

        if (id.Contains("Blue", StringComparison.Ordinal))
        {
            return "characters-blue";
        }

        if (id.Contains("Rocket", StringComparison.Ordinal)
            || id.Contains("Flare", StringComparison.Ordinal)
            || id.Contains("Needle", StringComparison.Ordinal)
            || id.Contains("Mine", StringComparison.Ordinal)
            || id.Contains("Projectile", StringComparison.Ordinal)
            || id.Contains("Explosion", StringComparison.Ordinal)
            || id.Contains("Flame", StringComparison.Ordinal)
            || id.Contains("Gib", StringComparison.Ordinal)
            || id.Contains("Blood", StringComparison.Ordinal))
        {
            return "projectiles-particles";
        }

        if (id.Contains("Sentry", StringComparison.Ordinal)
            || id.Contains("Generator", StringComparison.Ordinal)
            || id.Contains("Door", StringComparison.Ordinal)
            || id.Contains("ControlPoint", StringComparison.Ordinal)
            || id.Contains("Intel", StringComparison.Ordinal))
        {
            return "buildings-objects";
        }

        if (id.Contains("HUD", StringComparison.Ordinal)
            || id.Contains("Menu", StringComparison.Ordinal)
            || id.Contains("Font", StringComparison.Ordinal)
            || id.Contains("Scoreboard", StringComparison.Ordinal)
            || id.Contains("Timer", StringComparison.Ordinal)
            || id.Contains("ClassSelect", StringComparison.Ordinal)
            || id.Contains("TeamSelect", StringComparison.Ordinal))
        {
            return "hud-ui";
        }

        return "weapons-effects";
    }

    public static string GetGameMakerGroup(string spriteName)
    {
        if (spriteName.Contains("Red", StringComparison.Ordinal))
        {
            return "characters-red";
        }

        if (spriteName.Contains("Blue", StringComparison.Ordinal))
        {
            return "characters-blue";
        }

        if (spriteName.Contains("Rocket", StringComparison.Ordinal)
            || spriteName.Contains("Flare", StringComparison.Ordinal)
            || spriteName.Contains("Needle", StringComparison.Ordinal)
            || spriteName.Contains("Mine", StringComparison.Ordinal)
            || spriteName.Contains("Projectile", StringComparison.Ordinal)
            || spriteName.Contains("Explosion", StringComparison.Ordinal)
            || spriteName.Contains("Flame", StringComparison.Ordinal)
            || spriteName.Contains("Gib", StringComparison.Ordinal)
            || spriteName.Contains("Blood", StringComparison.Ordinal))
        {
            return "projectiles-particles";
        }

        if (spriteName.Contains("Sentry", StringComparison.Ordinal)
            || spriteName.Contains("Generator", StringComparison.Ordinal)
            || spriteName.Contains("Door", StringComparison.Ordinal)
            || spriteName.Contains("ControlPoint", StringComparison.Ordinal)
            || spriteName.Contains("Intel", StringComparison.Ordinal)
            || spriteName.Contains("HealthPack", StringComparison.Ordinal)
            || spriteName.Contains("Shell", StringComparison.Ordinal))
        {
            return "buildings-objects";
        }

        if (spriteName.Contains("Hud", StringComparison.OrdinalIgnoreCase)
            || spriteName.Contains("Menu", StringComparison.Ordinal)
            || spriteName.Contains("Font", StringComparison.Ordinal)
            || spriteName.Contains("Scoreboard", StringComparison.Ordinal)
            || spriteName.Contains("Timer", StringComparison.Ordinal)
            || spriteName.Contains("ClassSelect", StringComparison.Ordinal)
            || spriteName.Contains("TeamSelect", StringComparison.Ordinal)
            || spriteName.Contains("Crosshair", StringComparison.Ordinal)
            || spriteName.Contains("Radar", StringComparison.Ordinal))
        {
            return "hud-ui";
        }

        return "weapons-effects";
    }
}
