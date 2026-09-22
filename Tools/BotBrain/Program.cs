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
if (rawOptions.ContainsKey("compact-alpha-cache"))
{
    var compactionResult = Og2NavigationGraphCache.CompactPersistentCache();
    Console.WriteLine(
        $"alphaCacheCompaction scanned={compactionResult.Scanned} compressed={compactionResult.Compressed} " +
        $"skipped={compactionResult.Skipped} failed={compactionResult.Failed} " +
        $"pruned={compactionResult.Pruned} bytesBefore={compactionResult.BytesBefore} " +
        $"bytesAfter={compactionResult.BytesAfter} bytesPruned={compactionResult.BytesPruned}");
    return;
}

if (rawOptions.TryGetValue("verified-nav-report", out var verifiedNavReport)
    && bool.TryParse(verifiedNavReport, out var parsedVerifiedNavReport)
    && parsedVerifiedNavReport)
{
    BotBrainToolCommandHelpers.RunVerifiedNavReport(rawOptions, artifactJsonOptions);
    return;
}

if (rawOptions.TryGetValue("local-motion-lab", out var localMotionLab)
    && bool.TryParse(localMotionLab, out var parsedLocalMotionLab)
    && parsedLocalMotionLab)
{
    BotBrainToolCommandHelpers.RunLocalMotionLab(rawOptions);
    return;
}

if (rawOptions.TryGetValue("direct-drive-lab", out var directDriveLab)
    && bool.TryParse(directDriveLab, out var parsedDirectDriveLab)
    && parsedDirectDriveLab)
{
    BotBrainToolCommandHelpers.RunDirectDriveLab(rawOptions);
    return;
}

if (rawOptions.TryGetValue("topology-local-motion-lab", out var topologyLocalMotionLab)
    && bool.TryParse(topologyLocalMotionLab, out var parsedTopologyLocalMotionLab)
    && parsedTopologyLocalMotionLab)
{
    var labOptions = TopologyLocalMotionLabOptions.FromRawOptions(rawOptions, FindRepoRoot(AppContext.BaseDirectory));
    var labSummary = TopologyLocalMotionLab.Run(labOptions);
    Console.WriteLine(JsonSerializer.Serialize(labSummary, artifactJsonOptions));
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
        corridorRecordingMapScale);
    return;
}

var options = BotBrainCanaryOptions.Parse(args);

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
ContentRoot.Initialize(Path.Combine(repoRoot, "Core", "Content"));

if (rawOptions.ContainsKey("alpha-nav-report"))
{
    Og2AlphaNavigationDiagnostics.RunReport(rawOptions);
    return;
}

if (rawOptions.ContainsKey("alpha-graph-gate"))
{
    Og2AlphaNavigationDiagnostics.RunGraphGate(rawOptions);
    return;
}

if (rawOptions.ContainsKey("audit-shipped-alpha-graphs"))
{
    Og2AlphaNavigationDiagnostics.RunShippedGraphAudit(rawOptions);
    return;
}

if (rawOptions.ContainsKey("prewarm-alpha-graphs"))
{
    Og2AlphaNavigationDiagnostics.RunPrewarmShippedGraphs(rawOptions);
    return;
}

if (rawOptions.ContainsKey("alpha-raw-validate"))
{
    Og2AlphaNavigationDiagnostics.RunRawTransitionValidation(rawOptions);
    return;
}

if (rawOptions.ContainsKey("alpha-raw-sweep"))
{
    Og2AlphaNavigationDiagnostics.RunRawMovementSweep(rawOptions);
    return;
}

if (rawOptions.ContainsKey("alpha-capture-matrix"))
{
    Og2AlphaNavigationDiagnostics.RunCaptureMatrix(rawOptions);
    return;
}

if (rawOptions.TryGetValue("bot-traversal-soak", out var botTraversalSoak)
    && bool.TryParse(botTraversalSoak, out var parsedBotTraversalSoak)
    && parsedBotTraversalSoak)
{
    RunBotTraversalSoak(options, rawOptions);
    return;
}

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

if (rawOptions.TryGetValue("simulate-practice-roster", out var simulatePracticeRoster)
    && bool.TryParse(simulatePracticeRoster, out var parsedSimulatePracticeRoster)
    && parsedSimulatePracticeRoster)
{
    RunPracticeRosterSimulation(level, graph, options, rawOptions);
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

RunNeutralPreTicks(world, options.PreTicks);
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
var exactGoalNode = world.MatchRules.Mode is GameModeKind.Arena or GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill
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
BotBrainRuntimeStateArtifact? carryingIntelState = null;
var scoreTick = -1;
var objectiveCompletionReason = string.Empty;
var initialRedCaps = world.RedCaps;
var initialBlueCaps = world.BlueCaps;
var initialPlayerCaps = bot.Caps;
var lastPrintedGoalNode = -1;
var lastPrintedPathCount = -1;
var edgeDiagnostics = new EdgeExecutionDiagnostics();
var semanticRecoveryTraces = new List<string>();
var semanticRecoveryEvents = new List<SemanticRecoveryArtifact>();
var validationIssues = FilterControlMarkerValidationIssues(asset);
var artifactDirectory = ResolveArtifactDirectory(options.ArtifactsDirectory);
var proofCorridorSamples = new List<BotBrainCorridorRecordingSample>();
var initialRouteEdges = path is not null ? BuildRouteEdgeArtifacts(graph, path) : [];
var initialRouteQuality = AnalyzeRouteQuality(initialRouteEdges, path?.TotalCost ?? -1f);
var graphQuality = AnalyzeGraphQuality(graph);
var authorityDiagnostics = new AuthorityTransitionDiagnostics();

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
Console.WriteLine(
    $"graphQuality=suspiciousVerticalRelays:{graphQuality.SuspiciousVerticalRelayEdges} verticalWalk:{graphQuality.VerticalWalkEdges} " +
    $"uncertifiedNonWalk:{graphQuality.UncertifiedNonWalkEdges} missingCompletion:{graphQuality.MissingCompletionNonWalkEdges} " +
    $"weakProbe:{graphQuality.WeakProbeEdges} zeroOrLowCost:{graphQuality.ZeroOrLowCostEdges} maxOutDegree:{graphQuality.MaxOutDegree} " +
    $"worstOutNode:{graphQuality.WorstOutDegreeNode} poisonScore:{graphQuality.PoisonScore}");
Console.WriteLine($"objectiveReachability=rawGoal:({goal.X:0.0},{goal.Y:0.0}) exactGoalNode:{exactGoalNode} exactPathWaypoints:{exactPath?.Count ?? 0} fallbackGoalNode:{goalNode} fallbackUsed:{(goalNode != exactGoalNode).ToString(CultureInfo.InvariantCulture)} exactGoal:{FormatComponent(exactGoalComponent, components)} fallbackGoal:{FormatComponent(goalComponent, components)}");
Console.WriteLine($"reachability=fromStart:{reachableFromStart}/{graph.NodeCount} toGoal:{reachableToGoal}/{graph.NodeCount}");
Console.WriteLine($"components=count:{components.Summaries.Count} start:{FormatComponent(startComponent, components)} goal:{FormatComponent(goalComponent, components)} top:{string.Join(';', components.Summaries.OrderByDescending(static c => c.NodeCount).Take(5).Select(static c => $"#{c.Id}:{c.NodeCount}"))}");
if (path is not null)
{
    Console.WriteLine($"route={FormatRoute(graph, path)}");
    Console.WriteLine(
        $"routeQuality=edges:{initialRouteQuality.EdgeCount} cheapVertical:{initialRouteQuality.CheapVerticalRelayEdges} " +
        $"verticalWalk:{initialRouteQuality.VerticalWalkEdges} verticalNonWalk:{initialRouteQuality.VerticalNonWalkEdges} " +
        $"suspiciousVerticalWalk:{initialRouteQuality.SuspiciousVerticalWalkEdges} repeatedNodes:{initialRouteQuality.RepeatedNodes} " +
        $"runtimePenalty:{initialRouteQuality.RuntimePenaltyCost:0.0}");
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
        false,
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
        null,
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
        semanticRecoveryTraces,
        semanticRecoveryEvents);
    Environment.ExitCode = 2;
    return;
}

for (var tick = 1; tick <= options.Ticks; tick += 1)
{
    var input = brain.Think(bot, world, options.Team);
    authorityDiagnostics.Observe(
        tick,
        ResolveMovementAuthority(brain),
        bot.IsCarryingIntel,
        brain.CurrentPathIndex,
        brain.CurrentPathCount,
        brain.CurrentPathNode,
        brain.LastDirectDriveTrace,
        brain.LastSemanticRecoveryTrace);
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
    if (!string.IsNullOrWhiteSpace(brain.LastSemanticRecoveryTrace))
    {
        semanticRecoveryTraces.Add(brain.LastSemanticRecoveryTrace);
        semanticRecoveryEvents.Add(new SemanticRecoveryArtifact(
            Tick: tick,
            Reason: ExtractSemanticRecoveryReason(brain.LastSemanticRecoveryTrace),
            Trace: brain.LastSemanticRecoveryTrace,
            CarryingIntel: bot.IsCarryingIntel,
            PathIndex: tracePathIndex,
            PathCount: tracePathCount,
            PathNode: tracePathNode,
            X: diagnosticPreX,
            Y: diagnosticPreY,
            Grounded: diagnosticPreGrounded));
        Console.WriteLine($"tick:{tick} {brain.LastSemanticRecoveryTrace}");
    }

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
        var directDriveText = !string.IsNullOrWhiteSpace(brain.LastDirectDriveTrace)
            ? $" {brain.LastDirectDriveTrace}"
            : string.Empty;
        var intelText = world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            ? $" redIntel:{FormatIntelState(world.RedIntel)} blueIntel:{FormatIntelState(world.BlueIntel)}"
            : string.Empty;
        Console.WriteLine(
            $"trace tick={tick} pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0} speed=({bot.HorizontalSpeed:0.0},{bot.VerticalSpeed:0.0}) grounded={bot.IsGrounded} airJumps={bot.RemainingAirJumps} carrying={bot.IsCarryingIntel} input=L{Bit(traceInput.Left)}R{Bit(traceInput.Right)}U{Bit(traceInput.Up)}D{Bit(traceInput.Down)}F{Bit(traceInput.FirePrimary)} path={tracePathIndex}/{tracePathCount} node={nodeText}{edgeText}{recipeText}{directDriveText}{intelText}");
    }

    if (!bot.IsAlive)
    {
        deadTicks += 1;
    }

    if (bot.IsCarryingIntel && carryingIntelTick < 0)
    {
        carryingIntelTick = tick;
        carryingIntelState = CaptureRuntimeState(tick, bot, input);
    }

    if (scoreTick < 0
        && (bot.Caps > initialPlayerCaps
            || world.RedCaps > initialRedCaps
            || world.BlueCaps > initialBlueCaps))
    {
        scoreTick = tick;
        objectiveCompletionReason = "Score";
        BotBrainToolCommandHelpers.AddProofCorridorSample(proofCorridorSamples, world, bot, input, tick, "Score");
    }
    else if (scoreTick < 0 && TryDetectObjectiveCompletion(world, bot, options.Team, goal, out objectiveCompletionReason))
    {
        scoreTick = tick;
        BotBrainToolCommandHelpers.AddProofCorridorSample(proofCorridorSamples, world, bot, input, tick, objectiveCompletionReason);
    }
    else if (scoreTick < 0 && BotBrainToolCommandHelpers.ShouldRecordProofCorridorSample(proofCorridorSamples, bot, input, tick, carryingIntelTick))
    {
        BotBrainToolCommandHelpers.AddProofCorridorSample(proofCorridorSamples, world, bot, input, tick, "Stride");
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
Console.WriteLine($"summary=finalDistance:{finalDistance:0.0} bestDistance:{bestDistance:0.0} progress:{progress:0.0} totalMovement:{totalMovement:0.0} jumps:{jumpTicks} dropdowns:{dropdownTicks} fire:{fireTicks} deadTicks:{deadTicks} stagnantWindows:{stagnantWindows} carryingIntelTick:{carryingIntelTick} scoreTick:{scoreTick} objectiveReason:{(string.IsNullOrWhiteSpace(objectiveCompletionReason) ? "none" : objectiveCompletionReason)} redCaps:{world.RedCaps} blueCaps:{world.BlueCaps} playerCaps:{bot.Caps} finalNode:{finalNode} finalPathWaypoints:{finalPath?.Count ?? 0}");
Console.WriteLine($"failureBucket={failureBucket}");
var objectivePathAccepted = world.MatchRules.Mode == GameModeKind.Generator
    || (exactPath is not null && goalNode == exactGoalNode);
var proofPassed = scoreTick >= 0
    && validationIssues.Count == 0
    && objectivePathAccepted;
Console.WriteLine($"proofPassed={proofPassed}");
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
foreach (var profileLine in FormatCaptureProfileLines(
             edgeDiagnostics,
             graph,
             carryingIntelTick,
             scoreTick,
             totalMovement,
             jumpTicks,
             dropdownTicks,
             semanticRecoveryTraces.Count))
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
    proofPassed,
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
    carryingIntelState,
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
    semanticRecoveryTraces,
    semanticRecoveryEvents);

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

static BotBrainRuntimeStateArtifact CaptureRuntimeState(int tick, PlayerEntity bot, PlayerInputSnapshot input) =>
    new(
        tick,
        bot.X,
        bot.Y,
        bot.Bottom,
        bot.HorizontalSpeed,
        bot.VerticalSpeed,
        bot.IsGrounded,
        bot.RemainingAirJumps,
        bot.FacingDirectionX < 0f ? -1f : 1f,
        input.Left,
        input.Right,
        input.Up,
        input.Down,
        bot.IsCarryingIntel);

static IEnumerable<string> FormatCaptureProfileLines(
    EdgeExecutionDiagnostics edgeDiagnostics,
    NavGraph graph,
    int carryingIntelTick,
    int scoreTick,
    float totalMovement,
    int jumpTicks,
    int dropdownTicks,
    int semanticRecoveryCount)
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

    yield return FormatCaptureQuality(scoredEdges, graph, jumpTicks, dropdownTicks, semanticRecoveryCount, edgeDiagnostics.Blockers.Count);

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

static string FormatCaptureQuality(
    IReadOnlyCollection<EdgeExecutionArtifact> edges,
    NavGraph graph,
    int jumpTicks,
    int dropdownTicks,
    int semanticRecoveryCount,
    int blockerCount)
{
    const float ticksPerSecond = SimulationConfig.DefaultTicksPerSecond;
    var repeated = edges
        .GroupBy(static edge => $"{edge.FromNode}->{edge.ToNode}/{edge.Kind}")
        .Where(static group => group.Count() > 1)
        .ToArray();
    var repeatedEdgeVisits = repeated.Sum(static group => group.Count() - 1);
    var repeatedTicks = repeated.Sum(static group => group.Skip(1).Sum(static edge => edge.Ticks));
    var nonWalkEdges = 0;
    var verticalRelayEdges = 0;
    var uncertifiedNonWalkEdges = 0;
    var missingCompletionEdges = 0;
    var weakProbeEdges = 0;
    var recipeEdges = 0;

    foreach (var edgeArtifact in edges)
    {
        if (!TryFindGraphEdge(graph, edgeArtifact.FromNode, edgeArtifact.ToNode, edgeArtifact.Kind, out var edge))
        {
            continue;
        }

        if (edge.Kind != NavEdgeKind.Walk)
        {
            nonWalkEdges += 1;
            var hasCertifiedProof = edge.ProbeTicks > 0 || edge.ProbeVariantAttempts > 0 || edge.Completion.HasWindow;
            if (!hasCertifiedProof)
            {
                uncertifiedNonWalkEdges += 1;
            }

            if (!edge.Completion.HasWindow)
            {
                missingCompletionEdges += 1;
            }
        }

        if (edge.LaunchRecipe.HasRecipe)
        {
            recipeEdges += 1;
        }

        if (edge.ProbeVariantAttempts > 0 && edge.ProbeVariantSuccesses * 2 < edge.ProbeVariantAttempts)
        {
            weakProbeEdges += 1;
        }

        var from = graph.GetNode(edgeArtifact.FromNode);
        var to = graph.GetNode(edgeArtifact.ToNode);
        if (edge.Kind != NavEdgeKind.Walk && MathF.Abs(to.Y - from.Y) > 48f)
        {
            verticalRelayEdges += 1;
        }
    }

    return
        $"captureQuality=nonWalkEdges:{nonWalkEdges} verticalRelayEdges:{verticalRelayEdges} " +
        $"uncertifiedNonWalkEdges:{uncertifiedNonWalkEdges} missingCompletionEdges:{missingCompletionEdges} weakProbeEdges:{weakProbeEdges} " +
        $"recipeEdges:{recipeEdges} repeatedEdgeVisits:{repeatedEdgeVisits} repeatedSeconds:{repeatedTicks / ticksPerSecond:0.0} " +
        $"jumps:{jumpTicks} dropdowns:{dropdownTicks} semanticRecoveries:{semanticRecoveryCount} blockers:{blockerCount}";
}

static bool TryFindGraphEdge(NavGraph graph, int fromNode, int toNode, string kindText, out NavEdge edge)
{
    edge = default;
    if (!Enum.TryParse<NavEdgeKind>(kindText, ignoreCase: true, out var kind))
    {
        return false;
    }

    var edges = graph.GetEdges(fromNode);
    for (var i = 0; i < edges.Length; i += 1)
    {
        if (edges[i].ToNode == toNode && edges[i].Kind == kind)
        {
            edge = edges[i];
            return true;
        }
    }

    return false;
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

    RunNeutralPreTicks(candidateWorld, options.PreTicks);
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
    bool proofPassed,
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
    BotBrainRuntimeStateArtifact? carryingIntelState,
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
    IReadOnlyList<string> semanticRecoveryTraces,
    IReadOnlyList<SemanticRecoveryArtifact> semanticRecoveryEvents)
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
            proofPassed,
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
            carryingIntelState,
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
    WriteJson(Path.Combine(artifactDirectory, "graph-quality.json"), graphQuality, jsonOptions);
    WriteJson(Path.Combine(artifactDirectory, "initial-route-quality.json"), initialRouteQuality, jsonOptions);
    WriteJsonLines(Path.Combine(artifactDirectory, "initial-route.jsonl"), initialRouteEdges, jsonOptions);
    WriteJson(Path.Combine(artifactDirectory, "authority-summary.json"), authorityDiagnostics.BuildSummary(scoreTick), jsonOptions);
    WriteJsonLines(Path.Combine(artifactDirectory, "authority-transitions.jsonl"), authorityDiagnostics.Transitions, jsonOptions);
    WriteJsonLines(Path.Combine(artifactDirectory, "semantic-recoveries.jsonl"), semanticRecoveryEvents, jsonOptions);
    WriteJson(Path.Combine(artifactDirectory, "churn-summary.json"), BuildChurnSummary(edgeDiagnostics.Edges, semanticRecoveryEvents, semanticRecoveryTraces, scoreTick), jsonOptions);
    if (carryingIntelState is not null)
    {
        WriteJson(Path.Combine(artifactDirectory, "pickup-state.json"), carryingIntelState, jsonOptions);
    }
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

static bool TryDetectObjectiveCompletion(
    SimulationWorld world,
    PlayerEntity bot,
    PlayerTeam team,
    (float X, float Y) goal,
    out string reason)
{
    reason = string.Empty;
    if (world.MatchRules.Mode == GameModeKind.Arena)
    {
        foreach (var point in world.ControlPoints)
        {
            if (world.IsPlayerInControlPointCaptureZone(bot, point.Index))
            {
                reason = "ArenaCaptureZone";
                return true;
            }
        }

        foreach (var marker in world.Level.GetRoomObjects(RoomObjectType.CaptureZone))
        {
            if (bot.IntersectsMarker(marker.CenterX, marker.CenterY, marker.Width, marker.Height))
            {
                reason = "ArenaCaptureZone";
                return true;
            }
        }

        foreach (var marker in world.Level.GetRoomObjects(RoomObjectType.ArenaControlPoint))
        {
            if (bot.IntersectsMarker(marker.CenterX, marker.CenterY, marker.Width, marker.Height))
            {
                reason = "ArenaControlPoint";
                return true;
            }
        }
    }
    else if (world.MatchRules.Mode == GameModeKind.ControlPoint)
    {
        var targetPoint = world.ControlPoints
            .OrderBy(point => Distance(point.Marker.CenterX, point.Marker.CenterY, goal.X, goal.Y))
            .FirstOrDefault();
        if (targetPoint is not null
            && targetPoint.Team == team
            && world.IsPlayerInControlPointCaptureZone(bot, targetPoint.Index))
        {
            reason = "ControlPointDefenseZone";
            return true;
        }
    }
    else if (world.MatchRules.Mode == GameModeKind.Generator)
    {
        var opposingTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        foreach (var generator in world.Generators)
        {
            if (generator.Team != opposingTeam)
            {
                continue;
            }

            if (generator.IsDestroyed || generator.Health < generator.MaxHealth)
            {
                reason = generator.IsDestroyed ? "GeneratorDestroyed" : "GeneratorDamaged";
                return true;
            }

            var dx = MathF.Abs(generator.Marker.CenterX - bot.X);
            var dy = MathF.Abs(generator.Marker.CenterY - bot.Y);
            if (dx <= 96f && dy <= 96f && Distance(bot.X, bot.Y, goal.X, goal.Y) <= 128f)
            {
                reason = "GeneratorReached";
                return true;
            }
        }
    }

    return false;
}

static void RunBotTraversalSoak(
    BotBrainCanaryOptions options,
    IReadOnlyDictionary<string, string> rawOptions)
{
    var maps = GetTraversalSoakMaps(rawOptions, options.MapName);
    var failOnAcceptance = GetRosterBool(rawOptions, "fail-on-acceptance", true);
    if (GetRosterBool(rawOptions, "load-packaged-quote-curly", true))
    {
        RegisterPackagedQuoteCurlyGameplayPackIfAvailable(FindRepoRoot(AppContext.BaseDirectory));
    }

    var allPassed = true;
    var results = new List<TraversalSoakRunResult>(maps.Count);
    foreach (var mapName in maps)
    {
        var result = RunBotTraversalSoakMap(mapName, options, rawOptions);
        results.Add(result);
        allPassed &= result.Passed;
    }

    if (results.Count > 1)
    {
        Console.WriteLine(
            $"soakSuiteResult=maps:{results.Count} passed:{(allPassed ? 1 : 0)} " +
            $"failed:{results.Count(static result => !result.Passed)}");
        foreach (var result in results)
        {
            Console.WriteLine(
                $"soakSuiteMap=map:{result.MapName} area:{result.AreaIndex} " +
                $"passed:{(result.Passed ? 1 : 0)} reason:{result.Reason}");
        }
    }

    if (failOnAcceptance && !allPassed)
    {
        Environment.ExitCode = 1;
    }
}

static void RegisterPackagedQuoteCurlyGameplayPackIfAvailable(string repoRoot)
{
    if (CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(PlayerClass.Quote, out _))
    {
        Console.WriteLine("soakQuoteCurlyPack=already-loaded");
        return;
    }

    var packDirectory = Path.Combine(
        repoRoot,
        "Plugins",
        "Packaged",
        "Server",
        "Lua.QuoteCurly",
        "Gameplay",
        "quote-curly.gg2");
    if (!Directory.Exists(packDirectory))
    {
        Console.WriteLine($"soakQuoteCurlyPack=missing path=\"{packDirectory}\"");
        return;
    }

    var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory);
    if (!CharacterClassCatalog.RuntimeRegistry.TryRegisterModPack(
        pack,
        allowRuntimeClassBindingOverride: true,
        out var errorMessage))
    {
        throw new InvalidOperationException(
            $"Failed to register packaged Quote/Curly gameplay pack for traversal soak: {errorMessage}");
    }

    Console.WriteLine($"soakQuoteCurlyPack=loaded id:{pack.Id} path=\"{packDirectory}\"");
}

