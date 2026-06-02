#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Client;

internal sealed class HostSetupMapPreviewState : IDisposable
{
    public const float PixelPerfectDisplayScale = 1f;
    public const float GameSizeDisplayScale = 6f;
    private const float ZoomStepFactor = 1.1f;
    private const float SnapToleranceRatio = 0.05f;

    private readonly Texture2D _pixel;
    private readonly SimpleLevel _level;
    private readonly Color? _backgroundColor;
    private readonly List<(Texture2D Texture, float XFactor, float YFactor)> _parallaxLayers = new();
    private readonly Texture2D? _stockBackground;
    private readonly float _imageScale;
    private float _displayScale = 1f;
    private Vector2 _panOffset;

    private HostSetupMapPreviewState(
        Texture2D pixel,
        SimpleLevel level,
        Color? backgroundColor,
        float imageScale,
        Texture2D? stockBackground,
        List<(Texture2D Texture, float XFactor, float YFactor)> parallaxLayers)
    {
        _pixel = pixel;
        _level = level;
        _backgroundColor = backgroundColor;
        _imageScale = imageScale;
        _stockBackground = stockBackground;
        _parallaxLayers.AddRange(parallaxLayers);
    }

    public static HostSetupMapPreviewState Create(
        Game1 game,
        SimpleLevel level,
        Texture2D pixel,
        Texture2D? stockBackground)
    {
        var imageScale = 1f;
        Color? backgroundColor = null;
        var parallaxLayers = new List<(Texture2D Texture, float XFactor, float YFactor)>();
        var visuals = level.CustomMapVisuals;
        if (!ReferenceEquals(visuals, CustomMapVisualMetadata.Empty))
        {
            imageScale = MathF.Max(0.01f, visuals.ImageScale);
            if (TryParseHostSetupMapColor(visuals.BackgroundColor, out var bgColor))
            {
                backgroundColor = bgColor;
            }

            foreach (var layer in visuals.ParallaxLayers.OrderBy(static entry => entry.Index))
            {
                try
                {
                    var texture = TextureDecodeUtility.LoadTexture(game.GraphicsDevice, layer.Resource.Bytes, applyLegacyChromaKey: false);
                    parallaxLayers.Add((texture, layer.XFactor, layer.YFactor));
                }
                catch (InvalidOperationException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }
        }

        return new HostSetupMapPreviewState(pixel, level, backgroundColor, imageScale, stockBackground, parallaxLayers);
    }

    public float DisplayScale => _displayScale;

    public bool CanPan(Rectangle viewportBounds)
    {
        var mapWidth = _level.Bounds.Width * _imageScale * _displayScale;
        var mapHeight = _level.Bounds.Height * _imageScale * _displayScale;
        return mapWidth > viewportBounds.Width + 1f || mapHeight > viewportBounds.Height + 1f;
    }

    public void ResetView(Rectangle viewportBounds)
    {
        _displayScale = GetAreaFillScale(viewportBounds);
        _panOffset = Vector2.Zero;
    }

    public void ZoomIn(Rectangle viewportBounds)
    {
        _displayScale = SnapDisplayScale(_displayScale * ZoomStepFactor, viewportBounds);
        ClampPan(viewportBounds);
    }

    public void ZoomOut(Rectangle viewportBounds)
    {
        _displayScale = SnapDisplayScale(_displayScale / ZoomStepFactor, viewportBounds);
        ClampPan(viewportBounds);
    }

    public void Pan(Vector2 delta, Rectangle viewportBounds)
    {
        if (!CanPan(viewportBounds))
        {
            _panOffset = Vector2.Zero;
            return;
        }

        _panOffset += delta;
        ClampPan(viewportBounds);
    }

    public float GetAreaFillScale(Rectangle viewportBounds)
    {
        var worldWidth = Math.Max(1f, _level.Bounds.Width * _imageScale);
        var worldHeight = Math.Max(1f, _level.Bounds.Height * _imageScale);
        return Math.Min(viewportBounds.Width / worldWidth, viewportBounds.Height / worldHeight);
    }

    public float GetMinDisplayScale(Rectangle viewportBounds) => GetAreaFillScale(viewportBounds);

    public float GetMaxDisplayScale(Rectangle viewportBounds)
    {
        return Math.Max(GetAreaFillScale(viewportBounds), GameSizeDisplayScale);
    }

    public Point GetPreviewSize(int maxWidth, int maxHeight)
    {
        var fitScale = GetAreaFillScale(new Rectangle(0, 0, maxWidth, maxHeight));
        var worldWidth = Math.Max(1f, _level.Bounds.Width * _imageScale);
        var worldHeight = Math.Max(1f, _level.Bounds.Height * _imageScale);
        return new Point(
            Math.Max(120, (int)MathF.Ceiling(worldWidth * fitScale)),
            Math.Max(90, (int)MathF.Ceiling(worldHeight * fitScale)));
    }

    public void Draw(SpriteBatch spriteBatch, Rectangle viewportBounds, bool interactiveView)
    {
        if (!interactiveView)
        {
            _displayScale = GetAreaFillScale(viewportBounds);
            _panOffset = Vector2.Zero;
        }

        if (_backgroundColor.HasValue)
        {
            spriteBatch.Draw(_pixel, viewportBounds, _backgroundColor.Value);
        }
        else if (_parallaxLayers.Count == 0 && _stockBackground is null)
        {
            spriteBatch.Draw(_pixel, viewportBounds, new Color(34, 44, 60));
        }

        var camera = BuildCamera(viewportBounds);
        DrawClipped(spriteBatch, viewportBounds, () =>
        {
            var worldRectangle = new Rectangle(
                viewportBounds.X - (int)MathF.Round(camera.X),
                viewportBounds.Y - (int)MathF.Round(camera.Y),
                (int)MathF.Round(_level.Bounds.Width * _imageScale * _displayScale),
                (int)MathF.Round(_level.Bounds.Height * _imageScale * _displayScale));

            foreach (var (texture, xFactor, yFactor) in _parallaxLayers)
            {
                DrawPreviewParallax(spriteBatch, texture, xFactor, yFactor, camera, viewportBounds);
            }

            if (_stockBackground is not null)
            {
                spriteBatch.Draw(_stockBackground, worldRectangle, Color.White);
            }
        });
    }

    public void Dispose()
    {
        foreach (var (texture, _, _) in _parallaxLayers)
        {
            texture.Dispose();
        }

        _parallaxLayers.Clear();
    }

    private Vector2 BuildCamera(Rectangle viewportBounds)
    {
        var mapWidth = _level.Bounds.Width * _imageScale * _displayScale;
        var mapHeight = _level.Bounds.Height * _imageScale * _displayScale;
        var centered = new Vector2(
            (mapWidth * 0.5f) - (viewportBounds.Width * 0.5f),
            (mapHeight * 0.5f) - (viewportBounds.Height * 0.5f));
        return centered - _panOffset;
    }

    private void ClampPan(Rectangle viewportBounds)
    {
        var mapWidth = _level.Bounds.Width * _imageScale * _displayScale;
        var mapHeight = _level.Bounds.Height * _imageScale * _displayScale;
        var maxPanX = Math.Max(0f, (mapWidth - viewportBounds.Width) * 0.5f);
        var maxPanY = Math.Max(0f, (mapHeight - viewportBounds.Height) * 0.5f);
        _panOffset.X = Math.Clamp(_panOffset.X, -maxPanX, maxPanX);
        _panOffset.Y = Math.Clamp(_panOffset.Y, -maxPanY, maxPanY);
    }

    private float SnapDisplayScale(float scale, Rectangle viewportBounds)
    {
        var min = GetMinDisplayScale(viewportBounds);
        var max = GetMaxDisplayScale(viewportBounds);
        scale = Math.Clamp(scale, min, max);
        foreach (var snapTarget in GetSnapTargets(viewportBounds))
        {
            if (MathF.Abs(scale - snapTarget) <= MathF.Max(0.001f, snapTarget * SnapToleranceRatio))
            {
                return snapTarget;
            }
        }

        return scale;
    }

    private IEnumerable<float> GetSnapTargets(Rectangle viewportBounds)
    {
        var min = GetMinDisplayScale(viewportBounds);
        var max = GetMaxDisplayScale(viewportBounds);
        var candidates = new[] { min, PixelPerfectDisplayScale, GameSizeDisplayScale };
        var yielded = new HashSet<int>();
        foreach (var candidate in candidates.OrderBy(value => value))
        {
            var clamped = Math.Clamp(candidate, min, max);
            var key = (int)MathF.Round(clamped * 10000f);
            if (yielded.Add(key))
            {
                yield return clamped;
            }
        }
    }

    private void DrawClipped(SpriteBatch spriteBatch, Rectangle viewportBounds, Action drawContent)
    {
        spriteBatch.End();
        var graphicsDevice = spriteBatch.GraphicsDevice;
        var previousScissor = graphicsDevice.ScissorRectangle;
        using var scissorRasterizer = new RasterizerState
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true,
        };

        graphicsDevice.ScissorRectangle = viewportBounds;
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: scissorRasterizer);
        drawContent();
        spriteBatch.End();
        graphicsDevice.ScissorRectangle = previousScissor;
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
    }

    private void DrawPreviewParallax(
        SpriteBatch spriteBatch,
        Texture2D texture,
        float xFactor,
        float yFactor,
        Vector2 camera,
        Rectangle viewportBounds)
    {
        var layerScale = _imageScale * _displayScale;
        var scaledWidth = texture.Width * layerScale;
        var scaledHeight = texture.Height * layerScale;
        if (scaledWidth <= 0.01f || scaledHeight <= 0.01f)
        {
            return;
        }

        var drawPosition = new Vector2(
            viewportBounds.X - camera.X,
            viewportBounds.Y - camera.Y);
        spriteBatch.Draw(
            texture,
            drawPosition,
            null,
            Color.White,
            0f,
            Vector2.Zero,
            layerScale,
            SpriteEffects.None,
            0f);
    }

    private static bool TryParseHostSetupMapColor(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('$'))
        {
            trimmed = trimmed[1..];
        }

        if (!int.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return false;
        }

        var red = (rgb >> 16) & 0xFF;
        var green = (rgb >> 8) & 0xFF;
        var blue = rgb & 0xFF;
        color = new Color(red, green, blue);
        return true;
    }
}
