using System.Diagnostics;
using System.Globalization;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;

const float ProbeSurfaceLandingHorizontalSlack = 25f;
const float ProbeLandingVerticalSlack = 14f;

var options = BotBrainCanaryOptions.Parse(args);
var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
ContentRoot.Initialize(Path.Combine(repoRoot, "Core", "Content"));

var level = SimpleLevelFactory.CreateImportedLevel(options.MapName, options.AreaIndex)
    ?? throw new InvalidOperationException($"Could not load map '{options.MapName}' area {options.AreaIndex}.");

if (options.DumpRoomObjects)
{
    DumpRoomObjects(level);
    return;
}

var buildStopwatch = Stopwatch.StartNew();
var asset = BotNavigationAssetStore.LoadOrBuild(level);
var graph = BotNavigationAssetBuilder.ToGraph(asset);
buildStopwatch.Stop();

if (options.ProbeFromNode >= 0 && options.ProbeToNode >= 0)
{
    RunProbeTrace(level, asset, options);
    return;
}

var edgeCount = 0;
var walkEdges = 0;
var jumpEdges = 0;
var fallEdges = 0;
var dropdownEdges = 0;
for (var i = 0; i < graph.NodeCount; i += 1)
{
    var edges = graph.GetEdges(i);
    edgeCount += edges.Length;
    for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex += 1)
    {
        switch (edges[edgeIndex].Kind)
        {
            case NavEdgeKind.Walk:
                walkEdges += 1;
                break;
            case NavEdgeKind.Jump:
                jumpEdges += 1;
                break;
            case NavEdgeKind.Fall:
                fallEdges += 1;
                break;
            case NavEdgeKind.Dropdown:
                dropdownEdges += 1;
                break;
        }
    }
}

var world = new SimulationWorld();
if (!world.TryLoadLevel(options.MapName, options.AreaIndex, preservePlayerStats: false))
{
    throw new InvalidOperationException($"SimulationWorld failed to load '{options.MapName}' area {options.AreaIndex}.");
}

world.DespawnEnemyDummy();
world.DespawnFriendlyDummy();
world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, options.Team);
world.LocalPlayer.Kill();

var spawn = world.Level.GetSpawn(options.Team, 0);
world.TrySetNetworkPlayerSpawnOverride(options.BotSlot, spawn.X, spawn.Y);
world.TryPrepareNetworkPlayerJoin(options.BotSlot);
world.TrySetNetworkPlayerTeam(options.BotSlot, options.Team);
if (!world.TryApplyNetworkPlayerClassSelection(options.BotSlot, options.PlayerClass))
{
    throw new InvalidOperationException($"Could not spawn slot {options.BotSlot} as {options.Team} {options.PlayerClass}.");
}

if (!world.TryGetNetworkPlayer(options.BotSlot, out var bot))
{
    throw new InvalidOperationException($"Could not resolve bot slot {options.BotSlot}.");
}

var brain = new BotBrainController();
var goal = ObjectiveEvaluator.EvaluateGoal(bot, world, options.Team, combatTarget: null);
var startNode = graph.FindNearestTraversalStartNode(bot.X, bot.Y);
var exactGoalNode = graph.FindNearestNode(goal.X, goal.Y);
var goalNode = exactGoalNode;
var exactPath = graph.FindPath(startNode, goalNode, options.PlayerClass, team: options.Team);
var path = exactPath;
if (path is null)
{
    goalNode = graph.FindNearestReachableNode(goal.X, goal.Y, startNode, options.PlayerClass, team: options.Team);
    path = graph.FindPath(startNode, goalNode, options.PlayerClass, team: options.Team);
}
var reachableFromStart = CountReachableNodes(graph, startNode, options.PlayerClass, options.Team);
var reachableToGoal = CountReverseReachableNodes(graph, goalNode, options.PlayerClass, options.Team);
var components = BuildUndirectedComponents(graph, asset);
var startComponent = startNode >= 0 ? components.ComponentByNode[startNode] : -1;
var goalComponent = goalNode >= 0 ? components.ComponentByNode[goalNode] : -1;
var exactGoalComponent = exactGoalNode >= 0 ? components.ComponentByNode[exactGoalNode] : -1;
var initialDistance = Distance(bot.X, bot.Y, goal.X, goal.Y);
var bestDistance = initialDistance;
var bestTick = 0;
var previousX = bot.X;
var previousY = bot.Y;
var totalMovement = 0f;
var stagnantWindows = 0;
var lastWindowX = bot.X;
var lastWindowY = bot.Y;
var jumpTicks = 0;
var dropdownTicks = 0;
var fireTicks = 0;
var deadTicks = 0;
var carryingIntelTick = -1;
var scoreTick = -1;
var initialRedCaps = world.RedCaps;
var initialBlueCaps = world.BlueCaps;
var initialPlayerCaps = bot.Caps;
var lastPrintedGoalNode = -1;
var lastPrintedPathCount = -1;
var edgeDiagnostics = new EdgeExecutionDiagnostics();

