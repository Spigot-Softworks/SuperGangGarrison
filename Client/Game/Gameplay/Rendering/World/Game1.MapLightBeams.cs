#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Map lights beyond the plain lamp: logic power and colour, jukebox music, motion driven
/// by the music, and the visible beams of searchlights and lasers (clipped at walls and
/// players, lighting where they land, drawn in the air). Shared by gameplay and the
/// builder preview.
/// </summary>
public partial class Game1
{
    private readonly MapBeamRenderer _mapBeamRenderer = new();
    private readonly List<MapLightBeam> _mapLightBeams = new();
    private readonly List<ResolvedMapBeam> _resolvedMapBeams = new();
    private readonly List<MapLightEmitter> _scratchMapLightEmitters = new();
    private LightOcclusionField? _lightOcclusionField;

    private IReadOnlyList<MapLightMarker>? _musicMotionLights;
    private double[] _musicMotionCycles = Array.Empty<double>();
    private double _musicMotionLastTime = double.NaN;

    /// <summary>Most light-map lights a single laser beam adds along its length.</summary>
    private const int MaxLaserStripLights = 10;

    /// <summary>
    /// The jukebox as this player hears it (silent when muted or not playing), flagged when
    /// the player wants every light to follow it (Graphics > Lights Follow Jukebox).
    /// </summary>
    private MapLightMusic SampleMapLightMusic()
    {
        var music = _voiceChat?.JukeboxAnalyzer.Sample(VoiceClockSeconds) ?? MapLightMusic.Silent;
        return _gameplayManager.RuntimeSettings.LightsFollowJukeboxEnabled ? music with { ForceReact = true } : music;
    }

    /// <summary>
    /// Evaluates the level's placed lights (with their logic state and the music), adds them
    /// to the gameplay light map, and resolves their beams for drawing.
    /// </summary>
    private void AddGameplayMapLights(SimpleLevel level, double time)
    {
        var lights = level.MapLights;
        _mapLightEmitters.Clear();
        _mapLightBeams.Clear();
        _resolvedMapBeams.Clear();
        if (lights.Count == 0)
        {
            return;
        }

        var music = SampleMapLightMusic();
        var elapsed = AdvanceMusicMotionClock(lights, time);
        for (var index = 0; index < lights.Count; index += 1)
        {
            var light = lights[index];
            var propOpacity = GetDestructiblePropOpacity(light.DestructibleZoneIndex);
            light = light with { Intensity = (int)MathF.Round(light.Intensity * propOpacity) };
            var sample = level.MapLightState.GetSample(index, light.Style);
            if (IsMusicDrivingMotion(light))
            {
                sample = sample with { MotionCycles = AdvanceMusicMotion(index, light, music, elapsed) };
            }

            MapLightEmission.Evaluate(light, index, time, sample, music, _mapLightEmitters, _mapLightBeams);
        }

        SubmitMapLightEmitters(_gameplayLightmap, _mapLightEmitters, _mapLightReach, ResolveMapLightMask);
        ResolveMapBeams(_mapLightBeams, _resolvedMapBeams, level, _mapLightReach, _lightOcclusionField, includePlayers: true);
        AddMapBeamLights(_gameplayLightmap, _resolvedMapBeams);
    }

    /// <summary>
    /// With lighting off (by the map or the player), only damaging lasers are drawn: a
    /// hazard must never be invisible.
    /// </summary>
    private void ResolveDamagingLaserBeamsOnly(SimpleLevel level, double time)
    {
        _mapLightBeams.Clear();
        _resolvedMapBeams.Clear();
        var lights = level.MapLights;
        for (var index = 0; index < lights.Count; index += 1)
        {
            var light = lights[index] with { Intensity = (int)MathF.Round(lights[index].Intensity * GetDestructiblePropOpacity(lights[index].DestructibleZoneIndex)) };
            if (!light.Style.IsDamagingLaser)
            {
                continue;
            }

            _scratchMapLightEmitters.Clear();
            MapLightEmission.Evaluate(light, index, time, level.MapLightState.GetSample(index, light.Style), MapLightMusic.Silent, _scratchMapLightEmitters, _mapLightBeams);
        }

        ResolveMapBeams(_mapLightBeams, _resolvedMapBeams, level, Array.Empty<float[]?>(), null, includePlayers: true);
    }

    private static bool IsMusicDrivingMotion(in MapLightMarker light) =>
        light.Flicker == MapLightFlicker.Jukebox && light.Style.MusicDrivesSpeed;

