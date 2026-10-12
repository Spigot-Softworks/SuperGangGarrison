using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

/// <summary>
/// One light the renderer adds to the light map this frame, produced by <see cref="MapLightEmission"/>.
/// A placed light can produce several (a siren's two cones, a gradient lamp's two layers).
/// </summary>
/// <param name="SourceIndex">Index of the placed light this came from (for wall reach and masks).</param>
/// <param name="Intensity">Light strength, 1 = full colour (already includes steadiness and power).</param>
public readonly record struct MapLightEmitter(
    int SourceIndex,
    float X,
    float Y,
    float Radius,
    MapLightingColor Color,
    float Intensity,
    MapLightFalloff Falloff,
    float Direction,
    float Spread,
    string? Mask)
{
    public float MaskPixelSize { get; init; }
    public float MaskGlowRadius { get; init; }
}

/// <summary>
/// A visible beam (searchlight shaft or laser) for this frame. The renderer clips it at
/// walls and players, draws it in the air (scaled by haze) and lights where it lands.
/// </summary>
/// <param name="BeamIndex">Index within the light's fan (0 for single beams).</param>
/// <param name="Direction">Degrees: 0 = right, 90 = up.</param>
/// <param name="Length">Unclipped length in world pixels.</param>
/// <param name="Width">Laser core width, or the searchlight's lens width.</param>
/// <param name="Spread">Searchlight cone angle in degrees (0 for lasers).</param>
/// <param name="Color">Colour at the lens.</param>
/// <param name="EndColor">Colour at the far end (same as <paramref name="Color"/> unless gradient).</param>
/// <param name="Haze">The light's own beam visibility, 0..1 (times the map's beam haze).</param>
/// <param name="Damaging">A fixed damaging laser: always shows its core, uses the shared fixed geometry.</param>
public readonly record struct MapLightBeam(
    int SourceIndex,
    int BeamIndex,
    MapLightKind Kind,
    float X,
    float Y,
    float Direction,
    float Length,
    float Width,
    float Spread,
    MapLightingColor Color,
    MapLightingColor EndColor,
    float Intensity,
    float Haze,
    bool StopsAtPlayers,
    bool Damaging);

/// <summary>Jukebox music features for this frame, all 0..1 (see the client's jukebox analyzer).</summary>
/// <param name="Active">False when no music is audible (nothing playing, or the player muted it).</param>
/// <param name="Beat">Pulse that jumps to 1 on a detected beat and decays quickly.</param>
/// <param name="BeatCount">Number of beats so far (for colour swaps on the beat).</param>
public readonly record struct MapLightMusic(bool Active, float Level, float Bass, float Mids, float Highs, float Beat, int BeatCount)
{
    public static MapLightMusic Silent => default;

    /// <summary>True during a quiet section of the song (a breakdown or quiet verse).</summary>
    public bool Quiet { get; init; }

    /// <summary>Smoothed 0..1 version of <see cref="Quiet"/> (1 = fully in the quiet section).</summary>
    public float QuietBlend { get; init; }

    /// <summary>
    /// The player's "lights follow the jukebox" setting: every light pulses with the music,
    /// whatever the map set (lights already set to the jukebox steadiness are unchanged).
    /// </summary>
    public bool ForceReact { get; init; }

    public float Get(MapLightReact react) => react switch
    {
        MapLightReact.Bass => Bass,
        MapLightReact.Mids => Mids,
        MapLightReact.Highs => Highs,
        MapLightReact.Beat => Beat,
        _ => Level,
    };
}

