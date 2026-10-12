#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.ClientShared;

namespace OpenGarrison.Client;

/// <summary>
/// Builder property editor for lights: cycle buttons for the light's modes, rows shown
/// only when they matter (laser rows on lasers, music rows on jukebox lights...), readable
/// labels, a sensible row order, and wires from the logic inputs.
/// </summary>
public partial class Game1
{
    private static readonly string[] GarrisonBuilderLightPropertyOrder =
    [
        MapLightMetadata.KindKey,
        MapLightMetadata.ColorKey,
        MapLightMetadata.AltColorKey,
        MapLightMetadata.AltColor2Key,
        MapLightMetadata.ColorModeKey,
        MapLightMetadata.ColorRateKey,
        MapLightMetadata.ColorTransitionKey,
        MapLightMetadata.ColorTransitionTimeKey,
        MapLightMetadata.IntensityKey,
        MapLightMetadata.RadiusKey,
        MapLightMetadata.FlickerKey,
        MapLightMetadata.SirenStyleKey,
        MapLightMetadata.ReactKey,
        MapLightMetadata.ReactToKey,
        MapLightMetadata.SensitivityKey,
        MapLightMetadata.FloorKey,
        MapLightMetadata.DownTimeKey,
        MapLightMetadata.DirectionKey,
        MapLightMetadata.SpreadKey,
        MapLightMetadata.MotionKey,
        MapLightMetadata.ArcKey,
        MapLightMetadata.SpeedKey,
        MapLightMetadata.PhaseKey,
        MapLightMetadata.ChaseStepsKey,
        MapLightMetadata.MirrorKey,
        MapLightMetadata.MusicSweepKey,
        MapLightMetadata.BeamsKey,
        MapLightMetadata.FanSpreadKey,
        MapLightMetadata.WidthKey,
        MapLightMetadata.HazeKey,
        MapLightMetadata.FalloffKey,
        MapLightMetadata.MaskKey,
        GarrisonBuilderLightPaintMaskKey,
        MapLightMetadata.MaskGlowRadiusKey,
        DestructiblePropMetadata.ZoneKey,
        MapLightMetadata.StartOnKey,
        MapLightMetadata.TransitionKey,
        MapLightMetadata.FadeTimeKey,
        MapLightMetadata.FollowInputKey,
        MapLightMetadata.OnInputKey,
        MapLightMetadata.OffInputKey,
        MapLightMetadata.ToggleInputKey,
        MapLightMetadata.ColorInputKey,
        MapLightMetadata.TertiaryColorInputKey,
        MapLightMetadata.ColorActionKey,
        MapLightMetadata.StopsAtPlayersKey,
        MapLightMetadata.DamageKey,
        MapLightMetadata.DamagePerSecondKey,
        MapLightMetadata.BurnKey,
        MapLightMetadata.DamageTeamKey,
    ];

    private bool IsEditingGarrisonBuilderLightEntity() =>
        _builderPropertyTarget != GarrisonBuilderPropertyTarget.MapProperties
        && TryGetGarrisonBuilderEditedEntityType(out var entityType)
        && MapLightMetadata.IsLightEntityType(entityType);

