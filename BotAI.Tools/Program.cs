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

if (options.ProbeModernEdge)
{
    return RunModernEdgeProbe(options);
}

if (options.ProbeModernChain)
{
    return RunModernChainProbe(options);
}

if (options.ProbeCollisionWindow)
{
    return RunCollisionWindowProbe(options);
}

if (options.ProbeGmlLink)
{
    return RunGmlLinkProbe(options);
}

if (options.BuildScoreRoutes)
{
    return BuildScoreRoutes(options);
}

if (options.RunBotPerfSmoke)
{
    return RunBotPerfSmoke(options);
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

        var asset = BotNavigationModernPointGraphBuilder.Build(level, fingerprint, validateTraversals: options.ValidateModernEdges);
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
            $"built map={entry.Name} area={areaIndex} mesh=modern classes={classTokens} strategy={asset.BuildStrategy} edgeValidation={(options.ValidateModernEdges ? "movement" : "geometry")} nodes={asset.Nodes.Count} edges={asset.Edges.Count} ms={asset.Stats.BuildMilliseconds:F2} phases=sample:{asset.Stats.SurfaceSamplingMilliseconds:F1},anchors:{asset.Stats.AutoAnchorMilliseconds:F1},hints:{asset.Stats.HintNodeMilliseconds:F1},auto-edges:{asset.Stats.AutomaticEdgeMilliseconds:F1},hint-edges:{asset.Stats.HintEdgeMilliseconds:F1},drops:{asset.Stats.DropEdgeMilliseconds:F1} nav={(validation.IsStructurallyValid ? "ok" : $"invalid:{validation.Issues.Count}")} reachability={(generatedReachabilityAudit.IsStructurallyValid ? "ok" : $"issues:{generatedReachabilityAudit.Issues.Count}")}");
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
        var startOverride = scenario.StartFeetX.HasValue && scenario.StartFeetY.HasValue
            ? $" startFeet=({scenario.StartFeetX.Value:0.0},{scenario.StartFeetY.Value:0.0})"
            : string.Empty;
        var controllerMode = options.BotControllerMode.ToString();
        Console.WriteLine(
            $"run case={index + 1}/{cases.Count} map={scenario.LevelName} team={scenario.Team} class={scenario.ClassId} mode={controllerMode} seconds={scenario.SimulationSeconds} kind={scenario.Kind}{startOverride}");
        var result = RunBotScenarioCase(scenario, wallTimeoutMilliseconds, options.CollectBotDiagnostics, options.BotControllerMode);
        var progress = result.StartObjectiveDistance - result.BestObjectiveDistance;
        Console.WriteLine(
            $"result status={(result.Passed ? "pass" : result.TimedOut ? "timeout" : "fail")} map={scenario.LevelName} team={scenario.Team} class={scenario.ClassId} mode={controllerMode} elapsedMs={result.WallMilliseconds} start={result.StartObjectiveDistance:0.0} best={result.BestObjectiveDistance:0.0} progress={progress:0.0} maxSpawn={result.MaxDistanceFromSpawn:0.0} longestNoProgress={result.LongestNoProgressSeconds:0.0}s maxStuck={result.MaxStuckTicks} satisfied={result.SatisfiedScenario} completed={result.CompletedObjective} reason={result.FailureReason} objective={CompactScoreRouteLogValue(result.ObjectiveSummary, 320)}");
        var failureBucket = ClassifyBotScenarioFailure(result);
        if (!string.IsNullOrWhiteSpace(failureBucket))
        {
            Console.WriteLine($"  failure-bucket: {failureBucket}");
        }

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

static int RunBotPerfSmoke(NavBuildOptions options)
{
    var mapName = options.GetSingleMapNameOrDefault("TwodFortTwo");
    var seconds = options.GetEffectiveBotPerfSmokeSeconds();
    var botCount = options.GetEffectiveBotPerfSmokeBotCount();
    var minFps = options.GetEffectiveBotPerfSmokeMinFps();
    var world = new SimulationWorld(new SimulationConfig
    {
        EnableLocalDummies = false,
        EnableEnemyTrainingDummy = false,
        EnableFriendlySupportDummy = false,
    });
    world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
        EnablePracticeBotsPrioritizeKills: true));
    if (!world.TryLoadLevel(mapName, mapAreaIndex: 1, preservePlayerStats: false))
    {
        Console.Error.WriteLine($"bot-perf-smoke failed_to_load_level map={mapName}");
        return 2;
    }

    world.PrepareLocalPlayerJoin();
    var classes = options.GetEffectiveScenarioClasses();
    var controlledSlots = new Dictionary<byte, ControlledBotSlot>(botCount);
    for (var index = 0; index < botCount; index += 1)
    {
        var slot = (byte)(2 + index);
        var team = index % 2 == 0 ? PlayerTeam.Red : PlayerTeam.Blue;
        var classId = classes[index % classes.Count];
        if (!world.TryPrepareNetworkPlayerJoin(slot)
            || !world.TrySetNetworkPlayerTeam(slot, team)
            || !world.TryApplyNetworkPlayerClassSelection(slot, classId)
            || !world.TryGetNetworkPlayer(slot, out _))
        {
            Console.Error.WriteLine($"bot-perf-smoke failed_to_spawn_bot slot={slot} team={team} class={classId}");
            return 2;
        }

        controlledSlots[slot] = new ControlledBotSlot(slot, team, classId);
    }

    var controller = new MotionProofPracticeBotController
    {
        CollectDiagnostics = false,
    };
    var totalTicks = seconds * world.Config.TicksPerSecond;
    var totalStopwatch = Stopwatch.StartNew();
    var buildTicksTotal = 0L;
    var applyTicksTotal = 0L;
    var advanceTicksTotal = 0L;
    var maxBuildTicks = 0L;
    var maxApplyTicks = 0L;
    var maxAdvanceTicks = 0L;
    var maxTickTicks = 0L;
    var maxTickIndex = 0;
    var ticksOver16Ms = 0;
    var ticksOver33Ms = 0;
    var ticksOver100Ms = 0;
    var ticksOver500Ms = 0;
    for (var tick = 0; tick < totalTicks; tick += 1)
    {
        var tickStart = Stopwatch.GetTimestamp();
        var buildStart = Stopwatch.GetTimestamp();
        var inputs = controller.BuildInputs(world, controlledSlots);
        var buildTicks = Stopwatch.GetTimestamp() - buildStart;
        buildTicksTotal += buildTicks;
        if (buildTicks > maxBuildTicks)
        {
            maxBuildTicks = buildTicks;
        }

        var applyStart = Stopwatch.GetTimestamp();
        foreach (var entry in controlledSlots)
        {
            if (!inputs.TryGetValue(entry.Key, out var input))
            {
                input = default;
            }

            if (!world.TrySetNetworkPlayerInput(entry.Key, input))
            {
                Console.Error.WriteLine($"bot-perf-smoke failed_to_apply_input slot={entry.Key} tick={tick}");
                return 2;
            }
        }

        var applyTicks = Stopwatch.GetTimestamp() - applyStart;
        applyTicksTotal += applyTicks;
        if (applyTicks > maxApplyTicks)
        {
            maxApplyTicks = applyTicks;
        }

        var advanceStart = Stopwatch.GetTimestamp();
        world.AdvanceOneTick();
        var advanceTicks = Stopwatch.GetTimestamp() - advanceStart;
        advanceTicksTotal += advanceTicks;
        if (advanceTicks > maxAdvanceTicks)
        {
            maxAdvanceTicks = advanceTicks;
        }

        var tickTicks = Stopwatch.GetTimestamp() - tickStart;
        var tickMs = TicksToMilliseconds(tickTicks);
        if (tickMs > 16.667d)
        {
            ticksOver16Ms += 1;
        }

        if (tickMs > 33.334d)
        {
            ticksOver33Ms += 1;
        }

        if (tickMs > 100d)
        {
            ticksOver100Ms += 1;
        }

        if (tickMs > 500d)
        {
            ticksOver500Ms += 1;
        }

        if (tickTicks > maxTickTicks)
        {
            maxTickTicks = tickTicks;
            maxTickIndex = tick;
        }
    }

    totalStopwatch.Stop();
    var elapsedSeconds = Math.Max(0.0001d, totalStopwatch.Elapsed.TotalSeconds);
    var simFps = totalTicks / elapsedSeconds;
    var averageBuildMs = TicksToMilliseconds(buildTicksTotal) / Math.Max(1, totalTicks);
    var averageApplyMs = TicksToMilliseconds(applyTicksTotal) / Math.Max(1, totalTicks);
    var averageAdvanceMs = TicksToMilliseconds(advanceTicksTotal) / Math.Max(1, totalTicks);
    var maxBuildMs = TicksToMilliseconds(maxBuildTicks);
    var maxApplyMs = TicksToMilliseconds(maxApplyTicks);
    var maxAdvanceMs = TicksToMilliseconds(maxAdvanceTicks);
    var maxTickMs = TicksToMilliseconds(maxTickTicks);
    var kills = 0;
    var deaths = 0;
    foreach (var entry in controlledSlots)
    {
        if (world.TryGetNetworkPlayer(entry.Key, out var player))
        {
            kills += player.Kills;
            deaths += player.Deaths;
        }
    }

    var passed = simFps >= minFps;
    Console.WriteLine(
        $"bot-perf-smoke result status={(passed ? "pass" : "fail")} map={mapName} bots={botCount} seconds={seconds} ticks={totalTicks} simFps={simFps:0.0} minFps={minFps:0.0} elapsedMs={totalStopwatch.ElapsedMilliseconds} avgMs=build:{averageBuildMs:0.000}/apply:{averageApplyMs:0.000}/advance:{averageAdvanceMs:0.000} maxMs=build:{maxBuildMs:0.000}/apply:{maxApplyMs:0.000}/advance:{maxAdvanceMs:0.000}/tick:{maxTickMs:0.000}@{maxTickIndex} hitchTicks=>16:{ticksOver16Ms}/>33:{ticksOver33Ms}/>100:{ticksOver100Ms}/>500:{ticksOver500Ms} kills={kills} deaths={deaths}");
    return passed ? 0 : 3;
}

static double TicksToMilliseconds(long ticks)
{
    return ticks * 1000d / Stopwatch.Frequency;
}

static int BuildScoreRoutes(NavBuildOptions options)
{
    var mapNames = GetScoreRouteBuildMapNames(options);
    if (mapNames.Count == 0)
    {
        Console.Error.WriteLine("No maps matched the requested filters.");
        return 2;
    }

    var totalAreas = 0;
    var validatedContexts = 0;
    var failedContexts = 0;
    foreach (var mapName in mapNames)
    {
        var mapStopwatch = Stopwatch.StartNew();
        var baseLevel = SimpleLevelFactory.CreateImportedLevel(mapName);
        if (baseLevel is null)
        {
            Console.WriteLine($"score-routes map={mapName} status=load_fail");
            continue;
        }

        for (var areaIndex = 1; areaIndex <= baseLevel.MapAreaCount; areaIndex += 1)
        {
            var level = areaIndex == 1 ? baseLevel : SimpleLevelFactory.CreateImportedLevel(mapName, areaIndex);
            if (level is null)
            {
                Console.WriteLine($"score-routes map={mapName} area={areaIndex} status=load_fail");
                continue;
            }

            totalAreas += 1;
            var fingerprint = BotNavigationLevelFingerprint.Compute(level);
            // Prove score-routes on top of the same broad graph runtime uses.
            // Edge-level traversal validation is still useful, but not as the
            // primary graph source for scoring.
            var graphAsset = BotNavigationModernPointGraphBuilder.Build(level, fingerprint, validateTraversals: false);
            var navigationGraph = new BotNavigationRuntimeGraph(graphAsset);
            var savedRoutes = new List<BotNavigationScoreRouteEntry>();
            Console.WriteLine(
                $"score-routes map={level.Name} area={level.MapAreaIndex} mode={level.Mode} graphNodes={graphAsset.Nodes.Count} graphEdges={graphAsset.Edges.Count} buildMs={graphAsset.Stats.BuildMilliseconds:F1}");

            foreach (var team in new[] { PlayerTeam.Blue, PlayerTeam.Red })
            {
                foreach (var profilePlan in GetScoreRouteProfilePlans())
                {
                    if (TryGetScoreRouteBudgetStopReason(options.GetEffectiveScoreRouteMapBudgetMilliseconds(), mapStopwatch, out var mapBudgetStopReason))
                    {
                        failedContexts += 1;
                        Console.WriteLine(
                            $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} status=skipped reason={mapBudgetStopReason}");
                        continue;
                    }

                    if (TryBuildValidatedScoreRoutesForProfile(
                            level,
                            graphAsset,
                            navigationGraph,
                            team,
                            profilePlan,
                            options,
                            out var validatedRoutes,
                            out var summary))
                    {
                        savedRoutes.AddRange(validatedRoutes);
                        validatedContexts += 1;
                    }
                    else
                    {
                        failedContexts += 1;
                    }

                    Console.WriteLine(summary);
                }
            }

            var scoreRouteAsset = new BotNavigationScoreRouteAsset
            {
                FormatVersion = BotNavigationScoreRouteStore.CurrentFormatVersion,
                LevelName = level.Name,
                MapAreaIndex = level.MapAreaIndex,
                LevelFingerprint = fingerprint,
                Routes = savedRoutes
                    .OrderBy(route => route.Team)
                    .ThenBy(route => route.Profile)
                    .ThenBy(route => route.Phase)
                    .ThenBy(route => route.Key, StringComparer.Ordinal)
                    .ToArray(),
            };
            var scoreRoutePath = BotNavigationScoreRouteStore.ResolveWritablePath(level.Name, level.MapAreaIndex);
            if (options.ScoreRouteDiagnostics)
            {
                Console.WriteLine(
                    $"score-routes diagnostics map={level.Name} area={level.MapAreaIndex} validatedRoutes={scoreRouteAsset.Routes.Count} skippedSave=True path={scoreRoutePath}");
            }
            else if (scoreRouteAsset.Routes.Count > 0)
            {
                BotNavigationScoreRouteStore.Save(scoreRouteAsset);
                Console.WriteLine(
                    $"score-routes saved map={level.Name} area={level.MapAreaIndex} routes={scoreRouteAsset.Routes.Count} path={scoreRoutePath}");
            }
            else
            {
                if (File.Exists(scoreRoutePath))
                {
                    File.Delete(scoreRoutePath);
                }

                Console.WriteLine(
                    $"score-routes skipped map={level.Name} area={level.MapAreaIndex} routes=0 path={scoreRoutePath}");
            }
        }
    }

    Console.WriteLine($"score-routes summary areas={totalAreas} validatedContexts={validatedContexts} failedContexts={failedContexts}");
    return failedContexts == 0 ? 0 : 3;
}

static IReadOnlyList<string> GetScoreRouteBuildMapNames(NavBuildOptions options)
{
    if (options.MapNames.Count > 0)
    {
        return options.MapNames
            .OrderBy(static mapName => mapName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    if (options.IncludeCustomMaps)
    {
        return SimpleLevelFactory.GetAvailableSourceLevels()
            .Select(static entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static mapName => mapName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    return GetStockScoreMaps();
}

static bool TryBuildValidatedScoreRoutesForProfile(
    SimpleLevel level,
    BotNavigationAsset graphAsset,
    BotNavigationRuntimeGraph navigationGraph,
    PlayerTeam team,
    ScoreRouteProfilePlan profilePlan,
    NavBuildOptions options,
    out IReadOnlyList<BotNavigationScoreRouteEntry> validatedRoutes,
    out string summary)
{
    validatedRoutes = Array.Empty<BotNavigationScoreRouteEntry>();
    if (!TryCreateScoreRouteProbeWorld(level.Name, level.MapAreaIndex, team, profilePlan.ClassId, out var world, out var bot, out var failureReason))
    {
        summary = $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} status=probe_fail reason={failureReason}";
        return false;
    }

    var maxPhaseRoutesToTry = options.GetEffectiveScoreRouteMaxPhaseRoutes();
    var bestResult = default(BotScenarioResult);
    var hasBestResult = false;
    var proofAttempts = 0;
    var contextStopwatch = Stopwatch.StartNew();
    var stopReason = string.Empty;
    var traversalProof = BuildProvenScoreRouteSegments(level, graphAsset, navigationGraph, team, profilePlan);
    if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
    {
        if (!TryGetScoreRouteGoal(world, bot, team, BotNavigationScoreRoutePhase.AttackIntel, out var attackGoal, out failureReason)
            || !TryGetScoreRouteGoal(world, bot, team, BotNavigationScoreRoutePhase.ReturnIntel, out var returnGoal, out failureReason))
        {
            summary = $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} status=goal_fail reason={failureReason}";
            return false;
        }

        var attackBuild = BuildScoreRouteCandidates(level, navigationGraph, team, profilePlan, BotNavigationScoreRoutePhase.AttackIntel, attackGoal, traversalProof.ProvenSegmentsByKey);
        var returnBuild = BuildScoreRouteCandidates(level, navigationGraph, team, profilePlan, BotNavigationScoreRoutePhase.ReturnIntel, returnGoal, traversalProof.ProvenSegmentsByKey);
        var attackCandidates = attackBuild.Entries
            .Take(maxPhaseRoutesToTry)
            .ToArray();
        var returnCandidates = returnBuild.Entries
            .Take(maxPhaseRoutesToTry)
            .ToArray();
        if (attackCandidates.Length == 0 || returnCandidates.Length == 0)
        {
            summary =
                $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} mode=CTF status=no_candidates proof={traversalProof.ProvenEdgeCount}/{graphAsset.Edges.Count} jumps={traversalProof.ProvenJumpEdgeCount}/{traversalProof.TotalJumpEdgeCount} attack={attackCandidates.Length}/{attackBuild.StartNodeCount}:{attackBuild.RawRouteCount}:{attackBuild.BuildFailures} return={returnCandidates.Length}/{returnBuild.StartNodeCount}:{returnBuild.RawRouteCount}:{returnBuild.BuildFailures}";
            return false;
        }

        for (var returnIndex = 0; returnIndex < returnCandidates.Length; returnIndex += 1)
        {
            for (var attackIndex = 0; attackIndex < attackCandidates.Length; attackIndex += 1)
            {
                var bundle = new[]
                {
                    attackCandidates[attackIndex],
                    returnCandidates[returnIndex],
                };
                if (!TryRunScoreRouteProofAttempt(
                        level,
                        graphAsset,
                        bundle,
                        team,
                        profilePlan,
                        options,
                        contextStopwatch,
                        ref proofAttempts,
                        $"ctf_pair attack={attackIndex + 1}/{attackCandidates.Length} return={returnIndex + 1}/{returnCandidates.Length}",
                        out var result,
                        out stopReason))
                {
                    goto FinishCtfProofs;
                }

                if (!hasBestResult || IsBetterScoreRouteProofResult(result, bestResult))
                {
                    bestResult = result;
                    hasBestResult = true;
                }

                if (result.Passed)
                {
                    validatedRoutes = bundle;
                    summary =
                        $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} mode=CTF status=pass attempts={proofAttempts} attackPair={attackIndex + 1}/{attackCandidates.Length} returnPair={returnIndex + 1}/{returnCandidates.Length} start={result.StartObjectiveDistance:0.0} best={result.BestObjectiveDistance:0.0} reason={result.FailureReason}";
                    return true;
                }
            }
        }

        for (var returnCount = 1; returnCount <= returnCandidates.Length; returnCount += 1)
        {
            for (var attackCount = 1; attackCount <= attackCandidates.Length; attackCount += 1)
            {
                var bundle = attackCandidates.Take(attackCount)
                    .Concat(returnCandidates.Take(returnCount))
                    .ToArray();
                if (!TryRunScoreRouteProofAttempt(
                        level,
                        graphAsset,
                        bundle,
                        team,
                        profilePlan,
                        options,
                        contextStopwatch,
                        ref proofAttempts,
                        $"ctf_prefix attack={attackCount}/{attackCandidates.Length} return={returnCount}/{returnCandidates.Length}",
                        out var result,
                        out stopReason))
                {
                    goto FinishCtfProofs;
                }

                if (!hasBestResult || IsBetterScoreRouteProofResult(result, bestResult))
                {
                    bestResult = result;
                    hasBestResult = true;
                }

                if (result.Passed)
                {
                    validatedRoutes = bundle;
                    summary =
                        $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} mode=CTF status=pass attempts={proofAttempts} attack={attackCount}/{attackCandidates.Length} return={returnCount}/{returnCandidates.Length} start={result.StartObjectiveDistance:0.0} best={result.BestObjectiveDistance:0.0} reason={result.FailureReason}";
                    return true;
                }
            }
        }

FinishCtfProofs:
        summary =
            $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} mode=CTF status=fail proof={traversalProof.ProvenEdgeCount}/{graphAsset.Edges.Count} jumps={traversalProof.ProvenJumpEdgeCount}/{traversalProof.TotalJumpEdgeCount} attack={attackCandidates.Length} return={returnCandidates.Length} attempts={proofAttempts}{FormatScoreRouteStopReason(stopReason)} {FormatScoreRouteBestResult(hasBestResult, bestResult)}";
        return false;
    }

    if (!TryGetScoreRouteGoal(world, bot, team, BotNavigationScoreRoutePhase.CaptureObjective, out var captureGoal, out failureReason))
    {
        summary = $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} status=goal_fail reason={failureReason}";
        return false;
    }

    var captureBuild = BuildScoreRouteCandidates(level, navigationGraph, team, profilePlan, BotNavigationScoreRoutePhase.CaptureObjective, captureGoal, traversalProof.ProvenSegmentsByKey);
    var captureCandidates = captureBuild.Entries
        .Take(maxPhaseRoutesToTry)
        .ToArray();
    if (captureCandidates.Length == 0)
    {
        summary =
            $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} mode={world.MatchRules.Mode} status=no_candidates proof={traversalProof.ProvenEdgeCount}/{graphAsset.Edges.Count} jumps={traversalProof.ProvenJumpEdgeCount}/{traversalProof.TotalJumpEdgeCount} routes={captureCandidates.Length}/{captureBuild.StartNodeCount}:{captureBuild.RawRouteCount}:{captureBuild.BuildFailures}";
        return false;
    }

    for (var routeIndex = 0; routeIndex < captureCandidates.Length; routeIndex += 1)
    {
        var bundle = new[] { captureCandidates[routeIndex] };
        if (!TryRunScoreRouteProofAttempt(
                level,
                graphAsset,
                bundle,
                team,
                profilePlan,
                options,
                contextStopwatch,
                ref proofAttempts,
                $"capture_pair route={routeIndex + 1}/{captureCandidates.Length}",
                out var result,
                out stopReason))
        {
            goto FinishCaptureProofs;
        }

        if (!hasBestResult || IsBetterScoreRouteProofResult(result, bestResult))
        {
            bestResult = result;
            hasBestResult = true;
        }

        if (result.Passed)
        {
            validatedRoutes = bundle;
            summary =
                $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} mode={world.MatchRules.Mode} status=pass attempts={proofAttempts} routePair={routeIndex + 1}/{captureCandidates.Length} start={result.StartObjectiveDistance:0.0} best={result.BestObjectiveDistance:0.0} reason={result.FailureReason}";
            return true;
        }
    }

    for (var routeCount = 1; routeCount <= captureCandidates.Length; routeCount += 1)
    {
        var bundle = captureCandidates.Take(routeCount).ToArray();
        if (!TryRunScoreRouteProofAttempt(
                level,
                graphAsset,
                bundle,
                team,
                profilePlan,
                options,
                contextStopwatch,
                ref proofAttempts,
                $"capture_prefix routes={routeCount}/{captureCandidates.Length}",
                out var result,
                out stopReason))
        {
            goto FinishCaptureProofs;
        }

        if (!hasBestResult || IsBetterScoreRouteProofResult(result, bestResult))
        {
            bestResult = result;
            hasBestResult = true;
        }

        if (result.Passed)
        {
            validatedRoutes = bundle;
            summary =
                $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} mode={world.MatchRules.Mode} status=pass attempts={proofAttempts} routes={routeCount}/{captureCandidates.Length} start={result.StartObjectiveDistance:0.0} best={result.BestObjectiveDistance:0.0} reason={result.FailureReason}";
            return true;
        }
    }

