using OpenGarrison.Core;
using System.Linq;

namespace OpenGarrison.BotAI;

public sealed class BotNavigationDebugTraversalStep
{
    public string FromLabel { get; init; } = string.Empty;

    public string ToLabel { get; init; } = string.Empty;

    public float FromX { get; init; }

    public float FromY { get; init; }

    public float ToX { get; init; }

    public float ToY { get; init; }

    public BotNavigationTraversalKind Kind { get; init; }

    public IReadOnlyList<BotNavigationInputFrame> InputTape { get; init; } = Array.Empty<BotNavigationInputFrame>();

    public int ForcedHorizontalDirection { get; init; }
}

public sealed class BotNavigationDebugRoutePlan
{
    public PlayerClass ClassId { get; init; }

    public BotNavigationProfile Profile { get; init; }

    public string GoalLabel { get; init; } = string.Empty;

    public IReadOnlyList<BotNavigationDebugTraversalStep> Steps { get; init; } = Array.Empty<BotNavigationDebugTraversalStep>();
}

public static class BotNavigationDebugPlanner
{
    private const float StartNodeSearchDistance = 160f;

    public static bool TryBuildHintLinkTraversal(
        SimpleLevel level,
        PlayerClass classId,
        BotNavigationHintLink hintLink,
        float fromX,
        float fromY,
        float toX,
        float toY,
        bool requireGroundedArrival,
        double fixedDeltaSeconds,
        out BotNavigationDebugTraversalStep step,
        out string failureMessage)
    {
        step = default!;
        failureMessage = string.Empty;

        var profile = BotNavigationProfiles.GetProfileForClass(classId);
        var classDefinition = BotNavigationClasses.GetDefinition(classId);
        var recordedTraversal = FindRecordedTraversalForClass(hintLink.RecordedTraversals, classId, profile);
        if (recordedTraversal is not null)
        {
            var recordedKind = ResolveRecordedTraversalKind(fromY, toY, recordedTraversal.InputTape);

            step = CreateStep(
                hintLink.FromLabel,
                hintLink.ToLabel,
                fromX,
                fromY,
                toX,
                toY,
                hintLink.Traversal == BotNavigationHintTraversalKind.Drop ? BotNavigationTraversalKind.Drop : recordedKind,
                recordedTraversal.InputTape,
                GetHorizontalDirection(toX - fromX, fallbackDirection: 1));
            return true;
        }

        if (hintLink.Traversal is BotNavigationHintTraversalKind.Auto or BotNavigationHintTraversalKind.Walk)
        {
            if (BotNavigationMovementValidator.CanWalkDirectly(level, classDefinition, fromX, fromY, toX, toY))
            {
                step = CreateStep(
                    hintLink.FromLabel,
                    hintLink.ToLabel,
                    fromX,
                    fromY,
                    toX,
                    toY,
                    BotNavigationTraversalKind.Walk,
                    Array.Empty<BotNavigationInputFrame>(),
                    GetHorizontalDirection(toX - fromX));
                return true;
            }

            if (TryBuildGroundTapeForEitherTeam(level, classDefinition, fromX, fromY, toX, toY, out var walkTape, out _, out var walkFailureMessage))
            {
                step = CreateStep(
                    hintLink.FromLabel,
                    hintLink.ToLabel,
                    fromX,
                    fromY,
                    toX,
                    toY,
                    BotNavigationTraversalKind.Walk,
                    walkTape,
                    GetHorizontalDirection(toX - fromX));
                return true;
            }

            if (hintLink.Traversal == BotNavigationHintTraversalKind.Walk)
            {
                step = CreateStep(
                    hintLink.FromLabel,
                    hintLink.ToLabel,
                    fromX,
                    fromY,
                    toX,
                    toY,
                    BotNavigationTraversalKind.Walk,
                    Array.Empty<BotNavigationInputFrame>(),
                    GetHorizontalDirection(toX - fromX));
                return true;
            }
        }

        if (hintLink.Traversal is BotNavigationHintTraversalKind.Auto or BotNavigationHintTraversalKind.Jump)
        {
            if (TryBuildHintJumpTapeForEitherTeam(level, classDefinition, profile, fromX, fromY, toX, toY, requireGroundedArrival, out var jumpTape, out _))
            {
                step = CreateStep(
                    hintLink.FromLabel,
                    hintLink.ToLabel,
                    fromX,
                    fromY,
                    toX,
                    toY,
                    BotNavigationTraversalKind.Jump,
                    jumpTape,
                    GetHorizontalDirection(toX - fromX));
                return true;
            }

            if (hintLink.Traversal == BotNavigationHintTraversalKind.Jump)
            {
                var approximateJumpTape = BotNavigationMovementValidator.BuildApproximateHintJumpTape(
                    classDefinition,
                    profile,
                    fromX,
                    fromY,
                    toX,
                    toY);
                step = CreateStep(
                    hintLink.FromLabel,
                    hintLink.ToLabel,
                    fromX,
                    fromY,
                    toX,
                    toY,
                    BotNavigationTraversalKind.Jump,
                    approximateJumpTape,
                    GetHorizontalDirection(toX - fromX));
                return true;
            }
        }

        if (hintLink.Traversal is BotNavigationHintTraversalKind.Auto or BotNavigationHintTraversalKind.Drop)
        {
            if (toY <= fromY + 4f)
            {
                failureMessage = "drop preview target must be below the source anchor";
                return false;
            }

            step = CreateStep(
                hintLink.FromLabel,
                hintLink.ToLabel,
                fromX,
                fromY,
                toX,
                toY,
                BotNavigationTraversalKind.Drop,
                Array.Empty<BotNavigationInputFrame>(),
                GetHorizontalDirection(toX - fromX, fallbackDirection: 1));
            return true;
        }

        failureMessage = "no preview traversal could be built";
        return false;
    }

