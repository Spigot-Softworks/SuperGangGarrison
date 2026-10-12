using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

/// <summary>What a placed light is: an ordinary lamp (round or a static cone), a searchlight, or a laser.</summary>
public enum MapLightKind
{
    Lamp,
    Spotlight,
    Laser,
}

/// <summary>How a light's primary and alternate colours combine.</summary>
public enum MapLightColorMode
{
    /// <summary>Primary colour only.</summary>
    Solid,

    /// <summary>Switches between the two colours over time.</summary>
    Alternate,

    /// <summary>Crossfades smoothly between the two colours.</summary>
    Fade,

    /// <summary>Primary at the centre (or lens), alternate at the edge (or tip).</summary>
    Gradient,

    /// <summary>Half and half: siren sides, or alternating beams in a laser fan.</summary>
    Split,

    /// <summary>Swaps colours on detected music beats (jukebox steadiness).</summary>
    Beat,
}

/// <summary>How a spotlight, laser or cone pivots around its origin.</summary>
public enum MapLightMotion
{
    None,

    /// <summary>Smooth back and forth over the arc, easing at the ends.</summary>
    Sweep,

    /// <summary>Linear ping-pong with a sharp turnaround (laser scanner).</summary>
    Bounce,

    /// <summary>Continuous rotation.</summary>
    Spin,

    /// <summary>Smooth wandering inside the arc (a searching light).</summary>
    Wander,

    /// <summary>Laser-show macro: the fan opens and closes while it sweeps.</summary>
    Scan,

    /// <summary>Stage chase: steps between positions across the arc on each music beat.</summary>
    Beat,
}

/// <summary>What a music-driven light does in the song's quiet sections (breakdowns, quiet verses).</summary>
public enum MapLightDownTime
{
    /// <summary>Carries on as normal.</summary>
    Stay,

    /// <summary>Dims to its minimum brightness.</summary>
    Dim,

    /// <summary>Goes dark until the music picks up.</summary>
    Off,

    /// <summary>Slows to a gentle drift across its arc.</summary>
    Drift,
}

/// <summary>How the siren steadiness rotates.</summary>
public enum MapLightSirenStyle
{
    /// <summary>A rotating beacon seen from the side (rotation about a vertical axis).</summary>
    Beacon,

    /// <summary>Rotation in the screen plane.</summary>
    Spin,
}

/// <summary>Which part of the music a jukebox light follows.</summary>
public enum MapLightReact
{
    Level,
    Bass,
    Mids,
    Highs,
    Beat,
}

/// <summary>What the music drives on a jukebox light.</summary>
public enum MapLightReactTarget
{
    Brightness,
    Speed,
    Both,
}

/// <summary>How a light turns on and off.</summary>
public enum MapLightTransition
{
    Instant,
    Fade,

    /// <summary>A lamp-strike flicker that ramps up (searchlights).</summary>
    Warmup,
    Blink,
}

/// <summary>What the colour logic input does.</summary>
public enum MapLightColorAction
{
    /// <summary>Use the alternate colour while the signal is true.</summary>
    HoldAlt,

    /// <summary>Swap primary and alternate on each rising edge.</summary>
    Swap,

    /// <summary>Step primary, secondary, tertiary colour on each rising edge.</summary>
    Cycle,

    /// <summary>Use the tertiary colour while the signal is true.</summary>
    Trigger,
}

/// <summary>Which team a damaging laser hurts.</summary>
public enum MapLightDamageTeam
{
    All,
    Red,
    Blue,
}

/// <summary>
/// The extended settings of a placed light (type, colours, motion, logic, music, laser
/// hazard). Every default reproduces the original lamp, so maps made before these settings
/// existed look exactly the same.
/// </summary>
public sealed record MapLightStyle
{
    public static MapLightStyle Default { get; } = new();

    public MapLightKind Kind { get; init; } = MapLightKind.Lamp;

    /// <summary>Second colour; null means the light is single-coloured.</summary>
    public MapLightingColor? AltColor { get; init; }

    /// <summary>Third colour for alternate, fade, split and beat modes; null for two colours.</summary>
    public MapLightingColor? AltColor2 { get; init; }

    public MapLightColorMode ColorMode { get; init; } = MapLightColorMode.Solid;

    /// <summary>Alternate/fade speed in cycles per second.</summary>
    public float ColorRate { get; init; } = MapLightMetadata.DefaultColorRate;

    public MapLightMotion Motion { get; init; } = MapLightMotion.None;