FinishCaptureProofs:
    summary =
            $"score-route map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} mode={world.MatchRules.Mode} status=fail proof={traversalProof.ProvenEdgeCount}/{graphAsset.Edges.Count} jumps={traversalProof.ProvenJumpEdgeCount}/{traversalProof.TotalJumpEdgeCount} routes={captureCandidates.Length} attempts={proofAttempts}{FormatScoreRouteStopReason(stopReason)} {FormatScoreRouteBestResult(hasBestResult, bestResult)}";
    return false;
}

static ScoreRouteCandidateBuildResult BuildScoreRouteCandidates(
    SimpleLevel level,
    BotNavigationRuntimeGraph navigationGraph,
    PlayerTeam team,
    ScoreRouteProfilePlan profilePlan,
    BotNavigationScoreRoutePhase phase,
    (float X, float Y) goal,
    IReadOnlyDictionary<long, BotNavigationScoreRouteSegment> provenSegmentsByKey)
{
    var routeCandidates = new List<(BotNavigationScoreRouteEntry Entry, float Cost, float GoalDistance, int UnprovenJumpCount, int UntapedJumpCount)>();
    var seenRouteKeys = new HashSet<string>(StringComparer.Ordinal);
    var startNodeIds = GetScoreRouteStartNodeIds(level, navigationGraph, team, profilePlan, phase);
    var rawRouteCount = 0;
    var buildFailures = 0;
    foreach (var startNodeId in startNodeIds)
    {
        foreach (var candidateRoute in BuildCandidateRoutesFromStartNode(navigationGraph, startNodeId, goal))
        {
            rawRouteCount += 1;
            var routeKey = string.Join(">", candidateRoute);
            if (!seenRouteKeys.Add(routeKey))
            {
                continue;
            }

            if (!TryBuildScoreRouteEntry(
                    navigationGraph,
                    team,
                    profilePlan,
                    phase,
                    candidateRoute,
                    goal,
                    provenSegmentsByKey,
                    out var entry,
                    out var routeCost,
                    out var goalDistance,
                    out var unprovenJumpCount,
                    out var untapedJumpCount))
            {
                buildFailures += 1;
                continue;
            }

            routeCandidates.Add((entry, routeCost, goalDistance, unprovenJumpCount, untapedJumpCount));
        }
    }

    var orderedCandidates = routeCandidates
        .OrderBy(static entry => entry.GoalDistance)
        .ThenBy(static entry => entry.UnprovenJumpCount)
        .ThenBy(static entry => entry.UntapedJumpCount)
        .ThenBy(static entry => entry.Cost)
        .ThenBy(static entry => entry.Entry.RouteNodeIds.Count)
        .ThenBy(static entry => entry.Entry.Key, StringComparer.Ordinal)
        .ToList();
    var diversifiedCandidates = DiversifyScoreRouteCandidatesByFirstHop(orderedCandidates);

    return new ScoreRouteCandidateBuildResult(
        diversifiedCandidates
            .Select(static entry => entry.Entry)
            .ToArray(),
        startNodeIds.Count,
        rawRouteCount,
        buildFailures);
}

static IReadOnlyList<(BotNavigationScoreRouteEntry Entry, float Cost, float GoalDistance, int UnprovenJumpCount, int UntapedJumpCount)> DiversifyScoreRouteCandidatesByFirstHop(
    IReadOnlyList<(BotNavigationScoreRouteEntry Entry, float Cost, float GoalDistance, int UnprovenJumpCount, int UntapedJumpCount)> orderedCandidates)
{
    if (orderedCandidates.Count <= 1)
    {
        return orderedCandidates;
    }

    var diversified = new List<(BotNavigationScoreRouteEntry Entry, float Cost, float GoalDistance, int UnprovenJumpCount, int UntapedJumpCount)>(orderedCandidates.Count);
    var consumed = new bool[orderedCandidates.Count];
    var seenFirstHopKeys = new HashSet<long>();
    for (var index = 0; index < orderedCandidates.Count; index += 1)
    {
        var routeNodeIds = orderedCandidates[index].Entry.RouteNodeIds;
        if (routeNodeIds.Count < 2)
        {
            continue;
        }

        var firstHopKey = GetRouteEdgeKey(routeNodeIds[0], routeNodeIds[1]);
        if (!seenFirstHopKeys.Add(firstHopKey))
        {
            continue;
        }

        diversified.Add(orderedCandidates[index]);
        consumed[index] = true;
    }

    for (var index = 0; index < orderedCandidates.Count; index += 1)
    {
        if (consumed[index])
        {
            continue;
        }

        diversified.Add(orderedCandidates[index]);
    }

    return diversified;
}

static IReadOnlyList<int> GetScoreRouteStartNodeIds(
    SimpleLevel level,
    BotNavigationRuntimeGraph navigationGraph,
    PlayerTeam team,
    ScoreRouteProfilePlan profilePlan,
    BotNavigationScoreRoutePhase phase)
{
    var nodeIds = new List<int>();
    var seenNodeIds = new HashSet<int>();
    var enemyBase = level.GetIntelBase(GetOpposingTeamForScenario(team));
    var classDefinition = BotNavigationClasses.GetDefinition(profilePlan.ClassId);
    IEnumerable<(float X, float Y)> startPositions = phase switch
    {
        BotNavigationScoreRoutePhase.ReturnIntel => enemyBase.HasValue
            ? new[] { (enemyBase.Value.X, enemyBase.Value.Y) }
            : Array.Empty<(float X, float Y)>(),
        _ => (team == PlayerTeam.Blue ? level.BlueSpawns : level.RedSpawns)
            .Select(spawn => (spawn.X, spawn.Y + classDefinition.CollisionBottom)),
    };

    foreach (var startPosition in startPositions)
    {
        if (phase == BotNavigationScoreRoutePhase.ReturnIntel)
        {
            foreach (var nearbyNodeId in GetNearbyScoreRouteStartNodeIds(
                navigationGraph,
                startPosition.X,
                startPosition.Y,
                preferredRadius: 220f,
                fallbackRadius: 360f,
                maxNodeCount: 12))
            {
                if (seenNodeIds.Add(nearbyNodeId))
                {
                    nodeIds.Add(nearbyNodeId);
                }
            }

            continue;
        }

        foreach (var nearbyNodeId in GetNearbyScoreRouteStartNodeIds(
            navigationGraph,
            startPosition.X,
            startPosition.Y,
            preferredRadius: 180f,
            fallbackRadius: 320f,
            maxNodeCount: 6))
        {
            if (seenNodeIds.Add(nearbyNodeId))
            {
                nodeIds.Add(nearbyNodeId);
            }
        }
    }

    return nodeIds;
}

static IReadOnlyList<int> GetNearbyScoreRouteStartNodeIds(
    BotNavigationRuntimeGraph navigationGraph,
    float x,
    float y,
    float preferredRadius,
    float fallbackRadius,
    int maxNodeCount)
{
    maxNodeCount = Math.Max(1, maxNodeCount);
    var preferredDistanceSquared = preferredRadius > 0f
        ? preferredRadius * preferredRadius
        : float.PositiveInfinity;
    var fallbackDistanceSquared = fallbackRadius > 0f
        ? fallbackRadius * fallbackRadius
        : preferredDistanceSquared;
    var nearbyNodeIds = navigationGraph.Nodes
        .Select(node => new
        {
            Node = node,
            DistanceSquared = MathF.Pow(node.X - x, 2f) + MathF.Pow(node.Y - y, 2f),
        })
        .Where(candidate => candidate.DistanceSquared <= fallbackDistanceSquared)
        .OrderBy(candidate => candidate.DistanceSquared > preferredDistanceSquared ? 1 : 0)
        .ThenBy(candidate => candidate.Node.RequiresGroundSupport ? 0 : 1)
        .ThenBy(candidate => candidate.DistanceSquared)
        .ThenBy(candidate => candidate.Node.Id)
        .Take(maxNodeCount)
        .Select(candidate => candidate.Node.Id)
        .ToArray();

    if (nearbyNodeIds.Length > 0)
    {
        return nearbyNodeIds;
    }

    if (navigationGraph.TryFindNearestNode(x, y, fallbackRadius, requireGroundSupport: true, out var groundedNode)
        || navigationGraph.TryFindNearestNode(x, y, fallbackRadius, requireGroundSupport: false, out groundedNode))
    {
        return [groundedNode.Id];
    }

    return Array.Empty<int>();
}

static IReadOnlyList<int[]> BuildCandidateRoutesFromStartNode(
    BotNavigationRuntimeGraph navigationGraph,
    int startNodeId,
    (float X, float Y) goal)
{
    float[] goalRadii = [220f, 320f, 420f, 520f];
    var routes = new List<int[]>();
    var seenKeys = new HashSet<string>(StringComparer.Ordinal);
    var edgeFilter = CreateScoreRouteCandidateEdgeFilter(navigationGraph);
    var hasExactGoalNode = navigationGraph.TryFindNearestNode(goal.X, goal.Y, 220f, requireGroundSupport: true, out var exactGoalNode)
        || navigationGraph.TryFindNearestNode(goal.X, goal.Y, 220f, requireGroundSupport: false, out exactGoalNode);
    if (hasExactGoalNode)
    {
        TryAddCandidateRoute(navigationGraph.FindRoute(startNodeId, exactGoalNode.Id, edgeFilter), seenKeys, routes);
    }

    for (var radiusIndex = 0; radiusIndex < goalRadii.Length; radiusIndex += 1)
    {
        if (navigationGraph.TryFindRouteToGoalRadius(startNodeId, goal.X, goal.Y, goalRadii[radiusIndex], edgeFilter, out var radiusRoute, out _))
        {
            TryAddCandidateRoute(radiusRoute, seenKeys, routes);
        }
    }

    var baseRoute = routes.FirstOrDefault(route => route.Length > 1);
    if (hasExactGoalNode && baseRoute is not null)
    {
        for (var edgeIndex = 0; edgeIndex < baseRoute.Length - 1 && edgeIndex < 4; edgeIndex += 1)
        {
            var blockedFromNodeId = baseRoute[edgeIndex];
            var blockedToNodeId = baseRoute[edgeIndex + 1];
            Func<BotNavigationEdge, bool> alternateEdgeFilter = edge =>
                !navigationGraph.IsReverseOnlyTraversalBlocked(edge.ToNodeId, edge.FromNodeId)
                && (edge.FromNodeId != blockedFromNodeId
                    || edge.ToNodeId != blockedToNodeId);

            TryAddCandidateRoute(navigationGraph.FindRoute(startNodeId, exactGoalNode.Id, alternateEdgeFilter), seenKeys, routes);
            for (var radiusIndex = 0; radiusIndex < goalRadii.Length; radiusIndex += 1)
            {
                if (navigationGraph.TryFindRouteToGoalRadius(startNodeId, goal.X, goal.Y, goalRadii[radiusIndex], alternateEdgeFilter, out var radiusRoute, out _))
                {
                    TryAddCandidateRoute(radiusRoute, seenKeys, routes);
                }
            }

        }
    }

    if (routes.Count == 0)
    {
        float[] minimumImprovements = [160f, 96f, 48f, 24f, 8f];
        for (var index = 0; index < minimumImprovements.Length; index += 1)
        {
            if (navigationGraph.TryFindBestPartialRoute(
                    startNodeId,
                    goal.X,
                    goal.Y,
                    minimumImprovements[index],
                    edgeFilter,
                    out var partialRoute,
                    out _))
            {
                TryAddCandidateRoute(partialRoute, seenKeys, routes);
            }
        }
    }

    return routes;
}

static void TryAddCandidateRoute(int[]? route, HashSet<string> seenKeys, List<int[]> routes)
{
    if (route is null || route.Length <= 1)
    {
        return;
    }

    var routeKey = string.Join(">", route);
    if (!seenKeys.Add(routeKey))
    {
        return;
    }

    routes.Add(route);
}

static bool TryBuildScoreRouteEntry(
    BotNavigationRuntimeGraph navigationGraph,
    PlayerTeam team,
    ScoreRouteProfilePlan profilePlan,
    BotNavigationScoreRoutePhase phase,
    IReadOnlyList<int> routeNodeIds,
    (float X, float Y) goal,
    IReadOnlyDictionary<long, BotNavigationScoreRouteSegment> provenSegmentsByKey,
    out BotNavigationScoreRouteEntry entry,
    out float routeCost,
    out float goalDistance,
    out int unprovenJumpCount,
    out int untapedJumpCount)
{
    entry = default!;
    routeCost = 0f;
    goalDistance = float.PositiveInfinity;
    unprovenJumpCount = 0;
    untapedJumpCount = 0;
    if (routeNodeIds.Count <= 1)
    {
        return false;
    }

    var segments = new List<BotNavigationScoreRouteSegment>(routeNodeIds.Count - 1);
    for (var index = 0; index < routeNodeIds.Count - 1; index += 1)
    {
        var fromNodeId = routeNodeIds[index];
        var toNodeId = routeNodeIds[index + 1];
        if (!navigationGraph.TryGetEdge(fromNodeId, toNodeId, out var edge)
            || !TryGetScoreRouteSegment(edge, provenSegmentsByKey, out var segment, out var segmentWasProven))
        {
            return false;
        }

        if (edge.Kind == BotNavigationTraversalKind.Jump && !segmentWasProven)
        {
            unprovenJumpCount += 1;
            if (segment.InputTape.Count == 0)
            {
                untapedJumpCount += 1;
            }
        }

        routeCost += Math.Max(1f, edge.Cost);
        segments.Add(segment);
    }

    var goalNodeId = routeNodeIds[^1];
    if (!navigationGraph.TryGetNode(goalNodeId, out var goalNode))
    {
        return false;
    }

    goalDistance = DistanceBetween(goalNode.X, goalNode.Y, goal.X, goal.Y);
    entry = new BotNavigationScoreRouteEntry
    {
        Key = $"{team}:{profilePlan.Profile}:{phase}:{routeNodeIds[0]}->{goalNodeId}",
        Label = $"score:{phase}:{routeNodeIds[0]}->{goalNodeId}",
        Team = team,
        Profile = profilePlan.Profile,
        Phase = phase,
        GoalNodeId = goalNodeId,
        GoalX = goal.X,
        GoalY = goal.Y,
        RouteNodeIds = routeNodeIds.ToArray(),
        Segments = segments,
    };
    return true;
}

static bool TryGetScoreRouteSegment(
    BotNavigationEdge edge,
    IReadOnlyDictionary<long, BotNavigationScoreRouteSegment> provenSegmentsByKey,
    out BotNavigationScoreRouteSegment segment,
    out bool segmentWasProven)
{
    if (provenSegmentsByKey.TryGetValue(GetRouteEdgeKey(edge.FromNodeId, edge.ToNodeId), out segment!))
    {
        segmentWasProven = true;
        return true;
    }

    segmentWasProven = false;
    var startToleranceX = 18f;
    var startToleranceY = 24f;
    var landingToleranceX = 18f;
    var landingToleranceY = edge.Kind == BotNavigationTraversalKind.Walk ? 64f : 18f;
    var requireGroundedArrival = edge.Kind != BotNavigationTraversalKind.Drop;
    IReadOnlyList<BotNavigationInputFrame> inputTape = Array.Empty<BotNavigationInputFrame>();
    if (edge.Kind == BotNavigationTraversalKind.Jump)
    {
        startToleranceX = 10f;
        startToleranceY = 14f;
        landingToleranceX = 24f;
        landingToleranceY = 12f;
        requireGroundedArrival = true;
        inputTape = edge.InputTape;
    }

    segment = new BotNavigationScoreRouteSegment
    {
        FromNodeId = edge.FromNodeId,
        ToNodeId = edge.ToNodeId,
        TraversalKind = edge.Kind,
        StartToleranceX = startToleranceX,
        StartToleranceY = startToleranceY,
        LandingToleranceX = landingToleranceX,
        LandingToleranceY = landingToleranceY,
        RequireGroundedArrival = requireGroundedArrival,
        InputTape = inputTape,
    };
    return true;
}

