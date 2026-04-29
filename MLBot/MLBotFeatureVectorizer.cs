using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot;

public static class MLBotFeatureVectorizer
{
    public const int FeatureCountV1 = 43;
    public const int FeatureCountV2 = 62;
    public const int FeatureCountV3 = 78;
    public const int FeatureCountV4 = 110;
    public const int FeatureCountV5 = 101;
    public const int FeatureCountV6 = 147;
    public const int FeatureCountV7 = 172;
    public const int FeatureCount = FeatureCountV7;
    public const float PositionScale = 2048f;
    public const float VelocityScale = 32f;
    public const float DistanceScale = 2048f;
    public const float TickScale = 300f;
    public const float MovementScale = 512f;
    public const float PhysicsScale = 1024f;
    public const float SizeScale = 96f;
    public const float PositionDeltaScale = 16f;
    public const float AirJumpScale = 4f;
    public const float ShortTickScale = 60f;

    public static float[] Vectorize(in MLBotObservation observation, MLBotObservationVectorSchema schema = MLBotObservationVectorSchema.V5)
    {
        return schema switch
        {
            MLBotObservationVectorSchema.V1 => VectorizeV1(observation),
            MLBotObservationVectorSchema.V2 => VectorizeV2(observation),
            MLBotObservationVectorSchema.V3 => VectorizeV3(observation),
            MLBotObservationVectorSchema.V4 => VectorizeV4(observation),
            MLBotObservationVectorSchema.V5 => VectorizeV5(observation),
            MLBotObservationVectorSchema.V6 => VectorizeV6(observation),
            _ => VectorizeV7(observation),
        };
    }

    public static int GetFeatureCount(MLBotObservationVectorSchema schema)
    {
        return schema switch
        {
            MLBotObservationVectorSchema.V1 => FeatureCountV1,
            MLBotObservationVectorSchema.V2 => FeatureCountV2,
            MLBotObservationVectorSchema.V3 => FeatureCountV3,
            MLBotObservationVectorSchema.V4 => FeatureCountV4,
            MLBotObservationVectorSchema.V5 => FeatureCountV5,
            MLBotObservationVectorSchema.V6 => FeatureCountV6,
            _ => FeatureCountV7,
        };
    }

    public static bool TryResolveSchema(int featureCount, out MLBotObservationVectorSchema schema)
    {
        schema = featureCount switch
        {
            FeatureCountV1 => MLBotObservationVectorSchema.V1,
            FeatureCountV2 => MLBotObservationVectorSchema.V2,
            FeatureCountV3 => MLBotObservationVectorSchema.V3,
            FeatureCountV4 => MLBotObservationVectorSchema.V4,
            FeatureCountV5 => MLBotObservationVectorSchema.V5,
            FeatureCountV6 => MLBotObservationVectorSchema.V6,
            FeatureCountV7 => MLBotObservationVectorSchema.V7,
            _ => default,
        };

        return featureCount is FeatureCountV1 or FeatureCountV2 or FeatureCountV3 or FeatureCountV4 or FeatureCountV5 or FeatureCountV6 or FeatureCountV7;
    }

