using OpenGarrison.BotAI;
using OpenGarrison.Core;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

NavBuildOptions options;
try
{
    options = NavBuildOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(NavBuildOptions.Usage);
    return 1;
}

if (!options.IsValid(out var validationError))
{
    Console.Error.WriteLine(validationError);
    Console.Error.WriteLine(NavBuildOptions.Usage);
    return 1;
}

var sourceContentRoot = ProjectSourceLocator.FindDirectory("Core/Content") ?? ContentRoot.Path;
ContentRoot.Initialize(sourceContentRoot);

if (options.ProbeOrangeLip)
{
    return RunOrangeLipProbe();
}

if (options.ProbeOrangeBranch)
{
    return RunOrangeBranchProbe();
}

if (options.RunBotScenarioHarness)
{
    return RunBotScenarioHarness(options);
}

var outputDirectory = options.OutputDirectory
    ?? ProjectSourceLocator.FindDirectory("Core/Content/BotNav")
    ?? Path.Combine(sourceContentRoot, "BotNav");
Directory.CreateDirectory(outputDirectory);

var catalog = SimpleLevelFactory.GetAvailableSourceLevels()
    .Where(entry => options.IncludeCustomMaps || !IsCustomMapEntry(entry))
    .Where(entry => options.MapNames.Count == 0 || options.MapNames.Contains(entry.Name))
    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (catalog.Length == 0)
{
    Console.Error.WriteLine("No maps matched the requested filters.");
    return 2;
}

var totalAssets = 0;
var invalidAssets = 0;
var reachabilityIssues = 0;
foreach (var entry in catalog)
{
    var baseLevel = SimpleLevelFactory.CreateImportedLevel(entry.Name);
    if (baseLevel is null)
    {
        Console.Error.WriteLine($"Failed to import map {entry.Name}.");
        continue;
    }

    for (var areaIndex = 1; areaIndex <= baseLevel.MapAreaCount; areaIndex += 1)
    {
        var level = areaIndex == 1 ? baseLevel : SimpleLevelFactory.CreateImportedLevel(entry.Name, areaIndex);
        if (level is null)
        {
            Console.Error.WriteLine($"Failed to import map {entry.Name} area {areaIndex}.");
            continue;
        }

        var fingerprint = BotNavigationLevelFingerprint.Compute(level);
        if (options.AuditShipped)
        {
            totalAssets += 1;
            if (!BotNavigationAssetStore.TryLoadModernShippedAsset(level, out var shippedAsset, out var shippedPath, out var shippedMessage, out var shippedValidation))
            {
                invalidAssets += 1;
                Console.WriteLine($"shipped map={entry.Name} area={areaIndex} nav=missing path={shippedPath} message={shippedMessage}");
                continue;
            }

            var repairAddedEdges = 0;
            if (options.RepairShipped)
            {
                var repair = BotNavigationModernGraphRepairer.AddMissingClientBotEdges(level, shippedAsset!);
                shippedAsset = repair.Asset;
                repairAddedEdges = repair.AddedEdges;
                if (repairAddedEdges > 0)
                {
                    BotNavigationAssetStore.SaveShipped(shippedAsset, outputDirectory);
                    shippedValidation = BotNavigationAssetValidator.Validate(level, shippedAsset);
                }
            }

            var reachabilityAudit = options.AuditReachability
                ? BotNavigationAssetValidator.AuditAttackReachability(level, shippedAsset!)
                : BotNavigationValidationResult.Valid;
            if (options.AuditCaptureRoutes)
            {
                AuditCaptureRoutes(level, shippedAsset!);
            }
            invalidAssets += shippedValidation.IsStructurallyValid ? 0 : 1;
            reachabilityIssues += reachabilityAudit.Issues.Count;
            Console.WriteLine(
                $"shipped map={entry.Name} area={areaIndex} strategy={shippedAsset!.BuildStrategy} nodes={shippedAsset.Nodes.Count} edges={shippedAsset.Edges.Count} repairedEdges={repairAddedEdges} nav={(shippedValidation.IsStructurallyValid ? "ok" : $"invalid:{shippedValidation.Issues.Count}")} reachability={(reachabilityAudit.IsStructurallyValid ? "ok" : $"issues:{reachabilityAudit.Issues.Count}")} path={shippedPath}");
            if (!shippedValidation.IsStructurallyValid)
            {
                Console.WriteLine($"  issues: {shippedValidation.BuildSummary()}");
            }

            if (!reachabilityAudit.IsStructurallyValid)
            {
                foreach (var issue in reachabilityAudit.Issues)
                {
                    Console.WriteLine($"  reachability: {issue.Message}");
                }
            }

            continue;
        }

        DeleteLegacyClassAssets(outputDirectory, level.Name, level.MapAreaIndex);
        DeleteLegacyProfileAssets(outputDirectory, level.Name, level.MapAreaIndex);

        var asset = BotNavigationModernPointGraphBuilder.Build(level, fingerprint);
        var validation = BotNavigationAssetValidator.Validate(level, asset);
        var generatedReachabilityAudit = options.AuditReachability
            ? BotNavigationAssetValidator.AuditAttackReachability(level, asset)
            : BotNavigationValidationResult.Valid;
        if (options.AuditCaptureRoutes)
        {
            AuditCaptureRoutes(level, asset);
        }
        BotNavigationAssetStore.SaveShipped(asset, outputDirectory);
        totalAssets += 1;
        invalidAssets += validation.IsStructurallyValid ? 0 : 1;
        reachabilityIssues += generatedReachabilityAudit.Issues.Count;
        var classTokens = string.Join("/", BotNavigationClasses.All.Select(BotNavigationClasses.GetFileToken));
        Console.WriteLine(
            $"built map={entry.Name} area={areaIndex} mesh=modern classes={classTokens} strategy={asset.BuildStrategy} nodes={asset.Nodes.Count} edges={asset.Edges.Count} ms={asset.Stats.BuildMilliseconds:F2} phases=sample:{asset.Stats.SurfaceSamplingMilliseconds:F1},anchors:{asset.Stats.AutoAnchorMilliseconds:F1},hints:{asset.Stats.HintNodeMilliseconds:F1},auto-edges:{asset.Stats.AutomaticEdgeMilliseconds:F1},hint-edges:{asset.Stats.HintEdgeMilliseconds:F1},drops:{asset.Stats.DropEdgeMilliseconds:F1} nav={(validation.IsStructurallyValid ? "ok" : $"invalid:{validation.Issues.Count}")} reachability={(generatedReachabilityAudit.IsStructurallyValid ? "ok" : $"issues:{generatedReachabilityAudit.Issues.Count}")}");
        if (!validation.IsStructurallyValid)
        {
            Console.WriteLine($"  issues: {validation.BuildSummary()}");
        }

        if (!generatedReachabilityAudit.IsStructurallyValid)
        {
            foreach (var issue in generatedReachabilityAudit.Issues)
            {
                Console.WriteLine($"  reachability: {issue.Message}");
            }
        }
    }
}

Console.WriteLine($"done assets={totalAssets} invalid={invalidAssets} reachabilityIssues={reachabilityIssues} output={outputDirectory}");
return 0;

static int RunBotScenarioHarness(NavBuildOptions options)
{
    ModernPracticeBotController.CompatNavPointSourceOverride = options.CompatNavPointSource;
    var cases = BuildBotScenarioCases(options);
    if (cases.Count == 0)
    {
        Console.Error.WriteLine("No bot scenario cases matched the requested filters.");
        return 2;
    }

    var passed = 0;
    var failed = 0;
    var timedOut = 0;
    var wallTimeoutMilliseconds = options.GetEffectiveWallTimeoutMilliseconds();
    var startedAt = Stopwatch.StartNew();
    for (var index = 0; index < cases.Count; index += 1)
    {
        var scenario = cases[index];
        Console.WriteLine(
            $"run case={index + 1}/{cases.Count} map={scenario.LevelName} team={scenario.Team} class={scenario.ClassId} mode={scenario.PathMode} seconds={scenario.SimulationSeconds} kind={scenario.Kind}");
        var result = RunBotScenarioCase(scenario, wallTimeoutMilliseconds, options.CollectBotDiagnostics);
        var progress = result.StartObjectiveDistance - result.BestObjectiveDistance;
        Console.WriteLine(
            $"result status={(result.Passed ? "pass" : result.TimedOut ? "timeout" : "fail")} map={scenario.LevelName} team={scenario.Team} class={scenario.ClassId} mode={scenario.PathMode} elapsedMs={result.WallMilliseconds} start={result.StartObjectiveDistance:0.0} best={result.BestObjectiveDistance:0.0} progress={progress:0.0} maxSpawn={result.MaxDistanceFromSpawn:0.0} longestNoProgress={result.LongestNoProgressSeconds:0.0}s maxStuck={result.MaxStuckTicks} satisfied={result.SatisfiedScenario} completed={result.CompletedObjective} reason={result.FailureReason}");
        if (!string.IsNullOrWhiteSpace(result.RouteSummary))
        {
            Console.WriteLine($"  routes: {result.RouteSummary}");
        }

        if (!result.Passed && !string.IsNullOrWhiteSpace(result.TraceSummary))
        {
            Console.WriteLine($"  trace: {result.TraceSummary}");
        }

        if (result.Passed)
        {
            passed += 1;
        }
        else
        {
            failed += 1;
            if (result.TimedOut)
            {
                timedOut += 1;
            }

            if (options.FailFast)
            {
                break;
            }
        }
    }

    Console.WriteLine(
        $"summary total={cases.Count} passed={passed} failed={failed} timedOut={timedOut} elapsedMs={startedAt.ElapsedMilliseconds}");
    return failed == 0 ? 0 : 3;
}