static ScoreRouteTraversalProofResult BuildProvenScoreRouteSegments(
    SimpleLevel level,
    BotNavigationAsset graphAsset,
    BotNavigationRuntimeGraph navigationGraph,
    PlayerTeam team,
    ScoreRouteProfilePlan profilePlan)
{
    var provenSegmentsByKey = new Dictionary<long, BotNavigationScoreRouteSegment>();
    var classDefinition = BotNavigationClasses.GetDefinition(profilePlan.ClassId);
    var provenJumpEdgeCount = 0;
    var totalJumpEdgeCount = 0;
    for (var edgeIndex = 0; edgeIndex < graphAsset.Edges.Count; edgeIndex += 1)
    {
        var edge = graphAsset.Edges[edgeIndex];
        if (!navigationGraph.TryGetNode(edge.FromNodeId, out var fromNode)
            || !navigationGraph.TryGetNode(edge.ToNodeId, out var toNode))
        {
            continue;
        }

        IReadOnlyList<BotNavigationInputFrame> inputTape = Array.Empty<BotNavigationInputFrame>();
        var startToleranceX = 18f;
        var startToleranceY = 24f;
        var landingToleranceX = 18f;
        var landingToleranceY = edge.Kind == BotNavigationTraversalKind.Walk ? 64f : 18f;
        var requireGroundedArrival = edge.Kind != BotNavigationTraversalKind.Drop;
        if (edge.Kind == BotNavigationTraversalKind.Jump)
        {
            totalJumpEdgeCount += 1;
            if (!BotNavigationMovementValidator.TryBuildJumpTape(
                    level,
                    classDefinition,
                    profilePlan.Profile,
                    fromNode.X,
                    fromNode.Y - classDefinition.CollisionBottom,
                    toNode.X,
                    toNode.Y - classDefinition.CollisionBottom,
                    team,
                    out inputTape,
                    out _))
            {
                var fallbackValidated = edge.InputTape.Count > 0
                    && BotNavigationMovementValidator.TryValidateRecordedTraversalTape(
                        level,
                        classDefinition,
                        fromNode.X,
                        fromNode.Y - classDefinition.CollisionBottom,
                        toNode.X,
                        toNode.Y - classDefinition.CollisionBottom,
                        team,
                        edge.InputTape,
                        requireGroundedArrival: true,
                        out _,
                        out _);
                if (fallbackValidated)
                {
                    inputTape = edge.InputTape;
                }
                else if (!BotNavigationMovementValidator.TryBuildHintJumpTape(
                             level,
                             classDefinition,
                             profilePlan.Profile,
                             fromNode.X,
                             fromNode.Y - classDefinition.CollisionBottom,
                             toNode.X,
                             toNode.Y - classDefinition.CollisionBottom,
                             team,
                             requireGroundedArrival: true,
                             out inputTape,
                             out _))
                {
                    continue;
                }
            }

            provenJumpEdgeCount += 1;
            startToleranceX = 10f;
            startToleranceY = 14f;
            landingToleranceX = 24f;
            landingToleranceY = 12f;
            requireGroundedArrival = true;
        }
        else if (edge.Kind == BotNavigationTraversalKind.Drop)
        {
            startToleranceX = 18f;
            startToleranceY = 24f;
            landingToleranceX = 24f;
            landingToleranceY = 24f;
            requireGroundedArrival = false;
        }

        provenSegmentsByKey[GetRouteEdgeKey(edge.FromNodeId, edge.ToNodeId)] = new BotNavigationScoreRouteSegment
        {
            FromNodeId = edge.FromNodeId,
            ToNodeId = edge.ToNodeId,
            TraversalKind = edge.Kind,
            StartToleranceX = startToleranceX,
            StartToleranceY = startToleranceY,
            LandingToleranceX = landingToleranceX,
            LandingToleranceY = landingToleranceY,
            RequireGroundedArrival = requireGroundedArrival,
            InputTape = inputTape,
        };
    }

    return new ScoreRouteTraversalProofResult(
        provenSegmentsByKey,
        provenSegmentsByKey.Count,
        provenJumpEdgeCount,
        totalJumpEdgeCount);
}

static Func<BotNavigationEdge, bool> CreateScoreRouteCandidateEdgeFilter(BotNavigationRuntimeGraph navigationGraph)
{
    return edge => !navigationGraph.IsReverseOnlyTraversalBlocked(edge.ToNodeId, edge.FromNodeId);
}

static BotScenarioResult RunScoreRouteProof(
    SimpleLevel level,
    BotNavigationAsset graphAsset,
    IReadOnlyList<BotNavigationScoreRouteEntry> routes,
    PlayerTeam team,
    PlayerClass classId,
    NavBuildOptions options)
{
    var overrideAsset = new BotNavigationScoreRouteAsset
    {
        FormatVersion = BotNavigationScoreRouteStore.CurrentFormatVersion,
        LevelName = level.Name,
        MapAreaIndex = level.MapAreaIndex,
        LevelFingerprint = graphAsset.LevelFingerprint,
        Routes = routes.ToArray(),
    };
    BotNavigationScoreRouteStore.SetRuntimeOverride(level.Name, level.MapAreaIndex, overrideAsset);
    try
    {
        var scenario = new BotScenarioCase(
            level.Name,
            team,
            classId,
            options.GetEffectiveScoreRouteProofSeconds(),
            BotScenarioKind.Score,
            level.MapAreaIndex);
        return RunBotScenarioCase(
            scenario,
            options.GetEffectiveScoreRouteProofWallTimeoutMilliseconds(),
            collectDiagnostics: options.ScoreRouteDiagnostics,
            botControllerMode: BotControllerMode.ModernGraphRoute,
            earlyNoProgressStopSeconds: options.GetEffectiveScoreRouteNoProgressStopSeconds());
    }
    finally
    {
        BotNavigationScoreRouteStore.ClearRuntimeOverride(level.Name, level.MapAreaIndex);
    }
}

static BotScenarioResult RunScoreRouteDirectReplayProof(
    SimpleLevel level,
    BotNavigationAsset graphAsset,
    IReadOnlyList<BotNavigationScoreRouteEntry> routes,
    PlayerTeam team,
    PlayerClass classId,
    NavBuildOptions options)
{
    var wallClock = Stopwatch.StartNew();
    var scenario = new BotScenarioCase(
        level.Name,
        team,
        classId,
        options.GetEffectiveScoreRouteProofSeconds(),
        BotScenarioKind.Score,
        level.MapAreaIndex);
    var world = new SimulationWorld();
    if (!world.TryLoadLevel(scenario.LevelName, scenario.MapAreaIndex, preservePlayerStats: false))
    {
        return CreateDirectReplayResult(
            passed: false,
            completedObjective: false,
            startObjectiveDistance: 0f,
            bestObjectiveDistance: 0f,
            maxDistanceFromSpawn: 0f,
            longestNoProgressTicks: 0,
            ticksPerSecond: 60,
            wallMilliseconds: wallClock.ElapsedMilliseconds,
            routeSummary: FormatReplayRouteBundle(routes),
            traceSummary: string.Empty,
            objectiveSummary: string.Empty,
            failureReason: $"failed_to_load_level:{scenario.LevelName}:a{scenario.MapAreaIndex}");
    }

    world.PrepareLocalPlayerJoin();
    const byte botSlot = 2;
    if (!world.TryPrepareNetworkPlayerJoin(botSlot)
        || !world.TrySetNetworkPlayerTeam(botSlot, scenario.Team)
        || !world.TryApplyNetworkPlayerClassSelection(botSlot, scenario.ClassId)
        || !world.TryGetNetworkPlayer(botSlot, out var bot))
    {
        return CreateDirectReplayResult(
            passed: false,
            completedObjective: false,
            startObjectiveDistance: 0f,
            bestObjectiveDistance: 0f,
            maxDistanceFromSpawn: 0f,
            longestNoProgressTicks: 0,
            ticksPerSecond: world.Config.TicksPerSecond,
            wallMilliseconds: wallClock.ElapsedMilliseconds,
            routeSummary: FormatReplayRouteBundle(routes),
            traceSummary: string.Empty,
            objectiveSummary: string.Empty,
            failureReason: "failed_to_spawn_bot");
    }

    if (!TryPrepareScenario(world, bot, scenario, out var setupFailureReason))
    {
        return CreateDirectReplayResult(
            passed: false,
            completedObjective: false,
            startObjectiveDistance: 0f,
            bestObjectiveDistance: 0f,
            maxDistanceFromSpawn: 0f,
            longestNoProgressTicks: 0,
            ticksPerSecond: world.Config.TicksPerSecond,
            wallMilliseconds: wallClock.ElapsedMilliseconds,
            routeSummary: FormatReplayRouteBundle(routes),
            traceSummary: string.Empty,
            objectiveSummary: string.Empty,
            failureReason: setupFailureReason ?? "failed_to_prepare_scenario");
    }

    var navigationGraph = new BotNavigationRuntimeGraph(graphAsset);
    var initialRedCaps = world.RedCaps;
    var initialBlueCaps = world.BlueCaps;
    var initialRedKothTicks = world.KothRedTimerTicksRemaining;
    var initialBlueKothTicks = world.KothBlueTimerTicksRemaining;
    var startX = bot.X;
    var startY = bot.Y;
    var objective = ResolveTeamObjective(world, bot, team);
    var startObjectiveDistance = DistanceBetween(bot.X, bot.Y, objective.X, objective.Y);
    var bestObjectiveDistance = startObjectiveDistance;
    var maxDistanceFromSpawn = 0f;
    var ticksSinceBestObjectiveProgress = 0;
    var longestNoProgressTicks = 0;
    var completedObjective = false;
    var trace = options.ScoreRouteDiagnostics ? new List<string>() : null;
    var totalTickBudget = Math.Max(1, world.Config.TicksPerSecond * scenario.SimulationSeconds);
    var tickCount = 0;
    var routeReplayFailureReason = string.Empty;

    bool Step(PlayerInputSnapshot input, string label)
    {
        if (!world.TrySetNetworkPlayerInput(botSlot, input))
        {
            return false;
        }

        world.AdvanceOneTick();
        tickCount += 1;
        objective = ResolveTeamObjective(world, bot, team);
        completedObjective = HasCompletedTeamObjective(
            world,
            objective,
            team,
            initialRedCaps,
            initialBlueCaps,
            initialRedKothTicks,
            initialBlueKothTicks);
        var objectiveDistance = DistanceBetween(bot.X, bot.Y, objective.X, objective.Y);
        if (objectiveDistance + 16f < bestObjectiveDistance || IsPlayerInCaptureZone(world, bot))
        {
            bestObjectiveDistance = IsPlayerInCaptureZone(world, bot) ? 0f : Math.Min(bestObjectiveDistance, objectiveDistance);
            ticksSinceBestObjectiveProgress = 0;
        }
        else
        {
            ticksSinceBestObjectiveProgress += 1;
            longestNoProgressTicks = Math.Max(longestNoProgressTicks, ticksSinceBestObjectiveProgress);
        }

        maxDistanceFromSpawn = MathF.Max(maxDistanceFromSpawn, DistanceBetween(bot.X, bot.Y, startX, startY));
        if (trace is not null && tickCount % Math.Max(1, world.Config.TicksPerSecond / 10) == 0)
        {
            trace.Add(
                $"t={tickCount / (float)world.Config.TicksPerSecond:0.0} label={label} pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0} vel=({bot.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.0},{bot.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.0}) carry={bot.IsCarryingIntel} d={objectiveDistance:0.0} obj=({objective.X:0.0},{objective.Y:0.0})");
            if (trace.Count > 100)
            {
                trace.RemoveAt(0);
            }
        }

        return true;
    }

    bool CanContinue() => tickCount < totalTickBudget
        && wallClock.ElapsedMilliseconds <= options.GetEffectiveScoreRouteProofWallTimeoutMilliseconds()
        && !completedObjective;

    foreach (var route in routes)
    {
        if (!CanContinue())
        {
            break;
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            if (route.Phase == BotNavigationScoreRoutePhase.AttackIntel && bot.IsCarryingIntel)
            {
                continue;
            }

            if (route.Phase == BotNavigationScoreRoutePhase.ReturnIntel && !bot.IsCarryingIntel)
            {
                continue;
            }
        }

        if (!ReplayScoreRouteEntryDirect(
                world,
                bot,
                navigationGraph,
                route,
                team,
                Step,
                CanContinue,
                trace,
                out var replayFailure))
        {
            if (trace is not null)
            {
                trace.Add($"route_fail route={route.Label} reason={replayFailure}");
            }

            routeReplayFailureReason = $"{route.Label}:{replayFailure}";
            break;
        }

        if (!CanContinue())
        {
            break;
        }

        var routeGoal = ResolveTeamObjective(world, bot, team);
        RunDirectReplayApproach(
            world,
            bot,
            routeGoal.X,
            routeGoal.Y,
            label: $"phase_goal:{route.Phase}",
            maxTicks: world.Config.TicksPerSecond * 4,
            Step,
            CanContinue);
    }

    while (string.IsNullOrWhiteSpace(routeReplayFailureReason) && CanContinue() && tickCount < totalTickBudget)
    {
        var finalGoal = ResolveTeamObjective(world, bot, team);
        _ = RunDirectReplayApproach(
            world,
            bot,
            finalGoal.X,
            finalGoal.Y,
            label: "final_goal",
            maxTicks: world.Config.TicksPerSecond * 2,
            Step,
            CanContinue);
    }

    var timedOut = wallClock.ElapsedMilliseconds > options.GetEffectiveScoreRouteProofWallTimeoutMilliseconds();
    var failureReason = completedObjective
        ? string.Empty
        : timedOut
            ? $"direct_replay_wall_timeout_{options.GetEffectiveScoreRouteProofWallTimeoutMilliseconds()}ms"
            : !string.IsNullOrWhiteSpace(routeReplayFailureReason)
                ? $"direct_replay_route_failed_{routeReplayFailureReason}"
            : $"direct_replay_incomplete_score={world.RedCaps}-{world.BlueCaps}_koth={world.KothRedTimerTicksRemaining}-{world.KothBlueTimerTicksRemaining}";
    if (!completedObjective && longestNoProgressTicks >= world.Config.TicksPerSecond * 6)
    {
        failureReason += $"_no_progress_{longestNoProgressTicks / (float)world.Config.TicksPerSecond:0.0}s";
    }

    return CreateDirectReplayResult(
        passed: completedObjective,
        completedObjective,
        startObjectiveDistance,
        bestObjectiveDistance,
        maxDistanceFromSpawn,
        longestNoProgressTicks,
        world.Config.TicksPerSecond,
        wallClock.ElapsedMilliseconds,
        routeSummary: FormatReplayRouteBundle(routes),
        traceSummary: trace is null ? string.Empty : "|| events: " + string.Join(" | ", trace),
        objectiveSummary: $"mode={world.MatchRules.Mode} caps={world.RedCaps}-{world.BlueCaps} koth={world.KothRedTimerTicksRemaining}-{world.KothBlueTimerTicksRemaining} carry={bot.IsCarryingIntel} ticks={tickCount}",
        failureReason);
}

static bool ReplayScoreRouteEntryDirect(
    SimulationWorld world,
    PlayerEntity bot,
    BotNavigationRuntimeGraph navigationGraph,
    BotNavigationScoreRouteEntry route,
    PlayerTeam team,
    Func<PlayerInputSnapshot, string, bool> step,
    Func<bool> canContinue,
    List<string>? trace,
    out string failureReason)
{
    failureReason = string.Empty;
    if (route.RouteNodeIds.Count <= 0)
    {
        failureReason = "route_has_no_nodes";
        return false;
    }

    if (navigationGraph.TryGetNode(route.RouteNodeIds[0], out var firstNode))
    {
        RunDirectReplayApproach(
            world,
            bot,
            firstNode.X,
            firstNode.Y,
            label: $"prealign:{route.Phase}",
            maxTicks: world.Config.TicksPerSecond * 3,
            step,
            canContinue);
    }

    for (var index = 0; index < route.Segments.Count && canContinue(); index += 1)
    {
        var segment = route.Segments[index];
        if (!navigationGraph.TryGetNode(segment.FromNodeId, out var fromNode)
            || !navigationGraph.TryGetNode(segment.ToNodeId, out var toNode))
        {
            failureReason = $"missing_segment_node:{segment.FromNodeId}->{segment.ToNodeId}";
            return false;
        }

        if (DistanceBetween(bot.X, bot.Bottom, fromNode.X, fromNode.Y) > MathF.Max(segment.StartToleranceX, 48f))
        {
            RunDirectReplayApproach(
                world,
                bot,
                fromNode.X,
                fromNode.Y,
                label: $"align:{segment.FromNodeId}",
                maxTicks: world.Config.TicksPerSecond * 2,
                step,
                canContinue);
        }

        var reached = false;
        if (segment.TraversalKind == BotNavigationTraversalKind.Jump
            && TryBuildCurrentReplayJumpTape(world, bot, team, route.Profile, toNode, out var currentJumpTape))
        {
            var currentJumpSegment = new BotNavigationScoreRouteSegment
            {
                FromNodeId = segment.FromNodeId,
                ToNodeId = segment.ToNodeId,
                TraversalKind = BotNavigationTraversalKind.Jump,
                StartToleranceX = segment.StartToleranceX,
                StartToleranceY = segment.StartToleranceY,
                LandingToleranceX = segment.LandingToleranceX,
                LandingToleranceY = segment.LandingToleranceY,
                RequireGroundedArrival = segment.RequireGroundedArrival,
                InputTape = currentJumpTape,
            };
            reached = ReplayRecordedSegmentTape(
                world,
                bot,
                currentJumpSegment,
                toNode,
                step,
                canContinue);
        }

        if (!reached && segment.InputTape.Count > 0)
        {
            reached = ReplayRecordedSegmentTape(
                world,
                bot,
                segment,
                toNode,
                step,
                canContinue);
        }

        if (!reached && canContinue())
        {
            reached = RunDirectReplayApproach(
                world,
                bot,
                toNode.X,
                toNode.Y,
                label: $"edge:{segment.TraversalKind}:{segment.FromNodeId}->{segment.ToNodeId}",
                maxTicks: EstimateDirectReplayTicks(world, bot, toNode, segment),
                step,
                canContinue);
        }

        if (trace is not null && !reached)
        {
            trace.Add(
                $"segment_miss route={route.Label} edge={segment.FromNodeId}->{segment.ToNodeId} target=({toNode.X:0.0},{toNode.Y:0.0}) pos=({bot.X:0.0},{bot.Bottom:0.0})");
        }

        if (!reached)
        {
            failureReason = $"segment_miss:{segment.FromNodeId}->{segment.ToNodeId}:pos=({bot.X:0.0},{bot.Bottom:0.0}):target=({toNode.X:0.0},{toNode.Y:0.0})";
            return false;
        }
    }

    return true;
}

