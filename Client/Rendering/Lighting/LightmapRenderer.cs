#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Retro 2D lighting: a small light map (one texel per <see cref="CellSize"/> world
/// pixels) holding the ambient mood plus additive light sources, laid over the world
/// with a 2x multiply so the map can darken (values below one half) and brighten
/// (above one half). No shaders: everything is SpriteBatch blend states.
/// </summary>
/// <remarks>
/// <para>Frame flow: <see cref="Begin"/> and <see cref="AddLight"/> collect the frame's
/// lights; <see cref="Render"/> draws them into the render target and must run while
/// no sprite batch is active and before the logical frame target is bound (render-target
/// switches would discard it); <see cref="DrawMultiply"/> and <see cref="DrawGlow"/> are
/// then called from inside the world pass.</para>
/// <para>The light map origin is snapped to the world cell grid, so light bands stay put
/// on the map as the camera scrolls instead of swimming.</para>
/// </remarks>
internal sealed class LightmapRenderer : IDisposable
{
    /// <summary>Light map cell for retro-free (smooth) lighting, in world pixels.</summary>
    public const int CellSize = 1;

    /// <summary>
    /// Multisampling for the light map, so the hard edges in it (spotlight cones, light cut
    /// off by walls) come out anti-aliased instead of as a blurred staircase.
    /// </summary>
    private int _multiSampleCount = OperatingSystem.IsBrowser() ? 0 : 4;

    private static RenderTarget2D CreateTarget(GraphicsDevice device, int width, int height, int multiSampleCount) =>
        new(device, width, height, false, SurfaceFormat.Color, DepthFormat.None, multiSampleCount, RenderTargetUsage.PreserveContents);
    public const int MaxLights = 320;

    /// <summary>Largest light map edge; cells grow for huge views so the target stays small.</summary>
    private const int MaxTargetCells = 1800;
    private const int LightTextureSize = 64;

    /// <summary>Cone textures are larger so the angled edges stay clean on big lights.</summary>
    private const int ConeTextureSize = 128;

    /// <summary>Spotlight spreads are rounded to this step so a handful of cone textures covers them.</summary>
    private const int ConeSpreadStepDegrees = 10;

