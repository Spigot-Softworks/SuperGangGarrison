using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;

const float ProbeSurfaceLandingHorizontalSlack = 25f;
const float ProbeLandingVerticalSlack = 14f;
const double ColdBuildBudgetMilliseconds = 5_000d;

var artifactJsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
};

var rawOptions = BotBrainToolCommandHelpers.ParseRawOptions(args);
if (rawOptions.TryGetValue("author-traversal-tape", out var traversalScenarioPath))
{
    BotBrainToolCommandHelpers.AuthorTraversalTape(
        traversalScenarioPath,
        rawOptions.TryGetValue("tape-name", out var traversalTapeName) ? traversalTapeName : null);
    return;
}

if (rawOptions.TryGetValue("probe-return-tape", out var probeReturnTape)
    && bool.TryParse(probeReturnTape, out var parsedProbeReturnTape)
    && parsedProbeReturnTape)
{
    BotBrainToolCommandHelpers.ProbeAndAuthorReturnTape(rawOptions);
    return;
}

if (rawOptions.TryGetValue("compile-corridor", out var corridorRecordingPath))
{
    var corridorRecordingMapScale = rawOptions.TryGetValue("recording-map-scale", out var recordingMapScaleText)
        && float.TryParse(recordingMapScaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRecordingMapScale)
            ? parsedRecordingMapScale
            : 0f;
    BotBrainToolCommandHelpers.CompileBotBrainCorridorRecording(
        corridorRecordingPath,
        artifactJsonOptions,
        rawOptions.TryGetValue("install-corridor", out var installCorridor)
        && bool.TryParse(installCorridor, out var parsedInstallCorridor)
        && parsedInstallCorridor,
        rawOptions.TryGetValue("rebuild-asset", out var rebuildCorridorAsset)
        && bool.TryParse(rebuildCorridorAsset, out var parsedRebuildCorridorAsset)
        && parsedRebuildCorridorAsset,
        rawOptions.TryGetValue("bake-corridor-asset", out var bakeCorridorAsset)
        && bool.TryParse(bakeCorridorAsset, out var parsedBakeCorridorAsset)
        && parsedBakeCorridorAsset,
        rawOptions.TryGetValue("install-tape", out var installTape)
        && bool.TryParse(installTape, out var parsedInstallTape)
        && parsedInstallTape,
        corridorRecordingMapScale);
    return;
}

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

var assetStopwatch = Stopwatch.StartNew();
var assetSource = "cold-built";
BotNavigationAsset asset;
if (options.RebuildAsset)
{
    asset = BotNavigationAssetStore.BuildAndSaveRuntimeCache(level);
    assetSource = "rebuilt-runtime-cache";
}
else if (BotNavigationAssetStore.TryLoadShipped(level, out var shippedAsset))
{
    asset = shippedAsset;
    assetSource = "shipped";
}
else if (BotNavigationAssetStore.TryLoadRuntimeCache(level, out var cachedAsset))
{
    asset = cachedAsset;
    assetSource = "runtime-cache";
}
else
{
    asset = BotNavigationAssetStore.BuildAndSaveRuntimeCache(level);
}

if (options.SaveShippedAsset)
{
    BotNavigationAssetStore.SaveShippedSource(asset);
    Console.WriteLine($"savedShippedAsset={BotNavigationAssetStore.GetAssetFileName(asset.LevelName, asset.MapAreaIndex)}");
}

var graph = BotNavigationAssetBuilder.ToGraph(asset, level);
assetStopwatch.Stop();

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

if (float.IsFinite(options.StartX) && float.IsFinite(options.StartY))
{
    bot.TeleportTo(options.StartX, options.StartY);
    bot.ResolveBlockingOverlap(world.Level, options.Team);
    bot.RestoreMovementProbeState(isGrounded: true, bot.MaxAirJumps, options.Team == PlayerTeam.Red ? 1f : -1f);
    Console.WriteLine($"botStartOverride=({bot.X:0.0},{bot.Y:0.0}) requested=({options.StartX:0.0},{options.StartY:0.0})");
}

if (options.SpawnEnemyDummy)
{
    var enemyTeam = options.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
    world.SetEnemyPlayerTeam(enemyTeam);
    world.SpawnEnemyDummy();
    if (float.IsFinite(options.EnemyDummyX) && float.IsFinite(options.EnemyDummyY))
    {
        world.EnemyPlayer.TeleportTo(options.EnemyDummyX, options.EnemyDummyY);
        world.EnemyPlayer.ResolveBlockingOverlap(world.Level, enemyTeam);
    }

    world.SetEnemyInput(default);
    Console.WriteLine($"enemyDummy=enabled team={enemyTeam} pos=({world.EnemyPlayer.X:0.0},{world.EnemyPlayer.Y:0.0})");
}

if (options.DropRedIntel)
{
    world.RedIntel.Drop(options.DropRedIntelX, options.DropRedIntelY, returnTicks: 9000);
    Console.WriteLine($"redIntel=dropped pos=({world.RedIntel.X:0.0},{world.RedIntel.Y:0.0})");
}

if (options.DropBlueIntel)
{
    world.BlueIntel.Drop(options.DropBlueIntelX, options.DropBlueIntelY, returnTicks: 9000);
    Console.WriteLine($"blueIntel=dropped pos=({world.BlueIntel.X:0.0},{world.BlueIntel.Y:0.0})");
}

var brain = new BotBrainController(graph);
var goal = ObjectiveEvaluator.EvaluateGoal(bot, world, options.Team, combatTarget: null);
var startNode = graph.FindNearestTraversalStartNode(bot.X, bot.Y);
var exactGoalNode = world.MatchRules.Mode is GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
    ? graph.FindNearestReachableNode(goal.X, goal.Y, startNode, options.PlayerClass, team: options.Team)
    : graph.FindNearestNode(goal.X, goal.Y);
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
var semanticRecoveryTraces = new List<string>();
var validationIssues = FilterControlMarkerValidationIssues(asset);
var artifactDirectory = ResolveArtifactDirectory(options.ArtifactsDirectory);
var proofCorridorSamples = new List<BotBrainCorridorRecordingSample>();
var installProofTape = rawOptions.TryGetValue("install-proof-tape", out var installProofTapeText)
    && bool.TryParse(installProofTapeText, out var parsedInstallProofTape)
    && parsedInstallProofTape;
var proofTapeName = rawOptions.TryGetValue("proof-tape-name", out var providedProofTapeName)
    ? providedProofTapeName
    : $"{options.MapName}.a{options.AreaIndex}.{options.Team}.{options.PlayerClass}.proof";
var proofTapeSamples = new List<BotBrainProofTapeSample>();

