#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawSpritesheets(Vector2 cameraPosition, CustomMapSpriteLayerKind layer)
    {
        var visuals = GetRuntimeCustomMapVisuals();
        var spriteResources = visuals?.SpriteResources;
        if (visuals is null || spriteResources is null || spriteResources.Count == 0)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        var parallaxLayers = _world.Level.CustomMapVisuals.ParallaxLayers;
        var sprites = new List<(int Index, RoomObjectMarker Marker)>();
        for (var index = 0; index < _world.Level.RoomObjects.Count; index += 1)
        {
            var marker = _world.Level.RoomObjects[index];
            if (marker.Type != RoomObjectType.Spritesheet
                || marker.Spritesheet.Layer != layer
                || !_world.Level.IsRoomObjectActive(index))
            {
                continue;
            }

            sprites.Add((index, marker));
        }

        foreach (var (roomObjectIndex, marker) in sprites
                     .OrderBy(static entry => entry.Marker.Spritesheet.ZOrder)
                     .ThenBy(static entry => entry.Marker.CenterX)
                     .ThenBy(static entry => entry.Marker.CenterY))
        {
            var resourceName = marker.Spritesheet.ImageResourceName;
            if (string.IsNullOrWhiteSpace(resourceName)
                || !spriteResources.TryGetValue(resourceName, out var resource)
                || !TryGetCustomMapSpriteTexture(resource, out var texture))
            {
                continue;
            }

            var frameIndex = _world.GetSpritesheetFrame(roomObjectIndex);
            var source = SpritesheetMetadata.ResolveFrameSourceRectangle(
                texture.Width,
                texture.Height,
                frameIndex,
                marker.Spritesheet);
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
            var tint = Color.White * (layer == CustomMapSpriteLayerKind.Fg
                ? ApplyCoveredPlayerForegroundOpacity(1f, destination, cameraPosition)
                : 1f);
            _spriteBatch.Draw(
                texture,
                destination,
                new Rectangle(source.X, source.Y, source.Width, source.Height),
                tint);
        }
    }
}
