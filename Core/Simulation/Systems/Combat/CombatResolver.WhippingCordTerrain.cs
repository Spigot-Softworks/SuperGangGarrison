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

    public bool TryFindWhippingCordTerrainContact(
        PlayerEntity player,
        float aimWorldX,
        float aimWorldY,
        out float contactX,
        out float contactY)
    {
        contactX = 0f;
        contactY = 0f;
        var itemId = player.GameplayLoadoutState.PrimaryItemId;
        return !string.IsNullOrWhiteSpace(itemId)
            && CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item)
            && TryFindWhippingCordTerrainContact(
                player, item, aimWorldX, aimWorldY, out contactX, out contactY);
    }

    public bool TryFindWhippingCordTerrainContact(
        PlayerEntity player,
        GameplayItemDefinition item,
        float aimWorldX,
        float aimWorldY,
        out float contactX,
        out float contactY)
    {
        contactX = 0f;
        contactY = 0f;
        var spriteName = string.IsNullOrWhiteSpace(item.Presentation.RecoilSpriteName)
            ? WhippingCordCatalog.WhipRecoilSpriteName
            : item.Presentation.RecoilSpriteName;
        var mask = MeleeHitboxMaskCatalog.GetOrLoad(
            spriteName, WhippingCordCatalog.ExtendedWhipFrameIndex);
        if (mask is null)
        {
            return false;
        }

        var facingLeft = aimWorldX < player.X;
        var originX = player.X + item.Presentation.WeaponOffsetX
            * (facingLeft ? -1f : 1f) * player.PlayerScale;
        var originY = player.Y + item.Presentation.WeaponOffsetY * player.PlayerScale;
        var aimX = aimWorldX - originX;
        var aimY = aimWorldY - originY;
        if (aimX * aimX + aimY * aimY <= 0.00000001f)
        {
            return false;
        }

        return TryFindWhippingCordTerrainHit(
            mask,
            originX,
            originY,
            player.X,
            player.Y,
            DeterministicMath.Atan2(aimY, aimX),
            facingLeft,
            player.PlayerScale,
            out contactX,
            out contactY);
    }
}