/// <summary>Per-light runtime inputs: logic power and colour state, and optional motion position.</summary>
/// <param name="Powered">The logic power target.</param>
/// <param name="Progress">On/off transition position, 0 = fully off, 1 = fully on.</param>
/// <param name="ColorState">Logic colour state (see <see cref="MapLightColorState"/>).</param>
/// <param name="MotionCycles">Integrated motion position in cycles (when the music drives speed); null derives it from time.</param>
/// <param name="ColorProgress">Logic colour-change transition position, 0 = starting, 1 = complete.</param>
public readonly record struct MapLightRuntimeSample(bool Powered, float Progress, MapLightColorState ColorState, double? MotionCycles = null, float ColorProgress = 1f)
{
    public static MapLightRuntimeSample On => new(true, 1f, MapLightColorState.Normal);

    public static MapLightRuntimeSample Off => new(false, 0f, MapLightColorState.Normal);

    /// <summary>The state a light starts in before any logic runs.</summary>
    public static MapLightRuntimeSample Initial(MapLightStyle style) => style.StartOn ? On : Off;
}

/// <summary>What the colour logic input has done to a light's colours.</summary>
public enum MapLightColorState
{
    Normal,

    /// <summary>Primary and alternate swapped.</summary>
    Swapped,

    /// <summary>The tertiary colour (or legacy trigger fallback), solid.</summary>
    Trigger,
}

/// <summary>
/// Turns placed lights into what is drawn each frame. Pure and deterministic (no graphics),
/// so gameplay and the builder preview share it and tests can check it directly.
/// </summary>
public static class MapLightEmission
{
    /// <summary>Seed spacing between lights, so neighbouring lights do not flicker in step.</summary>
    public const float SeedStep = 7.31f;

    /// <summary>Searchlight cone when the light is left round (spread 360).</summary>
    public const float DefaultSpotlightSpread = 10f;

    /// <summary>Siren cone when the light is left round (spread 360).</summary>
    public const float DefaultSirenSpread = 50f;

    /// <summary>The steadiness seed for the light at an index.</summary>
    public static float GetSeed(int index) => index * SeedStep;

    /// <summary>Appends the emitters for one placed light (always on, no music: the original lamp path).</summary>
    public static void Evaluate(in MapLightMarker light, int seedIndex, double time, List<MapLightEmitter> output) =>
        Evaluate(light, seedIndex, time, MapLightRuntimeSample.On, MapLightMusic.Silent, output, beams: null);

    /// <summary>Appends the emitters for every light in a list, seeded by list position (always on, no music).</summary>
    public static void EvaluateAll(IReadOnlyList<MapLightMarker> lights, double time, List<MapLightEmitter> output)
    {
        for (var index = 0; index < lights.Count; index += 1)
        {
            var light = lights[index];
            Evaluate(light, index, time, output);
        }
    }

    /// <summary>
    /// Appends what one placed light draws this frame: light-map emitters, and (when
    /// <paramref name="beams"/> is given) visible beams for searchlights and lasers.
    /// </summary>
    public static void Evaluate(
        in MapLightMarker light,
        int seedIndex,
        double time,
        MapLightRuntimeSample runtime,
        MapLightMusic music,
        List<MapLightEmitter> output,
        List<MapLightBeam>? beams)
    {
        var style = light.Style;
        var power = GetPowerLevel(style, runtime, time, seedIndex);
        if (power <= 0.001f)
        {
            return;
        }

        var flicker = MapLightMetadata.GetEffectiveFlicker(light.Flicker, style);
        var seed = GetSeed(seedIndex);
        var brightness = flicker == MapLightFlicker.Jukebox
            ? GetMusicBrightness(style, music, time, seed)
            : MapLightAnimation.GetFlicker(flicker, time, seed);
        if (music.Active && music.ForceReact && flicker != MapLightFlicker.Jukebox)
        {
            brightness *= GetForcedMusicBrightness(music);
        }
        // (The renderer drops lights too faint to see; emitters are still produced so callers
        // see every light.)
        var intensity = light.Intensity / 100f * brightness * power;

        var usesMusic = music.Active && MapLightMetadata.UsesMusic(light);
        var quiet = usesMusic ? Math.Clamp(music.QuietBlend, 0f, 1f) : 0f;
        intensity *= GetDownTimeBrightness(style, quiet);

        var palette = ResolvePalette(light, runtime.ColorState, out var solid);
        var primary = palette[0];
        var alternate = palette.Length > 1 ? palette[1] : primary;
        var current = solid ? primary : GetTimedColor(style, palette, time, music);
        var cycles = runtime.MotionCycles ?? GetMotionCycles(style, time);
        var direction = MapLightMetadata.NormalizeDirection(MapLightMetadata.GetAimDirection(light) + GetShowMotionOffset(style, cycles, seed, time, music, usesMusic, quiet));

        if (flicker == MapLightFlicker.Siren)
        {
            EmitSiren(light, seedIndex, intensity, primary, solid ? primary : alternate, GetSirenCycles(style, time, runtime), output, beams);
            return;
        }

        switch (style.Kind)
        {
            case MapLightKind.Spotlight:
                EmitSpotlight(light, seedIndex, direction, intensity, current, solid ? current : alternate, output, beams);
                return;
            case MapLightKind.Laser:
                EmitLaser(light, seedIndex, direction, intensity, current, solid ? current : alternate, solid, cycles, palette, music, output, beams);
                return;
            default:
                EmitLamp(light, seedIndex, direction, intensity, current, solid ? current : alternate, solid, output);
                return;
        }
    }

