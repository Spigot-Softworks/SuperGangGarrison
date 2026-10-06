#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>Receives the solid-colour pixel rectangles a weather frame is made of.</summary>
internal interface IWeatherPixelSink
{
    void Fill(int x, int y, int width, int height, Color color);
}

/// <summary>
/// Procedural retro rain and snow, evaluated analytically from time.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is simulated or stored per frame. Each layer owns a fixed set of drops
/// laid out in a world-space tile; a drop's position is a closed-form function of
/// time (fall + wind integral + sway) and the tile repeats across the visible area.
/// Cost is therefore proportional to visible drops only, there are no allocations
/// after a weather change, frame rate and tick rate do not matter, and every pass
/// that draws the world (split view, death cam) sees the same weather.
/// </para>
/// <para>
/// Drops stop at the level's open-sky surface (<see cref="WeatherSkyMask"/>). A drop
/// keeps falling "underground" invisibly, and the distance it has fallen past the
/// surface drives a short splash (rain) or settle-and-fade (snow) animation at the
/// landing point, so impacts need no state either. Top-down maps have no sky
/// surface; drops land at a per-drop depth instead.
/// </para>
/// </remarks>
internal sealed class RetroWeatherField
{
    /// <summary>Hard per-frame rectangle cap so extreme zoom-out stays bounded.</summary>
    public const int MaxRectanglesPerFrame = 6000;

    private const float RainSplashSeconds = 0.24f;
    private const float SnowSettleSeconds = 1.6f;
    private const float StormSnowSettleSeconds = 0.45f;
    private const float LeafSettleSeconds = 4.5f;

    private readonly struct LayerSpec
    {
        public LayerSpec(
            float tileWidth,
            float tileHeight,
            float densityPer10K,
            float fallSpeed,
            float speedJitter,
            float lengthMin,
            float lengthMax,
            float alpha,
            Color color,
            float windFactor,
            bool impacts,
            int flakeSize,
            float swayAmplitude,
            float swayFrequency)
        {
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            DensityPer10K = densityPer10K;
            FallSpeed = fallSpeed;
            SpeedJitter = speedJitter;
            LengthMin = lengthMin;
            LengthMax = lengthMax;
            Alpha = alpha;
            Color = color;
            WindFactor = windFactor;
            Impacts = impacts;
            FlakeSize = flakeSize;
            SwayAmplitude = swayAmplitude;
            SwayFrequency = swayFrequency;
        }

        public float TileWidth { get; }
        public float TileHeight { get; }
        public float DensityPer10K { get; }
        public float FallSpeed { get; }
        public float SpeedJitter { get; }
        public float LengthMin { get; }
        public float LengthMax { get; }
        public float Alpha { get; }
        public Color Color { get; }
        public float WindFactor { get; }
        public bool Impacts { get; }
        public int FlakeSize { get; }
        public float SwayAmplitude { get; }
        public float SwayFrequency { get; }
    }

    private sealed class Layer
    {
        public required LayerSpec Spec { get; init; }
        public required float[] BaseX { get; init; }
        public required float[] BaseY { get; init; }
        public required float[] Speed { get; init; }
        public required float[] Length { get; init; }
        public required float[] Alpha { get; init; }
        public required float[] Phase { get; init; }
        public required float[] LandFraction { get; init; }
        public required byte[] Flags { get; init; }
        public required byte[] Variant { get; init; }
    }

    /// <summary>How a drifting (non-rain) layer moves and draws.</summary>
    private enum DriftStyle
    {
        Snow,
        StormSnow,
        Leaves,
    }

    private const byte FlagImpact = 1;
    private const byte FlagBigFlake = 2;

    // Tiles are larger than a typical view so a pattern never repeats on screen, and
    // their sizes differ per layer so layer repeats never line up. Cost depends on
    // density times visible area, not on tile size.
    private static readonly LayerSpec[] RainLayers =
    [
        new(997f, 613f, 3.6f, 680f, 0.10f, 12f, 18f, 0.62f, new Color(186, 204, 228), 1.00f, true, 1, 0f, 0f),
        new(853f, 577f, 4.2f, 520f, 0.12f, 8f, 11f, 0.42f, new Color(160, 180, 210), 0.85f, true, 1, 0f, 0f),
        new(701f, 541f, 4.8f, 380f, 0.15f, 4f, 6f, 0.26f, new Color(132, 150, 182), 0.70f, false, 1, 0f, 0f),
    ];

    private static readonly LayerSpec[] SnowLayers =
    [
        new(1009f, 607f, 2.4f, 54f, 0.25f, 0f, 0f, 0.95f, new Color(244, 247, 255), 1.00f, true, 2, 9f, 1.3f),
        new(859f, 571f, 3.4f, 38f, 0.25f, 0f, 0f, 0.72f, new Color(226, 232, 246), 0.85f, true, 1, 6f, 1.7f),
        new(709f, 547f, 4.4f, 25f, 0.30f, 0f, 0f, 0.42f, new Color(200, 208, 228), 0.65f, false, 1, 4f, 2.1f),
    ];

    // Snowstorm: more, faster flakes driven almost sideways; drawn as short streaks.
    private static readonly LayerSpec[] StormSnowLayers =
    [
        new(1021f, 631f, 3.0f, 92f, 0.30f, 0f, 0f, 0.95f, new Color(246, 248, 255), 1.00f, true, 2, 5f, 2.2f),
        new(877f, 593f, 4.4f, 72f, 0.30f, 0f, 0f, 0.74f, new Color(230, 236, 248), 0.90f, true, 1, 4f, 2.6f),
        new(727f, 563f, 5.8f, 54f, 0.35f, 0f, 0f, 0.50f, new Color(210, 218, 236), 0.80f, false, 1, 3f, 3.0f),
    ];