Console.WriteLine($"map={world.Level.Name} area={world.Level.MapAreaIndex} mode={world.MatchRules.Mode}");
Console.WriteLine($"asset=format{asset.FormatVersion} surfaces={asset.Surfaces.Count} nodes={asset.Nodes.Count} edges={asset.Edges.Count} anchors={asset.Anchors.Count} buildMs={buildStopwatch.Elapsed.TotalMilliseconds:0.0}");
if (asset.BuildStats is { } buildStats)
{
    Console.WriteLine($"buildStats=surfacePairs:{buildStats.SurfacePairChecks} nodePairs:{buildStats.NodePairChecks} probeAttempts:{buildStats.CertifiedProbeAttempts} probeSuccesses:{buildStats.CertifiedProbeSuccesses}");
}
if (asset.ValidationIssues.Count > 0)
{
    Console.WriteLine($"assetValidation=issues:{asset.ValidationIssues.Count} first:{FormatValidationIssue(asset.ValidationIssues[0])}");
}
Console.WriteLine($"graph=nodes:{graph.NodeCount} edges:{edgeCount} walk:{walkEdges} jump:{jumpEdges} fall:{fallEdges} dropdown:{dropdownEdges} startNode:{startNode} goalNode:{goalNode} pathWaypoints:{path?.Count ?? 0} pathCost:{path?.TotalCost ?? -1:0.0}");
Console.WriteLine($"objectiveReachability=rawGoal:({goal.X:0.0},{goal.Y:0.0}) exactGoalNode:{exactGoalNode} exactPathWaypoints:{exactPath?.Count ?? 0} fallbackGoalNode:{goalNode} fallbackUsed:{(goalNode != exactGoalNode).ToString(CultureInfo.InvariantCulture)} exactGoal:{FormatComponent(exactGoalComponent, components)} fallbackGoal:{FormatComponent(goalComponent, components)}");
Console.WriteLine($"reachability=fromStart:{reachableFromStart}/{graph.NodeCount} toGoal:{reachableToGoal}/{graph.NodeCount}");
Console.WriteLine($"components=count:{components.Summaries.Count} start:{FormatComponent(startComponent, components)} goal:{FormatComponent(goalComponent, components)} top:{string.Join(';', components.Summaries.OrderByDescending(static c => c.NodeCount).Take(5).Select(static c => $"#{c.Id}:{c.NodeCount}"))}");
if (path is not null)
{
    Console.WriteLine($"route={FormatRoute(graph, path)}");
}
Console.WriteLine($"bot=slot:{options.BotSlot} team:{options.Team} class:{options.PlayerClass} start=({bot.X:0.0},{bot.Y:0.0}) goal=({goal.X:0.0},{goal.Y:0.0}) initialDistance={initialDistance:0.0} redCaps={world.RedCaps} blueCaps={world.BlueCaps} playerCaps={bot.Caps}");

if (path is null)
{
    Console.WriteLine("result=NoPath");
    Environment.ExitCode = 2;
    return;
}

for (var tick = 1; tick <= options.Ticks; tick += 1)
{
    var input = brain.Think(bot, world, options.Team);
    if (input.Up)
    {
        jumpTicks += 1;
    }

    if (input.Down)
    {
        dropdownTicks += 1;
    }

    if (input.FirePrimary)
    {
        fireTicks += 1;
    }

    var tracePathIndex = brain.CurrentPathIndex;
    var tracePathCount = brain.CurrentPathCount;
    var tracePathNode = brain.CurrentPathNode;
    var traceInput = input;
    var diagnosticPreX = bot.X;
    var diagnosticPreY = bot.Y;
    var diagnosticPreGrounded = bot.IsGrounded;
    var diagnosticPreHorizontalSpeed = bot.HorizontalSpeed;
    var diagnosticPreVerticalSpeed = bot.VerticalSpeed;
    var diagnosticSteering = brain.LastSteeringOutput;
    var diagnosticHasEdge = TryGetActiveEdge(brain.CurrentPath, out var diagnosticFromNode, out var diagnosticToNode, out var diagnosticEdge);
    if (options.PrintPathChanges
        && brain.CurrentPath is not null
        && (brain.CurrentGoalNode != lastPrintedGoalNode || brain.CurrentPathCount != lastPrintedPathCount))
    {
        lastPrintedGoalNode = brain.CurrentGoalNode;
        lastPrintedPathCount = brain.CurrentPathCount;
        Console.WriteLine($"activeRoute tick={tick} carrying={bot.IsCarryingIntel} goalNode={brain.CurrentGoalNode} pathWaypoints={brain.CurrentPathCount} route={FormatRoute(graph, brain.CurrentPath)}");
    }

    if (!world.TrySetNetworkPlayerInput(options.BotSlot, input))
    {
        throw new InvalidOperationException($"Failed to set bot input for slot {options.BotSlot}.");
    }

    world.AdvanceOneTick();

    edgeDiagnostics.Observe(
        tick,
        graph,
        tracePathIndex,
        tracePathCount,
        tracePathNode,
        diagnosticHasEdge,
        diagnosticFromNode,
        diagnosticToNode,
        diagnosticEdge,
        diagnosticSteering,
        diagnosticPreX,
        diagnosticPreY,
        diagnosticPreGrounded,
        diagnosticPreHorizontalSpeed,
        diagnosticPreVerticalSpeed,
        bot,
        input);
    if (edgeDiagnostics.TryConsumeBlocker(out var blockerLine))
    {
        Console.WriteLine(blockerLine);
    }

    var traceCurrentEdge = TryMatchTraceEdge(options, brain.CurrentPath, out var traceFromNode, out var traceToNode, out var traceEdge);
    var recipeTrace = brain.LastSteeringOutput.RecipeTrace;
    if ((options.TraceFromTick > 0 && tick >= options.TraceFromTick && tick <= options.TraceToTick)
        || traceCurrentEdge)
    {
        var nodeText = tracePathNode >= 0
            ? $"{tracePathNode}@({graph.GetNode(tracePathNode).X:0},{graph.GetNode(tracePathNode).Y:0})"
            : "none";
        var edgeText = traceCurrentEdge
            ? $" edge={traceFromNode}->{traceToNode} kind={traceEdge.Kind} completion=({traceEdge.Completion.MinX:0.0},{traceEdge.Completion.MaxX:0.0})x({traceEdge.Completion.MinY:0.0},{traceEdge.Completion.MaxY:0.0}) jumpTick={traceEdge.JumpTriggerTick} groundedContinuation={Bit(traceEdge.RequiresGroundedContinuation)}"
            : string.Empty;
        var recipeText = traceCurrentEdge && recipeTrace.HasRecipe
            ? $" {FormatRecipeTrace(recipeTrace)}"
            : string.Empty;
        Console.WriteLine(
            $"trace tick={tick} pos=({bot.X:0.0},{bot.Y:0.0}) speed=({bot.HorizontalSpeed:0.0},{bot.VerticalSpeed:0.0}) grounded={bot.IsGrounded} carrying={bot.IsCarryingIntel} input=L{Bit(traceInput.Left)}R{Bit(traceInput.Right)}U{Bit(traceInput.Up)}D{Bit(traceInput.Down)}F{Bit(traceInput.FirePrimary)} path={tracePathIndex}/{tracePathCount} node={nodeText}{edgeText}{recipeText}");
    }

    if (!bot.IsAlive)
    {
        deadTicks += 1;
    }

    if (bot.IsCarryingIntel && carryingIntelTick < 0)
    {
        carryingIntelTick = tick;
    }

    if (scoreTick < 0
        && (bot.Caps > initialPlayerCaps
            || world.RedCaps > initialRedCaps
            || world.BlueCaps > initialBlueCaps))
    {
        scoreTick = tick;
    }

    var moved = Distance(previousX, previousY, bot.X, bot.Y);
    totalMovement += moved;
    previousX = bot.X;
    previousY = bot.Y;

    var distance = Distance(bot.X, bot.Y, goal.X, goal.Y);
    if (distance < bestDistance)
    {
        bestDistance = distance;
        bestTick = tick;
    }

    if (tick % options.ReportEveryTicks == 0)
    {
        var windowMove = Distance(lastWindowX, lastWindowY, bot.X, bot.Y);
        if (windowMove < 8f)
        {
            stagnantWindows += 1;
        }

        var currentNode = brain.CurrentPathNode;
        var currentNodeText = currentNode >= 0
            ? $"{currentNode}@({graph.GetNode(currentNode).X:0},{graph.GetNode(currentNode).Y:0})"
            : "none";
        Console.WriteLine(
            $"tick={tick} pos=({bot.X:0.0},{bot.Y:0.0}) dist={distance:0.0} best={bestDistance:0.0}@{bestTick} windowMove={windowMove:0.0} alive={bot.IsAlive} carrying={bot.IsCarryingIntel} redCaps={world.RedCaps} blueCaps={world.BlueCaps} playerCaps={bot.Caps} path={brain.CurrentPathIndex}/{brain.CurrentPathCount} node={currentNodeText}");
        lastWindowX = bot.X;
        lastWindowY = bot.Y;
    }
}

var finalDistance = Distance(bot.X, bot.Y, goal.X, goal.Y);
var finalNode = graph.FindNearestNode(bot.X, bot.Y);
var finalPath = graph.FindPath(finalNode, goalNode, options.PlayerClass, team: options.Team);
var progress = initialDistance - bestDistance;
Console.WriteLine($"result={(scoreTick >= 0 ? "Scored" : carryingIntelTick >= 0 ? "PickedIntel" : progress > 100f ? "Progressed" : "NoUsefulProgress")}");
Console.WriteLine($"summary=finalDistance:{finalDistance:0.0} bestDistance:{bestDistance:0.0} progress:{progress:0.0} totalMovement:{totalMovement:0.0} jumps:{jumpTicks} dropdowns:{dropdownTicks} fire:{fireTicks} deadTicks:{deadTicks} stagnantWindows:{stagnantWindows} carryingIntelTick:{carryingIntelTick} scoreTick:{scoreTick} redCaps:{world.RedCaps} blueCaps:{world.BlueCaps} playerCaps:{bot.Caps} finalNode:{finalNode} finalPathWaypoints:{finalPath?.Count ?? 0}");
Console.WriteLine(edgeDiagnostics.FormatSummary(graph, bot, brain.CurrentPathIndex, brain.CurrentPathCount, brain.CurrentPathNode));

if (path.Count == 0 || progress <= 100f || stagnantWindows >= Math.Max(3, options.Ticks / options.ReportEveryTicks / 2))
{
    Environment.ExitCode = 1;
}

static string FindRepoRoot(string startPath)
{
    var directory = new DirectoryInfo(startPath);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "OpenGarrison.sln"))
            && Directory.Exists(Path.Combine(directory.FullName, "Core")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate OpenGarrison repo root.");
}