static int RunOrangeLipProbe()
{
    const float sourceTickSeconds = 1f / LegacyMovementModel.SourceTicksPerSecond;
    var world = new SimulationWorld();
    if (!world.TryLoadLevel("Orange"))
    {
        Console.Error.WriteLine("failed_to_load_level Orange");
        return 2;
    }

    world.PrepareLocalPlayerJoin();
    const byte botSlot = 2;
    if (!world.TryPrepareNetworkPlayerJoin(botSlot)
        || !world.TrySetNetworkPlayerTeam(botSlot, PlayerTeam.Blue)
        || !world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Scout)
        || !world.TryGetNetworkPlayer(botSlot, out var player))
    {
        Console.Error.WriteLine("failed_to_spawn_probe_player");
        return 2;
    }

    Console.WriteLine(
        $"probe class={player.ClassId} collision=({player.CollisionLeftOffset:0.0},{player.CollisionTopOffset:0.0},{player.CollisionRightOffset:0.0},{player.CollisionBottomOffset:0.0}) jumpStrength={player.JumpStrength:0.00} jumpSpeed={player.JumpSpeed / LegacyMovementModel.SourceTicksPerSecond:0.00} runPower={player.RunPower:0.00}");

    // Captured from the compat Orange route trace on the successful waist-jump trigger tick.
    var jumpTickState = CreateProbeState(player, x: 3967.8f, y: 1260.0f, horizontalPerTick: -7.9f, verticalPerTick: 0f, grounded: true);
    var jumpInput = new PlayerInputSnapshot(
        Left: true,
        Right: false,
        Up: true,
        Down: false,
        BuildSentry: false,
        DestroySentry: false,
        Taunt: false,
        FirePrimary: false,
        FireSecondary: false,
        AimWorldX: 3906f,
        AimWorldY: 1284f,
        DebugKill: false);
    var jumped = jumpTickState.Player.Advance(jumpInput, jumpPressed: true, jumpTickState.World.Level, PlayerTeam.Blue, sourceTickSeconds);
    Console.WriteLine(
        $"jump_tick jumped={jumped} before=({3967.8f:0.0},{1260.0f:0.0}) after=({jumpTickState.Player.X:0.0},{jumpTickState.Player.Y:0.0}) bottom={jumpTickState.Player.Bottom:0.0} vel=({jumpTickState.Player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.0},{jumpTickState.Player.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.0}) grounded={jumpTickState.Player.IsGrounded}");

    // Captured from the next tick, where the bot is already in the air and no longer pressing jump.
    var collisionTickState = CreateProbeState(player, x: 3959.9f, y: 1246.0f, horizontalPerTick: -7.9f, verticalPerTick: -7.7f, grounded: false);
    ProbeOccupancyAlongMove(
        collisionTickState.World,
        collisionTickState.Player,
        PlayerTeam.Blue,
        moveX: collisionTickState.Player.HorizontalSpeed * sourceTickSeconds,
        moveY: collisionTickState.Player.VerticalSpeed * sourceTickSeconds);
    var cooldownInput = jumpInput with { Up = false };
    var cooldownJumped = collisionTickState.Player.Advance(cooldownInput, jumpPressed: false, collisionTickState.World.Level, PlayerTeam.Blue, sourceTickSeconds);
    Console.WriteLine(
        $"cooldown_tick jumped={cooldownJumped} start=(3959.9,1246.0) after=({collisionTickState.Player.X:0.0},{collisionTickState.Player.Y:0.0}) bottom={collisionTickState.Player.Bottom:0.0} vel=({collisionTickState.Player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.0},{collisionTickState.Player.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.0}) grounded={collisionTickState.Player.IsGrounded}");
    return 0;
}

static int RunOrangeBranchProbe()
{
    var world = new SimulationWorld();
    if (!world.TryLoadLevel("Orange"))
    {
        Console.Error.WriteLine("failed_to_load_level Orange");
        return 2;
    }

    if (!BotNavigationAssetStore.TryLoadModernShippedAsset(world.Level, out var asset, out _, out var loadMessage, out _)
        || asset is null)
    {
        Console.Error.WriteLine($"failed_to_load_orange_nav_asset {loadMessage}");
        return 2;
    }

    world.PrepareLocalPlayerJoin();
    const byte botSlot = 2;
    if (!world.TryPrepareNetworkPlayerJoin(botSlot)
        || !world.TrySetNetworkPlayerTeam(botSlot, PlayerTeam.Blue)
        || !world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Scout)
        || !world.TryGetNetworkPlayer(botSlot, out var player))
    {
        Console.Error.WriteLine("failed_to_spawn_probe_player");
        return 2;
    }

    var objective = ResolveTeamObjective(world, player, PlayerTeam.Blue);
    var goalNode = FindNearestNode(asset.Nodes, objective.X, objective.Y, requireGroundSupport: true, maxDistance: 220f)
        ?? FindNearestNode(asset.Nodes, objective.X, objective.Y, requireGroundSupport: false, maxDistance: 220f);
    if (goalNode is null)
    {
        Console.Error.WriteLine("failed_to_resolve_goal_node");
        return 2;
    }

    var goalWeights = BuildSourceGoalWeights(asset, goalNode.Id);
    if (goalWeights is null)
    {
        Console.Error.WriteLine("failed_to_build_goal_weights");
        return 2;
    }

    Console.WriteLine($"orange_goal objective=({objective.X:0.0},{objective.Y:0.0}) node={goalNode.Id} nodePos=({goalNode.X:0.0},{goalNode.Y:0.0})");
    foreach (var nodeId in new[] { 1109, 1091, 1085, 1078, 1203, 1041, 1028 })
    {
        if (TryGetAssetNode(asset, nodeId, out var node))
        {
            Console.WriteLine($"  node {nodeId} pos=({node.X:0.0},{node.Y:0.0}) weight={GetSourceGoalWeight(goalWeights, nodeId)}");
        }
    }

    DumpOrangeCompatBranchScores(
        world,
        player,
        asset,
        goalWeights,
        (objective.X, objective.Y),
        currentPointId: 1091,
        previousCurrentPointId: 1091,
        previousNextPointId: 1085,
        lastCommittedPointId: 1109,
        secondAnchorBlockPointId: -1,
        secondAnchorBlockTicks: 0,
        playerX: 4132.7f,
        playerY: 1260.0f,
        horizontalPerTick: -7.6f,
        label: "probe_1091");
    DumpOrangeCompatBranchScores(
        world,
        player,
        asset,
        goalWeights,
        (objective.X, objective.Y),
        currentPointId: 1085,
        previousCurrentPointId: 1085,
        previousNextPointId: 1078,
        lastCommittedPointId: 1091,
        secondAnchorBlockPointId: -1,
        secondAnchorBlockTicks: 0,
        playerX: 3967.8f,
        playerY: 1260.0f,
        horizontalPerTick: -7.9f,
        label: "probe_1085");
    return 0;
}

static void DumpOrangeCompatBranchScores(
    SimulationWorld world,
    PlayerEntity template,
    BotNavigationAsset asset,
    int[] goalWeights,
    (float X, float Y) destination,
    int currentPointId,
    int previousCurrentPointId,
    int previousNextPointId,
    int lastCommittedPointId,
    int secondAnchorBlockPointId,
    int secondAnchorBlockTicks,
    float playerX,
    float playerY,
    float horizontalPerTick,
    string label)
{
    if (!TryGetAssetNode(asset, currentPointId, out var currentNode))
    {
        Console.WriteLine($"{label} missing_current_node={currentPointId}");
        return;
    }

    var probeState = CreateProbeState(template, playerX, playerY, horizontalPerTick, verticalPerTick: 0f, grounded: true);
    var player = probeState.Player;
    var currentWeight = GetSourceGoalWeight(goalWeights, currentPointId);
    GetToolJumpProfile(player.ClassId, out var classJumpRange, out var classJumpHeight, out var classJumpHeightTotal);
    var currentFeetY = currentNode.Y;
    var feetY = player.Bottom;
    var horizontalSpeedSource = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
    Console.WriteLine($"{label} player=({player.X:0.0},{player.Y:0.0}) bottom={player.Bottom:0.0} hs={horizontalSpeedSource:0.0} cp={currentPointId} cpw={currentWeight} dest=({destination.X:0.0},{destination.Y:0.0})");

    var candidates = asset.Edges
        .Where(edge => edge.FromNodeId == currentPointId)
        .Select(edge => edge.ToNodeId)
        .Distinct()
        .OrderBy(id => id)
        .ToArray();
    foreach (var candidateId in candidates)
    {
        if (!TryGetAssetNode(asset, candidateId, out var candidateNode))
        {
            continue;
        }

        var candidateWeight = GetSourceGoalWeight(goalWeights, candidateId);
        var reverseBlocked = candidateNode.ReverseOnlyBlockedFromNodeIds.Contains(currentPointId);
        var yPoint2 = candidateNode.Y;
        var xPoint2 = candidateNode.X;
        var jumpRiseNeeded = MathF.Max(0f, currentFeetY - yPoint2);
        var jumpDistanceNeeded = MathF.Abs(xPoint2 - currentNode.X);
        var connectionUsable = candidateWeight < currentWeight && !reverseBlocked && jumpRiseNeeded <= classJumpHeightTotal;
        if (connectionUsable && jumpDistanceNeeded > classJumpRange)
        {
            connectionUsable = ToolCompatLineHitsSolid(world, currentNode.X, currentFeetY + 2f, xPoint2, yPoint2 + 2f);
        }

        if (!connectionUsable)
        {
            Console.WriteLine($"  cand {candidateId} pos=({xPoint2:0.0},{yPoint2:0.0}) w={candidateWeight} usable={connectionUsable} reverseBlocked={reverseBlocked} rise={jumpRiseNeeded:0.0} run={jumpDistanceNeeded:0.0}");
            continue;
        }

        var distanceScore = DistanceBetween(xPoint2, yPoint2, destination.X, destination.Y);
        var candidateScore = candidateWeight * 1000f + distanceScore;
        var previousCurrentPenalty = candidateId == previousCurrentPointId ? 220f : 0f;
        var previousNextBonus = candidateId == previousNextPointId ? -120f : 0f;
        var committedPenalty = candidateId == lastCommittedPointId ? 340f : 0f;
        var stalledPenalty = currentPointId == previousCurrentPointId && candidateId != previousNextPointId && MathF.Abs(horizontalSpeedSource) < 1.2f ? 220f : 0f;
        var secondAnchorPenalty = secondAnchorBlockTicks > 0 && candidateId == secondAnchorBlockPointId && currentPointId != secondAnchorBlockPointId ? 5000f : 0f;
        candidateScore += previousCurrentPenalty + previousNextBonus + committedPenalty + stalledPenalty + secondAnchorPenalty;

        var candidateRiseFromPlayer = feetY - yPoint2;
        var candidateRunFromPlayer = MathF.Abs(xPoint2 - player.X);
        var lipFrontFootBlocked = false;
        var lipHeadRoom = false;
        var lipPenalty = 0f;
        var lipBonus = 0f;
        if (currentPointId == previousCurrentPointId && MathF.Abs(horizontalSpeedSource) < 1.2f && candidateRunFromPlayer < 96f)
        {
            var lipProbeDirection = MathF.Sign(xPoint2 - player.X);
            if (lipProbeDirection != 0f)
            {
                var lipProbeX = player.X + (7f * lipProbeDirection);
                lipFrontFootBlocked = ToolCompatLineHitsSolid(world, lipProbeX, feetY + 3f, lipProbeX, feetY - 1f);
                lipHeadRoom = !ToolCompatLineHitsSolid(world, lipProbeX, feetY - (classJumpHeight + 2f), lipProbeX, feetY - 12f);
                if (lipFrontFootBlocked && lipHeadRoom && candidateRiseFromPlayer >= 0f && candidateRiseFromPlayer <= 10f)
                {
                    lipPenalty = 320f;
                    candidateScore += lipPenalty;
                }

                if (lipFrontFootBlocked && lipHeadRoom && yPoint2 >= currentFeetY + 2f)
                {
                    lipBonus = -110f;
                    candidateScore += lipBonus;
                }
            }
        }

        Console.WriteLine(
            $"  cand {candidateId} pos=({xPoint2:0.0},{yPoint2:0.0}) w={candidateWeight} score={candidateScore:0.0} dist={distanceScore:0.0} prevCur={previousCurrentPenalty:0.0} prevNext={previousNextBonus:0.0} last={committedPenalty:0.0} stalled={stalledPenalty:0.0} anchor={secondAnchorPenalty:0.0} lipPenalty={lipPenalty:0.0} lipBonus={lipBonus:0.0} lip={lipFrontFootBlocked}/{lipHeadRoom} rise={jumpRiseNeeded:0.0} run={jumpDistanceNeeded:0.0}");
    }
}