    // Autumn leaves: few, slow, big sway and a tumbling flutter. Colour comes from
    // LeafPalette per leaf; the spec colour is unused.
    private static readonly LayerSpec[] LeafLayers =
    [
        new(1013f, 619f, 1.2f, 46f, 0.30f, 0f, 0f, 1.00f, Color.White, 1.00f, true, 4, 22f, 1.1f),
        new(863f, 587f, 1.8f, 34f, 0.30f, 0f, 0f, 0.85f, Color.White, 0.85f, true, 3, 15f, 1.4f),
        new(719f, 557f, 2.4f, 24f, 0.35f, 0f, 0f, 0.60f, Color.White, 0.70f, false, 2, 10f, 1.8f),
    ];

    private static readonly Color[] LeafPalette =
    [
        new Color(186, 72, 34),
        new Color(214, 118, 38),
        new Color(226, 170, 58),
        new Color(150, 92, 46),
        new Color(168, 48, 40),
        new Color(124, 128, 52),
    ];

    private static readonly Color StormSheetColor = new(232, 238, 250);

    /// <summary>Opacity of each drift wisp segment, tail to head: tapered at both ends.</summary>
    private static readonly float[] DriftWispShape = [0.45f, 1f, 1f, 0.45f];

    private const int DriftWispCount = 300;

    private Layer[] _layers = [];
    private MapWeatherKind _kind;
    private MapWeatherIntensity _intensity;
    private Color? _rainColor;
    private int _rectanglesThisFrame;
    private IReadOnlyList<IndoorRegionMarker>? _hiddenRegions;

    public MapWeatherKind Kind => _kind;

