using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Builds the alpha OG2 navigation graph directly from the imported level geometry.
///
/// This builder intentionally does not read or write the legacy BotNavigationAsset
/// format and never runs a full PlayerEntity simulation for every candidate link.
/// The resulting graph is compact enough to build once per SimpleLevel and share
/// between every bot in the match.
/// </summary>
public static class Og2NavigationGraphBuilder
{
    // Bump whenever graph-generation behavior changes. Runtime steering
    // changes intentionally do not invalidate the graph cache.
    public const string GeneratorFingerprint = "og2-contact-20260731-v50-merge-static-class-edges";

    private static readonly ConditionalWeakTable<SimpleLevel, StaticNavigationBlockers> StaticBlockerCache = new();

    private const float SurfaceMergeVerticalTolerance = 2f;
    private const float SurfaceMergeGapTolerance = 2f;
    private const float MinimumSurfaceWidth = 6f;
    private const float MinimumRawSurfaceWidth = 1f;
    private const float MinimumSurfaceClearance = 2f;
    private const float MaximumNodeSpacing = 192f;
    private const float MaximumJumpHorizontalDistance = 192f;
    private const float MaximumCertifiedTallJumpHorizontalDistance = 144f;
    // Stair links are local transitions between adjacent platforms. Keeping
    // this bounded prevents the transition pass from degenerating into an
    // all-pairs scan while still covering the widest stock-map stair tread
    // observed in the OG2 maps.
    private const float MaximumStairHorizontalDistance = 320f;
    private const float MaximumStairRise = 96f;
    // A walk edge is a local OG2 step-chain link, not a substitute for a
    // jump or a route across an entire stairwell. The previous 240 px value
    // admitted false relays that the runtime could not execute. Larger
    // vertical changes must be represented by separately certified jumps.
    private const float MaximumStepProfileExcursion = MaximumStairRise;
    private const float MaximumFallHorizontalDistance = 240f;
    private const float MaximumFallDistance = 600f;
    private const float StandardJumpRise = 72f;
    private const float MaximumJumpRise = 96f;
    private const float StepHeight = 6f;
    private const float StepProfileSampleSpacing = 6f;
    private const int StepProfileExtraStateSteps = 2;
    private const float LandingVerticalTolerance = 8f;
    private const float MaximumJumpRunUpSeconds = 24f / 30f;
    private const float JumpLandingHorizontalSafetyMargin = 24f;
    private const float MaximumAnchorHorizontalDistance = 360f;
    private const float MaximumAnchorVerticalDistance = 240f;
    private const float MaximumSurfaceCandidateHorizontalDistance = 240f;
    private const float CollisionSampleSpacing = 24f;
    private const int JumpClearanceSamples = 18;
    private const int FallClearanceSamples = 12;
    private const float ObjectiveAnchorCost = 12f;
    private const float SpawnAnchorCost = 8f;
    private const float VerticalAscentPenalty = 220f;
    private static readonly bool EnableTallJumpCertification =
        Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CERTIFY_TALL_JUMPS") is "1" or "true" or "TRUE";

    public static NavGraph Build(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_GRAPH") is "1" or "true" or "TRUE")
        {
            return Og2LocalContactGraphBuilder.Build(level);
        }

        var stopwatch = Stopwatch.StartNew();
        var envelope = NavigationEnvelope.Create();
        var surfaces = BuildSurfaces(level, envelope);
        var nodes = new List<NavNode>(surfaces.Count * 4 + level.RoomObjects.Count + level.RedSpawns.Count + level.BlueSpawns.Count);
        var surfaceNodeIndices = new List<int[]>(surfaces.Count);

        foreach (var surface in surfaces)
        {
            surfaceNodeIndices.Add(AddSurfaceNodes(nodes, surface, envelope));
        }

        var spawnAnchors = new List<NavSpawnAnchor>(level.RedSpawns.Count + level.BlueSpawns.Count);
        var anchorRecords = AddAnchors(level, nodes, spawnAnchors);
        var adjacency = CreateAdjacency(nodes.Count);
        var edgeKeys = new HashSet<NavigationEdgeKey>();

        AddSurfaceWalkEdges(surfaces, surfaceNodeIndices, nodes, adjacency, edgeKeys);
        // Fall validation uses a short, quantized candidate sweep around the
        // source/landing corridor. Keeping this local is important: a global
        // list of every wall edge turns a linear-size graph build into a
        // geometry-pair explosion on larger stock maps.
        var bendCandidates = Array.Empty<float>();
        AddSurfaceTransitionEdges(level, surfaces, surfaceNodeIndices, nodes, envelope, bendCandidates, adjacency, edgeKeys);
        AddAnchorEdges(level, anchorRecords, surfaces, surfaceNodeIndices, nodes, envelope, adjacency, edgeKeys);
        AddRecoveryBridgeEdges(level, anchorRecords, nodes, envelope, adjacency, edgeKeys);

        var graph = new NavGraph(
            nodes.ToArray(),
            adjacency,
            level.Name,
            level.Mode,
            spawnAnchors,
            isOg2Alpha: true);

        var trace = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_BUILD_TRACE");
        if (trace is "1" or "true" or "TRUE")
        {
            Console.WriteLine(
                $"[botbrain] alpha-nav build level={level.Name} area={level.MapAreaIndex} " +
                $"surfaces={surfaces.Count} nodes={graph.NodeCount} edges={CountEdges(adjacency)} " +
                $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.00}");
        }

        return graph;
    }

    private static List<NavSurface> BuildSurfaces(SimpleLevel level, NavigationEnvelope envelope)
    {
        var solids = level.Solids
            .Where(static solid => solid.Width >= MinimumSurfaceWidth && solid.Height > 0f)
            .OrderBy(static solid => solid.Bottom)
            .ThenBy(static solid => solid.Left)
            .ToArray();
        var raw = new List<SurfaceInterval>(solids.Length);

        for (var solidIndex = 0; solidIndex < solids.Length; solidIndex += 1)
        {
            var solid = solids[solidIndex];
            var segments = new List<SurfaceInterval>
            {
                new(solid.Left, solid.Right, solid.Top, false),
            };

            for (var blockerIndex = 0; blockerIndex < solids.Length && segments.Count > 0; blockerIndex += 1)
            {
                if (blockerIndex == solidIndex)
                {
                    continue;
                }

                var blocker = solids[blockerIndex];
                if (blocker.Bottom > solid.Top + SurfaceMergeVerticalTolerance)
                {
                    break;
                }

                if (MathF.Abs(blocker.Bottom - solid.Top) > SurfaceMergeVerticalTolerance)
                {
                    continue;
                }

                SubtractInterval(segments, blocker.Left, blocker.Right);
            }

            // Keep narrow exposed lips until the envelope pass. They are the
            // individual steps of several stock-map staircases and cannot be
            // discarded before step-up topology is reconstructed.
            raw.AddRange(segments.Where(static segment => segment.Right - segment.Left >= MinimumRawSurfaceWidth));
        }

        foreach (var roomObject in level.RoomObjects)
        {
            if (roomObject.Type == RoomObjectType.DropdownPlatform
                && roomObject.Width >= MinimumSurfaceWidth
                && roomObject.Height >= 0f)
            {
                raw.Add(new SurfaceInterval(roomObject.Left, roomObject.Right, roomObject.Top, true));
            }
        }

        raw.Sort(static (left, right) =>
        {
            var height = left.Top.CompareTo(right.Top);
            return height != 0 ? height : left.Left.CompareTo(right.Left);
        });

        var merged = new List<SurfaceInterval>(raw.Count);
        foreach (var interval in raw)
        {
            if (merged.Count == 0)
            {
                merged.Add(interval);
                continue;
            }

            var previous = merged[^1];
            if (previous.IsDropdown == interval.IsDropdown
                && MathF.Abs(previous.Top - interval.Top) <= SurfaceMergeVerticalTolerance
                && interval.Left <= previous.Right + SurfaceMergeGapTolerance)
            {
                merged[^1] = previous with
                {
                    Right = MathF.Max(previous.Right, interval.Right),
                };
                continue;
            }

            merged.Add(interval);
        }

        var surfaces = new List<NavSurface>(merged.Count);
        foreach (var interval in merged)
        {
            // A standing player only needs a support overlap beneath the feet;
            // requiring the entire collision box to fit inside every top edge
            // deletes the ledge portals used by the stock maps. Occupancy is
            // still checked with the full class-agnostic body envelope below.
            var left = interval.Left - envelope.RightOffset + MinimumSurfaceClearance;
            var right = interval.Right - envelope.LeftOffset - MinimumSurfaceClearance;
            if (right <= left)
            {
                // A stock-map stair is often represented as a sequence of
                // 6-pixel exposed lips. The body can climb those lips through
                // the movement step-up resolver even though a full-width
                // standing interval does not fit on each one. Keep a small
                // support point for the topology; its clearance is still
                // checked against the complete envelope.
                var center = (interval.Left + interval.Right) * 0.5f;
                if (HasUsableClearance(level, center, interval.Top, envelope))
                {
                    surfaces.Add(new NavSurface(
                        surfaces.Count,
                        center - (MinimumSurfaceWidth * 0.5f),
                        center + (MinimumSurfaceWidth * 0.5f),
                        interval.Top,
                        interval.IsDropdown));
                }

                continue;
            }

            foreach (var clearance in SplitByClearanceBlockers(level, left, right, interval.Top, envelope))
            {
                if (clearance.Right - clearance.Left < MinimumSurfaceWidth)
                {
                    continue;
                }

                surfaces.Add(new NavSurface(
                    surfaces.Count,
                    clearance.Left,
                    clearance.Right,
                    interval.Top,
                    interval.IsDropdown));
                if (IsSurfaceTraceEnabled())
                {
                    Console.WriteLine(
                        $"[botbrain] alpha-nav surface id={surfaces[^1].Id} " +
                        $"interval=({clearance.Left:0.0},{clearance.Right:0.0}) top={interval.Top:0.0}");
                }
            }
        }

        return surfaces;
    }