    /// <summary>Brightness multiplier in the song's quiet sections, by the light's down-time setting.</summary>
    public static float GetDownTimeBrightness(MapLightStyle style, float quiet)
    {
        if (quiet <= 0f)
        {
            return 1f;
        }

        return style.DownTime switch
        {
            MapLightDownTime.Dim => 1f + (((style.Floor / 100f) - 1f) * quiet),
            MapLightDownTime.Off => 1f - quiet,
            MapLightDownTime.Drift => 1f - (0.4f * quiet),
            _ => 1f,
        };
    }

    /// <summary>
    /// The full motion offset for this frame: the base motion (or the beat chase), widened
    /// by the music, slowed to a drift in quiet sections, and mirrored when set.
    /// </summary>
    public static float GetShowMotionOffset(MapLightStyle style, double cycles, float seed, double time, MapLightMusic music, bool usesMusic, float quiet)
    {
        var motion = style.EffectiveMotion;
        if (motion == MapLightMotion.None)
        {
            return 0f;
        }

        var offset = motion == MapLightMotion.Beat
            ? GetBeatChaseOffset(style, music)
            : GetMotionOffset(style, cycles, seed);
        if (motion != MapLightMotion.Spin)
        {
            if (usesMusic && style.MusicSweep)
            {
                var amount = Math.Clamp(music.Get(style.React) * (style.Sensitivity / 100f), 0f, 1f);
                offset *= 0.3f + (0.7f * amount);
            }

            if (style.DownTime == MapLightDownTime.Drift && quiet > 0f)
            {
                // A slow, wide drift (one pass every eight seconds) while the song is quiet.
                var drift = style.Arc * 0.5f * DeterministicMath.Sin((float)((time / 8d) - Math.Floor(time / 8d)) * MathF.Tau);
                offset += (drift - offset) * quiet;
            }
        }

        return style.Mirror ? -offset : offset;
    }

    /// <summary>
    /// Stage chase: on each beat the light moves to the next of its steps across the arc,
    /// ping-ponging end to end, gliding there quickly as the beat pulse fades. With no
    /// music it rests at the centre. Lights with the same steps stay in step (the beat
    /// count is shared); phase offsets them by whole steps.
    /// </summary>
    public static float GetBeatChaseOffset(MapLightStyle style, MapLightMusic music)
    {
        if (!music.Active)
        {
            return 0f;
        }

        var steps = Math.Max(2, style.ChaseSteps);
        var period = 2 * (steps - 1);
        var shift = (int)MathF.Round(style.Phase / 360f * period);
        var current = GetChasePosition(style, music.BeatCount + shift, steps, period);
        var previous = GetChasePosition(style, music.BeatCount + shift - 1, steps, period);
        var glide = Math.Clamp(1f - music.Beat, 0f, 1f);
        glide = glide * glide * (3f - (2f * glide));
        return previous + ((current - previous) * glide);
    }