Console.WriteLine($"map={world.Level.Name} area={world.Level.MapAreaIndex} mode={world.MatchRules.Mode}");
Console.WriteLine($"asset=format{asset.FormatVersion} source={assetSource} surfaces={asset.Surfaces.Count} nodes={asset.Nodes.Count} edges={asset.Edges.Count} anchors={asset.Anchors.Count} loadMs={assetStopwatch.Elapsed.TotalMilliseconds:0.0}");
if (asset.BuildStats is { } buildStats)
{
    Console.WriteLine($"buildStats=surfacePairs:{buildStats.SurfacePairChecks} nodePairs:{buildStats.NodePairChecks} probeAttempts:{buildStats.CertifiedProbeAttempts} probeSuccesses:{buildStats.CertifiedProbeSuccesses}");
}
if (validationIssues.Count > 0)
{
    Console.WriteLine($"assetValidation=issues:{validationIssues.Count} first:{FormatValidationIssue(validationIssues[0])}");
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
BotBrainToolCommandHelpers.AddProofCorridorSample(proofCorridorSamples, world, bot, default, tick: 0, "Start");

if (path is null)
{
    Console.WriteLine("result=NoPath");
    const string noPathResult = "NoPath";
    const string noPathFailureBucket = "NoGraphPath";
    Console.WriteLine($"failureBucket={noPathFailureBucket}");
    WriteArtifacts(
        artifactDirectory,
        artifactJsonOptions,
        options,
        world,
        asset,
        assetSource,
        assetStopwatch.Elapsed.TotalMilliseconds,
        edgeCount,
        walkEdges,
        jumpEdges,
        fallEdges,
        dropdownEdges,
        goal,
        startNode,
        exactGoalNode,
        goalNode,
        exactPath?.Count ?? 0,
        path?.Count ?? 0,
        path?.TotalCost ?? -1f,
        reachableFromStart,
        reachableToGoal,
        components,
        startComponent,
        exactGoalComponent,
        goalComponent,
        noPathResult,
        noPathFailureBucket,
        initialDistance,
        initialDistance,
        initialDistance,
        0,
        0f,
        0f,
        0,
        0,
        0,
        0,
        0,
        -1,
        -1,
        initialRedCaps,
        initialBlueCaps,
        initialPlayerCaps,
        world.RedCaps,
        world.BlueCaps,
        bot.Caps,
        graph.FindNearestNode(bot.X, bot.Y),
        0,
        "edgeMax=none",
        edgeDiagnostics,
        semanticRecoveryTraces);
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
    if (!string.IsNullOrWhiteSpace(brain.LastSemanticRecoveryTrace))
    {
        semanticRecoveryTraces.Add(brain.LastSemanticRecoveryTrace);
        Console.WriteLine($"tick:{tick} {brain.LastSemanticRecoveryTrace}");
    }

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
        var directDriveText = !string.IsNullOrWhiteSpace(brain.LastDirectDriveTrace)
            ? $" {brain.LastDirectDriveTrace}"
            : string.Empty;
        var tapeText = !string.IsNullOrWhiteSpace(brain.LastObjectiveTapeTrace)
            ? $" {brain.LastObjectiveTapeTrace}"
            : string.Empty;
        Console.WriteLine(
            $"trace tick={tick} pos=({bot.X:0.0},{bot.Y:0.0}) speed=({bot.HorizontalSpeed:0.0},{bot.VerticalSpeed:0.0}) grounded={bot.IsGrounded} carrying={bot.IsCarryingIntel} input=L{Bit(traceInput.Left)}R{Bit(traceInput.Right)}U{Bit(traceInput.Up)}D{Bit(traceInput.Down)}F{Bit(traceInput.FirePrimary)} path={tracePathIndex}/{tracePathCount} node={nodeText}{edgeText}{recipeText}{directDriveText}{tapeText}");
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
        BotBrainToolCommandHelpers.AddProofCorridorSample(proofCorridorSamples, world, bot, input, tick, "Score");
    }
    else if (scoreTick < 0 && BotBrainToolCommandHelpers.ShouldRecordProofCorridorSample(proofCorridorSamples, bot, input, tick, carryingIntelTick))
    {
        BotBrainToolCommandHelpers.AddProofCorridorSample(proofCorridorSamples, world, bot, input, tick, "Stride");
    }

    if (carryingIntelTick >= 0 && (scoreTick < 0 || scoreTick == tick))
    {
        proofTapeSamples.Add(new BotBrainProofTapeSample(
            Tick: tick,
            X: bot.X,
            Y: bot.Y,
            Bottom: bot.Bottom,
            HorizontalSpeed: bot.HorizontalSpeed,
            VerticalSpeed: bot.VerticalSpeed,
            IsGrounded: bot.IsGrounded,
            Left: input.Left,
            Right: input.Right,
            Up: input.Up,
            Down: input.Down,
            IsCarryingIntel: true));
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
var result = scoreTick >= 0 ? "Scored" : carryingIntelTick >= 0 ? "PickedIntel" : progress > 100f ? "Progressed" : "NoUsefulProgress";
var failureBucket = ClassifyFailureBucket(
    result,
    assetSource,
    assetStopwatch.Elapsed.TotalMilliseconds,
    validationIssues.Count,
    exactPath is null,
    goalNode != exactGoalNode,
    reachableFromStart,
    reachableToGoal,
    graph.NodeCount,
    edgeDiagnostics,
    semanticRecoveryTraces,
    fireTicks,
    stagnantWindows,
    options.Ticks,
    options.ReportEveryTicks);
Console.WriteLine($"result={result}");
Console.WriteLine($"summary=finalDistance:{finalDistance:0.0} bestDistance:{bestDistance:0.0} progress:{progress:0.0} totalMovement:{totalMovement:0.0} jumps:{jumpTicks} dropdowns:{dropdownTicks} fire:{fireTicks} deadTicks:{deadTicks} stagnantWindows:{stagnantWindows} carryingIntelTick:{carryingIntelTick} scoreTick:{scoreTick} redCaps:{world.RedCaps} blueCaps:{world.BlueCaps} playerCaps:{bot.Caps} finalNode:{finalNode} finalPathWaypoints:{finalPath?.Count ?? 0}");
Console.WriteLine($"failureBucket={failureBucket}");
var proofPassed = scoreTick >= 0
    && validationIssues.Count == 0
    && exactPath is not null
    && goalNode == exactGoalNode;
Console.WriteLine($"proofPassed={proofPassed}");
if (installProofTape)
{
    if (scoreTick >= 0)
    {
        var proofTape = BotBrainToolCommandHelpers.BuildObjectiveTapeFromProofSamples(
            level,
            proofTapeName,
            options.Team,
            options.PlayerClass,
            proofTapeSamples);
        var proofTapePath = BotBrainObjectiveTapeStore.UpsertTape(level, proofTape);
        Console.WriteLine($"proofTape=installed path={proofTapePath} rawSamples={proofTapeSamples.Count} tapeSamples={proofTape.Segments.Sum(static segment => segment.Samples.Count)}");
    }
    else
    {
        Console.WriteLine($"proofTape=skipped reason:not_scored rawSamples={proofTapeSamples.Count}");
    }
}
if (options.AutoBakeProofCorridor)
{
    if (scoreTick >= 0)
    {
        var candidateAsset = CloneAsset(asset);
        var proofSegments = BotBrainToolCommandHelpers.BuildAutoProofCorridorSegments(level, proofCorridorSamples, options.Team);
        var proofBakeStats = BotBrainToolCommandHelpers.BakeIsolatedProofCorridorIntoAsset(level, candidateAsset, proofSegments, options.Team, options.PlayerClass);
        var candidateProof = EvaluateCandidateAsset(candidateAsset);
        var accepted = ShouldAcceptCandidateProof(
            baselineScoreTick: scoreTick,
            baselineTotalMovement: totalMovement,
            baselineJumpTicks: jumpTicks,
            baselineSemanticRecoveries: semanticRecoveryTraces.Count,
            candidateProof);
        if (accepted && options.AcceptProofCorridorBake)
        {
            BotNavigationAssetStore.SaveRuntimeCache(candidateAsset);
            if (options.SaveShippedAsset)
            {
                BotNavigationAssetStore.SaveShippedSource(candidateAsset);
            }
        }

        Console.WriteLine(
            $"autoProofCorridor={(accepted ? options.AcceptProofCorridorBake ? "saved" : "accepted_candidate" : "rejected")} " +
            $"segments:{proofSegments.Count} samples:{proofCorridorSamples.Count} nodesAdded:{proofBakeStats.NodesAdded} edgesAdded:{proofBakeStats.EdgesAdded} surfacesAdded:{proofBakeStats.SurfacesAdded} " +
            $"candidateScoreTick:{candidateProof.ScoreTick} baselineScoreTick:{scoreTick} candidateMove:{candidateProof.TotalMovement:0.0} baselineMove:{totalMovement:0.0} " +
            $"candidateJumps:{candidateProof.JumpTicks} baselineJumps:{jumpTicks} candidateRecoveries:{candidateProof.SemanticRecoveries} baselineRecoveries:{semanticRecoveryTraces.Count} " +
            $"runtimeCache:{(accepted && options.AcceptProofCorridorBake)} shipped:{(accepted && options.AcceptProofCorridorBake && options.SaveShippedAsset)}");
    }
    else
    {
        Console.WriteLine($"autoProofCorridor=skipped reason:not_scored samples:{proofCorridorSamples.Count}");
    }
}
var edgeSummary = edgeDiagnostics.FormatSummary(graph, bot, brain.CurrentPathIndex, brain.CurrentPathCount, brain.CurrentPathNode);
Console.WriteLine(edgeSummary);
foreach (var profileLine in FormatCaptureProfileLines(edgeDiagnostics, graph, carryingIntelTick, scoreTick, totalMovement))
{
    Console.WriteLine(profileLine);
}

WriteArtifacts(
    artifactDirectory,
    artifactJsonOptions,
    options,
    world,
    asset,
    assetSource,
    assetStopwatch.Elapsed.TotalMilliseconds,
    edgeCount,
    walkEdges,
    jumpEdges,
    fallEdges,
    dropdownEdges,
    goal,
    startNode,
    exactGoalNode,
    goalNode,
    exactPath?.Count ?? 0,
    path?.Count ?? 0,
    path?.TotalCost ?? -1f,
    reachableFromStart,
    reachableToGoal,
    components,
    startComponent,
    exactGoalComponent,
    goalComponent,
    result,
    failureBucket,
    initialDistance,
    finalDistance,
    bestDistance,
    bestTick,
    progress,
    totalMovement,
    jumpTicks,
    dropdownTicks,
    fireTicks,
    deadTicks,
    stagnantWindows,
    carryingIntelTick,
    scoreTick,
    initialRedCaps,
    initialBlueCaps,
    initialPlayerCaps,
    world.RedCaps,
    world.BlueCaps,
    bot.Caps,
    finalNode,
    finalPath?.Count ?? 0,
    edgeSummary,
    edgeDiagnostics,
    semanticRecoveryTraces);

if (!proofPassed)
{
    Environment.ExitCode = 1;
}

string? ResolveArtifactDirectory(string artifactsDirectory)
{
    if (string.IsNullOrWhiteSpace(artifactsDirectory))
    {
        return null;
    }

    var directory = Path.GetFullPath(artifactsDirectory);
    Directory.CreateDirectory(directory);
    return directory;
}

static IEnumerable<string> FormatCaptureProfileLines(
    EdgeExecutionDiagnostics edgeDiagnostics,
    NavGraph graph,
    int carryingIntelTick,
    int scoreTick,
    float totalMovement)
{
    if (scoreTick < 0)
    {
        yield break;
    }

    const float ticksPerSecond = SimulationConfig.DefaultTicksPerSecond;
    var pickupTicks = carryingIntelTick >= 0 ? carryingIntelTick : -1;
    var returnTicks = carryingIntelTick >= 0 ? scoreTick - carryingIntelTick : -1;
    var ticksPerHundredPixels = totalMovement > 0f
        ? scoreTick / Math.Max(1f, totalMovement / 100f)
        : 0f;
    yield return
        $"captureProfile=scoreTicks:{scoreTick} scoreSeconds:{scoreTick / ticksPerSecond:0.0} " +
        $"pickupTick:{pickupTicks} pickupSeconds:{(pickupTicks >= 0 ? pickupTicks / ticksPerSecond : -1):0.0} " +
        $"returnTicks:{returnTicks} returnSeconds:{(returnTicks >= 0 ? returnTicks / ticksPerSecond : -1):0.0} " +
        $"movement:{totalMovement:0.0} ticksPer100px:{ticksPerHundredPixels:0.0} edges:{edgeDiagnostics.Edges.Count} blockers:{edgeDiagnostics.Blockers.Count}";

    var scoredEdges = edgeDiagnostics.Edges
        .Where(edge => edge.StartTick <= scoreTick)
        .ToArray();
    if (scoredEdges.Length == 0)
    {
        yield break;
    }

    var outboundEdges = carryingIntelTick >= 0
        ? scoredEdges.Where(edge => edge.StartTick < carryingIntelTick).ToArray()
        : scoredEdges;
    var returnEdges = carryingIntelTick >= 0
        ? scoredEdges.Where(edge => edge.StartTick >= carryingIntelTick).ToArray()
        : [];

    yield return FormatCaptureStage("captureStage=outbound", outboundEdges);
    if (carryingIntelTick >= 0)
    {
        yield return FormatCaptureStage("captureStage=return", returnEdges);
    }

    foreach (var edge in scoredEdges
        .OrderByDescending(static edge => edge.Ticks)
        .ThenByDescending(static edge => edge.Movement)
        .Take(8))
    {
        yield return FormatProfileEdge("slow", edge, graph);
    }

    var repeatedEdges = scoredEdges
        .GroupBy(static edge => $"{edge.FromNode}->{edge.ToNode}/{edge.Kind}")
        .Select(static group => new
        {
            Key = group.Key,
            Count = group.Count(),
            Ticks = group.Sum(static edge => edge.Ticks),
            Movement = group.Sum(static edge => edge.Movement),
            First = group.Min(static edge => edge.StartTick),
            Last = group.Max(static edge => edge.EndTick),
        })
        .Where(static group => group.Count > 1)
        .OrderByDescending(static group => group.Ticks)
        .Take(6);
    foreach (var group in repeatedEdges)
    {
        yield return
            $"captureRepeat=edge:{group.Key} count:{group.Count} ticks:{group.Ticks} seconds:{group.Ticks / ticksPerSecond:0.0} " +
            $"movement:{group.Movement:0.0} firstTick:{group.First} lastTick:{group.Last}";
    }

    foreach (var blocker in edgeDiagnostics.Blockers.Take(8))
    {
        yield return
            $"captureBlocker=tick:{blocker.Tick} edge:{blocker.FromNode}->{blocker.ToNode}/{blocker.Kind} phase:{blocker.Phase} " +
            $"edgeTicks:{blocker.EdgeTicks} windowMove:{blocker.WindowMove:0.0} bestNodeDist:{blocker.BestNodeDistance:0.0} " +
            $"movement:{blocker.Movement:0.0} jumps:{blocker.Jumps} recipeReadyTicks:{blocker.RecipeReadyTicks} " +
            $"pos:({blocker.X:0.0},{blocker.Y:0.0}) path:{blocker.PathIndex}/{blocker.PathCount}";
    }
}

static string FormatCaptureStage(string prefix, IReadOnlyCollection<EdgeExecutionArtifact> edges)
{
    const float ticksPerSecond = SimulationConfig.DefaultTicksPerSecond;
    var ticks = edges.Sum(static edge => edge.Ticks);
    var movement = edges.Sum(static edge => edge.Movement);
    var jumps = edges.Sum(static edge => edge.Jumps);
    var stageRecipeTicks = edges.Where(static edge => edge.Phase == "StageRecipe").Sum(static edge => edge.Ticks);
    var airborneTicks = edges.Where(static edge => edge.Phase == "Airborne").Sum(static edge => edge.Ticks);
    var maxEdgeTicks = edges.Count > 0 ? edges.Max(static edge => edge.Ticks) : 0;
    return
        $"{prefix} edges:{edges.Count} ticks:{ticks} seconds:{ticks / ticksPerSecond:0.0} movement:{movement:0.0} " +
        $"jumps:{jumps} stageRecipeTicks:{stageRecipeTicks} airborneTicks:{airborneTicks} maxEdgeTicks:{maxEdgeTicks}";
}

static string FormatProfileEdge(string label, EdgeExecutionArtifact edge, NavGraph graph)
{
    const float ticksPerSecond = SimulationConfig.DefaultTicksPerSecond;
    var from = graph.GetNode(edge.FromNode);
    var to = graph.GetNode(edge.ToNode);
    return
        $"captureEdge={label} edge:{edge.FromNode}->{edge.ToNode}/{edge.Kind} ticks:{edge.Ticks} seconds:{edge.Ticks / ticksPerSecond:0.0} " +
        $"phase:{edge.Phase} movement:{edge.Movement:0.0} jumps:{edge.Jumps} bestNodeDist:{edge.BestNodeDistance:0.0} " +
        $"recipeReadyTicks:{edge.RecipeReadyTicks} firstRecipe:{edge.FirstRecipeReason} lastRecipe:{edge.LastRecipeReason} " +
        $"from:({from.X:0},{from.Y:0}) to:({to.X:0},{to.Y:0}) startTick:{edge.StartTick} endTick:{edge.EndTick}";
}

BotNavigationAsset CloneAsset(BotNavigationAsset source) =>
    JsonSerializer.Deserialize<BotNavigationAsset>(JsonSerializer.Serialize(source))!
    ?? throw new InvalidOperationException("Failed to clone BotBrain asset.");

BotBrainProofEvaluation EvaluateCandidateAsset(BotNavigationAsset candidateAsset)
{
    var candidateWorld = new SimulationWorld();
    if (!candidateWorld.TryLoadLevel(options.MapName, options.AreaIndex, preservePlayerStats: false))
    {
        return new BotBrainProofEvaluation(false, -1, 0f, 0, 0, "load_failed");
    }

    var candidateGraph = BotNavigationAssetBuilder.ToGraph(candidateAsset, candidateWorld.Level);

    candidateWorld.DespawnEnemyDummy();
    candidateWorld.DespawnFriendlyDummy();
    candidateWorld.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, options.Team);
    candidateWorld.LocalPlayer.Kill();

    var candidateSpawn = candidateWorld.Level.GetSpawn(options.Team, 0);
    candidateWorld.TrySetNetworkPlayerSpawnOverride(options.BotSlot, candidateSpawn.X, candidateSpawn.Y);
    candidateWorld.TryPrepareNetworkPlayerJoin(options.BotSlot);
    candidateWorld.TrySetNetworkPlayerTeam(options.BotSlot, options.Team);
    if (!candidateWorld.TryApplyNetworkPlayerClassSelection(options.BotSlot, options.PlayerClass)
        || !candidateWorld.TryGetNetworkPlayer(options.BotSlot, out var candidateBot))
    {
        return new BotBrainProofEvaluation(false, -1, 0f, 0, 0, "spawn_failed");
    }

    var candidateGoal = ObjectiveEvaluator.EvaluateGoal(candidateBot, candidateWorld, options.Team, combatTarget: null);
    var candidateStartNode = candidateGraph.FindNearestTraversalStartNode(candidateBot.X, candidateBot.Y);
    var candidateGoalNode = candidateGraph.FindNearestNode(candidateGoal.X, candidateGoal.Y);
    var candidateExactPath = candidateGraph.FindPath(candidateStartNode, candidateGoalNode, options.PlayerClass, team: options.Team);
    if (candidateExactPath is null)
    {
        return new BotBrainProofEvaluation(false, -1, 0f, 0, 0, "no_exact_path");
    }

    var candidateBrain = new BotBrainController(candidateGraph);
    var previousCandidateX = candidateBot.X;
    var previousCandidateY = candidateBot.Y;
    var candidateMovement = 0f;
    var candidateJumpTicks = 0;
    var candidateSemanticRecoveries = 0;
    var initialCandidateRedCaps = candidateWorld.RedCaps;
    var initialCandidateBlueCaps = candidateWorld.BlueCaps;
    var initialCandidatePlayerCaps = candidateBot.Caps;

    for (var tick = 1; tick <= options.Ticks; tick += 1)
    {
        var input = candidateBrain.Think(candidateBot, candidateWorld, options.Team);
        if (input.Up)
        {
            candidateJumpTicks += 1;
        }

        if (!candidateWorld.TrySetNetworkPlayerInput(options.BotSlot, input))
        {
            return new BotBrainProofEvaluation(false, -1, candidateMovement, candidateJumpTicks, candidateSemanticRecoveries, "input_failed");
        }

        candidateWorld.AdvanceOneTick();
        candidateMovement += Distance(previousCandidateX, previousCandidateY, candidateBot.X, candidateBot.Y);
        previousCandidateX = candidateBot.X;
        previousCandidateY = candidateBot.Y;
        if (!string.IsNullOrWhiteSpace(candidateBrain.LastSemanticRecoveryTrace))
        {
            candidateSemanticRecoveries += 1;
        }

        if (candidateBot.Caps > initialCandidatePlayerCaps
            || candidateWorld.RedCaps > initialCandidateRedCaps
            || candidateWorld.BlueCaps > initialCandidateBlueCaps)
        {
            return new BotBrainProofEvaluation(true, tick, candidateMovement, candidateJumpTicks, candidateSemanticRecoveries, string.Empty);
        }
    }

    return new BotBrainProofEvaluation(false, -1, candidateMovement, candidateJumpTicks, candidateSemanticRecoveries, "not_scored");
}

static bool ShouldAcceptCandidateProof(
    int baselineScoreTick,
    float baselineTotalMovement,
    int baselineJumpTicks,
    int baselineSemanticRecoveries,
    BotBrainProofEvaluation candidate)
{
    if (!candidate.Scored || baselineScoreTick < 0 || candidate.ScoreTick < 0)
    {
        return false;
    }

    if (candidate.ScoreTick > baselineScoreTick)
    {
        return false;
    }

    if (candidate.TotalMovement > baselineTotalMovement * 1.02f)
    {
        return false;
    }

    if (candidate.SemanticRecoveries > baselineSemanticRecoveries)
    {
        return false;
    }

    return candidate.ScoreTick < baselineScoreTick
        || candidate.TotalMovement < baselineTotalMovement * 0.98f
        || candidate.JumpTicks < baselineJumpTicks;
}

void WriteArtifacts(
    string? artifactDirectory,
    JsonSerializerOptions jsonOptions,
    BotBrainCanaryOptions options,
    SimulationWorld world,
    BotNavigationAsset asset,
    string assetSource,
    double assetLoadMilliseconds,
    int edgeCount,
    int walkEdges,
    int jumpEdges,
    int fallEdges,
    int dropdownEdges,
    (float X, float Y) goal,
    int startNode,
    int exactGoalNode,
    int fallbackGoalNode,
    int exactPathWaypoints,
    int pathWaypoints,
    float pathCost,
    int reachableFromStart,
    int reachableToGoal,
    ComponentDiagnostics components,
    int startComponent,
    int exactGoalComponent,
    int fallbackGoalComponent,
    string result,
    string failureBucket,
    float initialDistance,
    float finalDistance,
    float bestDistance,
    int bestTick,
    float progress,
    float totalMovement,
    int jumpTicks,
    int dropdownTicks,
    int fireTicks,
    int deadTicks,
    int stagnantWindows,
    int carryingIntelTick,
    int scoreTick,
    int initialRedCaps,
    int initialBlueCaps,
    int initialPlayerCaps,
    int finalRedCaps,
    int finalBlueCaps,
    int finalPlayerCaps,
    int finalNode,
    int finalPathWaypoints,
    string edgeSummary,
    EdgeExecutionDiagnostics edgeDiagnostics,
    IReadOnlyList<string> semanticRecoveryTraces)
{
    if (artifactDirectory is null)
    {
        return;
    }

    WriteJson(
        Path.Combine(artifactDirectory, "run.json"),
        new
        {
            map = world.Level.Name,
            area = world.Level.MapAreaIndex,
            mode = world.MatchRules.Mode.ToString(),
            team = options.Team.ToString(),
            playerClass = options.PlayerClass.ToString(),
            ticks = options.Ticks,
            result,
            failureBucket,
            assetSource,
            assetLoadMilliseconds,
            coldBuildBudgetMilliseconds = ColdBuildBudgetMilliseconds,
            initialDistance,
            finalDistance,
            bestDistance,
            bestTick,
            progress,
            totalMovement,
            jumpTicks,
            dropdownTicks,
            fireTicks,
            deadTicks,
            stagnantWindows,
            carryingIntelTick,
            scoreTick,
            initialRedCaps,
            initialBlueCaps,
            initialPlayerCaps,
            finalRedCaps,
            finalBlueCaps,
            finalPlayerCaps,
            finalNode,
            finalPathWaypoints,
            edgeSummary,
            semanticRecoveryTraces,
        },
        jsonOptions);

    WriteJson(
        Path.Combine(artifactDirectory, "asset.json"),
        new
        {
            formatVersion = asset.FormatVersion,
            asset.LevelName,
            asset.MapAreaIndex,
            asset.LevelFingerprint,
            assetSource,
            assetLoadMilliseconds,
            surfaces = asset.Surfaces.Count,
            nodes = asset.Nodes.Count,
            edges = asset.Edges.Count,
            graphEdges = edgeCount,
            walkEdges,
            jumpEdges,
            fallEdges,
            dropdownEdges,
            anchors = asset.Anchors.Count,
            portals = asset.Portals.Count,
            asset.BuildStats,
            validationIssues = asset.ValidationIssues,
        },
        jsonOptions);

    WriteJson(
        Path.Combine(artifactDirectory, "objective.json"),
        new
        {
            rawGoal = new { goal.X, goal.Y },
            startNode,
            exactGoalNode,
            fallbackGoalNode,
            fallbackUsed = fallbackGoalNode != exactGoalNode,
            exactPathWaypoints,
            pathWaypoints,
            pathCost,
            reachableFromStart,
            reachableToGoal,
            graphNodeCount = asset.Nodes.Count,
            components = new
            {
                count = components.Summaries.Count,
                startComponent,
                exactGoalComponent,
                fallbackGoalComponent,
                top = components.Summaries
                    .OrderByDescending(static component => component.NodeCount)
                    .Take(5)
                    .ToArray(),
            },
        },
        jsonOptions);

    WriteJsonLines(Path.Combine(artifactDirectory, "edges.jsonl"), edgeDiagnostics.Edges, jsonOptions);
    WriteJsonLines(Path.Combine(artifactDirectory, "blockers.jsonl"), edgeDiagnostics.Blockers, jsonOptions);
}

void WriteJson(string path, object value, JsonSerializerOptions jsonOptions)
{
    File.WriteAllText(path, JsonSerializer.Serialize(value, jsonOptions));
}

void WriteJsonLines<T>(string path, IEnumerable<T> values, JsonSerializerOptions jsonOptions)
{
    var lineOptions = new JsonSerializerOptions(jsonOptions)
    {
        WriteIndented = false,
    };

    using var writer = new StreamWriter(path);
    foreach (var value in values)
    {
        writer.WriteLine(JsonSerializer.Serialize(value, lineOptions));
    }
}

string ClassifyFailureBucket(
    string result,
    string assetSource,
    double assetLoadMilliseconds,
    int validationIssueCount,
    bool exactPathMissing,
    bool fallbackGoalUsed,
    int reachableFromStart,
    int reachableToGoal,
    int graphNodeCount,
    EdgeExecutionDiagnostics edgeDiagnostics,
    IReadOnlyList<string> semanticRecoveryTraces,
    int fireTicks,
    int stagnantWindows,
    int totalTicks,
    int reportEveryTicks)
{
    if (assetSource == "cold-built" && assetLoadMilliseconds > ColdBuildBudgetMilliseconds)
    {
        return "ColdBuildTooSlow";
    }

    if (result == "Scored")
    {
        return validationIssueCount > 0 ? "ScoredWithAssetValidationIssue" : "Scored";
    }

    if (graphNodeCount == 0 || reachableFromStart <= 0 || reachableToGoal <= 0)
    {
        return "NoGraphPath";
    }

    if (exactPathMissing || fallbackGoalUsed)
    {
        return "ObjectiveFallback";
    }

    var blocker = edgeDiagnostics.Blockers.LastOrDefault();
    if (blocker is not null)
    {
        if (blocker.RecipeReadyTicks == 0 && blocker.LastRecipeReason is not "none" and not "ready")
        {
            return "RecipeNeverReady";
        }

        if (blocker.RecipeReadyTicks > 0 && blocker.Phase == "CommitRecipe")
        {
            return "RecipeReadyNoLaunch";
        }

        if (semanticRecoveryTraces.Count > 0)
        {
            return "SemanticRecoveryStillStalled";
        }

        return "LoopingOrStalledEdge";
    }

    var reportWindows = Math.Max(1, totalTicks / Math.Max(1, reportEveryTicks));
    if (fireTicks > totalTicks / 4 && stagnantWindows >= Math.Max(2, reportWindows / 3))
    {
        return "CombatStall";
    }

    if (semanticRecoveryTraces.Count > 0)
    {
        return "SemanticRecoveryUsed";
    }

    return result;
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

List<BotNavigationValidationIssueAssetEntry> FilterControlMarkerValidationIssues(BotNavigationAsset sourceAsset)
{
    if (!sourceAsset.Anchors.Any(static anchor => anchor.Kind == "CaptureZone"))
    {
        return sourceAsset.ValidationIssues;
    }

    var controlPointAnchors = sourceAsset.Anchors
        .Where(static anchor => anchor.Kind == "ControlPoint")
        .ToArray();
    if (controlPointAnchors.Length == 0)
    {
        return sourceAsset.ValidationIssues;
    }

    return sourceAsset.ValidationIssues
        .Where(issue => !controlPointAnchors.Any(anchor =>
            MathF.Abs(anchor.X - issue.X) <= 1f
            && MathF.Abs(anchor.Y - issue.Y) <= 1f))
        .ToList();
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

internal sealed record EdgeExecutionArtifact(
    int FromNode,
    int ToNode,
    string Kind,
    int StartTick,
    int EndTick,
    int Ticks,
    string Phase,
    float StartX,
    float StartY,
    float BestNodeDistance,
    float Movement,
    int Jumps,
    int RecipeReadyTicks,
    string FirstRecipeReason,
    string LastRecipeReason);

internal sealed record BlockerArtifact(
    int Tick,
    int FromNode,
    int ToNode,
    string Kind,
    string Phase,
    int EdgeTicks,
    float WindowMove,
    float BestNodeDistance,
    float Movement,
    int Jumps,
    int RecipeReadyTicks,
    string FirstRecipeReason,
    string LastRecipeReason,
    float X,
    float Y,
    bool PreGrounded,
    float PreHorizontalSpeed,
    float PreVerticalSpeed,
    int PathIndex,
    int PathCount,
    int PathNode);

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
    private bool _currentFinalized;

    public List<EdgeExecutionArtifact> Edges { get; } = [];

    public List<BlockerArtifact> Blockers { get; } = [];

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
                Blockers.Add(new BlockerArtifact(
                    Tick: tick,
                    FromNode: _currentFromNode,
                    ToNode: _currentToNode,
                    Kind: _currentEdge.Kind.ToString(),
                    Phase: _lastPhase,
                    EdgeTicks: edgeTicks,
                    WindowMove: windowMove,
                    BestNodeDistance: _bestTargetDistance,
                    Movement: _edgeMovement,
                    Jumps: _edgeJumps,
                    RecipeReadyTicks: _edgeRecipeReadyTicks,
                    FirstRecipeReason: _firstRecipeReason,
                    LastRecipeReason: _lastRecipeReason,
                    X: bot.X,
                    Y: bot.Y,
                    PreGrounded: preGrounded,
                    PreHorizontalSpeed: preHorizontalSpeed,
                    PreVerticalSpeed: preVerticalSpeed,
                    PathIndex: pathIndex,
                    PathCount: pathCount,
                    PathNode: pathNode));
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
        _currentFinalized = false;
    }

    private void FinalizeCurrentEdge(int tick)
    {
        if (_currentFinalized || string.IsNullOrEmpty(_currentKey) || _currentKey == "none")
        {
            return;
        }

        var edgeTicks = Math.Max(0, tick - _edgeStartTick + 1);
        Edges.Add(new EdgeExecutionArtifact(
            FromNode: _currentFromNode,
            ToNode: _currentToNode,
            Kind: _currentEdge.Kind.ToString(),
            StartTick: _edgeStartTick,
            EndTick: tick,
            Ticks: edgeTicks,
            Phase: _lastPhase,
            StartX: _edgeStartX,
            StartY: _edgeStartY,
            BestNodeDistance: _bestTargetDistance,
            Movement: _edgeMovement,
            Jumps: _edgeJumps,
            RecipeReadyTicks: _edgeRecipeReadyTicks,
            FirstRecipeReason: _firstRecipeReason,
            LastRecipeReason: _lastRecipeReason));
        if (edgeTicks > _longestEdgeTicks)
        {
            _longestEdgeTicks = edgeTicks;
            _longestEdgeSummary =
                $"edge={_currentFromNode}->{_currentToNode} kind={_currentEdge.Kind} ticks={edgeTicks} phase={_lastPhase} bestNodeDist={_bestTargetDistance:0.0} movement={_edgeMovement:0.0} jumps={_edgeJumps} recipeReadyTicks={_edgeRecipeReadyTicks} firstRecipe={_firstRecipeReason} lastRecipe={_lastRecipeReason}";
        }

        _currentFinalized = true;
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

internal static class BotBrainToolCommandHelpers
{
    private const float MaximumCorridorSnapDistance = 192f;
    private const float MaximumCorridorSegmentDistance = 640f;
    private const float CorridorPreferredCostMultiplier = 0.2f;
    private const float CorridorObjectiveArrivalDistance = 96f;

    private static readonly JsonSerializerOptions CorridorRecordingJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Dictionary<string, string> ParseRawOptions(string[] args)
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

        return options;
    }

    public static void AuthorTraversalTape(string scenarioPath, string? tapeName)
    {
        if (string.IsNullOrWhiteSpace(scenarioPath))
        {
            throw new InvalidOperationException("--author-traversal-tape requires a TraversalLab scenario JSON path.");
        }

        var fullScenarioPath = Path.GetFullPath(scenarioPath);
        var compileRepoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(compileRepoRoot, "Core", "Content"));

        var scenario = JsonSerializer.Deserialize<TraversalLabScenario>(
            File.ReadAllText(fullScenarioPath),
            CorridorRecordingJsonOptions) ?? throw new InvalidOperationException($"Could not read TraversalLab scenario '{fullScenarioPath}'.");
        if (string.IsNullOrWhiteSpace(scenario.LevelName))
        {
            throw new InvalidOperationException("TraversalLab objective tape authoring requires a world LevelName scenario.");
        }

        var result = TraversalLabRunner.Run(scenario);
        var bestCase = result.Cases
            .Where(static candidate => candidate.Passed)
            .OrderBy(static candidate => candidate.ExecutedTicks)
            .ThenBy(static candidate => candidate.Samples.Count)
            .FirstOrDefault();
        Console.WriteLine($"traversalScenario={scenario.Name} cases={result.Cases.Count} passed={result.PassedCount} failed={result.FailedCount}");
        foreach (var candidate in result.Cases.Take(12))
        {
            Console.WriteLine($"case passed={(candidate.Passed ? 1 : 0)} ticks={candidate.ExecutedTicks} final=({candidate.FinalX:0.0},{candidate.FinalY:0.0}) bottom={candidate.FinalBottom:0.0} reason={candidate.FailureReason}");
        }

        if (bestCase is null)
        {
            throw new InvalidOperationException("TraversalLab scenario did not pass; refusing to install objective tape.");
        }

        var level = SimpleLevelFactory.CreateImportedLevel(scenario.LevelName, scenario.MapAreaIndex)
            ?? throw new InvalidOperationException($"Could not load map '{scenario.LevelName}' area {scenario.MapAreaIndex}.");
        var entry = BuildObjectiveTapeFromTraversalCase(
            string.IsNullOrWhiteSpace(tapeName) ? scenario.Name : tapeName!,
            scenario,
            bestCase);
        var tapePath = BotBrainObjectiveTapeStore.UpsertTape(level, entry);
        Console.WriteLine($"objectiveTape={tapePath}");
        Console.WriteLine($"selectedCase ticks={bestCase.ExecutedTicks} rawSamples={bestCase.Samples.Count} tapeSamples={entry.Segments.Sum(static segment => segment.Samples.Count)}");
    }

    public static void ProbeAndAuthorReturnTape(Dictionary<string, string> rawOptions)
    {
        var mapName = rawOptions.TryGetValue("map", out var mapText) ? mapText : "Truefort";
        var area = ReadIntOption(rawOptions, "area", 1);
        var team = ReadEnumOption(rawOptions, "team", PlayerTeam.Red);
        var classId = ReadEnumOption(rawOptions, "class", PlayerClass.Soldier);
        var startX = ReadFloatOption(rawOptions, "start-x", 4960f);
        var startBottom = ReadFloatOption(rawOptions, "start-bottom", 888f);
        var maxTicks = ReadIntOption(rawOptions, "ticks", 3600);
        var direction = ReadFloatOption(rawOptions, "direction", team == PlayerTeam.Red ? -1f : 1f) < 0f ? -1f : 1f;
        var tapeName = rawOptions.TryGetValue("tape-name", out var providedTapeName)
            ? providedTapeName
            : $"{mapName}.a{area}.{team}.{classId}.return.probed";

        var compileRepoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(compileRepoRoot, "Core", "Content"));
        var level = SimpleLevelFactory.CreateImportedLevel(mapName, area)
            ?? throw new InvalidOperationException($"Could not load map '{mapName}' area {area}.");

        TraversalLabScenario? bestScenario = null;
        TraversalLabCaseResult? bestCase = null;
        TraversalLabCaseResult? bestFailedCase = null;
        TraversalLabScenario? bestFailedScenario = null;
        var bestFailedDistance = float.PositiveInfinity;
        var attempts = 0;
        var phases = rawOptions.TryGetValue("phases", out var phaseText)
            ? ParseIntList(phaseText)
            : [0, 4, 8, 12, 16, 20, 24, 30];
        var intervals = rawOptions.TryGetValue("intervals", out var intervalText)
            ? ParseIntList(intervalText)
            : [0, 24, 30, 36, 42, 48, 54, 60, 72, 84];
        var holds = rawOptions.TryGetValue("holds", out var holdText)
            ? ParseIntList(holdText)
            : [1, 2, 3, 4, 6];
        var preTicksList = rawOptions.TryGetValue("pre-ticks", out var preTicksText)
            ? ParseIntList(preTicksText)
            : [0];
        var preIntervals = rawOptions.TryGetValue("pre-intervals", out var preIntervalText)
            ? ParseIntList(preIntervalText)
            : [0, 24, 36];
        var bottomOffsets = rawOptions.TryGetValue("bottom-offsets", out var bottomOffsetText)
            ? ParseFloatList(bottomOffsetText)
            : [0f, -12f, 12f, -24f, 24f];
        foreach (var bottomOffset in bottomOffsets)
        {
            foreach (var interval in intervals)
            {
                foreach (var hold in holds)
                {
                    foreach (var phase in phases)
                    {
                        foreach (var preTicks in preTicksList)
                        {
                            foreach (var preInterval in preIntervals)
                            {
                                if (interval == 0 && (hold != 1 || phase != 0))
                                {
                                    continue;
                                }

                                if (preTicks == 0 && preInterval != preIntervals[0])
                                {
                                    continue;
                                }

                                attempts += 1;
                                var scenario = new TraversalLabScenario
                                {
                                    Name = $"{tapeName}.pre{preTicks}.pi{preInterval}.i{interval}.h{hold}.p{phase}.b{bottomOffset:0}",
                                    LevelName = mapName,
                                    MapAreaIndex = area,
                                    Team = team,
                                    ClassId = classId,
                                    Start = new TraversalLabStartState
                                    {
                                        X = startX,
                                        Bottom = startBottom + bottomOffset,
                                        IsGrounded = true,
                                        IsCarryingIntel = true,
                                        FacingDirectionX = direction,
                                    },
                                    Steps = BuildTwoPhaseDriveProbeSteps(maxTicks, direction, preTicks, preInterval, interval, hold, phase),
                                    MaxTicks = maxTicks,
                                    TraceEveryTicks = 1,
                                    Expectation = new TraversalLabExpectation
                                    {
                                        MustEverOverlapOwnIntelMarker = true,
                                    },
                                };
                                var result = TraversalLabRunner.Run(scenario);
                                var passedCase = result.Cases.Count > 0 ? result.Cases[0] : null;
                                if (passedCase is null || !passedCase.Passed)
                                {
                                    var failedCase = passedCase;
                                    if (failedCase is not null)
                                    {
                                        var distance = DistanceToOwnIntel(level, team, failedCase);
                                        if (distance < bestFailedDistance)
                                        {
                                            bestFailedDistance = distance;
                                            bestFailedCase = failedCase;
                                            bestFailedScenario = scenario;
                                        }
                                    }

                                    continue;
                                }

                                if (bestCase is null || passedCase.ExecutedTicks < bestCase.ExecutedTicks)
                                {
                                    bestScenario = scenario;
                                    bestCase = passedCase;
                                }
                            }
                        }
                    }
                }
            }
        }

        Console.WriteLine($"returnProbe map={mapName} area={area} team={team} class={classId} attempts={attempts} passed={(bestCase is null ? 0 : 1)}");
        if (bestScenario is null || bestCase is null)
        {
            if (bestFailedCase is not null && bestFailedScenario is not null)
            {
                Console.WriteLine($"closestFailed={bestFailedScenario.Name} dist={bestFailedDistance:0.0} final=({bestFailedCase.FinalX:0.0},{bestFailedCase.FinalY:0.0}) bottom={bestFailedCase.FinalBottom:0.0} rangeX=({bestFailedCase.MinX:0.0},{bestFailedCase.MaxX:0.0}) rangeBottom=({bestFailedCase.MinBottom:0.0},{bestFailedCase.MaxBottom:0.0}) reason={bestFailedCase.FailureReason}");
            }

            throw new InvalidOperationException("Return tape probe failed to reach own intel marker.");
        }

        var entry = BuildObjectiveTapeFromTraversalSamples(
            tapeName,
            bestScenario,
            TrimTraversalSamplesAtOwnIntel(level, team, bestCase.Samples));
        var tapePath = BotBrainObjectiveTapeStore.UpsertTape(level, entry);
        Console.WriteLine($"objectiveTape={tapePath}");
        Console.WriteLine($"selected={bestScenario.Name} ticks={bestCase.ExecutedTicks} rawSamples={bestCase.Samples.Count} tapeSamples={entry.Segments.Sum(static segment => segment.Samples.Count)}");
    }

    private static List<TraversalLabInputStep> BuildTwoPhaseDriveProbeSteps(
        int maxTicks,
        float direction,
        int preTicks,
        int preJumpInterval,
        int jumpInterval,
        int jumpHold,
        int jumpPhase)
    {
        var steps = new List<TraversalLabInputStep>(maxTicks);
        for (var tick = 0; tick < maxTicks; tick += 1)
        {
            var inPrePhase = tick < preTicks;
            var activeDirection = inPrePhase ? -direction : direction;
            var phaseTick = inPrePhase ? tick : tick - preTicks;
            var activeInterval = inPrePhase ? preJumpInterval : jumpInterval;
            var activePhase = inPrePhase ? 0 : jumpPhase;
            var jump = activeInterval > 0 && phaseTick >= activePhase && ((phaseTick - activePhase) % activeInterval) < jumpHold;
            steps.Add(new TraversalLabInputStep
                            {
                Label = inPrePhase ? jump ? "pre_jump" : "pre_drive" : jump ? "drive_jump" : "drive",
                DurationTicks = 1,
                Left = activeDirection < 0f,
                Right = activeDirection > 0f,
                Up = jump,
                AimFacingDirectionX = activeDirection,
            });
        }

        return steps;
    }

    private static float DistanceToOwnIntel(SimpleLevel level, PlayerTeam team, TraversalLabCaseResult result)
    {
        var marker = level.GetIntelBase(team);
        if (!marker.HasValue)
        {
            return float.PositiveInfinity;
        }

        var bestDistance = float.PositiveInfinity;
        foreach (var sample in result.Samples)
        {
            var dx = sample.X - marker.Value.X;
            var dy = sample.Y - marker.Value.Y;
            bestDistance = MathF.Min(bestDistance, MathF.Sqrt((dx * dx) + (dy * dy)));
        }

        return bestDistance;
    }

    private static IReadOnlyList<TraversalLabTickSample> TrimTraversalSamplesAtOwnIntel(
        SimpleLevel level,
        PlayerTeam team,
        IReadOnlyList<TraversalLabTickSample> samples)
    {
        if (!level.GetIntelBase(team).HasValue)
        {
            return samples;
        }

        var marker = level.GetIntelBase(team)!.Value;
        var endIndex = samples.Count - 1;
        for (var i = 0; i < samples.Count; i += 1)
        {
            var sample = samples[i];
            if (MathF.Abs(sample.X - marker.X) <= 96f && MathF.Abs(sample.Y - marker.Y) <= 96f)
            {
                endIndex = i;
                break;
            }
        }

        return samples.Take(endIndex + 1).ToList();
    }

    private static int ReadIntOption(Dictionary<string, string> options, string key, int fallback) =>
        options.TryGetValue(key, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static float ReadFloatOption(Dictionary<string, string> options, string key, float fallback) =>
        options.TryGetValue(key, out var text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static TEnum ReadEnumOption<TEnum>(Dictionary<string, string> options, string key, TEnum fallback)
        where TEnum : struct
    {
        return options.TryGetValue(key, out var text) && Enum.TryParse<TEnum>(text, ignoreCase: true, out var value)
            ? value
            : fallback;
    }

    private static List<float> ParseFloatList(string text)
    {
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f)
            .ToList();
    }

    private static List<int> ParseIntList(string text)
    {
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0)
            .ToList();
    }

    private static BotBrainObjectiveTapeEntry BuildObjectiveTapeFromTraversalCase(
        string tapeName,
        TraversalLabScenario scenario,
        TraversalLabCaseResult bestCase)
    {
        return BuildObjectiveTapeFromTraversalSamples(tapeName, scenario, bestCase.Samples);
    }

    private static BotBrainObjectiveTapeEntry BuildObjectiveTapeFromTraversalSamples(
        string tapeName,
        TraversalLabScenario scenario,
        IReadOnlyList<TraversalLabTickSample> sourceSamples)
    {
        var samples = NormalizeTraversalTapeSamples(sourceSamples);
        if (samples.Count < 2)
        {
            throw new InvalidOperationException("TraversalLab case produced too few samples for an objective tape.");
        }

        return new BotBrainObjectiveTapeEntry
        {
            Name = tapeName,
            Team = scenario.Team,
            PlayerClass = scenario.ClassId,
            Segments =
            [
                new BotBrainObjectiveTapeSegment
                {
                    RequiresCarryingIntel = samples[0].IsCarryingIntel,
                    Samples = samples,
                },
            ],
        };
    }

    private static List<BotBrainObjectiveTapeSample> NormalizeTraversalTapeSamples(IReadOnlyList<TraversalLabTickSample> sourceSamples)
    {
        var converted = new List<BotBrainObjectiveTapeSample>(sourceSamples.Count);
        foreach (var sample in sourceSamples)
        {
            var moveDirection = sample.InputLeft == sample.InputRight
                ? 0f
                : sample.InputRight ? 1f : -1f;
            var candidate = new BotBrainObjectiveTapeSample
            {
                Tick = sample.Tick,
                X = sample.X,
                Y = sample.Y,
                Bottom = sample.Bottom,
                HorizontalSpeed = sample.HorizontalSpeed,
                VerticalSpeed = sample.VerticalSpeed,
                IsGrounded = sample.IsGrounded,
                MoveDirection = moveDirection,
                Jump = sample.InputUp,
                DropDown = sample.InputDown,
                IsCarryingIntel = sample.IsCarryingIntel,
            };

            if (converted.Count == 0 || ShouldKeepTraversalTapeSample(converted[^1], candidate))
            {
                converted.Add(candidate);
            }
            else
            {
                converted[^1] = candidate;
            }
        }

        return NormalizeTapeSegmentTicks(converted);
    }

    private static bool ShouldKeepTraversalTapeSample(BotBrainObjectiveTapeSample previous, BotBrainObjectiveTapeSample candidate)
    {
        if (previous.MoveDirection != candidate.MoveDirection
            || previous.Jump != candidate.Jump
            || previous.DropDown != candidate.DropDown
            || previous.IsGrounded != candidate.IsGrounded
            || previous.IsCarryingIntel != candidate.IsCarryingIntel)
        {
            return true;
        }

        var dx = candidate.X - previous.X;
        var dy = candidate.Y - previous.Y;
        return ((dx * dx) + (dy * dy)) >= 24f * 24f;
    }

    public static void CompileBotBrainCorridorRecording(
        string recordingPath,
        JsonSerializerOptions outputJsonOptions,
        bool installAuthoredCorridor,
        bool rebuildAsset,
        bool bakeCorridorAsset,
        bool installTape,
        float recordingMapScaleOverride)
    {
        if (string.IsNullOrWhiteSpace(recordingPath))
        {
            throw new InvalidOperationException("--compile-corridor requires a recording path.");
        }

        var fullRecordingPath = Path.GetFullPath(recordingPath);
        var recording = JsonSerializer.Deserialize<BotBrainCorridorRecording>(
            File.ReadAllText(fullRecordingPath),
            CorridorRecordingJsonOptions) ?? throw new InvalidOperationException($"Could not read BotBrain corridor recording '{fullRecordingPath}'.");

        var compileRepoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(compileRepoRoot, "Core", "Content"));
        var compileLevel = SimpleLevelFactory.CreateImportedLevel(recording.LevelName, recording.MapAreaIndex)
            ?? throw new InvalidOperationException($"Could not load map '{recording.LevelName}' area {recording.MapAreaIndex}.");

        var compileAsset = rebuildAsset
            ? BotNavigationAssetStore.BuildAndSaveRuntimeCache(compileLevel)
            : BotNavigationAssetStore.TryLoadRuntimeCache(compileLevel, out var cachedAsset)
                ? cachedAsset
                : BotNavigationAssetStore.TryLoadShipped(compileLevel, out var shippedAsset)
                    ? shippedAsset
                    : BotNavigationAssetStore.BuildAndSaveRuntimeCache(compileLevel);
        var compileGraph = BotNavigationAssetBuilder.ToGraph(compileAsset);

        var recordingMapScale = ResolveCorridorRecordingMapScale(recording, recordingMapScaleOverride);
        var normalizedSamples = NormalizeCorridorSamples(recording.Samples, recordingMapScale, compileLevel.MapScale);
        var selectedSamples = SelectCorridorCompileSamples(normalizedSamples);
        var segments = SplitCorridorCompileSegments(selectedSamples);
        var waypoints = new List<BotBrainCompiledCorridorWaypoint>();
        var waypointSegments = new List<int>();
        var rejectedNoNode = 0;
        var rejectedSnapDistance = 0;
        var worstSnapMisses = new List<BotBrainCorridorSnapMiss>();
        for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex += 1)
        {
            foreach (var sample in segments[segmentIndex])
            {
                var nodeIndex = compileGraph.FindNearestTraversalStartNode(sample.X, sample.Y, maxAboveDistance: 96f);
                if (nodeIndex < 0)
                {
                    rejectedNoNode += 1;
                    continue;
                }

                var node = compileGraph.GetNode(nodeIndex);
                var snapDx = node.X - sample.X;
                var snapDy = node.Y - sample.Y;
                var snapDistance = MathF.Sqrt((snapDx * snapDx) + (snapDy * snapDy));
                if (snapDistance > MaximumCorridorSnapDistance)
                {
                    rejectedSnapDistance += 1;
                    TrackCorridorSnapMiss(worstSnapMisses, new BotBrainCorridorSnapMiss(
                        sample.Tick,
                        sample.X,
                        sample.Y,
                        nodeIndex,
                        node.X,
                        node.Y,
                        snapDistance));
                    continue;
                }

                if (waypoints.Count > 0
                    && waypointSegments[^1] == segmentIndex
                    && waypoints[^1].NodeIndex == nodeIndex)
                {
                    continue;
                }

                waypoints.Add(new BotBrainCompiledCorridorWaypoint(
                    SampleTick: sample.Tick,
                    Reason: sample.Reason,
                    NodeIndex: nodeIndex,
                    NodeX: node.X,
                    NodeY: node.Y,
                    SurfaceId: node.SurfaceId,
                    SampleX: sample.X,
                    SampleY: sample.Y,
                    SampleGrounded: sample.IsGrounded,
                    SampleCarryingIntel: sample.IsCarryingIntel));
                waypointSegments.Add(segmentIndex);
            }
        }

        var gaps = new List<BotBrainCompiledCorridorGap>();
        for (var i = 0; i + 1 < waypoints.Count; i += 1)
        {
            if (waypointSegments[i] != waypointSegments[i + 1])
            {
                continue;
            }

            var from = waypoints[i];
            var to = waypoints[i + 1];
            if (compileGraph.FindPath(from.NodeIndex, to.NodeIndex, recording.PlayerClass, team: recording.Team) is not null)
            {
                continue;
            }

            var fromNode = compileGraph.GetNode(from.NodeIndex);
            var toNode = compileGraph.GetNode(to.NodeIndex);
            gaps.Add(new BotBrainCompiledCorridorGap(
                FromWaypointIndex: i,
                ToWaypointIndex: i + 1,
                FromNode: from.NodeIndex,
                ToNode: to.NodeIndex,
                FromX: fromNode.X,
                FromY: fromNode.Y,
                ToX: toNode.X,
                ToY: toNode.Y,
                Dx: toNode.X - fromNode.X,
                Dy: toNode.Y - fromNode.Y,
                SuggestedProbeCommand:
                    $"dotnet run --project BotBrain.Tools\\OpenGarrison.BotBrain.Tools.csproj --no-build -- --map {recording.LevelName} --area {recording.MapAreaIndex} --team {recording.Team} --class {recording.PlayerClass} --ticks 180 --probe-from {from.NodeIndex} --probe-to {to.NodeIndex}"));
        }

        var compiled = new BotBrainCompiledCorridor(
            FormatVersion: 1,
            SourceRecordingPath: fullRecordingPath,
            LevelName: recording.LevelName,
            MapAreaIndex: recording.MapAreaIndex,
            Team: recording.Team,
            PlayerClass: recording.PlayerClass,
            Waypoints: waypoints.ToArray(),
            Gaps: gaps.ToArray(),
            Marks: recording.Marks);

        var outputPath = Path.ChangeExtension(fullRecordingPath, ".compiled.botbrain-corridor.json");
        File.WriteAllText(outputPath, JsonSerializer.Serialize(compiled, outputJsonOptions));
        Console.WriteLine($"compiledCorridor={outputPath}");
        Console.WriteLine($"mapScale=recording:{recordingMapScale:0.###} compile:{compileLevel.MapScale:0.###}");
        Console.WriteLine($"samples=raw:{recording.Samples.Length} selected:{selectedSamples.Count} segments:{segments.Count} discontinuities:{Math.Max(0, segments.Count - 1)}");
        Console.WriteLine($"waypoints={waypoints.Count} gaps={gaps.Count} rejectedNoNode={rejectedNoNode} rejectedSnapDistance={rejectedSnapDistance}");
        if (installAuthoredCorridor)
        {
            var corridorName = Path.GetFileNameWithoutExtension(fullRecordingPath)
                .Replace(".botbrain-corridor", string.Empty, StringComparison.OrdinalIgnoreCase);
            string? authoredPath = null;
            var installedSegments = 0;
            for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex += 1)
            {
                var segment = segments[segmentIndex];
                if (segment.Count < 2)
                {
                    continue;
                }

                authoredPath = BotNavigationAuthoredCorridorStore.UpsertCorridor(
                    compileLevel,
                    new BotNavigationAuthoredCorridorEntry
                    {
                        Name = segments.Count == 1 ? corridorName : $"{corridorName}.s{segmentIndex + 1:00}",
                        Team = recording.Team,
                        PlayerClass = recording.PlayerClass,
                        Waypoints = segment
                            .Select(static sample => new BotNavigationAuthoredCorridorWaypoint
                            {
                                X = sample.X,
                                Y = sample.Y,
                                IsGrounded = sample.IsGrounded,
                                Reason = sample.Reason,
                            })
                            .ToList(),
                    });
                installedSegments += 1;
            }

            Console.WriteLine($"authoredCorridor={authoredPath ?? "(none)"} installedSegments={installedSegments}");
        }

        if (bakeCorridorAsset)
        {
            var bakeStats = BakeCorridorIntoAsset(compileLevel, compileAsset, segments, recording.Team, recording.PlayerClass);
            BotNavigationAssetStore.SaveRuntimeCache(compileAsset);
            Console.WriteLine($"bakedCorridorAsset=nodesAdded:{bakeStats.NodesAdded} edgesAdded:{bakeStats.EdgesAdded} surfacesAdded:{bakeStats.SurfacesAdded}");
        }

        if (installTape)
        {
            var tapeName = Path.GetFileNameWithoutExtension(fullRecordingPath)
                .Replace(".botbrain-corridor", string.Empty, StringComparison.OrdinalIgnoreCase);
            var tapePath = BotBrainObjectiveTapeStore.UpsertTape(
                compileLevel,
                BuildObjectiveTapeFromRecording(tapeName, recording, selectedSamples));
            Console.WriteLine($"objectiveTape={tapePath}");
        }

        foreach (var gap in gaps.Take(12))
        {
            Console.WriteLine($"gap={gap.FromNode}->{gap.ToNode} dx={gap.Dx:0.0} dy={gap.Dy:0.0}");
        }

        foreach (var miss in worstSnapMisses.OrderByDescending(static miss => miss.Distance).Take(8))
        {
            Console.WriteLine($"snapMiss tick={miss.Tick} sample=({miss.SampleX:0.0},{miss.SampleY:0.0}) node={miss.NodeIndex} nodePos=({miss.NodeX:0.0},{miss.NodeY:0.0}) dist={miss.Distance:0.0}");
        }
    }

    private static List<BotBrainCorridorRecordingSample> SelectCorridorCompileSamples(BotBrainCorridorRecordingSample[] samples)
    {
        var selected = new List<BotBrainCorridorRecordingSample>();
        for (var i = 0; i < samples.Length; i += 1)
        {
            var sample = samples[i];
            if (i == 0
                || i == samples.Length - 1
                || sample.Reason is not "Stride")
            {
                selected.Add(sample);
                continue;
            }

            if (selected.Count == 0 || sample.Tick - selected[^1].Tick >= 24)
            {
                selected.Add(sample);
            }
        }

        return selected;
    }

    private static BotBrainObjectiveTapeEntry BuildObjectiveTapeFromRecording(
        string tapeName,
        BotBrainCorridorRecording recording,
        IReadOnlyList<BotBrainCorridorRecordingSample> selectedSamples)
    {
        var segments = new List<BotBrainObjectiveTapeSegment>();
        var current = new List<BotBrainObjectiveTapeSample>();
        bool? currentCarrying = null;
        foreach (var sample in selectedSamples)
        {
            if (sample.Reason is "Cancel" or "Death")
            {
                continue;
            }

            if (current.Count > 0 && currentCarrying.HasValue && sample.IsCarryingIntel != currentCarrying.Value)
            {
                segments.Add(new BotBrainObjectiveTapeSegment
                {
                    RequiresCarryingIntel = currentCarrying.Value,
                    Samples = NormalizeTapeSegmentTicks(current),
                });
                current = [];
            }

            currentCarrying = sample.IsCarryingIntel;
            current.Add(ToObjectiveTapeSample(sample));
        }

        if (current.Count > 1 && currentCarrying.HasValue)
        {
            segments.Add(new BotBrainObjectiveTapeSegment
            {
                RequiresCarryingIntel = currentCarrying.Value,
                Samples = NormalizeTapeSegmentTicks(current),
            });
        }

        return new BotBrainObjectiveTapeEntry
        {
            Name = tapeName,
            Team = recording.Team,
            PlayerClass = recording.PlayerClass,
            Segments = segments,
        };
    }

    private static List<BotBrainObjectiveTapeSample> NormalizeTapeSegmentTicks(List<BotBrainObjectiveTapeSample> samples)
    {
        if (samples.Count == 0)
        {
            return samples;
        }

        var firstTick = samples[0].Tick;
        return samples
            .Select(sample => new BotBrainObjectiveTapeSample
            {
                Tick = Math.Max(0, sample.Tick - firstTick),
                X = sample.X,
                Y = sample.Y,
                Bottom = sample.Bottom,
                HorizontalSpeed = sample.HorizontalSpeed,
                VerticalSpeed = sample.VerticalSpeed,
                IsGrounded = sample.IsGrounded,
                MoveDirection = sample.MoveDirection,
                Jump = sample.Jump,
                DropDown = sample.DropDown,
                IsCarryingIntel = sample.IsCarryingIntel,
            })
            .ToList();
    }

    private static BotBrainObjectiveTapeSample ToObjectiveTapeSample(BotBrainCorridorRecordingSample sample) =>
        new()
        {
            Tick = sample.Tick,
            X = sample.X,
            Y = sample.Y,
            Bottom = sample.Bottom,
            HorizontalSpeed = sample.HorizontalSpeed,
            VerticalSpeed = sample.VerticalSpeed,
            IsGrounded = sample.IsGrounded,
            MoveDirection = sample.MoveDirection,
            Jump = sample.Jump,
            DropDown = sample.DropDown,
            IsCarryingIntel = sample.IsCarryingIntel,
        };

    private static float ResolveCorridorRecordingMapScale(
        BotBrainCorridorRecording recording,
        float recordingMapScaleOverride)
    {
        if (recordingMapScaleOverride > 0f)
        {
            return recordingMapScaleOverride;
        }

        return recording.MapScale > 0f ? recording.MapScale : 1f;
    }

    private static BotBrainCorridorRecordingSample[] NormalizeCorridorSamples(
        BotBrainCorridorRecordingSample[] samples,
        float sourceMapScale,
        float targetMapScale)
    {
        if (sourceMapScale <= 0f || MathF.Abs(sourceMapScale - targetMapScale) <= 0.0001f)
        {
            return samples;
        }

        var scale = targetMapScale / sourceMapScale;
        var normalized = new BotBrainCorridorRecordingSample[samples.Length];
        for (var i = 0; i < samples.Length; i += 1)
        {
            var sample = samples[i];
            normalized[i] = sample with
            {
                X = sample.X * scale,
                Y = sample.Y * scale,
                Bottom = sample.Bottom * scale,
                HorizontalSpeed = sample.HorizontalSpeed * scale,
                VerticalSpeed = sample.VerticalSpeed * scale,
            };
        }

        return normalized;
    }

    private static List<List<BotBrainCorridorRecordingSample>> SplitCorridorCompileSegments(
        List<BotBrainCorridorRecordingSample> selectedSamples)
    {
        var segments = new List<List<BotBrainCorridorRecordingSample>>();
        var current = new List<BotBrainCorridorRecordingSample>();
        foreach (var sample in selectedSamples)
        {
            if (current.Count > 0)
            {
                var previous = current[^1];
                var dx = sample.X - previous.X;
                var dy = sample.Y - previous.Y;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                if (distance > MaximumCorridorSegmentDistance)
                {
                    segments.Add(current);
                    current = [];
                }
            }

            current.Add(sample);
        }

        if (current.Count > 0)
        {
            segments.Add(current);
        }

        return segments;
    }

    public static bool ShouldRecordProofCorridorSample(
        IReadOnlyList<BotBrainCorridorRecordingSample> samples,
        PlayerEntity bot,
        PlayerInputSnapshot input,
        int tick,
        int carryingIntelTick)
    {
        if (!bot.IsGrounded)
        {
            return false;
        }

        if (samples.Count == 0)
        {
            return true;
        }

        var previous = samples[^1];
        if (bot.IsCarryingIntel != previous.IsCarryingIntel)
        {
            return true;
        }

        if (bot.IsGrounded && input.Up)
        {
            return true;
        }

        if (carryingIntelTick >= 0 && tick - carryingIntelTick <= 4)
        {
            return true;
        }

        return tick - previous.Tick >= 10
            && Distance(previous.X, previous.Y, bot.X, bot.Y) >= 48f;
    }

    public static void AddProofCorridorSample(
        List<BotBrainCorridorRecordingSample> samples,
        SimulationWorld world,
        PlayerEntity bot,
        PlayerInputSnapshot input,
        int tick,
        string reason)
    {
        samples.Add(new BotBrainCorridorRecordingSample(
            Frame: world.Frame,
            Tick: tick,
            Reason: reason,
            X: bot.X,
            Y: bot.Y,
            Bottom: bot.Bottom,
            HorizontalSpeed: bot.HorizontalSpeed,
            VerticalSpeed: bot.VerticalSpeed,
            IsGrounded: bot.IsGrounded,
            RemainingAirJumps: bot.RemainingAirJumps,
            MoveDirection: input.Right == input.Left ? 0f : input.Right ? 1f : -1f,
            Jump: input.Up,
            DropDown: input.Down,
            IsCarryingIntel: bot.IsCarryingIntel,
            RedCaps: world.RedCaps,
            BlueCaps: world.BlueCaps));
    }

    public static BotBrainObjectiveTapeEntry BuildObjectiveTapeFromProofSamples(
        SimpleLevel level,
        string tapeName,
        PlayerTeam team,
        PlayerClass playerClass,
        IReadOnlyList<BotBrainProofTapeSample> sourceSamples)
    {
        var ownBase = level.GetIntelBase(team)
            ?? throw new InvalidOperationException("Proof tape normalization requires an own intel base.");
        var selected = new List<BotBrainProofTapeSample>();
        var bestDistance = float.PositiveInfinity;
        foreach (var sample in sourceSamples)
        {
            var distance = Distance(sample.X, sample.Y, ownBase.X, ownBase.Y);
            var madeProgress = distance <= bestDistance - 36f;
            var isEndpoint = distance <= 96f;
            if (selected.Count == 0 || madeProgress || isEndpoint)
            {
                selected.Add(sample);
                bestDistance = MathF.Min(bestDistance, distance);
            }
        }

        if (selected.Count < 2)
        {
            throw new InvalidOperationException("Proof tape normalization produced too few samples.");
        }

        return new BotBrainObjectiveTapeEntry
        {
            Name = tapeName,
            Team = team,
            PlayerClass = playerClass,
            Segments =
            [
                new BotBrainObjectiveTapeSegment
                {
                    RequiresCarryingIntel = true,
                    Samples = BuildCompressedProofTapeSamples(selected),
                },
            ],
        };
    }

    private static List<BotBrainObjectiveTapeSample> BuildCompressedProofTapeSamples(
        IReadOnlyList<BotBrainProofTapeSample> selected)
    {
        var samples = new List<BotBrainObjectiveTapeSample>(selected.Count);
        var compressedTick = 0;
        for (var i = 0; i < selected.Count; i += 1)
        {
            var sample = selected[i];
            if (i > 0)
            {
                var previous = selected[i - 1];
                var distance = Distance(previous.X, previous.Y, sample.X, sample.Y);
                compressedTick += Math.Clamp((int)MathF.Ceiling(distance / 5f), 1, 18);
            }

            var moveDirection = sample.Left == sample.Right
                ? 0f
                : sample.Right ? 1f : -1f;
            samples.Add(new BotBrainObjectiveTapeSample
            {
                Tick = compressedTick,
                X = sample.X,
                Y = sample.Y,
                Bottom = sample.Bottom,
                HorizontalSpeed = sample.HorizontalSpeed,
                VerticalSpeed = sample.VerticalSpeed,
                IsGrounded = sample.IsGrounded,
                MoveDirection = moveDirection,
                Jump = sample.Up,
                DropDown = sample.Down,
                IsCarryingIntel = true,
            });
        }

        return samples;
    }

    public static List<List<BotBrainCorridorRecordingSample>> BuildAutoProofCorridorSegments(
        SimpleLevel level,
        IReadOnlyList<BotBrainCorridorRecordingSample> samples,
        PlayerTeam team)
    {
        var selected = new List<BotBrainCorridorRecordingSample>();
        var bestPhaseDistance = float.PositiveInfinity;
        var previousCarrying = false;
        foreach (var sample in samples)
        {
            if (!sample.IsGrounded && sample.Reason != "Score")
            {
                continue;
            }

            var phaseChanged = selected.Count == 0
                || sample.IsCarryingIntel != previousCarrying
                || sample.Reason is "Start" or "Score";
            if (phaseChanged)
            {
                selected.Add(sample);
                previousCarrying = sample.IsCarryingIntel;
                bestPhaseDistance = ResolveProofPhaseDistance(level, sample, team);
                continue;
            }

            var phaseDistance = ResolveProofPhaseDistance(level, sample, team);
            var previous = selected[^1];
            if ((phaseDistance <= bestPhaseDistance - 64f && Distance(previous.X, previous.Y, sample.X, sample.Y) >= 64f)
                || sample.Tick - previous.Tick >= 60)
            {
                selected.Add(sample);
                bestPhaseDistance = MathF.Min(bestPhaseDistance, phaseDistance);
            }
        }

        if (samples.Count > 0 && (selected.Count == 0 || selected[^1].Tick != samples[^1].Tick))
        {
            selected.Add(samples[^1]);
        }

        return SplitAutoProofCorridorSegments(selected);
    }

    private static List<List<BotBrainCorridorRecordingSample>> SplitAutoProofCorridorSegments(
        IReadOnlyList<BotBrainCorridorRecordingSample> selectedSamples)
    {
        var segments = new List<List<BotBrainCorridorRecordingSample>>();
        var current = new List<BotBrainCorridorRecordingSample>();
        foreach (var sample in selectedSamples)
        {
            if (current.Count > 0)
            {
                var previous = current[^1];
                var dx = sample.X - previous.X;
                var dy = sample.Y - previous.Y;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                if (distance > MaximumCorridorSegmentDistance
                    || (sample.Reason != "Score" && sample.IsCarryingIntel != previous.IsCarryingIntel))
                {
                    segments.Add(current);
                    current = [];
                }
            }

            current.Add(sample);
        }

        if (current.Count > 0)
        {
            segments.Add(current);
        }

        return segments;
    }

    private static float ResolveProofPhaseDistance(
        SimpleLevel level,
        BotBrainCorridorRecordingSample sample,
        PlayerTeam team)
    {
        if (level.Mode == GameModeKind.CaptureTheFlag)
        {
            var target = sample.IsCarryingIntel
                ? level.GetIntelBase(team)
                : level.GetIntelBase(team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red);
            return target.HasValue
                ? Distance(sample.X, sample.Y, target.Value.X, target.Value.Y)
                : 0f;
        }

        if (level.Mode is GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && TryFindNearestControlObjective(level, sample.X, sample.Y, out var point))
        {
            return Distance(sample.X, sample.Y, point.CenterX, point.CenterY);
        }

        return 0f;
    }

    private static void TrackCorridorSnapMiss(
        List<BotBrainCorridorSnapMiss> misses,
        BotBrainCorridorSnapMiss miss)
    {
        misses.Add(miss);
        if (misses.Count <= 16)
        {
            return;
        }

        var smallestIndex = 0;
        for (var i = 1; i < misses.Count; i += 1)
        {
            if (misses[i].Distance < misses[smallestIndex].Distance)
            {
                smallestIndex = i;
            }
        }

        misses.RemoveAt(smallestIndex);
    }

    public static BotBrainCorridorBakeStats BakeCorridorIntoAsset(
        SimpleLevel level,
        BotNavigationAsset asset,
        List<List<BotBrainCorridorRecordingSample>> segments,
        PlayerTeam team,
        PlayerClass playerClass)
    {
        var nodesAdded = 0;
        var edgesAdded = 0;
        var surfacesAdded = 0;
        var startTeam = ResolveCorridorStartTeam(level, segments, team);
        var supportedClassMask = ResolveCorridorSupportedClassMask(level, playerClass, team, startTeam);
        var corridorCostMultiplier = ResolveCorridorPreferredCostMultiplier(level, playerClass, team, startTeam);
        var supportedTeamMask = ResolveCorridorSupportedTeamMask(level, segments, team, startTeam);
        var existingEdges = new Dictionary<(int FromNode, int ToNode, NavEdgeKind Kind, int TeamMask), BotNavigationEdgeAssetEntry>();
        foreach (var edge in asset.Edges)
        {
            existingEdges[(edge.FromNode, edge.ToNode, edge.Kind, edge.SupportedTeamMask)] = edge;
        }

        foreach (var segment in segments)
        {
            var routeSegment = ResolveCorridorRouteSegment(level, segment);
            var previousNode = -1;
            foreach (var sample in routeSegment)
            {
                if (!sample.IsGrounded)
                {
                    continue;
                }

                var nodeIndex = FindOrAddCorridorAssetNode(asset, sample, ref nodesAdded, ref surfacesAdded);
                if (nodeIndex < 0)
                {
                    continue;
                }

                if (previousNode >= 0 && previousNode != nodeIndex)
                {
                    var from = asset.Nodes[previousNode];
                    var to = asset.Nodes[nodeIndex];
                    var distance = Distance(from.X, from.Y, to.X, to.Y);
                    if (distance <= 360f)
                    {
                        var kind = ResolveCorridorAssetEdgeKind(from, to);
                        var preferredCost = MathF.Max(1f, distance * corridorCostMultiplier);
                        var edgeKey = (previousNode, nodeIndex, kind, supportedTeamMask);
                        if (existingEdges.TryGetValue(edgeKey, out var existingEdge))
                        {
                            existingEdge.Cost = MathF.Min(existingEdge.Cost, preferredCost);
                            existingEdge.SupportedClassMask |= supportedClassMask;
                        }
                        else
                        {
                            var edge = new BotNavigationEdgeAssetEntry
                            {
                                FromNode = previousNode,
                                ToNode = nodeIndex,
                                Kind = kind,
                                Cost = preferredCost,
                                SupportedClassMask = supportedClassMask,
                                SupportedTeamMask = supportedTeamMask,
                            };
                            asset.Edges.Add(edge);
                            existingEdges.Add(edgeKey, edge);
                            edgesAdded += 1;
                        }
                    }
                }

                previousNode = nodeIndex;
            }
        }

        return new BotBrainCorridorBakeStats(nodesAdded, edgesAdded, surfacesAdded);
    }

    public static BotBrainCorridorBakeStats BakeIsolatedProofCorridorIntoAsset(
        SimpleLevel level,
        BotNavigationAsset asset,
        List<List<BotBrainCorridorRecordingSample>> segments,
        PlayerTeam team,
        PlayerClass playerClass)
    {
        var nodesAdded = 0;
        var edgesAdded = 0;
        var surfacesAdded = 0;
        var originalNodeCount = asset.Nodes.Count;
        var startTeam = ResolveCorridorStartTeam(level, segments, team);
        var supportedClassMask = ResolveCorridorSupportedClassMask(level, playerClass, team, startTeam);
        var supportedTeamMask = ResolveCorridorSupportedTeamMask(level, segments, team, startTeam);
        var existingEdges = new Dictionary<(int FromNode, int ToNode, NavEdgeKind Kind, int TeamMask), BotNavigationEdgeAssetEntry>();
        foreach (var edge in asset.Edges)
        {
            existingEdges[(edge.FromNode, edge.ToNode, edge.Kind, edge.SupportedTeamMask)] = edge;
        }

        foreach (var segment in segments)
        {
            var routeSegment = ResolveCorridorRouteSegment(level, segment)
                .Where(static sample => sample.IsGrounded || sample.Reason == "Score")
                .ToArray();
            if (routeSegment.Length < 2)
            {
                continue;
            }

            var firstOriginalNode = FindNearestCorridorAssetNode(asset, routeSegment[0].X, routeSegment[0].Y, maxDistance: 192f, nodeLimit: originalNodeCount);
            var lastOriginalNode = FindNearestCorridorAssetNode(asset, routeSegment[^1].X, routeSegment[^1].Y, maxDistance: 192f, nodeLimit: originalNodeCount);
            var previousNode = -1;
            var firstVirtualNode = -1;
            for (var i = 0; i < routeSegment.Length; i += 1)
            {
                var sample = routeSegment[i];
                var nodeIndex = AddIsolatedCorridorAssetNode(asset, sample, ref nodesAdded, ref surfacesAdded);
                if (firstVirtualNode < 0)
                {
                    firstVirtualNode = nodeIndex;
                }

                if (previousNode >= 0)
                {
                    edgesAdded += AddOrRelaxCorridorAssetEdge(
                        asset,
                        existingEdges,
                        previousNode,
                        nodeIndex,
                        supportedClassMask,
                        supportedTeamMask,
                        costMultiplier: 0.12f,
                        routeSegment[i - 1],
                        routeSegment[i]);
                }

                previousNode = nodeIndex;
            }

            if (firstOriginalNode >= 0 && firstVirtualNode >= 0 && firstOriginalNode != firstVirtualNode)
            {
                edgesAdded += AddOrRelaxCorridorAssetEdge(asset, existingEdges, firstOriginalNode, firstVirtualNode, supportedClassMask, supportedTeamMask, costMultiplier: 0.12f);
            }

            if (lastOriginalNode >= 0 && previousNode >= 0 && previousNode != lastOriginalNode)
            {
                edgesAdded += AddOrRelaxCorridorAssetEdge(asset, existingEdges, previousNode, lastOriginalNode, supportedClassMask, supportedTeamMask, costMultiplier: 0.12f);
            }
        }

        return new BotBrainCorridorBakeStats(nodesAdded, edgesAdded, surfacesAdded);
    }

    private static int AddOrRelaxCorridorAssetEdge(
        BotNavigationAsset asset,
        Dictionary<(int FromNode, int ToNode, NavEdgeKind Kind, int TeamMask), BotNavigationEdgeAssetEntry> existingEdges,
        int fromNode,
        int toNode,
        int supportedClassMask,
        int supportedTeamMask,
        float costMultiplier,
        BotBrainCorridorRecordingSample? fromSample = null,
        BotBrainCorridorRecordingSample? toSample = null)
    {
        if ((uint)fromNode >= (uint)asset.Nodes.Count || (uint)toNode >= (uint)asset.Nodes.Count || fromNode == toNode)
        {
            return 0;
        }

        var from = asset.Nodes[fromNode];
        var to = asset.Nodes[toNode];
        var distance = Distance(from.X, from.Y, to.X, to.Y);
        if (distance <= 0f || distance > 520f)
        {
            return 0;
        }

        var kind = ResolveCorridorAssetEdgeKind(from, to);
        var preferredCost = MathF.Max(1f, distance * costMultiplier);
        var edgeKey = (fromNode, toNode, kind, supportedTeamMask);
        if (existingEdges.TryGetValue(edgeKey, out var existingEdge))
        {
            existingEdge.Cost = MathF.Min(existingEdge.Cost, preferredCost);
            existingEdge.SupportedClassMask |= supportedClassMask;
            return 0;
        }

        var edge = new BotNavigationEdgeAssetEntry
        {
            FromNode = fromNode,
            ToNode = toNode,
            Kind = kind,
            Cost = preferredCost,
            SupportedClassMask = supportedClassMask,
            SupportedTeamMask = supportedTeamMask,
        };
        if (fromSample is not null && toSample is not null)
        {
            ApplyProofEdgeRecipe(edge, fromSample, toSample, kind);
        }

        asset.Edges.Add(edge);
        existingEdges.Add(edgeKey, edge);
        return 1;
    }

    private static void ApplyProofEdgeRecipe(
        BotNavigationEdgeAssetEntry edge,
        BotBrainCorridorRecordingSample from,
        BotBrainCorridorRecordingSample to,
        NavEdgeKind kind)
    {
        var moveDirection = MathF.Abs(from.MoveDirection) > 0.1f
            ? MathF.Sign(from.MoveDirection)
            : MathF.Sign(to.X - from.X);
        edge.ProbeCertified = true;
        edge.ProbeJumpTriggerTick = from.Jump ? 0 : kind == NavEdgeKind.Jump ? 3 : 0;
        edge.ProbeTicks = Math.Max(1, to.Tick - from.Tick);
        edge.ProbeMoveDirectionX = moveDirection;
        edge.ProbeVariantAttempts = 1;
        edge.ProbeVariantSuccesses = 1;
        edge.CompletionMinX = to.X - 48f;
        edge.CompletionMaxX = to.X + 48f;
        edge.CompletionMinY = to.Y - 32f;
        edge.CompletionMaxY = to.Y + 32f;
        edge.AcceptedLandingSurfaceIds = edge.AcceptedLandingSurfaceIds.Count == 0
            ? []
            : edge.AcceptedLandingSurfaceIds;
        edge.RequiresGroundedContinuation = to.IsGrounded;
        edge.LaunchRecipe = new BotNavigationLaunchRecipeAssetEntry
        {
            StartGrounded = from.IsGrounded,
            LaunchTick = edge.ProbeJumpTriggerTick,
            LaunchMinX = from.X - 64f,
            LaunchMaxX = from.X + 64f,
            LaunchMinY = from.Y - 48f,
            LaunchMaxY = from.Y + 48f,
            LaunchMinHorizontalSpeed = from.HorizontalSpeed - 160f,
            LaunchMaxHorizontalSpeed = from.HorizontalSpeed + 160f,
            ExpectedMoveDirectionX = moveDirection,
        };
    }

    private static IReadOnlyList<BotBrainCorridorRecordingSample> ResolveCorridorRouteSegment(
        SimpleLevel level,
        List<BotBrainCorridorRecordingSample> segment)
    {
        if (segment.Count < 2
            || level.Mode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
            || !TryFindNearestControlObjective(level, segment[0].X, segment[0].Y, out var objective))
        {
            return segment;
        }

        for (var i = 1; i < segment.Count; i += 1)
        {
            if (DistanceSquared(segment[i].X, segment[i].Y, objective.CenterX, objective.CenterY)
                <= CorridorObjectiveArrivalDistance * CorridorObjectiveArrivalDistance)
            {
                return segment.Take(i + 1).ToArray();
            }
        }

        return segment;
    }

    private static int ResolveCorridorSupportedClassMask(
        SimpleLevel level,
        PlayerClass playerClass,
        PlayerTeam recordedTeam,
        PlayerTeam startTeam) =>
        playerClass == PlayerClass.Heavy
        || (level.Mode is GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
            && startTeam != recordedTeam)
            ? -1
            : 1 << (int)playerClass;

    private static float ResolveCorridorPreferredCostMultiplier(
        SimpleLevel level,
        PlayerClass playerClass,
        PlayerTeam recordedTeam,
        PlayerTeam startTeam) =>
        level.Mode == GameModeKind.CaptureTheFlag
        || playerClass == PlayerClass.Heavy
        || startTeam != recordedTeam
            ? CorridorPreferredCostMultiplier
            : 1f;

    private static int ResolveCorridorSupportedTeamMask(
        SimpleLevel level,
        List<List<BotBrainCorridorRecordingSample>> segments,
        PlayerTeam team,
        PlayerTeam startTeam)
    {
        if (level.Mode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
        {
            return 1 << (int)team;
        }

        return 1 << (int)startTeam;
    }

    private static PlayerTeam ResolveCorridorStartTeam(
        SimpleLevel level,
        List<List<BotBrainCorridorRecordingSample>> segments,
        PlayerTeam team)
    {
        if (level.Mode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill))
        {
            return team;
        }

        foreach (var segment in segments)
        {
            if (segment.Count == 0)
            {
                continue;
            }

            var start = segment[0];
            var redDistance = MinSpawnDistanceSquared(level.RedSpawns, start.X, start.Y);
            var blueDistance = MinSpawnDistanceSquared(level.BlueSpawns, start.X, start.Y);
            if (!float.IsFinite(redDistance) || !float.IsFinite(blueDistance) || MathF.Abs(redDistance - blueDistance) < 1f)
            {
                break;
            }

            return redDistance < blueDistance ? PlayerTeam.Red : PlayerTeam.Blue;
        }

        return team;
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

    private static NavEdgeKind ResolveCorridorAssetEdgeKind(
        BotNavigationNodeAssetEntry from,
        BotNavigationNodeAssetEntry to)
    {
        if (to.Y > from.Y + 12f)
        {
            return NavEdgeKind.Fall;
        }

        return to.Y < from.Y - 12f
            ? NavEdgeKind.Jump
            : NavEdgeKind.Walk;
    }

    private static int FindOrAddCorridorAssetNode(
        BotNavigationAsset asset,
        BotBrainCorridorRecordingSample sample,
        ref int nodesAdded,
        ref int surfacesAdded)
    {
        var nearest = FindNearestCorridorAssetNode(asset, sample.X, sample.Y, maxDistance: 48f);
        if (nearest >= 0)
        {
            return nearest;
        }

        var nodeIndex = asset.Nodes.Count;
        var surfaceId = asset.Surfaces.Count;
        asset.Surfaces.Add(new BotNavigationSurfaceAssetEntry
        {
            Id = surfaceId,
            LeftX = sample.X,
            RightX = sample.X,
            TopY = sample.Y + 24f,
            IsDropdown = false,
            FirstNodeIndex = nodeIndex,
            LastNodeIndex = nodeIndex,
        });
        asset.Nodes.Add(new BotNavigationNodeAssetEntry
        {
            X = sample.X,
            Y = sample.Y,
            Kind = NavNodeKind.Surface,
            SurfaceId = surfaceId,
        });
        nodesAdded += 1;
        surfacesAdded += 1;
        return nodeIndex;
    }

    private static int AddIsolatedCorridorAssetNode(
        BotNavigationAsset asset,
        BotBrainCorridorRecordingSample sample,
        ref int nodesAdded,
        ref int surfacesAdded)
    {
        var nodeIndex = asset.Nodes.Count;
        var surfaceId = asset.Surfaces.Count;
        asset.Surfaces.Add(new BotNavigationSurfaceAssetEntry
        {
            Id = surfaceId,
            LeftX = sample.X,
            RightX = sample.X,
            TopY = sample.Y + 24f,
            IsDropdown = false,
            FirstNodeIndex = nodeIndex,
            LastNodeIndex = nodeIndex,
        });
        asset.Nodes.Add(new BotNavigationNodeAssetEntry
        {
            X = sample.X,
            Y = sample.Y,
            Kind = NavNodeKind.Surface,
            SurfaceId = surfaceId,
        });
        nodesAdded += 1;
        surfacesAdded += 1;
        return nodeIndex;
    }

    private static int FindNearestCorridorAssetNode(
        BotNavigationAsset asset,
        float x,
        float y,
        float maxDistance,
        int? nodeLimit = null)
    {
        var bestNode = -1;
        var bestDistanceSq = maxDistance * maxDistance;
        var count = Math.Min(asset.Nodes.Count, nodeLimit ?? asset.Nodes.Count);
        for (var i = 0; i < count; i += 1)
        {
            if (!asset.Nodes[i].SurfaceId.HasValue)
            {
                continue;
            }

            var dx = asset.Nodes[i].X - x;
            var dy = asset.Nodes[i].Y - y;
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

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static string FindRepoRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenGarrison.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not find repository root from '{start}'.");
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
    bool PrintPathChanges,
    bool RebuildAsset,
    bool SaveShippedAsset,
    bool AutoBakeProofCorridor,
    bool AcceptProofCorridorBake,
    bool SpawnEnemyDummy,
    float EnemyDummyX,
    float EnemyDummyY,
    float StartX,
    float StartY,
    bool DropRedIntel,
    float DropRedIntelX,
    float DropRedIntelY,
    bool DropBlueIntel,
    float DropBlueIntelX,
    float DropBlueIntelY,
    string ArtifactsDirectory)
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
            GetBool(options, "print-routes", false),
            GetBool(options, "rebuild-asset", false),
            GetBool(options, "save-shipped-asset", false),
            GetBool(options, "auto-bake-proof-corridor", false),
            GetBool(options, "accept-proof-corridor-bake", false),
            GetBool(options, "enemy-dummy", false),
            GetFloat(options, "enemy-x", float.NaN),
            GetFloat(options, "enemy-y", float.NaN),
            GetFloat(options, "start-x", float.NaN),
            GetFloat(options, "start-y", float.NaN),
            TryGetPoint(options, "drop-red-intel", out var dropRedIntelX, out var dropRedIntelY),
            dropRedIntelX,
            dropRedIntelY,
            TryGetPoint(options, "drop-blue-intel", out var dropBlueIntelX, out var dropBlueIntelY),
            dropBlueIntelX,
            dropBlueIntelY,
            GetString(options, "artifacts-dir", string.Empty));
    }

    private static string GetString(Dictionary<string, string> options, string key, string fallback)
        => options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static int GetInt(Dictionary<string, string> options, string key, int fallback)
        => options.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static float GetFloat(Dictionary<string, string> options, string key, float fallback)
        => options.TryGetValue(key, out var value) && float.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool TryGetPoint(Dictionary<string, string> options, string key, out float x, out float y)
    {
        x = float.NaN;
        y = float.NaN;
        if (!options.TryGetValue(key, out var value))
        {
            return false;
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && float.TryParse(parts[0], out x)
            && float.TryParse(parts[1], out y);
    }

    private static bool GetBool(Dictionary<string, string> options, string key, bool fallback)
        => options.TryGetValue(key, out var value) ? bool.TryParse(value, out var parsed) ? parsed : value == "1" : fallback;

    private static T GetEnum<T>(Dictionary<string, string> options, string key, T fallback)
        where T : struct
        => options.TryGetValue(key, out var value) && Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}

