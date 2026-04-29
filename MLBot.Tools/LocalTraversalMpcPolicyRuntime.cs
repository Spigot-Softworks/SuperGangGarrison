using OpenGarrison.Core;
using OpenGarrison.MLBot;
using OpenGarrison.MLBot.Contracts;
using OpenGarrison.MLBot.Environment;

internal sealed class LocalTraversalMpcPolicyRuntime(IMLBotPolicyRuntime innerPolicy) : IMLBotPolicyRuntime
{
    private const int HorizonTicks = 48;
    private const int CommitTicks = 10;
    private const float EngageDistance = 280f;
    private readonly Queue<MLBotAction> _committedActions = new();

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        if (!ShouldEngage(observation))
        {
            _committedActions.Clear();
            return innerPolicy.Evaluate(observation);
        }

        if (observation.Probes.TouchingLeftWall || observation.Probes.TouchingRightWall)
        {
            _committedActions.Clear();
            var moveDirection = observation.Probes.TouchingLeftWall ? 1 : -1;
            var jump = observation.IsGrounded && observation.Objective.RelativeY < -24f;
            return BuildAction(observation, moveDirection, jump);
        }

        if (_committedActions.Count > 0)
        {
            return _committedActions.Dequeue();
        }

        var best = SearchBestCandidate(observation);
        foreach (var action in best.Actions.Take(CommitTicks).Skip(1))
        {
            _committedActions.Enqueue(action);
        }

