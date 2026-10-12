using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

/// <summary>A light's steadiness (the <c>flicker</c> property).</summary>
/// <remarks>New values are appended so saved numeric values never shift.</remarks>
public enum MapLightFlicker
{
    Steady,
    Flicker,
    Pulse,

    /// <summary>Two opposite cones in the primary and alternate colours, rotating like a beacon.</summary>
    Siren,

    /// <summary>Brightness follows the jukebox music.</summary>
    Jukebox,
}

/// <summary>How a placed light fades toward its edge.</summary>
public enum MapLightFalloff
{
    /// <summary>Follow the map's lighting falloff (smooth or retro).</summary>
    Map,
    Smooth,
    Retro,
}

/// <summary>A placed point light. Presentation only; only visible when the map has lighting on.</summary>
public readonly record struct MapLightMarker(
    float X,
    float Y,
    MapLightingColor Color,
    float Radius,
    int Intensity,
    MapLightFlicker Flicker,
    MapLightFalloff Falloff = MapLightFalloff.Map,
    float Direction = 0f,
    float Spread = MapLightMetadata.FullSpread,
    string? Mask = null)
{
    private readonly MapLightStyle? _style;
    public string SourceName { get; init; } = "";
    public int DestructibleZoneIndex { get; init; } = -1;

    /// <summary>Fixed world size of a painted mask pixel; zero fits an imported mask to the radius.</summary>
    public float MaskPixelSize { get; init; }

    /// <summary>Soft light extending beyond a mask's edges, in world pixels; zero disables it.</summary>
    public float MaskGlowRadius { get; init; }

    /// <summary>Extended settings (type, colours, motion, logic, music, laser hazard). Never null.</summary>
    public MapLightStyle Style
    {
        get => _style ?? MapLightStyle.Default;
        // Defaults are stored as null, so a light with no extended settings equals one made
        // without them (keeps record equality stable for lights from older maps).
        init => _style = value is null || value == MapLightStyle.Default ? null : value;
    }

    public MapLightMarker Scale(float scale) => this with
    {
        X = X * scale,
        Y = Y * scale,
        Radius = Radius * scale,
        MaskPixelSize = MaskPixelSize * scale,
        MaskGlowRadius = MaskGlowRadius * scale,
        Style = Style with { Width = Style.Width * scale },
    };
}

public static class MapLightMetadata
{
    public const string EntityType = "light";
    public const string ColorKey = "color";
    public const string RadiusKey = "radius";
    public const string IntensityKey = "intensity";
    public const string FlickerKey = "flicker";
    public const string FalloffKey = "falloff";
    public const string DirectionKey = "direction";
    public const string SpreadKey = "spread";

    /// <summary>
    /// Optional image resource giving the light a custom shape: lit where the image is solid,
    /// dark where it is transparent (or by brightness for an image with no transparency).
    /// Stretched over the light's radius and turned by its direction; replaces the round or
    /// cone shape.
    /// </summary>
    public const string MaskKey = "mask";
    public const string MaskPixelSizeKey = "maskPixelSize";
    public const string MaskGlowRadiusKey = "maskGlowRadius";
    public const float MaxMaskGlowRadius = 128f;