    private static float[] VectorizeV1(in MLBotObservation observation)
    {
        var features = new float[FeatureCountV1];
        var index = 0;

        features[index++] = NormalizeSigned(observation.BotX, PositionScale);
        features[index++] = NormalizeSigned(observation.BotY, PositionScale);
        features[index++] = NormalizeSigned(observation.VelocityX, VelocityScale);
        features[index++] = NormalizeSigned(observation.VelocityY, VelocityScale);
        features[index++] = observation.IsGrounded ? 1f : 0f;
        features[index++] = observation.FacingDirectionX >= 0f ? 1f : -1f;
        features[index++] = Normalize01(observation.Health, Math.Max(1, observation.MaxHealth));
        features[index++] = observation.IsCarryingIntel ? 1f : 0f;

        AppendTaskPhaseOneHot(ref index, features, observation.TaskPhase);
        AppendClassProfileOneHot(ref index, features, observation.ClassId);

        features[index++] = observation.Objective.HasObjective ? 1f : 0f;
        features[index++] = NormalizeSigned(observation.Objective.RelativeX, DistanceScale);
        features[index++] = NormalizeSigned(observation.Objective.RelativeY, DistanceScale);
        features[index++] = NormalizeSigned(observation.Objective.HomeRelativeX, DistanceScale);
        features[index++] = NormalizeSigned(observation.Objective.HomeRelativeY, DistanceScale);

        features[index++] = Normalize01(observation.Probes.ForwardFootObstacleDistance, 64f);
        features[index++] = Normalize01(observation.Probes.ForwardHeadObstacleDistance, 64f);
        features[index++] = Normalize01(observation.Probes.GroundAheadDistance, 64f);
        features[index++] = Normalize01(observation.Probes.DropAheadDepth, 96f);
        features[index++] = Normalize01(observation.Probes.CeilingDistance, 64f);
        features[index++] = Normalize01(observation.Probes.LedgeHeightAhead, 96f);
        features[index++] = observation.Probes.BlockingGateAhead ? 1f : 0f;

        features[index++] = observation.NearestVisibleEnemy.Exists ? 1f : 0f;
        features[index++] = NormalizeSigned(observation.NearestVisibleEnemy.RelativeX, DistanceScale);
        features[index++] = NormalizeSigned(observation.NearestVisibleEnemy.RelativeY, DistanceScale);
        features[index++] = Normalize01(observation.NearestVisibleEnemy.Distance, DistanceScale);
        features[index++] = observation.NearestVisibleEnemy.HasLineOfSight ? 1f : 0f;

        features[index++] = observation.NearestVisibleTeammate.Exists ? 1f : 0f;
        features[index++] = NormalizeSigned(observation.NearestVisibleTeammate.RelativeX, DistanceScale);
        features[index++] = NormalizeSigned(observation.NearestVisibleTeammate.RelativeY, DistanceScale);

        features[index++] = Normalize01(observation.StuckTicks, TickScale);
        features[index++] = Normalize01(observation.ObjectiveDistance, DistanceScale);
        features[index++] = NormalizeSigned(observation.ObjectiveDistanceDelta, 128f);
        features[index++] = NormalizeControlPointOwner(observation.ControlPointOwner);
        features[index++] = NormalizeControlPointOwner(observation.ControlPointCappingTeam);
        features[index++] = Clamp01(observation.ControlPointCaptureProgress);
        features[index++] = observation.ControlPointLocked ? 1f : 0f;
        features[index++] = observation.IsRespawning ? 1f : 0f;

        return features;
    }

    private static float[] VectorizeV3(in MLBotObservation observation)
    {
        var features = new float[FeatureCountV3];
        VectorizeV2(observation).CopyTo(features, 0);
        var index = FeatureCountV2;

        features[index++] = observation.Waypoint.HasWaypoint ? 1f : 0f;
        features[index++] = NormalizeSigned(observation.Waypoint.RelativeX, DistanceScale);
        features[index++] = NormalizeSigned(observation.Waypoint.RelativeY, DistanceScale);
        features[index++] = Normalize01(observation.Waypoint.Distance, DistanceScale);
        features[index++] = observation.Waypoint.IsFinalWaypoint ? 1f : 0f;

        AppendSideProbeFeatures(ref index, features, observation.Probes);

        return features;
    }