static bool TryGetAssetNode(BotNavigationAsset asset, int nodeId, out BotNavigationNode node)
{
    for (var index = 0; index < asset.Nodes.Count; index += 1)
    {
        if (asset.Nodes[index].Id == nodeId)
        {
            node = asset.Nodes[index];
            return true;
        }
    }

    node = default;
    return false;
}

static void GetToolJumpProfile(
    PlayerClass classId,
    out float jumpRange,
    out float jumpHeight,
    out float jumpHeightTotal)
{
    jumpRange = 120f;
    jumpHeight = 60f;
    jumpHeightTotal = 60f;
    switch (classId)
    {
        case PlayerClass.Scout:
            jumpRange = 168f;
            jumpHeight = 60f;
            jumpHeightTotal = 120f;
            break;

        case PlayerClass.Heavy:
            jumpRange = 96f;
            jumpHeight = 60f;
            jumpHeightTotal = 60f;
            break;
    }
}

static bool ToolCompatLineHitsSolid(
    SimulationWorld world,
    float originX,
    float originY,
    float targetX,
    float targetY)
{
    var method = typeof(ModernPracticeBotController).GetMethod(
        "CompatLineHitsSolid",
        BindingFlags.Static | BindingFlags.NonPublic);
    if (method is null)
    {
        throw new InvalidOperationException("CompatLineHitsSolid not found.");
    }

    return method.Invoke(null, [world, originX, originY, targetX, targetY]) is true;
}

static void ProbeOccupancyAlongMove(
    SimulationWorld world,
    PlayerEntity player,
    PlayerTeam team,
    float moveX,
    float moveY)
{
    const float step = 0.05f;
    var startX = player.X;
    var startY = player.Y;
    var totalDistance = MathF.Sqrt((moveX * moveX) + (moveY * moveY));
    var directionX = totalDistance > 0f ? moveX / totalDistance : 0f;
    var directionY = totalDistance > 0f ? moveY / totalDistance : 0f;
    var lastFreeX = startX;
    var lastFreeY = startY;
    var blockedX = startX;
    var blockedY = startY;
    var blocked = false;
    var stepDirection = MathF.Sign(moveX);
    var lastForwardFreeX = startX;
    var lastForwardFreeY = startY;
    var lastStepUpFreeX = startX;
    var lastStepUpFreeY = startY;
    var sawForwardFree = false;
    var sawStepUpFree = false;

    for (var distance = step; distance <= totalDistance + 0.001f; distance += step)
    {
        var probeX = startX + (directionX * distance);
        var probeY = startY + (directionY * distance);
        if (InvokeCanOccupy(player, world.Level, team, probeX, probeY))
        {
            lastFreeX = probeX;
            lastFreeY = probeY;
            continue;
        }

        blocked = true;
        blockedX = probeX;
        blockedY = probeY;
        break;
    }

    if (stepDirection != 0f)
    {
        for (var distance = step; distance <= totalDistance + 0.001f; distance += step)
        {
            var probeX = startX + (directionX * distance);
            var probeY = startY + (directionY * distance);
            if (!InvokeCanOccupy(player, world.Level, team, probeX, probeY))
            {
                break;
            }

            if (InvokeCanOccupy(player, world.Level, team, probeX + stepDirection, probeY))
            {
                sawForwardFree = true;
                lastForwardFreeX = probeX;
                lastForwardFreeY = probeY;
            }

            if (InvokeCanOccupy(player, world.Level, team, probeX + stepDirection, probeY - 6f))
            {
                sawStepUpFree = true;
                lastStepUpFreeX = probeX;
                lastStepUpFreeY = probeY;
            }
        }
    }

    Console.WriteLine(
        $"cooldown_scan move=({moveX:0.00},{moveY:0.00}) dist={totalDistance:0.00} blocked={blocked} lastFree=({lastFreeX:0.00},{lastFreeY:0.00}) blockedAt=({blockedX:0.00},{blockedY:0.00})");
    if (!blocked)
    {
        return;
    }

    var onePixelForwardFree = InvokeCanOccupy(player, world.Level, team, lastFreeX + stepDirection, lastFreeY);
    var sourceStepUpFree = InvokeCanOccupy(player, world.Level, team, lastFreeX + stepDirection, lastFreeY - 6f);
    var sourceCeilingDownFree = InvokeCanOccupy(player, world.Level, team, lastFreeX + stepDirection, lastFreeY + 6f);
    var roundX = MathF.Round(lastFreeX);
    var roundY = MathF.Round(lastFreeY);
    var floorX = MathF.Floor(lastFreeX);
    var floorY = MathF.Floor(lastFreeY);
    var ceilX = MathF.Ceiling(lastFreeX);
    var ceilY = MathF.Ceiling(lastFreeY);
    var roundedStepUpFree = InvokeCanOccupy(player, world.Level, team, roundX + stepDirection, roundY - 6f);
    var flooredStepUpFree = InvokeCanOccupy(player, world.Level, team, floorX + stepDirection, floorY - 6f);
    var ceiledStepUpFree = InvokeCanOccupy(player, world.Level, team, ceilX + stepDirection, ceilY - 6f);
    Console.WriteLine(
        $"contact_checks onePixelForwardFree={onePixelForwardFree} sourceStepUpFree={sourceStepUpFree} sourceCeilingDownFree={sourceCeilingDownFree}");
    Console.WriteLine(
        $"quantized_stepup round=({roundX:0},{roundY:0}) free={roundedStepUpFree} floor=({floorX:0},{floorY:0}) free={flooredStepUpFree} ceil=({ceilX:0},{ceilY:0}) free={ceiledStepUpFree}");
    Console.WriteLine(
        $"segment_checks sawForwardFree={sawForwardFree} lastForwardFree=({lastForwardFreeX:0.00},{lastForwardFreeY:0.00}) sawStepUpFree={sawStepUpFree} lastStepUpFree=({lastStepUpFreeX:0.00},{lastStepUpFreeY:0.00})");
}

static (SimulationWorld World, PlayerEntity Player) CreateProbeState(
    PlayerEntity template,
    float x,
    float y,
    float horizontalPerTick,
    float verticalPerTick,
    bool grounded)
{
    var world = new SimulationWorld();
    _ = world.TryLoadLevel("Orange");
    world.PrepareLocalPlayerJoin();
    const byte botSlot = 2;
    _ = world.TryPrepareNetworkPlayerJoin(botSlot);
    _ = world.TrySetNetworkPlayerTeam(botSlot, PlayerTeam.Blue);
    _ = world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Scout);
    _ = world.TryGetNetworkPlayer(botSlot, out var player);
    player.TeleportTo(x, y);
    player.AddImpulse(
        horizontalPerTick * LegacyMovementModel.SourceTicksPerSecond,
        verticalPerTick * LegacyMovementModel.SourceTicksPerSecond);
    SetPrivateInstanceProperty(player, nameof(PlayerEntity.IsGrounded), grounded);
    SetPrivateInstanceProperty(player, nameof(PlayerEntity.RemainingAirJumps), template.MaxAirJumps);
    return (world, player);
}