static bool TryBuildCurrentReplayJumpTape(
    SimulationWorld world,
    PlayerEntity bot,
    PlayerTeam team,
    BotNavigationProfile profile,
    BotNavigationNode targetNode,
    out IReadOnlyList<BotNavigationInputFrame> tape)
{
    var classDefinition = BotNavigationClasses.GetDefinition(bot.ClassId);
    var sourceY = bot.Bottom - classDefinition.CollisionBottom;
    var targetY = targetNode.Y - classDefinition.CollisionBottom;
    return BotNavigationMovementValidator.TryBuildJumpTape(
        world.Level,
        classDefinition,
        profile,
        bot.X,
        sourceY,
        targetNode.X,
        targetY,
        team,
        out tape,
        out _);
}

static bool ReplayRecordedSegmentTape(
    SimulationWorld world,
    PlayerEntity bot,
    BotNavigationScoreRouteSegment segment,
    BotNavigationNode targetNode,
    Func<PlayerInputSnapshot, string, bool> step,
    Func<bool> canContinue)
{
    for (var frameIndex = 0; frameIndex < segment.InputTape.Count && canContinue(); frameIndex += 1)
    {
        var frame = segment.InputTape[frameIndex];
        var ticks = GetReplayFrameTicks(world, frame);
        for (var tick = 0; tick < ticks && canContinue(); tick += 1)
        {
            if (!step(CreateReplayInput(bot, frame.Left, frame.Right, frame.Up, down: false, targetNode.X, targetNode.Y), $"tape:{segment.FromNodeId}->{segment.ToNodeId}"))
            {
                return false;
            }

            if (HasReachedReplayTarget(bot, targetNode.X, targetNode.Y, segment.LandingToleranceX, segment.LandingToleranceY, segment.RequireGroundedArrival))
            {
                return true;
            }
        }
    }

    return HasReachedReplayTarget(bot, targetNode.X, targetNode.Y, segment.LandingToleranceX, segment.LandingToleranceY, segment.RequireGroundedArrival);
}

static bool RunDirectReplayApproach(
    SimulationWorld world,
    PlayerEntity bot,
    float targetX,
    float targetFeetY,
    string label,
    int maxTicks,
    Func<PlayerInputSnapshot, string, bool> step,
    Func<bool> canContinue)
{
    var previousDistance = DistanceBetween(bot.X, bot.Bottom, targetX, targetFeetY);
    var staleTicks = 0;
    var jumpCooldownTicks = 0;
    for (var tick = 0; tick < maxTicks && canContinue(); tick += 1)
    {
        var dx = targetX - bot.X;
        var dy = targetFeetY - bot.Bottom;
        var horizontal = MathF.Abs(dx) <= 6f ? 0 : MathF.Sign(dx);
        var shouldDrop = dy > 28f && MathF.Abs(dx) <= 80f;
        var shouldJump = false;
        if (bot.IsGrounded && jumpCooldownTicks <= 0)
        {
            shouldJump = dy < -18f
                || (MathF.Abs(dx) > 24f && MathF.Abs(bot.HorizontalSpeed) < 0.5f && staleTicks > world.Config.TicksPerSecond / 3);
            if (shouldJump)
            {
                jumpCooldownTicks = world.Config.TicksPerSecond / 2;
            }
        }

        if (!step(CreateReplayInput(bot, horizontal < 0, horizontal > 0, shouldJump, shouldDrop, targetX, targetFeetY), label))
        {
            return false;
        }

        jumpCooldownTicks -= 1;
        if (HasReachedReplayTarget(bot, targetX, targetFeetY, 28f, 56f, requireGroundedArrival: false))
        {
            return true;
        }

        var distance = DistanceBetween(bot.X, bot.Bottom, targetX, targetFeetY);
        if (distance + 1f < previousDistance)
        {
            previousDistance = distance;
            staleTicks = 0;
        }
        else
        {
            staleTicks += 1;
        }
    }

    return HasReachedReplayTarget(bot, targetX, targetFeetY, 36f, 72f, requireGroundedArrival: false);
}

static PlayerInputSnapshot CreateReplayInput(
    PlayerEntity bot,
    bool left,
    bool right,
    bool up,
    bool down,
    float targetX,
    float targetY)
{
    var aimX = MathF.Abs(targetX - bot.X) > 1f
        ? targetX
        : bot.X + (right ? 160f : left ? -160f : 160f);
    return new PlayerInputSnapshot(
        Left: left,
        Right: right,
        Up: up,
        Down: down,
        BuildSentry: false,
        DestroySentry: false,
        Taunt: false,
        FirePrimary: false,
        FireSecondary: false,
        AimWorldX: aimX,
        AimWorldY: targetY,
        DebugKill: false);
}

static int EstimateDirectReplayTicks(
    SimulationWorld world,
    PlayerEntity bot,
    BotNavigationNode targetNode,
    BotNavigationScoreRouteSegment segment)
{
    var distance = DistanceBetween(bot.X, bot.Bottom, targetNode.X, targetNode.Y);
    var baseTicks = segment.TraversalKind switch
    {
        BotNavigationTraversalKind.Jump => world.Config.TicksPerSecond * 2,
        BotNavigationTraversalKind.Drop => world.Config.TicksPerSecond * 2,
        _ => world.Config.TicksPerSecond,
    };
    return Math.Clamp((int)(baseTicks + (distance * 0.35f)), world.Config.TicksPerSecond / 2, world.Config.TicksPerSecond * 5);
}

static bool HasReachedReplayTarget(
    PlayerEntity bot,
    float targetX,
    float targetFeetY,
    float toleranceX,
    float toleranceY,
    bool requireGroundedArrival)
{
    return MathF.Abs(bot.X - targetX) <= toleranceX
        && MathF.Abs(bot.Bottom - targetFeetY) <= toleranceY
        && (!requireGroundedArrival || bot.IsGrounded);
}

static int GetReplayFrameTicks(SimulationWorld world, BotNavigationInputFrame frame)
{
    if (frame.Ticks > 0)
    {
        return frame.Ticks;
    }

    if (frame.DurationSeconds > 0d)
    {
        return Math.Max(1, (int)Math.Round(frame.DurationSeconds * world.Config.TicksPerSecond));
    }

    return 1;
}

static string FormatReplayRouteBundle(IReadOnlyList<BotNavigationScoreRouteEntry> routes)
{
    if (routes.Count == 0)
    {
        return "direct_replay routes=0";
    }

    return "direct_replay "
        + string.Join(
            ";",
            routes.Select(route => $"{route.Phase}:{route.RouteNodeIds.Count}n/{route.Segments.Count}s:{route.Key}"));
}

static BotScenarioResult CreateDirectReplayResult(
    bool passed,
    bool completedObjective,
    float startObjectiveDistance,
    float bestObjectiveDistance,
    float maxDistanceFromSpawn,
    int longestNoProgressTicks,
    int ticksPerSecond,
    long wallMilliseconds,
    string routeSummary,
    string traceSummary,
    string objectiveSummary,
    string failureReason)
{
    return new BotScenarioResult(
        Passed: passed,
        TimedOut: false,
        SatisfiedScenario: passed,
        CompletedObjective: completedObjective,
        StartObjectiveDistance: startObjectiveDistance,
        BestObjectiveDistance: bestObjectiveDistance,
        MaxDistanceFromSpawn: maxDistanceFromSpawn,
        MaxStuckTicks: 0,
        LongestNoProgressSeconds: ticksPerSecond > 0 ? longestNoProgressTicks / (float)ticksPerSecond : 0f,
        WallMilliseconds: wallMilliseconds,
        RouteSummary: routeSummary,
        TraceSummary: traceSummary,
        ObjectiveSummary: objectiveSummary,
        FailureReason: failureReason);
}

static bool TryRunScoreRouteProofAttempt(
    SimpleLevel level,
    BotNavigationAsset graphAsset,
    IReadOnlyList<BotNavigationScoreRouteEntry> routes,
    PlayerTeam team,
    ScoreRouteProfilePlan profilePlan,
    NavBuildOptions options,
    Stopwatch contextStopwatch,
    ref int proofAttempts,
    string attemptLabel,
    out BotScenarioResult result,
    out string stopReason)
{
    result = default;
    if (TryGetScoreRouteProofStopReason(options, contextStopwatch, proofAttempts, out stopReason))
    {
        return false;
    }

    proofAttempts += 1;
    result = RunScoreRouteProof(level, graphAsset, routes, team, profilePlan.ClassId, options);
    if (options.ScoreRouteDiagnostics)
    {
        Console.WriteLine(
            $"score-route-probe map={level.Name} area={level.MapAreaIndex} team={team} profile={profilePlan.Profile} class={profilePlan.ClassId} attempt={proofAttempts} kind=\"{CompactScoreRouteLogValue(attemptLabel, 120)}\" {FormatScoreRouteAttemptResult(result)}");
    }

    stopReason = string.Empty;
    return true;
}

static bool TryGetScoreRouteProofStopReason(
    NavBuildOptions options,
    Stopwatch contextStopwatch,
    int proofAttempts,
    out string stopReason)
{
    stopReason = string.Empty;
    var maxProofAttempts = options.GetEffectiveScoreRouteMaxProofAttemptsPerContext();
    if (maxProofAttempts > 0 && proofAttempts >= maxProofAttempts)
    {
        stopReason = $"max_proofs_{maxProofAttempts}";
        return true;
    }

    return TryGetScoreRouteBudgetStopReason(options.GetEffectiveScoreRouteContextBudgetMilliseconds(), contextStopwatch, out stopReason);
}

static bool TryGetScoreRouteBudgetStopReason(int budgetMilliseconds, Stopwatch stopwatch, out string stopReason)
{
    stopReason = string.Empty;
    if (budgetMilliseconds > 0 && stopwatch.ElapsedMilliseconds >= budgetMilliseconds)
    {
        stopReason = $"budget_{budgetMilliseconds}ms_elapsed_{stopwatch.ElapsedMilliseconds}ms";
        return true;
    }

    return false;
}

static string FormatScoreRouteStopReason(string stopReason)
{
    return string.IsNullOrWhiteSpace(stopReason)
        ? string.Empty
        : $" stopped={stopReason}";
}

static string FormatScoreRouteBestResult(bool hasBestResult, BotScenarioResult bestResult)
{
    if (!hasBestResult)
    {
        return "start=n/a best=n/a progress=n/a reason=no_proof_attempts";
    }

    return
        $"start={bestResult.StartObjectiveDistance:0.0} best={bestResult.BestObjectiveDistance:0.0} progress={bestResult.StartObjectiveDistance - bestResult.BestObjectiveDistance:0.0} reason={bestResult.FailureReason}";
}

static string FormatScoreRouteAttemptResult(BotScenarioResult result)
{
    var progress = result.StartObjectiveDistance - result.BestObjectiveDistance;
    var phase = ExtractLatestTraceToken(result.TraceSummary, "phase=");
    var navIssue = ExtractLatestTraceToken(result.TraceSummary, "navIssue=");
    var routeGoal = ExtractLatestTraceToken(result.TraceSummary, "routeGoal=");
    var status = result.Passed ? "pass" : result.TimedOut ? "timeout" : "fail";
    var details =
        $"status={status} elapsedMs={result.WallMilliseconds} start={result.StartObjectiveDistance:0.0} best={result.BestObjectiveDistance:0.0} progress={progress:0.0} noProgress={result.LongestNoProgressSeconds:0.0}s phase={phase} navIssue={navIssue} routeGoal={routeGoal} reason={CompactScoreRouteLogValue(result.FailureReason, 180)}";
    if (!string.IsNullOrWhiteSpace(result.RouteSummary))
    {
        details += $" routes=\"{CompactScoreRouteLogValue(result.RouteSummary, 240)}\"";
    }

    if (!string.IsNullOrWhiteSpace(result.ObjectiveSummary))
    {
        details += $" objective=\"{CompactScoreRouteLogValue(result.ObjectiveSummary, 320)}\"";
    }

    var traceEvents = ExtractTraceEvents(result.TraceSummary);
    if (!string.IsNullOrWhiteSpace(traceEvents))
    {
        details += $" events=\"{CompactScoreRouteLogValue(traceEvents, 500)}\"";
    }

    return details;
}

static string ClassifyBotScenarioFailure(BotScenarioResult result)
{
    if (result.Passed)
    {
        return string.Empty;
    }

    var combined = string.IsNullOrWhiteSpace(result.RouteSummary)
        ? result.TraceSummary ?? string.Empty
        : result.RouteSummary + " | " + (result.TraceSummary ?? string.Empty);
    var promotions = ExtractModernPromotions(combined);
    var verticalCu0 = promotions
        .Where(entry => entry.CanUse == 0 && Math.Max(entry.LiveRise, entry.GraphRise) >= 70f)
        .DistinctBy(entry => entry.Label)
        .Take(4)
        .ToArray();
    var earlyPromotions = promotions
        .Where(entry => Math.Abs(entry.Feet) >= 45f)
        .DistinctBy(entry => entry.Label)
        .Take(4)
        .ToArray();
    var traceSummary = result.TraceSummary ?? string.Empty;
    var doublebackNoJump = CountTraceFrames(traceSummary, "doubleback", "jump=nojump", ":h0");
    var committedNextBlocked = CountTraceFrames(traceSummary, "committed_next_blocked");
    var reanchors = ExtractTraceLabels(traceSummary, "reanchor:").Take(3).ToArray();
    var branchReturns = ExtractTraceLabels(traceSummary, "second_anchor_block_return").Take(3).ToArray();
    var routeMiss = CountTraceFrames(traceSummary, "mroute-miss") + CountTraceFrames(result.RouteSummary ?? string.Empty, "mroute-miss");

    var primary = "unknown";
    if (verticalCu0.Length > 0 && doublebackNoJump > 0)
    {
        primary = "vertical_continuation_executor";
    }
    else if (committedNextBlocked > 0)
    {
        primary = "selection_live_filter";
    }
    else if (earlyPromotions.Length > 0)
    {
        primary = "promotion_arrival_early";
    }
    else if (reanchors.Length > 0 || branchReturns.Length > 0)
    {
        primary = "reanchor_backtrack_churn";
    }
    else if (routeMiss > 0 || result.FailureReason.Contains("goal", StringComparison.OrdinalIgnoreCase))
    {
        primary = "objective_target_or_route_choice";
    }

    return
        $"primary={primary} verticalCu0={FormatPromotionList(verticalCu0)} earlyPromotions={FormatPromotionList(earlyPromotions)} doublebackNoJump={doublebackNoJump} committedNextBlocked={committedNextBlocked} reanchor={FormatStringList(reanchors)} backtrack={FormatStringList(branchReturns)} routeMiss={routeMiss}";
}

static IReadOnlyList<ModernPromotionTrace> ExtractModernPromotions(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return Array.Empty<ModernPromotionTrace>();
    }

    var result = new List<ModernPromotionTrace>();
    foreach (var part in EnumerateTraceParts(value))
    {
        if (!part.Contains("promote_np", StringComparison.Ordinal))
        {
            continue;
        }

        TryReadTraceMetric(part, ":cp", out var currentPoint);
        TryReadTraceMetric(part, ":np", out var nextPoint);
        TryReadTraceMetric(part, ":np2", out var lookaheadPoint);
        TryReadTraceMetric(part, ":feet", out var feet);
        TryReadTraceMetric(part, ":lr", out var liveRise);
        TryReadTraceMetric(part, ":gr", out var graphRise);
        TryReadTraceMetric(part, ":run", out var run);
        TryReadTraceMetric(part, ":cu", out var canUse);
        result.Add(new ModernPromotionTrace(
            CompactScoreRouteLogValue(part, 140),
            currentPoint,
            nextPoint,
            lookaheadPoint,
            feet,
            liveRise,
            graphRise,
            run,
            canUse));
    }

    return result;
}

static int CountTraceFrames(string value, params string[] requiredTokens)
{
    if (string.IsNullOrWhiteSpace(value) || requiredTokens.Length == 0)
    {
        return 0;
    }

    var count = 0;
    var frames = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var frame in frames)
    {
        var hasAllTokens = true;
        foreach (var token in requiredTokens)
        {
            if (!frame.Contains(token, StringComparison.Ordinal))
            {
                hasAllTokens = false;
                break;
            }
        }

        if (hasAllTokens)
        {
            count += 1;
        }
    }

    return count;
}

static IEnumerable<string> ExtractTraceLabels(string value, string marker)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        yield break;
    }

    foreach (var part in EnumerateTraceParts(value))
    {
        if (part.Contains(marker, StringComparison.Ordinal))
        {
            yield return CompactScoreRouteLogValue(part, 140);
        }
    }
}

static IEnumerable<string> EnumerateTraceParts(string value)
{
    var start = 0;
    for (var index = 0; index <= value.Length; index += 1)
    {
        if (index < value.Length && value[index] != ' ' && value[index] != ',' && value[index] != '|' && value[index] != ';')
        {
            continue;
        }

        if (index > start)
        {
            var part = value[start..index].Trim();
            if (part.Length > 0)
            {
                yield return part;
            }
        }

        start = index + 1;
    }
}

static bool TryReadTraceMetric(string text, string key, out float value)
{
    value = 0f;
    var start = text.IndexOf(key, StringComparison.Ordinal);
    if (start < 0)
    {
        return false;
    }

    start += key.Length;
    var end = start;
    while (end < text.Length)
    {
        var current = text[end];
        if (!char.IsDigit(current) && current != '-' && current != '.')
        {
            break;
        }

        end += 1;
    }

    return end > start
        && float.TryParse(text.AsSpan(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}

static string FormatPromotionList(IReadOnlyList<ModernPromotionTrace> promotions)
{
    if (promotions.Count == 0)
    {
        return "none";
    }

    return string.Join(
        ";",
        promotions.Select(entry =>
            $"cp{entry.CurrentPoint:0}->np{entry.NextPoint:0}/np2{entry.LookaheadPoint:0}:feet{entry.Feet:0}:lr{entry.LiveRise:0}:gr{entry.GraphRise:0}:run{entry.Run:0}:cu{entry.CanUse:0}"));
}

static string FormatStringList(IReadOnlyList<string> values)
{
    return values.Count == 0 ? "none" : string.Join(";", values.Select(value => CompactScoreRouteLogValue(value, 140)));
}

static string ExtractLatestTraceToken(string traceSummary, string tokenPrefix)
{
    if (string.IsNullOrWhiteSpace(traceSummary))
    {
        return "n/a";
    }

    var searchIndex = traceSummary.Length;
    while (searchIndex > 0)
    {
        var index = traceSummary.LastIndexOf(tokenPrefix, searchIndex - 1, StringComparison.Ordinal);
        if (index < 0)
        {
            return "n/a";
        }

        var valueStart = index + tokenPrefix.Length;
        if (valueStart >= traceSummary.Length)
        {
            searchIndex = index;
            continue;
        }

        var valueEnd = valueStart;
        while (valueEnd < traceSummary.Length
            && !char.IsWhiteSpace(traceSummary[valueEnd])
            && traceSummary[valueEnd] != '|')
        {
            valueEnd += 1;
        }

        if (valueEnd > valueStart)
        {
            return CompactScoreRouteLogValue(traceSummary[valueStart..valueEnd], 80);
        }

        searchIndex = index;
    }

    return "n/a";
}

static string ExtractTraceEvents(string traceSummary)
{
    if (string.IsNullOrWhiteSpace(traceSummary))
    {
        return string.Empty;
    }

    const string marker = "|| events:";
    var index = traceSummary.LastIndexOf(marker, StringComparison.Ordinal);
    return index < 0
        ? string.Empty
        : traceSummary[(index + marker.Length)..].Trim();
}

static string CompactScoreRouteLogValue(string value, int maxLength)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "n/a";
    }

    var compact = value
        .Replace('\r', ' ')
        .Replace('\n', ' ')
        .Replace('"', '\'')
        .Trim();
    while (compact.Contains("  ", StringComparison.Ordinal))
    {
        compact = compact.Replace("  ", " ", StringComparison.Ordinal);
    }

    if (maxLength > 0 && compact.Length > maxLength)
    {
        return compact[..Math.Max(0, maxLength - 3)] + "...";
    }

    return compact;
}

