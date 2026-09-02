namespace OpenGarrison.Core;

internal static class AirborneVelocityReachRules
{
    private const float MinimumBaselineSpeed = 0.0001f;

    public static float ResolveMultiplier(
        bool isGrounded,
        float horizontalSpeed,
        float verticalSpeed,
        float maxRunSpeed,
        float jumpSpeed,
        AirborneVelocityReachDefinition? definition)
    {
        if (definition is null || isGrounded)
        {
            return 1f;
        }

        var baselineSpeed = MathF.Sqrt(
            (maxRunSpeed * maxRunSpeed)
            + (jumpSpeed * jumpSpeed));
        if (!float.IsFinite(baselineSpeed) || baselineSpeed <= MinimumBaselineSpeed)
        {
            return 1f;
        }

        var currentSpeed = MathF.Sqrt(
            (horizontalSpeed * horizontalSpeed)
            + (verticalSpeed * verticalSpeed));
        if (!float.IsFinite(currentSpeed) || currentSpeed <= baselineSpeed)
        {
            return 1f;
        }

        var excessBaselineFraction = (currentSpeed / baselineSpeed) - 1f;
        var bonus = excessBaselineFraction * MathF.Max(0f, definition.BonusPerExcessBaseline);
        return Math.Clamp(1f + bonus, 1f, MathF.Max(1f, definition.MaxReachMultiplier));
    }

    public static float ResolveMultiplier(PlayerEntity player, AirborneVelocityReachDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(player);
        return ResolveMultiplier(
            player.IsGrounded,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.MaxRunSpeed,
            player.JumpSpeed,
            definition);
    }
}