static float Distance(float ax, float ay, float bx, float by)
{
    var dx = bx - ax;
    var dy = by - ay;
    return MathF.Sqrt((dx * dx) + (dy * dy));
}

static int Bit(bool value) => value ? 1 : 0;

static string FormatRecipeTrace(SteeringRecipeTrace trace)
{
    var reasons = new List<string>(5);
    if (!trace.StartMatches)
    {
        reasons.Add("start");
    }

    if (trace.RecipeStartGrounded && !trace.CurrentGrounded)
    {
        reasons.Add("grounded");
    }

    if (!trace.InLaunchXWindow)
    {
        reasons.Add("x");
    }

    if (!trace.InLaunchYWindow)
    {
        reasons.Add("y");
    }

    if (!trace.InLaunchSpeedWindow)
    {
        reasons.Add("speed");
    }

    if (!trace.DirectionMatches)
    {
        reasons.Add("dir");
    }

    var status = trace.RecipeReady ? "ready" : $"blocked:{string.Join(',', reasons)}";
    return
        $"recipe={status} edgeTick={trace.EdgeTicks} start=({Bit(trace.StartGrounded)},{trace.StartX:0.0},{trace.StartY:0.0},{trace.StartHorizontalSpeed:0.0},{trace.StartVerticalSpeed:0.0}) " +
        $"launchTick={trace.RecipeLaunchTick} x=[{trace.RecipeLaunchMinX:0.0},{trace.RecipeLaunchMaxX:0.0}] y=[{trace.RecipeLaunchMinY:0.0},{trace.RecipeLaunchMaxY:0.0}] " +
        $"hs=[{trace.RecipeLaunchMinHorizontalSpeed:0.0},{trace.RecipeLaunchMaxHorizontalSpeed:0.0}] dir={trace.RecipeExpectedMoveDirectionX:0} " +
        $"live=({Bit(trace.CurrentGrounded)},{trace.CurrentX:0.0},{trace.CurrentY:0.0},{trace.CurrentHorizontalSpeed:0.0},{trace.CurrentVerticalSpeed:0.0}) " +
        $"move={trace.RequestedMoveDirection:0}/{trace.FinalMoveDirection:0} jump={Bit(trace.RequestedJump)}/{Bit(trace.FinalJump)} suppress={Bit(trace.SuppressJumpUntilLaunch)} dx={trace.SteeringDx:0.0}";
}