    /// <summary>Seconds since the last motion step (resets when the level's lights change).</summary>
    private double AdvanceMusicMotionClock(IReadOnlyList<MapLightMarker> lights, double time)
    {
        if (!ReferenceEquals(lights, _musicMotionLights) || _musicMotionCycles.Length != lights.Count)
        {
            _musicMotionLights = lights;
            _musicMotionCycles = new double[lights.Count];
            for (var index = 0; index < lights.Count; index += 1)
            {
                _musicMotionCycles[index] = lights[index].Style.Phase / 360d;
            }

            _musicMotionLastTime = double.NaN;
        }

        var elapsed = double.IsNaN(_musicMotionLastTime) ? 0d : Math.Clamp(time - _musicMotionLastTime, 0d, 0.25d);
        _musicMotionLastTime = time;
        return elapsed;
    }

    /// <summary>Integrates a music-driven light's motion: the louder, the faster it moves.</summary>
    private double AdvanceMusicMotion(int index, in MapLightMarker light, MapLightMusic music, double elapsed)
    {
        var style = light.Style;
        var amount = music.Active ? Math.Clamp(music.Get(style.React) * (style.Sensitivity / 100f), 0f, 1f) : 0f;
        _musicMotionCycles[index] += elapsed * (style.Speed / 60d) * (0.25d + (1.75d * amount));
        return _musicMotionCycles[index];
    }

    /// <summary>
    /// Clips beams: damaging lasers use the shared fixed geometry (exactly what the server
    /// damages along); searchlights use their light's wall reach; other lasers cast against
    /// the wall field; lasers set to stop at players end at the first player they touch.
    /// </summary>
    private void ResolveMapBeams(
        List<MapLightBeam> beams,
        List<ResolvedMapBeam> resolved,
        SimpleLevel? level,
        float[]?[] reach,
        LightOcclusionField? field,
        bool includePlayers)
    {
        resolved.Clear();
        foreach (var beam in beams)
        {
            var (directionX, directionY) = MapLightMetadata.GetDirectionVector(beam.Direction);
            var length = beam.Length;
            var hit = false;
            if (beam.Damaging
                && level is not null
                && level.MapLightState.TryGetFixedBeamLength(level.MapLights, level.Solids, beam.SourceIndex, beam.BeamIndex, out var fixedLength))
            {
                hit = fixedLength < length - 0.5f;
                length = fixedLength;
            }
            else if (beam.Kind == MapLightKind.Spotlight
                && beam.SourceIndex >= 0
                && beam.SourceIndex < reach.Length
                && reach[beam.SourceIndex] is { Length: > 0 } rays)
            {
                var reached = SampleLightReach(rays, beam.Direction);
                if (reached < length)
                {
                    length = reached;
                    hit = true;
                }
            }
            else if (beam.Kind == MapLightKind.Laser && field is not null)
            {
                var reached = field.CastRay(beam.X, beam.Y, directionX, directionY, length);
                if (reached < length - 0.5f)
                {
                    length = reached;
                    hit = true;
                }
            }

            if (includePlayers && beam.StopsAtPlayers && beam.Kind == MapLightKind.Laser)
            {
                foreach (var player in EnumerateRenderablePlayers())
                {
                    if (!player.IsAlive)
                    {
                        continue;
                    }

                    player.GetCollisionBounds(out var left, out var top, out var right, out var bottom);
                    if (MapLightBeamGeometry.TryHitBox(beam.X, beam.Y, directionX, directionY, length, beam.Width * 0.5f, left, top, right, bottom, out var distance)
                        && distance < length)
                    {
                        length = distance;
                        hit = true;
                    }
                }
            }

            resolved.Add(new ResolvedMapBeam(beam, MathF.Max(0f, length), hit));
        }
    }

