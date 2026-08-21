using System.Diagnostics;

namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Builds the graph used by maps with the top-down movement model.
///
/// Top-down maps do not have platformer surfaces, jumps, or fall transitions.
/// Their graph is therefore a conservative occupancy grid over the imported
/// walkmask/solid geometry. The graph is shared by every class and team and
/// uses ordinary Walk edges, which keeps runtime route execution identical to
/// the existing bot planner while allowing the movement model to handle both
/// axes directly.
/// </summary>
internal static class TopDownNavigationGraphBuilder
{
    // Forty-eight pixels is acceptable for broad platformer surfaces, but it
    // skips the narrow corridors that are still fully traversable on a
    // top-down walkmask. A 32px lattice keeps the graph small while giving
    // the player envelope enough samples to preserve those corridors.
    private const float PreferredGridSpacing = 16f;
    private const int MaximumGridNodes = 100_000;
    private const int MaximumAnchorLinks = 8;
    private const float TeamBarrierSafetyMargin = 16f;

    public static NavGraph Build(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var stopwatch = Stopwatch.StartNew();
        var envelope = CreateCollisionEnvelope();
        var gridSpacing = ResolveGridSpacing(level.Bounds, PreferredGridSpacing);
        var minX = MathF.Max(-envelope.Left, 0f) + 2f;
        var minY = MathF.Max(-envelope.Top, 0f) + 2f;
        var maxX = MathF.Max(minX, level.Bounds.Width - envelope.Right - 2f);
        var maxY = MathF.Max(minY, level.Bounds.Height - envelope.Bottom - 2f);
        var barriers = level.RoomObjects
            .Select((marker, index) => (Marker: marker, Index: index))
            .Where(static entry => entry.Marker.Type == RoomObjectType.Barrier)
            .ToArray();
        var blockers = level.RoomObjects
            .Where(static marker => marker.Type is
                RoomObjectType.PlayerWall
                or RoomObjectType.Barrier
                or RoomObjectType.DirectionalWall
                or RoomObjectType.TeamGate
                or RoomObjectType.IntelGate
                or RoomObjectType.ControlPointSetupGate)
            // A team-filtered barrier is not a universal navigation wall. It
            // is open to at least one team in the shared graph and is enforced
            // by the runtime collision layer for the team that it blocks.
            // Keeping it here would disconnect the legal spawn exit for the
            // other team (ctf_hangar is the concrete regression case).
            .Select((marker, index) => (Marker: marker, Index: index))
            .Where(static entry => entry.Marker.Type != RoomObjectType.Barrier
                || (entry.Marker.Barrier.Blocks(BarrierTargetKind.RedPlayers)
                    && entry.Marker.Barrier.Blocks(BarrierTargetKind.BluePlayers)))
            .ToArray();

        var nodes = new List<NavNode>();
        var openGridNodes = new List<int>();
        var openGridPositions = new List<GridPosition>();
        var gridColumns = Math.Max(1, (int)MathF.Ceiling((maxX - minX) / gridSpacing) + 1);
        var gridRows = Math.Max(1, (int)MathF.Ceiling((maxY - minY) / gridSpacing) + 1);
        var openGrid = new bool[gridColumns * gridRows];
        var nodeByGridPosition = new Dictionary<(int Column, int Row), int>();

        for (var row = 0; row < gridRows; row += 1)
        {
            var y = MathF.Min(maxY, minY + (row * gridSpacing));
            for (var column = 0; column < gridColumns; column += 1)
            {
                var x = MathF.Min(maxX, minX + (column * gridSpacing));
                var gridIndex = (row * gridColumns) + column;
                if (!IsOpen(level, blockers, x, y, envelope))
                {
                    continue;
                }

                openGrid[gridIndex] = true;
                var nodeIndex = nodes.Count;
                openGridNodes.Add(nodeIndex);
                openGridPositions.Add(new GridPosition(column, row, x, y));
                nodeByGridPosition[(column, row)] = nodeIndex;
                nodes.Add(new NavNode(x, y, NavNodeKind.Surface));
            }
        }

        var objectiveAnchorCount = level.RoomObjects.Count(static roomObject =>
            roomObject.Type is RoomObjectType.ArenaControlPoint
                or RoomObjectType.ControlPoint
                or RoomObjectType.CaptureZone
                or RoomObjectType.Generator);
        // Map bot-spawn triggers are real runtime spawn locations. They are
        // deliberately not copied into RedSpawns/BlueSpawns because they are
        // trigger-driven, but the bot can still begin a route at one of
        // these coordinates. Include them as graph anchors so a trigger
        // spawn in the middle of a top-down map attaches to the same
        // class/team-agnostic walk lattice as ordinary spawns.
        var botSpawnAnchorCount = level.BotSpawns.Count;
        var anchorCount = level.RedSpawns.Count
            + level.BlueSpawns.Count
            + level.IntelBases.Count
            + objectiveAnchorCount
            + botSpawnAnchorCount;
        var adjacency = CreateAdjacency(nodes.Count + anchorCount);
        var edgeKeys = new HashSet<(int From, int To, int SupportedTeamMask, bool? CarryingRequirement)>();
        foreach (var position in openGridPositions)
        {
            var fromNode = nodeByGridPosition[(position.Column, position.Row)];
            for (var rowOffset = -1; rowOffset <= 1; rowOffset += 1)
            {
                for (var columnOffset = -1; columnOffset <= 1; columnOffset += 1)
                {
                    if (columnOffset == 0 && rowOffset == 0)
                    {
                        continue;
                    }

                    var targetColumn = position.Column + columnOffset;
                    var targetRow = position.Row + rowOffset;
                    if (targetColumn < 0 || targetColumn >= gridColumns
                        || targetRow < 0 || targetRow >= gridRows)
                    {
                        continue;
                    }

                    var targetGridIndex = (targetRow * gridColumns) + targetColumn;
                    if (!openGrid[targetGridIndex]
                        || (columnOffset != 0
                            && rowOffset != 0
                            && (!openGrid[(position.Row * gridColumns) + targetColumn]
                                || !openGrid[(targetRow * gridColumns) + position.Column])))
                    {
                        continue;
                    }

                    var toNode = nodeByGridPosition[(targetColumn, targetRow)];
                    if (!IsClear(level, blockers, gridSpacing, position.X, position.Y, nodes[toNode].X, nodes[toNode].Y, envelope))
                    {
                        continue;
                    }

                    AddTopDownWalkEdges(
                        level,
                        barriers,
                        envelope,
                        adjacency,
                        edgeKeys,
                        fromNode,
                        toNode,
                        position.X,
                        position.Y,
                        nodes[toNode].X,
                        nodes[toNode].Y);
                }
            }
        }

        var spawnAnchors = new List<NavSpawnAnchor>(level.RedSpawns.Count + level.BlueSpawns.Count);
        var anchors = new List<TopDownAnchor>();
        foreach (var spawn in level.RedSpawns)
        {
            spawnAnchors.Add(new NavSpawnAnchor(spawn.X, spawn.Y, PlayerTeam.Red));
            anchors.Add(new TopDownAnchor(spawn.X, spawn.Y, NavNodeKind.Spawn));
        }

        foreach (var spawn in level.BlueSpawns)
        {
            spawnAnchors.Add(new NavSpawnAnchor(spawn.X, spawn.Y, PlayerTeam.Blue));
            anchors.Add(new TopDownAnchor(spawn.X, spawn.Y, NavNodeKind.Spawn));
        }

        foreach (var botSpawn in level.BotSpawns)
        {
            spawnAnchors.Add(new NavSpawnAnchor(botSpawn.X, botSpawn.Y, botSpawn.Team));
            anchors.Add(new TopDownAnchor(botSpawn.X, botSpawn.Y, NavNodeKind.Spawn));
        }

        foreach (var intelBase in level.IntelBases)
        {
            anchors.Add(new TopDownAnchor(intelBase.X, intelBase.Y, NavNodeKind.Objective));
        }

        foreach (var roomObject in level.RoomObjects)
        {
            if (roomObject.Type is RoomObjectType.ArenaControlPoint
                or RoomObjectType.ControlPoint
                or RoomObjectType.CaptureZone
                or RoomObjectType.Generator)
            {
                anchors.Add(new TopDownAnchor(roomObject.CenterX, roomObject.CenterY, NavNodeKind.Objective));
            }
        }

        foreach (var anchor in anchors)
        {
            var anchorNode = nodes.Count;
            nodes.Add(new NavNode(anchor.X, anchor.Y, anchor.Kind));
            ConnectAnchor(
                level,
                blockers,
                envelope,
                gridSpacing,
                anchorNode,
                anchor,
                openGridNodes,
                nodes,
                adjacency,
                edgeKeys,
                barriers);
        }

        var graph = new NavGraph(
            nodes.ToArray(),
            adjacency,
            level.Name,
            level.Mode,
            spawnAnchors,
            isOg2Alpha: true);

        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_BUILD_TRACE") is "1" or "true" or "TRUE")
        {
            Console.WriteLine(
                $"[botbrain] topdown-nav build level={level.Name} spacing={gridSpacing:0.0} " +
                $"nodes={graph.NodeCount} edges={CountEdges(adjacency)} " +
                $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.00}");
        }

        return graph;
    }

