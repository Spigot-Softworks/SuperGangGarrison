using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Environment;

public sealed class MLBotEnvironment
{
    private const int HardStuckTerminalTicks = 180;
    private const int NoProgressTerminalTicks = 240;
    private const int LocalLoopTerminalTicks = 150;

    private readonly SimulationWorld _world;
    private readonly MLBotObservationRuntimeState _runtimeState = new();
    private int _ticksElapsed;
    private float _bestObjectiveDistance;
    private float _bestNavigationDistance;
    private int _noProgressTicks;
    private int _localLoopTicks;
    private float _lastProgressX;
    private float _lastProgressY;
    private int _startingRedCaps;
    private int _startingBlueCaps;
    private int _startingLocalDeaths;
    private readonly Dictionary<int, PlayerTeam?> _startingControlPointOwnersByIndex = new();
    private MLBotEpisodeConfig? _episodeConfig;

    public MLBotEnvironment(SimulationWorld? world = null)
    {
        _world = world ?? new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
    }

    public SimulationWorld World => _world;

    public MLBotObservation Reset(MLBotEpisodeConfig config)
    {
        _episodeConfig = config;
        _ticksElapsed = 0;
        _bestObjectiveDistance = float.MaxValue;
        _bestNavigationDistance = float.MaxValue;
        _noProgressTicks = 0;
        _localLoopTicks = 0;
        _lastProgressX = 0f;
        _lastProgressY = 0f;
        MLBotObservationRuntimeStateTracker.Reset(_runtimeState);

        if (!_world.TryLoadLevel(config.LevelName, config.MapAreaIndex, preservePlayerStats: false))
        {
            throw new InvalidOperationException($"Could not load level '{config.LevelName}'.");
        }

        _world.ConfigureMatchDefaults(respawnSeconds: 0);
        _world.PrepareLocalPlayerJoin();
        _world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, config.Team);
        _world.TryApplyNetworkPlayerClassSelection(SimulationWorld.LocalPlayerSlot, config.ClassId);

        ApplyEpisodeStartState(config);
        MLBotObservationRuntimeStateTracker.SeedEpisodeStart(_runtimeState, config, _world.LocalPlayer);

        _startingRedCaps = _world.RedCaps;
        _startingBlueCaps = _world.BlueCaps;
        _startingLocalDeaths = _world.LocalPlayer.Deaths;
        _startingControlPointOwnersByIndex.Clear();
        for (var index = 0; index < _world.ControlPoints.Count; index += 1)
        {
            var point = _world.ControlPoints[index];
            _startingControlPointOwnersByIndex[point.Index] = point.Team;
        }

