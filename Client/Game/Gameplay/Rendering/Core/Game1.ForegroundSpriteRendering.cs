#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float CoveredPlayerForegroundOpacity = 0.45f;

    private void DrawForegroundSprites(Vector2 cameraPosition, ForegroundSpriteLayerKind layer)
    {
        var visuals = GetRuntimeCustomMapVisuals();
        var spriteResources = visuals?.SpriteResources;

        var sprites = new List<(int Index, RoomObjectMarker Marker)>();
        for (var index = 0; index < _world.Level.RoomObjects.Count; index += 1)
        {
            var marker = _world.Level.RoomObjects[index];
            if (marker.Type != RoomObjectType.ForegroundSprite
                || marker.ForegroundSprite.Layer != layer
                || !_world.Level.IsRoomObjectActive(index))
            {
                continue;
            }

            sprites.Add((index, marker));
        }

        foreach (var (roomObjectIndex, marker) in sprites
                     .OrderBy(static entry => entry.Marker.ForegroundSprite.RelativeZ)
                     .ThenBy(static entry => entry.Marker.CenterX)
                     .ThenBy(static entry => entry.Marker.CenterY))
        {
            var resourceName = marker.ForegroundSprite.ImageResourceName;
            if (string.IsNullOrWhiteSpace(resourceName)
                || spriteResources is null
                || !spriteResources.TryGetValue(resourceName, out var resource)
                || !TryGetCustomMapSpriteTexture(resource, out var texture))
            {
                continue;
            }

            var screenX = marker.CenterX - cameraPosition.X;
            var screenY = marker.CenterY - cameraPosition.Y;
            var drawWidth = MathF.Max(1f, marker.Width);
            var drawHeight = MathF.Max(1f, marker.Height);
            var destination = new Rectangle(
                (int)MathF.Floor(screenX - (drawWidth * 0.5f)),
                (int)MathF.Floor(screenY - (drawHeight * 0.5f)),
                Math.Max(1, (int)MathF.Ceiling(drawWidth)),
                Math.Max(1, (int)MathF.Ceiling(drawHeight)));
            var opacity = ApplyCoveredPlayerForegroundOpacity(
                ResolveForegroundSpriteDrawOpacity(roomObjectIndex, marker.ForegroundSprite),
                destination,
                cameraPosition);
            var tint = Color.White * opacity;
            if (marker.ForegroundSprite.Tile)
            {
                var tileWidth = MathF.Max(1f, texture.Width * marker.ForegroundSprite.Scale);
                var tileHeight = MathF.Max(1f, texture.Height * marker.ForegroundSprite.Scale);
                MapSpriteTileRendering.DrawTiledSprite(
                    _spriteBatch,
                    texture,
                    destination,
                    tileWidth,
                    tileHeight,
                    marker.ForegroundSprite.TileAnchor,
                    tint);
            }
            else
            {
                _spriteBatch.Draw(texture, destination, tint);
            }
        }
    }

    private float ResolveForegroundSpriteDrawOpacity(int roomObjectIndex, ForegroundSpriteConfiguration configuration)
    {
        if (!configuration.Jungle)
        {
            return 1f;
        }

        var opacity = configuration.OutsideOpacity;
        if (_world.LocalPlayer.TryGetReplicatedStateBool(
                ForegroundSpriteMetadata.JungleReplicatedStateOwnerId,
                ForegroundSpriteMetadata.JungleReplicatedStateKey(roomObjectIndex),
                out var isInside)
            && isInside)
        {
            opacity = configuration.InsideOpacity;
        }

        return Math.Clamp(opacity, 0f, 1f);
    }

    private float ApplyCoveredPlayerForegroundOpacity(float baseOpacity, Rectangle destination, Vector2 cameraPosition)
    {
        if (baseOpacity <= CoveredPlayerForegroundOpacity
            || destination.Width <= 0
            || destination.Height <= 0)
        {
            return Math.Clamp(baseOpacity, 0f, 1f);
        }

        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || GetPlayerVisibilityAlpha(player) <= 0f)
            {
                continue;
            }

            var playerBounds = GetPlayerScreenBounds(player, GetRenderPosition(player), cameraPosition);
            if (destination.Intersects(playerBounds))
            {
                return CoveredPlayerForegroundOpacity;
            }
        }

        return Math.Clamp(baseOpacity, 0f, 1f);
    }
}
