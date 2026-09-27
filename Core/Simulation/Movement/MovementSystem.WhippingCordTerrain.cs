using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class MovementSystem
{
    /// <summary>Shared by the server and local prediction at the whip's extended frame.</summary>
    public bool TryLatchWhippingCordToTerrain(PlayerEntity player, float aimWorldX, float aimWorldY)
    {
        if (!player.IsAlive || Level.IsTopDown || player.IsServerNoclip
            || !player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.WhippingCord)
            || !player.IsWhippingCordSwingActive || player.IsWhippingCordLatched)
        {
            return false;
        }

        var itemId = player.GameplayLoadoutState.PrimaryItemId;
        if (string.IsNullOrWhiteSpace(itemId)
            || !CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item))
        {
            return false;
        }

        if (!_dependencies.TryFindWhippingCordTerrainContact(
                player,
                item,
                aimWorldX,
                aimWorldY,
                out var anchorX,
                out var anchorY))
        {
            return false;
        }

        var contactX = anchorX - player.X;
        var contactY = anchorY - player.Y;
        var ropeLength = MathF.Sqrt(contactX * contactX + contactY * contactY);
        var originX = player.X + item.Presentation.WeaponOffsetX
            * (aimWorldX < player.X ? -1f : 1f) * player.PlayerScale;
        var originY = player.Y + item.Presentation.WeaponOffsetY * player.PlayerScale;
        var rayX = anchorX - originX;
        var rayY = anchorY - originY;
        var rayDistance = MathF.Sqrt(rayX * rayX + rayY * rayY);
        if (ropeLength < WhippingCordCatalog.MinimumRopeLength || rayDistance <= 0.0001f)
        {
            return false;
        }

        // A player or structure between the hand and the contact pixel blocks
        // the latch. Terrain itself is resolved by the opaque whip pixels.
        if (!_dependencies.IsWhippingCordTerrainLatchPathClear(
                player,
                originX,
                originY,
                rayX / rayDistance,
                rayY / rayDistance,
                rayDistance))
        {
            return false;
        }

        player.LatchWhippingCord(anchorX, anchorY, ropeLength);
        return player.IsWhippingCordLatched;
    }
}
