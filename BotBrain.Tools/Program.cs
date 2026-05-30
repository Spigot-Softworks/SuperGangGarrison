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

if (rawOptions.TryGetValue("generate-proof-return", out var generateProofReturn)
    && bool.TryParse(generateProofReturn, out var parsedGenerateProofReturn)
    && parsedGenerateProofReturn)
{
    BotBrainToolCommandHelpers.GenerateProofReturnTape(rawOptions, artifactJsonOptions);
    return;
}

if (rawOptions.TryGetValue("plan-proof-return", out var planProofReturn)
    && bool.TryParse(planProofReturn, out var parsedPlanProofReturn)
    && parsedPlanProofReturn)
{
    BotBrainToolCommandHelpers.PlanProofReturnTape(rawOptions, artifactJsonOptions);
    return;
}

if (rawOptions.TryGetValue("plan-proof-pickup", out var planProofPickup)
    && bool.TryParse(planProofPickup, out var parsedPlanProofPickup)
    && parsedPlanProofPickup)
{
    BotBrainToolCommandHelpers.PlanProofReturnTape(rawOptions, artifactJsonOptions);
    return;
}

if (rawOptions.TryGetValue("plan-proof-objective", out var planProofObjective)
    && bool.TryParse(planProofObjective, out var parsedPlanProofObjective)
    && parsedPlanProofObjective)
{
    BotBrainToolCommandHelpers.PlanProofReturnTape(rawOptions, artifactJsonOptions);
    return;
}

if (rawOptions.TryGetValue("verified-nav-report", out var verifiedNavReport)
    && bool.TryParse(verifiedNavReport, out var parsedVerifiedNavReport)
    && parsedVerifiedNavReport)
{
    BotBrainToolCommandHelpers.RunVerifiedNavReport(rawOptions, artifactJsonOptions);
    return;
}

if (rawOptions.TryGetValue("build-proof-graph", out var buildProofGraph)
    && bool.TryParse(buildProofGraph, out var parsedBuildProofGraph)
    && parsedBuildProofGraph)
{
    BotBrainToolCommandHelpers.BuildVerifiedProofGraph(rawOptions, artifactJsonOptions);
    return;
}

if (rawOptions.TryGetValue("audit-proof-graphs", out var auditProofGraphs)
    && bool.TryParse(auditProofGraphs, out var parsedAuditProofGraphs)
    && parsedAuditProofGraphs)
{
    RunProofGraphAudit(rawOptions);
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

if (rawOptions.TryGetValue("extract-proof-trace", out var extractProofTrace)
    && bool.TryParse(extractProofTrace, out var parsedExtractProofTrace)
    && parsedExtractProofTrace)
{
    BotBrainToolCommandHelpers.ExtractVerifiedProofTraceFromPlannerSteps(rawOptions, artifactJsonOptions);
    return;
}

if (rawOptions.TryGetValue("generate-follow-proof-return", out var generateFollowProofReturn)
    && bool.TryParse(generateFollowProofReturn, out var parsedGenerateFollowProofReturn)
    && parsedGenerateFollowProofReturn)
{
    BotBrainToolCommandHelpers.GenerateFollowProofReturnTape(rawOptions, artifactJsonOptions);
    return;
}

if (rawOptions.TryGetValue("validate-return-tape-as-class", out var validateReturnTape)
    && bool.TryParse(validateReturnTape, out var parsedValidateReturnTape)
    && parsedValidateReturnTape)
{
    BotBrainToolCommandHelpers.ValidateReturnTapeAsClass(rawOptions, artifactJsonOptions);
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
if (!string.IsNullOrWhiteSpace(options.ProofGraphPath))
{
    Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.EnableEnvironmentVariable, "1");
    Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.PathEnvironmentVariable, Path.GetFullPath(options.ProofGraphPath));
}
if (options.ProofGraphRequired)
{
    Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.EnableEnvironmentVariable, "1");
    Environment.SetEnvironmentVariable(VerifiedNavProofGraphAssetStore.RequireEnvironmentVariable, "1");
}

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
ContentRoot.Initialize(Path.Combine(repoRoot, "Core", "Content"));

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
var proofGraphEvents = new List<ProofGraphTraceArtifact>();
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
var exportRunProofGraph = rawOptions.TryGetValue("export-run-proof-graph", out var exportRunProofGraphText)
    && bool.TryParse(exportRunProofGraphText, out var parsedExportRunProofGraph)
    && parsedExportRunProofGraph;
var runProofRouteKind = rawOptions.TryGetValue("run-proof-route-kind", out var runProofRouteKindText)
    && Enum.TryParse<VerifiedNavProofRouteKind>(runProofRouteKindText, ignoreCase: true, out var parsedRunProofRouteKind)
        ? parsedRunProofRouteKind
        : VerifiedNavProofRouteKind.Pickup;
var runProofSamples = new List<TraversalLabTickSample>();
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
runProofSamples.Add(CreateRuntimeProofSample(level, options.Team, bot, default, tick: 0, "Start"));

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
        semanticRecoveryEvents,
        proofGraphEvents);
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
        brain.LastProofGraphTrace,
        brain.LastObjectiveTapeTrace,
        brain.LastDirectDriveTrace,
        brain.LastSemanticRecoveryTrace);
    if (!string.IsNullOrWhiteSpace(brain.LastProofGraphTrace))
    {
        proofGraphEvents.Add(new ProofGraphTraceArtifact(
            Tick: tick,
            Trace: brain.LastProofGraphTrace,
            CarryingIntel: bot.IsCarryingIntel,
            X: bot.X,
            Y: bot.Y,
            Bottom: bot.Bottom,
            Grounded: bot.IsGrounded));
    }
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
    runProofSamples.Add(CreateRuntimeProofSample(level, options.Team, bot, input, tick, "Runtime"));

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
        var proofGraphText = !string.IsNullOrWhiteSpace(brain.LastProofGraphTrace)
            ? $" {brain.LastProofGraphTrace}"
            : string.Empty;
        var tapeText = !string.IsNullOrWhiteSpace(brain.LastObjectiveTapeTrace)
            ? $" {brain.LastObjectiveTapeTrace}"
            : string.Empty;
        var intelText = world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            ? $" redIntel:{FormatIntelState(world.RedIntel)} blueIntel:{FormatIntelState(world.BlueIntel)}"
            : string.Empty;
        Console.WriteLine(
            $"trace tick={tick} pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0} speed=({bot.HorizontalSpeed:0.0},{bot.VerticalSpeed:0.0}) grounded={bot.IsGrounded} airJumps={bot.RemainingAirJumps} carrying={bot.IsCarryingIntel} input=L{Bit(traceInput.Left)}R{Bit(traceInput.Right)}U{Bit(traceInput.Up)}D{Bit(traceInput.Down)}F{Bit(traceInput.FirePrimary)} path={tracePathIndex}/{tracePathCount} node={nodeText}{edgeText}{recipeText}{directDriveText}{proofGraphText}{tapeText}{intelText}");
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
Console.WriteLine($"summary=finalDistance:{finalDistance:0.0} bestDistance:{bestDistance:0.0} progress:{progress:0.0} totalMovement:{totalMovement:0.0} jumps:{jumpTicks} dropdowns:{dropdownTicks} fire:{fireTicks} deadTicks:{deadTicks} stagnantWindows:{stagnantWindows} carryingIntelTick:{carryingIntelTick} scoreTick:{scoreTick} objectiveReason:{(string.IsNullOrWhiteSpace(objectiveCompletionReason) ? "none" : objectiveCompletionReason)} redCaps:{world.RedCaps} blueCaps:{world.BlueCaps} playerCaps:{bot.Caps} finalNode:{finalNode} finalPathWaypoints:{finalPath?.Count ?? 0}");
Console.WriteLine($"failureBucket={failureBucket}");
var objectivePathAccepted = world.MatchRules.Mode == GameModeKind.Generator
    || (exactPath is not null && goalNode == exactGoalNode);
var proofPassed = scoreTick >= 0
    && validationIssues.Count == 0
    && objectivePathAccepted;
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
if (exportRunProofGraph)
{
    if (scoreTick >= 0)
    {
        var selectedSamples = runProofSamples
            .Where(sample => sample.Tick <= scoreTick)
            .ToArray();
        var verifiedGraph = VerifiedNavCandidateBuilder.Build(level, new VerifiedNavBuildOptions
        {
            Team = options.Team,
            ClassId = options.PlayerClass,
        });
        var proofTrace = VerifiedNavProofTraceExtractor.Extract(
            verifiedGraph,
            selectedSamples,
            $"RuntimeProof:{options.MapName}:a{options.AreaIndex}:{options.Team}:{options.PlayerClass}:{objectiveCompletionReason}",
            selectedSamples[0].X,
            selectedSamples[0].Bottom);
        var proofGraph = VerifiedNavProofGraphBuilder.Build(
            verifiedGraph,
            [(runProofRouteKind, proofTrace)]);
        if (artifactDirectory is not null)
        {
            WriteJson(Path.Combine(artifactDirectory, "runtime-proof-trace.json"), proofTrace, artifactJsonOptions);
            WriteJson(Path.Combine(artifactDirectory, "verified-proof-graph.json"), proofGraph, artifactJsonOptions);
        }

        Console.WriteLine(
            $"runProofGraph=exported routeKind:{runProofRouteKind} samples:{selectedSamples.Length} surfaces:{proofTrace.SurfaceTouchCount} " +
            $"edges:{proofTrace.Edges.Count} routes:{proofGraph.Routes.Count} artifactDir:{artifactDirectory ?? "none"}");
    }
    else
    {
        Console.WriteLine($"runProofGraph=skipped reason:not_scored samples:{runProofSamples.Count}");
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
    semanticRecoveryEvents,
    proofGraphEvents);

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
    IReadOnlyList<SemanticRecoveryArtifact> semanticRecoveryEvents,
    IReadOnlyList<ProofGraphTraceArtifact> proofGraphEvents)
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
            proofGraphTraceCount = proofGraphEvents.Count,
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
    WriteJsonLines(Path.Combine(artifactDirectory, "proof-graph-traces.jsonl"), proofGraphEvents, jsonOptions);
    WriteJson(Path.Combine(artifactDirectory, "proof-graph-summary.json"), BuildProofGraphSummary(proofGraphEvents, scoreTick), jsonOptions);
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

static TraversalLabTickSample CreateRuntimeProofSample(
    SimpleLevel level,
    PlayerTeam team,
    PlayerEntity player,
    PlayerInputSnapshot input,
    int tick,
    string stepLabel)
{
    const float IntelMarkerSize = 24f;
    var opposingTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
    var enemyIntel = level.GetIntelBase(opposingTeam);
    var ownIntel = level.GetIntelBase(team);

    return new TraversalLabTickSample(
        Tick: tick,
        X: player.X,
        Y: player.Y,
        Bottom: player.Bottom,
        HorizontalSpeed: player.HorizontalSpeed,
        VerticalSpeed: player.VerticalSpeed,
        IsGrounded: player.IsGrounded,
        FacingDirectionX: player.FacingDirectionX,
        SupportedBelow: false,
        BlockedLeft: false,
        BlockedRight: false,
        InputLeft: input.Left,
        InputRight: input.Right,
        InputUp: input.Up,
        InputDown: input.Down,
        InputFirePrimary: input.FirePrimary,
        InputFireSecondary: input.FireSecondary,
        InputUseAbility: input.UseAbility,
        InputDropIntel: input.DropIntel,
        IsCarryingIntel: player.IsCarryingIntel,
        OverlapsEnemyIntelMarker: enemyIntel is { } enemy && player.IntersectsMarker(enemy.X, enemy.Y, IntelMarkerSize, IntelMarkerSize),
        OverlapsOwnIntelMarker: ownIntel is { } own && player.IntersectsMarker(own.X, own.Y, IntelMarkerSize, IntelMarkerSize),
        IsInsideBlockingTeamGate: player.IsInsideBlockingTeamGate(level, team),
        StepLabel: stepLabel);
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

        controllers[slot] = new BotBrainController();
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
            ObserveTraversalSoakBotPreAdvance(stats[slot], tick, bot, input, controller, thinkElapsed);
        }

        foreach (var (slot, input) in inputs)
        {
            world.TrySetNetworkPlayerInput(slot, input);
        }

        world.AdvanceOneTick();
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

    Console.WriteLine(
        $"soakResult=map:{world.Level.Name} area:{world.Level.MapAreaIndex} passed:{(passed ? 1 : 0)} reason:{reason} " +
        $"redCaps:{finalRedCaps} blueCaps:{finalBlueCaps} redFirstCapTick:{redFirstCapTick} blueFirstCapTick:{blueFirstCapTick} " +
        $"maxInertTicks:{finalMaxInertTicks} inertFailTicks:{inertFailTicks} oscillationEvents:{finalOscillationEvents} " +
        $"totalThinkMs:{totalThinkMs:0.000} avgThinkMsPerTick:{avgThinkMsPerTickFinal:0.000} " +
        $"totalTickMs:{totalTickMs:0.000} avgTickMsPerTick:{avgTickMsPerTickFinal:0.000} " +
        $"avgThinkMsPerBotTick:{avgThinkMsPerBotTick:0.0000} maxTickThinkMs:{maxTickThinkMs:0.000}");
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
    stats.LastTraversalTrace = FormatTraversalSoakControllerTrace(controller);
    stats.LastSemanticTrace = controller.LastSemanticRecoveryTrace;
    stats.ThinkTicks += 1;
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
        stats.OscillationWindowLowSpeedMoveFlips += MathF.Abs(bot.HorizontalSpeed) < 40f ? 1 : 0;
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
    var trace = string.Empty;
    AppendTrace(controller.LastSemanticRecoveryTrace);
    AppendTrace(controller.LastDirectDriveTrace);
    AppendTrace(controller.LastObjectiveTapeTrace);
    AppendTrace(controller.LastProofGraphTrace);
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
        return;
    }

    if (tick % stagnantWindowTicks == 0)
    {
        var windowMovement = Distance(stats.WindowX, stats.WindowY, bot.X, bot.Y);
        stats.RecentWindowMovement = windowMovement;
        stats.RecentStagnant = windowMovement < stagnantDistance;
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
            && stats.OscillationWindowLowSpeedMoveFlips >= oscillationFlips)
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
    }
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
        $"oscillationEvents:{stats.OscillationEvents} firstOscillationTick:{stats.FirstOscillationTick} " +
        $"zeroInput:{stats.ZeroInputTicks} flips:{stats.MoveFlips} lowSpeedFlips:{stats.LowSpeedMoveFlips} " +
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
    var ticks = GetRosterInt(rawOptions, "ticks", options.Ticks);
    var reportEvery = GetRosterInt(rawOptions, "report-every", Math.Max(30, options.ReportEveryTicks));
    var friendlyCount = GetRosterInt(rawOptions, "friendly-bots", 3);
    var enemyCount = GetRosterInt(rawOptions, "enemy-bots", 3);
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
    AppendPracticeRosterBots(world, graph, controllers, stats, ref nextSlot, localTeam, friendlyCount, classCycle, classOffset: 0);
    AppendPracticeRosterBots(world, graph, controllers, stats, ref nextSlot, enemyTeam, enemyCount, classCycle, classOffset: 3);

    Console.WriteLine(
        $"practiceRoster=map:{level.Name} area:{level.MapAreaIndex} mode:{world.MatchRules.Mode} " +
        $"localTeam:{localTeam} friendlyBots:{friendlyCount} enemyBots:{enemyCount} ticks:{ticks} strictProof:{(VerifiedNavProofGraphAssetStore.IsRequired() ? 1 : 0)}");
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
            var proofIdle = stats.Values.Count(static s => s.ProofIdleTicks > 0 && s.ProofTicks == 0);
            var caps = $"redCaps:{world.RedCaps - initialRedCaps} blueCaps:{world.BlueCaps - initialBlueCaps}";
            Console.WriteLine(
                $"practiceRosterTick={tick} {caps} stagnantBots:{stagnant} proofIdleOnly:{proofIdle} " +
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
    int classOffset)
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

        controllers[slot] = new BotBrainController(graph);
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
    stats.LastProofTrace = controller.LastProofGraphTrace;
    stats.LastDirectTrace = controller.LastDirectDriveTrace;
    stats.LastTapeTrace = controller.LastObjectiveTapeTrace;
    stats.LastPathCount = controller.CurrentPathCount;
    stats.LastPathIndex = controller.CurrentPathIndex;

    if (!string.IsNullOrWhiteSpace(controller.LastProofGraphTrace))
    {
        ObservePracticeRosterProofTrace(stats, controller.LastProofGraphTrace);
        if (controller.LastProofGraphTrace.StartsWith("proofGraph=idle", StringComparison.Ordinal))
        {
            stats.ProofIdleTicks += 1;
        }
        else if (controller.LastProofGraphTrace.StartsWith("proofGraph=abort", StringComparison.Ordinal))
        {
            stats.ProofAbortTicks += 1;
        }
        else
        {
            stats.ProofTicks += 1;
        }
    }
    else if (!string.IsNullOrWhiteSpace(controller.LastDirectDriveTrace))
    {
        ObservePracticeRosterDirectTrace(stats, controller.LastDirectDriveTrace);
        stats.DirectTicks += 1;
    }
    else if (!string.IsNullOrWhiteSpace(controller.LastObjectiveTapeTrace))
    {
        stats.TapeTicks += 1;
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

    if (controller.LastProofGraphTrace.StartsWith("proofGraph=abort", StringComparison.Ordinal))
    {
        stats.LastIssue = controller.LastProofGraphTrace;
    }
    else if (controller.LastProofGraphTrace.StartsWith("proofGraph=idle", StringComparison.Ordinal)
        && string.IsNullOrWhiteSpace(stats.LastIssue))
    {
        stats.LastIssue = controller.LastProofGraphTrace;
    }
}