    /// <summary>Sweep width around the light's direction, in degrees.</summary>
    public float Arc { get; init; } = MapLightMetadata.DefaultArc;

    /// <summary>Cycles per minute for sweeps, revolutions per minute for spins and sirens.</summary>
    public float Speed { get; init; } = MapLightMetadata.DefaultSpeed;

    /// <summary>Motion offset in degrees of the cycle, for choreographing several lights.</summary>
    public float Phase { get; init; }

    /// <summary>Positions a beat chase steps through across its arc.</summary>
    public int ChaseSteps { get; init; } = MapLightMetadata.DefaultChaseSteps;

    /// <summary>Flips the motion (and siren rotation) into its mirror image, for lights on the other side of a stage.</summary>
    public bool Mirror { get; init; }

    /// <summary>The music widens and narrows the sweep (loud = full arc).</summary>
    public bool MusicSweep { get; init; }

    /// <summary>What the light does in the song's quiet sections.</summary>
    public MapLightDownTime DownTime { get; init; } = MapLightDownTime.Stay;

    /// <summary>Laser fan beam count.</summary>
    public int Beams { get; init; } = 1;

    /// <summary>Angle covered by a laser fan, in degrees.</summary>
    public float FanSpread { get; init; } = MapLightMetadata.DefaultFanSpread;

    /// <summary>Laser core or searchlight beam width, in world pixels.</summary>
    public float Width { get; init; } = MapLightMetadata.DefaultWidth;

    /// <summary>How visible the beam is in the air, 0-100 (times the map's beam haze).</summary>
    public int Haze { get; init; } = MapLightMetadata.DefaultHaze;

    public MapLightSirenStyle SirenStyle { get; init; } = MapLightSirenStyle.Beacon;

    public MapLightReact React { get; init; } = MapLightReact.Level;

    public MapLightReactTarget ReactTarget { get; init; } = MapLightReactTarget.Brightness;

    /// <summary>Music sensitivity, 0-200 (100 normal).</summary>
    public int Sensitivity { get; init; } = 100;

    /// <summary>Minimum brightness while reacting to music, 0-100.</summary>
    public int Floor { get; init; } = MapLightMetadata.DefaultFloor;

    public bool StartOn { get; init; } = true;

    public string OnInput { get; init; } = string.Empty;

    public string OffInput { get; init; } = string.Empty;

    public string ToggleInput { get; init; } = string.Empty;

    public string FollowInput { get; init; } = string.Empty;

    public string ColorInput { get; init; } = string.Empty;

    /// <summary>Independent held signal selecting the built-in tertiary colour; takes priority over ColorInput.</summary>
    public string TertiaryColorInput { get; init; } = string.Empty;

    public MapLightColorAction ColorAction { get; init; } = MapLightColorAction.HoldAlt;

    /// <summary>Legacy extra logic colour, used only when no tertiary colour is selected.</summary>
    public MapLightingColor? TriggerColor { get; init; }

    public MapLightTransition Transition { get; init; } = MapLightTransition.Instant;

    /// <summary>On/off transition length in seconds.</summary>
    public float FadeTime { get; init; } = MapLightMetadata.DefaultFadeTime;

    public MapLightTransition ColorTransition { get; init; } = MapLightTransition.Instant;

    /// <summary>Logic colour-change transition length in seconds.</summary>
    public float ColorTransitionTime { get; init; } = MapLightMetadata.DefaultFadeTime;

    public bool StopsAtPlayers { get; init; }

    public bool Damage { get; init; }

    public int DamagePerSecond { get; init; } = MapLightMetadata.DefaultDamagePerSecond;

    /// <summary>Afterburn strength while damage is on, 0-100 (100 matches a fire zone).</summary>
    public int Burn { get; init; } = 100;

    public MapLightDamageTeam DamageTeam { get; init; } = MapLightDamageTeam.All;

    /// <summary>A laser with damage on: a fixed hazard whose beam never moves.</summary>
    public bool IsDamagingLaser => Kind == MapLightKind.Laser && Damage;

    /// <summary>Motion actually used: damaging lasers stay fixed.</summary>
    public MapLightMotion EffectiveMotion => IsDamagingLaser ? MapLightMotion.None : Motion;

    /// <summary>Whether the music may change speed (never on a damaging laser).</summary>
    public bool MusicDrivesSpeed => !IsDamagingLaser && ReactTarget is MapLightReactTarget.Speed or MapLightReactTarget.Both;