static TraversalSoakRunResult RunBotTraversalSoakMap(
    string mapName,
    BotBrainCanaryOptions options,
    IReadOnlyDictionary<string, string> rawOptions)
{
    var areaIndex = GetRosterInt(rawOptions, "area", GetRosterInt(rawOptions, "area-index", options.AreaIndex));
    var ticks = GetRosterInt(rawOptions, "ticks", Math.Max(options.Ticks, 18_000));
    var reportEvery = GetRosterInt(rawOptions, "report-every", Math.Max(300, options.ReportEveryTicks));
    var requestedBots = GetRosterInt(rawOptions, "bots", 24);
    var includeLocal = GetRosterBool(rawOptions, "include-local", true);
    var failOnAcceptance = GetRosterBool(rawOptions, "fail-on-acceptance", true);
    var stagnantWindowTicks = Math.Max(1, GetRosterInt(rawOptions, "stagnant-window-ticks", 30));
    var inertFailTicks = Math.Max(stagnantWindowTicks, GetRosterInt(rawOptions, "inert-fail-ticks", 150));
    var stagnantDistance = MathF.Max(0f, GetRosterFloat(rawOptions, "stagnant-distance", 12f));
    var oscillationWindowTicks = Math.Max(1, GetRosterInt(rawOptions, "oscillation-window-ticks", 150));
    var oscillationFlips = Math.Max(1, GetRosterInt(rawOptions, "oscillation-flips", 8));
    var oscillationDistance = MathF.Max(0f, GetRosterFloat(rawOptions, "oscillation-distance", 48f));
    var thinkSpikeMilliseconds = MathF.Max(0f, GetRosterFloat(rawOptions, "think-spike-ms", 100f));
    var forceObjectiveNavigation = GetRosterBool(rawOptions, "force-objective-navigation", false);
    var disableCombat = GetRosterBool(rawOptions, "disable-combat", false);
    var traceSlot = GetRosterInt(rawOptions, "trace-slot", -1);
    var traceFromTick = Math.Max(1, GetRosterInt(rawOptions, "trace-from-tick", 1));
    var traceToTick = Math.Max(traceFromTick, GetRosterInt(rawOptions, "trace-to-tick", int.MaxValue));
    var maxBots = includeLocal
        ? SimulationWorld.MaxPlayableNetworkPlayers
        : SimulationWorld.MaxPlayableNetworkPlayers - 1;
    var botCount = Math.Clamp(requestedBots, 1, maxBots);
    PlayerClass[] defaultClassCycle =
    [
        PlayerClass.Scout,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Heavy,
        PlayerClass.Demoman,
        PlayerClass.Medic,
        PlayerClass.Engineer,
        PlayerClass.Spy,
        PlayerClass.Sniper,
        PlayerClass.Quote,
    ];
    var classCycle = ResolveTraversalSoakClassCycle(rawOptions, defaultClassCycle);

    var world = new SimulationWorld();
    if (!world.TryLoadLevel(mapName, areaIndex, preservePlayerStats: false))
    {
        throw new InvalidOperationException($"SimulationWorld failed to load '{mapName}' area {areaIndex}.");
    }

    world.DespawnEnemyDummy();
    world.DespawnFriendlyDummy();
    if (!includeLocal)
    {
        world.PrepareLocalPlayerJoin();
    }

    var controllers = new Dictionary<byte, BotBrainController>();
    var stats = new Dictionary<byte, TraversalSoakBotStats>();
    var redCount = (botCount + 1) / 2;
    for (var index = 0; index < botCount; index += 1)
    {
        var slot = includeLocal
            ? (byte)(SimulationWorld.LocalPlayerSlot + index)
            : (byte)(SimulationWorld.LocalPlayerSlot + index + 1);
        var team = index < redCount ? PlayerTeam.Red : PlayerTeam.Blue;
        var teamIndex = team == PlayerTeam.Red ? index : index - redCount;
        var classOffset = team == PlayerTeam.Red ? 0 : 3;
        var classId = classCycle[(teamIndex + classOffset) % classCycle.Length];
        if (!TryConfigureTraversalSoakBot(world, slot, team, classId, out var bot))
        {
            Console.WriteLine($"soakBotSkipped=slot:{slot} team:{team} class:{classId}");
            continue;
        }

        controllers[slot] = new BotBrainController
        {
            ForceObjectiveNavigationForDiagnostics = forceObjectiveNavigation,
            DisableCombatForDiagnostics = disableCombat,
        };
        stats[slot] = new TraversalSoakBotStats(slot, team, classId, bot.X, bot.Y, bot.Bottom);
    }

    if (requestedBots > botCount)
    {
        Console.WriteLine(
            $"soakCapacity=requested:{requestedBots} using:{botCount} " +
            $"maxPlayableSlots:{SimulationWorld.MaxPlayableNetworkPlayers} includeLocal:{(includeLocal ? 1 : 0)}");
    }

    Console.WriteLine(
        $"soakStart=map:{world.Level.Name} area:{world.Level.MapAreaIndex} mode:{world.MatchRules.Mode} " +
        $"requestedBots:{requestedBots} bots:{stats.Count} ticks:{ticks} reportEvery:{reportEvery} " +
        $"inertFailTicks:{inertFailTicks} stagnantWindowTicks:{stagnantWindowTicks} " +
        $"stagnantDistance:{stagnantDistance:0.0} oscillationWindowTicks:{oscillationWindowTicks} " +
        $"oscillationFlips:{oscillationFlips} oscillationDistance:{oscillationDistance:0.0} " +
        $"thinkSpikeMs:{thinkSpikeMilliseconds:0.0} " +
        $"forceObjectiveNavigation:{(forceObjectiveNavigation ? 1 : 0)} " +
        $"disableCombat:{(disableCombat ? 1 : 0)} " +
        $"classCycle:{string.Join('+', classCycle.Select(static classId => classId.ToString()))} " +
        $"failOnAcceptance:{(failOnAcceptance ? 1 : 0)}");
    foreach (var entry in stats.Values.OrderBy(static stat => stat.Slot))
    {
        Console.WriteLine(
            $"soakBot=slot:{entry.Slot} team:{entry.Team} class:{entry.ClassId} " +
            $"start=({entry.StartX:0.0},{entry.StartY:0.0})");
    }

    var initialRedCaps = world.RedCaps;
    var initialBlueCaps = world.BlueCaps;
    var redFirstCapTick = -1;
    var blueFirstCapTick = -1;
    long totalThinkStopwatchTicks = 0;
    long totalTickStopwatchTicks = 0;
    long maxTickThinkStopwatchTicks = 0;
    long maxWorldAdvanceStopwatchTicks = 0;
    var controlledThinkTicks = 0L;
    for (var tick = 1; tick <= ticks; tick += 1)
    {
        var tickThinkStart = Stopwatch.GetTimestamp();
        var inputs = new Dictionary<byte, PlayerInputSnapshot>(controllers.Count);
        foreach (var (slot, controller) in controllers)
        {
            if (!world.TryGetNetworkPlayer(slot, out var bot))
            {
                continue;
            }

            var thinkStart = Stopwatch.GetTimestamp();
            var input = controller.Think(bot, world, bot.Team);
            var thinkElapsed = Stopwatch.GetTimestamp() - thinkStart;
            totalThinkStopwatchTicks += thinkElapsed;
            controlledThinkTicks += 1;
            inputs[slot] = bot.IsAlive ? input : default;
            var previousMoveSign = stats[slot].LastMoveSign;
            ObserveTraversalSoakBotPreAdvance(stats[slot], tick, bot, input, controller, thinkElapsed);
            if (slot == traceSlot
                && tick >= traceFromTick
                && tick <= traceToTick
                && previousMoveSign != stats[slot].LastMoveSign)
            {
                Console.WriteLine(
                    $"soakTrace=slot:{slot} tick:{tick} pos=({bot.X:0.0},{bot.Y:0.0}) " +
                    $"speed=({bot.HorizontalSpeed:0.0},{bot.VerticalSpeed:0.0}) " +
                    $"grounded:{(bot.IsGrounded ? 1 : 0)} " +
                    $"input=({(input.Left ? 'L' : input.Right ? 'R' : '-')}{(input.Up ? 'J' : '-')}) " +
                    $"path:{controller.CurrentPathIndex}/{controller.CurrentPathCount} " +
                    $"node:{controller.CurrentPathNode} goal:{controller.CurrentGoalNode} " +
                    $"trace:{stats[slot].LastTraversalTrace}");
            }
            var thinkElapsedMilliseconds = StopwatchTicksToMilliseconds(thinkElapsed);
            if (thinkSpikeMilliseconds > 0f && thinkElapsedMilliseconds >= thinkSpikeMilliseconds)
            {
                Console.WriteLine(
                    $"soakThinkSpike=tick:{tick} slot:{slot} team:{bot.Team} class:{bot.ClassId} " +
                    $"elapsedMs:{thinkElapsedMilliseconds:0.000} pos=({bot.X:0.0},{bot.Y:0.0}) " +
                    $"alive:{(bot.IsAlive ? 1 : 0)} grounded:{(bot.IsGrounded ? 1 : 0)} " +
                    $"pathIndex:{controller.CurrentPathIndex} pathCount:{controller.CurrentPathCount} " +
                    $"direct:{controller.LastDirectDriveTrace} semantic:{controller.LastSemanticRecoveryTrace} " +
                    $"timing:{controller.LastThinkTimingTrace}");
            }
        }

        foreach (var (slot, input) in inputs)
        {
            world.TrySetNetworkPlayerInput(slot, input);
        }

        var worldAdvanceStart = Stopwatch.GetTimestamp();
        world.AdvanceOneTick();
        var worldAdvanceElapsed = Stopwatch.GetTimestamp() - worldAdvanceStart;
        if (worldAdvanceElapsed > maxWorldAdvanceStopwatchTicks)
        {
            maxWorldAdvanceStopwatchTicks = worldAdvanceElapsed;
        }

        var worldAdvanceMilliseconds = StopwatchTicksToMilliseconds(worldAdvanceElapsed);
        if (worldAdvanceMilliseconds >= 50d)
        {
            Console.WriteLine(
                $"soakWorldAdvanceSpike=tick:{tick} elapsedMs:{worldAdvanceMilliseconds:0.000} " +
                $"redCaps:{world.RedCaps - initialRedCaps} blueCaps:{world.BlueCaps - initialBlueCaps}");
        }
        var tickThinkElapsed = Stopwatch.GetTimestamp() - tickThinkStart;
        totalTickStopwatchTicks += tickThinkElapsed;
        if (tickThinkElapsed > maxTickThinkStopwatchTicks)
        {
            maxTickThinkStopwatchTicks = tickThinkElapsed;
        }

        if (redFirstCapTick < 0 && world.RedCaps > initialRedCaps)
        {
            redFirstCapTick = tick;
        }

        if (blueFirstCapTick < 0 && world.BlueCaps > initialBlueCaps)
        {
            blueFirstCapTick = tick;
        }

        foreach (var (slot, controller) in controllers)
        {
            if (!world.TryGetNetworkPlayer(slot, out var bot))
            {
                continue;
            }

            ObserveTraversalSoakBotPostAdvance(
                stats[slot],
                tick,
                bot,
                controller,
                stagnantWindowTicks,
                inertFailTicks,
                stagnantDistance,
                oscillationWindowTicks,
                oscillationFlips,
                oscillationDistance);
        }

        if (reportEvery > 0 && tick % reportEvery == 0)
        {
            var maxInertTicks = stats.Values.Select(static stat => stat.MaxConsecutiveInertTicks).DefaultIfEmpty().Max();
            var oscillationEvents = stats.Values.Sum(static stat => stat.OscillationEvents);
            var avgThinkMsPerTick = StopwatchTicksToMilliseconds(totalThinkStopwatchTicks) / tick;
            var avgTickMsPerTick = StopwatchTicksToMilliseconds(totalTickStopwatchTicks) / tick;
            Console.WriteLine(
                $"soakTick={tick} redCaps:{world.RedCaps - initialRedCaps} blueCaps:{world.BlueCaps - initialBlueCaps} " +
                $"maxInertTicks:{maxInertTicks} oscillationEvents:{oscillationEvents} " +
                $"avgThinkMsPerTick:{avgThinkMsPerTick:0.000} avgTickMsPerTick:{avgTickMsPerTick:0.000} " +
                $"{FormatPracticeRosterIntelState("redIntel", world.RedIntel)} {FormatPracticeRosterIntelState("blueIntel", world.BlueIntel)}");
            foreach (var entry in stats.Values
                         .OrderByDescending(static stat => stat.ConsecutiveInertTicks)
                         .ThenByDescending(static stat => stat.RecentStagnant)
                         .ThenBy(static stat => stat.Slot)
                         .Take(8))
            {
                Console.WriteLine(FormatTraversalSoakBotTick(entry));
            }
        }
    }

    var finalRedCaps = world.RedCaps - initialRedCaps;
    var finalBlueCaps = world.BlueCaps - initialBlueCaps;
    var finalMaxInertTicks = stats.Values.Select(static stat => stat.MaxConsecutiveInertTicks).DefaultIfEmpty().Max();
    var finalOscillationEvents = stats.Values.Sum(static stat => stat.OscillationEvents);
    var ctfCapsPassed = world.MatchRules.Mode != GameModeKind.CaptureTheFlag
        || (finalRedCaps > 0 && finalBlueCaps > 0);
    var inertPassed = finalMaxInertTicks < inertFailTicks;
    var oscillationPassed = finalOscillationEvents == 0;
    var passed = ctfCapsPassed && inertPassed && oscillationPassed;
    var reason = FormatTraversalSoakResultReason(ctfCapsPassed, inertPassed, oscillationPassed);
    var totalThinkMs = StopwatchTicksToMilliseconds(totalThinkStopwatchTicks);
    var totalTickMs = StopwatchTicksToMilliseconds(totalTickStopwatchTicks);
    var avgThinkMsPerTickFinal = ticks > 0 ? totalThinkMs / ticks : 0d;
    var avgTickMsPerTickFinal = ticks > 0 ? totalTickMs / ticks : 0d;
    var avgThinkMsPerBotTick = controlledThinkTicks > 0 ? totalThinkMs / controlledThinkTicks : 0d;
    var maxTickThinkMs = StopwatchTicksToMilliseconds(maxTickThinkStopwatchTicks);
    var maxWorldAdvanceMs = StopwatchTicksToMilliseconds(maxWorldAdvanceStopwatchTicks);

    Console.WriteLine(
        $"soakResult=map:{world.Level.Name} area:{world.Level.MapAreaIndex} passed:{(passed ? 1 : 0)} reason:{reason} " +
        $"redCaps:{finalRedCaps} blueCaps:{finalBlueCaps} redFirstCapTick:{redFirstCapTick} blueFirstCapTick:{blueFirstCapTick} " +
        $"maxInertTicks:{finalMaxInertTicks} inertFailTicks:{inertFailTicks} oscillationEvents:{finalOscillationEvents} " +
        $"totalThinkMs:{totalThinkMs:0.000} avgThinkMsPerTick:{avgThinkMsPerTickFinal:0.000} " +
        $"totalTickMs:{totalTickMs:0.000} avgTickMsPerTick:{avgTickMsPerTickFinal:0.000} " +
        $"avgThinkMsPerBotTick:{avgThinkMsPerBotTick:0.0000} maxTickThinkMs:{maxTickThinkMs:0.000} " +
        $"maxWorldAdvanceMs:{maxWorldAdvanceMs:0.000}");
    foreach (var entry in stats.Values.OrderBy(static stat => stat.Slot))
    {
        Console.WriteLine(FormatTraversalSoakBotSummary(entry));
    }

    return new TraversalSoakRunResult(world.Level.Name, world.Level.MapAreaIndex, passed, reason);
}

