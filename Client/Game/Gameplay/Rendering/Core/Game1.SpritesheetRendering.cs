#nullable enable

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
        foreach (var (roomObjectIndex, marker) in GetCachedSpritesheets(layer))
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
