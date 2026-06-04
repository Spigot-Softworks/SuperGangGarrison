#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly Dictionary<string, Texture2D> _customMapSpriteTextureCache = new(StringComparer.OrdinalIgnoreCase);

    private void ClearCustomMapSpriteTextureCache()
    {
        foreach (var texture in _customMapSpriteTextureCache.Values)
        {
            texture.Dispose();
        }

        _customMapSpriteTextureCache.Clear();
    }

    private void DrawCustomMapGameplaySprites(Vector2 cameraPosition, CustomMapSpriteLayerKind layer)
    {
        var visuals = GetRuntimeCustomMapVisuals();
        if (visuals is null || visuals.SpriteResources.Count == 0)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        var imageScale = visuals.ImageScale;
        var parallaxLayers = _world.Level.CustomMapVisuals.ParallaxLayers;
        var sprites = new List<(int Index, RoomObjectMarker Marker)>();
        for (var index = 0; index < _world.Level.RoomObjects.Count; index += 1)
        {
            var marker = _world.Level.RoomObjects[index];
            if (marker.Type != RoomObjectType.CustomMapSprite
                || marker.CustomMapSprite.Layer != layer
                || !_world.Level.IsRoomObjectActive(index))
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
                || !visuals.SpriteResources.TryGetValue(resourceName, out var resource)
                || !TryGetCustomMapSpriteTexture(resource, out var texture))
            {
                continue;
            }

            var (relX, relY) = CustomMapSpriteParallax.WorldToScreen(
                marker.CenterX,
                marker.CenterY,
                layer,
                cameraPosition.X,
                cameraPosition.Y,
                viewport.Width,
                viewport.Height,
                parallaxLayers);
            var drawWidth = MathF.Max(1f, marker.Width);
            var drawHeight = MathF.Max(1f, marker.Height);
            var destination = new Rectangle(
                (int)MathF.Floor(relX - (drawWidth * 0.5f)),
                (int)MathF.Floor(relY - (drawHeight * 0.5f)),
                Math.Max(1, (int)MathF.Ceiling(drawWidth)),
                Math.Max(1, (int)MathF.Ceiling(drawHeight)));
            _spriteBatch.Draw(texture, destination, Color.White);
        }
    }

    private bool TryGetCustomMapSpriteTexture(CustomMapVisualResource resource, out Texture2D texture)
    {
        if (_customMapSpriteTextureCache.TryGetValue(resource.Name, out var cached))
        {
            texture = cached;
            return true;
        }

        if (!TryLoadCustomMapVisualTexture(resource, out texture))
        {
            return false;
        }

        _customMapSpriteTextureCache[resource.Name] = texture;
        return true;
    }
}