static PlayerClass[] ResolveTraversalSoakClassCycle(
    IReadOnlyDictionary<string, string> rawOptions,
    PlayerClass[] fallback)
{
    if (!rawOptions.TryGetValue("class-cycle", out var classCycleText)
        && !rawOptions.TryGetValue("classes", out classCycleText))
    {
        return fallback;
    }

    var classes = new List<PlayerClass>();
    foreach (var token in classCycleText.Split(
        [',', ';', '|'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!Enum.TryParse<PlayerClass>(token, ignoreCase: true, out var classId))
        {
            throw new InvalidOperationException($"Unsupported traversal soak class \"{token}\".");
        }

        classes.Add(classId);
    }

    if (classes.Count == 0)
    {
        throw new InvalidOperationException("Traversal soak class-cycle cannot be empty.");
    }

    return classes.ToArray();
}

static bool TryConfigureTraversalSoakBot(
    SimulationWorld world,
    byte slot,
    PlayerTeam team,
    PlayerClass classId,
    out PlayerEntity bot)
{
    bot = null!;
    if (slot == SimulationWorld.LocalPlayerSlot)
    {
        world.TrySetNetworkPlayerTeam(slot, team);
        world.SetPendingLocalPlayerClass(classId);
        world.ForceRespawnLocalPlayer();
        return world.TryGetNetworkPlayer(slot, out bot);
    }

    return world.TryPrepareNetworkPlayerJoin(slot)
        && world.TrySetNetworkPlayerTeam(slot, team)
        && world.TryApplyNetworkPlayerClassSelection(slot, classId)
        && world.TryGetNetworkPlayer(slot, out bot);
}

static void ObserveTraversalSoakBotPreAdvance(
    TraversalSoakBotStats stats,
    int tick,
    PlayerEntity bot,
    PlayerInputSnapshot input,
    BotBrainController controller,
    long thinkStopwatchTicks)
{
    stats.LastPreX = bot.X;
    stats.LastPreY = bot.Y;
    stats.LastPreBottom = bot.Bottom;
    stats.LastInput = input;
    stats.LastSteering = controller.LastSteeringOutput;
        stats.LastTraversalTrace = FormatTraversalSoakControllerTrace(controller);
    stats.LastSemanticTrace = controller.LastSemanticRecoveryTrace;
        stats.ThinkTicks += 1;
        if (IsTraversalSoakIntentionalObjectiveHold(stats.LastTraversalTrace))
        {
            stats.IntentionalObjectiveHoldTicks += 1;
            stats.OscillationWindowIntentionalHoldTicks += 1;
        }
    stats.ThinkStopwatchTicks += thinkStopwatchTicks;
    if (thinkStopwatchTicks > stats.MaxThinkStopwatchTicks)
    {
        stats.MaxThinkStopwatchTicks = thinkStopwatchTicks;
    }

    if (!input.Left && !input.Right && !input.Up && !input.Down && !input.FirePrimary && !input.FireSecondary)
    {
        stats.ZeroInputTicks += 1;
    }

    var moveSign = input.Right == input.Left ? 0 : input.Right ? 1 : -1;
    if (moveSign != 0 && stats.LastMoveSign != 0 && moveSign != stats.LastMoveSign)
    {
        stats.MoveFlips += 1;
        if (MathF.Abs(bot.HorizontalSpeed) < 40f)
        {
            stats.OscillationWindowLowSpeedMoveFlips += 1;
            if (IsTraversalSoakGraphRouteOwned(stats.LastTraversalTrace))
            {
                stats.OscillationWindowRouteLowSpeedMoveFlips += 1;
                stats.RouteLowSpeedMoveFlips += 1;
            }
        }
        if (MathF.Abs(bot.HorizontalSpeed) < 40f)
        {
            stats.LowSpeedMoveFlips += 1;
        }
    }

    if (moveSign != 0)
    {
        stats.LastMoveSign = moveSign;
    }

    if (bot.IsCarryingIntel && stats.CarryingIntelTick < 0)
    {
        stats.CarryingIntelTick = tick;
    }
}

static string FormatTraversalSoakControllerTrace(BotBrainController controller)
{
    var steering = controller.LastSteeringOutput;
    var edgeTrace = controller.CurrentPath is { } currentPath
        && currentPath.TryGetCurrentEdge(out var currentEdge)
        ? $" edgeFrom:{(currentPath.CurrentIndex > 0 ? currentPath.GetWaypoint(currentPath.CurrentIndex - 1) : -1)} " +
          $"edgeProbe:{currentEdge.ProbeMoveDirectionX:0.0} edgeCompletion=({currentEdge.Completion.MinX:0.0},{currentEdge.Completion.MaxX:0.0},{currentEdge.Completion.MinY:0.0},{currentEdge.Completion.MaxY:0.0})"
        : string.Empty;
    var trace = string.Create(
        CultureInfo.InvariantCulture,
        $"nav=path:{controller.CurrentPathIndex}/{controller.CurrentPathCount} " +
        $"node:{controller.CurrentPathNode} goal:{controller.CurrentGoalNode} " +
        $"goalPos=({controller.CurrentGoalPosition.X:0.0},{controller.CurrentGoalPosition.Y:0.0}) " +
        $"combat:{(controller.LastCombatTarget is null ? 0 : 1)} " +
        $"move:{steering.MoveDirection:0.0} jump:{(steering.Jump ? 1 : 0)} " +
        $"repath:{(steering.RequestRepath ? 1 : 0)} edge:{steering.EdgeKind}{edgeTrace}");
    AppendTrace(controller.LastSemanticRecoveryTrace);
    AppendTrace(controller.LastDirectDriveTrace);
    if (steering.FailedEdge.HasFailure)
    {
        AppendTrace($"failedEdge={steering.FailedEdge.FromNode}->{steering.FailedEdge.ToNode}/{steering.FailedEdge.Kind}:{steering.FailedEdge.Reason}");
    }
    return trace;

    void AppendTrace(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        trace = trace.Length == 0 ? candidate : $"{trace} {candidate}";
    }
}

static void ObserveTraversalSoakBotPostAdvance(
    TraversalSoakBotStats stats,
    int tick,
    PlayerEntity bot,
    BotBrainController controller,
    int stagnantWindowTicks,
    int inertFailTicks,
    float stagnantDistance,
    int oscillationWindowTicks,
    int oscillationFlips,
    float oscillationDistance)
{
    var wasCarryingIntel = stats.LastCarryingIntel;
    var wasAlive = stats.LastAlive;
    var hadPreviousPostSample = stats.HasPostAdvanceSample;
    var capIncreased = bot.Caps > stats.LastCapsSeen;

    stats.LastX = bot.X;
    stats.LastY = bot.Y;
    stats.LastBottom = bot.Bottom;
    stats.LastGrounded = bot.IsGrounded;
    stats.LastHorizontalSpeed = bot.HorizontalSpeed;
    stats.LastVerticalSpeed = bot.VerticalSpeed;
    stats.LastCaps = bot.Caps;
    stats.LastCarryingIntel = bot.IsCarryingIntel;
    stats.HasPostAdvanceSample = true;

    if (hadPreviousPostSample && wasAlive && !bot.IsAlive)
    {
        stats.DeathCount += 1;
        if (stats.FirstDeathTick < 0)
        {
            stats.FirstDeathTick = tick;
        }
    }

    if (wasCarryingIntel && !bot.IsCarryingIntel && !capIncreased)
    {
        stats.CarryLossCount += 1;
        if (stats.FirstCarryLossTick < 0)
        {
            stats.FirstCarryLossTick = tick;
            stats.FirstCarryLossX = bot.X;
            stats.FirstCarryLossY = bot.Y;
        }
    }

    stats.LastAlive = bot.IsAlive;
    if (capIncreased)
    {
        if (stats.FirstCapTick < 0)
        {
            stats.FirstCapTick = tick;
        }

        if (stats.CarryingIntelTick >= 0 && stats.CarrierConversionTicks < 0)
        {
            stats.CarrierConversionTicks = tick - stats.CarryingIntelTick;
        }

        stats.LastCapsSeen = bot.Caps;
    }

    stats.TotalMovement += Distance(stats.PreviousX, stats.PreviousY, bot.X, bot.Y);
    stats.PreviousX = bot.X;
    stats.PreviousY = bot.Y;

    if (!bot.IsAlive)
    {
        stats.ConsecutiveInertTicks = 0;
        stats.WindowX = bot.X;
        stats.WindowY = bot.Y;
        stats.OscillationWindowX = bot.X;
        stats.OscillationWindowY = bot.Y;
        stats.OscillationWindowLowSpeedMoveFlips = 0;
        stats.OscillationWindowRouteLowSpeedMoveFlips = 0;
        stats.OscillationWindowIntentionalHoldTicks = 0;
        return;
    }

        if (tick % stagnantWindowTicks == 0)
        {
            var windowMovement = Distance(stats.WindowX, stats.WindowY, bot.X, bot.Y);
            stats.RecentWindowMovement = windowMovement;
            var intentionalObjectiveHold = IsTraversalSoakIntentionalObjectiveHold(stats.LastTraversalTrace);
            stats.RecentStagnant = windowMovement < stagnantDistance && !intentionalObjectiveHold;
        if (stats.RecentStagnant)
        {
            stats.StagnantWindows += 1;
            stats.ConsecutiveInertTicks += stagnantWindowTicks;
            if (stats.ConsecutiveInertTicks > stats.MaxConsecutiveInertTicks)
            {
                stats.MaxConsecutiveInertTicks = stats.ConsecutiveInertTicks;
            }

            if (stats.ConsecutiveInertTicks >= inertFailTicks && stats.FirstInertFailTick < 0)
            {
                stats.FirstInertFailTick = tick;
                stats.FirstInertFailX = bot.X;
                stats.FirstInertFailY = bot.Y;
                stats.FirstInertFailTrace = stats.LastTraversalTrace;
            }
        }
        else
        {
            stats.ConsecutiveInertTicks = 0;
        }

        stats.WindowX = bot.X;
        stats.WindowY = bot.Y;
    }

    if (tick % oscillationWindowTicks == 0)
    {
        var oscillationMovement = Distance(stats.OscillationWindowX, stats.OscillationWindowY, bot.X, bot.Y);
        stats.RecentOscillationWindowMovement = oscillationMovement;
        if (oscillationMovement < oscillationDistance
            && stats.OscillationWindowRouteLowSpeedMoveFlips >= oscillationFlips
            && stats.OscillationWindowIntentionalHoldTicks == 0)
        {
            stats.OscillationEvents += 1;
            if (stats.FirstOscillationTick < 0)
            {
                stats.FirstOscillationTick = tick;
                stats.FirstOscillationTrace = stats.LastTraversalTrace;
            }
        }

        stats.OscillationWindowX = bot.X;
        stats.OscillationWindowY = bot.Y;
        stats.OscillationWindowLowSpeedMoveFlips = 0;
        stats.OscillationWindowRouteLowSpeedMoveFlips = 0;
        stats.OscillationWindowIntentionalHoldTicks = 0;
    }
}

static bool IsTraversalSoakIntentionalObjectiveHold(string trace) =>
    trace.Contains("alphaCaptureArrivalHold", StringComparison.Ordinal)
    || trace.Contains("alphaCaptureContestHold", StringComparison.Ordinal)
    || trace.Contains("medicSupport:Pocket", StringComparison.Ordinal)
    || trace.Contains("medicSupport:Critical", StringComparison.Ordinal)
    || trace.Contains("engineerIntelDefense=hold", StringComparison.Ordinal)
    || trace.Contains("engineerIntelDefense=patrol", StringComparison.Ordinal);

static bool IsTraversalSoakGraphRouteOwned(string trace)
{
    if (string.IsNullOrWhiteSpace(trace)
        || !trace.Contains("nav=path:", StringComparison.Ordinal))
    {
        return false;
    }

    // These owners intentionally steer toward a moving combat/objective
    // target or run local recovery. Their direction changes are not evidence
    // that a static graph edge is oscillating, but keep the aggregate flip
    // counters visible in the report for performance review.
    return !trace.Contains("directRoute=", StringComparison.Ordinal)
        && !trace.Contains("directDrive=", StringComparison.Ordinal)
        && !trace.Contains("localMotion=", StringComparison.Ordinal)
        && !trace.Contains("routeFallback", StringComparison.Ordinal)
        && !trace.Contains("alphaPathless", StringComparison.Ordinal)
        && !trace.Contains("combat:1", StringComparison.Ordinal)
        && !trace.Contains("medicSupport:", StringComparison.Ordinal)
        && !trace.Contains("engineerIntelDefense=", StringComparison.Ordinal);
}

static string FormatTraversalSoakBotTick(TraversalSoakBotStats stats)
{
    var trace = stats.LastTraversalTrace;
    if (trace.Length > 240)
    {
        trace = trace[..240];
    }

    return string.Create(
        CultureInfo.InvariantCulture,
        $"soakBotTick=slot:{stats.Slot} {stats.Team} {stats.ClassId} pos=({stats.LastX:0.0},{stats.LastY:0.0}) " +
        $"speed=({stats.LastHorizontalSpeed:0.0},{stats.LastVerticalSpeed:0.0}) caps:{stats.LastCaps} " +
        $"carrying:{(stats.LastCarryingIntel ? 1 : 0)} inert:{stats.ConsecutiveInertTicks}/{stats.MaxConsecutiveInertTicks} " +
        $"objectiveHoldTicks:{stats.IntentionalObjectiveHoldTicks} " +
        $"windowMove:{stats.RecentWindowMovement:0.0} flips:{stats.LowSpeedMoveFlips} " +
        $"trace:{trace}");
}

static string FormatTraversalSoakBotSummary(TraversalSoakBotStats stats)
{
    var issue = stats.FirstInertFailTick >= 0
        ? stats.FirstInertFailTrace
        : stats.FirstOscillationTick >= 0
            ? stats.FirstOscillationTrace
            : stats.LastTraversalTrace;
    if (issue.Length > 180)
    {
        issue = issue[..180];
    }

    return string.Create(
        CultureInfo.InvariantCulture,
        $"soakBotSummary=slot:{stats.Slot} team:{stats.Team} class:{stats.ClassId} caps:{stats.LastCaps} " +
        $"carrying:{(stats.LastCarryingIntel ? 1 : 0)} carryTick:{stats.CarryingIntelTick} capTick:{stats.FirstCapTick} " +
        $"returnTicks:{stats.CarrierConversionTicks} carryLosses:{stats.CarryLossCount} firstCarryLossTick:{stats.FirstCarryLossTick} " +
        $"deaths:{stats.DeathCount} firstDeathTick:{stats.FirstDeathTick} movement:{stats.TotalMovement:0.0} " +
        $"stagnantWindows:{stats.StagnantWindows} maxInertTicks:{stats.MaxConsecutiveInertTicks} firstInertTick:{stats.FirstInertFailTick} " +
        $"objectiveHoldTicks:{stats.IntentionalObjectiveHoldTicks} " +
        $"oscillationEvents:{stats.OscillationEvents} firstOscillationTick:{stats.FirstOscillationTick} " +
        $"zeroInput:{stats.ZeroInputTicks} flips:{stats.MoveFlips} lowSpeedFlips:{stats.LowSpeedMoveFlips} " +
        $"routeLowSpeedFlips:{stats.RouteLowSpeedMoveFlips} " +
        $"avgThinkMs:{(stats.ThinkTicks > 0 ? StopwatchTicksToMilliseconds(stats.ThinkStopwatchTicks) / stats.ThinkTicks : 0d):0.0000} " +
        $"maxThinkMs:{StopwatchTicksToMilliseconds(stats.MaxThinkStopwatchTicks):0.000} final=({stats.LastX:0.0},{stats.LastY:0.0}) issue:{issue}");
}

static string FormatTraversalSoakResultReason(
    bool ctfCapsPassed,
    bool inertPassed,
    bool oscillationPassed)
{
    var reasons = new List<string>(3);
    if (!ctfCapsPassed)
    {
        reasons.Add("caps");
    }

    if (!inertPassed)
    {
        reasons.Add("inert");
    }

    if (!oscillationPassed)
    {
        reasons.Add("oscillation");
    }

    return reasons.Count == 0 ? "ok" : string.Join(',', reasons);
}

static IReadOnlyList<string> GetTraversalSoakMaps(
    IReadOnlyDictionary<string, string> rawOptions,
    string fallbackMap)
{
    if (!rawOptions.TryGetValue("maps", out var mapsText) || string.IsNullOrWhiteSpace(mapsText))
    {
        return [fallbackMap];
    }

    return mapsText
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static bool GetRosterBool(IReadOnlyDictionary<string, string> options, string key, bool fallback)
    => options.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
        ? parsed
        : fallback;

static float GetRosterFloat(IReadOnlyDictionary<string, string> options, string key, float fallback)
    => options.TryGetValue(key, out var value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
        ? parsed
        : fallback;

static double StopwatchTicksToMilliseconds(long stopwatchTicks)
    => stopwatchTicks * 1000d / Stopwatch.Frequency;

static void RunPracticeRosterSimulation(
    SimpleLevel level,
    NavGraph graph,
    BotBrainCanaryOptions options,
    IReadOnlyDictionary<string, string> rawOptions)
{
    // This diagnostic is intended to exercise the production alpha roster.
    // Passing the deprecated asset graph as a controller override silently
    // selects legacy proof/tape navigation, so obtain the same immutable OG2
    // graph that the live client resolves and force alpha mode explicitly.
    graph = Og2NavigationGraphStore.GetOrBuild(level);

    var ticks = GetRosterInt(rawOptions, "ticks", options.Ticks);
    var reportEvery = GetRosterInt(rawOptions, "report-every", Math.Max(30, options.ReportEveryTicks));
    var friendlyCount = GetRosterInt(rawOptions, "friendly-bots", 3);
    var enemyCount = GetRosterInt(rawOptions, "enemy-bots", 3);
    var forceObjectiveNavigation = GetRosterBool(rawOptions, "force-objective-navigation", false);
    var localTeam = options.Team;
    var enemyTeam = localTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
    PlayerClass[] classCycle =
    [
        PlayerClass.Scout,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Heavy,
        PlayerClass.Demoman,
        PlayerClass.Medic,
        PlayerClass.Engineer,
        PlayerClass.Spy,
        PlayerClass.Sniper,
        PlayerClass.Quote,
    ];

    var world = new SimulationWorld();
    if (!world.TryLoadLevel(options.MapName, options.AreaIndex, preservePlayerStats: false))
    {
        throw new InvalidOperationException($"SimulationWorld failed to load '{options.MapName}' area {options.AreaIndex}.");
    }

    world.DespawnEnemyDummy();
    world.DespawnFriendlyDummy();
    world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, localTeam);
    world.LocalPlayer.Kill();

    var controllers = new Dictionary<byte, BotBrainController>();
    var stats = new Dictionary<byte, PracticeRosterBotStats>();
    var nextSlot = (byte)(SimulationWorld.LocalPlayerSlot + 1);
    AppendPracticeRosterBots(world, graph, controllers, stats, ref nextSlot, localTeam, friendlyCount, classCycle, classOffset: 0, forceObjectiveNavigation);
    AppendPracticeRosterBots(world, graph, controllers, stats, ref nextSlot, enemyTeam, enemyCount, classCycle, classOffset: 3, forceObjectiveNavigation);

    Console.WriteLine(
        $"practiceRoster=map:{level.Name} area:{level.MapAreaIndex} mode:{world.MatchRules.Mode} " +
        $"localTeam:{localTeam} friendlyBots:{friendlyCount} enemyBots:{enemyCount} ticks:{ticks}");
    foreach (var entry in stats.Values.OrderBy(static s => s.Slot))
    {
        Console.WriteLine($"practiceBot=slot:{entry.Slot} team:{entry.Team} class:{entry.ClassId} start=({entry.StartX:0.0},{entry.StartY:0.0})");
    }

    var initialRedCaps = world.RedCaps;
    var initialBlueCaps = world.BlueCaps;
    var redDropStartedTick = -1;
    var blueDropStartedTick = -1;
    var redDroppedPickupLatencies = new List<int>();
    var blueDroppedPickupLatencies = new List<int>();
    for (var tick = 1; tick <= ticks; tick += 1)
    {
        var previousRedIntel = world.RedIntel;
        var previousBlueIntel = world.BlueIntel;
        var inputs = new Dictionary<byte, PlayerInputSnapshot>(controllers.Count);
        foreach (var (slot, controller) in controllers)
        {
            if (!world.TryGetNetworkPlayer(slot, out var bot) || !bot.IsAlive)
            {
                continue;
            }

            var input = controller.Think(bot, world, bot.Team);
            inputs[slot] = input;
            ObservePracticeRosterBotPreAdvance(stats[slot], tick, bot, input, controller);
        }

        foreach (var (slot, input) in inputs)
        {
            world.TrySetNetworkPlayerInput(slot, input);
        }

        world.AdvanceOneTick();
        ObservePracticeRosterIntelTransition(previousRedIntel, world.RedIntel, tick, ref redDropStartedTick, redDroppedPickupLatencies);
        ObservePracticeRosterIntelTransition(previousBlueIntel, world.BlueIntel, tick, ref blueDropStartedTick, blueDroppedPickupLatencies);

        foreach (var (slot, controller) in controllers)
        {
            if (!world.TryGetNetworkPlayer(slot, out var bot))
            {
                continue;
            }

            ObservePracticeRosterBotPostAdvance(stats[slot], tick, bot, controller);
        }

        if (reportEvery > 0 && tick % reportEvery == 0)
        {
            var stagnant = stats.Values.Count(static s => s.StagnantWindows > 0);
            var caps = $"redCaps:{world.RedCaps - initialRedCaps} blueCaps:{world.BlueCaps - initialBlueCaps}";
            Console.WriteLine(
                $"practiceRosterTick={tick} {caps} stagnantBots:{stagnant} " +
                $"{FormatPracticeRosterIntelState("redIntel", world.RedIntel)} {FormatPracticeRosterIntelState("blueIntel", world.BlueIntel)} " +
                $"droppedPickups:red:{redDroppedPickupLatencies.Count} blue:{blueDroppedPickupLatencies.Count}");
            foreach (var entry in stats.Values.OrderByDescending(static s => s.RecentStagnant).ThenBy(static s => s.Slot).Take(8))
            {
                Console.WriteLine(FormatPracticeRosterBotTick(entry));
            }
        }
    }

    Console.WriteLine($"practiceRosterResult=redCaps:{world.RedCaps - initialRedCaps} blueCaps:{world.BlueCaps - initialBlueCaps}");
    Console.WriteLine(FormatPracticeRosterAcceptance(stats.Values, redDroppedPickupLatencies, blueDroppedPickupLatencies));
    foreach (var entry in stats.Values.OrderBy(static s => s.Slot))
    {
        Console.WriteLine(FormatPracticeRosterBotSummary(entry));
    }
}

static void AppendPracticeRosterBots(
    SimulationWorld world,
    NavGraph graph,
    Dictionary<byte, BotBrainController> controllers,
    Dictionary<byte, PracticeRosterBotStats> stats,
    ref byte nextSlot,
    PlayerTeam team,
    int count,
    IReadOnlyList<PlayerClass> classCycle,
    int classOffset,
    bool forceObjectiveNavigation)
{
    for (var index = 0; index < count && nextSlot <= SimulationWorld.MaxPlayableNetworkPlayers; index += 1)
    {
        var classId = classCycle[(index + classOffset) % classCycle.Count];
        var slot = nextSlot;
        nextSlot += 1;
        world.TryPrepareNetworkPlayerJoin(slot);
        world.TrySetNetworkPlayerTeam(slot, team);
        if (!world.TryApplyNetworkPlayerClassSelection(slot, classId)
            || !world.TryGetNetworkPlayer(slot, out var bot))
        {
            continue;
        }

        controllers[slot] = new BotBrainController(graph)
        {
            ForceObjectiveNavigationForDiagnostics = forceObjectiveNavigation,
        };
        stats[slot] = new PracticeRosterBotStats(slot, team, classId, bot.X, bot.Y, bot.Bottom);
    }
}

static void ObservePracticeRosterBotPreAdvance(
    PracticeRosterBotStats stats,
    int tick,
    PlayerEntity bot,
    PlayerInputSnapshot input,
    BotBrainController controller)
{
    stats.LastPreX = bot.X;
    stats.LastPreY = bot.Y;
    stats.LastPreBottom = bot.Bottom;
    stats.LastInput = input;
    stats.LastDirectTrace = controller.LastDirectDriveTrace;
    stats.LastPathCount = controller.CurrentPathCount;
    stats.LastPathIndex = controller.CurrentPathIndex;
    stats.LastRouteOwned = controller.CurrentPathCount > 0
        && string.IsNullOrWhiteSpace(controller.LastDirectDriveTrace);
    stats.CurrentRoutePathIndex = controller.CurrentPathIndex;
    stats.CurrentRouteEdgeKind = controller.LastSteeringOutput.EdgeKind;

    if (!string.IsNullOrWhiteSpace(controller.LastDirectDriveTrace))
    {
        ObservePracticeRosterDirectTrace(stats, controller.LastDirectDriveTrace);
        stats.DirectTicks += 1;
    }

    else if (controller.CurrentPathCount > 0)
    {
        stats.GraphTicks += 1;
    }

    if (!input.Left && !input.Right && !input.Up && !input.Down && !input.FirePrimary && !input.FireSecondary)
    {
        stats.ZeroInputTicks += 1;
    }

    var moveSign = input.Right == input.Left ? 0 : input.Right ? 1 : -1;
    if (moveSign != 0 && stats.LastMoveSign != 0 && moveSign != stats.LastMoveSign)
    {
        stats.MoveFlips += 1;
        if (MathF.Abs(bot.HorizontalSpeed) < 40f)
        {
            stats.LowSpeedMoveFlips += 1;
        }
    }

    if (moveSign != 0)
    {
        stats.LastMoveSign = moveSign;
        if (stats.LastRouteOwned)
        {
            if (stats.LastRouteMoveSign != 0 && moveSign != stats.LastRouteMoveSign)
            {
                stats.RouteMoveFlips += 1;
                if (stats.LastRoutePathIndex == stats.CurrentRoutePathIndex
                    && stats.LastRouteEdgeKind == stats.CurrentRouteEdgeKind)
                {
                    stats.RouteSameEdgeFlips += 1;
                    if (stats.CurrentRouteEdgeKind == NavEdgeKind.Walk)
                    {
                        stats.RouteSameEdgeWalkFlips += 1;
                    }
                }

                if (MathF.Abs(bot.HorizontalSpeed) < 40f)
                {
                    stats.RouteLowSpeedMoveFlips += 1;
                }
            }

            stats.LastRouteMoveSign = moveSign;
        }
        else
        {
            stats.LastRouteMoveSign = 0;
        }
    }

    if (stats.LastRouteOwned)
    {
        stats.LastRoutePathIndex = stats.CurrentRoutePathIndex;
        stats.LastRouteEdgeKind = stats.CurrentRouteEdgeKind;
    }
    else
    {
        stats.LastRoutePathIndex = -1;
    }

    if (bot.IsCarryingIntel && stats.CarryingIntelTick < 0)
    {
        stats.CarryingIntelTick = tick;
    }
}

static void ObservePracticeRosterBotPostAdvance(
    PracticeRosterBotStats stats,
    int tick,
    PlayerEntity bot,
    BotBrainController controller)
{
    var wasCarryingIntel = stats.LastCarryingIntel;
    var wasAlive = stats.LastAlive;
    var hadPreviousPostSample = stats.HasPostAdvanceSample;
    var capIncreased = bot.Caps > stats.LastCapsSeen;

    stats.LastX = bot.X;
    stats.LastY = bot.Y;
    stats.LastBottom = bot.Bottom;
    stats.LastGrounded = bot.IsGrounded;
    stats.LastHorizontalSpeed = bot.HorizontalSpeed;
    stats.LastVerticalSpeed = bot.VerticalSpeed;
    stats.LastCaps = bot.Caps;
    stats.LastCarryingIntel = bot.IsCarryingIntel;
    stats.HasPostAdvanceSample = true;

    if (hadPreviousPostSample && wasAlive && !bot.IsAlive)
    {
        stats.DeathCount += 1;
        if (stats.FirstDeathTick < 0)
        {
            stats.FirstDeathTick = tick;
        }
    }

    if (wasCarryingIntel && !bot.IsCarryingIntel && !capIncreased)
    {
        stats.CarryLossCount += 1;
        if (stats.FirstCarryLossTick < 0)
        {
            stats.FirstCarryLossTick = tick;
            stats.FirstCarryLossX = bot.X;
            stats.FirstCarryLossY = bot.Y;
        }
    }

    stats.LastAlive = bot.IsAlive;
    if (capIncreased)
    {
        if (stats.FirstCapTick < 0)
        {
            stats.FirstCapTick = tick;
        }

        if (stats.CarryingIntelTick >= 0 && stats.CarrierConversionTicks < 0)
        {
            stats.CarrierConversionTicks = tick - stats.CarryingIntelTick;
        }

        stats.LastCapsSeen = bot.Caps;
    }

    stats.TotalMovement += Distance(stats.PreviousX, stats.PreviousY, bot.X, bot.Y);
    stats.PreviousX = bot.X;
    stats.PreviousY = bot.Y;

    if (tick % 30 == 0)
    {
        var windowMovement = Distance(stats.WindowX, stats.WindowY, bot.X, bot.Y);
        stats.RecentWindowMovement = windowMovement;
        stats.RecentStagnant = windowMovement < 12f;
        if (stats.RecentStagnant)
        {
            stats.StagnantWindows += 1;
        }

        stats.WindowX = bot.X;
        stats.WindowY = bot.Y;
    }

}

static string FormatPracticeRosterBotTick(PracticeRosterBotStats stats)
{
    var authority = stats.LastDirectTrace.Length > 0 ? "direct" : stats.LastPathCount > 0 ? "graph" : "none";
    var trace = stats.LastDirectTrace;
    if (trace.Length > 220)
    {
        trace = trace[..220];
    }

    return string.Create(
        CultureInfo.InvariantCulture,
        $"practiceBotTick=slot:{stats.Slot} {stats.Team} {stats.ClassId} pos=({stats.LastX:0.0},{stats.LastY:0.0}) " +
        $"speed=({stats.LastHorizontalSpeed:0.0},{stats.LastVerticalSpeed:0.0}) caps:{stats.LastCaps} " +
        $"input=L{Bit(stats.LastInput.Left)}R{Bit(stats.LastInput.Right)}U{Bit(stats.LastInput.Up)}D{Bit(stats.LastInput.Down)}F{Bit(stats.LastInput.FirePrimary)} " +
        $"steer=({stats.LastSteering.MoveDirection:0.0},{stats.LastSteering.MoveDirectionY:0.0}) repath:{(stats.LastSteering.RequestRepath ? 1 : 0)} " +
        $"carrying:{(stats.LastCarryingIntel ? 1 : 0)} " +
        $"stagnant:{(stats.RecentStagnant ? 1 : 0)} windowMove:{stats.RecentWindowMovement:0.0} " +
        $"auth:{authority} path:{stats.LastPathIndex}/{stats.LastPathCount} trace:{trace}");
}

static string FormatPracticeRosterBotSummary(PracticeRosterBotStats stats)
{
    var issue = string.IsNullOrWhiteSpace(stats.LastIssue) ? "none" : stats.LastIssue;
    if (issue.Length > 120)
    {
        issue = issue[..120];
    }

    return string.Create(
        CultureInfo.InvariantCulture,
        $"practiceBotSummary=slot:{stats.Slot} team:{stats.Team} class:{stats.ClassId} caps:{stats.LastCaps} " +
        $"carrying:{(stats.LastCarryingIntel ? 1 : 0)} carryTick:{stats.CarryingIntelTick} capTick:{stats.FirstCapTick} returnTicks:{stats.CarrierConversionTicks} " +
        $"carryLossTick:{stats.FirstCarryLossTick} carryLosses:{stats.CarryLossCount} carryLossPos=({stats.FirstCarryLossX:0.0},{stats.FirstCarryLossY:0.0}) " +
        $"deaths:{stats.DeathCount} firstDeathTick:{stats.FirstDeathTick} " +
        $"movement:{stats.TotalMovement:0.0} stagnantWindows:{stats.StagnantWindows} " +
        $"zeroInput:{stats.ZeroInputTicks} flips:{stats.MoveFlips} lowSpeedFlips:{stats.LowSpeedMoveFlips} " +
        $"routeFlips:{stats.RouteMoveFlips} routeLowSpeedFlips:{stats.RouteLowSpeedMoveFlips} " +
        $"routeSameEdgeFlips:{stats.RouteSameEdgeFlips} routeSameEdgeWalkFlips:{stats.RouteSameEdgeWalkFlips} " +
        $"direct:{stats.DirectTicks} directRejects:{SumValues(stats.DirectRejectByReason)} localMotionFailures:{SumValues(stats.LocalMotionFailureByReason)} graph:{stats.GraphTicks} " +
        $"final=({stats.LastX:0.0},{stats.LastY:0.0}) issue:{issue}");
}

static void ObservePracticeRosterIntelTransition(
    TeamIntelligenceState previous,
    TeamIntelligenceState current,
    int tick,
    ref int droppedStartedTick,
    List<int> pickupLatencies)
{
    if (!previous.IsDropped && current.IsDropped)
    {
        droppedStartedTick = tick;
        return;
    }

    if (previous.IsDropped && current.IsCarried && droppedStartedTick >= 0)
    {
        pickupLatencies.Add(tick - droppedStartedTick);
        droppedStartedTick = -1;
        return;
    }

    if (current.IsAtBase)
    {
        droppedStartedTick = -1;
    }
}

static string FormatPracticeRosterAcceptance(
    IEnumerable<PracticeRosterBotStats> stats,
    IReadOnlyList<int> redDroppedPickupLatencies,
    IReadOnlyList<int> blueDroppedPickupLatencies)
{
    var entries = stats.ToArray();
    var classesPicked = entries.Where(static s => s.CarryingIntelTick >= 0).Select(static s => s.ClassId).Distinct().Count();
    var classesCapped = entries.Where(static s => s.LastCaps > 0).Select(static s => s.ClassId).Distinct().Count();
    var conversions = entries.Where(static s => s.CarrierConversionTicks >= 0).Select(static s => s.CarrierConversionTicks).Order().ToArray();
    var droppedLatencies = redDroppedPickupLatencies.Concat(blueDroppedPickupLatencies).Order().ToArray();
    var directRejects = entries.Sum(static s => SumValues(s.DirectRejectByReason));
    var localMotionFailures = entries.Sum(static s => SumValues(s.LocalMotionFailureByReason));
    var carryLosses = entries.Sum(static s => s.CarryLossCount);
    var deaths = entries.Sum(static s => s.DeathCount);
    return string.Create(
        CultureInfo.InvariantCulture,
        $"practiceRosterAcceptance=classesPicked:{classesPicked} classesCapped:{classesCapped} " +
        $"carrierConversions:{conversions.Length} medianReturnTicks:{MedianOrMinusOne(conversions)} p90ReturnTicks:{PercentileOrMinusOne(conversions, 0.9f)} " +
        $"droppedPickupCount:{droppedLatencies.Length} medianDroppedPickupTicks:{MedianOrMinusOne(droppedLatencies)} " +
        $"carryLosses:{carryLosses} deaths:{deaths} " +
        $"directRejects:{directRejects} localMotionFailures:{localMotionFailures}");
}

static void ObservePracticeRosterDirectTrace(PracticeRosterBotStats stats, string trace)
{
    var key = NormalizePracticeRosterTraceKey(trace);
    if (key == stats.LastDirectTraceKey)
    {
        return;
    }

    stats.LastDirectTraceKey = key;
    if (trace.Contains("reject:", StringComparison.Ordinal))
    {
        IncrementCounter(stats.DirectRejectByReason, ExtractPracticeRosterReason(trace));
    }

    if (trace.StartsWith("localMotion=failed", StringComparison.Ordinal)
        || trace.StartsWith("localMotion=suppressed", StringComparison.Ordinal))
    {
        IncrementCounter(stats.LocalMotionFailureByReason, ExtractPracticeRosterReason(trace));
    }
}

static string NormalizePracticeRosterTraceKey(string trace)
{
    if (string.IsNullOrWhiteSpace(trace))
    {
        return string.Empty;
    }

    var actionTickIndex = trace.IndexOf(" actionTick:", StringComparison.Ordinal);
    if (actionTickIndex >= 0)
    {
        trace = trace[..actionTickIndex];
    }

    var routeTicksIndex = trace.IndexOf(" routeTicks:", StringComparison.Ordinal);
    if (routeTicksIndex >= 0)
    {
        trace = trace[..routeTicksIndex];
    }

    var ageIndex = trace.IndexOf(" age:", StringComparison.Ordinal);
    if (ageIndex >= 0)
    {
        trace = trace[..ageIndex];
    }

    return trace.Length <= 96 ? trace : trace[..96];
}

static string ExtractPracticeRosterReason(string trace)
{
    var reasonIndex = trace.IndexOf("reason:", StringComparison.Ordinal);
    if (reasonIndex >= 0)
    {
        var reason = trace[(reasonIndex + "reason:".Length)..];
        var spaceIndex = reason.IndexOf(' ');
        return spaceIndex >= 0 ? reason[..spaceIndex] : reason;
    }

    var rejectIndex = trace.IndexOf("reject:", StringComparison.Ordinal);
    if (rejectIndex >= 0)
    {
        var reason = trace[(rejectIndex + "reject:".Length)..];
        var spaceIndex = reason.IndexOf(' ');
        return $"reject:{(spaceIndex >= 0 ? reason[..spaceIndex] : reason)}";
    }

    var labelIndex = trace.IndexOf("label:", StringComparison.Ordinal);
    if (labelIndex >= 0)
    {
        var reason = trace[..labelIndex].Trim();
        return reason.Length == 0 ? "unknown" : reason;
    }

    var firstSpace = trace.IndexOf(' ');
    return firstSpace >= 0 ? trace[..firstSpace] : trace;
}

static void IncrementCounter(Dictionary<string, int> counters, string key)
{
    counters[key] = counters.TryGetValue(key, out var count) ? count + 1 : 1;
}

static int SumValues(Dictionary<string, int> counters) => counters.Values.Sum();

static int MedianOrMinusOne(IReadOnlyList<int> values)
{
    return values.Count == 0 ? -1 : values[values.Count / 2];
}

static int PercentileOrMinusOne(IReadOnlyList<int> values, float percentile)
{
    if (values.Count == 0)
    {
        return -1;
    }

    var index = Math.Clamp((int)MathF.Ceiling((values.Count * percentile) - 1f), 0, values.Count - 1);
    return values[index];
}

static string FormatPracticeRosterIntelState(string label, TeamIntelligenceState intel)
{
    var state = intel.IsAtBase
        ? "base"
        : intel.IsDropped
            ? $"dropped:{intel.ReturnTicksRemaining}"
            : "carried";
    return string.Create(
        CultureInfo.InvariantCulture,
        $"{label}:{state}@({intel.X:0.0},{intel.Y:0.0})");
}

static int GetRosterInt(IReadOnlyDictionary<string, string> options, string key, int fallback)
    => options.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
        ? parsed
        : fallback;

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

static void RunNeutralPreTicks(SimulationWorld world, int ticks)
{
    if (ticks <= 0)
    {
        return;
    }

    world.SetLocalInput(default);
    world.SetEnemyInput(default);
    for (var tick = 0; tick < ticks; tick += 1)
    {
        world.AdvanceOneTick();
    }

    Console.WriteLine($"preTicks={ticks} controlPointSetupActive={world.ControlPointSetupActive} setupTicksRemaining={world.ControlPointSetupTicksRemaining}");
}

static int Bit(bool value) => value ? 1 : 0;

static string FormatIntelState(TeamIntelligenceState intel)
{
    var state = intel.IsAtBase
        ? "base"
        : intel.IsDropped
            ? "dropped"
            : "carried";
    return $"{state}@({intel.X:0.0},{intel.Y:0.0}) return:{intel.ReturnTicksRemaining}";
}

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
            var graphNode = graph.GetNode(current);
            minX = MathF.Min(minX, graphNode.X);
            maxX = MathF.Max(maxX, graphNode.X);
            minY = MathF.Min(minY, graphNode.Y);
            maxY = MathF.Max(maxY, graphNode.Y);
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
    var hasCaptureZone = sourceAsset.Anchors.Any(static anchor => anchor.Kind == "CaptureZone");
    var generatorAnchors = sourceAsset.Anchors
        .Where(static anchor => anchor.Kind == "Generator")
        .ToArray();
    if (!hasCaptureZone && generatorAnchors.Length == 0)
    {
        return sourceAsset.ValidationIssues;
    }

    bool IsGeneratorProximityIssue(BotNavigationValidationIssueAssetEntry issue)
    {
        return issue.Kind is "SpawnObjectiveNoPath" or "PortalSpawnObjectiveNoPath"
            && generatorAnchors.Any(anchor =>
                MathF.Abs(anchor.X - issue.X) <= 1f
                && MathF.Abs(anchor.Y - issue.Y) <= 1f);
    }

    var filtered = sourceAsset.ValidationIssues
        .Where(issue => !IsGeneratorProximityIssue(issue))
        .ToList();

    var controlObjectiveAnchors = sourceAsset.Anchors
        .Where(static anchor => anchor.Kind is "ControlPoint" or "CaptureZone")
        .ToArray();
    if (controlObjectiveAnchors.Length <= 1)
    {
        return filtered;
    }

    return filtered
        .Where(issue => !controlObjectiveAnchors.Any(anchor =>
            MathF.Abs(anchor.X - issue.X) <= 1f
            && MathF.Abs(anchor.Y - issue.Y) <= 1f))
        .ToList();
}

static string ResolveMovementAuthority(BotBrainController brain)
{

    if (!string.IsNullOrWhiteSpace(brain.LastDirectDriveTrace))
    {
        return "direct-drive";
    }

    if (brain.CurrentPathCount > 0)
    {
        return "graph";
    }

    if (!string.IsNullOrWhiteSpace(brain.LastSemanticRecoveryTrace))
    {
        return "semantic-recovery";
    }

    return "none";
}

static RouteEdgeArtifact[] BuildRouteEdgeArtifacts(NavGraph graph, NavPath path)
{
    var edges = new List<RouteEdgeArtifact>(Math.Max(0, path.Count - 1));
    for (var i = 1; i < path.Count; i += 1)
    {
        var fromNodeIndex = path.GetWaypoint(i - 1);
        var toNodeIndex = path.GetWaypoint(i);
        var from = graph.GetNode(fromNodeIndex);
        var to = graph.GetNode(toNodeIndex);
        var hasIncomingEdge = path.TryGetIncomingEdge(i, out var edge);
        var kind = hasIncomingEdge ? edge.Kind : NavEdgeKind.Walk;
        var cost = hasIncomingEdge ? edge.Cost : Distance(from.X, from.Y, to.X, to.Y);
        var dx = MathF.Abs(to.X - from.X);
        var dy = MathF.Abs(to.Y - from.Y);
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        var cheapVerticalRelay = dy >= 24f
            && cost < distance * 0.55f;
        edges.Add(new RouteEdgeArtifact(
            Index: i - 1,
            FromNode: fromNodeIndex,
            ToNode: toNodeIndex,
            Kind: kind.ToString(),
            Cost: cost,
            Distance: distance,
            HorizontalDelta: dx,
            VerticalDelta: dy,
            ProbeTicks: hasIncomingEdge ? edge.ProbeTicks : 0,
            ProbeMoveDirectionX: hasIncomingEdge ? edge.ProbeMoveDirectionX : 0f,
            ProbeVariantAttempts: hasIncomingEdge ? edge.ProbeVariantAttempts : 0,
            ProbeVariantSuccesses: hasIncomingEdge ? edge.ProbeVariantSuccesses : 0,
            HasCompletionWindow: hasIncomingEdge && edge.Completion.HasWindow,
            CompletionMinX: hasIncomingEdge ? edge.Completion.MinX : 0f,
            CompletionMaxX: hasIncomingEdge ? edge.Completion.MaxX : 0f,
            CompletionMinY: hasIncomingEdge ? edge.Completion.MinY : 0f,
            CompletionMaxY: hasIncomingEdge ? edge.Completion.MaxY : 0f,
            AcceptedCompletionSurfaces: hasIncomingEdge ? edge.Completion.AcceptedSurfaceIds.Length : 0,
            RequiresGroundedContinuation: hasIncomingEdge && edge.RequiresGroundedContinuation,
            RequiresCarryingIntel: hasIncomingEdge && edge.RequiresCarryingIntel,
            HasLaunchRecipe: hasIncomingEdge && edge.LaunchRecipe.HasRecipe,
            LaunchTick: hasIncomingEdge ? edge.LaunchRecipe.LaunchTick : -1,
            LaunchMinX: hasIncomingEdge ? edge.LaunchRecipe.LaunchMinX : 0f,
            LaunchMaxX: hasIncomingEdge ? edge.LaunchRecipe.LaunchMaxX : 0f,
            LaunchMinY: hasIncomingEdge ? edge.LaunchRecipe.LaunchMinY : 0f,
            LaunchMaxY: hasIncomingEdge ? edge.LaunchRecipe.LaunchMaxY : 0f,
            LaunchMinHorizontalSpeed: hasIncomingEdge ? edge.LaunchRecipe.LaunchMinHorizontalSpeed : 0f,
            LaunchMaxHorizontalSpeed: hasIncomingEdge ? edge.LaunchRecipe.LaunchMaxHorizontalSpeed : 0f,
            ExpectedLaunchMoveDirectionX: hasIncomingEdge ? edge.LaunchRecipe.ExpectedMoveDirectionX : 0f,
            SupportedClassMask: hasIncomingEdge ? edge.SupportedClassMask : 0,
            SupportedTeamMask: hasIncomingEdge ? edge.SupportedTeamMask : 0,
            CheapVerticalRelay: cheapVerticalRelay,
            VerticalWalk: kind == NavEdgeKind.Walk && dy > 24f,
            SuspiciousVerticalWalk: kind == NavEdgeKind.Walk && cheapVerticalRelay));
    }

    return edges.ToArray();
}

static GraphQualityArtifact AnalyzeGraphQuality(NavGraph graph)
{
    const float verticalRelayThreshold = 24f;
    const float suspiciousRelayHorizontalReach = 260f;
    const float suspiciousRelayCostFloorMultiplier = 0.55f;

    var edgeCount = 0;
    var suspiciousVerticalRelays = 0;
    var verticalWalkEdges = 0;
    var uncertifiedNonWalkEdges = 0;
    var missingCompletionNonWalkEdges = 0;
    var weakProbeEdges = 0;
    var zeroOrLowCostEdges = 0;
    var maxOutDegree = 0;
    var worstOutDegreeNode = -1;

    for (var fromNodeIndex = 0; fromNodeIndex < graph.NodeCount; fromNodeIndex += 1)
    {
        var from = graph.GetNode(fromNodeIndex);
        var edges = graph.GetEdges(fromNodeIndex);
        if (edges.Length > maxOutDegree)
        {
            maxOutDegree = edges.Length;
            worstOutDegreeNode = fromNodeIndex;
        }

        for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex += 1)
        {
            var edge = edges[edgeIndex];
            var to = graph.GetNode(edge.ToNode);
            var horizontalDelta = MathF.Abs(to.X - from.X);
            var verticalDelta = MathF.Abs(to.Y - from.Y);
            var distance = MathF.Sqrt((horizontalDelta * horizontalDelta) + (verticalDelta * verticalDelta));
            var isNonWalk = edge.Kind != NavEdgeKind.Walk;
            var hasCertifiedProof = edge.ProbeTicks > 0 || edge.ProbeVariantAttempts > 0;

            edgeCount += 1;
            if (edge.Cost <= 1f)
            {
                zeroOrLowCostEdges += 1;
            }

            if (edge.Kind == NavEdgeKind.Walk && verticalDelta > verticalRelayThreshold)
            {
                verticalWalkEdges += 1;
            }

            if (verticalDelta >= verticalRelayThreshold
                && horizontalDelta <= suspiciousRelayHorizontalReach
                && edge.Cost < distance * suspiciousRelayCostFloorMultiplier)
            {
                suspiciousVerticalRelays += 1;
            }

            if (isNonWalk && !hasCertifiedProof)
            {
                uncertifiedNonWalkEdges += 1;
            }

            if (isNonWalk && !edge.Completion.HasWindow)
            {
                missingCompletionNonWalkEdges += 1;
            }

            if (edge.ProbeVariantAttempts > 0 && edge.ProbeVariantSuccesses * 2 < edge.ProbeVariantAttempts)
            {
                weakProbeEdges += 1;
            }
        }
    }

    var poisonScore = suspiciousVerticalRelays * 4
        + verticalWalkEdges * 3
        + uncertifiedNonWalkEdges * 2
        + missingCompletionNonWalkEdges * 2
        + weakProbeEdges
        + zeroOrLowCostEdges;

    return new GraphQualityArtifact(
        EdgeCount: edgeCount,
        SuspiciousVerticalRelayEdges: suspiciousVerticalRelays,
        VerticalWalkEdges: verticalWalkEdges,
        UncertifiedNonWalkEdges: uncertifiedNonWalkEdges,
        MissingCompletionNonWalkEdges: missingCompletionNonWalkEdges,
        WeakProbeEdges: weakProbeEdges,
        ZeroOrLowCostEdges: zeroOrLowCostEdges,
        MaxOutDegree: maxOutDegree,
        WorstOutDegreeNode: worstOutDegreeNode,
        PoisonScore: poisonScore);
}