    private static float[] VectorizeV4(in MLBotObservation observation)
    {
        var features = new float[FeatureCountV4];
        VectorizeV3(observation).CopyTo(features, 0);
        var index = FeatureCountV3;

        features[index++] = observation.Traversal.HasTraversal ? 1f : 0f;
        AppendTraversalLinkKindOneHot(ref index, features, observation.Traversal.LinkKind);
        features[index++] = Math.Clamp(observation.Traversal.ExpectedMoveDirection, -1f, 1f);
        features[index++] = NormalizeSigned(observation.Traversal.CurrentNodeRelativeX, DistanceScale);
        features[index++] = NormalizeSigned(observation.Traversal.CurrentNodeRelativeY, DistanceScale);
        features[index++] = NormalizeSigned(observation.Traversal.TargetNodeRelativeX, DistanceScale);
        features[index++] = NormalizeSigned(observation.Traversal.TargetNodeRelativeY, DistanceScale);
        features[index++] = NormalizeSigned(observation.Traversal.SegmentDeltaX, DistanceScale);
        features[index++] = NormalizeSigned(observation.Traversal.SegmentDeltaY, DistanceScale);
        features[index++] = Normalize01(observation.Traversal.SegmentDistance, DistanceScale);
        features[index++] = Clamp01(observation.Traversal.SegmentProgress);
        features[index++] = Normalize01(observation.Traversal.AllowedOvershootDistance, 256f);
        features[index++] = Normalize01(observation.Traversal.MaxSegmentTicks, TickScale);
        features[index++] = Clamp01(observation.Traversal.SegmentDifficulty);
        features[index++] = Normalize01(observation.Traversal.SegmentTicks, TickScale);
        features[index++] = Normalize01(observation.Traversal.AttemptCount, 8f);

        AppendPreviousActionFeatures(ref index, features, observation);

        return features;
    }

    private static float[] VectorizeV5(in MLBotObservation observation)
    {
        var features = new float[FeatureCountV5];
        VectorizeV2(observation).CopyTo(features, 0);
        var index = FeatureCountV2;

        AppendSideProbeFeatures(ref index, features, observation.Probes);
        AppendPreviousActionFeatures(ref index, features, observation);
        AppendControlPointObjectiveFeatures(ref index, features, observation.ControlPointObjective);

        return features;
    }

    private static float[] VectorizeV6(in MLBotObservation observation)
    {
        var features = new float[FeatureCountV6];
        VectorizeV5(observation).CopyTo(features, 0);
        var index = FeatureCountV5;

        AppendWorldTruthAffordanceFeatures(ref index, features, observation);

        return features;
    }

    private static float[] VectorizeV7(in MLBotObservation observation)
    {
        var features = new float[FeatureCountV7];
        VectorizeV6(observation).CopyTo(features, 0);
        var index = FeatureCountV6;

        AppendTerrainAffordanceFeatures(ref index, features, observation.TerrainAffordance);

        return features;
    }

    private static float[] VectorizeV2(in MLBotObservation observation)
    {
        var features = new float[FeatureCountV2];
        VectorizeV1(observation).CopyTo(features, 0);
        var index = FeatureCountV1;

        features[index++] = Normalize01(observation.RemainingAirJumps, Math.Max(1, observation.MaxAirJumps));
        features[index++] = Normalize01(observation.MaxAirJumps, (int)AirJumpScale);
        features[index++] = NormalizeSigned(observation.RunPower, 12f);
        features[index++] = NormalizeSigned(observation.MaxRunSpeed, MovementScale);
        features[index++] = NormalizeSigned(observation.GroundAcceleration, PhysicsScale);
        features[index++] = NormalizeSigned(observation.GroundDeceleration, PhysicsScale);
        features[index++] = NormalizeSigned(observation.Gravity, PhysicsScale);
        features[index++] = NormalizeSigned(observation.JumpSpeed, MovementScale);
        features[index++] = Normalize01(observation.Width, 64f);
        features[index++] = Normalize01(observation.Height, SizeScale);
        features[index++] = NormalizeSigned(observation.PreviousVelocityX, MovementScale);
        features[index++] = NormalizeSigned(observation.PreviousVelocityY, MovementScale);
        features[index++] = NormalizeSigned(observation.PreviousPositionDeltaX, PositionDeltaScale);
        features[index++] = NormalizeSigned(observation.PreviousPositionDeltaY, PositionDeltaScale);
        features[index++] = NormalizeSigned(observation.PreviousObjectiveDistanceDelta, 128f);
        features[index++] = observation.PreviousFacingDirectionX >= 0f ? 1f : -1f;
        features[index++] = observation.PreviousIsGrounded ? 1f : 0f;
        features[index++] = Normalize01(observation.AirborneTicks, ShortTickScale);
        features[index++] = Normalize01(observation.JumpTicks, ShortTickScale);

        return features;
    }