    public int DropCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < _layers.Length; index += 1)
            {
                count += _layers[index].BaseX.Length;
            }

            return count;
        }
    }

    /// <summary>Rebuilds the drop tables when the weather kind or intensity changes.</summary>
    /// <summary>
    /// Recolours the rain: the nearest layer takes this colour and farther layers a dimmer
    /// version of it, as with the default pale blue. Null restores the default colours.
    /// </summary>
    public void SetRainColor(Color? color) => _rainColor = color;

    public void Configure(MapWeatherKind kind, MapWeatherIntensity intensity)
    {
        if (kind == _kind && intensity == _intensity && (_layers.Length > 0 || kind == MapWeatherKind.None))
        {
            return;
        }

        _kind = kind;
        _intensity = intensity;
        if (kind == MapWeatherKind.None)
        {
            _layers = [];
            return;
        }

        var specs = (kind, intensity) switch
        {
            (MapWeatherKind.Snow, MapWeatherIntensity.Storm) => StormSnowLayers,
            (MapWeatherKind.Snow, _) => SnowLayers,
            (MapWeatherKind.Leaves, _) => LeafLayers,
            _ => RainLayers,
        };
        var amount = intensity switch
        {
            MapWeatherIntensity.Light => 0.45f,
            MapWeatherIntensity.Heavy => 1.7f,
            // Storm snow already uses its own dense tables.
            MapWeatherIntensity.Storm => kind == MapWeatherKind.Snow ? 1f : 2.1f,
            _ => 1f,
        };
        _layers = new Layer[specs.Length];
        for (var layerIndex = 0; layerIndex < specs.Length; layerIndex += 1)
        {
            _layers[layerIndex] = CreateLayer(specs[layerIndex], amount, unchecked(((uint)(layerIndex + 1) * 0x9E3779B1u) ^ (uint)kind));
        }
    }

    /// <summary>
    /// Emits the visible weather for one camera.
    /// </summary>
    /// <param name="sink">Pixel output, in camera-relative world pixels.</param>
    /// <param name="skyMask">Open-sky surface; null on top-down maps.</param>
    /// <param name="cameraX">Camera top-left in world pixels.</param>
    /// <param name="cameraY">Camera top-left in world pixels.</param>
    /// <param name="viewWidth">Visible world width.</param>
    /// <param name="viewHeight">Visible world height.</param>
    /// <param name="worldWidth">Map width; weather stays inside the map.</param>
    /// <param name="worldHeight">Map height.</param>
    /// <param name="timeSeconds">Monotonic presentation time.</param>
    /// <param name="wind">Author-chosen prevailing wind.</param>
    /// <param name="densityScale">0..1 share of drops to draw (quality settings).</param>
    /// <param name="hiddenRegions">Areas where no weather pixel may appear (indoor
    /// regions on top-down maps; platformer maps roof them in the sky mask instead).</param>
    /// <returns>Rectangles emitted.</returns>
    public int Emit<TSink>(
        ref TSink sink,
        WeatherSkyMask? skyMask,
        float cameraX,
        float cameraY,
        float viewWidth,
        float viewHeight,
        float worldWidth,
        float worldHeight,
        double timeSeconds,
        MapWeatherWind wind,
        float densityScale,
        IReadOnlyList<IndoorRegionMarker>? hiddenRegions = null)
        where TSink : struct, IWeatherPixelSink
    {
        _rectanglesThisFrame = 0;
        _hiddenRegions = hiddenRegions is { Count: > 0 } ? hiddenRegions : null;
        if (_layers.Length == 0 || densityScale <= 0f || viewWidth <= 0f || viewHeight <= 0f)
        {
            return 0;
        }

        // Clip to the map: weather never falls in the void around it.
        var left = MathF.Max(cameraX, 0f);
        var right = MathF.Min(cameraX + viewWidth, worldWidth);
        var top = MathF.Max(cameraY, 0f);
        var bottom = MathF.Min(cameraY + viewHeight, worldHeight);
        if (right <= left || bottom <= top)
        {
            return 0;
        }

        var clip = new ClipRect(left, top, right, bottom, cameraX, cameraY);
        var storm = _intensity == MapWeatherIntensity.Storm;
        var driftStyle = _kind switch
        {
            MapWeatherKind.Leaves => DriftStyle.Leaves,
            MapWeatherKind.Snow when storm => DriftStyle.StormSnow,
            _ => DriftStyle.Snow,
        };
        for (var layerIndex = _layers.Length - 1; layerIndex >= 0; layerIndex -= 1)
        {
            var layer = _layers[layerIndex];
            var count = (int)MathF.Ceiling(layer.BaseX.Length * Math.Clamp(densityScale, 0f, 1f));
            if (driftStyle == DriftStyle.StormSnow && layerIndex == 0)
            {
                // Whiteout haze and drift sit between the distant flakes and the nearest ones.
                EmitStormWhiteout(ref sink, skyMask, clip, timeSeconds, wind, densityScale);
            }

            if (_kind == MapWeatherKind.Rain)
            {
                EmitRainLayer(ref sink, layer, count, skyMask, clip, timeSeconds, wind, storm);
            }
            else
            {
                EmitDriftLayer(ref sink, layer, count, skyMask, clip, timeSeconds, wind, driftStyle, storm);
            }

            if (_rectanglesThisFrame >= MaxRectanglesPerFrame)
            {
                break;
            }
        }

        if (driftStyle == DriftStyle.StormSnow && skyMask is not null && _rectanglesThisFrame < MaxRectanglesPerFrame)
        {
            EmitSpindrift(ref sink, skyMask, clip, timeSeconds, wind, densityScale);
        }

        return _rectanglesThisFrame;
    }

    /// <summary>
    /// Heavy-rain lightning brightness (0..1) at a time. Rare, soft double pulses so it
    /// reads as distant weather rather than a strobe.
    /// </summary>
    public static float GetLightningFlash(double timeSeconds, bool storm = false)
    {
        var slotSeconds = storm ? 6d : 9d;
        var slot = (long)Math.Floor(timeSeconds / slotSeconds);
        var hash = Hash(unchecked((uint)slot) ^ 0xA511E9B3u);
        if (Unit(hash) > (storm ? 0.6f : 0.4f))
        {
            return 0f;
        }

        var strikeAt = (0.15d + 0.7d * Unit(Hash(hash))) * slotSeconds;
        var local = timeSeconds - (slot * slotSeconds) - strikeAt;
        return Pulse(local, 0d) + 0.7f * Pulse(local, 0.13d);

        static float Pulse(double t, double start)
        {
            var dt = t - start;
            return dt < 0d || dt > 0.1d ? 0f : (float)(1d - dt / 0.1d);
        }
    }

    /// <summary>
    /// Snowstorm gust strength (0..1) at a time: slow swells with sharper bursts, used
    /// for the whiteout haze and drift opacity so the storm visibly breathes.
    /// </summary>
    public static float GetStormGust(double timeSeconds)
    {
        var swell = 0.5d + (0.5d * Math.Sin(0.31d * timeSeconds));
        var burst = Math.Max(0d, Math.Sin((1.07d * timeSeconds) + 0.6d));
        return (float)Math.Clamp((0.65d * swell) + (0.35d * burst * burst), 0d, 1d);
    }

    private readonly struct ClipRect
    {
        public ClipRect(float left, float top, float right, float bottom, float cameraX, float cameraY)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            CameraX = cameraX;
            CameraY = cameraY;
        }

        public float Left { get; }
        public float Top { get; }
        public float Right { get; }
        public float Bottom { get; }
        public float CameraX { get; }
        public float CameraY { get; }
    }

    private void EmitRainLayer<TSink>(
        ref TSink sink,
        Layer layer,
        int count,
        WeatherSkyMask? skyMask,
        in ClipRect clip,
        double time,
        MapWeatherWind wind,
        bool storm)
        where TSink : struct, IWeatherPixelSink
    {
        var spec = layer.Spec;
        var tileWidth = spec.TileWidth;
        var tileHeight = spec.TileHeight;
        // A thunderstorm always drives the rain hard; calm picks a direction.
        var baseSlant = (wind, storm) switch
        {
            (MapWeatherWind.Left, true) => -0.55f,
            (_, true) => 0.5f,
            (MapWeatherWind.Left, _) => -0.34f,
            (MapWeatherWind.Right, _) => 0.34f,
            _ => 0.05f,
        };
        var gustScale = (storm ? 3f : wind == MapWeatherWind.Calm ? 1f : 2.2f) * spec.WindFactor;
        var gustOffset = (float)GustOffset(time) * gustScale;
        var gustVelocity = (float)GustVelocity(time) * gustScale;
        var splashDistanceMax = spec.FallSpeed * 1.2f * RainSplashSeconds;
        var layerColor = spec.Color;
        if (_rainColor is { } rainColor)
        {
            // Keep the layer's depth dimming relative to the nearest layer.
            var front = RainLayers[0].Color;
            var dim = (spec.Color.R + spec.Color.G + spec.Color.B) / (float)Math.Max(1, front.R + front.G + front.B);
            layerColor = new Color(rainColor.ToVector3() * dim);
        }

        var horizontalMargin = 4f + MathF.Abs(baseSlant) * (spec.LengthMax + splashDistanceMax) + 3f;
        var scanLeft = clip.Left - horizontalMargin;
        var scanRight = clip.Right + horizontalMargin;
        var scanTop = clip.Top;
        var scanBottom = clip.Bottom + spec.LengthMax + splashDistanceMax;

        for (var index = 0; index < count; index += 1)
        {
            var speed = layer.Speed[index];
            var vx = baseSlant * speed;
            var slant = (vx + gustVelocity) / speed;
            var length = layer.Length[index];
            var alpha = layer.Alpha[index];
            var impacts = (layer.Flags[index] & FlagImpact) != 0;
            var splashDistance = speed * RainSplashSeconds;

            // Continuous (unwrapped) position; tile instances are offsets of it.
            var xContinuous = layer.BaseX[index] + (vx * time) + gustOffset;
            var yContinuous = layer.BaseY[index] + (speed * time);
            var firstX = scanLeft + PositiveModulo(xContinuous - scanLeft, tileWidth);
            var firstY = scanTop + PositiveModulo(yContinuous - scanTop, tileHeight);
            var color = layerColor * alpha;

            for (var headX = firstX; headX <= scanRight; headX += tileWidth)
            {
                for (var headY = firstY; headY <= scanBottom; headY += tileHeight)
                {
                    float ground;
                    if (skyMask is null)
                    {
                        // Top-down: each drop lands at its own depth within the tile cycle.
                        var cycle = PositiveModulo(yContinuous, tileHeight) / tileHeight;
                        ground = headY - ((float)cycle - layer.LandFraction[index]) * tileHeight;
                    }
                    else
                    {
                        ground = skyMask.GroundAt(headX);
                    }

                    if (headY < ground)
                    {
                        var skyTop = skyMask is null ? clip.Top : MathF.Max(clip.Top, skyMask.SkyStartAt(headX));
                        DrawRainStreak(ref sink, headX, headY, length, slant, skyTop, MathF.Min(ground, clip.Bottom), color, clip);
                    }
                    else if (impacts)
                    {
                        var fallen = headY - ground;
                        if (fallen < splashDistance)
                        {
                            // Where the drop actually hit, rewound along its path.
                            var landX = headX - (fallen * slant);
                            if (skyMask is null || MathF.Abs(skyMask.GroundAt(landX) - ground) < 2f)
                            {
                                DrawRainSplash(ref sink, landX, ground, fallen / splashDistance, color, clip);
                            }
                        }
                    }

                    if (_rectanglesThisFrame >= MaxRectanglesPerFrame)
                    {
                        return;
                    }
                }
            }
        }
    }

    private void EmitDriftLayer<TSink>(
        ref TSink sink,
        Layer layer,
        int count,
        WeatherSkyMask? skyMask,
        in ClipRect clip,
        double time,
        MapWeatherWind wind,
        DriftStyle style,
        bool storm)
        where TSink : struct, IWeatherPixelSink
    {
        var spec = layer.Spec;
        var tileWidth = spec.TileWidth;
        var tileHeight = spec.TileHeight;
        var baseDrift = (style, wind) switch
        {
            // Snowstorms and gales always blow; calm picks a direction.
            (DriftStyle.StormSnow, MapWeatherWind.Left) => -190f,
            (DriftStyle.StormSnow, MapWeatherWind.Right) => 190f,
            (DriftStyle.StormSnow, _) => 150f,
            (DriftStyle.Leaves, MapWeatherWind.Left) => storm ? -160f : -45f,
            (DriftStyle.Leaves, MapWeatherWind.Right) => storm ? 160f : 45f,
            (DriftStyle.Leaves, _) => storm ? 120f : 10f,
            (_, MapWeatherWind.Left) => -36f,
            (_, MapWeatherWind.Right) => 36f,
            _ => 4f,
        };
        var windDirection = MathF.Sign(baseDrift);
        var gustScale = style switch
        {
            DriftStyle.StormSnow => 3.6f,
            DriftStyle.Leaves => storm ? 3.2f : wind == MapWeatherWind.Calm ? 2f : 3f,
            _ => wind == MapWeatherWind.Calm ? 1.4f : 2.4f,
        } * spec.WindFactor;
        var settleSeconds = style switch
        {
            DriftStyle.StormSnow => StormSnowSettleSeconds,
            DriftStyle.Leaves => LeafSettleSeconds,
            _ => SnowSettleSeconds,
        };
        // Leaves stall and drop as they flutter: a vertical wobble on top of the fall.
        var flutter = style == DriftStyle.Leaves ? spec.SwayAmplitude * 0.35f : 0f;
        var gustOffset = GustOffset(time) * gustScale;
        var margin = spec.SwayAmplitude + 8f;
        var scanLeft = clip.Left - margin;
        var scanRight = clip.Right + margin;
        var scanTop = clip.Top - 3f - flutter;
        var scanBottom = clip.Bottom + (spec.FallSpeed * 1.4f * settleSeconds) + flutter;

        for (var index = 0; index < count; index += 1)
        {
            var speed = layer.Speed[index];
            var phase = layer.Phase[index];
            var sway = spec.SwayAmplitude * (0.6f + 0.4f * layer.Length[index]);
            var drift = baseDrift * spec.WindFactor;
            var xContinuous = layer.BaseX[index] + (drift * time) + gustOffset
                + (sway * Math.Sin((spec.SwayFrequency * time) + phase));
            var yContinuous = layer.BaseY[index] + (speed * time)
                + (flutter * Math.Sin((2.1d * spec.SwayFrequency * time) + (phase * 1.7d)));
            var firstX = scanLeft + PositiveModulo(xContinuous - scanLeft, tileWidth);
            var firstY = scanTop + PositiveModulo(yContinuous - scanTop, tileHeight);
            var big = (layer.Flags[index] & FlagBigFlake) != 0;
            var impacts = (layer.Flags[index] & FlagImpact) != 0;
            var color = style == DriftStyle.Leaves
                ? LeafPalette[layer.Variant[index] % LeafPalette.Length] * (spec.Alpha * layer.Alpha[index])
                : spec.Color * layer.Alpha[index];
            var settleDistance = speed * settleSeconds;
            // Leaves tumble: 1.2-3.2 flips a second, faster in a gale.
            var tumble = style == DriftStyle.Leaves
                ? (int)PositiveModulo(((1.2d + (2d * layer.Length[index])) * (storm ? 1.8d : 1d) * time) + phase, 4f)
                : 0;
            // Leaf poses only occupy rows at or above the anchor row.
            var extent = style == DriftStyle.Leaves ? 1 : big ? 2 : spec.FlakeSize;

            for (var flakeX = firstX; flakeX <= scanRight; flakeX += tileWidth)
            {
                for (var flakeY = firstY; flakeY <= scanBottom; flakeY += tileHeight)
                {
                    float ground;
                    if (skyMask is null)
                    {
                        var cycle = PositiveModulo(yContinuous, tileHeight) / tileHeight;
                        ground = flakeY - ((float)cycle - layer.LandFraction[index]) * tileHeight;
                    }
                    else
                    {
                        ground = skyMask.GroundAt(flakeX);
                    }

                    var flakeBottom = flakeY + extent;
                    if (flakeBottom <= ground)
                    {
                        if (skyMask is not null && flakeY - (style == DriftStyle.Leaves ? 2f : 1f) < skyMask.SkyStartAt(flakeX))
                        {
                            continue;
                        }

                        switch (style)
                        {
                            case DriftStyle.Leaves:
                                DrawLeaf(ref sink, flakeX, flakeY, spec.FlakeSize, tumble, color, clip);
                                break;
                            case DriftStyle.StormSnow:
                                DrawStormFlake(ref sink, flakeX, flakeY, spec.FlakeSize, windDirection, color, clip);
                                break;
                            default:
                                DrawFlake(ref sink, flakeX, flakeY, spec.FlakeSize, big, color, clip);
                                break;
                        }
                    }
                    else if (impacts)
                    {
                        var fallen = flakeBottom - ground;
                        if (fallen < settleDistance)
                        {
                            // Rest where it touched down, then melt (snow) or fade late (leaves).
                            var landedAgo = fallen / speed;
                            var landX = flakeX - (float)(
                                (drift * landedAgo)
                                + (GustOffset(time) - GustOffset(time - landedAgo)) * gustScale
                                + sway * (Math.Sin((spec.SwayFrequency * time) + phase)
                                    - Math.Sin((spec.SwayFrequency * (time - landedAgo)) + phase)));
                            var landGround = skyMask?.GroundAt(landX) ?? ground;
                            if (MathF.Abs(landGround - ground) < 2f)
                            {
                                var progress = fallen / settleDistance;
                                if (style == DriftStyle.Leaves)
                                {
                                    var fade = Math.Clamp((1f - progress) * 3f, 0f, 1f);
                                    var lyingWidth = Math.Max(1, spec.FlakeSize);
                                    Fill(ref sink, landX - (lyingWidth / 2), landGround - 1f, lyingWidth, 1, color * fade, clip);
                                }
                                else
                                {
                                    var size = big ? 2 : spec.FlakeSize;
                                    Fill(ref sink, landX, landGround - size, size, size, color * (1f - progress), clip);
                                }
                            }
                        }
                    }

                    if (_rectanglesThisFrame >= MaxRectanglesPerFrame)
                    {
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Snowstorm whiteout, in two parts. A faint haze over open sky whose density rolls
    /// with the wind, drawn in world-anchored columns so its edges stay fixed against the
    /// terrain as gusts pass. Over it, wind-blown drift: long, faint streaks of powder that
    /// brighten as they pass through slanted gust fronts, so the storm reads as blowing
    /// snow instead of solid panels sliding across the screen.
    /// </summary>
    private void EmitStormWhiteout<TSink>(
        ref TSink sink,
        WeatherSkyMask? skyMask,
        in ClipRect clip,
        double time,
        MapWeatherWind wind,
        float densityScale)
        where TSink : struct, IWeatherPixelSink
    {
        var direction = wind == MapWeatherWind.Left ? -1f : 1f;
        var gust = GetStormGust(time);
        EmitStormHaze(ref sink, skyMask, clip, time, direction, gust);
        EmitStormDrift(ref sink, skyMask, clip, time, direction, gust, densityScale);
    }

    private void EmitStormHaze<TSink>(
        ref TSink sink,
        WeatherSkyMask? skyMask,
        in ClipRect clip,
        double time,
        float direction,
        float gust)
        where TSink : struct, IWeatherPixelSink
    {
        // Columns sit on a fixed world grid (coarser when zoomed far out), so each one
        // samples the same terrain every frame and only its opacity changes.
        var columnWidth = Math.Max(2, (int)MathF.Ceiling((clip.Right - clip.Left) / 400f));
        var strength = 0.02f + (0.05f * gust);
        for (var x = MathF.Floor(clip.Left / columnWidth) * columnWidth; x < clip.Right; x += columnWidth)
        {
            var centre = x + (columnWidth * 0.5f);
            var density = (0.65f * ValueNoise((centre - (direction * 240d * time)) / 520d))
                + (0.35f * ValueNoise(((centre - (direction * 330d * time)) / 190d) + 17.3d));
            var alpha = strength * (0.3f + (0.7f * density));
            var top = clip.Top;
            var bottom = clip.Bottom;
            if (skyMask is not null)
            {
                var columnRight = x + columnWidth - 1f;
                top = MathF.Max(top, MathF.Max(skyMask.SkyStartAt(x), skyMask.SkyStartAt(columnRight)));
                bottom = MathF.Min(bottom, MathF.Min(skyMask.HazeBottomAt(x), skyMask.HazeBottomAt(columnRight)));
            }

            FillColumnAroundHiddenRegions(ref sink, x, top, bottom, columnWidth, StormSheetColor * alpha, clip);
            if (_rectanglesThisFrame >= MaxRectanglesPerFrame)
            {
                return;
            }
        }
    }

    private void EmitStormDrift<TSink>(
        ref TSink sink,
        WeatherSkyMask? skyMask,
        in ClipRect clip,
        double time,
        float direction,
        float gust,
        float densityScale)
        where TSink : struct, IWeatherPixelSink
    {
        const float PeriodX = 1100f;
        const float PeriodY = 600f;
        var count = (int)(DriftWispCount * Math.Clamp(densityScale, 0f, 1f));
        var gustAlpha = 0.55f + (0.45f * gust);
        for (var index = 0; index < count; index += 1)
        {
            var seed = Hash(unchecked((uint)(index + 1) * 0x3C6EF372u));
            var baseX = Unit(seed) * PeriodX;
            var baseY = Unit(Hash(seed)) * PeriodY;
            var speed = 300f + (Unit(Hash(seed ^ 0x51u)) * 160f);
            var fall = 30f + (Unit(Hash(seed ^ 0x77u)) * 50f);
            var length = 50f + (Unit(Hash(seed ^ 0x3Cu)) * 100f);
            var height = 1 + (int)(Unit(Hash(seed ^ 0x11u)) * 2.99f);
            var baseAlpha = 0.07f + (0.08f * Unit(Hash(seed ^ 0xA5u)));
            var phase = Unit(Hash(seed ^ 0x99u)) * MathF.Tau;

            var scanLeft = clip.Left - length;
            var scanTop = clip.Top - 3f;
            var firstX = scanLeft + PositiveModulo(baseX + (direction * speed * time) - scanLeft, PeriodX);
            var firstY = scanTop + PositiveModulo(baseY + (fall * time) + (10d * Math.Sin((0.9d * time) + phase)) - scanTop, PeriodY);
            for (var headX = firstX; headX < clip.Right + length; headX += PeriodX)
            {
                for (var headY = firstY; headY < clip.Bottom; headY += PeriodY)
                {
                    var front = GetDriftFront(headX, headY, time, direction, gust);
                    if (front <= 0.02f)
                    {
                        continue;
                    }

                    DrawDriftWisp(ref sink, skyMask, headX, headY, length, height, direction, StormSheetColor * (baseAlpha * front * gustAlpha), clip);
                    if (_rectanglesThisFrame >= MaxRectanglesPerFrame)
                    {
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 0..1 strength of the gust front at a point. Fronts are slanted bands that travel
    /// with the wind; stronger gusts widen them. Smooth everywhere, so a wisp fades in and
    /// out as it crosses one instead of popping.
    /// </summary>
    private static float GetDriftFront(float x, float y, double time, float direction, float gust)
    {
        var broad = ValueNoise(((x - (direction * 260d * time) + (0.55d * y)) / 420d) + 2.1d);
        var fine = ValueNoise(((x - (direction * 340d * time) - (0.3d * y)) / 230d) + 11.7d);
        return SmoothStep(0.38f - (0.2f * gust), 0.85f, (0.7f * broad) + (0.3f * fine));
    }

    /// <summary>A drift wisp: four segments with tapered ends, trailing behind the head.</summary>
    private void DrawDriftWisp<TSink>(
        ref TSink sink,
        WeatherSkyMask? skyMask,
        float headX,
        float y,
        float length,
        int height,
        float direction,
        Color color,
        in ClipRect clip)
        where TSink : struct, IWeatherPixelSink
    {
        var left = direction >= 0f ? headX - length : headX;
        var segment = length / DriftWispShape.Length;
        var segmentWidth = (int)MathF.Ceiling(segment);
        for (var part = 0; part < DriftWispShape.Length; part += 1)
        {
            var segmentLeft = left + (segment * part);
            var partColor = color * DriftWispShape[part];
            if (skyMask is null
                || (IsOpenAir(skyMask, segmentLeft, y, height)
                    && IsOpenAir(skyMask, segmentLeft + (segmentWidth * 0.5f), y, height)
                    && IsOpenAir(skyMask, segmentLeft + segmentWidth - 1f, y, height)))
            {
                Fill(ref sink, segmentLeft, y, segmentWidth, height, partColor, clip);
                continue;
            }

            // Partly blocked by a wall or ledge edge: keep only the open 4px pieces, so the
            // wisp is cut cleanly at the terrain rather than blinking in and out whole.
            for (var pieceLeft = segmentLeft; pieceLeft < segmentLeft + segmentWidth; pieceLeft += 4f)
            {
                var pieceWidth = (int)MathF.Min(4f, segmentLeft + segmentWidth - pieceLeft);
                if (pieceWidth < 1)
                {
                    break;
                }

                if (IsOpenAir(skyMask, pieceLeft, y, height) && IsOpenAir(skyMask, pieceLeft + pieceWidth - 1f, y, height))
                {
                    Fill(ref sink, pieceLeft, y, pieceWidth, height, partColor, clip);
                }
            }
        }
    }

    /// <summary>True when a 1px-wide run of the given height at x is open sky above the first surface.</summary>
    private static bool IsOpenAir(WeatherSkyMask skyMask, float x, float y, int height) =>
        y >= skyMask.SkyStartAt(x) && y + height <= skyMask.GroundAt(x);

    /// <summary>Fills a vertical span, leaving gaps wherever it crosses a hidden (indoor) region.</summary>
    private void FillColumnAroundHiddenRegions<TSink>(
        ref TSink sink,
        float x,
        float top,
        float bottom,
        int width,
        Color color,
        in ClipRect clip)
        where TSink : struct, IWeatherPixelSink
    {
        var current = top;
        while (bottom - current >= 1f)
        {
            var gapEnd = bottom;
            var resume = bottom;
            if (_hiddenRegions is { } hidden)
            {
                for (var index = 0; index < hidden.Count; index += 1)
                {
                    var region = hidden[index];
                    if (x >= region.Right || x + width <= region.Left || region.Bottom <= current || region.Top >= bottom)
                    {
                        continue;
                    }

                    var regionTop = MathF.Max(current, region.Top);
                    if (regionTop < gapEnd)
                    {
                        gapEnd = regionTop;
                        resume = region.Bottom;
                    }
                }
            }

            if (gapEnd - current >= 1f)
            {
                Fill(ref sink, x, current, width, (int)(gapEnd - current), color, clip);
            }

            current = resume;
        }
    }

    /// <summary>Smooth 1D value noise in 0..1.</summary>
    private static float ValueNoise(double x)
    {
        var cell = Math.Floor(x);
        var fraction = (float)(x - cell);
        var key = unchecked((uint)(long)cell);
        var a = Unit(Hash(key));
        var b = Unit(Hash(unchecked(key + 1u)));
        var t = fraction * fraction * (3f - (2f * fraction));
        return a + ((b - a) * t);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>
    /// Spindrift: powder skimming along the ground in short hops, driven by the storm.
    /// Anchored to the open-sky surface, so it only appears where snow would lie.
    /// </summary>
    private void EmitSpindrift<TSink>(
        ref TSink sink,
        WeatherSkyMask skyMask,
        in ClipRect clip,
        double time,
        MapWeatherWind wind,
        float densityScale)
        where TSink : struct, IWeatherPixelSink
    {
        const float Period = 509f;
        var particles = (int)(70 * Math.Clamp(densityScale, 0f, 1f));
        var direction = wind == MapWeatherWind.Left ? -1f : 1f;
        var gust = 0.5f + (0.5f * GetStormGust(time));
        for (var index = 0; index < particles; index += 1)
        {
            var seed = Hash(unchecked((uint)(index + 1) * 0x2C1B3C6Du));
            var baseX = Unit(seed) * Period;
            var speed = 240f + (Unit(Hash(seed)) * 140f);
            var hopFrequency = 3f + (Unit(Hash(seed ^ 0x9E37u)) * 4f);
            var hopHeight = 1f + (Unit(Hash(seed ^ 0x7F4Au)) * 5f * gust);
            var phase = Unit(Hash(seed ^ 0x1234u)) * MathF.Tau;
            var alpha = (0.35f + (0.45f * Unit(Hash(seed ^ 0xBEEFu)))) * gust;
            var firstX = clip.Left + PositiveModulo(baseX + (direction * speed * time) - clip.Left, Period);
            for (var x = firstX; x < clip.Right; x += Period)
            {
                var ground = MathF.Min(skyMask.GroundAt(x), skyMask.GroundAt(x + 1f));
                if (ground >= skyMask.Height - 0.5f || ground < clip.Top || ground > clip.Bottom + 1f)
                {
                    continue;
                }

                var hop = hopHeight * MathF.Abs((float)Math.Sin((hopFrequency * time) + phase));
                Fill(ref sink, x, ground - 1f - hop, 2, 1, StormSheetColor * alpha, clip);
                if (_rectanglesThisFrame >= MaxRectanglesPerFrame)
                {
                    return;
                }
            }
        }
    }

    private void DrawRainStreak<TSink>(
        ref TSink sink,
        float headX,
        float headY,
        float length,
        float slant,
        float clipTop,
        float clipBottom,
        Color color,
        in ClipRect clip)
        where TSink : struct, IWeatherPixelSink
    {
        var tailY = headY - length;
        var visibleHeadY = MathF.Min(headY, clipBottom);
        var visibleTailY = MathF.Max(tailY, clipTop);
        if (visibleHeadY - visibleTailY < 1f)
        {
            return;
        }

        // Stair-stepped 1px streak: one segment per pixel of horizontal travel, the
        // look of a hand-drawn diagonal rather than an anti-aliased line.
        var spanX = MathF.Abs(slant * length);
        var segments = Math.Clamp((int)MathF.Round(spanX) + 1, 1, 4);
        var segmentLength = length / segments;
        for (var segment = 0; segment < segments; segment += 1)
        {
            var segmentTop = tailY + (segment * segmentLength);
            var segmentBottom = segmentTop + segmentLength;
            var top = MathF.Max(segmentTop, visibleTailY);
            var bottom = MathF.Min(segmentBottom, visibleHeadY);
            if (bottom <= top)
            {
                continue;
            }

            var segmentX = headX - (slant * (headY - ((segmentTop + segmentBottom) * 0.5f)));
            var pixelTop = MathF.Floor(top);
            var pixelHeight = (int)(bottom - pixelTop);
            if (pixelHeight >= 1)
            {
                Fill(ref sink, segmentX, pixelTop, 1, pixelHeight, color, clip);
            }
        }
    }

    private void DrawRainSplash<TSink>(
        ref TSink sink,
        float x,
        float ground,
        float progress,
        Color color,
        in ClipRect clip)
        where TSink : struct, IWeatherPixelSink
    {
        if (progress < 0.28f)
        {
            // Impact: a flat 3px blip on the surface.
            Fill(ref sink, x - 1f, ground - 1f, 3, 1, color, clip);
            return;
        }

        // Two droplets hop outwards and fall back.
        var t = (progress - 0.28f) / 0.72f;
        var spread = 1f + (t * 2.5f);
        var rise = MathF.Sin(t * MathF.PI) * 2.5f;
        var droplet = color * (1f - (t * 0.8f));
        Fill(ref sink, x - spread, ground - 1f - rise, 1, 1, droplet, clip);
        Fill(ref sink, x + spread, ground - 1f - rise, 1, 1, droplet, clip);
    }

    /// <summary>A storm flake streaks with the wind: bright head, dimmer trailing pixels.</summary>
    private void DrawStormFlake<TSink>(
        ref TSink sink,
        float x,
        float y,
        int size,
        float windDirection,
        Color color,
        in ClipRect clip)
        where TSink : struct, IWeatherPixelSink
    {
        var trail = windDirection >= 0f ? -1f : 1f;
        if (size >= 2)
        {
            Fill(ref sink, x, y, 2, 1, color, clip);
            Fill(ref sink, x + (trail * 2f), y, 2, 1, color * 0.55f, clip);
            Fill(ref sink, x + (trail * 4f), y, 2, 1, color * 0.25f, clip);
            return;
        }

        Fill(ref sink, x, y, 1, 1, color, clip);
        Fill(ref sink, x + trail, y, 1, 1, color * 0.45f, clip);
    }

    /// <summary>
    /// A tumbling leaf: four hand-made pixel poses cycled as it flips, so it reads as
    /// rotating without any rotation maths.
    /// </summary>
    private void DrawLeaf<TSink>(
        ref TSink sink,
        float x,
        float y,
        int size,
        int tumble,
        Color color,
        in ClipRect clip)
        where TSink : struct, IWeatherPixelSink
    {
        var shade = new Color((int)(color.R * 0.7f), (int)(color.G * 0.7f), (int)(color.B * 0.7f), color.A);
        if (size >= 4)
        {
            switch (tumble)
            {
                case 0:
                    Fill(ref sink, x - 2f, y, 4, 1, color, clip);
                    Fill(ref sink, x - 1f, y - 1f, 2, 1, shade, clip);
                    break;
                case 1:
                    Fill(ref sink, x - 2f, y - 2f, 2, 1, color, clip);
                    Fill(ref sink, x - 1f, y - 1f, 2, 1, color, clip);
                    Fill(ref sink, x, y, 2, 1, shade, clip);
                    break;
                case 2:
                    Fill(ref sink, x, y - 2f, 1, 3, shade, clip);
                    break;
                default:
                    Fill(ref sink, x, y - 2f, 2, 1, shade, clip);
                    Fill(ref sink, x - 1f, y - 1f, 2, 1, color, clip);
                    Fill(ref sink, x - 2f, y, 2, 1, color, clip);
                    break;
            }

            return;
        }

        if (size >= 3)
        {
            switch (tumble)
            {
                case 0: // flat, curled tip
                    Fill(ref sink, x - 1f, y, 3, 1, color, clip);
                    Fill(ref sink, x + 1f, y - 1f, 1, 1, shade, clip);
                    break;
                case 1: // falling diagonal
                    Fill(ref sink, x - 1f, y - 1f, 2, 1, color, clip);
                    Fill(ref sink, x, y, 2, 1, shade, clip);
                    break;
                case 2: // edge-on: a thin sliver
                    Fill(ref sink, x, y - 1f, 1, 2, shade, clip);
                    break;
                default: // other diagonal
                    Fill(ref sink, x, y - 1f, 2, 1, shade, clip);
                    Fill(ref sink, x - 1f, y, 2, 1, color, clip);
                    break;
            }

            return;
        }

        if (size == 2)
        {
            if ((tumble & 1) == 0)
            {
                Fill(ref sink, x, y, 2, 1, color, clip);
            }
            else
            {
                Fill(ref sink, x, y - 1f, 1, 2, color, clip);
            }

            return;
        }

        Fill(ref sink, x, y, 1, 1, color, clip);
    }

    private void DrawFlake<TSink>(
        ref TSink sink,
        float x,
        float y,
        int size,
        bool big,
        Color color,
        in ClipRect clip)
        where TSink : struct, IWeatherPixelSink
    {
        if (big)
        {
            // Plus-shaped 3px flake.
            Fill(ref sink, x - 1f, y, 3, 1, color, clip);
            Fill(ref sink, x, y - 1f, 1, 1, color, clip);
            Fill(ref sink, x, y + 1f, 1, 1, color, clip);
            return;
        }

        Fill(ref sink, x, y, size, size, color, clip);
    }

    private void Fill<TSink>(ref TSink sink, float worldX, float worldY, int width, int height, Color color, in ClipRect clip)
        where TSink : struct, IWeatherPixelSink
    {
        if (_rectanglesThisFrame >= MaxRectanglesPerFrame
            || worldX + width <= clip.Left || worldX >= clip.Right || worldY + height <= clip.Top || worldY >= clip.Bottom)
        {
            return;
        }

        if (_hiddenRegions is { } hidden && IsInsideAny(hidden, worldX, worldY, width, height))
        {
            return;
        }

        _rectanglesThisFrame += 1;
        // Floor, not round: a rectangle never reaches below the float position plus
        // its height, so nothing pokes into the surface it lands on.
        sink.Fill(
            (int)MathF.Floor(worldX - clip.CameraX),
            (int)MathF.Floor(worldY - clip.CameraY),
            width,
            height,
            color);
    }

    private static bool IsInsideAny(IReadOnlyList<IndoorRegionMarker> regions, float x, float y, int width, int height)
    {
        for (var index = 0; index < regions.Count; index += 1)
        {
            var region = regions[index];
            if (x < region.Right && x + width > region.Left && y < region.Bottom && y + height > region.Top)
            {
                return true;
            }
        }

        return false;
    }

    private static Layer CreateLayer(LayerSpec spec, float amount, uint seed)
    {
        var count = Math.Max(1, (int)MathF.Round(spec.DensityPer10K * amount * spec.TileWidth * spec.TileHeight / 10000f));
        var layer = new Layer
        {
            Spec = spec,
            BaseX = new float[count],
            BaseY = new float[count],
            Speed = new float[count],
            Length = new float[count],
            Alpha = new float[count],
            Phase = new float[count],
            LandFraction = new float[count],
            Flags = new byte[count],
            Variant = new byte[count],
        };

        var state = seed;
        for (var index = 0; index < count; index += 1)
        {
            layer.BaseX[index] = Next(ref state) * spec.TileWidth;
            layer.BaseY[index] = Next(ref state) * spec.TileHeight;
            layer.Speed[index] = spec.FallSpeed * (1f + ((Next(ref state) * 2f) - 1f) * spec.SpeedJitter);
            var lengthRoll = Next(ref state);
            layer.Length[index] = spec.LengthMax > 0f
                ? spec.LengthMin + (lengthRoll * (spec.LengthMax - spec.LengthMin))
                : lengthRoll;
            layer.Alpha[index] = 0.75f + (Next(ref state) * 0.25f);
            layer.Phase[index] = Next(ref state) * MathF.Tau;
            layer.LandFraction[index] = 0.35f + (Next(ref state) * 0.6f);
            byte flags = 0;
            if (spec.Impacts && Next(ref state) < 0.7f)
            {
                flags |= FlagImpact;
            }

            if (spec.FlakeSize >= 2 && Next(ref state) < 0.25f)
            {
                flags |= FlagBigFlake;
            }

            layer.Flags[index] = flags;
            layer.Variant[index] = (byte)(Next(ref state) * 255f);
        }

        return layer;
    }

    // Wind gusts as a displacement whose derivative is the gust velocity, so drops
    // drift smoothly instead of jumping when the wind changes.
    private static double GustOffset(double time) =>
        (26d * Math.Sin(0.37d * time)) + (11d * Math.Sin((1.13d * time) + 1.7d));

    private static double GustVelocity(double time) =>
        (26d * 0.37d * Math.Cos(0.37d * time)) + (11d * 1.13d * Math.Cos((1.13d * time) + 1.7d));

    private static float PositiveModulo(double value, float modulus)
    {
        var result = value % modulus;
        if (result < 0d)
        {
            result += modulus;
        }

        return (float)result;
    }

    private static float Next(ref uint state)
    {
        state = Hash(unchecked(state + 0x6D2B79F5u));
        return Unit(state);
    }

    private static float Unit(uint hash) => (hash >> 8) * (1f / 16777216f);

    private static uint Hash(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
