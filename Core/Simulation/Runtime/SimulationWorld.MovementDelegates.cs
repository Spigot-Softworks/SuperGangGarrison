namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public MovementSystem Movement { get; }

    public IReadOnlyList<MovingPlatformRuntimeState> MovingPlatforms => Movement.MovingPlatforms;

    private bool WouldRunIntoWall(PlayerEntity player, float moveDirection)
        => Movement.WouldRunIntoWall(player, moveDirection);

    private bool ApplyRoomForces(PlayerEntity player, bool jumpPressed = false)
        => Movement.ApplyRoomForces(player, jumpPressed);

    private void ResetMovingPlatformsForLevel()
        => Movement.ResetMovingPlatformsForLevel();

    private void AdvanceMovingPlatforms()
        => Movement.AdvanceMovingPlatforms();

    public bool HasLandedArrowGroundSupport(PlayerEntity player, bool allowFallThrough)
        => Movement.HasLandedArrowGroundSupport(player, allowFallThrough);

    public void ResolveLandedArrowLanding(PlayerEntity player, float previousBottom, bool allowFallThrough)
        => Movement.ResolveLandedArrowLanding(player, previousBottom, allowFallThrough);

    private void AdvanceJumpPads()
        => Movement.AdvanceJumpPads();

    public bool TryApplyJumpPadJumpBoostForPrediction(PlayerEntity player, bool jumped)
        => Movement.TryApplyJumpPadJumpBoostForPrediction(player, jumped);

    private void ApplyTeleportZones(PlayerEntity player)
        => Movement.ApplyTeleportZones(player);

    private void ResetTeleportTracking()
        => Movement.ResetTeleportTracking();

    private void ClearJumpInputBuffer(PlayerEntity player)
        => Movement.ClearJumpInputBuffer(player);

    public bool TryLatchWhippingCordToTerrain(PlayerEntity player, float aimWorldX, float aimWorldY)
        => Movement.TryLatchWhippingCordToTerrain(player, aimWorldX, aimWorldY);
}