static RouteQualityArtifact AnalyzeRouteQuality(IReadOnlyList<RouteEdgeArtifact> edges, float resolvedPathCost)
{
    var rawCost = edges.Sum(static edge => edge.Cost);
    var runtimePenaltyCost = resolvedPathCost >= 0f
        ? MathF.Max(0f, resolvedPathCost - rawCost)
        : 0f;
    var repeatedEdges = edges
        .GroupBy(static edge => $"{edge.FromNode}->{edge.ToNode}/{edge.Kind}")
        .Where(static group => group.Count() > 1)
        .Sum(static group => group.Count() - 1);
    var routeNodes = edges.Count > 0
        ? edges.Select(static edge => edge.ToNode).Prepend(edges[0].FromNode).ToArray()
        : [];
    var repeatedNodes = routeNodes
        .GroupBy(static node => node)
        .Where(static group => group.Count() > 1)
        .Sum(static group => group.Count() - 1);

    return new RouteQualityArtifact(
        EdgeCount: edges.Count,
        RawCost: rawCost,
        ResolvedCost: resolvedPathCost,
        RuntimePenaltyCost: runtimePenaltyCost,
        CheapVerticalRelayEdges: edges.Count(static edge => edge.CheapVerticalRelay),
        VerticalWalkEdges: edges.Count(static edge => edge.VerticalWalk),
        VerticalNonWalkEdges: edges.Count(static edge => !edge.VerticalWalk && edge.VerticalDelta > 24f),
        SuspiciousVerticalWalkEdges: edges.Count(static edge => edge.SuspiciousVerticalWalk),
        RepeatedEdges: repeatedEdges,
        RepeatedNodes: repeatedNodes);
}

