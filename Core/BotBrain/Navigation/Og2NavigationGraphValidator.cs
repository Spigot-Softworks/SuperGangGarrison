namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Structural acceptance checks for the OG2 contact graph.
///
/// This validator deliberately does not simulate a bot. A passing report only
/// means that the graph is internally coherent and exposes a filtered route
/// for the requested movement contexts. Runtime execution remains a separate
/// acceptance gate.
/// </summary>
public static class Og2NavigationGraphValidator
{
    private const float SpawnMaxAboveDistance = 64f;
    private const float SpawnMaxBelowDistance = 96f;
    private const float ObjectiveApproachMaxAboveDistance = 256f;
    private const float ObjectiveApproachMaxBelowDistance = 256f;
    private const float ObjectiveApproachMaxHorizontalDistance = 256f;
    private const float CarrierMaxAboveDistance = 96f;
    private const float CarrierMaxBelowDistance = 128f;
    private const int MaximumReportedIssues = 256;

    public static Og2NavigationGraphValidationReport Validate(
        SimpleLevel level,
        NavGraph graph,
        IReadOnlyList<PlayerClass> classes)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(classes);

        var issues = new List<Og2NavigationGraphValidationIssue>();
        var routes = new List<Og2NavigationGraphRouteCheck>();
        ValidateGraphShape(graph, issues);

        var distinctClasses = classes.Distinct().ToArray();
        if (distinctClasses.Length == 0)
        {
            AddIssue(issues, "no_classes", "No movement classes were supplied to the graph gate.");
            return new Og2NavigationGraphValidationReport(issues, routes);
        }

        var targets = ResolveObjectiveTargets(level, graph, issues);
        if (targets.Count == 0)
        {
            AddIssue(issues, "no_objectives", "The level has no objective anchor that can be checked.");
            return new Og2NavigationGraphValidationReport(issues, routes);
        }

        foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
        {
            var spawns = team == PlayerTeam.Red ? level.RedSpawns : level.BlueSpawns;
            if (spawns.Count == 0)
            {
                AddIssue(issues, "no_spawns", $"Team {team} has no spawn regions.");
                continue;
            }

            foreach (var playerClass in distinctClasses)
            {
                foreach (var spawn in spawns)
                {
                    if (level.Mode == GameModeKind.CaptureTheFlag)
                    {
                        ValidateCaptureTheFlagRoutes(
                            level,
                            graph,
                            team,
                            playerClass,
                            spawn,
                            targets,
                            routes,
                            issues);
                    }
                    else
                    {
                        ValidateObjectiveRoutes(
                            graph,
                            team,
                            playerClass,
                            spawn,
                            targets,
                            routes,
                            issues);
                    }
                }
            }
        }