static bool InvokeCanOccupy(PlayerEntity player, SimpleLevel level, PlayerTeam team, float x, float y)
{
    var method = typeof(PlayerEntity).GetMethod(
        "CanOccupy",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (method is null)
    {
        throw new InvalidOperationException("PlayerEntity.CanOccupy not found.");
    }

    return method.Invoke(player, [level, team, x, y]) is true;
}

static void SetPrivateInstanceProperty<T>(object target, string propertyName, T value)
{
    var property = target.GetType().GetProperty(
        propertyName,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (property is null)
    {
        throw new InvalidOperationException($"Property '{propertyName}' not found on {target.GetType().Name}.");
    }

    property.SetValue(target, value);
}

static List<BotScenarioCase> BuildBotScenarioCases(NavBuildOptions options)
{
    var cases = new List<BotScenarioCase>();
    var scenarioClasses = options.GetEffectiveScenarioClasses();
    if (options.RunStockScoreMatrix)
    {
        foreach (var mapName in GetStockScoreMaps().Where(map => options.MapNames.Count == 0 || options.MapNames.Contains(map)))
        {
            foreach (var scenarioClass in scenarioClasses)
            {
                cases.Add(new BotScenarioCase(mapName, PlayerTeam.Blue, scenarioClass, options.GetEffectiveScenarioSeconds(), options.ScenarioPathMode, BotScenarioKind.Score));
                cases.Add(new BotScenarioCase(mapName, PlayerTeam.Red, scenarioClass, options.GetEffectiveScenarioSeconds(), options.ScenarioPathMode, BotScenarioKind.Score));
            }
        }

        return cases;
    }

    if (options.RunStockCtfKothMatrix)
    {
        foreach (var mapName in GetStockCtfAndKothMaps().Where(map => options.MapNames.Count == 0 || options.MapNames.Contains(map)))
        {
            if (options.ScenarioKind == BotScenarioKind.ReturnCap
                && (!TryGetStockMapMode(mapName, out var modeForReturnCap, out _)
                    || modeForReturnCap != GameModeKind.CaptureTheFlag))
            {
                continue;
            }

            foreach (var scenarioClass in scenarioClasses)
            {
                cases.Add(new BotScenarioCase(mapName, PlayerTeam.Blue, scenarioClass, options.GetEffectiveScenarioSeconds(), options.ScenarioPathMode, options.ScenarioKind));
                cases.Add(new BotScenarioCase(mapName, PlayerTeam.Red, scenarioClass, options.GetEffectiveScenarioSeconds(), options.ScenarioPathMode, options.ScenarioKind));
            }
        }

        return cases;
    }

    foreach (var mapName in options.MapNames)
    {
        foreach (var scenarioClass in scenarioClasses)
        {
            cases.Add(new BotScenarioCase(mapName, options.ScenarioTeam, scenarioClass, options.GetEffectiveScenarioSeconds(), options.ScenarioPathMode, options.ScenarioKind));
        }
    }

    return cases;
}

static BotScenarioResult RunBotScenarioCase(BotScenarioCase scenario, int wallTimeoutMilliseconds, bool collectDiagnostics)
{
    var wallClock = Stopwatch.StartNew();
    var world = new SimulationWorld();
    if (!world.TryLoadLevel(scenario.LevelName))
    {
        return new BotScenarioResult(
            Passed: false,
            TimedOut: false,
            SatisfiedScenario: false,
            CompletedObjective: false,
            StartObjectiveDistance: 0f,
            BestObjectiveDistance: 0f,
            MaxDistanceFromSpawn: 0f,
            MaxStuckTicks: 0,
            LongestNoProgressSeconds: 0f,
            WallMilliseconds: wallClock.ElapsedMilliseconds,
            RouteSummary: string.Empty,
            TraceSummary: string.Empty,
            FailureReason: "failed_to_load_level");
    }

    world.PrepareLocalPlayerJoin();

    const byte botSlot = 2;
    if (!world.TryPrepareNetworkPlayerJoin(botSlot)
        || !world.TrySetNetworkPlayerTeam(botSlot, scenario.Team)
        || !world.TryApplyNetworkPlayerClassSelection(botSlot, scenario.ClassId)
        || !world.TryGetNetworkPlayer(botSlot, out var bot))
    {
        return new BotScenarioResult(
            Passed: false,
            TimedOut: false,
            SatisfiedScenario: false,
            CompletedObjective: false,
            StartObjectiveDistance: 0f,
            BestObjectiveDistance: 0f,
            MaxDistanceFromSpawn: 0f,
            MaxStuckTicks: 0,
            LongestNoProgressSeconds: 0f,
            WallMilliseconds: wallClock.ElapsedMilliseconds,
            RouteSummary: string.Empty,
            TraceSummary: string.Empty,
            FailureReason: "failed_to_spawn_bot");
    }

    var controller = new ModernPracticeBotController
    {
        CollectDiagnostics = collectDiagnostics,
    };
    var controlledSlots = new Dictionary<byte, ControlledBotSlot>
    {
        [botSlot] = new(botSlot, scenario.Team, scenario.ClassId, scenario.PathMode),
    };

    if (!TryPrepareScenario(world, bot, scenario, out var setupFailureReason))
    {
        return new BotScenarioResult(
            Passed: false,
            TimedOut: false,
            SatisfiedScenario: false,
            CompletedObjective: false,
            StartObjectiveDistance: 0f,
            BestObjectiveDistance: 0f,
            MaxDistanceFromSpawn: 0f,
            MaxStuckTicks: 0,
            LongestNoProgressSeconds: 0f,
            WallMilliseconds: wallClock.ElapsedMilliseconds,
            RouteSummary: string.Empty,
            TraceSummary: string.Empty,
            FailureReason: setupFailureReason ?? "failed_to_prepare_scenario");
    }

    var startX = bot.X;
    var startY = bot.Y;
    var objective = ResolveTeamObjective(world, bot, scenario.Team);
    var initialRedCaps = world.RedCaps;
    var initialBlueCaps = world.BlueCaps;
    var initialRedKothTicks = world.KothRedTimerTicksRemaining;
    var initialBlueKothTicks = world.KothBlueTimerTicksRemaining;
    var startObjectiveDistance = DistanceBetween(bot.X, bot.Y, objective.X, objective.Y);
    var bestObjectiveDistance = startObjectiveDistance;
    var maxDistanceFromSpawn = 0f;
    var maxStuckTicks = 0;
    var ticksSinceBestObjectiveProgress = 0;
    var longestNoProgressTicks = 0;
    var routeLabelCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    var routeTrace = collectDiagnostics ? new List<string>() : null;
    var objectiveTrace = collectDiagnostics ? new List<string>() : null;
    var completedObjective = false;
    var satisfiedScenario = false;
    var totalTicks = world.Config.TicksPerSecond * scenario.SimulationSeconds;
    var lastCarryState = bot.IsCarryingIntel;
    var lastRedCaps = world.RedCaps;
    var lastBlueCaps = world.BlueCaps;
    var lastRedKothTicks = world.KothRedTimerTicksRemaining;
    var lastBlueKothTicks = world.KothBlueTimerTicksRemaining;
    var lastPointState = DescribeTrackedControlPoint(world, objective.ControlPointIndex);

    for (var tick = 0; tick < totalTicks; tick += 1)
    {
        if (wallClock.ElapsedMilliseconds > wallTimeoutMilliseconds)
        {
            return CreateScenarioResult(
                scenario,
                world,
                bot,
                startObjectiveDistance,
                bestObjectiveDistance,
                maxDistanceFromSpawn,
                maxStuckTicks,
                longestNoProgressTicks,
                wallClock.ElapsedMilliseconds,
                routeLabelCounts,
                routeTrace,
                objectiveTrace,
                satisfiedScenario,
                completedObjective,
                failureReason: $"wall_timeout_{wallTimeoutMilliseconds}ms",
                timedOut: true);
        }

        var inputs = controller.BuildInputs(world, controlledSlots);
        if (!inputs.TryGetValue(botSlot, out var input) || !world.TrySetNetworkPlayerInput(botSlot, input))
        {
            return CreateScenarioResult(
                scenario,
                world,
                bot,
                startObjectiveDistance,
                bestObjectiveDistance,
                maxDistanceFromSpawn,
                maxStuckTicks,
                longestNoProgressTicks,
                wallClock.ElapsedMilliseconds,
                routeLabelCounts,
                routeTrace,
                objectiveTrace,
                satisfiedScenario,
                completedObjective,
                failureReason: "failed_to_apply_input",
                timedOut: false);
        }

        world.AdvanceOneTick();

        objective = ResolveTeamObjective(world, bot, scenario.Team);
        completedObjective = HasCompletedTeamObjective(
            world,
            objective,
            scenario.Team,
            initialRedCaps,
            initialBlueCaps,
            initialRedKothTicks,
            initialBlueKothTicks);

        var diagnostic = collectDiagnostics
            ? controller.LastDiagnostics.Entries.Single(entry => entry.Slot == botSlot)
            : default;
        var objectiveDistance = DistanceBetween(bot.X, bot.Y, objective.X, objective.Y);
        var botInCaptureZone = IsPlayerInCaptureZone(world, bot);
        if (objectiveDistance + 16f < bestObjectiveDistance
            || botInCaptureZone
            || (collectDiagnostics && diagnostic.RouteLabel.EndsWith("capture_zone_hold", StringComparison.Ordinal)))
        {
            bestObjectiveDistance = botInCaptureZone ? 0f : Math.Min(bestObjectiveDistance, objectiveDistance);
            ticksSinceBestObjectiveProgress = 0;
        }
        else
        {
            ticksSinceBestObjectiveProgress += 1;
            longestNoProgressTicks = Math.Max(longestNoProgressTicks, ticksSinceBestObjectiveProgress);
        }

        maxDistanceFromSpawn = MathF.Max(maxDistanceFromSpawn, DistanceBetween(bot.X, bot.Y, startX, startY));
        if (collectDiagnostics)
        {
            maxStuckTicks = Math.Max(maxStuckTicks, diagnostic.StuckTicks);
            routeLabelCounts.TryGetValue(diagnostic.RouteLabel, out var routeLabelCount);
            routeLabelCounts[diagnostic.RouteLabel] = routeLabelCount + 1;
        }
        if (collectDiagnostics && tick % Math.Max(1, world.Config.TicksPerSecond / 30) == 0)
        {
            routeTrace!.Add(
                $"t={tick / (float)world.Config.TicksPerSecond:0.0} pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0} vel=({bot.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.0},{bot.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.0}) target=({diagnostic.MovementTargetX:0.0},{diagnostic.MovementTargetY:0.0}) input=L{input.Left}/R{input.Right}/J{input.Up} req={diagnostic.RequestedHorizontal}/{diagnostic.RequestedJump} move={diagnostic.MoveDebug} jump={diagnostic.JumpDebug} g={diagnostic.IsGrounded}/{diagnostic.ProbeGrounded} st={diagnostic.StuckTicks}/{diagnostic.ModernStuckTicks} d={objectiveDistance:0.0} route={diagnostic.RouteLabel} cp={diagnostic.CurrentPointId} np={diagnostic.NextPointId} np2={diagnostic.NextPoint2Id} prev={diagnostic.PreviousCurrentPointId}->{diagnostic.PreviousNextPointId} goal={diagnostic.RouteGoalNodeId} sab={diagnostic.SecondAnchorBlockPointId}/{diagnostic.SecondAnchorBlockTicksRemaining}");
            if (routeTrace.Count > 120)
            {
                routeTrace.RemoveAt(0);
            }
        }

        if (collectDiagnostics)
        {
            var timeSeconds = tick / (float)world.Config.TicksPerSecond;
            if (bot.IsCarryingIntel != lastCarryState)
            {
                objectiveTrace!.Add($"t={timeSeconds:0.0} carry={bot.IsCarryingIntel}");
                lastCarryState = bot.IsCarryingIntel;
            }

            if (world.RedCaps != lastRedCaps || world.BlueCaps != lastBlueCaps)
            {
                objectiveTrace!.Add($"t={timeSeconds:0.0} caps={world.RedCaps}-{world.BlueCaps}");
                lastRedCaps = world.RedCaps;
                lastBlueCaps = world.BlueCaps;
            }

            if (world.KothRedTimerTicksRemaining != lastRedKothTicks || world.KothBlueTimerTicksRemaining != lastBlueKothTicks)
            {
                objectiveTrace!.Add($"t={timeSeconds:0.0} koth={world.KothRedTimerTicksRemaining}-{world.KothBlueTimerTicksRemaining}");
                lastRedKothTicks = world.KothRedTimerTicksRemaining;
                lastBlueKothTicks = world.KothBlueTimerTicksRemaining;
            }

            var pointState = DescribeTrackedControlPoint(world, objective.ControlPointIndex);
            if (!string.Equals(pointState, lastPointState, StringComparison.Ordinal))
            {
                objectiveTrace!.Add($"t={timeSeconds:0.0} point={pointState}");
                lastPointState = pointState;
            }

            if (objectiveTrace.Count > 40)
            {
                objectiveTrace.RemoveAt(0);
            }
        }

        satisfiedScenario = HasSatisfiedScenario(
            scenario,
            world,
            bot,
            objective,
            completedObjective,
            botInCaptureZone,
            startObjectiveDistance,
            bestObjectiveDistance);
        if (satisfiedScenario)
        {
            break;
        }
    }

    string? failureReason = null;
    if (!satisfiedScenario)
    {
        failureReason = scenario.Kind switch
        {
            BotScenarioKind.Score => completedObjective
                ? null
                : $"objective_incomplete_score={world.RedCaps}-{world.BlueCaps}_koth={world.KothRedTimerTicksRemaining}-{world.KothBlueTimerTicksRemaining}",
            BotScenarioKind.Route => maxDistanceFromSpawn < 128f
                ? $"moved_only_{maxDistanceFromSpawn:0.0}px"
                : $"route_progress_{(startObjectiveDistance - bestObjectiveDistance):0.0}px",
            BotScenarioKind.Arrival => world.MatchRules.Mode == GameModeKind.CaptureTheFlag
                ? $"arrival_miss_carry={bot.IsCarryingIntel}_best={bestObjectiveDistance:0.0}"
                : $"arrival_miss_best={bestObjectiveDistance:0.0}",
            BotScenarioKind.ReturnCap => $"return_cap_incomplete_score={world.RedCaps}-{world.BlueCaps}",
            _ => $"scenario_unsatisfied_{scenario.Kind}"
        };
        if (longestNoProgressTicks >= world.Config.TicksPerSecond * 6)
        {
            failureReason += $"_no_progress_{longestNoProgressTicks / (float)world.Config.TicksPerSecond:0.0}s";
        }
        else if (maxStuckTicks >= 90)
        {
            failureReason += $"_stuck_{maxStuckTicks}_ticks";
        }
    }

    return CreateScenarioResult(
        scenario,
        world,
        bot,
        startObjectiveDistance,
        bestObjectiveDistance,
        maxDistanceFromSpawn,
        maxStuckTicks,
        longestNoProgressTicks,
        wallClock.ElapsedMilliseconds,
        routeLabelCounts,
        routeTrace,
        objectiveTrace,
        satisfiedScenario,
        completedObjective,
        failureReason,
        timedOut: false);
}

static BotScenarioResult CreateScenarioResult(
    BotScenarioCase scenario,
    SimulationWorld world,
    PlayerEntity bot,
    float startObjectiveDistance,
    float bestObjectiveDistance,
    float maxDistanceFromSpawn,
    int maxStuckTicks,
    int longestNoProgressTicks,
    long wallMilliseconds,
    Dictionary<string, int> routeLabelCounts,
    List<string>? routeTrace,
    List<string>? objectiveTrace,
    bool satisfiedScenario,
    bool completedObjective,
    string? failureReason,
    bool timedOut)
{
    var routeSummary = string.Join(", ", routeLabelCounts
        .OrderByDescending(static pair => pair.Value)
        .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
        .Take(12)
        .Select(static pair => $"{pair.Key}x{pair.Value}"));
    var traceSummary = routeTrace is null ? string.Empty : string.Join(" | ", routeTrace);
    if (objectiveTrace is not null && objectiveTrace.Count > 0)
    {
        traceSummary = string.IsNullOrEmpty(traceSummary)
            ? $"events: {string.Join(" | ", objectiveTrace)}"
            : $"{traceSummary} || events: {string.Join(" | ", objectiveTrace)}";
    }
    return new BotScenarioResult(
        Passed: !timedOut && satisfiedScenario && string.IsNullOrEmpty(failureReason),
        TimedOut: timedOut,
        SatisfiedScenario: satisfiedScenario,
        CompletedObjective: completedObjective,
        StartObjectiveDistance: startObjectiveDistance,
        BestObjectiveDistance: bestObjectiveDistance,
        MaxDistanceFromSpawn: maxDistanceFromSpawn,
        MaxStuckTicks: maxStuckTicks,
        LongestNoProgressSeconds: longestNoProgressTicks / (float)world.Config.TicksPerSecond,
        WallMilliseconds: wallMilliseconds,
        RouteSummary: routeSummary,
        TraceSummary: traceSummary,
        FailureReason: failureReason ?? string.Empty);
}

static string DescribeTrackedControlPoint(SimulationWorld world, int controlPointIndex)
{
    if (controlPointIndex < 0)
    {
        return string.Empty;
    }

    var point = world.ControlPoints.FirstOrDefault(candidate => candidate.Index == controlPointIndex);
    if (point is null)
    {
        return string.Empty;
    }

    var team = point.Team?.ToString() ?? "Neutral";
    var capping = point.CappingTeam?.ToString() ?? "None";
    return $"{point.Index}:{team}:{capping}:{point.CappingTicks:0.0}/{point.CapTimeTicks:0.0}:{point.RedCappers}-{point.BlueCappers}";
}

static bool TryPrepareScenario(
    SimulationWorld world,
    PlayerEntity bot,
    BotScenarioCase scenario,
    out string? failureReason)
{
    failureReason = null;
    return scenario.Kind switch
    {
        BotScenarioKind.Score or BotScenarioKind.Route => true,
        BotScenarioKind.Arrival => TryPrepareArrivalScenario(world, bot, scenario.Team, out failureReason),
        BotScenarioKind.ReturnCap => TryPrepareReturnCapScenario(world, bot, scenario.Team, out failureReason),
        _ => false,
    };
}

static bool TryPrepareArrivalScenario(
    SimulationWorld world,
    PlayerEntity bot,
    PlayerTeam team,
    out string? failureReason)
{
    var objective = ResolveTeamObjective(world, bot, team);
    if (!TryLoadShippedAssetForScenario(world, out var asset, out failureReason))
    {
        return false;
    }

    var preferredRouteDistance = world.MatchRules.Mode == GameModeKind.CaptureTheFlag ? 2 : 3;
    var teamSpawns = team == PlayerTeam.Blue ? world.Level.BlueSpawns : world.Level.RedSpawns;
    var probeStarts = teamSpawns.Select(static spawn => (spawn.X, spawn.Y)).ToArray();
    if (!TryFindProbeStartNode(
        asset!,
        probeStarts,
        objective.X,
        objective.Y,
        minimumRouteDistance: 2,
        maximumRouteDistance: 8,
        preferredRouteDistance,
        candidate => IsScenarioProbeSpawnMobile(world, bot, team, candidate, objective.X),
        out var probeNode,
        out _))
    {
        failureReason = "arrival_probe_node_missing";
        return false;
    }

    if (!TrySpawnPlayerResolvedForScenario(world, bot, team, probeNode.X, probeNode.Y - bot.CollisionBottomOffset))
    {
        failureReason = "arrival_probe_spawn_failed";
        return false;
    }

    failureReason = null;
    return true;
}

static bool TryPrepareReturnCapScenario(
    SimulationWorld world,
    PlayerEntity bot,
    PlayerTeam team,
    out string? failureReason)
{
    if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag)
    {
        failureReason = "return_cap_requires_ctf";
        return false;
    }

    var ownBase = world.Level.GetIntelBase(team);
    if (!ownBase.HasValue)
    {
        failureReason = "return_cap_missing_own_base";
        return false;
    }

    if (!TryLoadShippedAssetForScenario(world, out var asset, out failureReason))
    {
        return false;
    }

    var enemyBase = world.Level.GetIntelBase(GetOpposingTeamForScenario(team));
    (float X, float Y)[] probeStarts = enemyBase.HasValue
        ? new[] { (enemyBase.Value.X, enemyBase.Value.Y) }
        : new[] { (bot.X, bot.Y) };
    if (!TryFindProbeStartNode(
        asset!,
        probeStarts,
        ownBase.Value.X,
        ownBase.Value.Y,
        minimumRouteDistance: 2,
        maximumRouteDistance: 8,
        preferredRouteDistance: 4,
        candidate => IsScenarioProbeSpawnMobile(world, bot, team, candidate, ownBase.Value.X),
        out var probeNode,
        out _))
    {
        failureReason = "return_cap_probe_node_missing";
        return false;
    }

    if (!TrySpawnPlayerResolvedForScenario(world, bot, team, probeNode.X, probeNode.Y - bot.CollisionBottomOffset))
    {
        failureReason = "return_cap_probe_spawn_failed";
        return false;
    }

    var enemyIntel = team == PlayerTeam.Blue ? world.RedIntel : world.BlueIntel;
    enemyIntel.PickUp();
    bot.PickUpIntel(0f);
    failureReason = null;
    return true;
}

static bool TryLoadShippedAssetForScenario(
    SimulationWorld world,
    out BotNavigationAsset? asset,
    out string? failureReason)
{
    if (!BotNavigationAssetStore.TryLoadModernShippedAsset(world.Level, out asset, out _, out var loadMessage, out _)
        || asset is null)
    {
        failureReason = $"scenario_nav_asset_missing_{loadMessage}";
        return false;
    }

    failureReason = null;
    return true;
}

static bool TrySpawnPlayerResolvedForScenario(
    SimulationWorld world,
    PlayerEntity player,
    PlayerTeam team,
    float x,
    float y)
{
    var method = typeof(SimulationWorld).GetMethod(
        "SpawnPlayerResolved",
        BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null,
        [typeof(PlayerEntity), typeof(PlayerTeam), typeof(float), typeof(float), typeof(bool)],
        modifiers: null);
    if (method is null)
    {
        return false;
    }

    method.Invoke(world, [player, team, x, y, true]);
    return true;
}

static bool IsScenarioProbeSpawnMobile(
    SimulationWorld world,
    PlayerEntity player,
    PlayerTeam team,
    BotNavigationNode candidate,
    float objectiveX)
{
    var canOccupy = typeof(PlayerEntity).GetMethod(
        "CanOccupy",
        BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null,
        [typeof(SimpleLevel), typeof(PlayerTeam), typeof(float), typeof(float)],
        modifiers: null);
    if (canOccupy is null)
    {
        return true;
    }

    var spawnY = candidate.Y - player.CollisionBottomOffset;
    if (canOccupy.Invoke(player, [world.Level, team, candidate.X, spawnY]) is not true)
    {
        return false;
    }

    var direction = MathF.Sign(objectiveX - candidate.X);
    if (direction == 0f)
    {
        return true;
    }

    return canOccupy.Invoke(player, [world.Level, team, candidate.X + direction, spawnY]) is true;
}

static bool TryFindProbeStartNode(
    BotNavigationAsset asset,
    IReadOnlyList<(float X, float Y)> probeStarts,
    float goalX,
    float goalY,
    int minimumRouteDistance,
    int maximumRouteDistance,
    int preferredRouteDistance,
    Func<BotNavigationNode, bool>? candidateFilter,
    out BotNavigationNode selectedNode,
    out int selectedRouteDistance)
{
    var goalNode = FindNearestNode(asset.Nodes, goalX, goalY, requireGroundSupport: true, maxDistance: 220f)
        ?? FindNearestNode(asset.Nodes, goalX, goalY, requireGroundSupport: false, maxDistance: 220f);
    if (goalNode is null)
    {
        selectedNode = default!;
        selectedRouteDistance = -1;
        return false;
    }

    _ = probeStarts;
    var sourceGoalWeights = BuildSourceGoalWeights(asset, goalNode.Id);
    if (sourceGoalWeights is null)
    {
        selectedNode = default!;
        selectedRouteDistance = -1;
        return false;
    }

    var fallbackScore = float.PositiveInfinity;
    BotNavigationNode? fallbackNode = null;
    var fallbackRouteDistance = -1;
    for (var index = 0; index < asset.Nodes.Count; index += 1)
    {
        var candidate = asset.Nodes[index];
        var sourceWeight = GetSourceGoalWeight(sourceGoalWeights, candidate.Id);
        var routeDistance = sourceWeight - 1;
        if (!candidate.RequiresGroundSupport
            || routeDistance <= 0
            || candidate.Id == goalNode.Id
            || candidateFilter?.Invoke(candidate) == false)
        {
            continue;
        }

        var physicalDistance = DistanceBetween(candidate.X, candidate.Y, goalX, goalY);
        if (physicalDistance < 48f)
        {
            continue;
        }

        var routeDistancePenalty = MathF.Abs(routeDistance - preferredRouteDistance) * 1000f;
        var physicalDistancePenalty = MathF.Abs(physicalDistance - 160f);
        var outOfBandPenalty = routeDistance < minimumRouteDistance || routeDistance > maximumRouteDistance
            ? 10_000f
            : 0f;
        var score = outOfBandPenalty + routeDistancePenalty + physicalDistancePenalty;
        if (score < fallbackScore)
        {
            fallbackScore = score;
            fallbackNode = candidate;
            fallbackRouteDistance = routeDistance;
        }
    }

    if (fallbackNode is null)
    {
        selectedNode = default!;
        selectedRouteDistance = -1;
        return false;
    }

    selectedNode = fallbackNode;
    selectedRouteDistance = fallbackRouteDistance;
    return true;
}

static int[]? BuildSourceGoalWeights(BotNavigationAsset asset, int goalNodeId)
{
    var maximumNodeId = asset.Nodes.Count == 0 ? -1 : asset.Nodes.Max(static node => node.Id);
    if (goalNodeId < 0 || goalNodeId > maximumNodeId)
    {
        return null;
    }

    var knownNodeIds = asset.Nodes.Select(static node => node.Id).ToHashSet();
    if (!knownNodeIds.Contains(goalNodeId))
    {
        return null;
    }

    var weights = new int[maximumNodeId + 1];
    var outgoing = BuildAdjacency(asset.Edges);
    var reverseBlockedPairs = new HashSet<long>();
    for (var nodeIndex = 0; nodeIndex < asset.Nodes.Count; nodeIndex += 1)
    {
        var node = asset.Nodes[nodeIndex];
        for (var blockedIndex = 0; blockedIndex < node.ReverseOnlyBlockedFromNodeIds.Count; blockedIndex += 1)
        {
            reverseBlockedPairs.Add(GetNodePairKey(node.Id, node.ReverseOnlyBlockedFromNodeIds[blockedIndex]));
        }
    }

    ProcessSourceGoalPoints([goalNodeId], weights, branchDepth: 1, sourceNodeId: -1, maximumDepth: 130, outgoing, reverseBlockedPairs);
    return weights;
}

static void ProcessSourceGoalPoints(
    IReadOnlyList<int> branches,
    int[] weights,
    int branchDepth,
    int sourceNodeId,
    int maximumDepth,
    IReadOnlyDictionary<int, int[]> outgoing,
    IReadOnlySet<long> reverseBlockedPairs)
{
    for (var branchIndex = 0; branchIndex < branches.Count; branchIndex += 1)
    {
        var branch = branches[branchIndex];
        if (branch < 0 || branch >= weights.Length)
        {
            continue;
        }

        if (weights[branch] != 0 && weights[branch] <= branchDepth)
        {
            continue;
        }

        if (sourceNodeId >= 0 && reverseBlockedPairs.Contains(GetNodePairKey(sourceNodeId, branch)))
        {
            continue;
        }

        weights[branch] = branchDepth;
        if (branchDepth > maximumDepth || !outgoing.TryGetValue(branch, out var nextBranches))
        {
            continue;
        }

        ProcessSourceGoalPoints(nextBranches, weights, branchDepth + 1, branch, maximumDepth, outgoing, reverseBlockedPairs);
    }
}

static int GetSourceGoalWeight(IReadOnlyList<int> weights, int nodeId)
{
    return nodeId >= 0 && nodeId < weights.Count ? weights[nodeId] : 0;
}

static long GetNodePairKey(int fromNodeId, int toNodeId)
{
    return ((long)fromNodeId << 32) | (uint)toNodeId;
}

static Dictionary<int, int[]> BuildAdjacency(IReadOnlyList<BotNavigationEdge> edges)
{
    return edges
        .GroupBy(static edge => edge.FromNodeId)
        .ToDictionary(static group => group.Key, static group => group.Select(static edge => edge.ToNodeId).Distinct().ToArray());
}

static List<int>? FindBestShortestPath(
    IReadOnlyDictionary<int, int[]> adjacency,
    IReadOnlyList<int> startNodeIds,
    int goalNodeId)
{
    List<int>? bestPath = null;
    for (var index = 0; index < startNodeIds.Count; index += 1)
    {
        var startNodeId = startNodeIds[index];
        var path = TryFindShortestPath(adjacency, startNodeId, goalNodeId);
        if (path is null)
        {
            continue;
        }

        if (bestPath is null || path.Count < bestPath.Count)
        {
            bestPath = path;
        }
    }

    return bestPath;
}

static List<int>? TryFindShortestPath(
    IReadOnlyDictionary<int, int[]> adjacency,
    int startNodeId,
    int goalNodeId)
{
    if (startNodeId == goalNodeId)
    {
        return [startNodeId];
    }

    var cameFrom = new Dictionary<int, int>();
    var visited = new HashSet<int> { startNodeId };
    var queue = new Queue<int>();
    queue.Enqueue(startNodeId);
    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (!adjacency.TryGetValue(current, out var neighbors))
        {
            continue;
        }

        for (var index = 0; index < neighbors.Length; index += 1)
        {
            var neighbor = neighbors[index];
            if (!visited.Add(neighbor))
            {
                continue;
            }

            cameFrom[neighbor] = current;
            if (neighbor == goalNodeId)
            {
                var path = new List<int> { goalNodeId };
                var walk = goalNodeId;
                while (cameFrom.TryGetValue(walk, out var previous))
                {
                    path.Add(previous);
                    if (previous == startNodeId)
                    {
                        break;
                    }

                    walk = previous;
                }

                path.Reverse();
                return path;
            }

            queue.Enqueue(neighbor);
        }
    }

    return null;
}