static object BuildChurnSummary(
    IReadOnlyList<EdgeExecutionArtifact> edges,
    IReadOnlyList<SemanticRecoveryArtifact> semanticRecoveryEvents,
    IReadOnlyList<string> semanticRecoveryTraces,
    int scoreTick)
{
    var scoredEdges = scoreTick >= 0
        ? edges.Where(edge => edge.StartTick <= scoreTick).ToArray()
        : edges.ToArray();
    var scoredSemanticRecoveries = scoreTick >= 0
        ? semanticRecoveryEvents.Where(recovery => recovery.Tick <= scoreTick).ToArray()
        : semanticRecoveryEvents.ToArray();
    var repeatedEdges = scoredEdges
        .GroupBy(static edge => $"{edge.FromNode}->{edge.ToNode}/{edge.Kind}")
        .Where(static group => group.Count() > 1)
        .ToArray();
    var repeatedEdgeVisits = repeatedEdges.Sum(static group => group.Count() - 1);
    var repeatedEdgeTicks = repeatedEdges.Sum(static group => group.Sum(static edge => edge.Ticks));
    var slowEdges = scoredEdges
        .OrderByDescending(static edge => edge.Ticks)
        .Take(8)
        .ToArray();
    var semanticRecoveryReasons = scoredSemanticRecoveries.Length > 0
        ? scoredSemanticRecoveries.Select(static recovery => recovery.Reason)
        : semanticRecoveryTraces.Select(ExtractSemanticRecoveryReason);
    var semanticRecoveriesByReason = semanticRecoveryReasons
        .Where(static reason => !string.IsNullOrWhiteSpace(reason))
        .GroupBy(static reason => reason)
        .Select(static group => new { reason = group.Key, count = group.Count() })
        .OrderByDescending(static item => item.count)
        .ThenBy(static item => item.reason)
        .ToArray();

    return new
    {
        scoreTick,
        scoredEdgeCount = scoredEdges.Length,
        maxEdgeTicks = scoredEdges.Length > 0 ? scoredEdges.Max(static edge => edge.Ticks) : 0,
        slowEdgeCount60 = scoredEdges.Count(static edge => edge.Ticks >= 60),
        repeatedEdgeVisits,
        repeatedEdgeTicks,
        semanticRecoveries = semanticRecoveryEvents.Count > 0 ? scoredSemanticRecoveries.Length : semanticRecoveryTraces.Count,
        semanticRecoveriesTotal = semanticRecoveryEvents.Count > 0 ? semanticRecoveryEvents.Count : semanticRecoveryTraces.Count,
        failedJumpRecoveries = semanticRecoveryEvents.Count > 0
            ? scoredSemanticRecoveries.Count(static recovery => recovery.Reason is "landed_below_completion" or "missed_completion")
            : semanticRecoveryTraces.Count(static trace =>
                trace.Contains("landed_below_completion", StringComparison.Ordinal)
                || trace.Contains("missed_completion", StringComparison.Ordinal)),
        walkTimeoutRecoveries = semanticRecoveryEvents.Count > 0
            ? scoredSemanticRecoveries.Count(static recovery => recovery.Reason is "walk_timeout" or "walk_airborne_timeout")
            : semanticRecoveryTraces.Count(static trace =>
                trace.Contains("walk_timeout", StringComparison.Ordinal)
                || trace.Contains("walk_airborne_timeout", StringComparison.Ordinal)),
        semanticRecoveriesByReason,
        slowEdges,
    };
}

