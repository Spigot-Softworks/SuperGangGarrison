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
/// <param name="Banded">Retro stepped light falloff instead of smooth gradients.</param>
/// <param name="PlayerRange">Radius of the player light, as a percentage of its normal size.</param>
/// <param name="EffectRange">Radius of gameplay effect lights, as a percentage of their normal size.</param>
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
    int EffectRange = 100)
{
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
        StyleKey,
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
            PlayerRange = ReadValue(metadata, PlayerRangeKey, lighting.PlayerRange, MaxScale),
            EffectRange = ReadValue(metadata, EffectRangeKey, lighting.EffectRange, MaxScale),
        };

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
        metadata[StyleKey] = lighting.Banded ? BandedStyleValue : SmoothStyleValue;
        metadata[PlayerRangeKey] = ClampScale(lighting.PlayerRange).ToString(CultureInfo.InvariantCulture);
        metadata[EffectRangeKey] = ClampScale(lighting.EffectRange).ToString(CultureInfo.InvariantCulture);
    }

    public static int Clamp(int percent) => Math.Clamp(percent, 0, 100);

    /// <summary>Clamp for brightness and the ranges, where 100 is normal and 200 is double.</summary>
    public static int ClampScale(int percent) => Math.Clamp(percent, 0, MaxScale);

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
