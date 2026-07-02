using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

public sealed record BotNavigationModernGraphDeleteResult(
    BotNavigationAsset Asset,
    int RemovedNodes,
    int RemovedEdges,
    int AddedEdges);

public static class BotNavigationModernGraphEditor
{
    private const int GridStep = 6;
    private const int ConnectionClearanceHeight = 12;
    private const float MaximumDirectedTraversalCost = 168f;
    private const float MaximumSlope = 4f;
    private const float DropTraversalHorizontalTolerance = 18f;
    private const float DropTraversalMinimumDistance = 18f;

    public static BotNavigationAsset Normalize(BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return WithNodesAndEdges(asset, asset.Nodes.ToArray(), asset.Edges);
    }

    public static BotNavigationAsset AddSurfaceNode(BotNavigationAsset asset, int surfaceId, float x, float y)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var nextNodeId = asset.Nodes.Count == 0
            ? 0
            : asset.Nodes.Max(static node => node.Id) + 1;
        var normalizedSurfaceId = surfaceId >= 0
            ? surfaceId
            : asset.Nodes.Count == 0
                ? 0
                : asset.Nodes.Max(static node => node.SurfaceId) + 1;
        var nodes = asset.Nodes
            .Concat(
            [
                new BotNavigationNode
                {
                    Id = nextNodeId,
                    X = x,
                    Y = y,
                    SurfaceId = normalizedSurfaceId,
                    Kind = BotNavigationNodeKind.Surface,
                    RequiresGroundSupport = true,
                },
            ])
            .OrderBy(static node => node.Id)
            .ToArray();
        return WithNodesAndEdges(asset, nodes, asset.Edges);
    }

    public static BotNavigationAsset MoveNode(SimpleLevel level, BotNavigationAsset asset, int nodeId, float x, float y)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(asset);

        var found = false;
        var nodes = asset.Nodes
            .Select(node =>
            {
                if (node.Id != nodeId)
                {
                    return node;
                }

                found = true;
                return new BotNavigationNode
                {
                    Id = node.Id,
                    X = x,
                    Y = y,
                    SurfaceId = node.SurfaceId,
                    Kind = node.Kind,
                    Team = node.Team,
                    Label = node.Label,
                    RequiresGroundSupport = node.RequiresGroundSupport,
                    ReverseOnlyBlockedFromNodeIds = node.ReverseOnlyBlockedFromNodeIds,
                };
            })
            .ToArray();
        if (!found)
        {
            return asset;
        }

        var nodeById = nodes.ToDictionary(static node => node.Id);
        var rebuiltEdges = new List<BotNavigationEdge>(asset.Edges.Count);
        for (var index = 0; index < asset.Edges.Count; index += 1)
        {
            var edge = asset.Edges[index];
            if (edge.FromNodeId != nodeId && edge.ToNodeId != nodeId)
            {
                rebuiltEdges.Add(edge);
                continue;
            }

            if (!nodeById.TryGetValue(edge.FromNodeId, out var fromNode)
                || !nodeById.TryGetValue(edge.ToNodeId, out var toNode))
            {
                continue;
            }

            if (TryCreateDirectedEdge(level, fromNode, toNode, out var rebuiltEdge))
            {
                rebuiltEdges.Add(rebuiltEdge);
            }
        }

        return WithNodesAndEdges(asset, nodes, rebuiltEdges);
    }

    public static bool TryBuildDirectedEdge(
        SimpleLevel level,
        BotNavigationAsset asset,
        int fromNodeId,
        int toNodeId,
        out BotNavigationEdge edge,
        out string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(asset);

        edge = default!;
        failureMessage = string.Empty;

        if (fromNodeId == toNodeId)
        {
            failureMessage = "graph edges must connect two different nodes";
            return false;
        }

        if (!TryGetNode(asset.Nodes, fromNodeId, out var fromNode))
        {
            failureMessage = $"graph node {fromNodeId} was not found";
            return false;
        }

        if (!TryGetNode(asset.Nodes, toNodeId, out var toNode))
        {
            failureMessage = $"graph node {toNodeId} was not found";
            return false;
        }

        if (asset.Edges.Any(existing => existing.FromNodeId == fromNodeId && existing.ToNodeId == toNodeId))
        {
            failureMessage = $"graph edge {fromNodeId}->{toNodeId} already exists";
            return false;
        }

        if (!TryCreateDirectedEdge(level, fromNode, toNode, out edge))
        {
            failureMessage = "edge failed slope/clearance/cost validation; add a helper node or move the seam closer";
            return false;
        }

        return true;
    }

    public static BotNavigationAsset UpsertDirectedEdge(BotNavigationAsset asset, BotNavigationEdge edge)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(edge);

        var edges = asset.Edges
            .Where(existing => existing.FromNodeId != edge.FromNodeId || existing.ToNodeId != edge.ToNodeId)
            .Concat([edge])
            .ToArray();
        return WithNodesAndEdges(asset, asset.Nodes.ToArray(), edges);
    }

    public static BotNavigationAsset RemoveDirectedEdge(BotNavigationAsset asset, int fromNodeId, int toNodeId, out bool removed)
    {
        ArgumentNullException.ThrowIfNull(asset);

        removed = false;
        var edges = new List<BotNavigationEdge>(asset.Edges.Count);
        for (var index = 0; index < asset.Edges.Count; index += 1)
        {
            var edge = asset.Edges[index];
            if (edge.FromNodeId == fromNodeId && edge.ToNodeId == toNodeId)
            {
                removed = true;
                continue;
            }

            edges.Add(edge);
        }

        return removed
            ? WithNodesAndEdges(asset, asset.Nodes.ToArray(), edges)
            : asset;
    }

    public static BotNavigationModernGraphDeleteResult DeleteNodeAndReconnect(SimpleLevel level, BotNavigationAsset asset, int nodeId)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(asset);

        if (!TryGetNode(asset.Nodes, nodeId, out var removedNode))
        {
            return new BotNavigationModernGraphDeleteResult(asset, RemovedNodes: 0, RemovedEdges: 0, AddedEdges: 0);
        }

        var survivingNodes = asset.Nodes
            .Where(node => node.Id != nodeId)
            .OrderBy(static node => node.Id)
            .ToArray();
        var survivingNodeIds = survivingNodes
            .Select(static node => node.Id)
            .ToHashSet();
        var remainingEdges = asset.Edges
            .Where(edge => edge.FromNodeId != nodeId && edge.ToNodeId != nodeId)
            .ToList();
        var removedEdges = asset.Edges.Count(edge => edge.FromNodeId == nodeId || edge.ToNodeId == nodeId);
        if (survivingNodes.Length == 0)
        {
            return new BotNavigationModernGraphDeleteResult(
                WithNodesAndEdges(asset, Array.Empty<BotNavigationNode>(), Array.Empty<BotNavigationEdge>()),
                RemovedNodes: 1,
                RemovedEdges: removedEdges,
                AddedEdges: 0);
        }

        var incomingNodeIds = asset.Edges
            .Where(edge => edge.ToNodeId == nodeId && survivingNodeIds.Contains(edge.FromNodeId))
            .Select(static edge => edge.FromNodeId)
            .Distinct()
            .ToArray();
        var outgoingNodeIds = asset.Edges
            .Where(edge => edge.FromNodeId == nodeId && survivingNodeIds.Contains(edge.ToNodeId))
            .Select(static edge => edge.ToNodeId)
            .Distinct()
            .ToArray();
        var nodesById = survivingNodes.ToDictionary(static node => node.Id);
        var candidateNodesByDistance = survivingNodes
            .OrderBy(node => DistanceSquared(node.X, node.Y, removedNode.X, removedNode.Y))
            .ThenBy(static node => node.Id)
            .ToArray();
        var edgeKeys = new HashSet<long>(remainingEdges.Select(static edge => GetEdgeKey(edge.FromNodeId, edge.ToNodeId)));
        var addedEdges = 0;

        for (var incomingIndex = 0; incomingIndex < incomingNodeIds.Length; incomingIndex += 1)
        {
            if (!nodesById.TryGetValue(incomingNodeIds[incomingIndex], out var fromNode))
            {
                continue;
            }

            BotNavigationEdge? bestEdge = null;
            var bestScore = float.PositiveInfinity;
            for (var outgoingIndex = 0; outgoingIndex < outgoingNodeIds.Length; outgoingIndex += 1)
            {
                if (!nodesById.TryGetValue(outgoingNodeIds[outgoingIndex], out var toNode)
                    || toNode.Id == fromNode.Id
                    || edgeKeys.Contains(GetEdgeKey(fromNode.Id, toNode.Id))
                    || !TryCreateDirectedEdge(level, fromNode, toNode, out var reconnectEdge))
                {
                    continue;
                }

                var score = reconnectEdge.Cost + DistanceSquared(toNode.X, toNode.Y, removedNode.X, removedNode.Y);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestEdge = reconnectEdge;
            }

            if (bestEdge is null)
            {
                for (var candidateIndex = 0; candidateIndex < candidateNodesByDistance.Length; candidateIndex += 1)
                {
                    var candidateNode = candidateNodesByDistance[candidateIndex];
                    if (candidateNode.Id == fromNode.Id
                        || edgeKeys.Contains(GetEdgeKey(fromNode.Id, candidateNode.Id))
                        || !TryCreateDirectedEdge(level, fromNode, candidateNode, out var fallbackEdge))
                    {
                        continue;
                    }

                    bestEdge = fallbackEdge;
                    break;
                }
            }

            if (bestEdge is null)
            {
                continue;
            }

            remainingEdges.Add(bestEdge);
            edgeKeys.Add(GetEdgeKey(bestEdge.FromNodeId, bestEdge.ToNodeId));
            addedEdges += 1;
        }

        return new BotNavigationModernGraphDeleteResult(
            WithNodesAndEdges(asset, survivingNodes, remainingEdges),
            RemovedNodes: 1,
            RemovedEdges: removedEdges,
            AddedEdges: addedEdges);
    }

    private static BotNavigationAsset WithNodesAndEdges(
        BotNavigationAsset asset,
        IReadOnlyList<BotNavigationNode> nodes,
        IEnumerable<BotNavigationEdge> edges)
    {
        var nodesById = nodes.ToDictionary(static node => node.Id);
        var survivingNodeIds = nodesById.Keys.ToHashSet();
        var normalizedNodes = nodes
            .OrderBy(static node => node.Id)
            .Select(node => new BotNavigationNode
            {
                Id = node.Id,
                X = node.X,
                Y = node.Y,
                SurfaceId = node.SurfaceId,
                Kind = node.Kind,
                Team = node.Team,
                Label = node.Label,
                RequiresGroundSupport = node.RequiresGroundSupport,
                ReverseOnlyBlockedFromNodeIds = node.ReverseOnlyBlockedFromNodeIds
                    .Where(survivingNodeIds.Contains)
                    .Distinct()
                    .Order()
                    .ToArray(),
            })
            .ToArray();

        var edgeKeys = new HashSet<long>();
        var normalizedEdges = new List<BotNavigationEdge>();
        foreach (var edge in edges)
        {
            if (!nodesById.TryGetValue(edge.FromNodeId, out var fromNode)
                || !nodesById.TryGetValue(edge.ToNodeId, out var toNode)
                || !edgeKeys.Add(GetEdgeKey(edge.FromNodeId, edge.ToNodeId)))
            {
                continue;
            }

            normalizedEdges.Add(new BotNavigationEdge
            {
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Kind = ResolveTraversalKind(fromNode, toNode),
                Cost = Math.Max(GridStep, DistanceBetween(fromNode.X, fromNode.Y, toNode.X, toNode.Y)),
                InputTape = edge.InputTape,
            });
        }

        var stats = asset.Stats;
        return new BotNavigationAsset
        {
            FormatVersion = asset.FormatVersion,
            LevelName = asset.LevelName,
            MapAreaIndex = asset.MapAreaIndex,
            ClassId = asset.ClassId,
            Profile = asset.Profile,
            LevelFingerprint = asset.LevelFingerprint,
            BuildStrategy = asset.BuildStrategy,
            BuiltUtc = DateTime.UtcNow,
            Stats = new BotNavigationBuildStats
            {
                SurfaceCount = stats.SurfaceCount,
                CandidateNodeCount = stats.CandidateNodeCount,
                SurfaceSampleNodeCount = stats.SurfaceSampleNodeCount,
                AutoAnchorNodeCount = normalizedNodes.Count(static node => node.Kind != BotNavigationNodeKind.Surface),
                HintNodeCount = stats.HintNodeCount,
                NodeCount = normalizedNodes.Length,
                EdgeCount = normalizedEdges.Count,
                WalkEdgeCount = normalizedEdges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Walk),
                JumpEdgeCount = normalizedEdges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Jump),
                DropEdgeCount = normalizedEdges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Drop),
                HintEdgeCount = stats.HintEdgeCount,
                BuildMilliseconds = stats.BuildMilliseconds,
                SurfaceSamplingMilliseconds = stats.SurfaceSamplingMilliseconds,
                AutoAnchorMilliseconds = stats.AutoAnchorMilliseconds,
                HintNodeMilliseconds = stats.HintNodeMilliseconds,
                AutomaticEdgeMilliseconds = stats.AutomaticEdgeMilliseconds,
                HintEdgeMilliseconds = stats.HintEdgeMilliseconds,
                DropEdgeMilliseconds = stats.DropEdgeMilliseconds,
            },
            Nodes = normalizedNodes,
            Edges = normalizedEdges,
        };
    }

    private static bool TryCreateDirectedEdge(SimpleLevel level, BotNavigationNode fromNode, BotNavigationNode toNode, out BotNavigationEdge edge)
    {
        edge = default!;
        if (!CanConnectDirected(level, fromNode, toNode))
        {
            return false;
        }

        edge = new BotNavigationEdge
        {
            FromNodeId = fromNode.Id,
            ToNodeId = toNode.Id,
            Kind = ResolveTraversalKind(fromNode, toNode),
            Cost = Math.Max(GridStep, DistanceBetween(fromNode.X, fromNode.Y, toNode.X, toNode.Y)),
        };
        return true;
    }

    private static bool CanConnectDirected(SimpleLevel level, BotNavigationNode fromNode, BotNavigationNode toNode)
    {
        var horizontalDistance = MathF.Abs(toNode.X - fromNode.X);
        if (horizontalDistance < GridStep)
        {
            return false;
        }

        if (ComputeDirectedTraversalCost(fromNode.X, fromNode.Y, toNode.X, toNode.Y) > MaximumDirectedTraversalCost)
        {
            return false;
        }

        var slope = (fromNode.Y - toNode.Y) / (toNode.X - fromNode.X);
        if (slope > MaximumSlope || slope < -MaximumSlope)
        {
            return false;
        }

        return !LineHitsSolid(level, fromNode.X, fromNode.Y - 1f, fromNode.X, fromNode.Y - ConnectionClearanceHeight)
            && !LineHitsSolid(level, toNode.X, toNode.Y - 1f, toNode.X, toNode.Y - ConnectionClearanceHeight)
            && !LineHitsSolid(level, fromNode.X, fromNode.Y - ConnectionClearanceHeight, toNode.X, toNode.Y - ConnectionClearanceHeight);
    }

    private static bool TryGetNode(IReadOnlyList<BotNavigationNode> nodes, int nodeId, out BotNavigationNode node)
    {
        for (var index = 0; index < nodes.Count; index += 1)
        {
            if (nodes[index].Id == nodeId)
            {
                node = nodes[index];
                return true;
            }
        }

        node = default!;
        return false;
    }

    private static float ComputeDirectedTraversalCost(float fromX, float fromY, float toX, float toY)
    {
        return MathF.Abs(fromX - toX) + (MathF.Max(0f, fromY - toY - 18f) * 1.2f);
    }

    private static BotNavigationTraversalKind ResolveTraversalKind(BotNavigationNode fromNode, BotNavigationNode toNode)
    {
        var horizontalDistance = MathF.Abs(toNode.X - fromNode.X);
        var verticalDistance = toNode.Y - fromNode.Y;
        if (fromNode.SurfaceId == toNode.SurfaceId || MathF.Abs(verticalDistance) <= GridStep)
        {
            return BotNavigationTraversalKind.Walk;
        }

        if (verticalDistance >= DropTraversalMinimumDistance && horizontalDistance <= DropTraversalHorizontalTolerance)
        {
            return BotNavigationTraversalKind.Drop;
        }

        return BotNavigationTraversalKind.Jump;
    }

    private static bool LineHitsSolid(SimpleLevel level, float startX, float startY, float endX, float endY)
    {
        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0.001f)
        {
            return false;
        }

        var directionX = deltaX / distance;
        var directionY = deltaY / distance;
        for (var index = 0; index < level.Solids.Count; index += 1)
        {
            var solid = level.Solids[index];
            if (GetRayIntersectionDistanceWithRectangle(
                    startX,
                    startY,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance).HasValue)
            {
                return true;
            }
        }

        return false;
    }

    private static float? GetRayIntersectionDistanceWithRectangle(
        float originX,
        float originY,
        float directionX,
        float directionY,
        float left,
        float top,
        float right,
        float bottom,
        float maxDistance)
    {
        var inverseX = MathF.Abs(directionX) < 0.0001f ? float.PositiveInfinity : 1f / directionX;
        var inverseY = MathF.Abs(directionY) < 0.0001f ? float.PositiveInfinity : 1f / directionY;
        var tx1 = (left - originX) * inverseX;
        var tx2 = (right - originX) * inverseX;
        var ty1 = (top - originY) * inverseY;
        var ty2 = (bottom - originY) * inverseY;
        var tMin = MathF.Max(MathF.Min(tx1, tx2), MathF.Min(ty1, ty2));
        var tMax = MathF.Min(MathF.Max(tx1, tx2), MathF.Max(ty1, ty2));
        if (tMax < 0f || tMin > tMax)
        {
            return null;
        }

        var distance = tMin < 0f ? 0f : tMin;
        return distance <= maxDistance ? distance : null;
    }

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static long GetEdgeKey(int fromNodeId, int toNodeId)
    {
        return ((long)fromNodeId << 32) | (uint)toNodeId;
    }
}