static string FormatPracticeRosterBotTick(PracticeRosterBotStats stats)
{
    var authority = stats.LastProofTrace.Length > 0
        ? "proof"
        : stats.LastDirectTrace.Length > 0
            ? "direct"
            : stats.LastTapeTrace.Length > 0
                ? "tape"
                : stats.LastPathCount > 0
                    ? "graph"
                    : "none";
    var trace = stats.LastProofTrace.Length > 0
        ? stats.LastProofTrace
        : stats.LastDirectTrace.Length > 0
            ? stats.LastDirectTrace
            : stats.LastTapeTrace;
    if (trace.Length > 220)
    {
        trace = trace[..220];
    }

    return string.Create(
        CultureInfo.InvariantCulture,
        $"practiceBotTick=slot:{stats.Slot} {stats.Team} {stats.ClassId} pos=({stats.LastX:0.0},{stats.LastY:0.0}) " +
        $"speed=({stats.LastHorizontalSpeed:0.0},{stats.LastVerticalSpeed:0.0}) caps:{stats.LastCaps} " +
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
        $"proof:{stats.ProofTicks} proofIdle:{stats.ProofIdleTicks} proofAbort:{stats.ProofAbortTicks} proofAbortEvents:{SumValues(stats.ProofAbortByReason)} proofIdleEvents:{SumValues(stats.ProofIdleByReason)} " +
        $"direct:{stats.DirectTicks} directRejects:{SumValues(stats.DirectRejectByReason)} localMotionFailures:{SumValues(stats.LocalMotionFailureByReason)} graph:{stats.GraphTicks} tape:{stats.TapeTicks} " +
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
    var proofAbortEvents = entries.Sum(static s => SumValues(s.ProofAbortByReason));
    var proofIdleEvents = entries.Sum(static s => SumValues(s.ProofIdleByReason));
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
        $"proofAbortEvents:{proofAbortEvents} proofIdleEvents:{proofIdleEvents} directRejects:{directRejects} localMotionFailures:{localMotionFailures}");
}

static void ObservePracticeRosterProofTrace(PracticeRosterBotStats stats, string trace)
{
    var key = NormalizePracticeRosterTraceKey(trace);
    if (key == stats.LastProofTraceKey)
    {
        return;
    }

    stats.LastProofTraceKey = key;
    if (trace.StartsWith("proofGraph=abort", StringComparison.Ordinal))
    {
        IncrementCounter(stats.ProofAbortByReason, ExtractPracticeRosterReason(trace));
    }
    else if (trace.StartsWith("proofGraph=idle", StringComparison.Ordinal))
    {
        IncrementCounter(stats.ProofIdleByReason, ExtractPracticeRosterReason(trace));
    }
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

void RunProofGraphAudit(IReadOnlyDictionary<string, string> rawOptions)
{
    var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
    var proofGraphDirectory = rawOptions.TryGetValue("proof-graph-dir", out var configuredDirectory)
        && !string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.GetFullPath(configuredDirectory)
            : Path.Combine(repoRoot, "Core", "Content", "BotBrainProofGraphs");
    if (!Directory.Exists(proofGraphDirectory))
    {
        throw new DirectoryNotFoundException($"Proof graph directory not found: {proofGraphDirectory}");
    }

    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    var files = Directory
        .EnumerateFiles(proofGraphDirectory, "*.verified-proof-graph.json", SearchOption.TopDirectoryOnly)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var assetCount = 0;
    var routeCount = 0;
    var exactOrActionOnlyRoutes = 0;
    var laneBackedRoutes = 0;
    var missingRouteRoutes = 0;
    var totalBytes = 0L;

    Console.WriteLine($"proofGraphAudit=directory:{proofGraphDirectory} files:{files.Length}");
    foreach (var file in files)
    {
        var json = File.ReadAllText(file);
        var asset = JsonSerializer.Deserialize<VerifiedNavProofGraphAsset>(json, jsonOptions)
            ?? throw new InvalidOperationException($"Could not parse proof graph asset: {file}");
        assetCount += 1;
        var fileBytes = new FileInfo(file).Length;
        totalBytes += fileBytes;

        var assetExactOrActionOnlyRoutes = 0;
        var assetLaneBackedRoutes = 0;
        var assetMissingRouteRoutes = 0;
        foreach (var route in asset.Routes)
        {
            routeCount += 1;
            var actionTicks = CountActionTicks(route.Actions);
            var terminalActionTicks = CountActionTicks(route.TerminalActions);
            var edgeActionTicks = route.EdgeIds
                .Select(edgeId => asset.Edges.FirstOrDefault(edge => edge.Id == edgeId))
                .Where(edge => edge is not null)
                .Sum(edge => CountActionTicks(edge!.Actions));
            var laneActionTicks = route.LaneSegmentIds
                .Select(segmentId => asset.LaneSegments.FirstOrDefault(segment => segment.Id == segmentId))
                .Where(segment => segment is not null)
                .Sum(segment => CountActionTicks(segment!.Actions));
            var classification = ClassifyProofRoute(route, actionTicks, edgeActionTicks, terminalActionTicks);
            switch (classification)
            {
                case "lane-backed":
                    laneBackedRoutes += 1;
                    assetLaneBackedRoutes += 1;
                    break;
                case "missing-route":
                    missingRouteRoutes += 1;
                    assetMissingRouteRoutes += 1;
                    break;
                default:
                    exactOrActionOnlyRoutes += 1;
                    assetExactOrActionOnlyRoutes += 1;
                    break;
            }

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"proofGraphRoute=map:{asset.LevelName} area:{asset.MapAreaIndex} team:{asset.Team} class:{asset.ClassId} " +
                    $"kind:{route.Kind} source:{route.Source} edges:{route.EdgeIds.Count} lanes:{route.LaneSegmentIds.Count} " +
                    $"routeActionRuns:{route.Actions.Count} routeActionTicks:{actionTicks} edgeActionTicks:{edgeActionTicks} " +
                    $"laneActionTicks:{laneActionTicks} terminalActionTicks:{terminalActionTicks} " +
                    $"start=({route.StartX:0.0},{route.StartBottom:0.0}) classification:{classification}"));
        }

        Console.WriteLine(
            $"proofGraphAsset=file:{Path.GetFileName(file)} map:{asset.LevelName} area:{asset.MapAreaIndex} " +
            $"team:{asset.Team} class:{asset.ClassId} routes:{asset.Routes.Count} edges:{asset.Edges.Count} " +
            $"lanes:{asset.LaneSegments.Count} links:{asset.Links.Count} surfaces:{asset.Surfaces.Count} " +
            $"laneBackedRoutes:{assetLaneBackedRoutes} exactOrActionOnlyRoutes:{assetExactOrActionOnlyRoutes} " +
            $"missingRouteRoutes:{assetMissingRouteRoutes} bytes:{fileBytes}");
    }

    Console.WriteLine(
        $"proofGraphAuditSummary=assets:{assetCount} routes:{routeCount} laneBackedRoutes:{laneBackedRoutes} " +
        $"exactOrActionOnlyRoutes:{exactOrActionOnlyRoutes} missingRouteRoutes:{missingRouteRoutes} bytes:{totalBytes}");
    Console.WriteLine("proofGraphAuditGate=robustnessMetadata:missing installedEnvelopeValidation:unknown routeActionOnlyIsNotRobust:true");
}

string ClassifyProofRoute(
    VerifiedNavProofGraphRoute route,
    int routeActionTicks,
    int edgeActionTicks,
    int terminalActionTicks)
{
    if (route.EdgeIds.Count == 0)
    {
        return "missing-route";
    }

    if (route.LaneSegmentIds.Count > 0)
    {
        return "lane-backed";
    }

    if (routeActionTicks > 0)
    {
        return "route-action-only";
    }

    if (edgeActionTicks > 0 || terminalActionTicks > 0)
    {
        return "edge-action-only";
    }

    return "surface-only";
}