        var taskPhase = MLBotTaskStateResolver.Resolve(_world, _world.LocalPlayer);
        var observation = MLBotObservationBuilder.Build(
            _world,
            SimulationWorld.LocalPlayerSlot,
            _world.LocalPlayer,
            taskPhase,
            _runtimeState);
        MLBotObservationRuntimeStateTracker.Update(_runtimeState, observation, _world.LocalPlayer);
        _bestObjectiveDistance = observation.ObjectiveDistance;
        _bestNavigationDistance = GetNavigationDistance(observation);
        _lastProgressX = observation.BotX;
        _lastProgressY = observation.BotY;
        return observation;
    }

    public MLBotStepResult Step(MLBotAction action)
    {
        if (_episodeConfig is null)
        {
            throw new InvalidOperationException("Reset must be called before Step.");
        }

        _world.SetLocalInput(MLBotActionDecoder.Decode(action));
        _world.AdvanceOneTick();
        _ticksElapsed += 1;
        MLBotObservationRuntimeStateTracker.RecordAction(_runtimeState, action);

        var taskPhase = MLBotTaskStateResolver.Resolve(_world, _world.LocalPlayer);
        var observation = MLBotObservationBuilder.Build(
            _world,
            SimulationWorld.LocalPlayerSlot,
            _world.LocalPlayer,
            taskPhase,
            _runtimeState);
        MLBotObservationRuntimeStateTracker.Update(_runtimeState, observation, _world.LocalPlayer);

        UpdateProgressCounters(_episodeConfig.TaskPhase, observation);
        var (success, terminalReason) = EvaluateTerminalState(_episodeConfig, _world);
        var timedOut = _ticksElapsed >= _episodeConfig.MaxTicks;
        var stallTerminalReason = success || terminalReason == "death" || timedOut
            ? string.Empty
            : EvaluateStallTerminalState(_episodeConfig.TaskPhase, observation);
        var stalled = !string.IsNullOrWhiteSpace(stallTerminalReason);
        var terminal = success || timedOut || terminalReason == "death" || stalled;
        var reward = BuildReward(
            _episodeConfig.TaskPhase,
            observation,
            action,
            success,
            timedOut,
            terminalReason == "death",
            stalled,
            _bestObjectiveDistance,
            _bestNavigationDistance,
            _runtimeState.PreviousNavigationDistanceDelta,
            _noProgressTicks,
            _localLoopTicks);
        _bestObjectiveDistance = MathF.Min(_bestObjectiveDistance, observation.ObjectiveDistance);
        _bestNavigationDistance = MathF.Min(_bestNavigationDistance, GetNavigationDistance(observation));
        var resolvedTerminalReason = success
            ? terminalReason
            : timedOut
                ? "timeout"
                : stalled
                    ? stallTerminalReason
                    : terminalReason;

        return new MLBotStepResult(
            Observation: observation,
            Reward: reward,
            IsTerminal: terminal,
            IsSuccess: success,
            Tick: _ticksElapsed,
            TerminalReason: resolvedTerminalReason);
    }

    private static MLBotRewardBreakdown BuildReward(
        MLBotTaskPhase taskPhase,
        in MLBotObservation observation,
        in MLBotAction action,
        bool success,
        bool timedOut,
        bool died,
        bool stalled,
        float bestObjectiveDistance,
        float bestNavigationDistance,
        float navigationDistanceDelta,
        int noProgressTicks,
        int localLoopTicks)
    {
        var progressScale = taskPhase switch
        {
            MLBotTaskPhase.AttackIntel => 0.0125f,
            MLBotTaskPhase.ReturnIntel => 0.015f,
            MLBotTaskPhase.CaptureObjective => 0.0125f,
            _ => 0.01f,
        };

        var verticalGapClosing = ComputeVerticalGapClosing(observation);
        var verticalGapMagnitude = MathF.Abs(observation.Objective.RelativeY);
        var verticalPlatformTransition = verticalGapMagnitude >= 36f;

        var progressReward = observation.ObjectiveDistanceDelta * progressScale;
        var navigationProgressScale = taskPhase == MLBotTaskPhase.ReturnIntel ? 0.018f : 0.008f;
        progressReward += navigationDistanceDelta * navigationProgressScale;
        if (verticalPlatformTransition && verticalGapClosing > 0f)
        {
            var verticalScale = taskPhase switch
            {
                MLBotTaskPhase.ReturnIntel => 0.0075f,
                MLBotTaskPhase.AttackIntel => 0.0065f,
                MLBotTaskPhase.CaptureObjective => 0.0055f,
                _ => 0.01f,
            };
            progressReward += MathF.Min(0.08f, verticalGapClosing * verticalScale);
        }

        var terrain = observation.TerrainAffordance;
        if (terrain.HasBestUpwardLanding && observation.Objective.RelativeY < -32f)
        {
            var bestDirection = MathF.Sign(terrain.BestUpwardLandingDirection);
            var movingTowardLanding = bestDirection != 0f && action.MoveDirection == (int)bestDirection;
            if (movingTowardLanding)
            {
                progressReward += terrain.BestUpwardLandingMovesAwayFromObjective ? 0.035f : 0.02f;
            }

            if (movingTowardLanding && action.Jump && (observation.IsGrounded || observation.RemainingAirJumps > 0))
            {
                progressReward += terrain.BestUpwardLandingMovesAwayFromObjective ? 0.04f : 0.025f;
            }

            if (terrain.BestUpwardLandingObjectiveDistanceDelta < -8f
                && terrain.BestUpwardLandingMovesAwayFromObjective
                && movingTowardLanding)
            {
                progressReward += 0.025f;
            }
        }

        if (verticalPlatformTransition
            && action.Jump
            && observation.IsGrounded
            && IsMovingTowardVerticalObjective(observation)
            && (observation.StuckTicks >= 2f
                || MathF.Abs(observation.VelocityX) < 24f
                || observation.Probes.ForwardFootObstacleDistance <= 16f))
        {
            progressReward += taskPhase == MLBotTaskPhase.ReturnIntel ? 0.025f : 0.018f;
        }

        var navigationDistance = GetNavigationDistance(observation);
        var bestNavigationImprovement = MathF.Max(0f, bestNavigationDistance - navigationDistance);
        if (bestNavigationImprovement > 0f)
        {
            progressReward += bestNavigationImprovement * (taskPhase == MLBotTaskPhase.ReturnIntel ? 0.025f : 0.01f);

            if (observation.Waypoint is { HasWaypoint: true, IsFinalWaypoint: false })
            {
                if (navigationDistance <= 96f)
                {
                    progressReward += taskPhase == MLBotTaskPhase.ReturnIntel ? 0.04f : 0.02f;
                }

                if (navigationDistance <= 48f)
                {
                    progressReward += taskPhase == MLBotTaskPhase.ReturnIntel ? 0.08f : 0.04f;
                }
            }
        }

        var bestDistanceImprovement = MathF.Max(0f, bestObjectiveDistance - observation.ObjectiveDistance);
        if (bestDistanceImprovement > 0f)
        {
            var breakthroughScale = taskPhase == MLBotTaskPhase.ReturnIntel ? 0.02f : 0.01f;
            progressReward += bestDistanceImprovement * breakthroughScale;

            if (observation.ObjectiveDistance <= 256f)
            {
                progressReward += taskPhase == MLBotTaskPhase.ReturnIntel ? 0.05f : 0.025f;
            }

            if (observation.ObjectiveDistance <= 128f)
            {
                progressReward += taskPhase == MLBotTaskPhase.ReturnIntel ? 0.075f : 0.05f;
            }

            if (observation.ObjectiveDistance <= 64f)
            {
                progressReward += taskPhase == MLBotTaskPhase.ReturnIntel ? 0.15f : 0.1f;
            }
        }

        if (taskPhase == MLBotTaskPhase.CaptureObjective && observation.ControlPointObjective.HasObjective)
        {
            var controlPoint = observation.ControlPointObjective;
            var encodedTeam = observation.Team == PlayerTeam.Red ? 1 : 2;
            if (controlPoint.IsPlayerInCaptureZone && (!controlPoint.IsLocked || controlPoint.IsKothMode))
            {
                progressReward += 0.015f;
            }

            if (controlPoint.CappingTeam == encodedTeam && controlPoint.FriendlyCappers > 0 && controlPoint.CaptureProgressDelta > 0f)
            {
                progressReward += controlPoint.CaptureProgressDelta * 4f;
            }

            if (controlPoint.IsContested || controlPoint.EnemyCappers > 0)
            {
                progressReward -= 0.02f;
            }
        }

        var objectiveReward = success ? 10f : 0f;
        var deathPenalty = died ? -2f : 0f;
        var timeoutPenalty = timedOut ? -12f : stalled ? -8f : 0f;
        var stuckPenalty = BuildStallPenalty(taskPhase, observation, action, navigationDistanceDelta, noProgressTicks, localLoopTicks);
        return new MLBotRewardBreakdown(progressReward, objectiveReward, deathPenalty, timeoutPenalty, stuckPenalty);
    }

    private string EvaluateStallTerminalState(MLBotTaskPhase taskPhase, in MLBotObservation observation)
    {
        if (IsProductiveObjectiveHold(taskPhase, observation))
        {
            return string.Empty;
        }

        if (observation.StuckTicks >= HardStuckTerminalTicks && _noProgressTicks >= 45)
        {
            return "stalled_stuck";
        }

        if (_localLoopTicks >= LocalLoopTerminalTicks)
        {
            return "stalled_local_loop";
        }

        if (_noProgressTicks >= NoProgressTerminalTicks)
        {
            return "stalled_no_progress";
        }

        return string.Empty;
    }

    private void UpdateProgressCounters(MLBotTaskPhase taskPhase, in MLBotObservation observation)
    {
        if (IsProductiveObjectiveHold(taskPhase, observation))
        {
            _noProgressTicks = 0;
            _localLoopTicks = 0;
            _lastProgressX = observation.BotX;
            _lastProgressY = observation.BotY;
            return;
        }

        var navigationDistance = GetNavigationDistance(observation);
        var madeMeaningfulProgress = observation.ObjectiveDistance < _bestObjectiveDistance - 8f
            || navigationDistance < _bestNavigationDistance - 8f
            || (MathF.Abs(observation.Objective.RelativeY) >= 36f && ComputeVerticalGapClosing(observation) >= 3f);
        if (madeMeaningfulProgress)
        {
            _noProgressTicks = 0;
            _localLoopTicks = 0;
            _lastProgressX = observation.BotX;
            _lastProgressY = observation.BotY;
            return;
        }

        _noProgressTicks += 1;
        var dx = observation.BotX - _lastProgressX;
        var dy = observation.BotY - _lastProgressY;
        var stayedInSameLocalPocket = (dx * dx) + (dy * dy) <= 96f * 96f;
        if (stayedInSameLocalPocket && _noProgressTicks >= 45)
        {
            _localLoopTicks += 1;
        }
        else if (!stayedInSameLocalPocket)
        {
            _localLoopTicks = 0;
            _lastProgressX = observation.BotX;
            _lastProgressY = observation.BotY;
        }
    }

    private static float BuildStallPenalty(
        MLBotTaskPhase taskPhase,
        in MLBotObservation observation,
        in MLBotAction action,
        float navigationDistanceDelta,
        int noProgressTicks,
        int localLoopTicks)
    {
        if (IsProductiveObjectiveHold(taskPhase, observation))
        {
            return 0f;
        }

        var movementMagnitude = MathF.Abs(observation.PreviousPositionDeltaX) + MathF.Abs(observation.PreviousPositionDeltaY);
        var speedMagnitude = MathF.Abs(observation.VelocityX) + MathF.Abs(observation.VelocityY);
        var isBarelyMoving = movementMagnitude < 0.15f && speedMagnitude < 0.75f;
        var objectiveProgress = observation.ObjectiveDistanceDelta;
        var verticalGapClosing = ComputeVerticalGapClosing(observation);
        var verticalPlatformTransition = MathF.Abs(observation.Objective.RelativeY) >= 36f;
        var verticalProgress = verticalPlatformTransition && verticalGapClosing >= 0.75f;
        var noObjectiveProgress = !verticalProgress && objectiveProgress <= 0.25f && navigationDistanceDelta <= 0.25f;
        var traversalMultiplier = taskPhase switch
        {
            MLBotTaskPhase.ReturnIntel => 1.45f,
            MLBotTaskPhase.AttackIntel => 1.35f,
            MLBotTaskPhase.CaptureObjective => 1.2f,
            _ => 1f,
        };
        var penalty = 0f;

        if (noObjectiveProgress)
        {
            penalty -= (isBarelyMoving ? 0.28f : 0.095f) * traversalMultiplier;
        }

        if (observation.StuckTicks >= 6f)
        {
            penalty -= MathF.Min(0.36f, 0.05f + (observation.StuckTicks - 6f) * 0.006f) * traversalMultiplier;
        }

        if (noProgressTicks >= 20)
        {
            penalty -= MathF.Min(0.42f, 0.06f + (noProgressTicks - 20) * 0.004f) * traversalMultiplier;
        }

        if (localLoopTicks > 0)
        {
            penalty -= MathF.Min(0.55f, 0.12f + localLoopTicks * 0.006f) * traversalMultiplier;
        }

        var touchingWall = observation.Probes.TouchingLeftWall || observation.Probes.TouchingRightWall;
        var pushingIntoNearbyObstacle = action.MoveDirection != 0
            && (observation.Probes.ForwardFootObstacleDistance <= 12f || touchingWall);
        if (noObjectiveProgress && pushingIntoNearbyObstacle)
        {
            penalty -= (action.Jump ? 0.24f : 0.18f) * traversalMultiplier;
        }

        var repeatedJumpingWithoutProgress = noObjectiveProgress
            && action.Jump
            && observation.FramesSinceJumpPressed <= 8f
            && noProgressTicks >= 20;
        if (repeatedJumpingWithoutProgress)
        {
            penalty -= 0.22f * traversalMultiplier;
        }

        var movingAwayDistance = MathF.Max(0f, -MathF.Min(objectiveProgress, navigationDistanceDelta));
        if (movingAwayDistance > 0.5f && !verticalProgress)
        {
            penalty -= MathF.Min(0.32f, movingAwayDistance * 0.012f) * traversalMultiplier;
        }

        return penalty;
    }

    private static float ComputeVerticalGapClosing(in MLBotObservation observation)
    {
        var relativeY = observation.Objective.RelativeY;
        if (relativeY < -12f)
        {
            return MathF.Max(0f, -observation.PreviousPositionDeltaY);
        }

        if (relativeY > 12f)
        {
            return MathF.Max(0f, observation.PreviousPositionDeltaY);
        }

        return 0f;
    }

    private static bool IsMovingTowardVerticalObjective(in MLBotObservation observation)
    {
        var relativeY = observation.Objective.RelativeY;
        return (relativeY < -12f && observation.VelocityY <= 0f)
            || (relativeY > 12f && observation.VelocityY >= 0f);
    }

    private static bool IsProductiveObjectiveHold(MLBotTaskPhase taskPhase, in MLBotObservation observation)
    {
        if (taskPhase != MLBotTaskPhase.CaptureObjective || !observation.ControlPointObjective.HasObjective)
        {
            return false;
        }

        var controlPoint = observation.ControlPointObjective;
        var encodedTeam = observation.Team == PlayerTeam.Red ? 1 : 2;
        return controlPoint.IsPlayerInCaptureZone
            && controlPoint.FriendlyCappers > 0
            && controlPoint.EnemyCappers <= 0
            && (controlPoint.CappingTeam == encodedTeam || controlPoint.Owner == encodedTeam);
    }

    private static float GetNavigationDistance(in MLBotObservation observation)
    {
        return observation.Waypoint is { HasWaypoint: true, IsFinalWaypoint: false }
            ? observation.Waypoint.Distance
            : observation.ObjectiveDistance;
    }

    private (bool Success, string Reason) EvaluateTerminalState(MLBotEpisodeConfig config, SimulationWorld world)
    {
        if (!world.LocalPlayer.IsAlive || world.LocalPlayer.Deaths > _startingLocalDeaths)
        {
            return (false, "death");
        }

        return config.TaskPhase switch
        {
            MLBotTaskPhase.None when HasCompletedPrimaryObjective(config.Team, world.MatchRules.Mode) => (true, "completed_primary_objective"),
            MLBotTaskPhase.AttackIntel when world.LocalPlayer.IsCarryingIntel => (true, "picked_up_intel"),
            MLBotTaskPhase.ReturnIntel when HasTeamScored(config.Team) => (true, "scored"),
            MLBotTaskPhase.CaptureObjective when HasTeamCapturedObjective(config.Team) => (true, "captured"),
            _ => (false, string.Empty),
        };

        bool HasCompletedPrimaryObjective(PlayerTeam team, GameModeKind mode)
        {
            return mode switch
            {
                GameModeKind.CaptureTheFlag => HasTeamScored(team),
                GameModeKind.KingOfTheHill => HasTeamCapturedObjective(team),
                GameModeKind.DoubleKingOfTheHill => HasTeamCapturedObjective(team),
                GameModeKind.ControlPoint => HasTeamCapturedObjective(team),
                _ => false,
            };
        }

        bool HasTeamScored(PlayerTeam team)
        {
            return team == PlayerTeam.Red
                ? world.RedCaps > _startingRedCaps
                : world.BlueCaps > _startingBlueCaps;
        }

        bool HasTeamCapturedObjective(PlayerTeam team)
        {
            for (var index = 0; index < world.ControlPoints.Count; index += 1)
            {
                var point = world.ControlPoints[index];
                _startingControlPointOwnersByIndex.TryGetValue(point.Index, out var startingOwner);
                if (point.Team == team && !point.IsLocked && startingOwner != team)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void GiveLocalPlayerEnemyIntel(PlayerTeam team)
    {
        var enemyIntel = team == PlayerTeam.Red ? _world.BlueIntel : _world.RedIntel;
        enemyIntel.PickUp();
        _world.LocalPlayer.PickUpIntel();
    }

    private void ApplyEpisodeStartState(MLBotEpisodeConfig config)
    {
        var hasExplicitStart = config.StartX.HasValue && config.StartY.HasValue;
        var shouldCarryIntel = config.CarryingIntel ?? config.TaskPhase == MLBotTaskPhase.ReturnIntel;
        if (config.StartNodeId >= 0 && !hasExplicitStart)
        {
            throw new NotSupportedException(
                "StartNodeId is not applied by the world-truth MLBot environment. "
                + "Use explicit StartX/StartY fixtures for comparable headless evaluation.");
        }

        if (!hasExplicitStart && !shouldCarryIntel)
        {
            return;
        }

        var classDefinition = CharacterClassCatalog.GetDefinition(config.ClassId);
        var startX = config.StartX;
        var startY = config.StartY;
        if (!hasExplicitStart)
        {
            var enemyIntel = config.Team == PlayerTeam.Red ? _world.BlueIntel : _world.RedIntel;
            var returnDirection = config.Team == PlayerTeam.Blue ? 1 : -1;
            startX = enemyIntel.HomeX + (returnDirection * 96f);
            startY = enemyIntel.HomeY + 12f;
        }

        _world.LocalPlayer.SetClassDefinition(classDefinition);
        _world.LocalPlayer.Spawn(config.Team, startX!.Value, startY!.Value);
        if (config.StartVelocityX.HasValue || config.StartVelocityY.HasValue)
        {
            _world.LocalPlayer.AddImpulse(config.StartVelocityX ?? 0f, config.StartVelocityY ?? 0f);
        }

        _world.LocalPlayer.RestoreMovementProbeState(
            config.StartIsGrounded,
            config.StartRemainingAirJumps,
            config.StartFacingDirectionX);

        if (config.StartPreviousMoveInput.HasValue
            || config.StartPreviousJumpHeld.HasValue
            || config.StartPreviousDropInput.HasValue
            || config.StartPreviousFirePrimary.HasValue
            || config.StartPreviousFireSecondary.HasValue)
        {
            _world.SetLocalPreviousInput(BuildPreviousInput(config));
        }

        if (shouldCarryIntel)
        {
            GiveLocalPlayerEnemyIntel(config.Team);
        }

        _world.SetLocalHealth(_world.LocalPlayer.MaxHealth);
    }

    private static PlayerInputSnapshot BuildPreviousInput(MLBotEpisodeConfig config)
    {
        var move = config.StartPreviousMoveInput ?? 0;
        return new PlayerInputSnapshot(
            Left: move < 0,
            Right: move > 0,
            Up: config.StartPreviousJumpHeld ?? false,
            Down: config.StartPreviousDropInput ?? false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: config.StartPreviousFirePrimary ?? false,
            FireSecondary: config.StartPreviousFireSecondary ?? false,
            AimWorldX: config.StartX ?? 0f,
            AimWorldY: config.StartY ?? 0f,
            DebugKill: false,
            DropIntel: false,
            FireSecondaryWeapon: false,
            InteractWeapon: false);
    }
}
