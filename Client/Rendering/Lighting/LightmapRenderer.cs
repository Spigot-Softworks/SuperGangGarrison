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
    /// <summary>Smallest light map cell, in world pixels.</summary>
    public const int CellSize = 2;
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

    private readonly List<LightSample> _lights = new();
    private RenderTarget2D? _target;
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
        float[]? Reach);

    /// <summary>World position of the light map's top-left texel.</summary>
    public Vector2 Origin { get; private set; }

    public int CellsWide { get; private set; }

    public int CellsHigh { get; private set; }

    /// <summary>True once <see cref="Render"/> produced a light map for the current frame.</summary>
    public bool HasFrame { get; private set; }

    public MapLighting Lighting => _lighting;

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
        HasFrame = false;
        // Zoomed far out (large builder views), use coarser cells instead of a huge target.
        var largestEdge = MathF.Max(viewWidth, viewHeight);
        _cellSize = Math.Max(CellSize, (int)MathF.Ceiling(largestEdge / MaxTargetCells));
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
        float[]? reach = null)
    {
        if (_lights.Count >= MaxLights || radius <= 0.5f || intensity <= 0.004f)
        {
            return;
        }

        var left = Origin.X - radius;
        var top = Origin.Y - radius;
        var right = Origin.X + (CellsWide * _cellSize) + radius;
        var bottom = Origin.Y + (CellsHigh * _cellSize) + radius;
        if (x < left || x > right || y < top || y > bottom)
        {
            return;
        }

        _lights.Add(new LightSample(x, y, radius, color, intensity, glows, falloff, directionDegrees, spreadDegrees, reach));
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
            if (!light.Glows)
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
        device.SetRenderTarget(_target);
        device.Clear(Color.Black);

        // Ambient: horizontal rows lerping sky -> ground over the map's height.
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
        for (var row = 0; row < CellsHigh; row += RowStep)
        {
            var worldY = Origin.Y + ((row + (RowStep * 0.5f)) * _cellSize);
            var t = Math.Clamp(worldY / _worldHeight, 0f, 1f);
            if (_lighting.Banded)
            {
                t = MathF.Round(t * 8f) / 8f;
            }

            var ambient = Vector3.Lerp(sky, ground, t) * level * 0.5f;
            batch.Draw(_white, new Rectangle(0, row, CellsWide, RowStep), new Color(ambient));
        }

        batch.End();

        // Lights add on top. Lights with ray reach are drawn afterwards as clipped fans.
        batch.Begin(SpriteSortMode.Deferred, AddOne, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        var anyBlockedLights = false;
        foreach (var light in _lights)
        {
            if (light.Reach is not null)
            {
                anyBlockedLights = true;
                continue;
            }

            var banded = MapLightMetadata.UsesRetroBands(light.Falloff, _lighting.Banded);
            var lightTexture = GetLightTexture(light, banded, out var rotation);
            var size = light.Radius * 2f / _cellSize;
            var center = new Vector2((light.X - Origin.X) / _cellSize, (light.Y - Origin.Y) / _cellSize);
            batch.Draw(
                lightTexture,
                center,
                null,
                light.Color * (light.Intensity * 0.5f),
                rotation,
                new Vector2(lightTexture.Width * 0.5f),
                size / lightTexture.Width,
                SpriteEffects.None,
                0f);
        }

        batch.End();
        if (anyBlockedLights)
        {
            DrawBlockedLights(device);
        }

        if (_lighting.Vignette > 0)
        {
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            batch.Draw(_vignette, new Rectangle(0, 0, CellsWide, CellsHigh), Color.White * (_lighting.Vignette / 100f));
            batch.End();
        }

        device.SetRenderTarget(null);
        HasFrame = true;
    }

    /// <summary>
    /// Multiplies the light map onto whatever is already drawn. The caller begins the batch
    /// with <see cref="Multiply2x"/>; positions map world to batch space as world*scale+offset.
    /// </summary>
    public void DrawMultiply(SpriteBatch batch, float scale, Vector2 offset)
    {
        if (!HasFrame || _target is null)
        {
            return;
        }

        batch.Draw(
            _target,
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
        if (!HasFrame || _lighting.Glow <= 0 || _smoothLight is null)
        {
            return;
        }

        var glow = _lighting.Glow / 100f;
        foreach (var light in _lights)
        {
            if (!light.Glows)
            {
                continue;
            }

            // Halos follow the light's shape (a spotlight blooms along its beam), always smooth.
            var glowTexture = GetLightTexture(light, banded: false, out var rotation);
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
                new Vector2(glowTexture.Width * 0.5f),
                radius * 2f * scale / glowTexture.Width,
                SpriteEffects.None,
                0f);
        }
    }

    /// <summary>Deterministic 0..1 flicker for a light, smooth enough to read as fire.</summary>
    public static float GetFlicker(MapLightFlicker flicker, double time, float seed)
    {
        return flicker switch
        {
            MapLightFlicker.Flicker => 0.78f + (0.22f * SmoothNoise((time * 9d) + seed)),
            MapLightFlicker.Pulse => 0.55f + (0.45f * (0.5f + (0.5f * (float)Math.Sin((time * 2.2d) + seed)))),
            _ => 1f,
        };
    }

    public void Dispose()
    {
        _target?.Dispose();
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

    private static float SmoothNoise(double x)
    {
        var cell = Math.Floor(x);
        var fraction = (float)(x - cell);
        var a = Hash01((long)cell);
        var b = Hash01((long)cell + 1);
        var t = fraction * fraction * (3f - (2f * fraction));
        return a + ((b - a) * t);
    }

    private static float Hash01(long value)
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
            _target = new RenderTarget2D(
                device,
                CellsWide + 16,
                CellsHigh + 16,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents);
        }

        _device = device;
        _white ??= CreateSolid(device);
        _smoothLight ??= CreateLightTexture(device, banded: false);
        _bandedLight ??= CreateLightTexture(device, banded: true);
        _vignette ??= CreateVignette(device);
    }

    /// <summary>
    /// Wall-blocked lights: a triangle fan out to where each ray stops, textured with the
    /// same falloff (round or cone) so it matches an unblocked light exactly where it is
    /// not cut off. Uses the stock BasicEffect, no custom shader.
    /// </summary>
    private void DrawBlockedLights(GraphicsDevice device)
    {
        if (_target is null)
        {
            return;
        }

        _fanEffect ??= new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
        _fanEffect.World = Matrix.Identity;
        _fanEffect.View = Matrix.Identity;
        _fanEffect.Projection = Matrix.CreateOrthographicOffCenter(0f, _target.Width, _target.Height, 0f, 0f, 1f);

        foreach (var light in _lights)
        {
            if (light.Reach is not { Length: > 2 } reach)
            {
                continue;
            }

            var banded = MapLightMetadata.UsesRetroBands(light.Falloff, _lighting.Banded);
            var texture = GetLightTexture(light, banded, out var rotation);
            var rays = reach.Length;
            EnsureFanCapacity(rays);
            var color = light.Color * (light.Intensity * 0.5f);
            var center = new Vector2((light.X - Origin.X) / _cellSize, (light.Y - Origin.Y) / _cellSize);

            // Texture space is the sprite's space before its rotation: undo it for UVs.
            var cos = MathF.Cos(-rotation);
            var sin = MathF.Sin(-rotation);
            var uvScale = 0.5f / light.Radius;
            _fanVertices[0] = new VertexPositionColorTexture(new Vector3(center, 0f), color, new Vector2(0.5f, 0.5f));
            for (var index = 0; index < rays; index += 1)
            {
                var angle = index * MathF.Tau / rays;
                var offsetX = MathF.Cos(angle) * reach[index];
                var offsetY = MathF.Sin(angle) * reach[index];
                var textureX = (offsetX * cos) - (offsetY * sin);
                var textureY = (offsetX * sin) + (offsetY * cos);
                _fanVertices[index + 1] = new VertexPositionColorTexture(
                    new Vector3(center.X + (offsetX / _cellSize), center.Y + (offsetY / _cellSize), 0f),
                    color,
                    new Vector2(0.5f + (textureX * uvScale), 0.5f + (textureY * uvScale)));
                _fanIndices[(index * 3) + 0] = 0;
                _fanIndices[(index * 3) + 1] = (short)(index + 1);
                _fanIndices[(index * 3) + 2] = (short)(((index + 1) % rays) + 1);
            }

            _fanEffect.Texture = texture;
            _fanEffect.CurrentTechnique.Passes[0].Apply();
            device.BlendState = AddOne;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearClamp;
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _fanVertices, 0, rays + 1, _fanIndices, 0, rays);
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

    /// <summary>Radial falloff; the banded variant steps it into five rings for a retro look.</summary>
    private static Texture2D CreateLightTexture(GraphicsDevice device, bool banded)
    {
        var pixels = new Color[LightTextureSize * LightTextureSize];
        var half = LightTextureSize * 0.5f;
        for (var y = 0; y < LightTextureSize; y += 1)
        {
            for (var x = 0; x < LightTextureSize; x += 1)
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
                pixels[(y * LightTextureSize) + x] = new Color(channel, channel, channel, channel);
            }
        }

        var texture = new Texture2D(device, LightTextureSize, LightTextureSize);
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