    private static bool HasUsableClearance(SimpleLevel level, float x, float surfaceTop, NavigationEnvelope envelope)
    {
        var y = surfaceTop - envelope.BottomOffset;
        return !IntersectsBlockingGeometry(level, x, y, envelope, PlayerTeam.Red, carryingIntel: false)
            || !IntersectsBlockingGeometry(level, x, y, envelope, PlayerTeam.Blue, carryingIntel: false);
    }

    private static List<ClearanceInterval> SplitByClearanceBlockers(
        SimpleLevel level,
        float left,
        float right,
        float surfaceTop,
        NavigationEnvelope envelope)
    {
        var cuts = new List<float> { left, right };
        var bodyTop = surfaceTop - envelope.BottomOffset + envelope.TopOffset;

        foreach (var solid in level.Solids)
        {
            if (solid.Top >= surfaceTop - SurfaceMergeVerticalTolerance
                || solid.Bottom <= bodyTop + SurfaceMergeVerticalTolerance)
            {
                continue;
            }

            AddClearanceCut(cuts, left, right, solid.Left - envelope.RightOffset, solid.Right - envelope.LeftOffset);
        }

        foreach (var roomObject in level.RoomObjects)
        {
            if (roomObject.Bottom <= bodyTop + SurfaceMergeVerticalTolerance
                || roomObject.Top >= surfaceTop - SurfaceMergeVerticalTolerance)
            {
                continue;
            }

            AddClearanceCut(
                cuts,
                left,
                right,
                roomObject.Left - envelope.RightOffset,
                roomObject.Right - envelope.LeftOffset);
        }

        cuts.Sort();
        var intervals = new List<ClearanceInterval>(cuts.Count - 1);
        for (var cutIndex = 0; cutIndex + 1 < cuts.Count; cutIndex += 1)
        {
            var segmentLeft = cuts[cutIndex];
            var segmentRight = cuts[cutIndex + 1];
            if (segmentRight - segmentLeft < MinimumSurfaceWidth)
            {
                continue;
            }

            var midpoint = (segmentLeft + segmentRight) * 0.5f;
            if (HasUsableClearance(level, midpoint, surfaceTop, envelope))
            {
                intervals.Add(new ClearanceInterval(segmentLeft, segmentRight));
            }
        }

        return intervals;
    }

    private static void AddClearanceCut(List<float> cuts, float left, float right, float blockedLeft, float blockedRight)
    {
        if (blockedRight <= left || blockedLeft >= right)
        {
            return;
        }

        cuts.Add(Math.Clamp(blockedLeft, left, right));
        cuts.Add(Math.Clamp(blockedRight, left, right));
    }

    private static int[] AddSurfaceNodes(
        List<NavNode> nodes,
        NavSurface surface,
        NavigationEnvelope envelope)
    {
        var width = surface.Right - surface.Left;
        var segmentCount = Math.Max(1, (int)MathF.Ceiling(width / MaximumNodeSpacing));
        var indices = new int[segmentCount + 1];
        var y = surface.Top - envelope.BottomOffset;
        for (var i = 0; i <= segmentCount; i += 1)
        {
            var x = surface.Left + width * i / segmentCount;
            var kind = i == 0 || i == segmentCount
                ? NavNodeKind.Ledge
                : NavNodeKind.Surface;
            indices[i] = nodes.Count;
            nodes.Add(new NavNode(x, y, kind, surface.Id));
        }

        return indices;
    }

    private static List<AnchorRecord> AddAnchors(
        SimpleLevel level,
        List<NavNode> nodes,
        List<NavSpawnAnchor> spawnAnchors)
    {
        var records = new List<AnchorRecord>();
        foreach (var spawn in level.RedSpawns)
        {
            var nodeIndex = nodes.Count;
            nodes.Add(new NavNode(spawn.X, spawn.Y, NavNodeKind.Spawn));
            spawnAnchors.Add(new NavSpawnAnchor(spawn.X, spawn.Y, PlayerTeam.Red));
            records.Add(new AnchorRecord(nodeIndex, spawn.X, spawn.Y, PlayerTeam.Red, IsObjective: false));
        }

        foreach (var spawn in level.BlueSpawns)
        {
            var nodeIndex = nodes.Count;
            nodes.Add(new NavNode(spawn.X, spawn.Y, NavNodeKind.Spawn));
            spawnAnchors.Add(new NavSpawnAnchor(spawn.X, spawn.Y, PlayerTeam.Blue));
            records.Add(new AnchorRecord(nodeIndex, spawn.X, spawn.Y, PlayerTeam.Blue, IsObjective: false));
        }

        foreach (var intelBase in level.IntelBases)
        {
            var nodeIndex = nodes.Count;
            nodes.Add(new NavNode(intelBase.X, intelBase.Y, NavNodeKind.Objective));
            records.Add(new AnchorRecord(nodeIndex, intelBase.X, intelBase.Y, intelBase.Team, IsObjective: true));
        }

        foreach (var roomObject in level.RoomObjects)
        {
            if (roomObject.Type is not (RoomObjectType.ArenaControlPoint
                or RoomObjectType.ControlPoint
                or RoomObjectType.CaptureZone
                or RoomObjectType.Generator))
            {
                continue;
            }

            var nodeIndex = nodes.Count;
            nodes.Add(new NavNode(roomObject.CenterX, roomObject.CenterY, NavNodeKind.Objective));
            records.Add(new AnchorRecord(nodeIndex, roomObject.CenterX, roomObject.CenterY, roomObject.Team, IsObjective: true));
        }

        return records;
    }

    private static List<NavEdge>[] CreateAdjacency(int nodeCount)
    {
        var adjacency = new List<NavEdge>[nodeCount];
        for (var i = 0; i < nodeCount; i += 1)
        {
            adjacency[i] = [];
        }

        return adjacency;
    }

    private static void AddSurfaceWalkEdges(
        IReadOnlyList<NavSurface> surfaces,
        IReadOnlyList<int[]> surfaceNodeIndices,
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<List<NavEdge>> adjacency,
        HashSet<NavigationEdgeKey> edgeKeys)
    {
        for (var surfaceIndex = 0; surfaceIndex < surfaces.Count; surfaceIndex += 1)
        {
            var nodeIndices = surfaceNodeIndices[surfaceIndex];
            for (var nodeIndex = 0; nodeIndex + 1 < nodeIndices.Length; nodeIndex += 1)
            {
                AddBidirectionalEdge(
                    nodeIndices[nodeIndex],
                    nodeIndices[nodeIndex + 1],
                    NavEdgeKind.Walk,
                    Distance(nodes[nodeIndices[nodeIndex]], nodes[nodeIndices[nodeIndex + 1]]),
                    nodes,
                    adjacency,
                    edgeKeys);
            }
        }
    }

