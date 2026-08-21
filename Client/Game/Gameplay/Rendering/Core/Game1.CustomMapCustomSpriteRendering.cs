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
    private SimpleLevel? _customMapLayerCacheLevel;
    private readonly Dictionary<CustomMapSpriteLayerKind, (int Index, RoomObjectMarker Marker)[]> _customMapSpriteLayerCache = new();
    private readonly Dictionary<CustomMapSpriteLayerKind, (int Index, RoomObjectMarker Marker)[]> _spritesheetLayerCache = new();

    private void ClearCustomMapSpriteTextureCache()
    {
        foreach (var texture in _customMapSpriteTextureCache.Values)
        {
            texture.Dispose();
        }

        _customMapSpriteTextureCache.Clear();
        _customMapLayerCacheLevel = null;
        _customMapSpriteLayerCache.Clear();
        _spritesheetLayerCache.Clear();
    }

    private (int Index, RoomObjectMarker Marker)[] GetCachedCustomMapSprites(CustomMapSpriteLayerKind layer)
    {
        EnsureCustomMapLayerCacheLevel();
        if (_customMapSpriteLayerCache.TryGetValue(layer, out var cached))
        {
            return cached;
        }

        cached = _world.Level.RoomObjects
            .Select(static (marker, index) => (Index: index, Marker: marker))
            .Where(entry => entry.Marker.Type == RoomObjectType.CustomMapSprite
                && entry.Marker.CustomMapSprite.Layer == layer)
            .OrderBy(static entry => entry.Marker.CustomMapSprite.ZOrder)
            .ThenBy(static entry => entry.Marker.CenterX)
            .ThenBy(static entry => entry.Marker.CenterY)
            .ToArray();
        _customMapSpriteLayerCache[layer] = cached;
        return cached;
    }

    private (int Index, RoomObjectMarker Marker)[] GetCachedSpritesheets(CustomMapSpriteLayerKind layer)
    {
        EnsureCustomMapLayerCacheLevel();
        if (_spritesheetLayerCache.TryGetValue(layer, out var cached))
        {
            return cached;
        }

        cached = _world.Level.RoomObjects
            .Select(static (marker, index) => (Index: index, Marker: marker))
            .Where(entry => entry.Marker.Type == RoomObjectType.Spritesheet
                && entry.Marker.Spritesheet.Layer == layer)
            .OrderBy(static entry => entry.Marker.Spritesheet.ZOrder)
            .ThenBy(static entry => entry.Marker.CenterX)
            .ThenBy(static entry => entry.Marker.CenterY)
            .ToArray();
        _spritesheetLayerCache[layer] = cached;
        return cached;
    }

    private void EnsureCustomMapLayerCacheLevel()
    {
        if (ReferenceEquals(_customMapLayerCacheLevel, _world.Level))
        {
            return;
        }

        // Resource names are only unique within a level.  A newly loaded map
        // may reuse a name with different bytes, so dispose the old texture
        // cache along with the marker-layer cache.
        foreach (var texture in _customMapSpriteTextureCache.Values)
        {
            texture.Dispose();
        }

        _customMapSpriteTextureCache.Clear();
        _customMapLayerCacheLevel = _world.Level;
        _customMapSpriteLayerCache.Clear();
        _spritesheetLayerCache.Clear();
    }

    private void DrawCustomMapGameplaySprites(Vector2 cameraPosition, CustomMapSpriteLayerKind layer)
    {
        var visuals = GetRuntimeCustomMapVisuals();
        var spriteResources = visuals?.SpriteResources;
        if (visuals is null || spriteResources is null || spriteResources.Count == 0)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        var parallaxLayers = _world.Level.CustomMapVisuals.ParallaxLayers;
        foreach (var (roomObjectIndex, marker) in GetCachedCustomMapSprites(layer))
        {
            if (!_world.Level.IsRoomObjectActive(roomObjectIndex))
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
            if (destination.Right <= 0
                || destination.Left >= viewport.Width
                || destination.Bottom <= 0
                || destination.Top >= viewport.Height)
            {
                continue;
            }

            var resourceName = marker.CustomMapSprite.ImageResourceName;
            if (string.IsNullOrWhiteSpace(resourceName)
                || !spriteResources.TryGetValue(resourceName, out var resource)
                || !TryGetCustomMapSpriteTexture(resource, out var texture))
            {
                continue;
            }

            var tint = Color.White * (layer == CustomMapSpriteLayerKind.Fg
                ? ApplyCoveredPlayerForegroundOpacity(1f, destination, cameraPosition)
                : 1f);
            if (marker.CustomMapSprite.Tile)
            {
                var tileWidth = MathF.Max(1f, texture.Width * marker.CustomMapSprite.Scale);
                var tileHeight = MathF.Max(1f, texture.Height * marker.CustomMapSprite.Scale);
                MapSpriteTileRendering.DrawTiledSprite(
                    _spriteBatch,
                    texture,
                    destination,
                    tileWidth,
                    tileHeight,
                    marker.CustomMapSprite.TileAnchor,
                    tint);
            }
            else
            {
                _spriteBatch.Draw(texture, destination, tint);
            }
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