    // Extended settings (see MapLightStyle). Every default reproduces the original lamp.
    public const string KindKey = "kind";
    public const string AltColorKey = "altColor";
    public const string AltColor2Key = "altColor2";
    public const string ChaseStepsKey = "steps";
    public const string MirrorKey = "mirror";
    public const string MusicSweepKey = "musicSweep";
    public const string DownTimeKey = "downTime";
    public const string ColorModeKey = "colorMode";
    public const string ColorRateKey = "colorRate";
    public const string MotionKey = "motion";
    public const string ArcKey = "arc";
    public const string SpeedKey = "speed";
    public const string PhaseKey = "phase";
    public const string BeamsKey = "beams";
    public const string FanSpreadKey = "fanSpread";
    public const string WidthKey = "width";
    public const string HazeKey = "haze";
    public const string SirenStyleKey = "sirenStyle";
    public const string ReactKey = "react";
    public const string ReactToKey = "reactTo";
    public const string SensitivityKey = "sensitivity";
    public const string FloorKey = "floor";
    public const string StartOnKey = "startOn";
    public const string OnInputKey = "onInput";
    public const string OffInputKey = "offInput";
    public const string ToggleInputKey = "toggleInput";
    public const string FollowInputKey = "followInput";
    public const string ColorInputKey = "colorInput";
    public const string TertiaryColorInputKey = "tertiaryColorInput";
    public const string ColorActionKey = "colorAction";
    public const string TriggerColorKey = "triggerColor";
    public const string TransitionKey = "transition";
    public const string FadeTimeKey = "fadeTime";
    public const string ColorTransitionKey = "colorTransition";
    public const string ColorTransitionTimeKey = "colorTransitionTime";
    public const string StopsAtPlayersKey = "stopsAtPlayers";
    public const string DamageKey = "damage";
    public const string DamagePerSecondKey = "damagePerSecond";
    public const string BurnKey = "burn";
    public const string DamageTeamKey = "damageTeam";

    public const float DefaultColorRate = 1f;
    public const float MinColorRate = 0.05f;
    public const float MaxColorRate = 20f;
    public const float DefaultArc = 90f;
    public const float DefaultSpeed = 10f;
    public const float MaxSpeed = 600f;
    public const int MaxBeams = 16;
    public const int DefaultChaseSteps = 4;
    public const int MaxChaseSteps = 16;
    public const float DefaultFanSpread = 30f;
    public const float DefaultWidth = 4f;
    public const float MinWidth = 1f;
    public const float MaxWidth = 256f;
    public const int DefaultHaze = 100;
    public const int DefaultFloor = 20;
    public const float DefaultFadeTime = 0.5f;
    public const float MaxFadeTime = 10f;
    public const int DefaultDamagePerSecond = 60;
    public const int MaxDamagePerSecond = 500;

    /// <summary>The logic input properties on a light (each holds a logic signal reference).</summary>
    public static IReadOnlyList<string> LogicInputKeys { get; } =
    [
        OnInputKey,
        OffInputKey,
        ToggleInputKey,
        FollowInputKey,
        ColorInputKey,
        TertiaryColorInputKey,
    ];