    /// <summary>Cycle-button rows of a light. Returns true when the click was handled.</summary>
    private bool TryCycleGarrisonBuilderLightProperty(string key, string value)
    {
        if (!IsEditingGarrisonBuilderLightEntity())
        {
            return false;
        }

        if (key.Equals(GarrisonBuilderLightPresetKey, StringComparison.Ordinal))
        {
            ApplyNextGarrisonBuilderLightPreset();
            return true;
        }

        if (key == GarrisonBuilderLightPaintMaskKey)
        {
            BeginGarrisonBuilderLightMaskPainting();
            return true;
        }

        string next;
        if (key.Equals(MapLightMetadata.FlickerKey, StringComparison.OrdinalIgnoreCase))
        {
            var style = MapLightStyleMetadata.Parse(_builderPropertyEditorValues);
            next = MapLightMetadata.CycleFlickerValue(value, style);
            AdjustGarrisonBuilderSirenSpeed(MapLightMetadata.ParseFlicker(value), MapLightMetadata.ParseFlicker(next));
        }
        else if (key.Equals(MapLightMetadata.KindKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleGarrisonBuilderLightEnum<MapLightKind>(value, MapLightStyleMetadata.ParseKind, MapLightStyleMetadata.ToKindValue);
        }
        else if (key.Equals(MapLightMetadata.ColorModeKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleGarrisonBuilderLightEnum<MapLightColorMode>(value, MapLightStyleMetadata.ParseColorMode, MapLightStyleMetadata.ToColorModeValue);
        }
        else if (key.Equals(MapLightMetadata.MotionKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleGarrisonBuilderLightEnum<MapLightMotion>(value, MapLightStyleMetadata.ParseMotion, MapLightStyleMetadata.ToMotionValue);
        }
        else if (key.Equals(MapLightMetadata.SirenStyleKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleGarrisonBuilderLightEnum<MapLightSirenStyle>(value, MapLightStyleMetadata.ParseSirenStyle, MapLightStyleMetadata.ToSirenStyleValue);
        }
        else if (key.Equals(MapLightMetadata.ReactKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleGarrisonBuilderLightEnum<MapLightReact>(value, MapLightStyleMetadata.ParseReact, MapLightStyleMetadata.ToReactValue);
        }
        else if (key.Equals(MapLightMetadata.ReactToKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleGarrisonBuilderLightEnum<MapLightReactTarget>(value, MapLightStyleMetadata.ParseReactTarget, MapLightStyleMetadata.ToReactTargetValue);
        }
        else if (key.Equals(MapLightMetadata.ColorActionKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleGarrisonBuilderLightEnum<MapLightColorAction>(value, MapLightStyleMetadata.ParseColorAction, MapLightStyleMetadata.ToColorActionValue);
        }
        else if (key.Equals(MapLightMetadata.TransitionKey, StringComparison.OrdinalIgnoreCase)
            || key.Equals(MapLightMetadata.ColorTransitionKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleGarrisonBuilderLightEnum<MapLightTransition>(value, MapLightStyleMetadata.ParseTransition, MapLightStyleMetadata.ToTransitionValue);
        }
        else if (key.Equals(MapLightMetadata.DamageTeamKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleGarrisonBuilderLightEnum<MapLightDamageTeam>(value, MapLightStyleMetadata.ParseDamageTeam, MapLightStyleMetadata.ToDamageTeamValue);
        }
        else if (key.Equals(MapLightMetadata.DownTimeKey, StringComparison.OrdinalIgnoreCase))
        {
            next = CycleGarrisonBuilderLightEnum<MapLightDownTime>(value, MapLightStyleMetadata.ParseDownTime, MapLightStyleMetadata.ToDownTimeValue);
        }
        else
        {
            return false;
        }

        _builderPropertyEditorValues[key] = next;
        ApplyGarrisonBuilderPropertyEditorLivePreview();
        MarkGarrisonBuilderPropertyEditorChanged();
        if (MapLightStyleMetadata.Parse(_builderPropertyEditorValues).IsDamagingLaser
            && (key.Equals(MapLightMetadata.KindKey, StringComparison.OrdinalIgnoreCase) || key.Equals(MapLightMetadata.FlickerKey, StringComparison.OrdinalIgnoreCase)))
        {
            _builderStatus = "damage enabled: motion and siren are disabled for this laser";
        }

        return true;
    }

    /// <summary>
    /// A siren at the default sweep speed would turn far too slowly: switching to siren
    /// bumps it to a beacon's turn rate, and switching away puts the default back.
    /// </summary>
    private void AdjustGarrisonBuilderSirenSpeed(MapLightFlicker from, MapLightFlicker to)
    {
        _builderPropertyEditorValues.TryGetValue(MapLightMetadata.SpeedKey, out var rawSpeed);
        var speed = float.TryParse(rawSpeed?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : MapLightMetadata.DefaultSpeed;
        if (to == MapLightFlicker.Siren && MathF.Abs(speed - MapLightMetadata.DefaultSpeed) < 0.01f)
        {
            _builderPropertyEditorValues[MapLightMetadata.SpeedKey] = MapLightMetadata.DefaultSirenSpeed.ToString(CultureInfo.InvariantCulture);
        }
        else if (from == MapLightFlicker.Siren && to != MapLightFlicker.Siren && MathF.Abs(speed - MapLightMetadata.DefaultSirenSpeed) < 0.01f)
        {
            _builderPropertyEditorValues[MapLightMetadata.SpeedKey] = MapLightMetadata.DefaultSpeed.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string CycleGarrisonBuilderLightEnum<T>(string value, Func<string?, T> parse, Func<T, string> format)
        where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        var index = Array.IndexOf(values, parse(value));
        return format(values[(index + 1) % values.Length]);
    }

    /// <summary>Hides light rows that do nothing with the light's current settings.</summary>
    private bool ShouldSkipGarrisonBuilderLightPropertyRow(string key)
    {
        if (!IsEditingGarrisonBuilderLightEntity())
        {
            return false;
        }

        var values = _builderPropertyEditorValues;
        var style = MapLightStyleMetadata.Parse(values);
        values.TryGetValue(MapLightMetadata.FlickerKey, out var rawFlicker);
        var flicker = MapLightMetadata.GetEffectiveFlicker(MapLightMetadata.ParseFlicker(rawFlicker), style);
        var siren = flicker == MapLightFlicker.Siren;
        var jukebox = flicker == MapLightFlicker.Jukebox;
        var laser = style.Kind == MapLightKind.Laser;
        var lamp = style.Kind == MapLightKind.Lamp;
        var roundLamp = lamp && !MapLightMetadata.IsDirectional(MapLightMetadata.FromProperties(0f, 0f, values).Spread);
        var motionShown = !style.IsDamagingLaser && !siren && !roundLamp;
        var moving = motionShown && style.Motion != MapLightMotion.None;
        var usesMusic = jukebox || style.FollowsBeat;
        var musicSweep = moving && style.MusicSweep && style.Motion != MapLightMotion.Spin;

        bool Is(string candidate) => key.Equals(candidate, StringComparison.OrdinalIgnoreCase);

        if (Is(MapLightMetadata.WidthKey))
        {
            return lamp;
        }

        if (Is(MapLightMetadata.BeamsKey))
        {
            return !laser;
        }

        if (Is(MapLightMetadata.FanSpreadKey))
        {
            return !laser || style.Beams <= 1;
        }

        if (Is(MapLightMetadata.HazeKey))
        {
            return lamp;
        }

        if (Is(MapLightMetadata.StopsAtPlayersKey) || Is(MapLightMetadata.DamageKey))
        {
            return !laser;
        }

        if (Is(MapLightMetadata.DamagePerSecondKey) || Is(MapLightMetadata.BurnKey) || Is(MapLightMetadata.DamageTeamKey))
        {
            return !style.IsDamagingLaser;
        }

        if (Is(MapLightMetadata.MotionKey))
        {
            return !motionShown;
        }

        if (Is(MapLightMetadata.ArcKey))
        {
            return !moving || style.Motion == MapLightMotion.Spin;
        }

        if (Is(MapLightMetadata.SpeedKey))
        {
            // A beat chase moves with the beat, not at a set speed.
            return !siren && (!moving || style.Motion == MapLightMotion.Beat);
        }

        if (Is(MapLightMetadata.PhaseKey))
        {
            return !siren && !moving;
        }

        if (Is(MapLightMetadata.ChaseStepsKey))
        {
            return !moving || style.Motion != MapLightMotion.Beat;
        }

        if (Is(MapLightMetadata.MirrorKey))
        {
            return !moving && !siren;
        }

        if (Is(MapLightMetadata.MusicSweepKey))
        {
            return !moving || style.Motion == MapLightMotion.Spin || !usesMusic;
        }

        if (Is(MapLightMetadata.DownTimeKey))
        {
            return !usesMusic;
        }

        if (Is(MapLightMetadata.TriggerColorKey)) return true;

        if (Is(MapLightMetadata.SirenStyleKey))
        {
            return !siren;
        }

        if (Is(MapLightMetadata.ReactKey) || Is(MapLightMetadata.SensitivityKey))
        {
            return !jukebox && !(musicSweep && usesMusic);
        }

        if (Is(MapLightMetadata.FloorKey))
        {
            return !jukebox && !(usesMusic && style.DownTime == MapLightDownTime.Dim);
        }

        if (Is(MapLightMetadata.ReactToKey))
        {
            return !jukebox || style.IsDamagingLaser;
        }

        if (Is(MapLightMetadata.ColorModeKey))
        {
            return style.AltColor is null && style.AltColor2 is null;
        }

        if (Is(MapLightMetadata.ColorRateKey))
        {
            return (style.AltColor is null && style.AltColor2 is null) || style.ColorMode is not (MapLightColorMode.Alternate or MapLightColorMode.Fade);
        }

        if (Is(MapLightMetadata.MaskPixelSizeKey)) return true;
        if (Is(BuilderPaintedLightMask.ResourceKey) || Is(BuilderPaintedLightMask.PixelSizeKey) || Is(BuilderPaintedLightMask.BrushSizeKey)) return true;
        if (key == GarrisonBuilderLightPaintMaskKey) return !lamp || siren;
        if (Is(MapLightMetadata.MaskGlowRadiusKey)) return laser || siren;

        if (Is(MapLightMetadata.ColorActionKey))
        {
            return style.ColorInput.Length == 0;
        }

        if (Is(MapLightMetadata.FadeTimeKey))
        {
            return style.Transition == MapLightTransition.Instant;
        }

        if (Is(MapLightMetadata.ColorTransitionTimeKey))
        {
            return style.ColorTransition == MapLightTransition.Instant;
        }

        return false;
    }

    /// <summary>Readable labels for light rows; null leaves the row to the generic label.</summary>
    private string? FormatGarrisonBuilderLightPropertyRowLabel(string key, string value)
    {
        if (!IsEditingGarrisonBuilderLightEntity() || MapLightMetadata.IsLogicInputKey(key))
        {
            return null;
        }

        if (key.Equals(GarrisonBuilderLightPresetKey, StringComparison.Ordinal))
        {
            return FormatGarrisonBuilderLightPresetLabel();
        }

        if (key == GarrisonBuilderLightPaintMaskKey) return "Paint mask...";
        if (key.Equals(MapLightMetadata.MaskGlowRadiusKey, StringComparison.OrdinalIgnoreCase))
            return $"Outside mask glow (px, 0 = off): {value}";

        var style = MapLightStyleMetadata.Parse(_builderPropertyEditorValues);
        bool Is(string candidate) => key.Equals(candidate, StringComparison.OrdinalIgnoreCase);
        var text = string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();

        if (Is(MapLightMetadata.KindKey))
        {
            return $"Type: {GetLightKindLabel(MapLightStyleMetadata.ParseKind(value))}";
        }

        if (Is(MapLightMetadata.FlickerKey))
        {
            var chosen = MapLightMetadata.ParseFlicker(value);
            var effective = MapLightMetadata.GetEffectiveFlicker(chosen, style);
            return effective == chosen
                ? $"Steadiness: {GetLightFlickerLabel(chosen)}"
                : $"Steadiness: {GetLightFlickerLabel(effective)} (siren unavailable with damage)";
        }

        if (Is(MapLightMetadata.ColorKey))
        {
            return $"Primary colour: {FormatGarrisonBuilderLightColorValue(value, "None")}";
        }

        if (Is(MapLightMetadata.AltColorKey))
        {
            return $"Secondary colour: {FormatGarrisonBuilderLightColorValue(value, "None")}";
        }

        if (Is(MapLightMetadata.AltColor2Key))
        {
            return $"Tertiary colour: {FormatGarrisonBuilderLightColorValue(value, "None")}";
        }

        if (Is(MapLightMetadata.ChaseStepsKey))
        {
            return $"Chase steps: {text}";
        }

        if (Is(MapLightMetadata.MirrorKey))
        {
            return "Mirrored";
        }

        if (Is(MapLightMetadata.MusicSweepKey))
        {
            return "Audio-reactive sweep width";
        }

        if (Is(MapLightMetadata.DownTimeKey))
        {
            return $"During breakdowns: {GetLightDownTimeLabel(MapLightStyleMetadata.ParseDownTime(value))}";
        }

        if (Is(MapLightMetadata.ColorModeKey))
        {
            return $"Colour mode: {GetLightColorModeLabel(MapLightStyleMetadata.ParseColorMode(value))}";
        }

        if (Is(MapLightMetadata.ColorRateKey))
        {
            return $"Colour rate (per sec): {text}";
        }

        if (Is(MapLightMetadata.MotionKey))
        {
            return $"Motion: {GetLightMotionLabel(MapLightStyleMetadata.ParseMotion(value))}";
        }

        if (Is(MapLightMetadata.ArcKey))
        {
            return $"Sweep arc (deg): {text}";
        }

        if (Is(MapLightMetadata.SpeedKey))
        {
            _builderPropertyEditorValues.TryGetValue(MapLightMetadata.FlickerKey, out var rawFlicker);
            var siren = MapLightMetadata.ParseFlicker(rawFlicker) == MapLightFlicker.Siren;
            return siren || style.Motion == MapLightMotion.Spin
                ? $"Rotation speed (rpm): {text}"
                : $"Speed (cycles/min): {text}";
        }

        if (Is(MapLightMetadata.PhaseKey))
        {
            return $"Phase (deg): {text}";
        }

        if (Is(MapLightMetadata.BeamsKey))
        {
            return $"Beams: {text}";
        }

        if (Is(MapLightMetadata.FanSpreadKey))
        {
            return $"Fan spread (deg): {text}";
        }

        if (Is(MapLightMetadata.WidthKey))
        {
            return $"Beam width (px): {text}";
        }

        if (Is(MapLightMetadata.HazeKey))
        {
            return $"Beam haze (%): {text}";
        }

        if (Is(MapLightMetadata.SirenStyleKey))
        {
            return $"Siren style: {(MapLightStyleMetadata.ParseSirenStyle(value) == MapLightSirenStyle.Beacon ? "Beacon" : "Rotate")}";
        }

        if (Is(MapLightMetadata.ReactKey))
        {
            return $"Audio source: {GetLightReactLabel(MapLightStyleMetadata.ParseReact(value))}";
        }

        if (Is(MapLightMetadata.ReactToKey))
        {
            return $"Audio controls: {GetLightReactTargetLabel(MapLightStyleMetadata.ParseReactTarget(value))}";
        }

        if (Is(MapLightMetadata.SensitivityKey))
        {
            return $"Sensitivity (%): {text}";
        }

        if (Is(MapLightMetadata.FloorKey))
        {
            return $"Minimum brightness (%): {text}";
        }

        if (Is(MapLightMetadata.StartOnKey))
        {
            return "Initially on";
        }

        if (Is(MapLightMetadata.TransitionKey))
        {
            return $"Power transition: {GetLightTransitionLabel(MapLightStyleMetadata.ParseTransition(value))}";
        }

        if (Is(MapLightMetadata.FadeTimeKey))
        {
            return $"Transition time (sec): {text}";
        }

        if (Is(MapLightMetadata.ColorTransitionKey))
        {
            return $"Colour transition: {GetLightTransitionLabel(MapLightStyleMetadata.ParseTransition(value))}";
        }

        if (Is(MapLightMetadata.ColorTransitionTimeKey))
        {
            return $"Colour transition time (sec): {text}";
        }

        if (Is(MapLightMetadata.ColorActionKey))
        {
            return $"Colour trigger action: {GetLightColorActionLabel(MapLightStyleMetadata.ParseColorAction(value))}";
        }


        if (Is(MapLightMetadata.StopsAtPlayersKey))
        {
            return "Blocked by players";
        }

        if (Is(MapLightMetadata.DamageKey))
        {
            return "Damage";
        }

        if (Is(MapLightMetadata.DamagePerSecondKey))
        {
            return $"Damage per second: {text}";
        }

        if (Is(MapLightMetadata.BurnKey))
        {
            return $"Afterburn strength (%): {text}";
        }

        if (Is(MapLightMetadata.DamageTeamKey))
        {
            var hurts = MapLightStyleMetadata.ParseDamageTeam(value) switch
            {
                MapLightDamageTeam.Red => "Red",
                MapLightDamageTeam.Blue => "Blue",
                _ => "All",
            };
            return $"Damaged team: {hurts}";
        }

        return null;
    }

    /// <summary>Light rows in a sensible order (type and colour first, logic and hazard last).</summary>
    private List<string> OrderGarrisonBuilderLightPropertyRows(List<string> rows)
    {
        // The preset row (not a saved property) sits on top.
        var ordered = new List<string>(rows.Count + 2) { GarrisonBuilderLightPresetKey };
        foreach (var key in GarrisonBuilderLightPropertyOrder)
        {
            if (key == GarrisonBuilderLightPaintMaskKey)
            {
                if (!ShouldSkipGarrisonBuilderLightPropertyRow(key)) ordered.Add(key);
                continue;
            }
            var match = rows.FirstOrDefault(row => row.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ordered.Add(match);
            }
        }

        foreach (var row in rows)
        {
            if (!ordered.Contains(row, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(row);
            }
        }

        return ordered;
    }

    /// <summary>Wires from the logic nodes a light listens to.</summary>
    private void DrawGarrisonBuilderLightInputLinks(CustomMapBuilderEntity entity)
    {
        var inputs = new List<string>();
        foreach (var key in MapLightMetadata.LogicInputKeys)
        {
            var logicRef = GetEntityProperty(entity.Properties, key, string.Empty);
            if (!string.IsNullOrWhiteSpace(logicRef))
            {
                inputs.Add(logicRef);
            }
        }

        for (var index = 0; index < inputs.Count; index += 1)
        {
            DrawGarrisonBuilderLogicConsumerLink(entity, inputs[index], index, inputs.Count);
        }
    }

    /// <summary>A colour row's value as #RRGGBB, or the fallback when unset.</summary>
    private static string FormatGarrisonBuilderLightColorValue(string value, string fallback) =>
        MapLightingColor.TryParse(value, out var color) ? $"#{color.ToHex().ToUpperInvariant()}" : fallback;

    private static string GetLightKindLabel(MapLightKind kind) => kind switch
    {
        MapLightKind.Spotlight => "Searchlight",
        MapLightKind.Laser => "Laser",
        _ => "Lamp",
    };

    private static string GetLightFlickerLabel(MapLightFlicker flicker) => flicker switch
    {
        MapLightFlicker.Flicker => "Flicker",
        MapLightFlicker.Pulse => "Pulse",
        MapLightFlicker.Siren => "Siren",
        MapLightFlicker.Jukebox => "Jukebox",
        _ => "Steady",
    };

    private static string GetLightColorModeLabel(MapLightColorMode mode) => mode switch
    {
        MapLightColorMode.Alternate => "Alternate",
        MapLightColorMode.Fade => "Fade",
        MapLightColorMode.Gradient => "Gradient",
        MapLightColorMode.Split => "Split",
        MapLightColorMode.Beat => "Beat sync",
        _ => "Single",
    };

    private static string GetLightMotionLabel(MapLightMotion motion) => motion switch
    {
        MapLightMotion.Sweep => "Sweep",
        MapLightMotion.Bounce => "Bounce",
        MapLightMotion.Spin => "Rotate",
        MapLightMotion.Wander => "Wander",
        MapLightMotion.Scan => "Fan scan",
        MapLightMotion.Beat => "Beat chase",
        _ => "None",
    };

    private static string GetLightReactLabel(MapLightReact react) => react switch
    {
        MapLightReact.Bass => "Bass",
        MapLightReact.Mids => "Mids",
        MapLightReact.Highs => "Highs",
        MapLightReact.Beat => "Beat",
        _ => "Level",
    };

    private static string GetLightReactTargetLabel(MapLightReactTarget target) => target switch
    {
        MapLightReactTarget.Speed => "Speed",
        MapLightReactTarget.Both => "Brightness + speed",
        _ => "Brightness",
    };

    private static string GetLightDownTimeLabel(MapLightDownTime downTime) => downTime switch
    {
        MapLightDownTime.Dim => "Dim",
        MapLightDownTime.Off => "Off",
        MapLightDownTime.Drift => "Drift",
        _ => "No change",
    };

    private static string GetLightTransitionLabel(MapLightTransition transition) => transition switch
    {
        MapLightTransition.Fade => "Fade",
        MapLightTransition.Warmup => "Warm-up",
        MapLightTransition.Blink => "Blink",
        _ => "Instant",
    };

    private static string GetLightColorActionLabel(MapLightColorAction action) => action switch
    {
        MapLightColorAction.Swap => "Swap on pulse",
        MapLightColorAction.Cycle => "Cycle on pulse",
        MapLightColorAction.Trigger => "Hold tertiary colour",
        _ => "Hold secondary",
    };
}