static Dictionary<int, int[]> BuildReverseAdjacency(IReadOnlyList<BotNavigationEdge> edges)
{
    return edges
        .GroupBy(static edge => edge.ToNodeId)
        .ToDictionary(static group => group.Key, static group => group.Select(static edge => edge.FromNodeId).Distinct().ToArray());
}

static Dictionary<int, int> BuildDistancesToGoal(IReadOnlyDictionary<int, int[]> reverseAdjacency, int goalNodeId)
{
    var distances = new Dictionary<int, int>
    {
        [goalNodeId] = 0,
    };
    var queue = new Queue<int>();
    queue.Enqueue(goalNodeId);
    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (!reverseAdjacency.TryGetValue(current, out var predecessors))
        {
            continue;
        }

        for (var index = 0; index < predecessors.Length; index += 1)
        {
            var predecessor = predecessors[index];
            if (distances.ContainsKey(predecessor))
            {
                continue;
            }

            distances[predecessor] = distances[current] + 1;
            queue.Enqueue(predecessor);
        }
    }

    return distances;
}

static bool HasSatisfiedScenario(
    BotScenarioCase scenario,
    SimulationWorld world,
    PlayerEntity bot,
    BotObjectiveProbe objective,
    bool completedObjective,
    bool botInCaptureZone,
    float startObjectiveDistance,
    float bestObjectiveDistance)
{
    return scenario.Kind switch
    {
        BotScenarioKind.Score => completedObjective,
        BotScenarioKind.Route => completedObjective
            || botInCaptureZone
            || bestObjectiveDistance <= GetArrivalDistance(world.MatchRules.Mode)
            || (startObjectiveDistance - bestObjectiveDistance) >= GetRequiredRouteProgress(world.MatchRules.Mode, scenario.ClassId, startObjectiveDistance),
        BotScenarioKind.Arrival => world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            ? bot.IsCarryingIntel || bestObjectiveDistance <= GetArrivalDistance(world.MatchRules.Mode)
            : completedObjective || botInCaptureZone || bestObjectiveDistance <= GetArrivalDistance(world.MatchRules.Mode),
        BotScenarioKind.ReturnCap => completedObjective,
        _ => false,
    };
}