static string ExtractSemanticRecoveryReason(string trace)
{
    const string prefix = "semanticRecovery=continuation reason:";
    if (!trace.StartsWith(prefix, StringComparison.Ordinal))
    {
        return string.Empty;
    }

    var end = trace.IndexOf(' ', prefix.Length);
    return end > prefix.Length
        ? trace[prefix.Length..end]
        : trace[prefix.Length..];
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

internal sealed record GraphQualityArtifact(
    int EdgeCount,
    int SuspiciousVerticalRelayEdges,
    int VerticalWalkEdges,
    int UncertifiedNonWalkEdges,
    int MissingCompletionNonWalkEdges,
    int WeakProbeEdges,
    int ZeroOrLowCostEdges,
    int MaxOutDegree,
    int WorstOutDegreeNode,
    int PoisonScore);

internal sealed record RouteQualityArtifact(
    int EdgeCount,
    float RawCost,
    float ResolvedCost,
    float RuntimePenaltyCost,
    int CheapVerticalRelayEdges,
    int VerticalWalkEdges,
    int VerticalNonWalkEdges,
    int SuspiciousVerticalWalkEdges,
    int RepeatedEdges,
    int RepeatedNodes);

internal sealed record RouteEdgeArtifact(
    int Index,
    int FromNode,
    int ToNode,
    string Kind,
    float Cost,
    float Distance,
    float HorizontalDelta,
    float VerticalDelta,
    int ProbeTicks,
    float ProbeMoveDirectionX,
    int ProbeVariantAttempts,
    int ProbeVariantSuccesses,
    bool HasCompletionWindow,
    float CompletionMinX,
    float CompletionMaxX,
    float CompletionMinY,
    float CompletionMaxY,
    int AcceptedCompletionSurfaces,
    bool RequiresGroundedContinuation,
    bool RequiresCarryingIntel,
    bool HasLaunchRecipe,
    int LaunchTick,
    float LaunchMinX,
    float LaunchMaxX,
    float LaunchMinY,
    float LaunchMaxY,
    float LaunchMinHorizontalSpeed,
    float LaunchMaxHorizontalSpeed,
    float ExpectedLaunchMoveDirectionX,
    int SupportedClassMask,
    int SupportedTeamMask,
    bool CheapVerticalRelay,
    bool VerticalWalk,
    bool SuspiciousVerticalWalk);

internal sealed record AuthorityTransitionArtifact(
    int StartTick,
    int EndTick,
    int Ticks,
    string Authority,
    bool CarryingIntel,
    int PathIndex,
    int PathCount,
    int PathNode,
    string DirectDriveLabel,
    string SemanticRecoveryReason);

internal sealed record SemanticRecoveryArtifact(
    int Tick,
    string Reason,
    string Trace,
    bool CarryingIntel,
    int PathIndex,
    int PathCount,
    int PathNode,
    float X,
    float Y,
    bool Grounded);

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

internal sealed class AuthorityTransitionDiagnostics
{
    private string _currentKey = string.Empty;
    private int _startTick;
    private int _lastTick;
    private string _authority = "none";
    private bool _carryingIntel;
    private int _pathIndex = -1;
    private int _pathCount;
    private int _pathNode = -1;
    private string _directDriveLabel = string.Empty;
    private string _semanticRecoveryReason = string.Empty;

    public List<AuthorityTransitionArtifact> Transitions { get; } = [];

    public void Observe(
        int tick,
        string authority,
        bool carryingIntel,
        int pathIndex,
        int pathCount,
        int pathNode,
        string directDriveTrace,
        string semanticRecoveryTrace)
    {
        var directDriveLabel = ExtractDirectDriveLabel(directDriveTrace);
        var semanticRecoveryReason = ExtractSemanticRecoveryReason(semanticRecoveryTrace);
        var key = $"{authority}|{carryingIntel}|{directDriveLabel}|{semanticRecoveryReason}";
        if (!string.Equals(_currentKey, key, StringComparison.Ordinal))
        {
            FinalizeCurrent(tick - 1);
            _currentKey = key;
            _startTick = tick;
            _authority = authority;
            _carryingIntel = carryingIntel;
            _pathIndex = pathIndex;
            _pathCount = pathCount;
            _pathNode = pathNode;
            _directDriveLabel = directDriveLabel;
            _semanticRecoveryReason = semanticRecoveryReason;
        }

        _lastTick = tick;
    }

    public object BuildSummary(int scoreTick)
    {
        FinalizeCurrent(_lastTick);
        var scoredTransitions = scoreTick >= 0
            ? Transitions.Where(transition => transition.StartTick <= scoreTick).ToArray()
            : Transitions.ToArray();
        var authorityTicks = scoredTransitions
            .GroupBy(static transition => transition.Authority)
            .Select(static group => new
            {
                authority = group.Key,
                ticks = group.Sum(static transition => transition.Ticks),
                transitions = group.Count(),
            })
            .OrderByDescending(static item => item.ticks)
            .ThenBy(static item => item.authority)
            .ToArray();
        return new
        {
            scoreTick,
            transitionCount = scoredTransitions.Length,
            authorityTicks,
            first = scoredTransitions.FirstOrDefault(),
            last = scoredTransitions.LastOrDefault(),
        };
    }

    private void FinalizeCurrent(int endTick)
    {
        if (string.IsNullOrEmpty(_currentKey) || _startTick <= 0 || endTick < _startTick)
        {
            return;
        }

        var previous = Transitions.LastOrDefault();
        if (previous is not null
            && previous.StartTick == _startTick
            && previous.Authority == _authority
            && previous.CarryingIntel == _carryingIntel)
        {
            return;
        }

        Transitions.Add(new AuthorityTransitionArtifact(
            StartTick: _startTick,
            EndTick: endTick,
            Ticks: endTick - _startTick + 1,
            Authority: _authority,
            CarryingIntel: _carryingIntel,
            PathIndex: _pathIndex,
            PathCount: _pathCount,
            PathNode: _pathNode,
            DirectDriveLabel: _directDriveLabel,
            SemanticRecoveryReason: _semanticRecoveryReason));
    }

    private static string ExtractDirectDriveLabel(string trace)
    {
        const string prefix = "directDrive=";
        if (!trace.StartsWith(prefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var end = trace.IndexOf(" dx:", StringComparison.Ordinal);
        return end > prefix.Length
            ? trace[prefix.Length..end]
            : trace[prefix.Length..];
    }

    private static string ExtractSemanticRecoveryReason(string trace)
    {
        const string prefix = "semanticRecovery=continuation reason:";
        if (!trace.StartsWith(prefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var end = trace.IndexOf(' ', prefix.Length);
        return end > prefix.Length
            ? trace[prefix.Length..end]
            : trace[prefix.Length..];
    }
}

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

            var rawKey = args[i][2..];
            var equalsIndex = rawKey.IndexOf('=');
            if (equalsIndex >= 0)
            {
                var optionKey = rawKey[..equalsIndex];
                var value = rawKey[(equalsIndex + 1)..];
                options[optionKey] = value;
                continue;
            }

            var key = rawKey;
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


    private static int ResolveVerifiedNavExploreTargetSurface(
        VerifiedNavCandidateGraph graph,
        Dictionary<string, string> rawOptions)
    {
        var target = rawOptions.TryGetValue("explore-target", out var targetText)
            ? targetText.Trim()
            : "enemy";
        if (int.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetSurfaceId))
        {
            return targetSurfaceId >= 0 && targetSurfaceId < graph.Surfaces.Count ? targetSurfaceId : -1;
        }

        var targetKind = target.Equals("own", StringComparison.OrdinalIgnoreCase)
            ? VerifiedNavPortalKind.OwnIntel
            : target.Equals("spawn", StringComparison.OrdinalIgnoreCase)
                ? VerifiedNavPortalKind.Spawn
                : VerifiedNavPortalKind.EnemyIntel;
        return graph.Portals
            .Where(portal => portal.Kind == targetKind && portal.SurfaceId.HasValue)
            .Select(portal => portal.SurfaceId!.Value)
            .FirstOrDefault(-1);
    }

    public static void RunVerifiedNavReport(
        Dictionary<string, string> rawOptions,
        JsonSerializerOptions outputJsonOptions)
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(repoRoot, "Core", "Content"));

        var mapName = rawOptions.TryGetValue("map", out var mapText) ? mapText : "Truefort";
        var area = ReadIntOption(rawOptions, "area", 1);
        var team = ReadEnumOption(rawOptions, "team", PlayerTeam.Red);
        var classId = ReadEnumOption(rawOptions, "class", PlayerClass.Pyro);
        var artifactDirectory = rawOptions.TryGetValue("artifacts-dir", out var artifactsText) && !string.IsNullOrWhiteSpace(artifactsText)
            ? Path.GetFullPath(artifactsText)
            : Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "verified-nav", $"{mapName}-{area}-{team}-{classId}");
        Directory.CreateDirectory(artifactDirectory);

        var level = SimpleLevelFactory.CreateImportedLevel(mapName, area)
            ?? throw new InvalidOperationException($"Could not load map '{mapName}' area {area}.");
        var graph = VerifiedNavCandidateBuilder.Build(level, new VerifiedNavBuildOptions
        {
            Team = team,
            ClassId = classId,
            SampleStep = ReadFloatOption(rawOptions, "surface-sample-step", 8f),
            MinSurfaceWidth = ReadFloatOption(rawOptions, "min-surface-width", 24f),
            SameSurfaceMaxEdgeLength = ReadFloatOption(rawOptions, "same-surface-edge-length", 240f),
            DropHorizontalReach = ReadFloatOption(rawOptions, "drop-horizontal-reach", 144f),
            DropVerticalLimit = ReadFloatOption(rawOptions, "drop-vertical-limit", 420f),
            JumpHorizontalReach = ReadFloatOption(rawOptions, "jump-horizontal-reach", 184f),
            JumpVerticalLimit = ReadFloatOption(rawOptions, "jump-vertical-limit", 180f),
        });

        var summary = new
        {
            graph.LevelName,
            graph.MapAreaIndex,
            graph.Team,
            graph.ClassId,
            SurfaceCount = graph.Surfaces.Count,
            PortalCount = graph.Portals.Count,
            CandidateEdgeCount = graph.CandidateEdges.Count,
            WalkCandidateCount = graph.CandidateEdges.Count(static edge => edge.Intent == VerifiedNavEdgeIntent.Walk),
            DropCandidateCount = graph.CandidateEdges.Count(static edge => edge.Intent == VerifiedNavEdgeIntent.Drop),
            JumpCandidateCount = graph.CandidateEdges.Count(static edge => edge.Intent == VerifiedNavEdgeIntent.Jump),
            SolidSurfaceCount = graph.Surfaces.Count(static surface => surface.Kind == VerifiedNavSurfaceKind.SolidTop),
            DropdownSurfaceCount = graph.Surfaces.Count(static surface => surface.Kind == VerifiedNavSurfaceKind.DropdownPlatform),
        };

        File.WriteAllText(
            Path.Combine(artifactDirectory, "verified-nav-summary.json"),
            JsonSerializer.Serialize(summary, outputJsonOptions));
        File.WriteAllText(
            Path.Combine(artifactDirectory, "verified-nav-candidates.json"),
            JsonSerializer.Serialize(graph, outputJsonOptions));

        var shouldCertify = rawOptions.TryGetValue("verified-nav-certify", out var certifyText)
            && bool.TryParse(certifyText, out var parsedCertify)
            && parsedCertify;
        VerifiedNavCertificationReport? certification = null;
        if (shouldCertify)
        {
            certification = VerifiedNavCandidateCertifier.Certify(level, graph, new VerifiedNavCertificationOptions
            {
                MaxEdges = ReadIntOption(rawOptions, "certify-limit", 128),
                MaxTicks = ReadIntOption(rawOptions, "certify-max-ticks", 210),
                TraceEveryTicks = ReadIntOption(rawOptions, "certify-trace-every", 6),
                StartXOffsets = rawOptions.TryGetValue("certify-x-offsets", out var xOffsets)
                    ? ParseFloatList(xOffsets)
                    : [0f],
                StartBottomOffsets = rawOptions.TryGetValue("certify-bottom-offsets", out var bottomOffsets)
                    ? ParseFloatList(bottomOffsets)
                    : [0f],
                StartHorizontalSpeedOffsets = rawOptions.TryGetValue("certify-horizontal-speeds", out var horizontalSpeeds)
                    ? ParseFloatList(horizontalSpeeds)
                    : [0f],
                StartVerticalSpeedOffsets = rawOptions.TryGetValue("certify-vertical-speeds", out var verticalSpeeds)
                    ? ParseFloatList(verticalSpeeds)
                    : [0f],
            });
            File.WriteAllText(
                Path.Combine(artifactDirectory, "verified-nav-certification.json"),
                JsonSerializer.Serialize(certification, outputJsonOptions));
        }

        var shouldExplore = rawOptions.TryGetValue("verified-nav-explore", out var exploreText)
            && bool.TryParse(exploreText, out var parsedExplore)
            && parsedExplore;
        VerifiedNavExplorationReport? exploration = null;
        if (shouldExplore)
        {
            var startSurfaceIds = rawOptions.TryGetValue("explore-start-surfaces", out var startSurfaceText)
                ? ParseIntList(startSurfaceText)
                : [];
            var exploreAllLanes = ReadBoolOption(rawOptions, "verified-nav-explore-all-lanes", false);
            if (exploreAllLanes)
            {
                startSurfaceIds.AddRange(BuildLaneCoverageSeedSurfaceIds(graph));
            }

            var startSurfaceId = ReadIntOption(rawOptions, "explore-start-surface", -1);
            if (startSurfaceId >= 0)
            {
                startSurfaceIds.Add(startSurfaceId);
            }

            if (startSurfaceIds.Count == 0)
            {
                startSurfaceId = graph.Portals
                    .Where(static portal => portal.Kind == VerifiedNavPortalKind.Spawn && portal.SurfaceId.HasValue)
                    .Select(static portal => portal.SurfaceId!.Value)
                    .FirstOrDefault(-1);
                if (startSurfaceId >= 0)
                {
                    startSurfaceIds.Add(startSurfaceId);
                }
            }

            startSurfaceIds = startSurfaceIds
                .Where(surfaceId => surfaceId >= 0 && surfaceId < graph.Surfaces.Count)
                .Distinct()
                .ToList();
            if (startSurfaceIds.Count == 0)
            {
                throw new InvalidOperationException("Verified nav exploration requires a snapped spawn portal or --explore-start-surface.");
            }

            var targetSurfaceId = ResolveVerifiedNavExploreTargetSurface(graph, rawOptions);
            exploration = VerifiedNavSurfaceExplorer.ExploreMany(level, graph, startSurfaceIds, new VerifiedNavExplorationOptions
            {
                MaxSurfaceExpansions = ReadIntOption(rawOptions, "explore-max-surfaces", 2000),
                TargetSurfaceId = targetSurfaceId,
                MaxMacroTicks = ReadIntOption(rawOptions, "explore-max-macro-ticks", 120),
                SurfaceProbeInset = ReadFloatOption(rawOptions, "explore-surface-inset", 10f),
                Durations = rawOptions.TryGetValue("explore-durations", out var durationsText)
                    ? ParseIntList(durationsText)
                    : [8, 12, 18, 24, 32, 42, 56, 72, 96],
                JumpHoldTicks = rawOptions.TryGetValue("explore-jump-holds", out var jumpHoldsText)
                    ? ParseIntList(jumpHoldsText)
                    : [2, 6, 10],
            });
            File.WriteAllText(
                Path.Combine(artifactDirectory, "verified-nav-exploration.json"),
                JsonSerializer.Serialize(exploration, outputJsonOptions));
        }

        Console.WriteLine(
            $"verifiedNav map={graph.LevelName} area={graph.MapAreaIndex} team={graph.Team} class={graph.ClassId} surfaces={graph.Surfaces.Count} portals={graph.Portals.Count} candidates={graph.CandidateEdges.Count}");
        Console.WriteLine(
            $"verifiedNavBreakdown walk={summary.WalkCandidateCount} drop={summary.DropCandidateCount} jump={summary.JumpCandidateCount} solidSurfaces={summary.SolidSurfaceCount} dropdownSurfaces={summary.DropdownSurfaceCount}");
        if (certification is not null)
        {
            Console.WriteLine(
                $"verifiedNavCertification tested={certification.TestedEdgeCount}/{certification.CandidateEdgeCount} certified={certification.CertifiedEdgeCount} rejected={certification.RejectedEdgeCount}");
        }

        if (exploration is not null)
        {
            var enemySurface = graph.Portals
                .Where(static portal => portal.Kind == VerifiedNavPortalKind.EnemyIntel && portal.SurfaceId.HasValue)
                .Select(static portal => portal.SurfaceId!.Value)
                .FirstOrDefault(-1);
            var ownSurface = graph.Portals
                .Where(static portal => portal.Kind == VerifiedNavPortalKind.OwnIntel && portal.SurfaceId.HasValue)
                .Select(static portal => portal.SurfaceId!.Value)
                .FirstOrDefault(-1);
            Console.WriteLine(
                $"verifiedNavExploration startSurface={exploration.StartSurfaceId} starts:{string.Join(',', exploration.StartSurfaceIds)} reachable={exploration.ReachableSurfaceCount}/{graph.Surfaces.Count} edges={exploration.Edges.Count} reachesEnemySurface={exploration.ReachableSurfaceIds.Contains(enemySurface)} reachesOwnSurface={exploration.ReachableSurfaceIds.Contains(ownSurface)} reachesEnemyMarker={exploration.ReachedEnemyIntelMarker} reachesOwnMarker={exploration.ReachedOwnIntelMarker}");
        }

        Console.WriteLine($"verifiedNavArtifact={artifactDirectory}");
    }

    private static int ReadIntOption(Dictionary<string, string> options, string key, int fallback) =>
        options.TryGetValue(key, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static float ReadFloatOption(Dictionary<string, string> options, string key, float fallback) =>
        options.TryGetValue(key, out var text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static bool ReadBoolOption(Dictionary<string, string> options, string key, bool fallback) =>
        options.TryGetValue(key, out var text) && bool.TryParse(text, out var value)
            ? value
            : fallback;

    private static List<int> BuildLaneCoverageSeedSurfaceIds(VerifiedNavCandidateGraph graph)
    {
        var seeds = new HashSet<int>();
        foreach (var portal in graph.Portals)
        {
            if (portal.SurfaceId.HasValue
                && portal.Kind is VerifiedNavPortalKind.Spawn or VerifiedNavPortalKind.OwnIntel or VerifiedNavPortalKind.EnemyIntel)
            {
                seeds.Add(portal.SurfaceId.Value);
            }
        }

        if (graph.Surfaces.Count == 0)
        {
            return seeds.Order().ToList();
        }

        var minX = graph.Surfaces.Min(static surface => surface.Left);
        var maxX = graph.Surfaces.Max(static surface => surface.Right);
        var minY = graph.Surfaces.Min(static surface => surface.Top);
        var maxY = graph.Surfaces.Max(static surface => surface.Top);
        var xAnchors = new[]
        {
            Lerp(minX, maxX, 0.18f),
            Lerp(minX, maxX, 0.50f),
            Lerp(minX, maxX, 0.82f),
        };
        var yAnchors = new[]
        {
            Lerp(minY, maxY, 0.18f),
            Lerp(minY, maxY, 0.50f),
            Lerp(minY, maxY, 0.82f),
        };

        foreach (var x in xAnchors)
        {
            foreach (var y in yAnchors)
            {
                var nearest = graph.Surfaces
                    .OrderBy(surface => MathF.Abs(surface.CenterX - x) + (MathF.Abs(surface.Top - y) * 1.35f))
                    .First();
                seeds.Add(nearest.Id);
            }
        }

        return seeds.Order().ToList();
    }

    private static float Lerp(float from, float to, float amount) => from + ((to - from) * amount);

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

    public static void CompileBotBrainCorridorRecording(
        string recordingPath,
        JsonSerializerOptions outputJsonOptions,
        bool installAuthoredCorridor,
        bool rebuildAsset,
        bool bakeCorridorAsset,
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
                    $"dotnet run --project Tools\\BotBrain\\OpenGarrison.BotBrain.Tools.csproj --no-build -- --map {recording.LevelName} --area {recording.MapAreaIndex} --team {recording.Team} --class {recording.PlayerClass} --ticks 180 --probe-from {from.NodeIndex} --probe-to {to.NodeIndex}"));
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

    public static void RunLocalMotionLab(Dictionary<string, string> rawOptions)
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(repoRoot, "Core", "Content"));

        var mapName = rawOptions.TryGetValue("map", out var mapText) ? mapText : "Truefort";
        var area = ReadIntOption(rawOptions, "area", 1);
        var team = ReadEnumOption(rawOptions, "team", PlayerTeam.Red);
        var classId = ReadEnumOption(rawOptions, "class", PlayerClass.Pyro);
        var ticks = ReadIntOption(rawOptions, "ticks", 180);
        var xOffsets = rawOptions.TryGetValue("validation-x-offsets", out var xOffsetText)
            ? ParseFloatList(xOffsetText)
            : [-24f, 0f, 24f];
        var bottomOffsets = rawOptions.TryGetValue("validation-bottom-offsets", out var bottomOffsetText)
            ? ParseFloatList(bottomOffsetText)
            : [-8f, 0f, 8f];
        var horizontalSpeeds = rawOptions.TryGetValue("validation-horizontal-speeds", out var horizontalSpeedText)
            ? ParseFloatList(horizontalSpeedText)
            : [-60f, 0f, 60f];
        var verticalSpeeds = rawOptions.TryGetValue("validation-vertical-speeds", out var verticalSpeedText)
            ? ParseFloatList(verticalSpeedText)
            : [0f];
        var scenarios = BuildLocalMotionLabScenarios(rawOptions);
        Console.WriteLine(
            $"localMotionLab map={mapName} area={area} team={team} class={classId} scenarios={scenarios.Count} " +
            $"variants={xOffsets.Count * bottomOffsets.Count * horizontalSpeeds.Count * verticalSpeeds.Count} ticks={ticks}");

        var totalPrimitivePassed = 0;
        var totalStochasticPassed = 0;
        var totalCases = 0;
        foreach (var scenario in scenarios)
        {
            var primitiveResults = RunLocalMotionLabScenario(
                mapName,
                area,
                team,
                classId,
                scenario,
                ticks,
                xOffsets,
                bottomOffsets,
                horizontalSpeeds,
                verticalSpeeds,
                LocalMotionLabMode.Primitive);
            var stochasticResults = RunLocalMotionLabScenario(
                mapName,
                area,
                team,
                classId,
                scenario,
                ticks,
                xOffsets,
                bottomOffsets,
                horizontalSpeeds,
                verticalSpeeds,
                LocalMotionLabMode.Stochastic);

            totalCases += primitiveResults.Count;
            totalPrimitivePassed += primitiveResults.Count(static result => result.Passed);
            totalStochasticPassed += stochasticResults.Count(static result => result.Passed);
            Console.WriteLine(FormatLocalMotionLabSummary(scenario, LocalMotionLabMode.Primitive, primitiveResults));
            Console.WriteLine(FormatLocalMotionLabSummary(scenario, LocalMotionLabMode.Stochastic, stochasticResults));
        }

        Console.WriteLine(
            $"localMotionLabTotal primitive={totalPrimitivePassed}/{totalCases} " +
            $"stochastic={totalStochasticPassed}/{totalCases} delta={totalStochasticPassed - totalPrimitivePassed}");
    }

    public static void RunDirectDriveLab(Dictionary<string, string> rawOptions)
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(repoRoot, "Core", "Content"));

        var mapName = rawOptions.TryGetValue("map", out var mapText) ? mapText : "Truefort";
        var area = ReadIntOption(rawOptions, "area", 1);
        var team = ReadEnumOption(rawOptions, "team", PlayerTeam.Red);
        var classId = ReadEnumOption(rawOptions, "class", PlayerClass.Pyro);
        var ticks = ReadIntOption(rawOptions, "ticks", 2400);
        var reportEvery = ReadIntOption(rawOptions, "report-every", 60);
        var stuckWindowTicks = ReadIntOption(rawOptions, "stuck-window", 180);
        var stuckMovement = ReadFloatOption(rawOptions, "stuck-movement", 24f);
        var stuckProgress = ReadFloatOption(rawOptions, "stuck-progress", 18f);
        var dumpCandidates = rawOptions.TryGetValue("dump-candidates", out var dumpText)
            && bool.TryParse(dumpText, out var parsedDump)
            && parsedDump;
        var dumpXMin = ReadFloatOption(rawOptions, "dump-x-min", float.NegativeInfinity);
        var dumpXMax = ReadFloatOption(rawOptions, "dump-x-max", float.PositiveInfinity);

        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        if (!world.TryLoadLevel(mapName, area, preservePlayerStats: false))
        {
            Console.WriteLine($"directDriveLab=load_failed map={mapName} area={area}");
            return;
        }

        var enemyTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var enemyIntel = world.Level.GetIntelBase(enemyTeam);
        if (!enemyIntel.HasValue)
        {
            Console.WriteLine($"directDriveLab=no_enemy_intel map={mapName} area={area} team={team}");
            return;
        }

        const byte botSlot = 2;
        var spawn = world.Level.GetSpawn(team, 0);
        world.PrepareLocalPlayerJoin();
        world.TrySetNetworkPlayerSpawnOverride(botSlot, spawn.X, spawn.Y);
        if (!world.TryPrepareNetworkPlayerJoin(botSlot)
            || !world.TrySetNetworkPlayerTeam(botSlot, team)
            || !world.TryApplyNetworkPlayerClassSelection(botSlot, classId)
            || !world.TryGetNetworkPlayer(botSlot, out var bot))
        {
            Console.WriteLine($"directDriveLab=spawn_failed map={mapName} area={area} team={team} class={classId}");
            return;
        }

        var targetBottom = enemyIntel.Value.Y + bot.CollisionBottomOffset;
        var goal = StochasticLocalMotionGoal.FromPoint(
            enemyIntel.Value.X,
            targetBottom,
            "enemyIntel",
            acceptanceX: 44f,
            acceptanceBottom: 34f);
        var planner = new StochasticLocalMotionPlanner();
        var startMetric = MeasureDirectDriveMetric(bot.X, bot.Bottom, goal);
        var bestMetric = startMetric;
        var bestTick = 0;
        var bestX = bot.X;
        var bestBottom = bot.Bottom;
        var windowStartTick = 0;
        var windowStartX = bot.X;
        var windowStartBottom = bot.Bottom;
        var windowStartBestMetric = bestMetric;
        var totalCpuMs = 0d;
        var totalCandidates = 0;
        var totalSimTicks = 0;
        var noPlanTicks = 0;
        var lastTrace = string.Empty;
        var lastFrontierDiagnostics = string.Empty;

        Console.WriteLine(
            $"directDriveLab map={mapName} area={area} team={team} class={classId} " +
            $"spawn=({bot.X:0.0},{bot.Bottom:0.0}) target=({goal.X:0.0},{goal.Bottom:0.0}) " +
            $"initialMetric={startMetric:0.0} ticks={ticks} mode=stochastic-direct");

        for (var tick = 1; tick <= ticks; tick += 1)
        {
            var resolved = planner.TryResolve(world, bot, goal, tick, out var input, out var trace);
            if (!string.IsNullOrWhiteSpace(planner.LastCandidateTrace))
            {
                lastFrontierDiagnostics = planner.LastCandidateTrace;
            }

            if (dumpCandidates
                && bot.X >= dumpXMin
                && bot.X <= dumpXMax
                && !string.IsNullOrWhiteSpace(planner.LastCandidateTrace))
            {
                Console.WriteLine(
                    $"directDriveCandidates tick={tick} pos=({bot.X:0.0},{bot.Bottom:0.0}) " +
                    $"speed=({bot.HorizontalSpeed:0.0},{bot.VerticalSpeed:0.0}) grounded={bot.IsGrounded} " +
                    planner.LastCandidateTrace);
            }

            if (!resolved)
            {
                noPlanTicks += 1;
                input = default;
            }

            if (trace.Candidates > 0)
            {
                totalCandidates += trace.Candidates;
                totalSimTicks += trace.SimTicks;
                totalCpuMs += trace.ElapsedMilliseconds;
            }

            if (!string.IsNullOrWhiteSpace(trace.Source) || !string.IsNullOrWhiteSpace(trace.RejectedReason))
            {
                lastTrace =
                    $"source:{trace.Source} macro:{trace.MacroLabel} reject:{trace.RejectedReason} " +
                    $"start:{trace.StartMetric:0.0} best:{trace.BestMetric:0.0} final:{trace.FinalMetric:0.0} " +
                    $"progress:{trace.Progress:0.0} score:{trace.Score:0.0} candidates:{trace.Candidates} simTicks:{trace.SimTicks} cpuMs:{trace.ElapsedMilliseconds:0.000}";
            }

            if (!world.TrySetNetworkPlayerInput(botSlot, input))
            {
                Console.WriteLine($"directDriveLab=input_failed tick={tick}");
                return;
            }

            world.AdvanceOneTick();
            var metric = MeasureDirectDriveMetric(bot.X, bot.Bottom, goal);
            if (metric < bestMetric)
            {
                bestMetric = metric;
                bestTick = tick;
                bestX = bot.X;
                bestBottom = bot.Bottom;
            }

            if (HasReachedDirectDriveGoal(bot, goal))
            {
                Console.WriteLine(
                    $"directDriveLabResult=Reached tick={tick} pos=({bot.X:0.0},{bot.Bottom:0.0}) " +
                    $"bestMetric={bestMetric:0.0}@{bestTick} totalCandidates={totalCandidates} " +
                    $"totalSimTicks={totalSimTicks} cpuMs={totalCpuMs:0.000} noPlanTicks={noPlanTicks}");
                return;
            }

            if (reportEvery > 0 && tick % reportEvery == 0)
            {
                Console.WriteLine(
                    $"directDriveLabTick tick={tick} pos=({bot.X:0.0},{bot.Bottom:0.0}) " +
                    $"speed=({bot.HorizontalSpeed:0.0},{bot.VerticalSpeed:0.0}) grounded={bot.IsGrounded} " +
                    $"metric={metric:0.0} best={bestMetric:0.0}@{bestTick} input=L{Flag(input.Left)}R{Flag(input.Right)}U{Flag(input.Up)}D{Flag(input.Down)} trace={lastTrace}");
            }

            if (tick - windowStartTick < stuckWindowTicks)
            {
                continue;
            }

            var windowMovement = Distance(bot.X, bot.Bottom, windowStartX, windowStartBottom);
            var windowProgress = windowStartBestMetric - bestMetric;
            if (windowMovement <= stuckMovement && windowProgress <= stuckProgress)
            {
                Console.WriteLine(
                    $"directDriveLabResult=Stuck tick={tick} pos=({bot.X:0.0},{bot.Bottom:0.0}) " +
                    $"windowStart=({windowStartX:0.0},{windowStartBottom:0.0}) windowTicks={tick - windowStartTick} " +
                    $"windowMovement={windowMovement:0.0} windowProgress={windowProgress:0.0} metric={metric:0.0} " +
                    $"best=({bestX:0.0},{bestBottom:0.0}) bestMetric={bestMetric:0.0}@{bestTick} " +
                    $"speed=({bot.HorizontalSpeed:0.0},{bot.VerticalSpeed:0.0}) grounded={bot.IsGrounded} " +
                    $"target=({goal.X:0.0},{goal.Bottom:0.0}) noPlanTicks={noPlanTicks} lastTrace={lastTrace} " +
                    $"frontierDiagnostics={lastFrontierDiagnostics}");
                return;
            }

            windowStartTick = tick;
            windowStartX = bot.X;
            windowStartBottom = bot.Bottom;
            windowStartBestMetric = bestMetric;
        }

        Console.WriteLine(
            $"directDriveLabResult=Timeout tick={ticks} pos=({bot.X:0.0},{bot.Bottom:0.0}) " +
            $"best=({bestX:0.0},{bestBottom:0.0}) bestMetric={bestMetric:0.0}@{bestTick} " +
            $"target=({goal.X:0.0},{goal.Bottom:0.0}) totalCandidates={totalCandidates} totalSimTicks={totalSimTicks} " +
            $"cpuMs={totalCpuMs:0.000} noPlanTicks={noPlanTicks} lastTrace={lastTrace} " +
            $"frontierDiagnostics={lastFrontierDiagnostics}");
    }

    private static float MeasureDirectDriveMetric(float x, float bottom, StochasticLocalMotionGoal goal)
    {
        var horizontal = MathF.Max(0f, MathF.Abs(goal.X - x) - goal.AcceptanceX);
        var vertical = MathF.Max(0f, MathF.Abs(goal.Bottom - bottom) - goal.AcceptanceBottom);
        return horizontal + (vertical * 2.4f);
    }

    private static bool HasReachedDirectDriveGoal(PlayerEntity player, StochasticLocalMotionGoal goal)
        => MathF.Abs(player.X - goal.X) <= goal.AcceptanceX
            && MathF.Abs(player.Bottom - goal.Bottom) <= goal.AcceptanceBottom;

    private static int Flag(bool value) => value ? 1 : 0;

    private static List<LocalMotionLabScenario> BuildLocalMotionLabScenarios(Dictionary<string, string> rawOptions)
    {
        var hasExplicitScenario = rawOptions.ContainsKey("start-x")
            && rawOptions.ContainsKey("start-bottom")
            && rawOptions.ContainsKey("target-x")
            && rawOptions.ContainsKey("target-bottom");
        if (hasExplicitScenario)
        {
            return
            [
                new LocalMotionLabScenario(
                    rawOptions.TryGetValue("scenario-name", out var scenarioName) ? scenarioName : "explicit",
                    ReadFloatOption(rawOptions, "start-x", 0f),
                    ReadFloatOption(rawOptions, "start-bottom", 0f),
                    ReadFloatOption(rawOptions, "target-x", 0f),
                    ReadFloatOption(rawOptions, "target-bottom", 0f),
                    ReadFloatOption(rawOptions, "acceptance-x", 36f),
                    ReadFloatOption(rawOptions, "acceptance-bottom", 24f)),
            ];
        }

        return
        [
            new LocalMotionLabScenario("truefort_mid_walk_18_19", 1624.3f, 498f, 1869.0f, 498f, 42f, 18f),
            new LocalMotionLabScenario("truefort_lip_19_22", 1869.0f, 498f, 2044.6f, 516f, 42f, 22f),
            new LocalMotionLabScenario("truefort_lower_bridge_51_58", 3022.9f, 708f, 3376.5f, 768f, 48f, 28f),
            new LocalMotionLabScenario("truefort_battlement_drop_17_76", 4332.0f, 480f, 4527.7f, 912f, 54f, 36f),
        ];
    }

    private static List<LocalMotionLabCaseResult> RunLocalMotionLabScenario(
        string mapName,
        int area,
        PlayerTeam team,
        PlayerClass classId,
        LocalMotionLabScenario scenario,
        int ticks,
        IReadOnlyList<float> xOffsets,
        IReadOnlyList<float> bottomOffsets,
        IReadOnlyList<float> horizontalSpeeds,
        IReadOnlyList<float> verticalSpeeds,
        LocalMotionLabMode mode)
    {
        var results = new List<LocalMotionLabCaseResult>();
        foreach (var xOffset in xOffsets)
        {
            foreach (var bottomOffset in bottomOffsets)
            {
                foreach (var horizontalSpeed in horizontalSpeeds)
                {
                    foreach (var verticalSpeed in verticalSpeeds)
                    {
                        results.Add(RunLocalMotionLabCase(
                            mapName,
                            area,
                            team,
                            classId,
                            scenario,
                            ticks,
                            xOffset,
                            bottomOffset,
                            horizontalSpeed,
                            verticalSpeed,
                            mode));
                    }
                }
            }
        }

        return results;
    }

    private static LocalMotionLabCaseResult RunLocalMotionLabCase(
        string mapName,
        int area,
        PlayerTeam team,
        PlayerClass classId,
        LocalMotionLabScenario scenario,
        int ticks,
        float xOffset,
        float bottomOffset,
        float horizontalSpeed,
        float verticalSpeed,
        LocalMotionLabMode mode)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        if (!world.TryLoadLevel(mapName, area, preservePlayerStats: false))
        {
            return LocalMotionLabCaseResult.Failed("load_failed", scenario, mode, xOffset, bottomOffset, horizontalSpeed, verticalSpeed);
        }

        const byte botSlot = 2;
        world.PrepareLocalPlayerJoin();
        if (!world.TryPrepareNetworkPlayerJoin(botSlot)
            || !world.TrySetNetworkPlayerTeam(botSlot, team)
            || !world.TryApplyNetworkPlayerClassSelection(botSlot, classId)
            || !world.TryGetNetworkPlayer(botSlot, out var bot))
        {
            return LocalMotionLabCaseResult.Failed("spawn_failed", scenario, mode, xOffset, bottomOffset, horizontalSpeed, verticalSpeed);
        }

        var startX = scenario.StartX + xOffset;
        var startBottom = scenario.StartBottom + bottomOffset;
        var startY = startBottom - bot.CollisionBottomOffset;
        bot.Spawn(team, startX, startY);
        bot.TeleportTo(startX, startY);
        bot.ResolveBlockingOverlap(world.Level, team);
        if (horizontalSpeed != 0f || verticalSpeed != 0f)
        {
            bot.AddImpulse(horizontalSpeed, verticalSpeed);
        }

        bot.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: bot.MaxAirJumps, facingDirectionX: scenario.TargetX >= scenario.StartX ? 1f : -1f);

        var planner = new StochasticLocalMotionPlanner();
        var goal = StochasticLocalMotionGoal.FromPoint(
            scenario.TargetX,
            scenario.TargetBottom,
            scenario.Name,
            scenario.AcceptanceX,
            scenario.AcceptanceBottom);
        var previousInput = default(PlayerInputSnapshot);
        var bestMetric = MeasureLocalMotionLabMetric(bot.X, bot.Bottom, scenario);
        var startMetric = bestMetric;
        var totalSimTicks = 0;
        var totalCandidates = 0;
        var totalCpuMs = 0d;
        var decisions = 0;
        var noPlanTicks = 0;
        var flips = 0;
        var stagnantWindows = 0;
        var windowBestMetric = bestMetric;
        var previousMove = 0;
        var lastTrace = string.Empty;
        for (var tick = 1; tick <= ticks; tick += 1)
        {
            PlayerInputSnapshot input;
            if (mode == LocalMotionLabMode.Stochastic)
            {
                var resolved = planner.TryResolve(world, bot, goal, tick, out input, out var trace);
                if (trace.Candidates > 0 || trace.Resolved && trace.Source == "probe")
                {
                    decisions += 1;
                    totalSimTicks += trace.SimTicks;
                    totalCandidates += trace.Candidates;
                    totalCpuMs += trace.ElapsedMilliseconds;
                }

                if (!resolved)
                {
                    noPlanTicks += 1;
                    input = default;
                }

                if (!string.IsNullOrWhiteSpace(trace.Source) || !string.IsNullOrWhiteSpace(trace.RejectedReason))
                {
                    lastTrace = $"{trace.Source}:{trace.MacroLabel}:{trace.RejectedReason} p:{trace.Progress:0.0} cpu:{trace.ElapsedMilliseconds:0.000}";
                }
            }
            else
            {
                var targetY = scenario.TargetBottom - bot.CollisionBottomOffset;
                var resolved = PrimitiveDirectDrive.TryResolveRecovery(
                    world,
                    bot,
                    new DirectDriveTarget(DirectDriveTargetKind.Objective, scenario.TargetX, targetY, scenario.Name),
                    default,
                    out var steering,
                    out var trace);
                if (!resolved)
                {
                    noPlanTicks += 1;
                    input = default;
                }
                else
                {
                    input = BotInputSynthesizer.Synthesize(
                        bot,
                        steering,
                        scenario.TargetX,
                        targetY,
                        default,
                        previousInput);
                    lastTrace = trace;
                }
            }

            var move = input.Left == input.Right ? 0 : input.Right ? 1 : -1;
            if (move != 0 && previousMove != 0 && move != previousMove)
            {
                flips += 1;
            }

            if (move != 0)
            {
                previousMove = move;
            }

            if (!world.TrySetNetworkPlayerInput(botSlot, input))
            {
                return LocalMotionLabCaseResult.Failed("input_failed", scenario, mode, xOffset, bottomOffset, horizontalSpeed, verticalSpeed);
            }

            world.AdvanceOneTick();
            previousInput = input;
            var metric = MeasureLocalMotionLabMetric(bot.X, bot.Bottom, scenario);
            bestMetric = MathF.Min(bestMetric, metric);
            windowBestMetric = MathF.Min(windowBestMetric, metric);
            if (tick % 30 == 0)
            {
                if (windowBestMetric > bestMetric + 0.1f || startMetric - windowBestMetric < 6f)
                {
                    stagnantWindows += 1;
                }

                windowBestMetric = metric;
            }

            if (HasReachedLocalMotionLabGoal(bot, scenario))
            {
                return new LocalMotionLabCaseResult(
                    scenario.Name,
                    mode,
                    Passed: true,
                    FailureReason: string.Empty,
                    Ticks: tick,
                    XOffset: xOffset,
                    BottomOffset: bottomOffset,
                    HorizontalSpeed: horizontalSpeed,
                    VerticalSpeed: verticalSpeed,
                    StartMetric: startMetric,
                    BestMetric: bestMetric,
                    FinalMetric: metric,
                    Decisions: decisions,
                    Candidates: totalCandidates,
                    SimTicks: totalSimTicks,
                    CpuMilliseconds: totalCpuMs,
                    NoPlanTicks: noPlanTicks,
                    MoveFlips: flips,
                    StagnantWindows: stagnantWindows,
                    FinalX: bot.X,
                    FinalBottom: bot.Bottom,
                    LastTrace: lastTrace);
            }
        }

        var finalMetric = MeasureLocalMotionLabMetric(bot.X, bot.Bottom, scenario);
        return new LocalMotionLabCaseResult(
            scenario.Name,
            mode,
            Passed: false,
            FailureReason: "timeout",
            Ticks: ticks,
            XOffset: xOffset,
            BottomOffset: bottomOffset,
            HorizontalSpeed: horizontalSpeed,
            VerticalSpeed: verticalSpeed,
            StartMetric: startMetric,
            BestMetric: bestMetric,
            FinalMetric: finalMetric,
            Decisions: decisions,
            Candidates: totalCandidates,
            SimTicks: totalSimTicks,
            CpuMilliseconds: totalCpuMs,
            NoPlanTicks: noPlanTicks,
            MoveFlips: flips,
            StagnantWindows: stagnantWindows,
            FinalX: bot.X,
            FinalBottom: bot.Bottom,
            LastTrace: lastTrace);
    }

    private static string FormatLocalMotionLabSummary(
        LocalMotionLabScenario scenario,
        LocalMotionLabMode mode,
        IReadOnlyList<LocalMotionLabCaseResult> results)
    {
        var passed = results.Count(static result => result.Passed);
        var passedResults = results.Where(static result => result.Passed).ToArray();
        var failedResults = results.Where(static result => !result.Passed).ToArray();
        var medianTicks = passedResults.Length == 0 ? -1 : MedianInt(passedResults.Select(static result => result.Ticks).ToArray());
        var p95Cpu = Percentile(results.Select(static result => result.CpuMilliseconds).ToArray(), 0.95f);
        var p95SimTicks = Percentile(results.Select(static result => (double)result.SimTicks).ToArray(), 0.95f);
        var worst = failedResults
            .OrderBy(static result => result.BestMetric)
            .FirstOrDefault();
        var worstText = failedResults.Length == 0
            ? "none"
            : $"{worst.FailureReason}@x{worst.XOffset:0}/b{worst.BottomOffset:0}/hs{worst.HorizontalSpeed:0} best:{worst.BestMetric:0.0} final:({worst.FinalX:0.0},{worst.FinalBottom:0.0}) trace:{worst.LastTrace}";
        var failureList = failedResults.Length == 0
            ? "none"
            : string.Join(
                ";",
                failedResults
                    .OrderBy(static result => result.XOffset)
                    .ThenBy(static result => result.BottomOffset)
                    .ThenBy(static result => result.HorizontalSpeed)
                    .Take(8)
                    .Select(static result =>
                        $"x{result.XOffset:0}/b{result.BottomOffset:0}/hs{result.HorizontalSpeed:0}->({result.FinalX:0.0},{result.FinalBottom:0.0}) best:{result.BestMetric:0.0} trace:{result.LastTrace}"));
        return
            $"localMotionLabScenario={scenario.Name} mode={mode} pass={passed}/{results.Count} " +
            $"medianTicks={medianTicks} noPlan:{results.Sum(static result => result.NoPlanTicks)} " +
            $"flips:{results.Sum(static result => result.MoveFlips)} stagnant:{results.Sum(static result => result.StagnantWindows)} " +
            $"decisions:{results.Sum(static result => result.Decisions)} candidates:{results.Sum(static result => result.Candidates)} " +
            $"simTicks:{results.Sum(static result => result.SimTicks)} p95SimTicks:{p95SimTicks:0.0} cpuMsTotal:{results.Sum(static result => result.CpuMilliseconds):0.000} " +
            $"cpuMsP95:{p95Cpu:0.000} worstFailure:{worstText} failures:{failureList}";
    }

    private static float MeasureLocalMotionLabMetric(float x, float bottom, LocalMotionLabScenario scenario)
    {
        var horizontal = MathF.Max(0f, MathF.Abs(scenario.TargetX - x) - scenario.AcceptanceX);
        var vertical = MathF.Max(0f, MathF.Abs(scenario.TargetBottom - bottom) - scenario.AcceptanceBottom);
        return horizontal + (vertical * 2.4f);
    }

    private static bool HasReachedLocalMotionLabGoal(PlayerEntity player, LocalMotionLabScenario scenario)
    {
        return MathF.Abs(player.X - scenario.TargetX) <= scenario.AcceptanceX
            && MathF.Abs(player.Bottom - scenario.TargetBottom) <= scenario.AcceptanceBottom;
    }

    private static int MedianInt(int[] values)
    {
        if (values.Length == 0)
        {
            return -1;
        }

        Array.Sort(values);
        return values[values.Length / 2];
    }

    private static double Percentile(double[] values, float percentile)
    {
        if (values.Length == 0)
        {
            return 0d;
        }

        Array.Sort(values);
        var index = Math.Clamp((int)MathF.Ceiling((values.Length - 1) * percentile), 0, values.Length - 1);
        return values[index];
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
    int PreTicks,
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
            GetInt(options, "pre-ticks", 0),
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

internal readonly record struct BotBrainRuntimeStateArtifact(
    int Tick,
    float X,
    float Y,
    float Bottom,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool IsGrounded,
    int RemainingAirJumps,
    float FacingDirectionX,
    bool Left,
    bool Right,
    bool Up,
    bool Down,
    bool IsCarryingIntel);

internal readonly record struct LocalMotionLabScenario(
    string Name,
    float StartX,
    float StartBottom,
    float TargetX,
    float TargetBottom,
    float AcceptanceX,
    float AcceptanceBottom);

internal enum LocalMotionLabMode
{
    Primitive,
    Stochastic,
}

internal readonly record struct LocalMotionLabCaseResult(
    string Scenario,
    LocalMotionLabMode Mode,
    bool Passed,
    string FailureReason,
    int Ticks,
    float XOffset,
    float BottomOffset,
    float HorizontalSpeed,
    float VerticalSpeed,
    float StartMetric,
    float BestMetric,
    float FinalMetric,
    int Decisions,
    int Candidates,
    int SimTicks,
    double CpuMilliseconds,
    int NoPlanTicks,
    int MoveFlips,
    int StagnantWindows,
    float FinalX,
    float FinalBottom,
    string LastTrace)
{
    public static LocalMotionLabCaseResult Failed(
        string reason,
        LocalMotionLabScenario scenario,
        LocalMotionLabMode mode,
        float xOffset,
        float bottomOffset,
        float horizontalSpeed,
        float verticalSpeed) =>
        new(
            scenario.Name,
            mode,
            Passed: false,
            reason,
            Ticks: 0,
            xOffset,
            bottomOffset,
            horizontalSpeed,
            verticalSpeed,
            StartMetric: float.PositiveInfinity,
            BestMetric: float.PositiveInfinity,
            FinalMetric: float.PositiveInfinity,
            Decisions: 0,
            Candidates: 0,
            SimTicks: 0,
            CpuMilliseconds: 0d,
            NoPlanTicks: 0,
            MoveFlips: 0,
            StagnantWindows: 0,
            FinalX: 0f,
            FinalBottom: 0f,
            LastTrace: string.Empty);
}

internal sealed record TraversalSoakRunResult(
    string MapName,
    int AreaIndex,
    bool Passed,
    string Reason);

internal sealed class TraversalSoakBotStats
{
    public TraversalSoakBotStats(byte slot, PlayerTeam team, PlayerClass classId, float startX, float startY, float startBottom)
    {
        Slot = slot;
        Team = team;
        ClassId = classId;
        StartX = startX;
        StartY = startY;
        StartBottom = startBottom;
        LastX = startX;
        LastY = startY;
        LastBottom = startBottom;
        PreviousX = startX;
        PreviousY = startY;
        WindowX = startX;
        WindowY = startY;
        OscillationWindowX = startX;
        OscillationWindowY = startY;
    }

    public byte Slot { get; }

    public PlayerTeam Team { get; }

    public PlayerClass ClassId { get; }

    public float StartX { get; }

    public float StartY { get; }

    public float StartBottom { get; }

    public float LastPreX { get; set; }

    public float LastPreY { get; set; }

    public float LastPreBottom { get; set; }

    public float LastX { get; set; }

    public float LastY { get; set; }

    public float LastBottom { get; set; }

    public bool LastGrounded { get; set; }

    public float LastHorizontalSpeed { get; set; }

    public float LastVerticalSpeed { get; set; }

    public PlayerInputSnapshot LastInput { get; set; }

    public SteeringOutput LastSteering { get; set; }

    public string LastTraversalTrace { get; set; } = string.Empty;

    public string LastSemanticTrace { get; set; } = string.Empty;

    public int LastCaps { get; set; }

    public int LastCapsSeen { get; set; }

    public int FirstCapTick { get; set; } = -1;

    public int CarrierConversionTicks { get; set; } = -1;

    public bool LastCarryingIntel { get; set; }

    public bool HasPostAdvanceSample { get; set; }

    public bool LastAlive { get; set; } = true;

    public int DeathCount { get; set; }

    public int FirstDeathTick { get; set; } = -1;

    public int CarryLossCount { get; set; }

    public int FirstCarryLossTick { get; set; } = -1;

    public float FirstCarryLossX { get; set; }

    public float FirstCarryLossY { get; set; }

    public float PreviousX { get; set; }

    public float PreviousY { get; set; }

    public float WindowX { get; set; }

    public float WindowY { get; set; }

    public float RecentWindowMovement { get; set; }

    public bool RecentStagnant { get; set; }

    public float TotalMovement { get; set; }

    public int StagnantWindows { get; set; }

    public int ConsecutiveInertTicks { get; set; }

    public int MaxConsecutiveInertTicks { get; set; }

    public int FirstInertFailTick { get; set; } = -1;

    public float FirstInertFailX { get; set; }

    public float FirstInertFailY { get; set; }

    public string FirstInertFailTrace { get; set; } = string.Empty;

    public float OscillationWindowX { get; set; }

    public float OscillationWindowY { get; set; }

    public int OscillationWindowLowSpeedMoveFlips { get; set; }

    public int OscillationWindowRouteLowSpeedMoveFlips { get; set; }

    public int OscillationWindowIntentionalHoldTicks { get; set; }

    public float RecentOscillationWindowMovement { get; set; }

    public int OscillationEvents { get; set; }

    public int FirstOscillationTick { get; set; } = -1;

    public string FirstOscillationTrace { get; set; } = string.Empty;

    public int ZeroInputTicks { get; set; }

    public int IntentionalObjectiveHoldTicks { get; set; }

    public int MoveFlips { get; set; }

    public int LowSpeedMoveFlips { get; set; }

    public int RouteLowSpeedMoveFlips { get; set; }

    public int LastMoveSign { get; set; }

    public int CarryingIntelTick { get; set; } = -1;

    public long ThinkTicks { get; set; }

    public long ThinkStopwatchTicks { get; set; }

    public long MaxThinkStopwatchTicks { get; set; }

}

internal sealed class PracticeRosterBotStats
{
    public PracticeRosterBotStats(byte slot, PlayerTeam team, PlayerClass classId, float startX, float startY, float startBottom)
    {
        Slot = slot;
        Team = team;
        ClassId = classId;
        StartX = startX;
        StartY = startY;
        StartBottom = startBottom;
        LastX = startX;
        LastY = startY;
        LastBottom = startBottom;
        PreviousX = startX;
        PreviousY = startY;
        WindowX = startX;
        WindowY = startY;
    }

    public byte Slot { get; }

    public PlayerTeam Team { get; }

    public PlayerClass ClassId { get; }

    public float StartX { get; }

    public float StartY { get; }

    public float StartBottom { get; }

    public float LastPreX { get; set; }

    public float LastPreY { get; set; }

    public float LastPreBottom { get; set; }

    public float LastX { get; set; }

    public float LastY { get; set; }

    public float LastBottom { get; set; }

    public bool LastGrounded { get; set; }

    public float LastHorizontalSpeed { get; set; }

    public float LastVerticalSpeed { get; set; }

    public PlayerInputSnapshot LastInput { get; set; }

    public SteeringOutput LastSteering { get; set; }


    public string LastDirectTrace { get; set; } = string.Empty;


    public string LastIssue { get; set; } = string.Empty;

    public int LastPathCount { get; set; }

    public int LastPathIndex { get; set; }


    public bool LastRouteOwned { get; set; }

    public int RouteMoveFlips { get; set; }

    public int RouteLowSpeedMoveFlips { get; set; }

    public int LastRouteMoveSign { get; set; }

    public int LastRoutePathIndex { get; set; } = -1;

    public NavEdgeKind LastRouteEdgeKind { get; set; }

    public int CurrentRoutePathIndex { get; set; } = -1;

    public NavEdgeKind CurrentRouteEdgeKind { get; set; }

    public int RouteSameEdgeFlips { get; set; }

    public int RouteSameEdgeWalkFlips { get; set; }

    public int LastCaps { get; set; }

    public int LastCapsSeen { get; set; }

    public int FirstCapTick { get; set; } = -1;

    public int CarrierConversionTicks { get; set; } = -1;

    public bool LastCarryingIntel { get; set; }

    public bool HasPostAdvanceSample { get; set; }

    public bool LastAlive { get; set; } = true;

    public int DeathCount { get; set; }

    public int FirstDeathTick { get; set; } = -1;

    public int CarryLossCount { get; set; }

    public int FirstCarryLossTick { get; set; } = -1;

    public float FirstCarryLossX { get; set; }

    public float FirstCarryLossY { get; set; }

    public float PreviousX { get; set; }

    public float PreviousY { get; set; }

    public float WindowX { get; set; }

    public float WindowY { get; set; }

    public float RecentWindowMovement { get; set; }

    public bool RecentStagnant { get; set; }

    public float TotalMovement { get; set; }

    public int StagnantWindows { get; set; }

    public int ZeroInputTicks { get; set; }

    public int MoveFlips { get; set; }

    public int LowSpeedMoveFlips { get; set; }

    public int LastMoveSign { get; set; }




    public int DirectTicks { get; set; }

    public int GraphTicks { get; set; }


    public int CarryingIntelTick { get; set; } = -1;


    public string LastDirectTraceKey { get; set; } = string.Empty;



    public Dictionary<string, int> DirectRejectByReason { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> LocalMotionFailureByReason { get; } = new(StringComparer.Ordinal);
}

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