static bool TryGetActiveEdge(
    NavPath? path,
    out int fromNode,
    out int toNode,
    out NavEdge edge)
{
    fromNode = -1;
    toNode = -1;
    edge = default;
    if (path is null
        || path.CurrentIndex <= 0
        || path.CurrentIndex >= path.Count
        || !path.TryGetCurrentEdge(out edge))
    {
        return false;
    }

    fromNode = path.GetWaypoint(path.CurrentIndex - 1);
    toNode = path.CurrentNode;
    return true;
}

static bool TryMatchTraceEdge(
    BotBrainCanaryOptions options,
    NavPath? path,
    out int fromNode,
    out int toNode,
    out NavEdge edge)
{
    fromNode = -1;
    toNode = -1;
    edge = default;
    if (options.TraceEdgeFromNode < 0
        || options.TraceEdgeToNode < 0
        || path is null
        || path.CurrentIndex <= 0
        || path.CurrentIndex >= path.Count
        || !path.TryGetCurrentEdge(out edge))
    {
        return false;
    }

    fromNode = path.GetWaypoint(path.CurrentIndex - 1);
    toNode = path.CurrentNode;
    return fromNode == options.TraceEdgeFromNode && toNode == options.TraceEdgeToNode;
}

static void RunProbeTrace(SimpleLevel level, BotNavigationAsset asset, BotBrainCanaryOptions options)
{
    if (options.ProbeFromNode >= asset.Nodes.Count || options.ProbeToNode >= asset.Nodes.Count)
    {
        throw new InvalidOperationException("Probe node index is outside the loaded asset.");
    }

    var from = asset.Nodes[options.ProbeFromNode];
    var to = asset.Nodes[options.ProbeToNode];
    var targetSurface = to.SurfaceId.HasValue
        ? asset.Surfaces.FirstOrDefault(surface => surface.Id == to.SurfaceId.Value)
        : null;
    var classDefinition = CharacterClassCatalog.GetDefinition(options.PlayerClass);
    var direction = (float)Math.Sign(to.X - from.X);
    if (direction == 0f)
    {
        direction = 1f;
    }

    var player = new PlayerEntity(-900_002, classDefinition, "BotBrainProbeTrace");
    player.Spawn(options.Team, from.X, from.Y);
    player.TeleportTo(from.X, from.Y);
    player.ResolveBlockingOverlap(level, options.Team);
    player.RestoreMovementProbeState(isGrounded: true, player.MaxAirJumps, direction);

    var previousInput = default(PlayerInputSnapshot);
    var jumpTick = Math.Max(0, options.ProbeJumpTick);
    var targetSurfaceText = targetSurface is null
        ? "none"
        : targetSurface.Id.ToString(CultureInfo.InvariantCulture);
    Console.WriteLine($"probe=from:{options.ProbeFromNode}@({from.X:0},{from.Y:0}) to:{options.ProbeToNode}@({to.X:0},{to.Y:0}) class:{options.PlayerClass} team:{options.Team} jumpTick:{jumpTick} surface:{targetSurfaceText}");
    if (targetSurface is not null)
    {
        Console.WriteLine($"surface=id:{targetSurface.Id} x:[{targetSurface.LeftX:0.0},{targetSurface.RightX:0.0}] top:{targetSurface.TopY:0.0}");
    }

    for (var tick = 0; tick < options.Ticks; tick += 1)
    {
        var input = new PlayerInputSnapshot(
            Left: direction < 0f,
            Right: direction > 0f,
            Up: tick == jumpTick,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: to.X,
            AimWorldY: to.Y,
            DebugKill: false);
        var jumpPressed = input.Up && !previousInput.Up;
        player.Advance(input, jumpPressed, level, options.Team, 1d / SimulationConfig.DefaultTicksPerSecond);
        previousInput = input;

        var surfaceCompletion = false;
        if (targetSurface is not null)
        {
            var nearestX = Math.Clamp(player.X, targetSurface.LeftX, targetSurface.RightX);
            var horizontalError = MathF.Abs(player.X - nearestX);
            var verticalError = MathF.Abs(player.Bottom - targetSurface.TopY);
            surfaceCompletion = player.IsGrounded
                && horizontalError <= ProbeSurfaceLandingHorizontalSlack
                && verticalError <= ProbeLandingVerticalSlack;
        }

        Console.WriteLine($"probeTick={tick + 1} pos=({player.X:0.0},{player.Y:0.0}) bottom={player.Bottom:0.0} speed=({player.HorizontalSpeed:0.0},{player.VerticalSpeed:0.0}) grounded={player.IsGrounded} input=L{Bit(input.Left)}R{Bit(input.Right)}U{Bit(input.Up)} complete={surfaceCompletion}");
        if (surfaceCompletion)
        {
            break;
        }
    }
}