    private static float GetChasePosition(MapLightStyle style, int step, int steps, int period)
    {
        var index = ((step % period) + period) % period;
        var position = index < steps ? index : period - index;
        return (style.Arc * -0.5f) + (style.Arc * position / (steps - 1));
    }

    /// <summary>
    /// The light's colours in order (primary, alternate, alternate 2; those set), after
    /// logic: swapped rotates them by one, trigger forces the trigger colour.
    /// </summary>
    public static MapLightingColor[] ResolvePalette(in MapLightMarker light, MapLightColorState state, out bool solid)
    {
        var style = light.Style;
        if (state == MapLightColorState.Trigger)
        {
            solid = true;
            return [style.ResolveTriggerColor(light.Color)];
        }

        var colors = new List<MapLightingColor>(3) { light.Color };
        if (style.AltColor is { } alt)
        {
            colors.Add(alt);
        }

        if (style.AltColor2 is { } alt2)
        {
            colors.Add(alt2);
        }

        solid = colors.Count == 1;
        if (state == MapLightColorState.Swapped && style.AltColor is not null && colors.Count > 1)
        {
            colors.Add(colors[0]);
            colors.RemoveAt(0);
        }

        return colors.ToArray();
    }

    /// <summary>
    /// Visible power 0..1 from the logic state and the light's transition (fade, warm-up
    /// flicker, blink).
    /// </summary>
    public static float GetPowerLevel(MapLightStyle style, MapLightRuntimeSample runtime, double time, int seedIndex)
    {
        return GetTransitionLevel(style.Transition, runtime.Powered, runtime.Progress, time, seedIndex)
            * GetTransitionLevel(style.ColorTransition, true, runtime.ColorProgress, time, seedIndex);
    }

    private static float GetTransitionLevel(MapLightTransition transition, bool powered, float progress, double time, int seedIndex)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        if (progress >= 1f)
        {
            return 1f;
        }

        if (progress <= 0f)
        {
            return 0f;
        }

