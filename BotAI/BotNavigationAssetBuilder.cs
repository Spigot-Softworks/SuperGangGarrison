using OpenGarrison.Core;
using System.Diagnostics;

namespace OpenGarrison.BotAI;

public static class BotNavigationAssetBuilder
{
    private const float BaseSampleSpacing = 96f;
    private const float HorizontalProbeStep = 8f;
    private const float MinimumJumpHorizontalDistance = 18f;
    private const int MaxJumpTargetsPerSourceNode = 8;
    private const float DropHorizontalTolerance = 18f;
    private const float MaximumDropDistance = 320f;
    private const float MinimumDropDistance = 18f;

    public static BotNavigationAsset Build(SimpleLevel level, BotNavigationProfile profile, string? levelFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(level);

        var stopwatch = Stopwatch.StartNew();
        var classDefinition = BotNavigationProfiles.GetRepresentativeClassDefinition(profile);
        var hintAsset = BotNavigationHintStore.Load(level);
        var candidateNodeCount = 0;
        var mutableNodes = new Dictionary<string, MutableNode>(StringComparer.Ordinal);
        var nodesBySurface = new Dictionary<int, List<MutableNode>>();

        for (var surfaceIndex = 0; surfaceIndex < level.Solids.Count; surfaceIndex += 1)
        {
            var solid = level.Solids[surfaceIndex];
            foreach (var sampleX in EnumerateSurfaceSamplePositions(solid, classDefinition, profile))
            {
                candidateNodeCount += 1;
                TryAddSurfaceNode(
                    level,
                    classDefinition,
                    mutableNodes,
                    nodesBySurface,
                    surfaceIndex,
                    sampleX,
                    solid.Top - classDefinition.CollisionBottom,
                    BotNavigationNodeKind.Surface,
                    string.Empty);
            }
        }

        foreach (var anchor in EnumerateAnchors(level))
        {
            candidateNodeCount += 1;
            if (!TryProjectAnchor(level, classDefinition, anchor, out var projected))
            {
                continue;
            }

            TryAddSurfaceNode(
                level,
                classDefinition,
                mutableNodes,
                nodesBySurface,
                projected.SurfaceId,
                projected.X,
                projected.Y,
                anchor.Kind,
                anchor.Label);
        }

        if (hintAsset is not null)
        {
            foreach (var hintNode in hintAsset.Nodes)
            {
                if (!AppliesToProfile(hintNode.Profiles, profile))
                {
                    continue;
                }

                TryAddHintNode(level, classDefinition, mutableNodes, nodesBySurface, hintNode);
            }
        }

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
        var jumpSearchEnvelope = BotNavigationMovementValidator.GetSearchEnvelope(profile, classDefinition);

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
                ref dropEdgeCount);
        }

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

        stopwatch.Stop();

        var builtNodes = orderedNodes
            .Select(static node => new BotNavigationNode
            {
                Id = node.Id,
                X = node.X,
                Y = node.Y,
                SurfaceId = node.SurfaceId,
                Kind = node.Kind,
                Label = node.Label,
            })
            .ToArray();

        return new BotNavigationAsset
        {
            FormatVersion = BotNavigationAssetStore.CurrentFormatVersion,
            LevelName = level.Name,
            MapAreaIndex = level.MapAreaIndex,
            Profile = profile,
            LevelFingerprint = levelFingerprint ?? BotNavigationLevelFingerprint.Compute(level),
            BuildStrategy = hintAsset is null
                ? BotNavigationBuildStrategy.GeometrySampledValidatedJumps
                : BotNavigationBuildStrategy.HintAugmentedValidatedJumps,
            BuiltUtc = DateTime.UtcNow,
            Stats = new BotNavigationBuildStats
            {
                SurfaceCount = level.Solids.Count,
                CandidateNodeCount = candidateNodeCount,
                NodeCount = builtNodes.Length,
                EdgeCount = edges.Count,
                WalkEdgeCount = walkEdgeCount,
                JumpEdgeCount = jumpEdgeCount,
                DropEdgeCount = dropEdgeCount,
                BuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            },
            Nodes = builtNodes,
            Edges = edges.ToArray(),
        };
    }

    private static IEnumerable<AnchorCandidate> EnumerateAnchors(SimpleLevel level)
    {
        yield return new AnchorCandidate(level.LocalSpawn.X, level.LocalSpawn.Y, BotNavigationNodeKind.Spawn, "local-spawn");

        foreach (var spawn in level.RedSpawns)
        {
            yield return new AnchorCandidate(spawn.X, spawn.Y, BotNavigationNodeKind.Spawn, "red-spawn");
        }

        foreach (var spawn in level.BlueSpawns)
        {
            yield return new AnchorCandidate(spawn.X, spawn.Y, BotNavigationNodeKind.Spawn, "blue-spawn");
        }

        foreach (var intelBase in level.IntelBases)
        {
            yield return new AnchorCandidate(intelBase.X, intelBase.Y, BotNavigationNodeKind.Objective, $"{intelBase.Team}-intel");
        }

        foreach (var roomObject in level.RoomObjects)
        {
            switch (roomObject.Type)
            {
                case RoomObjectType.HealingCabinet:
                    yield return new AnchorCandidate(roomObject.CenterX, roomObject.CenterY, BotNavigationNodeKind.HealingCabinet, "cabinet");
                    break;
                case RoomObjectType.ArenaControlPoint:
                case RoomObjectType.CaptureZone:
                case RoomObjectType.ControlPoint:
                case RoomObjectType.Generator:
                    yield return new AnchorCandidate(roomObject.CenterX, roomObject.CenterY, BotNavigationNodeKind.Objective, roomObject.Type.ToString());
                    break;
            }
        }
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

    private static bool TryAddSurfaceNode(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        IDictionary<string, MutableNode> mutableNodes,
        IDictionary<int, List<MutableNode>> nodesBySurface,
        int surfaceId,
        float x,
        float y,
        BotNavigationNodeKind kind,
        string label)
    {
        if (!CanOccupy(level, classDefinition, x, y)
            || !HasGroundSupport(level, classDefinition, x, y))
        {
            return false;
        }

        var key = $"{surfaceId}:{MathF.Round(x, 2):F2}:{MathF.Round(y, 2):F2}";
        if (mutableNodes.TryGetValue(key, out var existing))
        {
            existing.TryPromote(kind, label);
            return false;
        }

        var node = new MutableNode(surfaceId, x, y, kind, label);
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

        var minX = MathF.Min(from.X, to.X);
        var maxX = MathF.Max(from.X, to.X);
        for (var x = minX; x <= maxX; x += HorizontalProbeStep)
        {
            if (!CanOccupy(level, classDefinition, x, from.Y)
                || !HasGroundSupport(level, classDefinition, x, from.Y))
            {
                return false;
            }
        }

        return true;
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
        ref int dropEdgeCount)
    {
        foreach (var hintLink in hintAsset.Links)
        {
            if (!AppliesToProfile(hintLink.Profiles, profile))
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
                ref dropEdgeCount);

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
                    ref dropEdgeCount);
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
        ref int dropEdgeCount)
    {
        if (!TryBuildHintEdge(level, classDefinition, profile, fromNode, toNode, hintLink, out var edge))
        {
            return false;
        }

        if (!TryAddEdge(edgeKeys, edges, edge))
        {
            return false;
        }

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
        if (hintLink.Traversal is BotNavigationHintTraversalKind.Auto or BotNavigationHintTraversalKind.Walk)
        {
            if (CanWalkBetween(level, classDefinition, fromNode, toNode))
            {
                edge = new BotNavigationEdge
                {
                    FromNodeId = fromNode.Id,
                    ToNodeId = toNode.Id,
                    Kind = BotNavigationTraversalKind.Walk,
                    Cost = MathF.Abs(toNode.X - fromNode.X) * costMultiplier,
                };
                return true;
            }

            if (hintLink.Traversal == BotNavigationHintTraversalKind.Walk)
            {
                return false;
            }
        }

        if (hintLink.Traversal is BotNavigationHintTraversalKind.Auto or BotNavigationHintTraversalKind.Jump)
        {
            if (BotNavigationMovementValidator.TryBuildHintJumpTape(
                    level,
                    classDefinition,
                    profile,
                    fromNode.X,
                    fromNode.Y,
                    toNode.X,
                    toNode.Y,
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
                return false;
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

    private static bool TryAddHintNode(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        IDictionary<string, MutableNode> mutableNodes,
        IDictionary<int, List<MutableNode>> nodesBySurface,
        BotNavigationHintNode hintNode)
    {
        if (TryAddSurfaceNode(
                level,
                classDefinition,
                mutableNodes,
                nodesBySurface,
                FindNearestSurfaceId(level, hintNode.X, hintNode.Y),
                hintNode.X,
                hintNode.Y,
                hintNode.Kind,
                hintNode.Label))
        {
            return true;
        }

        return TryProjectAnchor(level, classDefinition, new AnchorCandidate(hintNode.X, hintNode.Y, hintNode.Kind, hintNode.Label), out var projected)
            && TryAddSurfaceNode(
                level,
                classDefinition,
                mutableNodes,
                nodesBySurface,
                projected.SurfaceId,
                projected.X,
                projected.Y,
                hintNode.Kind,
                hintNode.Label);
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
        var candidateTargets = allNodes
            .Where(candidate => IsJumpCandidate(sourceNode, candidate, jumpSearchEnvelope))
            .OrderBy(candidate => GetJumpCandidateScore(sourceNode, candidate))
            .Take(MaxJumpTargetsPerSourceNode)
            .ToArray();

        for (var index = 0; index < candidateTargets.Length; index += 1)
        {
            var candidate = candidateTargets[index];
            if (!BotNavigationMovementValidator.TryBuildJumpTape(
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

    private static float GetJumpCandidateScore(MutableNode sourceNode, MutableNode candidate)
    {
        var horizontalDistance = MathF.Abs(candidate.X - sourceNode.X);
        var verticalDistance = MathF.Abs(candidate.Y - sourceNode.Y);
        return horizontalDistance + (verticalDistance * 1.5f);
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

    private static bool TryAddEdge(ISet<long> edgeKeys, ICollection<BotNavigationEdge> edges, BotNavigationEdge edge)
    {
        var edgeKey = GetEdgeKey(edge.FromNodeId, edge.ToNodeId);
        if (!edgeKeys.Add(edgeKey))
        {
            return false;
        }

        edges.Add(edge);
        return true;
    }

    private static bool AppliesToProfile(IReadOnlyList<BotNavigationProfile> profiles, BotNavigationProfile profile)
    {
        if (profiles.Count == 0)
        {
            return true;
        }

        for (var index = 0; index < profiles.Count; index += 1)
        {
            if (profiles[index] == profile)
            {
                return true;
            }
        }

        return false;
    }

    private static long GetEdgeKey(int fromNodeId, int toNodeId)
    {
        return ((long)fromNodeId << 32) | (uint)toNodeId;
    }

    private sealed class MutableNode
    {
        public MutableNode(int surfaceId, float x, float y, BotNavigationNodeKind kind, string label)
        {
            SurfaceId = surfaceId;
            X = x;
            Y = y;
            Kind = kind;
            Label = label;
        }

        public int Id { get; set; }

        public int SurfaceId { get; }

        public float X { get; }

        public float Y { get; }

        public BotNavigationNodeKind Kind { get; private set; }

        public string Label { get; private set; }

        public void TryPromote(BotNavigationNodeKind kind, string label)
        {
            if (kind > Kind)
            {
                Kind = kind;
            }

            if (label.Length > Label.Length)
            {
                Label = label;
            }
        }
    }

    private readonly record struct AnchorCandidate(float X, float Y, BotNavigationNodeKind Kind, string Label);

    private readonly record struct ProjectedAnchor(int SurfaceId, float X, float Y);
}
