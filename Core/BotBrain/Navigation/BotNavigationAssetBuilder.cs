namespace OpenGarrison.Core.BotBrain;

public static class BotNavigationAssetBuilder
{
    private const float ProbeHalfWidth = 20f;
    private const float ProbeHeight = 48f;
    private const float SurfaceWaypointSpacing = 80f;
    private const float MaxJumpReach = 280f;
    private const float MaxFallDistance = 600f;
    private const float ConservativeJumpSpeed = 200f;
    private const float StepDownTolerance = 12f;
    private const float SurfaceMergeVerticalTolerance = 2f;
    private const float SurfaceMergeGapTolerance = 4f;
    private const float MinimumSurfaceWidth = 4f;
    private const float MinimumInsetSurfaceWidth = ProbeHalfWidth * 2f;
    private const float StepRelayHorizontalReach = 96f;
    private const float StepRelayVerticalReach = 14f;
    private const float StairRampRelayHorizontalReach = 132f;
    private const float StairRampRelayVerticalReach = 88f;
    private const float StairRampRelayJumpVerticalReach = 74f;
    private const float StairRampRelayMinimumHorizontal = 8f;
    private const float StairRampRelayCostMultiplier = 0.18f;
    private const float ReverseFallJumpVerticalReach = 120f;
    private const float MinimumPortalSpacing = 36f;
    private const float LongSurfacePortalSpacing = 320f;
    private const float InteriorCornerSurfaceTolerance = 18f;
    private const float AnchorPortalHorizontalRange = 192f;
    private const float AnchorPortalVerticalRange = 512f;
    private const int DefaultMaxPortalCandidatesPerKindDirection = 24;
    private const int CompactObjectiveMaxPortalCandidatesPerKindDirection = 8;
    private const int CompactObjectiveSurfaceCountThreshold = 600;
    private const int CtfVerticalBandPortalCandidatesPerKindDirection = 12;
    private const float CtfVerticalBandMinAscent = 96f;
    private const float CtfVerticalBandMaxAscent = 160f;
    private const float BlockedMicroStepRelayWalkPenalty = 250f;
    private const float AuthoredCorridorMaxSnapDistance = 192f;
    private const float AuthoredCorridorVirtualNodeSpacing = 48f;
    private const float AuthoredCorridorDirectEdgeMaxDistance = 360f;
    private const float AuthoredCorridorPreferredCostMultiplier = 0.2f;
    private const float AuthoredCorridorObjectiveArrivalDistance = 96f;

    public static BotNavigationAsset BuildAsset(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var graphBuild = BuildGraphData(level);
        var asset = new BotNavigationAsset
        {
            FormatVersion = BotNavigationAssetStore.CurrentFormatVersion,
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
            LevelFingerprint = BotNavigationAssetStore.ComputeLevelFingerprint(level),
            Surfaces = graphBuild.Surfaces
                .Select(static surface => new BotNavigationSurfaceAssetEntry
                {
                    Id = surface.Id,
                    LeftX = surface.LeftX,
                    RightX = surface.RightX,
                    TopY = surface.TopY,
                    IsDropdown = surface.IsDropdown,
                    FirstNodeIndex = surface.FirstNodeIndex,
                    LastNodeIndex = surface.LastNodeIndex,
                })
                .ToList(),
            Nodes = graphBuild.Nodes
                .Select(static node => new BotNavigationNodeAssetEntry
                {
                    X = node.X,
                    Y = node.Y,
                    Kind = node.Kind,
                    SurfaceId = node.SurfaceId,
                })
                .ToList(),
            Edges = graphBuild.Edges
                .Select(static edge =>
                {
                    var probeResult = edge.ProbeResult;
                    var completionResult = probeResult ?? edge.SemanticCompletion;
                    return new BotNavigationEdgeAssetEntry
                    {
                        FromNode = edge.FromNode,
                        ToNode = edge.ToNode,
                        Kind = edge.Kind,
                        Cost = edge.Cost,
                        FromPortalId = edge.FromPortalId,
                        ToPortalId = edge.ToPortalId,
                        ProbeCertified = probeResult.HasValue,
                        ProbeJumpTriggerTick = probeResult?.JumpTriggerTick ?? 0,
                        ProbeTicks = probeResult?.Ticks ?? 0,
                        ProbeMoveDirectionX = probeResult?.MoveDirectionX ?? 0f,
                        ProbeVariantAttempts = probeResult?.VariantAttempts ?? 0,
                        ProbeVariantSuccesses = probeResult?.VariantSuccesses ?? 0,
                        SupportedClassMask = edge.SupportedClassMask,
                        SupportedTeamMask = edge.SupportedTeamMask,
                        CompletionMinX = completionResult?.CompletionMinX ?? 0f,
                        CompletionMaxX = completionResult?.CompletionMaxX ?? 0f,
                        CompletionMinY = completionResult?.CompletionMinY ?? 0f,
                        CompletionMaxY = completionResult?.CompletionMaxY ?? 0f,
                        AcceptedLandingSurfaceIds = completionResult?.AcceptedLandingSurfaceIds.ToList() ?? [],
                        RequiresGroundedContinuation = completionResult?.RequiresGroundedContinuation ?? false,
                        LaunchRecipe = probeResult?.LaunchRecipe is { } launchRecipe
                            ? new BotNavigationLaunchRecipeAssetEntry
                            {
                                StartGrounded = launchRecipe.StartGrounded,
                                LaunchTick = launchRecipe.LaunchTick,
                                LaunchMinX = launchRecipe.LaunchMinX,
                                LaunchMaxX = launchRecipe.LaunchMaxX,
                                LaunchMinY = launchRecipe.LaunchMinY,
                                LaunchMaxY = launchRecipe.LaunchMaxY,
                                LaunchMinHorizontalSpeed = launchRecipe.LaunchMinHorizontalSpeed,
                                LaunchMaxHorizontalSpeed = launchRecipe.LaunchMaxHorizontalSpeed,
                                ExpectedMoveDirectionX = launchRecipe.ExpectedMoveDirectionX,
                            }
                            : null,
                    };
                })
                .ToList(),
            Portals = graphBuild.Portals
                .Select(static portal => new BotNavigationPortalAssetEntry
                {
                    Id = portal.Id,
                    Kind = portal.Kind.ToString(),
                    NodeIndex = portal.NodeIndex,
                    SurfaceId = portal.SurfaceId,
                    AnchorIndex = portal.AnchorIndex,
                    X = portal.X,
                    Y = portal.Y,
                })
                .ToList(),
            Anchors = graphBuild.Anchors,
            BuildStats = new BotNavigationBuildStatsAssetEntry
            {
                SurfacePairChecks = graphBuild.Stats.SurfacePairChecks,
                NodePairChecks = graphBuild.Stats.NodePairChecks,
                CertifiedProbeAttempts = graphBuild.Stats.CertifiedProbeAttempts,
                CertifiedProbeSuccesses = graphBuild.Stats.CertifiedProbeSuccesses,
            },
        };
        asset.ValidationIssues = ValidateSpawnObjectiveReachability(asset);
        return asset;
    }

    public static NavGraph BuildGraph(SimpleLevel level)
    {
        return ToGraph(BuildAsset(level));
    }

    public static NavGraph ToGraph(BotNavigationAsset asset)
    {
        return ToGraph(asset, level: null);
    }

    public static NavGraph ToGraph(BotNavigationAsset asset, SimpleLevel? level)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var nodes = asset.Nodes
            .Select(static node => new NavNode(node.X, node.Y, node.Kind, node.SurfaceId))
            .ToArray();
        var adjacency = new List<NavEdge>[nodes.Length];
        for (var i = 0; i < adjacency.Length; i += 1)
        {
            adjacency[i] = new List<NavEdge>(4);
        }

        foreach (var edge in asset.Edges)
        {
            if (edge.FromNode < 0
                || edge.FromNode >= nodes.Length
                || edge.ToNode < 0
                || edge.ToNode >= nodes.Length
                || edge.Cost < 0f)
            {
                continue;
            }

            var hasCompletion = edge.CompletionMaxX > edge.CompletionMinX
                && edge.CompletionMaxY > edge.CompletionMinY;
            var completion = hasCompletion
                ? new NavEdgeCompletion(
                    edge.CompletionMinX,
                    edge.CompletionMaxX,
                    edge.CompletionMinY,
                    edge.CompletionMaxY,
                    edge.AcceptedLandingSurfaceIds.ToArray())
                : NavEdgeCompletion.None;
            var launchRecipe = edge.LaunchRecipe is { } recipe
                ? new NavEdgeLaunchRecipe(
                    recipe.StartGrounded,
                    recipe.LaunchTick,
                    recipe.LaunchMinX,
                    recipe.LaunchMaxX,
                    recipe.LaunchMinY,
                    recipe.LaunchMaxY,
                    recipe.LaunchMinHorizontalSpeed,
                    recipe.LaunchMaxHorizontalSpeed,
                    MathF.Sign(recipe.ExpectedMoveDirectionX))
                : NavEdgeLaunchRecipe.None;
            adjacency[edge.FromNode].Add(new NavEdge(
                edge.ToNode,
                edge.Kind,
                edge.Cost,
                completion,
                Math.Max(0, edge.ProbeJumpTriggerTick),
                Math.Max(0, edge.ProbeTicks),
                MathF.Sign(edge.ProbeMoveDirectionX),
                Math.Max(0, edge.ProbeVariantAttempts),
                Math.Max(0, edge.ProbeVariantSuccesses),
                edge.SupportedClassMask,
                edge.SupportedTeamMask,
                edge.RequiresGroundedContinuation,
                RequiresCarryingIntel: false,
                launchRecipe));
        }

        if (level is not null)
        {
            AddAuthoredCorridorReturnEdges(level, nodes, adjacency);
        }