static bool TryCreateScoreRouteProbeWorld(
    string levelName,
    int mapAreaIndex,
    PlayerTeam team,
    PlayerClass classId,
    out SimulationWorld world,
    out PlayerEntity bot,
    out string? failureReason)
{
    world = new SimulationWorld();
    bot = default!;
    if (!world.TryLoadLevel(levelName, mapAreaIndex, preservePlayerStats: false))
    {
        failureReason = $"failed_to_load_level:{levelName}:a{mapAreaIndex}";
        return false;
    }

    world.PrepareLocalPlayerJoin();
    const byte botSlot = 2;
    if (!world.TryPrepareNetworkPlayerJoin(botSlot)
        || !world.TrySetNetworkPlayerTeam(botSlot, team)
        || !world.TryApplyNetworkPlayerClassSelection(botSlot, classId)
        || !world.TryGetNetworkPlayer(botSlot, out bot))
    {
        failureReason = "failed_to_spawn_probe_player";
        return false;
    }

    failureReason = null;
    return true;
}

static bool TryGetScoreRouteGoal(
    SimulationWorld world,
    PlayerEntity bot,
    PlayerTeam team,
    BotNavigationScoreRoutePhase phase,
    out (float X, float Y) goal,
    out string? failureReason)
{
    goal = default;
    failureReason = null;
    switch (phase)
    {
        case BotNavigationScoreRoutePhase.AttackIntel:
        case BotNavigationScoreRoutePhase.CaptureObjective:
        {
            var objective = ResolveTeamObjective(world, bot, team);
            goal = (objective.X, objective.Y);
            return true;
        }

        case BotNavigationScoreRoutePhase.ReturnIntel:
        {
            var ownBase = world.Level.GetIntelBase(team);
            if (!ownBase.HasValue)
            {
                failureReason = "missing_own_base";
                return false;
            }

            goal = (ownBase.Value.X, ownBase.Value.Y);
            return true;
        }
    }

    failureReason = $"unsupported_phase:{phase}";
    return false;
}

static bool IsBetterScoreRouteProofResult(BotScenarioResult candidate, BotScenarioResult currentBest)
{
    if (candidate.Passed != currentBest.Passed)
    {
        return candidate.Passed;
    }

    if (candidate.CompletedObjective != currentBest.CompletedObjective)
    {
        return candidate.CompletedObjective;
    }

    if (candidate.BestObjectiveDistance != currentBest.BestObjectiveDistance)
    {
        return candidate.BestObjectiveDistance < currentBest.BestObjectiveDistance;
    }

    return candidate.MaxDistanceFromSpawn > currentBest.MaxDistanceFromSpawn;
}

static IReadOnlyList<ScoreRouteProfilePlan> GetScoreRouteProfilePlans()
{
    return
    [
        new ScoreRouteProfilePlan(BotNavigationProfile.Light, PlayerClass.Scout),
        new ScoreRouteProfilePlan(BotNavigationProfile.Standard, PlayerClass.Pyro),
        new ScoreRouteProfilePlan(BotNavigationProfile.Heavy, PlayerClass.Heavy),
    ];
}

static int RunModernEdgeProbe(NavBuildOptions options)
{
    var levelName = options.MapNames.Count == 1
        ? options.MapNames.Single()
        : string.Empty;
    if (string.IsNullOrWhiteSpace(levelName))
    {
        Console.Error.WriteLine("--probe-modern-edge requires exactly one --map.");
        return 2;
    }

    var world = new SimulationWorld();
    if (!world.TryLoadLevel(levelName))
    {
        Console.Error.WriteLine($"failed_to_load_level {levelName}");
        return 2;
    }

    var asset = BotNavigationModernPointGraphBuilder.Build(
        world.Level,
        BotNavigationLevelFingerprint.Compute(world.Level),
        validateTraversals: false);
    var navPoints = ClientBotNavPoints.Build(asset, world.Level);
    if (!navPoints.TryGetPoint(options.ProbeFromNodeId, out var fromNode)
        || !navPoints.TryGetPoint(options.ProbeToNodeId, out var toNode))
    {
        Console.Error.WriteLine($"missing_probe_node {options.ProbeFromNodeId}->{options.ProbeToNodeId}");
        return 2;
    }

    var hasProbeEdge = navPoints.TryGetConnection(options.ProbeFromNodeId, options.ProbeToNodeId, out _);
    var startX = options.ProbeStartX ?? fromNode.X;
    var startBottom = options.ProbeStartBottom ?? fromNode.Y;
    var targetX = toNode.X;
    var targetBottom = toNode.Y;
    Console.WriteLine(
        $"modern_edge_probe map={levelName} team={options.ScenarioTeam} class={options.ScenarioClasses[0]} edge={options.ProbeFromNodeId}->{options.ProbeToNodeId} exists={hasProbeEdge} start=({startX:0.0},{startBottom:0.0}) from=({fromNode.X:0.0},{fromNode.Y:0.0}) to=({toNode.X:0.0},{toNode.Y:0.0}) nodes={asset.Nodes.Count} edges={asset.Edges.Count}");

    foreach (var policy in new[] { ModernEdgeProbePolicy.HorizontalOnly, ModernEdgeProbePolicy.JumpWhenGrounded, ModernEdgeProbePolicy.JumpWhenBlockedOrAbove })
    {
        var result = RunModernEdgeProbePolicy(
            levelName,
            options.ScenarioTeam,
            options.ScenarioClasses[0],
            navPoints,
            options.ProbeFromNodeId,
            options.ProbeToNodeId,
            startX,
            startBottom,
            targetX,
            targetBottom,
            policy,
            seconds: Math.Max(1, options.GetEffectiveScenarioSeconds()));
        Console.WriteLine(result);
    }

    return 0;
}

static int RunModernChainProbe(NavBuildOptions options)
{
    var levelName = options.MapNames.Count == 1
        ? options.MapNames.Single()
        : string.Empty;
    if (string.IsNullOrWhiteSpace(levelName))
    {
        Console.Error.WriteLine("--probe-modern-chain requires exactly one --map.");
        return 2;
    }

    var world = new SimulationWorld();
    if (!world.TryLoadLevel(levelName))
    {
        Console.Error.WriteLine($"failed_to_load_level {levelName}");
        return 2;
    }

    var asset = BotNavigationModernPointGraphBuilder.Build(
        world.Level,
        BotNavigationLevelFingerprint.Compute(world.Level),
        validateTraversals: false);
    var navPoints = ClientBotNavPoints.Build(asset, world.Level);
    var chain = options.ProbeChainNodeIds;
    var nodes = new BotNavigationNode[chain.Count];
    for (var index = 0; index < chain.Count; index += 1)
    {
        if (!navPoints.TryGetPoint(chain[index], out nodes[index]))
        {
            Console.Error.WriteLine($"missing_chain_node {chain[index]}");
            return 2;
        }
    }

    var startX = options.ProbeStartX ?? nodes[0].X;
    var startBottom = options.ProbeStartBottom ?? nodes[0].Y;
    Console.WriteLine(
        $"modern_chain_probe map={levelName} team={options.ScenarioTeam} class={options.ScenarioClasses[0]} chain={string.Join("->", chain)} start=({startX:0.0},{startBottom:0.0}) nodes={asset.Nodes.Count} edges={asset.Edges.Count}");

    for (var index = 0; index < chain.Count - 1; index += 1)
    {
        Console.WriteLine(
            $"chain_edge {chain[index]}->{chain[index + 1]} exists={navPoints.TryGetConnection(chain[index], chain[index + 1], out _)} from=({nodes[index].X:0.0},{nodes[index].Y:0.0}) to=({nodes[index + 1].X:0.0},{nodes[index + 1].Y:0.0})");
    }

    foreach (var policy in new[]
             {
                 ModernChainProbePolicy.TrackTargetJumpWhenGrounded,
                 ModernChainProbePolicy.TrackTargetNextDirectionGate,
                 ModernChainProbePolicy.HoldEdgeJumpWhenGrounded,
                 ModernChainProbePolicy.HoldEdgeJumpWhenBlockedOrAbove,
                 ModernChainProbePolicy.HoldEdgeSingleJumpPerSegment,
             })
    {
        Console.WriteLine(RunModernChainProbePolicy(
            levelName,
            options.ScenarioTeam,
            options.ScenarioClasses[0],
            navPoints,
            chain,
            nodes,
            startX,
            startBottom,
            policy,
            seconds: Math.Max(1, options.GetEffectiveScenarioSeconds())));
    }

    return 0;
}

static string RunModernChainProbePolicy(
    string levelName,
    PlayerTeam team,
    PlayerClass classId,
    ClientBotNavPoints navPoints,
    IReadOnlyList<int> chain,
    IReadOnlyList<BotNavigationNode> nodes,
    float startX,
    float startBottom,
    ModernChainProbePolicy policy,
    int seconds)
{
    var world = new SimulationWorld();
    if (!world.TryLoadLevel(levelName))
    {
        return $"policy={policy} failed_to_load_level";
    }

    world.PrepareLocalPlayerJoin();
    const byte botSlot = 2;
    if (!world.TryPrepareNetworkPlayerJoin(botSlot)
        || !world.TrySetNetworkPlayerTeam(botSlot, team)
        || !world.TryApplyNetworkPlayerClassSelection(botSlot, classId)
        || !world.TryGetNetworkPlayer(botSlot, out var player))
    {
        return $"policy={policy} failed_to_spawn_player";
    }

    if (!TrySpawnPlayerResolvedForScenario(world, player, team, startX, startBottom - player.CollisionBottomOffset))
    {
        return $"policy={policy} failed_to_place_player";
    }

    player.TeleportTo(startX, startBottom - player.CollisionBottomOffset);
    player.ResolveBlockingOverlap(world.Level, team);

    var lines = new List<string> { $"policy={policy}" };
    var segmentIndex = 0;
    var segmentJumpUsed = false;
    var jumpCooldownTicks = 0;
    var bestDistance = float.PositiveInfinity;
    var bestSegment = 0;
    var bestTick = 0;
    var completeTick = -1;
    var totalTicks = Math.Max(1, seconds) * world.Config.TicksPerSecond;

    for (var tick = 0; tick < totalTicks && segmentIndex < nodes.Count - 1; tick += 1)
    {
        var fromNode = nodes[segmentIndex];
        var targetNode = nodes[segmentIndex + 1];
        var targetX = targetNode.X;
        var targetBottom = targetNode.Y;
        var horizontal = policy switch
        {
            ModernChainProbePolicy.TrackTargetJumpWhenGrounded or ModernChainProbePolicy.TrackTargetNextDirectionGate => MathF.Sign(targetX - player.X),
            _ => MathF.Sign(targetNode.X - fromNode.X),
        };
        var left = horizontal < 0f;
        var right = horizontal > 0f;
        var targetAbove = targetBottom < player.Bottom - 8f;
        var slowHorizontal = MathF.Abs(player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond) < 0.25f;
        var jump = policy switch
        {
            ModernChainProbePolicy.HoldEdgeSingleJumpPerSegment => player.IsGrounded && !segmentJumpUsed && targetAbove,
            ModernChainProbePolicy.HoldEdgeJumpWhenBlockedOrAbove => player.IsGrounded && jumpCooldownTicks <= 0 && (targetAbove || slowHorizontal),
            _ => player.IsGrounded && jumpCooldownTicks <= 0 && targetAbove,
        };

        if (jump)
        {
            jumpCooldownTicks = 8;
            segmentJumpUsed = true;
        }
        else if (jumpCooldownTicks > 0)
        {
            jumpCooldownTicks -= 1;
        }

        var input = new PlayerInputSnapshot(
            Left: left,
            Right: right,
            Up: jump,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: targetX,
            AimWorldY: targetBottom,
            DebugKill: false);
        if (!world.TrySetNetworkPlayerInput(botSlot, input))
        {
            return $"policy={policy} failed_to_apply_input";
        }

        world.AdvanceOneTick();

        var distance = DistanceBetween(player.X, player.Bottom, targetX, targetBottom);
        if (distance < bestDistance || segmentIndex > bestSegment)
        {
            bestDistance = distance;
            bestSegment = segmentIndex;
            bestTick = tick + 1;
        }

        var reachedSegment = MathF.Abs(player.X - targetX) <= 18f && MathF.Abs(player.Bottom - targetBottom) <= 36f;
        var advanceAllowed = reachedSegment
            && (policy != ModernChainProbePolicy.TrackTargetNextDirectionGate
                || segmentIndex + 2 >= nodes.Count
                || IsModernChainNextDirectionReady(player, targetNode, nodes[segmentIndex + 2]));
        if ((tick < 12 || tick % 10 == 9 || reachedSegment) && lines.Count < 44)
        {
            lines.Add(
                $"t={(tick + 1) / (float)world.Config.TicksPerSecond:0.00} seg={segmentIndex}:{chain[segmentIndex]}->{chain[segmentIndex + 1]} pos=({player.X:0.0},{player.Y:0.0}) bottom={player.Bottom:0.0} vel=({player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.00},{player.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.00}) input=L{left}/R{right}/J{jump} d={distance:0.0} reached={reachedSegment} advance={advanceAllowed} grounded={player.IsGrounded}");
        }

        if (advanceAllowed)
        {
            lines.Add(
                $"advance t={(tick + 1) / (float)world.Config.TicksPerSecond:0.00} reached={chain[segmentIndex + 1]} bottomDelta={player.Bottom - targetBottom:0.0} xDelta={player.X - targetX:0.0} grounded={player.IsGrounded}");
            segmentIndex += 1;
            segmentJumpUsed = false;
            bestDistance = float.PositiveInfinity;
            if (segmentIndex >= nodes.Count - 1)
            {
                completeTick = tick + 1;
                break;
            }
        }
    }

    var finalTarget = nodes[^1];
    var finalDistance = DistanceBetween(player.X, player.Bottom, finalTarget.X, finalTarget.Y);
    lines.Add(
        $"summary policy={policy} completeTick={FormatProbeTick(completeTick, world.Config.TicksPerSecond)} reachedSegment={segmentIndex}/{nodes.Count - 1} bestSegment={bestSegment} best={bestDistance:0.0}@{bestTick / (float)world.Config.TicksPerSecond:0.00}s final=({player.X:0.0},{player.Y:0.0}) bottom={player.Bottom:0.0} finalDistance={finalDistance:0.0} vel=({player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.00},{player.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.00}) grounded={player.IsGrounded}");
    if (segmentIndex < nodes.Count - 1)
    {
        lines.Add(ModernPracticeBotController.DescribeModernEdgeProbe(
            world,
            player,
            navPoints,
            chain[segmentIndex],
            chain[segmentIndex + 1],
            stuckTicks: Math.Min(totalTicks, 120)));
    }

    return string.Join(Environment.NewLine, lines);
}

static bool IsModernChainNextDirectionReady(PlayerEntity player, BotNavigationNode reachedNode, BotNavigationNode nextNode)
{
    var nextDirection = MathF.Sign(nextNode.X - reachedNode.X);
    var horizontalSpeed = player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond;
    return nextDirection == 0f
        || player.IsGrounded
        || MathF.Abs(horizontalSpeed) <= 0.25f
        || MathF.Sign(horizontalSpeed) == nextDirection;
}

static string RunModernEdgeProbePolicy(
    string levelName,
    PlayerTeam team,
    PlayerClass classId,
    ClientBotNavPoints navPoints,
    int fromNodeId,
    int toNodeId,
    float startX,
    float startBottom,
    float targetX,
    float targetBottom,
    ModernEdgeProbePolicy policy,
    int seconds)
{
    var world = new SimulationWorld();
    if (!world.TryLoadLevel(levelName))
    {
        return $"policy={policy} failed_to_load_level";
    }

    world.PrepareLocalPlayerJoin();
    const byte botSlot = 2;
    if (!world.TryPrepareNetworkPlayerJoin(botSlot)
        || !world.TrySetNetworkPlayerTeam(botSlot, team)
        || !world.TryApplyNetworkPlayerClassSelection(botSlot, classId)
        || !world.TryGetNetworkPlayer(botSlot, out var player))
    {
        return $"policy={policy} failed_to_spawn_player";
    }

    if (!TrySpawnPlayerResolvedForScenario(world, player, team, startX, startBottom - player.CollisionBottomOffset))
    {
        return $"policy={policy} failed_to_place_player";
    }

    player.TeleportTo(startX, startBottom - player.CollisionBottomOffset);
    player.ResolveBlockingOverlap(world.Level, team);

    var lines = new List<string>
    {
        $"policy={policy}",
        ModernPracticeBotController.DescribeModernEdgeProbe(world, player, navPoints, fromNodeId, toNodeId, stuckTicks: 0),
    };

    var bestDistance = DistanceBetween(player.X, player.Bottom, targetX, targetBottom);
    var bestTick = 0;
    var successTick = -1;
    var jumpCooldownTicks = 0;
    var totalTicks = Math.Max(1, seconds) * world.Config.TicksPerSecond;
    for (var tick = 0; tick < totalTicks; tick += 1)
    {
        var horizontal = MathF.Sign(targetX - player.X);
        var left = horizontal < 0f;
        var right = horizontal > 0f;
        var jump = policy switch
        {
            ModernEdgeProbePolicy.JumpWhenGrounded => player.IsGrounded && jumpCooldownTicks <= 0,
            ModernEdgeProbePolicy.JumpWhenBlockedOrAbove => player.IsGrounded
                && jumpCooldownTicks <= 0
                && (targetBottom < player.Bottom - 8f || MathF.Abs(player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond) < 0.25f),
            _ => false,
        };

        if (jump)
        {
            jumpCooldownTicks = 8;
        }
        else if (jumpCooldownTicks > 0)
        {
            jumpCooldownTicks -= 1;
        }

        var input = new PlayerInputSnapshot(
            Left: left,
            Right: right,
            Up: jump,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: targetX,
            AimWorldY: targetBottom,
            DebugKill: false);
        if (!world.TrySetNetworkPlayerInput(botSlot, input))
        {
            return $"policy={policy} failed_to_apply_input";
        }

        world.AdvanceOneTick();

        var distance = DistanceBetween(player.X, player.Bottom, targetX, targetBottom);
        if (distance < bestDistance)
        {
            bestDistance = distance;
            bestTick = tick + 1;
        }

        if ((tick < 10 || tick % 15 == 14) && lines.Count < 18)
        {
            lines.Add(
                $"t={(tick + 1) / (float)world.Config.TicksPerSecond:0.00} pos=({player.X:0.0},{player.Y:0.0}) bottom={player.Bottom:0.0} vel=({player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.00},{player.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.00}) input=L{left}/R{right}/J{jump} d={distance:0.0} grounded={player.IsGrounded}");
        }

        if (MathF.Abs(player.X - targetX) <= 18f && MathF.Abs(player.Bottom - targetBottom) <= 36f)
        {
            successTick = tick + 1;
            break;
        }
    }

    lines.Add(ModernPracticeBotController.DescribeModernEdgeProbe(world, player, navPoints, fromNodeId, toNodeId, stuckTicks: Math.Min(totalTicks, 120)));
    lines.Add(
        $"summary policy={policy} successTick={FormatProbeTick(successTick, world.Config.TicksPerSecond)} best={bestDistance:0.0}@{bestTick / (float)world.Config.TicksPerSecond:0.00}s final=({player.X:0.0},{player.Y:0.0}) bottom={player.Bottom:0.0} vel=({player.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.00},{player.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.00}) grounded={player.IsGrounded}");
    return string.Join(Environment.NewLine, lines);
}

static string FormatProbeTick(int tick, int ticksPerSecond)
{
    return tick < 0 ? "n/a" : $"{tick / (float)Math.Max(1, ticksPerSecond):0.00}s";
}