    /// <summary>
    /// Where beams light the world: a laser casts a faint strip of its colour along its
    /// length and a hot spot where it lands; a searchlight leaves a pool where it hits.
    /// </summary>
    private static void AddMapBeamLights(LightmapRenderer lightmap, IReadOnlyList<ResolvedMapBeam> beams)
    {
        foreach (var resolved in beams)
        {
            var beam = resolved.Beam;
            if (resolved.Length <= 0.5f || beam.Intensity <= 0.004f)
            {
                continue;
            }

            var direction = resolved.Direction;
            if (beam.Kind == MapLightKind.Laser)
            {
                var count = Math.Clamp((int)(resolved.Length / 56f), 1, MaxLaserStripLights);
                var radius = 14f + (beam.Width * 3f);
                for (var step = 0; step < count; step += 1)
                {
                    var t = (step + 0.5f) / count;
                    var point = resolved.Origin + (direction * (resolved.Length * t));
                    var color = MapLightEmission.Lerp(beam.Color, beam.EndColor, t);
                    lightmap.AddLight(point.X, point.Y, radius, new Color(color.R, color.G, color.B), beam.Intensity * 0.22f, glows: false);
                }

                // Characters near the beam are rim-lit from it.
                var beamEnd = resolved.End;
                lightmap.AddLightSegment(beam.X, beam.Y, beamEnd.X, beamEnd.Y, beam.Width, new Color(beam.Color.R, beam.Color.G, beam.Color.B), beam.Intensity);

                if (resolved.Hit)
                {
                    var end = resolved.End;
                    lightmap.AddLight(end.X, end.Y, 26f + (beam.Width * 4f), new Color(beam.EndColor.R, beam.EndColor.G, beam.EndColor.B), beam.Intensity * 0.9f, glowBoost: beam.Intensity * 0.6f);
                }

                continue;
            }

            if (resolved.Hit)
            {
                var end = resolved.End;
                lightmap.AddLight(end.X, end.Y, Math.Clamp(resolved.EndWidth * 0.5f, 16f, 160f), new Color(beam.EndColor.R, beam.EndColor.G, beam.EndColor.B), beam.Intensity * 0.5f, glows: false);
            }
        }
    }

    /// <summary>How far a light's wall-reach rays travel toward a map direction.</summary>
    private static float SampleLightReach(float[] rays, float directionDegrees)
    {
        // Rays run clockwise on screen from the right (y down); map directions run counter-clockwise.
        var angle = -directionDegrees * (MathF.PI / 180f);
        angle %= MathF.Tau;
        if (angle < 0f)
        {
            angle += MathF.Tau;
        }

        var index = (int)MathF.Round(angle / MathF.Tau * rays.Length) % rays.Length;
        return rays[index];
    }

    /// <summary>Draws resolved beams additively over the world (the batch is restarted around it).</summary>
    private void DrawMapBeamsInWorldPass(IReadOnlyList<ResolvedMapBeam> beams, float mapHaze, Matrix? transform, RasterizerState rasterizer, Vector2 offset, float intensity, bool banded = false)
    {
        if (beams.Count == 0)
        {
            return;
        }

        _spriteBatch.Begin(SpriteSortMode.Deferred, LightmapRenderer.AddOne, MapBeamRenderer.GetSampler(banded), DepthStencilState.None, rasterizer, null, transform);
        _mapBeamRenderer.Draw(GraphicsDevice, _spriteBatch, beams, mapHaze, 1f, offset, intensity, banded);
        _spriteBatch.End();
    }

    /// <summary>
    /// A 120 BPM test signal so music lights can be previewed in the editor: 24 beats of
    /// music, then an 8-beat breakdown (down time), repeating.
    /// </summary>
    private static MapLightMusic GetBuilderPreviewMusic(double time)
    {
        const double BeatsPerSecond = 2d;
        const double LoopBeats = 32d;
        const double QuietFromBeat = 24d;
        const double QuietRampBeats = 0.7d;
        var beats = time * BeatsPerSecond;
        var sinceBeat = (beats - Math.Floor(beats)) / BeatsPerSecond;
        var inLoop = beats - (Math.Floor(beats / LoopBeats) * LoopBeats);
        var quiet = inLoop >= QuietFromBeat;
        var quietBlend = quiet
            ? Math.Clamp((inLoop - QuietFromBeat) / QuietRampBeats, 0d, 1d)
            : Math.Clamp(1d - (inLoop / QuietRampBeats), 0d, 1d) * (beats >= LoopBeats ? 1d : 0d);
        var energy = (float)(1d - (0.8d * quietBlend));
        var beat = (float)Math.Exp(-sinceBeat / 0.14d) * energy;
        var bass = (0.25f + (0.75f * beat)) * energy;
        var mids = (0.45f + (0.35f * (float)(0.5d + (0.5d * Math.Sin(time * 5.3d))))) * energy;
        var highs = (0.3f + (0.5f * MapLightAnimation.SmoothNoise(time * 11d))) * energy;
        var level = Math.Clamp((bass * 0.5f) + (mids * 0.3f) + (highs * 0.2f), 0f, 1f);
        return new MapLightMusic(true, level, bass, mids, highs, beat, (int)Math.Floor(beats))
        {
            Quiet = quiet,
            QuietBlend = (float)quietBlend,
        };
    }
}