    private static void AppendTaskPhaseOneHot(ref int index, float[] features, MLBotTaskPhase taskPhase)
    {
        features[index++] = taskPhase == MLBotTaskPhase.AttackIntel ? 1f : 0f;
        features[index++] = taskPhase == MLBotTaskPhase.ReturnIntel ? 1f : 0f;
        features[index++] = taskPhase == MLBotTaskPhase.CaptureObjective ? 1f : 0f;
        features[index++] = taskPhase == MLBotTaskPhase.DefendObjective ? 1f : 0f;
    }

    private static void AppendClassProfileOneHot(ref int index, float[] features, PlayerClass classId)
    {
        features[index++] = classId switch
        {
            PlayerClass.Scout or PlayerClass.Sniper or PlayerClass.Spy => 1f,
            _ => 0f,
        };
        features[index++] = classId == PlayerClass.Heavy ? 1f : 0f;
        features[index++] = classId switch
        {
            PlayerClass.Engineer or PlayerClass.Pyro or PlayerClass.Soldier or PlayerClass.Demoman or PlayerClass.Medic or PlayerClass.Quote => 1f,
            _ => 0f,
        };
    }

    private static void AppendTraversalLinkKindOneHot(ref int index, float[] features, MLBotTraversalLinkKind linkKind)
    {
        features[index++] = linkKind == MLBotTraversalLinkKind.Walk ? 1f : 0f;
        features[index++] = linkKind == MLBotTraversalLinkKind.JumpUp ? 1f : 0f;
        features[index++] = linkKind == MLBotTraversalLinkKind.JumpAcross ? 1f : 0f;
        features[index++] = linkKind == MLBotTraversalLinkKind.DropDown ? 1f : 0f;
        features[index++] = linkKind == MLBotTraversalLinkKind.Climb ? 1f : 0f;
        features[index++] = linkKind == MLBotTraversalLinkKind.FallRecover ? 1f : 0f;
        features[index++] = linkKind == MLBotTraversalLinkKind.Gate ? 1f : 0f;
        features[index++] = linkKind == MLBotTraversalLinkKind.UnknownCandidate ? 1f : 0f;
    }

    private static void AppendSideProbeFeatures(ref int index, float[] features, MLBotProbeSnapshot probes)
    {
        features[index++] = Normalize01(probes.LeftFootObstacleDistance, 64f);
        features[index++] = Normalize01(probes.LeftHeadObstacleDistance, 64f);
        features[index++] = Normalize01(probes.RightFootObstacleDistance, 64f);
        features[index++] = Normalize01(probes.RightHeadObstacleDistance, 64f);
        features[index++] = Normalize01(probes.LeftGroundDistance, 64f);
        features[index++] = Normalize01(probes.RightGroundDistance, 64f);
        features[index++] = Normalize01(probes.LeftDropDepth, 96f);
        features[index++] = Normalize01(probes.RightDropDepth, 96f);
        features[index++] = probes.TouchingLeftWall ? 1f : 0f;
        features[index++] = probes.TouchingRightWall ? 1f : 0f;
        features[index++] = probes.TouchingCeiling ? 1f : 0f;
    }

