using OpenGarrison.Core;
using System.Diagnostics;

namespace OpenGarrison.BotAI;

public static class BotNavigationAssetBuilder
{
    private const float BaseSampleSpacing = 96f;
    private const float HorizontalProbeStep = 8f;
    private const float MinimumJumpHorizontalDistance = 18f;
    private const int MaxJumpTargetsPerSourceNode = 8;
    private const int MaxGroundTraversalTargetsPerSourceNode = 12;
    private const float GroundTraversalMaxHorizontalDistance = 240f;
    private const float GroundTraversalMaxRiseDistance = 220f;
    private const float GroundTraversalMaxDescentDistance = 96f;
    private const float DropHorizontalTolerance = 18f;
    private const float MaximumDropDistance = 320f;
    private const float MinimumDropDistance = 18f;

    public static BotNavigationAsset Build(SimpleLevel level, PlayerClass classId, string? levelFingerprint = null)
    {
        return Build(level, classId, levelFingerprint, BotNavigationHintStore.Load(level));
    }

    public static BotNavigationAsset Build(
        SimpleLevel level,
        PlayerClass classId,
        string? levelFingerprint,
        BotNavigationHintAsset? hintAsset)
    {
        ArgumentNullException.ThrowIfNull(level);

        var stopwatch = Stopwatch.StartNew();
        var profile = BotNavigationProfiles.GetProfileForClass(classId);
        var classDefinition = BotNavigationClasses.GetDefinition(classId);
        var hintBuildMode = ResolveHintBuildMode(hintAsset);
        var useExplicitHintGraph = hintAsset is not null && hintBuildMode == BotNavigationHintBuildMode.ExplicitGraph;
        var candidateNodeCount = 0;
        var surfaceSampleNodeCount = 0;
        var autoAnchorNodeCount = 0;
        var hintNodeCount = 0;
        var mutableNodes = new Dictionary<string, MutableNode>(StringComparer.Ordinal);
        var nodesBySurface = new Dictionary<int, List<MutableNode>>();
        var nextVirtualSurfaceId = -1;

        var phaseStopwatch = Stopwatch.StartNew();
        var surfaceSamplingMilliseconds = 0d;
        var autoAnchorMilliseconds = 0d;
        var hintNodeMilliseconds = 0d;
        var automaticEdgeMilliseconds = 0d;
        var hintEdgeMilliseconds = 0d;
        var dropEdgeMilliseconds = 0d;

        if (!useExplicitHintGraph)
        {
            for (var surfaceIndex = 0; surfaceIndex < level.Solids.Count; surfaceIndex += 1)
            {
                var solid = level.Solids[surfaceIndex];
                foreach (var sampleX in EnumerateSurfaceSamplePositions(solid, classDefinition, profile))
                {
                    candidateNodeCount += 1;
                    if (TryAddNode(
                            level,
                            classDefinition,
                            mutableNodes,
                            nodesBySurface,
                            surfaceIndex,
                            sampleX,
                            solid.Top - classDefinition.CollisionBottom,
                            BotNavigationNodeKind.Surface,
                            null,
                            string.Empty,
                            requiresGroundSupport: true))
                    {
                        surfaceSampleNodeCount += 1;
                    }
                }
            }
        }
        surfaceSamplingMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
        phaseStopwatch.Restart();

        foreach (var anchor in EnumerateAnchors(level, hintAsset))
        {
            candidateNodeCount += 1;
            if (!TryProjectAnchor(level, classDefinition, anchor, out var projected))
            {
                continue;
            }

            if (TryAddNode(
                    level,
                    classDefinition,
                    mutableNodes,
                    nodesBySurface,
                    projected.SurfaceId,
                    projected.X,
                    projected.Y,
                    anchor.Kind,
                    anchor.Team,
                    anchor.Label,
                    requiresGroundSupport: true))
            {
                autoAnchorNodeCount += 1;
            }
        }
        autoAnchorMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
        phaseStopwatch.Restart();

        if (hintAsset is not null)
        {
            foreach (var hintNode in hintAsset.Nodes)
            {
                if (!BotNavigationClasses.AppliesToProfile(hintNode.Classes, hintNode.Profiles, profile))
                {
                    continue;
                }

                candidateNodeCount += 1;
                if (TryAddHintNode(level, classDefinition, mutableNodes, nodesBySurface, hintNode, useExplicitHintGraph, ref nextVirtualSurfaceId))
                {
                    hintNodeCount += 1;
                }
            }
        }
        hintNodeMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
        phaseStopwatch.Restart();

        var orderedNodes = mutableNodes.Values
            .OrderBy(static node => node.SurfaceId)
            .ThenBy(static node => node.X)
            .ToList();
        for (var index = 0; index < orderedNodes.Count; index += 1)
        {
            orderedNodes[index].Id = index;
        }
        var labeledNodes = orderedNodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Label))
            .GroupBy(node => node.Label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderByDescending(node => node.Kind == BotNavigationNodeKind.RouteAnchor)
                    .ThenBy(node => node.Id)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var edges = new List<BotNavigationEdge>();
        var edgeKeys = new HashSet<long>();
        var walkEdgeCount = 0;
        var jumpEdgeCount = 0;
        var dropEdgeCount = 0;
        var hintEdgeCount = 0;
        var jumpSearchEnvelope = BotNavigationMovementValidator.GetSearchEnvelope(profile, classDefinition);

        if (!useExplicitHintGraph)
        {
            foreach (var surfaceNodes in nodesBySurface.Values)
            {
                surfaceNodes.Sort(static (left, right) => left.X.CompareTo(right.X));
                for (var index = 0; index + 1 < surfaceNodes.Count; index += 1)
                {
                    var from = surfaceNodes[index];
                    var to = surfaceNodes[index + 1];
                    if (!CanWalkBetween(level, classDefinition, from, to))
                    {
                        continue;
                    }

                    var cost = MathF.Abs(to.X - from.X);
                    if (TryAddEdge(edgeKeys, edges, new BotNavigationEdge { FromNodeId = from.Id, ToNodeId = to.Id, Kind = BotNavigationTraversalKind.Walk, Cost = cost }))
                    {
                        walkEdgeCount += 1;
                    }

                    if (TryAddEdge(edgeKeys, edges, new BotNavigationEdge { FromNodeId = to.Id, ToNodeId = from.Id, Kind = BotNavigationTraversalKind.Walk, Cost = cost }))
                    {
                        walkEdgeCount += 1;
                    }
                }
            }

            foreach (var sourceNode in orderedNodes)
            {
                TryAddGroundTraversalEdges(
                    level,
                    classDefinition,
                    sourceNode,
                    orderedNodes,
                    edgeKeys,
                    edges,
                    ref walkEdgeCount);
                TryAddJumpEdges(
                    level,
                    classDefinition,
                    profile,
                    sourceNode,
                    orderedNodes,
                    jumpSearchEnvelope,
                    edgeKeys,
                    edges,
                    ref jumpEdgeCount);
            }
        }
        automaticEdgeMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
        phaseStopwatch.Restart();

        if (hintAsset is not null)
        {
            TryAddHintEdges(
                level,
                classDefinition,
                profile,
                hintAsset,
                labeledNodes,
                edgeKeys,
                edges,
                ref walkEdgeCount,
                ref jumpEdgeCount,
                ref dropEdgeCount,
                ref hintEdgeCount);
        }
        hintEdgeMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;
        phaseStopwatch.Restart();

        if (!useExplicitHintGraph)
        {
            foreach (var surfaceNodes in nodesBySurface.Values)
            {
                if (surfaceNodes.Count == 0)
                {
                    continue;
                }

                TryAddDropEdge(level, classDefinition, surfaceNodes[0], orderedNodes, edgeKeys, edges, ref dropEdgeCount);
                if (surfaceNodes.Count > 1)
                {
                    TryAddDropEdge(level, classDefinition, surfaceNodes[^1], orderedNodes, edgeKeys, edges, ref dropEdgeCount);
                }
            }
        }
        dropEdgeMilliseconds = phaseStopwatch.Elapsed.TotalMilliseconds;

        stopwatch.Stop();

        var builtNodes = orderedNodes
            .Select(static node => new BotNavigationNode
            {
                Id = node.Id,
                X = node.X,
                Y = node.Y,
                SurfaceId = node.SurfaceId,
                Kind = node.Kind,
                Team = node.Team,
                Label = node.Label,
                RequiresGroundSupport = node.RequiresGroundSupport,
            })
            .ToArray();

        return new BotNavigationAsset
        {
            FormatVersion = BotNavigationAssetStore.CurrentFormatVersion,
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
            ClassId = classId,
            Profile = profile,
            LevelFingerprint = levelFingerprint ?? BotNavigationLevelFingerprint.Compute(level),
            BuildStrategy = useExplicitHintGraph
                ? BotNavigationBuildStrategy.ExplicitHintGraphValidatedTraversals
                : hintAsset is null
                ? BotNavigationBuildStrategy.GeometrySampledValidatedJumps
                : BotNavigationBuildStrategy.HintAugmentedValidatedJumps,
            BuiltUtc = DateTime.UtcNow,
            Stats = new BotNavigationBuildStats
            {
                SurfaceCount = level.Solids.Count,
                CandidateNodeCount = candidateNodeCount,
                SurfaceSampleNodeCount = surfaceSampleNodeCount,
                AutoAnchorNodeCount = autoAnchorNodeCount,
                HintNodeCount = hintNodeCount,
                NodeCount = builtNodes.Length,
                EdgeCount = edges.Count,
                WalkEdgeCount = walkEdgeCount,
                JumpEdgeCount = jumpEdgeCount,
                DropEdgeCount = dropEdgeCount,
                HintEdgeCount = hintEdgeCount,
                BuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                SurfaceSamplingMilliseconds = surfaceSamplingMilliseconds,
                AutoAnchorMilliseconds = autoAnchorMilliseconds,
                HintNodeMilliseconds = hintNodeMilliseconds,
                AutomaticEdgeMilliseconds = automaticEdgeMilliseconds,
                HintEdgeMilliseconds = hintEdgeMilliseconds,
                DropEdgeMilliseconds = dropEdgeMilliseconds,
            },
            Nodes = builtNodes,
            Edges = edges.ToArray(),
        };
    }

    public static BotNavigationAsset Build(SimpleLevel level, BotNavigationProfile profile, string? levelFingerprint = null)
    {
        return Build(level, BotNavigationProfiles.GetRepresentativeClassDefinition(profile).Id, levelFingerprint);
    }

    private static BotNavigationHintBuildMode ResolveHintBuildMode(BotNavigationHintAsset? hintAsset)
    {
        if (hintAsset is null)
        {
            return BotNavigationHintBuildMode.GeometryAugmented;
        }

        if (hintAsset.BuildMode == BotNavigationHintBuildMode.ExplicitGraph)
        {
            return BotNavigationHintBuildMode.ExplicitGraph;
        }

        // Older editor-authored files predate BuildMode but already mark auto-generated labels.
        return hintAsset.Links.Count > 0 && hintAsset.Nodes.Any(static node => node.AutoLabel)
            ? BotNavigationHintBuildMode.ExplicitGraph
            : BotNavigationHintBuildMode.GeometryAugmented;
    }

    private static IEnumerable<AnchorCandidate> EnumerateAnchors(SimpleLevel level, BotNavigationHintAsset? hintAsset)
    {
        var hasLocalSpawnOverride = HasHintNodeOverride(hintAsset, BotNavigationNodeKind.Spawn, null);
        var hasRedSpawnOverride = HasHintNodeOverride(hintAsset, BotNavigationNodeKind.Spawn, PlayerTeam.Red);
        var hasBlueSpawnOverride = HasHintNodeOverride(hintAsset, BotNavigationNodeKind.Spawn, PlayerTeam.Blue);
        var hasNeutralObjectiveOverride = HasHintNodeOverride(hintAsset, BotNavigationNodeKind.Objective, null);
        var hasRedObjectiveOverride = HasHintNodeOverride(hintAsset, BotNavigationNodeKind.Objective, PlayerTeam.Red);
        var hasBlueObjectiveOverride = HasHintNodeOverride(hintAsset, BotNavigationNodeKind.Objective, PlayerTeam.Blue);
        var hasCabinetOverride = hintAsset?.Nodes.Any(node => node.Kind == BotNavigationNodeKind.HealingCabinet) == true;

        if (!hasLocalSpawnOverride)
        {
            yield return new AnchorCandidate(level.LocalSpawn.X, level.LocalSpawn.Y, BotNavigationNodeKind.Spawn, null, "local-spawn");
        }

        if (!hasRedSpawnOverride)
        {
            foreach (var spawn in level.RedSpawns)
            {
                yield return new AnchorCandidate(spawn.X, spawn.Y, BotNavigationNodeKind.Spawn, PlayerTeam.Red, "red-spawn");
            }
        }

        if (!hasBlueSpawnOverride)
        {
            foreach (var spawn in level.BlueSpawns)
            {
                yield return new AnchorCandidate(spawn.X, spawn.Y, BotNavigationNodeKind.Spawn, PlayerTeam.Blue, "blue-spawn");
            }
        }

        foreach (var intelBase in level.IntelBases)
        {
            if ((intelBase.Team == PlayerTeam.Red && hasRedObjectiveOverride)
                || (intelBase.Team == PlayerTeam.Blue && hasBlueObjectiveOverride))
            {
                continue;
            }

            yield return new AnchorCandidate(intelBase.X, intelBase.Y, BotNavigationNodeKind.Objective, intelBase.Team, $"{intelBase.Team}-intel");
        }

        foreach (var roomObject in level.RoomObjects)
        {
            switch (roomObject.Type)
            {
                case RoomObjectType.HealingCabinet:
                    if (!hasCabinetOverride)
                    {
                        yield return new AnchorCandidate(
                            roomObject.CenterX,
                            roomObject.CenterY,
                            BotNavigationNodeKind.HealingCabinet,
                            InferFallbackCabinetTeam(level, roomObject.CenterX, roomObject.CenterY),
                            "cabinet");
                    }
                    break;
                case RoomObjectType.ArenaControlPoint:
                case RoomObjectType.CaptureZone:
                case RoomObjectType.ControlPoint:
                case RoomObjectType.Generator:
                    if (roomObject.Team == PlayerTeam.Red && hasRedObjectiveOverride)
                    {
                        break;
                    }

                    if (roomObject.Team == PlayerTeam.Blue && hasBlueObjectiveOverride)
                    {
                        break;
                    }

                    if (!roomObject.Team.HasValue && hasNeutralObjectiveOverride)
                    {
                        break;
                    }

                    yield return new AnchorCandidate(roomObject.CenterX, roomObject.CenterY, BotNavigationNodeKind.Objective, roomObject.Team, roomObject.Type.ToString());
                    break;
            }
        }
    }

    private static bool HasHintNodeOverride(BotNavigationHintAsset? hintAsset, BotNavigationNodeKind kind, PlayerTeam? team)
    {
        if (hintAsset is null)
        {
            return false;
        }

        return hintAsset.Nodes.Any(node => node.Kind == kind && node.Team == team);
    }

    private static PlayerTeam? InferFallbackCabinetTeam(SimpleLevel level, float x, float y)
    {
        var redDistanceSquared = GetNearestSpawnDistanceSquared(level.RedSpawns, x, y);
        var blueDistanceSquared = GetNearestSpawnDistanceSquared(level.BlueSpawns, x, y);
        if (!redDistanceSquared.HasValue && !blueDistanceSquared.HasValue)
        {
            return null;
        }

        if (!redDistanceSquared.HasValue)
        {
            return PlayerTeam.Blue;
        }

        if (!blueDistanceSquared.HasValue)
        {
            return PlayerTeam.Red;
        }

        if (MathF.Abs(redDistanceSquared.Value - blueDistanceSquared.Value) <= 1f)
        {
            return null;
        }

        return redDistanceSquared.Value < blueDistanceSquared.Value
            ? PlayerTeam.Red
            : PlayerTeam.Blue;
    }

    private static float? GetNearestSpawnDistanceSquared(IReadOnlyList<SpawnPoint> spawns, float x, float y)
    {
        float? bestDistanceSquared = null;
        for (var index = 0; index < spawns.Count; index += 1)
        {
            var spawn = spawns[index];
            var deltaX = spawn.X - x;
            var deltaY = spawn.Y - y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (!bestDistanceSquared.HasValue || distanceSquared < bestDistanceSquared.Value)
            {
                bestDistanceSquared = distanceSquared;
            }
        }

        return bestDistanceSquared;
    }

    private static IEnumerable<float> EnumerateSurfaceSamplePositions(LevelSolid solid, CharacterClassDefinition classDefinition, BotNavigationProfile profile)
    {
        var horizontalMargin = MathF.Max(
            MathF.Abs(classDefinition.CollisionLeft),
            MathF.Abs(classDefinition.CollisionRight)) + 4f;
        var minX = solid.Left + horizontalMargin;
        var maxX = solid.Right - horizontalMargin;
        if (minX > maxX)
        {
            yield break;
        }

        yield return minX;
        if (maxX > minX)
        {
            yield return maxX;
        }

        var spacing = profile switch
        {
            BotNavigationProfile.Light => BaseSampleSpacing * 0.85f,
            BotNavigationProfile.Heavy => BaseSampleSpacing * 1.1f,
            _ => BaseSampleSpacing,
        };

        for (var x = minX + spacing; x < maxX; x += spacing)
        {
            yield return x;
        }
    }

    private static bool TryProjectAnchor(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        AnchorCandidate anchor,
        out ProjectedAnchor projected)
    {
        projected = default;
        var bestDistance = float.PositiveInfinity;

        for (var surfaceIndex = 0; surfaceIndex < level.Solids.Count; surfaceIndex += 1)
        {
            var solid = level.Solids[surfaceIndex];
            if (anchor.X < solid.Left || anchor.X > solid.Right)
            {
                continue;
            }

            var projectedY = solid.Top - classDefinition.CollisionBottom;
            if (!CanOccupy(level, classDefinition, anchor.X, projectedY)
                || !HasGroundSupport(level, classDefinition, anchor.X, projectedY))
            {
                continue;
            }

            var distance = MathF.Abs(projectedY - anchor.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            projected = new ProjectedAnchor(surfaceIndex, anchor.X, projectedY);
        }

        return bestDistance < float.PositiveInfinity;
    }

    private static bool TryAddNode(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        IDictionary<string, MutableNode> mutableNodes,
        IDictionary<int, List<MutableNode>> nodesBySurface,
        int surfaceId,
        float x,
        float y,
        BotNavigationNodeKind kind,
        PlayerTeam? team,
        string label,
        bool requiresGroundSupport,
        bool preferLabel = false)
    {
        if (!CanOccupy(level, classDefinition, x, y)
            || requiresGroundSupport && !HasGroundSupport(level, classDefinition, x, y))
        {
            return false;
        }

        var key = $"{surfaceId}:{MathF.Round(x, 2):F2}:{MathF.Round(y, 2):F2}";
        if (mutableNodes.TryGetValue(key, out var existing))
        {
            existing.TryPromote(kind, team, label, requiresGroundSupport, preferLabel);
            return false;
        }

        var node = new MutableNode(surfaceId, x, y, kind, team, label, requiresGroundSupport);
        mutableNodes[key] = node;
        if (!nodesBySurface.TryGetValue(surfaceId, out var surfaceNodes))
        {
            surfaceNodes = new List<MutableNode>();
            nodesBySurface[surfaceId] = surfaceNodes;
        }

        surfaceNodes.Add(node);
        return true;
    }

    private static bool CanWalkBetween(SimpleLevel level, CharacterClassDefinition classDefinition, MutableNode from, MutableNode to)
    {
        if (from.SurfaceId != to.SurfaceId)
        {
            return false;
        }

        return BotNavigationMovementValidator.CanWalkDirectly(level, classDefinition, from.X, from.Y, to.X, to.Y);
    }

    private static int FindSupportingSurfaceId(SimpleLevel level, CharacterClassDefinition classDefinition, float x, float y)
    {
        var bestSurfaceId = -1;
        var bestDistance = float.PositiveInfinity;
        for (var surfaceIndex = 0; surfaceIndex < level.Solids.Count; surfaceIndex += 1)
        {
            var solid = level.Solids[surfaceIndex];
            if (x < solid.Left || x > solid.Right)
            {
                continue;
            }

            var projectedY = solid.Top - classDefinition.CollisionBottom;
            var distance = MathF.Abs(projectedY - y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestSurfaceId = surfaceIndex;
        }

        return bestSurfaceId >= 0
            ? bestSurfaceId
            : FindNearestSurfaceId(level, x, y);
    }

    private static int FindNearestSurfaceId(SimpleLevel level, float x, float y)
    {
        var bestSurfaceId = -1;
        var bestDistanceSquared = float.PositiveInfinity;
        for (var surfaceIndex = 0; surfaceIndex < level.Solids.Count; surfaceIndex += 1)
        {
            var solid = level.Solids[surfaceIndex];
            var clampedX = Math.Clamp(x, solid.Left, solid.Right);
            var surfaceY = solid.Top;
            var deltaX = clampedX - x;
            var deltaY = surfaceY - y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestSurfaceId = surfaceIndex;
        }

        return bestSurfaceId;
    }

    private static void TryAddHintEdges(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        BotNavigationHintAsset hintAsset,
        IReadOnlyDictionary<string, MutableNode> labeledNodes,
        ISet<long> edgeKeys,
        List<BotNavigationEdge> edges,
        ref int walkEdgeCount,
        ref int jumpEdgeCount,
        ref int dropEdgeCount,
        ref int hintEdgeCount)
    {
        foreach (var hintLink in hintAsset.Links)
        {
            if (!BotNavigationClasses.AppliesToProfile(hintLink.Classes, hintLink.Profiles, profile))
            {
                continue;
            }

            if (!labeledNodes.TryGetValue(hintLink.FromLabel, out var fromNode)
                || !labeledNodes.TryGetValue(hintLink.ToLabel, out var toNode))
            {
                continue;
            }

            _ = TryAddHintEdge(
                level,
                classDefinition,
                profile,
                fromNode,
                toNode,
                hintLink,
                edgeKeys,
                edges,
                ref walkEdgeCount,
                ref jumpEdgeCount,
                ref dropEdgeCount,
                ref hintEdgeCount);

            if (hintLink.Bidirectional)
            {
                _ = TryAddHintEdge(
                    level,
                    classDefinition,
                    profile,
                    toNode,
                    fromNode,
                    hintLink,
                    edgeKeys,
                    edges,
                    ref walkEdgeCount,
                    ref jumpEdgeCount,
                    ref dropEdgeCount,
                    ref hintEdgeCount);
            }
        }
    }

    private static bool TryAddHintEdge(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        MutableNode fromNode,
        MutableNode toNode,
        BotNavigationHintLink hintLink,
        ISet<long> edgeKeys,
        List<BotNavigationEdge> edges,
        ref int walkEdgeCount,
        ref int jumpEdgeCount,
        ref int dropEdgeCount,
        ref int hintEdgeCount)
    {
        if (!TryBuildHintEdge(level, classDefinition, profile, fromNode, toNode, hintLink, out var edge))
        {
            return false;
        }

        if (!TryAddEdge(edgeKeys, edges, edge))
        {
            return false;
        }

        hintEdgeCount += 1;

        switch (edge.Kind)
        {
            case BotNavigationTraversalKind.Walk:
                walkEdgeCount += 1;
                break;
            case BotNavigationTraversalKind.Jump:
                jumpEdgeCount += 1;
                break;
            case BotNavigationTraversalKind.Drop:
                dropEdgeCount += 1;
                break;
        }

        return true;
    }

    private static bool TryBuildHintEdge(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        MutableNode fromNode,
        MutableNode toNode,
        BotNavigationHintLink hintLink,
        out BotNavigationEdge edge)
    {
        edge = default!;
        var costMultiplier = Math.Clamp(hintLink.CostMultiplier, 0.1f, 4f);
        if (TryBuildRecordedHintEdge(
                level,
                classDefinition,
                profile,
                fromNode,
                toNode,
                hintLink,
                costMultiplier,
                out edge))
        {
            return true;
        }

        if (hintLink.Traversal is BotNavigationHintTraversalKind.Auto or BotNavigationHintTraversalKind.Walk)
        {
            if (CanWalkBetween(level, classDefinition, fromNode, toNode))
            {
                edge = CreateAuthoredWalkEdge(fromNode, toNode, costMultiplier);
                return true;
            }

            if (TryBuildGroundTapeForEitherTeam(
                    level,
                    classDefinition,
                    fromNode.X,
                    fromNode.Y,
                    toNode.X,
                    toNode.Y,
                    out var inputTape,
                    out var groundCost))
            {
                edge = new BotNavigationEdge
                {
                    FromNodeId = fromNode.Id,
                    ToNodeId = toNode.Id,
                    Kind = BotNavigationTraversalKind.Walk,
                    Cost = groundCost * costMultiplier,
                    InputTape = inputTape,
                };
                return true;
            }

            if (hintLink.Traversal == BotNavigationHintTraversalKind.Walk)
            {
                edge = CreateAuthoredWalkEdge(fromNode, toNode, costMultiplier);
                return true;
            }
        }

        if (hintLink.Traversal is BotNavigationHintTraversalKind.Auto or BotNavigationHintTraversalKind.Jump)
        {
            if (TryBuildHintJumpTapeForEitherTeam(
                    level,
                    classDefinition,
                    profile,
                    fromNode.X,
                    fromNode.Y,
                    toNode.X,
                    toNode.Y,
                    toNode.RequiresGroundSupport,
                    out var inputTape,
                    out var jumpCost))
            {
                edge = new BotNavigationEdge
                {
                    FromNodeId = fromNode.Id,
                    ToNodeId = toNode.Id,
                    Kind = BotNavigationTraversalKind.Jump,
                    Cost = jumpCost * costMultiplier,
                    InputTape = inputTape,
                };
                return true;
            }

            if (hintLink.Traversal == BotNavigationHintTraversalKind.Jump)
            {
                var approximateJumpTape = BotNavigationMovementValidator.BuildApproximateHintJumpTape(
                    classDefinition,
                    profile,
                    fromNode.X,
                    fromNode.Y,
                    toNode.X,
                    toNode.Y);
                edge = new BotNavigationEdge
                {
                    FromNodeId = fromNode.Id,
                    ToNodeId = toNode.Id,
                    Kind = BotNavigationTraversalKind.Jump,
                    Cost = GetTraversalTapeCost(approximateJumpTape) * costMultiplier,
                    InputTape = approximateJumpTape,
                };
                return true;
            }
        }

        if (hintLink.Traversal is BotNavigationHintTraversalKind.Auto or BotNavigationHintTraversalKind.Drop)
        {
            if (toNode.Y > fromNode.Y
                && MathF.Abs(toNode.X - fromNode.X) <= DropHorizontalTolerance
                && (toNode.Y - fromNode.Y) >= MinimumDropDistance
                && (toNode.Y - fromNode.Y) <= MaximumDropDistance
                && CanDropBetween(level, classDefinition, fromNode.X, fromNode.Y, toNode.Y))
            {
                edge = new BotNavigationEdge
                {
                    FromNodeId = fromNode.Id,
                    ToNodeId = toNode.Id,
                    Kind = BotNavigationTraversalKind.Drop,
                    Cost = MathF.Abs(toNode.Y - fromNode.Y) * costMultiplier,
                };
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildRecordedHintEdge(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        MutableNode fromNode,
        MutableNode toNode,
        BotNavigationHintLink hintLink,
        float costMultiplier,
        out BotNavigationEdge edge)
    {
        edge = default!;
        var representativeClass = classDefinition.Id;
        var recordedTraversal = hintLink.RecordedTraversals
            .FirstOrDefault(entry => entry.ClassId == representativeClass && entry.InputTape.Count > 0)
            ?? hintLink.RecordedTraversals.FirstOrDefault(entry => entry.ClassId.HasValue && BotNavigationProfiles.GetProfileForClass(entry.ClassId.Value) == profile && entry.InputTape.Count > 0)
            ?? hintLink.RecordedTraversals.FirstOrDefault(entry => entry.Profile == profile && entry.InputTape.Count > 0);
        if (recordedTraversal is null)
        {
            return false;
        }

        var recordedKind = ResolveRecordedTraversalKind(fromNode.Y, toNode.Y, recordedTraversal.InputTape);
        var recordedCost = GetTraversalTapeCost(recordedTraversal.InputTape);

        edge = new BotNavigationEdge
        {
            FromNodeId = fromNode.Id,
            ToNodeId = toNode.Id,
            Kind = hintLink.Traversal == BotNavigationHintTraversalKind.Drop
                ? BotNavigationTraversalKind.Drop
                : recordedKind,
            Cost = recordedCost * costMultiplier,
            InputTape = recordedTraversal.InputTape.ToArray(),
        };
        return true;
    }

    private static BotNavigationEdge CreateAuthoredWalkEdge(MutableNode fromNode, MutableNode toNode, float costMultiplier)
    {
        return new BotNavigationEdge
        {
            FromNodeId = fromNode.Id,
            ToNodeId = toNode.Id,
            Kind = BotNavigationTraversalKind.Walk,
            Cost = MathF.Abs(toNode.X - fromNode.X) * costMultiplier,
            InputTape = Array.Empty<BotNavigationInputFrame>(),
        };
    }

    private static float GetTraversalTapeCost(IReadOnlyList<BotNavigationInputFrame> tape)
    {
        if (tape.Count == 0)
        {
            return 0f;
        }

        var totalSeconds = 0d;
        for (var index = 0; index < tape.Count; index += 1)
        {
            var frame = tape[index];
            totalSeconds += frame.DurationSeconds > 0d
                ? frame.DurationSeconds
                : Math.Max(1, frame.Ticks) / (double)SimulationConfig.DefaultTicksPerSecond;
        }

        var totalTicks = Math.Max(1, (int)Math.Round(totalSeconds * SimulationConfig.DefaultTicksPerSecond, MidpointRounding.AwayFromZero));
        return totalTicks * 12f;
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

    private static bool TryAddHintNode(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        IDictionary<string, MutableNode> mutableNodes,
        IDictionary<int, List<MutableNode>> nodesBySurface,
        BotNavigationHintNode hintNode,
        bool preserveAuthoredPlacement,
        ref int nextVirtualSurfaceId)
    {
        if (preserveAuthoredPlacement
            && CanOccupy(level, classDefinition, hintNode.X, hintNode.Y))
        {
            var requiresGroundSupport = HasGroundSupport(level, classDefinition, hintNode.X, hintNode.Y);
            var surfaceId = requiresGroundSupport
                ? FindSupportingSurfaceId(level, classDefinition, hintNode.X, hintNode.Y)
                : nextVirtualSurfaceId--;
            if (TryAddNode(
                    level,
                    classDefinition,
                    mutableNodes,
                    nodesBySurface,
                    surfaceId,
                    hintNode.X,
                    hintNode.Y,
                    hintNode.Kind,
                    hintNode.Team,
                    hintNode.Label,
                    requiresGroundSupport,
                    preferLabel: true))
            {
                return true;
            }
        }

        if (TryAddNode(
                level,
                classDefinition,
                mutableNodes,
                nodesBySurface,
                FindNearestSurfaceId(level, hintNode.X, hintNode.Y),
                hintNode.X,
                hintNode.Y,
                hintNode.Kind,
                hintNode.Team,
                hintNode.Label,
                requiresGroundSupport: true,
                preferLabel: true))
        {
            return true;
        }

        return TryProjectAnchor(level, classDefinition, new AnchorCandidate(hintNode.X, hintNode.Y, hintNode.Kind, hintNode.Team, hintNode.Label), out var projected)
            && TryAddNode(
                level,
                classDefinition,
                mutableNodes,
                nodesBySurface,
                projected.SurfaceId,
                projected.X,
                projected.Y,
                hintNode.Kind,
                hintNode.Team,
                hintNode.Label,
                requiresGroundSupport: true,
                preferLabel: true);
    }

    private static void TryAddGroundTraversalEdges(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        MutableNode sourceNode,
        IReadOnlyList<MutableNode> allNodes,
        ISet<long> edgeKeys,
        List<BotNavigationEdge> edges,
        ref int walkEdgeCount)
    {
        var candidateTargets = SelectTraversalCandidates(
            sourceNode,
            allNodes,
            IsGroundTraversalCandidate,
            GetGroundTraversalCandidateScore,
            MaxGroundTraversalTargetsPerSourceNode);

        for (var index = 0; index < candidateTargets.Length; index += 1)
        {
            var candidate = candidateTargets[index];
            if (!TryBuildGroundTapeForEitherTeam(
                    level,
                    classDefinition,
                    sourceNode.X,
                    sourceNode.Y,
                    candidate.X,
                    candidate.Y,
                    out var inputTape,
                    out var cost))
            {
                continue;
            }

            if (!TryAddEdge(
                    edgeKeys,
                    edges,
                    new BotNavigationEdge
                    {
                        FromNodeId = sourceNode.Id,
                        ToNodeId = candidate.Id,
                        Kind = BotNavigationTraversalKind.Walk,
                        Cost = cost,
                        InputTape = inputTape,
                    }))
            {
                continue;
            }

            walkEdgeCount += 1;
        }
    }

    private static void TryAddJumpEdges(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        BotNavigationProfile profile,
        MutableNode sourceNode,
        IReadOnlyList<MutableNode> allNodes,
        JumpSearchEnvelope jumpSearchEnvelope,
        ISet<long> edgeKeys,
        List<BotNavigationEdge> edges,
        ref int jumpEdgeCount)
    {
        var candidateTargets = SelectTraversalCandidates(
            sourceNode,
            allNodes,
            static (source, candidate, envelope) => IsJumpCandidate(source, candidate, envelope),
            GetJumpCandidateScore,
            MaxJumpTargetsPerSourceNode,
            jumpSearchEnvelope);

        for (var index = 0; index < candidateTargets.Length; index += 1)
        {
            var candidate = candidateTargets[index];
            if (!TryBuildJumpTapeForEitherTeam(
                    level,
                    classDefinition,
                    profile,
                    sourceNode.X,
                    sourceNode.Y,
                    candidate.X,
                    candidate.Y,
                    out var inputTape,
                    out var cost))
            {
                continue;
            }

            if (!TryAddEdge(
                    edgeKeys,
                    edges,
                    new BotNavigationEdge
                    {
                        FromNodeId = sourceNode.Id,
                        ToNodeId = candidate.Id,
                        Kind = BotNavigationTraversalKind.Jump,
                        Cost = cost,
                        InputTape = inputTape,
                    }))
            {
                continue;
            }

            jumpEdgeCount += 1;
        }
    }

    private static bool IsJumpCandidate(MutableNode sourceNode, MutableNode candidate, JumpSearchEnvelope jumpSearchEnvelope)
    {
        if (candidate.SurfaceId == sourceNode.SurfaceId)
        {
            return false;
        }

        var horizontalDistance = MathF.Abs(candidate.X - sourceNode.X);
        if (horizontalDistance < MinimumJumpHorizontalDistance
            || horizontalDistance > jumpSearchEnvelope.MaxHorizontalDistance)
        {
            return false;
        }

        var riseDistance = sourceNode.Y - candidate.Y;
        if (riseDistance > jumpSearchEnvelope.MaxRiseDistance
            || riseDistance < -jumpSearchEnvelope.MaxDescentDistance)
        {
            return false;
        }

        return true;
    }

    private static bool IsGroundTraversalCandidate(MutableNode sourceNode, MutableNode candidate)
    {
        if (candidate.SurfaceId == sourceNode.SurfaceId)
        {
            return false;
        }

        var horizontalDistance = MathF.Abs(candidate.X - sourceNode.X);
        if (horizontalDistance < 12f || horizontalDistance > GroundTraversalMaxHorizontalDistance)
        {
            return false;
        }

        var riseDistance = sourceNode.Y - candidate.Y;
        if (riseDistance > GroundTraversalMaxRiseDistance
            || riseDistance < -GroundTraversalMaxDescentDistance)
        {
            return false;
        }

        return true;
    }

    private static float GetJumpCandidateScore(MutableNode sourceNode, MutableNode candidate)
    {
        var horizontalDistance = MathF.Abs(candidate.X - sourceNode.X);
        var verticalDistance = MathF.Abs(candidate.Y - sourceNode.Y);
        return horizontalDistance + (verticalDistance * 1.5f);
    }

    private static float GetGroundTraversalCandidateScore(MutableNode sourceNode, MutableNode candidate)
    {
        var horizontalDistance = MathF.Abs(candidate.X - sourceNode.X);
        var verticalDistance = MathF.Abs(candidate.Y - sourceNode.Y);
        return horizontalDistance + verticalDistance;
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
        tape = Array.Empty<BotNavigationInputFrame>();
        cost = 0f;

        if (BotNavigationMovementValidator.TryBuildGroundTape(
                level,
                classDefinition,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Red,
                out tape,
                out cost))
        {
            return true;
        }

        return BotNavigationMovementValidator.TryBuildGroundTape(
            level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            PlayerTeam.Blue,
            out tape,
            out cost);
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
        tape = Array.Empty<BotNavigationInputFrame>();
        cost = 0f;

        if (BotNavigationMovementValidator.TryBuildJumpTape(
                level,
                classDefinition,
                profile,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Red,
                out tape,
                out cost))
        {
            return true;
        }

        return BotNavigationMovementValidator.TryBuildJumpTape(
            level,
            classDefinition,
            profile,
            sourceX,
            sourceY,
            targetX,
            targetY,
            PlayerTeam.Blue,
            out tape,
            out cost);
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
        tape = Array.Empty<BotNavigationInputFrame>();
        cost = 0f;

        if (BotNavigationMovementValidator.TryBuildHintJumpTape(
                level,
                classDefinition,
                profile,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Red,
                requireGroundedArrival,
                out tape,
                out cost))
        {
            return true;
        }

        return BotNavigationMovementValidator.TryBuildHintJumpTape(
            level,
            classDefinition,
            profile,
            sourceX,
            sourceY,
            targetX,
            targetY,
            PlayerTeam.Blue,
            requireGroundedArrival,
            out tape,
            out cost);
    }

    private static bool TryValidateRecordedHintTapeForEitherTeam(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        bool requireGroundedArrival,
        IReadOnlyList<BotNavigationInputFrame> tape,
        out float cost,
        out BotNavigationTraversalKind traversalKind)
    {
        cost = 0f;
        traversalKind = BotNavigationTraversalKind.Walk;
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
                out cost,
                out traversalKind))
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
            out cost,
            out traversalKind);
    }

    private static MutableNode[] SelectTraversalCandidates(
        MutableNode sourceNode,
        IReadOnlyList<MutableNode> allNodes,
        Func<MutableNode, MutableNode, bool> predicate,
        Func<MutableNode, MutableNode, float> scoreSelector,
        int maxCandidateCount)
    {
        return allNodes
            .Where(candidate => predicate(sourceNode, candidate))
            .GroupBy(static candidate => candidate.SurfaceId)
            .Select(group => group
                .OrderByDescending(static candidate => candidate.Kind)
                .ThenBy(candidate => scoreSelector(sourceNode, candidate))
                .ThenBy(candidate => MathF.Abs(candidate.X - sourceNode.X))
                .First())
            .OrderBy(candidate => scoreSelector(sourceNode, candidate))
            .ThenByDescending(static candidate => candidate.Kind)
            .Take(maxCandidateCount)
            .ToArray();
    }

    private static MutableNode[] SelectTraversalCandidates<TContext>(
        MutableNode sourceNode,
        IReadOnlyList<MutableNode> allNodes,
        Func<MutableNode, MutableNode, TContext, bool> predicate,
        Func<MutableNode, MutableNode, float> scoreSelector,
        int maxCandidateCount,
        TContext context)
    {
        return allNodes
            .Where(candidate => predicate(sourceNode, candidate, context))
            .GroupBy(static candidate => candidate.SurfaceId)
            .Select(group => group
                .OrderByDescending(static candidate => candidate.Kind)
                .ThenBy(candidate => scoreSelector(sourceNode, candidate))
                .ThenBy(candidate => MathF.Abs(candidate.X - sourceNode.X))
                .First())
            .OrderBy(candidate => scoreSelector(sourceNode, candidate))
            .ThenByDescending(static candidate => candidate.Kind)
            .Take(maxCandidateCount)
            .ToArray();
    }

    private static void TryAddDropEdge(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        MutableNode sourceNode,
        IReadOnlyList<MutableNode> allNodes,
        ISet<long> edgeKeys,
        List<BotNavigationEdge> edges,
        ref int dropEdgeCount)
    {
        MutableNode? bestTarget = null;
        var bestDistance = float.PositiveInfinity;

        for (var index = 0; index < allNodes.Count; index += 1)
        {
            var candidate = allNodes[index];
            if (candidate.SurfaceId == sourceNode.SurfaceId)
            {
                continue;
            }

            var horizontalDistance = MathF.Abs(candidate.X - sourceNode.X);
            var verticalDistance = candidate.Y - sourceNode.Y;
            if (horizontalDistance > DropHorizontalTolerance
                || verticalDistance < MinimumDropDistance
                || verticalDistance > MaximumDropDistance)
            {
                continue;
            }

            if (!CanDropBetween(level, classDefinition, sourceNode.X, sourceNode.Y, candidate.Y))
            {
                continue;
            }

            var score = verticalDistance + horizontalDistance;
            if (score >= bestDistance)
            {
                continue;
            }

            bestDistance = score;
            bestTarget = candidate;
        }

        if (bestTarget is null)
        {
            return;
        }

        if (!TryAddEdge(
                edgeKeys,
                edges,
                new BotNavigationEdge
                {
                    FromNodeId = sourceNode.Id,
                    ToNodeId = bestTarget.Id,
                    Kind = BotNavigationTraversalKind.Drop,
                    Cost = MathF.Abs(bestTarget.Y - sourceNode.Y),
                }))
        {
            return;
        }

        dropEdgeCount += 1;
    }

    private static bool CanDropBetween(SimpleLevel level, CharacterClassDefinition classDefinition, float x, float fromY, float toY)
    {
        for (var y = fromY + HorizontalProbeStep; y <= toY; y += HorizontalProbeStep)
        {
            if (!CanOccupy(level, classDefinition, x, y))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanOccupy(SimpleLevel level, CharacterClassDefinition classDefinition, float x, float y)
    {
        var left = x + classDefinition.CollisionLeft;
        var top = y + classDefinition.CollisionTop;
        var right = x + classDefinition.CollisionRight;
        var bottom = y + classDefinition.CollisionBottom;

        if (left < 0f
            || top < 0f
            || right > level.Bounds.Width
            || bottom > level.Bounds.Height)
        {
            return false;
        }

        foreach (var solid in level.Solids)
        {
            if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
            {
                return false;
            }
        }

        foreach (var wall in level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (left < wall.Right && right > wall.Left && top < wall.Bottom && bottom > wall.Top)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasGroundSupport(SimpleLevel level, CharacterClassDefinition classDefinition, float x, float y)
    {
        return !CanOccupy(level, classDefinition, x, y + 1f);
    }

    private static bool TryAddEdge(ISet<long> edgeKeys, List<BotNavigationEdge> edges, BotNavigationEdge edge)
    {
        var edgeKey = GetEdgeKey(edge.FromNodeId, edge.ToNodeId);
        if (!edgeKeys.Add(edgeKey))
        {
            return false;
        }

        edges.Add(edge);
        return true;
    }

    private static long GetEdgeKey(int fromNodeId, int toNodeId)
    {
        return ((long)fromNodeId << 32) | (uint)toNodeId;
    }

    private sealed class MutableNode
    {
        public MutableNode(int surfaceId, float x, float y, BotNavigationNodeKind kind, PlayerTeam? team, string label, bool requiresGroundSupport)
        {
            SurfaceId = surfaceId;
            X = x;
            Y = y;
            Kind = kind;
            Team = team;
            Label = label;
            RequiresGroundSupport = requiresGroundSupport;
        }

        public int Id { get; set; }

        public int SurfaceId { get; }

        public float X { get; }

        public float Y { get; }

        public BotNavigationNodeKind Kind { get; private set; }

        public PlayerTeam? Team { get; private set; }

        public string Label { get; private set; }

        public bool RequiresGroundSupport { get; private set; }

        public void TryPromote(BotNavigationNodeKind kind, PlayerTeam? team, string label, bool requiresGroundSupport, bool preferLabel = false)
        {
            if (kind > Kind)
            {
                Kind = kind;
            }

            if (!Team.HasValue && team.HasValue)
            {
                Team = team;
            }

            if (requiresGroundSupport)
            {
                RequiresGroundSupport = true;
            }

            if (preferLabel && !string.IsNullOrWhiteSpace(label))
            {
                Label = label;
            }
            else if (label.Length > Label.Length)
            {
                Label = label;
            }
        }
    }

    private readonly record struct AnchorCandidate(float X, float Y, BotNavigationNodeKind Kind, PlayerTeam? Team, string Label);

    private readonly record struct ProjectedAnchor(int SurfaceId, float X, float Y);
}