        return new Og2NavigationGraphValidationReport(issues, routes);
    }

    private static void ValidateGraphShape(
        NavGraph graph,
        List<Og2NavigationGraphValidationIssue> issues)
    {
        if (graph.NodeCount == 0)
        {
            AddIssue(issues, "empty_graph", "The generated graph contains no nodes.");
            return;
        }

        var incoming = new int[graph.NodeCount];
        for (var fromNode = 0; fromNode < graph.NodeCount; fromNode += 1)
        {
            var node = graph.GetNode(fromNode);
            if (!float.IsFinite(node.X) || !float.IsFinite(node.Y))
            {
                AddIssue(issues, "non_finite_node", $"Node {fromNode} has non-finite coordinates.");
            }

            foreach (var edge in graph.GetEdges(fromNode))
            {
                if (edge.ToNode < 0 || edge.ToNode >= graph.NodeCount)
                {
                    AddIssue(issues, "edge_target_out_of_range", $"Edge {fromNode}->{edge.ToNode} has an invalid target.");
                    continue;
                }

                incoming[edge.ToNode] += 1;
                ValidateEdge(graph, fromNode, edge, issues);
            }
        }

        for (var nodeIndex = 0; nodeIndex < graph.NodeCount; nodeIndex += 1)
        {
            var node = graph.GetNode(nodeIndex);
            var outgoing = graph.GetEdges(nodeIndex).Length;
            if (node.Kind == NavNodeKind.Objective)
            {
                // Objective anchors may intentionally be terminal goals, but
                // every non-objective node must participate in the graph.
                continue;
            }

            if (outgoing == 0 && incoming[nodeIndex] == 0)
            {
                AddIssue(issues, "isolated_node", $"Node {nodeIndex} at ({node.X:0.0},{node.Y:0.0}) is isolated.");
            }
        }
    }

    private static void ValidateEdge(
        NavGraph graph,
        int fromNode,
        NavEdge edge,
        List<Og2NavigationGraphValidationIssue> issues)
    {
        if (!float.IsFinite(edge.Cost) || edge.Cost <= 0f)
        {
            AddIssue(issues, "invalid_edge_cost", $"Edge {fromNode}->{edge.ToNode}/{edge.Kind} has cost {edge.Cost}.");
        }

        if (edge.SupportedClassMask == 0)
        {
            AddIssue(issues, "edge_has_no_class", $"Edge {fromNode}->{edge.ToNode}/{edge.Kind} supports no class.");
        }

        if (edge.SupportedTeamMask == 0)
        {
            AddIssue(issues, "edge_has_no_team", $"Edge {fromNode}->{edge.ToNode}/{edge.Kind} supports no team.");
        }

        if (edge.CarryingIntelRequirement.HasValue
            && edge.RequiresCarryingIntel != edge.CarryingIntelRequirement.Value)
        {
            AddIssue(
                issues,
                "carry_state_mismatch",
                $"Edge {fromNode}->{edge.ToNode}/{edge.Kind} disagrees about carrying-intel eligibility.");
        }

        if (edge.Kind == NavEdgeKind.Walk)
        {
            return;
        }

        if (edge.ProbeTicks <= 0 || edge.ProbeVariantAttempts <= 0 || edge.ProbeVariantSuccesses <= 0)
        {
            AddIssue(
                issues,
                "unproven_transition",
                $"Edge {fromNode}->{edge.ToNode}/{edge.Kind} has no successful OG2 probe metadata.");
        }

        if (!edge.Completion.HasWindow)
        {
            AddIssue(
                issues,
                "missing_completion_window",
                $"Edge {fromNode}->{edge.ToNode}/{edge.Kind} has no settled completion window.");
        }

        if (edge.Completion.AcceptedSurfaceIds.Length == 0)
        {
            AddIssue(
                issues,
                "missing_completion_surface",
                $"Edge {fromNode}->{edge.ToNode}/{edge.Kind} has no accepted destination surface.");
        }

        if (edge.Kind == NavEdgeKind.Jump
            && (!edge.LaunchRecipe.HasRecipe || edge.JumpTriggerTick < 0))
        {
            AddIssue(
                issues,
                "missing_jump_recipe",
                $"Jump edge {fromNode}->{edge.ToNode} has no executable launch recipe.");
        }

        if (edge.Completion.HasWindow
            && edge.Completion.AcceptedSurfaceIds.Length > 0)
        {
            var target = graph.GetNode(edge.ToNode);
            if (!target.SurfaceId.HasValue
                || !edge.Completion.AcceptedSurfaceIds.Contains(target.SurfaceId.Value))
            {
                AddIssue(
                    issues,
                    "completion_surface_mismatch",
                    $"Edge {fromNode}->{edge.ToNode}/{edge.Kind} completion does not name the target surface.");
            }
        }
    }

    private static void ValidateCaptureTheFlagRoutes(
        SimpleLevel level,
        NavGraph graph,
        PlayerTeam team,
        PlayerClass playerClass,
        SpawnPoint spawn,
        IReadOnlyList<Og2NavigationObjectiveTarget> targets,
        List<Og2NavigationGraphRouteCheck> routes,
        List<Og2NavigationGraphValidationIssue> issues)
    {
        var enemyTeam = OpposingTeam(team);
        var ownTarget = targets.FirstOrDefault(target => target.Team == team);
        var enemyTarget = targets.FirstOrDefault(target => target.Team == enemyTeam);
        if (!enemyTarget.IsValid || !ownTarget.IsValid)
        {
            AddIssue(issues, "missing_ctf_target", $"Could not resolve both intel bases for team {team}.");
            return;
        }

        var spawnNode = FindSpawnNode(graph, spawn);
        var enemyGoalNode = FindObjectiveNode(graph, enemyTarget);
        var ownGoalNode = FindObjectiveNode(graph, ownTarget);
        if (spawnNode < 0 || enemyGoalNode < 0 || ownGoalNode < 0)
        {
            AddIssue(
                issues,
                "missing_ctf_attachment",
                $"Could not attach {team}/{playerClass} spawn ({spawn.X:0.0},{spawn.Y:0.0}) or intel target to the graph.");
            return;
        }

        var outbound = graph.FindPath(spawnNode, enemyGoalNode, playerClass, team: team, carryingIntel: false);
        AddRouteCheck(
            routes,
            issues,
            graph,
            $"{team}/{playerClass}/outbound",
            team,
            playerClass,
            carryingIntel: false,
            spawnNode,
            enemyGoalNode,
            outbound);

        var carrierStartNode = FindNearestSurfaceNode(
            graph,
            enemyTarget.X,
            enemyTarget.Y,
            CarrierMaxAboveDistance,
            CarrierMaxBelowDistance);
        var returnPath = carrierStartNode >= 0
            ? graph.FindPath(carrierStartNode, ownGoalNode, playerClass, team: team, carryingIntel: true)
            : null;
        AddRouteCheck(
            routes,
            issues,
            graph,
            $"{team}/{playerClass}/carrier-return",
            team,
            playerClass,
            carryingIntel: true,
            carrierStartNode,
            ownGoalNode,
            returnPath);
    }

    private static void ValidateObjectiveRoutes(
        NavGraph graph,
        PlayerTeam team,
        PlayerClass playerClass,
        SpawnPoint spawn,
        IReadOnlyList<Og2NavigationObjectiveTarget> targets,
        List<Og2NavigationGraphRouteCheck> routes,
        List<Og2NavigationGraphValidationIssue> issues)
    {
        var spawnNode = FindSpawnNode(graph, spawn);
        if (spawnNode < 0)
        {
            AddIssue(issues, "spawn_not_attached", $"Could not attach {team}/{playerClass} spawn ({spawn.X:0.0},{spawn.Y:0.0}) to the graph.");
            return;
        }

        foreach (var target in targets)
        {
            var approach = FindObjectiveApproachPath(graph, target, spawnNode, playerClass, team);
            AddRouteCheck(
                routes,
                issues,
                graph,
                $"{team}/{playerClass}/objective:{target.Label}",
                team,
                playerClass,
                carryingIntel: false,
                spawnNode,
                approach.GoalNode,
                approach.Path);
        }
    }

    private static (int GoalNode, NavPath? Path) FindObjectiveApproachPath(
        NavGraph graph,
        Og2NavigationObjectiveTarget target,
        int startNode,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        // The exact objective marker is not necessarily a walkable coordinate:
        // stock CP/KOTH maps commonly place the logical marker above the
        // floor. Try it first for maps that provide a real attached objective
        // node, then fall back to the nearest reachable surface node within
        // the same approach envelope used by alpha runtime navigation.
        var exactGoalNode = FindObjectiveNode(graph, target);
        if (exactGoalNode >= 0)
        {
            var exactPath = graph.FindPath(startNode, exactGoalNode, playerClass, team: team);
            if (exactPath is not null)
            {
                return (exactGoalNode, exactPath);
            }
        }

        var candidates = new List<(int NodeIndex, float Score)>();
        for (var nodeIndex = 0; nodeIndex < graph.NodeCount; nodeIndex += 1)
        {
            var node = graph.GetNode(nodeIndex);
            if (!node.SurfaceId.HasValue || node.Kind is NavNodeKind.Spawn or NavNodeKind.Objective)
            {
                continue;
            }

            var dx = node.X - target.X;
            var dy = node.Y - target.Y;
            if (MathF.Abs(dx) > ObjectiveApproachMaxHorizontalDistance
                || dy < -ObjectiveApproachMaxAboveDistance
                || dy > ObjectiveApproachMaxBelowDistance)
            {
                continue;
            }

            candidates.Add((nodeIndex, (dx * dx) + (dy * dy * 4f)));
        }

        foreach (var candidate in candidates.OrderBy(static candidate => candidate.Score))
        {
            var path = graph.FindPath(startNode, candidate.NodeIndex, playerClass, team: team);
            if (path is not null)
            {
                return (candidate.NodeIndex, path);
            }
        }

        return (exactGoalNode, null);
    }

    private static void AddRouteCheck(
        List<Og2NavigationGraphRouteCheck> routes,
        List<Og2NavigationGraphValidationIssue> issues,
        NavGraph graph,
        string label,
        PlayerTeam team,
        PlayerClass playerClass,
        bool carryingIntel,
        int startNode,
        int goalNode,
        NavPath? path)
    {
        var passed = path is not null && path.Count > 0;
        var reason = passed ? "reachable" : "no_filtered_path";
        if (passed)
        {
            reason = ValidatePathEdges(graph, path!, team, playerClass, carryingIntel, issues, label);
            passed = reason == "reachable";
        }

        routes.Add(new Og2NavigationGraphRouteCheck(
            label,
            team,
            playerClass,
            carryingIntel,
            startNode,
            goalNode,
            path?.Count ?? 0,
            passed,
            reason));

        if (!passed)
        {
            AddIssue(issues, "route_unreachable", $"{label} has no valid filtered graph route ({reason}).");
        }
    }

    private static string ValidatePathEdges(
        NavGraph graph,
        NavPath path,
        PlayerTeam team,
        PlayerClass playerClass,
        bool carryingIntel,
        List<Og2NavigationGraphValidationIssue> issues,
        string label)
    {
        for (var index = 1; index < path.Count; index += 1)
        {
            if (!path.TryGetIncomingEdge(index, out var edge))
            {
                return "missing_path_edge";
            }

            var fromNode = path.GetWaypoint(index - 1);
            var toNode = path.GetWaypoint(index);
            if (!edge.Supports(playerClass, team, carryingIntel))
            {
                AddIssue(issues, "path_edge_filter_mismatch", $"{label} contains an edge rejected by its own filter.");
                return "path_edge_filter_mismatch";
            }

            if (edge.ToNode != toNode)
            {
                AddIssue(issues, "path_edge_target_mismatch", $"{label} path edge {fromNode}->{toNode} targets {edge.ToNode}.");
                return "path_edge_target_mismatch";
            }

            if (edge.Kind != NavEdgeKind.Walk
                && !graph.IsEdgeCompletionSatisfied(
                    graph.GetNode(toNode).X,
                    graph.GetNode(toNode).Y,
                    edge.Completion))
            {
                AddIssue(issues, "path_completion_mismatch", $"{label} ends a transition outside its completion contract.");
                return "path_completion_mismatch";
            }
        }

        return "reachable";
    }

    private static int FindSpawnNode(NavGraph graph, SpawnPoint spawn) =>
        FindNearestSurfaceNode(
            graph,
            spawn.X,
            spawn.Y,
            SpawnMaxAboveDistance,
            SpawnMaxBelowDistance);

    private static int FindNearestSurfaceNode(
        NavGraph graph,
        float x,
        float y,
        float maxAboveDistance,
        float maxBelowDistance)
    {
        var bestNode = -1;
        var bestScore = float.MaxValue;
        for (var nodeIndex = 0; nodeIndex < graph.NodeCount; nodeIndex += 1)
        {
            var node = graph.GetNode(nodeIndex);
            // Objective anchors sit at the exact intel/control-point marker
            // coordinate. They are valid goals but never valid traversal
            // starts for a carrier leaving the marker.
            if (!node.SurfaceId.HasValue)
            {
                continue;
            }

            if (node.Y < y - maxAboveDistance || node.Y > y + maxBelowDistance)
            {
                continue;
            }

            var dx = node.X - x;
            var dy = node.Y - y;
            var score = (dx * dx) + (dy * dy * 4f);
            if (score < bestScore)
            {
                bestScore = score;
                bestNode = nodeIndex;
            }
        }

        return bestNode;
    }

    private static int FindObjectiveNode(NavGraph graph, Og2NavigationObjectiveTarget target)
    {
        var bestNode = -1;
        var bestDistance = float.MaxValue;
        for (var nodeIndex = 0; nodeIndex < graph.NodeCount; nodeIndex += 1)
        {
            var node = graph.GetNode(nodeIndex);
            if (node.Kind != NavNodeKind.Objective)
            {
                continue;
            }

            var dx = node.X - target.X;
            var dy = node.Y - target.Y;
            var distance = (dx * dx) + (dy * dy);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestNode = nodeIndex;
            }
        }

        return bestNode;
    }

    private static List<Og2NavigationObjectiveTarget> ResolveObjectiveTargets(
        SimpleLevel level,
        NavGraph graph,
        List<Og2NavigationGraphValidationIssue> issues)
    {
        var targets = new List<Og2NavigationObjectiveTarget>();
        if (level.Mode == GameModeKind.CaptureTheFlag)
        {
            foreach (var intel in level.IntelBases)
            {
                targets.Add(new Og2NavigationObjectiveTarget(
                    $"intel:{intel.Team}",
                    intel.X,
                    intel.Y,
                    intel.Team));
            }

            return targets;
        }

        var hasCaptureZones = level.GetRoomObjects(RoomObjectType.CaptureZone).Count > 0;
        foreach (var roomObject in level.RoomObjects)
        {
            if (roomObject.Type is not (RoomObjectType.ArenaControlPoint
                or RoomObjectType.ControlPoint
                or RoomObjectType.CaptureZone
                or RoomObjectType.Generator))
            {
                continue;
            }

            // The control-point marker is a logical/visual object and may be
            // above the walkable floor. The runtime alpha planner targets the
            // associated CaptureZone, so validating both coordinates would
            // report a false unreachable route for an otherwise playable map.
            if (hasCaptureZones
                && roomObject.Type is RoomObjectType.ArenaControlPoint or RoomObjectType.ControlPoint)
            {
                continue;
            }

            targets.Add(new Og2NavigationObjectiveTarget(
                $"{roomObject.Type}:{roomObject.CenterX:0}:{roomObject.CenterY:0}",
                roomObject.CenterX,
                roomObject.CenterY,
                roomObject.Team));
        }

        if (targets.Count == 0)
        {
            for (var nodeIndex = 0; nodeIndex < graph.NodeCount; nodeIndex += 1)
            {
                var node = graph.GetNode(nodeIndex);
                if (node.Kind == NavNodeKind.Objective)
                {
                    targets.Add(new Og2NavigationObjectiveTarget(
                        $"node:{nodeIndex}",
                        node.X,
                        node.Y,
                        null));
                }
            }
        }

        return targets
            .GroupBy(static target => (target.Label, target.X, target.Y, target.Team))
            .Select(static group => group.First())
            .ToList();
    }

    private static PlayerTeam OpposingTeam(PlayerTeam team) =>
        team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;

    private static void AddIssue(
        List<Og2NavigationGraphValidationIssue> issues,
        string code,
        string message)
    {
        if (issues.Count < MaximumReportedIssues)
        {
            issues.Add(new Og2NavigationGraphValidationIssue(code, message));
        }
    }
}

public sealed class Og2NavigationGraphValidationReport
{
    public Og2NavigationGraphValidationReport(
        IReadOnlyList<Og2NavigationGraphValidationIssue> issues,
        IReadOnlyList<Og2NavigationGraphRouteCheck> routes)
    {
        Issues = issues;
        Routes = routes;
    }

    public IReadOnlyList<Og2NavigationGraphValidationIssue> Issues { get; }

    public IReadOnlyList<Og2NavigationGraphRouteCheck> Routes { get; }

    public int ErrorCount => Issues.Count;

    public int RouteCount => Routes.Count;

    public int PassedRouteCount => Routes.Count(static route => route.Passed);

    public bool Passed => ErrorCount == 0 && RouteCount > 0 && PassedRouteCount == RouteCount;
}

public readonly record struct Og2NavigationGraphValidationIssue(string Code, string Message);

public readonly record struct Og2NavigationGraphRouteCheck(
    string Label,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    bool CarryingIntel,
    int StartNode,
    int GoalNode,
    int PathNodeCount,
    bool Passed,
    string Reason);

public readonly record struct Og2NavigationObjectiveTarget(
    string Label,
    float X,
    float Y,
    PlayerTeam? Team)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Label);
}