    private static void AddSurfaceTransitionEdges(
        SimpleLevel level,
        IReadOnlyList<NavSurface> surfaces,
        IReadOnlyList<int[]> surfaceNodeIndices,
        IReadOnlyList<NavNode> nodes,
        NavigationEnvelope envelope,
        IReadOnlyList<float> bendCandidates,
        IReadOnlyList<List<NavEdge>> adjacency,
        HashSet<NavigationEdgeKey> edgeKeys)
    {
        // Surfaces are ordered by height for stable node ids, not by x. A left-edge
        // index keeps transition generation local instead of scanning every surface
        // for every source node.
        var surfacesByLeft = Enumerable.Range(0, surfaces.Count)
            .OrderBy(index => surfaces[index].Left)
            .ToArray();
        for (var sourceSurfaceIndex = 0; sourceSurfaceIndex < surfaces.Count; sourceSurfaceIndex += 1)
        {
            var sourceSurface = surfaces[sourceSurfaceIndex];
            var sourceNodes = surfaceNodeIndices[sourceSurfaceIndex];
            for (var sourceNodeIndex = 0; sourceNodeIndex < sourceNodes.Length; sourceNodeIndex += 1)
            {
                var sourceNode = nodes[sourceNodes[sourceNodeIndex]];
                var sourceX = sourceNode.X;

                var minimumTargetX = sourceX - MaximumStairHorizontalDistance;
                var maximumTargetX = sourceX + MaximumStairHorizontalDistance;
                foreach (var targetSurfaceIndex in surfacesByLeft)
                {
                    if (sourceSurfaceIndex == targetSurfaceIndex)
                    {
                        continue;
                    }

                    var targetSurface = surfaces[targetSurfaceIndex];
                    if (targetSurface.Left > maximumTargetX)
                    {
                        break;
                    }

                    var surfaceVerticalDelta = targetSurface.Top - sourceSurface.Top;
                    var isPossibleStairTransition = surfaceVerticalDelta < -StepHeight
                        && -surfaceVerticalDelta <= MaximumStepProfileExcursion;
                    var horizontalLimit = isPossibleStairTransition
                        ? MaximumStairHorizontalDistance
                        : MaximumSurfaceCandidateHorizontalDistance;
                    if (targetSurface.Right < sourceX - horizontalLimit
                        || targetSurface.Left > sourceX + horizontalLimit
                        || MathF.Abs(surfaceVerticalDelta) > MaximumFallDistance)
                    {
                        continue;
                    }

                    var targetX = Math.Clamp(sourceX, targetSurface.Left, targetSurface.Right);
                    foreach (var targetNodeIndex in FindCandidateNodesOnSurface(
                                 nodes,
                                 surfaceNodeIndices[targetSurfaceIndex],
                                 targetX))
                    {
                        var targetNode = nodes[targetNodeIndex];
                        var verticalDelta = targetNode.Y - sourceNode.Y;
                        var horizontalDelta = MathF.Abs(targetNode.X - sourceNode.X);
                        var isPossibleStepTransition = verticalDelta < -StepHeight
                            && -verticalDelta <= MaximumStepProfileExcursion
                            && horizontalDelta <= MaximumStairHorizontalDistance;
                        if (horizontalDelta > MaximumFallHorizontalDistance
                            && !isPossibleStepTransition)
                        {
                            continue;
                        }

                        var kind = ResolveTransitionKind(sourceSurface, targetSurface, sourceNode, targetNode);
                        if (kind is null)
                        {
                            continue;
                        }

                        var canTraverse = CanTraverse(
                            level,
                            sourceNode,
                            targetNode,
                            kind.Value,
                            envelope,
                            sourceSurface,
                            targetSurface,
                            bendCandidates,
                            out var fallBendX);
                        var supportedClassMask = BotBrainClassMask.All;
                        if (!canTraverse
                            && kind == NavEdgeKind.Walk
                            && verticalDelta < -StepHeight
                            && -verticalDelta <= StandardJumpRise
                            && horizontalDelta <= MaximumJumpHorizontalDistance)
                        {
                            // A nearby elevated platform may be a genuine jump
                            // rather than a step profile. Keep the stair-first
                            // classification, but fall back to the certified
                            // jump model when the support profile rejects it.
                            kind = NavEdgeKind.Jump;
                            canTraverse = CanTraverse(
                                level,
                                sourceNode,
                                targetNode,
                                kind.Value,
                                envelope,
                                sourceSurface,
                                targetSurface,
                                bendCandidates,
                                out fallBendX);
                        }
                        else if (!canTraverse
                            && kind == NavEdgeKind.Walk
                            && verticalDelta < -StandardJumpRise
                            && -verticalDelta <= MaximumJumpRise
                            && horizontalDelta <= MaximumCertifiedTallJumpHorizontalDistance
                            && IsCompactStairJumpCandidate(sourceSurface, targetSurface)
                            && EnableTallJumpCertification)
                        {
                            // Some stock stair platforms are separated by more
                            // than the conservative shared ballistic rise. A
                            // geometric envelope cannot prove these links, so
                            // certify only compact, stair-shaped candidates
                            // with the authoritative OG2 movement validator.
                            // This remains class capability, never objective
                            // or carrier state.
                            kind = NavEdgeKind.Jump;
                            supportedClassMask = ResolveTallJumpClassMask(
                                level,
                                sourceNode,
                                targetNode);
                            canTraverse = supportedClassMask != 0;
                        }
                        if (!canTraverse)
                        {
                            continue;
                        }

                        var isLongAscendingStep = kind.Value == NavEdgeKind.Walk
                            && verticalDelta < -StandardJumpRise;
                        var completion = kind.Value == NavEdgeKind.Walk
                            ? isLongAscendingStep
                                ? CreateCompletion(targetNode, targetSurface.Id)
                                : NavEdgeCompletion.None
                            : kind.Value is NavEdgeKind.Fall or NavEdgeKind.Dropdown
                                && MathF.Abs(fallBendX - targetNode.X) > 12f
                                ? CreateCompletion(new NavNode(fallBendX, targetNode.Y, NavNodeKind.Ledge), null)
                                : CreateCompletion(targetNode, targetSurface.Id);
                        var cost = Distance(sourceNode, targetNode)
                            + ResolveTransitionCost(kind.Value, verticalDelta)
                            + (kind.Value == NavEdgeKind.Jump && verticalDelta < 0f ? VerticalAscentPenalty : 0f);
                        AddDirectedEdge(
                            sourceNodes[sourceNodeIndex],
                            targetNodeIndex,
                            kind.Value,
                            cost,
                            completion,
                            sourceNode,
                            targetNode,
                            adjacency,
                            edgeKeys,
                            supportedClassMask,
                            kind == NavEdgeKind.Jump
                                && RequiresDelayedJumpLaunch(level, sourceNode, targetNode, envelope)
                                ? CreateJumpLaunchRecipe(sourceNode, targetNode, envelope)
                                : NavEdgeLaunchRecipe.None);

                        // Links are directional in the runtime. A jump out of a
                        // ledge is not automatically a valid fall back to it, and
                        // a dropdown may require a different landing bend on the
                        // return route. Validate the reverse independently.
                        var reverseKind = ReverseKind(kind.Value);
                        var reverseSupportedClassMask = BotBrainClassMask.All;
                        var reverseCanTraverse = CanTraverse(
                                level,
                                targetNode,
                                sourceNode,
                                reverseKind,
                                envelope,
                                targetSurface,
                                sourceSurface,
                                bendCandidates,
                                out var reverseBendX);
                        if (!reverseCanTraverse
                            && reverseKind == NavEdgeKind.Jump
                            && sourceNode.Y - targetNode.Y > StandardJumpRise
                            && sourceNode.Y - targetNode.Y <= MaximumJumpRise
                            && horizontalDelta <= MaximumCertifiedTallJumpHorizontalDistance
                            && EnableTallJumpCertification)
                        {
                            reverseSupportedClassMask = ResolveTallJumpClassMask(
                                level,
                                targetNode,
                                sourceNode);
                            reverseCanTraverse = reverseSupportedClassMask != 0;
                        }

                        if (reverseCanTraverse
                            && !isLongAscendingStep)
                        {
                            var reverseCompletion = reverseKind is NavEdgeKind.Fall or NavEdgeKind.Dropdown
                                && MathF.Abs(reverseBendX - sourceNode.X) > 12f
                                ? CreateCompletion(new NavNode(reverseBendX, sourceNode.Y, NavNodeKind.Ledge), null)
                                : reverseKind == NavEdgeKind.Walk
                                    ? NavEdgeCompletion.None
                                    : CreateCompletion(sourceNode, sourceSurface.Id);
                            AddDirectedEdge(
                                targetNodeIndex,
                                sourceNodes[sourceNodeIndex],
                                reverseKind,
                                cost,
                                reverseCompletion,
                                targetNode,
                                sourceNode,
                                adjacency,
                                edgeKeys,
                                reverseSupportedClassMask,
                                    reverseKind == NavEdgeKind.Jump
                                        && RequiresDelayedJumpLaunch(level, targetNode, sourceNode, envelope)
                                    ? CreateJumpLaunchRecipe(targetNode, sourceNode, envelope)
                                    : NavEdgeLaunchRecipe.None);
                        }
                    }
                }
            }
        }
    }

    private static void AddAnchorEdges(
        SimpleLevel level,
        IReadOnlyList<AnchorRecord> anchors,
        IReadOnlyList<NavSurface> surfaces,
        IReadOnlyList<int[]> surfaceNodeIndices,
        IReadOnlyList<NavNode> nodes,
        NavigationEnvelope envelope,
        IReadOnlyList<List<NavEdge>> adjacency,
        HashSet<NavigationEdgeKey> edgeKeys)
    {
        foreach (var anchor in anchors)
        {
            var candidates = new List<(int NodeIndex, float Score)>();
            for (var surfaceIndex = 0; surfaceIndex < surfaces.Count; surfaceIndex += 1)
            {
                var surface = surfaces[surfaceIndex];
                if (surface.Left > anchor.X + MaximumAnchorHorizontalDistance
                    || surface.Right < anchor.X - MaximumAnchorHorizontalDistance
                    || MathF.Abs(surface.Top - anchor.Y) > MaximumAnchorVerticalDistance)
                {
                    continue;
                }

                foreach (var nodeIndex in surfaceNodeIndices[surfaceIndex])
                {
                    var node = nodes[nodeIndex];
                    var dx = MathF.Abs(node.X - anchor.X);
                    var dy = MathF.Abs(node.Y - anchor.Y);
                    if (dx > MaximumAnchorHorizontalDistance || dy > MaximumAnchorVerticalDistance)
                    {
                        continue;
                    }

                    candidates.Add((nodeIndex, dx + dy * 1.75f));
                }
            }

            foreach (var candidate in candidates
                         .OrderBy(static candidate => candidate.Score)
                         .Take(4))
            {
                var source = nodes[anchor.NodeIndex];
                var target = nodes[candidate.NodeIndex];
                var kind = ResolveAnchorLinkKind(source, target);
                var cost = candidate.Score + (anchor.IsObjective ? ObjectiveAnchorCost : SpawnAnchorCost);

                if (anchor.IsObjective)
                {
                    // Objective markers and capture-zone centers are gameplay goal
                    // coordinates, not standing surfaces. Validate the approach from
                    // the walkable node, then allow the final goal point to be inside
                    // the marker's backing solid. This keeps objectives connected
                    // without making the graph pretend the marker itself is geometry.
                    var approachKind = ResolveAnchorLinkKind(target, source);
                    if (!CanTraverseAnchorApproach(level, target, approachKind, envelope))
                    {
                        continue;
                    }

                    AddDirectedEdge(
                        candidate.NodeIndex,
                        anchor.NodeIndex,
                        approachKind,
                        cost,
                        CreateCompletion(source, null),
                        target,
                        source,
                        adjacency,
                        edgeKeys);
                    AddDirectedEdge(
                        anchor.NodeIndex,
                        candidate.NodeIndex,
                        kind,
                        cost,
                        NavEdgeCompletion.None,
                        source,
                        target,
                        adjacency,
                        edgeKeys);
                    continue;
                }

                if (!CanTraverseAnchor(level, source, target, kind, envelope))
                {
                    continue;
                }

                AddBidirectionalEdge(
                    anchor.NodeIndex,
                    candidate.NodeIndex,
                    kind,
                    cost,
                    nodes,
                    adjacency,
                    edgeKeys);
            }
        }
    }