internal readonly record struct BotBrainProofEvaluation(
    bool Scored,
    int ScoreTick,
    float TotalMovement,
    int JumpTicks,
    int SemanticRecoveries,
    string FailureReason);

internal sealed record BotBrainCorridorRecording(
    int FormatVersion,
    string LevelName,
    int MapAreaIndex,
    float MapScale,
    string Mode,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    long StartFrame,
    long EndFrame,
    int StartRedCaps,
    int StartBlueCaps,
    int EndRedCaps,
    int EndBlueCaps,
    BotBrainCorridorRecordingSample[] Samples,
    BotBrainCorridorRecordingMark[] Marks);

internal sealed record BotBrainCorridorRecordingSample(
    long Frame,
    int Tick,
    string Reason,
    float X,
    float Y,
    float Bottom,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool IsGrounded,
    int RemainingAirJumps,
    float MoveDirection,
    bool Jump,
    bool DropDown,
    bool IsCarryingIntel,
    int RedCaps,
    int BlueCaps);

internal sealed record BotBrainProofTapeSample(
    int Tick,
    float X,
    float Y,
    float Bottom,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool IsGrounded,
    bool Left,
    bool Right,
    bool Up,
    bool Down,
    bool IsCarryingIntel);

internal sealed record BotBrainCorridorRecordingMark(
    string Kind,
    long Frame,
    int Tick,
    float X,
    float Y,
    float Bottom,
    bool IsGrounded,
    bool IsCarryingIntel);