        switch (transition)
        {
            case MapLightTransition.Fade:
                return progress * progress * (3f - (2f * progress));
            case MapLightTransition.Warmup:
                if (!powered)
                {
                    return progress;
                }

                // Lamp strike: stutters hard at first, settling as it reaches full power.
                var stutter = MapLightAnimation.SmoothNoise((time * 23d) + GetSeed(seedIndex));
                var settle = progress * progress;
                return Math.Clamp(settle + ((1f - settle) * (stutter > 0.55f ? stutter : 0.05f)), 0f, 1f) * (0.35f + (0.65f * progress));
            case MapLightTransition.Blink:
                // A few hard blinks during the transition, ending in the target state.
                return ((int)(progress * 7f) & 1) == 0 ? (powered ? 1f : 0f) : (powered ? 0f : 1f);
            default:
                return powered ? 1f : 0f;
        }
    }

    /// <summary>Brightness for the jukebox steadiness: floor when silent, up to full with the music.</summary>
    public static float GetMusicBrightness(MapLightStyle style, MapLightMusic music, double time, float seed)
    {
        var floor = style.Floor / 100f;
        if (!music.Active || !style.MusicDrivesBrightness)
        {
            return music.Active ? 1f : floor;
        }

        var amount = Math.Clamp(music.Get(style.React) * (style.Sensitivity / 100f), 0f, 1f);
        return floor + ((1f - floor) * amount);
    }

    /// <summary>
    /// Brightness for a light the player forces to follow the jukebox: a punchy pulse on
    /// the bass and beats over a base of 35%, so lights never vanish between beats.
    /// </summary>
    public static float GetForcedMusicBrightness(MapLightMusic music) =>
        0.35f + (0.65f * Math.Clamp(MathF.Max(music.Bass, music.Beat), 0f, 1f));

    /// <summary>The light's primary and second colour after logic, and whether it is forced solid.</summary>
    public static (MapLightingColor Primary, MapLightingColor Alternate, bool Solid) ResolveColors(in MapLightMarker light, MapLightColorState state)
    {
        var style = light.Style;
        var primary = light.Color;
        var alternate = style.ResolveAltColor(primary);
        return state switch
        {
            MapLightColorState.Swapped => (alternate, primary, style.AltColor is null),
            MapLightColorState.Trigger => (style.ResolveTriggerColor(primary), style.ResolveTriggerColor(primary), true),
            _ => (primary, alternate, style.AltColor is null),
        };
    }

    /// <summary>
    /// Colour for the time-based modes across all the light's colours: alternate steps
    /// through them, fade blends around them, beat moves to the next on each beat.
    /// </summary>
    public static MapLightingColor GetTimedColor(MapLightStyle style, MapLightingColor[] palette, double time, MapLightMusic music)
    {
        if (palette.Length <= 2)
        {
            return GetTimedColor(style, palette[0], palette.Length > 1 ? palette[1] : palette[0], time, music);
        }

        var count = palette.Length;
        switch (style.ColorMode)
        {
            case MapLightColorMode.Alternate:
                return palette[(int)(((long)Math.Floor(time * style.ColorRate) % count + count) % count)];
            case MapLightColorMode.Fade:
                var position = time * style.ColorRate;
                var step = (long)Math.Floor(position);
                var blend = (float)(position - step);
                blend = blend * blend * (3f - (2f * blend));
                var from = (int)(((step % count) + count) % count);
                return Lerp(palette[from], palette[(from + 1) % count], blend);
            case MapLightColorMode.Beat:
                return music.Active ? palette[((music.BeatCount % count) + count) % count] : palette[0];
            default:
                return palette[0];
        }
    }

    /// <summary>Colour for the time-based modes (alternate, fade, beat) with two colours; other modes return the primary.</summary>
    public static MapLightingColor GetTimedColor(MapLightStyle style, MapLightingColor primary, MapLightingColor alternate, double time, MapLightMusic music)
    {
        switch (style.ColorMode)
        {
            case MapLightColorMode.Alternate:
                return ((long)Math.Floor(time * style.ColorRate) & 1) == 0 ? primary : alternate;
            case MapLightColorMode.Fade:
                var t = 0.5f - (0.5f * DeterministicMath.Cos((float)(time * style.ColorRate * Math.PI * 2d)));
                return Lerp(primary, alternate, t);
            case MapLightColorMode.Beat:
                return music.Active && (music.BeatCount & 1) == 1 ? alternate : primary;
            default:
                return primary;
        }
    }

    /// <summary>Motion position in cycles at a time: speed is cycles (or turns) per minute.</summary>
    public static double GetMotionCycles(MapLightStyle style, double time) =>
        (time * style.Speed / 60d) + (style.Phase / 360d);

    /// <summary>Degrees added to the light's direction by its motion at a cycle position.</summary>
    public static float GetMotionOffset(MapLightStyle style, double cycles, float seed)
    {
        var halfArc = style.Arc * 0.5f;
        var fraction = (float)(cycles - Math.Floor(cycles));
        return style.EffectiveMotion switch
        {
            // Sine: slows and turns at the ends like a real searchlight.
            MapLightMotion.Sweep or MapLightMotion.Scan => halfArc * DeterministicMath.Sin(fraction * MathF.Tau),
            // Triangle wave: constant speed, sharp turnaround.
            MapLightMotion.Bounce => halfArc * ((4f * MathF.Abs(fraction - 0.5f)) - 1f),
            MapLightMotion.Spin => fraction * 360f,
            // Two octaves of smooth noise: drifts and hesitates like a searching operator.
            MapLightMotion.Wander => halfArc * Math.Clamp(
                (((MapLightAnimation.SmoothNoise((cycles * 2d) + seed) * 2f) - 1f) * 0.75f)
                + (((MapLightAnimation.SmoothNoise((cycles * 5d) + seed + 31d) * 2f) - 1f) * 0.25f),
                -1f,
                1f),
            _ => 0f,
        };
    }

    /// <summary>Fan width multiplier for the laser scan motion (opens and closes).</summary>
    public static float GetFanScale(MapLightStyle style, double cycles) =>
        style.EffectiveMotion == MapLightMotion.Scan
            ? 0.15f + (0.85f * (0.5f + (0.5f * DeterministicMath.Sin((float)((cycles * 0.5d) - Math.Floor(cycles * 0.5d)) * MathF.Tau))))
            : 1f;

    /// <summary>Angle of one beam in a laser fan.</summary>
    public static float GetFanAngle(MapLightStyle style, float direction, int beamIndex, float fanScale = 1f)
    {
        var count = Math.Max(1, style.Beams);
        if (count == 1)
        {
            return direction;
        }

        var spread = style.FanSpread * fanScale;
        // A mirrored fan runs the other way, so its beams (and colours) mirror too.
        var position = style.Mirror ? count - 1 - beamIndex : beamIndex;
        return MapLightMetadata.NormalizeDirection(direction - (spread * 0.5f) + (spread * position / (count - 1)));
    }

    private static double GetSirenCycles(MapLightStyle style, double time, MapLightRuntimeSample runtime) =>
        runtime.MotionCycles ?? GetMotionCycles(style, time);

    private static void EmitLamp(
        in MapLightMarker light,
        int sourceIndex,
        float direction,
        float intensity,
        MapLightingColor color,
        MapLightingColor alternate,
        bool solid,
        List<MapLightEmitter> output)
    {
        var mode = light.Style.ColorMode;
        if (!solid && mode == MapLightColorMode.Gradient && light.Mask is null)
        {
            // Alternate at the edge, primary at the heart: the full-size layer in the second
            // colour, a smaller primary layer on top.
            output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, light.Radius, alternate, intensity * 0.8f, light.Falloff, direction, light.Spread, null));
            output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, light.Radius * 0.55f, color, intensity * 0.9f, light.Falloff, direction, light.Spread, null));
            return;
        }

        if (!solid && mode == MapLightColorMode.Split && light.Mask is null)
        {
            // Two halves: across a round light, or the two sides of a cone.
            if (MapLightMetadata.IsDirectional(light.Spread))
            {
                var quarter = light.Spread * 0.25f;
                var half = MathF.Max(MapLightMetadata.MinSpread, light.Spread * 0.5f);
                output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, light.Radius, color, intensity, light.Falloff, MapLightMetadata.NormalizeDirection(direction + quarter), half, null));
                output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, light.Radius, alternate, intensity, light.Falloff, MapLightMetadata.NormalizeDirection(direction - quarter), half, null));
            }
            else
            {
                output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, light.Radius, color, intensity, light.Falloff, direction, 180f, null));
                output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, light.Radius, alternate, intensity, light.Falloff, MapLightMetadata.NormalizeDirection(direction + 180f), 180f, null));
            }

            return;
        }

        output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, light.Radius, color, intensity, light.Falloff, direction, light.Spread, light.Mask) { MaskPixelSize = light.MaskPixelSize, MaskGlowRadius = light.MaskGlowRadius });
    }

    private static void EmitSpotlight(
        in MapLightMarker light,
        int sourceIndex,
        float direction,
        float intensity,
        MapLightingColor color,
        MapLightingColor endColor,
        List<MapLightEmitter> output,
        List<MapLightBeam>? beams)
    {
        var spread = MapLightMetadata.IsDirectional(light.Spread) ? light.Spread : DefaultSpotlightSpread;
        var gradient = light.Style.ColorMode == MapLightColorMode.Gradient;
        // The cone lights whatever it falls on (wall-blocked through the light's reach).
        output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, light.Radius, gradient ? endColor : color, intensity, light.Falloff, direction, spread, light.Mask) { MaskPixelSize = light.MaskPixelSize, MaskGlowRadius = light.MaskGlowRadius });
        beams?.Add(new MapLightBeam(
            sourceIndex,
            0,
            MapLightKind.Spotlight,
            light.X,
            light.Y,
            direction,
            light.Radius,
            light.Style.Width,
            spread,
            color,
            gradient ? endColor : color,
            intensity,
            light.Style.Haze / 100f,
            light.Style.StopsAtPlayers,
            Damaging: false));
    }

    private static void EmitLaser(
        in MapLightMarker light,
        int sourceIndex,
        float direction,
        float intensity,
        MapLightingColor color,
        MapLightingColor alternate,
        bool solid,
        double cycles,
        MapLightingColor[] palette,
        MapLightMusic music,
        List<MapLightEmitter> output,
        List<MapLightBeam>? beams)
    {
        var style = light.Style;
        var count = Math.Max(1, style.Beams);
        var colorCount = palette.Length;
        var fanScale = GetFanScale(style, cycles);
        var damaging = style.IsDamagingLaser;
        // A damaging laser never dims away completely: the hazard stays readable.
        var beamIntensity = damaging ? MathF.Max(intensity, MinimumDamagingLaserIntensity) : intensity;

        // A small glow at the emitter so the fixture reads.
        output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, MathF.Min(light.Radius, 18f + (style.Width * 3f)), color, beamIntensity * 0.6f, light.Falloff, direction, MapLightMetadata.FullSpread, null));
        if (beams is null)
        {
            return;
        }

        for (var beam = 0; beam < count; beam += 1)
        {
            var beamColor = color;
            var endColor = color;
            if (!solid)
            {
                switch (style.ColorMode)
                {
                    case MapLightColorMode.Split:
                        beamColor = endColor = palette[beam % colorCount];
                        break;
                    case MapLightColorMode.Beat when count > 1 && music.Active:
                        // Each beat shifts the colours one beam along the fan.
                        beamColor = endColor = palette[(((beam + music.BeatCount) % colorCount) + colorCount) % colorCount];
                        break;
                    case MapLightColorMode.Gradient:
                        endColor = alternate;
                        break;
                    case MapLightColorMode.Alternate when count > 1:
                        // Colour chase across the fan.
                        var step = (long)Math.Floor(cycles * count * 2d) + beam;
                        beamColor = endColor = palette[(int)(((step % colorCount) + colorCount) % colorCount)];
                        break;
                }
            }

            beams.Add(new MapLightBeam(
                sourceIndex,
                beam,
                MapLightKind.Laser,
                light.X,
                light.Y,
                GetFanAngle(style, direction, beam, fanScale),
                light.Radius,
                style.Width,
                0f,
                beamColor,
                endColor,
                beamIntensity,
                style.Haze / 100f,
                style.StopsAtPlayers,
                damaging));
        }
    }

    /// <summary>A damaging laser's core never fades below this (so a hazard is never invisible).</summary>
    public const float MinimumDamagingLaserIntensity = 0.45f;

    /// <summary>
    /// Siren: two opposite beams in the primary and alternate colours. Beacon style is a
    /// rotator turning about a vertical axis, seen from the side: the beams swing left and
    /// right, stretch and fade with the turn, and flash as each faces the camera. Spin
    /// style turns the two cones in the screen plane.
    /// </summary>
    private static void EmitSiren(
        in MapLightMarker light,
        int sourceIndex,
        float intensity,
        MapLightingColor primary,
        MapLightingColor alternate,
        double cycles,
        List<MapLightEmitter> output,
        List<MapLightBeam>? beams)
    {
        var style = light.Style;
        var spread = MapLightMetadata.IsDirectional(light.Spread) ? light.Spread : DefaultSirenSpread;
        var fraction = (float)(cycles - Math.Floor(cycles));
        if (style.Mirror)
        {
            // Turn the other way: the mirror image of an unmirrored siren beside it.
            fraction = 1f - fraction;
        }

        var laser = style.Kind == MapLightKind.Laser;
        for (var side = 0; side < 2; side += 1)
        {
            var color = side == 0 ? primary : alternate;
            float direction;
            float strength;
            float reach;
            if (style.SirenStyle == MapLightSirenStyle.Spin)
            {
                direction = MapLightMetadata.NormalizeDirection(MapLightMetadata.GetAimDirection(light) + (fraction * 360f) + (side * 180f));
                strength = 1f;
                reach = 1f;
            }
            else
            {
                // Azimuth around the vertical axis; sin > 0 faces the camera.
                var azimuth = (fraction + (side * 0.5f)) * MathF.Tau;
                var across = DeterministicMath.Cos(azimuth);
                var toward = DeterministicMath.Sin(azimuth);
                direction = across >= 0f ? 0f : 180f;
                var sideways = MathF.Abs(across);
                // Seen edge-on a beam is long and bright; pointing at or away from us it is short.
                strength = DeterministicMath.Pow(sideways, 0.6f) * (toward >= 0f ? 1f : 0.45f);
                reach = 0.3f + (0.7f * sideways);
                // The flash as the beam sweeps across the camera.
                var flash = DeterministicMath.Pow(MathF.Max(0f, toward), 10f);
                if (flash > 0.01f)
                {
                    output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, light.Radius * 0.6f, color, intensity * flash * 1.4f, light.Falloff, 0f, MapLightMetadata.FullSpread, null));
                }
            }

            if (strength <= 0.01f)
            {
                continue;
            }

            if (laser)
            {
                output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, 18f + (style.Width * 3f), color, intensity * 0.5f, light.Falloff, 0f, MapLightMetadata.FullSpread, null));
                beams?.Add(new MapLightBeam(sourceIndex, side, MapLightKind.Laser, light.X, light.Y, direction, light.Radius * reach, style.Width, 0f, color, color, intensity * strength, style.Haze / 100f, style.StopsAtPlayers, Damaging: false));
                continue;
            }

            output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, light.Radius * reach, color, intensity * strength, light.Falloff, direction, spread, null));
            if (style.Kind == MapLightKind.Spotlight)
            {
                beams?.Add(new MapLightBeam(sourceIndex, side, MapLightKind.Spotlight, light.X, light.Y, direction, light.Radius * reach, style.Width, spread, color, color, intensity * strength, style.Haze / 100f, style.StopsAtPlayers, Damaging: false));
            }
        }

        // The lamp itself.
        output.Add(new MapLightEmitter(sourceIndex, light.X, light.Y, MathF.Min(light.Radius, 20f), Lerp(primary, alternate, 0.5f), intensity * 0.5f, light.Falloff, 0f, MapLightMetadata.FullSpread, null));
    }

    public static MapLightingColor Lerp(MapLightingColor from, MapLightingColor to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new MapLightingColor(
            (byte)Math.Clamp((int)MathF.Round(from.R + ((to.R - from.R) * t)), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(from.G + ((to.G - from.G) * t)), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(from.B + ((to.B - from.B) * t)), 0, 255));
    }
}