int CountActionTicks(IReadOnlyList<VerifiedNavProofGraphActionRun> actions)
{
    var ticks = 0;
    for (var index = 0; index < actions.Count; index += 1)
    {
        ticks += actions[index].Ticks;
    }

    return ticks;
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
    if (brain.LastProofGraphTrace.StartsWith("proofGraph=route:", StringComparison.Ordinal)
        || brain.LastProofGraphTrace.StartsWith("proofGraph=lane", StringComparison.Ordinal)
        || brain.LastProofGraphTrace.StartsWith("proofGraph=terminal", StringComparison.Ordinal)
        || brain.LastProofGraphTrace.StartsWith("proofGraph=routeAction", StringComparison.Ordinal))
    {
        return "proof-graph";
    }

    if (!string.IsNullOrWhiteSpace(brain.LastDirectDriveTrace))
    {
        return "direct-drive";
    }

    if (brain.LastObjectiveTapeTrace.StartsWith("objectiveTape=tape:", StringComparison.Ordinal))
    {
        return "tape";
    }

    if (brain.CurrentPathCount > 0)
    {
        return "graph";
    }

    if (!string.IsNullOrWhiteSpace(brain.LastSemanticRecoveryTrace))
    {
        return "semantic-recovery";
    }

    if (brain.LastObjectiveTapeTrace.StartsWith("objectiveTape=idle", StringComparison.Ordinal))
    {
        return "tape-idle";
    }

    if (brain.LastProofGraphTrace.StartsWith("proofGraph=idle", StringComparison.Ordinal))
    {
        return "proof-graph-idle";
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

static object BuildProofGraphSummary(
    IReadOnlyList<ProofGraphTraceArtifact> events,
    int scoreTick)
{
    var scoredEvents = scoreTick >= 0
        ? events.Where(e => e.Tick <= scoreTick).ToArray()
        : events.ToArray();
    var routeEvents = scoredEvents
        .Where(static e => e.Trace.StartsWith("proofGraph=route:", StringComparison.Ordinal)
            || e.Trace.StartsWith("proofGraph=lane", StringComparison.Ordinal)
            || e.Trace.StartsWith("proofGraph=terminal", StringComparison.Ordinal)
            || e.Trace.StartsWith("proofGraph=routeAction", StringComparison.Ordinal))
        .ToArray();
    var selectedEvents = scoredEvents
        .Where(static e => e.Trace.StartsWith("proofGraph=selected", StringComparison.Ordinal))
        .ToArray();
    var abortEvents = scoredEvents
        .Where(static e => e.Trace.StartsWith("proofGraph=abort", StringComparison.Ordinal))
        .ToArray();
    var edgeTicks = routeEvents
        .Select(static e => ExtractProofGraphEdgeId(e.Trace))
        .Where(static edge => edge.Length > 0)
        .GroupBy(static edge => edge)
        .Select(static group => new { edge = group.Key, ticks = group.Count() })
        .OrderByDescending(static item => item.ticks)
        .ThenBy(static item => item.edge)
        .ToArray();

    return new
    {
        scoreTick,
        eventCount = scoredEvents.Length,
        selectedRoutes = selectedEvents.Length,
        routeTicks = routeEvents.Length,
        aborts = abortEvents.Length,
        fallbackReasons = abortEvents
            .Select(static e => ExtractProofGraphAbortReason(e.Trace))
            .Where(static reason => reason.Length > 0)
            .GroupBy(static reason => reason)
            .Select(static group => new { reason = group.Key, count = group.Count() })
            .OrderByDescending(static item => item.count)
            .ThenBy(static item => item.reason)
            .ToArray(),
        edgeTicks,
    };
}

static string ExtractProofGraphEdgeId(string trace)
{
    const string marker = " edge:";
    var start = trace.IndexOf(marker, StringComparison.Ordinal);
    if (start < 0)
    {
        return string.Empty;
    }

    start += marker.Length;
    var end = trace.IndexOf(' ', start);
    return end > start ? trace[start..end] : trace[start..];
}

static string ExtractProofGraphAbortReason(string trace)
{
    const string marker = " reason:";
    var start = trace.IndexOf(marker, StringComparison.Ordinal);
    if (start < 0)
    {
        return string.Empty;
    }

    start += marker.Length;
    var end = trace.IndexOf(' ', start);
    return end > start ? trace[start..end] : trace[start..];
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
    string ProofGraphLabel,
    string TapeName,
    string DirectDriveLabel,
    string SemanticRecoveryReason);

internal sealed record ProofGraphTraceArtifact(
    int Tick,
    string Trace,
    bool CarryingIntel,
    float X,
    float Y,
    float Bottom,
    bool Grounded);

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
    private string _proofGraphLabel = string.Empty;
    private string _tapeName = string.Empty;
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
        string proofGraphTrace,
        string objectiveTapeTrace,
        string directDriveTrace,
        string semanticRecoveryTrace)
    {
        var proofGraphLabel = ExtractProofGraphLabel(proofGraphTrace);
        var tapeName = ExtractTapeName(objectiveTapeTrace);
        var directDriveLabel = ExtractDirectDriveLabel(directDriveTrace);
        var semanticRecoveryReason = ExtractSemanticRecoveryReason(semanticRecoveryTrace);
        var key = $"{authority}|{carryingIntel}|{proofGraphLabel}|{tapeName}|{directDriveLabel}|{semanticRecoveryReason}";
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
            _proofGraphLabel = proofGraphLabel;
            _tapeName = tapeName;
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
            ProofGraphLabel: _proofGraphLabel,
            TapeName: _tapeName,
            DirectDriveLabel: _directDriveLabel,
            SemanticRecoveryReason: _semanticRecoveryReason));
    }

    private static string ExtractTapeName(string trace)
    {
        const string prefix = "objectiveTape=tape:";
        if (!trace.StartsWith(prefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var end = trace.IndexOf(' ', prefix.Length);
        return end > prefix.Length
            ? trace[prefix.Length..end]
            : trace[prefix.Length..];
    }

    private static string ExtractProofGraphLabel(string trace)
    {
        var prefix = "proofGraph=route:";
        if (trace.StartsWith("proofGraph=lane", StringComparison.Ordinal))
        {
            prefix = "proofGraph=lane route:";
        }
        else if (trace.StartsWith("proofGraph=terminal", StringComparison.Ordinal))
        {
            prefix = "proofGraph=terminal route:";
        }

        if (!trace.StartsWith(prefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var edgeIndexMarker = trace.IndexOf(" edgeIndex:", StringComparison.Ordinal);
        var edgeMarker = trace.IndexOf(" edge:", StringComparison.Ordinal);
        var routeEnd = edgeIndexMarker > prefix.Length
            ? edgeIndexMarker
            : edgeMarker > prefix.Length
                ? edgeMarker
                : trace.Length;
        var route = trace[prefix.Length..routeEnd];
        if (edgeMarker <= prefix.Length)
        {
            return route;
        }

        var edgeStart = edgeMarker + " edge:".Length;
        var edgeEnd = trace.IndexOf(' ', edgeStart);
        var edge = edgeEnd > edgeStart ? trace[edgeStart..edgeEnd] : trace[edgeStart..];
        return $"{route}:{edge}";
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

    private static string ExtractProofGraphLabel(string trace)
    {
        var prefix = "proofGraph=route:";
        if (trace.StartsWith("proofGraph=lane", StringComparison.Ordinal))
        {
            prefix = "proofGraph=lane route:";
        }
        else if (trace.StartsWith("proofGraph=terminal", StringComparison.Ordinal))
        {
            prefix = "proofGraph=terminal route:";
        }

        if (!trace.StartsWith(prefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var edgeMarker = trace.IndexOf(" edge:", StringComparison.Ordinal);
        var edgeIndexMarker = trace.IndexOf(" edgeIndex:", StringComparison.Ordinal);
        if (edgeMarker <= prefix.Length)
        {
            return edgeIndexMarker > prefix.Length
                ? trace[prefix.Length..edgeIndexMarker]
                : trace[prefix.Length..];
        }

        var route = edgeIndexMarker > prefix.Length
            ? trace[prefix.Length..edgeIndexMarker]
            : trace[prefix.Length..edgeMarker];
        var edgeEnd = trace.IndexOf(' ', edgeMarker + 1);
        var edge = edgeEnd > edgeMarker
            ? trace[(edgeMarker + " edge:".Length)..edgeEnd]
            : trace[(edgeMarker + " edge:".Length)..];
        return $"{route}:{edge}";
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

    public static void BuildVerifiedProofGraph(
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
            : Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "verified-nav", $"{mapName}-{area}-{team}-{classId}-proofgraph");
        Directory.CreateDirectory(artifactDirectory);

        var traceOptions = new JsonSerializerOptions(outputJsonOptions)
        {
            PropertyNameCaseInsensitive = true,
        };
        VerifiedNavProofGraphAsset? proofGraph = null;
        if (rawOptions.TryGetValue("base-proof-graph", out var baseProofGraphPath)
            && !string.IsNullOrWhiteSpace(baseProofGraphPath))
        {
            proofGraph = JsonSerializer.Deserialize<VerifiedNavProofGraphAsset>(
                File.ReadAllText(Path.GetFullPath(baseProofGraphPath)),
                traceOptions) ?? throw new InvalidOperationException($"Could not read base proof graph '{baseProofGraphPath}'.");
        }

        var routeTraces = new List<(VerifiedNavProofRouteKind Kind, VerifiedNavProofTraceReport Trace)>();
        var pickupTracePaths = ReadProofTracePathList(rawOptions, "pickup-trace", "pickup-traces");
        foreach (var pickupTracePath in pickupTracePaths)
        {
            routeTraces.Add((VerifiedNavProofRouteKind.Pickup, ReadProofTrace(pickupTracePath)));
        }

        var returnTracePaths = ReadProofTracePathList(rawOptions, "return-trace", "return-traces");
        foreach (var returnTracePath in returnTracePaths)
        {
            routeTraces.Add((VerifiedNavProofRouteKind.Return, ReadProofTrace(returnTracePath)));
        }

        if (routeTraces.Count == 0 && proofGraph is null)
        {
            throw new InvalidOperationException("--build-proof-graph requires --base-proof-graph, --pickup-trace/--pickup-traces, or --return-trace/--return-traces.");
        }

        var level = SimpleLevelFactory.CreateImportedLevel(mapName, area)
            ?? throw new InvalidOperationException($"Could not load map '{mapName}' area {area}.");
        var candidateGraph = VerifiedNavCandidateBuilder.Build(level, new VerifiedNavBuildOptions
        {
            Team = team,
            ClassId = classId,
            DropHorizontalReach = ReadFloatOption(rawOptions, "drop-horizontal-reach", 360f),
            JumpHorizontalReach = ReadFloatOption(rawOptions, "jump-horizontal-reach", 360f),
        });
        if (routeTraces.Count > 0)
        {
            proofGraph = VerifiedNavProofGraphBuilder.Build(
                candidateGraph,
                routeTraces,
                new VerifiedNavProofGraphBuildOptions
                {
                    RouteActionOnlyKinds = BuildRouteActionOnlyKinds(rawOptions),
                });
        }

        var explorationReports = new List<VerifiedNavExplorationReport>();
        var explorationReportPaths = ReadProofTracePathList(rawOptions, "exploration-report", "exploration-reports");
        foreach (var explorationReportPath in explorationReportPaths)
        {
            explorationReports.Add(JsonSerializer.Deserialize<VerifiedNavExplorationReport>(
                File.ReadAllText(explorationReportPath),
                traceOptions) ?? throw new InvalidOperationException($"Could not read exploration report '{explorationReportPath}'."));
        }

        if (explorationReports.Count > 0)
        {
            VerifiedNavProofGraphBuilder.AddExplorationLinks(
                proofGraph!,
                explorationReports,
                ReadIntOption(rawOptions, "exploration-links-per-pair", 3));
            var reachable = explorationReports
                .SelectMany(static report => report.ReachableSurfaceIds)
                .Distinct()
                .Count();
            Console.WriteLine(
                $"verifiedProofGraphExplorationLinks reports={explorationReports.Count} reachableSurfaces={reachable}/{candidateGraph.Surfaces.Count} links={proofGraph!.Links.Count}");
        }

        var outputPath = Path.Combine(artifactDirectory, "verified-proof-graph.json");
        File.WriteAllText(outputPath, JsonSerializer.Serialize(proofGraph!, outputJsonOptions));

        Console.WriteLine(
            $"verifiedProofGraph map={proofGraph!.LevelName} area={proofGraph.MapAreaIndex} team={proofGraph.Team} class={proofGraph.ClassId} surfaces={proofGraph.Surfaces.Count} routes={proofGraph.Routes.Count} edges={proofGraph.Edges.Count} links={proofGraph.Links.Count}");
        foreach (var route in proofGraph.Routes)
        {
            Console.WriteLine(
                $"verifiedProofRoute kind={route.Kind} samples={route.SampleCount} surfaces={route.SurfaceSequence.Count} edges={route.EdgeIds.Count} sequence={string.Join(',', route.SurfaceSequence)}");
        }

        Console.WriteLine($"verifiedProofGraphArtifact={outputPath}");

        VerifiedNavProofTraceReport ReadProofTrace(string tracePath)
        {
            return JsonSerializer.Deserialize<VerifiedNavProofTraceReport>(
                File.ReadAllText(tracePath),
                traceOptions) ?? throw new InvalidOperationException($"Could not read proof trace '{tracePath}'.");
        }
    }

    public static void ExtractVerifiedProofTraceFromPlannerSteps(
        Dictionary<string, string> rawOptions,
        JsonSerializerOptions outputJsonOptions)
    {
        var mapName = rawOptions.TryGetValue("map", out var mapText) ? mapText : "Truefort";
        var area = ReadIntOption(rawOptions, "area", 1);
        var team = ReadEnumOption(rawOptions, "team", PlayerTeam.Red);
        var classId = ReadEnumOption(rawOptions, "class", PlayerClass.Pyro);
        var stepsPath = rawOptions.TryGetValue("planner-steps", out var stepsText)
            ? Path.GetFullPath(stepsText)
            : throw new InvalidOperationException("--extract-proof-trace requires --planner-steps.");
        var tapePath = rawOptions.TryGetValue("candidate-tape", out var tapeText)
            ? Path.GetFullPath(tapeText)
            : throw new InvalidOperationException("--extract-proof-trace requires --candidate-tape.");
        var artifactDirectory = rawOptions.TryGetValue("artifacts-dir", out var artifactsText) && !string.IsNullOrWhiteSpace(artifactsText)
            ? Path.GetFullPath(artifactsText)
            : Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "verified-nav", $"{mapName}-{team}-{classId}-extracted-proof-trace");

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(repoRoot, "Core", "Content"));
        var level = SimpleLevelFactory.CreateImportedLevel(mapName, area)
            ?? throw new InvalidOperationException($"Could not load map '{mapName}' area {area}.");
        var readOptions = new JsonSerializerOptions(outputJsonOptions)
        {
            PropertyNameCaseInsensitive = true,
        };
        var steps = JsonSerializer.Deserialize<List<TraversalLabInputStep>>(
            File.ReadAllText(stepsPath),
            readOptions) ?? throw new InvalidOperationException($"Could not read planner steps '{stepsPath}'.");
        var tape = JsonSerializer.Deserialize<BotBrainObjectiveTapeEntry>(
            File.ReadAllText(tapePath),
            readOptions) ?? throw new InvalidOperationException($"Could not read candidate tape '{tapePath}'.");
        var segment = tape.Segments.FirstOrDefault(static segment => segment.Samples.Count > 0)
            ?? throw new InvalidOperationException($"Candidate tape '{tapePath}' has no samples.");
        var firstSample = segment.Samples[0];
        var carryingIntel = segment.RequiresCarryingIntel || firstSample.IsCarryingIntel;
        var opposingTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var targetTeam = carryingIntel ? team : opposingTeam;
        var targetBase = level.GetIntelBase(targetTeam)
            ?? throw new InvalidOperationException("Proof trace extraction requires a target intel marker.");
        var firstMoveDirection = steps
            .Select(static step => step.Right == step.Left ? 0f : step.Right ? 1f : -1f)
            .FirstOrDefault(static direction => direction != 0f);
        var scenario = new TraversalLabScenario
        {
            Name = $"{tape.Name}.extracted-proof-trace",
            LevelName = mapName,
            MapAreaIndex = area,
            Team = team,
            ClassId = classId,
            Start = new TraversalLabStartState
            {
                X = firstSample.X,
                Bottom = firstSample.Bottom,
                HorizontalSpeed = firstSample.HorizontalSpeed,
                VerticalSpeed = firstSample.VerticalSpeed,
                IsGrounded = firstSample.IsGrounded,
                IsCarryingIntel = carryingIntel,
                FacingDirectionX = firstMoveDirection == 0f
                    ? team == PlayerTeam.Red ? -1f : 1f
                    : firstMoveDirection,
            },
            Steps = steps,
            MaxTicks = steps.Sum(static step => Math.Max(0, step.DurationTicks)),
            TraceEveryTicks = 1,
            Expectation = new TraversalLabExpectation
            {
                MustEverOverlapOwnIntelMarker = carryingIntel,
                MustOverlapEnemyIntelMarker = carryingIntel ? null : true,
            },
        };
        var validation = TraversalLabRunner.Run(scenario);
        var bestCase = validation.Cases
            .Where(static candidate => candidate.Passed)
            .OrderBy(static candidate => candidate.ExecutedTicks)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Extracted proof trace scenario failed validation. passed={validation.PassedCount} failed={validation.FailedCount}");
        var trimmedSamples = TrimTraversalSamplesAtIntel(level, targetTeam, classId, bestCase.Samples);
        var verifiedGraph = VerifiedNavCandidateBuilder.Build(level, new VerifiedNavBuildOptions
        {
            Team = team,
            ClassId = classId,
            DropHorizontalReach = ReadFloatOption(rawOptions, "drop-horizontal-reach", 360f),
            JumpHorizontalReach = ReadFloatOption(rawOptions, "jump-horizontal-reach", 360f),
        });
        var proofTrace = VerifiedNavProofTraceExtractor.Extract(
            verifiedGraph,
            trimmedSamples,
            carryingIntel ? "MotionProofReturnPlanner" : "MotionProofPickupPlanner");

        Directory.CreateDirectory(artifactDirectory);
        File.WriteAllText(
            Path.Combine(artifactDirectory, "scenario.json"),
            JsonSerializer.Serialize(scenario, outputJsonOptions));
        File.WriteAllText(
            Path.Combine(artifactDirectory, "validation.json"),
            JsonSerializer.Serialize(new
            {
                validation.Cases.Count,
                validation.PassedCount,
                validation.FailedCount,
                validation.Passed,
                bestCase.ExecutedTicks,
                bestCase.FinalX,
                bestCase.FinalBottom,
                targetBase.X,
                targetBase.Y,
            }, outputJsonOptions));
        File.WriteAllText(
            Path.Combine(artifactDirectory, "proof-trace-edges.json"),
            JsonSerializer.Serialize(proofTrace, outputJsonOptions));

        Console.WriteLine(
            $"extractedProofTrace map={mapName} area={area} team={team} class={classId} carrying={(carryingIntel ? 1 : 0)} " +
            $"ticks={bestCase.ExecutedTicks} samples={trimmedSamples.Count} surfaces={proofTrace.SurfaceTouchCount} edges={proofTrace.Edges.Count} " +
            $"actions={proofTrace.Actions.Count} artifactDir={artifactDirectory}");
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
            TrimTraversalSamplesAtOwnIntel(level, team, classId, bestCase.Samples));
        var tapePath = BotBrainObjectiveTapeStore.UpsertTape(level, entry);
        Console.WriteLine($"objectiveTape={tapePath}");
        Console.WriteLine($"selected={bestScenario.Name} ticks={bestCase.ExecutedTicks} rawSamples={bestCase.Samples.Count} tapeSamples={entry.Segments.Sum(static segment => segment.Samples.Count)}");
    }

    public static void GenerateProofReturnTape(Dictionary<string, string> rawOptions, JsonSerializerOptions outputJsonOptions)
    {
        var mapName = rawOptions.TryGetValue("map", out var mapText) ? mapText : "Truefort";
        var area = ReadIntOption(rawOptions, "area", 1);
        var team = ReadEnumOption(rawOptions, "team", PlayerTeam.Red);
        var classId = ReadEnumOption(rawOptions, "class", PlayerClass.Soldier);
        var maxTicks = ReadIntOption(rawOptions, "ticks", 3600);
        var direction = ReadFloatOption(rawOptions, "direction", team == PlayerTeam.Red ? -1f : 1f) < 0f ? -1f : 1f;
        var accept = rawOptions.TryGetValue("accept-proof-return", out var acceptText)
            && bool.TryParse(acceptText, out var parsedAccept)
            && parsedAccept;
        var tapeName = rawOptions.TryGetValue("tape-name", out var providedTapeName)
            ? providedTapeName
            : $"MotionProof.{mapName}.{team}.{classId}.GeneratedReturn";
        var artifactDirectory = rawOptions.TryGetValue("artifacts-dir", out var artifactsText) && !string.IsNullOrWhiteSpace(artifactsText)
            ? Path.GetFullPath(artifactsText)
            : Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "botbrain-generated-proof-return", $"{mapName}-{team}-{classId}");

        var compileRepoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(compileRepoRoot, "Core", "Content"));
        var level = SimpleLevelFactory.CreateImportedLevel(mapName, area)
            ?? throw new InvalidOperationException($"Could not load map '{mapName}' area {area}.");

        var opposingTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var enemyBase = level.GetIntelBase(opposingTeam)
            ?? throw new InvalidOperationException("Proof return generation requires an opposing intel base.");
        var ownBase = level.GetIntelBase(team)
            ?? throw new InvalidOperationException("Proof return generation requires an own intel base.");
        var startX = ReadFloatOption(rawOptions, "start-x", enemyBase.X);
        var startBottom = ReadFloatOption(rawOptions, "start-bottom", enemyBase.Y + 24f);

        var search = SearchReturnProofCandidate(
            mapName,
            area,
            team,
            classId,
            startX,
            startBottom,
            direction,
            maxTicks,
            tapeName,
            rawOptions,
            level);

        Directory.CreateDirectory(artifactDirectory);
        File.WriteAllText(
            Path.Combine(artifactDirectory, "search-summary.json"),
            JsonSerializer.Serialize(search.Summary, outputJsonOptions));

        Console.WriteLine(
            $"proofReturnSearch map={mapName} area={area} team={team} class={classId} attempts={search.Summary.Attempts} " +
            $"passed={(search.BestCase is null ? 0 : 1)} start=({startX:0.0},{startBottom:0.0}) ownBase=({ownBase.X:0.0},{ownBase.Y:0.0})");

        if (search.BestScenario is null || search.BestCase is null)
        {
            if (search.BestFailedCase is not null && search.BestFailedScenario is not null)
            {
                Console.WriteLine(
                    $"closestFailed={search.BestFailedScenario.Name} dist={search.Summary.BestFailedDistance:0.0} " +
                    $"final=({search.BestFailedCase.FinalX:0.0},{search.BestFailedCase.FinalY:0.0}) bottom={search.BestFailedCase.FinalBottom:0.0} " +
                    $"rangeX=({search.BestFailedCase.MinX:0.0},{search.BestFailedCase.MaxX:0.0}) rangeBottom=({search.BestFailedCase.MinBottom:0.0},{search.BestFailedCase.MaxBottom:0.0}) " +
                    $"reason={search.BestFailedCase.FailureReason}");
            }

            Console.WriteLine("proofReturn=rejected reason:no_successful_candidate");
            return;
        }

        var trimmedSamples = TrimTraversalSamplesAtOwnIntel(level, team, classId, search.BestCase.Samples);
        var entry = BuildObjectiveTapeFromTraversalSamples(tapeName, search.BestScenario, trimmedSamples);
        var validationScenario = BuildReturnProofValidationScenario(search.BestScenario, rawOptions);
        var validation = TraversalLabRunner.Run(validationScenario);
        var validationPassed = validation.Passed;
        var validationFailures = validation.Cases
            .Where(static candidate => !candidate.Passed)
            .Select(static candidate => new ReturnProofValidationFailure(
                candidate.Variant.StartXOffset,
                candidate.Variant.StartBottomOffset,
                candidate.Variant.StartHorizontalSpeedOffset,
                candidate.Variant.StartVerticalSpeedOffset,
                candidate.Variant.StartGrounded,
                candidate.FailureReason,
                candidate.FinalX,
                candidate.FinalY,
                candidate.FinalBottom))
            .ToArray();
        var validationArtifact = new ReturnProofValidationArtifact(
            validation.Cases.Count,
            validation.PassedCount,
            validation.FailedCount,
            validationPassed,
            validationFailures);

        File.WriteAllText(
            Path.Combine(artifactDirectory, "candidate-tape.json"),
            JsonSerializer.Serialize(entry, outputJsonOptions));
        File.WriteAllText(
            Path.Combine(artifactDirectory, "validation.json"),
            JsonSerializer.Serialize(validationArtifact, outputJsonOptions));

        Console.WriteLine(
            $"proofReturnCandidate={search.BestScenario.Name} ticks={search.BestCase.ExecutedTicks} rawSamples={search.BestCase.Samples.Count} " +
            $"trimmedSamples={trimmedSamples.Count} tapeSamples={entry.Segments.Sum(static segment => segment.Samples.Count)}");
        Console.WriteLine(
            $"proofReturnValidation passed={(validationPassed ? 1 : 0)} cases={validation.Cases.Count} passedCases={validation.PassedCount} failedCases={validation.FailedCount}");
        foreach (var failure in validationFailures.Take(8))
        {
            Console.WriteLine(
                $"validationFailure xOff:{failure.StartXOffset:0.0} bottomOff:{failure.StartBottomOffset:0.0} hSpeed:{failure.StartHorizontalSpeedOffset:0.0} " +
                $"vSpeed:{failure.StartVerticalSpeedOffset:0.0} grounded:{(failure.StartGrounded ? 1 : 0)} final=({failure.FinalX:0.0},{failure.FinalY:0.0}) bottom={failure.FinalBottom:0.0} reason={failure.Reason}");
        }

        if (!validationPassed)
        {
            Console.WriteLine($"proofReturn=rejected reason:validation_failed artifactDir={artifactDirectory}");
            return;
        }

        if (!accept)
        {
            Console.WriteLine($"proofReturn=accepted_candidate artifactDir={artifactDirectory} install=skipped reason:missing_accept_flag");
            return;
        }

        var tapePath = BotBrainObjectiveTapeStore.UpsertTape(level, entry);
        Console.WriteLine($"proofReturn=installed path={tapePath} artifactDir={artifactDirectory}");
    }

    public static void PlanProofReturnTape(Dictionary<string, string> rawOptions, JsonSerializerOptions outputJsonOptions)
    {
        const float IntelMarkerSize = 24f;
        var planPickup = rawOptions.TryGetValue("plan-proof-pickup", out var planPickupText)
            && bool.TryParse(planPickupText, out var parsedPlanPickup)
            && parsedPlanPickup;
        var planObjective = rawOptions.TryGetValue("plan-proof-objective", out var planObjectiveText)
            && bool.TryParse(planObjectiveText, out var parsedPlanObjective)
            && parsedPlanObjective;
        var mapName = rawOptions.TryGetValue("map", out var mapText) ? mapText : "Truefort";
        var area = ReadIntOption(rawOptions, "area", 1);
        var team = ReadEnumOption(rawOptions, "team", PlayerTeam.Red);
        var classId = ReadEnumOption(rawOptions, "class", PlayerClass.Pyro);
        var maxTicks = ReadIntOption(rawOptions, "ticks", 3600);
        var maxExpansions = ReadIntOption(rawOptions, "max-expansions", 25000);
        var skipSolutions = ReadIntOption(rawOptions, "planner-skip-solutions", 0);
        var maxValidatedSolutions = ReadIntOption(rawOptions, "planner-max-candidates", 16);
        var searchValidationEnvelope = ReadBoolOption(rawOptions, "planner-search-validation-envelope", false);
        var accept = rawOptions.TryGetValue("accept-proof-return", out var acceptText)
            && bool.TryParse(acceptText, out var parsedAccept)
            && parsedAccept;
        var tapeName = rawOptions.TryGetValue("tape-name", out var providedTapeName)
            ? providedTapeName
            : planObjective
                ? $"MotionProof.{mapName}.{team}.{classId}.PlannedObjective"
            : planPickup
                ? $"MotionProof.{mapName}.{team}.{classId}.PlannedPickup"
                : $"MotionProof.{mapName}.{team}.{classId}.PlannedReturn";
        var artifactDirectory = rawOptions.TryGetValue("artifacts-dir", out var artifactsText) && !string.IsNullOrWhiteSpace(artifactsText)
            ? Path.GetFullPath(artifactsText)
            : Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "botbrain-planned-proof-return", $"{mapName}-{team}-{classId}");

        var compileRepoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(compileRepoRoot, "Core", "Content"));
        var level = SimpleLevelFactory.CreateImportedLevel(mapName, area)
            ?? throw new InvalidOperationException($"Could not load map '{mapName}' area {area}.");
        var opposingTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var enemyBase = level.GetIntelBase(opposingTeam);
        var ownBase = level.GetIntelBase(team);
        if (!planObjective && !enemyBase.HasValue)
        {
            throw new InvalidOperationException("Planner proof return requires an opposing intel base.");
        }
        if (!planObjective && !ownBase.HasValue)
        {
            throw new InvalidOperationException("Planner proof return requires an own intel base.");
        }

        var targetBase = planObjective
            ? (
                X: ReadFloatOption(rawOptions, "target-x", float.NaN),
                Y: ReadFloatOption(rawOptions, "target-y", float.NaN))
            : planPickup
                ? (enemyBase!.Value.X, enemyBase.Value.Y)
                : (ownBase!.Value.X, ownBase.Value.Y);
        if (planObjective && (!float.IsFinite(targetBase.X) || !float.IsFinite(targetBase.Y)))
        {
            throw new InvalidOperationException("--plan-proof-objective requires --target-x and --target-y.");
        }

        var targetRadiusX = ReadFloatOption(rawOptions, "target-radius-x", planObjective ? 96f : IntelMarkerSize);
        var targetRadiusBottom = ReadFloatOption(rawOptions, "target-radius-bottom", planObjective ? 96f : IntelMarkerSize);
        var classDefinition = CharacterClassCatalog.GetDefinition(classId);
        var spawn = level.GetSpawn(team, 0);
        var artifactStartState = TryReadRuntimeStateOption(rawOptions, outputJsonOptions);
        var startX = ReadFloatOption(rawOptions, "start-x", artifactStartState?.X ?? (planPickup || planObjective ? spawn.X : enemyBase!.Value.X));
        var startBottom = ReadFloatOption(rawOptions, "start-bottom", artifactStartState?.Bottom ?? (planPickup || planObjective ? spawn.Y + classDefinition.CollisionBottom : enemyBase!.Value.Y + 24f));
        var startHorizontalSpeed = ReadFloatOption(rawOptions, "start-horizontal-speed", artifactStartState?.HorizontalSpeed ?? 0f);
        var startVerticalSpeed = ReadFloatOption(rawOptions, "start-vertical-speed", artifactStartState?.VerticalSpeed ?? 0f);
        var startGrounded = ReadBoolOption(rawOptions, "start-grounded", artifactStartState?.IsGrounded ?? true);
        var startAirJumps = rawOptions.TryGetValue("start-air-jumps", out var startAirJumpsText)
            && int.TryParse(startAirJumpsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStartAirJumps)
                ? parsedStartAirJumps
                : artifactStartState?.RemainingAirJumps;
        var startFacing = ReadFloatOption(rawOptions, "direction", artifactStartState?.FacingDirectionX ?? (team == PlayerTeam.Red ? -1f : 1f)) < 0f ? -1f : 1f;
        var rootState = new MotionProofPlannerState(
            startX,
            startBottom,
            startHorizontalSpeed,
            startVerticalSpeed,
            startGrounded,
            startAirJumps,
            startFacing);
        var root = new MotionProofPlannerNode(
            Id: 0,
            Parent: null,
            Macro: null,
            State: rootState,
            Tick: 0,
            Heuristic: Distance(startX, startBottom, targetBase.X, targetBase.Y));

        var macros = BuildMotionProofPlannerMacros(rawOptions);
        var open = new PriorityQueue<MotionProofPlannerNode, float>();
        open.Enqueue(root, ScorePlannerNode(root, targetBase, team));
        var bestByCell = new Dictionary<MotionProofPlannerCell, float>();
        var validationEnvelopeByNodeId = new Dictionary<int, MotionProofPlannerState[]>();
        var bestNode = root;
        MotionProofPlannerCandidateResult? solvedCandidate = null;
        MotionProofPlannerCandidateResult? lastRejectedCandidate = null;
        var nextId = 1;
        var expansions = 0;
        var solutionsSeen = 0;
        var validatedCandidates = 0;
        var candidateLimitReached = false;
        if (searchValidationEnvelope)
        {
            validationEnvelopeByNodeId[root.Id] = BuildPlannerValidationEnvelope();
        }

        MotionProofPlannerState[] BuildPlannerValidationEnvelope()
        {
            var xOffsets = rawOptions.TryGetValue("planner-search-x-offsets", out var searchXOffsetsText)
                ? ParseFloatList(searchXOffsetsText)
                : rawOptions.TryGetValue("validation-x-offsets", out var xOffsetsText)
                ? ParseFloatList(xOffsetsText)
                : [-24f, 0f, 24f];
            var bottomOffsets = rawOptions.TryGetValue("planner-search-bottom-offsets", out var searchBottomOffsetsText)
                ? ParseFloatList(searchBottomOffsetsText)
                : rawOptions.TryGetValue("validation-bottom-offsets", out var bottomOffsetsText)
                ? ParseFloatList(bottomOffsetsText)
                : [-12f, 0f, 12f];
            var horizontalSpeedOffsets = rawOptions.TryGetValue("planner-search-horizontal-speeds", out var searchHorizontalSpeedsText)
                ? ParseFloatList(searchHorizontalSpeedsText)
                : rawOptions.TryGetValue("validation-horizontal-speeds", out var horizontalSpeedsText)
                ? ParseFloatList(horizontalSpeedsText)
                : [-60f, 0f, 60f];
            var verticalSpeedOffsets = rawOptions.TryGetValue("planner-search-vertical-speeds", out var searchVerticalSpeedsText)
                ? ParseFloatList(searchVerticalSpeedsText)
                : rawOptions.TryGetValue("validation-vertical-speeds", out var verticalSpeedsText)
                ? ParseFloatList(verticalSpeedsText)
                : [0f];
            var groundedStates = rawOptions.TryGetValue("planner-search-grounded", out var searchGroundedStatesText)
                ? ParseBoolList(searchGroundedStatesText)
                : rawOptions.TryGetValue("validation-grounded", out var groundedStatesText)
                ? ParseBoolList(groundedStatesText)
                : [startGrounded];
            var states = new List<MotionProofPlannerState>(
                xOffsets.Count * bottomOffsets.Count * horizontalSpeedOffsets.Count * verticalSpeedOffsets.Count * groundedStates.Count);
            foreach (var xOffset in xOffsets)
            {
                foreach (var bottomOffset in bottomOffsets)
                {
                    foreach (var horizontalSpeedOffset in horizontalSpeedOffsets)
                    {
                        foreach (var verticalSpeedOffset in verticalSpeedOffsets)
                        {
                            foreach (var grounded in groundedStates)
                            {
                                states.Add(new MotionProofPlannerState(
                                    startX + xOffset,
                                    startBottom + bottomOffset,
                                    startHorizontalSpeed + horizontalSpeedOffset,
                                    startVerticalSpeed + verticalSpeedOffset,
                                    grounded,
                                    startAirJumps,
                                    startFacing));
                            }
                        }
                    }
                }
            }

            return states.ToArray();
        }

        bool StateIntersectsTarget(MotionProofPlannerState state)
        {
            if (planObjective)
            {
                return MathF.Abs(state.X - targetBase.X) <= targetRadiusX
                    && MathF.Abs(state.Bottom - targetBase.Y) <= targetRadiusBottom;
            }

            var player = CreatePlannerPlayer(level, classId, team, state, carryingIntel: !planPickup);
            return player.IntersectsMarker(targetBase.X, targetBase.Y, IntelMarkerSize, IntelMarkerSize);
        }

        bool NodeIntersectsTarget(MotionProofPlannerNode node)
        {
            if (!searchValidationEnvelope)
            {
                return StateIntersectsTarget(node.State);
            }

            return validationEnvelopeByNodeId.TryGetValue(node.Id, out var envelope)
                && envelope.All(StateIntersectsTarget);
        }

        float ScorePlannerSearchNode(MotionProofPlannerNode node)
        {
            var nominalScore = ScorePlannerNode(node, targetBase, team);
            if (!searchValidationEnvelope
                || !validationEnvelopeByNodeId.TryGetValue(node.Id, out var envelope))
            {
                return nominalScore;
            }

            var worstDistance = 0f;
            var totalDistance = 0f;
            foreach (var state in envelope)
            {
                var distance = Distance(state.X, state.Bottom, targetBase.X, targetBase.Y);
                worstDistance = Math.Max(worstDistance, distance);
                totalDistance += distance;
            }

            var averageDistance = envelope.Length == 0 ? 0f : totalDistance / envelope.Length;
            return (nominalScore * 0.35f) + worstDistance + (averageDistance * 0.25f);
        }

        bool TrySimulatePlannerEnvelope(
            MotionProofPlannerNode node,
            MotionProofPlannerMacro macro,
            out MotionProofPlannerState[]? nextStates)
        {
            nextStates = null;
            if (!searchValidationEnvelope
                || !validationEnvelopeByNodeId.TryGetValue(node.Id, out var envelope))
            {
                return true;
            }

            var simulated = new MotionProofPlannerState[envelope.Length];
            for (var i = 0; i < envelope.Length; i += 1)
            {
                var transition = SimulatePlannerMacro(level, team, classId, envelope[i], macro, carryingIntel: !planPickup && !planObjective);
                if (!transition.Valid)
                {
                    return false;
                }

                simulated[i] = transition.State;
            }

            nextStates = simulated;
            return true;
        }

        MotionProofPlannerCandidateResult EvaluatePlannerCandidate(MotionProofPlannerNode candidate)
        {
            var candidateSteps = ReconstructPlannerSteps(candidate);
            var validationScenario = new TraversalLabScenario
            {
                Name = $"{tapeName}.validation",
                LevelName = mapName,
                MapAreaIndex = area,
                Team = team,
                ClassId = classId,
                Start = new TraversalLabStartState
                {
                    X = startX,
                    Bottom = startBottom,
                    HorizontalSpeed = startHorizontalSpeed,
                    VerticalSpeed = startVerticalSpeed,
                    IsGrounded = startGrounded,
                    RemainingAirJumps = startAirJumps,
                IsCarryingIntel = !planPickup && !planObjective,
                    FacingDirectionX = startFacing,
                },
                Steps = candidateSteps,
                MaxTicks = candidateSteps.Sum(static step => Math.Max(0, step.DurationTicks)),
                TraceEveryTicks = 1,
                StartXOffsets = rawOptions.TryGetValue("validation-x-offsets", out var xOffsets)
                    ? ParseFloatList(xOffsets)
                    : [-24f, 0f, 24f],
                StartBottomOffsets = rawOptions.TryGetValue("validation-bottom-offsets", out var bottomOffsets)
                    ? ParseFloatList(bottomOffsets)
                    : [-12f, 0f, 12f],
                StartHorizontalSpeedOffsets = rawOptions.TryGetValue("validation-horizontal-speeds", out var horizontalSpeeds)
                    ? ParseFloatList(horizontalSpeeds)
                    : [-60f, 0f, 60f],
                StartVerticalSpeedOffsets = rawOptions.TryGetValue("validation-vertical-speeds", out var verticalSpeeds)
                    ? ParseFloatList(verticalSpeeds)
                    : [0f],
                GroundedStates = rawOptions.TryGetValue("validation-grounded", out var groundedStates)
                    ? ParseBoolList(groundedStates)
                    : [startGrounded],
                FacingDirections = [startFacing],
                Expectation = new TraversalLabExpectation
                {
                FinalX = planObjective ? targetBase.X : null,
                FinalBottom = planObjective ? targetBase.Y : null,
                RadiusX = targetRadiusX,
                RadiusBottom = targetRadiusBottom,
                MustEverOverlapOwnIntelMarker = !planPickup && !planObjective,
                MustOverlapEnemyIntelMarker = planPickup ? true : null,
                },
            };
            var validation = TraversalLabRunner.Run(validationScenario);
            var validationFailures = validation.Cases
                .Where(static validationCase => !validationCase.Passed)
                .Select(static validationCase => new ReturnProofValidationFailure(
                    validationCase.Variant.StartXOffset,
                    validationCase.Variant.StartBottomOffset,
                    validationCase.Variant.StartHorizontalSpeedOffset,
                    validationCase.Variant.StartVerticalSpeedOffset,
                    validationCase.Variant.StartGrounded,
                    validationCase.FailureReason,
                    validationCase.FinalX,
                    validationCase.FinalY,
                    validationCase.FinalBottom))
                .ToArray();
            var validationArtifact = new ReturnProofValidationArtifact(
                validation.Cases.Count,
                validation.PassedCount,
                validation.FailedCount,
                validation.Passed,
                validationFailures);
            return new MotionProofPlannerCandidateResult(
                candidate,
                candidateSteps,
                validationScenario,
                validation,
                validationArtifact);
        }

        while (open.Count > 0 && expansions < maxExpansions)
        {
            var node = open.Dequeue();
            expansions += 1;
            if (node.Heuristic < bestNode.Heuristic)
            {
                bestNode = node;
            }

            if (NodeIntersectsTarget(node))
            {
                solutionsSeen += 1;
                if (solutionsSeen <= skipSolutions)
                {
                    continue;
                }

                validatedCandidates += 1;
                var candidate = EvaluatePlannerCandidate(node);
                Console.WriteLine(
                    $"motionProofPlannerCandidate index={validatedCandidates} ticks={node.Tick} steps={candidate.Steps.Count} validationPassed={(candidate.Validation.Passed ? 1 : 0)} " +
                    $"cases={candidate.Validation.Cases.Count} passedCases={candidate.Validation.PassedCount} failedCases={candidate.Validation.FailedCount}");
                if (candidate.Validation.Passed)
                {
                    solvedCandidate = candidate;
                    break;
                }

                lastRejectedCandidate = candidate;
                if (validatedCandidates >= maxValidatedSolutions)
                {
                    candidateLimitReached = true;
                    break;
                }

                continue;
            }

            foreach (var macro in macros)
            {
                if (node.Tick + macro.Ticks > maxTicks)
                {
                    continue;
                }

                var transition = SimulatePlannerMacro(level, team, classId, node.State, macro, carryingIntel: !planPickup && !planObjective);
                if (!transition.Valid)
                {
                    continue;
                }
                if (!TrySimulatePlannerEnvelope(node, macro, out var nextEnvelope))
                {
                    continue;
                }

                var heuristic = Distance(transition.State.X, transition.State.Bottom, targetBase.X, targetBase.Y);
                if (nextEnvelope is not null)
                {
                    foreach (var state in nextEnvelope)
                    {
                        heuristic = Math.Max(heuristic, Distance(state.X, state.Bottom, targetBase.X, targetBase.Y));
                    }
                }

                var child = new MotionProofPlannerNode(
                    nextId++,
                    node,
                    macro,
                    transition.State,
                    node.Tick + macro.Ticks,
                    heuristic);
                if (nextEnvelope is not null)
                {
                    validationEnvelopeByNodeId[child.Id] = nextEnvelope;
                }

                var cell = MotionProofPlannerCell.From(child.State);
                var dominanceCost = heuristic + (child.Tick * 0.08f);
                if (bestByCell.TryGetValue(cell, out var previousBest) && dominanceCost >= previousBest - 8f)
                {
                    continue;
                }

                bestByCell[cell] = dominanceCost;
                open.Enqueue(child, ScorePlannerSearchNode(child));
            }
        }

        Directory.CreateDirectory(artifactDirectory);
        var planSummary = new MotionProofPlannerSummary(
            expansions,
            nextId,
            bestNode.Tick,
            bestNode.Heuristic,
            bestNode.State.X,
            bestNode.State.Bottom,
            solvedCandidate is not null,
            solvedCandidate?.Node.Tick ?? -1,
            macros.Length);
        File.WriteAllText(
            Path.Combine(artifactDirectory, "planner-summary.json"),
            JsonSerializer.Serialize(planSummary, outputJsonOptions));

        Console.WriteLine(
            $"motionProofPlanner mode={(planObjective ? "objective" : planPickup ? "pickup" : "return")} map={mapName} area={area} team={team} class={classId} expansions={expansions} generated={nextId - 1} " +
            $"solved={(solvedCandidate is null ? 0 : 1)} nominalSolutions={solutionsSeen} validatedCandidates={validatedCandidates} candidateLimitReached={(candidateLimitReached ? 1 : 0)} " +
            $"bestDist={bestNode.Heuristic:0.0} best=({bestNode.State.X:0.0},{bestNode.State.Bottom:0.0}) bestTick={bestNode.Tick}");

        if (solvedCandidate is null)
        {
            if (lastRejectedCandidate is not null)
            {
                File.WriteAllText(
                    Path.Combine(artifactDirectory, "planner-steps.json"),
                    JsonSerializer.Serialize(lastRejectedCandidate.Steps, outputJsonOptions));
                File.WriteAllText(
                    Path.Combine(artifactDirectory, "validation.json"),
                    JsonSerializer.Serialize(lastRejectedCandidate.ValidationArtifact, outputJsonOptions));
                foreach (var failure in lastRejectedCandidate.ValidationArtifact.Failures.Take(12))
                {
                    Console.WriteLine(
                        $"validationFailure xOff:{failure.StartXOffset:0.0} bottomOff:{failure.StartBottomOffset:0.0} hSpeed:{failure.StartHorizontalSpeedOffset:0.0} " +
                        $"vSpeed:{failure.StartVerticalSpeedOffset:0.0} grounded:{(failure.StartGrounded ? 1 : 0)} final=({failure.FinalX:0.0},{failure.FinalY:0.0}) bottom={failure.FinalBottom:0.0} reason={failure.Reason}");
                }

                var reason = candidateLimitReached ? "validation_failed_candidate_limit" : "validation_failed";
                Console.WriteLine($"motionProofPlanner=rejected reason:{reason} artifactDir={artifactDirectory}");
            }
            else
            {
                var bestSteps = ReconstructPlannerSteps(bestNode);
                File.WriteAllText(
                    Path.Combine(artifactDirectory, "best-failed-steps.json"),
                    JsonSerializer.Serialize(bestSteps, outputJsonOptions));
                Console.WriteLine($"motionProofPlanner=rejected reason:no_solution artifactDir={artifactDirectory}");
            }

            return;
        }

        var steps = solvedCandidate.Steps;
        var validationScenario = solvedCandidate.ValidationScenario;
        var validation = solvedCandidate.Validation;
        var validationArtifact = solvedCandidate.ValidationArtifact;
        File.WriteAllText(
            Path.Combine(artifactDirectory, "planner-steps.json"),
            JsonSerializer.Serialize(steps, outputJsonOptions));
        File.WriteAllText(
            Path.Combine(artifactDirectory, "validation.json"),
            JsonSerializer.Serialize(validationArtifact, outputJsonOptions));

        Console.WriteLine(
            $"motionProofPlannerAcceptedCandidate ticks={solvedCandidate.Node.Tick} steps={steps.Count} validationPassed={(validation.Passed ? 1 : 0)} " +
            $"cases={validation.Cases.Count} passedCases={validation.PassedCount} failedCases={validation.FailedCount}");
        foreach (var failure in validationArtifact.Failures.Take(12))
        {
            Console.WriteLine(
                $"validationFailure xOff:{failure.StartXOffset:0.0} bottomOff:{failure.StartBottomOffset:0.0} hSpeed:{failure.StartHorizontalSpeedOffset:0.0} " +
                $"vSpeed:{failure.StartVerticalSpeedOffset:0.0} grounded:{(failure.StartGrounded ? 1 : 0)} final=({failure.FinalX:0.0},{failure.FinalY:0.0}) bottom={failure.FinalBottom:0.0} reason={failure.Reason}");
        }

        var bestCase = validation.Cases
            .Where(static candidate => candidate.Passed)
            .OrderBy(static candidate => candidate.ExecutedTicks)
            .First();
        var trimmedPlannerSamples = planObjective
            ? bestCase.Samples
            : planPickup
            ? TrimTraversalSamplesAtIntel(level, opposingTeam, classId, bestCase.Samples)
            : TrimTraversalSamplesAtIntel(level, team, classId, bestCase.Samples);
        var verifiedGraph = VerifiedNavCandidateBuilder.Build(level, new VerifiedNavBuildOptions
        {
            Team = team,
            ClassId = classId,
            DropHorizontalReach = ReadFloatOption(rawOptions, "drop-horizontal-reach", 360f),
            JumpHorizontalReach = ReadFloatOption(rawOptions, "jump-horizontal-reach", 360f),
        });
        var proofTrace = VerifiedNavProofTraceExtractor.Extract(
            verifiedGraph,
            trimmedPlannerSamples,
            planObjective ? "MotionProofObjectivePlanner" : planPickup ? "MotionProofPickupPlanner" : "MotionProofReturnPlanner",
            startX,
            startBottom);
        proofTrace = CloneProofTraceWithActions(
            proofTrace,
            BuildVerifiedNavProofGraphActionsFromSteps(steps, trimmedPlannerSamples.Count));
        File.WriteAllText(
            Path.Combine(artifactDirectory, "proof-trace-edges.json"),
            JsonSerializer.Serialize(proofTrace, outputJsonOptions));
        var entry = BuildObjectiveActionTapeFromTraversalSamples(
            tapeName,
            validationScenario,
            trimmedPlannerSamples,
            steps);
        File.WriteAllText(
            Path.Combine(artifactDirectory, "candidate-tape.json"),
            JsonSerializer.Serialize(entry, outputJsonOptions));

        if (!accept)
        {
            Console.WriteLine($"motionProofPlanner=accepted_candidate artifactDir={artifactDirectory} install=skipped reason:missing_accept_flag");
            return;
        }

        var tapePath = BotBrainObjectiveTapeStore.UpsertTape(level, entry);
        Console.WriteLine($"motionProofPlanner=installed path={tapePath} artifactDir={artifactDirectory}");
    }

    private static PlayerEntity CreatePlannerPlayer(
        SimpleLevel level,
        PlayerClass classId,
        PlayerTeam team,
        MotionProofPlannerState state,
        bool carryingIntel)
    {
        var definition = CharacterClassCatalog.GetDefinition(classId);
        var player = new PlayerEntity(1, definition, "MotionProofPlanner");
        var y = state.Bottom - player.CollisionBottomOffset;
        player.Spawn(team, state.X, y);
        player.TeleportTo(state.X, y);
        player.ResolveBlockingOverlap(level, team);
        player.ApplyVelocityImpulse(state.HorizontalSpeed, state.VerticalSpeed);
        if (carryingIntel)
        {
            player.PickUpIntel();
        }

        player.RestoreMovementProbeState(state.IsGrounded, state.RemainingAirJumps, state.FacingDirectionX);
        player.SetAimWorldPosition(state.X + (state.FacingDirectionX * 256f), player.Y);
        return player;
    }

    private static MotionProofPlannerTransition SimulatePlannerMacro(
        SimpleLevel level,
        PlayerTeam team,
        PlayerClass classId,
        MotionProofPlannerState start,
        MotionProofPlannerMacro macro,
        bool carryingIntel)
    {
        var player = CreatePlannerPlayer(level, classId, team, start, carryingIntel);
        var previousInput = default(PlayerInputSnapshot);
        for (var tick = 0; tick < macro.Ticks; tick += 1)
        {
            var input = CreatePlannerInput(player, macro, tick);
            var jumpPressed = input.Up && !previousInput.Up;
            player.Advance(
                input,
                jumpPressed,
                level,
                team,
                deltaSeconds: 1d / SimulationConfig.DefaultTicksPerSecond);

            previousInput = input;
            if (player.X < -256f
                || player.X > level.Bounds.Width + 256f
                || player.Bottom < -512f
                || player.Bottom > level.Bounds.Height + 1024f)
            {
                return MotionProofPlannerTransition.Invalid;
            }
        }

        var facing = macro.Direction != 0
            ? macro.Direction
            : player.FacingDirectionX == 0f ? start.FacingDirectionX : player.FacingDirectionX;
        return new MotionProofPlannerTransition(
            true,
            new MotionProofPlannerState(
                player.X,
                player.Bottom,
                player.HorizontalSpeed,
                player.VerticalSpeed,
                player.IsGrounded,
                player.RemainingAirJumps,
                facing < 0f ? -1f : 1f));
    }

    private static PlayerInputSnapshot CreatePlannerInput(PlayerEntity player, MotionProofPlannerMacro macro, int tick)
    {
        var direction = macro.Direction;
        var aimDirection = direction == 0
            ? player.FacingDirectionX == 0f ? 1f : player.FacingDirectionX
            : direction;
        return new PlayerInputSnapshot(
            Left: direction < 0,
            Right: direction > 0,
            Up: macro.JumpTicks > 0 && tick < macro.JumpTicks,
            Down: macro.Drop,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + (aimDirection * 256f),
            AimWorldY: player.Y,
            DebugKill: false,
            DropIntel: false,
            UseAbility: false);
    }

    private static MotionProofPlannerMacro[] BuildMotionProofPlannerMacros(Dictionary<string, string> rawOptions)
    {
        var durations = rawOptions.TryGetValue("planner-durations", out var durationText)
            ? ParseIntList(durationText)
            : [6, 10, 16, 24, 36, 48];
        var jumpHolds = rawOptions.TryGetValue("planner-jump-holds", out var jumpHoldText)
            ? ParseIntList(jumpHoldText)
            : [1, 3, 6];
        var macros = new List<MotionProofPlannerMacro>();
        foreach (var duration in durations.Where(static value => value > 0).Distinct())
        {
            macros.Add(new MotionProofPlannerMacro($"idle.{duration}", 0, duration, 0, Drop: false));
            foreach (var direction in new[] { -1, 1 })
            {
                macros.Add(new MotionProofPlannerMacro($"run.{direction}.{duration}", direction, duration, 0, Drop: false));
                macros.Add(new MotionProofPlannerMacro($"drop.{direction}.{duration}", direction, duration, 0, Drop: true));
                foreach (var jumpHold in jumpHolds.Where(static value => value > 0).Distinct())
                {
                    macros.Add(new MotionProofPlannerMacro($"jump.{direction}.{duration}.{jumpHold}", direction, duration, Math.Min(duration, jumpHold), Drop: false));
                    macros.Add(new MotionProofPlannerMacro($"dropjump.{direction}.{duration}.{jumpHold}", direction, duration, Math.Min(duration, jumpHold), Drop: true));
                }
            }
        }

        return macros.ToArray();
    }

    private static float ScorePlannerNode(MotionProofPlannerNode node, (float X, float Y) target, PlayerTeam team)
    {
        var directionProgress = team == PlayerTeam.Red
            ? node.State.X - target.X
            : target.X - node.State.X;
        var progressPenalty = MathF.Max(0f, directionProgress) * 0.08f;
        var velocityPenalty = MathF.Abs(node.State.HorizontalSpeed) < 5f && !node.State.IsGrounded ? 20f : 0f;
        return node.Heuristic + progressPenalty + velocityPenalty + (node.Tick * 0.12f);
    }

    private static List<TraversalLabInputStep> ReconstructPlannerSteps(MotionProofPlannerNode node)
    {
        var macros = new Stack<MotionProofPlannerMacro>();
        var cursor = node;
        while (cursor.Macro is not null && cursor.Parent is not null)
        {
            macros.Push(cursor.Macro.Value);
            cursor = cursor.Parent;
        }

        var steps = new List<TraversalLabInputStep>();
        foreach (var macro in macros)
        {
            for (var tick = 0; tick < macro.Ticks; tick += 1)
            {
                steps.Add(new TraversalLabInputStep
                {
                    Label = macro.Label,
                    DurationTicks = 1,
                    Left = macro.Direction < 0,
                    Right = macro.Direction > 0,
                    Up = macro.JumpTicks > 0 && tick < macro.JumpTicks,
                    Down = macro.Drop,
                    AimFacingDirectionX = macro.Direction == 0 ? null : macro.Direction,
                });
            }
        }

        return steps;
    }

    private static BotBrainObjectiveTapeEntry BuildObjectiveActionTapeFromTraversalSamples(
        string tapeName,
        TraversalLabScenario scenario,
        IReadOnlyList<TraversalLabTickSample> sourceSamples,
        IReadOnlyList<TraversalLabInputStep> steps)
    {
        var entry = BuildObjectiveTapeFromTraversalSamples(tapeName, scenario, sourceSamples);
        var segment = entry.Segments[0];
        segment.Actions = BuildObjectiveActionsFromSteps(steps);
        segment.EntryRadius = 128f;
        segment.ExitRadius = 180f;
        return entry;
    }

    private static List<BotBrainObjectiveTapeAction> BuildObjectiveActionsFromSteps(IReadOnlyList<TraversalLabInputStep> steps)
    {
        var actions = new List<BotBrainObjectiveTapeAction>();
        foreach (var step in steps)
        {
            var direction = step.Left == step.Right
                ? 0
                : step.Right ? 1 : -1;
            var kind = step.Up
                ? "Jump"
                : step.Down ? "Drop" : "Run";
            var ticks = Math.Max(1, step.DurationTicks);
            if (actions.Count > 0
                && actions[^1].Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)
                && actions[^1].Direction == direction)
            {
                actions[^1].Ticks += ticks;
                continue;
            }

            actions.Add(new BotBrainObjectiveTapeAction
            {
                Kind = kind,
                Direction = direction,
                Ticks = ticks,
            });
        }

        return actions;
    }

    private static List<VerifiedNavProofGraphActionRun> BuildVerifiedNavProofGraphActionsFromSteps(
        IReadOnlyList<TraversalLabInputStep> steps,
        int maxTicks)
    {
        var actions = new List<VerifiedNavProofGraphActionRun>();
        var remainingTicks = Math.Max(0, maxTicks);
        foreach (var step in steps)
        {
            if (remainingTicks <= 0)
            {
                break;
            }

            var ticks = Math.Min(remainingTicks, Math.Max(0, step.DurationTicks));
            if (ticks <= 0)
            {
                continue;
            }

            var direction = step.Left == step.Right
                ? 0f
                : step.Right ? 1f : -1f;
            if (actions.Count > 0
                && actions[^1].MoveDirection == direction
                && actions[^1].Jump == step.Up
                && actions[^1].DropDown == step.Down)
            {
                var previous = actions[^1];
                actions[^1] = previous with { Ticks = previous.Ticks + ticks };
            }
            else
            {
                actions.Add(new VerifiedNavProofGraphActionRun(
                    ticks,
                    direction,
                    step.Up,
                    step.Down));
            }

            remainingTicks -= ticks;
        }

        return actions;
    }

    private static VerifiedNavProofTraceReport CloneProofTraceWithActions(
        VerifiedNavProofTraceReport source,
        List<VerifiedNavProofGraphActionRun> actions)
    {
        return new VerifiedNavProofTraceReport
        {
            LevelName = source.LevelName,
            MapAreaIndex = source.MapAreaIndex,
            Team = source.Team,
            ClassId = source.ClassId,
            Source = source.Source,
            SampleCount = source.SampleCount,
            SurfaceTouchCount = source.SurfaceTouchCount,
            StartX = source.StartX,
            StartBottom = source.StartBottom,
            SurfaceSequence = source.SurfaceSequence,
            Edges = source.Edges,
            Actions = actions,
            TerminalStartX = source.TerminalStartX,
            TerminalStartBottom = source.TerminalStartBottom,
            TerminalEndX = source.TerminalEndX,
            TerminalEndBottom = source.TerminalEndBottom,
            TerminalActions = source.TerminalActions,
        };
    }

    public static void ValidateReturnTapeAsClass(Dictionary<string, string> rawOptions, JsonSerializerOptions outputJsonOptions)
    {
        var mapName = rawOptions.TryGetValue("map", out var mapText) ? mapText : "Truefort";
        var area = ReadIntOption(rawOptions, "area", 1);
        var team = ReadEnumOption(rawOptions, "team", PlayerTeam.Red);
        var classId = ReadEnumOption(rawOptions, "class", PlayerClass.Soldier);
        if (!rawOptions.TryGetValue("tape-name", out var tapeName) || string.IsNullOrWhiteSpace(tapeName))
        {
            throw new InvalidOperationException("--validate-return-tape-as-class requires --tape-name.");
        }

        var artifactDirectory = rawOptions.TryGetValue("artifacts-dir", out var artifactsText) && !string.IsNullOrWhiteSpace(artifactsText)
            ? Path.GetFullPath(artifactsText)
            : Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "botbrain-return-tape-validation", $"{mapName}-{team}-{classId}");

        var compileRepoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(compileRepoRoot, "Core", "Content"));
        var level = SimpleLevelFactory.CreateImportedLevel(mapName, area)
            ?? throw new InvalidOperationException($"Could not load map '{mapName}' area {area}.");
        if (!BotBrainObjectiveTapeStore.TryLoad(level, out var tapeAsset))
        {
            throw new InvalidOperationException($"Could not load objective tapes for '{mapName}' area {area}.");
        }

        var tape = tapeAsset.Tapes.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, tapeName, StringComparison.OrdinalIgnoreCase)
            && candidate.Team == team)
            ?? tapeAsset.Tapes.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, tapeName, StringComparison.OrdinalIgnoreCase));
        if (tape is null)
        {
            var available = string.Join(", ", tapeAsset.Tapes
                .Where(candidate => candidate.Team == team)
                .Select(static candidate => candidate.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20));
            throw new InvalidOperationException($"Could not find tape '{tapeName}'. Available {team} tapes: {available}");
        }

        var segment = tape.Segments.FirstOrDefault(static candidate =>
            candidate.RequiresCarryingIntel
            && candidate.Samples.Count >= 2
            && candidate.Actions.Count > 0)
            ?? throw new InvalidOperationException($"Tape '{tape.Name}' does not contain a carrying return action segment.");

        var scenario = BuildReturnTapeValidationScenario(
            mapName,
            area,
            team,
            classId,
            tape,
            segment,
            rawOptions);
        var validation = TraversalLabRunner.Run(scenario);
        var validationFailures = validation.Cases
            .Where(static candidate => !candidate.Passed)
            .Select(static candidate => new ReturnProofValidationFailure(
                candidate.Variant.StartXOffset,
                candidate.Variant.StartBottomOffset,
                candidate.Variant.StartHorizontalSpeedOffset,
                candidate.Variant.StartVerticalSpeedOffset,
                candidate.Variant.StartGrounded,
                candidate.FailureReason,
                candidate.FinalX,
                candidate.FinalY,
                candidate.FinalBottom))
            .ToArray();
        var validationArtifact = new ReturnProofValidationArtifact(
            validation.Cases.Count,
            validation.PassedCount,
            validation.FailedCount,
            validation.Passed,
            validationFailures);

        Directory.CreateDirectory(artifactDirectory);
        File.WriteAllText(
            Path.Combine(artifactDirectory, "scenario.json"),
            JsonSerializer.Serialize(scenario, outputJsonOptions));
        File.WriteAllText(
            Path.Combine(artifactDirectory, "validation.json"),
            JsonSerializer.Serialize(validationArtifact, outputJsonOptions));

        Console.WriteLine(
            $"returnTapeValidation tape={tape.Name} sourceClass={tape.PlayerClass} asClass={classId} team={team} " +
            $"actions={segment.Actions.Count} ticks={scenario.Steps.Sum(static step => step.DurationTicks)} " +
            $"cases={validation.Cases.Count} passedCases={validation.PassedCount} failedCases={validation.FailedCount} passedAll={(validation.Passed ? 1 : 0)}");
        foreach (var failure in validationFailures.Take(12))
        {
            Console.WriteLine(
                $"validationFailure xOff:{failure.StartXOffset:0.0} bottomOff:{failure.StartBottomOffset:0.0} hSpeed:{failure.StartHorizontalSpeedOffset:0.0} " +
                $"vSpeed:{failure.StartVerticalSpeedOffset:0.0} grounded:{(failure.StartGrounded ? 1 : 0)} final=({failure.FinalX:0.0},{failure.FinalY:0.0}) bottom={failure.FinalBottom:0.0} reason={failure.Reason}");
        }

        Console.WriteLine($"returnTapeValidationArtifact={artifactDirectory}");
    }

    private static TraversalLabScenario BuildReturnTapeValidationScenario(
        string mapName,
        int area,
        PlayerTeam team,
        PlayerClass classId,
        BotBrainObjectiveTapeEntry tape,
        BotBrainObjectiveTapeSegment segment,
        Dictionary<string, string> rawOptions)
    {
        var firstSample = segment.Samples[0];
        var firstAction = segment.Actions[0];
        var sampleHeight = firstSample.Bottom - firstSample.Y;
        var startX = ReadFloatOption(rawOptions, "start-x", segment.EntryX ?? firstSample.X);
        var startBottomFallback = segment.EntryY.HasValue
            ? segment.EntryY.Value + sampleHeight
            : firstSample.Bottom;
        var startBottom = ReadFloatOption(rawOptions, "start-bottom", startBottomFallback);
        var facingDirection = firstAction.Direction != 0
            ? firstAction.Direction
            : firstSample.MoveDirection != 0f
                ? firstSample.MoveDirection
                : team == PlayerTeam.Red ? -1f : 1f;
        var steps = BuildReturnTapeValidationSteps(segment);
        var maxTicks = ReadIntOption(rawOptions, "ticks", steps.Sum(static step => Math.Max(0, step.DurationTicks)));
        return new TraversalLabScenario
        {
            Name = $"{tape.Name}.as.{classId}.validation",
            LevelName = mapName,
            MapAreaIndex = area,
            Team = team,
            ClassId = classId,
            Start = new TraversalLabStartState
            {
                X = startX,
                Bottom = startBottom,
                HorizontalSpeed = ReadFloatOption(rawOptions, "start-horizontal-speed", firstSample.HorizontalSpeed),
                VerticalSpeed = ReadFloatOption(rawOptions, "start-vertical-speed", firstSample.VerticalSpeed),
                IsGrounded = rawOptions.TryGetValue("start-grounded", out var startGroundedText)
                    && bool.TryParse(startGroundedText, out var parsedStartGrounded)
                        ? parsedStartGrounded
                        : firstSample.IsGrounded,
                IsCarryingIntel = true,
                FacingDirectionX = facingDirection < 0f ? -1f : 1f,
            },
            Steps = steps,
            MaxTicks = maxTicks,
            TraceEveryTicks = Math.Max(1, ReadIntOption(rawOptions, "trace-every", maxTicks)),
            StartXOffsets = rawOptions.TryGetValue("validation-x-offsets", out var xOffsets)
                ? ParseFloatList(xOffsets)
                : [0f],
            StartBottomOffsets = rawOptions.TryGetValue("validation-bottom-offsets", out var bottomOffsets)
                ? ParseFloatList(bottomOffsets)
                : [0f],
            StartHorizontalSpeedOffsets = rawOptions.TryGetValue("validation-horizontal-speeds", out var horizontalSpeeds)
                ? ParseFloatList(horizontalSpeeds)
                : [0f],
            StartVerticalSpeedOffsets = rawOptions.TryGetValue("validation-vertical-speeds", out var verticalSpeeds)
                ? ParseFloatList(verticalSpeeds)
                : [0f],
            GroundedStates = rawOptions.TryGetValue("validation-grounded", out var groundedStates)
                ? ParseBoolList(groundedStates)
                : [firstSample.IsGrounded],
            FacingDirections = rawOptions.TryGetValue("validation-facing-directions", out var facingDirections)
                ? ParseFloatList(facingDirections)
                : [facingDirection < 0f ? -1f : 1f],
            Expectation = new TraversalLabExpectation
            {
                FinalX = segment.ExitX ?? segment.Samples[^1].X,
                FinalBottom = segment.ExitY.HasValue
                    ? segment.ExitY.Value + (segment.Samples[^1].Bottom - segment.Samples[^1].Y)
                    : segment.Samples[^1].Bottom,
                RadiusX = ReadFloatOption(rawOptions, "validation-exit-radius-x", segment.ExitRadius ?? 96f),
                RadiusBottom = ReadFloatOption(rawOptions, "validation-exit-radius-bottom", segment.ExitRadius ?? 96f),
                MustEverOverlapOwnIntelMarker = rawOptions.TryGetValue("require-own-intel-overlap", out var requireOwnIntelOverlap)
                    && bool.TryParse(requireOwnIntelOverlap, out var parsedRequireOwnIntelOverlap)
                    && parsedRequireOwnIntelOverlap,
                MustCarryIntel = rawOptions.TryGetValue("require-final-carrying", out var requireFinalCarrying)
                    && bool.TryParse(requireFinalCarrying, out var parsedRequireFinalCarrying)
                    && parsedRequireFinalCarrying
                        ? true
                        : null,
            },
        };
    }

    private static List<TraversalLabInputStep> BuildReturnTapeValidationSteps(BotBrainObjectiveTapeSegment segment)
    {
        var steps = new List<TraversalLabInputStep>();
        for (var actionIndex = 0; actionIndex < segment.Actions.Count; actionIndex += 1)
        {
            var action = segment.Actions[actionIndex];
            var duration = Math.Max(0, action.Ticks);
            var moveDirection = action.Direction < 0 ? -1 : action.Direction > 0 ? 1 : 0;
            var kind = string.IsNullOrWhiteSpace(action.Kind) ? "Run" : action.Kind;
            for (var tick = 0; tick < duration; tick += 1)
            {
                steps.Add(new TraversalLabInputStep
                {
                    Label = $"tape_{actionIndex}_{kind}",
                    DurationTicks = 1,
                    Left = moveDirection < 0,
                    Right = moveDirection > 0,
                    Up = kind.Equals("Jump", StringComparison.OrdinalIgnoreCase) && tick == 0,
                    Down = kind.Equals("Drop", StringComparison.OrdinalIgnoreCase),
                    AimFacingDirectionX = moveDirection == 0 ? null : moveDirection,
                });
            }
        }

        return steps;
    }

    public static void GenerateFollowProofReturnTape(Dictionary<string, string> rawOptions, JsonSerializerOptions outputJsonOptions)
    {
        var mapName = rawOptions.TryGetValue("map", out var mapText) ? mapText : "Truefort";
        var area = ReadIntOption(rawOptions, "area", 1);
        var team = ReadEnumOption(rawOptions, "team", PlayerTeam.Red);
        var classId = ReadEnumOption(rawOptions, "class", PlayerClass.Pyro);
        if (!rawOptions.TryGetValue("source-tape-name", out var sourceTapeName) || string.IsNullOrWhiteSpace(sourceTapeName))
        {
            throw new InvalidOperationException("--generate-follow-proof-return requires --source-tape-name.");
        }

        var accept = rawOptions.TryGetValue("accept-proof-return", out var acceptText)
            && bool.TryParse(acceptText, out var parsedAccept)
            && parsedAccept;
        var tapeName = rawOptions.TryGetValue("tape-name", out var providedTapeName)
            ? providedTapeName
            : $"MotionProof.{mapName}.{team}.{classId}.FollowReturn";
        var artifactDirectory = rawOptions.TryGetValue("artifacts-dir", out var artifactsText) && !string.IsNullOrWhiteSpace(artifactsText)
            ? Path.GetFullPath(artifactsText)
            : Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "botbrain-follow-proof-return", $"{mapName}-{team}-{classId}");

        var compileRepoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ContentRoot.Initialize(Path.Combine(compileRepoRoot, "Core", "Content"));
        var level = SimpleLevelFactory.CreateImportedLevel(mapName, area)
            ?? throw new InvalidOperationException($"Could not load map '{mapName}' area {area}.");
        if (!BotBrainObjectiveTapeStore.TryLoad(level, out var tapeAsset))
        {
            throw new InvalidOperationException($"Could not load objective tapes for '{mapName}' area {area}.");
        }

        var sourceTape = tapeAsset.Tapes.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, sourceTapeName, StringComparison.OrdinalIgnoreCase)
            && candidate.Team == team)
            ?? throw new InvalidOperationException($"Could not find source tape '{sourceTapeName}' for {team}.");
        var sourceSegment = sourceTape.Segments.FirstOrDefault(static candidate =>
            candidate.RequiresCarryingIntel
            && candidate.Samples.Count >= 2
            && candidate.Actions.Count > 0)
            ?? throw new InvalidOperationException($"Source tape '{sourceTape.Name}' does not contain a carrying return action segment.");

        var sourceOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["trace-every"] = "1",
            ["validation-x-offsets"] = "0",
            ["validation-bottom-offsets"] = "0",
            ["validation-horizontal-speeds"] = "0",
            ["validation-vertical-speeds"] = "0",
        };
        var sourceScenario = BuildReturnTapeValidationScenario(
            mapName,
            area,
            team,
            sourceTape.PlayerClass,
            sourceTape,
            sourceSegment,
            sourceOptions);
        var sourceReplay = TraversalLabRunner.Run(sourceScenario);
        var sourceCase = sourceReplay.Cases.FirstOrDefault();
        if (sourceCase is null || !sourceCase.Passed)
        {
            throw new InvalidOperationException($"Source tape '{sourceTape.Name}' failed validation as {sourceTape.PlayerClass}: {sourceCase?.FailureReason}");
        }

        var maxTicks = ReadIntOption(rawOptions, "ticks", sourceCase.ExecutedTicks + 900);
        var lookaheadTicks = rawOptions.TryGetValue("follow-lookahead-ticks", out var lookaheadText)
            ? ParseIntList(lookaheadText)
            : [12, 24, 36, 48];
        var deadZones = rawOptions.TryGetValue("follow-deadzones", out var deadzoneText)
            ? ParseFloatList(deadzoneText)
            : [24f];
        var jumpThresholds = rawOptions.TryGetValue("follow-jump-thresholds", out var jumpThresholdText)
            ? ParseFloatList(jumpThresholdText)
            : [12f, 24f, 36f];
        var jumpCooldowns = rawOptions.TryGetValue("follow-jump-cooldowns", out var jumpCooldownText)
            ? ParseIntList(jumpCooldownText)
            : [8];
        var dropThresholds = rawOptions.TryGetValue("follow-drop-thresholds", out var dropThresholdText)
            ? ParseFloatList(dropThresholdText)
            : [24f, 48f];
        var runupTicksList = rawOptions.TryGetValue("follow-runup-ticks", out var runupTickText)
            ? ParseIntList(runupTickText)
            : [0, 12, 24];
        var useSpatialFollow = rawOptions.TryGetValue("follow-spatial", out var spatialText)
            && bool.TryParse(spatialText, out var parsedSpatial)
            && parsedSpatial;

        FollowProofCaseResult? bestCase = null;
        FollowProofCaseResult? bestFailedCase = null;
        FollowProofParameters? bestParameters = null;
        FollowProofParameters? bestFailedParameters = null;
        var attempts = 0;
        foreach (var lookahead in lookaheadTicks)
        {
            foreach (var deadzone in deadZones)
            {
                foreach (var jumpThreshold in jumpThresholds)
                {
                    foreach (var jumpCooldown in jumpCooldowns)
                    {
                        foreach (var dropThreshold in dropThresholds)
                        {
                            foreach (var runupTicks in runupTicksList)
                            {
                                attempts += 1;
                                var parameters = new FollowProofParameters(
                                    lookahead,
                                    deadzone,
                                    jumpThreshold,
                                    jumpCooldown,
                                    dropThreshold,
                                    runupTicks);
                                var result = RunFollowProofCase(
                                    mapName,
                                    area,
                                    team,
                                    classId,
                                    sourceSegment,
                                    sourceCase.Samples,
                                    parameters,
                                    maxTicks,
                                    0f,
                                    0f,
                                0f,
                                0f,
                                sourceSegment.Samples[0].IsGrounded,
                                useSpatialFollow);
                                if (result.Passed)
                                {
                                    if (bestCase is null || result.ExecutedTicks < bestCase.ExecutedTicks)
                                    {
                                        bestCase = result;
                                        bestParameters = parameters;
                                    }
                                }
                                else if (bestFailedCase is null || result.ExitDistance < bestFailedCase.ExitDistance)
                                {
                                    bestFailedCase = result;
                                    bestFailedParameters = parameters;
                                }
                            }
                        }
                    }
                }
            }
        }

        Directory.CreateDirectory(artifactDirectory);
        var searchSummary = new FollowProofSearchSummary(
            attempts,
            bestCase?.ExecutedTicks ?? -1,
            bestFailedCase?.ExitDistance ?? float.PositiveInfinity,
            bestParameters,
            bestFailedParameters);
        File.WriteAllText(
            Path.Combine(artifactDirectory, "search-summary.json"),
            JsonSerializer.Serialize(searchSummary, outputJsonOptions));
        if (bestFailedCase is not null)
        {
            File.WriteAllText(
                Path.Combine(artifactDirectory, "best-failed-samples.json"),
                JsonSerializer.Serialize(bestFailedCase.Samples, outputJsonOptions));
        }

        File.WriteAllText(
            Path.Combine(artifactDirectory, "source-samples.json"),
            JsonSerializer.Serialize(sourceCase.Samples, outputJsonOptions));

        Console.WriteLine(
            $"followProofSearch map={mapName} area={area} team={team} class={classId} source={sourceTape.Name} " +
            $"attempts={attempts} passed={(bestCase is null ? 0 : 1)}");
        if (bestCase is null || bestParameters is null)
        {
            if (bestFailedCase is not null && bestFailedParameters is not null)
            {
                Console.WriteLine(
                    $"closestFailed dist={bestFailedCase.ExitDistance:0.0} final=({bestFailedCase.FinalX:0.0},{bestFailedCase.FinalY:0.0}) " +
                    $"bottom={bestFailedCase.FinalBottom:0.0} params={bestFailedParameters} reason={bestFailedCase.FailureReason}");
            }

            Console.WriteLine($"followProof=rejected reason:no_successful_candidate artifactDir={artifactDirectory}");
            return;
        }

        var validationXOffsets = rawOptions.TryGetValue("validation-x-offsets", out var xOffsets)
            ? ParseFloatList(xOffsets)
            : [-24f, 0f, 24f];
        var validationBottomOffsets = rawOptions.TryGetValue("validation-bottom-offsets", out var bottomOffsets)
            ? ParseFloatList(bottomOffsets)
            : [-12f, 0f, 12f];
        var validationHorizontalSpeeds = rawOptions.TryGetValue("validation-horizontal-speeds", out var horizontalSpeeds)
            ? ParseFloatList(horizontalSpeeds)
            : [-60f, 0f, 60f];
        var validationVerticalSpeeds = rawOptions.TryGetValue("validation-vertical-speeds", out var verticalSpeeds)
            ? ParseFloatList(verticalSpeeds)
            : [0f];
        var validationGroundedStates = rawOptions.TryGetValue("validation-grounded", out var groundedStates)
            ? ParseBoolList(groundedStates)
            : [sourceSegment.Samples[0].IsGrounded];
        var failures = new List<ReturnProofValidationFailure>();
        var validationCases = 0;
        foreach (var xOffset in validationXOffsets)
        {
            foreach (var bottomOffset in validationBottomOffsets)
            {
                foreach (var horizontalSpeed in validationHorizontalSpeeds)
                {
                    foreach (var verticalSpeed in validationVerticalSpeeds)
                    {
                        foreach (var grounded in validationGroundedStates)
                        {
                            validationCases += 1;
                            var result = RunFollowProofCase(
                                mapName,
                                area,
                                team,
                                classId,
                                sourceSegment,
                                sourceCase.Samples,
                                bestParameters.Value,
                                maxTicks,
                                xOffset,
                                bottomOffset,
                                horizontalSpeed,
                                verticalSpeed,
                                grounded,
                                useSpatialFollow);
                            if (!result.Passed)
                            {
                                failures.Add(new ReturnProofValidationFailure(
                                    xOffset,
                                    bottomOffset,
                                    horizontalSpeed,
                                    verticalSpeed,
                                    grounded,
                                    result.FailureReason,
                                    result.FinalX,
                                    result.FinalY,
                                    result.FinalBottom));
                            }
                        }
                    }
                }
            }
        }

        var validationArtifact = new ReturnProofValidationArtifact(
            validationCases,
            validationCases - failures.Count,
            failures.Count,
            failures.Count == 0,
            failures.ToArray());
        var entry = BuildObjectiveTapeFromFollowProofSamples(
            tapeName,
            team,
            classId,
            bestCase.Samples,
            sourceSegment);
        File.WriteAllText(
            Path.Combine(artifactDirectory, "candidate-tape.json"),
            JsonSerializer.Serialize(entry, outputJsonOptions));
        File.WriteAllText(
            Path.Combine(artifactDirectory, "validation.json"),
            JsonSerializer.Serialize(validationArtifact, outputJsonOptions));

        Console.WriteLine(
            $"followProofCandidate ticks={bestCase.ExecutedTicks} samples={bestCase.Samples.Count} params={bestParameters}");
        Console.WriteLine(
            $"followProofValidation passed={(validationArtifact.PassedAll ? 1 : 0)} cases={validationArtifact.Cases} passedCases={validationArtifact.Passed} failedCases={validationArtifact.Failed}");
        foreach (var failure in failures.Take(12))
        {
            Console.WriteLine(
                $"validationFailure xOff:{failure.StartXOffset:0.0} bottomOff:{failure.StartBottomOffset:0.0} hSpeed:{failure.StartHorizontalSpeedOffset:0.0} " +
                $"vSpeed:{failure.StartVerticalSpeedOffset:0.0} grounded:{(failure.StartGrounded ? 1 : 0)} final=({failure.FinalX:0.0},{failure.FinalY:0.0}) bottom={failure.FinalBottom:0.0} reason={failure.Reason}");
        }

        if (!validationArtifact.PassedAll)
        {
            Console.WriteLine($"followProof=rejected reason:validation_failed artifactDir={artifactDirectory}");
            return;
        }

        if (!accept)
        {
            Console.WriteLine($"followProof=accepted_candidate artifactDir={artifactDirectory} install=skipped reason:missing_accept_flag");
            return;
        }

        var tapePath = BotBrainObjectiveTapeStore.UpsertTape(level, entry);
        Console.WriteLine($"followProof=installed path={tapePath} artifactDir={artifactDirectory}");
    }

    private static FollowProofCaseResult RunFollowProofCase(
        string mapName,
        int area,
        PlayerTeam team,
        PlayerClass classId,
        BotBrainObjectiveTapeSegment sourceSegment,
        IReadOnlyList<TraversalLabTickSample> sourceSamples,
        FollowProofParameters parameters,
        int maxTicks,
        float startXOffset,
        float startBottomOffset,
        float startHorizontalSpeedOffset,
        float startVerticalSpeedOffset,
        bool startGrounded,
        bool useSpatialFollow)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        if (!world.TryLoadLevel(mapName, area, preservePlayerStats: false))
        {
            return FollowProofCaseResult.Failed("failed_to_load_level", float.PositiveInfinity);
        }

        world.PrepareLocalPlayerJoin();
        const byte botSlot = 2;
        if (!world.TryPrepareNetworkPlayerJoin(botSlot)
            || !world.TrySetNetworkPlayerTeam(botSlot, team)
            || !world.TryApplyNetworkPlayerClassSelection(botSlot, classId)
            || !world.TryGetNetworkPlayer(botSlot, out var player))
        {
            return FollowProofCaseResult.Failed("failed_to_prepare_player", float.PositiveInfinity);
        }

        var firstSample = sourceSegment.Samples[0];
        var lastSample = sourceSegment.Samples[^1];
        var startX = (sourceSegment.EntryX ?? firstSample.X) + startXOffset;
        var startBottom = (sourceSegment.EntryY.HasValue
            ? sourceSegment.EntryY.Value + (firstSample.Bottom - firstSample.Y)
            : firstSample.Bottom) + startBottomOffset;
        var startY = startBottom - player.CollisionBottomOffset;
        player.Spawn(team, startX, startY);
        player.TeleportTo(startX, startY);
        player.ResolveBlockingOverlap(world.Level, team);
        player.AddImpulse(firstSample.HorizontalSpeed + startHorizontalSpeedOffset, firstSample.VerticalSpeed + startVerticalSpeedOffset);
        player.PickUpIntel();
        player.RestoreMovementProbeState(startGrounded, remainingAirJumps: null, firstSample.MoveDirection == 0f ? team == PlayerTeam.Red ? -1f : 1f : firstSample.MoveDirection);
        player.SetAimWorldPosition(player.X + ((team == PlayerTeam.Red ? -1f : 1f) * 256f), player.Y);

        var exitX = sourceSegment.ExitX ?? lastSample.X;
        var exitBottom = sourceSegment.ExitY.HasValue
            ? sourceSegment.ExitY.Value + (lastSample.Bottom - lastSample.Y)
            : lastSample.Bottom;
        var exitRadius = sourceSegment.ExitRadius ?? 96f;
        var proofSamples = new List<BotBrainProofTapeSample>(maxTicks + 1);
        var previousInput = default(PlayerInputSnapshot);
        var jumpCooldown = 0;
        var lastHorizontalProgressTick = 0;
        var lastHorizontalProgressX = player.X;
        var recoveryRunupTicksRemaining = 0;
        var recoveryLaunchTicksRemaining = 0;
        var recoveryLaunchDirection = 0;
        var followSampleIndex = 0;
        var bestExitDistance = Distance(player.X, player.Bottom, exitX, exitBottom);
        AddFollowProofSample(proofSamples, player, previousInput, 0);

        for (var tick = 0; tick < maxTicks; tick += 1)
        {
            TraversalLabTickSample current;
            TraversalLabTickSample target;
            if (useSpatialFollow)
            {
                followSampleIndex = FindNearestFollowSampleIndex(sourceSamples, followSampleIndex, player.X, player.Bottom);
                current = sourceSamples[followSampleIndex];
                target = sourceSamples[Math.Min(sourceSamples.Count - 1, followSampleIndex + Math.Max(1, parameters.LookaheadTicks))];
            }
            else
            {
                current = FindFollowTargetSample(sourceSamples, tick);
                target = FindFollowTargetSample(sourceSamples, tick + parameters.LookaheadTicks);
            }
            var dx = target.X - player.X;
            var moveDirection = MathF.Abs(dx) > parameters.DeadZone
                ? dx < 0f ? -1 : 1
                : current.InputLeft == current.InputRight
                    ? 0
                    : current.InputRight ? 1 : -1;
            var targetIsAbove = target.Bottom < player.Bottom - parameters.JumpBottomThreshold;
            var sourceJump = current.InputUp || target.InputUp;
            var blocked = (moveDirection < 0 && current.BlockedLeft) || (moveDirection > 0 && current.BlockedRight);
            var stagnantTowardTarget = moveDirection != 0
                && tick - lastHorizontalProgressTick >= 14
                && MathF.Abs(player.X - lastHorizontalProgressX) < 10f;
            var forceJump = false;
            if (recoveryRunupTicksRemaining > 0)
            {
                moveDirection = -recoveryLaunchDirection;
                recoveryRunupTicksRemaining -= 1;
                if (recoveryRunupTicksRemaining == 0)
                {
                    recoveryLaunchTicksRemaining = 8;
                }
            }
            else if (recoveryLaunchTicksRemaining > 0)
            {
                moveDirection = recoveryLaunchDirection;
                forceJump = recoveryLaunchTicksRemaining == 8;
                recoveryLaunchTicksRemaining -= 1;
            }
            else if (stagnantTowardTarget
                && parameters.RunupTicks > 0
                && player.IsGrounded)
            {
                recoveryLaunchDirection = moveDirection;
                recoveryRunupTicksRemaining = parameters.RunupTicks - 1;
                moveDirection = -recoveryLaunchDirection;
                if (recoveryRunupTicksRemaining == 0)
                {
                    recoveryLaunchTicksRemaining = 8;
                }
            }

            var jump = false;
            if (forceJump
                && (player.IsGrounded || player.RemainingAirJumps > 0))
            {
                jump = true;
                jumpCooldown = parameters.JumpCooldownTicks;
            }
            else if (jumpCooldown <= 0
                && (sourceJump || targetIsAbove || blocked || stagnantTowardTarget)
                && (player.IsGrounded || player.RemainingAirJumps > 0))
            {
                jump = true;
                jumpCooldown = parameters.JumpCooldownTicks;
            }
            else if (jumpCooldown > 0)
            {
                jumpCooldown -= 1;
            }

            var drop = current.InputDown || target.InputDown || target.Bottom > player.Bottom + parameters.DropBottomThreshold;
            var aimDirection = moveDirection == 0 ? player.FacingDirectionX == 0f ? team == PlayerTeam.Red ? -1f : 1f : player.FacingDirectionX : moveDirection;
            var input = new PlayerInputSnapshot(
                Left: moveDirection < 0,
                Right: moveDirection > 0,
                Up: jump,
                Down: drop,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: player.X + (aimDirection * 256f),
                AimWorldY: player.Y,
                DebugKill: false,
                DropIntel: false,
                UseAbility: false);
            if (!world.TrySetNetworkPlayerInput(botSlot, input))
            {
                return FollowProofCaseResult.Failed("failed_to_apply_input", bestExitDistance);
            }

            world.AdvanceOneTick();
            previousInput = input;
            AddFollowProofSample(proofSamples, player, input, tick + 1);
            if (MathF.Abs(player.X - lastHorizontalProgressX) >= 18f)
            {
                lastHorizontalProgressTick = tick + 1;
                lastHorizontalProgressX = player.X;
            }

            var exitDistance = Distance(player.X, player.Bottom, exitX, exitBottom);
            bestExitDistance = MathF.Min(bestExitDistance, exitDistance);
            if (MathF.Abs(player.X - exitX) <= exitRadius
                && MathF.Abs(player.Bottom - exitBottom) <= exitRadius
                && player.IsCarryingIntel)
            {
                return new FollowProofCaseResult(
                    true,
                    string.Empty,
                    tick + 1,
                    player.X,
                    player.Y,
                    player.Bottom,
                    exitDistance,
                    proofSamples);
            }
        }

        var finalDistance = Distance(player.X, player.Bottom, exitX, exitBottom);
        return new FollowProofCaseResult(
            false,
            "exit_window_missed",
            maxTicks,
            player.X,
            player.Y,
            player.Bottom,
            finalDistance,
            proofSamples);
    }

    private static TraversalLabTickSample FindFollowTargetSample(IReadOnlyList<TraversalLabTickSample> samples, int tick)
    {
        var best = samples[0];
        for (var i = 1; i < samples.Count; i += 1)
        {
            if (samples[i].Tick > tick)
            {
                break;
            }

            best = samples[i];
        }

        return best;
    }

    private static int FindNearestFollowSampleIndex(
        IReadOnlyList<TraversalLabTickSample> samples,
        int previousIndex,
        float x,
        float bottom)
    {
        var start = Math.Clamp(previousIndex, 0, samples.Count - 1);
        var end = Math.Min(samples.Count - 1, previousIndex + 240);
        var bestIndex = previousIndex;
        var bestDistanceSq = float.PositiveInfinity;
        for (var i = start; i <= end; i += 1)
        {
            var dx = samples[i].X - x;
            var db = samples[i].Bottom - bottom;
            var distanceSq = (dx * dx) + (db * db);
            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static void AddFollowProofSample(
        List<BotBrainProofTapeSample> samples,
        PlayerEntity player,
        PlayerInputSnapshot input,
        int tick)
    {
        samples.Add(new BotBrainProofTapeSample(
            tick,
            player.X,
            player.Y,
            player.Bottom,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.IsGrounded,
            input.Left,
            input.Right,
            input.Up,
            input.Down,
            player.IsCarryingIntel));
    }

    private static BotBrainObjectiveTapeEntry BuildObjectiveTapeFromFollowProofSamples(
        string tapeName,
        PlayerTeam team,
        PlayerClass playerClass,
        IReadOnlyList<BotBrainProofTapeSample> sourceSamples,
        BotBrainObjectiveTapeSegment sourceSegment)
    {
        var samples = new List<BotBrainObjectiveTapeSample>(sourceSamples.Count);
        foreach (var sample in sourceSamples)
        {
            var moveDirection = sample.Left == sample.Right
                ? 0f
                : sample.Right ? 1f : -1f;
            samples.Add(new BotBrainObjectiveTapeSample
            {
                Tick = sample.Tick,
                X = sample.X,
                Y = sample.Y,
                Bottom = sample.Bottom,
                HorizontalSpeed = sample.HorizontalSpeed,
                VerticalSpeed = sample.VerticalSpeed,
                IsGrounded = sample.IsGrounded,
                MoveDirection = moveDirection,
                Jump = sample.Up,
                DropDown = sample.Down,
                IsCarryingIntel = sample.IsCarryingIntel,
            });
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
                    Source = "MotionProof",
                    EntryX = sourceSegment.EntryX ?? samples[0].X,
                    EntryY = sourceSegment.EntryY ?? samples[0].Y,
                    EntryRadius = sourceSegment.EntryRadius ?? 128f,
                    ExitX = sourceSegment.ExitX ?? samples[^1].X,
                    ExitY = sourceSegment.ExitY ?? samples[^1].Y,
                    ExitRadius = sourceSegment.ExitRadius ?? 144f,
                    Samples = NormalizeTapeSegmentTicks(samples),
                },
            ],
        };
    }

    private static ReturnProofSearchResult SearchReturnProofCandidate(
        string mapName,
        int area,
        PlayerTeam team,
        PlayerClass classId,
        float startX,
        float startBottom,
        float direction,
        int maxTicks,
        string tapeName,
        Dictionary<string, string> rawOptions,
        SimpleLevel level)
    {
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
        var xOffsets = rawOptions.TryGetValue("x-offsets", out var xOffsetText)
            ? ParseFloatList(xOffsetText)
            : [0f];
        var dropIntervals = rawOptions.TryGetValue("drop-intervals", out var dropIntervalText)
            ? ParseIntList(dropIntervalText)
            : [0];
        var dropHolds = rawOptions.TryGetValue("drop-holds", out var dropHoldText)
            ? ParseIntList(dropHoldText)
            : [1];
        var dropPhases = rawOptions.TryGetValue("drop-phases", out var dropPhaseText)
            ? ParseIntList(dropPhaseText)
            : [0];
        foreach (var xOffset in xOffsets)
        {
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
                                    foreach (var dropInterval in dropIntervals)
                                    {
                                        foreach (var dropHold in dropHolds)
                                        {
                                            foreach (var dropPhase in dropPhases)
                                            {
                                                if (interval == 0 && (hold != 1 || phase != 0))
                                                {
                                                    continue;
                                                }

                                                if (dropInterval == 0 && (dropHold != dropHolds[0] || dropPhase != dropPhases[0]))
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
                                                    Name = $"{tapeName}.x{xOffset:0}.pre{preTicks}.pi{preInterval}.i{interval}.h{hold}.p{phase}.d{dropInterval}.dh{dropHold}.dp{dropPhase}.b{bottomOffset:0}",
                                                    LevelName = mapName,
                                                    MapAreaIndex = area,
                                                    Team = team,
                                                    ClassId = classId,
                                                    Start = new TraversalLabStartState
                                                    {
                                                        X = startX + xOffset,
                                                        Bottom = startBottom + bottomOffset,
                                                        IsGrounded = true,
                                                        IsCarryingIntel = true,
                                                        FacingDirectionX = direction,
                                                    },
                                                    Steps = BuildDriveProbeSteps(maxTicks, direction, preTicks, preInterval, interval, hold, phase, dropInterval, dropHold, dropPhase),
                                                    MaxTicks = maxTicks,
                                                    TraceEveryTicks = 1,
                                                    Expectation = new TraversalLabExpectation
                                                    {
                                                        MustEverOverlapOwnIntelMarker = true,
                                                        MustCarryIntel = true,
                                                    },
                                                };
                                                var result = TraversalLabRunner.Run(scenario);
                                                var passedCase = result.Cases.Count > 0 ? result.Cases[0] : null;
                                                if (passedCase is null || !passedCase.Passed)
                                                {
                                                    if (passedCase is not null)
                                                    {
                                                        var distance = DistanceToOwnIntel(level, team, passedCase);
                                                        if (distance < bestFailedDistance)
                                                        {
                                                            bestFailedDistance = distance;
                                                            bestFailedCase = passedCase;
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
                    }
                }
            }
        }

        return new ReturnProofSearchResult(
            bestScenario,
            bestCase,
            bestFailedScenario,
            bestFailedCase,
            new ReturnProofSearchSummary(attempts, bestCase?.ExecutedTicks ?? -1, bestFailedDistance));
    }

    private static List<TraversalLabInputStep> BuildDriveProbeSteps(
        int maxTicks,
        float direction,
        int preTicks,
        int preJumpInterval,
        int jumpInterval,
        int jumpHold,
        int jumpPhase,
        int dropInterval,
        int dropHold,
        int dropPhase)
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
            var drop = !jump && dropInterval > 0 && phaseTick >= dropPhase && ((phaseTick - dropPhase) % dropInterval) < dropHold;
            steps.Add(new TraversalLabInputStep
                                    {
                Label = inPrePhase ? jump ? "pre_jump" : drop ? "pre_drop" : "pre_drive" : jump ? "drive_jump" : drop ? "drive_drop" : "drive",
                DurationTicks = 1,
                Left = activeDirection < 0f,
                Right = activeDirection > 0f,
                Up = jump,
                Down = drop,
                AimFacingDirectionX = activeDirection,
            });
        }

        return steps;
    }

    private static TraversalLabScenario BuildReturnProofValidationScenario(
        TraversalLabScenario candidate,
        Dictionary<string, string> rawOptions)
    {
        return new TraversalLabScenario
        {
            Name = $"{candidate.Name}.validation",
            LevelName = candidate.LevelName,
            MapAreaIndex = candidate.MapAreaIndex,
            Team = candidate.Team,
            ClassId = candidate.ClassId,
            Start = candidate.Start,
            Steps = candidate.Steps,
            MaxTicks = candidate.MaxTicks,
            TraceEveryTicks = Math.Max(1, candidate.MaxTicks),
            StartXOffsets = rawOptions.TryGetValue("validation-x-offsets", out var xOffsets)
                ? ParseFloatList(xOffsets)
                : [-24f, 0f, 24f],
            StartBottomOffsets = rawOptions.TryGetValue("validation-bottom-offsets", out var bottomOffsets)
                ? ParseFloatList(bottomOffsets)
                : [-12f, 0f, 12f],
            StartHorizontalSpeedOffsets = rawOptions.TryGetValue("validation-horizontal-speeds", out var horizontalSpeeds)
                ? ParseFloatList(horizontalSpeeds)
                : [-60f, 0f, 60f],
            StartVerticalSpeedOffsets = rawOptions.TryGetValue("validation-vertical-speeds", out var verticalSpeeds)
                ? ParseFloatList(verticalSpeeds)
                : [0f],
            GroundedStates = rawOptions.TryGetValue("validation-grounded", out var groundedStates)
                ? ParseBoolList(groundedStates)
                : [true],
            FacingDirections = [candidate.Start.FacingDirectionX],
            Expectation = new TraversalLabExpectation
            {
                MustEverOverlapOwnIntelMarker = true,
                MustCarryIntel = true,
            },
        };
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
        PlayerClass classId,
        IReadOnlyList<TraversalLabTickSample> samples)
    {
        return TrimTraversalSamplesAtIntel(level, team, classId, samples);
    }

    private static IReadOnlyList<TraversalLabTickSample> TrimTraversalSamplesAtIntel(
        SimpleLevel level,
        PlayerTeam team,
        PlayerClass classId,
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
            if (SampleIntersectsMarker(classId, sample, marker.X, marker.Y, 24f, 24f))
            {
                endIndex = i;
                break;
            }
        }

        return samples.Take(endIndex + 1).ToList();
    }

    private static bool SampleIntersectsMarker(
        PlayerClass classId,
        TraversalLabTickSample sample,
        float markerX,
        float markerY,
        float markerWidth,
        float markerHeight)
    {
        var classDefinition = CharacterClassCatalog.GetDefinition(classId);
        var left = sample.X + classDefinition.CollisionLeft;
        var top = sample.Y + classDefinition.CollisionTop;
        var right = sample.X + classDefinition.CollisionRight;
        var bottom = sample.Y + classDefinition.CollisionBottom;
        var markerLeft = markerX - (markerWidth / 2f);
        var markerRight = markerX + (markerWidth / 2f);
        var markerTop = markerY - (markerHeight / 2f);
        var markerBottom = markerY + (markerHeight / 2f);

        return left < markerRight
            && right > markerLeft
            && top < markerBottom
            && bottom > markerTop;
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

    private static HashSet<VerifiedNavProofRouteKind> BuildRouteActionOnlyKinds(Dictionary<string, string> options)
    {
        var kinds = new HashSet<VerifiedNavProofRouteKind>();
        if (ReadBoolOption(options, "route-action-only-pickup", false))
        {
            kinds.Add(VerifiedNavProofRouteKind.Pickup);
        }

        if (ReadBoolOption(options, "route-action-only-return", false))
        {
            kinds.Add(VerifiedNavProofRouteKind.Return);
        }

        return kinds;
    }

    private static List<string> ReadProofTracePathList(
        Dictionary<string, string> options,
        string singleKey,
        string listKey)
    {
        var paths = new List<string>();
        if (options.TryGetValue(singleKey, out var singlePath) && !string.IsNullOrWhiteSpace(singlePath))
        {
            paths.Add(Path.GetFullPath(singlePath));
        }

        if (options.TryGetValue(listKey, out var pathList) && !string.IsNullOrWhiteSpace(pathList))
        {
            paths.AddRange(
                pathList
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Path.GetFullPath));
        }

        return paths;
    }

    private static BotBrainRuntimeStateArtifact? TryReadRuntimeStateOption(
        Dictionary<string, string> options,
        JsonSerializerOptions jsonOptions)
    {
        var path = options.TryGetValue("start-state", out var startStatePath)
            ? startStatePath
            : options.TryGetValue("pickup-state", out var pickupStatePath)
                ? pickupStatePath
                : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        var state = JsonSerializer.Deserialize<BotBrainRuntimeStateArtifact>(
            File.ReadAllText(fullPath),
            jsonOptions);
        Console.WriteLine(
            $"motionProofPlannerStartState path={fullPath} tick={state.Tick} x={state.X:0.0} bottom={state.Bottom:0.0} " +
            $"speed=({state.HorizontalSpeed:0.0},{state.VerticalSpeed:0.0}) grounded={state.IsGrounded} airJumps={state.RemainingAirJumps}");
        return state;
    }

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

    private static List<bool> ParseBoolList(string text)
    {
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => value is "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
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
                    Source = tapeName.StartsWith("MotionProof.", StringComparison.OrdinalIgnoreCase) ? "MotionProof" : "TraversalLab",
                    EntryX = samples[0].X,
                    EntryY = samples[0].Y,
                    EntryRadius = 96f,
                    ExitX = samples[^1].X,
                    ExitY = samples[^1].Y,
                    ExitRadius = 144f,
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

        var first = selected[0];
        var last = selected[^1];
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
                    Source = "MotionProof",
                    EntryX = first.X,
                    EntryY = first.Y,
                    EntryRadius = 128f,
                    ExitX = last.X,
                    ExitY = last.Y,
                    ExitRadius = 180f,
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
    string ProofGraphPath,
    bool ProofGraphRequired,
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
            GetString(options, "proof-graph", string.Empty),
            GetBool(options, "proof-graph-required", false),
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

    public float RecentOscillationWindowMovement { get; set; }

    public int OscillationEvents { get; set; }

    public int FirstOscillationTick { get; set; } = -1;

    public string FirstOscillationTrace { get; set; } = string.Empty;

    public int ZeroInputTicks { get; set; }

    public int MoveFlips { get; set; }

    public int LowSpeedMoveFlips { get; set; }

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

    public string LastProofTrace { get; set; } = string.Empty;

    public string LastDirectTrace { get; set; } = string.Empty;

    public string LastTapeTrace { get; set; } = string.Empty;

    public string LastIssue { get; set; } = string.Empty;

    public int LastPathCount { get; set; }

    public int LastPathIndex { get; set; }

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

    public int ProofTicks { get; set; }

    public int ProofIdleTicks { get; set; }

    public int ProofAbortTicks { get; set; }

    public int DirectTicks { get; set; }

    public int GraphTicks { get; set; }

    public int TapeTicks { get; set; }

    public int CarryingIntelTick { get; set; } = -1;

    public string LastProofTraceKey { get; set; } = string.Empty;

    public string LastDirectTraceKey { get; set; } = string.Empty;

    public Dictionary<string, int> ProofAbortByReason { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> ProofIdleByReason { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> DirectRejectByReason { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> LocalMotionFailureByReason { get; } = new(StringComparer.Ordinal);
}

internal sealed record ReturnProofSearchResult(
    TraversalLabScenario? BestScenario,
    TraversalLabCaseResult? BestCase,
    TraversalLabScenario? BestFailedScenario,
    TraversalLabCaseResult? BestFailedCase,
    ReturnProofSearchSummary Summary);

internal sealed record ReturnProofSearchSummary(
    int Attempts,
    int BestScoreTick,
    float BestFailedDistance);

internal sealed record ReturnProofValidationArtifact(
    int Cases,
    int Passed,
    int Failed,
    bool PassedAll,
    ReturnProofValidationFailure[] Failures);

internal sealed record ReturnProofValidationFailure(
    float StartXOffset,
    float StartBottomOffset,
    float StartHorizontalSpeedOffset,
    float StartVerticalSpeedOffset,
    bool StartGrounded,
    string Reason,
    float FinalX,
    float FinalY,
    float FinalBottom);

internal sealed record MotionProofPlannerSummary(
    int Expansions,
    int Generated,
    int BestTick,
    float BestDistance,
    float BestX,
    float BestBottom,
    bool Solved,
    int SolvedTick,
    int MacroCount);

internal sealed record MotionProofPlannerCandidateResult(
    MotionProofPlannerNode Node,
    List<TraversalLabInputStep> Steps,
    TraversalLabScenario ValidationScenario,
    TraversalLabBatchResult Validation,
    ReturnProofValidationArtifact ValidationArtifact);

internal sealed record MotionProofPlannerNode(
    int Id,
    MotionProofPlannerNode? Parent,
    MotionProofPlannerMacro? Macro,
    MotionProofPlannerState State,
    int Tick,
    float Heuristic);

internal readonly record struct MotionProofPlannerState(
    float X,
    float Bottom,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool IsGrounded,
    int? RemainingAirJumps,
    float FacingDirectionX);

internal readonly record struct MotionProofPlannerMacro(
    string Label,
    int Direction,
    int Ticks,
    int JumpTicks,
    bool Drop);

internal readonly record struct MotionProofPlannerTransition(
    bool Valid,
    MotionProofPlannerState State)
{
    public static MotionProofPlannerTransition Invalid => new(false, default);
}

internal readonly record struct MotionProofPlannerCell(
    int X,
    int Bottom,
    int HorizontalSpeed,
    int VerticalSpeed,
    bool IsGrounded)
{
    public static MotionProofPlannerCell From(MotionProofPlannerState state) =>
        new(
            (int)MathF.Round(state.X / 36f),
            (int)MathF.Round(state.Bottom / 36f),
            (int)MathF.Round(state.HorizontalSpeed / 60f),
            (int)MathF.Round(state.VerticalSpeed / 60f),
            state.IsGrounded);
}

internal sealed record FollowProofSearchSummary(
    int Attempts,
    int BestScoreTick,
    float BestFailedDistance,
    FollowProofParameters? BestParameters,
    FollowProofParameters? BestFailedParameters);

internal readonly record struct FollowProofParameters(
    int LookaheadTicks,
    float DeadZone,
    float JumpBottomThreshold,
    int JumpCooldownTicks,
    float DropBottomThreshold,
    int RunupTicks);

internal sealed record FollowProofCaseResult(
    bool Passed,
    string FailureReason,
    int ExecutedTicks,
    float FinalX,
    float FinalY,
    float FinalBottom,
    float ExitDistance,
    IReadOnlyList<BotBrainProofTapeSample> Samples)
{
    public static FollowProofCaseResult Failed(string reason, float exitDistance) =>
        new(
            false,
            reason,
            0,
            0f,
            0f,
            0f,
            exitDistance,
            []);
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