static void DumpRoomObjects(SimpleLevel level)
{
    Console.WriteLine($"roomObjects=map:{level.Name} area:{level.MapAreaIndex} count:{level.RoomObjects.Count}");
    foreach (var roomObject in level.RoomObjects
                 .OrderBy(static roomObject => roomObject.Type)
                 .ThenBy(static roomObject => roomObject.Left)
                 .ThenBy(static roomObject => roomObject.Top))
    {
        Console.WriteLine(
            $"object=type:{roomObject.Type} source:{roomObject.SourceName} team:{roomObject.Team?.ToString() ?? "None"} bounds:({roomObject.Left:0.0},{roomObject.Top:0.0})-({roomObject.Right:0.0},{roomObject.Bottom:0.0}) center:({roomObject.CenterX:0.0},{roomObject.CenterY:0.0}) value:{roomObject.Value:0.###}");
    }
}

static int CountReachableNodes(NavGraph graph, int startNode, PlayerClass playerClass, PlayerTeam team)
{
    if (startNode < 0)
    {
        return 0;
    }

    var visited = new bool[graph.NodeCount];
    var queue = new Queue<int>();
    visited[startNode] = true;
    queue.Enqueue(startNode);
    while (queue.Count > 0)
    {
        var node = queue.Dequeue();
        var edges = graph.GetEdges(node);
        for (var i = 0; i < edges.Length; i += 1)
        {
            if (!edges[i].Supports(playerClass, team))
            {
                continue;
            }

            var next = edges[i].ToNode;
            if (next < 0 || next >= visited.Length || visited[next])
            {
                continue;
            }

            visited[next] = true;
            queue.Enqueue(next);
        }
    }

    return visited.Count(static value => value);
}