    private static void AddRecoveryBridgeEdges(
        SimpleLevel level,
        IReadOnlyList<AnchorRecord> anchors,
        IReadOnlyList<NavNode> nodes,
        NavigationEnvelope envelope,
        IReadOnlyList<List<NavEdge>> adjacency,
        HashSet<NavigationEdgeKey> edgeKeys)
    {
        var trace = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_RECOVERY_TRACE") is "1" or "true" or "TRUE";
        var objectives = anchors.Where(static anchor => anchor.IsObjective).ToArray();
        foreach (var objective in objectives)
        {
            var targets = anchors
                .Where(target => target.NodeIndex != objective.NodeIndex
                    && (!objective.Team.HasValue
                        || !target.Team.HasValue
                        || objective.Team.Value != target.Team.Value))
                .ToArray();
            foreach (var targetAnchor in targets)
            {
                // A return route can contain several consecutive stair lifts.
                // Recompute the two components after each certified bridge so
                // the next missing link is discovered from the newly joined
                // boundary rather than being limited to the first gap.
                for (var bridgeIndex = 0; bridgeIndex < 8; bridgeIndex += 1)
                {
                    var reachableFromObjective = ComputeReachable(objective.NodeIndex, adjacency);
                    if (reachableFromObjective[targetAnchor.NodeIndex])
                    {
                        if (trace)
                        {
                            Console.WriteLine($"[botbrain] recovery objective={objective.NodeIndex} target={targetAnchor.NodeIndex} connected bridgeIndex={bridgeIndex}");
                        }

                        break;
                    }

                    var canReachTarget = ComputeReverseReachable(targetAnchor.NodeIndex, adjacency);
                    var candidates = new List<(int FromNode, int ToNode, float Score)>();
                    var nearestComponentPair = (-1, -1, float.MaxValue);
                    for (var fromNodeIndex = 0; fromNodeIndex < nodes.Count; fromNodeIndex += 1)
                    {
                        if (!reachableFromObjective[fromNodeIndex]
                            || nodes[fromNodeIndex].Kind is NavNodeKind.Objective or NavNodeKind.Spawn)
                        {
                            continue;
                        }

                        var source = nodes[fromNodeIndex];
                        for (var toNodeIndex = 0; toNodeIndex < nodes.Count; toNodeIndex += 1)
                        {
                            if (!canReachTarget[toNodeIndex]
                                || nodes[toNodeIndex].Kind is NavNodeKind.Objective or NavNodeKind.Spawn
                                || edgeKeys.Contains(new NavigationEdgeKey(fromNodeIndex, toNodeIndex, NavEdgeKind.Jump)))
                            {
                                continue;
                            }

                            var target = nodes[toNodeIndex];
                            var rise = source.Y - target.Y;
                            var horizontalDistance = MathF.Abs(source.X - target.X);
                            var componentDistance = horizontalDistance + MathF.Abs(source.Y - target.Y);
                            if (componentDistance < nearestComponentPair.Item3)
                            {
                                nearestComponentPair = (fromNodeIndex, toNodeIndex, componentDistance);
                            }

                            if (rise <= LandingVerticalTolerance
                                || rise > MaximumJumpRise
                                || horizontalDistance > MaximumCertifiedTallJumpHorizontalDistance)
                            {
                                continue;
                            }

                            candidates.Add((
                                fromNodeIndex,
                                toNodeIndex,
                                horizontalDistance + (rise * 0.5f)));
                        }
                    }

                    var addedBridge = false;
                    if (trace)
                    {
                        Console.WriteLine($"[botbrain] recovery objective={objective.NodeIndex} target={targetAnchor.NodeIndex} bridgeIndex={bridgeIndex} candidates={candidates.Count}");
                        if (nearestComponentPair.Item1 >= 0)
                        {
                            var nearestSource = nodes[nearestComponentPair.Item1];
                            var nearestTarget = nodes[nearestComponentPair.Item2];
                            Console.WriteLine($"[botbrain] recovery nearest {nearestComponentPair.Item1}->{nearestComponentPair.Item2} distance={nearestComponentPair.Item3:0.0} source=({nearestSource.X:0.0},{nearestSource.Y:0.0}) target=({nearestTarget.X:0.0},{nearestTarget.Y:0.0})");
                        }
                        foreach (var candidate in candidates.OrderBy(static candidate => candidate.Score).Take(5))
                        {
                            Console.WriteLine($"[botbrain] recovery candidate {candidate.FromNode}->{candidate.ToNode} score={candidate.Score:0.0} source=({nodes[candidate.FromNode].X:0.0},{nodes[candidate.FromNode].Y:0.0}) target=({nodes[candidate.ToNode].X:0.0},{nodes[candidate.ToNode].Y:0.0})");
                        }
                    }

                    // Recovery certification is deliberately demand-driven.
                    // The normal graph pass stays geometric and fast; only a
                    // small number of links that bridge an actually
                    // disconnected objective component receive the
                    // authoritative OG2 movement search.
                    foreach (var candidate in candidates
                                 .OrderBy(static candidate => candidate.Score)
                                 .Take(4))
                    {
                        var source = nodes[candidate.FromNode];
                        var target = nodes[candidate.ToNode];
                        var supportedClassMask = ResolveTallJumpClassMask(
                            level,
                            source,
                            target);
                        if (trace)
                        {
                            Console.WriteLine($"[botbrain] recovery certification {candidate.FromNode}->{candidate.ToNode} mask={supportedClassMask}");
                        }
                        if (supportedClassMask == 0)
                        {
                            continue;
                        }

                        var verticalDelta = target.Y - source.Y;
                        AddDirectedEdge(
                            candidate.FromNode,
                            candidate.ToNode,
                            NavEdgeKind.Jump,
                            Distance(source, target)
                                + ResolveTransitionCost(NavEdgeKind.Jump, verticalDelta)
                                + VerticalAscentPenalty,
                            CreateCompletion(target, target.SurfaceId),
                            source,
                            target,
                            adjacency,
                            edgeKeys,
                            supportedClassMask,
                            CreateJumpLaunchRecipe(source, target, envelope));

                        addedBridge = true;
                        if (trace)
                        {
                            Console.WriteLine($"[botbrain] recovery added {candidate.FromNode}->{candidate.ToNode}");
                        }
                        break;
                    }

                    if (!addedBridge)
                    {
                        break;
                    }
                }
            }
        }
    }