    private static void AppendPreviousActionFeatures(ref int index, float[] features, in MLBotObservation observation)
    {
        features[index++] = NormalizeSigned(observation.PreviousMoveInput, 1f);
        features[index++] = observation.PreviousJumpPressed ? 1f : 0f;
        features[index++] = observation.PreviousJumpHeld ? 1f : 0f;
        features[index++] = observation.PreviousDropInput ? 1f : 0f;
        features[index++] = observation.PreviousActionFirePrimary ? 1f : 0f;
        features[index++] = observation.PreviousActionFireSecondary ? 1f : 0f;
        features[index++] = observation.PreviousActionDropIntel ? 1f : 0f;
        features[index++] = Normalize01(observation.FramesSinceJumpPressed, ShortTickScale);
        features[index++] = Normalize01(observation.FramesSinceJumpReleased, ShortTickScale);
    }

    private static void AppendControlPointObjectiveFeatures(
        ref int index,
        float[] features,
        MLBotControlPointObjectiveSnapshot controlPoint)
    {
        features[index++] = controlPoint.HasObjective ? 1f : 0f;
        features[index++] = Normalize01(controlPoint.Index, 8);
        features[index++] = NormalizeControlPointOwner(controlPoint.Owner);
        features[index++] = NormalizeControlPointOwner(controlPoint.CappingTeam);
        features[index++] = Clamp01(controlPoint.CaptureProgress);
        features[index++] = NormalizeSigned(controlPoint.CaptureProgressDelta, 1f);
        features[index++] = controlPoint.IsLocked ? 1f : 0f;
        features[index++] = Normalize01(controlPoint.FriendlyCappers, 6);
        features[index++] = Normalize01(controlPoint.EnemyCappers, 6);
        features[index++] = Normalize01(controlPoint.TotalCappers, 8);
        features[index++] = controlPoint.IsContested ? 1f : 0f;
        features[index++] = controlPoint.IsPlayerInCaptureZone ? 1f : 0f;
        features[index++] = Normalize01(controlPoint.TimeOnPointTicks, TickScale);
        features[index++] = Normalize01(controlPoint.TimeSinceLeftPointTicks, TickScale);
        features[index++] = Normalize01(controlPoint.FriendlyKothTimerTicksRemaining, 5400);
        features[index++] = Normalize01(controlPoint.EnemyKothTimerTicksRemaining, 5400);
        features[index++] = Normalize01(controlPoint.KothUnlockTicksRemaining, 900);
        features[index++] = controlPoint.IsKothMode ? 1f : 0f;
        features[index++] = controlPoint.IsDoubleKothMode ? 1f : 0f;
    }