static int CountReverseReachableNodes(NavGraph graph, int goalNode, PlayerClass playerClass, PlayerTeam team)
{
    if (goalNode < 0)
    {
        return 0;
    }

    var reverse = new List<int>[graph.NodeCount];
    for (var i = 0; i < reverse.Length; i += 1)
    {
        reverse[i] = [];
    }

    for (var i = 0; i < graph.NodeCount; i += 1)
    {
        var edges = graph.GetEdges(i);
        for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex += 1)
        {
            if (edges[edgeIndex].Supports(playerClass, team))
            {
                reverse[edges[edgeIndex].ToNode].Add(i);
            }
        }
    }

    var visited = new bool[graph.NodeCount];
    var queue = new Queue<int>();
    visited[goalNode] = true;
    queue.Enqueue(goalNode);
    while (queue.Count > 0)
    {
        var node = queue.Dequeue();
        foreach (var previous in reverse[node])
        {
            if (visited[previous])
            {
                continue;
            }

            visited[previous] = true;
            queue.Enqueue(previous);
        }
    }

    return visited.Count(static value => value);
}

static ComponentDiagnostics BuildUndirectedComponents(NavGraph graph, BotNavigationAsset asset)
{
    var neighbors = new List<int>[graph.NodeCount];
    for (var i = 0; i < neighbors.Length; i += 1)
    {
        neighbors[i] = [];
    }

    for (var i = 0; i < graph.NodeCount; i += 1)
    {
        var edges = graph.GetEdges(i);
        for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex += 1)
        {
            var to = edges[edgeIndex].ToNode;
            neighbors[i].Add(to);
            neighbors[to].Add(i);
        }
    }

    var componentByNode = Enumerable.Repeat(-1, graph.NodeCount).ToArray();
    var summaries = new List<ComponentSummary>();
    for (var node = 0; node < graph.NodeCount; node += 1)
    {
        if (componentByNode[node] >= 0)
        {
            continue;
        }

        var id = summaries.Count;
        var queue = new Queue<int>();
        queue.Enqueue(node);
        componentByNode[node] = id;
        var count = 0;
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            count += 1;
            var assetNode = asset.Nodes[current];
            minX = MathF.Min(minX, assetNode.X);
            maxX = MathF.Max(maxX, assetNode.X);
            minY = MathF.Min(minY, assetNode.Y);
            maxY = MathF.Max(maxY, assetNode.Y);
            foreach (var next in neighbors[current])
            {
                if (componentByNode[next] >= 0)
                {
                    continue;
                }

                componentByNode[next] = id;
                queue.Enqueue(next);
            }
        }

        summaries.Add(new ComponentSummary(id, count, minX, maxX, minY, maxY));
    }

    return new ComponentDiagnostics(componentByNode, summaries);
}

static string FormatComponent(int id, ComponentDiagnostics components)
{
    if (id < 0 || id >= components.Summaries.Count)
    {
        return "none";
    }

    var c = components.Summaries[id];
    return $"#{c.Id} nodes:{c.NodeCount} bounds:({c.MinX:0},{c.MinY:0})-({c.MaxX:0},{c.MaxY:0})";
}

static string FormatValidationIssue(BotNavigationValidationIssueAssetEntry issue)
{
    return $"{issue.Kind} team:{issue.Team?.ToString() ?? "None"} from:{issue.FromNode?.ToString(CultureInfo.InvariantCulture) ?? "none"} to:{issue.ToNode?.ToString(CultureInfo.InvariantCulture) ?? "none"} at:({issue.X:0.0},{issue.Y:0.0}) {issue.Message}";
}

static string FormatRoute(NavGraph graph, NavPath path)
{
    var parts = new List<string>();
    for (var i = 0; i < path.Count; i += 1)
    {
        var nodeIndex = path.GetWaypoint(i);
        var node = graph.GetNode(nodeIndex);
        if (i == 0)
        {
            parts.Add($"{nodeIndex}@({node.X:0},{node.Y:0})");
            continue;
        }

        var edgeKind = path.TryGetIncomingEdge(i, out var edge) ? edge.Kind : NavEdgeKind.Walk;
        parts.Add($"{edgeKind}->{nodeIndex}@({node.X:0},{node.Y:0})");
    }

    return string.Join(" ", parts);
}

internal sealed record ComponentDiagnostics(int[] ComponentByNode, List<ComponentSummary> Summaries);

internal sealed record ComponentSummary(int Id, int NodeCount, float MinX, float MaxX, float MinY, float MaxY);

internal sealed class EdgeExecutionDiagnostics
{
    private const int StallWindowTicks = 90;
    private const float StallWindowMoveThreshold = 16f;
    private const int MinimumBlockerEdgeTicks = 90;

    private string _currentKey = string.Empty;
    private int _currentFromNode = -1;
    private int _currentToNode = -1;
    private NavEdge _currentEdge;
    private int _edgeStartTick;
    private float _edgeStartX;
    private float _edgeStartY;
    private int _windowStartTick;
    private float _windowStartX;
    private float _windowStartY;
    private float _bestTargetDistance = float.MaxValue;
    private float _edgeMovement;
    private int _edgeJumps;
    private int _edgeRecipeReadyTicks;
    private string _firstRecipeReason = "none";
    private string _lastRecipeReason = "none";
    private string _lastPhase = "none";
    private string _pendingBlocker = string.Empty;
    private bool _blockerPrintedForEdge;
    private int _longestEdgeTicks;
    private string _longestEdgeSummary = "none";
    private int _lastObservedTick;