/// <summary>Deterministic animation curves for lights (shared by map lights and gameplay effects).</summary>
public static class MapLightAnimation
{
    /// <summary>Deterministic 0..1 steadiness for a light, smooth enough to read as fire.</summary>
    public static float GetFlicker(MapLightFlicker flicker, double time, float seed)
    {
        return flicker switch
        {
            MapLightFlicker.Flicker => 0.78f + (0.22f * SmoothNoise((time * 9d) + seed)),
            MapLightFlicker.Pulse => 0.55f + (0.45f * (0.5f + (0.5f * DeterministicMath.Sin((float)((time * 2.2d) + seed))))),
            // Siren and jukebox shape their light elsewhere (MapLightEmission); full strength here.
            _ => 1f,
        };
    }

    /// <summary>Value noise in 0..1, smoothly interpolated between integer steps.</summary>
    public static float SmoothNoise(double x)
    {
        var cell = Math.Floor(x);
        var fraction = (float)(x - cell);
        var a = Hash01((long)cell);
        var b = Hash01((long)cell + 1);
        var t = fraction * fraction * (3f - (2f * fraction));
        return a + ((b - a) * t);
    }

    /// <summary>Integer hash to 0..1.</summary>
    public static float Hash01(long value)
    {
        unchecked
        {
            var h = (uint)value * 0x9E3779B1u;
            h ^= h >> 15;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }
}
