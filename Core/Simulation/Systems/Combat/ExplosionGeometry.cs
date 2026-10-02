namespace OpenGarrison.Core;

/// <summary>
/// Pure geometry shared by every explosive splash: distance from a blast origin
/// to a player's hit bounds, and the direction to the player's collision centre.
/// </summary>
internal static class ExplosionGeometry
{
    public static float GetDistanceToBounds(float left, float top, float right, float bottom, float originX, float originY)
    {
        var deltaX = 0f;
        if (originX < left)
        {
            deltaX = left - originX;
        }
        else if (originX > right)
        {
            deltaX = originX - right;
        }

        var deltaY = 0f;
        if (originY < top)
        {
            deltaY = top - originY;
        }
        else if (originY > bottom)
        {
            deltaY = originY - bottom;
        }

        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    public static void GetDirectionToPlayerCenter(PlayerEntity player, float originX, float originY, out float deltaX, out float deltaY, out float distance)
    {
        var centerX = player.X + ((player.CollisionLeftOffset + player.CollisionRightOffset) * 0.5f);
        var centerY = player.Y + ((player.CollisionTopOffset + player.CollisionBottomOffset) * 0.5f);
        deltaX = centerX - originX;
        deltaY = centerY - originY;
        distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}