static int RunCollisionWindowProbe(NavBuildOptions options)
{
    var levelName = options.MapNames.Count == 1
        ? options.MapNames.Single()
        : string.Empty;
    if (string.IsNullOrWhiteSpace(levelName))
    {
        Console.Error.WriteLine("--probe-collision-window requires exactly one --map.");
        return 2;
    }

    if (!options.ProbeStartX.HasValue || !options.ProbeStartBottom.HasValue)
    {
        Console.Error.WriteLine("--probe-collision-window requires --start-x and --start-bottom.");
        return 2;
    }

    var world = new SimulationWorld();
    if (!world.TryLoadLevel(levelName))
    {
        Console.Error.WriteLine($"failed_to_load_level {levelName}");
        return 2;
    }

    world.PrepareLocalPlayerJoin();
    const byte botSlot = 2;
    var team = options.ScenarioTeam;
    var classId = options.ScenarioClasses[0];
    if (!world.TryPrepareNetworkPlayerJoin(botSlot)
        || !world.TrySetNetworkPlayerTeam(botSlot, team)
        || !world.TryApplyNetworkPlayerClassSelection(botSlot, classId)
        || !world.TryGetNetworkPlayer(botSlot, out var player))
    {
        Console.Error.WriteLine("failed_to_spawn_player");
        return 2;
    }

    var startX = options.ProbeStartX.Value;
    var startBottom = options.ProbeStartBottom.Value;
    player.TeleportTo(startX, startBottom - player.CollisionBottomOffset);
    player.ResolveBlockingOverlap(world.Level, team);

    Console.WriteLine(
        $"collision_window map={levelName} team={team} class={classId} requested=({startX:0.0},{startBottom:0.0}) resolved=({player.X:0.0},{player.Y:0.0}) bottom={player.Bottom:0.0} bounds={FormatCollisionBounds(player, player.X, player.Y)} solids={world.Level.Solids.Count}");
    Console.WriteLine(DescribeNearbyRoomObjects(world.Level, player.X, player.Bottom, radius: 220f));
    Console.WriteLine(DescribeCollisionOccupancy(world.Level, player, team, player.X, player.Y, "current"));

    foreach (var (dx, dy, label) in new[]
    {
        (1f, 0f, "x+1"),
        (2f, 0f, "x+2"),
        (4f, 0f, "x+4"),
        (8f, 0f, "x+8"),
        (1f, -6f, "x+1_y-6_step"),
        (1f, -12f, "x+1_y-12"),
        (1f, 6f, "x+1_y+6"),
        (-1f, 0f, "x-1"),
        (-8f, 0f, "x-8"),
    })
    {
        Console.WriteLine(DescribeCollisionOccupancy(world.Level, player, team, player.X + dx, player.Y + dy, label));
    }

    Console.WriteLine(DescribeNearestOccupiableOffsets(world.Level, player, team, player.X, player.Y));
    Console.WriteLine(DescribeHorizontalWallProfile(world.Level, player, team, player.X, player.Y, direction: 1));

    return 0;
}

static int RunGmlLinkProbe(NavBuildOptions options)
{
    var levelName = options.MapNames.Count == 1
        ? options.MapNames.Single()
        : string.Empty;
    if (string.IsNullOrWhiteSpace(levelName))
    {
        Console.Error.WriteLine("--probe-gml-link requires exactly one --map.");
        return 2;
    }

    var world = new SimulationWorld();
    if (!world.TryLoadLevel(levelName))
    {
        Console.Error.WriteLine($"failed_to_load_level {levelName}");
        return 2;
    }

    var asset = BotNavigationModernPointGraphBuilder.Build(
        world.Level,
        BotNavigationLevelFingerprint.Compute(world.Level),
        validateTraversals: false);
    Console.WriteLine(GmlClientBotPointGraphBuilder.DescribeConnection(world.Level, asset, options.ProbeFromNodeId, options.ProbeToNodeId));
    return 0;
}

static string FormatCollisionBounds(PlayerEntity player, float x, float y)
{
    player.GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);
    return $"[{left:0.0},{top:0.0},{right:0.0},{bottom:0.0}]";
}

static string DescribeNearbyRoomObjects(SimpleLevel level, float x, float y, float radius)
{
    var nearby = level.RoomObjects
        .Where(roomObject => MathF.Abs(roomObject.CenterX - x) <= radius && MathF.Abs(roomObject.CenterY - y) <= radius)
        .OrderBy(roomObject => DistanceBetween(roomObject.CenterX, roomObject.CenterY, x, y))
        .Take(16)
        .Select(roomObject => $"{roomObject.Type}:team{FormatNullableTeam(roomObject.Team)}:{roomObject.SourceName}[{roomObject.Left:0},{roomObject.Top:0},{roomObject.Right:0},{roomObject.Bottom:0}]")
        .ToArray();
    return nearby.Length == 0
        ? $"nearby_room_objects none radius={radius:0}"
        : $"nearby_room_objects {string.Join(" | ", nearby)}";
}

static string DescribeCollisionOccupancy(SimpleLevel level, PlayerEntity player, PlayerTeam team, float x, float y, string label)
{
    player.GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var bottom);
    var blockers = GetCollisionBlockers(level, player, team, left, top, right, bottom);
    return blockers.Count == 0
        ? $"{label} pos=({x:0.0},{y:0.0}) bottom={y + player.CollisionBottomOffset:0.0} bounds=[{left:0.0},{top:0.0},{right:0.0},{bottom:0.0}] occupy=True"
        : $"{label} pos=({x:0.0},{y:0.0}) bottom={y + player.CollisionBottomOffset:0.0} bounds=[{left:0.0},{top:0.0},{right:0.0},{bottom:0.0}] occupy=False blockers={string.Join(" | ", blockers.Take(8))}";
}

static IReadOnlyList<string> GetCollisionBlockers(SimpleLevel level, PlayerEntity player, PlayerTeam team, float left, float top, float right, float bottom)
{
    var blockers = new List<string>();
    for (var index = 0; index < level.Solids.Count; index += 1)
    {
        var solid = level.Solids[index];
        if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
        {
            blockers.Add($"solid#{index}[{solid.Left:0},{solid.Top:0},{solid.Right:0},{solid.Bottom:0}]");
        }
    }

    foreach (var gate in level.GetBlockingTeamGates(team, player.IsCarryingIntel))
    {
        if (left < gate.Right && right > gate.Left && top < gate.Bottom && bottom > gate.Top)
        {
            blockers.Add($"gate:{gate.Type}:team{FormatNullableTeam(gate.Team)}:{gate.SourceName}[{gate.Left:0},{gate.Top:0},{gate.Right:0},{gate.Bottom:0}]");
        }
    }

    foreach (var wall in level.GetRoomObjects(RoomObjectType.PlayerWall))
    {
        if (left < wall.Right && right > wall.Left && top < wall.Bottom && bottom > wall.Top)
        {
            blockers.Add($"wall:{wall.Type}:{wall.SourceName}[{wall.Left:0},{wall.Top:0},{wall.Right:0},{wall.Bottom:0}]");
        }
    }

    return blockers;
}

static string FormatNullableTeam(PlayerTeam? team)
{
    return team.HasValue ? team.Value.ToString() : "None";
}

static string DescribeNearestOccupiableOffsets(SimpleLevel level, PlayerEntity player, PlayerTeam team, float originX, float originY)
{
    var hits = new List<string>();
    for (var radius = 1; radius <= 40 && hits.Count < 12; radius += 1)
    {
        for (var dy = -radius; dy <= radius && hits.Count < 12; dy += 1)
        {
            foreach (var dx in new[] { radius, -radius })
            {
                if (Math.Abs(dy) != radius && Math.Abs(dx) != radius)
                {
                    continue;
                }

                player.GetCollisionBoundsAt(originX + dx, originY + dy, out var left, out var top, out var right, out var bottom);
                if (GetCollisionBlockers(level, player, team, left, top, right, bottom).Count == 0)
                {
                    hits.Add($"dx{dx:+0;-0;0}/dy{dy:+0;-0;0}->bottom{originY + dy + player.CollisionBottomOffset:0.0}");
                }
            }
        }
    }

    return hits.Count == 0
        ? "nearest_occupiable_offsets none_within_40"
        : $"nearest_occupiable_offsets {string.Join(", ", hits)}";
}

static string DescribeHorizontalWallProfile(SimpleLevel level, PlayerEntity player, PlayerTeam team, float originX, float originY, int direction)
{
    var samples = new List<string>();
    for (var dx = 0; dx <= 96; dx += 4)
    {
        var x = originX + (dx * direction);
        var firstFreeUp = int.MinValue;
        for (var dy = 0; dy >= -48; dy -= 2)
        {
            player.GetCollisionBoundsAt(x, originY + dy, out var left, out var top, out var right, out var bottom);
            if (GetCollisionBlockers(level, player, team, left, top, right, bottom).Count == 0)
            {
                firstFreeUp = dy;
                break;
            }
        }

        if (firstFreeUp == int.MinValue)
        {
            samples.Add($"dx{dx}:blocked>48");
        }
        else if (firstFreeUp == 0)
        {
            samples.Add($"dx{dx}:free");
        }
        else
        {
            samples.Add($"dx{dx}:up{-firstFreeUp}");
        }
    }

    return $"right_profile {string.Join(" ", samples)}";
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
                cases.Add(new BotScenarioCase(mapName, PlayerTeam.Blue, scenarioClass, options.GetEffectiveScenarioSeconds(), BotScenarioKind.Score));
                cases.Add(new BotScenarioCase(mapName, PlayerTeam.Red, scenarioClass, options.GetEffectiveScenarioSeconds(), BotScenarioKind.Score));
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
                cases.Add(new BotScenarioCase(mapName, PlayerTeam.Blue, scenarioClass, options.GetEffectiveScenarioSeconds(), options.ScenarioKind));
                cases.Add(new BotScenarioCase(mapName, PlayerTeam.Red, scenarioClass, options.GetEffectiveScenarioSeconds(), options.ScenarioKind));
            }
        }

        return cases;
    }

    foreach (var mapName in options.MapNames)
    {
        foreach (var scenarioClass in scenarioClasses)
        {
            cases.Add(new BotScenarioCase(
                mapName,
                options.ScenarioTeam,
                scenarioClass,
                options.GetEffectiveScenarioSeconds(),
                options.ScenarioKind,
                StartFeetX: options.ScenarioStartFeetX,
                StartFeetY: options.ScenarioStartFeetY,
                EnemyClassId: options.ScenarioEnemyClass,
                EnemyFeetX: options.ScenarioEnemyFeetX,
                EnemyFeetY: options.ScenarioEnemyFeetY));
        }
    }

    return cases;
}

static BotScenarioResult RunBotScenarioCase(
    BotScenarioCase scenario,
    int wallTimeoutMilliseconds,
    bool collectDiagnostics,
    BotControllerMode botControllerMode,
    int earlyNoProgressStopSeconds = 0)
{
    var wallClock = Stopwatch.StartNew();
    var world = botControllerMode == BotControllerMode.MotionProof
        ? new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        })
        : new SimulationWorld();
    if (!world.TryLoadLevel(scenario.LevelName, scenario.MapAreaIndex, preservePlayerStats: false))
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
            ObjectiveSummary: string.Empty,
            FailureReason: $"failed_to_load_level:{scenario.LevelName}:a{scenario.MapAreaIndex}");
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
            ObjectiveSummary: string.Empty,
            FailureReason: "failed_to_spawn_bot");
    }

    var controller = CreateScenarioController(botControllerMode);
    controller.CollectDiagnostics = collectDiagnostics;
    var controlledSlots = new Dictionary<byte, ControlledBotSlot>
    {
        [botSlot] = new(botSlot, scenario.Team, scenario.ClassId),
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
            ObjectiveSummary: string.Empty,
            FailureReason: setupFailureReason ?? "failed_to_prepare_scenario");
    }

    PlayerEntity? combatEnemy = null;
    if (scenario.Kind == BotScenarioKind.Combat
        && !TryPrepareCombatEnemy(world, scenario, out combatEnemy, out var combatSetupFailureReason))
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
            ObjectiveSummary: string.Empty,
            FailureReason: combatSetupFailureReason ?? "failed_to_prepare_combat_enemy");
    }

    if (scenario.Kind == BotScenarioKind.Combat)
    {
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnablePracticeBotsPrioritizeKills: true));
    }

    var startX = bot.X;
    var startY = bot.Y;
    var objective = ResolveScenarioObjective(world, bot, scenario.Team, combatEnemy);
    var initialRedCaps = world.RedCaps;
    var initialBlueCaps = world.BlueCaps;
    var initialRedKothTicks = world.KothRedTimerTicksRemaining;
    var initialBlueKothTicks = world.KothBlueTimerTicksRemaining;
    var initialBotKills = bot.Kills;
    var initialCombatEnemyDeaths = combatEnemy?.Deaths ?? 0;
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
    var earlyNoProgressStopTicks = earlyNoProgressStopSeconds > 0
        ? world.Config.TicksPerSecond * earlyNoProgressStopSeconds
        : 0;
    var lastCarryState = bot.IsCarryingIntel;
    var lastRedCaps = world.RedCaps;
    var lastBlueCaps = world.BlueCaps;
    var lastRedKothTicks = world.KothRedTimerTicksRemaining;
    var lastBlueKothTicks = world.KothBlueTimerTicksRemaining;
    var startBotInCaptureZone = IsPlayerInCaptureZone(world, bot);
    var lastPointState = DescribeTrackedControlPoint(world, objective.ControlPointIndex);
    var firstObjectiveZoneTick = IsBotInObjectiveZoneForScenario(world, bot, objective, scenario.Team, startBotInCaptureZone, startObjectiveDistance)
        ? 0
        : (int?)null;
    var firstCarryTick = bot.IsCarryingIntel ? 0 : (int?)null;
    var firstCapProgressTick = HasTeamCaptureProgressStarted(world, objective.ControlPointIndex, scenario.Team, initialRedKothTicks, initialBlueKothTicks)
        ? 0
        : (int?)null;
    var objectiveAreaEntryTick = IsBotInObjectiveAreaForScenario(objective.Mode, startObjectiveDistance, startBotInCaptureZone)
        ? 0
        : (int?)null;
    var objectiveAreaEntryRouteLabel = string.Empty;
    var objectiveAreaEntryRouteGoal = string.Empty;
    var hasEverCaptureZoneRect = startBotInCaptureZone;
    var captureHoldSeen = false;
    var bestCtfAttackDistance = objective.Mode == GameModeKind.CaptureTheFlag && !bot.IsCarryingIntel
        ? startObjectiveDistance
        : float.PositiveInfinity;
    var bestCtfReturnDistance = objective.Mode == GameModeKind.CaptureTheFlag && bot.IsCarryingIntel
        ? startObjectiveDistance
        : float.PositiveInfinity;
    var lastScorePhase = collectDiagnostics
        ? DescribeScorePhase(
            scenario,
            world,
            bot,
            objective,
            completedObjective: false,
            botInCaptureZone: startBotInCaptureZone,
            objectiveDistance: startObjectiveDistance,
            initialRedCaps,
            initialBlueCaps,
            initialRedKothTicks,
            initialBlueKothTicks,
            scenario.Team)
        : string.Empty;
    var lastRouteGoalKey = string.Empty;
    var lastNavigationIssueKey = string.Empty;
    if (collectDiagnostics && !string.IsNullOrEmpty(lastScorePhase))
    {
        objectiveTrace!.Add(
            $"t=0.0 phase={lastScorePhase} obj=({objective.X:0.0},{objective.Y:0.0}) d={startObjectiveDistance:0.0} carry={bot.IsCarryingIntel}");
    }

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
                firstObjectiveZoneTick,
                firstCarryTick,
                firstCapProgressTick,
                hasEverCaptureZoneRect,
                captureHoldSeen,
                collectDiagnostics,
                objectiveAreaEntryTick,
                objectiveAreaEntryRouteLabel,
                objectiveAreaEntryRouteGoal,
                bestCtfAttackDistance,
                bestCtfReturnDistance,
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
                firstObjectiveZoneTick,
                firstCarryTick,
                firstCapProgressTick,
                hasEverCaptureZoneRect,
                captureHoldSeen,
                collectDiagnostics,
                objectiveAreaEntryTick,
                objectiveAreaEntryRouteLabel,
                objectiveAreaEntryRouteGoal,
                bestCtfAttackDistance,
                bestCtfReturnDistance,
                failureReason: "failed_to_apply_input",
                timedOut: false);
        }

        world.AdvanceOneTick();

        objective = ResolveScenarioObjective(world, bot, scenario.Team, combatEnemy);
        completedObjective = scenario.Kind == BotScenarioKind.Combat
            ? combatEnemy is not null && (bot.Kills > initialBotKills || combatEnemy.Deaths > initialCombatEnemyDeaths)
            : HasCompletedTeamObjective(
                world,
                objective,
                scenario.Team,
                initialRedCaps,
                initialBlueCaps,
                initialRedKothTicks,
                initialBlueKothTicks);

        var hasDiagnostic = collectDiagnostics
            && controller.LastDiagnostics.Entries.Any(entry => entry.Slot == botSlot);
        var diagnostic = hasDiagnostic
            ? controller.LastDiagnostics.Entries.Single(entry => entry.Slot == botSlot)
            : default;
        var objectiveDistance = DistanceBetween(bot.X, bot.Y, objective.X, objective.Y);
        var botInCaptureZone = IsPlayerInCaptureZone(world, bot);
        if (objective.Mode == GameModeKind.CaptureTheFlag)
        {
            if (bot.IsCarryingIntel)
            {
                bestCtfReturnDistance = MathF.Min(bestCtfReturnDistance, objectiveDistance);
            }
            else
            {
                bestCtfAttackDistance = MathF.Min(bestCtfAttackDistance, objectiveDistance);
            }
        }

        if (!firstCarryTick.HasValue && bot.IsCarryingIntel)
        {
            firstCarryTick = tick;
        }

        if (!firstObjectiveZoneTick.HasValue
            && IsBotInObjectiveZoneForScenario(world, bot, objective, scenario.Team, botInCaptureZone, objectiveDistance))
        {
            firstObjectiveZoneTick = tick;
        }

        if (!firstCapProgressTick.HasValue
            && HasTeamCaptureProgressStarted(world, objective.ControlPointIndex, scenario.Team, initialRedKothTicks, initialBlueKothTicks))
        {
            firstCapProgressTick = tick;
        }

        hasEverCaptureZoneRect |= botInCaptureZone;
        if (objectiveDistance + 16f < bestObjectiveDistance
            || botInCaptureZone
            || (hasDiagnostic && diagnostic.RouteLabel.EndsWith("capture_zone_hold", StringComparison.Ordinal)))
        {
            bestObjectiveDistance = botInCaptureZone ? 0f : Math.Min(bestObjectiveDistance, objectiveDistance);
            ticksSinceBestObjectiveProgress = 0;
        }
        else
        {
            ticksSinceBestObjectiveProgress += 1;
            longestNoProgressTicks = Math.Max(longestNoProgressTicks, ticksSinceBestObjectiveProgress);
        }

        if (!objectiveAreaEntryTick.HasValue
            && IsBotInObjectiveAreaForScenario(objective.Mode, objectiveDistance, botInCaptureZone))
        {
            objectiveAreaEntryTick = tick;
            if (hasDiagnostic)
            {
                objectiveAreaEntryRouteLabel = diagnostic.RouteLabel;
                objectiveAreaEntryRouteGoal = $"{diagnostic.RouteGoalNodeId}@({diagnostic.RouteGoalX:0.0},{diagnostic.RouteGoalY:0.0})";
            }
        }

        if (hasDiagnostic
            && !captureHoldSeen
            && diagnostic.RouteLabel.Contains("capture_zone_hold", StringComparison.Ordinal))
        {
            captureHoldSeen = true;
        }

        maxDistanceFromSpawn = MathF.Max(maxDistanceFromSpawn, DistanceBetween(bot.X, bot.Y, startX, startY));
        if (hasDiagnostic)
        {
            maxStuckTicks = Math.Max(maxStuckTicks, diagnostic.StuckTicks);
            routeLabelCounts.TryGetValue(diagnostic.RouteLabel, out var routeLabelCount);
            routeLabelCounts[diagnostic.RouteLabel] = routeLabelCount + 1;
        }
        if (hasDiagnostic && tick % Math.Max(1, world.Config.TicksPerSecond / 30) == 0)
        {
            var combatTrace = scenario.Kind == BotScenarioKind.Combat && combatEnemy is not null
                ? $" fire=P{input.FirePrimary}/S{input.FireSecondary} aim=({input.AimWorldX:0.0},{input.AimWorldY:0.0}) enemy=({combatEnemy.X:0.0},{combatEnemy.Y:0.0}) ehp={combatEnemy.Health}/{combatEnemy.MaxHealth} shells={bot.CurrentShells} cd={bot.PrimaryCooldownTicks} reload={bot.ReloadTicksUntilNextShell}"
                : string.Empty;
            routeTrace!.Add(
                $"t={tick / (float)world.Config.TicksPerSecond:0.0} pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0} vel=({bot.HorizontalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.0},{bot.VerticalSpeed / LegacyMovementModel.SourceTicksPerSecond:0.0}) target=({diagnostic.MovementTargetX:0.0},{diagnostic.MovementTargetY:0.0}) input=L{input.Left}/R{input.Right}/J{input.Up}{combatTrace} req={diagnostic.RequestedHorizontal}/{diagnostic.RequestedJump} move={diagnostic.MoveDebug} jump={diagnostic.JumpDebug} g={diagnostic.IsGrounded}/{diagnostic.ProbeGrounded} st={diagnostic.StuckTicks}/{diagnostic.ModernStuckTicks} d={objectiveDistance:0.0} route={diagnostic.RouteLabel} cp={diagnostic.CurrentPointId} np={diagnostic.NextPointId} np2={diagnostic.NextPoint2Id} prev={diagnostic.PreviousCurrentPointId}->{diagnostic.PreviousNextPointId} goal={diagnostic.RouteGoalNodeId}@({diagnostic.RouteGoalX:0.0},{diagnostic.RouteGoalY:0.0}) nn={diagnostic.NoNextPointTicks} sab={diagnostic.SecondAnchorBlockPointId}/{diagnostic.SecondAnchorBlockTicksRemaining} issue={diagnostic.NavigationIssueLabel} br={diagnostic.BranchFromPointId}->{diagnostic.BranchToPointId}:{diagnostic.BranchTicks}/{diagnostic.BranchNoProgressTicks} dt={diagnostic.DirectTargetTicks}/{diagnostic.DirectTargetNoProgressTicks}");
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

            var scorePhase = DescribeScorePhase(
                scenario,
                world,
                bot,
                objective,
                completedObjective,
                botInCaptureZone,
                objectiveDistance,
                initialRedCaps,
                initialBlueCaps,
                initialRedKothTicks,
                initialBlueKothTicks,
                scenario.Team);
            if (!string.IsNullOrEmpty(scorePhase)
                && !string.Equals(scorePhase, lastScorePhase, StringComparison.Ordinal))
            {
                objectiveTrace!.Add(
                    $"t={timeSeconds:0.0} phase={scorePhase} d={objectiveDistance:0.0} carry={bot.IsCarryingIntel} caps={world.RedCaps}-{world.BlueCaps} koth={world.KothRedTimerTicksRemaining}-{world.KothBlueTimerTicksRemaining}");
                lastScorePhase = scorePhase;
            }

            if (hasDiagnostic)
            {
                var routeGoalKey = $"{diagnostic.RouteGoalNodeId}@{diagnostic.RouteGoalX:0.0},{diagnostic.RouteGoalY:0.0}";
                if (!string.Equals(routeGoalKey, lastRouteGoalKey, StringComparison.Ordinal))
                {
                    objectiveTrace!.Add(
                        $"t={timeSeconds:0.0} routeGoal={routeGoalKey} target=({diagnostic.MovementTargetX:0.0},{diagnostic.MovementTargetY:0.0}) route={diagnostic.RouteLabel} cp={diagnostic.CurrentPointId} np={diagnostic.NextPointId} nn={diagnostic.NoNextPointTicks} issue={diagnostic.NavigationIssueLabel}");
                    lastRouteGoalKey = routeGoalKey;
                }

                var navigationIssueKey = string.IsNullOrWhiteSpace(diagnostic.NavigationIssueLabel)
                    ? "clear"
                    : diagnostic.NavigationIssueLabel;
                if (!string.Equals(navigationIssueKey, lastNavigationIssueKey, StringComparison.Ordinal))
                {
                    objectiveTrace!.Add(
                        $"t={timeSeconds:0.0} navIssue={navigationIssueKey} branch={diagnostic.BranchFromPointId}->{diagnostic.BranchToPointId} br={diagnostic.BranchTicks}/{diagnostic.BranchNoProgressTicks} dt={diagnostic.DirectTargetTicks}/{diagnostic.DirectTargetNoProgressTicks} route={diagnostic.RouteLabel}");
                    lastNavigationIssueKey = navigationIssueKey;
                }
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

            if (objectiveTrace!.Count > 80)
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

        if (earlyNoProgressStopTicks > 0
            && ticksSinceBestObjectiveProgress >= earlyNoProgressStopTicks)
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
                firstObjectiveZoneTick,
                firstCarryTick,
                firstCapProgressTick,
                hasEverCaptureZoneRect,
                captureHoldSeen,
                collectDiagnostics,
                objectiveAreaEntryTick,
                objectiveAreaEntryRouteLabel,
                objectiveAreaEntryRouteGoal,
                bestCtfAttackDistance,
                bestCtfReturnDistance,
                failureReason: $"early_no_progress_{ticksSinceBestObjectiveProgress / (float)world.Config.TicksPerSecond:0.0}s",
                timedOut: false);
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
            BotScenarioKind.Combat => combatEnemy is null
                ? "combat_enemy_missing"
                : $"combat_enemy_alive_health={combatEnemy.Health}_deaths={combatEnemy.Deaths - initialCombatEnemyDeaths}_botKills={bot.Kills - initialBotKills}_best={bestObjectiveDistance:0.0}",
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
        firstObjectiveZoneTick,
        firstCarryTick,
        firstCapProgressTick,
        hasEverCaptureZoneRect,
        captureHoldSeen,
        collectDiagnostics,
        objectiveAreaEntryTick,
        objectiveAreaEntryRouteLabel,
        objectiveAreaEntryRouteGoal,
        bestCtfAttackDistance,
        bestCtfReturnDistance,
        failureReason,
        timedOut: false);
}

