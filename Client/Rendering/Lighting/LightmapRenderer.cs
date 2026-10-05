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
    private MapLighting _lighting = MapLighting.None;
    private float _worldHeight = 1f;
    private float _ambientBoost;
    private double _time;
    private int _cellSize = CellSize;

    private readonly record struct LightSample(float X, float Y, float Radius, Color Color, float Intensity, bool Glows);

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
    public void AddLight(float x, float y, float radius, Color color, float intensity, bool glows = true)
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

        _lights.Add(new LightSample(x, y, radius, color, intensity, glows));
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

        // Lights add on top.
        batch.Begin(SpriteSortMode.Deferred, AddOne, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        var lightTexture = _lighting.Banded ? _bandedLight! : _smoothLight!;
        foreach (var light in _lights)
        {
            var size = light.Radius * 2f / _cellSize;
            var center = new Vector2((light.X - Origin.X) / _cellSize, (light.Y - Origin.Y) / _cellSize);
            batch.Draw(
                lightTexture,
                center,
                null,
                light.Color * (light.Intensity * 0.5f),
                0f,
                new Vector2(LightTextureSize * 0.5f),
                size / LightTextureSize,
                SpriteEffects.None,
                0f);
        }

        batch.End();

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

            var radius = light.Radius * 0.45f;
            batch.Draw(
                _smoothLight,
                (new Vector2(light.X, light.Y) * scale) + offset,
                null,
                light.Color * (light.Intensity * glow * 0.22f),
                0f,
                new Vector2(LightTextureSize * 0.5f),
                radius * 2f * scale / LightTextureSize,
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

        _white ??= CreateSolid(device);
        _smoothLight ??= CreateLightTexture(device, banded: false);
        _bandedLight ??= CreateLightTexture(device, banded: true);
        _vignette ??= CreateVignette(device);
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
