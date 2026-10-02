using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool TryFindWhippingCordTerrainContact(
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

    private bool TryFindWhippingCordTerrainContact(
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

        return GeometryResolver.TryFindWhippingCordTerrainHit(
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