    /// <summary>
    /// True when the light's colour or motion follows the beat (a beat chase, or swapping
    /// colours on the beat). Together with the jukebox steadiness (see
    /// <see cref="MapLightMetadata.UsesMusic"/>) these are the lights down time and music
    /// sweep apply to.
    /// </summary>
    public bool FollowsBeat => ColorMode == MapLightColorMode.Beat || Motion == MapLightMotion.Beat;

    /// <summary>Whether the music changes brightness.</summary>
    public bool MusicDrivesBrightness => ReactTarget is MapLightReactTarget.Brightness or MapLightReactTarget.Both;

    /// <summary>True when any logic input is wired.</summary>
    public bool HasLogicInputs =>
        OnInput.Length > 0
        || OffInput.Length > 0
        || ToggleInput.Length > 0
        || FollowInput.Length > 0
        || ColorInput.Length > 0
        || TertiaryColorInput.Length > 0;

    /// <summary>Second colour, or the primary when the light has none.</summary>
    public MapLightingColor ResolveAltColor(MapLightingColor primary) => AltColor ?? primary;

    /// <summary>Tertiary colour, with the old trigger-colour property retained as a legacy fallback.</summary>
    public MapLightingColor ResolveTriggerColor(MapLightingColor primary) => AltColor2 ?? TriggerColor ?? AltColor ?? primary;
}

/// <summary>Parsing for the extended light settings (see <see cref="MapLightStyle"/>).</summary>
public static class MapLightStyleMetadata
{
    private static readonly (MapLightKind Value, string Text)[] KindValues =
    [
        (MapLightKind.Lamp, "lamp"),
        (MapLightKind.Spotlight, "spotlight"),
        (MapLightKind.Laser, "laser"),
    ];

    private static readonly (MapLightColorMode Value, string Text)[] ColorModeValues =
    [
        (MapLightColorMode.Solid, "solid"),
        (MapLightColorMode.Alternate, "alternate"),
        (MapLightColorMode.Fade, "fade"),
        (MapLightColorMode.Gradient, "gradient"),
        (MapLightColorMode.Split, "split"),
        (MapLightColorMode.Beat, "beat"),
    ];

    private static readonly (MapLightMotion Value, string Text)[] MotionValues =
    [
        (MapLightMotion.None, "none"),
        (MapLightMotion.Sweep, "sweep"),
        (MapLightMotion.Bounce, "bounce"),
        (MapLightMotion.Spin, "spin"),
        (MapLightMotion.Wander, "wander"),
        (MapLightMotion.Scan, "scan"),
        (MapLightMotion.Beat, "beat"),
    ];

    private static readonly (MapLightDownTime Value, string Text)[] DownTimeValues =
    [
        (MapLightDownTime.Stay, "stay"),
        (MapLightDownTime.Dim, "dim"),
        (MapLightDownTime.Off, "off"),
        (MapLightDownTime.Drift, "drift"),
    ];

    private static readonly (MapLightSirenStyle Value, string Text)[] SirenStyleValues =
    [
        (MapLightSirenStyle.Beacon, "beacon"),
        (MapLightSirenStyle.Spin, "spin"),
    ];

    private static readonly (MapLightReact Value, string Text)[] ReactValues =
    [
        (MapLightReact.Level, "level"),
        (MapLightReact.Bass, "bass"),
        (MapLightReact.Mids, "mids"),
        (MapLightReact.Highs, "highs"),
        (MapLightReact.Beat, "beat"),
    ];

    private static readonly (MapLightReactTarget Value, string Text)[] ReactTargetValues =
    [
        (MapLightReactTarget.Brightness, "brightness"),
        (MapLightReactTarget.Speed, "speed"),
        (MapLightReactTarget.Both, "both"),
    ];

    private static readonly (MapLightTransition Value, string Text)[] TransitionValues =
    [
        (MapLightTransition.Instant, "instant"),
        (MapLightTransition.Fade, "fade"),
        (MapLightTransition.Warmup, "warmup"),
        (MapLightTransition.Blink, "blink"),
    ];

    private static readonly (MapLightColorAction Value, string Text)[] ColorActionValues =
    [
        (MapLightColorAction.HoldAlt, "holdAlt"),
        (MapLightColorAction.Swap, "swap"),
        (MapLightColorAction.Cycle, "cycle"),
        (MapLightColorAction.Trigger, "trigger"),
    ];

