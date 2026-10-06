using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public enum MapLightingPreset
{
    None,
    Custom,
    Dusk,
    Sunset,
    Night,
    Moonlit,
    Overcast,
    Underground,
    Toxic,
    BloodMoon,
}

/// <summary>An RGB colour stored in map metadata as six hex digits.</summary>
public readonly record struct MapLightingColor(byte R, byte G, byte B)
{
    public static MapLightingColor White => new(255, 255, 255);

    public string ToHex() => $"{R:x2}{G:x2}{B:x2}";

    public static bool TryParse(string? value, out MapLightingColor color)
    {
        color = White;
        var text = value?.Trim().TrimStart('#') ?? string.Empty;
        if (text.Length != 6
            || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return false;
        }

        color = new MapLightingColor((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);
        return true;
    }
}

/// <summary>
/// Map-wide lighting mood. Presentation only: it darkens and tints the world (players
/// included, HUD excluded) and lets light sources cut through. Amounts are 0-100, except
/// brightness and the two ranges, which run 0-200 with 100 as the normal value.
/// </summary>
/// <param name="SkyTint">Ambient colour at the top of the map.</param>
/// <param name="GroundTint">Ambient colour at the bottom of the map.</param>
/// <param name="Brightness">
/// Ambient level. The tints set the colour (normalised so their brightest channel counts
/// as full), so 100 shows the tint at full strength and white at 100 changes nothing;
/// above 100 brightens the scene, up to 2x at 200.
/// </param>
/// <param name="Glow">Halo bloom drawn over light sources.</param>
/// <param name="EffectLights">Strength of gameplay lights (fire, explosions, rockets, flares).</param>
/// <param name="PlayerLight">Strength of the soft light around every player so nobody is hidden in the dark.</param>
/// <param name="Vignette">Darkening toward the screen edges.</param>
/// <param name="Pulse">Slow breathing of the ambient light (eerie or toxic moods).</param>
/// <param name="RimBlend">How the rim colour combines with the sprite pixel under it (Normal at 100% opacity paints it flat).</param>
/// <param name="RimOpacity">Strength of the rim blend, 0-100.</param>
/// <param name="SkyReach">How far down the map the sky light holds before fading into the ground tint, 0-100% of the map height (0 = a fade over the whole height).</param>
/// <param name="Banded">Retro stepped light falloff instead of smooth gradients.</param>
/// <param name="PlayerRange">Radius of the player light, as a percentage of its normal size.</param>
/// <param name="EffectRange">Radius of gameplay effect lights, as a percentage of their normal size.</param>
/// <param name="RimLight">Rim light on characters' light-facing edges (0 = off).</param>
/// <param name="RimWidth">Rim thickness in art pixels (1-3), or 0 for half a pixel (saved as "0.5"; sprites without finer texels draw it as 1).</param>
/// <param name="RimWrap">How far the rim creeps around the silhouette away from the light.</param>
/// <param name="RimColorMode">The rim's one flat colour: the light's colour, a brighter glow of it, the player's team colour, or a custom colour.</param>
/// <param name="RimSource">Where the rim and shadow light comes from: the nearest light, the sky (from above), or both blended.</param>
/// <param name="CastShadow">Opacity of the character's dark silhouette cast away from the light (0 = off).</param>
/// <param name="ShadowDistance">How far, in world pixels, the cast shadow falls from the character (1-8).</param>
/// <param name="BodyShade">How far the character's body (everything but the rim) is darkened toward black (0 = off).</param>
/// <param name="BodySaturation">Colour saturation of the character's body, 0-200 (100 = unchanged, 0 = grey).</param>
/// <param name="RimCustomColor">The rim colour when <paramref name="RimColorMode"/> is Custom; null uses a warm white.</param>
/// <param name="SkyRim">With lights + sky: how strong the sky's dim rim is away from lights, 0-100.</param>
/// <param name="UberRim">Übercharged players turn into a dark silhouette lit hard from below in their team colour, flashing as the charge runs out.</param>
/// <summary>How the character rim light is coloured.</summary>
public enum MapRimColorMode
{
    // Every mode paints the whole rim in one flat colour, so edge pixels never mix with the
    // sprite's own colours. The numeric values are kept stable for saved maps.

    /// <summary>The player's team colour (red or blue). Saved as "team"; old maps' "sprite" reads as this.</summary>
    Team = 0,

    /// <summary>The light's colour.</summary>
    Light = 1,

    /// <summary>A brighter, whiter version of the light's colour, so the edge glows. Saved as "add".</summary>
    Additive = 2,

    /// <summary>A colour the map author picks (<see cref="MapLighting.RimCustomColor"/>).</summary>
    Custom = 3,
}

/// <summary>How the rim colour combines with the sprite pixel under it, like layer modes in a paint program.</summary>
public enum MapRimBlendMode
{
    /// <summary>The rim colour over the pixel; at 100% opacity a flat colour.</summary>
    Normal,

    /// <summary>Adds the rim colour: a glowing edge.</summary>
    Add,

    /// <summary>A softer brighten that never blows out to white.</summary>
    Screen,

    /// <summary>Brightens hard and saturates, keeping the art's darks: a hot, burning edge.</summary>
    ColorDodge,

    /// <summary>Boosts contrast and saturation toward the rim colour.</summary>
    Overlay,
}

/// <summary>Where the character rim light and cast shadow take their light from.</summary>
public enum MapRimLightSource
{
    /// <summary>The strongest nearby light; no rim away from lights.</summary>
    Lights,

    /// <summary>Always the sky, from above.</summary>
    Sky,

    /// <summary>A dim rim from the sky everywhere, turning toward and brightening with nearby lights.</summary>
    Both,
}

public sealed record MapLighting(
    MapLightingPreset Preset,
    MapLightingColor SkyTint,
    MapLightingColor GroundTint,
    int Brightness,
    int Glow,
    int EffectLights,
    int PlayerLight,
    int Vignette,
    int Pulse,
    bool Banded,
    int PlayerRange = 100,
    int EffectRange = 100,
    int RimLight = 0,
    int RimWidth = 1,
    int RimWrap = 25,
    MapRimColorMode RimColorMode = MapRimColorMode.Light,
    MapRimLightSource RimSource = MapRimLightSource.Lights,
    int CastShadow = 0,
    int ShadowDistance = 3,
    int BodyShade = 0,
    int BodySaturation = 100,
    MapLightingColor? RimCustomColor = null,
    int SkyRim = 35,
    bool UberRim = false,
    int SkyReach = 0,
    MapRimBlendMode RimBlend = MapRimBlendMode.Normal,
    int RimOpacity = 100)
{
    /// <summary>True when the rim always comes from the sky.</summary>
    public bool RimFromSky => RimSource == MapRimLightSource.Sky;

    /// <summary>The custom rim colour, or the default warm white when none was set.</summary>
    public MapLightingColor ResolvedRimCustomColor => RimCustomColor ?? MapLightingMetadata.DefaultRimCustomColor;

    public bool HasCharacterLighting => IsActive && (RimLight > 0 || CastShadow > 0 || HasBodyAdjustment || UberRim);

    /// <summary>True when the character's body is darkened or its saturation changed.</summary>
    public bool HasBodyAdjustment => BodyShade > 0 || BodySaturation != 100;

    public static MapLighting None { get; } = new(
        MapLightingPreset.None,
        MapLightingColor.White,
        MapLightingColor.White,
        100,
        0,
        0,
        0,
        0,
        0,
        false);

    public bool IsActive => Preset != MapLightingPreset.None;
}

/// <summary>Reads and writes the lighting keys in map metadata.</summary>
public static class MapLightingMetadata
{
    public const string PresetKey = "lighting";
    public const string SkyTintKey = "lightSkyTint";
    public const string GroundTintKey = "lightGroundTint";
    public const string BrightnessKey = "lightBrightness";
    public const string GlowKey = "lightGlow";
    public const string EffectLightsKey = "lightEffects";
    public const string PlayerLightKey = "lightPlayers";
    public const string VignetteKey = "lightVignette";
    public const string PulseKey = "lightPulse";
    public const string StyleKey = "lightStyle";
    public const string RimKey = "lightRim";
    public const string RimWidthKey = "lightRimWidth";
    public const string RimWrapKey = "lightRimWrap";
    public const string RimColorKey = "lightRimColor";
    public const string RimSourceKey = "lightRimSource";
    public const string ShadowKey = "lightShadow";
    public const string ShadowDistanceKey = "lightShadowDistance";
    public const string BodyShadeKey = "lightBodyShade";
    public const string BodySaturationKey = "lightBodySaturation";
    public const string RimCustomColorKey = "lightRimCustomColor";
    public const string RimCustomColorValue = "custom";

    /// <summary>Starting colour for a custom rim: a warm white.</summary>
    public static MapLightingColor DefaultRimCustomColor => new(255, 236, 200);

    public const string RimTeamColorValue = "team";
    public const string RimSpriteColorValue = "sprite";
    public const string RimLightColorValue = "light";
    public const string RimAdditiveColorValue = "add";
    public const string RimLightsSourceValue = "lights";
    public const string RimSkySourceValue = "sky";
    public const string RimBothSourceValue = "both";
    public const string SkyRimKey = "lightRimSky";
    public const string SkyReachKey = "lightSkyReach";
    public const string RimBlendKey = "lightRimBlend";
    public const string RimOpacityKey = "lightRimOpacity";

    /// <summary>Sky reach stops short of the bottom so there is always some fade to the ground tint.</summary>
    public const int MaxSkyReach = 95;
    public const string UberRimKey = "lightUberRim";
    public const string OnValue = "on";
    public const string OffValue = "off";

    /// <summary>The half-pixel rim width (shown and saved as 0.5).</summary>
    public const int HalfRimWidth = 0;
    public const string HalfRimWidthValue = "0.5";
    public const int MinRimWidth = HalfRimWidth;
    public const int MaxRimWidth = 3;
    public const int MinShadowDistance = 1;
    public const int MaxShadowDistance = 8;
    public const string PlayerRangeKey = "lightPlayerRange";
    public const string EffectRangeKey = "lightEffectRange";

    /// <summary>Upper limit for brightness and the two range settings (100 is normal).</summary>
    public const int MaxScale = 200;

    public const string SmoothStyleValue = "smooth";
    public const string BandedStyleValue = "retro";

    private static readonly string[] AllKeys =
    [
        PresetKey,
        SkyTintKey,
        GroundTintKey,
        BrightnessKey,
        GlowKey,
        EffectLightsKey,
        PlayerLightKey,
        VignetteKey,
        PulseKey,
        SkyReachKey,
        RimBlendKey,
        RimOpacityKey,
        StyleKey,
        RimKey,
        RimWidthKey,
        RimWrapKey,
        RimColorKey,
        RimSourceKey,
        SkyRimKey,
        UberRimKey,
        ShadowKey,
        ShadowDistanceKey,
        BodyShadeKey,
        BodySaturationKey,
        RimCustomColorKey,
        PlayerRangeKey,
        EffectRangeKey,
    ];

    public static IReadOnlyList<string> Keys => AllKeys;

    public static IReadOnlyList<MapLightingPreset> Presets { get; } =
    [
        MapLightingPreset.None,
        MapLightingPreset.Dusk,
        MapLightingPreset.Sunset,
        MapLightingPreset.Night,
        MapLightingPreset.Moonlit,
        MapLightingPreset.Overcast,
        MapLightingPreset.Underground,
        MapLightingPreset.Toxic,
        MapLightingPreset.BloodMoon,
        MapLightingPreset.Custom,
    ];

    public static bool IsLightingKey(string key)
    {
        foreach (var candidate in AllKeys)
        {
            if (candidate.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The starting values for a preset; Custom starts neutral.</summary>
    public static MapLighting GetPresetDefaults(MapLightingPreset preset) => preset switch
    {
        MapLightingPreset.Dusk => new(preset, new(255, 170, 150), new(150, 140, 200), 78, 40, 70, 15, 25, 0, false),
        MapLightingPreset.Sunset => new(preset, new(255, 190, 120), new(230, 150, 120), 90, 35, 55, 5, 20, 0, false),
        MapLightingPreset.Night => new(preset, new(110, 130, 200), new(70, 85, 150), 48, 55, 100, 30, 40, 0, false),
        MapLightingPreset.Moonlit => new(preset, new(170, 190, 255), new(110, 130, 190), 62, 45, 85, 20, 30, 0, false),
        MapLightingPreset.Overcast => new(preset, new(200, 205, 215), new(165, 170, 180), 82, 20, 45, 0, 15, 0, false),
        MapLightingPreset.Underground => new(preset, new(90, 80, 70), new(70, 60, 55), 32, 65, 100, 40, 50, 10, true),
        MapLightingPreset.Toxic => new(preset, new(150, 230, 120), new(90, 170, 90), 62, 60, 80, 20, 35, 35, false),
        MapLightingPreset.BloodMoon => new(preset, new(240, 90, 80), new(140, 50, 60), 55, 55, 90, 25, 40, 15, false),
        MapLightingPreset.Custom => new(preset, MapLightingColor.White, MapLightingColor.White, 100, 40, 70, 15, 20, 0, false),
        _ => MapLighting.None,
    };

    public static MapLightingPreset ParsePreset(string? value)
    {
        var text = value?.Trim().Replace(" ", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        foreach (var preset in Presets)
        {
            if (preset.ToString().Equals(text, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }

        return MapLightingPreset.None;
    }

    public static string ToPresetValue(MapLightingPreset preset) =>
        preset == MapLightingPreset.None ? "none" : char.ToLowerInvariant(preset.ToString()[0]) + preset.ToString()[1..];

    public static string GetPresetDisplayLabel(MapLightingPreset preset) => preset switch
    {
        MapLightingPreset.None => "Off",
        MapLightingPreset.BloodMoon => "Blood moon",
        _ => preset.ToString(),
    };

    /// <summary>
    /// Preset defaults overridden by any explicit keys, so a map can pick "night" and
    /// then tweak only the values that matter to it.
    /// </summary>
    public static MapLighting Parse(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue(PresetKey, out var presetValue))
        {
            return MapLighting.None;
        }

        var preset = ParsePreset(presetValue);
        if (preset == MapLightingPreset.None)
        {
            return MapLighting.None;
        }

        var lighting = GetPresetDefaults(preset);
        if (metadata.TryGetValue(SkyTintKey, out var sky) && MapLightingColor.TryParse(sky, out var skyColor))
        {
            lighting = lighting with { SkyTint = skyColor };
        }

        if (metadata.TryGetValue(GroundTintKey, out var ground) && MapLightingColor.TryParse(ground, out var groundColor))
        {
            lighting = lighting with { GroundTint = groundColor };
        }

        lighting = lighting with
        {
            Brightness = ReadValue(metadata, BrightnessKey, lighting.Brightness, MaxScale),
            Glow = ReadPercent(metadata, GlowKey, lighting.Glow),
            EffectLights = ReadPercent(metadata, EffectLightsKey, lighting.EffectLights),
            PlayerLight = ReadPercent(metadata, PlayerLightKey, lighting.PlayerLight),
            Vignette = ReadPercent(metadata, VignetteKey, lighting.Vignette),
            Pulse = ReadPercent(metadata, PulseKey, lighting.Pulse),
            SkyReach = ReadValue(metadata, SkyReachKey, lighting.SkyReach, MaxSkyReach),
            RimOpacity = ReadPercent(metadata, RimOpacityKey, lighting.RimOpacity),
            PlayerRange = ReadValue(metadata, PlayerRangeKey, lighting.PlayerRange, MaxScale),
            EffectRange = ReadValue(metadata, EffectRangeKey, lighting.EffectRange, MaxScale),
            RimLight = ReadPercent(metadata, RimKey, lighting.RimLight),
            RimWidth = ReadRimWidth(metadata, lighting.RimWidth),
            RimWrap = ReadPercent(metadata, RimWrapKey, lighting.RimWrap),
            CastShadow = ReadPercent(metadata, ShadowKey, lighting.CastShadow),
            ShadowDistance = Math.Max(MinShadowDistance, ReadValue(metadata, ShadowDistanceKey, lighting.ShadowDistance, MaxShadowDistance)),
            BodyShade = ReadPercent(metadata, BodyShadeKey, lighting.BodyShade),
            BodySaturation = ReadValue(metadata, BodySaturationKey, lighting.BodySaturation, MaxScale),
        };

        if (metadata.TryGetValue(RimColorKey, out var rimColor))
        {
            lighting = lighting with { RimColorMode = ParseRimColorMode(rimColor) };
        }

        if (metadata.TryGetValue(RimCustomColorKey, out var rimCustom) && MapLightingColor.TryParse(rimCustom, out var rimCustomColor))
        {
            lighting = lighting with { RimCustomColor = rimCustomColor };
        }

        if (metadata.TryGetValue(RimSourceKey, out var rimSource))
        {
            lighting = lighting with { RimSource = ParseRimSource(rimSource) };
        }

        lighting = lighting with { SkyRim = ReadPercent(metadata, SkyRimKey, lighting.SkyRim) };
        if (metadata.TryGetValue(UberRimKey, out var uberRim))
        {
            lighting = lighting with { UberRim = uberRim.Trim().Equals(OnValue, StringComparison.OrdinalIgnoreCase) };
        }

        if (metadata.TryGetValue(RimBlendKey, out var rimBlend))
        {
            lighting = lighting with { RimBlend = ParseRimBlend(rimBlend) };
        }

        if (metadata.TryGetValue(StyleKey, out var style))
        {
            lighting = lighting with { Banded = style.Trim().Equals(BandedStyleValue, StringComparison.OrdinalIgnoreCase) };
        }

        return lighting;
    }

    /// <summary>
    /// Writes every lighting key (or removes them all when lighting is off) so the saved
    /// map always reproduces exactly what the author saw.
    /// </summary>
    public static void Write(IDictionary<string, string> metadata, MapLighting lighting)
    {
        foreach (var key in AllKeys)
        {
            metadata.Remove(key);
        }

        if (!lighting.IsActive)
        {
            return;
        }

        metadata[PresetKey] = ToPresetValue(lighting.Preset);
        metadata[SkyTintKey] = lighting.SkyTint.ToHex();
        metadata[GroundTintKey] = lighting.GroundTint.ToHex();
        metadata[BrightnessKey] = ClampScale(lighting.Brightness).ToString(CultureInfo.InvariantCulture);
        metadata[GlowKey] = Clamp(lighting.Glow).ToString(CultureInfo.InvariantCulture);
        metadata[EffectLightsKey] = Clamp(lighting.EffectLights).ToString(CultureInfo.InvariantCulture);
        metadata[PlayerLightKey] = Clamp(lighting.PlayerLight).ToString(CultureInfo.InvariantCulture);
        metadata[VignetteKey] = Clamp(lighting.Vignette).ToString(CultureInfo.InvariantCulture);
        metadata[PulseKey] = Clamp(lighting.Pulse).ToString(CultureInfo.InvariantCulture);
        metadata[SkyReachKey] = Math.Clamp(lighting.SkyReach, 0, MaxSkyReach).ToString(CultureInfo.InvariantCulture);
        metadata[RimBlendKey] = ToRimBlendValue(lighting.RimBlend);
        metadata[RimOpacityKey] = Clamp(lighting.RimOpacity).ToString(CultureInfo.InvariantCulture);
        metadata[StyleKey] = lighting.Banded ? BandedStyleValue : SmoothStyleValue;
        metadata[PlayerRangeKey] = ClampScale(lighting.PlayerRange).ToString(CultureInfo.InvariantCulture);
        metadata[EffectRangeKey] = ClampScale(lighting.EffectRange).ToString(CultureInfo.InvariantCulture);
        metadata[RimKey] = Clamp(lighting.RimLight).ToString(CultureInfo.InvariantCulture);
        metadata[RimWidthKey] = FormatRimWidth(lighting.RimWidth);
        metadata[RimWrapKey] = Clamp(lighting.RimWrap).ToString(CultureInfo.InvariantCulture);
        metadata[RimColorKey] = ToRimColorValue(lighting.RimColorMode);
        metadata[RimSourceKey] = ToRimSourceValue(lighting.RimSource);
        metadata[SkyRimKey] = Clamp(lighting.SkyRim).ToString(CultureInfo.InvariantCulture);
        metadata[UberRimKey] = lighting.UberRim ? OnValue : OffValue;
        metadata[ShadowKey] = Clamp(lighting.CastShadow).ToString(CultureInfo.InvariantCulture);
        metadata[ShadowDistanceKey] = Math.Clamp(lighting.ShadowDistance, MinShadowDistance, MaxShadowDistance).ToString(CultureInfo.InvariantCulture);
        metadata[BodyShadeKey] = Clamp(lighting.BodyShade).ToString(CultureInfo.InvariantCulture);
        metadata[BodySaturationKey] = ClampScale(lighting.BodySaturation).ToString(CultureInfo.InvariantCulture);
        if (lighting.RimCustomColor is { } rimCustomColor)
        {
            metadata[RimCustomColorKey] = rimCustomColor.ToHex();
        }
    }

    public static int Clamp(int percent) => Math.Clamp(percent, 0, 100);

    /// <summary>Clamp for brightness and the ranges, where 100 is normal and 200 is double.</summary>
    public static int ClampScale(int percent) => Math.Clamp(percent, 0, MaxScale);

    /// <summary>Rim width as shown and saved: "0.5" for the half width, else the whole number.</summary>
    public static string FormatRimWidth(int width)
    {
        var clamped = Math.Clamp(width, MinRimWidth, MaxRimWidth);
        return clamped == HalfRimWidth ? HalfRimWidthValue : clamped.ToString(CultureInfo.InvariantCulture);
    }

    private static int ReadRimWidth(IReadOnlyDictionary<string, string> metadata, int fallback)
    {
        if (!metadata.TryGetValue(RimWidthKey, out var raw))
        {
            return fallback;
        }

        if (!float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value))
        {
            return fallback;
        }

        // Anything below 1 is the half width.
        return value < 1f ? HalfRimWidth : Math.Clamp((int)MathF.Round(value), 1, MaxRimWidth);
    }

    public static string GetRimBlendDisplayLabel(MapRimBlendMode mode) =>
        mode == MapRimBlendMode.ColorDodge ? "Color dodge" : mode.ToString();

    public static MapRimBlendMode ParseRimBlend(string? value)
    {
        var text = value?.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        foreach (var mode in Enum.GetValues<MapRimBlendMode>())
        {
            if (mode.ToString().Equals(text, StringComparison.OrdinalIgnoreCase))
            {
                return mode;
            }
        }

        return MapRimBlendMode.Normal;
    }

    public static string ToRimBlendValue(MapRimBlendMode mode) => mode.ToString().ToLowerInvariant();

    /// <summary>Normal -> Add -> Screen -> Color dodge -> Overlay -> Normal.</summary>
    public static MapRimBlendMode NextRimBlend(MapRimBlendMode mode) =>
        (MapRimBlendMode)(((int)mode + 1) % Enum.GetValues<MapRimBlendMode>().Length);

    public static MapRimLightSource ParseRimSource(string? value)
    {
        var text = value?.Trim();
        if (string.Equals(text, RimSkySourceValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapRimLightSource.Sky;
        }

        return string.Equals(text, RimBothSourceValue, StringComparison.OrdinalIgnoreCase)
            ? MapRimLightSource.Both
            : MapRimLightSource.Lights;
    }

    public static string ToRimSourceValue(MapRimLightSource source) => source switch
    {
        MapRimLightSource.Sky => RimSkySourceValue,
        MapRimLightSource.Both => RimBothSourceValue,
        _ => RimLightsSourceValue,
    };

    /// <summary>Nearest light -> Lights + sky -> Sky -> Nearest light.</summary>
    public static MapRimLightSource NextRimSource(MapRimLightSource source) => source switch
    {
        MapRimLightSource.Lights => MapRimLightSource.Both,
        MapRimLightSource.Both => MapRimLightSource.Sky,
        _ => MapRimLightSource.Lights,
    };

    public static string GetRimSourceDisplayLabel(MapRimLightSource source) => source switch
    {
        MapRimLightSource.Sky => "Sky",
        MapRimLightSource.Both => "Lights + sky",
        _ => "Nearest light",
    };

    public static MapRimColorMode ParseRimColorMode(string? value)
    {
        var text = value?.Trim();
        if (string.Equals(text, RimTeamColorValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, RimSpriteColorValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapRimColorMode.Team;
        }

        if (string.Equals(text, RimAdditiveColorValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "additive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "glow", StringComparison.OrdinalIgnoreCase))
        {
            return MapRimColorMode.Additive;
        }

        if (string.Equals(text, RimCustomColorValue, StringComparison.OrdinalIgnoreCase))
        {
            return MapRimColorMode.Custom;
        }

        return MapRimColorMode.Light;
    }

    public static string ToRimColorValue(MapRimColorMode mode) => mode switch
    {
        MapRimColorMode.Team => RimTeamColorValue,
        MapRimColorMode.Additive => RimAdditiveColorValue,
        MapRimColorMode.Custom => RimCustomColorValue,
        _ => RimLightColorValue,
    };

    /// <summary>Light -> Glow -> Team -> Custom -> Light.</summary>
    public static MapRimColorMode NextRimColorMode(MapRimColorMode mode) => mode switch
    {
        MapRimColorMode.Light => MapRimColorMode.Additive,
        MapRimColorMode.Additive => MapRimColorMode.Team,
        MapRimColorMode.Team => MapRimColorMode.Custom,
        _ => MapRimColorMode.Light,
    };

    public static string GetRimColorDisplayLabel(MapRimColorMode mode) => mode switch
    {
        MapRimColorMode.Team => "Team colour",
        MapRimColorMode.Additive => "Light (glow)",
        MapRimColorMode.Custom => "Custom",
        _ => "Light colour",
    };

    private static int ReadPercent(IReadOnlyDictionary<string, string> metadata, string key, int fallback) =>
        ReadValue(metadata, key, fallback, 100);

    private static int ReadValue(IReadOnlyDictionary<string, string> metadata, string key, int fallback, int max)
    {
        return metadata.TryGetValue(key, out var raw)
            && int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, 0, max)
                : fallback;
    }
}