    /// <summary>result = 2 x light x world: 0.5 in the light map leaves the world unchanged.</summary>
    public static readonly BlendState Multiply2x = new()
    {
        Name = "OpenGarrison.Lighting.Multiply2x",
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.SourceColor,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    /// <summary>Plain addition; light textures carry their falloff in colour and alpha.</summary>
    public static readonly BlendState AddOne = new()
    {
        Name = "OpenGarrison.Lighting.AddOne",
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    /// <summary>Writes only destination alpha (the clip mask), replacing it.</summary>
    private static readonly BlendState WriteAlphaOnly = new()
    {
        Name = "OpenGarrison.Lighting.WriteAlphaOnly",
        ColorWriteChannels = ColorWriteChannels.Alpha,
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.Zero,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.Zero,
    };

    /// <summary>result = source x (1 - mask) + destination x mask, the mask in destination alpha.</summary>
    private static readonly BlendState LerpByDestinationAlpha = new()
    {
        Name = "OpenGarrison.Lighting.LerpByDestinationAlpha",
        ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green | ColorWriteChannels.Blue,
        ColorSourceBlend = Blend.InverseDestinationAlpha,
        ColorDestinationBlend = Blend.DestinationAlpha,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    private readonly List<LightSample> _lights = new();

    /// <summary>Laser beams this frame, as segments, for character rim light and shadows.</summary>
    private readonly List<LightSegment> _segments = new();

    /// <summary>How far from a laser beam a character still picks up its rim light, past the beam's width.</summary>
    private const float SegmentRimReach = 56f;

    private readonly record struct LightSegment(float X0, float Y0, float X1, float Y1, float HalfWidth, Color Color, float Intensity);

    /// <summary>Shapes (jungle foliage) that take only the ambient light, never the lights.</summary>
    private readonly List<(Texture2D Shape, Vector2 TopLeft, Vector2 Size, float Opacity)> _ambientOnlyShapes = new();
    private RenderTarget2D? _target;
    private RenderTarget2D? _bandTarget;
    private bool _bandedFrame;

    /// <summary>
    /// World-resolution light map, used when a light has a mask (a pixel-art shape): the
    /// coarse map is scaled up into it and mask lights are added at full resolution with
    /// point sampling, so their pixels stay sharp and square.
    /// </summary>
    private RenderTarget2D? _detailTarget;
    private RenderTarget2D? _paintedEmissionTarget;
    private RenderTarget2D? _outsideMaskGlowTarget;
    private readonly MaskGlowBuffer _maskGlowBuffer = new();
    private bool _paintedEmissionFrame;
    private bool _outsideMaskGlowFrame;
    private bool _detailFrame;
    private int _detailScale = 1;
    private int _detailWidth;
    private int _detailHeight;

    /// <summary>Largest detail map edge; zoomed far out, a detail texel covers several world pixels.</summary>
    private const int MaxDetailTexels = 2048;
    private Texture2D? _clipArt;
    private float _clipWorldWidth;
    private float _clipWorldHeight;
    private Texture2D? _smoothLight;
    private Texture2D? _bandedLight;
    private Texture2D? _vignette;
    private Texture2D? _white;
    private readonly Dictionary<int, Texture2D> _coneTextures = new();
    private GraphicsDevice? _device;
    private BasicEffect? _fanEffect;
    private VertexPositionColorTexture[] _fanVertices = new VertexPositionColorTexture[65];
    private short[] _fanIndices = new short[64 * 3];
    private MapLighting _lighting = MapLighting.None;
    private float _worldHeight = 1f;
    private float _ambientBoost;
    private double _time;
    private int _cellSize = CellSize;

    private readonly record struct LightSample(
        float X,
        float Y,
        float Radius,
        Color Color,
        float Intensity,
        bool Glows,
        MapLightFalloff Falloff,
        float DirectionDegrees,
        float SpreadDegrees,
        float[]? Reach,
        Texture2D? Mask,
        float GlowBoost = 0f,
        float MaskPixelSize = 0f,
        float MaskGlowRadius = 0f);

    /// <summary>World position of the light map's top-left texel.</summary>
    public Vector2 Origin { get; private set; }

    public int CellsWide { get; private set; }

    public int CellsHigh { get; private set; }

    /// <summary>True once <see cref="Render"/> produced a light map for the current frame.</summary>
    public bool HasFrame { get; private set; }

    public MapLighting Lighting => _lighting;

    private bool _hasGlowBoost;

    /// <summary>True when <see cref="DrawGlow"/> has anything to draw this frame.</summary>
    public bool HasGlow => HasFrame && (_lighting.Glow > 0 || _hasGlowBoost || _outsideMaskGlowFrame);

    /// <summary>Sharp painted pixels, drawn with map art before gameplay objects and foreground.</summary>
    public bool HasPaintedMaskEmission => HasFrame && _paintedEmissionFrame;

    /// <summary>
    /// The player's lighting intensity, 0..1: 1 is the map as authored; lower values ease the
    /// whole light map (ambient, lights, vignette, glow) toward unlit. Set before Begin.
    /// </summary>
    public float Intensity { get; set; } = 1f;

    private float ClampedIntensity => Math.Clamp(Intensity, 0f, 1f);

    /// <summary>A light map value eased toward unlit (0.5) by <see cref="Intensity"/>.</summary>
    private Vector3 ApplyIntensity(Vector3 value) => Vector3.Lerp(new Vector3(0.5f), value, ClampedIntensity);

    /// <summary>
    /// The LightBands shader. With it, retro maps render lights smooth and step the finished
    /// light map into crisp bands; without it they fall back to pre-banded light textures.
    /// </summary>
    public Effect? BandEffect { get; set; }

    /// <summary>True when the light map is drawn in retro style (sample it with point filtering).</summary>
    public bool IsBanded => _lighting.Banded;

    /// <summary>
    /// Whether to apply the light map with point sampling: retro bands, or a frame with
    /// masked lights (already at world resolution; filtering would blur their pixels).
    /// </summary>
    public bool PrefersPointSampling => _lighting.Banded || _detailFrame;

    /// <summary>The sampler to begin the <see cref="DrawGlow"/> batch with: stepped in retro style, smooth otherwise.</summary>
    public SamplerState GlowSampler => _lighting.Banded || _outsideMaskGlowFrame ? SamplerState.PointClamp : SamplerState.LinearClamp;

    /// <summary>
    /// The map art that retro bands are clipped to (when the map asks for it), drawn over
    /// (0,0)..(worldWidth, worldHeight). Where it is transparent, the light stays smooth.
    /// </summary>
    public void SetBandClipArt(Texture2D? art, float worldWidth, float worldHeight)
    {
        _clipArt = art;
        _clipWorldWidth = worldWidth;
        _clipWorldHeight = worldHeight;
    }

    public int LightCount => _lights.Count;

    /// <summary>Starts a frame for the visible world rectangle.</summary>
    /// <param name="ambientBoost">Extra ambient (0..1), e.g. a lightning flash.</param>
    public void Begin(
        MapLighting lighting,
        float viewLeft,
        float viewTop,
        float viewWidth,
        float viewHeight,
        float worldHeight,
        double time,
        float ambientBoost = 0f)
    {
        _lighting = lighting;
        _worldHeight = MathF.Max(1f, worldHeight);
        _time = time;
        _ambientBoost = Math.Clamp(ambientBoost, 0f, 1f);
        _lights.Clear();
        _segments.Clear();
        _hasGlowBoost = false;
        _paintedEmissionFrame = false;
        _outsideMaskGlowFrame = false;
        _ambientOnlyShapes.Clear();
        HasFrame = false;
        // Zoomed far out (large builder views), use coarser cells instead of a huge target.
        var largestEdge = MathF.Max(viewWidth, viewHeight);
        var baseCell = lighting.Banded
            ? Math.Clamp(lighting.BandPixel, MapLightingMetadata.MinBandPixel, MapLightingMetadata.MaxBandPixel)
            : CellSize;
        _cellSize = Math.Max(baseCell, (int)MathF.Ceiling(largestEdge / MaxTargetCells));
        var originX = MathF.Floor(viewLeft / _cellSize) * _cellSize;
        var originY = MathF.Floor(viewTop / _cellSize) * _cellSize;
        Origin = new Vector2(originX, originY);
        CellsWide = Math.Max(1, (int)MathF.Ceiling((viewLeft + viewWidth - originX) / _cellSize) + 1);
        CellsHigh = Math.Max(1, (int)MathF.Ceiling((viewTop + viewHeight - originY) / _cellSize) + 1);
    }

    /// <summary>Queues a light in world coordinates; intensity 1 lights its area to full colour.</summary>
    /// <param name="falloff">Edge fade for this light; <see cref="MapLightFalloff.Map"/> follows the map style.</param>
    /// <param name="directionDegrees">Spotlight aim: 0 = right, 90 = up, 180 = left, 270 = down.</param>
    /// <param name="spreadDegrees">Spotlight cone width; 360 is an ordinary round light.</param>
    /// <param name="reach">
    /// Optional per-ray reach from <see cref="LightOcclusionField.ComputeVisibility"/>: walls
    /// cut the light off where its rays stop. Null lights the full circle.
    /// </param>
    public void AddLight(
        float x,
        float y,
        float radius,
        Color color,
        float intensity,
        bool glows = true,
        MapLightFalloff falloff = MapLightFalloff.Map,
        float directionDegrees = 0f,
        float spreadDegrees = MapLightMetadata.FullSpread,
        float[]? reach = null,
        Texture2D? mask = null,
        float glowBoost = 0f,
        float maskPixelSize = 0f,
        float maskGlowRadius = 0f)
    {
        intensity *= ClampedIntensity;
        glowBoost *= ClampedIntensity;
        maskGlowRadius = float.IsFinite(maskGlowRadius) ? MathF.Max(0f, maskGlowRadius) : 0f;
        if (maskPixelSize > 0f && mask is { IsDisposed: false })
            radius = MathF.Max(radius, MathF.Sqrt((float)mask.Width * mask.Width + (float)mask.Height * mask.Height) * maskPixelSize * 0.5f + maskPixelSize);
        if (_lights.Count >= MaxLights || radius <= 0.5f || intensity <= 0.004f)
        {
            return;
        }

        // Imported masks can be square and rotated; their corners extend beyond
        // the nominal round radius, especially after snapping their pixel scale.
        var cullRadius = radius * (mask is { IsDisposed: false } && maskPixelSize <= 0f ? 2f : 1f) + maskGlowRadius;
        var left = Origin.X - cullRadius;
        var top = Origin.Y - cullRadius;
        var right = Origin.X + (CellsWide * _cellSize) + cullRadius;
        var bottom = Origin.Y + (CellsHigh * _cellSize) + cullRadius;
        if (x < left || x > right || y < top || y > bottom)
        {
            return;
        }

        _lights.Add(new LightSample(x, y, radius, color, intensity, glows, falloff, directionDegrees, spreadDegrees, reach, mask, Math.Clamp(glowBoost, 0f, 1f), maskPixelSize, maskGlowRadius));
        if (glowBoost > 0f)
        {
            _hasGlowBoost = true;
        }
    }

    /// <summary>
    /// Registers a laser beam (already clipped) so characters near it are rim-lit from the
    /// beam's nearest point. It adds nothing to the light map itself.
    /// </summary>
    public void AddLightSegment(float x0, float y0, float x1, float y1, float width, Color color, float intensity)
    {
        intensity *= ClampedIntensity;
        if (intensity <= 0.004f || (MathF.Abs(x1 - x0) + MathF.Abs(y1 - y0)) < 0.5f)
        {
            return;
        }

        _segments.Add(new LightSegment(x0, y0, x1, y1, MathF.Max(0.5f, width * 0.5f), color, intensity));
    }

    /// <summary>
    /// Keeps lights off a shape: where the shape is solid, the light map holds only the
    /// map's ambient light (sky to ground), so lamps, flames and player lights do not shine
    /// on it. Used for jungle foliage. The shape is a white alpha mask; opacity scales it.
    /// </summary>
    public void AddAmbientOnlyShape(Texture2D shape, float left, float top, float width, float height, float opacity)
    {
        if (shape.IsDisposed || width <= 0f || height <= 0f || opacity <= 0.004f)
        {
            return;
        }

        var right = Origin.X + (CellsWide * _cellSize);
        var bottom = Origin.Y + (CellsHigh * _cellSize);
        if (left + width < Origin.X || top + height < Origin.Y || left > right || top > bottom)
        {
            return;
        }

        _ambientOnlyShapes.Add((shape, new Vector2(left, top), new Vector2(width, height), Math.Clamp(opacity, 0f, 1f)));
    }

    /// <summary>
    /// The light map value of the ambient light alone at a height (0.5 = unchanged world),
    /// matching the ambient rows drawn in <see cref="Render"/>.
    /// </summary>
    private Vector3 GetAmbientLightValue(float worldY, bool roundToRows)
    {
        var sky = ToVector(_lighting.SkyTint);
        var ground = ToVector(_lighting.GroundTint);
        var peak = MathF.Max(MaxChannel(sky), MaxChannel(ground));
        var normalize = peak > (1f / 255f) ? 1f / peak : 1f;
        var level = ((_lighting.Brightness / 100f * GetPulseFactor()) + (_ambientBoost * 0.6f)) * normalize;
        var reach = Math.Clamp(_lighting.SkyReach, 0, MapLightingMetadata.MaxSkyReach) / 100f;
        var t = Math.Clamp(((worldY / _worldHeight) - reach) / (1f - reach), 0f, 1f);
        if (roundToRows)
        {
            t = MathF.Round(t * 8f) / 8f;
        }

        return ApplyIntensity(Vector3.Lerp(sky, ground, t) * level * 0.5f);
    }

    /// <summary>
    /// Roughly how brightly this frame's light map lights a point: 0 is black, 1 is the
    /// world at its own colour (or brighter). Ambient plus every light that reaches the point
    /// (cones and walls respected, masks treated as round). For effects that fade in the
    /// dark, such as character brightmaps; 1 when there is no light map this frame.
    /// </summary>
    public float EstimateBrightness(float x, float y)
    {
        if (!HasFrame)
        {
            return 1f;
        }

        var value = GetAmbientLightValue(y, roundToRows: false);
        foreach (var light in _lights)
        {
            // Painted pixels and their bloom belong to the background artwork layer.
            // They must not brighten character surfaces or character brightmaps.
            if (light.MaskPixelSize > 0f) continue;
            var deltaX = x - light.X;
            var deltaY = y - light.Y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= light.Radius * light.Radius)
            {
                continue;
            }

            var distance = MathF.Sqrt(distanceSquared);
            if (distance > 0.5f && MapLightMetadata.IsDirectional(light.SpreadDegrees))
            {
                var aim = MapLightMetadata.GetDirectionVector(light.DirectionDegrees);
                var cosine = ((deltaX * aim.X) + (deltaY * aim.Y)) / distance;
                if (cosine < MathF.Cos(light.SpreadDegrees * (MathF.PI / 360f)))
                {
                    continue;
                }
            }

            if (light.Reach is { } reach && !LightOcclusionField.Reaches(reach, light.X, light.Y, x, y))
            {
                continue;
            }

            var fade = 1f - (distance / light.Radius);
            value += light.Color.ToVector3() * (light.Intensity * 0.5f * fade * fade);
        }

        // The light map is applied doubled (0.5 leaves the world unchanged).
        var lit = value * 2f;
        return Math.Clamp((lit.X * 0.299f) + (lit.Y * 0.587f) + (lit.Z * 0.114f), 0f, 1f);
    }

    /// <summary>
    /// The light that reaches a point most strongly this frame (for character rim light
    /// and cast shadows). Player lights (non-glowing) and lights sitting on the point
    /// itself are ignored, and walls block lights that carry ray reach.
    /// </summary>
    /// <param name="toLight">Unit vector from the point toward the light (screen space, y down).</param>
    /// <param name="strength">0..1 by intensity, distance and spotlight cone.</param>
    public bool TryGetDominantLight(float x, float y, out Vector2 toLight, out Color color, out float strength)
    {
        toLight = Vector2.Zero;
        color = Color.White;
        strength = 0f;
        foreach (var light in _lights)
        {
            if (!light.Glows || light.MaskPixelSize > 0f)
            {
                continue;
            }

            var deltaX = light.X - x;
            var deltaY = light.Y - y;
            var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance < 10f || distance >= light.Radius)
            {
                continue;
            }

            var contribution = light.Intensity * (1f - (distance / light.Radius));
            if (MapLightMetadata.IsDirectional(light.SpreadDegrees))
            {
                // Outside the cone the light does not reach.
                var aim = MapLightMetadata.GetDirectionVector(light.DirectionDegrees);
                var cosine = ((-deltaX * aim.X) + (-deltaY * aim.Y)) / distance;
                var halfSpread = light.SpreadDegrees * (MathF.PI / 360f);
                if (cosine < MathF.Cos(halfSpread))
                {
                    continue;
                }
            }

            if (light.Reach is { } reach && !LightOcclusionField.Reaches(reach, light.X, light.Y, x, y))
            {
                continue;
            }

            var candidate = Math.Clamp(contribution * 1.6f, 0f, 1f);
            if (candidate > strength)
            {
                strength = candidate;
                toLight = new Vector2(deltaX / distance, deltaY / distance);
                color = light.Color;
            }
        }

        // Laser beams light a character from the nearest point along the beam.
        foreach (var segment in _segments)
        {
            var segmentX = segment.X1 - segment.X0;
            var segmentY = segment.Y1 - segment.Y0;
            var lengthSquared = (segmentX * segmentX) + (segmentY * segmentY);
            var t = lengthSquared > 0f
                ? Math.Clamp((((x - segment.X0) * segmentX) + ((y - segment.Y0) * segmentY)) / lengthSquared, 0f, 1f)
                : 0f;
            var deltaX = segment.X0 + (segmentX * t) - x;
            var deltaY = segment.Y0 + (segmentY * t) - y;
            var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            var reach = SegmentRimReach + segment.HalfWidth;
            if (distance >= reach)
            {
                continue;
            }

            var candidate = Math.Clamp(segment.Intensity * (1f - (distance / reach)) * 1.6f, 0f, 1f);
            if (candidate <= strength)
            {
                continue;
            }

            strength = candidate;
            color = segment.Color;
            // A beam passing straight through lights the body from its own direction.
            toLight = distance > 0.5f
                ? new Vector2(deltaX / distance, deltaY / distance)
                : Vector2.Normalize(new Vector2(-segmentY, segmentX) + new Vector2(0.0001f, 0f));
        }

        return strength > 0.01f;
    }

    /// <summary>
    /// Draws ambient and lights into the light map. Call with no sprite batch active and
    /// before the frame's main render target is bound; leaves the back buffer bound.
    /// </summary>
    public void Render(GraphicsDevice device, SpriteBatch batch)
    {
        HasFrame = false;
        if (!_lighting.IsActive)
        {
            return;
        }

        EnsureResources(device);
        // Retro with the shader: render smooth, then step the finished map (crisp contours).
        var quantize = _lighting.Banded && BandEffect is { IsDisposed: false };
        device.SetRenderTarget(_target);
        device.Clear(Color.Black);

        // Ambient: horizontal rows lerping sky -> ground over the map's height. Sky reach keeps
        // the top part at the full sky light and fades over what is left below it.
        batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        var pulse = GetPulseFactor();
        var sky = ToVector(_lighting.SkyTint);
        var ground = ToVector(_lighting.GroundTint);
        // Tints pick the colour, brightness picks the level: scale the tints so their
        // brightest channel counts as full. Otherwise a dark tint (night blue) kept the map
        // dark even at full brightness. The sky/ground ratio is kept.
        var peak = MathF.Max(MaxChannel(sky), MaxChannel(ground));
        var normalize = peak > (1f / 255f) ? 1f / peak : 1f;
        var level = ((_lighting.Brightness / 100f * pulse) + (_ambientBoost * 0.6f)) * normalize;
        const int RowStep = 2;
        var reach = Math.Clamp(_lighting.SkyReach, 0, MapLightingMetadata.MaxSkyReach) / 100f;
        for (var row = 0; row < CellsHigh; row += RowStep)
        {
            var worldY = Origin.Y + ((row + (RowStep * 0.5f)) * _cellSize);
            var t = Math.Clamp(((worldY / _worldHeight) - reach) / (1f - reach), 0f, 1f);
            if (_lighting.Banded && !quantize)
            {
                t = MathF.Round(t * 8f) / 8f;
            }

            var ambient = ApplyIntensity(Vector3.Lerp(sky, ground, t) * level * 0.5f);
            batch.Draw(_white, new Rectangle(0, row, CellsWide, RowStep), new Color(ambient));
        }

        batch.End();

        // Lights add on top. Lights with ray reach are drawn afterwards as clipped fans.
        batch.Begin(SpriteSortMode.Deferred, AddOne, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        DrawUnblockedLights(batch, quantize, afterBands: false, out var anyBlockedLights);
        batch.End();
        if (anyBlockedLights)
        {
            DrawBlockedLights(device, quantize, afterBands: false);
        }

        if (_lighting.Vignette > 0)
        {
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            batch.Draw(_vignette, new Rectangle(0, 0, CellsWide, CellsHigh), Color.White * (_lighting.Vignette / 100f * ClampedIntensity));
            batch.End();
        }

        _bandedFrame = false;
        if (quantize)
        {
            RenderBands(device, batch);
        }

        _detailFrame = false;
        if (HasMaskLights() || _ambientOnlyShapes.Count > 0)
        {
            // World resolution, so masks stay sharp and ambient-only shapes follow their pixels.
            RenderDetail(device, batch);
        }

        RenderPaintedMaskEmission(device, batch);
        RenderMaskOutsideGlow(device, batch);

        if (_ambientOnlyShapes.Count > 0)
        {
            ApplyAmbientOnlyShapes(device, batch, quantize);
        }

        device.SetRenderTarget(null);
        HasFrame = true;
    }

    /// <summary>
    /// Steps the smooth light map into the band target, optionally keeps the smooth light
    /// wherever the map art is transparent, then adds the lights set to smooth falloff.
    /// </summary>
    private void RenderBands(GraphicsDevice device, SpriteBatch batch)
    {
        EnsureBandTarget(device);
        var effect = BandEffect!;
        effect.Parameters["BandCount"]?.SetValue((float)Math.Clamp(
            _lighting.BandSteps,
            MapLightingMetadata.MinBandSteps,
            MapLightingMetadata.MaxBandSteps));
        device.SetRenderTarget(_bandTarget);
        device.Clear(Color.Black);
        var cells = new Rectangle(0, 0, CellsWide, CellsHigh);
        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, effect);
        batch.Draw(_target, cells, cells, Color.White);
        batch.End();

        if (_lighting.BandClip
            && _clipArt is { IsDisposed: false } art
            && _clipWorldWidth > 0f
            && _clipWorldHeight > 0f)
        {
            // Mask = the art's alpha in light map cells (bands stay where the art is solid) ...
            batch.Begin(SpriteSortMode.Deferred, WriteAlphaOnly, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            batch.Draw(
                art,
                new Vector2(-Origin.X / _cellSize, -Origin.Y / _cellSize),
                null,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(_clipWorldWidth / _cellSize / art.Width, _clipWorldHeight / _cellSize / art.Height),
                SpriteEffects.None,
                0f);
            batch.End();

            // ... and the smooth light map everywhere else.
            batch.Begin(SpriteSortMode.Deferred, LerpByDestinationAlpha, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            batch.Draw(_target, cells, cells, Color.White);
            batch.End();
        }

        batch.Begin(SpriteSortMode.Deferred, AddOne, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        DrawUnblockedLights(batch, quantize: true, afterBands: true, out var anyBlockedLights);
        batch.End();
        if (anyBlockedLights)
        {
            DrawBlockedLights(device, quantize: true, afterBands: true);
        }

        _bandedFrame = true;
    }

    /// <summary>
    /// With the band shader, lights set to smooth falloff skip the banding and are added
    /// after it; every other light is drawn smooth and banded with the map.
    /// </summary>
    private static bool DrawsAfterBands(in LightSample light, bool quantize) =>
        quantize && light.Falloff == MapLightFalloff.Smooth;

    private void DrawUnblockedLights(SpriteBatch batch, bool quantize, bool afterBands, out bool anyBlockedLights)
    {
        anyBlockedLights = false;
        foreach (var light in _lights)
        {
            // Masked lights are drawn at world resolution in RenderDetail.
            if (IsMasked(light) || DrawsAfterBands(light, quantize) != afterBands)
            {
                continue;
            }

            var banded = !quantize && MapLightMetadata.UsesRetroBands(light.Falloff, _lighting.Banded);
            if (light.Reach is not null || UsesConeGeometry(light, banded))
            {
                // Drawn as geometry in DrawBlockedLights.
                anyBlockedLights = true;
                continue;
            }

            var lightTexture = GetLightTexture(light, banded, out var rotation);
            var size = light.Radius * 2f / _cellSize;
            var center = new Vector2((light.X - Origin.X) / _cellSize, (light.Y - Origin.Y) / _cellSize);
            // Fit the texture's longest side to the light's diameter (masks need not be square).
            batch.Draw(
                lightTexture,
                center,
                null,
                light.Color * (light.Intensity * 0.5f),
                rotation,
                new Vector2(lightTexture.Width * 0.5f, lightTexture.Height * 0.5f),
                size / Math.Max(lightTexture.Width, lightTexture.Height),
                SpriteEffects.None,
                0f);
        }
    }

    /// <summary>
    /// Multiplies the light map onto whatever is already drawn. The caller begins the batch
    /// with <see cref="Multiply2x"/>; positions map world to batch space as world*scale+offset.
    /// </summary>
    public void DrawMultiply(SpriteBatch batch, float scale, Vector2 offset)
    {
        if (HasFrame && _detailFrame && _detailTarget is { IsDisposed: false } detail)
        {
            batch.Draw(
                detail,
                (Origin * scale) + offset,
                new Rectangle(0, 0, _detailWidth, _detailHeight),
                Color.White,
                0f,
                Vector2.Zero,
                _detailScale * scale,
                SpriteEffects.None,
                0f);
            return;
        }

        var source = _bandedFrame && _bandTarget is { IsDisposed: false } ? _bandTarget : _target;
        if (!HasFrame || source is null)
        {
            return;
        }

        batch.Draw(
            source,
            (Origin * scale) + offset,
            new Rectangle(0, 0, CellsWide, CellsHigh),
            Color.White,
            0f,
            Vector2.Zero,
            _cellSize * scale,
            SpriteEffects.None,
            0f);
    }

    /// <summary>
    /// Soft halos over bright sources (begin the batch with <see cref="AddOne"/>), so lamps
    /// and fire bloom a little into the air around them.
    /// </summary>
    public void DrawGlow(SpriteBatch batch, float scale, Vector2 offset)
    {
        if (!HasGlow || _smoothLight is null)
        {
            return;
        }

        if (_outsideMaskGlowFrame && _outsideMaskGlowTarget is { IsDisposed: false } glow)
        {
            batch.Draw(glow, Origin * scale + offset, new Rectangle(0, 0, _detailWidth, _detailHeight),
                Color.White, 0f, Vector2.Zero, _detailScale * scale, SpriteEffects.None, 0f);
        }
        DrawGlowHalos(batch, scale, offset);
    }

    /// <summary>Painted mask artwork belongs behind players and foreground; begin the batch with AddOne/PointClamp.</summary>
    public void DrawPaintedMaskEmission(SpriteBatch batch, float scale, Vector2 offset)
    {
        if (!HasPaintedMaskEmission || _paintedEmissionTarget is not { IsDisposed: false } painted) return;
        batch.Draw(painted, Origin * scale + offset, new Rectangle(0, 0, _detailWidth, _detailHeight),
            Color.White, 0f, Vector2.Zero, _detailScale * scale, SpriteEffects.None, 0f);
    }

    /// <summary>Draws every glowing light's halo; positions map world to batch space as world*scale+offset.</summary>
    private void DrawGlowHalos(SpriteBatch batch, float scale, Vector2 offset)
    {
        var mapGlow = _lighting.Glow / 100f;
        foreach (var light in _lights)
        {
            // A mask is an authored pixel shape; a rescaled soft copy of it only smears it.
            if (!light.Glows || IsMasked(light))
            {
                continue;
            }

            // Some lights (muzzle flashes) glow in the air even when the map's glow is low.
            var glow = MathF.Max(mapGlow, light.GlowBoost);
            if (glow <= 0f)
            {
                continue;
            }

            // Halos follow the light's shape (a spotlight blooms along its beam); in retro style
            // they are stepped too (sample with point filtering), so they do not blur the bands.
            var glowTexture = GetLightTexture(light, banded: _lighting.Banded, out var rotation);
            var radius = light.Radius * 0.45f;
            if (light.Reach is { Length: > 0 } reach)
            {
                // Keep a blocked light's halo from shining through the walls around it.
                var total = 0f;
                foreach (var distance in reach)
                {
                    total += distance;
                }

                radius = MathF.Min(radius, total / reach.Length * 0.5f);
            }
            batch.Draw(
                glowTexture,
                (new Vector2(light.X, light.Y) * scale) + offset,
                null,
                light.Color * (light.Intensity * glow * 0.22f),
                rotation,
                new Vector2(glowTexture.Width * 0.5f, glowTexture.Height * 0.5f),
                radius * 2f * scale / Math.Max(glowTexture.Width, glowTexture.Height),
                SpriteEffects.None,
                0f);
        }
    }

    /// <summary>Deterministic 0..1 flicker for a light, smooth enough to read as fire.</summary>
    public static float GetFlicker(MapLightFlicker flicker, double time, float seed) =>
        MapLightAnimation.GetFlicker(flicker, time, seed);

    public void Dispose()
    {
        _target?.Dispose();
        _bandTarget?.Dispose();
        _bandTarget = null;
        _detailTarget?.Dispose();
        _detailTarget = null;
        _paintedEmissionTarget?.Dispose();
        _paintedEmissionTarget = null;
        _outsideMaskGlowTarget?.Dispose();
        _outsideMaskGlowTarget = null;
        _maskGlowBuffer.Dispose();
        foreach (var cone in _coneTextures.Values)
        {
            cone.Dispose();
        }

        _coneTextures.Clear();
        _fanEffect?.Dispose();
        _fanEffect = null;
        _device = null;
        _smoothLight?.Dispose();
        _bandedLight?.Dispose();
        _vignette?.Dispose();
        _white?.Dispose();
        _target = null;
        _smoothLight = null;
        _bandedLight = null;
        _vignette = null;
        _white = null;
    }

    private float GetPulseFactor()
    {
        if (_lighting.Pulse <= 0)
        {
            return 1f;
        }

        var amount = _lighting.Pulse / 100f;
        var breathe = 0.5f + (0.5f * (float)Math.Sin(_time * 1.1d));
        var shimmer = SmoothNoise(_time * 3.1d);
        return 1f - (amount * ((0.28f * breathe) + (0.08f * shimmer)));
    }

    private static float SmoothNoise(double x) => MapLightAnimation.SmoothNoise(x);

    private static Vector3 ToVector(MapLightingColor color) => new(color.R / 255f, color.G / 255f, color.B / 255f);

    private static float MaxChannel(Vector3 color) => MathF.Max(color.X, MathF.Max(color.Y, color.Z));

    private void EnsureResources(GraphicsDevice device)
    {
        if (_target is null
            || _target.IsDisposed
            || _target.Width < CellsWide
            || _target.Height < CellsHigh
            || _target.Width > CellsWide + 512
            || _target.Height > CellsHigh + 512)
        {
            _target?.Dispose();
            // A little headroom so small viewport changes do not reallocate.
            try
            {
                _target = CreateTarget(device, CellsWide + 16, CellsHigh + 16, _multiSampleCount);
            }
            catch (Exception) when (_multiSampleCount > 0)
            {
                // No multisampled render targets on this device: plain edges, still smooth.
                _multiSampleCount = 0;
                _target = CreateTarget(device, CellsWide + 16, CellsHigh + 16, 0);
            }
        }

        _device = device;
        _white ??= CreateSolid(device);
        _smoothLight ??= CreateLightTexture(device, banded: false);
        _bandedLight ??= CreateLightTexture(device, banded: true);
        _vignette ??= CreateVignette(device);
    }

    private void EnsureBandTarget(GraphicsDevice device)
    {
        if (_target is null)
        {
            return;
        }

        if (_bandTarget is null
            || _bandTarget.IsDisposed
            || _bandTarget.Width != _target.Width
            || _bandTarget.Height != _target.Height)
        {
            _bandTarget?.Dispose();
            _bandTarget = new RenderTarget2D(
                device,
                _target.Width,
                _target.Height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents);
        }
    }

    /// <summary>
    /// Wall-blocked lights: a triangle fan out to where each ray stops, textured with the
    /// same falloff (round or cone) so it matches an unblocked light exactly where it is
    /// not cut off. Uses the stock BasicEffect, no custom shader.
    /// </summary>
    private void DrawBlockedLights(GraphicsDevice device, bool quantize, bool afterBands)
    {
        if (_target is null)
        {
            return;
        }

        foreach (var light in _lights)
        {
            if (IsMasked(light) || DrawsAfterBands(light, quantize) != afterBands)
            {
                continue;
            }

            var banded = !quantize && MapLightMetadata.UsesRetroBands(light.Falloff, _lighting.Banded);
            if (UsesConeGeometry(light, banded))
            {
                DrawConeLight(device, light, light.Reach is { Length: > 2 } coneReach ? coneReach : null, _target.Width, _target.Height);
                continue;
            }

            if (light.Reach is not { Length: > 2 } reach)
            {
                continue;
            }

            var texture = GetLightTexture(light, banded, out var rotation);
            // Longest texture side spans the light's diameter, as in the unblocked draw.
            var longest = (float)Math.Max(texture.Width, texture.Height);
            DrawLightFan(
                device,
                light,
                reach,
                texture,
                rotation,
                new Vector2(light.X, light.Y),
                0.5f / light.Radius * (longest / texture.Width),
                0.5f / light.Radius * (longest / texture.Height),
                _cellSize,
                _target.Width,
                _target.Height,
                SamplerState.LinearClamp);
        }
    }

    /// <summary>
    /// One wall-blocked light: a triangle fan out to where each ray stops, textured with the
    /// light's shape so it matches the unblocked draw wherever it is not cut off. Uses the
    /// stock BasicEffect, no custom shader. uvPerWorld* map world offsets to texture space.
    /// </summary>
    private void DrawLightFan(
        GraphicsDevice device,
        in LightSample light,
        float[] reach,
        Texture2D texture,
        float rotation,
        Vector2 worldCenter,
        float uvPerWorldX,
        float uvPerWorldY,
        float worldPerTexel,
        int targetWidth,
        int targetHeight,
        SamplerState sampler,
        Vector2? targetOrigin = null)
    {
        _fanEffect ??= new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
        _fanEffect.World = Matrix.Identity;
        _fanEffect.View = Matrix.Identity;
        _fanEffect.Projection = Matrix.CreateOrthographicOffCenter(0f, targetWidth, targetHeight, 0f, 0f, 1f);

        var rays = reach.Length;
        EnsureFanCapacity(rays);
        var color = light.Color * (light.Intensity * 0.5f);
        var center = (worldCenter - (targetOrigin ?? Origin)) / worldPerTexel;
        // The rays start at the light; texture coordinates are relative to the shape's centre.
        var lightOffset = new Vector2(light.X, light.Y) - worldCenter;

        // Texture space is the sprite's space before its rotation: undo it for UVs.
        var cos = MathF.Cos(-rotation);
        var sin = MathF.Sin(-rotation);
        Vector2 ToUv(float worldX, float worldY)
        {
            var textureX = (worldX * cos) - (worldY * sin);
            var textureY = (worldX * sin) + (worldY * cos);
            return new Vector2(0.5f + (textureX * uvPerWorldX), 0.5f + (textureY * uvPerWorldY));
        }

        var lightTexel = center + (lightOffset / worldPerTexel);
        _fanVertices[0] = new VertexPositionColorTexture(new Vector3(lightTexel, 0f), color, ToUv(lightOffset.X, lightOffset.Y));
        for (var index = 0; index < rays; index += 1)
        {
            var angle = index * MathF.Tau / rays;
            var offsetX = lightOffset.X + (MathF.Cos(angle) * reach[index]);
            var offsetY = lightOffset.Y + (MathF.Sin(angle) * reach[index]);
            _fanVertices[index + 1] = new VertexPositionColorTexture(
                new Vector3(center.X + (offsetX / worldPerTexel), center.Y + (offsetY / worldPerTexel), 0f),
                color,
                ToUv(offsetX, offsetY));
            _fanIndices[(index * 3) + 0] = 0;
            _fanIndices[(index * 3) + 1] = (short)(index + 1);
            _fanIndices[(index * 3) + 2] = (short)(((index + 1) % rays) + 1);
        }

        _fanEffect.Texture = texture;
        _fanEffect.CurrentTechnique.Passes[0].Apply();
        device.BlendState = AddOne;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = sampler;
        device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _fanVertices, 0, rays + 1, _fanIndices, 0, rays);
    }

    /// <summary>
    /// Smooth spotlights are drawn as geometry rather than a stretched cone texture: the
    /// cone's sides are polygon edges (sharp, anti-aliased by the multisampled light map)
    /// while the falloff along the beam stays smooth. Retro-banded cones keep their texture.
    /// </summary>
    private static bool UsesConeGeometry(in LightSample light, bool banded) =>
        !banded && !IsMasked(light) && MapLightMetadata.IsDirectional(light.SpreadDegrees);

    /// <summary>
    /// One spotlight as a triangle fan over its cone, each edge vertex as far as the light's
    /// radius or its wall reach that way, textured with the round falloff. Plus the small
    /// glow at the lamp itself, as in the cone texture, so the fixture is not dark.
    /// </summary>
    private void DrawConeLight(GraphicsDevice device, in LightSample light, float[]? reach, int targetWidth, int targetHeight)
    {
        if (_smoothLight is null)
        {
            return;
        }

        var spread = Math.Clamp(light.SpreadDegrees, 1f, 359f) * (MathF.PI / 180f);
        // Screen angles run clockwise from the right (y down); map directions counter-clockwise.
        var axis = -light.DirectionDegrees * (MathF.PI / 180f);
        var segments = Math.Clamp((int)MathF.Ceiling(spread / (2f * MathF.PI / 180f)), 4, 180);
        if (reach is not null)
        {
            // At least two vertices per wall ray inside the cone, so walls cut it as cleanly as a lamp.
            segments = Math.Clamp(Math.Max(segments, (int)MathF.Ceiling(reach.Length * spread / MathF.Tau) * 2), 4, 1024);
        }

        EnsureFanCapacity(segments + 2);
        var color = light.Color * (light.Intensity * 0.5f);
        var center = new Vector2((light.X - Origin.X) / _cellSize, (light.Y - Origin.Y) / _cellSize);
        var uvScale = 0.5f / light.Radius;
        _fanVertices[0] = new VertexPositionColorTexture(new Vector3(center, 0f), color, new Vector2(0.5f, 0.5f));
        for (var index = 0; index <= segments; index += 1)
        {
            var angle = axis - (spread * 0.5f) + (spread * index / segments);
            var distance = reach is null ? light.Radius : MathF.Min(light.Radius, SampleReach(reach, angle));
            var offsetX = MathF.Cos(angle) * distance;
            var offsetY = MathF.Sin(angle) * distance;
            _fanVertices[index + 1] = new VertexPositionColorTexture(
                new Vector3(center.X + (offsetX / _cellSize), center.Y + (offsetY / _cellSize), 0f),
                color,
                new Vector2(0.5f + (offsetX * uvScale), 0.5f + (offsetY * uvScale)));
        }

        for (var index = 0; index < segments; index += 1)
        {
            _fanIndices[(index * 3) + 0] = 0;
            _fanIndices[(index * 3) + 1] = (short)(index + 1);
            _fanIndices[(index * 3) + 2] = (short)(index + 2);
        }

        ApplyFanEffect(device, _smoothLight, targetWidth, targetHeight, SamplerState.LinearClamp);
        device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _fanVertices, 0, segments + 2, _fanIndices, 0, segments);

        // The lamp's own small glow, all round (the cone texture's 0.35 "fixture" term).
        var lampRadius = light.Radius / 6f;
        if (lampRadius >= 1f)
        {
            const int LampSegments = 24;
            EnsureFanCapacity(LampSegments + 2);
            var lampColor = light.Color * (light.Intensity * 0.5f * 0.35f);
            var lampUv = 0.5f / lampRadius;
            _fanVertices[0] = new VertexPositionColorTexture(new Vector3(center, 0f), lampColor, new Vector2(0.5f, 0.5f));
            for (var index = 0; index <= LampSegments; index += 1)
            {
                var angle = index * MathF.Tau / LampSegments;
                var distance = reach is null ? lampRadius : MathF.Min(lampRadius, SampleReach(reach, angle));
                var offsetX = MathF.Cos(angle) * distance;
                var offsetY = MathF.Sin(angle) * distance;
                _fanVertices[index + 1] = new VertexPositionColorTexture(
                    new Vector3(center.X + (offsetX / _cellSize), center.Y + (offsetY / _cellSize), 0f),
                    lampColor,
                    new Vector2(0.5f + (offsetX * lampUv), 0.5f + (offsetY * lampUv)));
                if (index < LampSegments)
                {
                    _fanIndices[(index * 3) + 0] = 0;
                    _fanIndices[(index * 3) + 1] = (short)(index + 1);
                    _fanIndices[(index * 3) + 2] = (short)(index + 2);
                }
            }

            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _fanVertices, 0, LampSegments + 2, _fanIndices, 0, LampSegments);
        }
    }

    /// <summary>How far a light's wall-reach rays travel at a screen angle (radians, clockwise from the right).</summary>
    private static float SampleReach(float[] reach, float angle)
    {
        var turn = angle % MathF.Tau;
        if (turn < 0f)
        {
            turn += MathF.Tau;
        }

        var position = turn / MathF.Tau * reach.Length;
        var lower = (int)MathF.Floor(position) % reach.Length;
        var upper = (lower + 1) % reach.Length;
        var t = position - MathF.Floor(position);
        // Between two rays that both reach about as far, follow the wall smoothly; across a
        // wall corner (a big jump), take the nearer ray's reach so the light does not bleed.
        var a = reach[lower];
        var b = reach[upper];
        return MathF.Abs(a - b) <= MathF.Max(a, b) * 0.15f ? a + ((b - a) * t) : (t < 0.5f ? a : b);
    }

    private void ApplyFanEffect(GraphicsDevice device, Texture2D texture, int targetWidth, int targetHeight, SamplerState sampler)
    {
        _fanEffect ??= new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
        _fanEffect.World = Matrix.Identity;
        _fanEffect.View = Matrix.Identity;
        _fanEffect.Projection = Matrix.CreateOrthographicOffCenter(0f, targetWidth, targetHeight, 0f, 0f, 1f);
        _fanEffect.Texture = texture;
        _fanEffect.CurrentTechnique.Passes[0].Apply();
        device.BlendState = AddOne;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = sampler;
    }

    private static bool IsMasked(in LightSample light) => light.Mask is { IsDisposed: false };

    /// <summary>
    /// Replaces the light with the ambient light wherever an ambient-only shape is solid:
    /// result = ambient x coverage + light x (1 - coverage), drawn as an ordinary
    /// premultiplied alpha blend of the white shape tinted with the ambient value.
    /// </summary>
    private void ApplyAmbientOnlyShapes(GraphicsDevice device, SpriteBatch batch, bool quantize)
    {
        if (!_detailFrame || _detailTarget is null)
        {
            return;
        }

        device.SetRenderTarget(_detailTarget);
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        var unit = (float)_detailScale;
        var roundToRows = _lighting.Banded && !quantize;
        foreach (var (shape, topLeft, size, opacity) in _ambientOnlyShapes)
        {
            var ambient = GetAmbientLightValue(topLeft.Y + (size.Y * 0.5f), roundToRows);
            var tint = new Color(
                Math.Clamp(ambient.X, 0f, 1f),
                Math.Clamp(ambient.Y, 0f, 1f),
                Math.Clamp(ambient.Z, 0f, 1f),
                1f) * opacity;
            batch.Draw(
                shape,
                (topLeft - Origin) / unit,
                null,
                tint,
                0f,
                Vector2.Zero,
                new Vector2(size.X / unit / shape.Width, size.Y / unit / shape.Height),
                SpriteEffects.None,
                0f);
        }

        batch.End();
    }

    private bool HasMaskLights()
    {
        foreach (var light in _lights)
        {
            if (IsMasked(light))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Scales the finished coarse light map up to world resolution and adds imported
    /// mask lights there, sharp: point sampling, a whole-number pixel scale and a pixel-aligned
    /// position, so every mask pixel lands on the same number of screen pixels. Masks are
    /// not banded; their own pixels are the shape. Painted masks emit map artwork in a
    /// separate background layer, together with their optional outside glow.
    /// </summary>
    private void RenderDetail(GraphicsDevice device, SpriteBatch batch)
    {
        var worldWidth = CellsWide * _cellSize;
        var worldHeight = CellsHigh * _cellSize;
        _detailScale = Math.Max(1, (int)MathF.Ceiling(Math.Max(worldWidth, worldHeight) / (float)MaxDetailTexels));
        _detailWidth = Math.Max(1, (int)MathF.Ceiling(worldWidth / (float)_detailScale));
        _detailHeight = Math.Max(1, (int)MathF.Ceiling(worldHeight / (float)_detailScale));
        if (_detailTarget is null
            || _detailTarget.IsDisposed
            || _detailTarget.Width < _detailWidth
            || _detailTarget.Height < _detailHeight
            || _detailTarget.Width > _detailWidth + 512
            || _detailTarget.Height > _detailHeight + 512)
        {
            _detailTarget?.Dispose();
            _detailTarget = new RenderTarget2D(
                device,
                _detailWidth + 32,
                _detailHeight + 32,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents);
        }

        var coarse = _bandedFrame && _bandTarget is { IsDisposed: false } ? _bandTarget : _target;
        device.SetRenderTarget(_detailTarget);
        device.Clear(Color.Black);
        // Same filtering the coarse map would have had on screen: square cells in retro
        // style, a smooth blend otherwise.
        batch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            _lighting.Banded ? SamplerState.PointClamp : SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);
        batch.Draw(
            coarse,
            Vector2.Zero,
            new Rectangle(0, 0, CellsWide, CellsHigh),
            Color.White,
            0f,
            Vector2.Zero,
            _cellSize / (float)_detailScale,
            SpriteEffects.None,
            0f);
        batch.End();

        DrawDetailMasks(device, batch, paintedOnly: false, strength: 1f);
        _detailFrame = true;
    }

    /// <summary>
    /// Where and how big a mask is drawn, in world units. Its longest side still roughly
    /// spans the light's diameter, but the pixel scale is a whole number (or a whole
    /// fraction when shrinking) so the art is never resampled unevenly; quarter-turn
    /// directions snap exactly; and its top-left corner sits on the pixel grid.
    /// </summary>
    private (Vector2 Center, float WorldScale, float Rotation) GetMaskPlacement(in LightSample light, Texture2D mask)
        => GetMaskPlacement(light.X, light.Y, light.Radius, light.DirectionDegrees, light.MaskPixelSize, mask);

    internal (Vector2 Center, float WorldScale, float Rotation) GetMaskPlacement(in MapLightMarker light, Texture2D mask)
        => GetMaskPlacement(light.X, light.Y, light.Radius, light.Direction, light.MaskPixelSize, mask);

    private (Vector2 Center, float WorldScale, float Rotation) GetMaskPlacement(float x, float y, float radius, float direction, float pixelSize, Texture2D mask)
    {
        if (pixelSize > 0f)
        {
            var size = pixelSize;
            var origin = GetPaintedMaskOrigin(x, y, mask.Width, mask.Height, size);
            return (origin + new Vector2(mask.Width * size * 0.5f, mask.Height * size * 0.5f), size, 0f);
        }

        var longest = Math.Max(1, Math.Max(mask.Width, mask.Height));
        var raw = radius * 2f / longest;
        var worldScale = raw >= 1f
            ? MathF.Max(1f, MathF.Round(raw))
            : 1f / MathF.Max(1f, MathF.Round(1f / raw));

        var rotation = -direction * (MathF.PI / 180f);
        var quarters = rotation / (MathF.PI * 0.5f);
        var roundedQuarters = MathF.Round(quarters);
        var sideways = false;
        if (MathF.Abs(quarters - roundedQuarters) < 0.02f)
        {
            rotation = roundedQuarters * (MathF.PI * 0.5f);
            sideways = ((int)roundedQuarters & 1) != 0;
        }

        var halfWidth = (sideways ? mask.Height : mask.Width) * worldScale * 0.5f;
        var halfHeight = (sideways ? mask.Width : mask.Height) * worldScale * 0.5f;
        var grid = (float)_detailScale;
        var left = Origin.X + (MathF.Round((x - halfWidth - Origin.X) / grid) * grid);
        var top = Origin.Y + (MathF.Round((y - halfHeight - Origin.Y) / grid) * grid);
        return (new Vector2(left + halfWidth, top + halfHeight), worldScale, rotation);
    }

    private void DrawDetailMasks(GraphicsDevice device, SpriteBatch batch, bool paintedOnly, float strength)
    {
        batch.Begin(SpriteSortMode.Deferred, AddOne, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        var anyBlocked = false;
        foreach (var sourceLight in _lights)
        {
            var light = sourceLight with { Intensity = sourceLight.Intensity * strength };
            if (!ShouldDrawMaskInPass(light, paintedOnly))
            {
                continue;
            }

            if (light.Reach is { Length: > 2 })
            {
                anyBlocked = true;
                continue;
            }

            var mask = light.Mask!;
            var (center, worldScale, rotation) = GetMaskPlacement(light, mask);
            batch.Draw(
                mask,
                (center - Origin) / _detailScale,
                null,
                light.Color * (light.Intensity * 0.5f),
                rotation,
                new Vector2(mask.Width * 0.5f, mask.Height * 0.5f),
                worldScale / _detailScale,
                SpriteEffects.None,
                0f);
        }

        batch.End();
        if (anyBlocked)
        {
            foreach (var sourceLight in _lights)
            {
                var light = sourceLight with { Intensity = sourceLight.Intensity * strength };
                if (!ShouldDrawMaskInPass(light, paintedOnly) || light.Reach is not { Length: > 2 } reach)
                {
                    continue;
                }

                var mask = light.Mask!;
                var (center, worldScale, rotation) = GetMaskPlacement(light, mask);
                DrawLightFan(
                    device,
                    light,
                    reach,
                    mask,
                    rotation,
                    center,
                    1f / (mask.Width * worldScale),
                    1f / (mask.Height * worldScale),
                    _detailScale,
                    device.Viewport.Width,
                    device.Viewport.Height,
                    SamplerState.PointClamp);
            }
        }

    }

    private static bool ShouldDrawMaskInPass(in LightSample light, bool paintedOnly) =>
        IsMasked(light) && (paintedOnly
            ? light.MaskPixelSize > 0f && light.Glows
            : light.MaskPixelSize <= 0f);

    internal static Vector2 GetPaintedMaskOrigin(float x, float y, int width, int height, float pixelSize) =>
        new(MathF.Floor((x - width * pixelSize * 0.5f) / pixelSize) * pixelSize,
            MathF.Floor((y - height * pixelSize * 0.5f) / pixelSize) * pixelSize);

    /// <summary>Painted pixels emit colour even over black art; they retain the light's power and colour logic.</summary>
    private void RenderPaintedMaskEmission(GraphicsDevice device, SpriteBatch batch)
    {
        if (!_detailFrame) return;
        var anyPainted = false;
        foreach (var light in _lights)
            if (IsMasked(light) && light.MaskPixelSize > 0f && light.Glows) { anyPainted = true; break; }
        if (!anyPainted) return;
        if (_paintedEmissionTarget is null || _paintedEmissionTarget.IsDisposed
            || _paintedEmissionTarget.Width != _detailWidth || _paintedEmissionTarget.Height != _detailHeight)
        {
            _paintedEmissionTarget?.Dispose();
            _paintedEmissionTarget = new RenderTarget2D(device, _detailWidth, _detailHeight, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }
        device.SetRenderTarget(_paintedEmissionTarget);
        device.Clear(Color.Transparent);
        DrawDetailMasks(device, batch, paintedOnly: true, strength: MathF.Max(0.3f, _lighting.Glow / 100f));
        _paintedEmissionFrame = true;
    }

    /// <summary>
    /// Diffuse a mask while retaining its sharp copy. A painted mask's complete bloom
    /// stays in the background artwork layer; imported light projections illuminate
    /// surfaces and air through the final lighting passes.
    /// </summary>
    private void RenderMaskOutsideGlow(GraphicsDevice device, SpriteBatch batch)
    {
        if (!_detailFrame || _detailTarget is null) return;
        foreach (var light in _lights)
        {
            if (!IsMasked(light) || light.MaskGlowRadius <= 0f) continue;
            var mask = light.Mask!;
            var (center, worldScale, rotation) = GetMaskPlacement(light, mask);
            var bounds = MaskGlowBuffer.GetBounds(center, new Vector2(mask.Width, mask.Height) * worldScale,
                rotation, light.MaskGlowRadius, Origin, new Vector2(_detailWidth, _detailHeight) * _detailScale, _detailScale);
            if (bounds is not { } region) continue;

            _maskGlowBuffer.BeginSource(device, region);
            if (light.Reach is { Length: > 2 } reach)
            {
                // Start from the visible, wall-clipped mask. Painted masks are emissive
                // art and have no reach clipping, including when painted on a wall.
                var source = light with { Color = Color.White, Intensity = 2f };
                DrawLightFan(device, source, reach, mask, rotation, center,
                    1f / (mask.Width * worldScale), 1f / (mask.Height * worldScale),
                    region.WorldPerTexel, device.Viewport.Width, device.Viewport.Height,
                    SamplerState.PointClamp, region.Origin);
            }
            else
            {
                batch.Begin(SpriteSortMode.Deferred, AddOne, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone);
                batch.Draw(mask, (center - region.Origin) / region.WorldPerTexel, null, Color.White,
                    rotation, new Vector2(mask.Width, mask.Height) * 0.5f, worldScale / region.WorldPerTexel,
                    SpriteEffects.None, 0f);
                batch.End();
            }
            var blurred = _maskGlowBuffer.Blur(device, batch, light.MaskGlowRadius / region.WorldPerTexel);
            if (light.MaskPixelSize > 0f)
            {
                // The blurred texture contains the ENTIRE painted shape, including
                // its core. Adding it to the final multiply/air-glow passes re-stamps
                // that shape onto players and foreground even if the sharp pixels
                // were drawn behind them. Keep both copies in the same occluded layer.
                if (light.Glows && _paintedEmissionFrame && _paintedEmissionTarget is { IsDisposed: false } emission)
                {
                    _maskGlowBuffer.Composite(device, batch, emission, blurred, region, Origin, _detailScale,
                        light.Color * (light.Intensity * MathF.Max(0.3f, _lighting.Glow / 100f) * 0.5f));
                }
                continue;
            }
            _maskGlowBuffer.Composite(device, batch, _detailTarget, blurred, region, Origin, _detailScale,
                light.Color * (light.Intensity * 0.5f));
            if (!light.Glows) continue;
            if (!_outsideMaskGlowFrame)
            {
                if (_outsideMaskGlowTarget is null || _outsideMaskGlowTarget.IsDisposed
                    || _outsideMaskGlowTarget.Width != _detailWidth || _outsideMaskGlowTarget.Height != _detailHeight)
                {
                    _outsideMaskGlowTarget?.Dispose();
                    _outsideMaskGlowTarget = new RenderTarget2D(device, _detailWidth, _detailHeight, false,
                        SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                }
                device.SetRenderTarget(_outsideMaskGlowTarget);
                device.Clear(Color.Transparent);
                _outsideMaskGlowFrame = true;
            }
            _maskGlowBuffer.Composite(device, batch, _outsideMaskGlowTarget!, blurred, region, Origin, _detailScale,
                light.Color * (light.Intensity * MathF.Max(0.3f, _lighting.Glow / 100f) * 0.5f));
        }
    }

    private void EnsureFanCapacity(int rays)
    {
        if (_fanVertices.Length < rays + 1)
        {
            _fanVertices = new VertexPositionColorTexture[rays + 1];
        }

        if (_fanIndices.Length < rays * 3)
        {
            _fanIndices = new short[rays * 3];
        }
    }

    /// <summary>Round texture for ordinary lights; a rotated cone texture for spotlights.</summary>
    private Texture2D GetLightTexture(in LightSample light, bool banded, out float rotation)
    {
        rotation = 0f;
        if (light.Mask is { IsDisposed: false } mask)
        {
            // A custom shape, drawn as authored at direction 0 and turned with the direction
            // (counter-clockwise like the cones, hence the minus sign).
            rotation = -light.DirectionDegrees * (MathF.PI / 180f);
            return mask;
        }

        if (!MapLightMetadata.IsDirectional(light.SpreadDegrees) || _device is null)
        {
            return banded ? _bandedLight! : _smoothLight!;
        }

        // Cones point right in the texture; SpriteBatch rotates clockwise on screen, and
        // map directions count counter-clockwise (90 = up), hence the minus sign.
        rotation = -light.DirectionDegrees * (MathF.PI / 180f);
        var step = Math.Clamp(
            (int)MathF.Round(light.SpreadDegrees / ConeSpreadStepDegrees),
            1,
            (360 / ConeSpreadStepDegrees) - 1);
        var key = (step * 2) + (banded ? 1 : 0);
        if (!_coneTextures.TryGetValue(key, out var texture))
        {
            // Built once per spread/style and cached (a few textures per map at most).
            texture = CreateConeTexture(_device, step * ConeSpreadStepDegrees, banded);
            _coneTextures[key] = texture;
        }

        return texture;
    }

    /// <summary>
    /// A spotlight: the round falloff masked to a cone pointing right, with a soft angular
    /// edge (hard for retro bands) and a small glow at the lamp so the fixture is not dark.
    /// </summary>
    private static Texture2D CreateConeTexture(GraphicsDevice device, float spreadDegrees, bool banded)
    {
        var pixels = new Color[ConeTextureSize * ConeTextureSize];
        var half = ConeTextureSize * 0.5f;
        var halfAngle = spreadDegrees * (MathF.PI / 360f);
        var softEdge = MathF.Min(12f * (MathF.PI / 180f), halfAngle * 0.35f);
        for (var y = 0; y < ConeTextureSize; y += 1)
        {
            for (var x = 0; x < ConeTextureSize; x += 1)
            {
                var dx = (x + 0.5f - half) / half;
                var dy = (y + 0.5f - half) / half;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                var radial = MathF.Pow(MathF.Max(0f, 1f - distance), 1.6f);
                var angle = MathF.Abs(MathF.Atan2(dy, dx));
                float angular;
                if (banded)
                {
                    angular = angle <= halfAngle - (softEdge * 0.5f) ? 1f : 0f;
                }
                else
                {
                    var t = Math.Clamp((halfAngle - angle) / softEdge, 0f, 1f);
                    angular = t * t * (3f - (2f * t));
                }

                var lamp = 0.35f * MathF.Pow(MathF.Max(0f, 1f - (distance * 6f)), 1.6f);
                var value = MathF.Max(radial * angular, lamp);
                if (banded)
                {
                    value = distance < 1f ? MathF.Ceiling(value * 5f) / 5f : 0f;
                }

                var channel = (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
                pixels[(y * ConeTextureSize) + x] = new Color(channel, channel, channel, channel);
            }
        }

        var texture = new Texture2D(device, ConeTextureSize, ConeTextureSize);
        texture.SetData(pixels);
        return texture;
    }

    private static Texture2D CreateSolid(GraphicsDevice device)
    {
        var texture = new Texture2D(device, 1, 1);
        texture.SetData(new[] { Color.White });
        return texture;
    }

    /// <summary>
    /// Radial falloff; the banded variant steps it into five rings for a retro look, at a
    /// higher resolution so the ring edges stay sharp on big lights.
    /// </summary>
    private static Texture2D CreateLightTexture(GraphicsDevice device, bool banded)
    {
        var size = banded ? ConeTextureSize : LightTextureSize;
        var pixels = new Color[size * size];
        var half = size * 0.5f;
        for (var y = 0; y < size; y += 1)
        {
            for (var x = 0; x < size; x += 1)
            {
                var dx = (x + 0.5f - half) / half;
                var dy = (y + 0.5f - half) / half;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                var value = MathF.Pow(MathF.Max(0f, 1f - distance), 1.6f);
                if (banded)
                {
                    value = MathF.Ceiling(value * 5f) / 5f;
                    if (distance >= 1f)
                    {
                        value = 0f;
                    }
                }

                var channel = (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
                pixels[(y * size) + x] = new Color(channel, channel, channel, channel);
            }
        }

        var texture = new Texture2D(device, size, size);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Premultiplied black whose alpha rises toward the corners.</summary>
    private static Texture2D CreateVignette(GraphicsDevice device)
    {
        const int Size = 64;
        var pixels = new Color[Size * Size];
        for (var y = 0; y < Size; y += 1)
        {
            for (var x = 0; x < Size; x += 1)
            {
                var dx = ((x + 0.5f) / Size * 2f) - 1f;
                var dy = ((y + 0.5f) / Size * 2f) - 1f;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy)) / MathF.Sqrt(2f);
                var t = Math.Clamp((distance - 0.45f) / 0.55f, 0f, 1f);
                var alpha = (byte)Math.Clamp((int)MathF.Round(t * t * (3f - (2f * t)) * 230f), 0, 255);
                pixels[(y * Size) + x] = new Color((byte)0, (byte)0, (byte)0, alpha);
            }
        }

        var texture = new Texture2D(device, Size, Size);
        texture.SetData(pixels);
        return texture;
    }
}
