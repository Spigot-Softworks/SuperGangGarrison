namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public MovementSystem Movement { get; }

    public IReadOnlyList<MovingPlatformRuntimeState> MovingPlatforms => Movement.MovingPlatforms;

    private void ResetMovingPlatformsForLevel()
        => Movement.ResetMovingPlatformsForLevel();

    public bool HasLandedArrowGroundSupport(PlayerEntity player, bool allowFallThrough)
        => Movement.HasLandedArrowGroundSupport(player, allowFallThrough);

    public void ResolveLandedArrowLanding(PlayerEntity player, float previousBottom, bool allowFallThrough)
        => Movement.ResolveLandedArrowLanding(player, previousBottom, allowFallThrough);

    public bool TryApplyJumpPadJumpBoostForPrediction(PlayerEntity player, bool jumped)
        => Movement.TryApplyJumpPadJumpBoostForPrediction(player, jumped);

    private void ResetTeleportTracking()
        => Movement.ResetTeleportTracking();

    public bool TryLatchWhippingCordToTerrain(PlayerEntity player, float aimWorldX, float aimWorldY)
        => Movement.TryLatchWhippingCordToTerrain(player, aimWorldX, aimWorldY);
}
