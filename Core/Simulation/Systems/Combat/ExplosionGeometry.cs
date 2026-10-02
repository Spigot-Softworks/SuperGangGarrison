namespace OpenGarrison.Core;

/// <summary>
/// Pure geometry shared by every explosive splash: distance from a blast origin
/// to a player's hit bounds, the direction to the player's collision centre, and
/// the GG2 knockback impulse that follows from them.
/// </summary>
internal static class ExplosionGeometry
{
    public const float SourceExplosionKnockbackCap = 15f;
    public const float ExplosiveJumpPadDamageMultiplier = 1.5f;

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

    public static void ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse)
    {
        if (impulse <= 0.0001f)
        {
            return;
        }

        GetDirectionToPlayerCenter(player, originX, originY, out var deltaX, out var deltaY, out var distance);
        if (distance <= 0.0001f)
        {
            player.AddImpulse(0f, -impulse);
            return;
        }

        player.AddImpulse((deltaX / distance) * impulse, (deltaY / distance) * impulse);
    }

    public static void ApplyMineExplosionImpulse(PlayerEntity player, float originX, float originY, float distanceFactor)
    {
        var impulse = GetExplosionImpulseMagnitude(
            player,
            originX,
            originY,
            MineProjectileEntity.BlastImpulse,
            distanceFactor,
            useMineVectorProfile: true);
        ApplyExplosionImpulse(player, originX, originY, impulse);
    }

    public static float GetExplosionImpulseMagnitude(
        PlayerEntity player,
        float originX,
        float originY,
        float knockbackPerTick,
        float distanceFactor,
        bool useMineVectorProfile)
    {
        if (distanceFactor <= 0f)
        {
            return 0f;
        }

        GetDirectionToPlayerCenter(player, originX, originY, out var deltaX, out var deltaY, out var distance);
        if (distance <= 0.0001f)
        {
            return MathF.Min(SourceExplosionKnockbackCap, knockbackPerTick * distanceFactor) * LegacyMovementModel.SourceTicksPerSecond;
        }

        var unitX = deltaX / distance;
        var unitY = deltaY / distance;
        var vectorFactor = useMineVectorProfile
            ? MathF.Sqrt((unitY * unitY) + (0.64f * unitX * unitX))
            : MathF.Sqrt((unitX * unitX * unitX * unitX) + (unitY * unitY * unitY * unitY));

        return MathF.Min(SourceExplosionKnockbackCap, knockbackPerTick * distanceFactor) * vectorFactor * LegacyMovementModel.SourceTicksPerSecond;
    }
}