    public static bool TryPlanRoute(
        SimpleLevel level,
        BotNavigationAsset asset,
        PlayerClass classId,
        float startX,
        float startY,
        string goalLabel,
        out BotNavigationDebugRoutePlan plan,
        out string failureMessage)
    {
        plan = default!;
        failureMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(goalLabel))
        {
            failureMessage = "route target label is missing";
            return false;
        }

        var graph = new BotNavigationRuntimeGraph(asset);
        if (!graph.TryFindNearestNode(startX, startY, StartNodeSearchDistance, requireGroundSupport: true, out var startNode))
        {
            failureMessage = $"no {BotNavigationClasses.GetShortLabel(classId)} nav node is close enough to the current position";
            return false;
        }

        var goalNode = asset.Nodes.FirstOrDefault(node => string.Equals(node.Label, goalLabel, StringComparison.OrdinalIgnoreCase));
        if (goalNode is null)
        {
            failureMessage = $"could not find a {BotNavigationClasses.GetShortLabel(classId)} route-test node labeled {goalLabel}; make sure that anchor/link applies to the selected class";
            return false;
        }

        return TryBuildRoutePlan(level, asset, classId, graph, startNode, goalNode, out plan, out failureMessage);
    }

    public static bool TryPlanRouteBetweenLabels(
        SimpleLevel level,
        BotNavigationAsset asset,
        PlayerClass classId,
        string startLabel,
        string goalLabel,
        out BotNavigationDebugRoutePlan plan,
        out string failureMessage)
    {
        plan = default!;
        failureMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(startLabel) || string.IsNullOrWhiteSpace(goalLabel))
        {
            failureMessage = "route test needs both a start label and a goal label";
            return false;
        }

        var graph = new BotNavigationRuntimeGraph(asset);
        var startNode = asset.Nodes.FirstOrDefault(node => string.Equals(node.Label, startLabel, StringComparison.OrdinalIgnoreCase));
        if (startNode is null)
        {
            failureMessage = $"could not find a {BotNavigationClasses.GetShortLabel(classId)} route-test start node labeled {startLabel}; make sure that anchor/link applies to the selected class";
            return false;
        }

        var goalNode = asset.Nodes.FirstOrDefault(node => string.Equals(node.Label, goalLabel, StringComparison.OrdinalIgnoreCase));
        if (goalNode is null)
        {
            failureMessage = $"could not find a {BotNavigationClasses.GetShortLabel(classId)} route-test node labeled {goalLabel}; make sure that anchor/link applies to the selected class";
            return false;
        }

        return TryBuildRoutePlan(level, asset, classId, graph, startNode, goalNode, out plan, out failureMessage);
    }

    private static bool TryBuildRoutePlan(
        SimpleLevel level,
        BotNavigationAsset asset,
        PlayerClass classId,
        BotNavigationRuntimeGraph graph,
        BotNavigationNode startNode,
        BotNavigationNode goalNode,
        out BotNavigationDebugRoutePlan plan,
        out string failureMessage)
    {
        plan = default!;
        failureMessage = string.Empty;

        var route = graph.FindRoute(startNode.Id, goalNode.Id);
        if (route is null || route.Length < 2)
        {
            failureMessage = $"no route found from {startNode.Label} to {goalNode.Label}";
            return false;
        }

        var classDefinition = BotNavigationClasses.GetDefinition(classId);
        var steps = new List<BotNavigationDebugTraversalStep>(route.Length - 1);
        for (var index = 1; index < route.Length; index += 1)
        {
            if (!graph.TryGetNode(route[index - 1], out var fromNode)
                || !graph.TryGetNode(route[index], out var toNode)
                || !graph.TryGetEdge(fromNode.Id, toNode.Id, out var edge))
            {
                failureMessage = "route planning failed because a baked edge could not be resolved";
                return false;
            }

            var tape = edge.InputTape;
            if (tape.Count == 0 && edge.Kind == BotNavigationTraversalKind.Walk)
            {
                _ = TryBuildGroundTapeForEitherTeam(level, classDefinition, fromNode.X, fromNode.Y, toNode.X, toNode.Y, out tape, out _);
            }
            else if (tape.Count == 0 && edge.Kind == BotNavigationTraversalKind.Jump)
            {
                if (!TryBuildJumpTapeForEitherTeam(level, classDefinition, asset.Profile, fromNode.X, fromNode.Y, toNode.X, toNode.Y, out tape, out _))
                {
                    failureMessage = $"route step {fromNode.Label} -> {toNode.Label} could not rebuild its jump tape";
                    return false;
                }
            }

            steps.Add(CreateStep(
                string.IsNullOrWhiteSpace(fromNode.Label) ? $"node-{fromNode.Id}" : fromNode.Label,
                string.IsNullOrWhiteSpace(toNode.Label) ? $"node-{toNode.Id}" : toNode.Label,
                fromNode.X,
                fromNode.Y,
                toNode.X,
                toNode.Y,
                edge.Kind,
                tape,
                edge.Kind == BotNavigationTraversalKind.Drop
                    ? graph.GetDropDirection(fromNode.Id, toNode.Id)
                    : GetHorizontalDirection(toNode.X - fromNode.X)));
        }

        plan = new BotNavigationDebugRoutePlan
        {
            ClassId = classId,
            Profile = asset.Profile,
            GoalLabel = goalNode.Label,
            Steps = steps,
        };
        return true;
    }

    private static BotNavigationHintRecordedTraversal? FindRecordedTraversalForClass(
        IReadOnlyList<BotNavigationHintRecordedTraversal> recordedTraversals,
        PlayerClass classId,
        BotNavigationProfile profile)
    {
        return recordedTraversals.FirstOrDefault(entry => entry.ClassId == classId && entry.InputTape.Count > 0)
            ?? recordedTraversals.FirstOrDefault(entry => entry.ClassId.HasValue && BotNavigationProfiles.GetProfileForClass(entry.ClassId.Value) == profile && entry.InputTape.Count > 0)
            ?? recordedTraversals.FirstOrDefault(entry => entry.Profile == profile && entry.InputTape.Count > 0);
    }

    private static BotNavigationTraversalKind ResolveRecordedTraversalKind(
        float sourceY,
        float targetY,
        IReadOnlyList<BotNavigationInputFrame> tape)
    {
        if (tape.Any(frame => frame.Up))
        {
            return BotNavigationTraversalKind.Jump;
        }

        return targetY > sourceY + 8f
            ? BotNavigationTraversalKind.Drop
            : BotNavigationTraversalKind.Walk;
    }

    private static BotNavigationDebugTraversalStep CreateStep(
        string fromLabel,
        string toLabel,
        float fromX,
        float fromY,
        float toX,
        float toY,
        BotNavigationTraversalKind kind,
        IReadOnlyList<BotNavigationInputFrame> inputTape,
        int forcedHorizontalDirection)
    {
        return new BotNavigationDebugTraversalStep
        {
            FromLabel = fromLabel,
            ToLabel = toLabel,
            FromX = fromX,
            FromY = fromY,
            ToX = toX,
            ToY = toY,
            Kind = kind,
            InputTape = inputTape,
            ForcedHorizontalDirection = forcedHorizontalDirection,
        };
    }

    private static bool TryBuildGroundTapeForEitherTeam(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return TryBuildGroundTapeForEitherTeam(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            out tape,
            out cost,
            out _);
    }

    private static bool TryBuildGroundTapeForEitherTeam(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost,
        out string failureMessage)
    {
        if (BotNavigationMovementValidator.TryBuildGroundTape(
                level,
                classDefinition,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Red,
                out tape,
                out cost,
                out failureMessage))
        {
            return true;
        }

        var redFailure = failureMessage;
        if (BotNavigationMovementValidator.TryBuildGroundTape(
                level,
                classDefinition,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Blue,
                out tape,
                out cost,
                out failureMessage))
        {
            return true;
        }

        failureMessage = string.Equals(redFailure, failureMessage, StringComparison.Ordinal)
            ? redFailure
            : $"red: {redFailure}; blue: {failureMessage}";
        return false;
    }

    private static bool TryBuildJumpTapeForEitherTeam(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return BotNavigationMovementValidator.TryBuildJumpTape(level, classDefinition, profile, sourceX, sourceY, targetX, targetY, PlayerTeam.Red, out tape, out cost)
            || BotNavigationMovementValidator.TryBuildJumpTape(level, classDefinition, profile, sourceX, sourceY, targetX, targetY, PlayerTeam.Blue, out tape, out cost);
    }

    private static bool TryBuildHintJumpTapeForEitherTeam(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        bool requireGroundedArrival,
        out IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost)
    {
        return BotNavigationMovementValidator.TryBuildHintJumpTape(level, classDefinition, profile, sourceX, sourceY, targetX, targetY, PlayerTeam.Red, requireGroundedArrival, out tape, out cost)
            || BotNavigationMovementValidator.TryBuildHintJumpTape(level, classDefinition, profile, sourceX, sourceY, targetX, targetY, PlayerTeam.Blue, requireGroundedArrival, out tape, out cost);
    }

    private static bool TryValidateRecordedHintTapeForEitherTeam(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        bool requireGroundedArrival,
        double fixedDeltaSeconds,
        IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost,
        out BotNavigationTraversalKind traversalKind,
        out string failureMessage)
    {
        if (BotNavigationMovementValidator.TryValidateRecordedTraversalTape(
                level,
                classDefinition,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Red,
                tape,
                requireGroundedArrival,
                fixedDeltaSeconds,
                out cost,
                out traversalKind,
                out failureMessage))
        {
            return true;
        }

        return BotNavigationMovementValidator.TryValidateRecordedTraversalTape(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            PlayerTeam.Blue,
            tape,
            requireGroundedArrival,
            fixedDeltaSeconds,
            out cost,
            out traversalKind,
            out failureMessage);
    }

    private static int GetHorizontalDirection(float deltaX, int fallbackDirection = 0)
    {
        if (deltaX > 2f)
        {
            return 1;
        }

        if (deltaX < -2f)
        {
            return -1;
        }

        return fallbackDirection;
    }
}