    private static readonly (MapLightDamageTeam Value, string Text)[] DamageTeamValues =
    [
        (MapLightDamageTeam.All, "all"),
        (MapLightDamageTeam.Red, "red"),
        (MapLightDamageTeam.Blue, "blue"),
    ];

    public static MapLightKind ParseKind(string? value) => Parse(KindValues, value);

    public static string ToKindValue(MapLightKind value) => Format(KindValues, value);

    public static MapLightColorMode ParseColorMode(string? value) => Parse(ColorModeValues, value);

    public static string ToColorModeValue(MapLightColorMode value) => Format(ColorModeValues, value);

    public static MapLightMotion ParseMotion(string? value) => Parse(MotionValues, value);

    public static string ToMotionValue(MapLightMotion value) => Format(MotionValues, value);

    public static MapLightSirenStyle ParseSirenStyle(string? value) => Parse(SirenStyleValues, value);

    public static string ToSirenStyleValue(MapLightSirenStyle value) => Format(SirenStyleValues, value);

    public static MapLightReact ParseReact(string? value) => Parse(ReactValues, value);

    public static string ToReactValue(MapLightReact value) => Format(ReactValues, value);

    public static MapLightReactTarget ParseReactTarget(string? value) => Parse(ReactTargetValues, value);

    public static string ToReactTargetValue(MapLightReactTarget value) => Format(ReactTargetValues, value);

    public static MapLightTransition ParseTransition(string? value) => Parse(TransitionValues, value);

    public static string ToTransitionValue(MapLightTransition value) => Format(TransitionValues, value);

    public static MapLightColorAction ParseColorAction(string? value) => Parse(ColorActionValues, value);

    public static string ToColorActionValue(MapLightColorAction value) => Format(ColorActionValues, value);

    public static MapLightDamageTeam ParseDamageTeam(string? value) => Parse(DamageTeamValues, value);

    public static MapLightDownTime ParseDownTime(string? value) => Parse(DownTimeValues, value);

    public static string ToDownTimeValue(MapLightDownTime value) => Format(DownTimeValues, value);

    public static string ToDamageTeamValue(MapLightDamageTeam value) => Format(DamageTeamValues, value);