static IPracticeBotController CreateScenarioController(BotControllerMode mode)
{
    return mode switch
    {
        BotControllerMode.ObjectiveTraversal => new ObjectiveTraversalBotController(),
        BotControllerMode.MotionProof => new MotionProofPracticeBotController(),
        _ => new ModernPracticeBotController(),
    };
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
    int? firstObjectiveZoneTick,
    int? firstCarryTick,
    int? firstCapProgressTick,
    bool hasEverCaptureZoneRect,
    bool captureHoldSeen,
    bool captureHoldKnown,
    int? objectiveAreaEntryTick,
    string objectiveAreaEntryRouteLabel,
    string objectiveAreaEntryRouteGoal,
    float bestCtfAttackDistance,
    float bestCtfReturnDistance,
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
    var objectiveSummary = BuildScenarioObjectiveSummary(
        scenario,
        world,
        firstObjectiveZoneTick,
        firstCarryTick,
        firstCapProgressTick,
        hasEverCaptureZoneRect,
        captureHoldSeen,
        captureHoldKnown,
        objectiveAreaEntryTick,
        objectiveAreaEntryRouteLabel,
        objectiveAreaEntryRouteGoal,
        bestCtfAttackDistance,
        bestCtfReturnDistance);

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
        ObjectiveSummary: objectiveSummary,
        FailureReason: failureReason ?? string.Empty);
}

static string BuildScenarioObjectiveSummary(
    BotScenarioCase scenario,
    SimulationWorld world,
    int? firstObjectiveZoneTick,
    int? firstCarryTick,
    int? firstCapProgressTick,
    bool hasEverCaptureZoneRect,
    bool captureHoldSeen,
    bool captureHoldKnown,
    int? objectiveAreaEntryTick,
    string objectiveAreaEntryRouteLabel,
    string objectiveAreaEntryRouteGoal,
    float bestCtfAttackDistance,
    float bestCtfReturnDistance)
{
    var ticksPerSecond = Math.Max(1, world.Config.TicksPerSecond);
    var ctfMode = world.MatchRules.Mode == GameModeKind.CaptureTheFlag;
    var ctfAttackBest = ctfMode && !float.IsPositiveInfinity(bestCtfAttackDistance)
        ? bestCtfAttackDistance.ToString("0.0", CultureInfo.InvariantCulture)
        : "n/a";
    var ctfReturnBest = ctfMode && !float.IsPositiveInfinity(bestCtfReturnDistance)
        ? bestCtfReturnDistance.ToString("0.0", CultureInfo.InvariantCulture)
        : "n/a";
    var capHold = captureHoldKnown
        ? (captureHoldSeen ? "true" : "false")
        : "n/a";
    var entryRoute = string.IsNullOrWhiteSpace(objectiveAreaEntryRouteLabel)
        ? "n/a"
        : CompactScoreRouteLogValue(objectiveAreaEntryRouteLabel, 60);
    var entryGoal = string.IsNullOrWhiteSpace(objectiveAreaEntryRouteGoal)
        ? "n/a"
        : CompactScoreRouteLogValue(objectiveAreaEntryRouteGoal, 80);

    return
        $"zoneFirst={FormatTickSeconds(firstObjectiveZoneTick, ticksPerSecond)};" +
        $"carryFirst={FormatTickSeconds(firstCarryTick, ticksPerSecond)};" +
        $"capProgFirst={FormatTickSeconds(firstCapProgressTick, ticksPerSecond)};" +
        $"capRectEver={hasEverCaptureZoneRect};" +
        $"capHold={capHold};" +
        $"entry={FormatTickSeconds(objectiveAreaEntryTick, ticksPerSecond)};" +
        $"entryRoute={entryRoute};" +
        $"entryGoal={entryGoal};" +
        $"ctfAttackBest={ctfAttackBest};" +
        $"ctfReturnBest={ctfReturnBest}";
}

static string FormatTickSeconds(int? tick, int ticksPerSecond)
{
    if (!tick.HasValue || tick.Value < 0 || ticksPerSecond <= 0)
    {
        return "n/a";
    }

    return (tick.Value / (float)ticksPerSecond).ToString("0.0s", CultureInfo.InvariantCulture);
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
    var prepared = scenario.Kind switch
    {
        BotScenarioKind.Score or BotScenarioKind.Route or BotScenarioKind.Combat => true,
        BotScenarioKind.Arrival => TryPrepareArrivalScenario(world, bot, scenario.Team, out failureReason),
        BotScenarioKind.ReturnCap => TryPrepareReturnCapScenario(world, bot, scenario.Team, out failureReason),
        _ => false,
    };
    if (!prepared)
    {
        return false;
    }

    if (scenario.StartFeetX.HasValue || scenario.StartFeetY.HasValue)
    {
        if (!scenario.StartFeetX.HasValue || !scenario.StartFeetY.HasValue)
        {
            failureReason = "scenario_start_feet_requires_x_and_y";
            return false;
        }

        if (!TrySpawnPlayerResolvedForScenario(
                world,
                bot,
                scenario.Team,
                scenario.StartFeetX.Value,
                scenario.StartFeetY.Value - bot.CollisionBottomOffset))
        {
            failureReason = "scenario_start_feet_spawn_failed";
            return false;
        }
    }

    return true;
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

static bool TryPrepareCombatEnemy(
    SimulationWorld world,
    BotScenarioCase scenario,
    out PlayerEntity enemy,
    out string? failureReason)
{
    const byte enemySlot = 3;
    enemy = default!;
    failureReason = null;
    var enemyTeam = GetOpposingTeamForScenario(scenario.Team);
    if (!world.TryPrepareNetworkPlayerJoin(enemySlot)
        || !world.TrySetNetworkPlayerTeam(enemySlot, enemyTeam)
        || !world.TryApplyNetworkPlayerClassSelection(enemySlot, scenario.EnemyClassId)
        || !world.TryGetNetworkPlayer(enemySlot, out enemy))
    {
        failureReason = "failed_to_spawn_combat_enemy";
        return false;
    }

    if (!TryResolveCombatEnemyFeet(scenario, out var feetX, out var feetY))
    {
        failureReason = "combat_enemy_feet_required";
        return false;
    }

    if (!TrySpawnPlayerResolvedForScenario(
            world,
            enemy,
            enemyTeam,
            feetX,
            feetY - enemy.CollisionBottomOffset))
    {
        failureReason = "combat_enemy_position_failed";
        return false;
    }

    return true;
}

static bool TryResolveCombatEnemyFeet(BotScenarioCase scenario, out float feetX, out float feetY)
{
    if (scenario.EnemyFeetX.HasValue && scenario.EnemyFeetY.HasValue)
    {
        feetX = scenario.EnemyFeetX.Value;
        feetY = scenario.EnemyFeetY.Value;
        return true;
    }

    var envX = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_X");
    var envY = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_Y");
    if (float.TryParse(envX, NumberStyles.Float, CultureInfo.InvariantCulture, out feetX)
        && float.TryParse(envY, NumberStyles.Float, CultureInfo.InvariantCulture, out feetY))
    {
        return true;
    }

    feetX = 0f;
    feetY = 0f;
    return false;
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
        BotScenarioKind.Combat => completedObjective,
        _ => false,
    };
}