    private static void AppendWorldTruthAffordanceFeatures(ref int index, float[] features, in MLBotObservation observation)
    {
        var objective = observation.Objective;
        var probes = observation.Probes;
        var controlPoint = observation.ControlPointObjective;
        var objectiveDirectionX = objective.HasObjective ? MathF.Sign(objective.RelativeX) : 0f;
        var objectiveDirectionY = objective.HasObjective ? MathF.Sign(objective.RelativeY) : 0f;

        features[index++] = observation.Team == PlayerTeam.Red ? 1f : -1f;
        features[index++] = observation.Mode == GameModeKind.CaptureTheFlag ? 1f : 0f;
        features[index++] = observation.Mode == GameModeKind.Arena ? 1f : 0f;
        features[index++] = observation.Mode == GameModeKind.ControlPoint ? 1f : 0f;
        features[index++] = observation.Mode == GameModeKind.KingOfTheHill ? 1f : 0f;
        features[index++] = observation.Mode == GameModeKind.DoubleKingOfTheHill ? 1f : 0f;
        features[index++] = observation.Mode == GameModeKind.Generator ? 1f : 0f;
        features[index++] = observation.Mode == GameModeKind.TeamDeathmatch ? 1f : 0f;

        features[index++] = objectiveDirectionX;
        features[index++] = objectiveDirectionY;
        features[index++] = Normalize01(MathF.Abs(objective.RelativeX), DistanceScale);
        features[index++] = Normalize01(MathF.Abs(objective.RelativeY), DistanceScale);
        features[index++] = NormalizeSigned(observation.VelocityX * objectiveDirectionX, MovementScale);
        features[index++] = NormalizeSigned(observation.VelocityY * objectiveDirectionY, MovementScale);
        features[index++] = NormalizeSigned(observation.PreviousPositionDeltaX * objectiveDirectionX, PositionDeltaScale);
        features[index++] = NormalizeSigned(observation.PreviousPositionDeltaY * objectiveDirectionY, PositionDeltaScale);

        var objectiveSideIsRight = objectiveDirectionX >= 0f;
        var objectiveSideFoot = objectiveSideIsRight ? probes.RightFootObstacleDistance : probes.LeftFootObstacleDistance;
        var objectiveSideHead = objectiveSideIsRight ? probes.RightHeadObstacleDistance : probes.LeftHeadObstacleDistance;
        var objectiveSideGround = objectiveSideIsRight ? probes.RightGroundDistance : probes.LeftGroundDistance;
        var objectiveSideDrop = objectiveSideIsRight ? probes.RightDropDepth : probes.LeftDropDepth;
        var oppositeSideFoot = objectiveSideIsRight ? probes.LeftFootObstacleDistance : probes.RightFootObstacleDistance;
        var oppositeSideHead = objectiveSideIsRight ? probes.LeftHeadObstacleDistance : probes.RightHeadObstacleDistance;
        var oppositeSideGround = objectiveSideIsRight ? probes.LeftGroundDistance : probes.RightGroundDistance;
        var oppositeSideDrop = objectiveSideIsRight ? probes.LeftDropDepth : probes.RightDropDepth;
        var touchingWallOnObjectiveSide = objectiveSideIsRight ? probes.TouchingRightWall : probes.TouchingLeftWall;
        var touchingWallOppositeObjective = objectiveSideIsRight ? probes.TouchingLeftWall : probes.TouchingRightWall;

        features[index++] = Normalize01(objectiveSideFoot, 64f);
        features[index++] = Normalize01(objectiveSideHead, 64f);
        features[index++] = Normalize01(objectiveSideGround, 64f);
        features[index++] = Normalize01(objectiveSideDrop, 96f);
        features[index++] = Normalize01(oppositeSideFoot, 64f);
        features[index++] = Normalize01(oppositeSideHead, 64f);
        features[index++] = Normalize01(oppositeSideGround, 64f);
        features[index++] = Normalize01(oppositeSideDrop, 96f);
        features[index++] = touchingWallOnObjectiveSide ? 1f : 0f;
        features[index++] = touchingWallOppositeObjective ? 1f : 0f;
        features[index++] = objectiveSideFoot <= 8f ? 1f : 0f;
        features[index++] = objectiveSideHead <= 8f ? 1f : 0f;

        features[index++] = objective.RelativeY < -12f ? 1f : 0f;
        features[index++] = objective.RelativeY > 12f ? 1f : 0f;
        features[index++] = MathF.Abs(objective.RelativeX) <= 32f ? 1f : 0f;
        features[index++] = MathF.Abs(objective.RelativeX) <= 96f ? 1f : 0f;
        features[index++] = MathF.Abs(objective.RelativeY) <= 32f ? 1f : 0f;
        features[index++] = MathF.Abs(objective.RelativeY) <= 96f ? 1f : 0f;
        features[index++] = observation.ObjectiveDistance <= 64f ? 1f : 0f;
        features[index++] = observation.ObjectiveDistance <= 128f ? 1f : 0f;
        features[index++] = observation.ObjectiveDistance <= 256f ? 1f : 0f;
        features[index++] = observation.ObjectiveDistance <= 512f ? 1f : 0f;

        var encodedTeam = observation.Team == PlayerTeam.Red ? 1 : 2;
        var owner = controlPoint.Owner;
        var cappingTeam = controlPoint.CappingTeam;
        features[index++] = owner == encodedTeam ? 1f : 0f;
        features[index++] = owner != 0 && owner != encodedTeam ? 1f : 0f;
        features[index++] = owner == 0 ? 1f : 0f;
        features[index++] = cappingTeam == encodedTeam ? 1f : 0f;
        features[index++] = cappingTeam != 0 && cappingTeam != encodedTeam ? 1f : 0f;
        features[index++] = cappingTeam == 0 ? 1f : 0f;
        features[index++] = controlPoint.HasObjective && (!controlPoint.IsLocked || controlPoint.IsKothMode) ? 1f : 0f;
        features[index++] = controlPoint.IsPlayerInCaptureZone
            && controlPoint.FriendlyCappers > 0
            && controlPoint.EnemyCappers <= 0
            && (controlPoint.CappingTeam == encodedTeam || controlPoint.Owner == encodedTeam)
                ? 1f
                : 0f;
    }

