using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

/// <summary>Phase 0 of the lighting plan: extended light settings and the shared evaluation path.</summary>
public sealed class MapLightStyleTests
{
    private static MapLightMarker Parse(Dictionary<string, string> properties) =>
        MapLightMetadata.FromProperties(10f, 20f, properties);

    [Fact]
    public void LightsWithoutExtendedSettingsAreTheOriginalLamp()
    {
        var light = Parse(new Dictionary<string, string>());
        Assert.Same(MapLightStyle.Default, light.Style);
        Assert.Equal(MapLightKind.Lamp, light.Style.Kind);
        Assert.Null(light.Style.AltColor);
        Assert.Equal(MapLightColorMode.Solid, light.Style.ColorMode);
        Assert.Equal(MapLightMotion.None, light.Style.Motion);
        Assert.True(light.Style.StartOn);
        Assert.False(light.Style.Damage);
        Assert.False(light.Style.HasLogicInputs);

        // New builder lights default to secondary hold, with a separate tertiary input.
        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition(MapLightMetadata.EntityType, out var definition));
        var placed = definition.CreateEntity(10f, 20f);
        Assert.Equal(MapLightStyle.Default,
            MapLightMetadata.FromProperties(placed.X, placed.Y, placed.Properties).Style);

        // A marker built positionally (older code paths) equals a parsed one.
        var positional = new MapLightMarker(10f, 20f, MapLightMetadata.DefaultColor, MapLightMetadata.DefaultRadius, MapLightMetadata.DefaultIntensity, MapLightFlicker.Steady);
        Assert.Equal(positional, light);
        Assert.Same(MapLightStyle.Default, default(MapLightMarker).Style);
    }

    [Fact]
    public void ExtendedSettingsParseClampAndFallBack()
    {
        var style = Parse(new Dictionary<string, string>
        {
            [MapLightMetadata.KindKey] = "Laser",
            [MapLightMetadata.AltColorKey] = "#00ff80",
            [MapLightMetadata.ColorModeKey] = "split",
            [MapLightMetadata.ColorRateKey] = "999",
            [MapLightMetadata.MotionKey] = "sweep",
            [MapLightMetadata.ArcKey] = "-5",
            [MapLightMetadata.SpeedKey] = "30",
            [MapLightMetadata.PhaseKey] = "-90",
            [MapLightMetadata.BeamsKey] = "40",
            [MapLightMetadata.FanSpreadKey] = "45",
            [MapLightMetadata.WidthKey] = "0",
            [MapLightMetadata.HazeKey] = "250",
            [MapLightMetadata.SirenStyleKey] = "spin",
            [MapLightMetadata.ReactKey] = "bass",
            [MapLightMetadata.ReactToKey] = "both",
            [MapLightMetadata.SensitivityKey] = "150",
            [MapLightMetadata.FloorKey] = "nope",
            [MapLightMetadata.StartOnKey] = "false",
            [MapLightMetadata.OnInputKey] = "  logic:on  ",
            [MapLightMetadata.ColorInputKey] = "logic:colour",
            [MapLightMetadata.ColorActionKey] = "cycle",
            [MapLightMetadata.TriggerColorKey] = "bad",
            [MapLightMetadata.TransitionKey] = "warmup",
            [MapLightMetadata.FadeTimeKey] = "2.5",
            [MapLightMetadata.ColorTransitionKey] = "BLINK",
            [MapLightMetadata.ColorTransitionTimeKey] = "9999",
            [MapLightMetadata.StopsAtPlayersKey] = "true",
            [MapLightMetadata.DamagePerSecondKey] = "9999",
            [MapLightMetadata.BurnKey] = "40",
            [MapLightMetadata.DamageTeamKey] = "BLUE",
        }).Style;

        Assert.Equal(MapLightKind.Laser, style.Kind);
        Assert.Equal(new MapLightingColor(0, 255, 128), style.AltColor);
        Assert.Equal(MapLightColorMode.Split, style.ColorMode);
        Assert.Equal(MapLightMetadata.MaxColorRate, style.ColorRate);
        Assert.Equal(MapLightMotion.Sweep, style.Motion);
        Assert.Equal(0f, style.Arc);
        Assert.Equal(30f, style.Speed);
        Assert.Equal(270f, style.Phase);
        Assert.Equal(MapLightMetadata.MaxBeams, style.Beams);
        Assert.Equal(45f, style.FanSpread);
        Assert.Equal(MapLightMetadata.MinWidth, style.Width);
        Assert.Equal(100, style.Haze);
        Assert.Equal(MapLightSirenStyle.Spin, style.SirenStyle);
        Assert.Equal(MapLightReact.Bass, style.React);
        Assert.Equal(MapLightReactTarget.Both, style.ReactTarget);
        Assert.Equal(150, style.Sensitivity);
        Assert.Equal(MapLightMetadata.DefaultFloor, style.Floor);
        Assert.False(style.StartOn);
        Assert.Equal("logic:on", style.OnInput);
        Assert.Equal("logic:colour", style.ColorInput);
        Assert.True(style.HasLogicInputs);
        Assert.Equal(MapLightColorAction.Cycle, style.ColorAction);
        Assert.Null(style.TriggerColor);
        Assert.Equal(MapLightTransition.Warmup, style.Transition);
        Assert.Equal(2.5f, style.FadeTime);
        Assert.Equal(MapLightTransition.Blink, style.ColorTransition);
        Assert.Equal(MapLightMetadata.MaxFadeTime, style.ColorTransitionTime);
        Assert.True(style.StopsAtPlayers);
        Assert.False(style.Damage);
        Assert.Equal(MapLightMetadata.MaxDamagePerSecond, style.DamagePerSecond);
        Assert.Equal(40, style.Burn);
        Assert.Equal(MapLightDamageTeam.Blue, style.DamageTeam);

        // Unknown words fall back to the first (default) value.
        Assert.Equal(MapLightKind.Lamp, MapLightStyleMetadata.ParseKind("disco ball"));
        Assert.Equal(MapLightSirenStyle.Beacon, MapLightStyleMetadata.ParseSirenStyle(null));
    }

    [Fact]
    public void EnumValuesRoundTripThroughTheirSavedText()
    {
        foreach (var value in Enum.GetValues<MapLightKind>())
        {
            Assert.Equal(value, MapLightStyleMetadata.ParseKind(MapLightStyleMetadata.ToKindValue(value)));
        }

        foreach (var value in Enum.GetValues<MapLightColorMode>())
        {
            Assert.Equal(value, MapLightStyleMetadata.ParseColorMode(MapLightStyleMetadata.ToColorModeValue(value)));
        }

        foreach (var value in Enum.GetValues<MapLightMotion>())
        {
            Assert.Equal(value, MapLightStyleMetadata.ParseMotion(MapLightStyleMetadata.ToMotionValue(value)));
        }

        foreach (var value in Enum.GetValues<MapLightTransition>())
        {
            Assert.Equal(value, MapLightStyleMetadata.ParseTransition(MapLightStyleMetadata.ToTransitionValue(value)));
        }

        foreach (var value in Enum.GetValues<MapLightColorAction>())
        {
            Assert.Equal(value, MapLightStyleMetadata.ParseColorAction(MapLightStyleMetadata.ToColorActionValue(value)));
        }

        foreach (var value in Enum.GetValues<MapLightReact>())
        {
            Assert.Equal(value, MapLightStyleMetadata.ParseReact(MapLightStyleMetadata.ToReactValue(value)));
        }

        foreach (var value in Enum.GetValues<MapLightFlicker>())
        {
            Assert.Equal(value, MapLightMetadata.ParseFlicker(MapLightMetadata.ToFlickerValue(value)));
        }
    }

    [Fact]
    public void SteadinessCyclesThroughSirenAndJukebox()
    {
        Assert.Equal(MapLightFlicker.Siren, Parse(new() { ["flicker"] = "Siren" }).Flicker);
        Assert.Equal(MapLightFlicker.Jukebox, Parse(new() { ["flicker"] = "jukebox" }).Flicker);

        Assert.Equal("flicker", MapLightMetadata.CycleFlickerValue("steady"));
        Assert.Equal("pulse", MapLightMetadata.CycleFlickerValue("flicker"));
        Assert.Equal("siren", MapLightMetadata.CycleFlickerValue("pulse"));
        Assert.Equal("jukebox", MapLightMetadata.CycleFlickerValue("siren"));
        Assert.Equal("steady", MapLightMetadata.CycleFlickerValue("jukebox"));

        // A damaging laser skips siren (it would move the beam).
        var damaging = Parse(new() { [MapLightMetadata.KindKey] = "laser", [MapLightMetadata.DamageKey] = "true" }).Style;
        Assert.Equal("jukebox", MapLightMetadata.CycleFlickerValue("pulse", damaging));

        // Their brightness curve is shaped by the emission, not the flicker curve.
        Assert.Equal(1f, MapLightAnimation.GetFlicker(MapLightFlicker.Siren, 3.7d, 1f));
        Assert.Equal(1f, MapLightAnimation.GetFlicker(MapLightFlicker.Jukebox, 3.7d, 1f));
    }

    [Fact]
    public void DamagingLasersStayFixed()
    {
        var moving = new Dictionary<string, string>
        {
            [MapLightMetadata.KindKey] = "laser",
            [MapLightMetadata.MotionKey] = "spin",
            [MapLightMetadata.ReactToKey] = "speed",
            ["flicker"] = "siren",
        };
        var harmless = Parse(moving);
        Assert.False(harmless.Style.IsDamagingLaser);
        Assert.Equal(MapLightMotion.Spin, harmless.Style.EffectiveMotion);
        Assert.True(harmless.Style.MusicDrivesSpeed);
        Assert.Equal(MapLightFlicker.Siren, MapLightMetadata.GetEffectiveFlicker(harmless.Flicker, harmless.Style));

        var damaging = Parse(new Dictionary<string, string>(moving) { [MapLightMetadata.DamageKey] = "true" });
        Assert.True(damaging.Style.IsDamagingLaser);
        Assert.Equal(MapLightMotion.None, damaging.Style.EffectiveMotion);
        Assert.False(damaging.Style.MusicDrivesSpeed);
        Assert.Equal(MapLightFlicker.Steady, MapLightMetadata.GetEffectiveFlicker(damaging.Flicker, damaging.Style));

        // Damage only means something on lasers.
        var lamp = Parse(new Dictionary<string, string> { [MapLightMetadata.DamageKey] = "true", [MapLightMetadata.MotionKey] = "sweep" });
        Assert.False(lamp.Style.IsDamagingLaser);
        Assert.Equal(MapLightMotion.Sweep, lamp.Style.EffectiveMotion);
    }

    [Fact]
    public void ScalingAMapScalesBeamWidth()
    {
        var light = Parse(new Dictionary<string, string> { [MapLightMetadata.WidthKey] = "6", ["radius"] = "100" });
        var scaled = light.Scale(2f);
        Assert.Equal(20f, scaled.X);
        Assert.Equal(200f, scaled.Radius);
        Assert.Equal(12f, scaled.Style.Width);

        // Width scales like every other size, even on a default light.
        Assert.Equal(MapLightMetadata.DefaultWidth * 2f, Parse(new()).Scale(2f).Style.Width);
    }

    [Fact]
    public void EmissionMatchesTheOriginalLampMath()
    {
        var lights = new[]
        {
            Parse(new() { ["color"] = "ff8000", ["intensity"] = "150", ["flicker"] = "flicker", ["direction"] = "45", ["spread"] = "60", ["falloff"] = "retro" }),
            Parse(new() { ["flicker"] = "pulse", ["mask"] = "shape.png" }),
            Parse(new() { ["intensity"] = "0" }),
        };

        var emitters = new List<MapLightEmitter>();
        foreach (var time in new[] { 0d, 0.37d, 12.5d, 1234.567d })
        {
            emitters.Clear();
            MapLightEmission.EvaluateAll(lights, time, emitters);
            Assert.Equal(lights.Length, emitters.Count);
            for (var index = 0; index < lights.Length; index += 1)
            {
                var light = lights[index];
                var emitter = emitters[index];
                // The exact expression the renderer used before Phase 0.
                var expected = light.Intensity / 100f * MapLightAnimation.GetFlicker(light.Flicker, time, index * 7.31f);
                Assert.Equal(expected, emitter.Intensity);
                Assert.Equal(index, emitter.SourceIndex);
                Assert.Equal(light.X, emitter.X);
                Assert.Equal(light.Y, emitter.Y);
                Assert.Equal(light.Radius, emitter.Radius);
                Assert.Equal(light.Color, emitter.Color);
                Assert.Equal(light.Falloff, emitter.Falloff);
                Assert.Equal(light.Direction, emitter.Direction);
                Assert.Equal(light.Spread, emitter.Spread);
                Assert.Equal(light.Mask, emitter.Mask);
            }
        }
    }

    [Fact]
    public void FlickerCurvesAreUnchanged()
    {
        // Spot values of the original formulas (flicker 0.78..1, pulse 0.55..1).
        for (var time = 0d; time < 20d; time += 0.173d)
        {
            var flicker = MapLightAnimation.GetFlicker(MapLightFlicker.Flicker, time, 3f);
            Assert.InRange(flicker, 0.78f, 1f);
            var pulse = MapLightAnimation.GetFlicker(MapLightFlicker.Pulse, time, 3f);
            var expected = 0.55f + (0.45f * (0.5f + (0.5f * (float)Math.Sin((time * 2.2d) + 3f))));
            Assert.InRange(MathF.Abs(expected - pulse), 0f, 0.00001f);
        }

        Assert.Equal(1f, MapLightAnimation.GetFlicker(MapLightFlicker.Steady, 5d, 0f));
    }

    private static (List<MapLightEmitter> Emitters, List<MapLightBeam> Beams) Emit(
        MapLightMarker light,
        double time,
        MapLightRuntimeSample? runtime = null,
        MapLightMusic music = default)
    {
        var emitters = new List<MapLightEmitter>();
        var beams = new List<MapLightBeam>();
        MapLightEmission.Evaluate(light, 0, time, runtime ?? MapLightRuntimeSample.On, music, emitters, beams);
        return (emitters, beams);
    }

    [Fact]
    public void LogicPowerTurnsLightsOffAndTransitionsShapeThem()
    {
        var light = Parse(new() { [MapLightMetadata.TransitionKey] = "fade" });
        Assert.Empty(Emit(light, 1d, MapLightRuntimeSample.Off).Emitters);
        Assert.Single(Emit(light, 1d, MapLightRuntimeSample.On).Emitters);

        var halfway = new MapLightRuntimeSample(true, 0.5f, MapLightColorState.Normal);
        Assert.Equal(0.5f, MapLightEmission.GetPowerLevel(light.Style, halfway, 0d, 0), 3);
        var blink = Parse(new() { [MapLightMetadata.TransitionKey] = "blink" }).Style;
        var level = MapLightEmission.GetPowerLevel(blink, halfway, 0d, 0);
        Assert.True(level is 0f or 1f);
    }

    [Fact]
    public void RuntimeStateStartsFromStartOnAndFades()
    {
        var lights = new[]
        {
            Parse(new() { [MapLightMetadata.StartOnKey] = "false", [MapLightMetadata.TransitionKey] = "fade", [MapLightMetadata.FadeTimeKey] = "1" }),
            Parse(new()),
        };
        var state = new MapLightRuntimeState();
        state.Bind(lights, MapLogicGraph.Empty);
        Assert.False(state.IsPowered(0, lights[0].Style));
        Assert.True(state.IsPowered(1, lights[1].Style));
        Assert.False(state.HasLogic);
        Assert.Equal(0f, state.GetSample(0, lights[0].Style).Progress);

        // Unbound indices fall back to the light's starting state.
        Assert.False(new MapLightRuntimeState().IsPowered(0, lights[0].Style));
        Assert.Equal(MapLightRuntimeSample.Initial(lights[0].Style), new MapLightRuntimeState().GetSample(0, lights[0].Style));
    }

    [Fact]
    public void ColoursFollowTheirModeAndLogicState()
    {
        var light = Parse(new()
        {
            ["color"] = "ff0000",
            [MapLightMetadata.AltColorKey] = "0000ff",
            [MapLightMetadata.ColorModeKey] = "alternate",
            [MapLightMetadata.ColorRateKey] = "1",
            [MapLightMetadata.TriggerColorKey] = "00ff00",
        });
        var red = new MapLightingColor(255, 0, 0);
        var blue = new MapLightingColor(0, 0, 255);
        Assert.Equal(red, Emit(light, 0.25d).Emitters[0].Color);
        Assert.Equal(blue, Emit(light, 1.25d).Emitters[0].Color);

        var swapped = new MapLightRuntimeSample(true, 1f, MapLightColorState.Swapped);
        Assert.Equal(blue, Emit(light, 0.25d, swapped).Emitters[0].Color);
        var trigger = new MapLightRuntimeSample(true, 1f, MapLightColorState.Trigger);
        Assert.Equal(new MapLightingColor(0, 255, 0), Emit(light, 1.25d, trigger).Emitters[0].Color);

        // Split lamp: two halves in the two colours.
        var split = Parse(new() { ["color"] = "ff0000", [MapLightMetadata.AltColorKey] = "0000ff", [MapLightMetadata.ColorModeKey] = "split" });
        var halves = Emit(split, 0d).Emitters;
        Assert.Equal(2, halves.Count);
        Assert.Equal(red, halves[0].Color);
        Assert.Equal(blue, halves[1].Color);
        Assert.Equal(180f, MathF.Abs(halves[0].Direction - halves[1].Direction));
    }

    [Fact]
    public void SweepMotionStaysInsideItsArcAndPhaseOffsetsIt()
    {
        var light = Parse(new()
        {
            [MapLightMetadata.KindKey] = "spotlight",
            ["direction"] = "90",
            [MapLightMetadata.MotionKey] = "sweep",
            [MapLightMetadata.ArcKey] = "60",
            [MapLightMetadata.SpeedKey] = "60",
        });
        for (var time = 0d; time < 3d; time += 0.05d)
        {
            var beam = Assert.Single(Emit(light, time).Beams);
            Assert.InRange(beam.Direction, 59.9f, 120.1f);
        }

        // A quarter cycle in: at the arc's end (60 sweeps/min = 1 per second).
        Assert.Equal(120f, Emit(light, 0.25d).Beams[0].Direction, 2);

        var shifted = light with { Style = light.Style with { Phase = 90f } };
        Assert.Equal(120f, Emit(shifted, 0d).Beams[0].Direction, 2);
    }

    [Fact]
    public void SirensThrowTwoOppositeBeamsInBothColours()
    {
        var spin = Parse(new()
        {
            ["flicker"] = "siren",
            ["color"] = "ff0000",
            [MapLightMetadata.AltColorKey] = "0000ff",
            [MapLightMetadata.SirenStyleKey] = "spin",
            [MapLightMetadata.SpeedKey] = "60",
        });
        var cones = Emit(spin, 0.1d).Emitters.Where(emitter => MapLightMetadata.IsDirectional(emitter.Spread)).ToList();
        Assert.Equal(2, cones.Count);
        Assert.Equal(new MapLightingColor(255, 0, 0), cones[0].Color);
        Assert.Equal(new MapLightingColor(0, 0, 255), cones[1].Color);
        Assert.Equal(180f, MapLightMetadata.NormalizeDirection(cones[1].Direction - cones[0].Direction), 2);

        // Beacon: beams point left or right, and over a turn both sides show.
        var beacon = spin with { Style = spin.Style with { SirenStyle = MapLightSirenStyle.Beacon } };
        var directions = new HashSet<float>();
        for (var time = 0d; time < 1d; time += 0.05d)
        {
            foreach (var cone in Emit(beacon, time).Emitters.Where(emitter => MapLightMetadata.IsDirectional(emitter.Spread)))
            {
                directions.Add(cone.Direction);
            }
        }

        Assert.Equal(2, directions.Count);
        Assert.Contains(0f, directions);
        Assert.Contains(180f, directions);
    }

    [Fact]
    public void LaserFansSpreadEvenlyAndSplitColours()
    {
        var laser = Parse(new()
        {
            [MapLightMetadata.KindKey] = "laser",
            ["direction"] = "90",
            ["color"] = "ff0000",
            [MapLightMetadata.AltColorKey] = "00ff00",
            [MapLightMetadata.ColorModeKey] = "split",
            [MapLightMetadata.BeamsKey] = "3",
            [MapLightMetadata.FanSpreadKey] = "40",
        });
        var beams = Emit(laser, 0d).Beams;
        Assert.Equal(3, beams.Count);
        Assert.Equal(70f, beams[0].Direction, 2);
        Assert.Equal(90f, beams[1].Direction, 2);
        Assert.Equal(110f, beams[2].Direction, 2);
        Assert.Equal(new MapLightingColor(255, 0, 0), beams[0].Color);
        Assert.Equal(new MapLightingColor(0, 255, 0), beams[1].Color);
        Assert.All(beams, beam => Assert.False(beam.Damaging));
    }

    [Fact]
    public void DamagingLasersAreFixedClippedAndNeverInvisible()
    {
        var laser = Parse(new()
        {
            [MapLightMetadata.KindKey] = "laser",
            [MapLightMetadata.DamageKey] = "true",
            [MapLightMetadata.MotionKey] = "spin",
            ["radius"] = "500",
            ["intensity"] = "0",
        }) with { X = 0f, Y = 50f };

        // Motion is ignored: the beam points the same way at any time.
        Assert.Equal(Emit(laser, 0d).Beams[0].Direction, Emit(laser, 7.3d).Beams[0].Direction);
        var beam = Emit(laser, 0d).Beams[0];
        Assert.True(beam.Damaging);
        Assert.True(beam.Intensity >= MapLightEmission.MinimumDamagingLaserIntensity);

        // A wall at x = 200 stops it; the lamp sunk in its own wall still fires.
        LevelSolid[] solids = [new LevelSolid(200f, 0f, 20f, 100f), new LevelSolid(-5f, 40f, 10f, 20f)];
        var fixedBeam = Assert.Single(MapLightBeamGeometry.BuildFixedBeams([laser], solids));
        Assert.Equal(200f, fixedBeam.Length, 2);
        Assert.Equal(200f, fixedBeam.EndX, 2);

        var state = new MapLightRuntimeState();
        Assert.True(state.TryGetFixedBeamLength([laser], solids, 0, 0, out var length));
        Assert.Equal(200f, length, 2);
    }

    [Fact]
    public void BeamsHitBoxesWithinTheirLengthIncludingTheirWidth()
    {
        Assert.True(MapLightBeamGeometry.TryHitBox(0f, 0f, 1f, 0f, 100f, 0f, 50f, -5f, 60f, 5f, out var distance));
        Assert.Equal(50f, distance, 2);
        Assert.False(MapLightBeamGeometry.TryHitBox(0f, 0f, 1f, 0f, 40f, 0f, 50f, -5f, 60f, 5f, out _)); // too short
        Assert.False(MapLightBeamGeometry.TryHitBox(0f, 0f, 1f, 0f, 100f, 0f, 50f, 8f, 60f, 20f, out _)); // passes above
        Assert.True(MapLightBeamGeometry.TryHitBox(0f, 0f, 1f, 0f, 100f, 4f, 50f, 3f, 60f, 20f, out _)); // grazes with width
        Assert.False(MapLightBeamGeometry.TryHitBox(0f, 0f, 1f, 0f, 100f, 0f, -60f, -5f, -50f, 5f, out _)); // behind
    }

    [Fact]
    public void JukeboxLightsHoldTheirFloorInSilenceAndFollowTheMusic()
    {
        var light = Parse(new()
        {
            ["flicker"] = "jukebox",
            [MapLightMetadata.ReactKey] = "bass",
            [MapLightMetadata.FloorKey] = "20",
        });
        Assert.Equal(0.2f, Emit(light, 1d).Emitters[0].Intensity, 3);
        var loud = new MapLightMusic(true, 0.5f, 1f, 0.2f, 0.1f, 0f, 0);
        Assert.Equal(1f, Emit(light, 1d, music: loud).Emitters[0].Intensity, 3);
        var quiet = loud with { Bass = 0f };
        Assert.Equal(0.2f, Emit(light, 1d, music: quiet).Emitters[0].Intensity, 3);
    }

    [Fact]
    public void MapBeamHazeRoundTripsWithLighting()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lighting = MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Night) with { BeamHaze = 65 };
        MapLightingMetadata.Write(metadata, lighting);
        Assert.Equal("65", metadata[MapLightingMetadata.BeamHazeKey]);
        Assert.Equal(65, MapLightingMetadata.Parse(metadata).BeamHaze);
    }

    [Fact]
    public void LightNetworkStatePacksAndTakesAuthority()
    {
        var lights = new[]
        {
            Parse(new() { [MapLightMetadata.StartOnKey] = "false" }),
            Parse(new()),
        };
        var server = new MapLightRuntimeState();
        server.Bind(lights, MapLogicGraph.Empty);
        var client = new MapLightRuntimeState();
        client.Bind(lights, MapLogicGraph.Empty);
        Assert.False(client.HasNetworkAuthority);
        Assert.Empty(server.LogicLightIndices); // nothing wired to logic

        // Server says: light 0 on, colours swapped; light 1 off.
        client.ApplyNetworkState(0, 0b0011);
        client.ApplyNetworkState(1, 0);
        Assert.True(client.HasNetworkAuthority);
        Assert.True(client.IsPowered(0, lights[0].Style));
        Assert.False(client.IsPowered(1, lights[1].Style));
        Assert.Equal(MapLightColorState.Swapped, client.GetSample(0, lights[0].Style).ColorState);
        Assert.Equal(0b0011, client.GetNetworkState(0));

        // Out of range and junk colour bits are ignored safely.
        client.ApplyNetworkState(99, 1);
        client.ApplyNetworkState(1, 0b0111);
        Assert.Equal(MapLightColorState.Normal, client.GetSample(1, lights[1].Style).ColorState);
    }

    [Fact]
    public void SnapshotsCarryMapLightStates()
    {
        var snapshot = new SnapshotMessage(
            Frame: 12,
            TickRate: 30,
            LevelName: "test",
            MapAreaIndex: 1,
            MapAreaCount: 1,
            GameMode: 1,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 900,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState(1, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(2, 100f, 0f, true, false, 0),
            Players: Array.Empty<SnapshotPlayerState>(),
            CombatTraces: Array.Empty<SnapshotCombatTraceState>(),
            SniperAimIndicators: Array.Empty<SnapshotSniperAimIndicatorState>(),
            Sentries: Array.Empty<SnapshotSentryState>(),
            Shots: Array.Empty<SnapshotShotState>(),
            Bubbles: Array.Empty<SnapshotShotState>(),
            Blades: Array.Empty<SnapshotShotState>(),
            Needles: Array.Empty<SnapshotShotState>(),
            RevolverShots: Array.Empty<SnapshotShotState>(),
            Rockets: Array.Empty<SnapshotRocketState>(),
            Flames: Array.Empty<SnapshotFlameState>(),
            Flares: Array.Empty<SnapshotShotState>(),
            Mines: Array.Empty<SnapshotMineState>(),
            DeadBodies: Array.Empty<SnapshotDeadBodyState>(),
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: Array.Empty<SnapshotControlPointState>(),
            Generators: Array.Empty<SnapshotGeneratorState>(),
            LocalDeathCam: null,
            KillFeed: Array.Empty<SnapshotKillFeedEntry>(),
            VisualEvents: Array.Empty<SnapshotVisualEvent>(),
            DamageEvents: Array.Empty<SnapshotDamageEvent>(),
            SoundEvents: Array.Empty<SnapshotSoundEvent>())
        {
            CapLimit = 3,
            MapLightStates = [new SnapshotMapLightState(4, 1), new SnapshotMapLightState(700, 0b0101)],
        };

        Assert.True(ProtocolCodec.TryDeserialize(ProtocolCodec.Serialize(snapshot), out var message));
        var decoded = Assert.IsType<SnapshotMessage>(message);
        Assert.Equal(3, decoded.CapLimit);
        Assert.Equal(snapshot.MapLightStates, decoded.MapLightStates);
    }

    [Fact]
    public void LaserBeamsRimLightNearbyCharacters()
    {
        var lightmap = new OpenGarrison.Client.LightmapRenderer();
        lightmap.Begin(MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Night), 0f, 0f, 400f, 200f, 200f, 0d);
        lightmap.AddLightSegment(0f, 100f, 400f, 100f, 4f, Microsoft.Xna.Framework.Color.Red, 1f);
        Assert.True(lightmap.TryGetDominantLight(200f, 80f, out var toLight, out var color, out var strength));
        Assert.Equal(Microsoft.Xna.Framework.Color.Red, color);
        Assert.True(toLight.Y > 0.99f); // the beam is below: light comes from +y (screen down)
        Assert.InRange(strength, 0.01f, 1f);
        Assert.False(lightmap.TryGetDominantLight(200f, 0f, out _, out _, out _)); // too far away
    }

    private static MapLightMusic Beat(int beatCount, float pulse = 0f, float level = 1f) =>
        new(true, level, level, level, level, pulse, beatCount);

    [Fact]
    public void ThreeColoursCycleOnTheBeatAndOverTime()
    {
        var light = Parse(new()
        {
            ["color"] = "ff0000",
            [MapLightMetadata.AltColorKey] = "00ff00",
            [MapLightMetadata.AltColor2Key] = "0000ff",
            [MapLightMetadata.ColorModeKey] = "beat",
        });
        var red = new MapLightingColor(255, 0, 0);
        var green = new MapLightingColor(0, 255, 0);
        var blue = new MapLightingColor(0, 0, 255);
        Assert.Equal(red, Emit(light, 0d, music: Beat(0)).Emitters[0].Color);
        Assert.Equal(green, Emit(light, 0d, music: Beat(1)).Emitters[0].Color);
        Assert.Equal(blue, Emit(light, 0d, music: Beat(2)).Emitters[0].Color);
        Assert.Equal(red, Emit(light, 0d, music: Beat(3)).Emitters[0].Color);
        Assert.Equal(red, Emit(light, 0d).Emitters[0].Color); // no music: primary

        var alternate = light with { Style = light.Style with { ColorMode = MapLightColorMode.Alternate, ColorRate = 1f } };
        Assert.Equal(blue, Emit(alternate, 2.5d).Emitters[0].Color);

        // Swapped by logic rotates the colours by one.
        var swapped = new MapLightRuntimeSample(true, 1f, MapLightColorState.Swapped);
        Assert.Equal(green, Emit(light, 0d, swapped, Beat(0)).Emitters[0].Color);
    }

    [Fact]
    public void LaserFansPassColoursAlongOnTheBeat()
    {
        var laser = Parse(new()
        {
            [MapLightMetadata.KindKey] = "laser",
            ["color"] = "ff0000",
            [MapLightMetadata.AltColorKey] = "00ff00",
            [MapLightMetadata.AltColor2Key] = "0000ff",
            [MapLightMetadata.ColorModeKey] = "beat",
            [MapLightMetadata.BeamsKey] = "3",
        });
        var first = Emit(laser, 0d, music: Beat(0)).Beams;
        var next = Emit(laser, 0d, music: Beat(1)).Beams;
        Assert.Equal(first[1].Color, next[0].Color);
        Assert.Equal(first[2].Color, next[1].Color);
        Assert.Equal(first[0].Color, next[2].Color);
    }

    [Fact]
    public void BeatChaseStepsAcrossTheArcAndMirrors()
    {
        var chase = Parse(new()
        {
            [MapLightMetadata.KindKey] = "spotlight",
            ["direction"] = "90",
            [MapLightMetadata.MotionKey] = "beat",
            [MapLightMetadata.ArcKey] = "60",
            [MapLightMetadata.ChaseStepsKey] = "4",
        });

        // Settled (pulse faded): 4 positions across 60 degrees, then back (ping-pong).
        float[] expected = [60f, 80f, 100f, 120f, 100f, 80f, 60f];
        for (var beat = 0; beat < expected.Length; beat += 1)
        {
            Assert.Equal(expected[beat], Emit(chase, 0d, music: Beat(beat)).Beams[0].Direction, 2);
        }

        // On the beat itself (pulse 1) it is still where the last beat left it.
        Assert.Equal(60f, Emit(chase, 0d, music: Beat(1, pulse: 1f)).Beams[0].Direction, 2);
        // No music: rests at the centre.
        Assert.Equal(90f, Emit(chase, 0d).Beams[0].Direction, 2);

        // Mirrored: the opposite side of centre at every step.
        var mirrored = chase with { Style = chase.Style with { Mirror = true } };
        for (var beat = 0; beat < expected.Length; beat += 1)
        {
            Assert.Equal(180f - expected[beat], Emit(mirrored, 0d, music: Beat(beat)).Beams[0].Direction, 2);
        }
    }

    [Fact]
    public void MirrorFlipsSweepsAndSirens()
    {
        var sweep = Parse(new()
        {
            [MapLightMetadata.KindKey] = "spotlight",
            ["direction"] = "90",
            [MapLightMetadata.MotionKey] = "sweep",
            [MapLightMetadata.ArcKey] = "60",
            [MapLightMetadata.SpeedKey] = "60",
        });
        var mirrored = sweep with { Style = sweep.Style with { Mirror = true } };
        Assert.Equal(120f, Emit(sweep, 0.25d).Beams[0].Direction, 2);
        Assert.Equal(60f, Emit(mirrored, 0.25d).Beams[0].Direction, 2);

        var siren = Parse(new() { ["flicker"] = "siren", [MapLightMetadata.SirenStyleKey] = "spin", [MapLightMetadata.SpeedKey] = "60" });
        var mirroredSiren = siren with { Style = siren.Style with { Mirror = true } };
        var turn = Emit(siren, 0.1d).Emitters.First(emitter => MapLightMetadata.IsDirectional(emitter.Spread)).Direction;
        var mirroredTurn = Emit(mirroredSiren, 0.1d).Emitters.First(emitter => MapLightMetadata.IsDirectional(emitter.Spread)).Direction;
        Assert.Equal(36f, turn, 2);
        Assert.Equal(144f, mirroredTurn, 2); // aim and rotation both mirrored: 180 - 36
    }

    [Fact]
    public void MirroredLightsAimAsTheirMirrorImage()
    {
        var left = Parse(new()
        {
            [MapLightMetadata.KindKey] = "laser",
            ["direction"] = "60",
            [MapLightMetadata.BeamsKey] = "3",
            [MapLightMetadata.FanSpreadKey] = "20",
            ["color"] = "ff0000",
            [MapLightMetadata.AltColorKey] = "00ff00",
            [MapLightMetadata.AltColor2Key] = "0000ff",
            [MapLightMetadata.ColorModeKey] = "split",
        });
        var right = left with { Style = left.Style with { Mirror = true } };
        Assert.Equal(120f, MapLightMetadata.GetAimDirection(right), 2);

        var leftBeams = Emit(left, 0d).Beams;
        var rightBeams = Emit(right, 0d).Beams;
        for (var beam = 0; beam < 3; beam += 1)
        {
            // Each beam (and its colour) is the mirror image of its partner.
            Assert.Equal(MapLightMetadata.MirrorDirection(leftBeams[beam].Direction), rightBeams[beam].Direction, 2);
            Assert.Equal(leftBeams[beam].Color, rightBeams[beam].Color);
        }

        // A damaging mirrored laser's fixed beam points the mirrored way too.
        var damaging = right with { X = 0f, Y = 0f, Style = right.Style with { Damage = true, Beams = 1 } };
        var fixedBeam = Assert.Single(MapLightBeamGeometry.BuildFixedBeams([damaging], Array.Empty<LevelSolid>()));
        Assert.True(fixedBeam.DirectionX < 0f); // 120 degrees: up and to the left
    }

    [Fact]
    public void QuietPartsDimTurnOffOrDriftMusicLights()
    {
        var quiet = Beat(4) with { Quiet = true, QuietBlend = 1f };
        var light = Parse(new() { ["flicker"] = "jukebox", [MapLightMetadata.FloorKey] = "20", [MapLightMetadata.ReactKey] = "level" });
        Assert.Equal(1f, Emit(light, 0d, music: quiet).Emitters[0].Intensity, 3); // stay
        var dim = light with { Style = light.Style with { DownTime = MapLightDownTime.Dim } };
        Assert.Equal(0.2f, Emit(dim, 0d, music: quiet).Emitters[0].Intensity, 3);
        var off = light with { Style = light.Style with { DownTime = MapLightDownTime.Off } };
        Assert.Equal(0f, Emit(off, 0d, music: quiet).Emitters[0].Intensity, 3);

        // Lights that ignore the music are left alone.
        var plain = Parse(new() { [MapLightMetadata.DownTimeKey] = "off" });
        Assert.Equal(1f, Emit(plain, 0d, music: quiet).Emitters[0].Intensity, 3);
    }

    [Fact]
    public void MusicCanWidenTheSweep()
    {
        var light = Parse(new()
        {
            [MapLightMetadata.KindKey] = "spotlight",
            ["direction"] = "90",
            [MapLightMetadata.MotionKey] = "beat",
            [MapLightMetadata.ArcKey] = "60",
            [MapLightMetadata.MusicSweepKey] = "true",
            [MapLightMetadata.ReactKey] = "level",
        });
        Assert.Equal(60f, Emit(light, 0d, music: Beat(0, level: 1f)).Beams[0].Direction, 2);
        Assert.Equal(81f, Emit(light, 0d, music: Beat(0, level: 0f)).Beams[0].Direction, 2); // 30% of the arc
    }

    [Fact]
    public void AnalyzerFindsTheQuietPartsOfASong()
    {
        var analyzer = new OpenGarrison.Client.JukeboxAnalyzer();
        var now = 0d;
        var quietSeen = false;
        void Play(double seconds, float amplitude)
        {
            const int Frames = 960;
            var chunks = (int)(seconds / 0.02d);
            for (var chunk = 0; chunk < chunks; chunk += 1)
            {
                var samples = new short[Frames * 2];
                for (var frame = 0; frame < Frames; frame += 1)
                {
                    var value = (short)(MathF.Sin((frame + (chunk * Frames)) * 0.05f) * amplitude * 32767f);
                    samples[frame * 2] = value;
                    samples[(frame * 2) + 1] = value;
                }

                analyzer.Feed(samples, 2, now);
                now += 0.02d;
                quietSeen = analyzer.Sample(now).Quiet;
            }
        }

        Play(8d, 0.6f);
        Assert.False(quietSeen);
        Play(3d, 0.05f);
        Assert.True(quietSeen);
        Play(2d, 0.6f);
        Assert.False(quietSeen);
    }

    [Fact]
    public void PlayersCanMakeEveryLightFollowTheJukebox()
    {
        var plain = Parse(new() { ["intensity"] = "100" });
        var beat = new MapLightMusic(true, 0.5f, 1f, 0.3f, 0.2f, 1f, 3);
        var between = beat with { Bass = 0f, Beat = 0f };

        // Off by default: the music changes nothing on a plain light.
        Assert.Equal(1f, Emit(plain, 0d, music: between).Emitters[0].Intensity, 3);

        // Forced: full on the beat, 35% between beats.
        Assert.Equal(1f, Emit(plain, 0d, music: beat with { ForceReact = true }).Emitters[0].Intensity, 3);
        Assert.Equal(0.35f, Emit(plain, 0d, music: between with { ForceReact = true }).Emitters[0].Intensity, 3);

        // No music (nothing playing, or muted): forced lights look as the map made them.
        Assert.Equal(1f, Emit(plain, 0d, music: MapLightMusic.Silent with { ForceReact = true }).Emitters[0].Intensity, 3);

        // Lights already on the jukebox steadiness keep their own settings.
        var jukebox = Parse(new() { ["flicker"] = "jukebox", [MapLightMetadata.FloorKey] = "50", [MapLightMetadata.ReactKey] = "bass" });
        Assert.Equal(0.5f, Emit(jukebox, 0d, music: between with { ForceReact = true }).Emitters[0].Intensity, 3);
    }

    [Fact]
    public void LightsFollowJukeboxPreferenceSavesAndLoads()
    {
        var path = Path.Combine(Path.GetTempPath(), "lights-follow-jukebox-" + Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            var settings = new OpenGarrisonPreferencesDocument { LightsFollowJukeboxEnabled = true };
            settings.Save(path);
            Assert.True(OpenGarrisonPreferencesDocument.Load(path).LightsFollowJukeboxEnabled);
            Assert.False(new OpenGarrisonPreferencesDocument().LightsFollowJukeboxEnabled);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
