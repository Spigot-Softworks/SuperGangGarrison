using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

internal sealed partial class CombatResolver
{
    public bool TryFindWhippingCordTerrainHit(
        MeleeHitboxMask mask,
        float originX,
        float originY,
        float playerX,
        float playerY,
        float aimRadians,
        bool facingLeft,
        float scale,
        out float contactX,
        out float contactY)
    {
        contactX = 0f;
        contactY = 0f;
        var found = false;
        var nearestDistanceSquared = float.PositiveInfinity;
        mask.GetWorldBounds(
            originX, originY, facingLeft, scale,
            out var left, out var top, out var right, out var bottom, aimRadians);
        var candidates = GetPotentialSolidRaycastCandidates(
            new RectangleHitbox(left, top, right, bottom));
        for (var pixelY = 0; pixelY < mask.Height; pixelY += 1)
        {
            for (var pixelX = 0; pixelX < mask.Width; pixelX += 1)
            {
                if (!mask.IsOpaqueAtPixel(pixelX, pixelY))
                {
                    continue;
                }

                mask.PixelToWorld(
                    pixelX, pixelY, originX, originY, facingLeft, scale,
                    out var worldX, out var worldY, aimRadians);
                var playerDeltaX = worldX - playerX;
                var playerDeltaY = worldY - playerY;
                if (playerDeltaX * playerDeltaX + playerDeltaY * playerDeltaY
                    < WhippingCordCatalog.MinimumRopeLength * WhippingCordCatalog.MinimumRopeLength)
                {
                    continue;
                }

                var originDeltaX = worldX - originX;
                var originDeltaY = worldY - originY;
                var distanceSquared = originDeltaX * originDeltaX + originDeltaY * originDeltaY;
                if (distanceSquared >= nearestDistanceSquared)
                {
                    continue;
                }

                foreach (var solid in candidates)
                {
                    if (worldX < solid.Left || worldX >= solid.Right
                        || worldY < solid.Top || worldY >= solid.Bottom)
                    {
                        continue;
                    }

                    nearestDistanceSquared = distanceSquared;
                    contactX = worldX;
                    contactY = worldY;
                    found = true;
                    break;
                }
            }
        }

        return found;
    }
}