    private static float ResolveGridSpacing(WorldBounds bounds, float preferred)
    {
        var estimated = MathF.Ceiling(bounds.Width / preferred) * MathF.Ceiling(bounds.Height / preferred);
        return estimated <= MaximumGridNodes
            ? preferred
            : MathF.Max(preferred, MathF.Sqrt((bounds.Width * bounds.Height) / MaximumGridNodes));
    }

    private static CollisionEnvelope CreateCollisionEnvelope()
    {
        var definitions = Enum.GetValues<PlayerClass>()
            .Select(CharacterClassCatalog.GetDefinition)
            .ToArray();
        return new CollisionEnvelope(
            definitions.Min(static definition => definition.CollisionLeft),
            definitions.Min(static definition => definition.CollisionTop),
            definitions.Max(static definition => definition.CollisionRight),
            definitions.Max(static definition => definition.CollisionBottom));
    }

    private static bool IsOpen(
        SimpleLevel level,
        IReadOnlyList<(RoomObjectMarker Marker, int Index)> blockers,
        float x,
        float y,
        CollisionEnvelope envelope)
    {
        var left = x + envelope.Left;
        var top = y + envelope.Top;
        var right = x + envelope.Right;
        var bottom = y + envelope.Bottom;
        if (level.IntersectsSolid(left, top, right, bottom))
        {
            return false;
        }

        foreach (var blocker in blockers)
        {
            if (!level.IsRoomObjectActive(blocker.Index))
            {
                continue;
            }

            var marker = blocker.Marker;
            if (left < marker.Right && right > marker.Left
                && top < marker.Bottom && bottom > marker.Top)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsClear(
        SimpleLevel level,
        IReadOnlyList<(RoomObjectMarker Marker, int Index)> blockers,
        float gridSpacing,
        float fromX,
        float fromY,
        float toX,
        float toY,
        CollisionEnvelope collision)
    {
        var distance = MathF.Sqrt(((toX - fromX) * (toX - fromX)) + ((toY - fromY) * (toY - fromY)));
        var samples = Math.Max(2, (int)MathF.Ceiling(distance / MathF.Max(8f, gridSpacing * 0.5f)));
        for (var sample = 1; sample < samples; sample += 1)
        {
            var fraction = sample / (float)samples;
            if (!IsOpen(level, blockers, fromX + ((toX - fromX) * fraction), fromY + ((toY - fromY) * fraction), collision))
            {
                return false;
            }
        }

        return true;
    }

    private static void ConnectAnchor(
        SimpleLevel level,
        IReadOnlyList<(RoomObjectMarker Marker, int Index)> blockers,
        CollisionEnvelope envelope,
        float gridSpacing,
        int anchorNode,
        TopDownAnchor anchor,
        IReadOnlyList<int> openGridNodes,
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<List<NavEdge>> adjacency,
        HashSet<(int From, int To, int SupportedTeamMask, bool? CarryingRequirement)> edgeKeys,
        IReadOnlyList<(RoomObjectMarker Marker, int Index)> barriers)
    {
        // Filter by a clear connector before applying the link cap. The old
        // radius-first selection could spend all eight links on nearby grid
        // cells hidden behind a blocker, leaving a perfectly valid farther
        // attachment unused. That failure was especially visible for
        // trigger-spawned top-down bots, whose exact spawn point is not
        // necessarily aligned to the lattice.
        var candidates = openGridNodes
            .Select(node => (Node: node, Distance: Distance(anchor.X, anchor.Y, nodes[node].X, nodes[node].Y)))
            .OrderBy(candidate => candidate.Distance)
            // A clear connector should normally be among the nearest few
            // lattice cells. Bound the probe set so a large custom map does
            // not turn each trigger anchor into an all-grid geometry scan.
            .Take(MaximumAnchorLinks * 8)
            .Where(candidate => IsClear(
                level,
                blockers,
                gridSpacing,
                anchor.X,
                anchor.Y,
                nodes[candidate.Node].X,
                nodes[candidate.Node].Y,
                envelope))
            .Take(MaximumAnchorLinks)
            .ToArray();
        foreach (var candidate in candidates)
        {
            AddTopDownWalkEdges(
                level,
                barriers,
                envelope,
                adjacency,
                edgeKeys,
                anchorNode,
                candidate.Node,
                anchor.X,
                anchor.Y,
                nodes[candidate.Node].X,
                nodes[candidate.Node].Y);
            AddTopDownWalkEdges(
                level,
                barriers,
                envelope,
                adjacency,
                edgeKeys,
                candidate.Node,
                anchorNode,
                nodes[candidate.Node].X,
                nodes[candidate.Node].Y,
                anchor.X,
                anchor.Y);
        }
    }

    private static void AddTopDownWalkEdges(
        SimpleLevel level,
        IReadOnlyList<(RoomObjectMarker Marker, int Index)> barriers,
        CollisionEnvelope envelope,
        IReadOnlyList<List<NavEdge>> adjacency,
        HashSet<(int From, int To, int SupportedTeamMask, bool? CarryingRequirement)> edgeKeys,
        int fromNode,
        int toNode,
        float fromX,
        float fromY,
        float toX,
        float toY)
    {
        var (nonCarrierTeams, carrierTeams, crossedFilteredBarrier) = ResolveBarrierConstraints(
            level,
            barriers,
            envelope,
            fromX,
            fromY,
            toX,
            toY);
        if (!crossedFilteredBarrier)
        {
            AddWalkEdge(
                adjacency,
                edgeKeys,
                fromNode,
                toNode,
                fromX,
                fromY,
                toX,
                toY,
                BotBrainTeamMask.All,
                carryingRequirement: null);
            return;
        }

        if (nonCarrierTeams != 0)
        {
            AddWalkEdge(
                adjacency,
                edgeKeys,
                fromNode,
                toNode,
                fromX,
                fromY,
                toX,
                toY,
                nonCarrierTeams,
                carryingRequirement: false);
        }

        if (carrierTeams != 0)
        {
            AddWalkEdge(
                adjacency,
                edgeKeys,
                fromNode,
                toNode,
                fromX,
                fromY,
                toX,
                toY,
                carrierTeams,
                carryingRequirement: true);
        }
    }

    private static (int NonCarrierTeams, int CarrierTeams, bool CrossedFilteredBarrier) ResolveBarrierConstraints(
        SimpleLevel level,
        IReadOnlyList<(RoomObjectMarker Marker, int Index)> barriers,
        CollisionEnvelope envelope,
        float fromX,
        float fromY,
        float toX,
        float toY)
    {
        var nonCarrierTeams = BotBrainTeamMask.All;
        var carrierTeams = BotBrainTeamMask.All;
        var crossedFilteredBarrier = false;
        // Team-filtered barriers are real runtime collision surfaces for one
        // or more traversal states. Add a small margin so a lattice edge that
        // terminates immediately beside a barrier cannot leave the live body
        // inside its expanded collision envelope after movement rounding.
        var left = MathF.Min(fromX + envelope.Left, toX + envelope.Left) - TeamBarrierSafetyMargin;
        var right = MathF.Max(fromX + envelope.Right, toX + envelope.Right) + TeamBarrierSafetyMargin;
        var top = MathF.Min(fromY + envelope.Top, toY + envelope.Top) - TeamBarrierSafetyMargin;
        var bottom = MathF.Max(fromY + envelope.Bottom, toY + envelope.Bottom) + TeamBarrierSafetyMargin;

        foreach (var barrier in barriers)
        {
            if (!level.IsRoomObjectActive(barrier.Index)
                || !BarrierCollision.Intersects(barrier.Marker, left, top, right, bottom))
            {
                continue;
            }

            if (barrier.Marker.Barrier.Blocks(BarrierTargetKind.RedPlayers)
                && barrier.Marker.Barrier.Blocks(BarrierTargetKind.BluePlayers))
            {
                // Universal barriers are already represented by the occupancy
                // test. Keep this guard harmless if a marker overlaps a grid
                // edge due to envelope expansion.
                continue;
            }

            crossedFilteredBarrier = true;
            nonCarrierTeams &= ResolveAllowedTeamMask(barrier.Marker.Barrier, carryingIntel: false);
            carrierTeams &= ResolveAllowedTeamMask(barrier.Marker.Barrier, carryingIntel: true);
        }

        return (nonCarrierTeams, carrierTeams, crossedFilteredBarrier);
    }

    private static int ResolveAllowedTeamMask(BarrierConfiguration barrier, bool carryingIntel)
    {
        var mask = 0;
        if (!BarrierCollision.BlocksPlayerWithoutDirection(barrier, PlayerTeam.Red, carryingIntel))
        {
            mask |= BotBrainTeamMask.For(PlayerTeam.Red);
        }

        if (!BarrierCollision.BlocksPlayerWithoutDirection(barrier, PlayerTeam.Blue, carryingIntel))
        {
            mask |= BotBrainTeamMask.For(PlayerTeam.Blue);
        }

        return mask;
    }

    private static void AddWalkEdge(
        IReadOnlyList<List<NavEdge>> adjacency,
        HashSet<(int From, int To, int SupportedTeamMask, bool? CarryingRequirement)> edgeKeys,
        int fromNode,
        int toNode,
        float fromX,
        float fromY,
        float toX,
        float toY,
        int supportedTeamMask,
        bool? carryingRequirement)
    {
        if (!edgeKeys.Add((fromNode, toNode, supportedTeamMask, carryingRequirement)))
        {
            return;
        }

        adjacency[fromNode].Add(new NavEdge(
            toNode,
            NavEdgeKind.Walk,
            MathF.Max(1f, Distance(fromX, fromY, toX, toY)),
            NavEdgeCompletion.None,
            0,
            0,
            0f,
            0,
            0,
            BotBrainClassMask.All,
            supportedTeamMask,
            false,
            false,
            NavEdgeLaunchRecipe.None,
            carryingRequirement));
    }

    private static List<NavEdge>[] CreateAdjacency(int count)
    {
        var adjacency = new List<NavEdge>[count];
        for (var index = 0; index < count; index += 1)
        {
            adjacency[index] = [];
        }

        return adjacency;
    }

    private static float Distance(float fromX, float fromY, float toX, float toY)
    {
        var deltaX = toX - fromX;
        var deltaY = toY - fromY;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static int CountEdges(IReadOnlyList<List<NavEdge>> adjacency) =>
        adjacency.Sum(static edges => edges.Count);

    private readonly record struct CollisionEnvelope(float Left, float Top, float Right, float Bottom);

    private readonly record struct GridPosition(int Column, int Row, float X, float Y);

    private readonly record struct TopDownAnchor(float X, float Y, NavNodeKind Kind);
}