    /// <summary>Reads the extended settings; anything missing or unreadable keeps its default.</summary>
    public static MapLightStyle Parse(IReadOnlyDictionary<string, string> properties)
    {
        var defaults = MapLightStyle.Default;
        return new MapLightStyle
        {
            Kind = ParseKind(Read(properties, MapLightMetadata.KindKey)),
            AltColor = ReadColor(properties, MapLightMetadata.AltColorKey),
            AltColor2 = ReadColor(properties, MapLightMetadata.AltColor2Key),
            ColorMode = ParseColorMode(Read(properties, MapLightMetadata.ColorModeKey)),
            ColorRate = ReadFloat(properties, MapLightMetadata.ColorRateKey, defaults.ColorRate, MapLightMetadata.MinColorRate, MapLightMetadata.MaxColorRate),
            Motion = ParseMotion(Read(properties, MapLightMetadata.MotionKey)),
            Arc = ReadFloat(properties, MapLightMetadata.ArcKey, defaults.Arc, 0f, 360f),
            Speed = ReadFloat(properties, MapLightMetadata.SpeedKey, defaults.Speed, 0f, MapLightMetadata.MaxSpeed),
            Phase = MapLightMetadata.NormalizeDirection(ReadFloat(properties, MapLightMetadata.PhaseKey, 0f, -100000f, 100000f)),
            ChaseSteps = ReadInt(properties, MapLightMetadata.ChaseStepsKey, defaults.ChaseSteps, 2, MapLightMetadata.MaxChaseSteps),
            Mirror = ReadBool(properties, MapLightMetadata.MirrorKey, defaults.Mirror),
            MusicSweep = ReadBool(properties, MapLightMetadata.MusicSweepKey, defaults.MusicSweep),
            DownTime = ParseDownTime(Read(properties, MapLightMetadata.DownTimeKey)),
            Beams = ReadInt(properties, MapLightMetadata.BeamsKey, defaults.Beams, 1, MapLightMetadata.MaxBeams),
            FanSpread = ReadFloat(properties, MapLightMetadata.FanSpreadKey, defaults.FanSpread, 0f, 360f),
            Width = ReadFloat(properties, MapLightMetadata.WidthKey, defaults.Width, MapLightMetadata.MinWidth, MapLightMetadata.MaxWidth),
            Haze = ReadInt(properties, MapLightMetadata.HazeKey, defaults.Haze, 0, 100),
            SirenStyle = ParseSirenStyle(Read(properties, MapLightMetadata.SirenStyleKey)),
            React = ParseReact(Read(properties, MapLightMetadata.ReactKey)),
            ReactTarget = ParseReactTarget(Read(properties, MapLightMetadata.ReactToKey)),
            Sensitivity = ReadInt(properties, MapLightMetadata.SensitivityKey, defaults.Sensitivity, 0, 200),
            Floor = ReadInt(properties, MapLightMetadata.FloorKey, defaults.Floor, 0, 100),
            StartOn = ReadBool(properties, MapLightMetadata.StartOnKey, defaults.StartOn),
            OnInput = ReadText(properties, MapLightMetadata.OnInputKey),
            OffInput = ReadText(properties, MapLightMetadata.OffInputKey),
            ToggleInput = ReadText(properties, MapLightMetadata.ToggleInputKey),
            FollowInput = ReadText(properties, MapLightMetadata.FollowInputKey),
            ColorInput = ReadText(properties, MapLightMetadata.ColorInputKey),
            TertiaryColorInput = ReadText(properties, MapLightMetadata.TertiaryColorInputKey),
            ColorAction = ParseColorAction(Read(properties, MapLightMetadata.ColorActionKey)),
            TriggerColor = ReadColor(properties, MapLightMetadata.TriggerColorKey),
            Transition = ParseTransition(Read(properties, MapLightMetadata.TransitionKey)),
            FadeTime = ReadFloat(properties, MapLightMetadata.FadeTimeKey, defaults.FadeTime, 0f, MapLightMetadata.MaxFadeTime),
            ColorTransition = ParseTransition(Read(properties, MapLightMetadata.ColorTransitionKey)),
            ColorTransitionTime = ReadFloat(properties, MapLightMetadata.ColorTransitionTimeKey, defaults.ColorTransitionTime, 0f, MapLightMetadata.MaxFadeTime),
            StopsAtPlayers = ReadBool(properties, MapLightMetadata.StopsAtPlayersKey, defaults.StopsAtPlayers),
            Damage = ReadBool(properties, MapLightMetadata.DamageKey, defaults.Damage),
            DamagePerSecond = ReadInt(properties, MapLightMetadata.DamagePerSecondKey, defaults.DamagePerSecond, 0, MapLightMetadata.MaxDamagePerSecond),
            Burn = ReadInt(properties, MapLightMetadata.BurnKey, defaults.Burn, 0, 100),
            DamageTeam = ParseDamageTeam(Read(properties, MapLightMetadata.DamageTeamKey)),
        };
    }

    private static T Parse<T>((T Value, string Text)[] values, string? text)
        where T : struct, Enum
    {
        var trimmed = text?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            foreach (var (value, name) in values)
            {
                if (name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }
        }

        return values[0].Value;
    }

    private static string Format<T>((T Value, string Text)[] values, T value)
        where T : struct, Enum
    {
        foreach (var (candidate, name) in values)
        {
            if (EqualityComparer<T>.Default.Equals(candidate, value))
            {
                return name;
            }
        }

        return values[0].Text;
    }

    private static string? Read(IReadOnlyDictionary<string, string> properties, string key) =>
        properties.TryGetValue(key, out var value) ? value : null;

    private static string ReadText(IReadOnlyDictionary<string, string> properties, string key) =>
        properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : string.Empty;

    private static MapLightingColor? ReadColor(IReadOnlyDictionary<string, string> properties, string key) =>
        properties.TryGetValue(key, out var value) && MapLightingColor.TryParse(value, out var color) ? color : null;

    private static float ReadFloat(IReadOnlyDictionary<string, string> properties, string key, float fallback, float min, float max) =>
        properties.TryGetValue(key, out var value)
            && float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && float.IsFinite(parsed)
                ? Math.Clamp(parsed, min, max)
                : fallback;

    private static int ReadInt(IReadOnlyDictionary<string, string> properties, string key, int fallback, int min, int max) =>
        properties.TryGetValue(key, out var value)
            && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? Math.Clamp(parsed, min, max)
                : fallback;

    private static bool ReadBool(IReadOnlyDictionary<string, string> properties, string key, bool fallback)
    {
        if (!properties.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();
        if (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1" || text.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (text.Equals("false", StringComparison.OrdinalIgnoreCase) || text == "0" || text.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fallback;
    }
}