    public void Observe(
        int tick,
        NavGraph graph,
        int pathIndex,
        int pathCount,
        int pathNode,
        bool hasEdge,
        int fromNode,
        int toNode,
        NavEdge edge,
        SteeringOutput steering,
        float preX,
        float preY,
        bool preGrounded,
        float preHorizontalSpeed,
        float preVerticalSpeed,
        PlayerEntity bot,
        PlayerInputSnapshot input)
    {
        _lastObservedTick = tick;
        var key = hasEdge ? $"{fromNode}->{toNode}:{edge.Kind}" : "none";
        if (!string.Equals(_currentKey, key, StringComparison.Ordinal))
        {
            FinalizeCurrentEdge(tick - 1);
            StartEdge(tick, key, fromNode, toNode, edge, preX, preY);
        }

        if (!hasEdge)
        {
            return;
        }

        var moved = Distance(preX, preY, bot.X, bot.Y);
        _edgeMovement += moved;
        if (input.Up)
        {
            _edgeJumps += 1;
        }

        var targetNode = graph.GetNode(toNode);
        _bestTargetDistance = MathF.Min(_bestTargetDistance, Distance(bot.X, bot.Y, targetNode.X, targetNode.Y));
        if (steering.RecipeTrace.HasRecipe)
        {
            var reason = FormatRecipeReason(steering.RecipeTrace);
            _lastRecipeReason = reason;
            if (_firstRecipeReason == "none" && reason != "ready")
            {
                _firstRecipeReason = reason;
            }

            if (steering.RecipeTrace.RecipeReady)
            {
                _edgeRecipeReadyTicks += 1;
            }
        }

        _lastPhase = ResolvePhase(edge, steering, preGrounded, input);
        var edgeTicks = Math.Max(0, tick - _edgeStartTick + 1);
        if (tick - _windowStartTick >= StallWindowTicks)
        {
            var windowMove = Distance(_windowStartX, _windowStartY, bot.X, bot.Y);
            if (!_blockerPrintedForEdge
                && edgeTicks >= MinimumBlockerEdgeTicks
                && windowMove < StallWindowMoveThreshold)
            {
                _pendingBlocker = FormatBlocker(
                    tick,
                    graph,
                    pathIndex,
                    pathCount,
                    pathNode,
                    edgeTicks,
                    windowMove,
                    bot,
                    preGrounded,
                    preHorizontalSpeed,
                    preVerticalSpeed);
                _blockerPrintedForEdge = true;
            }

            _windowStartTick = tick;
            _windowStartX = bot.X;
            _windowStartY = bot.Y;
        }
    }

    public bool TryConsumeBlocker(out string line)
    {
        line = _pendingBlocker;
        _pendingBlocker = string.Empty;
        return !string.IsNullOrEmpty(line);
    }

    public string FormatSummary(NavGraph graph, PlayerEntity bot, int pathIndex, int pathCount, int pathNode)
    {
        FinalizeCurrentEdge(_lastObservedTick);
        var currentNodeText = pathNode >= 0
            ? $"{pathNode}@({graph.GetNode(pathNode).X:0},{graph.GetNode(pathNode).Y:0})"
            : "none";
        return $"edgeMax={_longestEdgeSummary} currentPath={pathIndex}/{pathCount} currentNode={currentNodeText} currentPos=({bot.X:0.0},{bot.Y:0.0})";
    }

    private void StartEdge(int tick, string key, int fromNode, int toNode, NavEdge edge, float x, float y)
    {
        _currentKey = key;
        _currentFromNode = fromNode;
        _currentToNode = toNode;
        _currentEdge = edge;
        _edgeStartTick = tick;
        _edgeStartX = x;
        _edgeStartY = y;
        _windowStartTick = tick;
        _windowStartX = x;
        _windowStartY = y;
        _bestTargetDistance = float.MaxValue;
        _edgeMovement = 0f;
        _edgeJumps = 0;
        _edgeRecipeReadyTicks = 0;
        _firstRecipeReason = "none";
        _lastRecipeReason = "none";
        _lastPhase = "start";
        _blockerPrintedForEdge = false;
    }

    private void FinalizeCurrentEdge(int tick)
    {
        if (string.IsNullOrEmpty(_currentKey) || _currentKey == "none")
        {
            return;
        }

        var edgeTicks = Math.Max(0, tick - _edgeStartTick + 1);
        if (edgeTicks > _longestEdgeTicks)
        {
            _longestEdgeTicks = edgeTicks;
            _longestEdgeSummary =
                $"edge={_currentFromNode}->{_currentToNode} kind={_currentEdge.Kind} ticks={edgeTicks} phase={_lastPhase} bestNodeDist={_bestTargetDistance:0.0} movement={_edgeMovement:0.0} jumps={_edgeJumps} recipeReadyTicks={_edgeRecipeReadyTicks} firstRecipe={_firstRecipeReason} lastRecipe={_lastRecipeReason}";
        }
    }