    private static void AppendTerrainAffordanceFeatures(
        ref int index,
        float[] features,
        MLBotTerrainAffordanceSnapshot affordance)
    {
        features[index++] = affordance.HasLeftLanding ? 1f : 0f;
        features[index++] = NormalizeSigned(affordance.LeftLandingRelativeX, DistanceScale);
        features[index++] = NormalizeSigned(affordance.LeftLandingRelativeY, DistanceScale);
        features[index++] = NormalizeSigned(affordance.LeftLandingSurfaceDeltaY, 384f);
        features[index++] = NormalizeSigned(affordance.LeftLandingObjectiveDistanceDelta, 512f);
        features[index++] = affordance.LeftLandingIsHigher ? 1f : 0f;
        features[index++] = affordance.LeftLandingRequiresJump ? 1f : 0f;

        features[index++] = affordance.HasRightLanding ? 1f : 0f;
        features[index++] = NormalizeSigned(affordance.RightLandingRelativeX, DistanceScale);
        features[index++] = NormalizeSigned(affordance.RightLandingRelativeY, DistanceScale);
        features[index++] = NormalizeSigned(affordance.RightLandingSurfaceDeltaY, 384f);
        features[index++] = NormalizeSigned(affordance.RightLandingObjectiveDistanceDelta, 512f);
        features[index++] = affordance.RightLandingIsHigher ? 1f : 0f;
        features[index++] = affordance.RightLandingRequiresJump ? 1f : 0f;

        features[index++] = affordance.HasBestUpwardLanding ? 1f : 0f;
        features[index++] = NormalizeSigned(affordance.BestUpwardLandingRelativeX, DistanceScale);
        features[index++] = NormalizeSigned(affordance.BestUpwardLandingRelativeY, DistanceScale);
        features[index++] = NormalizeSigned(affordance.BestUpwardLandingSurfaceDeltaY, 384f);
        features[index++] = NormalizeSigned(affordance.BestUpwardLandingObjectiveDistanceDelta, 512f);
        features[index++] = Math.Clamp(affordance.BestUpwardLandingDirection, -1f, 1f);
        features[index++] = affordance.BestUpwardLandingMovesAwayFromObjective ? 1f : 0f;
        features[index++] = Normalize01(affordance.BestUpwardLandingHorizontalGap, 384f);
        features[index++] = Normalize01(affordance.BestUpwardLandingHeadroom, 160f);
        features[index++] = Normalize01(affordance.CurrentSurfaceClearanceLeft, 384f);
        features[index++] = Normalize01(affordance.CurrentSurfaceClearanceRight, 384f);
    }

    private static float NormalizeControlPointOwner(int value)
    {
        return value switch
        {
            1 => -1f,
            2 => 1f,
            _ => 0f,
        };
    }

    private static float NormalizeSigned(float value, float scale)
    {
        return Math.Clamp(value / scale, -1f, 1f);
    }

    private static float Normalize01(float value, float maxValue)
    {
        return Clamp01(value / maxValue);
    }

    private static float Normalize01(int value, int maxValue)
    {
        return Clamp01((float)value / maxValue);
    }

    private static float Clamp01(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }
}