    private static bool[] ComputeReachable(
        int startNode,
        IReadOnlyList<List<NavEdge>> adjacency)
    {
        var reachable = new bool[adjacency.Count];
        if (startNode < 0 || startNode >= adjacency.Count)
        {
            return reachable;
        }

        var pending = new Queue<int>();
        reachable[startNode] = true;
        pending.Enqueue(startNode);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var edge in adjacency[current])
            {
                if (reachable[edge.ToNode])
                {
                    continue;
                }

                reachable[edge.ToNode] = true;
                pending.Enqueue(edge.ToNode);
            }
        }

        return reachable;
    }

    private static bool[] ComputeReverseReachable(
        int targetNode,
        IReadOnlyList<List<NavEdge>> adjacency)
    {
        var reverseAdjacency = new List<int>[adjacency.Count];
        for (var nodeIndex = 0; nodeIndex < adjacency.Count; nodeIndex += 1)
        {
            reverseAdjacency[nodeIndex] = [];
        }

        for (var fromNodeIndex = 0; fromNodeIndex < adjacency.Count; fromNodeIndex += 1)
        {
            foreach (var edge in adjacency[fromNodeIndex])
            {
                reverseAdjacency[edge.ToNode].Add(fromNodeIndex);
            }
        }

        var reachable = new bool[adjacency.Count];
        if (targetNode < 0 || targetNode >= adjacency.Count)
        {
            return reachable;
        }

        var pending = new Queue<int>();
        reachable[targetNode] = true;
        pending.Enqueue(targetNode);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var predecessor in reverseAdjacency[current])
            {
                if (reachable[predecessor])
                {
                    continue;
                }

                reachable[predecessor] = true;
                pending.Enqueue(predecessor);
            }
        }

        return reachable;
    }

    private static NavEdgeKind? ResolveTransitionKind(
        NavSurface sourceSurface,
        NavSurface targetSurface,
        NavNode source,
        NavNode target)
    {
        var verticalDelta = target.Y - source.Y;
        var horizontalGap = ResolveHorizontalGap(sourceSurface, targetSurface);
        if (MathF.Abs(verticalDelta) <= StepHeight
            && horizontalGap <= 4f
            && verticalDelta <= 0f)
        {
            return NavEdgeKind.Walk;
        }

        if (verticalDelta < -StepHeight
            && -verticalDelta <= MaximumStepProfileExcursion
            && MathF.Abs(target.X - source.X) <= MaximumStairHorizontalDistance)
        {
            return NavEdgeKind.Walk;
        }

        if (verticalDelta < -LandingVerticalTolerance
            && -verticalDelta <= MaximumJumpRise
            && MathF.Abs(target.X - source.X) <= MaximumJumpHorizontalDistance)
        {
            return NavEdgeKind.Jump;
        }

        if (verticalDelta > LandingVerticalTolerance
            && verticalDelta <= MaximumFallDistance
            && MathF.Abs(target.X - source.X) <= MaximumFallHorizontalDistance)
        {
            return sourceSurface.IsDropdown && horizontalGap <= 60f
                ? NavEdgeKind.Dropdown
                : NavEdgeKind.Fall;
        }

        if (MathF.Abs(verticalDelta) <= StepHeight
            && horizontalGap <= MaximumJumpHorizontalDistance)
        {
            return verticalDelta <= 0f ? NavEdgeKind.Jump : NavEdgeKind.Fall;
        }

        return null;
    }

    private static NavEdgeKind ResolveAnchorLinkKind(NavNode source, NavNode target)
    {
        var verticalDelta = target.Y - source.Y;
        if (MathF.Abs(verticalDelta) <= StepHeight)
        {
            return NavEdgeKind.Walk;
        }

        return verticalDelta < 0f ? NavEdgeKind.Jump : NavEdgeKind.Fall;
    }

    private static bool CanTraverseAnchor(
        SimpleLevel level,
        NavNode source,
        NavNode target,
        NavEdgeKind kind,
        NavigationEnvelope envelope)
    {
        if (kind == NavEdgeKind.Walk)
        {
            return !IntersectsBlockingGeometry(level, source.X, source.Y, envelope, PlayerTeam.Red, false)
                && !IntersectsBlockingGeometry(level, target.X, target.Y, envelope, PlayerTeam.Red, false);
        }

        return CanTraverseArc(level, source, target, kind, envelope);
    }

    private static bool CanTraverseAnchorApproach(
        SimpleLevel level,
        NavNode source,
        NavEdgeKind kind,
        NavigationEnvelope envelope)
    {
        if (kind == NavEdgeKind.Walk)
        {
            return !IntersectsBlockingGeometry(level, source.X, source.Y, envelope, PlayerTeam.Red, false)
                || !IntersectsBlockingGeometry(level, source.X, source.Y, envelope, PlayerTeam.Blue, false);
        }

        // The target is a gameplay marker rather than a physical landing surface;
        // only validate the launch/approach node here. The final objective test is
        // owned by the objective evaluator and capture/intel simulation.
        return !IntersectsBlockingGeometry(level, source.X, source.Y, envelope, PlayerTeam.Red, false)
            || !IntersectsBlockingGeometry(level, source.X, source.Y, envelope, PlayerTeam.Blue, false);
    }

    private static bool CanTraverse(
        SimpleLevel level,
        NavNode source,
        NavNode target,
        NavEdgeKind kind,
        NavigationEnvelope envelope,
        NavSurface sourceSurface,
        NavSurface targetSurface,
        IReadOnlyList<float> bendCandidates,
        out float fallBendX)
    {
        fallBendX = target.X;
        if (kind == NavEdgeKind.Walk)
        {
            if (MathF.Abs(target.Y - source.Y) > StepHeight
                || ResolveHorizontalGap(sourceSurface, targetSurface) > 4f)
            {
                return CanTraverseStepPath(level, source, target, sourceSurface, targetSurface, envelope)
                    ;
            }

            return IsClearForEitherTeam(level, source.X, source.Y, envelope)
                && IsClearForEitherTeam(level, target.X, target.Y, envelope);
        }

        if (kind == NavEdgeKind.Dropdown && !sourceSurface.IsDropdown)
        {
            return false;
        }

        if (kind == NavEdgeKind.Fall && targetSurface.Top <= sourceSurface.Top)
        {
            return false;
        }

        if (kind is NavEdgeKind.Fall or NavEdgeKind.Dropdown)
        {
            return CanTraverseFallPath(level, source, target, envelope, bendCandidates, out fallBendX);
        }

        return CanTraverseArc(level, source, target, kind, envelope);
    }

    private static bool CanTraverseStepPath(
        SimpleLevel level,
        NavNode source,
        NavNode target,
        NavSurface sourceSurface,
        NavSurface targetSurface,
        NavigationEnvelope envelope)
    {
        var trace = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_STEP_TRACE") is "1" or "true" or "TRUE"
            && MathF.Abs(source.X - 3632f) < 1f
            && MathF.Abs(target.X - 4521f) < 1f;
        var horizontalDistance = MathF.Abs(target.X - source.X);
        if (horizontalDistance > MaximumStairHorizontalDistance
            || MathF.Abs(targetSurface.Top - sourceSurface.Top) > MaximumStepProfileExcursion)
        {
            return false;
        }

        // The runtime resolves horizontal collisions with exactly one 6 px
        // step up/down. Model the traversable ground as a bounded vertical
        // profile instead of lifting an imaginary player until it is clear.
        // This is directional on purpose: a staircase can be valid in one
        // direction but not the other, so reverse links must be certified on
        // their own geometry pass.
        var targetStep = (int)MathF.Round((target.Y - source.Y) / StepHeight);
        var maximumProfileRiseSteps = (int)MathF.Ceiling(MaximumStepProfileExcursion / StepHeight);
        var minimumStep = Math.Min(-maximumProfileRiseSteps, targetStep) - StepProfileExtraStateSteps;
        var maximumStep = Math.Max(0, targetStep) + StepProfileExtraStateSteps;
        var reachable = new bool[maximumStep - minimumStep + 1];
        reachable[-minimumStep] = true;
        if (trace)
        {
            Console.WriteLine($"[botbrain] alpha-nav step trace start steps=({minimumStep},{maximumStep})");
        }

        var sampleCount = Math.Max(1, (int)MathF.Ceiling(horizontalDistance / StepProfileSampleSpacing));
        for (var sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex += 1)
        {
            var t = sampleIndex / (float)sampleCount;
            var x = source.X + ((target.X - source.X) * t);
            var next = new bool[reachable.Length];
            for (var stateIndex = 0; stateIndex < reachable.Length; stateIndex += 1)
            {
                if (!reachable[stateIndex])
                {
                    continue;
                }

                var currentStep = minimumStep + stateIndex;
                for (var stepDelta = -1; stepDelta <= 1; stepDelta += 1)
                {
                    var candidateStep = currentStep + stepDelta;
                    if (candidateStep < minimumStep || candidateStep > maximumStep)
                    {
                        continue;
                    }

                    var candidateY = source.Y + (candidateStep * StepHeight);
                    if (IsClearForEitherTeam(level, x, candidateY, envelope)
                        && HasSupportAt(level, x, candidateY, envelope))
                    {
                        next[candidateStep - minimumStep] = true;
                    }
                }
            }

            reachable = next;
            if (trace && (sampleIndex % 20 == 0 || !reachable.Any(static state => state)))
            {
                var states = string.Join(',', reachable
                    .Select((isReachable, index) => isReachable ? (int?)(minimumStep + index) : null)
                    .Where(static state => state.HasValue)
                    .Select(static state => state!.Value));
                Console.WriteLine($"[botbrain] alpha-nav step trace sample={sampleIndex}/{sampleCount} x={x:0} states={states}");
            }
            if (!reachable.Any(static state => state))
            {
                return false;
            }
        }

        var targetIndex = targetStep - minimumStep;
        for (var stateIndex = Math.Max(0, targetIndex - 1); stateIndex <= Math.Min(reachable.Length - 1, targetIndex + 1); stateIndex += 1)
        {
            if (reachable[stateIndex])
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanTraverseCoarseAscendingStepPath(
        SimpleLevel level,
        NavNode source,
        NavNode target,
        NavSurface sourceSurface,
        NavSurface targetSurface,
        NavigationEnvelope envelope)
    {
        if (target.Y >= source.Y
            || sourceSurface.Top <= targetSurface.Top
            || sourceSurface.Top - targetSurface.Top > MaximumStepProfileExcursion)
        {
            return false;
        }

        var horizontalDistance = MathF.Abs(target.X - source.X);
        var sampleCount = Math.Max(2, (int)MathF.Ceiling(horizontalDistance / CollisionSampleSpacing));
        var currentY = source.Y;
        var maximumLiftSteps = (int)MathF.Ceiling(MaximumStepProfileExcursion / StepHeight);
        for (var sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex += 1)
        {
            var t = sampleIndex / (float)sampleCount;
            var x = source.X + ((target.X - source.X) * t);
            var liftSteps = 0;
            while (!IsClearForEitherTeam(level, x, currentY, envelope))
            {
                if (liftSteps++ >= maximumLiftSteps)
                {
                    return false;
                }

                currentY -= StepHeight;
            }

            if (!HasSupportAt(level, x, currentY, envelope))
            {
                return false;
            }
        }

        return MathF.Abs(currentY - target.Y) <= StepHeight + SurfaceMergeVerticalTolerance;
    }

    private static bool CanTraverseCoarseDescendingStepPath(
        SimpleLevel level,
        NavNode source,
        NavNode target,
        NavSurface sourceSurface,
        NavSurface targetSurface,
        NavigationEnvelope envelope)
    {
        var trace = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_DESCENDING_TRACE") is "1" or "true" or "TRUE"
            && MathF.Abs(source.X - 4521f) < 1f
            && MathF.Abs(source.Y - 888f) < 1f
            && MathF.Abs(target.X - 3632f) < 1f
            && MathF.Abs(target.Y - 936f) < 1f;
        if (target.Y <= source.Y
            || targetSurface.Top <= sourceSurface.Top
            || targetSurface.Top - sourceSurface.Top > MaximumStepProfileExcursion)
        {
            if (trace)
            {
                Console.WriteLine($"[botbrain] alpha-nav descending trace rejected precondition sourceSurface=({sourceSurface.Left:0.0},{sourceSurface.Top:0.0})-({sourceSurface.Right:0.0},{sourceSurface.Top:0.0}) targetSurface=({targetSurface.Left:0.0},{targetSurface.Top:0.0})-({targetSurface.Right:0.0},{targetSurface.Top:0.0})");
            }

            return false;
        }

        var horizontalDistance = MathF.Abs(target.X - source.X);
        var sampleCount = Math.Max(2, (int)MathF.Ceiling(horizontalDistance / CollisionSampleSpacing));
        var currentY = source.Y;
        var maximumDropSteps = (int)MathF.Ceiling(MaximumStepProfileExcursion / StepHeight);
        for (var sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex += 1)
        {
            var t = sampleIndex / (float)sampleCount;
            var x = source.X + ((target.X - source.X) * t);
            var dropSteps = 0;
            while (!HasSupportAt(level, x, currentY, envelope))
            {
                if (trace)
                {
                    Console.WriteLine($"[botbrain] alpha-nav descending trace sample={sampleIndex}/{sampleCount} x={x:0.0} y={currentY:0.0} support=0 clear={(IsClearForEitherTeam(level, x, currentY, envelope) ? 1 : 0)} dropSteps={dropSteps}");
                }

                if (dropSteps++ >= maximumDropSteps
                    || !IsClearForEitherTeam(level, x, currentY, envelope))
                {
                    if (trace)
                    {
                        Console.WriteLine("[botbrain] alpha-nav descending trace rejected while dropping");
                    }

                    return false;
                }

                currentY += StepHeight;
            }

            if (trace)
            {
                Console.WriteLine($"[botbrain] alpha-nav descending trace sample={sampleIndex}/{sampleCount} x={x:0.0} y={currentY:0.0} support=1 clear={(IsClearForEitherTeam(level, x, currentY, envelope) ? 1 : 0)}");
            }

            if (!IsClearForEitherTeam(level, x, currentY, envelope))
            {
                if (trace)
                {
                    Console.WriteLine("[botbrain] alpha-nav descending trace rejected at supported sample");
                }

                return false;
            }
        }

        var accepted = MathF.Abs(currentY - target.Y) <= StepHeight + SurfaceMergeVerticalTolerance;
        if (trace)
        {
            Console.WriteLine($"[botbrain] alpha-nav descending trace result currentY={currentY:0.0} targetY={target.Y:0.0} accepted={(accepted ? 1 : 0)}");
        }

        return accepted;
    }

    private static bool HasSupportAt(
        SimpleLevel level,
        float x,
        float y,
        NavigationEnvelope envelope)
    {
        var bodyLeft = x + envelope.LeftOffset;
        var bodyRight = x + envelope.RightOffset;
        var supportTop = y + envelope.BottomOffset;
        foreach (var solid in level.Solids)
        {
            if (solid.Right > bodyLeft
                && solid.Left < bodyRight
                && MathF.Abs(solid.Top - supportTop) <= SurfaceMergeVerticalTolerance)
            {
                return true;
            }
        }

        foreach (var roomObject in level.RoomObjects)
        {
            if (roomObject.Type == RoomObjectType.DropdownPlatform
                && roomObject.Right > bodyLeft
                && roomObject.Left < bodyRight
                && MathF.Abs(roomObject.Top - supportTop) <= SurfaceMergeVerticalTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanTraverseArc(
        SimpleLevel level,
        NavNode source,
        NavNode target,
        NavEdgeKind kind,
        NavigationEnvelope envelope)
    {
        var horizontalDistance = MathF.Abs(target.X - source.X);
        var verticalDistance = target.Y - source.Y;
        var horizontalLimit = kind is NavEdgeKind.Fall or NavEdgeKind.Dropdown
            ? MaximumFallHorizontalDistance
            : MaximumJumpHorizontalDistance;
        if (horizontalDistance > horizontalLimit
            || verticalDistance < -MaximumJumpRise
            || verticalDistance > MaximumFallDistance)
        {
            return false;
        }

        if (kind is NavEdgeKind.Fall or NavEdgeKind.Dropdown)
        {
            return CanTraverseFallPath(level, source, target, envelope, [], out _);
        }

        if (kind == NavEdgeKind.Jump)
        {
            var direction = MathF.Sign(target.X - source.X);
            var maximumRunUpDistance = envelope.MinimumRunSpeed * MaximumJumpRunUpSeconds;
            var launchDistance = MathF.Min(horizontalDistance, maximumRunUpDistance);
            var launchX = source.X + (direction * launchDistance);
            var horizontalClear = CanTraverseHorizontalPhase(level, source.X, launchX, source.Y, envelope);
            var arcClear = horizontalClear
                && CanTraverseJumpArc(level, launchX, source.Y, target.X, target.Y, envelope);
            if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_JUMP_TRACE") is "1" or "true" or "TRUE"
                && MathF.Abs(source.X - 4399f) < 1f
                && MathF.Abs(source.Y - 786f) < 1f
                && MathF.Abs(target.X - 4582f) < 1f
                && MathF.Abs(target.Y - 732f) < 1f)
            {
                Console.WriteLine(
                    $"[botbrain] alpha-nav jump trace source=({source.X:0.0},{source.Y:0.0}) target=({target.X:0.0},{target.Y:0.0}) " +
                    $"minimumRunSpeed={envelope.MinimumRunSpeed:0.0} launchX={launchX:0.0} horizontalClear={(horizontalClear ? 1 : 0)} arcClear={(arcClear ? 1 : 0)}");
            }

            return arcClear;
        }

        return CanTraverseFallPath(level, source, target, envelope, [], out _);
    }

    private static bool CanTraverseJumpArc(
        SimpleLevel level,
        float launchX,
        float launchY,
        float targetX,
        float targetY,
        NavigationEnvelope envelope)
    {
        var horizontalDistance = MathF.Abs(targetX - launchX);
        var verticalDistance = targetY - launchY;
        var gravity = envelope.MaximumGravityPerSecondSquared;
        var jumpSpeed = envelope.MinimumJumpSpeed;
        var discriminant = (jumpSpeed * jumpSpeed) + (2f * gravity * verticalDistance);
        if (discriminant <= 0f)
        {
            return false;
        }

        // Use the descending root of the actual OG2 jump equation. The old
        // sinusoid reached an arbitrary apex and did not model launch timing.
        var flightTime = (jumpSpeed + MathF.Sqrt(discriminant)) / gravity;
        var horizontalReach = envelope.MinimumRunSpeed
            * (flightTime + MaximumJumpRunUpSeconds)
            + JumpLandingHorizontalSafetyMargin;
        if (horizontalDistance > horizontalReach)
        {
            TraceJumpArcFailure(
                launchX,
                launchY,
                targetX,
                targetY,
                $"reach distance={horizontalDistance:0.0} max={horizontalReach:0.0}");
            return false;
        }

        for (var sampleIndex = 1; sampleIndex < JumpClearanceSamples; sampleIndex += 1)
        {
            var t = sampleIndex / (float)JumpClearanceSamples;
            var x = launchX + ((targetX - launchX) * t);
            var elapsed = flightTime * t;
            var y = launchY - (jumpSpeed * elapsed) + (0.5f * gravity * elapsed * elapsed);

            var redBlocked = IntersectsBlockingGeometry(level, x, y, envelope, PlayerTeam.Red, false);
            var blueBlocked = IntersectsBlockingGeometry(level, x, y, envelope, PlayerTeam.Blue, false);
            if (redBlocked && blueBlocked)
            {
                TraceJumpArcFailure(
                    launchX,
                    launchY,
                    targetX,
                    targetY,
                    $"blocked sample={sampleIndex} x={x:0.0} y={y:0.0}");
                return false;
            }
        }

        return true;
    }

    private static void TraceJumpArcFailure(
        float launchX,
        float launchY,
        float targetX,
        float targetY,
        string reason)
    {
        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_JUMP_TRACE") is "1" or "true" or "TRUE"
            && MathF.Abs(launchX - 4490f) < 8f
            && MathF.Abs(launchY - 786f) < 1f
            && MathF.Abs(targetX - 4582f) < 1f
            && MathF.Abs(targetY - 732f) < 1f)
        {
            Console.WriteLine($"[botbrain] alpha-nav jump trace failure={reason}");
        }
    }

    private static bool CanTraverseFallPath(
        SimpleLevel level,
        NavNode source,
        NavNode target,
        NavigationEnvelope envelope,
        IReadOnlyList<float> bendCandidates,
        out float bendX)
    {
        // A falling player normally walks off the source edge before dropping,
        // so a straight diagonal ray is the wrong clearance model for stair
        // lips and shallow ledges. Validate the two conservative phases that
        // the movement runtime actually produces: horizontal departure at the
        // source height, followed by a vertical drop over the landing slot.
        var verticalDistance = target.Y - source.Y;
        var corridorLeft = MathF.Min(source.X, target.X) - 48f;
        var corridorRight = MathF.Max(source.X, target.X) + 48f;
        var candidates = new List<float>(32)
        {
            source.X,
            target.X,
            (source.X + target.X) * 0.5f,
            source.X - 48f,
            source.X + 48f,
            target.X - 48f,
            target.X + 48f,
        };
        var corridorStart = MathF.Round(corridorLeft / 12f) * 12f;
        var corridorEnd = MathF.Round(corridorRight / 12f) * 12f;
        for (var candidate = corridorStart; candidate <= corridorEnd; candidate += 12f)
        {
            candidates.Add(candidate);
        }

        candidates.Sort();
        var previousCandidate = float.NaN;
        foreach (var candidateBendX in candidates)
        {
            if (!float.IsNaN(previousCandidate) && MathF.Abs(candidateBendX - previousCandidate) < 0.5f)
            {
                continue;
            }

            previousCandidate = candidateBendX;
            if (!CanTraverseHorizontalPhase(level, source.X, candidateBendX, source.Y, envelope)
                || !CanTraverseVerticalPhase(level, candidateBendX, source.Y, verticalDistance, envelope)
                || !CanTraverseHorizontalPhase(level, candidateBendX, target.X, target.Y, envelope))
            {
                continue;
            }

            bendX = candidateBendX;
            return true;
        }

        bendX = target.X;
        return false;
    }

    private static bool CanTraverseHorizontalPhase(
        SimpleLevel level,
        float sourceX,
        float targetX,
        float y,
        NavigationEnvelope envelope)
    {
        var distance = MathF.Abs(targetX - sourceX);
        var samples = Math.Max(2, (int)MathF.Ceiling(distance / CollisionSampleSpacing));
        for (var sampleIndex = 1; sampleIndex < samples; sampleIndex += 1)
        {
            var t = sampleIndex / (float)samples;
            var x = sourceX + ((targetX - sourceX) * t);
            if (!IsClearForEitherTeam(level, x, y, envelope))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanTraverseVerticalPhase(
        SimpleLevel level,
        float x,
        float sourceY,
        float verticalDistance,
        NavigationEnvelope envelope)
    {
        var samples = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(verticalDistance) / CollisionSampleSpacing));
        for (var sampleIndex = 1; sampleIndex < samples; sampleIndex += 1)
        {
            var t = sampleIndex / (float)samples;
            var y = sourceY + (verticalDistance * t);
            if (!IsClearForEitherTeam(level, x, y, envelope))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsClearForEitherTeam(SimpleLevel level, float x, float y, NavigationEnvelope envelope)
        => !IntersectsBlockingGeometry(level, x, y, envelope, PlayerTeam.Red, false)
            || !IntersectsBlockingGeometry(level, x, y, envelope, PlayerTeam.Blue, false);

    private static bool IntersectsBlockingGeometry(
        SimpleLevel level,
        float x,
        float y,
        NavigationEnvelope envelope,
        PlayerTeam team,
        bool carryingIntel)
    {
        var left = x + envelope.LeftOffset;
        var top = y + envelope.TopOffset;
        var right = x + envelope.RightOffset;
        var bottom = y + envelope.BottomOffset;
        if (level.IntersectsSolid(left, top, right, bottom))
        {
            return true;
        }

        foreach (var gate in level.GetBlockingTeamGates(team, carryingIntel))
        {
            if (Intersects(left, top, right, bottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return true;
            }
        }

        foreach (var blocker in StaticBlockerCache.GetValue(level, static currentLevel =>
                     new StaticNavigationBlockers(BuildStaticNavigationBlockers(currentLevel))).Items)
        {
            if (Intersects(left, top, right, bottom, blocker.Left, blocker.Top, blocker.Right, blocker.Bottom))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<StaticNavigationBlocker> BuildStaticNavigationBlockers(SimpleLevel level)
    {
        var blockers = new List<StaticNavigationBlocker>();
        for (var index = 0; index < level.RoomObjects.Count; index += 1)
        {
            var roomObject = level.RoomObjects[index];
            if (!level.IsRoomObjectActive(index))
            {
                continue;
            }

            var blocksPlayers = roomObject.Type switch
            {
                RoomObjectType.PlayerWall => true,
                RoomObjectType.Barrier => roomObject.Barrier.Targets.BlocksAnyPlayerMovement(),
                RoomObjectType.DirectionalWall => roomObject.DirectionalWall.AffectsPlayers,
                RoomObjectType.DamageableZone => DamageableMetadata.BlocksPlayers(
                    roomObject.DamageableZone,
                    level.GetDamageableZoneCurrentHealth(index, roomObject)),
                _ => false,
            };
            if (blocksPlayers)
            {
                blockers.Add(new StaticNavigationBlocker(
                    roomObject.Left,
                    roomObject.Top,
                    roomObject.Right,
                    roomObject.Bottom));
            }
        }

        return blockers;
    }

    private static bool Intersects(
        float left,
        float top,
        float right,
        float bottom,
        float otherLeft,
        float otherTop,
        float otherRight,
        float otherBottom) =>
        left < otherRight
        && right > otherLeft
        && top < otherBottom
        && bottom > otherTop;

    private static NavEdgeCompletion CreateCompletion(NavNode target, int? surfaceId)
    {
        var acceptedSurfaces = surfaceId.HasValue
            ? new[] { surfaceId.Value }
            : Array.Empty<int>();
        return new NavEdgeCompletion(
            target.X - 18f,
            target.X + 18f,
            target.Y - 18f,
            target.Y + 18f,
            acceptedSurfaces);
    }

    private static void AddBidirectionalEdge(
        int fromNode,
        int toNode,
        NavEdgeKind kind,
        float cost,
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<List<NavEdge>> adjacency,
        HashSet<NavigationEdgeKey> edgeKeys) =>
        AddBidirectionalEdge(fromNode, toNode, kind, cost, NavEdgeCompletion.None, nodes, adjacency, edgeKeys);

    private static void AddBidirectionalEdge(
        int fromNode,
        int toNode,
        NavEdgeKind kind,
        float cost,
        NavEdgeCompletion completion,
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<List<NavEdge>> adjacency,
        HashSet<NavigationEdgeKey> edgeKeys)
    {
        // This helper is retained for walk edges and anchors. Transition links
        // perform their own directional certification before reaching here.
        AddDirectedEdge(fromNode, toNode, kind, cost, completion, nodes[fromNode], nodes[toNode], adjacency, edgeKeys);
        AddDirectedEdge(toNode, fromNode, ReverseKind(kind), cost, NavEdgeCompletion.None, nodes[toNode], nodes[fromNode], adjacency, edgeKeys);
    }

    private static void AddDirectedEdge(
        int fromNode,
        int toNode,
        NavEdgeKind kind,
        float cost,
        NavEdgeCompletion completion,
        NavNode from,
        NavNode to,
        IReadOnlyList<List<NavEdge>> adjacency,
        HashSet<NavigationEdgeKey> edgeKeys,
        int supportedClassMask = BotBrainClassMask.All,
        NavEdgeLaunchRecipe launchRecipe = default)
    {
        if (fromNode == toNode || fromNode < 0 || toNode < 0)
        {
            return;
        }

        var key = new NavigationEdgeKey(fromNode, toNode, kind);
        if (!edgeKeys.Add(key))
        {
            return;
        }

        adjacency[fromNode].Add(new NavEdge(
            toNode,
            kind,
            MathF.Max(1f, cost),
            completion,
            JumpTriggerTick: kind == NavEdgeKind.Jump ? 0 : 0,
            ProbeTicks: 0,
            ProbeMoveDirectionX: MathF.Sign(to.X - from.X),
            ProbeVariantAttempts: 0,
            ProbeVariantSuccesses: 0,
            SupportedClassMask: supportedClassMask,
            SupportedTeamMask: BotBrainTeamMask.All,
            RequiresGroundedContinuation: kind is NavEdgeKind.Jump or NavEdgeKind.Dropdown
                || kind == NavEdgeKind.Walk
                    && to.Y < from.Y
                    && MathF.Abs(to.Y - from.Y) <= StepHeight * 2f,
            RequiresCarryingIntel: false,
            LaunchRecipe: launchRecipe));
    }

    private static NavEdgeLaunchRecipe CreateJumpLaunchRecipe(
        NavNode source,
        NavNode target,
        NavigationEnvelope envelope)
    {
        var direction = MathF.Sign(target.X - source.X);
        if (direction == 0f)
        {
            return NavEdgeLaunchRecipe.None;
        }

        var launchDistance = MathF.Min(
            MathF.Abs(target.X - source.X),
            envelope.MinimumRunSpeed * MaximumJumpRunUpSeconds);
        var launchEndX = source.X + (direction * launchDistance);
        return new NavEdgeLaunchRecipe(
            StartGrounded: true,
            LaunchTick: 0,
            LaunchMinX: MathF.Min(source.X, launchEndX) - 6f,
            LaunchMaxX: MathF.Max(source.X, launchEndX) + 6f,
            LaunchMinY: source.Y - 6f,
            LaunchMaxY: source.Y + 6f,
            LaunchMinHorizontalSpeed: 0f,
            LaunchMaxHorizontalSpeed: 300f,
            ExpectedMoveDirectionX: direction);
    }

    private static bool RequiresDelayedJumpLaunch(
        SimpleLevel level,
        NavNode source,
        NavNode target,
        NavigationEnvelope envelope)
    {
        if (CanTraverseHorizontalPhase(level, source.X, source.X, source.Y, envelope)
            && CanTraverseJumpArc(level, source.X, source.Y, target.X, target.Y, envelope))
        {
            return false;
        }

        var direction = MathF.Sign(target.X - source.X);
        var launchDistance = MathF.Min(
            MathF.Abs(target.X - source.X),
            envelope.MinimumRunSpeed * MaximumJumpRunUpSeconds);
        var launchX = source.X + (direction * launchDistance);
        return CanTraverseHorizontalPhase(level, source.X, launchX, source.Y, envelope)
            && CanTraverseJumpArc(level, launchX, source.Y, target.X, target.Y, envelope);
    }

    private static int ResolveTallJumpClassMask(
        SimpleLevel level,
        NavNode source,
        NavNode target)
    {
        var supportedMask = 0;
        // The validator's movement search is intentionally authoritative, but
        // it is also much more expensive than the geometric checks used for
        // ordinary edges. Classes are grouped by the same OG2 navigation
        // profile, so certify one representative per profile and expand the
        // result to the classes that share that movement model.
        foreach (var profile in OpenGarrison.BotAI.BotNavigationProfiles.All)
        {
            var definition = OpenGarrison.BotAI.BotNavigationProfiles.GetRepresentativeClassDefinition(profile);
            if (OpenGarrison.BotAI.BotNavigationMovementValidator.TryBuildJumpTape(
                    level,
                    definition,
                    profile,
                    source.X,
                    source.Y,
                    target.X,
                    target.Y,
                    PlayerTeam.Red,
                    out _,
                    out _))
            {
                foreach (var playerClass in Enum.GetValues<PlayerClass>())
                {
                    if (OpenGarrison.BotAI.BotNavigationProfiles.GetProfileForClass(playerClass) == profile)
                    {
                        supportedMask |= BotBrainClassMask.For(playerClass);
                    }
                }
            }
        }

        return supportedMask;
    }

    private static bool IsCompactStairJumpCandidate(
        NavSurface sourceSurface,
        NavSurface targetSurface) =>
        sourceSurface.Right - sourceSurface.Left <= 64f
        || targetSurface.Right - targetSurface.Left <= 64f;

    private static NavEdgeKind ReverseKind(NavEdgeKind kind) => kind switch
    {
        NavEdgeKind.Jump => NavEdgeKind.Fall,
        NavEdgeKind.Fall => NavEdgeKind.Jump,
        NavEdgeKind.Dropdown => NavEdgeKind.Jump,
        _ => NavEdgeKind.Walk,
    };

    private static int FindNearestNodeOnSurface(
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<int> surfaceNodeIndices,
        float x)
    {
        var bestNode = -1;
        var bestDistance = float.MaxValue;
        foreach (var nodeIndex in surfaceNodeIndices)
        {
            var distance = MathF.Abs(nodes[nodeIndex].X - x);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestNode = nodeIndex;
        }

        return bestNode;
    }

    private static IReadOnlyList<int> FindCandidateNodesOnSurface(
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<int> surfaceNodeIndices,
        float x)
    {
        var candidates = new List<int>(3);
        var nearest = FindNearestNodeOnSurface(nodes, surfaceNodeIndices, x);
        if (nearest >= 0)
        {
            candidates.Add(nearest);
        }

        if (surfaceNodeIndices.Count > 0)
        {
            var left = surfaceNodeIndices[0];
            var right = surfaceNodeIndices[^1];
            if (!candidates.Contains(left))
            {
                candidates.Add(left);
            }

            if (!candidates.Contains(right))
            {
                candidates.Add(right);
            }
        }

        return candidates;
    }

    private static float ResolveHorizontalGap(NavSurface left, NavSurface right)
    {
        if (left.Right < right.Left)
        {
            return right.Left - left.Right;
        }

        if (right.Right < left.Left)
        {
            return left.Left - right.Right;
        }

        return 0f;
    }

    private static float ResolveTransitionCost(NavEdgeKind kind, float verticalDelta) => kind switch
    {
        NavEdgeKind.Jump => 36f + MathF.Abs(verticalDelta) * 0.25f,
        NavEdgeKind.Fall => 24f + MathF.Abs(verticalDelta) * 0.08f,
        NavEdgeKind.Dropdown => 18f,
        _ => 0f,
    };

    private static float Distance(NavNode from, NavNode to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static int CountEdges(IReadOnlyList<List<NavEdge>> adjacency) =>
        adjacency.Sum(static edges => edges.Count);

    private static bool IsSurfaceTraceEnabled()
        => Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SURFACE_TRACE") is "1" or "true" or "TRUE";

    private static void SubtractInterval(List<SurfaceInterval> segments, float coveredLeft, float coveredRight)
    {
        for (var index = segments.Count - 1; index >= 0; index -= 1)
        {
            var segment = segments[index];
            var overlapLeft = MathF.Max(segment.Left, coveredLeft);
            var overlapRight = MathF.Min(segment.Right, coveredRight);
            if (overlapRight <= overlapLeft)
            {
                continue;
            }

            segments.RemoveAt(index);
            if (segment.Left < overlapLeft)
            {
                segments.Insert(index, segment with { Right = overlapLeft });
            }

            if (overlapRight < segment.Right)
            {
                segments.Insert(index + (segment.Left < overlapLeft ? 1 : 0), segment with { Left = overlapRight });
            }
        }
    }

    private readonly record struct SurfaceInterval(float Left, float Right, float Top, bool IsDropdown);

    private readonly record struct ClearanceInterval(float Left, float Right);

    private readonly record struct NavSurface(int Id, float Left, float Right, float Top, bool IsDropdown);

    private readonly record struct AnchorRecord(
        int NodeIndex,
        float X,
        float Y,
        PlayerTeam? Team,
        bool IsObjective);

    private readonly record struct NavigationEdgeKey(int FromNode, int ToNode, NavEdgeKind Kind);

    private readonly record struct StaticNavigationBlocker(
        float Left,
        float Top,
        float Right,
        float Bottom);

    private sealed class StaticNavigationBlockers(IReadOnlyList<StaticNavigationBlocker> items)
    {
        public IReadOnlyList<StaticNavigationBlocker> Items { get; } = items;
    }

    private readonly record struct NavigationEnvelope(
        float LeftOffset,
        float TopOffset,
        float RightOffset,
        float BottomOffset,
        float MinimumJumpSpeed,
        float MinimumRunSpeed,
        float MaximumGravityPerSecondSquared)
    {
        public static NavigationEnvelope Create()
        {
            var definitions = Enum.GetValues<PlayerClass>()
                .Select(CharacterClassCatalog.GetDefinition)
                .ToArray();
            return new NavigationEnvelope(
                definitions.Min(static definition => definition.CollisionLeft),
                definitions.Min(static definition => definition.CollisionTop),
                definitions.Max(static definition => definition.CollisionRight),
                definitions.Max(static definition => definition.CollisionBottom),
                definitions.Min(static definition => definition.JumpSpeed),
                definitions.Min(static definition => definition.MaxRunSpeed),
                definitions.Max(static definition => definition.Gravity));
        }
    }
}

/// <summary>
/// Shared runtime cache for the immutable alpha graph. A SimpleLevel is already
/// the lifetime boundary for a match area, so the weak-key cache avoids rebuilding
/// the same graph once per bot controller.
/// </summary>
public static class Og2NavigationGraphStore
{
    private const string ExtendedSweepTicks = "96";
    private static readonly ConditionalWeakTable<SimpleLevel, CachedGraph> Cache = new();
    private static readonly object Sync = new();

    public static NavGraph GetOrBuild(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        // This store is the production entry point for the alpha controller.
        // Keep the runtime path on the same lightweight, class-agnostic contact
        // graph that the acceptance diagnostics validate. Diagnostics may
        // override these values explicitly; an unset value gets the validated
        // production default so a practice match cannot silently fall back to
        // the deprecated graph builder or its slow exploratory sweep.
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_GRAPH", "1");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SWEEP_TICKS")))
        {
            Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SWEEP_TICKS", "32");
        }

        var requestedKey = Og2NavigationGraphCache.BuildKey(level);
        if (Cache.TryGetValue(level, out var cached))
        {
            if (string.Equals(cached.RequestedKey, requestedKey, StringComparison.Ordinal)
                || string.Equals(cached.EffectiveKey, requestedKey, StringComparison.Ordinal))
            {
                return cached.Graph;
            }

            Cache.Remove(level);
        }

        lock (Sync)
        {
            if (Cache.TryGetValue(level, out cached))
            {
                if (string.Equals(cached.RequestedKey, requestedKey, StringComparison.Ordinal)
                    || string.Equals(cached.EffectiveKey, requestedKey, StringComparison.Ordinal))
                {
                    return cached.Graph;
                }

                Cache.Remove(level);
            }

            NavGraph graph;
            var effectiveKey = requestedKey;
            if (Og2NavigationGraphCache.TryLoad(level, requestedKey, out var cachedGraph, out var cachePath))
            {
                graph = cachedGraph;
                TraceCache(level, "hit", cachePath, cachedGraph);
            }
            else
            {
                graph = Og2NavigationGraphBuilder.Build(level);
                Og2NavigationGraphCache.Save(level, requestedKey, graph, out var savedPath);
                TraceCache(level, "miss-built", savedPath, graph);
            }

            // The extended class sweep is a certification/build-time tool. It
            // must not run implicitly during a live practice match: that turns
            // the first bot Think into a multi-second frame stall. Production
            // loads the cached base graph; certification can opt in explicitly
            // with BOTBRAIN_NAV_ALPHA_EXTENDED_SWEEP=1.
            var extendedContactClasses = IsExtendedContactSweepEnabled()
                ? ResolveExtendedContactClasses(level, graph)
                : Array.Empty<PlayerClass>();
            if (extendedContactClasses.Length > 0)
            {
                var previousExtendedClasses = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_EXTENDED_CONTACT_CLASSES");
                var previousBaseSweepTicks = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_BASE_SWEEP_TICKS");
                Environment.SetEnvironmentVariable(
                    "BOTBRAIN_NAV_ALPHA_EXTENDED_CONTACT_CLASSES",
                    string.Join(',', extendedContactClasses));
                try
                {
                    effectiveKey = Og2NavigationGraphCache.BuildKey(
                        level,
                        sweepTicksOverride: ExtendedSweepTicks,
                        contactGraphOverride: "1");
                    if (Og2NavigationGraphCache.TryLoad(level, effectiveKey, out var extendedGraph, out var extendedCachePath))
                    {
                        graph = extendedGraph;
                        TraceCache(level, "hit-extended", extendedCachePath, graph);
                    }
                    else
                    {
                        var previousSweepTicks = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SWEEP_TICKS");
                        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_BASE_SWEEP_TICKS", previousSweepTicks ?? "32");
                        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SWEEP_TICKS", ExtendedSweepTicks);
                        try
                        {
                            graph = Og2NavigationGraphBuilder.Build(level);
                        }
                        finally
                        {
                            Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SWEEP_TICKS", previousSweepTicks);
                        }

                        Og2NavigationGraphCache.Save(level, effectiveKey, graph, out var extendedSavedPath);
                        TraceCache(level, "miss-built-extended", extendedSavedPath, graph);
                    }
                }
                finally
                {
                    Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_EXTENDED_CONTACT_CLASSES", previousExtendedClasses);
                    Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_BASE_SWEEP_TICKS", previousBaseSweepTicks);
                }
            }

            Cache.Add(level, new CachedGraph(requestedKey, effectiveKey, graph));
            return graph;
        }
    }

    private static PlayerClass[] ResolveExtendedContactClasses(SimpleLevel level, NavGraph graph)
    {
        if (!graph.IsOg2Alpha
            || Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SKIP_EXTENDED_SWEEP") is "1" or "true" or "TRUE"
            || string.Equals(
                Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SWEEP_TICKS"),
                ExtendedSweepTicks,
                StringComparison.Ordinal))
        {
            return Array.Empty<PlayerClass>();
        }

        var report = Og2NavigationGraphValidator.Validate(level, graph, ResolveValidationClasses());
        return report.Routes
            .Where(static route => !route.Passed)
            .Select(static route => route.PlayerClass)
            .Distinct()
            .ToArray();
    }

    private static bool IsExtendedContactSweepEnabled() =>
        Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_EXTENDED_SWEEP") is "1" or "true" or "TRUE"
        && Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SKIP_EXTENDED_SWEEP") is not ("1" or "true" or "TRUE");

    private static IReadOnlyList<PlayerClass> ResolveValidationClasses()
    {
        var configured = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_CLASSES");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Enum.GetValues<PlayerClass>();
        }

        var classes = configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var playerClass)
                ? (PlayerClass?)playerClass
                : null)
            .Where(static playerClass => playerClass.HasValue)
            .Select(static playerClass => playerClass!.Value)
            .Distinct()
            .ToArray();

        return classes.Length > 0 ? classes : Enum.GetValues<PlayerClass>();
    }

    private static void TraceCache(SimpleLevel level, string status, string path, NavGraph graph)
    {
        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CACHE_TRACE") is not ("1" or "true" or "TRUE"))
        {
            return;
        }

        var edgeCount = Enumerable.Range(0, graph.NodeCount).Sum(index => graph.GetEdges(index).Length);
        Console.WriteLine(
            $"[botbrain] og2-nav-cache level={level.Name} area={level.MapAreaIndex} " +
            $"status={status} nodes={graph.NodeCount} edges={edgeCount} path=\"{path}\"");
    }

    private sealed record CachedGraph(string RequestedKey, string EffectiveKey, NavGraph Graph);
}
