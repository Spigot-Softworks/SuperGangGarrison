using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Policies;

public sealed class DirectObjectivePolicyRuntime : IMLBotPolicyRuntime
{
    private const float HorizontalDeadZone = 6f;
    private const float FireDistance = 180f;

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        var aimX = observation.Objective.HasObjective
            ? observation.Objective.WorldX
            : observation.BotX + (observation.FacingDirectionX * 32f);
        var aimY = observation.Objective.HasObjective
            ? observation.Objective.WorldY
            : observation.BotY;

        if (observation.NearestVisibleEnemy.Exists && observation.NearestVisibleEnemy.HasLineOfSight)
        {
            aimX = observation.BotX + observation.NearestVisibleEnemy.RelativeX;
            aimY = observation.BotY + observation.NearestVisibleEnemy.RelativeY;
        }

        if (!observation.Objective.HasObjective || observation.IsRespawning)
        {
            return MLBotAction.Idle(aimX, aimY);
        }

        var moveDirection = observation.Objective.RelativeX switch
        {
            > HorizontalDeadZone => 1,
            < -HorizontalDeadZone => -1,
            _ => 0,
        };
        var objectiveAbove = observation.Objective.RelativeY < -12f;
        var jumpForObstacle = observation.Probes.ForwardFootObstacleDistance <= 12f
            || (observation.Probes.ForwardHeadObstacleDistance <= 12f && observation.Probes.CeilingDistance > 12f);
        var jumpForGap = observation.Probes.DropAheadDepth >= 24f && objectiveAbove;
        var jump = observation.IsGrounded && (jumpForObstacle || jumpForGap || objectiveAbove);

        var firePrimary = observation.NearestVisibleEnemy.Exists
            && observation.NearestVisibleEnemy.HasLineOfSight
            && observation.NearestVisibleEnemy.Distance <= FireDistance;

        return new MLBotAction(
            MoveDirection: moveDirection,
            Jump: jump,
            Crouch: false,
            FirePrimary: firePrimary,
            FireSecondary: false,
            DropIntel: false,
            AimWorldX: aimX,
            AimWorldY: aimY);
    }
}