        return best.Actions.Length > 0 ? best.Actions[0] : innerPolicy.Evaluate(observation);
    }

    private static bool ShouldEngage(in MLBotObservation observation)
    {
        if (!observation.Objective.HasObjective || observation.IsRespawning)
        {
            return false;
        }

        return observation.ObjectiveDistance <= EngageDistance
            || observation.StuckTicks >= 12f
            || (observation.ObjectiveDistance <= 320f
                && observation.TerrainAffordance.HasBestUpwardLanding
                && observation.Objective.RelativeY < -96f)
            || (observation.ObjectiveDistance <= 320f
                && MathF.Abs(observation.Objective.RelativeY) > 128f
                && observation.Probes.ForwardFootObstacleDistance <= 16f);
    }

    private static MpcCandidate SearchBestCandidate(in MLBotObservation observation)
    {
        var candidates = BuildCandidates(observation);
        var best = candidates[0];
        var bestScore = float.NegativeInfinity;
        foreach (var candidate in candidates)
        {
            var score = EvaluateCandidate(observation, candidate);
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static List<MpcCandidate> BuildCandidates(in MLBotObservation observation)
    {
        var objectiveDirection = DirectionFromDelta(observation.Objective.RelativeX, 6f);
        var landingDirection = observation.TerrainAffordance.HasBestUpwardLanding
            ? Math.Sign(observation.TerrainAffordance.BestUpwardLandingDirection)
            : 0;
        var directions = new[] { objectiveDirection, landingDirection, -objectiveDirection, -landingDirection, -1, 1, 0 }
            .Where(static direction => direction is >= -1 and <= 1)
            .Distinct()
            .ToArray();

        var candidates = new List<MpcCandidate>();
        foreach (var direction in directions)
        {
            AddHold(candidates, observation, $"hold_{direction}", direction, jumpStart: -1, jumpDuration: 0);
            AddHold(candidates, observation, $"jump_{direction}", direction, jumpStart: 0, jumpDuration: 8);
            AddSwitch(candidates, observation, $"switch8_{direction}", direction, -direction, switchTick: 8, jumpStart: 0, jumpDuration: 8);
            AddSwitch(candidates, observation, $"backoff8_{direction}", -direction, direction, switchTick: 8, jumpStart: 4, jumpDuration: 8);
        }

        return candidates;
    }

    private static void AddHold(
        List<MpcCandidate> candidates,
        in MLBotObservation observation,
        string name,
        int moveDirection,
        int jumpStart,
        int jumpDuration)
    {
        var actions = new MLBotAction[HorizonTicks];
        for (var tick = 0; tick < actions.Length; tick += 1)
        {
            actions[tick] = BuildAction(observation, moveDirection, IsJumpTick(tick, jumpStart, jumpDuration));
        }

        candidates.Add(new MpcCandidate(name, actions));
    }

    private static void AddSwitch(
        List<MpcCandidate> candidates,
        in MLBotObservation observation,
        string name,
        int firstDirection,
        int secondDirection,
        int switchTick,
        int jumpStart,
        int jumpDuration)
    {
        var actions = new MLBotAction[HorizonTicks];
        for (var tick = 0; tick < actions.Length; tick += 1)
        {
            var direction = tick < switchTick ? firstDirection : secondDirection;
            actions[tick] = BuildAction(observation, direction, IsJumpTick(tick, jumpStart, jumpDuration));
        }

        candidates.Add(new MpcCandidate(name, actions));
    }

    private static void AddPulse(
        List<MpcCandidate> candidates,
        in MLBotObservation observation,
        string name,
        int moveDirection,
        int pulseTicks,
        int jumpStart,
        int jumpDuration)
    {
        var actions = new MLBotAction[HorizonTicks];
        for (var tick = 0; tick < actions.Length; tick += 1)
        {
            var phase = tick / Math.Max(1, pulseTicks);
            var direction = phase % 2 == 0 ? moveDirection : -moveDirection;
            actions[tick] = BuildAction(observation, direction, IsJumpTick(tick, jumpStart, jumpDuration));
        }

        candidates.Add(new MpcCandidate(name, actions));
    }

    private static float EvaluateCandidate(in MLBotObservation start, MpcCandidate candidate)
    {
        var environment = new MLBotEnvironment();
        var initial = environment.Reset(BuildConfig(start, HorizonTicks));
        var minObjectiveDistance = initial.ObjectiveDistance;
        var maxVerticalGain = 0f;
        var wallTicks = 0;
        var noProgressTicks = 0;
        var previousX = initial.BotX;
        var previousY = initial.BotY;
        MLBotStepResult result = default;
        for (var tick = 0; tick < candidate.Actions.Length; tick += 1)
        {
            result = environment.Step(candidate.Actions[tick]);
            var observation = result.Observation;
            minObjectiveDistance = MathF.Min(minObjectiveDistance, observation.ObjectiveDistance);
            maxVerticalGain = MathF.Max(maxVerticalGain, initial.BotY - observation.BotY);
            if (observation.Probes.TouchingLeftWall || observation.Probes.TouchingRightWall)
            {
                wallTicks += 1;
            }

            if (MathF.Abs(observation.BotX - previousX) + MathF.Abs(observation.BotY - previousY) < 0.35f)
            {
                noProgressTicks += 1;
            }

            previousX = observation.BotX;
            previousY = observation.BotY;
            if (result.IsTerminal)
            {
                break;
            }
        }

        var end = result.Observation.Equals(default) ? initial : result.Observation;
        if (result.IsSuccess)
        {
            return 1_000_000f - result.Tick;
        }

        var objectiveDelta = initial.ObjectiveDistance - end.ObjectiveDistance;
        var minObjectiveDelta = initial.ObjectiveDistance - minObjectiveDistance;
        var score = 0f;
        score += objectiveDelta * 16f;
        score += minObjectiveDelta * 24f;
        score -= MathF.Max(0f, end.ObjectiveDistance - minObjectiveDistance) * 30f;
        score += maxVerticalGain * (initial.Objective.RelativeY < -32f ? 10f : 2f);
        score += MathF.Max(0f, 320f - end.ObjectiveDistance) * 12f;
        score += MathF.Max(0f, 180f - minObjectiveDistance) * 20f;
        score -= wallTicks * 24f;
        score -= noProgressTicks * 30f;
        if (result.IsTerminal && result.TerminalReason is "hard_stuck" or "local_loop" or "no_progress")
        {
            score -= 2500f;
        }

        var firstAction = candidate.Actions.Length > 0 ? candidate.Actions[0] : default;
        if (initial.Probes.TouchingLeftWall)
        {
            score += firstAction.MoveDirection > 0 ? 900f : 0f;
            score -= firstAction.MoveDirection < 0 ? 1600f : 0f;
        }

        if (initial.Probes.TouchingRightWall)
        {
            score += firstAction.MoveDirection < 0 ? 900f : 0f;
            score -= firstAction.MoveDirection > 0 ? 1600f : 0f;
        }

        var terrain = initial.TerrainAffordance;
        var objectiveDirection = DirectionFromDelta(initial.Objective.RelativeX, 8f);
        if (terrain.HasBestUpwardLanding && initial.Objective.RelativeY < -24f)
        {
            var landingDirection = Math.Sign(terrain.BestUpwardLandingDirection);
            if (firstAction.MoveDirection == landingDirection)
            {
                score += terrain.BestUpwardLandingMovesAwayFromObjective ? 900f : 350f;
            }

            if (firstAction.Jump && firstAction.MoveDirection == landingDirection)
            {
                score += 250f;
            }
        }
        else if (objectiveDirection != 0 && firstAction.MoveDirection == -objectiveDirection)
        {
            score -= 650f;
        }

        return score;
    }

    private static MLBotEpisodeConfig BuildConfig(in MLBotObservation observation, int maxTicks)
    {
        return MLBotEpisodeConfig.CreateDefault(
            levelName: observation.LevelName,
            taskPhase: observation.TaskPhase,
            team: observation.Team,
            classId: observation.ClassId,
            maxTicks: maxTicks,
            startX: observation.BotX,
            startY: observation.BotY,
            startVelocityX: observation.VelocityX,
            startVelocityY: observation.VelocityY,
            carryingIntel: observation.IsCarryingIntel,
            startIsGrounded: observation.IsGrounded,
            startRemainingAirJumps: observation.RemainingAirJumps,
            startFacingDirectionX: observation.FacingDirectionX,
            startPreviousMoveInput: observation.PreviousMoveInput,
            startPreviousJumpHeld: observation.PreviousJumpHeld,
            startPreviousDropInput: observation.PreviousDropInput,
            startPreviousFirePrimary: observation.PreviousActionFirePrimary,
            startPreviousFireSecondary: observation.PreviousActionFireSecondary,
            startPreviousPositionDeltaX: observation.PreviousPositionDeltaX,
            startPreviousPositionDeltaY: observation.PreviousPositionDeltaY,
            startPreviousVelocityX: observation.PreviousVelocityX,
            startPreviousVelocityY: observation.PreviousVelocityY,
            startPreviousFacingDirectionX: observation.PreviousFacingDirectionX,
            startPreviousIsGrounded: observation.PreviousIsGrounded,
            startObjectiveDistance: observation.ObjectiveDistance,
            startObjectiveDistanceDelta: observation.ObjectiveDistanceDelta,
            startPreviousObjectiveDistanceDelta: observation.PreviousObjectiveDistanceDelta,
            startAirborneTicks: observation.AirborneTicks,
            startJumpTicks: observation.JumpTicks,
            startFramesSinceJumpPressed: observation.FramesSinceJumpPressed,
            startFramesSinceJumpReleased: observation.FramesSinceJumpReleased);
    }

    private static MLBotAction BuildAction(in MLBotObservation observation, int moveDirection, bool jump)
    {
        var aimX = observation.Objective.HasObjective ? observation.Objective.WorldX : observation.BotX;
        var aimY = observation.Objective.HasObjective ? observation.Objective.WorldY : observation.BotY;
        return new MLBotAction(moveDirection, jump, false, false, false, false, aimX, aimY);
    }

    private static bool IsJumpTick(int tick, int jumpStart, int jumpDuration)
    {
        return jumpStart >= 0 && tick >= jumpStart && tick < jumpStart + jumpDuration;
    }

    private static int DirectionFromDelta(float delta, float deadZone)
    {
        if (delta < -deadZone)
        {
            return -1;
        }

        return delta > deadZone ? 1 : 0;
    }

    private sealed record MpcCandidate(string Name, MLBotAction[] Actions);
}