        return new NavGraph(nodes, adjacency, level?.Name, level?.Mode);
    }

    private static void AddAuthoredCorridorReturnEdges(SimpleLevel level, NavNode[] nodes, List<NavEdge>[] adjacency)
    {
        if (level.Mode != GameModeKind.CaptureTheFlag
            || nodes.Length == 0
            || !BotNavigationAuthoredCorridorStore.TryLoad(level, out var corridorAsset))
        {
            return;
        }

        foreach (var corridor in corridorAsset.Corridors)
        {
            if (!TryResolveCaptureTheFlagReturnRange(level, corridor, out var startIndex))
            {
                continue;
            }

            var supportedClassMask = corridor.PlayerClass == PlayerClass.Heavy
                ? BotBrainClassMask.All
                : BotBrainClassMask.For(corridor.PlayerClass);
            var supportedTeamMask = BotBrainTeamMask.For(corridor.Team);
            var previousNode = -1;
            for (var i = startIndex; i < corridor.Waypoints.Count; i += 1)
            {
                var waypoint = corridor.Waypoints[i];
                if (!waypoint.IsGrounded)
                {
                    continue;
                }

                var nodeIndex = FindNearestCorridorGraphNode(nodes, waypoint.X, waypoint.Y, maxDistance: 128f);
                if (nodeIndex < 0)
                {
                    continue;
                }

                if (previousNode >= 0 && previousNode != nodeIndex)
                {
                    AddAuthoredCorridorGraphEdge(nodes, adjacency, previousNode, nodeIndex, supportedClassMask, supportedTeamMask);
                }

                previousNode = nodeIndex;
            }
        }
    }

    private static bool TryResolveCaptureTheFlagReturnRange(
        SimpleLevel level,
        BotNavigationAuthoredCorridorEntry corridor,
        out int startIndex)
    {
        startIndex = -1;
        var opposingTeam = corridor.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var enemyBase = level.GetIntelBase(opposingTeam);
        var ownBase = level.GetIntelBase(corridor.Team);
        if (!enemyBase.HasValue || !ownBase.HasValue || corridor.Waypoints.Count < 2)
        {
            return false;
        }

        var bestEnemyDistance = float.PositiveInfinity;
        var bestEnemyIndex = -1;
        for (var i = 0; i < corridor.Waypoints.Count; i += 1)
        {
            var waypoint = corridor.Waypoints[i];
            var enemyDistance = DistanceSquared(waypoint.X, waypoint.Y, enemyBase.Value.X, enemyBase.Value.Y);
            if (enemyDistance < bestEnemyDistance)
            {
                bestEnemyDistance = enemyDistance;
                bestEnemyIndex = i;
            }
        }

        var last = corridor.Waypoints[^1];
        if (bestEnemyIndex < 0
            || bestEnemyIndex >= corridor.Waypoints.Count - 1
            || bestEnemyDistance > 260f * 260f
            || DistanceSquared(last.X, last.Y, ownBase.Value.X, ownBase.Value.Y) > 960f * 960f)
        {
            return false;
        }

        startIndex = bestEnemyIndex;
        return true;
    }

    private static int FindNearestCorridorGraphNode(NavNode[] nodes, float x, float y, float maxDistance)
    {
        var bestIndex = -1;
        var bestDistanceSq = maxDistance * maxDistance;
        for (var i = 0; i < nodes.Length; i += 1)
        {
            var dx = nodes[i].X - x;
            var dy = nodes[i].Y - y;
            var distanceSq = (dx * dx) + (dy * dy);
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static void AddAuthoredCorridorGraphEdge(
        NavNode[] nodes,
        List<NavEdge>[] adjacency,
        int fromNode,
        int toNode,
        int supportedClassMask,
        int supportedTeamMask)
    {
        var from = nodes[fromNode];
        var to = nodes[toNode];
        var distance = MathF.Sqrt(DistanceSquared(from.X, from.Y, to.X, to.Y));
        if (distance <= 0f || distance > 560f)
        {
            return;
        }

        var edges = adjacency[fromNode];
        for (var i = 0; i < edges.Count; i += 1)
        {
            var edge = edges[i];
            if (edge.ToNode != toNode)
            {
                continue;
            }

            var cost = MathF.Max(1f, distance * 0.12f);
            edges.Add(edge with
            {
                Cost = MathF.Min(edge.Cost, cost),
                SupportedClassMask = edge.SupportedClassMask | supportedClassMask,
                SupportedTeamMask = edge.SupportedTeamMask | supportedTeamMask,
                RequiresCarryingIntel = true,
            });
            return;
        }
    }

    private static GraphBuildData BuildGraphData(SimpleLevel level)
    {
        var tracePath = Environment.GetEnvironmentVariable("BOTBRAIN_BUILD_TRACE");
        TraceBuild(tracePath, $"start level={level.Name} solids={level.Solids.Count} roomObjects={level.RoomObjects.Count}");
        var nodes = new List<BuildNode>();
        var surfaces = BuildSurfaces(level);
        TraceBuild(tracePath, $"surfaces count={surfaces.Count}");
        for (var i = 0; i < surfaces.Count; i += 1)
        {
            var surface = surfaces[i];
            surface.FirstNodeIndex = nodes.Count;
            AddSurfaceNodes(nodes, surface);
            surface.LastNodeIndex = nodes.Count - 1;
            surfaces[i] = surface;
        }

        TraceBuild(tracePath, $"nodes count={nodes.Count}");
        var anchors = AddAnchorNodes(level, nodes);
        var portals = BuildPortals(level, surfaces, nodes, anchors);
        TraceBuild(tracePath, $"portals count={portals.Count}");
        var probeSurfaces = surfaces
            .Select(static surface => new BotBrainProbeSurface(surface.Id, surface.LeftX, surface.RightX, surface.TopY))
            .ToArray();
        var edgeSet = new HashSet<EdgeKey>();
        var edges = new List<BuildEdge>();
        var stats = new BuildStats { TracePath = tracePath };
        AddSurfaceWalkEdges(surfaces, nodes, edges, edgeSet);
        TraceBuild(tracePath, $"walkEdges edges={edges.Count}");
        AddStepRelayEdges(level, surfaces, portals, nodes, edges, edgeSet);
        TraceBuild(tracePath, $"stepRelay edges={edges.Count}");
        if (ShouldUseCtfMicroStepRepairs(level))
        {
            AddMicroStepExitJumpEdges(level, surfaces, portals, probeSurfaces, nodes, edges, edgeSet, stats);
        }

        TraceBuild(tracePath, $"microStepExits edges={edges.Count} attempts={stats.CertifiedProbeAttempts} successes={stats.CertifiedProbeSuccesses}");
        AddLedgeDropDiscoveryEdges(level, surfaces, portals, anchors, probeSurfaces, nodes, edges, edgeSet, stats);
        TraceBuild(tracePath, $"ledgeDrops edges={edges.Count} attempts={stats.CertifiedProbeAttempts} successes={stats.CertifiedProbeSuccesses}");
        AddTraversalEdges(level, surfaces, portals, probeSurfaces, nodes, edges, edgeSet, stats);
        TraceBuild(tracePath, $"traversal edges={edges.Count} attempts={stats.CertifiedProbeAttempts} successes={stats.CertifiedProbeSuccesses} nodePairs={stats.NodePairChecks}");
        AddOrangeSpawnExitBridgeEdges(level, surfaces, portals, probeSurfaces, nodes, edges, edgeSet, stats);
        TraceBuild(tracePath, $"orangeSpawnExitBridges edges={edges.Count} attempts={stats.CertifiedProbeAttempts} successes={stats.CertifiedProbeSuccesses}");
        AddAuthoredCorridorVirtualNodes(level, surfaces, nodes);
        TraceBuild(tracePath, $"authoredCorridorNodes surfaces={surfaces.Count} nodes={nodes.Count}");
        AddAuthoredCorridorEdges(level, surfaces, portals, probeSurfaces, nodes, edges, edgeSet, stats);
        TraceBuild(tracePath, $"authoredCorridors edges={edges.Count} attempts={stats.CertifiedProbeAttempts} successes={stats.CertifiedProbeSuccesses}");
        AddAnchorEdges(level, surfaces, portals, probeSurfaces, nodes, edges, edgeSet, stats);
        TraceBuild(tracePath, $"anchors edges={edges.Count} attempts={stats.CertifiedProbeAttempts} successes={stats.CertifiedProbeSuccesses}");

        return new GraphBuildData(surfaces, nodes, edges, portals, anchors, stats);
    }

    private static void TraceBuild(string? tracePath, string message)
    {
        if (string.IsNullOrWhiteSpace(tracePath))
        {
            return;
        }

        try
        {
            File.AppendAllText(tracePath, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must not affect asset generation.
        }
    }

    private static List<BotNavigationValidationIssueAssetEntry> ValidateSpawnObjectiveReachability(BotNavigationAsset asset)
    {
        var issues = new List<BotNavigationValidationIssueAssetEntry>();
        var graph = ToGraph(asset);
        foreach (var spawn in asset.Anchors.Where(static anchor => anchor.Kind == "Spawn" && anchor.NodeIndex.HasValue))
        {
            var startNode = graph.FindNearestTraversalStartNode(spawn.X, spawn.Y);
            if (startNode < 0)
            {
                issues.Add(new BotNavigationValidationIssueAssetEntry
                {
                    Kind = "SpawnStartMissing",
                    Message = "Spawn anchor could not attach to a traversal start node.",
                    Team = spawn.Team,
                    FromNode = spawn.NodeIndex,
                    X = spawn.X,
                    Y = spawn.Y,
                });
                continue;
            }

            var validationTeam = spawn.Team ?? PlayerTeam.Red;
            var reachableByProfile = EnumerateValidationClasses()
                .Select(playerClass => BuildReachableSet(graph, startNode, playerClass, validationTeam))
                .ToArray();
            for (var objectiveIndex = 0; objectiveIndex < asset.Anchors.Count; objectiveIndex += 1)
            {
                var objective = asset.Anchors[objectiveIndex];
                if (!IsScoreObjectiveAnchor(objective.Kind) || !objective.NodeIndex.HasValue)
                {
                    continue;
                }

                var objectiveNodes = GetObjectiveValidationNodes(asset, objectiveIndex, objective);
                if (reachableByProfile.Any(reachable => objectiveNodes.Any(objectiveNode => objectiveNode >= 0 && objectiveNode < reachable.Length && reachable[objectiveNode])))
                {
                    continue;
                }

                issues.Add(new BotNavigationValidationIssueAssetEntry
                {
                    Kind = "SpawnObjectiveNoPath",
                    Message = $"Spawn component cannot reach objective anchor '{objective.Kind}'.",
                    Team = spawn.Team,
                    FromNode = startNode,
                    ToNode = objective.NodeIndex,
                    X = objective.X,
                    Y = objective.Y,
                });
            }
        }

        issues.AddRange(ValidatePortalSpawnObjectiveReachability(asset));
        return issues;
    }

    private static int[] GetObjectiveValidationNodes(BotNavigationAsset asset, int objectiveIndex, BotNavigationAnchorAssetEntry objective)
    {
        var standingNodes = asset.Portals
            .Where(portal => portal.AnchorIndex == objectiveIndex
                && string.Equals(portal.Kind, BuildPortalKind.ObjectiveStanding.ToString(), StringComparison.Ordinal)
                && portal.NodeIndex >= 0
                && portal.NodeIndex < asset.Nodes.Count)
            .Select(static portal => portal.NodeIndex)
            .Distinct()
            .ToArray();
        if (standingNodes.Length > 0)
        {
            return standingNodes;
        }

        return objective.NodeIndex.HasValue ? [objective.NodeIndex.Value] : [];
    }

    private static List<BotNavigationValidationIssueAssetEntry> ValidatePortalSpawnObjectiveReachability(BotNavigationAsset asset)
    {
        var issues = new List<BotNavigationValidationIssueAssetEntry>();
        if (asset.Portals.Count == 0)
        {
            issues.Add(new BotNavigationValidationIssueAssetEntry
            {
                Kind = "PortalGraphMissing",
                Message = "Navigation asset has no class/team-agnostic portals.",
            });
            return issues;
        }

        var portalsByAnchor = asset.Portals
            .Where(static portal => portal.AnchorIndex.HasValue)
            .GroupBy(static portal => portal.AnchorIndex!.Value)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var executableGraph = ToGraph(asset);
        var adjacency = BuildPortalValidationAdjacency(asset);
        for (var anchorIndex = 0; anchorIndex < asset.Anchors.Count; anchorIndex += 1)
        {
            var spawn = asset.Anchors[anchorIndex];
            if (spawn.Kind != "Spawn" || !portalsByAnchor.TryGetValue(anchorIndex, out var spawnPortals))
            {
                continue;
            }

            for (var objectiveIndex = 0; objectiveIndex < asset.Anchors.Count; objectiveIndex += 1)
            {
                var objective = asset.Anchors[objectiveIndex];
                if (!IsScoreObjectiveAnchor(objective.Kind)
                    || !portalsByAnchor.TryGetValue(objectiveIndex, out var objectivePortals))
                {
                    continue;
                }

                var validationTeam = spawn.Team ?? PlayerTeam.Red;
                var reachableByAnyProfile = EnumerateValidationClasses()
                    .Any(playerClass => AnyPortalReachable(adjacency, spawnPortals, objectivePortals, playerClass, validationTeam));
                if (reachableByAnyProfile)
                {
                    continue;
                }

                if (spawn.NodeIndex.HasValue
                    && objective.NodeIndex.HasValue
                    && EnumerateValidationClasses().Any(playerClass =>
                    {
                        var reachable = BuildReachableSet(executableGraph, spawn.NodeIndex.Value, playerClass, validationTeam);
                        return objective.NodeIndex.Value >= 0
                            && objective.NodeIndex.Value < reachable.Length
                            && reachable[objective.NodeIndex.Value];
                    }))
                {
                    continue;
                }

                issues.Add(new BotNavigationValidationIssueAssetEntry
                {
                    Kind = "PortalSpawnObjectiveNoPath",
                    Message = $"Portal graph cannot route spawn to objective anchor '{objective.Kind}'.",
                    Team = spawn.Team,
                    FromNode = spawn.NodeIndex,
                    ToNode = objective.NodeIndex,
                    X = objective.X,
                    Y = objective.Y,
                });
            }
        }

        return issues;
    }

    private static List<PortalValidationEdge>[] BuildPortalValidationAdjacency(BotNavigationAsset asset)
    {
        var adjacency = new List<PortalValidationEdge>[asset.Portals.Count];
        for (var i = 0; i < adjacency.Length; i += 1)
        {
            adjacency[i] = [];
        }

        foreach (var edge in asset.Edges)
        {
            if (!edge.FromPortalId.HasValue
                || !edge.ToPortalId.HasValue
                || edge.FromPortalId.Value < 0
                || edge.FromPortalId.Value >= adjacency.Length
                || edge.ToPortalId.Value < 0
                || edge.ToPortalId.Value >= adjacency.Length)
            {
                continue;
            }

            adjacency[edge.FromPortalId.Value].Add(new PortalValidationEdge(edge.ToPortalId.Value, edge.SupportedClassMask, edge.SupportedTeamMask));
        }

        foreach (var group in asset.Portals
                     .Where(static portal => portal.SurfaceId.HasValue)
                     .GroupBy(static portal => portal.SurfaceId!.Value))
        {
            var ordered = group.OrderBy(static portal => portal.X).ThenBy(static portal => portal.Id).ToArray();
            for (var i = 0; i + 1 < ordered.Length; i += 1)
            {
                adjacency[ordered[i].Id].Add(new PortalValidationEdge(ordered[i + 1].Id, BotBrainClassMask.All, BotBrainTeamMask.All));
                adjacency[ordered[i + 1].Id].Add(new PortalValidationEdge(ordered[i].Id, BotBrainClassMask.All, BotBrainTeamMask.All));
            }
        }

        return adjacency;
    }

    private static bool AnyPortalReachable(
        IReadOnlyList<PortalValidationEdge>[] adjacency,
        IReadOnlyList<BotNavigationPortalAssetEntry> starts,
        IReadOnlyList<BotNavigationPortalAssetEntry> goals,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        var goalIds = goals.Select(static portal => portal.Id).ToHashSet();
        var visited = new bool[adjacency.Length];
        var queue = new Queue<int>();
        foreach (var start in starts)
        {
            if (start.Id < 0 || start.Id >= adjacency.Length || visited[start.Id])
            {
                continue;
            }

            visited[start.Id] = true;
            queue.Enqueue(start.Id);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (goalIds.Contains(current))
            {
                return true;
            }

            foreach (var edge in adjacency[current])
            {
                if (!BotBrainClassMask.Contains(edge.SupportedClassMask, playerClass)
                    || !BotBrainTeamMask.Contains(edge.SupportedTeamMask, team)
                    || visited[edge.ToPortalId])
                {
                    continue;
                }

                visited[edge.ToPortalId] = true;
                queue.Enqueue(edge.ToPortalId);
            }
        }

        return false;
    }

    private static bool[] BuildReachableSet(NavGraph graph, int startNode, PlayerClass playerClass, PlayerTeam team)
    {
        var visited = new bool[graph.NodeCount];
        if (startNode < 0 || startNode >= graph.NodeCount)
        {
            return visited;
        }

        var queue = new Queue<int>();
        visited[startNode] = true;
        queue.Enqueue(startNode);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var edge in graph.GetEdges(node))
            {
                if (!edge.Supports(playerClass, team) || visited[edge.ToNode])
                {
                    continue;
                }

                visited[edge.ToNode] = true;
                queue.Enqueue(edge.ToNode);
            }
        }

        return visited;
    }

    private static bool IsScoreObjectiveAnchor(string kind)
    {
        return kind is "IntelBase"
            or "ArenaControlPoint"
            or "ControlPoint"
            or "CaptureZone"
            or "Generator";
    }

    private static IEnumerable<PlayerClass> EnumerateValidationClasses()
    {
        yield return PlayerClass.Heavy;
        yield return PlayerClass.Soldier;
        yield return PlayerClass.Scout;
    }

    private static List<BuildSurface> BuildSurfaces(SimpleLevel level)
    {
        var rawSurfaces = BuildExposedSolidSurfaces(level.Solids)
            .Concat(BuildDropdownSurfaces(level.RoomObjects))
            .OrderBy(static surface => surface.TopY)
            .ThenBy(static surface => surface.LeftX)
            .ToArray();
        if (rawSurfaces.Length == 0)
        {
            return [];
        }

        var merged = new List<SurfaceInterval>();
        foreach (var raw in rawSurfaces)
        {
            if (merged.Count == 0)
            {
                merged.Add(raw);
                continue;
            }

            var previous = merged[^1];
            var sameHeight = MathF.Abs(previous.TopY - raw.TopY) <= SurfaceMergeVerticalTolerance;
            var sameKind = previous.IsDropdown == raw.IsDropdown;
            var touches = raw.LeftX <= previous.RightX + SurfaceMergeGapTolerance;
            if (sameHeight && sameKind && touches)
            {
                merged[^1] = previous with { RightX = MathF.Max(previous.RightX, raw.RightX) };
                continue;
            }

            merged.Add(raw);
        }

        var surfaces = new List<BuildSurface>();
        foreach (var surface in merged)
        {
            var rawWidth = surface.RightX - surface.LeftX;
            if (rawWidth < MinimumSurfaceWidth)
            {
                continue;
            }

            var canInset = rawWidth >= MinimumInsetSurfaceWidth;
            var left = canInset ? surface.LeftX + ProbeHalfWidth : (surface.LeftX + surface.RightX) * 0.5f;
            var right = canInset ? surface.RightX - ProbeHalfWidth : left;
            if (!HasUsableSurfaceClearance(level, left, right, surface.TopY))
            {
                continue;
            }

            surfaces.Add(new BuildSurface(
                surfaces.Count,
                left,
                right,
                surface.TopY,
                surface.IsDropdown,
                FirstNodeIndex: -1,
                LastNodeIndex: -1));
        }

        return surfaces;
    }

    private static bool HasUsableSurfaceClearance(SimpleLevel level, float left, float right, float topY)
    {
        var nodeY = SurfaceNodeY(topY);
        if (!HasBlockingSolidAt(level, left, nodeY))
        {
            return true;
        }

        if (MathF.Abs(right - left) <= 0.5f)
        {
            return false;
        }

        if (!HasBlockingSolidAt(level, right, nodeY))
        {
            return true;
        }

        var center = (left + right) * 0.5f;
        return !HasBlockingSolidAt(level, center, nodeY);
    }

    private static IEnumerable<SurfaceInterval> BuildExposedSolidSurfaces(IReadOnlyList<LevelSolid> solids)
    {
        for (var index = 0; index < solids.Count; index += 1)
        {
            var solid = solids[index];
            if (solid.Width < MinimumSurfaceWidth || solid.Height <= 0f)
            {
                continue;
            }

            var exposedSegments = new List<SurfaceInterval>
            {
                new(solid.Left, solid.Right, solid.Top, IsDropdown: false),
            };
            for (var blockerIndex = 0; blockerIndex < solids.Count && exposedSegments.Count > 0; blockerIndex += 1)
            {
                if (blockerIndex == index)
                {
                    continue;
                }

                var blocker = solids[blockerIndex];
                if (blocker.Width <= 0f
                    || blocker.Height <= 0f
                    || MathF.Abs(blocker.Bottom - solid.Top) > SurfaceMergeVerticalTolerance
                    || blocker.Right <= solid.Left
                    || blocker.Left >= solid.Right)
                {
                    continue;
                }

                SubtractCoveredInterval(exposedSegments, blocker.Left, blocker.Right);
            }

            foreach (var segment in exposedSegments)
            {
                if (segment.RightX - segment.LeftX >= MinimumSurfaceWidth)
                {
                    yield return segment;
                }
            }
        }
    }

    private static IEnumerable<SurfaceInterval> BuildDropdownSurfaces(IReadOnlyList<RoomObjectMarker> roomObjects)
    {
        foreach (var roomObject in roomObjects)
        {
            if (roomObject.Type == RoomObjectType.DropdownPlatform && roomObject.Width >= MinimumSurfaceWidth)
            {
                yield return new SurfaceInterval(roomObject.Left, roomObject.Right, roomObject.Top, IsDropdown: true);
            }
        }
    }

    private static void SubtractCoveredInterval(List<SurfaceInterval> segments, float coveredLeft, float coveredRight)
    {
        for (var index = segments.Count - 1; index >= 0; index -= 1)
        {
            var segment = segments[index];
            var overlapLeft = MathF.Max(segment.LeftX, coveredLeft);
            var overlapRight = MathF.Min(segment.RightX, coveredRight);
            if (overlapRight <= overlapLeft)
            {
                continue;
            }

            segments.RemoveAt(index);
            if (segment.LeftX < overlapLeft)
            {
                segments.Insert(index, segment with { RightX = overlapLeft });
                index += 1;
            }

            if (overlapRight < segment.RightX)
            {
                segments.Insert(index, segment with { LeftX = overlapRight });
            }
        }
    }

    private static void AddSurfaceNodes(List<BuildNode> nodes, BuildSurface surface)
    {
        if (MathF.Abs(surface.RightX - surface.LeftX) <= 0.5f)
        {
            nodes.Add(new BuildNode(surface.LeftX, SurfaceNodeY(surface.TopY), NavNodeKind.Surface, surface.Id));
            return;
        }

        nodes.Add(new BuildNode(surface.LeftX, SurfaceNodeY(surface.TopY), NavNodeKind.Ledge, surface.Id));

        var surfaceWidth = surface.RightX - surface.LeftX;
        if (surfaceWidth > SurfaceWaypointSpacing * 1.5f)
        {
            var interiorCount = Math.Max(2, (int)MathF.Ceiling(surfaceWidth / SurfaceWaypointSpacing));
            for (var i = 1; i < interiorCount; i += 1)
            {
                var x = surface.LeftX + (surfaceWidth * i / interiorCount);
                nodes.Add(new BuildNode(x, SurfaceNodeY(surface.TopY), NavNodeKind.Surface, surface.Id));
            }
        }

        nodes.Add(new BuildNode(surface.RightX, SurfaceNodeY(surface.TopY), NavNodeKind.Ledge, surface.Id));
    }

    private static List<BotNavigationAnchorAssetEntry> AddAnchorNodes(SimpleLevel level, List<BuildNode> nodes)
    {
        var anchors = new List<BotNavigationAnchorAssetEntry>();
        foreach (var spawn in level.RedSpawns)
        {
            AddAnchor(nodes, anchors, "Spawn", spawn.X, spawn.Y, PlayerTeam.Red, NavNodeKind.Spawn);
        }

        foreach (var spawn in level.BlueSpawns)
        {
            AddAnchor(nodes, anchors, "Spawn", spawn.X, spawn.Y, PlayerTeam.Blue, NavNodeKind.Spawn);
        }

        foreach (var intelBase in level.IntelBases)
        {
            AddAnchor(nodes, anchors, "IntelBase", intelBase.X, intelBase.Y, intelBase.Team, NavNodeKind.Objective);
        }

        foreach (var roomObject in level.RoomObjects)
        {
            if (roomObject.Type is RoomObjectType.ArenaControlPoint
                or RoomObjectType.ControlPoint
                or RoomObjectType.CaptureZone
                or RoomObjectType.Generator
                or RoomObjectType.HealingCabinet)
            {
                AddAnchor(nodes, anchors, roomObject.Type.ToString(), roomObject.CenterX, roomObject.CenterY, roomObject.Team, NavNodeKind.Objective);
            }
        }

        return anchors;
    }

    private static void AddAnchor(
        List<BuildNode> nodes,
        List<BotNavigationAnchorAssetEntry> anchors,
        string kind,
        float x,
        float y,
        PlayerTeam? team,
        NavNodeKind nodeKind)
    {
        var nodeIndex = nodes.Count;
        nodes.Add(new BuildNode(x, y, nodeKind, SurfaceId: null));
        anchors.Add(new BotNavigationAnchorAssetEntry
        {
            Kind = kind,
            X = x,
            Y = y,
            Team = team,
            NodeIndex = nodeIndex,
        });
    }

    private static void AddSurfaceWalkEdges(
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildNode> nodes,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet)
    {
        foreach (var surface in surfaces)
        {
            for (var i = surface.FirstNodeIndex; i < surface.LastNodeIndex; i += 1)
            {
                AddBidirectionalEdge(i, i + 1, NavEdgeKind.Walk, Distance(nodes[i], nodes[i + 1]), edges, edgeSet);
            }
        }
    }

    private static List<BuildPortal> BuildPortals(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildNode> nodes,
        List<BotNavigationAnchorAssetEntry> anchors)
    {
        var portals = new List<BuildPortal>();
        var portalKeys = new HashSet<PortalKey>();

        for (var surfaceIndex = 0; surfaceIndex < surfaces.Count; surfaceIndex += 1)
        {
            var surface = surfaces[surfaceIndex];
            AddPortal(portals, portalKeys, BuildPortalKind.Ledge, surface.FirstNodeIndex, nodes, AnchorIndex: null);
            if (surface.LastNodeIndex != surface.FirstNodeIndex)
            {
                AddPortal(portals, portalKeys, BuildPortalKind.Ledge, surface.LastNodeIndex, nodes, AnchorIndex: null);
            }

            for (var nodeIndex = surface.FirstNodeIndex + 1; nodeIndex < surface.LastNodeIndex; nodeIndex += 1)
            {
                AddPortal(portals, portalKeys, BuildPortalKind.SurfaceBreakpoint, nodeIndex, nodes, AnchorIndex: null);
            }

            if (IsStepRelaySurface(surfaces, surface))
            {
                var centerNode = FindNearestNodeOnSurface(nodes, surface.Id, (surface.LeftX + surface.RightX) * 0.5f);
                AddPortal(portals, portalKeys, BuildPortalKind.StepRelay, centerNode, nodes, AnchorIndex: null);
            }
        }

        foreach (var solid in level.Solids)
        {
            AddInteriorCornerPortal(surfaces, nodes, portals, portalKeys, solid.Left, solid.Top);
            AddInteriorCornerPortal(surfaces, nodes, portals, portalKeys, solid.Right, solid.Top);
        }

        for (var anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex += 1)
        {
            var anchor = anchors[anchorIndex];
            var anchorNode = anchor.NodeIndex ?? -1;
            if (anchorNode >= 0)
            {
                AddPortal(portals, portalKeys, BuildPortalKind.Anchor, anchorNode, nodes, anchorIndex);
            }

            var standingNode = FindBestAnchorStandingNode(nodes, anchor.X, anchor.Y);
            if (standingNode >= 0)
            {
                AddPortal(portals, portalKeys, BuildPortalKind.ObjectiveStanding, standingNode, nodes, anchorIndex);
            }
        }

        return portals;
    }

    private static void AddInteriorCornerPortal(
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildNode> nodes,
        List<BuildPortal> portals,
        HashSet<PortalKey> portalKeys,
        float x,
        float y)
    {
        if (!TryFindNearestSurface(surfaces, x, y, InteriorCornerSurfaceTolerance, InteriorCornerSurfaceTolerance, out var surface)
            || x <= surface.LeftX + (MinimumPortalSpacing * 0.5f)
            || x >= surface.RightX - (MinimumPortalSpacing * 0.5f))
        {
            return;
        }

        var leftNode = FindNearestNodeOnSurface(nodes, surface.Id, x - MinimumPortalSpacing * 0.5f);
        var rightNode = FindNearestNodeOnSurface(nodes, surface.Id, x + MinimumPortalSpacing * 0.5f);
        AddPortal(portals, portalKeys, BuildPortalKind.InteriorCorner, leftNode, nodes, AnchorIndex: null);
        AddPortal(portals, portalKeys, BuildPortalKind.InteriorCorner, rightNode, nodes, AnchorIndex: null);
    }

    private static void AddPortal(
        List<BuildPortal> portals,
        HashSet<PortalKey> portalKeys,
        BuildPortalKind kind,
        int nodeIndex,
        IReadOnlyList<BuildNode> nodes,
        int? AnchorIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= nodes.Count)
        {
            return;
        }

        var node = nodes[nodeIndex];
        var spacingBucket = (int)MathF.Round(node.X / MinimumPortalSpacing);
        var key = new PortalKey(kind, node.SurfaceId, nodeIndex, spacingBucket, AnchorIndex);
        if (!portalKeys.Add(key))
        {
            return;
        }

        portals.Add(new BuildPortal(
            portals.Count,
            kind,
            nodeIndex,
            node.SurfaceId,
            AnchorIndex,
            node.X,
            node.Y));
    }

    private static int FindBestAnchorStandingNode(IReadOnlyList<BuildNode> nodes, float x, float y)
    {
        var bestNode = -1;
        var bestScore = float.MaxValue;
        for (var i = 0; i < nodes.Count; i += 1)
        {
            var node = nodes[i];
            if (!node.SurfaceId.HasValue)
            {
                continue;
            }

            var dx = MathF.Abs(node.X - x);
            var dy = MathF.Abs(node.Y - y);
            if (dx > AnchorPortalHorizontalRange || dy > AnchorPortalVerticalRange)
            {
                continue;
            }

            var score = dy + dx * 0.5f + (node.Kind == NavNodeKind.Ledge ? 64f : 0f);
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestNode = i;
        }

        return bestNode;
    }

    private static bool IsStepRelaySurface(IReadOnlyList<BuildSurface> surfaces, BuildSurface surface)
    {
        var hasNeighbor = false;
        for (var i = 0; i < surfaces.Count; i += 1)
        {
            var candidate = surfaces[i];
            if (candidate.Id == surface.Id
                || MathF.Abs(candidate.TopY - surface.TopY) > StepRelayVerticalReach
                || candidate.RightX < surface.LeftX - StepRelayHorizontalReach
                || candidate.LeftX > surface.RightX + StepRelayHorizontalReach)
            {
                continue;
            }

            hasNeighbor = true;
            break;
        }

        return hasNeighbor && surface.RightX - surface.LeftX <= StepRelayHorizontalReach * 1.5f;
    }

    private static IEnumerable<PortalTraversalCandidate> EnumeratePortalTraversalCandidates(
        IReadOnlyList<BuildPortal> portals,
        IReadOnlyList<BuildNode> nodes,
        int maxPortalCandidatesPerKindDirection,
        bool includeCtfVerticalBandRescue)
    {
        var surfacePortals = portals
            .Where(static portal => portal.SurfaceId.HasValue
                && portal.Kind is not BuildPortalKind.Anchor)
            .OrderBy(static portal => portal.X)
            .ToArray();

        for (var i = 0; i < surfacePortals.Length; i += 1)
        {
            var source = surfacePortals[i];
            var sourceNode = nodes[source.NodeIndex];
            var candidates = new List<ScoredPortalTraversalCandidate>();
            var minX = sourceNode.X - MaxJumpReach;
            var maxX = sourceNode.X + MaxJumpReach;
            var startIndex = FindFirstPortalAtOrAfterX(surfacePortals, minX);
            for (var j = startIndex; j < surfacePortals.Length; j += 1)
            {
                if (i == j)
                {
                    continue;
                }

                var target = surfacePortals[j];
                if (target.X > maxX)
                {
                    break;
                }

                if (source.SurfaceId == target.SurfaceId)
                {
                    continue;
                }

                var targetNode = nodes[target.NodeIndex];
                var dx = MathF.Abs(targetNode.X - sourceNode.X);
                var dy = targetNode.Y - sourceNode.Y;
                var dist = Distance(sourceNode, targetNode);
                if (dist > MaxJumpReach && MathF.Abs(dy) > MaxFallDistance)
                {
                    continue;
                }

                if (dx > MaxJumpReach && MathF.Abs(dy) <= MaxFallDistance)
                {
                    continue;
                }

                var kind = dy < -StepDownTolerance
                    ? NavEdgeKind.Jump
                    : dy > StepDownTolerance
                        ? NavEdgeKind.Fall
                        : NavEdgeKind.Walk;
                var direction = Math.Sign(targetNode.X - sourceNode.X);
                var score = dist
                    + MathF.Abs(dy) * 0.35f
                    + PortalKindPenalty(source.Kind)
                    + PortalKindPenalty(target.Kind);
                candidates.Add(new ScoredPortalTraversalCandidate(source, target, kind, direction, score));
            }

            foreach (var candidate in SelectPortalTraversalCandidates(
                         candidates,
                         maxPortalCandidatesPerKindDirection,
                         includeCtfVerticalBandRescue))
            {
                yield return new PortalTraversalCandidate(
                    candidate.FromPortal,
                    candidate.ToPortal,
                    candidate.Kind,
                    candidate.Direction,
                    candidate.Score);
            }
        }
    }


    private static IEnumerable<ScoredPortalTraversalCandidate> SelectPortalTraversalCandidates(
        IEnumerable<ScoredPortalTraversalCandidate> candidates,
        int maxPortalCandidatesPerKindDirection,
        bool includeCtfVerticalBandRescue)
    {
        foreach (var group in candidates.GroupBy(static candidate => (candidate.Kind, candidate.Direction)))
        {
            var ordered = group
                .OrderBy(static candidate => candidate.Score)
                .ToArray();
            var yieldedPortalIds = new HashSet<int>();
            foreach (var candidate in ordered.Take(maxPortalCandidatesPerKindDirection))
            {
                yieldedPortalIds.Add(candidate.ToPortal.Id);
                yield return candidate;
            }

            if (!includeCtfVerticalBandRescue || group.Key.Kind != NavEdgeKind.Jump || group.Key.Direction == 0)
            {
                continue;
            }

            foreach (var candidate in ordered
                         .Where(static candidate => IsCtfVerticalBandRescueCandidate(candidate))
                         .OrderBy(static candidate => CtfVerticalBandRescueScore(candidate))
                         .Take(CtfVerticalBandPortalCandidatesPerKindDirection))
            {
                if (!yieldedPortalIds.Add(candidate.ToPortal.Id))
                {
                    continue;
                }

                yield return candidate;
            }
        }
    }

    private static bool IsCtfVerticalBandRescueCandidate(ScoredPortalTraversalCandidate candidate)
    {
        var ascent = candidate.FromPortal.Y - candidate.ToPortal.Y;
        return ascent >= CtfVerticalBandMinAscent
            && ascent <= CtfVerticalBandMaxAscent;
    }

    private static float CtfVerticalBandRescueScore(ScoredPortalTraversalCandidate candidate)
    {
        var dx = candidate.ToPortal.X - candidate.FromPortal.X;
        var dy = candidate.ToPortal.Y - candidate.FromPortal.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy))
            + PortalKindPenalty(candidate.ToPortal.Kind);
    }

    private static int FindFirstPortalAtOrAfterX(IReadOnlyList<BuildPortal> portals, float minX)
    {
        var low = 0;
        var high = portals.Count;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (portals[mid].X < minX)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private static float PortalKindPenalty(BuildPortalKind kind)
    {
        return kind switch
        {
            BuildPortalKind.Ledge => 0f,
            BuildPortalKind.StepRelay => 4f,
            BuildPortalKind.InteriorCorner => 8f,
            BuildPortalKind.SurfaceBreakpoint => 16f,
            _ => 64f,
        };
    }

    private static void AddTraversalEdges(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildPortal> portals,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats)
    {
        var pairSet = new HashSet<NodePairKey>();
        var portalCandidateLimit = GetPortalCandidateLimit(level.Mode, surfaces.Count);
        foreach (var candidate in EnumeratePortalTraversalCandidates(
                         portals,
                         nodes,
                         portalCandidateLimit,
                         includeCtfVerticalBandRescue: level.Mode == GameModeKind.CaptureTheFlag)
                     .OrderBy(static candidate => candidate.Score))
        {
            if (!pairSet.Add(new NodePairKey(candidate.FromPortal.NodeIndex, candidate.ToPortal.NodeIndex)))
            {
                continue;
            }

            var fromSurfaceId = nodes[candidate.FromPortal.NodeIndex].SurfaceId;
            var toSurfaceId = nodes[candidate.ToPortal.NodeIndex].SurfaceId;
            if (!fromSurfaceId.HasValue || !toSurfaceId.HasValue || fromSurfaceId.Value == toSurfaceId.Value)
            {
                continue;
            }

            var surfaceA = FindSurface(surfaces, fromSurfaceId);
            var surfaceB = FindSurface(surfaces, toSurfaceId);
            if (!surfaceA.HasValue || !surfaceB.HasValue)
            {
                continue;
            }

            stats.SurfacePairChecks += 1;
            stats.NodePairChecks += 1;
            AddTraversalEdgePair(
                level,
                surfaces,
                portals,
                probeSurfaces,
                nodes,
                candidate.FromPortal.NodeIndex,
                candidate.ToPortal.NodeIndex,
                surfaceA.Value,
                surfaceB.Value,
                candidate.FromPortal.Id,
                candidate.ToPortal.Id,
                edges,
                edgeSet,
                stats);
        }
    }

    private static int GetPortalCandidateLimit(GameModeKind mode, int surfaceCount)
    {
        return UsesCompactObjectiveTraversalBudget(mode, surfaceCount)
            ? CompactObjectiveMaxPortalCandidatesPerKindDirection
            : DefaultMaxPortalCandidatesPerKindDirection;
    }

    private static bool UsesCompactObjectiveTraversalBudget(GameModeKind mode, int surfaceCount)
    {
        return surfaceCount >= CompactObjectiveSurfaceCountThreshold
            && mode is (GameModeKind.Arena
            or GameModeKind.ControlPoint
            or GameModeKind.KingOfTheHill
            or GameModeKind.DoubleKingOfTheHill);
    }

    private static bool ShouldUseCtfMicroStepRepairs(SimpleLevel level)
    {
        return level.Mode == GameModeKind.CaptureTheFlag
            && level.Name is "Conflict" or "Eiger" or "Waterway" or "ClassicWell";
    }

    private static void AddOrangeSpawnExitBridgeEdges(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildPortal> portals,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats)
    {
        if (level.Mode != GameModeKind.CaptureTheFlag || level.Name != "Orange")
        {
            return;
        }

        TryAddOrangeSpawnExitBridge(778f, 1122f, 855f, 1158f);
        TryAddOrangeSpawnExitBridge(4022f, 1122f, 3945f, 1158f);
        TryAddOrangeSpawnExitBridge(206f, 1338f, 42f, 1212f);
        TryAddOrangeSpawnExitBridge(42f, 1212f, 21f, 1206f);
        TryAddOrangeSpawnExitBridge(21f, 1206f, 558f, 1458f, allowTeamSpecificFallback: true);
        TryAddOrangeSpawnExitBridge(2355f, 1296f, 2271f, 1206f);
        TryAddOrangeSpawnExitBridge(2271f, 1206f, 2882f, 1344f);
        TryAddOrangeSpawnExitBridge(4594f, 1338f, 4758f, 1212f);
        TryAddOrangeSpawnExitBridge(4758f, 1212f, 4779f, 1206f);
        TryAddOrangeSpawnExitBridge(4779f, 1206f, 4242f, 1458f, allowTeamSpecificFallback: true);
        TryAddOrangeSpawnExitBridge(2445f, 1296f, 2529f, 1206f);
        TryAddOrangeSpawnExitBridge(2529f, 1206f, 1918f, 1344f);

        void TryAddOrangeSpawnExitBridge(float fromX, float fromY, float toX, float toY, bool allowTeamSpecificFallback = false)
        {
            var fromNode = FindNearestNodeNear(nodes, fromX, fromY);
            var toNode = FindNearestNodeNear(nodes, toX, toY);
            if ((uint)fromNode >= (uint)nodes.Count || (uint)toNode >= (uint)nodes.Count)
            {
                return;
            }

            if (Distance(nodes[fromNode], new BuildNode(fromX, fromY, NavNodeKind.Surface, null)) > 2f
                || Distance(nodes[toNode], new BuildNode(toX, toY, NavNodeKind.Surface, null)) > 2f)
            {
                return;
            }

            var targetSurface = FindSurface(surfaces, nodes[toNode].SurfaceId);
            if (!targetSurface.HasValue)
            {
                return;
            }

            var fromPortalId = FindPortalForNode(portals, fromNode, BuildPortalKind.Ledge);
            var cost = GetJumpCost(nodes[fromNode], nodes[toNode], Distance(nodes[fromNode], nodes[toNode]));
            TryAddCertifiedTraversalEdge(
                level,
                probeSurfaces,
                nodes,
                fromNode,
                toNode,
                fromPortalId,
                FindPortalForNode(portals, toNode, BuildPortalKind.Ledge),
                targetSurface.Value,
                NavEdgeKind.Jump,
                cost,
                edges,
                edgeSet,
                stats,
                allowTeamSpecificFallback);
        }
    }

    private static int FindNearestNodeNear(IReadOnlyList<BuildNode> nodes, float x, float y)
    {
        var bestNode = -1;
        var bestDistance = float.MaxValue;
        var target = new BuildNode(x, y, NavNodeKind.Surface, null);
        for (var i = 0; i < nodes.Count; i += 1)
        {
            var distance = Distance(nodes[i], target);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestNode = i;
        }

        return bestNode;
    }

    private static void AddAuthoredCorridorEdges(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildPortal> portals,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats)
    {
        if (!BotNavigationAuthoredCorridorStore.TryLoad(level, out var source))
        {
            return;
        }

        foreach (var corridor in source.Corridors)
        {
            if (corridor.Waypoints.Count < 2)
            {
                continue;
            }

            var routeWaypoints = ResolveAuthoredCorridorRouteWaypoints(level, corridor);
            if (routeWaypoints.Count < 2)
            {
                continue;
            }

            var startTeam = ResolveCorridorStartTeam(level, corridor);
            var supportedTeamMask = ResolveAuthoredCorridorTeamMask(level, corridor, startTeam);
            var supportedClassMask = ResolveAuthoredCorridorClassMask(level, corridor, startTeam);
            var corridorCostMultiplier = ResolveAuthoredCorridorCostMultiplier(level, corridor, startTeam);
            var previousNode = SnapAuthoredCorridorWaypoint(nodes, routeWaypoints[0]);
            for (var i = 1; i < routeWaypoints.Count; i += 1)
            {
                var nextNode = SnapAuthoredCorridorWaypoint(nodes, routeWaypoints[i]);
                if ((uint)previousNode >= (uint)nodes.Count || (uint)nextNode >= (uint)nodes.Count)
                {
                    previousNode = nextNode;
                    continue;
                }

                if (previousNode == nextNode)
                {
                    continue;
                }

                if (nodes[previousNode].SurfaceId == nodes[nextNode].SurfaceId)
                {
                    previousNode = nextNode;
                    continue;
                }

                var distance = Distance(nodes[previousNode], nodes[nextNode]);
                if (IsAuthoredCorridorWalkRelayCandidate(nodes[previousNode], nodes[nextNode]))
                {
                    AddEdge(
                        previousNode,
                        nextNode,
                        NavEdgeKind.Walk,
                        GetAuthoredCorridorPreferredCost(distance, corridorCostMultiplier),
                        edges,
                        edgeSet,
                        supportedClassMask: supportedClassMask,
                        supportedTeamMask: supportedTeamMask,
                        preferLowerCost: true);
                    previousNode = nextNode;
                    continue;
                }

                if (IsGroundedStairRampRelayCandidate(level, surfaces, nodes[previousNode], nodes[nextNode]))
                {
                    var stairKind = ResolveStairRampRelayKind(surfaces, nodes[previousNode], nodes[nextNode]);
                    AddEdge(
                        previousNode,
                        nextNode,
                        stairKind,
                        MathF.Min(
                            GetAuthoredCorridorPreferredCost(distance, corridorCostMultiplier),
                            GetStairRampRelayCost(distance)),
                        edges,
                        edgeSet,
                        semanticCompletion: CreateStairRampCompletion(surfaces, nodes[previousNode], nodes[nextNode], stairKind),
                        supportedClassMask: supportedClassMask,
                        supportedTeamMask: supportedTeamMask,
                        preferLowerCost: true);
                    previousNode = nextNode;
                    continue;
                }

                if (distance <= AuthoredCorridorDirectEdgeMaxDistance)
                {
                    var directKind = ResolveAuthoredCorridorEdgeKind(nodes[previousNode], nodes[nextNode]);
                    var directCost = directKind == NavEdgeKind.Fall
                        ? distance * 0.8f
                        : GetJumpCost(nodes[previousNode], nodes[nextNode], distance);
                    var directTemplate = FindCertifiedTraversalTemplateEdge(
                        edges,
                        previousNode,
                        nextNode,
                        directKind,
                        supportedTeamMask,
                        supportedClassMask);
                    if (directKind != NavEdgeKind.Jump || directTemplate.HasValue)
                    {
                        AddEdge(
                            previousNode,
                            nextNode,
                            directKind,
                            GetAuthoredCorridorPreferredCost(directCost, corridorCostMultiplier),
                            edges,
                            edgeSet,
                            directTemplate?.ProbeResult,
                            supportedClassMask: ResolveAuthoredCorridorDirectClassMask(directKind, supportedClassMask, directTemplate),
                            supportedTeamMask: supportedTeamMask,
                            preferLowerCost: true);
                        previousNode = nextNode;
                        continue;
                    }
                }

                var targetSurface = FindSurface(surfaces, nodes[nextNode].SurfaceId);
                if (!targetSurface.HasValue)
                {
                    previousNode = nextNode;
                    continue;
                }

                var kind = ResolveAuthoredCorridorEdgeKind(nodes[previousNode], nodes[nextNode]);
                var cost = kind == NavEdgeKind.Fall
                    ? distance * 0.8f
                    : GetJumpCost(nodes[previousNode], nodes[nextNode], distance);
                TryAddCertifiedTraversalEdge(
                    level,
                    probeSurfaces,
                    nodes,
                    previousNode,
                    nextNode,
                    FindPortalForNode(portals, previousNode, BuildPortalKind.Ledge),
                    FindPortalForNode(portals, nextNode, BuildPortalKind.Ledge),
                    targetSurface.Value,
                    kind,
                    cost,
                    edges,
                    edgeSet,
                    stats,
                    allowTeamSpecificFallback: true,
                    useProbeEnsemble: true);

                var template = FindCertifiedTraversalTemplateEdge(
                    edges,
                    previousNode,
                    nextNode,
                    kind,
                    supportedTeamMask,
                    supportedClassMask);
                if (kind != NavEdgeKind.Jump || template.HasValue)
                {
                    AddEdge(
                        previousNode,
                        nextNode,
                        kind,
                        GetAuthoredCorridorPreferredCost(cost, corridorCostMultiplier),
                        edges,
                        edgeSet,
                        template?.ProbeResult,
                        supportedClassMask: ResolveAuthoredCorridorDirectClassMask(kind, supportedClassMask, template),
                        supportedTeamMask: supportedTeamMask,
                        fromPortalId: FindPortalForNode(portals, previousNode, BuildPortalKind.Ledge),
                        toPortalId: FindPortalForNode(portals, nextNode, BuildPortalKind.Ledge),
                        preferLowerCost: true);
                }

                previousNode = nextNode;
            }
        }
    }

    private static BotBrainMovementProbeResult? FindCertifiedTraversalTemplate(
        IReadOnlyList<BuildEdge> edges,
        int fromNode,
        int toNode,
        NavEdgeKind kind,
        int supportedTeamMask,
        int supportedClassMask)
    {
        return FindCertifiedTraversalTemplateEdge(edges, fromNode, toNode, kind, supportedTeamMask, supportedClassMask)?.ProbeResult;
    }

    private static BuildEdge? FindCertifiedTraversalTemplateEdge(
        IReadOnlyList<BuildEdge> edges,
        int fromNode,
        int toNode,
        NavEdgeKind kind,
        int supportedTeamMask,
        int supportedClassMask)
    {
        BuildEdge? bestEdge = null;
        var bestScore = int.MinValue;
        for (var i = 0; i < edges.Count; i += 1)
        {
            var edge = edges[i];
            if (edge.FromNode != fromNode
                || edge.ToNode != toNode
                || edge.Kind != kind
                || edge.ProbeResult is null
                || !MasksOverlapOrAll(edge.SupportedTeamMask, supportedTeamMask)
                || !MasksOverlapOrAll(edge.SupportedClassMask, supportedClassMask))
            {
                continue;
            }

            var score = 0;
            if (edge.SupportedTeamMask == supportedTeamMask)
            {
                score += 2;
            }
            else if (edge.SupportedTeamMask == BotBrainTeamMask.All)
            {
                score += 1;
            }

            if (edge.SupportedClassMask == supportedClassMask)
            {
                score += 2;
            }
            else if (edge.SupportedClassMask == BotBrainClassMask.All)
            {
                score += 1;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestEdge = edge;
            }
        }

        return bestEdge;
    }

    private static int ResolveAuthoredCorridorDirectClassMask(
        NavEdgeKind kind,
        int requestedClassMask,
        BuildEdge? certifiedTemplate) =>
        kind == NavEdgeKind.Jump && certifiedTemplate.HasValue
            ? certifiedTemplate.Value.SupportedClassMask
            : requestedClassMask;

    private static bool MasksOverlapOrAll(int first, int second) =>
        first == BotBrainClassMask.All
        || second == BotBrainClassMask.All
        || (first & second) != 0;

    private static IReadOnlyList<BotNavigationAuthoredCorridorWaypoint> ResolveAuthoredCorridorRouteWaypoints(
        SimpleLevel level,
        BotNavigationAuthoredCorridorEntry corridor)
    {
        if (level.Mode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
            || !TryFindNearestControlObjective(level, corridor.Waypoints[0].X, corridor.Waypoints[0].Y, out var objective))
        {
            return corridor.Waypoints;
        }

        for (var i = 1; i < corridor.Waypoints.Count; i += 1)
        {
            var waypoint = corridor.Waypoints[i];
            if (DistanceSquared(waypoint.X, waypoint.Y, objective.CenterX, objective.CenterY)
                <= AuthoredCorridorObjectiveArrivalDistance * AuthoredCorridorObjectiveArrivalDistance)
            {
                return corridor.Waypoints.Take(i + 1).ToArray();
            }
        }

        return corridor.Waypoints;
    }

    private static void AddAuthoredCorridorVirtualNodes(
        SimpleLevel level,
        List<BuildSurface> surfaces,
        List<BuildNode> nodes)
    {
        if (!BotNavigationAuthoredCorridorStore.TryLoad(level, out var source))
        {
            return;
        }

        foreach (var corridor in source.Corridors)
        {
            foreach (var waypoint in corridor.Waypoints)
            {
                if (!waypoint.IsGrounded
                    || FindNearestAuthoredCorridorNode(nodes, waypoint, AuthoredCorridorVirtualNodeSpacing) >= 0)
                {
                    continue;
                }

                var nodeIndex = nodes.Count;
                var surfaceId = surfaces.Count;
                surfaces.Add(new BuildSurface(
                    surfaceId,
                    waypoint.X,
                    waypoint.X,
                    waypoint.Y + (ProbeHeight * 0.5f),
                    IsDropdown: false,
                    FirstNodeIndex: nodeIndex,
                    LastNodeIndex: nodeIndex));
                nodes.Add(new BuildNode(waypoint.X, waypoint.Y, NavNodeKind.Surface, surfaceId));
            }
        }
    }

    private static int SnapAuthoredCorridorWaypoint(
        IReadOnlyList<BuildNode> nodes,
        BotNavigationAuthoredCorridorWaypoint waypoint)
    {
        var bestNode = -1;
        var bestScore = float.MaxValue;
        for (var i = 0; i < nodes.Count; i += 1)
        {
            if (nodes[i].SurfaceId is null)
            {
                continue;
            }

            var dx = nodes[i].X - waypoint.X;
            var dy = nodes[i].Y - waypoint.Y;
            var absDy = MathF.Abs(dy);
            if (MathF.Abs(dx) > 160f || absDy > 128f)
            {
                continue;
            }

            var distanceSq = (dx * dx) + (dy * dy);
            if (distanceSq > AuthoredCorridorMaxSnapDistance * AuthoredCorridorMaxSnapDistance)
            {
                continue;
            }

            var score = MathF.Abs(dx) + absDy * 2f;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestNode = i;
        }

        return bestNode;
    }

    private static int FindNearestAuthoredCorridorNode(
        IReadOnlyList<BuildNode> nodes,
        BotNavigationAuthoredCorridorWaypoint waypoint,
        float maxDistance)
    {
        var bestNode = -1;
        var bestDistanceSq = maxDistance * maxDistance;
        for (var i = 0; i < nodes.Count; i += 1)
        {
            if (nodes[i].SurfaceId is null)
            {
                continue;
            }

            var dx = nodes[i].X - waypoint.X;
            var dy = nodes[i].Y - waypoint.Y;
            var distanceSq = (dx * dx) + (dy * dy);
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestNode = i;
        }

        return bestNode;
    }

    private static bool IsAuthoredCorridorWalkRelayCandidate(BuildNode from, BuildNode to)
    {
        var dx = MathF.Abs(to.X - from.X);
        var dy = MathF.Abs(to.Y - from.Y);
        return dx <= StepRelayHorizontalReach
            && dy <= StepRelayVerticalReach;
    }

    private static float GetAuthoredCorridorPreferredCost(float baseCost, float costMultiplier) =>
        MathF.Max(1f, baseCost * costMultiplier);

    private static float GetStairRampRelayCost(float baseCost) =>
        MathF.Max(2f, baseCost * StairRampRelayCostMultiplier);

    private static NavEdgeKind ResolveStairRampRelayKind(
        IReadOnlyList<BuildSurface> surfaces,
        BuildNode from,
        BuildNode to)
    {
        var fromSurface = FindSurface(surfaces, from.SurfaceId);
        var toSurface = FindSurface(surfaces, to.SurfaceId);
        return fromSurface.HasValue && toSurface.HasValue
            ? ResolveStairRampRelayKind(fromSurface.Value, toSurface.Value, from, to)
            : ResolveStairRampRelayKind(from, to, forceJumpToNarrowTarget: false);
    }

    private static NavEdgeKind ResolveStairRampRelayKind(
        BuildSurface fromSurface,
        BuildSurface toSurface,
        BuildNode from,
        BuildNode to)
    {
        var targetWidth = toSurface.RightX - toSurface.LeftX;
        return ResolveStairRampRelayKind(
            from,
            to,
            forceJumpToNarrowTarget: targetWidth <= MinimumPortalSpacing);
    }

    private static NavEdgeKind ResolveStairRampRelayKind(BuildNode from, BuildNode to, bool forceJumpToNarrowTarget) =>
        to.Y < from.Y - StepDownTolerance
        && (from.Y - to.Y <= StairRampRelayJumpVerticalReach || forceJumpToNarrowTarget)
            ? NavEdgeKind.Jump
            : NavEdgeKind.Walk;

    private static BotBrainMovementProbeResult? CreateStairRampCompletion(
        IReadOnlyList<BuildSurface> surfaces,
        BuildNode from,
        BuildNode to,
        NavEdgeKind kind)
    {
        var targetSurface = FindSurface(surfaces, to.SurfaceId);
        return targetSurface.HasValue
            ? CreateStairRampCompletion(targetSurface.Value, from, to, kind)
            : null;
    }

    private static BotBrainMovementProbeResult CreateStairRampCompletion(
        BuildSurface targetSurface,
        BuildNode from,
        BuildNode to,
        NavEdgeKind kind)
    {
        var surfacePadding = kind == NavEdgeKind.Jump ? 32f : 20f;
        var verticalPadding = kind == NavEdgeKind.Jump ? 132f : 96f;
        var minX = MathF.Min(targetSurface.LeftX, to.X) - surfacePadding;
        var maxX = MathF.Max(targetSurface.RightX, to.X) + surfacePadding;
        var moveDirection = MathF.Sign(to.X - from.X);
        return new BotBrainMovementProbeResult(
            minX,
            maxX,
            to.Y - verticalPadding,
            to.Y + verticalPadding,
            [],
            Ticks: 0,
            JumpTriggerTick: 0,
            MoveDirectionX: moveDirection,
            VariantAttempts: 0,
            VariantSuccesses: 0,
            RequiresGroundedContinuation: false,
            LaunchRecipe: null);
    }

    private static int ResolveAuthoredCorridorTeamMask(SimpleLevel level, BotNavigationAuthoredCorridorEntry corridor, PlayerTeam startTeam)
    {
        if (level.Mode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
        {
            return BotBrainTeamMask.For(corridor.Team);
        }

        return BotBrainTeamMask.For(startTeam);
    }

    private static PlayerTeam ResolveCorridorStartTeam(SimpleLevel level, BotNavigationAuthoredCorridorEntry corridor)
    {
        if (corridor.Waypoints.Count == 0)
        {
            return corridor.Team;
        }

        var start = corridor.Waypoints[0];
        var redDistance = MinSpawnDistanceSquared(level.RedSpawns, start.X, start.Y);
        var blueDistance = MinSpawnDistanceSquared(level.BlueSpawns, start.X, start.Y);
        if (!float.IsFinite(redDistance) || !float.IsFinite(blueDistance) || MathF.Abs(redDistance - blueDistance) < 1f)
        {
            return corridor.Team;
        }

        return redDistance < blueDistance ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    private static float MinSpawnDistanceSquared(IReadOnlyList<SpawnPoint> spawns, float x, float y)
    {
        var best = float.PositiveInfinity;
        foreach (var spawn in spawns)
        {
            var dx = spawn.X - x;
            var dy = spawn.Y - y;
            var distanceSq = (dx * dx) + (dy * dy);
            if (distanceSq < best)
            {
                best = distanceSq;
            }
        }

        return best;
    }

    private static bool TryFindNearestControlObjective(SimpleLevel level, float x, float y, out RoomObjectMarker objective)
    {
        objective = default;
        var bestDistanceSq = float.PositiveInfinity;
        foreach (var marker in level.RoomObjects)
        {
            if (marker.Type is not (RoomObjectType.ControlPoint or RoomObjectType.ArenaControlPoint))
            {
                continue;
            }

            var distanceSq = DistanceSquared(x, y, marker.CenterX, marker.CenterY);
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            objective = marker;
        }

        return float.IsFinite(bestDistanceSq);
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return (dx * dx) + (dy * dy);
    }

    private static int ResolveAuthoredCorridorClassMask(
        SimpleLevel level,
        BotNavigationAuthoredCorridorEntry corridor,
        PlayerTeam startTeam) =>
        level.Mode == GameModeKind.CaptureTheFlag
        || corridor.PlayerClass == PlayerClass.Heavy
        || (level.Mode is GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && startTeam != corridor.Team)
            ? BotBrainClassMask.All
            : BotBrainClassMask.For(corridor.PlayerClass);

    private static float ResolveAuthoredCorridorCostMultiplier(
        SimpleLevel level,
        BotNavigationAuthoredCorridorEntry corridor,
        PlayerTeam startTeam) =>
        level.Mode == GameModeKind.CaptureTheFlag
        || corridor.PlayerClass == PlayerClass.Heavy
        || (level.Mode is GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && startTeam != corridor.Team)
            ? AuthoredCorridorPreferredCostMultiplier
            : 1f;

    private static NavEdgeKind ResolveAuthoredCorridorEdgeKind(BuildNode from, BuildNode to)
    {
        return to.Y > from.Y + StepDownTolerance
            ? NavEdgeKind.Fall
            : NavEdgeKind.Jump;
    }

    private static void AddStepRelayEdges(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildPortal> portals,
        IReadOnlyList<BuildNode> nodes,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet)
    {
        var surfaceById = surfaces.ToDictionary(static surface => surface.Id);
        var allowMicroStepRelay = ShouldUseCtfMicroStepRepairs(level);
        var stepPortals = portals
            .Where(static portal => portal.SurfaceId.HasValue && portal.Kind is BuildPortalKind.StepRelay or BuildPortalKind.Ledge or BuildPortalKind.SurfaceBreakpoint)
            .ToArray();
        for (var i = 0; i < stepPortals.Length; i += 1)
        {
            var portalA = stepPortals[i];
            for (var j = i + 1; j < stepPortals.Length; j += 1)
            {
                var portalB = stepPortals[j];
                if (portalA.SurfaceId == portalB.SurfaceId)
                {
                    continue;
                }

                var a = nodes[portalA.NodeIndex];
                var b = nodes[portalB.NodeIndex];
                var dx = MathF.Abs(a.X - b.X);
                var dy = MathF.Abs(a.Y - b.Y);
                var hasSurfaceA = surfaceById.TryGetValue(portalA.SurfaceId!.Value, out var surfaceA);
                var hasSurfaceB = surfaceById.TryGetValue(portalB.SurfaceId!.Value, out var surfaceB);
                var isMicroStepRelay = allowMicroStepRelay
                    && hasSurfaceA
                    && hasSurfaceB
                    && IsMicroStepRelay(surfaceA, surfaceB, a, b);
                if (hasSurfaceA
                    && hasSurfaceB
                    && IsGroundedStairRampRelayCandidate(level, surfaceA, surfaceB, a, b))
                {
                    AddBidirectionalPreferredStairRampEdges(
                        portalA.NodeIndex,
                        portalB.NodeIndex,
                        a,
                        b,
                        surfaceA,
                        surfaceB,
                        GetStairRampRelayCost(Distance(a, b)),
                        edges,
                        edgeSet);
                    continue;
                }

                if (dx > StepRelayHorizontalReach
                    || dy > StepRelayVerticalReach)
                {
                    continue;
                }

                var hasBlockingSolid = HasBlockingSolidAt(level, (a.X + b.X) * 0.5f, MathF.Min(a.Y, b.Y));
                if (hasBlockingSolid && !isMicroStepRelay)
                {
                    continue;
                }

                var cost = Distance(a, b) + (hasBlockingSolid ? BlockedMicroStepRelayWalkPenalty : 0f);
                AddBidirectionalEdge(portalA.NodeIndex, portalB.NodeIndex, NavEdgeKind.Walk, cost, edges, edgeSet);
            }
        }
    }

    private static bool IsMicroStepRelay(BuildSurface surfaceA, BuildSurface surfaceB, BuildNode a, BuildNode b)
    {
        var widthA = surfaceA.RightX - surfaceA.LeftX;
        var widthB = surfaceB.RightX - surfaceB.LeftX;
        return widthA <= MinimumSurfaceWidth
            && widthB <= MinimumSurfaceWidth
            && MathF.Abs(a.X - b.X) <= MinimumPortalSpacing
            && MathF.Abs(a.Y - b.Y) <= StepDownTolerance;
    }

    private static bool IsGroundedStairRampRelayCandidate(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        BuildNode from,
        BuildNode to)
    {
        var surfaceA = FindSurface(surfaces, from.SurfaceId);
        var surfaceB = FindSurface(surfaces, to.SurfaceId);
        return surfaceA.HasValue
            && surfaceB.HasValue
            && IsGroundedStairRampRelayCandidate(level, surfaceA.Value, surfaceB.Value, from, to);
    }

    private static bool IsGroundedStairRampRelayCandidate(
        SimpleLevel level,
        BuildSurface surfaceA,
        BuildSurface surfaceB,
        BuildNode a,
        BuildNode b)
    {
        var dx = MathF.Abs(a.X - b.X);
        var dy = MathF.Abs(a.Y - b.Y);
        if (dx > StairRampRelayHorizontalReach
            || dy <= StepRelayVerticalReach
            || dy > StairRampRelayVerticalReach
            || (dx < StairRampRelayMinimumHorizontal && dy > StepDownTolerance * 2f))
        {
            return false;
        }

        var widthA = surfaceA.RightX - surfaceA.LeftX;
        var widthB = surfaceB.RightX - surfaceB.LeftX;
        var narrowA = widthA <= StepRelayHorizontalReach * 1.5f;
        var narrowB = widthB <= StepRelayHorizontalReach * 1.5f;
        if (!narrowA && !narrowB)
        {
            return false;
        }

        return HasGroundedStairRampSupport(level, a, b);
    }

    private static bool HasGroundedStairRampSupport(SimpleLevel level, BuildNode a, BuildNode b)
    {
        var samples = Math.Clamp((int)MathF.Ceiling(Distance(a, b) / 24f), 3, 8);
        var supported = 0;
        for (var i = 0; i <= samples; i += 1)
        {
            var t = i / (float)samples;
            var x = a.X + ((b.X - a.X) * t);
            var y = a.Y + ((b.Y - a.Y) * t);
            var feetY = y + (ProbeHeight * 0.5f);
            if (level.IntersectsSolid(x - ProbeHalfWidth * 0.35f, feetY, x + ProbeHalfWidth * 0.35f, feetY + 18f))
            {
                supported += 1;
            }
        }

        return supported >= Math.Max(2, samples / 2);
    }

    private static void AddMicroStepExitJumpEdges(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildPortal> portals,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats)
    {
        var surfaceById = surfaces.ToDictionary(static surface => surface.Id);
        var surfacePortals = portals
            .Where(static portal => portal.SurfaceId.HasValue
                && portal.Kind is BuildPortalKind.Ledge or BuildPortalKind.SurfaceBreakpoint or BuildPortalKind.StepRelay)
            .ToArray();

        foreach (var sourcePortal in surfacePortals)
        {
            if (!surfaceById.TryGetValue(sourcePortal.SurfaceId!.Value, out var sourceSurface)
                || !IsMicroSurface(sourceSurface))
            {
                continue;
            }

            var source = nodes[sourcePortal.NodeIndex];
            var candidates = new List<ScoredPortalTraversalCandidate>();
            foreach (var targetPortal in surfacePortals)
            {
                if (targetPortal.SurfaceId == sourcePortal.SurfaceId
                    || !surfaceById.TryGetValue(targetPortal.SurfaceId!.Value, out var targetSurface)
                    || IsMicroSurface(targetSurface))
                {
                    continue;
                }

                var target = nodes[targetPortal.NodeIndex];
                var dx = target.X - source.X;
                var dy = target.Y - source.Y;
                var distance = Distance(source, target);
                if (MathF.Abs(dx) > MaxJumpReach
                    || dy >= -StepDownTolerance
                    || -dy > ReverseFallJumpVerticalReach
                    || distance > MaxJumpReach)
                {
                    continue;
                }

                var score = distance + (-dy * 0.35f) + PortalKindPenalty(targetPortal.Kind);
                candidates.Add(new ScoredPortalTraversalCandidate(
                    sourcePortal,
                    targetPortal,
                    NavEdgeKind.Jump,
                    Math.Sign(dx),
                    score));
            }

            foreach (var candidate in candidates
                         .Where(static candidate => candidate.Direction != 0)
                         .GroupBy(static candidate => candidate.Direction)
                         .SelectMany(static group => group.OrderBy(static candidate => candidate.Score).Take(4)))
            {
                TryAddCertifiedLandingRelayEdge(
                    level,
                    probeSurfaces,
                    nodes,
                    portals,
                    candidate.FromPortal.NodeIndex,
                    candidate.ToPortal.NodeIndex,
                    candidate.FromPortal.Id,
                    NavEdgeKind.Jump,
                    GetJumpCost(nodes[candidate.FromPortal.NodeIndex], nodes[candidate.ToPortal.NodeIndex], Distance(nodes[candidate.FromPortal.NodeIndex], nodes[candidate.ToPortal.NodeIndex])),
                    edges,
                    edgeSet,
                    stats);
            }
        }
    }

    private static bool IsMicroSurface(BuildSurface surface) =>
        surface.RightX - surface.LeftX <= MinimumSurfaceWidth;

    private static void AddLedgeDropDiscoveryEdges(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildPortal> portals,
        IReadOnlyList<BotNavigationAnchorAssetEntry> anchors,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats)
    {
        if (!ShouldDiscoverSpawnLedgeDrops(level.Mode))
        {
            return;
        }

        foreach (var portal in portals)
        {
            if (portal.Kind != BuildPortalKind.Ledge || !portal.SurfaceId.HasValue)
            {
                continue;
            }

            var surface = FindSurface(surfaces, portal.SurfaceId.Value);
            if (!surface.HasValue)
            {
                continue;
            }

            var node = nodes[portal.NodeIndex];
            if (!TryFindNearbySpawnTeam(anchors, node, out _))
            {
                continue;
            }

            if (node.X <= surface.Value.LeftX + SurfaceMergeGapTolerance)
            {
                TryAddLedgeDropDiscoveryEdge(level, surfaces, anchors, probeSurfaces, nodes, portals, portal, direction: -1f, edges, edgeSet, stats);
            }

            if (node.X >= surface.Value.RightX - SurfaceMergeGapTolerance)
            {
                TryAddLedgeDropDiscoveryEdge(level, surfaces, anchors, probeSurfaces, nodes, portals, portal, direction: 1f, edges, edgeSet, stats);
            }
        }
    }

    private static bool ShouldDiscoverSpawnLedgeDrops(GameModeKind mode) =>
        mode is GameModeKind.Arena
            or GameModeKind.ControlPoint
            or GameModeKind.KingOfTheHill
            or GameModeKind.DoubleKingOfTheHill;

    private static bool TryAddLedgeDropDiscoveryEdge(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BotNavigationAnchorAssetEntry> anchors,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        IReadOnlyList<BuildPortal> portals,
        BuildPortal portal,
        float direction,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats)
    {
        var from = nodes[portal.NodeIndex];
        if (!HasPotentialLandingBelow(surfaces, from, direction))
        {
            return false;
        }

        stats.CertifiedProbeAttempts += 1;
        if (stats.CertifiedProbeAttempts % 1_000 == 0)
        {
            TraceBuild(stats.TracePath, $"probe attempts={stats.CertifiedProbeAttempts} successes={stats.CertifiedProbeSuccesses}");
        }

        var target = new BotBrainProbeNode(
            from.X + (direction * StepRelayHorizontalReach),
            from.Y + MaxFallDistance);
        if (!BotBrainMovementProbe.TryDiscoverTeamAgnosticLandingEdge(
                level,
                new BotBrainProbeNode(from.X, from.Y),
                target,
                probeSurfaces,
                from.SurfaceId ?? -1,
                NavEdgeKind.Fall,
                out var probeResult,
                out var supportedClassMask,
                out var landingSurfaceId)
            && (!TryFindNearbySpawnTeam(anchors, from, out var spawnTeam)
                || !BotBrainMovementProbe.TryDiscoverLandingEdgeForTeam(
                    level,
                    new BotBrainProbeNode(from.X, from.Y),
                    target,
                    probeSurfaces,
                    from.SurfaceId ?? -1,
                    NavEdgeKind.Fall,
                    spawnTeam,
                    out probeResult,
                    out supportedClassMask,
                    out landingSurfaceId)))
        {
            return false;
        }

        var landingX = (probeResult.CompletionMinX + probeResult.CompletionMaxX) * 0.5f;
        var landingNode = FindNearestNodeOnSurface(nodes, landingSurfaceId, landingX);
        if (landingNode < 0 || landingNode == portal.NodeIndex)
        {
            return false;
        }

        var landingPortalId = FindPortalForNode(portals, landingNode, BuildPortalKind.Ledge);
        var cost = MathF.Max(Distance(from, nodes[landingNode]) * 0.8f, probeResult.Ticks);
        AddEdge(
            portal.NodeIndex,
            landingNode,
            NavEdgeKind.Fall,
            cost,
            edges,
            edgeSet,
            probeResult: probeResult,
            supportedClassMask: supportedClassMask,
            supportedTeamMask: BotBrainTeamMask.All,
            fromPortalId: portal.Id,
            toPortalId: landingPortalId);
        stats.CertifiedProbeSuccesses += 1;
        return true;
    }

    private static bool TryFindNearbySpawnTeam(
        IReadOnlyList<BotNavigationAnchorAssetEntry> anchors,
        BuildNode from,
        out PlayerTeam team)
    {
        foreach (var anchor in anchors)
        {
            if (!string.Equals(anchor.Kind, "Spawn", StringComparison.Ordinal)
                || !anchor.Team.HasValue
                || MathF.Abs(anchor.X - from.X) > MaxJumpReach
                || MathF.Abs(anchor.Y - from.Y) > ProbeHeight * 2f)
            {
                continue;
            }

            team = anchor.Team.Value;
            return true;
        }

        team = default;
        return false;
    }

    private static bool HasPotentialLandingBelow(
        IReadOnlyList<BuildSurface> surfaces,
        BuildNode from,
        float direction)
    {
        var probeLeft = MathF.Min(from.X, from.X + (direction * StepRelayHorizontalReach)) - ProbeHalfWidth;
        var probeRight = MathF.Max(from.X, from.X + (direction * StepRelayHorizontalReach)) + ProbeHalfWidth;
        foreach (var surface in surfaces)
        {
            if (surface.TopY <= from.Y + StepDownTolerance || surface.TopY > from.Y + MaxFallDistance)
            {
                continue;
            }

            if (surface.RightX < probeLeft || surface.LeftX > probeRight)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsFirstFallLandingCandidate(
        IReadOnlyList<BuildSurface> surfaces,
        BuildNode from,
        BuildSurface fromSurface,
        BuildSurface targetSurface)
    {
        if (targetSurface.TopY <= fromSurface.TopY)
        {
            return false;
        }

        var corridorLeft = MathF.Min(from.X, targetSurface.LeftX) - ProbeHalfWidth;
        var corridorRight = MathF.Max(from.X, targetSurface.RightX) + ProbeHalfWidth;
        foreach (var surface in surfaces)
        {
            if (surface.Id == fromSurface.Id || surface.Id == targetSurface.Id)
            {
                continue;
            }

            if (surface.TopY <= fromSurface.TopY + SurfaceMergeVerticalTolerance
                || surface.TopY >= targetSurface.TopY - SurfaceMergeVerticalTolerance
                || surface.RightX < corridorLeft
                || surface.LeftX > corridorRight)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static void AddTraversalEdgePair(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildPortal> portals,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        int nodeA,
        int nodeB,
        BuildSurface surfaceA,
        BuildSurface surfaceB,
        int? portalAId,
        int? portalBId,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats)
    {
        var a = nodes[nodeA];
        var b = nodes[nodeB];
        var dx = MathF.Abs(b.X - a.X);
        var dy = b.Y - a.Y;
        var dist = Distance(a, b);
        if (dist > MaxJumpReach && MathF.Abs(dy) > MaxFallDistance)
        {
            return;
        }

        if (dy <= StepDownTolerance && dist <= MaxJumpReach && CanJumpBetween(level, a, b))
        {
            TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, nodeA, nodeB, portalAId, portalBId, surfaceB, NavEdgeKind.Jump, GetJumpCost(a, b, dist), edges, edgeSet, stats);
        }

        if (dy >= -StepDownTolerance && dist <= MaxJumpReach && CanJumpBetween(level, b, a))
        {
            TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, nodeB, nodeA, portalBId, portalAId, surfaceA, NavEdgeKind.Jump, GetJumpCost(b, a, dist), edges, edgeSet, stats);
        }

        if (dy > StepDownTolerance && dy <= MaxFallDistance && dx < MaxJumpReach * 0.6f && CanFallBetween(level, a, b))
        {
            var kind = surfaceA.IsDropdown && dx < ProbeHalfWidth * 3f ? NavEdgeKind.Dropdown : NavEdgeKind.Fall;
            TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, nodeA, nodeB, portalAId, portalBId, surfaceB, kind, dist * 0.8f, edges, edgeSet, stats);
            if (dy <= ReverseFallJumpVerticalReach && dist <= MaxJumpReach)
            {
                TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, nodeB, nodeA, portalBId, portalAId, surfaceA, NavEdgeKind.Jump, GetJumpCost(b, a, dist), edges, edgeSet, stats);
            }
        }
        else if (ShouldUseCtfMicroStepRepairs(level) && dy <= ReverseFallJumpVerticalReach && dist <= MaxJumpReach && CanAttemptJumpBetween(level, b, a))
        {
            TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, nodeB, nodeA, portalBId, portalAId, surfaceA, NavEdgeKind.Jump, GetJumpCost(b, a, dist), edges, edgeSet, stats);
        }

        if (dy > StepDownTolerance && dy <= MaxFallDistance && dx <= MaxJumpReach && CanJumpBetween(level, a, b))
        {
            TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, nodeA, nodeB, portalAId, portalBId, surfaceB, NavEdgeKind.Jump, GetJumpCost(a, b, dist), edges, edgeSet, stats);
        }

        if (dy < -StepDownTolerance && -dy <= MaxFallDistance && dx < MaxJumpReach * 0.6f && CanFallBetween(level, b, a))
        {
            var kind = surfaceB.IsDropdown && dx < ProbeHalfWidth * 3f ? NavEdgeKind.Dropdown : NavEdgeKind.Fall;
            TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, nodeB, nodeA, portalBId, portalAId, surfaceA, kind, dist * 0.8f, edges, edgeSet, stats);
            if (-dy <= ReverseFallJumpVerticalReach && dist <= MaxJumpReach)
            {
                TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, nodeA, nodeB, portalAId, portalBId, surfaceB, NavEdgeKind.Jump, GetJumpCost(a, b, dist), edges, edgeSet, stats);
            }
        }
        else if (ShouldUseCtfMicroStepRepairs(level) && -dy <= ReverseFallJumpVerticalReach && dist <= MaxJumpReach && CanAttemptJumpBetween(level, a, b))
        {
            TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, nodeA, nodeB, portalAId, portalBId, surfaceB, NavEdgeKind.Jump, GetJumpCost(a, b, dist), edges, edgeSet, stats);
        }

        if (dy < -StepDownTolerance && -dy <= MaxFallDistance && dx <= MaxJumpReach && CanJumpBetween(level, b, a))
        {
            TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, nodeB, nodeA, portalBId, portalAId, surfaceA, NavEdgeKind.Jump, GetJumpCost(b, a, dist), edges, edgeSet, stats);
        }
    }

    private static void AddAnchorEdges(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BuildPortal> portals,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats)
    {
        for (var i = 0; i < nodes.Count; i += 1)
        {
            if (nodes[i].SurfaceId.HasValue)
            {
                continue;
            }

            var anchorPortalId = FindPortalForNode(portals, i, BuildPortalKind.Anchor);
            var anchorIndex = FindAnchorIndexForNode(portals, i);
            var attached = 0;
            if (anchorIndex.HasValue)
            {
                foreach (var portal in portals.Where(portal => portal.AnchorIndex == anchorIndex && portal.SurfaceId.HasValue))
                {
                    attached += TryAttachAnchorPortal(level, surfaces, probeSurfaces, nodes, i, anchorPortalId, portal, edges, edgeSet, stats)
                        ? 1
                        : 0;
                }
            }

            foreach (var candidate in EnumerateAttachmentCandidates(nodes, portals, i))
            {
                var anchor = nodes[i];
                var surface = nodes[candidate.NodeIndex];
                var dist = Distance(anchor, surface);
                if (candidate.Kind == NavEdgeKind.Walk)
                {
                    AddBidirectionalEdge(i, candidate.NodeIndex, NavEdgeKind.Walk, dist, edges, edgeSet, anchorPortalId, candidate.PortalId);
                    attached += 1;
                }
                else if (candidate.Kind == NavEdgeKind.Jump && CanJumpBetween(level, anchor, surface))
                {
                    var targetSurface = FindSurface(surfaces, surface.SurfaceId);
                    var added = TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, i, candidate.NodeIndex, anchorPortalId, candidate.PortalId, targetSurface, NavEdgeKind.Jump, GetJumpCost(anchor, surface, dist), edges, edgeSet, stats);
                    if (CanFallBetween(level, surface, anchor))
                    {
                        TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, candidate.NodeIndex, i, candidate.PortalId, anchorPortalId, targetSurface: null, NavEdgeKind.Fall, dist, edges, edgeSet, stats);
                    }

                    attached += added ? 1 : 0;
                }
                else if (candidate.Kind == NavEdgeKind.Fall && CanFallBetween(level, anchor, surface))
                {
                    var targetSurface = FindSurface(surfaces, surface.SurfaceId);
                    var added = TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, i, candidate.NodeIndex, anchorPortalId, candidate.PortalId, targetSurface, NavEdgeKind.Fall, dist, edges, edgeSet, stats);
                    if (CanJumpBetween(level, surface, anchor))
                    {
                        TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, candidate.NodeIndex, i, candidate.PortalId, anchorPortalId, targetSurface: null, NavEdgeKind.Jump, GetJumpCost(surface, anchor, dist), edges, edgeSet, stats);
                    }

                    attached += added ? 1 : 0;
                }

                if (attached >= 2)
                {
                    break;
                }
            }
        }
    }

    private static bool TryAttachAnchorPortal(
        SimpleLevel level,
        IReadOnlyList<BuildSurface> surfaces,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        int anchorNodeIndex,
        int? anchorPortalId,
        BuildPortal portal,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats)
    {
        if (portal.NodeIndex == anchorNodeIndex)
        {
            return false;
        }

        var anchor = nodes[anchorNodeIndex];
        var surface = nodes[portal.NodeIndex];
        var dy = surface.Y - anchor.Y;
        var dist = Distance(anchor, surface);
        if (dist > MaxJumpReach)
        {
            return false;
        }

        if (MathF.Abs(dy) <= StepDownTolerance * 2f)
        {
            AddBidirectionalEdge(anchorNodeIndex, portal.NodeIndex, NavEdgeKind.Walk, dist, edges, edgeSet, anchorPortalId, portal.Id);
            return true;
        }

        if (dy < 0f && CanJumpBetween(level, anchor, surface))
        {
            var targetSurface = FindSurface(surfaces, surface.SurfaceId);
            return TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, anchorNodeIndex, portal.NodeIndex, anchorPortalId, portal.Id, targetSurface, NavEdgeKind.Jump, GetJumpCost(anchor, surface, dist), edges, edgeSet, stats);
        }

        if (dy > 0f && CanFallBetween(level, anchor, surface))
        {
            var targetSurface = FindSurface(surfaces, surface.SurfaceId);
            return TryAddCertifiedTraversalEdge(level, probeSurfaces, nodes, anchorNodeIndex, portal.NodeIndex, anchorPortalId, portal.Id, targetSurface, NavEdgeKind.Fall, dist, edges, edgeSet, stats);
        }

        return false;
    }

    private static IEnumerable<AnchorAttachmentCandidate> EnumerateAttachmentCandidates(
        IReadOnlyList<BuildNode> nodes,
        IReadOnlyList<BuildPortal> portals,
        int anchorNode)
    {
        var anchor = nodes[anchorNode];
        return portals
            .Where(static portal => portal.SurfaceId.HasValue && portal.Kind is BuildPortalKind.ObjectiveStanding or BuildPortalKind.Ledge or BuildPortalKind.StepRelay or BuildPortalKind.SurfaceBreakpoint)
            .Select(portal =>
            {
                var node = nodes[portal.NodeIndex];
                var dy = node.Y - anchor.Y;
                var dist = Distance(anchor, node);
                var kind = MathF.Abs(dy) <= StepDownTolerance * 2f
                    ? NavEdgeKind.Walk
                    : dy < 0f
                        ? NavEdgeKind.Jump
                        : NavEdgeKind.Fall;
                return new AnchorAttachmentCandidate(portal.NodeIndex, portal.Id, kind, dist);
            })
            .Where(static candidate => candidate.Distance <= MaxJumpReach)
            .OrderBy(static candidate => candidate.Distance)
            .Take(8);
    }

    private static int? FindPortalForNode(IReadOnlyList<BuildPortal> portals, int nodeIndex, BuildPortalKind preferredKind)
    {
        for (var i = 0; i < portals.Count; i += 1)
        {
            if (portals[i].NodeIndex == nodeIndex && portals[i].Kind == preferredKind)
            {
                return portals[i].Id;
            }
        }

        for (var i = 0; i < portals.Count; i += 1)
        {
            if (portals[i].NodeIndex == nodeIndex)
            {
                return portals[i].Id;
            }
        }

        return null;
    }

    private static int? FindAnchorIndexForNode(IReadOnlyList<BuildPortal> portals, int nodeIndex)
    {
        for (var i = 0; i < portals.Count; i += 1)
        {
            if (portals[i].NodeIndex == nodeIndex && portals[i].Kind == BuildPortalKind.Anchor)
            {
                return portals[i].AnchorIndex;
            }
        }

        return null;
    }

    private static void AddBidirectionalEdge(
        int fromNode,
        int toNode,
        NavEdgeKind kind,
        float cost,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        int? fromPortalId = null,
        int? toPortalId = null)
    {
        AddEdge(fromNode, toNode, kind, cost, edges, edgeSet, supportedClassMask: BotBrainClassMask.All, supportedTeamMask: BotBrainTeamMask.All, fromPortalId: fromPortalId, toPortalId: toPortalId);
        AddEdge(toNode, fromNode, kind, cost, edges, edgeSet, supportedClassMask: BotBrainClassMask.All, supportedTeamMask: BotBrainTeamMask.All, fromPortalId: toPortalId, toPortalId: fromPortalId);
    }

    private static void AddBidirectionalPreferredStairRampEdges(
        int fromNode,
        int toNode,
        BuildNode from,
        BuildNode to,
        BuildSurface fromSurface,
        BuildSurface toSurface,
        float cost,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet)
    {
        var forwardKind = ResolveStairRampRelayKind(fromSurface, toSurface, from, to);
        var reverseKind = ResolveStairRampRelayKind(toSurface, fromSurface, to, from);
        AddEdge(
            fromNode,
            toNode,
            forwardKind,
            cost,
            edges,
            edgeSet,
            semanticCompletion: CreateStairRampCompletion(toSurface, from, to, forwardKind),
            preferLowerCost: true);
        AddEdge(
            toNode,
            fromNode,
            reverseKind,
            cost,
            edges,
            edgeSet,
            semanticCompletion: CreateStairRampCompletion(fromSurface, to, from, reverseKind),
            preferLowerCost: true);
    }

    private static bool TryAddCertifiedTraversalEdge(
        SimpleLevel level,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        int fromNode,
        int toNode,
        int? fromPortalId,
        int? toPortalId,
        BuildSurface? targetSurface,
        NavEdgeKind kind,
        float cost,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats,
        bool allowTeamSpecificFallback = false,
        bool useProbeEnsemble = false)
    {
        stats.CertifiedProbeAttempts += 1;
        if (stats.CertifiedProbeAttempts % 1_000 == 0)
        {
            TraceBuild(stats.TracePath, $"probe attempts={stats.CertifiedProbeAttempts} successes={stats.CertifiedProbeSuccesses}");
        }

        var allTeamsEdgeKey = new EdgeKey(fromNode, toNode, kind, BotBrainTeamMask.All);
        if (fromNode == toNode || cost < 0f || edgeSet.Contains(allTeamsEdgeKey) || stats.FailedCertifiedEdges.Contains(allTeamsEdgeKey))
        {
            return false;
        }

        var from = nodes[fromNode];
        var to = nodes[toNode];
        if (!string.IsNullOrWhiteSpace(stats.TracePath) && stats.CertifiedProbeAttempts <= 200)
        {
            TraceBuild(stats.TracePath, $"probe start attempt={stats.CertifiedProbeAttempts} edge={fromNode}->{toNode} kind={kind} from=({from.X:0.0},{from.Y:0.0}) to=({to.X:0.0},{to.Y:0.0})");
        }

        var probeTargetSurface = targetSurface.HasValue
            ? new BotBrainProbeSurface(
                targetSurface.Value.Id,
                targetSurface.Value.LeftX,
                targetSurface.Value.RightX,
                targetSurface.Value.TopY)
            : (BotBrainProbeSurface?)null;
        if (!BotBrainMovementProbe.TryCertifyTeamAgnosticEdge(
                level,
                new BotBrainProbeNode(from.X, from.Y),
                new BotBrainProbeNode(to.X, to.Y),
                probeTargetSurface,
                kind,
                out var probeResult,
                out var supportedClassMask,
                useProbeEnsemble))
        {
            if (!allowTeamSpecificFallback && !ShouldUseCtfMicroStepRepairs(level))
            {
                stats.FailedCertifiedEdges.Add(allTeamsEdgeKey);
                if (!string.IsNullOrWhiteSpace(stats.TracePath) && stats.CertifiedProbeAttempts <= 200)
                {
                    TraceBuild(stats.TracePath, $"probe fail attempt={stats.CertifiedProbeAttempts} edge={fromNode}->{toNode} kind={kind}");
                }

                return false;
            }

            var addedTeamSpecificEdge = false;
            foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
            {
                var teamMask = BotBrainTeamMask.For(team);
                var teamEdgeKey = new EdgeKey(fromNode, toNode, kind, teamMask);
                if (edgeSet.Contains(teamEdgeKey) || stats.FailedCertifiedEdges.Contains(teamEdgeKey))
                {
                    continue;
                }

                if (!BotBrainMovementProbe.TryCertifyEdgeForTeam(
                        level,
                        new BotBrainProbeNode(from.X, from.Y),
                        new BotBrainProbeNode(to.X, to.Y),
                        probeTargetSurface,
                        kind,
                        team,
                        out var teamProbeResult,
                        out var teamSupportedClassMask,
                        useProbeEnsemble))
                {
                    stats.FailedCertifiedEdges.Add(teamEdgeKey);
                    continue;
                }

                AddEdge(
                    fromNode,
                    toNode,
                    kind,
                    AdjustCertifiedTraversalCost(kind, cost, teamProbeResult),
                    edges,
                    edgeSet,
                    probeResult: teamProbeResult,
                    supportedClassMask: teamSupportedClassMask,
                    supportedTeamMask: teamMask,
                    fromPortalId: fromPortalId,
                    toPortalId: toPortalId);
                stats.CertifiedProbeSuccesses += 1;
                addedTeamSpecificEdge = true;
            }

            if (!addedTeamSpecificEdge)
            {
                stats.FailedCertifiedEdges.Add(allTeamsEdgeKey);
                if (!string.IsNullOrWhiteSpace(stats.TracePath) && stats.CertifiedProbeAttempts <= 200)
                {
                    TraceBuild(stats.TracePath, $"probe fail attempt={stats.CertifiedProbeAttempts} edge={fromNode}->{toNode} kind={kind}");
                }

                return false;
            }

            return true;
        }

        AddEdge(
            fromNode,
            toNode,
            kind,
            AdjustCertifiedTraversalCost(kind, cost, probeResult),
            edges,
            edgeSet,
            probeResult: probeResult,
            supportedClassMask: supportedClassMask,
            supportedTeamMask: BotBrainTeamMask.All,
            fromPortalId: fromPortalId,
            toPortalId: toPortalId);
        stats.CertifiedProbeSuccesses += 1;
        if (!string.IsNullOrWhiteSpace(stats.TracePath) && stats.CertifiedProbeAttempts <= 200)
        {
            TraceBuild(stats.TracePath, $"probe success attempt={stats.CertifiedProbeAttempts} edge={fromNode}->{toNode} kind={kind}");
        }

        return true;
    }

    private static float AdjustCertifiedTraversalCost(
        NavEdgeKind kind,
        float cost,
        BotBrainMovementProbeResult probeResult)
    {
        var penalty = kind switch
        {
            NavEdgeKind.Jump => 24f,
            NavEdgeKind.Dropdown => 18f,
            NavEdgeKind.Fall => 12f,
            _ => 0f,
        };

        if (probeResult.RequiresGroundedContinuation)
        {
            penalty += 18f;
        }

        if (probeResult.JumpTriggerTick > 0)
        {
            penalty += MathF.Min(36f, probeResult.JumpTriggerTick * 2f);
        }

        if (probeResult.Ticks > 0)
        {
            penalty += MathF.Min(36f, probeResult.Ticks * 0.5f);
        }

        if (probeResult.VariantAttempts > 0)
        {
            var successRate = Math.Clamp(
                probeResult.VariantSuccesses / (float)probeResult.VariantAttempts,
                0f,
                1f);
            penalty += (1f - successRate) * 60f;
            if (probeResult.VariantSuccesses <= 1)
            {
                penalty += 24f;
            }
        }

        return cost + penalty;
    }


    private static bool TryAddCertifiedLandingRelayEdge(
        SimpleLevel level,
        IReadOnlyList<BotBrainProbeSurface> probeSurfaces,
        IReadOnlyList<BuildNode> nodes,
        IReadOnlyList<BuildPortal> portals,
        int fromNode,
        int intendedToNode,
        int? fromPortalId,
        NavEdgeKind kind,
        float cost,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BuildStats stats)
    {
        stats.CertifiedProbeAttempts += 1;
        if (stats.CertifiedProbeAttempts % 1_000 == 0)
        {
            TraceBuild(stats.TracePath, $"probe attempts={stats.CertifiedProbeAttempts} successes={stats.CertifiedProbeSuccesses}");
        }

        var from = nodes[fromNode];
        var intendedTo = nodes[intendedToNode];
        if (fromNode == intendedToNode || cost < 0f)
        {
            return false;
        }

        if (BotBrainMovementProbe.TryDiscoverTeamAgnosticLandingEdge(
                level,
                new BotBrainProbeNode(from.X, from.Y),
                new BotBrainProbeNode(intendedTo.X, intendedTo.Y),
                probeSurfaces,
                from.SurfaceId ?? -1,
                kind,
                out var probeResult,
                out var supportedClassMask,
                out var landingSurfaceId))
        {
            return TryAddLandingRelayEdge(
                nodes,
                portals,
                fromNode,
                intendedTo,
                fromPortalId,
                kind,
                cost,
                edges,
                edgeSet,
                probeResult,
                supportedClassMask,
                BotBrainTeamMask.All,
                landingSurfaceId,
                stats);
        }

        if (!ShouldUseCtfMicroStepRepairs(level))
        {
            return false;
        }

        var addedTeamSpecificEdge = false;
        foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
        {
            if (!BotBrainMovementProbe.TryDiscoverLandingEdgeForTeam(
                    level,
                    new BotBrainProbeNode(from.X, from.Y),
                    new BotBrainProbeNode(intendedTo.X, intendedTo.Y),
                    probeSurfaces,
                    from.SurfaceId ?? -1,
                    kind,
                    team,
                    out var teamProbeResult,
                    out var teamSupportedClassMask,
                    out var teamLandingSurfaceId))
            {
                continue;
            }

            addedTeamSpecificEdge |= TryAddLandingRelayEdge(
                nodes,
                portals,
                fromNode,
                intendedTo,
                fromPortalId,
                kind,
                cost,
                edges,
                edgeSet,
                teamProbeResult,
                teamSupportedClassMask,
                BotBrainTeamMask.For(team),
                teamLandingSurfaceId,
                stats);
        }

        return addedTeamSpecificEdge;
    }

    private static bool TryAddLandingRelayEdge(
        IReadOnlyList<BuildNode> nodes,
        IReadOnlyList<BuildPortal> portals,
        int fromNode,
        BuildNode intendedTo,
        int? fromPortalId,
        NavEdgeKind kind,
        float cost,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BotBrainMovementProbeResult probeResult,
        int supportedClassMask,
        int supportedTeamMask,
        int landingSurfaceId,
        BuildStats stats)
    {
        var from = nodes[fromNode];
        var landingNode = FindNearestNodeOnSurface(nodes, landingSurfaceId, intendedTo.X);
        if (landingNode < 0 || landingNode == fromNode)
        {
            return false;
        }

        var landingCost = MathF.Max(cost, Distance(from, nodes[landingNode]));
        var landingPortalId = FindPortalForNode(portals, landingNode, BuildPortalKind.Ledge);
        var beforeCount = edges.Count;
        AddEdge(
            fromNode,
            landingNode,
            kind,
            landingCost,
            edges,
            edgeSet,
            probeResult: probeResult,
            supportedClassMask: supportedClassMask,
            supportedTeamMask: supportedTeamMask,
            fromPortalId: fromPortalId,
            toPortalId: landingPortalId);
        if (edges.Count == beforeCount)
        {
            return false;
        }

        stats.CertifiedProbeSuccesses += 1;
        return true;
    }

    private static void AddEdge(
        int fromNode,
        int toNode,
        NavEdgeKind kind,
        float cost,
        List<BuildEdge> edges,
        HashSet<EdgeKey> edgeSet,
        BotBrainMovementProbeResult? probeResult = null,
        BotBrainMovementProbeResult? semanticCompletion = null,
        int supportedClassMask = BotBrainClassMask.All,
        int supportedTeamMask = BotBrainTeamMask.All,
        int? fromPortalId = null,
        int? toPortalId = null,
        bool preferLowerCost = false)
    {
        if (fromNode == toNode || cost < 0f)
        {
            return;
        }

        var edgeKey = new EdgeKey(fromNode, toNode, kind, supportedTeamMask);
        if (!edgeSet.Add(edgeKey))
        {
            MergeExistingEdgeSupport(fromNode, toNode, kind, supportedTeamMask, supportedClassMask, cost, probeResult, semanticCompletion, fromPortalId, toPortalId, preferLowerCost, edges);
            return;
        }

        edges.Add(new BuildEdge(fromNode, toNode, kind, cost, fromPortalId, toPortalId, probeResult, semanticCompletion, supportedClassMask, supportedTeamMask));
    }

    private static void MergeExistingEdgeSupport(
        int fromNode,
        int toNode,
        NavEdgeKind kind,
        int supportedTeamMask,
        int supportedClassMask,
        float cost,
        BotBrainMovementProbeResult? probeResult,
        BotBrainMovementProbeResult? semanticCompletion,
        int? fromPortalId,
        int? toPortalId,
        bool preferLowerCost,
        List<BuildEdge> edges)
    {
        for (var i = 0; i < edges.Count; i += 1)
        {
            var edge = edges[i];
            if (edge.FromNode != fromNode
                || edge.ToNode != toNode
                || edge.Kind != kind
                || edge.SupportedTeamMask != supportedTeamMask)
            {
                continue;
            }

            edges[i] = edge with
            {
                SupportedClassMask = MergeClassMasks(edge.SupportedClassMask, supportedClassMask),
                Cost = preferLowerCost ? MathF.Min(edge.Cost, cost) : edge.Cost,
                ProbeResult = edge.ProbeResult ?? probeResult,
                SemanticCompletion = edge.SemanticCompletion ?? semanticCompletion,
                FromPortalId = edge.FromPortalId ?? fromPortalId,
                ToPortalId = edge.ToPortalId ?? toPortalId,
            };
            return;
        }
    }

    private static int MergeClassMasks(int first, int second)
    {
        return first == BotBrainClassMask.All || second == BotBrainClassMask.All
            ? BotBrainClassMask.All
            : first | second;
    }

    private static BuildSurface? FindSurface(IReadOnlyList<BuildSurface> surfaces, int? surfaceId)
    {
        if (!surfaceId.HasValue)
        {
            return null;
        }

        for (var i = 0; i < surfaces.Count; i += 1)
        {
            if (surfaces[i].Id == surfaceId.Value)
            {
                return surfaces[i];
            }
        }

        return null;
    }

    private static bool TryFindNearestSurface(
        IReadOnlyList<BuildSurface> surfaces,
        float x,
        float y,
        float maxHorizontalDistance,
        float maxVerticalDistance,
        out BuildSurface surface)
    {
        var bestScore = float.MaxValue;
        var bestSurface = default(BuildSurface);
        var found = false;
        for (var i = 0; i < surfaces.Count; i += 1)
        {
            var candidate = surfaces[i];
            var clampedX = Math.Clamp(x, candidate.LeftX, candidate.RightX);
            var dx = MathF.Abs(clampedX - x);
            var dy = MathF.Abs(candidate.TopY - y);
            if (dx > maxHorizontalDistance || dy > maxVerticalDistance)
            {
                continue;
            }

            var score = dy + dx * 0.5f;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestSurface = candidate;
            found = true;
        }

        surface = bestSurface;
        return found;
    }

    private static int FindNearestNodeOnSurface(IReadOnlyList<BuildNode> nodes, int surfaceId, float preferredX)
    {
        var bestNode = -1;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < nodes.Count; i += 1)
        {
            if (nodes[i].SurfaceId != surfaceId)
            {
                continue;
            }

            var distance = MathF.Abs(nodes[i].X - preferredX);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestNode = i;
        }

        return bestNode;
    }

    private static bool CanAttemptJumpBetween(SimpleLevel level, BuildNode from, BuildNode to)
    {
        return CanJumpBetween(level, from, to) || IsCloseStepJumpCandidate(from, to);
    }

    private static bool IsCloseStepJumpCandidate(BuildNode from, BuildNode to)
    {
        return MathF.Abs(to.X - from.X) <= StepRelayHorizontalReach * 1.25f
            && MathF.Abs(to.Y - from.Y) <= SurfaceMergeVerticalTolerance * 2f;
    }

    private static bool CanJumpBetween(SimpleLevel level, BuildNode from, BuildNode to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var jumpHeight = (ConservativeJumpSpeed * ConservativeJumpSpeed) /
            (2f * LegacyMovementModel.GravityPerTick * LegacyMovementModel.SourceTicksPerSecond * LegacyMovementModel.SourceTicksPerSecond);
        if (-dy > jumpHeight * 1.1f)
        {
            return false;
        }

        return !HasBlockingSolidAt(level, from.X + dx * 0.25f, from.Y + dy * 0.25f - jumpHeight * 0.3f)
            && !HasBlockingSolidAt(level, from.X + dx * 0.5f, MathF.Min(from.Y, to.Y) - jumpHeight * 0.45f)
            && !HasBlockingSolidAt(level, from.X + dx * 0.75f, from.Y + dy * 0.75f - jumpHeight * 0.3f);
    }

    private static float GetJumpCost(BuildNode from, BuildNode to, float distance)
    {
        return distance * 1.5f;
    }

    private static bool CanFallBetween(SimpleLevel level, BuildNode upper, BuildNode lower)
    {
        if (lower.Y <= upper.Y)
        {
            return false;
        }

        var x = (upper.X + lower.X) * 0.5f;
        var steps = (int)MathF.Ceiling((lower.Y - upper.Y) / 32f);
        steps = Math.Max(2, Math.Min(steps, 20));
        for (var i = 1; i < steps; i += 1)
        {
            var sampleY = upper.Y + ((lower.Y - upper.Y) * i / steps);
            if (HasBlockingSolidAt(level, x, sampleY))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasBlockingSolidAt(SimpleLevel level, float x, float y)
    {
        return level.IntersectsSolid(
            x - ProbeHalfWidth,
            y - ProbeHeight * 0.5f,
            x + ProbeHalfWidth,
            y + ProbeHeight * 0.5f);
    }

    private static float Distance(BuildNode a, BuildNode b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float SurfaceNodeY(float surfaceTopY) => surfaceTopY - (ProbeHeight * 0.5f);

    private sealed record GraphBuildData(
        List<BuildSurface> Surfaces,
        List<BuildNode> Nodes,
        List<BuildEdge> Edges,
        List<BuildPortal> Portals,
        List<BotNavigationAnchorAssetEntry> Anchors,
        BuildStats Stats);

    private sealed class BuildStats
    {
        public int SurfacePairChecks { get; set; }

        public int NodePairChecks { get; set; }

        public int CertifiedProbeAttempts { get; set; }

        public int CertifiedProbeSuccesses { get; set; }

        public string? TracePath { get; init; }

        public HashSet<EdgeKey> FailedCertifiedEdges { get; } = [];
    }

    private readonly record struct SurfaceInterval(float LeftX, float RightX, float TopY, bool IsDropdown);

    private readonly record struct BuildNode(float X, float Y, NavNodeKind Kind, int? SurfaceId);

    private readonly record struct BuildEdge(
        int FromNode,
        int ToNode,
        NavEdgeKind Kind,
        float Cost,
        int? FromPortalId,
        int? ToPortalId,
        BotBrainMovementProbeResult? ProbeResult,
        BotBrainMovementProbeResult? SemanticCompletion,
        int SupportedClassMask,
        int SupportedTeamMask);

    private readonly record struct EdgeKey(int FromNode, int ToNode, NavEdgeKind Kind, int SupportedTeamMask);

    private readonly record struct AnchorAttachmentCandidate(int NodeIndex, int PortalId, NavEdgeKind Kind, float Distance);

    private readonly record struct PortalKey(BuildPortalKind Kind, int? SurfaceId, int NodeIndex, int SpacingBucket, int? AnchorIndex);

    private readonly record struct NodePairKey(int FromNode, int ToNode);

    private readonly record struct BuildPortal(
        int Id,
        BuildPortalKind Kind,
        int NodeIndex,
        int? SurfaceId,
        int? AnchorIndex,
        float X,
        float Y);

    private readonly record struct PortalTraversalCandidate(
        BuildPortal FromPortal,
        BuildPortal ToPortal,
        NavEdgeKind Kind,
        int Direction,
        float Score);

    private readonly record struct PortalValidationEdge(int ToPortalId, int SupportedClassMask, int SupportedTeamMask);

    private readonly record struct ScoredPortalTraversalCandidate(
        BuildPortal FromPortal,
        BuildPortal ToPortal,
        NavEdgeKind Kind,
        int Direction,
        float Score);

    private enum BuildPortalKind : byte
    {
        Ledge,
        SurfaceBreakpoint,
        InteriorCorner,
        StepRelay,
        Anchor,
        ObjectiveStanding,
    }

    private record struct BuildSurface(
        int Id,
        float LeftX,
        float RightX,
        float TopY,
        bool IsDropdown,
        int FirstNodeIndex,
        int LastNodeIndex);
}