static string DescribeScorePhase(
    BotScenarioCase scenario,
    SimulationWorld world,
    PlayerEntity bot,
    BotObjectiveProbe objective,
    bool completedObjective,
    bool botInCaptureZone,
    float objectiveDistance,
    int initialRedCaps,
    int initialBlueCaps,
    int initialRedKothTicks,
    int initialBlueKothTicks,
    PlayerTeam team)
{
    if (scenario.Kind != BotScenarioKind.Score)
    {
        return string.Empty;
    }

    if (completedObjective)
    {
        return "score_complete";
    }

    if (objective.Mode == GameModeKind.CaptureTheFlag)
    {
        if (bot.IsCarryingIntel)
        {
            return objectiveDistance <= MathF.Max(80f, GetArrivalDistance(objective.Mode) + 24f)
                ? "ctf_pre_score"
                : "ctf_return";
        }

        return "ctf_attack";
    }

    if (objective.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
    {
        var hasTimerProgress = team == PlayerTeam.Blue
            ? world.KothBlueTimerTicksRemaining < initialBlueKothTicks
            : world.KothRedTimerTicksRemaining < initialRedKothTicks;
        if (hasTimerProgress)
        {
            return botInCaptureZone ? "koth_progress_hold" : "koth_progress";
        }

        return botInCaptureZone ? "koth_capture_zone" : "koth_approach";
    }

    if (objective.ControlPointIndex >= 0)
    {
        var point = world.ControlPoints.FirstOrDefault(candidate => candidate.Index == objective.ControlPointIndex);
        if (point is not null && point.Team == team)
        {
            return "cp_owned";
        }
    }

    return botInCaptureZone ? "cp_capture_zone" : "cp_approach";
}

static bool IsBotInObjectiveZoneForScenario(
    SimulationWorld world,
    PlayerEntity bot,
    BotObjectiveProbe objective,
    PlayerTeam team,
    bool botInCaptureZone,
    float objectiveDistance)
{
    _ = team;
    if (objective.Mode == GameModeKind.CaptureTheFlag)
    {
        return objectiveDistance <= GetArrivalDistance(world.MatchRules.Mode);
    }

    return botInCaptureZone;
}

static bool IsBotInObjectiveAreaForScenario(
    GameModeKind mode,
    float objectiveDistance,
    bool botInCaptureZone)
{
    if (mode == GameModeKind.CaptureTheFlag)
    {
        return objectiveDistance <= MathF.Max(96f, GetArrivalDistance(mode) + 24f);
    }

    return botInCaptureZone || objectiveDistance <= MathF.Max(120f, GetArrivalDistance(mode) + 36f);
}

static bool HasTeamCaptureProgressStarted(
    SimulationWorld world,
    int controlPointIndex,
    PlayerTeam team,
    int initialRedKothTicks,
    int initialBlueKothTicks)
{
    if (world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
    {
        return team == PlayerTeam.Blue
            ? world.KothBlueTimerTicksRemaining < initialBlueKothTicks
            : world.KothRedTimerTicksRemaining < initialRedKothTicks;
    }

    if (controlPointIndex < 0)
    {
        return false;
    }

    var point = world.ControlPoints.FirstOrDefault(candidate => candidate.Index == controlPointIndex);
    if (point is null)
    {
        return false;
    }

    if (point.Team == team)
    {
        return true;
    }

    if (point.CappingTeam != team)
    {
        return false;
    }

    return point.CappingTicks > 0f
        || (team == PlayerTeam.Blue ? point.BlueCappers : point.RedCappers) > 0;
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
    if (TryResolveMotionProofOverrideGoal(world, bot, out var overrideObjective))
    {
        return overrideObjective;
    }

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

static BotObjectiveProbe ResolveScenarioObjective(
    SimulationWorld world,
    PlayerEntity bot,
    PlayerTeam team,
    PlayerEntity? combatEnemy)
{
    if (combatEnemy is not null)
    {
        return new BotObjectiveProbe(world.MatchRules.Mode, combatEnemy.X, combatEnemy.Y, ControlPointIndex: -1);
    }

    return ResolveTeamObjective(world, bot, team);
}

static bool TryResolveMotionProofOverrideGoal(SimulationWorld world, PlayerEntity bot, out BotObjectiveProbe objective)
{
    var envX = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_X");
    var envY = Environment.GetEnvironmentVariable("OG_MOTION_PROOF_GOAL_Y");
    if (float.TryParse(envX, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
        && float.TryParse(envY, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
    {
        objective = new BotObjectiveProbe(world.MatchRules.Mode, x, y - bot.CollisionBottomOffset, ControlPointIndex: -1);
        return true;
    }

    objective = default;
    return false;
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

static long GetRouteEdgeKey(int fromNodeId, int toNodeId)
{
    return ((long)fromNodeId << 32) | (uint)toNodeId;
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

internal enum BotControllerMode
{
    ModernGraphRoute = 0,
    ObjectiveTraversal = 1,
    MotionProof = 2,
}

internal sealed class NavBuildOptions
{
    public const string Usage =
        "usage: dotnet run --project BotAI.Tools -- [--map MapName] [--output Path] [--include-custom] [--audit-reachability] [--audit-shipped] [--repair-shipped] [--audit-capture-routes] [--validate-modern-edges] [--build-score-routes] [--score-route-diagnostics] [--score-route-max-phase-routes N] [--score-route-max-proof-attempts N] [--score-route-context-budget-ms N] [--score-route-map-budget-ms N] [--score-route-no-progress-stop-seconds N] [--run-bot-scenario] [--bot-controller ModernGraphRoute|ObjectiveTraversal|MotionProof] [--stock-score-matrix] [--stock-ctf-koth-matrix] [--scenario-kind Score|Route|Arrival|ReturnCap|Combat] [--team Red|Blue] [--class Scout|Engineer|Pyro|Soldier|Demoman|Heavy|Sniper|Medic|Spy|Quote]... [--enemy-class Scout|Engineer|Pyro|Soldier|Demoman|Heavy|Sniper|Medic|Spy|Quote] [--enemy-feet-x X --enemy-feet-y Y] [--seconds N] [--wall-timeout-ms N] [--bot-perf-smoke] [--perf-bots N] [--perf-min-fps N] [--fail-fast] [--no-bot-diagnostics] [--probe-orange-lip] [--probe-orange-branch] [--probe-modern-edge --from-node N --to-node N [--start-x X --start-bottom Y]] [--probe-modern-chain --chain-nodes N,N,N [--start-x X --start-bottom Y]] [--probe-collision-window --start-x X --start-bottom Y] [--probe-gml-link --from-node N --to-node N]";

    public HashSet<string> MapNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? OutputDirectory { get; private set; }

    public bool IncludeCustomMaps { get; private set; }

    public bool AuditReachability { get; private set; }

    public bool AuditShipped { get; private set; }

    public bool RepairShipped { get; private set; }

    public bool AuditCaptureRoutes { get; private set; }

    public bool ValidateModernEdges { get; private set; }

    public bool BuildScoreRoutes { get; private set; }

    public bool ScoreRouteDiagnostics { get; private set; }

    public int ScoreRouteMaxPhaseRoutes { get; private set; }

    public int ScoreRouteMaxProofAttemptsPerContext { get; private set; }

    public int ScoreRouteContextBudgetMilliseconds { get; private set; }

    public int ScoreRouteMapBudgetMilliseconds { get; private set; }

    public int ScoreRouteNoProgressStopSeconds { get; private set; } = 45;

    public bool RunBotScenarioHarness { get; private set; }

    public bool RunBotPerfSmoke { get; private set; }

    public int BotPerfSmokeBotCount { get; private set; } = 12;

    public float BotPerfSmokeMinFps { get; private set; } = 60f;

    public BotControllerMode BotControllerMode { get; private set; } = BotControllerMode.ModernGraphRoute;

    public bool ProbeOrangeLip { get; private set; }

    public bool ProbeOrangeBranch { get; private set; }

    public bool ProbeModernEdge { get; private set; }

    public bool ProbeModernChain { get; private set; }

    public bool ProbeCollisionWindow { get; private set; }

    public bool ProbeGmlLink { get; private set; }

    public int ProbeFromNodeId { get; private set; } = -1;

    public int ProbeToNodeId { get; private set; } = -1;

    public IReadOnlyList<int> ProbeChainNodeIds => _probeChainNodeIds;

    public float? ProbeStartX { get; private set; }

    public float? ProbeStartBottom { get; private set; }

    public bool RunStockScoreMatrix { get; private set; }

    public bool RunStockCtfKothMatrix { get; private set; }

    public PlayerTeam ScenarioTeam { get; private set; } = PlayerTeam.Blue;

    public IReadOnlyList<PlayerClass> ScenarioClasses => _scenarioClasses;

    public int ScenarioSeconds { get; private set; } = 120;

    public int WallTimeoutMilliseconds { get; private set; } = 45000;

    public BotScenarioKind ScenarioKind { get; private set; } = BotScenarioKind.Score;

    public float? ScenarioStartFeetX { get; private set; }

    public float? ScenarioStartFeetY { get; private set; }

    public PlayerClass ScenarioEnemyClass { get; private set; } = PlayerClass.Scout;

    public float? ScenarioEnemyFeetX { get; private set; }

    public float? ScenarioEnemyFeetY { get; private set; }

    public bool CollectBotDiagnostics { get; private set; } = true;

    public bool FailFast { get; private set; }

    private readonly List<PlayerClass> _scenarioClasses = [PlayerClass.Scout];

    private readonly List<int> _probeChainNodeIds = [];

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

            if (arg.Equals("--validate-modern-edges", StringComparison.OrdinalIgnoreCase))
            {
                options.ValidateModernEdges = true;
                continue;
            }

            if (arg.Equals("--build-score-routes", StringComparison.OrdinalIgnoreCase))
            {
                options.BuildScoreRoutes = true;
                continue;
            }

            if (arg.Equals("--score-route-diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                options.ScoreRouteDiagnostics = true;
                continue;
            }

            if (arg.Equals("--score-route-max-phase-routes", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!int.TryParse(args[++index].Trim(), out var maxPhaseRoutes))
                {
                    throw new ArgumentException($"Invalid score-route-max-phase-routes value '{args[index]}'.");
                }

                options.ScoreRouteMaxPhaseRoutes = maxPhaseRoutes;
                continue;
            }

            if (arg.Equals("--score-route-max-proof-attempts", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!int.TryParse(args[++index].Trim(), out var maxProofAttempts))
                {
                    throw new ArgumentException($"Invalid score-route-max-proof-attempts value '{args[index]}'.");
                }

                options.ScoreRouteMaxProofAttemptsPerContext = maxProofAttempts;
                continue;
            }

            if (arg.Equals("--score-route-context-budget-ms", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!int.TryParse(args[++index].Trim(), out var contextBudgetMs))
                {
                    throw new ArgumentException($"Invalid score-route-context-budget-ms value '{args[index]}'.");
                }

                options.ScoreRouteContextBudgetMilliseconds = contextBudgetMs;
                continue;
            }

            if (arg.Equals("--score-route-map-budget-ms", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!int.TryParse(args[++index].Trim(), out var mapBudgetMs))
                {
                    throw new ArgumentException($"Invalid score-route-map-budget-ms value '{args[index]}'.");
                }

                options.ScoreRouteMapBudgetMilliseconds = mapBudgetMs;
                continue;
            }

            if (arg.Equals("--score-route-no-progress-stop-seconds", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!int.TryParse(args[++index].Trim(), out var noProgressStopSeconds))
                {
                    throw new ArgumentException($"Invalid score-route-no-progress-stop-seconds value '{args[index]}'.");
                }

                options.ScoreRouteNoProgressStopSeconds = noProgressStopSeconds;
                continue;
            }

            if (arg.Equals("--run-bot-scenario", StringComparison.OrdinalIgnoreCase))
            {
                options.RunBotScenarioHarness = true;
                continue;
            }

            if (arg.Equals("--bot-perf-smoke", StringComparison.OrdinalIgnoreCase))
            {
                options.RunBotPerfSmoke = true;
                continue;
            }

            if (arg.Equals("--perf-bots", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!int.TryParse(args[++index].Trim(), out var perfBots))
                {
                    throw new ArgumentException($"Invalid perf-bots value '{args[index]}'.");
                }

                options.BotPerfSmokeBotCount = perfBots;
                continue;
            }

            if (arg.Equals("--perf-min-fps", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!float.TryParse(args[++index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var perfMinFps))
                {
                    throw new ArgumentException($"Invalid perf-min-fps value '{args[index]}'.");
                }

                options.BotPerfSmokeMinFps = perfMinFps;
                continue;
            }

            if (arg.Equals("--bot-controller", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!Enum.TryParse<BotControllerMode>(args[++index].Trim(), ignoreCase: true, out var botControllerMode))
                {
                    throw new ArgumentException($"Invalid bot-controller value '{args[index]}'.");
                }

                options.BotControllerMode = botControllerMode;
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

            if (arg.Equals("--probe-modern-edge", StringComparison.OrdinalIgnoreCase))
            {
                options.ProbeModernEdge = true;
                continue;
            }

            if (arg.Equals("--probe-modern-chain", StringComparison.OrdinalIgnoreCase))
            {
                options.ProbeModernChain = true;
                continue;
            }

            if (arg.Equals("--probe-collision-window", StringComparison.OrdinalIgnoreCase))
            {
                options.ProbeCollisionWindow = true;
                continue;
            }

            if (arg.Equals("--probe-gml-link", StringComparison.OrdinalIgnoreCase))
            {
                options.ProbeGmlLink = true;
                continue;
            }

            if (arg.Equals("--from-node", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!int.TryParse(args[++index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromNodeId))
                {
                    throw new ArgumentException($"Invalid from-node value '{args[index]}'.");
                }

                options.ProbeFromNodeId = fromNodeId;
                continue;
            }

            if (arg.Equals("--to-node", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!int.TryParse(args[++index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var toNodeId))
                {
                    throw new ArgumentException($"Invalid to-node value '{args[index]}'.");
                }

                options.ProbeToNodeId = toNodeId;
                continue;
            }

            if (arg.Equals("--chain-nodes", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options._probeChainNodeIds.Clear();
                foreach (var token in args[++index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nodeId))
                    {
                        throw new ArgumentException($"Invalid chain node value '{token}'.");
                    }

                    options._probeChainNodeIds.Add(nodeId);
                }

                continue;
            }

            if (arg.Equals("--start-x", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!float.TryParse(args[++index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var startX))
                {
                    throw new ArgumentException($"Invalid start-x value '{args[index]}'.");
                }

                options.ProbeStartX = startX;
                continue;
            }

            if (arg.Equals("--start-bottom", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!float.TryParse(args[++index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var startBottom))
                {
                    throw new ArgumentException($"Invalid start-bottom value '{args[index]}'.");
                }

                options.ProbeStartBottom = startBottom;
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

            if (arg.Equals("--start-feet-x", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!float.TryParse(args[++index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var startFeetX))
                {
                    throw new ArgumentException($"Invalid start-feet-x value '{args[index]}'.");
                }

                options.ScenarioStartFeetX = startFeetX;
                continue;
            }

            if (arg.Equals("--start-feet-y", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!float.TryParse(args[++index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var startFeetY))
                {
                    throw new ArgumentException($"Invalid start-feet-y value '{args[index]}'.");
                }

                options.ScenarioStartFeetY = startFeetY;
                continue;
            }

            if (arg.Equals("--enemy-class", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!Enum.TryParse<PlayerClass>(args[++index].Trim(), ignoreCase: true, out var enemyClass))
                {
                    throw new ArgumentException($"Unknown enemy class '{args[index]}'.");
                }

                options.ScenarioEnemyClass = enemyClass;
                continue;
            }

            if (arg.Equals("--enemy-feet-x", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!float.TryParse(args[++index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var enemyFeetX))
                {
                    throw new ArgumentException($"Invalid enemy-feet-x value '{args[index]}'.");
                }

                options.ScenarioEnemyFeetX = enemyFeetX;
                continue;
            }

            if (arg.Equals("--enemy-feet-y", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                if (!float.TryParse(args[++index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var enemyFeetY))
                {
                    throw new ArgumentException($"Invalid enemy-feet-y value '{args[index]}'.");
                }

                options.ScenarioEnemyFeetY = enemyFeetY;
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
        if (ProbeModernEdge)
        {
            if (MapNames.Count != 1)
            {
                message = "--probe-modern-edge requires exactly one --map.";
                return false;
            }

            if (ProbeFromNodeId < 0 || ProbeToNodeId < 0)
            {
                message = "--probe-modern-edge requires --from-node and --to-node.";
                return false;
            }

            if (GetEffectiveScenarioSeconds() <= 0)
            {
                message = "--seconds must be greater than zero.";
                return false;
            }
        }

        if (ProbeModernChain)
        {
            if (MapNames.Count != 1)
            {
                message = "--probe-modern-chain requires exactly one --map.";
                return false;
            }

            if (_probeChainNodeIds.Count < 2)
            {
                message = "--probe-modern-chain requires --chain-nodes with at least two node ids.";
                return false;
            }

            if (GetEffectiveScenarioSeconds() <= 0)
            {
                message = "--seconds must be greater than zero.";
                return false;
            }
        }

        if (ProbeCollisionWindow)
        {
            if (MapNames.Count != 1)
            {
                message = "--probe-collision-window requires exactly one --map.";
                return false;
            }

            if (!ProbeStartX.HasValue || !ProbeStartBottom.HasValue)
            {
                message = "--probe-collision-window requires --start-x and --start-bottom.";
                return false;
            }
        }

        if (ProbeGmlLink)
        {
            if (MapNames.Count != 1)
            {
                message = "--probe-gml-link requires exactly one --map.";
                return false;
            }

            if (ProbeFromNodeId < 0 || ProbeToNodeId < 0)
            {
                message = "--probe-gml-link requires --from-node and --to-node.";
                return false;
            }
        }

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

            if (ScenarioStartFeetX.HasValue != ScenarioStartFeetY.HasValue)
            {
                message = "Scenario start override requires both --start-feet-x and --start-feet-y.";
                return false;
            }

            if (ScenarioKind == BotScenarioKind.Combat
                && ScenarioEnemyFeetX.HasValue != ScenarioEnemyFeetY.HasValue)
            {
                message = "Combat scenario enemy spawn requires both --enemy-feet-x and --enemy-feet-y.";
                return false;
            }
        }

        if (RunBotPerfSmoke)
        {
            if (MapNames.Count > 1)
            {
                message = "Bot perf smoke accepts at most one --map.";
                return false;
            }

            if (GetEffectiveBotPerfSmokeSeconds() <= 0)
            {
                message = "--seconds must be greater than zero.";
                return false;
            }

            if (GetEffectiveBotPerfSmokeBotCount() <= 1)
            {
                message = "--perf-bots must be greater than one.";
                return false;
            }

            if (GetEffectiveBotPerfSmokeBotCount() > SimulationWorld.MaxPlayableNetworkPlayers - 1)
            {
                message = $"--perf-bots cannot exceed {SimulationWorld.MaxPlayableNetworkPlayers - 1}; slot {SimulationWorld.LocalPlayerSlot} is reserved for the local player.";
                return false;
            }

            if (GetEffectiveBotPerfSmokeMinFps() <= 0f)
            {
                message = "--perf-min-fps must be greater than zero.";
                return false;
            }
        }

        if (BuildScoreRoutes && GetEffectiveWallTimeoutMilliseconds() <= 0)
        {
            message = "--wall-timeout-ms must be greater than zero.";
            return false;
        }

        if (BuildScoreRoutes)
        {
            if (GetEffectiveScoreRouteProofSeconds() <= 0)
            {
                message = "--seconds must be greater than zero.";
                return false;
            }

            if (GetEffectiveScoreRouteProofWallTimeoutMilliseconds() <= 0)
            {
                message = "--wall-timeout-ms must be greater than zero.";
                return false;
            }

            if (GetEffectiveScoreRouteMaxPhaseRoutes() <= 0)
            {
                message = "--score-route-max-phase-routes must be greater than zero.";
                return false;
            }

            if (ScoreRouteMaxProofAttemptsPerContext < 0)
            {
                message = "--score-route-max-proof-attempts must be zero or greater.";
                return false;
            }

            if (ScoreRouteContextBudgetMilliseconds < 0)
            {
                message = "--score-route-context-budget-ms must be zero or greater.";
                return false;
            }

            if (ScoreRouteMapBudgetMilliseconds < 0)
            {
                message = "--score-route-map-budget-ms must be zero or greater.";
                return false;
            }

            if (ScoreRouteNoProgressStopSeconds < 0)
            {
                message = "--score-route-no-progress-stop-seconds must be zero or greater.";
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

        if (RunBotPerfSmoke && !_hasExplicitScenarioClasses)
        {
            return
            [
                PlayerClass.Scout,
                PlayerClass.Heavy,
                PlayerClass.Pyro,
                PlayerClass.Soldier,
                PlayerClass.Demoman,
                PlayerClass.Medic,
            ];
        }

        return _scenarioClasses;
    }

    public string GetSingleMapNameOrDefault(string defaultMapName)
    {
        return MapNames.Count == 0 ? defaultMapName : MapNames.First();
    }

    public int GetEffectiveBotPerfSmokeSeconds()
    {
        return _hasExplicitScenarioSeconds ? ScenarioSeconds : 30;
    }

    public int GetEffectiveBotPerfSmokeBotCount()
    {
        return BotPerfSmokeBotCount;
    }

    public float GetEffectiveBotPerfSmokeMinFps()
    {
        return BotPerfSmokeMinFps;
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

    public int GetEffectiveScoreRouteMaxPhaseRoutes()
    {
        if (ScoreRouteMaxPhaseRoutes > 0)
        {
            return ScoreRouteMaxPhaseRoutes;
        }

        return ScoreRouteDiagnostics ? 2 : 4;
    }

    public int GetEffectiveScoreRouteMaxProofAttemptsPerContext()
    {
        if (ScoreRouteMaxProofAttemptsPerContext > 0)
        {
            return ScoreRouteMaxProofAttemptsPerContext;
        }

        return ScoreRouteDiagnostics ? 4 : 0;
    }

    public int GetEffectiveScoreRouteContextBudgetMilliseconds()
    {
        if (ScoreRouteContextBudgetMilliseconds > 0)
        {
            return ScoreRouteContextBudgetMilliseconds;
        }

        return ScoreRouteDiagnostics ? 60_000 : 0;
    }

    public int GetEffectiveScoreRouteMapBudgetMilliseconds()
    {
        if (ScoreRouteMapBudgetMilliseconds > 0)
        {
            return ScoreRouteMapBudgetMilliseconds;
        }

        return ScoreRouteDiagnostics ? 180_000 : 0;
    }

    public int GetEffectiveScoreRouteProofSeconds()
    {
        if (_hasExplicitScenarioSeconds)
        {
            return ScenarioSeconds;
        }

        return ScoreRouteDiagnostics ? 60 : GetEffectiveScenarioSeconds();
    }

    public int GetEffectiveScoreRouteProofWallTimeoutMilliseconds()
    {
        if (_hasExplicitWallTimeoutMilliseconds)
        {
            return WallTimeoutMilliseconds;
        }

        return ScoreRouteDiagnostics ? 20_000 : GetEffectiveWallTimeoutMilliseconds();
    }

    public int GetEffectiveScoreRouteNoProgressStopSeconds()
    {
        return ScoreRouteNoProgressStopSeconds;
    }
}

internal enum BotScenarioKind
{
    Score = 0,
    Route = 1,
    Arrival = 2,
    ReturnCap = 3,
    Combat = 4,
}

internal enum ModernEdgeProbePolicy
{
    HorizontalOnly,
    JumpWhenGrounded,
    JumpWhenBlockedOrAbove,
}

internal enum ModernChainProbePolicy
{
    TrackTargetJumpWhenGrounded,
    TrackTargetNextDirectionGate,
    HoldEdgeJumpWhenGrounded,
    HoldEdgeJumpWhenBlockedOrAbove,
    HoldEdgeSingleJumpPerSegment,
}

internal readonly record struct BotScenarioCase(
    string LevelName,
    PlayerTeam Team,
    PlayerClass ClassId,
    int SimulationSeconds,
    BotScenarioKind Kind,
    int MapAreaIndex = 1,
    float? StartFeetX = null,
    float? StartFeetY = null,
    PlayerClass EnemyClassId = PlayerClass.Scout,
    float? EnemyFeetX = null,
    float? EnemyFeetY = null);

internal readonly record struct ScoreRouteProfilePlan(
    BotNavigationProfile Profile,
    PlayerClass ClassId);

internal readonly record struct ScoreRouteCandidateBuildResult(
    IReadOnlyList<BotNavigationScoreRouteEntry> Entries,
    int StartNodeCount,
    int RawRouteCount,
    int BuildFailures);

internal readonly record struct ScoreRouteTraversalProofResult(
    IReadOnlyDictionary<long, BotNavigationScoreRouteSegment> ProvenSegmentsByKey,
    int ProvenEdgeCount,
    int ProvenJumpEdgeCount,
    int TotalJumpEdgeCount);

internal readonly record struct BotObjectiveProbe(GameModeKind Mode, float X, float Y, int ControlPointIndex);

internal readonly record struct ModernPromotionTrace(
    string Label,
    float CurrentPoint,
    float NextPoint,
    float LookaheadPoint,
    float Feet,
    float LiveRise,
    float GraphRise,
    float Run,
    float CanUse);

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
    string ObjectiveSummary,
    string FailureReason);