    private string FormatBlocker(
        int tick,
        NavGraph graph,
        int pathIndex,
        int pathCount,
        int pathNode,
        int edgeTicks,
        float windowMove,
        PlayerEntity bot,
        bool preGrounded,
        float preHorizontalSpeed,
        float preVerticalSpeed)
    {
        var nodeText = pathNode >= 0
            ? $"{pathNode}@({graph.GetNode(pathNode).X:0},{graph.GetNode(pathNode).Y:0})"
            : "none";
        return
            $"blocker=tick:{tick} edge={_currentFromNode}->{_currentToNode} kind={_currentEdge.Kind} phase={_lastPhase} edgeTicks={edgeTicks} " +
            $"windowMove={windowMove:0.0} bestNodeDist={_bestTargetDistance:0.0} movement={_edgeMovement:0.0} jumps={_edgeJumps} " +
            $"recipeReadyTicks={_edgeRecipeReadyTicks} firstRecipe={_firstRecipeReason} lastRecipe={_lastRecipeReason} " +
            $"pos=({bot.X:0.0},{bot.Y:0.0}) pre=({(preGrounded ? 1 : 0)},{preHorizontalSpeed:0.0},{preVerticalSpeed:0.0}) path={pathIndex}/{pathCount} node={nodeText}";
    }

    private static string ResolvePhase(NavEdge edge, SteeringOutput steering, bool grounded, PlayerInputSnapshot input)
    {
        if (steering.RecipeTrace.HasRecipe && !steering.RecipeTrace.RecipeReady)
        {
            return "StageRecipe";
        }

        if (steering.RecipeTrace.HasRecipe && steering.RecipeTrace.RecipeReady && !input.Up)
        {
            return "CommitRecipe";
        }

        if (edge.Kind == NavEdgeKind.Jump && input.Up)
        {
            return "Launch";
        }

        if (!grounded)
        {
            return "Airborne";
        }

        return edge.Completion.HasWindow ? "SeekCompletion" : "Traverse";
    }

    private static string FormatRecipeReason(SteeringRecipeTrace trace)
    {
        if (!trace.HasRecipe)
        {
            return "none";
        }

        if (trace.RecipeReady)
        {
            return "ready";
        }

        var reasons = new List<string>(6);
        if (!trace.StartMatches)
        {
            reasons.Add("start");
        }

        if (trace.RecipeStartGrounded && !trace.CurrentGrounded)
        {
            reasons.Add("grounded");
        }

        if (!trace.InLaunchXWindow)
        {
            reasons.Add("x");
        }

        if (!trace.InLaunchYWindow)
        {
            reasons.Add("y");
        }

        if (!trace.InLaunchSpeedWindow)
        {
            reasons.Add("speed");
        }

        if (!trace.DirectionMatches)
        {
            reasons.Add("dir");
        }

        return reasons.Count == 0 ? "unknown" : string.Join('+', reasons);
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}

internal sealed record BotBrainCanaryOptions(
    string MapName,
    int AreaIndex,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    byte BotSlot,
    int Ticks,
    int ReportEveryTicks,
    int TraceFromTick,
    int TraceToTick,
    int TraceEdgeFromNode,
    int TraceEdgeToNode,
    int ProbeFromNode,
    int ProbeToNode,
    int ProbeJumpTick,
    bool DumpRoomObjects,
    bool PrintPathChanges)
{
    public static BotBrainCanaryOptions Parse(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i += 1)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[key] = args[i + 1];
                i += 1;
            }
            else
            {
                options[key] = "true";
            }
        }

        return new BotBrainCanaryOptions(
            GetString(options, "map", "Truefort"),
            GetInt(options, "area", 1),
            GetEnum(options, "team", PlayerTeam.Red),
            GetEnum(options, "class", PlayerClass.Scout),
            (byte)GetInt(options, "slot", 3),
            GetInt(options, "ticks", 900),
            GetInt(options, "report-every", 30),
            GetInt(options, "trace-from", -1),
            GetInt(options, "trace-to", -1),
            GetInt(options, "trace-edge-from", -1),
            GetInt(options, "trace-edge-to", -1),
            GetInt(options, "probe-from", -1),
            GetInt(options, "probe-to", -1),
            GetInt(options, "probe-jump", 0),
            GetBool(options, "dump-room-objects", false),
            GetBool(options, "print-routes", false));
    }

    private static string GetString(Dictionary<string, string> options, string key, string fallback)
        => options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static int GetInt(Dictionary<string, string> options, string key, int fallback)
        => options.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool GetBool(Dictionary<string, string> options, string key, bool fallback)
        => options.TryGetValue(key, out var value) ? bool.TryParse(value, out var parsed) ? parsed : value == "1" : fallback;

    private static T GetEnum<T>(Dictionary<string, string> options, string key, T fallback)
        where T : struct
        => options.TryGetValue(key, out var value) && Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
