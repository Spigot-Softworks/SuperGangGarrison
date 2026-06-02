#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private CustomMapVisualMetadata? _loadedCustomMapVisualsSource;
    private RuntimeCustomMapVisuals? _loadedCustomMapVisuals;

    private void DrawCustomMapBackdrop(Rectangle worldRectangle)
    {
        var visuals = GetRuntimeCustomMapVisuals();
        if (visuals?.BackgroundColor is null)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), visuals.BackgroundColor.Value);
    }

    private void DrawCustomMapParallaxBackgrounds(Vector2 cameraPosition)
    {
        var visuals = GetRuntimeCustomMapVisuals();
        if (visuals is null || visuals.ParallaxLayers.Count == 0)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        foreach (var layer in visuals.ParallaxLayers)
        {
            DrawCustomMapParallaxLayer(layer, cameraPosition, viewport.Width, viewport.Height, visuals.ImageScale);
        }
    }

    private void DrawCustomMapForegroundAndVoid(Vector2 cameraPosition, Rectangle worldRectangle)
    {
        var visuals = GetRuntimeCustomMapVisuals();
        if (visuals is null)
        {
            return;
        }

        if (visuals.Foreground is not null)
        {
            _spriteBatch.Draw(
                visuals.Foreground,
                new Vector2(
                    -cameraPosition.X + visuals.ForegroundOffsetX,
                    -cameraPosition.Y + visuals.ForegroundOffsetY),
                null,
                Color.White,
                0f,
                Vector2.Zero,
                visuals.ImageScale,
                SpriteEffects.None,
                0f);
        }

        if (visuals.VoidColor is not null)
        {
            DrawCustomMapVoidBorders(worldRectangle, visuals.VoidColor.Value);
        }
    }

    private void DrawCustomMapParallaxLayer(RuntimeCustomMapParallaxLayer layer, Vector2 cameraPosition, int viewportWidth, int viewportHeight, float imageScale)
    {
        var texture = layer.Texture;
        var scaledWidth = texture.Width * imageScale;
        var scaledHeight = texture.Height * imageScale;
        if (scaledWidth <= 0.01f || scaledHeight <= 0.01f)
        {
            return;
        }

        var xOrigin = cameraPosition.X + (viewportWidth / 2f) - (scaledWidth / 2f);
        var yOrigin = cameraPosition.Y + (viewportHeight / 2f) - (scaledHeight / 2f);
        var xParallax = MathF.Abs(layer.XFactor) > 0.0001f
            ? xOrigin / (layer.XFactor * layer.XFactor)
            : 0f;
        var yParallax = MathF.Abs(layer.YFactor) > 0.0001f
            ? yOrigin / (layer.YFactor * layer.YFactor)
            : 0f;

        var worldX = xOrigin - xParallax;
        var screenY = yOrigin - yParallax - cameraPosition.Y;
        var firstScreenX = worldX - cameraPosition.X;
        while (firstScreenX > 0f)
        {
            firstScreenX -= scaledWidth;
        }

        for (var screenX = firstScreenX; screenX < viewportWidth; screenX += scaledWidth)
        {
            _spriteBatch.Draw(
                texture,
                new Vector2(screenX, screenY),
                null,
                Color.White,
                0f,
                Vector2.Zero,
                imageScale,
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawCustomMapVoidBorders(Rectangle worldRectangle, Color color)
    {
        var viewport = GraphicsDevice.Viewport;
        if (worldRectangle.Top > 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, worldRectangle.Top), color);
        }

        if (worldRectangle.Bottom < viewport.Height)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, worldRectangle.Bottom, viewport.Width, viewport.Height - worldRectangle.Bottom), color);
        }

        if (worldRectangle.Left > 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, Math.Max(0, worldRectangle.Top), worldRectangle.Left, Math.Min(viewport.Height, worldRectangle.Height)), color);
        }

        if (worldRectangle.Right < viewport.Width)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(worldRectangle.Right, Math.Max(0, worldRectangle.Top), viewport.Width - worldRectangle.Right, Math.Min(viewport.Height, worldRectangle.Height)), color);
        }
    }

    private RuntimeCustomMapVisuals? GetRuntimeCustomMapVisuals()
    {
        var source = _world.Level.CustomMapVisuals;
        if (ReferenceEquals(source, CustomMapVisualMetadata.Empty))
        {
            ClearRuntimeCustomMapVisuals();
            return null;
        }

        if (ReferenceEquals(source, _loadedCustomMapVisualsSource))
        {
            return _loadedCustomMapVisuals;
        }

        ClearRuntimeCustomMapVisuals();
        _loadedCustomMapVisualsSource = source;
        _loadedCustomMapVisuals = LoadRuntimeCustomMapVisuals(source);
        return _loadedCustomMapVisuals;
    }

    private RuntimeCustomMapVisuals LoadRuntimeCustomMapVisuals(CustomMapVisualMetadata source)
    {
        var layers = new List<RuntimeCustomMapParallaxLayer>();
        foreach (var layer in source.ParallaxLayers.OrderBy(static layer => layer.Index))
        {
            if (TryLoadCustomMapVisualTexture(layer.Resource, out var texture))
            {
                layers.Add(new RuntimeCustomMapParallaxLayer(texture, layer.XFactor, layer.YFactor));
            }
        }

        Texture2D? foreground = null;
        if (source.Foreground is not null)
        {
            if (TryLoadCustomMapVisualTexture(source.Foreground, out var foregroundTexture))
            {
                foreground = foregroundTexture;
            }
        }

        return new RuntimeCustomMapVisuals(
            MathF.Max(0.01f, source.ImageScale),
            TryParseGmlColor(source.BackgroundColor, out var backgroundColor) ? backgroundColor : null,
            TryParseGmlColor(source.VoidColor, out var voidColor) ? voidColor : null,
            layers,
            foreground,
            LoadRuntimeCustomMapVisualResources(source),
            source.ForegroundOffsetX,
            source.ForegroundOffsetY);
    }

    private Dictionary<string, Texture2D> LoadRuntimeCustomMapVisualResources(CustomMapVisualMetadata source)
    {
        var resources = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in source.Resources.Values)
        {
            if (TryLoadCustomMapVisualTexture(resource, out var texture))
            {
                resources[resource.Name] = texture;
            }
        }

        return resources;
    }

    private bool TryLoadCustomMapVisualTexture(CustomMapVisualResource resource, out Texture2D texture)
    {
        try
        {
            texture = TextureDecodeUtility.LoadTexture(GraphicsDevice, resource.Bytes, applyLegacyChromaKey: false);
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

    private void ClearRuntimeCustomMapVisuals()
    {
        _loadedCustomMapVisuals?.Dispose();
        _loadedCustomMapVisuals = null;
        _loadedCustomMapVisualsSource = null;
    }

    private bool TryGetRuntimeCustomMapVisualResourceTexture(string resourceName, out Texture2D texture)
    {
        texture = null!;
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return false;
        }

        var visuals = GetRuntimeCustomMapVisuals();
        if (visuals is null)
        {
            return false;
        }

        if (visuals.Resources.TryGetValue(resourceName.Trim(), out var foundTexture))
        {
            texture = foundTexture;
            return true;
        }

        return false;
    }

    private static bool TryParseGmlColor(string? rawValue, out Color color)
    {
        color = Color.Transparent;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var value = rawValue.Trim();
        if (value.Equals("c_black", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.Black;
            return true;
        }

        if (value.Equals("c_white", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.White;
            return true;
        }

        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length == 6
            && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            color = new Color((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gmlColor))
        {
            color = new Color(gmlColor & 0xff, (gmlColor >> 8) & 0xff, (gmlColor >> 16) & 0xff);
            return true;
        }

        return false;
    }

    private sealed record RuntimeCustomMapParallaxLayer(Texture2D Texture, float XFactor, float YFactor);

    private sealed class RuntimeCustomMapVisuals : IDisposable
    {
        public RuntimeCustomMapVisuals(
            float imageScale,
            Color? backgroundColor,
            Color? voidColor,
            IReadOnlyList<RuntimeCustomMapParallaxLayer> parallaxLayers,
            Texture2D? foreground,
            IReadOnlyDictionary<string, Texture2D> resources,
            float foregroundOffsetX,
            float foregroundOffsetY)
        {
            ImageScale = imageScale;
            BackgroundColor = backgroundColor;
            VoidColor = voidColor;
            ParallaxLayers = parallaxLayers;
            Foreground = foreground;
            Resources = resources;
            ForegroundOffsetX = foregroundOffsetX;
            ForegroundOffsetY = foregroundOffsetY;
        }

        public float ImageScale { get; }

        public Color? BackgroundColor { get; }

        public Color? VoidColor { get; }

        public IReadOnlyList<RuntimeCustomMapParallaxLayer> ParallaxLayers { get; }

        public Texture2D? Foreground { get; }

        public IReadOnlyDictionary<string, Texture2D> Resources { get; }

        public float ForegroundOffsetX { get; }

        public float ForegroundOffsetY { get; }

        public void Dispose()
        {
            foreach (var layer in ParallaxLayers)
            {
                layer.Texture.Dispose();
            }

            Foreground?.Dispose();
            foreach (var resource in Resources.Values)
            {
                resource.Dispose();
            }
        }
    }
}
