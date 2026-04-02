using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public sealed record BotNavigationValidationIssue(
    string Code,
    string Message);

public sealed record BotNavigationValidationResult(
    IReadOnlyList<BotNavigationValidationIssue> Issues)
{
    public static BotNavigationValidationResult Valid { get; } = new(Array.Empty<BotNavigationValidationIssue>());

    public bool IsStructurallyValid => Issues.Count == 0;

    public string BuildSummary()
    {
        if (Issues.Count == 0)
        {
            return "graph ok";
        }

        return string.Join("; ", Issues.Select(static issue => issue.Message));
    }
}

public static class BotNavigationAssetValidator
{
    // Keep in sync with the runtime bot controller's node lookup envelope.
    private const float StartNodeSearchDistance = 144f;
    private const float GoalNodeSearchDistance = 220f;

    public static BotNavigationValidationResult Validate(SimpleLevel level, BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.Nodes.Count == 0)
        {
            return new BotNavigationValidationResult(
            [
                new BotNavigationValidationIssue("empty-graph", "graph contains no navigation nodes"),
            ]);
        }

        var issues = new List<BotNavigationValidationIssue>();
        ValidateTraversalExecutability(asset, issues);
        var graph = new BotNavigationRuntimeGraph(asset);
        if (asset.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            ValidateModernMarkerCoverage(level, asset, graph, PlayerTeam.Red, issues);
            ValidateModernMarkerCoverage(level, asset, graph, PlayerTeam.Blue, issues);
        }
        else
        {
            ValidateAttackReachability(level, asset, graph, PlayerTeam.Red, issues);
            ValidateAttackReachability(level, asset, graph, PlayerTeam.Blue, issues);
        }
        return issues.Count == 0
            ? BotNavigationValidationResult.Valid
            : new BotNavigationValidationResult(issues);
    }

    private static void ValidateTraversalExecutability(
        BotNavigationAsset asset,
        List<BotNavigationValidationIssue> issues)
    {
        if (asset.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            return;
        }

        var jumpEdgesMissingTape = asset.Edges.Count(edge =>
            edge.Kind == BotNavigationTraversalKind.Jump
            && (edge.InputTape is null || edge.InputTape.Count == 0));
        if (jumpEdgesMissingTape <= 0)
        {
            return;
        }

        issues.Add(new BotNavigationValidationIssue(
            "jump-edge-missing-tape",
            $"graph contains {jumpEdgesMissingTape} jump edge(s) without traversal input tapes"));
    }

    private static void ValidateAttackReachability(
        SimpleLevel level,
        BotNavigationAsset asset,
        BotNavigationRuntimeGraph graph,
        PlayerTeam team,
        List<BotNavigationValidationIssue> issues)
    {
        var spawns = GetTeamSpawns(level, asset, team);
        if (spawns.Length == 0)
        {
            issues.Add(new BotNavigationValidationIssue(
                $"spawn-missing-{team}",
                $"{team} has no usable spawns for nav validation"));
            return;
        }

        var objectives = GetAttackObjectives(level, asset, team);
        if (objectives.Count == 0)
        {
            issues.Add(new BotNavigationValidationIssue(
                $"objective-missing-{team}",
                $"{team} has no attack objective markers for nav validation"));
            return;
        }

        for (var spawnIndex = 0; spawnIndex < spawns.Length; spawnIndex += 1)
        {
            var spawn = spawns[spawnIndex];
            if (!graph.TryFindNearestNode(spawn.X, spawn.Y, StartNodeSearchDistance, requireGroundSupport: true, out var startNode))
            {
                issues.Add(new BotNavigationValidationIssue(
                    $"spawn-node-miss-{team}-{spawnIndex}",
                    $"{team} spawn {spawnIndex + 1} at ({spawn.X:F0},{spawn.Y:F0}) has no nearby nav node"));
                continue;
            }

            var reachableObjective = false;
            for (var objectiveIndex = 0; objectiveIndex < objectives.Count; objectiveIndex += 1)
            {
                var objective = objectives[objectiveIndex];
                if (graph.TryFindRouteToGoalRadius(
                        startNode.Id,
                        objective.X,
                        objective.Y,
                        GoalNodeSearchDistance,
                        out _,
                        out _))
                {
                    reachableObjective = true;
                    break;
                }
            }

            if (!reachableObjective)
            {
                var objectiveSummary = string.Join(", ", objectives.Select(static objective => objective.Label));
                issues.Add(new BotNavigationValidationIssue(
                    $"attack-disconnected-{team}-{spawnIndex}",
                    $"{team} spawn {spawnIndex + 1} at ({spawn.X:F0},{spawn.Y:F0}) cannot route to any attack objective ({objectiveSummary})"));
            }
        }
    }

    private static void ValidateModernMarkerCoverage(
        SimpleLevel level,
        BotNavigationAsset asset,
        BotNavigationRuntimeGraph graph,
        PlayerTeam team,
        List<BotNavigationValidationIssue> issues)
    {
        var spawns = GetTeamSpawns(level, asset, team);
        if (spawns.Length == 0)
        {
            issues.Add(new BotNavigationValidationIssue(
                $"spawn-missing-{team}",
                $"{team} has no usable spawns for nav validation"));
            return;
        }

        var objectives = GetAttackObjectives(level, asset, team);
        if (objectives.Count == 0)
        {
            issues.Add(new BotNavigationValidationIssue(
                $"objective-missing-{team}",
                $"{team} has no attack objective markers for nav validation"));
            return;
        }

        for (var spawnIndex = 0; spawnIndex < spawns.Length; spawnIndex += 1)
        {
            var spawn = spawns[spawnIndex];
            if (!graph.TryFindNearestNode(spawn.X, spawn.Y, StartNodeSearchDistance, requireGroundSupport: true, out _))
            {
                issues.Add(new BotNavigationValidationIssue(
                    $"spawn-node-miss-{team}-{spawnIndex}",
                    $"{team} spawn {spawnIndex + 1} at ({spawn.X:F0},{spawn.Y:F0}) has no nearby nav node"));
            }
        }

        for (var objectiveIndex = 0; objectiveIndex < objectives.Count; objectiveIndex += 1)
        {
            var objective = objectives[objectiveIndex];
            if (!graph.TryFindNearestNode(objective.X, objective.Y, GoalNodeSearchDistance, requireGroundSupport: false, out _))
            {
                issues.Add(new BotNavigationValidationIssue(
                    $"objective-node-miss-{team}-{objectiveIndex}",
                    $"Objective '{objective.Label}' at ({objective.X:F0},{objective.Y:F0}) has no nearby nav node"));
            }
        }
    }

    private static NavMarkerPoint[] GetTeamSpawns(SimpleLevel level, BotNavigationAsset asset, PlayerTeam team)
    {
        var authoredSpawns = asset.Nodes
            .Where(node => node.Kind == BotNavigationNodeKind.Spawn && GetNodeTeam(node) == team)
            .Select(node => new NavMarkerPoint(node.X, node.Y, string.IsNullOrWhiteSpace(node.Label) ? $"{team} spawn" : node.Label))
            .ToArray();
        if (authoredSpawns.Length > 0)
        {
            return authoredSpawns;
        }

        var spawns = team == PlayerTeam.Red ? level.RedSpawns : level.BlueSpawns;
        if (spawns.Count > 0)
        {
            return spawns
                .Select(spawn => new NavMarkerPoint(spawn.X, spawn.Y, $"{team} spawn"))
                .ToArray();
        }

        return
        [
            new NavMarkerPoint(level.LocalSpawn.X, level.LocalSpawn.Y, "local spawn"),
        ];
    }

    private static IReadOnlyList<ObjectivePoint> GetAttackObjectives(SimpleLevel level, BotNavigationAsset asset, PlayerTeam team)
    {
        var enemyTeam = GetOpposingTeam(team);
        var authoredObjectives = asset.Nodes
            .Where(node => node.Kind == BotNavigationNodeKind.Objective && GetNodeTeam(node) == enemyTeam)
            .Select(node => new ObjectivePoint(node.X, node.Y, string.IsNullOrWhiteSpace(node.Label) ? $"{enemyTeam} objective" : node.Label))
            .ToArray();
        if (authoredObjectives.Length > 0)
        {
            return authoredObjectives;
        }

        var authoredNeutralObjectives = asset.Nodes
            .Where(node => node.Kind == BotNavigationNodeKind.Objective && !GetNodeTeam(node).HasValue)
            .Select(node => new ObjectivePoint(node.X, node.Y, string.IsNullOrWhiteSpace(node.Label) ? "objective" : node.Label))
            .ToArray();
        if (authoredNeutralObjectives.Length > 0)
        {
            return authoredNeutralObjectives;
        }

        return level.Mode switch
        {
            GameModeKind.CaptureTheFlag => GetCaptureTheFlagObjectives(level, enemyTeam),
            GameModeKind.Generator => GetGeneratorObjectives(level, enemyTeam),
            GameModeKind.ControlPoint => GetControlPointObjectives(level),
            GameModeKind.Arena => GetArenaObjectives(level),
            _ => GetFallbackObjectives(level),
        };
    }

    private static ObjectivePoint[] GetCaptureTheFlagObjectives(SimpleLevel level, PlayerTeam enemyTeam)
    {
        var intelBase = level.GetIntelBase(enemyTeam);
        return intelBase.HasValue
            ?
            [
                new ObjectivePoint(intelBase.Value.X, intelBase.Value.Y, $"{enemyTeam} intel"),
            ]
            : Array.Empty<ObjectivePoint>();
    }

    private static ObjectivePoint[] GetGeneratorObjectives(SimpleLevel level, PlayerTeam enemyTeam)
    {
        var objectives = level.GetRoomObjects(RoomObjectType.Generator)
            .Where(generator => !generator.Team.HasValue || generator.Team.Value == enemyTeam)
            .Select(generator => new ObjectivePoint(generator.CenterX, generator.CenterY, $"{enemyTeam} generator"))
            .ToArray();
        return objectives.Length > 0
            ? objectives
            : level.GetRoomObjects(RoomObjectType.Generator)
                .Select(generator => new ObjectivePoint(generator.CenterX, generator.CenterY, "generator"))
                .ToArray();
    }

    private static ObjectivePoint[] GetControlPointObjectives(SimpleLevel level)
    {
        var controlPoints = level.GetRoomObjects(RoomObjectType.ControlPoint)
            .Select(point => new ObjectivePoint(point.CenterX, point.CenterY, "control point"))
            .ToArray();
        if (controlPoints.Length > 0)
        {
            return controlPoints;
        }

        return level.GetRoomObjects(RoomObjectType.CaptureZone)
            .Select(point => new ObjectivePoint(point.CenterX, point.CenterY, "capture zone"))
            .ToArray();
    }

    private static ObjectivePoint[] GetArenaObjectives(SimpleLevel level)
    {
        return level.GetRoomObjects(RoomObjectType.ArenaControlPoint)
            .Select(point => new ObjectivePoint(point.CenterX, point.CenterY, "arena point"))
            .ToArray();
    }

    private static IReadOnlyList<ObjectivePoint> GetFallbackObjectives(SimpleLevel level)
    {
        return
        [
            new ObjectivePoint(level.Bounds.Width / 2f, level.Bounds.Height / 2f, "level center"),
        ];
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    private static PlayerTeam? GetNodeTeam(BotNavigationNode node)
    {
        if (node.Team.HasValue)
        {
            return node.Team.Value;
        }

        if (node.Label.StartsWith("red", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerTeam.Red;
        }

        if (node.Label.StartsWith("blue", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    private readonly record struct NavMarkerPoint(float X, float Y, string Label);

    private readonly record struct ObjectivePoint(float X, float Y, string Label);
}
