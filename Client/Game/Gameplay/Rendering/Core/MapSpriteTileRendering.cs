#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class MapSpriteTileRendering
{
    public static void DrawTiledSprite(
        SpriteBatch spriteBatch,
        Texture2D texture,
        Rectangle areaDestination,
        float tileWidth,
        float tileHeight,
        MapSpriteTileAnchor anchor,
        Color color)
    {
        if (tileWidth <= 0.01f || tileHeight <= 0.01f || areaDestination.Width <= 0 || areaDestination.Height <= 0)
        {
            return;
        }

        var areaLeft = areaDestination.Left;
        var areaTop = areaDestination.Top;
        var areaRight = areaDestination.Right;
        var areaBottom = areaDestination.Bottom;
        var (anchorLeft, anchorTop) = MapSpriteTileMetadata.ResolveAnchorTopLeft(
            areaLeft,
            areaTop,
            areaDestination.Width,
            areaDestination.Height,
            tileWidth,
            tileHeight,
            anchor);
        var startCol = (int)MathF.Floor((areaLeft - anchorLeft) / tileWidth);
        var endCol = (int)MathF.Ceiling((areaRight - anchorLeft) / tileWidth);
        var startRow = (int)MathF.Floor((areaTop - anchorTop) / tileHeight);
        var endRow = (int)MathF.Ceiling((areaBottom - anchorTop) / tileHeight);

        for (var row = startRow; row < endRow; row += 1)
        {
            for (var col = startCol; col < endCol; col += 1)
            {
                var tileLeft = anchorLeft + (col * tileWidth);
                var tileTop = anchorTop + (row * tileHeight);
                var tileRight = tileLeft + tileWidth;
                var tileBottom = tileTop + tileHeight;
                var clipLeft = MathF.Max(tileLeft, areaLeft);
                var clipTop = MathF.Max(tileTop, areaTop);
                var clipRight = MathF.Min(tileRight, areaRight);
                var clipBottom = MathF.Min(tileBottom, areaBottom);
                if (clipRight <= clipLeft || clipBottom <= clipTop)
                {
                    continue;
                }

                var destinationWidth = clipRight - clipLeft;
                var destinationHeight = clipBottom - clipTop;
                var sourceX = ((clipLeft - tileLeft) / tileWidth) * texture.Width;
                var sourceY = ((clipTop - tileTop) / tileHeight) * texture.Height;
                var sourceWidth = (destinationWidth / tileWidth) * texture.Width;
                var sourceHeight = (destinationHeight / tileHeight) * texture.Height;
                spriteBatch.Draw(
                    texture,
                    new Rectangle(
                        (int)MathF.Floor(clipLeft),
                        (int)MathF.Floor(clipTop),
                        Math.Max(1, (int)MathF.Ceiling(destinationWidth)),
                        Math.Max(1, (int)MathF.Ceiling(destinationHeight))),
                    new Rectangle(
                        (int)MathF.Floor(sourceX),
                        (int)MathF.Floor(sourceY),
                        Math.Max(1, (int)MathF.Ceiling(sourceWidth)),
                        Math.Max(1, (int)MathF.Ceiling(sourceHeight))),
                    color);
            }
        }
    }
}