static bool IsPlayerInCaptureZone(SimulationWorld world, PlayerEntity player)
{
    if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
    {
        return false;
    }

    var captureZones = world.Level.GetRoomObjects(RoomObjectType.CaptureZone);
    for (var index = 0; index < captureZones.Count; index += 1)
    {
        var zone = captureZones[index];
        if (player.IntersectsMarker(zone.CenterX, zone.CenterY, zone.Width, zone.Height))
        {
            return true;
        }
    }

    return false;
}

static float GetArrivalDistance(GameModeKind mode)
{
    return mode == GameModeKind.CaptureTheFlag ? 40f : 72f;
}

static float GetRequiredRouteProgress(GameModeKind mode, PlayerClass classId, float startObjectiveDistance)
{
    var baseline = mode == GameModeKind.CaptureTheFlag ? 192f : 128f;
    if (classId == PlayerClass.Heavy)
    {
        baseline -= 32f;
    }
    else if (classId == PlayerClass.Scout)
    {
        baseline += 32f;
    }

    return MathF.Min(baseline, MathF.Max(64f, startObjectiveDistance * 0.25f));
}

static PlayerTeam GetOpposingTeamForScenario(PlayerTeam team)
{
    return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
}

static IReadOnlyList<string> GetStockCtfAndKothMaps()
{
    return GetStockScoreMaps()
        .Where(static mapName => TryGetStockMapMode(mapName, out var mode, out _) && mode is GameModeKind.CaptureTheFlag or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
        .ToArray();
}

static bool TryGetStockMapMode(string mapName, out GameModeKind mode, out string? failureReason)
{
    var world = new SimulationWorld();
    if (!world.TryLoadLevel(mapName))
    {
        mode = default;
        failureReason = "failed_to_load_level";
        return false;
    }

    mode = world.MatchRules.Mode;
    failureReason = null;
    return true;
}

static bool IsCustomMapEntry(SimpleLevelFactory.LevelCatalogEntry entry)
{
    if (!entry.RoomSourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var parentDirectoryName = Path.GetFileName(Path.GetDirectoryName(entry.RoomSourcePath));
    return string.Equals(parentDirectoryName, "Maps", StringComparison.OrdinalIgnoreCase);
}

static void DeleteLegacyClassAssets(string outputDirectory, string levelName, int mapAreaIndex)
{
    foreach (var classId in BotNavigationClasses.All)
    {
        var legacyPath = Path.Combine(outputDirectory, BotNavigationAssetStore.GetAssetFileName(levelName, mapAreaIndex, classId));
        if (File.Exists(legacyPath))
        {
            File.Delete(legacyPath);
        }
    }
}

static void DeleteLegacyProfileAssets(string outputDirectory, string levelName, int mapAreaIndex)
{
    foreach (var profile in BotNavigationProfiles.All)
    {
        var legacyPath = Path.Combine(outputDirectory, BotNavigationAssetStore.GetLegacyAssetFileName(levelName, mapAreaIndex, profile));
        if (File.Exists(legacyPath))
        {
            File.Delete(legacyPath);
        }
    }
}

static void AuditCaptureRoutes(SimpleLevel level, BotNavigationAsset asset)
{
    var captureZones = level.GetRoomObjects(RoomObjectType.CaptureZone).ToArray();
    var controlPoints = level.GetRoomObjects(RoomObjectType.ControlPoint).ToArray();
    if (captureZones.Length == 0 && controlPoints.Length == 0)
    {
        return;
    }

    var outgoing = asset.Edges
        .GroupBy(static edge => edge.FromNodeId)
        .ToDictionary(static group => group.Key, static group => group.Select(static edge => edge.ToNodeId).Distinct().ToArray());
    Console.WriteLine($"  capture markers cp={controlPoints.Length} zones={captureZones.Length}");
    for (var pointIndex = 0; pointIndex < controlPoints.Length; pointIndex += 1)
    {
        var point = controlPoints[pointIndex];
        Console.WriteLine($"  cp[{pointIndex}] {point.SourceName} center=({point.CenterX:F0},{point.CenterY:F0}) bounds=({point.Left:F0},{point.Top:F0},{point.Right:F0},{point.Bottom:F0})");
    }

    for (var zoneIndex = 0; zoneIndex < captureZones.Length; zoneIndex += 1)
    {
        var zone = captureZones[zoneIndex];
        var nearest = FindNearestNode(asset.Nodes, zone.CenterX, zone.CenterY, requireGroundSupport: false, maxDistance: 0f);
        var nearestGround = FindNearestNode(asset.Nodes, zone.CenterX, zone.CenterY, requireGroundSupport: true, maxDistance: 220f);
        var redRoute = FindShortestRouteLength(asset.Nodes, outgoing, level.RedSpawns, nearest?.Id);
        var blueRoute = FindShortestRouteLength(asset.Nodes, outgoing, level.BlueSpawns, nearest?.Id);
        Console.WriteLine(
            $"  zone[{zoneIndex}] center=({zone.CenterX:F0},{zone.CenterY:F0}) node={FormatNode(nearest)} ground={FormatNode(nearestGround)} redRoute={FormatRoute(redRoute)} blueRoute={FormatRoute(blueRoute)}");
    }
}

static BotNavigationNode? FindNearestNode(
    IReadOnlyList<BotNavigationNode> nodes,
    float x,
    float y,
    bool requireGroundSupport,
    float maxDistance)
{
    var bestDistanceSquared = maxDistance <= 0f ? float.PositiveInfinity : maxDistance * maxDistance;
    BotNavigationNode? best = null;
    for (var index = 0; index < nodes.Count; index += 1)
    {
        var node = nodes[index];
        if (requireGroundSupport && !node.RequiresGroundSupport)
        {
            continue;
        }

        var dx = node.X - x;
        var dy = node.Y - y;
        var distanceSquared = (dx * dx) + (dy * dy);
        if (distanceSquared > bestDistanceSquared)
        {
            continue;
        }

        bestDistanceSquared = distanceSquared;
        best = node;
    }

    return best;
}

static int? FindShortestRouteLength(
    IReadOnlyList<BotNavigationNode> nodes,
    IReadOnlyDictionary<int, int[]> outgoing,
    IReadOnlyList<SpawnPoint> spawns,
    int? goalNodeId)
{
    if (!goalNodeId.HasValue)
    {
        return null;
    }

    var best = int.MaxValue;
    for (var spawnIndex = 0; spawnIndex < spawns.Count; spawnIndex += 1)
    {
        var spawn = spawns[spawnIndex];
        var start = FindNearestNode(nodes, spawn.X, spawn.Y, requireGroundSupport: true, maxDistance: 220f);
        if (start is null)
        {
            continue;
        }

        var routeLength = FindRouteLength(outgoing, start.Id, goalNodeId.Value);
        if (routeLength.HasValue)
        {
            best = Math.Min(best, routeLength.Value);
        }
    }

    return best == int.MaxValue ? null : best;
}

static int? FindRouteLength(IReadOnlyDictionary<int, int[]> outgoing, int startNodeId, int goalNodeId)
{
    if (startNodeId == goalNodeId)
    {
        return 0;
    }

    var visited = new HashSet<int> { startNodeId };
    var queue = new Queue<(int NodeId, int Distance)>();
    queue.Enqueue((startNodeId, 0));
    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (!outgoing.TryGetValue(current.NodeId, out var neighbors))
        {
            continue;
        }

        for (var index = 0; index < neighbors.Length; index += 1)
        {
            var neighbor = neighbors[index];
            if (!visited.Add(neighbor))
            {
                continue;
            }

            var distance = current.Distance + 1;
            if (neighbor == goalNodeId)
            {
                return distance;
            }

            queue.Enqueue((neighbor, distance));
        }
    }

    return null;
}

static string FormatNode(BotNavigationNode? node)
{
    return node is not null
        ? $"{node.Id}@({node.X:F0},{node.Y:F0})"
        : "miss";
}

static string FormatRoute(int? routeLength)
{
    return routeLength.HasValue ? routeLength.Value.ToString(CultureInfo.InvariantCulture) : "miss";
}

static BotObjectiveProbe ResolveTeamObjective(SimulationWorld world, PlayerEntity bot, PlayerTeam team)
{
    if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
    {
        if (bot.IsCarryingIntel)
        {
            var ownBase = world.Level.GetIntelBase(team);
            if (ownBase.HasValue)
            {
                return new BotObjectiveProbe(world.MatchRules.Mode, ownBase.Value.X, ownBase.Value.Y, ControlPointIndex: -1);
            }
        }

        var enemyIntel = team == PlayerTeam.Blue ? world.RedIntel : world.BlueIntel;
        return new BotObjectiveProbe(world.MatchRules.Mode, enemyIntel.X, enemyIntel.Y, ControlPointIndex: -1);
    }

    var point = world.ControlPoints
        .OrderBy(point => DistanceBetween(bot.X, bot.Y, point.Marker.CenterX, point.Marker.CenterY))
        .FirstOrDefault();
    if (point is not null)
    {
        if (TryResolveCaptureObjectiveForTool(world, bot, point.Marker, out var targetPoint))
        {
            return new BotObjectiveProbe(world.MatchRules.Mode, targetPoint.X, targetPoint.Y, point.Index);
        }

        return new BotObjectiveProbe(world.MatchRules.Mode, point.Marker.CenterX, point.Marker.CenterY, point.Index);
    }

    return new BotObjectiveProbe(world.MatchRules.Mode, world.Level.Bounds.Width * 0.5f, world.Level.Bounds.Height * 0.5f, ControlPointIndex: -1);
}

static bool HasCompletedTeamObjective(
    SimulationWorld world,
    BotObjectiveProbe objective,
    PlayerTeam team,
    int initialRedCaps,
    int initialBlueCaps,
    int initialRedKothTicks,
    int initialBlueKothTicks)
{
    if (objective.Mode == GameModeKind.CaptureTheFlag)
    {
        return team == PlayerTeam.Blue
            ? world.BlueCaps > initialBlueCaps
            : world.RedCaps > initialRedCaps;
    }

    if (objective.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
    {
        return team == PlayerTeam.Blue
            ? world.KothBlueTimerTicksRemaining < initialBlueKothTicks
            : world.KothRedTimerTicksRemaining < initialRedKothTicks;
    }

    var point = world.ControlPoints.FirstOrDefault(point => point.Index == objective.ControlPointIndex);
    return point is not null && point.Team == team;
}

static float DistanceBetween(float x1, float y1, float x2, float y2)
{
    var dx = x2 - x1;
    var dy = y2 - y1;
    return MathF.Sqrt((dx * dx) + (dy * dy));
}

static bool TryResolveCaptureObjectiveForTool(
    SimulationWorld world,
    PlayerEntity player,
    RoomObjectMarker marker,
    out (float X, float Y) targetPoint)
{
    targetPoint = default;
    var method = typeof(ModernPracticeBotController).GetMethod(
        "TryResolveModernCaptureObjective",
        BindingFlags.Static | BindingFlags.NonPublic,
        binder: null,
        [typeof(SimulationWorld), typeof(PlayerEntity), typeof(RoomObjectMarker), typeof(object).MakeByRefType()],
        modifiers: null);
    if (method is null)
    {
        return false;
    }

    var parameters = new object?[] { world, player, marker, null };
    var resolved = method.Invoke(null, parameters);
    if (resolved is not bool success || !success || parameters[3] is null)
    {
        return false;
    }

    var captureObjective = parameters[3]!;
    var targetPointProperty = captureObjective.GetType().GetProperty("TargetPoint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    var pointValue = targetPointProperty?.GetValue(captureObjective);
    if (pointValue is null)
    {
        return false;
    }

    var pointType = pointValue.GetType();
    var xField = pointType.GetField("Item1") ?? pointType.GetField("X");
    var yField = pointType.GetField("Item2") ?? pointType.GetField("Y");
    if (xField?.GetValue(pointValue) is float x && yField?.GetValue(pointValue) is float y)
    {
        targetPoint = (x, y);
        return true;
    }

    return false;
}

static IReadOnlyList<string> GetStockScoreMaps()
{
    return
    [
        "Truefort",
        "TwodFortTwo",
        "Conflict",
        "ClassicWell",
        "Waterway",
        "Orange",
        "Avanti",
        "Eiger",
        "Valley",
        "Corinth",
        "Harvest",
        "Atalia",
        "Sixties",
        "Gallery",
    ];
}

internal sealed class NavBuildOptions
{
    public const string Usage =
        "usage: dotnet run --project BotAI.Tools -- [--map MapName] [--output Path] [--include-custom] [--audit-reachability] [--audit-shipped] [--repair-shipped] [--audit-capture-routes] [--run-bot-scenario] [--stock-score-matrix] [--stock-ctf-koth-matrix] [--scenario-kind Score|Route|Arrival|ReturnCap] [--team Red|Blue] [--class Scout|Engineer|Pyro|Soldier|Demoman|Heavy|Sniper|Medic|Spy|Quote]... [--path-mode ModernHybrid|ClientBot2020Compat] [--seconds N] [--wall-timeout-ms N] [--fail-fast] [--no-bot-diagnostics] [--probe-orange-lip] [--probe-orange-branch]";

    public HashSet<string> MapNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? OutputDirectory { get; private set; }

    public bool IncludeCustomMaps { get; private set; }

    public bool AuditReachability { get; private set; }

    public bool AuditShipped { get; private set; }

    public bool RepairShipped { get; private set; }

    public bool AuditCaptureRoutes { get; private set; }

    public bool RunBotScenarioHarness { get; private set; }

    public bool ProbeOrangeLip { get; private set; }

    public bool ProbeOrangeBranch { get; private set; }

    public bool RunStockScoreMatrix { get; private set; }

    public bool RunStockCtfKothMatrix { get; private set; }

    public PlayerTeam ScenarioTeam { get; private set; } = PlayerTeam.Blue;

    public IReadOnlyList<PlayerClass> ScenarioClasses => _scenarioClasses;

    public BotPathMode ScenarioPathMode { get; private set; } = BotPathMode.ClientBot2020Compat;

    public CompatNavPointSource CompatNavPointSource { get; private set; } = CompatNavPointSource.Auto;

    public int ScenarioSeconds { get; private set; } = 120;

    public int WallTimeoutMilliseconds { get; private set; } = 45000;

    public BotScenarioKind ScenarioKind { get; private set; } = BotScenarioKind.Score;

    public bool CollectBotDiagnostics { get; private set; } = true;

    public bool FailFast { get; private set; }

    private readonly List<PlayerClass> _scenarioClasses = [PlayerClass.Scout];

    private bool _hasExplicitScenarioClasses;

    private bool _hasExplicitScenarioSeconds;

    private bool _hasExplicitWallTimeoutMilliseconds;

    public static NavBuildOptions Parse(IReadOnlyList<string> args)
    {
        var options = new NavBuildOptions();
        for (var index = 0; index < args.Count; index += 1)
        {
            var arg = args[index];
            if (arg.Equals("--map", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.MapNames.Add(args[++index].Trim());
                continue;
            }

            if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.OutputDirectory = args[++index].Trim();
                continue;
            }

            if (arg.Equals("--include-custom", StringComparison.OrdinalIgnoreCase))
            {
                options.IncludeCustomMaps = true;
                continue;
            }

            if (arg.Equals("--audit-reachability", StringComparison.OrdinalIgnoreCase))
            {
                options.AuditReachability = true;
                continue;
            }

            if (arg.Equals("--audit-shipped", StringComparison.OrdinalIgnoreCase))
            {
                options.AuditShipped = true;
                continue;
            }

            if (arg.Equals("--repair-shipped", StringComparison.OrdinalIgnoreCase))
            {
                options.AuditShipped = true;
                options.RepairShipped = true;
                continue;
            }

            if (arg.Equals("--audit-capture-routes", StringComparison.OrdinalIgnoreCase))
            {
                options.AuditCaptureRoutes = true;
                continue;
            }

            if (arg.Equals("--run-bot-scenario", StringComparison.OrdinalIgnoreCase))
            {
                options.RunBotScenarioHarness = true;
                continue;
            }

            if (arg.Equals("--probe-orange-lip", StringComparison.OrdinalIgnoreCase))
            {
                options.ProbeOrangeLip = true;
                continue;
            }

            if (arg.Equals("--probe-orange-branch", StringComparison.OrdinalIgnoreCase))
            {
                options.ProbeOrangeBranch = true;
                continue;
            }

            if (arg.Equals("--no-bot-diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                options.CollectBotDiagnostics = false;
                continue;
            }

            if (arg.Equals("--stock-score-matrix", StringComparison.OrdinalIgnoreCase))
            {
                options.RunBotScenarioHarness = true;
                options.RunStockScoreMatrix = true;
                options.ScenarioKind = BotScenarioKind.Score;
                continue;
            }

            if (arg.Equals("--stock-ctf-koth-matrix", StringComparison.OrdinalIgnoreCase))
            {
                options.RunBotScenarioHarness = true;
                options.RunStockCtfKothMatrix = true;
                options.ScenarioKind = BotScenarioKind.Route;
                continue;
            }

            if (arg.Equals("--team", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!Enum.TryParse<PlayerTeam>(args[++index].Trim(), ignoreCase: true, out var team))
                {
                    throw new ArgumentException($"Unknown team '{args[index]}'.");
                }

                options.ScenarioTeam = team;
                continue;
            }

            if (arg.Equals("--class", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!Enum.TryParse<PlayerClass>(args[++index].Trim(), ignoreCase: true, out var playerClass))
                {
                    throw new ArgumentException($"Unknown class '{args[index]}'.");
                }

                if (!options._hasExplicitScenarioClasses)
                {
                    options._scenarioClasses.Clear();
                    options._hasExplicitScenarioClasses = true;
                }

                if (!options._scenarioClasses.Contains(playerClass))
                {
                    options._scenarioClasses.Add(playerClass);
                }

                continue;
            }

            if (arg.Equals("--path-mode", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!Enum.TryParse<BotPathMode>(args[++index].Trim(), ignoreCase: true, out var pathMode))
                {
                    throw new ArgumentException($"Unknown path mode '{args[index]}'.");
                }

                options.ScenarioPathMode = pathMode;
                continue;
            }

            if (arg.Equals("--compat-nav-source", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!Enum.TryParse<CompatNavPointSource>(args[++index].Trim(), ignoreCase: true, out var compatNavPointSource))
                {
                    throw new ArgumentException($"Unknown compat nav source '{args[index]}'.");
                }

                options.CompatNavPointSource = compatNavPointSource;
                continue;
            }

            if (arg.Equals("--seconds", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!int.TryParse(args[++index].Trim(), out var seconds))
                {
                    throw new ArgumentException($"Invalid seconds value '{args[index]}'.");
                }

                options.ScenarioSeconds = seconds;
                options._hasExplicitScenarioSeconds = true;
                continue;
            }

            if (arg.Equals("--wall-timeout-ms", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!int.TryParse(args[++index].Trim(), out var timeoutMs))
                {
                    throw new ArgumentException($"Invalid wall-timeout-ms value '{args[index]}'.");
                }

                options.WallTimeoutMilliseconds = timeoutMs;
                options._hasExplicitWallTimeoutMilliseconds = true;
                continue;
            }

            if (arg.Equals("--scenario-kind", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!Enum.TryParse<BotScenarioKind>(args[++index].Trim(), ignoreCase: true, out var kind))
                {
                    throw new ArgumentException($"Unknown scenario kind '{args[index]}'.");
                }

                options.ScenarioKind = kind;
                continue;
            }

            if (arg.Equals("--fail-fast", StringComparison.OrdinalIgnoreCase))
            {
                options.FailFast = true;
                continue;
            }
        }

        return options;
    }

    public bool IsValid(out string message)
    {
        message = string.Empty;
        if (RunBotScenarioHarness)
        {
            if (!RunStockScoreMatrix && !RunStockCtfKothMatrix && MapNames.Count == 0)
            {
                message = "Bot scenario mode requires at least one --map, or use --stock-score-matrix/--stock-ctf-koth-matrix.";
                return false;
            }

            if (GetEffectiveScenarioSeconds() <= 0)
            {
                message = "--seconds must be greater than zero.";
                return false;
            }

            if (GetEffectiveWallTimeoutMilliseconds() <= 0)
            {
                message = "--wall-timeout-ms must be greater than zero.";
                return false;
            }
        }

        return true;
    }

    public IReadOnlyList<PlayerClass> GetEffectiveScenarioClasses()
    {
        if (RunStockCtfKothMatrix && !_hasExplicitScenarioClasses)
        {
            return
            [
                PlayerClass.Scout,
                PlayerClass.Heavy,
                PlayerClass.Pyro,
            ];
        }

        return _scenarioClasses;
    }

    public int GetEffectiveScenarioSeconds()
    {
        if (_hasExplicitScenarioSeconds)
        {
            return ScenarioSeconds;
        }

        return ScenarioKind switch
        {
            BotScenarioKind.Score => 120,
            BotScenarioKind.Route => 12,
            BotScenarioKind.Arrival => 8,
            BotScenarioKind.ReturnCap => 10,
            _ => ScenarioSeconds,
        };
    }

    public int GetEffectiveWallTimeoutMilliseconds()
    {
        if (_hasExplicitWallTimeoutMilliseconds)
        {
            return WallTimeoutMilliseconds;
        }

        return ScenarioKind switch
        {
            BotScenarioKind.Score => 45_000,
            BotScenarioKind.Route => 30_000,
            BotScenarioKind.Arrival => 30_000,
            BotScenarioKind.ReturnCap => 30_000,
            _ => WallTimeoutMilliseconds,
        };
    }
}

internal enum BotScenarioKind
{
    Score = 0,
    Route = 1,
    Arrival = 2,
    ReturnCap = 3,
}

internal readonly record struct BotScenarioCase(
    string LevelName,
    PlayerTeam Team,
    PlayerClass ClassId,
    int SimulationSeconds,
    BotPathMode PathMode,
    BotScenarioKind Kind);

internal readonly record struct BotObjectiveProbe(GameModeKind Mode, float X, float Y, int ControlPointIndex);

internal readonly record struct BotScenarioResult(
    bool Passed,
    bool TimedOut,
    bool SatisfiedScenario,
    bool CompletedObjective,
    float StartObjectiveDistance,
    float BestObjectiveDistance,
    float MaxDistanceFromSpawn,
    int MaxStuckTicks,
    float LongestNoProgressSeconds,
    long WallMilliseconds,
    string RouteSummary,
    string TraceSummary,
    string FailureReason);