    public static bool IsLogicInputKey(string key)
    {
        foreach (var candidate in LogicInputKeys)
        {
            if (candidate.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A spread this wide (or wider) is an ordinary round light.</summary>
    public const float FullSpread = 360f;
    public const float MinSpread = 10f;

    public const float DefaultRadius = 160f;
    public const float MinRadius = 8f;
    public const float MaxRadius = 2000f;
    public const int DefaultIntensity = 100;
    public static MapLightingColor DefaultColor => new(255, 207, 128);

    public static bool IsLightEntityType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.Equals(EntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static MapLightFlicker ParseFlicker(string? value)
    {
        var text = value?.Trim();
        if (string.Equals(text, "flicker", StringComparison.OrdinalIgnoreCase))
        {
            return MapLightFlicker.Flicker;
        }

        if (string.Equals(text, "pulse", StringComparison.OrdinalIgnoreCase))
        {
            return MapLightFlicker.Pulse;
        }

        if (string.Equals(text, "siren", StringComparison.OrdinalIgnoreCase))
        {
            return MapLightFlicker.Siren;
        }

        if (string.Equals(text, "jukebox", StringComparison.OrdinalIgnoreCase))
        {
            return MapLightFlicker.Jukebox;
        }

        return MapLightFlicker.Steady;
    }

    public static string ToFlickerValue(MapLightFlicker flicker) => flicker switch
    {
        MapLightFlicker.Flicker => "flicker",
        MapLightFlicker.Pulse => "pulse",
        MapLightFlicker.Siren => "siren",
        MapLightFlicker.Jukebox => "jukebox",
        _ => "steady",
    };

    /// <summary>Steadiness values the builder's cycle button steps through, in order.</summary>
    private static readonly MapLightFlicker[] OfferedFlickers =
    [
        MapLightFlicker.Steady,
        MapLightFlicker.Flicker,
        MapLightFlicker.Pulse,
        MapLightFlicker.Siren,
        MapLightFlicker.Jukebox,
    ];

    /// <summary>A siren's default turn rate (rpm), set when a light is switched to siren at the default speed.</summary>
    public const float DefaultSirenSpeed = 90f;

    public static string CycleFlickerValue(string? current) => CycleFlickerValue(current, MapLightStyle.Default);

    /// <summary>Next offered steadiness; siren is skipped on a damaging laser (it would move the beam).</summary>
    public static string CycleFlickerValue(string? current, MapLightStyle style)
    {
        var offered = new List<MapLightFlicker>(OfferedFlickers.Length);
        foreach (var flicker in OfferedFlickers)
        {
            if (flicker != MapLightFlicker.Siren || !style.IsDamagingLaser)
            {
                offered.Add(flicker);
            }
        }

        var index = offered.IndexOf(ParseFlicker(current));
        return ToFlickerValue(offered[(index + 1) % offered.Count]);
    }

    /// <summary>
    /// True when a light follows the jukebox: the jukebox steadiness, a beat chase, or
    /// colours swapped on the beat. Down time and music sweep apply to these lights.
    /// </summary>
    public static bool UsesMusic(in MapLightMarker light) =>
        light.Flicker == MapLightFlicker.Jukebox || light.Style.FollowsBeat;

    /// <summary>Steadiness actually used: a damaging laser cannot be a (rotating) siren.</summary>
    public static MapLightFlicker GetEffectiveFlicker(MapLightFlicker flicker, MapLightStyle style) =>
        flicker == MapLightFlicker.Siren && style.IsDamagingLaser ? MapLightFlicker.Steady : flicker;

    public static MapLightFalloff ParseFalloff(string? value)
    {
        var text = value?.Trim();
        if (string.Equals(text, "smooth", StringComparison.OrdinalIgnoreCase))
        {
            return MapLightFalloff.Smooth;
        }

        if (string.Equals(text, "retro", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "bands", StringComparison.OrdinalIgnoreCase))
        {
            return MapLightFalloff.Retro;
        }

        return MapLightFalloff.Map;
    }

    public static string ToFalloffValue(MapLightFalloff falloff) => falloff switch
    {
        MapLightFalloff.Smooth => "smooth",
        MapLightFalloff.Retro => "retro",
        _ => "map",
    };

    /// <summary>Map -> Smooth -> Retro -> Map.</summary>
    public static string CycleFalloffValue(string? current) =>
        ToFalloffValue((MapLightFalloff)(((int)ParseFalloff(current) + 1) % 3));

    /// <summary>
    /// Whether this light draws with retro bands, given the map's own falloff style.
    /// </summary>
    public static bool UsesRetroBands(MapLightFalloff falloff, bool mapBanded) => falloff switch
    {
        MapLightFalloff.Smooth => false,
        MapLightFalloff.Retro => true,
        _ => mapBanded,
    };

    public static MapLightMarker FromProperties(float x, float y, IReadOnlyDictionary<string, string> properties)
    {
        var color = properties.TryGetValue(ColorKey, out var rawColor) && MapLightingColor.TryParse(rawColor, out var parsedColor)
            ? parsedColor
            : DefaultColor;
        var radius = properties.TryGetValue(RadiusKey, out var rawRadius)
            && float.TryParse(rawRadius.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRadius)
            && float.IsFinite(parsedRadius)
                ? Math.Clamp(parsedRadius, MinRadius, MaxRadius)
                : DefaultRadius;
        var intensity = properties.TryGetValue(IntensityKey, out var rawIntensity)
            && int.TryParse(rawIntensity.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIntensity)
                ? Math.Clamp(parsedIntensity, 0, 200)
                : DefaultIntensity;
        properties.TryGetValue(FlickerKey, out var flicker);
        properties.TryGetValue(FalloffKey, out var falloff);
        var direction = properties.TryGetValue(DirectionKey, out var rawDirection)
            && float.TryParse(rawDirection.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDirection)
            && float.IsFinite(parsedDirection)
                ? NormalizeDirection(parsedDirection)
                : 0f;
        var spread = properties.TryGetValue(SpreadKey, out var rawSpread)
            && float.TryParse(rawSpread.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSpread)
            && float.IsFinite(parsedSpread)
                ? Math.Clamp(parsedSpread, MinSpread, FullSpread)
                : FullSpread;
        var mask = properties.TryGetValue(MaskKey, out var rawMask) && !string.IsNullOrWhiteSpace(rawMask)
            ? rawMask.Trim()
            : null;
        return new MapLightMarker(x, y, color, radius, intensity, ParseFlicker(flicker), ParseFalloff(falloff), direction, spread, mask)
        {
            SourceName = properties.GetValueOrDefault(MapLogicMetadata.MapEntityIdPropertyKey) ?? "",
            Style = MapLightStyleMetadata.Parse(properties),
            MaskGlowRadius = properties.TryGetValue(MaskGlowRadiusKey, out var rawGlowRadius)
                && float.TryParse(rawGlowRadius, NumberStyles.Float, CultureInfo.InvariantCulture, out var glowRadius)
                && float.IsFinite(glowRadius) ? Math.Clamp(glowRadius, 0f, MaxMaskGlowRadius) : 0f,
            MaskPixelSize = mask is not null && properties.TryGetValue(MaskPixelSizeKey, out var rawPixelSize)
                && float.TryParse(rawPixelSize, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixelSize)
                && float.IsFinite(pixelSize) && pixelSize > 0f ? Math.Clamp(pixelSize, 0.01f, 256f) : 0f,
        };
    }

    /// <summary>
    /// Where a light points: its direction, reflected left-to-right when the light is
    /// mirrored (so a mirrored copy of a light aims and moves as its mirror image).
    /// </summary>
    public static float GetAimDirection(in MapLightMarker light) =>
        light.Style.Mirror ? MirrorDirection(light.Direction) : light.Direction;

    /// <summary>A direction reflected left-to-right (across the vertical axis).</summary>
    public static float MirrorDirection(float degrees) => NormalizeDirection(180f - degrees);

    /// <summary>Degrees in [0, 360): 0 = right, 90 = up, 180 = left, 270 = down.</summary>
    public static float NormalizeDirection(float degrees)
    {
        var result = degrees % 360f;
        return result < 0f ? result + 360f : result;
    }

    /// <summary>True for a spotlight (a cone narrower than a full circle).</summary>
    public static bool IsDirectional(float spread) => spread < FullSpread - 5f;

    /// <summary>World-space unit vector for a direction (screen y points down).</summary>
    public static (float X, float Y) GetDirectionVector(float degrees)
    {
        var radians = degrees * (MathF.PI / 180f);
        return (DeterministicMath.Cos(radians), -DeterministicMath.Sin(radians));
    }
}

internal sealed class MapLightMapEntityRuntimeImporter : ICustomMapEntityRuntimeImporter
{
    public string EntityType => MapLightMetadata.EntityType;

    public bool TryImport(CustomMapEntityImportArgs args, CustomMapEntityImportContext context)
    {
        if (!MapLightMetadata.IsLightEntityType(args.Type))
        {
            return false;
        }

        context.MapLights.Add(MapLightMetadata.FromProperties(args.X, args.Y, args.Properties));
        return true;
    }
}
