#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    private readonly Color? _voidColor;
    private readonly List<(Texture2D Texture, float XFactor, float YFactor)> _parallaxLayers = new();
    private readonly Texture2D? _stockBackground;
    private readonly Texture2D? _foreground;
    private readonly Dictionary<string, Texture2D> _spriteTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly float _imageScale;
    private readonly float _foregroundOffsetX;
    private readonly float _foregroundOffsetY;
    private float _displayScale = 1f;
    private Vector2 _panOffset;

    private HostSetupMapPreviewState(
        Texture2D pixel,
        SimpleLevel level,
        Color? backgroundColor,
        Color? voidColor,
        float imageScale,
        Texture2D? stockBackground,
        Texture2D? foreground,
        List<(Texture2D Texture, float XFactor, float YFactor)> parallaxLayers,
        Dictionary<string, Texture2D> spriteTextures,
        float foregroundOffsetX,
        float foregroundOffsetY)
    {
        _pixel = pixel;
        _level = level;
        _backgroundColor = backgroundColor;
        _voidColor = voidColor;
        _imageScale = imageScale;
        _stockBackground = stockBackground;
        _foreground = foreground;
        _parallaxLayers.AddRange(parallaxLayers);
        foreach (var pair in spriteTextures)
        {
            _spriteTextures[pair.Key] = pair.Value;
        }

        _foregroundOffsetX = foregroundOffsetX;
        _foregroundOffsetY = foregroundOffsetY;
    }

    public static HostSetupMapPreviewState Create(
        Game1 game,
        SimpleLevel level,
        Texture2D pixel,
        Texture2D? stockBackground)
    {
        var imageScale = 1f;
        Color? backgroundColor = null;
        Color? voidColor = null;
        var parallaxLayers = new List<(Texture2D Texture, float XFactor, float YFactor)>();
        Texture2D? foreground = null;
        var spriteTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        var visuals = level.CustomMapVisuals;
        if (!ReferenceEquals(visuals, CustomMapVisualMetadata.Empty))
        {
            imageScale = MathF.Max(0.01f, visuals.ImageScale);
            if (TryParseHostSetupMapColor(visuals.BackgroundColor, out var bgColor))
            {
                backgroundColor = bgColor;
            }

            if (TryParseHostSetupMapColor(visuals.VoidColor, out var parsedVoidColor))
            {
                voidColor = parsedVoidColor;
            }

            foreach (var layer in visuals.ParallaxLayers.OrderBy(static entry => entry.Index))
            {
                if (TryLoadCustomMapVisualTexture(game.GraphicsDevice, layer.Resource, out var texture))
                {
                    parallaxLayers.Add((texture, layer.XFactor, layer.YFactor));
                }
            }

            if (visuals.Foreground is not null)
            {
                TryLoadCustomMapVisualTexture(game.GraphicsDevice, visuals.Foreground, out foreground);
            }

            foreach (var resource in visuals.SpriteResources.Values)
            {
                if (TryLoadCustomMapVisualTexture(game.GraphicsDevice, resource, out var spriteTexture))
                {
                    spriteTextures[resource.Name] = spriteTexture;
                }
            }
        }

        return new HostSetupMapPreviewState(
            pixel,
            level,
            backgroundColor,
            voidColor,
            imageScale,
            stockBackground,
            foreground,
            parallaxLayers,
            spriteTextures,
            visuals.ForegroundOffsetX,
            visuals.ForegroundOffsetY);
    }

    public float DisplayScale => _displayScale;

    public bool CanPan(Rectangle viewportBounds)
    {
        var mapWidth = _level.Bounds.Width * _displayScale;
        var mapHeight = _level.Bounds.Height * _displayScale;
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
        var worldWidth = Math.Max(1f, _level.Bounds.Width);
        var worldHeight = Math.Max(1f, _level.Bounds.Height);
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
        var worldWidth = Math.Max(1f, _level.Bounds.Width);
        var worldHeight = Math.Max(1f, _level.Bounds.Height);
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
                (int)MathF.Round(_level.Bounds.Width * _displayScale),
                (int)MathF.Round(_level.Bounds.Height * _displayScale));

            foreach (var (texture, xFactor, yFactor) in _parallaxLayers)
            {
                DrawPreviewParallax(spriteBatch, texture, xFactor, yFactor, camera, viewportBounds);
            }

            for (var parallaxLayer = 0; parallaxLayer <= 6; parallaxLayer += 1)
            {
                DrawPreviewCustomSprites(
                    spriteBatch,
                    (CustomMapSpriteLayerKind)(parallaxLayer + 1),
                    camera,
                    viewportBounds);
            }

            if (_stockBackground is not null)
            {
                spriteBatch.Draw(_stockBackground, worldRectangle, Color.White);
            }
            else if (!_backgroundColor.HasValue)
            {
                spriteBatch.Draw(_pixel, worldRectangle, new Color(34, 44, 60));
            }

            DrawPreviewCustomSprites(spriteBatch, CustomMapSpriteLayerKind.Bg, camera, viewportBounds);
            DrawPreviewCustomSprites(spriteBatch, CustomMapSpriteLayerKind.Fg, camera, viewportBounds);
            DrawPreviewForeground(spriteBatch, camera, viewportBounds);

            if (_voidColor.HasValue)
            {
                DrawPreviewVoidBorders(spriteBatch, viewportBounds, worldRectangle, _voidColor.Value);
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
        _foreground?.Dispose();
        foreach (var texture in _spriteTextures.Values)
        {
            texture.Dispose();
        }

        _spriteTextures.Clear();
    }

    private Vector2 BuildCamera(Rectangle viewportBounds)
    {
        var mapWidth = _level.Bounds.Width * _displayScale;
        var mapHeight = _level.Bounds.Height * _displayScale;
        var centered = new Vector2(
            (mapWidth * 0.5f) - (viewportBounds.Width * 0.5f),
            (mapHeight * 0.5f) - (viewportBounds.Height * 0.5f));
        return centered - _panOffset;
    }

    private void ClampPan(Rectangle viewportBounds)
    {
        var mapWidth = _level.Bounds.Width * _displayScale;
        var mapHeight = _level.Bounds.Height * _displayScale;
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

    private static void DrawClipped(SpriteBatch spriteBatch, Rectangle viewportBounds, Action drawContent)
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

        var xOrigin = camera.X + (viewportBounds.Width / 2f) - (scaledWidth / 2f);
        var yOrigin = camera.Y + (viewportBounds.Height / 2f) - (scaledHeight / 2f);
        var xParallax = MathF.Abs(xFactor) > 0.0001f
            ? xOrigin / (xFactor * xFactor)
            : 0f;
        var yParallax = MathF.Abs(yFactor) > 0.0001f
            ? yOrigin / (yFactor * yFactor)
            : 0f;

        var worldX = xOrigin - xParallax;
        var screenY = viewportBounds.Y + yOrigin - yParallax - camera.Y;
        var firstScreenX = viewportBounds.X + worldX - camera.X;
        while (firstScreenX > viewportBounds.X)
        {
            firstScreenX -= scaledWidth;
        }

        for (var screenX = firstScreenX; screenX < viewportBounds.Right; screenX += scaledWidth)
        {
            spriteBatch.Draw(
                texture,
                new Vector2(screenX, screenY),
                null,
                Color.White,
                0f,
                Vector2.Zero,
                layerScale,
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawPreviewCustomSprites(
        SpriteBatch spriteBatch,
        CustomMapSpriteLayerKind layer,
        Vector2 camera,
        Rectangle viewportBounds)
    {
        if (_spriteTextures.Count == 0)
        {
            return;
        }

        var sprites = new List<(int Index, RoomObjectMarker Marker)>();
        for (var index = 0; index < _level.RoomObjects.Count; index += 1)
        {
            var marker = _level.RoomObjects[index];
            if (marker.Type != RoomObjectType.CustomMapSprite
                || marker.CustomMapSprite.Layer != layer
                || !_level.IsRoomObjectActive(index))
            {
                continue;
            }

            sprites.Add((index, marker));
        }

        foreach (var (_, marker) in sprites
                     .OrderBy(static entry => entry.Marker.CustomMapSprite.ZOrder)
                     .ThenBy(static entry => entry.Marker.CenterX)
                     .ThenBy(static entry => entry.Marker.CenterY))
        {
            var resourceName = marker.CustomMapSprite.ImageResourceName;
            if (string.IsNullOrWhiteSpace(resourceName)
                || !_spriteTextures.TryGetValue(resourceName.Trim(), out var texture))
            {
                continue;
            }

            var (relX, relY) = CustomMapSpriteParallax.WorldToScreen(
                marker.CenterX * _displayScale,
                marker.CenterY * _displayScale,
                layer,
                camera.X,
                camera.Y,
                viewportBounds.Width,
                viewportBounds.Height,
                _level.CustomMapVisuals.ParallaxLayers);
            var drawWidth = MathF.Max(1f, marker.Width * _displayScale);
            var drawHeight = MathF.Max(1f, marker.Height * _displayScale);
            var destination = new Rectangle(
                viewportBounds.X + (int)MathF.Floor(relX - (drawWidth * 0.5f)),
                viewportBounds.Y + (int)MathF.Floor(relY - (drawHeight * 0.5f)),
                Math.Max(1, (int)MathF.Ceiling(drawWidth)),
                Math.Max(1, (int)MathF.Ceiling(drawHeight)));
            spriteBatch.Draw(texture, destination, Color.White);
        }
    }

    private void DrawPreviewForeground(SpriteBatch spriteBatch, Vector2 camera, Rectangle viewportBounds)
    {
        if (_foreground is null)
        {
            return;
        }

        var foregroundScale = _imageScale * _displayScale;
        spriteBatch.Draw(
            _foreground,
            new Vector2(
                viewportBounds.X - camera.X + (_foregroundOffsetX * _displayScale),
                viewportBounds.Y - camera.Y + (_foregroundOffsetY * _displayScale)),
            null,
            Color.White,
            0f,
            Vector2.Zero,
            foregroundScale,
            SpriteEffects.None,
            0f);
    }

    private void DrawPreviewVoidBorders(SpriteBatch spriteBatch, Rectangle viewportBounds, Rectangle worldRectangle, Color color)
    {
        if (worldRectangle.Top > viewportBounds.Top)
        {
            spriteBatch.Draw(
                _pixel,
                new Rectangle(viewportBounds.X, viewportBounds.Y, viewportBounds.Width, worldRectangle.Top - viewportBounds.Top),
                color);
        }

        if (worldRectangle.Bottom < viewportBounds.Bottom)
        {
            spriteBatch.Draw(
                _pixel,
                new Rectangle(viewportBounds.X, worldRectangle.Bottom, viewportBounds.Width, viewportBounds.Bottom - worldRectangle.Bottom),
                color);
        }

        if (worldRectangle.Left > viewportBounds.Left)
        {
            var visibleTop = Math.Max(viewportBounds.Top, worldRectangle.Top);
            var visibleBottom = Math.Min(viewportBounds.Bottom, worldRectangle.Bottom);
            var visibleHeight = visibleBottom - visibleTop;
            if (visibleHeight > 0)
            {
                spriteBatch.Draw(
                    _pixel,
                    new Rectangle(
                        viewportBounds.X,
                        visibleTop,
                        worldRectangle.Left - viewportBounds.Left,
                        visibleHeight),
                    color);
            }
        }

        if (worldRectangle.Right < viewportBounds.Right)
        {
            var visibleTop = Math.Max(viewportBounds.Top, worldRectangle.Top);
            var visibleBottom = Math.Min(viewportBounds.Bottom, worldRectangle.Bottom);
            var visibleHeight = visibleBottom - visibleTop;
            if (visibleHeight > 0)
            {
                spriteBatch.Draw(
                    _pixel,
                    new Rectangle(
                        worldRectangle.Right,
                        visibleTop,
                        viewportBounds.Right - worldRectangle.Right,
                        visibleHeight),
                    color);
            }
        }
    }

    private static bool TryLoadCustomMapVisualTexture(
        GraphicsDevice graphicsDevice,
        CustomMapVisualResource resource,
        out Texture2D texture)
    {
        try
        {
            texture = TextureDecodeUtility.LoadTexture(graphicsDevice, resource.Bytes, applyLegacyChromaKey: false);
            return true;
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }

        texture = null!;
        return false;
    }

    private static bool TryParseHostSetupMapColor(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals("c_black", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.Black;
            return true;
        }

        if (trimmed.Equals("c_white", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.White;
            return true;
        }

        if (trimmed.StartsWith('$') || trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
            if (trimmed.Length == 6
                && int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexRgb))
            {
                color = new Color((hexRgb >> 16) & 0xFF, (hexRgb >> 8) & 0xFF, hexRgb & 0xFF);
                return true;
            }

            return false;
        }

        if (trimmed.Length == 6
            && trimmed.All(static character => Uri.IsHexDigit(character))
            && int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            color = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return true;
        }

        if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gmlColor))
        {
            return false;
        }

        var red = gmlColor & 0xFF;
        var green = (gmlColor >> 8) & 0xFF;
        var blue = (gmlColor >> 16) & 0xFF;
        color = new Color(red, green, blue);
        return true;
    }
}