internal sealed record BotBrainCompiledCorridor(
    int FormatVersion,
    string SourceRecordingPath,
    string LevelName,
    int MapAreaIndex,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    BotBrainCompiledCorridorWaypoint[] Waypoints,
    BotBrainCompiledCorridorGap[] Gaps,
    BotBrainCorridorRecordingMark[] Marks);

internal sealed record BotBrainCompiledCorridorWaypoint(
    int SampleTick,
    string Reason,
    int NodeIndex,
    float NodeX,
    float NodeY,
    int? SurfaceId,
    float SampleX,
    float SampleY,
    bool SampleGrounded,
    bool SampleCarryingIntel);

internal sealed record BotBrainCompiledCorridorGap(
    int FromWaypointIndex,
    int ToWaypointIndex,
    int FromNode,
    int ToNode,
    float FromX,
    float FromY,
    float ToX,
    float ToY,
    float Dx,
    float Dy,
    string SuggestedProbeCommand);

internal sealed record BotBrainCorridorSnapMiss(
    int Tick,
    float SampleX,
    float SampleY,
    int NodeIndex,
    float NodeX,
    float NodeY,
    float Distance);

internal sealed record BotBrainCorridorBakeStats(
    int NodesAdded,
    int EdgesAdded,
    int SurfacesAdded);
