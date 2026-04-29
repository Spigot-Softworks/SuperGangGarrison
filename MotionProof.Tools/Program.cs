using OpenGarrison.Core;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

var options = MotionProofOptions.Parse(args);
var sourceContentRoot = ProjectSourceLocator.FindDirectory("Core/Content") ?? ContentRoot.Path;
ContentRoot.Initialize(sourceContentRoot);

if (options.ShowHelp)
{
    Console.WriteLine(MotionProofOptions.Usage);
    return 0;
}

return MotionProofRunner.Run(options);

internal static class MotionProofRunner
{
    private const float IntelMarkerSize = 24f;
    private const float ObjectiveAreaSize = 128f;
    private const float MinimumPrimitiveProgressDistance = 3f;
    private const int BotSlot = 2;
    private const int EnemySlot = 3;
    private static readonly double TickSeconds = 1d / LegacyMovementModel.SourceTicksPerSecond;
    private static readonly JsonSerializerOptions ArtifactJsonOptions = new() { WriteIndented = true };

    public static int Run(MotionProofOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var world = CreateWorld(options, out var bot, out var failureReason);
        if (world is null)
        {
            Console.Error.WriteLine(failureReason);
            return 2;
        }

        var initialRedCaps = world.RedCaps;
        var initialBlueCaps = world.BlueCaps;
        Console.WriteLine(
            $"motion-proof start map={world.Level.Name} area={world.Level.MapAreaIndex} mode={world.MatchRules.Mode} team={options.Team} class={options.ClassId} pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0} deps=CoreOnly");

        if (!string.IsNullOrWhiteSpace(options.MirrorArtifactPath))
        {
            return RunMirroredArtifactProof(world, bot, options, stopwatch, initialRedCaps, initialBlueCaps);
        }

        if (options.BakeGraph)
        {
            return RunGraphBake(world, bot, options, stopwatch);
        }

        if (options.CombatSmoke)
        {
            return RunCombatSmoke(options, stopwatch);
        }

        if (options.ProveGraph)
        {
            return RunGraphProof(world, bot, options, stopwatch);
        }

        if (IsKothMode(world.MatchRules.Mode))
        {
            return RunKothProof(world, bot, options, stopwatch);
        }

        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag)
        {
            Console.Error.WriteLine($"mode_not_supported:{world.MatchRules.Mode}");
            return 2;
        }

        var enemyBase = world.Level.GetIntelBase(GetOpposingTeam(options.Team));
        var ownBase = world.Level.GetIntelBase(options.Team);
        if (!enemyBase.HasValue || !ownBase.HasValue)
        {
            Console.Error.WriteLine("missing_intel_base");
            return 2;
        }

        var attackStart = MotionState.FromPlayer(bot);
        var attackGoal = new MotionGoal(enemyBase.Value.X, enemyBase.Value.Y, IntelMarkerSize, IntelMarkerSize, "enemy_intel");
        if (!TryFindPrimitivePath(
                world.Level,
                options.Team,
                options.ClassId,
                carryingIntel: false,
                attackStart,
                attackGoal,
                options,
                searchBudgetMillisecondsOverride: null,
                out var attackPath,
                out var attackStats))
        {
            Console.WriteLine($"motion-proof attack status=fail {attackStats}");
            return 3;
        }

        Console.WriteLine($"motion-proof attack status=found actions={attackPath.Actions.Count} ticks={attackPath.TotalTicks} {attackStats}");
        Console.WriteLine($"motion-proof attack tape={FormatActionSequence(attackPath)}");
        ReplayPath(world, bot, options.Team, attackPath, "attack");
        if (!bot.IsCarryingIntel)
        {
            Console.WriteLine(
                $"motion-proof replay status=fail phase=attack reason=no_intel pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
            return 3;
        }

        var returnStart = MotionState.FromPlayer(bot);
        var returnGoal = new MotionGoal(ownBase.Value.X, ownBase.Value.Y, IntelMarkerSize, IntelMarkerSize, "own_base");
        if (!TryFindPrimitivePath(
                world.Level,
                options.Team,
                options.ClassId,
                carryingIntel: true,
                returnStart,
                returnGoal,
                options,
                searchBudgetMillisecondsOverride: null,
                out var returnPath,
                out var returnStats))
        {
            Console.WriteLine($"motion-proof return status=fail {returnStats}");
            return 3;
        }

        Console.WriteLine($"motion-proof return status=found actions={returnPath.Actions.Count} ticks={returnPath.TotalTicks} {returnStats}");
        Console.WriteLine($"motion-proof return tape={FormatActionSequence(returnPath)}");
        ReplayPath(world, bot, options.Team, returnPath, "return");
        var completed = options.Team == PlayerTeam.Blue
            ? world.BlueCaps > initialBlueCaps
            : world.RedCaps > initialRedCaps;
        Console.WriteLine(
            $"motion-proof result status={(completed ? "pass" : "fail")} caps={world.RedCaps}-{world.BlueCaps} carrying={bot.IsCarryingIntel} elapsedMs={stopwatch.ElapsedMilliseconds} final=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
        if (completed && !string.IsNullOrWhiteSpace(options.OutputPath))
        {
            WriteProofArtifact(options.OutputPath, world, options, attackPath, returnPath);
            Console.WriteLine($"motion-proof artifact={Path.GetFullPath(options.OutputPath)}");
        }

        return completed ? 0 : 3;
    }

    private static int RunMirroredArtifactProof(
        SimulationWorld world,
        PlayerEntity bot,
        MotionProofOptions options,
        Stopwatch stopwatch,
        int initialRedCaps,
        int initialBlueCaps)
    {
        if (string.IsNullOrWhiteSpace(options.MirrorArtifactPath))
        {
            Console.Error.WriteLine("mirror_requires_artifact");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            Console.Error.WriteLine("mirror_requires_output");
            return 2;
        }

        var sourceArtifact = ReadJsonArtifact<MotionProofArtifact>(options.MirrorArtifactPath);
        if (sourceArtifact is null)
        {
            Console.Error.WriteLine($"mirror_load_failed:{options.MirrorArtifactPath}");
            return 2;
        }

        if (!ValidateMirrorSource(world, options, sourceArtifact, out var validationMessage))
        {
            Console.WriteLine($"motion-proof mirror status=fail reason={validationMessage} source={options.MirrorArtifactPath}");
            return 3;
        }

        var attackPath = new MotionPath(MirrorActions(sourceArtifact.Attack));
        var returnPath = new MotionPath(MirrorActions(sourceArtifact.Return));
        Console.WriteLine($"motion-proof mirror status=loaded source={Path.GetFileName(options.MirrorArtifactPath)} sourceTeam={sourceArtifact.Team} targetTeam={options.Team} attackActions={attackPath.Actions.Count} returnActions={returnPath.Actions.Count}");

        if (IsKothMode(world.MatchRules.Mode))
        {
            return RunMirroredKothArtifactProof(world, bot, options, stopwatch, attackPath);
        }

        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag)
        {
            Console.Error.WriteLine($"mirror_mode_not_supported:{world.MatchRules.Mode}");
            return 2;
        }

        ReplayPath(world, bot, options.Team, attackPath, "mirror_attack");
        if (!bot.IsCarryingIntel)
        {
            Console.WriteLine(
                $"motion-proof mirror result status=fail phase=attack reason=no_intel pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
            return 3;
        }

        ReplayPath(world, bot, options.Team, returnPath, "mirror_return");
        var completed = options.Team == PlayerTeam.Blue
            ? world.BlueCaps > initialBlueCaps
            : world.RedCaps > initialRedCaps;
        Console.WriteLine(
            $"motion-proof mirror result status={(completed ? "pass" : "fail")} caps={world.RedCaps}-{world.BlueCaps} carrying={bot.IsCarryingIntel} elapsedMs={stopwatch.ElapsedMilliseconds} final=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
        if (completed)
        {
            WriteProofArtifact(options.OutputPath, world, options, attackPath, returnPath);
            Console.WriteLine($"motion-proof mirror artifact={Path.GetFullPath(options.OutputPath)}");
        }

        return completed ? 0 : 3;
    }

    private static int RunMirroredKothArtifactProof(
        SimulationWorld world,
        PlayerEntity bot,
        MotionProofOptions options,
        Stopwatch stopwatch,
        MotionPath attackPath)
    {
        var targetPoint = SelectKothTargetPoint(world, options.Team);
        if (targetPoint is null)
        {
            Console.WriteLine("motion-proof mirror koth status=fail reason=missing_control_point");
            return 3;
        }

        var initialRedTimer = world.KothRedTimerTicksRemaining;
        var initialBlueTimer = world.KothBlueTimerTicksRemaining;
        ReplayPath(world, bot, options.Team, attackPath, "mirror_koth");
        if (!world.IsPlayerInControlPointCaptureZone(bot, targetPoint.Index))
        {
            HoldInput(world, 90);
            Console.WriteLine(
                $"motion-proof mirror koth settle ticks=90 pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
        }

        if (!world.IsPlayerInControlPointCaptureZone(bot, targetPoint.Index))
        {
            Console.WriteLine(
                $"motion-proof mirror koth result status=fail reason=not_in_capture_zone pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
            return 3;
        }

        var holdTicks = Math.Max(0, world.KothUnlockTicksRemaining) + Math.Max(1, targetPoint.CapTimeTicks) + 90;
        HoldInput(world, holdTicks);
        var timerAdvanced = options.Team == PlayerTeam.Blue
            ? world.KothBlueTimerTicksRemaining < initialBlueTimer
            : world.KothRedTimerTicksRemaining < initialRedTimer;
        var completed = targetPoint.Team == options.Team && timerAdvanced;
        Console.WriteLine(
            $"motion-proof mirror koth result status={(completed ? "pass" : "fail")} pointTeam={targetPoint.Team?.ToString() ?? "None"} timers={world.KothRedTimerTicksRemaining}-{world.KothBlueTimerTicksRemaining} initialTimers={initialRedTimer}-{initialBlueTimer} holdTicks={holdTicks} elapsedMs={stopwatch.ElapsedMilliseconds} final=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
        if (completed)
        {
            WriteProofArtifact(options.OutputPath!, world, options, attackPath, MotionPath.Empty);
            Console.WriteLine($"motion-proof mirror artifact={Path.GetFullPath(options.OutputPath!)}");
        }

        return completed ? 0 : 3;
    }

    private static int RunGraphBake(
        SimulationWorld world,
        PlayerEntity bot,
        MotionProofOptions options,
        Stopwatch stopwatch)
    {
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            Console.Error.WriteLine("graph_bake_requires_output");
            return 2;
        }

        var classDefinition = CharacterClassCatalog.GetDefinition(options.ClassId);
        var movementProfileId = options.MovementProfileId ?? GetMovementProfileId(options.ClassId);
        var classProbe = new PlayerEntity(id: 99, classDefinition, displayName: "motion-proof-class-probe");
        var actions = BuildPrimitiveActions().ToArray();
        var start = MotionState.FromPlayer(bot);
        var coverageTracker = MotionCoverageTracker.Create(world.Level, classDefinition, options);
        var nodes = new List<MotionGraphBuildNode>();
        var nodeIndicesByKey = new Dictionary<MotionGraphKey, List<int>>();
        var edges = new List<MotionGraphEdge>();
        var queue = new PriorityQueue<int, float>();
        var queuedNodeIndices = new HashSet<int>();
        var expandedNodeIndices = new HashSet<int>();
        AddGraphNode(start);
        var gameplayGoals = (options.SkipGameplaySeedGoals
                ? Enumerable.Empty<MotionGoal>()
                : BuildGraphSeedGoals(world, options.Team))
            .Concat(options.ExtraSeedGoals.Select(goal => new MotionGoal(goal.X, goal.Bottom, options.GoalRadius, options.GoalRadius, goal.Label)))
            .ToArray();
        var coverageAnchors = options.SeedCoverageAnchors ? AddCoverageAnchorNodes() : 0;
        var seedGoals = gameplayGoals
            .Concat(BuildWalkableCoverageSeedGoals(world.Level, classDefinition, options))
            .ToArray();
        var coverageConnectivityPaths = 0;
        var coverageHubPaths = 0;
        var coverageGapPaths = 0;
        var seededPaths = 0;
        var importedProofPaths = 0;
        var explicitSeedPaths = 0;
        var expanded = 0;
        var rejected = 0;
        while (queue.Count > 0
            && nodes.Count < options.GraphExpansionMaxNodes
            && stopwatch.ElapsedMilliseconds < options.SearchBudgetMilliseconds)
        {
            var fromIndex = queue.Dequeue();
            if (!expandedNodeIndices.Add(fromIndex))
            {
                continue;
            }

            var fromNode = nodes[fromIndex];
            expanded += 1;
            foreach (var action in actions)
            {
                if (!TrySimulateAction(
                        world.Level,
                        options.Team,
                        classDefinition,
                        carryingIntel: false,
                        fromNode.State,
                        action,
                        goal: null,
                        requireStableEnd: true,
                        out var nextState,
                        out var actualAction,
                        out _))
                {
                    rejected += 1;
                    continue;
                }

                var toIndex = AddGraphNode(nextState);
                edges.Add(new MotionGraphEdge(
                    From: fromIndex,
                    To: toIndex,
                    CostTicks: actualAction.Ticks,
                    Action: MotionProofAction.From(actualAction),
                    TeamMask: "Any",
                    CarryMask: "Free"));
                if (nodes.Count >= options.GraphExpansionMaxNodes)
                {
                    break;
                }
            }
        }

        var localWeldEdges = AddLocalGroundWeldEdges();
        coverageConnectivityPaths = options.SeedCoverageConnectivity ? AddCoverageConnectivitySeedPaths() : 0;
        importedProofPaths = AddProofArtifactSeedPaths();
        explicitSeedPaths = AddExplicitSeedPathPairs();
        seededPaths = options.SkipObjectiveSeedPaths ? 0 : AddObjectiveSeedPaths();
        if (options.SkipObjectiveSeedPaths)
        {
            Console.WriteLine("motion-proof graph-seed status=skipped reason=skip_objective_seed_paths");
        }

        coverageGapPaths = options.RequireObjectiveReachability ? AddCoverageObjectiveGapSeedPaths() : 0;
        if (options.SeedCoverageHubPaths && !HasReachedObjectiveReachabilityTarget())
        {
            coverageHubPaths = AddCoverageHubSeedPaths();
            coverageGapPaths += options.RequireObjectiveReachability ? AddCoverageObjectiveGapSeedPaths() : 0;
        }
        else if (options.SeedCoverageHubPaths)
        {
            Console.WriteLine("motion-proof coverage-hub status=skipped reason=objective_reachability_satisfied");
        }

        var artifact = new MotionGraphArtifact(
            Version: 1,
            Map: world.Level.Name,
            Area: world.Level.MapAreaIndex,
            Mode: world.MatchRules.Mode.ToString(),
            Team: options.Team.ToString(),
            Class: movementProfileId,
            Nodes: nodes.Select(node => new MotionGraphNode(
                node.Id,
                node.State.X,
                node.State.Y,
                node.State.Bottom,
                node.State.IsGrounded,
                node.State.HorizontalSpeed,
                node.State.VerticalSpeed,
                node.State.RemainingAirJumps)).ToArray(),
            Edges: edges.ToArray());
        var reachabilityReport = MotionCoverageReachabilityReport.Create(
            nodes,
            edges,
            coverageTracker.Samples,
            classDefinition,
            gameplayGoals,
            options.CoverageSampleRadius);
        var coveragePassed = coverageTracker.HasReachedTarget;
        var objectiveReachabilityPassed = !options.RequireObjectiveReachability
            || reachabilityReport.Ratio >= options.ObjectiveReachabilityTargetRatio;
        var passed = coveragePassed && objectiveReachabilityPassed;
        var shouldWriteArtifact = passed || options.WriteFailedGraph;
        Console.WriteLine(
            $"motion-proof graph-bake status={(passed ? "pass" : "fail")} nodes={nodes.Count} edges={edges.Count} coverageAnchors={coverageAnchors} localWeldEdges={localWeldEdges} coverageLinks={coverageConnectivityPaths} coverageHubPaths={coverageHubPaths} coverageGapPaths={coverageGapPaths} importedProofPaths={importedProofPaths} explicitSeedPaths={explicitSeedPaths} seededPaths={seededPaths} expanded={expanded} rejected={rejected} coverage={coverageTracker.CoveredCount}/{coverageTracker.SampleCount} ratio={coverageTracker.CoverageRatio:0.000} target={options.CoverageTargetRatio:0.000} objectiveReachable={reachabilityReport.ReachableSamples}/{reachabilityReport.SampleCount} objectiveRatio={reachabilityReport.Ratio:0.000} objectiveTarget={options.ObjectiveReachabilityTargetRatio:0.000} elapsedMs={stopwatch.ElapsedMilliseconds} artifactWritten={shouldWriteArtifact} artifact={Path.GetFullPath(options.OutputPath)}");
        if (!passed)
        {
            Console.WriteLine($"motion-proof graph-bake uncovered={FormatCoverageSamples(coverageTracker.GetUncoveredSamples(limit: 12))}");
            Console.WriteLine($"motion-proof graph-bake objectiveUnreachable={FormatCoverageSamples(reachabilityReport.GetUnreachableSamples(limit: 12))}");
        }

        if (shouldWriteArtifact)
        {
            WriteJsonArtifact(options.OutputPath, artifact);
        }

        return passed ? 0 : 3;

        bool HasReachedObjectiveReachabilityTarget()
        {
            if (!options.RequireObjectiveReachability)
            {
                return true;
            }

            var report = MotionCoverageReachabilityReport.Create(
                nodes,
                edges,
                coverageTracker.Samples,
                classDefinition,
                gameplayGoals,
                options.CoverageSampleRadius);
            return report.Ratio >= options.ObjectiveReachabilityTargetRatio;
        }

        int AddCoverageAnchorNodes()
        {
            var count = 0;
            foreach (var sample in coverageTracker.Samples)
            {
                var nodeIndex = AddGraphNode(MotionState.FromGrounded(
                    sample.X,
                    sample.Y,
                    classDefinition.CollisionBottom,
                    classProbe.MaxAirJumps));
                EnqueueGraphNode(nodeIndex, 0f);
                count += 1;
            }

            Console.WriteLine($"motion-proof coverage-anchor status=seeded anchors={count}");
            return count;
        }

        int AddProofArtifactSeedPaths()
        {
            var count = 0;
            foreach (var artifactPath in options.ProofSeedArtifactPaths)
            {
                var artifact = ReadJsonArtifact<MotionProofArtifact>(artifactPath);
                if (artifact is null)
                {
                    Console.WriteLine($"motion-proof graph-proof-seed status=fail reason=load_failed artifact={artifactPath}");
                    continue;
                }

                var attackPath = new MotionPath(artifact.Attack.Select(action => action.ToMotionAction()).ToArray());
                var attackStart = start;
                var attackEnd = AddPathToGraph(attackStart, attackPath, carryingIntel: false);
                count += 1;
                Console.WriteLine($"motion-proof graph-proof-seed status=imported phase=attack artifact={Path.GetFileName(artifactPath)} actions={attackPath.Actions.Count} ticks={attackPath.TotalTicks}");

                if (artifact.Return.Count == 0 || !attackEnd.HasValue)
                {
                    continue;
                }

                var returnPath = new MotionPath(artifact.Return.Select(action => action.ToMotionAction()).ToArray());
                AddPathToGraph(attackEnd.Value, returnPath, carryingIntel: true);
                count += 1;
                Console.WriteLine($"motion-proof graph-proof-seed status=imported phase=return artifact={Path.GetFileName(artifactPath)} actions={returnPath.Actions.Count} ticks={returnPath.TotalTicks}");
            }

            return count;
        }

        int AddExplicitSeedPathPairs()
        {
            var count = 0;
            foreach (var seedPath in options.ExplicitSeedPaths)
            {
                var seedStart = MotionState.FromGrounded(
                    seedPath.StartX,
                    seedPath.StartBottom,
                    classDefinition.CollisionBottom,
                    classProbe.MaxAirJumps);
                var goal = new MotionGoal(
                    seedPath.GoalX,
                    seedPath.GoalBottom,
                    options.GoalRadius,
                    options.GoalRadius,
                    seedPath.Label);
                if (!TryFindPrimitivePath(
                        world.Level,
                        options.Team,
                        options.ClassId,
                        carryingIntel: false,
                        seedStart,
                        goal,
                        options,
                        searchBudgetMillisecondsOverride: null,
                        out var path,
                        out var stats))
                {
                    Console.WriteLine($"motion-proof explicit-seed status=fail start=({seedPath.StartX:0.0},{seedPath.StartBottom:0.0}) goal={seedPath.Label} {stats}");
                    continue;
                }

                AddPathToGraphUntilGoal(seedStart, path, goal, carryingIntel: false);
                count += 1;
                Console.WriteLine($"motion-proof explicit-seed status=found start=({seedPath.StartX:0.0},{seedPath.StartBottom:0.0}) goal={seedPath.Label} actions={path.Actions.Count} ticks={path.TotalTicks}");
            }

            return count;
        }

        MotionState? AddPathToGraphUntilGoal(MotionState pathStart, MotionPath path, MotionGoal goal, bool carryingIntel)
        {
            var fromState = pathStart;
            var fromIndex = AddGraphNode(fromState);
            foreach (var action in path.Actions)
            {
                if (!TrySimulateAction(
                        world.Level,
                        options.Team,
                        classDefinition,
                        carryingIntel,
                        fromState,
                        action,
                        goal,
                        requireStableEnd: false,
                        out var toState,
                        out var actualAction,
                        out var reachedGoal))
                {
                    return null;
                }

                var toIndex = AddGraphNode(toState);
                edges.Add(new MotionGraphEdge(
                    From: fromIndex,
                    To: toIndex,
                    CostTicks: actualAction.Ticks,
                    Action: MotionProofAction.From(actualAction),
                    TeamMask: "Any",
                    CarryMask: carryingIntel ? "Carrying" : "Free"));
                fromState = toState;
                fromIndex = toIndex;
                if (reachedGoal)
                {
                    return fromState;
                }
            }

            return fromState;
        }

        int AddLocalGroundWeldEdges()
        {
            const float maximumHorizontalDistance = 220f;
            const float maximumBottomDelta = 18f;
            const int maximumNeighborsPerSide = 4;
            var count = 0;
            var snapshotCount = nodes.Count;
            for (var fromIndex = 0; fromIndex < snapshotCount; fromIndex += 1)
            {
                var from = nodes[fromIndex].State;
                if (!from.IsGrounded)
                {
                    continue;
                }

                var leftIndices = new int[maximumNeighborsPerSide];
                var leftDistances = new float[maximumNeighborsPerSide];
                var rightIndices = new int[maximumNeighborsPerSide];
                var rightDistances = new float[maximumNeighborsPerSide];
                Array.Fill(leftIndices, -1);
                Array.Fill(rightIndices, -1);
                Array.Fill(leftDistances, float.MaxValue);
                Array.Fill(rightDistances, float.MaxValue);
                for (var toIndex = 0; toIndex < snapshotCount; toIndex += 1)
                {
                    if (fromIndex == toIndex)
                    {
                        continue;
                    }

                    var to = nodes[toIndex].State;
                    if (!to.IsGrounded
                        || MathF.Abs(to.Bottom - from.Bottom) > maximumBottomDelta
                        || MathF.Abs(to.X - from.X) > maximumHorizontalDistance)
                    {
                        continue;
                    }

                    var deltaX = to.X - from.X;
                    var distance = MathF.Abs(deltaX);
                    var score = distance + (IsStableGroundedWeldState(to) ? 0f : 1000f);
                    if (deltaX < 0)
                    {
                        InsertNearestCandidate(leftIndices, leftDistances, toIndex, score);
                    }
                    else if (deltaX > 0)
                    {
                        InsertNearestCandidate(rightIndices, rightDistances, toIndex, score);
                    }
                }

                count += AddLocalGroundWeldCandidates(fromIndex, from, leftIndices);
                count += AddLocalGroundWeldCandidates(fromIndex, from, rightIndices);
            }

            Console.WriteLine($"motion-proof local-weld status=seeded edges={count} nodes={snapshotCount}");
            return count;
        }

        int AddLocalGroundWeldCandidates(int fromIndex, MotionState from, int[] candidateIndices)
        {
            var count = 0;
            foreach (var toIndex in candidateIndices)
            {
                if (toIndex < 0)
                {
                    continue;
                }

                var to = nodes[toIndex].State;
                var direction = Math.Sign(to.X - from.X);
                if (direction == 0 || !TryBuildSingleRunWeld(from, to, direction, out var action))
                {
                    continue;
                }

                edges.Add(new MotionGraphEdge(
                    From: fromIndex,
                    To: toIndex,
                    CostTicks: action.Ticks,
                    Action: MotionProofAction.From(action),
                    TeamMask: "Any",
                    CarryMask: "Free"));
                count += 1;
            }

            return count;
        }

        static void InsertNearestCandidate(int[] indices, float[] distances, int index, float distance)
        {
            for (var candidateSlot = 0; candidateSlot < distances.Length; candidateSlot += 1)
            {
                if (distance >= distances[candidateSlot])
                {
                    continue;
                }

                for (var moveSlot = distances.Length - 1; moveSlot > candidateSlot; moveSlot -= 1)
                {
                    distances[moveSlot] = distances[moveSlot - 1];
                    indices[moveSlot] = indices[moveSlot - 1];
                }

                distances[candidateSlot] = distance;
                indices[candidateSlot] = index;
                return;
            }
        }

        static bool IsStableGroundedWeldState(MotionState state)
        {
            return state.IsGrounded
                && MathF.Abs(state.HorizontalSpeed) <= 1f
                && MathF.Abs(state.VerticalSpeed) <= 1f;
        }

        bool TryBuildSingleRunWeld(MotionState from, MotionState to, int direction, out MotionAction action)
        {
            const float goalRadius = 56f;
            var goal = new MotionGoal(to.X, to.Bottom, goalRadius, goalRadius, "local_weld");
            foreach (var ticks in new[] { 8, 14, 22, 34, 52 })
            {
                var candidate = MotionAction.Run(direction, ticks);
                if (!TrySimulateAction(
                        world.Level,
                        options.Team,
                        classDefinition,
                        carryingIntel: false,
                        from,
                        candidate,
                        goal,
                        requireStableEnd: false,
                        out _,
                        out var actualAction,
                        out var reachedGoal)
                    || !reachedGoal)
                {
                    continue;
                }

                action = actualAction;
                return true;
            }

            action = default;
            return false;
        }

        int AddCoverageConnectivitySeedPaths()
        {
            var samples = coverageTracker.Samples;
            if (samples.Count <= 1 || options.CoverageNeighborLinks <= 0)
            {
                return 0;
            }

            var discoveredPaths = new ConcurrentBag<MotionCoverageSeedPath>();
            Parallel.For(
                0,
                samples.Count,
                new ParallelOptions { MaxDegreeOfParallelism = options.CoveragePathParallelism },
                sourceIndex =>
                {
                    var source = samples[sourceIndex];
                    var sourceState = MotionState.FromGrounded(
                        source.X,
                        source.Y,
                        classDefinition.CollisionBottom,
                        classProbe.MaxAirJumps);
                    foreach (var target in SelectCoverageNeighborGoals(samples, sourceIndex, options))
                    {
                        if (!TryFindPrimitivePath(
                                world.Level,
                                options.Team,
                                options.ClassId,
                                carryingIntel: false,
                                sourceState,
                                target,
                                options,
                                searchBudgetMillisecondsOverride: options.CoverageLinkSearchBudgetMilliseconds,
                                out var linkPath,
                                out _))
                        {
                            continue;
                        }

                        discoveredPaths.Add(new MotionCoverageSeedPath(sourceState, linkPath, CarryingIntel: false));
                    }
                });

            var count = 0;
            foreach (var discoveredPath in discoveredPaths)
            {
                AddPathToGraph(discoveredPath.Start, discoveredPath.Path, discoveredPath.CarryingIntel);
                count += 1;
            }

            Console.WriteLine(
                $"motion-proof coverage-link status=seeded links={count} samples={samples.Count} neighbors={options.CoverageNeighborLinks} radius={options.CoverageLinkMaxDistance:0.0} budgetMs={options.CoverageLinkSearchBudgetMilliseconds} parallelism={options.CoveragePathParallelism}");
            return count;
        }

        int AddCoverageHubSeedPaths()
        {
            var samples = coverageTracker.Samples;
            if (samples.Count == 0 || gameplayGoals.Length == 0)
            {
                return 0;
            }

            var hubs = SelectCoverageHubGoals(gameplayGoals, samples, options).ToArray();
            if (hubs.Length == 0)
            {
                return 0;
            }

            var sampleGoals = samples
                .Select((sample, index) => sample with
                {
                    Width = options.CoverageSampleRadius,
                    Height = options.CoverageSampleRadius,
                    Label = $"coverage_sample_{index + 1}",
                })
                .ToArray();
            var carryReturnGoals = BuildCtfCarryReturnGoals(world, options.Team).ToArray();
            var discoveredPaths = new ConcurrentBag<MotionCoverageSeedPath>();
            Parallel.For(
                0,
                samples.Count,
                new ParallelOptions { MaxDegreeOfParallelism = options.CoveragePathParallelism },
                sourceIndex =>
                {
                    var source = samples[sourceIndex];
                    var sourceState = MotionState.FromGrounded(
                        source.X,
                        source.Y,
                        classDefinition.CollisionBottom,
                        classProbe.MaxAirJumps);
                    var foundHubPaths = TryFindPrimitivePathsToGoals(
                        world.Level,
                        options.Team,
                        options.ClassId,
                        carryingIntel: false,
                        sourceState,
                        hubs,
                        options,
                        searchBudgetMillisecondsOverride: options.CoverageLinkSearchBudgetMilliseconds,
                        out _);
                    foreach (var path in foundHubPaths.Values)
                    {
                        discoveredPaths.Add(new MotionCoverageSeedPath(sourceState, path, CarryingIntel: false));
                    }

                    if (carryReturnGoals.Length > 0)
                    {
                        var foundCarryHubPaths = TryFindPrimitivePathsToGoals(
                            world.Level,
                            options.Team,
                            options.ClassId,
                            carryingIntel: true,
                            sourceState,
                            hubs,
                            options,
                            searchBudgetMillisecondsOverride: options.CoverageLinkSearchBudgetMilliseconds,
                            out _);
                        foreach (var path in foundCarryHubPaths.Values)
                        {
                            discoveredPaths.Add(new MotionCoverageSeedPath(sourceState, path, CarryingIntel: true));
                        }
                    }
                });

            Parallel.ForEach(
                hubs,
                new ParallelOptions { MaxDegreeOfParallelism = options.CoveragePathParallelism },
                hub =>
                {
                    var hubState = MotionState.FromGrounded(
                        hub.X,
                        hub.Y,
                        classDefinition.CollisionBottom,
                        classProbe.MaxAirJumps);
                    var foundSamplePaths = TryFindPrimitivePathsToGoals(
                    world.Level,
                    options.Team,
                    options.ClassId,
                    carryingIntel: false,
                    hubState,
                    sampleGoals,
                    options,
                    searchBudgetMillisecondsOverride: options.CoverageHubFanoutSearchBudgetMilliseconds,
                    out _);
                    foreach (var path in foundSamplePaths.Values)
                    {
                        discoveredPaths.Add(new MotionCoverageSeedPath(hubState, path, CarryingIntel: false));
                    }

                    if (carryReturnGoals.Length > 0)
                    {
                        var foundCarrySamplePaths = TryFindPrimitivePathsToGoals(
                            world.Level,
                            options.Team,
                            options.ClassId,
                            carryingIntel: true,
                            hubState,
                            sampleGoals,
                            options,
                            searchBudgetMillisecondsOverride: options.CoverageHubFanoutSearchBudgetMilliseconds,
                            out _);
                        foreach (var path in foundCarrySamplePaths.Values)
                        {
                            discoveredPaths.Add(new MotionCoverageSeedPath(hubState, path, CarryingIntel: true));
                        }
                    }

                    var foundObjectivePaths = TryFindPrimitivePathsToGoals(
                        world.Level,
                        options.Team,
                        options.ClassId,
                        carryingIntel: false,
                        hubState,
                        gameplayGoals,
                        options,
                        searchBudgetMillisecondsOverride: options.CoverageHubSearchBudgetMilliseconds,
                        out _);
                    foreach (var path in foundObjectivePaths.Values)
                    {
                        discoveredPaths.Add(new MotionCoverageSeedPath(hubState, path, CarryingIntel: false));
                    }

                    if (carryReturnGoals.Length > 0)
                    {
                        var foundCarryReturnPaths = TryFindPrimitivePathsToGoals(
                            world.Level,
                            options.Team,
                            options.ClassId,
                            carryingIntel: true,
                            hubState,
                            carryReturnGoals,
                            options,
                            searchBudgetMillisecondsOverride: options.CoverageHubSearchBudgetMilliseconds,
                            out _);
                        foreach (var path in foundCarryReturnPaths.Values)
                        {
                            discoveredPaths.Add(new MotionCoverageSeedPath(hubState, path, CarryingIntel: true));
                        }
                    }
                });

            var count = 0;
            foreach (var discoveredPath in discoveredPaths)
            {
                AddPathToGraph(discoveredPath.Start, discoveredPath.Path, discoveredPath.CarryingIntel);
                count += 1;
            }

            Console.WriteLine(
                $"motion-proof coverage-hub status=seeded paths={count} hubs={hubs.Length} samples={samples.Count} parallelism={options.CoveragePathParallelism} hubBudgetMs={options.CoverageHubSearchBudgetMilliseconds} fanoutBudgetMs={options.CoverageHubFanoutSearchBudgetMilliseconds}");
            return count;
        }

        int AddCoverageObjectiveGapSeedPaths()
        {
            var currentReport = MotionCoverageReachabilityReport.Create(
                nodes,
                edges,
                coverageTracker.Samples,
                classDefinition,
                gameplayGoals,
                options.CoverageSampleRadius);
            if (currentReport.Ratio >= options.ObjectiveReachabilityTargetRatio)
            {
                Console.WriteLine(
                    $"motion-proof coverage-gap status=skipped reason=target_met objectiveReachable={currentReport.ReachableSamples}/{currentReport.SampleCount} ratio={currentReport.Ratio:0.000}");
                return 0;
            }

            var gapSamples = currentReport.GetUnreachableSamples().ToArray();
            if (gapSamples.Length == 0)
            {
                return 0;
            }

            if (options.CoverageGapMaxSamples <= 0 || options.CoverageGapSearchBudgetMilliseconds <= 0)
            {
                Console.WriteLine(
                    $"motion-proof coverage-gap status=skipped reason=disabled gaps={gapSamples.Length} objectiveReachable={currentReport.ReachableSamples}/{currentReport.SampleCount} ratio={currentReport.Ratio:0.000}");
                return 0;
            }

            gapSamples = SelectGapRepairSamplesByComponent(gapSamples);
            if (gapSamples.Length > options.CoverageGapMaxSamples)
            {
                gapSamples = gapSamples
                    .OrderByDescending(sample => Distance(sample.X, sample.Y, start.X, start.Bottom))
                    .Take(options.CoverageGapMaxSamples)
                    .ToArray();
            }

            var carryReturnGoals = BuildCtfCarryReturnGoals(world, options.Team).ToArray();
            var discoveredPaths = new ConcurrentBag<MotionCoverageSeedPath>();
            Parallel.ForEach(
                gapSamples,
                new ParallelOptions { MaxDegreeOfParallelism = options.CoveragePathParallelism },
                sample =>
                {
                    var sourceState = MotionState.FromGrounded(
                        sample.X,
                        sample.Y,
                        classDefinition.CollisionBottom,
                        classProbe.MaxAirJumps);
                    var foundObjectivePaths = TryFindPrimitivePathsToGoals(
                        world.Level,
                        options.Team,
                        options.ClassId,
                        carryingIntel: false,
                        sourceState,
                        gameplayGoals,
                        options,
                        searchBudgetMillisecondsOverride: options.CoverageGapSearchBudgetMilliseconds,
                        out _);
                    foreach (var path in foundObjectivePaths.Values)
                    {
                        discoveredPaths.Add(new MotionCoverageSeedPath(sourceState, path, CarryingIntel: false));
                    }

                    if (carryReturnGoals.Length > 0)
                    {
                        var foundCarryReturnPaths = TryFindPrimitivePathsToGoals(
                            world.Level,
                            options.Team,
                            options.ClassId,
                            carryingIntel: true,
                            sourceState,
                            carryReturnGoals,
                            options,
                            searchBudgetMillisecondsOverride: options.CoverageGapSearchBudgetMilliseconds,
                            out _);
                        foreach (var path in foundCarryReturnPaths.Values)
                        {
                            discoveredPaths.Add(new MotionCoverageSeedPath(sourceState, path, CarryingIntel: true));
                        }
                    }
                });

            var count = 0;
            foreach (var discoveredPath in discoveredPaths)
            {
                AddPathToGraph(discoveredPath.Start, discoveredPath.Path, discoveredPath.CarryingIntel);
                count += 1;
            }

            Console.WriteLine(
                $"motion-proof coverage-gap status=seeded paths={count} gaps={gapSamples.Length} objectiveReachable={currentReport.ReachableSamples}/{currentReport.SampleCount} ratio={currentReport.Ratio:0.000} budgetMs={options.CoverageGapSearchBudgetMilliseconds}");
            return count;

            MotionGoal[] SelectGapRepairSamplesByComponent(MotionGoal[] samplesToRepair)
            {
                if (samplesToRepair.Length <= 1 || nodes.Count == 0)
                {
                    return samplesToRepair;
                }

                var componentByNode = BuildWeakComponentIndex(nodes.Count, edges);
                return samplesToRepair
                    .Select(sample => new
                    {
                        Sample = sample,
                        NodeIndex = FindNearestGraphNodeIndex(sample),
                    })
                    .Where(entry => entry.NodeIndex >= 0)
                    .GroupBy(entry => componentByNode[entry.NodeIndex])
                    .SelectMany(group => group
                        .OrderByDescending(entry => Distance(entry.Sample.X, entry.Sample.Y, start.X, start.Bottom))
                        .Take(Math.Max(1, options.CoverageGapSamplesPerComponent))
                        .Select(entry => entry.Sample))
                    .ToArray();
            }

            int FindNearestGraphNodeIndex(MotionGoal sample)
            {
                var bestIndex = -1;
                var bestDistance = float.MaxValue;
                for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex += 1)
                {
                    var node = nodes[nodeIndex].State;
                    var distance = Distance(node.X, node.Bottom, sample.X, sample.Y);
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    bestIndex = nodeIndex;
                }

                return bestDistance <= MathF.Max(options.CoverageSampleRadius, 96f)
                    ? bestIndex
                    : -1;
            }
        }

        int AddObjectiveSeedPaths()
        {
            var count = 0;
            count += AddSeedPathsFromStart(start, seedGoals, startLabel: null, allowCoveredSkip: true);

            foreach (var extraStart in options.ExtraSeedStarts)
            {
                var seedStart = MotionState.FromGrounded(
                    extraStart.X,
                    extraStart.Bottom,
                    classDefinition.CollisionBottom,
                    classProbe.MaxAirJumps);
                count += AddSeedPathsFromStart(seedStart, seedGoals, $"start=({extraStart.X:0.0},{extraStart.Bottom:0.0})", allowCoveredSkip: false);
            }

            return count;
        }

        int AddSeedPathsFromStart(
            MotionState seedStart,
            IReadOnlyList<MotionGoal> goals,
            string? startLabel,
            bool allowCoveredSkip)
        {
            var count = 0;
            var pendingGoalIndices = new List<int>();
            for (var goalIndex = 0; goalIndex < goals.Count; goalIndex += 1)
            {
                var goal = goals[goalIndex];
                if (allowCoveredSkip && IsWalkableCoverageGoal(goal) && IsGoalCoveredByCurrentGraph(goal))
                {
                    count += 1;
                    Console.WriteLine($"motion-proof graph-seed status=covered {FormatSeedStartLabel(startLabel)}goal={goal.Label} radius={options.WalkableSeedCoveredRadius:0.0}");
                    continue;
                }

                pendingGoalIndices.Add(goalIndex);
            }

            if (pendingGoalIndices.Count == 0)
            {
                return count;
            }

            if (!options.UseMultiGoalSeedSearch)
            {
                foreach (var originalGoalIndex in pendingGoalIndices)
                {
                    var goal = goals[originalGoalIndex];
                    if (!TryFindPrimitivePath(
                            world.Level,
                            options.Team,
                            options.ClassId,
                            carryingIntel: false,
                            seedStart,
                            goal,
                            options,
                            searchBudgetMillisecondsOverride: GetSeedSearchBudgetMilliseconds(goal, options),
                            out var seedPath,
                            out var seedStats))
                    {
                        Console.WriteLine($"motion-proof graph-seed status=fail {FormatSeedStartLabel(startLabel)}goal={goal.Label} {seedStats}");
                        continue;
                    }

                    var pathEnd = AddPathToGraph(seedStart, seedPath, carryingIntel: false);
                    count += 1;
                    count += AddCarryReturnSeedPaths(goal, pathEnd, startLabel);
                    Console.WriteLine($"motion-proof graph-seed status=found {FormatSeedStartLabel(startLabel)}goal={goal.Label} actions={seedPath.Actions.Count} ticks={seedPath.TotalTicks} source=single");
                }

                return count;
            }

            var pendingGoals = pendingGoalIndices.Select(index => goals[index]).ToArray();
            var foundByPendingIndex = TryFindPrimitivePathsToGoals(
                world.Level,
                options.Team,
                options.ClassId,
                carryingIntel: false,
                seedStart,
                pendingGoals,
                options,
                searchBudgetMillisecondsOverride: null,
                out var multiStats);
            Console.WriteLine($"motion-proof graph-seed-multigoal {FormatSeedStartLabel(startLabel)}status=complete goals={pendingGoals.Length} found={foundByPendingIndex.Count} {multiStats}");

            var foundOriginalGoalIndices = new HashSet<int>();
            foreach (var foundEntry in foundByPendingIndex.OrderBy(entry => entry.Key))
            {
                var originalGoalIndex = pendingGoalIndices[foundEntry.Key];
                var goal = goals[originalGoalIndex];
                var pathEnd = AddPathToGraph(seedStart, foundEntry.Value, carryingIntel: false);
                count += 1;
                count += AddCarryReturnSeedPaths(goal, pathEnd, startLabel);
                foundOriginalGoalIndices.Add(originalGoalIndex);
                Console.WriteLine($"motion-proof graph-seed status=found {FormatSeedStartLabel(startLabel)}goal={goal.Label} actions={foundEntry.Value.Actions.Count} ticks={foundEntry.Value.TotalTicks} source=multigoal");
            }

            foreach (var originalGoalIndex in pendingGoalIndices)
            {
                if (foundOriginalGoalIndices.Contains(originalGoalIndex))
                {
                    continue;
                }

                var goal = goals[originalGoalIndex];
                if (!TryFindPrimitivePath(
                        world.Level,
                        options.Team,
                        options.ClassId,
                        carryingIntel: false,
                        seedStart,
                        goal,
                        options,
                        searchBudgetMillisecondsOverride: GetSeedSearchBudgetMilliseconds(goal, options),
                        out var seedPath,
                        out var seedStats))
                {
                    Console.WriteLine($"motion-proof graph-seed status=fail {FormatSeedStartLabel(startLabel)}goal={goal.Label} {seedStats}");
                    continue;
                }

                var pathEnd = AddPathToGraph(seedStart, seedPath, carryingIntel: false);
                count += 1;
                count += AddCarryReturnSeedPaths(goal, pathEnd, startLabel);
                Console.WriteLine($"motion-proof graph-seed status=found {FormatSeedStartLabel(startLabel)}goal={goal.Label} actions={seedPath.Actions.Count} ticks={seedPath.TotalTicks} source=fallback");
            }

            return count;
        }

        int AddCarryReturnSeedPaths(MotionGoal attackGoal, MotionState? attackEnd, string? startLabel)
        {
            if (!attackEnd.HasValue || !IsCtfEnemyIntelGoal(attackGoal))
            {
                return 0;
            }

            var returnGoals = BuildCtfCarryReturnGoals(world, options.Team).ToArray();
            if (returnGoals.Length == 0)
            {
                return 0;
            }

            var count = 0;
            var foundReturnPaths = options.UseMultiGoalSeedSearch
                ? TryFindPrimitivePathsToGoals(
                    world.Level,
                    options.Team,
                    options.ClassId,
                    carryingIntel: true,
                    attackEnd.Value,
                    returnGoals,
                    options,
                    searchBudgetMillisecondsOverride: null,
                    out _)
                : new Dictionary<int, MotionPath>();
            if (options.UseMultiGoalSeedSearch)
            {
                foreach (var foundEntry in foundReturnPaths.OrderBy(entry => entry.Key))
                {
                    var returnGoal = returnGoals[foundEntry.Key];
                    AddPathToGraph(attackEnd.Value, foundEntry.Value, carryingIntel: true);
                    count += 1;
                    Console.WriteLine($"motion-proof graph-seed status=found {FormatSeedStartLabel(startLabel)}goal={attackGoal.Label}->{returnGoal.Label} actions={foundEntry.Value.Actions.Count} ticks={foundEntry.Value.TotalTicks} carry=True source=multigoal");
                }

                return count;
            }

            foreach (var returnGoal in returnGoals)
            {
                if (!TryFindPrimitivePath(
                        world.Level,
                        options.Team,
                        options.ClassId,
                        carryingIntel: true,
                        attackEnd.Value,
                        returnGoal,
                        options,
                        searchBudgetMillisecondsOverride: null,
                        out var returnPath,
                        out var returnStats))
                {
                    Console.WriteLine($"motion-proof graph-seed status=fail {FormatSeedStartLabel(startLabel)}goal={attackGoal.Label}->{returnGoal.Label} carry=True {returnStats}");
                    continue;
                }

                AddPathToGraph(attackEnd.Value, returnPath, carryingIntel: true);
                count += 1;
                Console.WriteLine($"motion-proof graph-seed status=found {FormatSeedStartLabel(startLabel)}goal={attackGoal.Label}->{returnGoal.Label} actions={returnPath.Actions.Count} ticks={returnPath.TotalTicks} carry=True source=single");
            }

            return count;
        }

        static bool IsCtfEnemyIntelGoal(MotionGoal goal)
        {
            return goal.Label.StartsWith("enemy_intel", StringComparison.OrdinalIgnoreCase);
        }

        string FormatSeedStartLabel(string? startLabel)
        {
            return string.IsNullOrWhiteSpace(startLabel) ? string.Empty : $"{startLabel} ";
        }

        bool IsGoalCoveredByCurrentGraph(MotionGoal goal)
        {
            for (var index = 0; index < nodes.Count; index += 1)
            {
                var node = nodes[index];
                if (Distance(node.State.X, node.State.Bottom, goal.X, goal.Y) <= options.WalkableSeedCoveredRadius)
                {
                    return true;
                }
            }

            return false;
        }

        MotionState? AddPathToGraph(MotionState pathStart, MotionPath path, bool carryingIntel)
        {
            var pathEnd = AddPathToGraphCore(pathStart, path, carryingIntel);
            if (!options.AddVerifiedReverseSeedPaths
                || path.Actions.Count == 0
                || !pathEnd.HasValue
                || !TryBuildVerifiedReversePath(pathEnd.Value, pathStart, path, carryingIntel, out var reversePath))
            {
                TryConnectPathEndToCoverageAnchor(pathEnd, carryingIntel);
                return pathEnd;
            }

            var reverseEnd = AddPathToGraphCore(pathEnd.Value, reversePath, carryingIntel);
            TryConnectPathEndToCoverageAnchor(pathEnd, carryingIntel);
            TryConnectPathEndToCoverageAnchor(reverseEnd, carryingIntel);
            return pathEnd;
        }

        MotionState? AddPathToGraphCore(MotionState pathStart, MotionPath path, bool carryingIntel)
        {
            var fromState = pathStart;
            var fromIndex = AddGraphNode(fromState);
            foreach (var action in path.Actions)
            {
                if (!TrySimulateAction(
                        world.Level,
                        options.Team,
                        classDefinition,
                        carryingIntel,
                        fromState,
                        action,
                        goal: null,
                        requireStableEnd: false,
                        out var toState,
                        out var actualAction,
                        out _))
                {
                    return null;
                }

                var toIndex = AddGraphNode(toState);
                edges.Add(new MotionGraphEdge(
                    From: fromIndex,
                    To: toIndex,
                    CostTicks: actualAction.Ticks,
                    Action: MotionProofAction.From(actualAction),
                    TeamMask: "Any",
                    CarryMask: carryingIntel ? "Carrying" : "Free"));
                fromState = toState;
                fromIndex = toIndex;
            }

            return fromState;
        }

        void TryConnectPathEndToCoverageAnchor(MotionState? pathEnd, bool carryingIntel)
        {
            if (!pathEnd.HasValue)
            {
                return;
            }

            var nearest = coverageTracker.Samples
                .Select(sample => new
                {
                    Sample = sample,
                    Distance = Distance(pathEnd.Value.X, pathEnd.Value.Bottom, sample.X, sample.Y),
                })
                .Where(entry => entry.Distance <= options.CoverageAnchorWeldRadius)
                .OrderBy(entry => entry.Distance)
                .FirstOrDefault();
            if (nearest is null)
            {
                return;
            }

            var anchorGoal = nearest.Sample with
            {
                Width = MathF.Min(64f, options.CoverageSampleRadius),
                Height = MathF.Min(64f, options.CoverageSampleRadius),
                Label = $"coverage_anchor_weld_{nearest.Sample.Label}",
            };
            if (!TryFindPrimitivePath(
                    world.Level,
                    options.Team,
                    options.ClassId,
                    carryingIntel,
                    pathEnd.Value,
                    anchorGoal,
                    options,
                    searchBudgetMillisecondsOverride: options.CoverageAnchorWeldSearchBudgetMilliseconds,
                    out var weldPath,
                    out _))
            {
                return;
            }

            AddPathToGraphCore(pathEnd.Value, weldPath, carryingIntel);
        }

        bool TryBuildVerifiedReversePath(MotionState pathEnd, MotionState pathStart, MotionPath path, bool carryingIntel, out MotionPath reversePath)
        {
            reversePath = MotionPath.Empty;
            var reverseActions = path.Actions
                .Reverse()
                .Select(ReverseAction)
                .ToArray();
            var verifiedActions = new List<MotionAction>(reverseActions.Length);
            var fromState = pathEnd;
            foreach (var action in reverseActions)
            {
                if (!TrySimulateAction(
                        world.Level,
                        options.Team,
                        classDefinition,
                        carryingIntel,
                        fromState,
                        action,
                        goal: null,
                        requireStableEnd: false,
                        out var toState,
                        out var actualAction,
                        out _))
                {
                    return false;
                }

                fromState = toState;
                verifiedActions.Add(actualAction);
            }

            if (Distance(fromState.X, fromState.Bottom, pathStart.X, pathStart.Bottom) > options.VerifiedReversePathTolerance)
            {
                return false;
            }

            reversePath = new MotionPath(verifiedActions);
            return true;
        }

        MotionAction ReverseAction(MotionAction action)
        {
            var direction = -action.Direction;
            return action.Kind switch
            {
                MotionActionKind.Drop => MotionAction.Jump(direction, action.Ticks),
                MotionActionKind.Fall => MotionAction.Jump(direction, action.Ticks),
                _ => new MotionAction(action.Kind, direction, action.Ticks),
            };
        }

        int AddGraphNode(MotionState state)
        {
            var key = MotionGraphKey.From(state);
            if (nodeIndicesByKey.TryGetValue(key, out var existingIndices))
            {
                foreach (var existingIndex in existingIndices)
                {
                    if (AreReplayEquivalent(nodes[existingIndex].State, state))
                    {
                        return existingIndex;
                    }
                }
            }

            var index = nodes.Count;
            nodes.Add(new MotionGraphBuildNode(index, state));
            coverageTracker.MarkCovered(state.X, state.Bottom);
            if (existingIndices is null)
            {
                existingIndices = [];
                nodeIndicesByKey[key] = existingIndices;
            }

            existingIndices.Add(index);
            EnqueueGraphNode(index, GetGraphBakePriority(start, state, coverageTracker));
            return index;
        }

        void EnqueueGraphNode(int index, float priority)
        {
            if (!queuedNodeIndices.Add(index))
            {
                return;
            }

            queue.Enqueue(index, priority);
        }
    }

    private static IEnumerable<MotionGoal> BuildGraphSeedGoals(SimulationWorld world, PlayerTeam team)
    {
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            var enemyBase = world.Level.GetIntelBase(GetOpposingTeam(team));
            if (enemyBase.HasValue)
            {
                yield return new MotionGoal(enemyBase.Value.X, enemyBase.Value.Y, IntelMarkerSize, IntelMarkerSize, "enemy_intel");
                yield return new MotionGoal(enemyBase.Value.X, enemyBase.Value.Y, ObjectiveAreaSize, ObjectiveAreaSize, "enemy_intel_area");
            }

            var ownBase = world.Level.GetIntelBase(team);
            if (ownBase.HasValue)
            {
                yield return new MotionGoal(ownBase.Value.X, ownBase.Value.Y, IntelMarkerSize, IntelMarkerSize, "own_base");
                yield return new MotionGoal(ownBase.Value.X, ownBase.Value.Y, ObjectiveAreaSize, ObjectiveAreaSize, "own_base_area");
            }
        }

        foreach (var point in world.ControlPoints)
        {
            yield return new MotionGoal(
                point.Marker.CenterX,
                point.Marker.CenterY,
                MathF.Max(42f, point.Marker.Width),
                MathF.Max(42f, point.Marker.Height),
                $"control_point_{point.Index}");
        }

        var captureZones = world.Level.GetRoomObjects(RoomObjectType.CaptureZone);
        for (var index = 0; index < captureZones.Count; index += 1)
        {
            var zone = captureZones[index];
            yield return new MotionGoal(
                zone.CenterX,
                zone.CenterY,
                MathF.Max(42f, zone.Width),
                MathF.Max(42f, zone.Height),
                $"capture_zone_{index + 1}");
        }
    }

    private static IEnumerable<MotionGoal> BuildCtfCarryReturnGoals(SimulationWorld world, PlayerTeam team)
    {
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag)
        {
            yield break;
        }

        var ownBase = world.Level.GetIntelBase(team);
        if (!ownBase.HasValue)
        {
            yield break;
        }

        yield return new MotionGoal(ownBase.Value.X, ownBase.Value.Y, IntelMarkerSize, IntelMarkerSize, "own_base_carry");
        yield return new MotionGoal(ownBase.Value.X, ownBase.Value.Y, ObjectiveAreaSize, ObjectiveAreaSize, "own_base_carry_area");
    }

    private static int? GetSeedSearchBudgetMilliseconds(MotionGoal goal, MotionProofOptions options)
    {
        return IsWalkableCoverageGoal(goal)
            ? options.WalkableSeedSearchBudgetMilliseconds
            : null;
    }

    private static bool IsWalkableCoverageGoal(MotionGoal goal)
    {
        return goal.Label.StartsWith("walkable_", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<MotionGoal> BuildWalkableCoverageSeedGoals(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        MotionProofOptions options)
    {
        if (!options.WalkableSeedStride.HasValue || options.WalkableSeedLimit <= 0)
        {
            yield break;
        }

        var stride = MathF.Max(48f, options.WalkableSeedStride.Value);
        var candidates = BuildAllWalkableGoals(level, classDefinition, stride, width: 72f, height: 72f, labelPrefix: "walkable");
        var selected = SelectSpreadCoverageGoals(candidates, options.WalkableSeedLimit);
        Console.WriteLine(
            $"motion-proof coverage-seed status=selected candidates={candidates.Count} selected={selected.Count} stride={stride:0.0}");
        for (var index = 0; index < selected.Count; index += 1)
        {
            var goal = selected[index];
            Console.WriteLine($"motion-proof coverage-seed goal=walkable_{index + 1} x={goal.X:0.0} bottom={goal.Y:0.0}");
            yield return goal with { Label = $"walkable_{index + 1}" };
        }
    }

    private static List<MotionGoal> BuildAllWalkableGoals(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float stride,
        float width,
        float height,
        string labelPrefix)
    {
        var candidates = new List<MotionGoal>();
        var seen = new HashSet<(int X, int Bottom)>();
        var halfWidth = MathF.Max(MathF.Abs(classDefinition.CollisionLeft), MathF.Abs(classDefinition.CollisionRight));
        foreach (var solid in level.Solids)
        {
            if (solid.Width < (halfWidth * 2f) + 8f)
            {
                continue;
            }

            var firstX = solid.Left + halfWidth + 4f;
            var lastX = solid.Right - halfWidth - 4f;
            for (var x = firstX; x <= lastX; x += stride)
            {
                var bottom = solid.Top;
                if (!IsValidGroundedCoveragePoint(level, classDefinition, x, bottom))
                {
                    continue;
                }

                var key = ((int)MathF.Round(x / 48f), (int)MathF.Round(bottom / 48f));
                if (seen.Add(key))
                {
                    candidates.Add(new MotionGoal(x, bottom, width, height, $"{labelPrefix}_{candidates.Count + 1}"));
                }
            }
        }

        return candidates;
    }

    private static bool IsValidGroundedCoveragePoint(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        float x,
        float bottom)
    {
        var y = bottom - classDefinition.CollisionBottom;
        var left = x + classDefinition.CollisionLeft + 1f;
        var right = x + classDefinition.CollisionRight - 1f;
        var top = y + classDefinition.CollisionTop + 1f;
        var innerBottom = bottom - 1f;
        if (left >= right || top >= innerBottom)
        {
            return false;
        }

        if (left < 0f || right > level.Bounds.Width || top < 0f || bottom > level.Bounds.Height)
        {
            return false;
        }

        if (level.IntersectsSolid(left, top, right, innerBottom))
        {
            return false;
        }

        var supportTop = level.FindBlockingSolidTop(left, bottom, right, bottom + 4f);
        return supportTop.HasValue && MathF.Abs(supportTop.Value - bottom) <= 4f;
    }

    private static List<MotionGoal> SelectSpreadCoverageGoals(List<MotionGoal> candidates, int limit)
    {
        if (candidates.Count <= limit)
        {
            return candidates;
        }

        var selected = new List<MotionGoal>();
        var first = candidates
            .OrderBy(goal => goal.X)
            .ThenByDescending(goal => goal.Y)
            .First();
        selected.Add(first);
        while (selected.Count < limit)
        {
            MotionGoal best = default;
            var bestDistance = float.MinValue;
            foreach (var candidate in candidates)
            {
                if (selected.Any(goal => Distance(goal.X, goal.Y, candidate.X, candidate.Y) < 1f))
                {
                    continue;
                }

                var nearestSelectedDistance = selected.Min(goal => Distance(goal.X, goal.Y, candidate.X, candidate.Y));
                var centerBias = MathF.Abs(candidate.X - candidates[candidates.Count / 2].X) * 0.05f;
                var score = nearestSelectedDistance + centerBias;
                if (score <= bestDistance)
                {
                    continue;
                }

                bestDistance = score;
                best = candidate;
            }

            if (bestDistance == float.MinValue)
            {
                break;
            }

            selected.Add(best);
        }

        return selected;
    }

    private static IEnumerable<MotionGoal> SelectCoverageNeighborGoals(
        IReadOnlyList<MotionGoal> samples,
        int sourceIndex,
        MotionProofOptions options)
    {
        var source = samples[sourceIndex];
        return samples
            .Select((sample, index) => new
            {
                Sample = sample,
                Index = index,
                Distance = Distance(source.X, source.Y, sample.X, sample.Y),
            })
            .Where(entry => entry.Index != sourceIndex
                && entry.Distance <= options.CoverageLinkMaxDistance)
            .OrderBy(entry => entry.Distance)
            .ThenBy(entry => MathF.Abs(entry.Sample.Y - source.Y))
            .Take(options.CoverageNeighborLinks)
            .Select(entry => entry.Sample with
            {
                Width = options.CoverageSampleRadius,
                Height = options.CoverageSampleRadius,
                Label = $"coverage_link_{sourceIndex + 1}_to_{entry.Index + 1}",
            });
    }

    private static List<MotionGoal> SelectCoverageHubGoals(
        IReadOnlyList<MotionGoal> gameplayGoals,
        IReadOnlyList<MotionGoal> samples,
        MotionProofOptions options)
    {
        const int maxCoverageHubs = 16;
        var selected = new List<MotionGoal>();
        foreach (var gameplayGoal in gameplayGoals)
        {
            var nearestSample = samples
                .OrderBy(sample => Distance(sample.X, sample.Y, gameplayGoal.X, gameplayGoal.Y))
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(nearestSample.Label))
            {
                continue;
            }

            if (selected.Any(existing => Distance(existing.X, existing.Y, nearestSample.X, nearestSample.Y) <= 1f))
            {
                continue;
            }

            selected.Add(nearestSample with
            {
                Width = options.CoverageSampleRadius,
                Height = options.CoverageSampleRadius,
                Label = $"coverage_hub_{selected.Count + 1}",
            });
        }

        while (selected.Count < maxCoverageHubs && selected.Count < samples.Count)
        {
            var next = samples
                .Where(sample => selected.All(existing => Distance(existing.X, existing.Y, sample.X, sample.Y) > 1f))
                .Select(sample => new
                {
                    Sample = sample,
                    Distance = selected.Count == 0
                        ? float.MaxValue
                        : selected.Min(existing => Distance(existing.X, existing.Y, sample.X, sample.Y)),
                })
                .OrderByDescending(entry => entry.Distance)
                .FirstOrDefault();
            if (next is null || string.IsNullOrWhiteSpace(next.Sample.Label))
            {
                break;
            }

            selected.Add(next.Sample with
            {
                Width = options.CoverageSampleRadius,
                Height = options.CoverageSampleRadius,
                Label = $"coverage_hub_{selected.Count + 1}",
            });
        }

        return selected;
    }

    private sealed class MotionCoverageTracker
    {
        private readonly MotionGoal[] _samples;
        private readonly bool[] _covered;
        private readonly float _radius;

        private MotionCoverageTracker(MotionGoal[] samples, float radius, float targetRatio)
        {
            _samples = samples;
            _covered = new bool[samples.Length];
            _radius = radius;
            TargetRatio = Math.Clamp(targetRatio, 0f, 1f);
        }

        public int SampleCount => _samples.Length;

        public int CoveredCount { get; private set; }

        public float TargetRatio { get; }

        public IReadOnlyList<MotionGoal> Samples => _samples;

        public float CoverageRatio => SampleCount == 0 ? 1f : CoveredCount / (float)SampleCount;

        public bool HasReachedTarget => CoverageRatio >= TargetRatio;

        public static MotionCoverageTracker Create(
            SimpleLevel level,
            CharacterClassDefinition classDefinition,
            MotionProofOptions options)
        {
            var stride = MathF.Max(48f, options.CoverageSampleStride);
            var samples = BuildAllWalkableGoals(
                    level,
                    classDefinition,
                    stride,
                    width: options.CoverageSampleRadius,
                    height: options.CoverageSampleRadius,
                    labelPrefix: "coverage")
                .ToArray();
            Console.WriteLine(
                $"motion-proof coverage status=initialized samples={samples.Length} stride={stride:0.0} radius={options.CoverageSampleRadius:0.0} target={options.CoverageTargetRatio:0.000}");
            return new MotionCoverageTracker(samples, options.CoverageSampleRadius, options.CoverageTargetRatio);
        }

        public void MarkCovered(float x, float bottom)
        {
            for (var index = 0; index < _samples.Length; index += 1)
            {
                if (_covered[index])
                {
                    continue;
                }

                var sample = _samples[index];
                if (Distance(x, bottom, sample.X, sample.Y) > _radius)
                {
                    continue;
                }

                _covered[index] = true;
                CoveredCount += 1;
            }
        }

        public float? GetDistanceToNearestUncovered(float x, float bottom)
        {
            var best = float.MaxValue;
            for (var index = 0; index < _samples.Length; index += 1)
            {
                if (_covered[index])
                {
                    continue;
                }

                var sample = _samples[index];
                best = MathF.Min(best, Distance(x, bottom, sample.X, sample.Y));
            }

            return best == float.MaxValue ? null : best;
        }

        public IEnumerable<MotionGoal> GetUncoveredSamples(int limit)
        {
            var emitted = 0;
            for (var index = 0; index < _samples.Length && emitted < limit; index += 1)
            {
                if (_covered[index])
                {
                    continue;
                }

                emitted += 1;
                yield return _samples[index];
            }
        }
    }

    private sealed class MotionCoverageReachabilityReport
    {
        private readonly MotionGoal[] _samples;
        private readonly bool[] _reachable;

        private MotionCoverageReachabilityReport(MotionGoal[] samples, bool[] reachable)
        {
            _samples = samples;
            _reachable = reachable;
            ReachableSamples = reachable.Count(value => value);
        }

        public int SampleCount => _samples.Length;

        public int ReachableSamples { get; }

        public float Ratio => SampleCount == 0 ? 1f : ReachableSamples / (float)SampleCount;

        public static MotionCoverageReachabilityReport Create(
            List<MotionGraphBuildNode> nodes,
            IReadOnlyList<MotionGraphEdge> edges,
            IReadOnlyList<MotionGoal> samples,
            CharacterClassDefinition classDefinition,
            MotionGoal[] goals,
            float sampleRadius)
        {
            if (samples.Count == 0)
            {
                return new MotionCoverageReachabilityReport(Array.Empty<MotionGoal>(), Array.Empty<bool>());
            }

            if (nodes.Count == 0 || goals.Length == 0)
            {
                return new MotionCoverageReachabilityReport(samples.ToArray(), new bool[samples.Count]);
            }

            var terminalNodes = FindGameplayTerminalNodes(nodes, classDefinition, goals);
            var reverseReachable = BuildReverseReachableNodes(edges, terminalNodes);
            var reachableSamples = new bool[samples.Count];
            for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex += 1)
            {
                var sample = samples[sampleIndex];
                for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex += 1)
                {
                    if (!reverseReachable.Contains(nodeIndex))
                    {
                        continue;
                    }

                    var node = nodes[nodeIndex].State;
                    if (Distance(node.X, node.Bottom, sample.X, sample.Y) > sampleRadius)
                    {
                        continue;
                    }

                    reachableSamples[sampleIndex] = true;
                    break;
                }
            }

            return new MotionCoverageReachabilityReport(samples.ToArray(), reachableSamples);
        }

        public IEnumerable<MotionGoal> GetUnreachableSamples(int limit)
        {
            var emitted = 0;
            for (var index = 0; index < _samples.Length && emitted < limit; index += 1)
            {
                if (_reachable[index])
                {
                    continue;
                }

                emitted += 1;
                yield return _samples[index];
            }
        }

        public IEnumerable<MotionGoal> GetUnreachableSamples()
        {
            for (var index = 0; index < _samples.Length; index += 1)
            {
                if (!_reachable[index])
                {
                    yield return _samples[index];
                }
            }
        }

        private static HashSet<int> FindGameplayTerminalNodes(
            List<MotionGraphBuildNode> nodes,
            CharacterClassDefinition classDefinition,
            MotionGoal[] goals)
        {
            var terminalNodes = new HashSet<int>();
            for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex += 1)
            {
                var node = nodes[nodeIndex].State;
                foreach (var goal in goals)
                {
                    if (IntersectsGoal(node, classDefinition, goal)
                        || Distance(node.X, node.Bottom, goal.X, goal.Y) <= MathF.Max(goal.Width, goal.Height))
                    {
                        terminalNodes.Add(nodeIndex);
                        break;
                    }
                }
            }

            return terminalNodes;
        }

        private static HashSet<int> BuildReverseReachableNodes(
            IReadOnlyList<MotionGraphEdge> edges,
            HashSet<int> terminalNodes)
        {
            var reachable = new HashSet<int>();
            if (terminalNodes.Count == 0)
            {
                return reachable;
            }

            var reverseEdges = edges
                .GroupBy(edge => edge.To)
                .ToDictionary(group => group.Key, group => group.Select(edge => edge.From).ToArray());
            var queue = new Queue<int>();
            foreach (var terminalNode in terminalNodes)
            {
                if (reachable.Add(terminalNode))
                {
                    queue.Enqueue(terminalNode);
                }
            }

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (!reverseEdges.TryGetValue(node, out var predecessors))
                {
                    continue;
                }

                foreach (var predecessor in predecessors)
                {
                    if (reachable.Add(predecessor))
                    {
                        queue.Enqueue(predecessor);
                    }
                }
            }

            return reachable;
        }
    }

    private static string FormatCoverageSamples(IEnumerable<MotionGoal> samples)
    {
        var formatted = samples
            .Select(sample => $"({sample.X:0.0},{sample.Y:0.0})")
            .ToArray();
        return formatted.Length == 0 ? "none" : string.Join(";", formatted);
    }

    private static bool AreReplayEquivalent(MotionState left, MotionState right)
    {
        return MathF.Abs(left.X - right.X) <= 0.35f
            && MathF.Abs(left.Y - right.Y) <= 0.35f
            && MathF.Abs(left.Bottom - right.Bottom) <= 0.35f
            && MathF.Abs(left.HorizontalSpeed - right.HorizontalSpeed) <= 1f
            && MathF.Abs(left.VerticalSpeed - right.VerticalSpeed) <= 1f
            && left.IsGrounded == right.IsGrounded
            && left.RemainingAirJumps == right.RemainingAirJumps;
    }

    private static float GetGraphBakePriority(MotionState origin, MotionState state, MotionCoverageTracker coverageTracker)
    {
        var distanceToCoverage = coverageTracker.GetDistanceToNearestUncovered(state.X, state.Bottom);
        if (distanceToCoverage.HasValue)
        {
            var coverageDistanceFromOrigin = Distance(origin.X, origin.Bottom, state.X, state.Bottom);
            return distanceToCoverage.Value + (coverageDistanceFromOrigin * 0.02f);
        }

        var distanceFromOrigin = Distance(origin.X, origin.Bottom, state.X, state.Bottom);
        var horizontalSpread = MathF.Abs(state.X - origin.X);
        return -(distanceFromOrigin + (horizontalSpread * 0.35f));
    }

    private static int RunGraphProof(
        SimulationWorld world,
        PlayerEntity bot,
        MotionProofOptions options,
        Stopwatch stopwatch)
    {
        if (string.IsNullOrWhiteSpace(options.GraphPath))
        {
            Console.Error.WriteLine("graph_proof_requires_graph");
            return 2;
        }

        if (!options.GoalX.HasValue || !options.GoalY.HasValue)
        {
            Console.Error.WriteLine("graph_proof_requires_goal");
            return 2;
        }

        var graph = ReadJsonArtifact<MotionGraphArtifact>(options.GraphPath);
        if (graph is null || graph.Nodes.Count == 0)
        {
            Console.Error.WriteLine("graph_load_failed");
            return 2;
        }

        var startCandidates = FindGraphNodesWithinRadius(graph.Nodes, bot.X, bot.Bottom, options.GraphAttachRadius, 128, preferStable: false);
        if (startCandidates.Length == 0)
        {
            var nearestStart = FindNearestGraphNodes(graph.Nodes, bot.X, bot.Bottom, 1, preferStable: false)[0];
            var nearestNode = graph.Nodes[nearestStart];
            Console.WriteLine($"motion-proof graph-proof status=fail reason=no_local_start nearest={nearestStart} startNode=({nearestNode.X:0.0},{nearestNode.Bottom:0.0}) distance={Distance(nearestNode.X, nearestNode.Bottom, bot.X, bot.Bottom):0.0} attachRadius={options.GraphAttachRadius:0.0}");
            return 3;
        }

        var goalCandidates = FindGraphNodesWithinRadius(graph.Nodes, options.GoalX.Value, options.GoalY.Value, options.GoalRadius, 64);
        if (goalCandidates.Length == 0)
        {
            goalCandidates = FindNearestGraphNodes(graph.Nodes, options.GoalX.Value, options.GoalY.Value, 8, preferStable: false);
        }
        var graphIndex = BuildGraphSearchIndex(graph);
        var startIndex = -1;
        var goalIndex = -1;
        MotionPath path = MotionPath.Empty;
        foreach (var candidateStart in startCandidates)
        {
            if (TryFindGraphPathToAnyGoal(
                    graph,
                    graphIndex,
                    candidateStart,
                    goalCandidates,
                    options.GoalX.Value,
                    options.GoalY.Value,
                    options.Team,
                    carryingIntel: false,
                    out path,
                    out var reachedGoal))
            {
                startIndex = candidateStart;
                goalIndex = reachedGoal;
                break;
            }
        }

        if (startIndex < 0 || goalIndex < 0)
        {
            var nearestStart = graph.Nodes[startCandidates[0]];
            var nearestGoal = graph.Nodes[goalCandidates[0]];
            Console.WriteLine($"motion-proof graph-proof status=fail reason=no_path start={startCandidates[0]} startNode=({nearestStart.X:0.0},{nearestStart.Bottom:0.0}) goal={goalCandidates[0]} goalNode=({nearestGoal.X:0.0},{nearestGoal.Bottom:0.0})");
            return 3;
        }

        var startNode = graph.Nodes[startIndex];
        var goalNode = graph.Nodes[goalIndex];
        Console.WriteLine(
            $"motion-proof graph-proof status=found start={startIndex} startNode=({startNode.X:0.0},{startNode.Bottom:0.0}) goal={goalIndex} goalNode=({goalNode.X:0.0},{goalNode.Bottom:0.0}) actions={path.Actions.Count} ticks={path.TotalTicks}");
        ReplayPath(world, bot, options.Team, path, "graph");
        var distance = Distance(bot.X, bot.Bottom, options.GoalX.Value, options.GoalY.Value);
        var completed = distance <= options.GoalRadius;
        Console.WriteLine(
            $"motion-proof graph-proof result status={(completed ? "pass" : "fail")} distance={distance:0.0} radius={options.GoalRadius:0.0} elapsedMs={stopwatch.ElapsedMilliseconds} final=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
        return completed ? 0 : 3;
    }

    private static int RunCombatSmoke(MotionProofOptions options, Stopwatch stopwatch)
    {
        if (string.IsNullOrWhiteSpace(options.GraphPath))
        {
            Console.Error.WriteLine("combat_smoke_requires_graph");
            return 2;
        }

        var graph = ReadJsonArtifact<MotionGraphArtifact>(options.GraphPath);
        if (graph is null || graph.Nodes.Count == 0)
        {
            Console.Error.WriteLine("graph_load_failed");
            return 2;
        }

        var random = new Random(options.SmokeSeed);
        var passCount = 0;
        for (var trial = 1; trial <= options.SmokeTrials; trial += 1)
        {
            var world = CreateWorld(options, out var bot, out var failureReason);
            if (world is null)
            {
                Console.Error.WriteLine(failureReason);
                return 2;
            }

            if (!TryCreateSmokeEnemy(world, options, out var enemy))
            {
                Console.Error.WriteLine("failed_to_spawn_smoke_enemy");
                return 2;
            }

            var enemyDefinition = CharacterClassCatalog.GetDefinition(options.EnemyClassId);
            var candidates = BuildCombatSmokeTargets(world.Level, enemyDefinition, random, maxCandidates: 48);
            if (candidates.Length == 0)
            {
                Console.Error.WriteLine("combat_smoke_no_walkable_targets");
                return 2;
            }

            var startAnchor = FindNearestGraphNode(graph.Nodes, bot.X, bot.Bottom);
            var reachable = BuildGraphReachability(graph, startAnchor);
            var target = default(MotionGoal);
            var path = MotionPath.Empty;
            var goalIndex = -1;
            var fairCandidates = 0;
            foreach (var candidate in candidates)
            {
                if (!TryBuildReachableCombatPath(
                        world,
                        graph,
                        reachable,
                        bot,
                        candidate,
                        options.ClassId,
                        out var candidatePath,
                        out var candidateGoalIndex))
                {
                    continue;
                }

                target = candidate;
                path = candidatePath;
                goalIndex = candidateGoalIndex;
                fairCandidates += 1;
                break;
            }

            if (goalIndex < 0)
            {
                Console.WriteLine(
                    $"motion-proof combat-smoke trial={trial} result=fail reason=no_fair_reachable_target sampled={candidates.Length} reachableNodes={reachable.CostByNode.Count}");
                continue;
            }

            TeleportGrounded(enemy, target.X, target.Y, enemyDefinition);
            ReplayPath(world, bot, options.Team, path, $"combat-trial-{trial}-approach");
            var approachDistance = Distance(bot.X, bot.Bottom, enemy.X, enemy.Bottom);
            var killed = RunDirectCombat(world, bot, enemy, options.SmokeCombatTicks);
            Console.WriteLine(
                $"motion-proof combat-smoke trial={trial} target=({target.X:0.0},{target.Y:0.0}) start={startAnchor} goal={goalIndex} fairCandidates={fairCandidates} sampled={candidates.Length} reachableNodes={reachable.CostByNode.Count} approachTicks={path.TotalTicks} approachDistance={approachDistance:0.0} enemyHealth={enemy.Health} enemyAlive={enemy.IsAlive} result={(killed ? "pass" : "fail")}");
            if (killed)
            {
                passCount += 1;
            }
        }

        var passed = passCount == options.SmokeTrials;
        Console.WriteLine(
            $"motion-proof combat-smoke result status={(passed ? "pass" : "fail")} passed={passCount}/{options.SmokeTrials} elapsedMs={stopwatch.ElapsedMilliseconds}");
        return passed ? 0 : 3;
    }

    private static bool TryCreateSmokeEnemy(SimulationWorld world, MotionProofOptions options, out PlayerEntity enemy)
    {
        enemy = default!;
        var enemyTeam = GetOpposingTeam(options.Team);
        return world.TryPrepareNetworkPlayerJoin((byte)EnemySlot)
            && world.TrySetNetworkPlayerTeam((byte)EnemySlot, enemyTeam)
            && world.TryApplyNetworkPlayerClassSelection((byte)EnemySlot, options.EnemyClassId)
            && world.TryGetNetworkPlayer((byte)EnemySlot, out enemy);
    }

    private static MotionGoal[] BuildCombatSmokeTargets(
        SimpleLevel level,
        CharacterClassDefinition classDefinition,
        Random random,
        int maxCandidates)
    {
        var allTargets = BuildAllWalkableGoals(level, classDefinition, stride: 160f, width: 72f, height: 72f, labelPrefix: "combat").ToArray();
        return allTargets
            .OrderBy(_ => random.Next())
            .Take(Math.Min(maxCandidates, allTargets.Length))
            .ToArray();
    }

    private static bool TryBuildGraphPathFromPositions(
        MotionGraphArtifact graph,
        float startX,
        float startBottom,
        float goalX,
        float goalBottom,
        out MotionPath path,
        out int startIndex,
        out int goalIndex)
    {
        path = MotionPath.Empty;
        startIndex = -1;
        goalIndex = -1;
        var graphIndex = BuildGraphSearchIndex(graph);
        var startCandidates = FindNearestGraphNodes(graph.Nodes, startX, startBottom, 1);
        var goalCandidates = FindGraphNodesWithinRadius(graph.Nodes, goalX, goalBottom, 160f, 16);
        if (goalCandidates.Length == 0)
        {
            goalCandidates = FindNearestGraphNodes(graph.Nodes, goalX, goalBottom, 4);
        }
        var bestDistance = float.MaxValue;
        var bestTicks = int.MaxValue;
        foreach (var candidateStart in startCandidates)
        {
            if (!TryFindGraphPathToAnyGoal(
                    graph,
                    graphIndex,
                    candidateStart,
                    goalCandidates,
                    goalX,
                    goalBottom,
                    PlayerTeam.Blue,
                    carryingIntel: false,
                    out var candidatePath,
                    out var candidateGoal))
            {
                continue;
            }

            var candidateNode = graph.Nodes[candidateGoal];
            var candidateDistance = Distance(candidateNode.X, candidateNode.Bottom, goalX, goalBottom);
            if (candidateDistance > bestDistance
                || (MathF.Abs(candidateDistance - bestDistance) <= 0.1f && candidatePath.TotalTicks >= bestTicks))
            {
                continue;
            }

            bestDistance = candidateDistance;
            bestTicks = candidatePath.TotalTicks;
            path = candidatePath;
            startIndex = candidateStart;
            goalIndex = candidateGoal;
        }

        return startIndex >= 0;
    }

    private static bool TryBuildReachableCombatPath(
        SimulationWorld world,
        MotionGraphArtifact graph,
        GraphReachability reachable,
        PlayerEntity bot,
        MotionGoal target,
        PlayerClass botClass,
        out MotionPath path,
        out int goalIndex)
    {
        path = MotionPath.Empty;
        goalIndex = -1;
        var candidates = FindNearestGraphNodes(graph.Nodes, target.X, target.Y, 32);
        var bestScore = float.MaxValue;
        var bestTicks = int.MaxValue;
        foreach (var candidate in candidates)
        {
            if (!reachable.CostByNode.TryGetValue(candidate, out var costTicks))
            {
                continue;
            }

            var node = graph.Nodes[candidate];
            var distanceToEnemy = Distance(node.X, node.Bottom, target.X, target.Y);
            if (distanceToEnemy > GetMaximumFireRange(botClass))
            {
                continue;
            }

            if (!HasLineOfSight(world, node.X, node.Y, target.X, target.Y - 24f, bot.Team, carryingIntel: false))
            {
                continue;
            }

            var score = distanceToEnemy + (costTicks * 0.02f);
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestTicks = costTicks;
            goalIndex = candidate;
        }

        if (goalIndex < 0)
        {
            return false;
        }

        path = BuildPathFromReachability(reachable, goalIndex);
        return path.Actions.Count > 0 || bestTicks == 0;
    }

    private static bool RunDirectCombat(SimulationWorld world, PlayerEntity bot, PlayerEntity enemy, int ticks)
    {
        var previousBotInput = default(PlayerInputSnapshot);
        for (var tick = 0; tick < ticks && enemy.IsAlive; tick += 1)
        {
            var input = BuildCombatInput(world, bot, enemy);
            _ = input.Up && !previousBotInput.Up;
            if (!world.TrySetNetworkPlayerInput((byte)BotSlot, input))
            {
                throw new InvalidOperationException("failed_to_apply_combat_input");
            }

            world.TrySetNetworkPlayerInput((byte)EnemySlot, BuildIdleCombatTargetInput(enemy));
            world.AdvanceOneTick();
            previousBotInput = input;
        }

        return !enemy.IsAlive;
    }

    private static PlayerInputSnapshot BuildCombatInput(SimulationWorld world, PlayerEntity bot, PlayerEntity enemy)
    {
        var aimX = enemy.X;
        var aimY = GetAimTargetY(enemy);
        var distance = Distance(bot.X, bot.Y, enemy.X, enemy.Y);
        var hasLineOfSight = HasCombatLineOfSight(world, bot, enemy);
        var desiredRange = GetDesiredCombatRange(bot.ClassId);
        var moveDirection = 0;
        if (distance > desiredRange)
        {
            moveDirection = Math.Sign(enemy.X - bot.X);
        }

        return new PlayerInputSnapshot(
            Left: moveDirection < 0,
            Right: moveDirection > 0,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: hasLineOfSight && distance <= GetMaximumFireRange(bot.ClassId),
            FireSecondary: false,
            AimWorldX: aimX,
            AimWorldY: aimY,
            DebugKill: false);
    }

    private static PlayerInputSnapshot BuildIdleCombatTargetInput(PlayerEntity enemy)
    {
        return new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: enemy.X,
            AimWorldY: enemy.Y,
            DebugKill: false);
    }

    private static float GetDesiredCombatRange(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Pyro => 135f,
            PlayerClass.Heavy => 260f,
            PlayerClass.Soldier => 330f,
            _ => 260f,
        };
    }

    private static float GetMaximumFireRange(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Pyro => 230f,
            PlayerClass.Heavy => 520f,
            PlayerClass.Soldier => 700f,
            _ => 520f,
        };
    }

    private static void TeleportGrounded(PlayerEntity player, float x, float bottom, CharacterClassDefinition classDefinition)
    {
        player.TeleportTo(x, bottom - classDefinition.CollisionBottom);
        SetPrivateInstanceProperty(player, nameof(PlayerEntity.IsGrounded), true);
        SetPrivateInstanceProperty(player, nameof(PlayerEntity.RemainingAirJumps), player.MaxAirJumps);
    }

    private static bool HasCombatLineOfSight(SimulationWorld world, PlayerEntity origin, PlayerEntity target)
    {
        return HasLineOfSight(
            world,
            origin.X,
            origin.Y,
            target.X,
            GetAimTargetY(target),
            origin.Team,
            origin.IsCarryingIntel);
    }

    private static bool HasLineOfSight(
        SimulationWorld world,
        float originX,
        float originY,
        float targetX,
        float targetY,
        PlayerTeam team,
        bool carryingIntel)
    {
        var distance = Distance(originX, originY, targetX, targetY);
        if (distance <= 0.0001f)
        {
            return true;
        }

        var directionX = (targetX - originX) / distance;
        var directionY = (targetY - originY) / distance;
        var lineLeft = MathF.Min(originX, targetX);
        var lineTop = MathF.Min(originY, targetY);
        var lineRight = MathF.Max(originX, targetX);
        var lineBottom = MathF.Max(originY, targetY);
        foreach (var solid in world.Level.Solids)
        {
            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, solid.Left, solid.Top, solid.Right, solid.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    solid.Left,
                    solid.Top,
                    solid.Right,
                    solid.Bottom,
                    distance).HasValue)
            {
                return false;
            }
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, carryingIntel))
        {
            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    gate.Left,
                    gate.Top,
                    gate.Right,
                    gate.Bottom,
                    distance).HasValue)
            {
                return false;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if (wall.Type != RoomObjectType.PlayerWall && wall.Type != RoomObjectType.BulletWall)
            {
                continue;
            }

            if (!RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                continue;
            }

            if (GetRayIntersectionDistanceWithRectangle(
                    originX,
                    originY,
                    directionX,
                    directionY,
                    wall.Left,
                    wall.Top,
                    wall.Right,
                    wall.Bottom,
                    distance).HasValue)
            {
                return false;
            }
        }

        return true;
    }

    private static float GetAimTargetY(PlayerEntity target)
    {
        return target.Y - (target.Height / 4f);
    }

    private static bool RectanglesOverlap(
        float leftA,
        float topA,
        float rightA,
        float bottomA,
        float leftB,
        float topB,
        float rightB,
        float bottomB)
    {
        return leftA < rightB && rightA > leftB && topA < bottomB && bottomA > topB;
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
        var tMin = 0f;
        var tMax = maxDistance;
        if (!ClipRaySlab(originX, directionX, left, right, ref tMin, ref tMax)
            || !ClipRaySlab(originY, directionY, top, bottom, ref tMin, ref tMax))
        {
            return null;
        }

        return tMin >= 0f && tMin <= maxDistance ? tMin : null;
    }

    private static bool ClipRaySlab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(direction) < 0.0001f)
        {
            return origin >= min && origin <= max;
        }

        var inverse = 1f / direction;
        var t1 = (min - origin) * inverse;
        var t2 = (max - origin) * inverse;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        return tMin <= tMax;
    }

    private static int RunKothProof(
        SimulationWorld world,
        PlayerEntity bot,
        MotionProofOptions options,
        Stopwatch stopwatch)
    {
        var targetPoint = SelectKothTargetPoint(world, options.Team);
        if (targetPoint is null)
        {
            Console.WriteLine("motion-proof koth status=fail reason=missing_control_point");
            return 3;
        }

        var initialRedTimer = world.KothRedTimerTicksRemaining;
        var initialBlueTimer = world.KothBlueTimerTicksRemaining;
        var goal = SelectKothCaptureGoal(world, targetPoint);
        if (!TryFindPrimitivePath(
                world.Level,
                options.Team,
                options.ClassId,
                carryingIntel: false,
                MotionState.FromPlayer(bot),
                goal,
                options,
                searchBudgetMillisecondsOverride: null,
                out var path,
                out var stats))
        {
            Console.WriteLine($"motion-proof koth status=fail {stats}");
            return 3;
        }

        Console.WriteLine($"motion-proof koth status=found point={targetPoint.Index} actions={path.Actions.Count} ticks={path.TotalTicks} {stats}");
        Console.WriteLine($"motion-proof koth tape={FormatActionSequence(path)}");
        ReplayPath(world, bot, options.Team, path, "koth");
        if (!world.IsPlayerInControlPointCaptureZone(bot, targetPoint.Index))
        {
            HoldInput(world, 90);
            Console.WriteLine(
                $"motion-proof koth settle ticks=90 pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
        }

        if (!world.IsPlayerInControlPointCaptureZone(bot, targetPoint.Index))
        {
            Console.WriteLine(
                $"motion-proof koth result status=fail reason=not_in_capture_zone pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
            return 3;
        }

        var holdTicks = Math.Max(0, world.KothUnlockTicksRemaining) + Math.Max(1, targetPoint.CapTimeTicks) + 90;
        HoldInput(world, holdTicks);
        var timerAdvanced = options.Team == PlayerTeam.Blue
            ? world.KothBlueTimerTicksRemaining < initialBlueTimer
            : world.KothRedTimerTicksRemaining < initialRedTimer;
        var completed = targetPoint.Team == options.Team && timerAdvanced;
        Console.WriteLine(
            $"motion-proof koth result status={(completed ? "pass" : "fail")} pointTeam={targetPoint.Team?.ToString() ?? "None"} timers={world.KothRedTimerTicksRemaining}-{world.KothBlueTimerTicksRemaining} initialTimers={initialRedTimer}-{initialBlueTimer} holdTicks={holdTicks} elapsedMs={stopwatch.ElapsedMilliseconds} final=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0}");
        if (completed && !string.IsNullOrWhiteSpace(options.OutputPath))
        {
            WriteProofArtifact(options.OutputPath, world, options, path, MotionPath.Empty);
            Console.WriteLine($"motion-proof artifact={Path.GetFullPath(options.OutputPath)}");
        }

        return completed ? 0 : 3;
    }

    private static ControlPointState? SelectKothTargetPoint(SimulationWorld world, PlayerTeam team)
    {
        if (world.MatchRules.Mode == GameModeKind.DoubleKingOfTheHill)
        {
            return world.ControlPoints.FirstOrDefault(point =>
                team == PlayerTeam.Blue
                    ? point.Marker.IsRedKothControlPoint()
                    : point.Marker.IsBlueKothControlPoint());
        }

        return world.ControlPoints.FirstOrDefault(point => point.Marker.IsSingleKothControlPoint())
            ?? (world.ControlPoints.Count > 0 ? world.ControlPoints[0] : null);
    }

    private static MotionGoal SelectKothCaptureGoal(SimulationWorld world, ControlPointState targetPoint)
    {
        var captureZones = world.Level.GetRoomObjects(RoomObjectType.CaptureZone);
        if (captureZones.Count > 0)
        {
            var zone = captureZones
                .OrderBy(candidate => Distance(candidate.CenterX, candidate.CenterY, targetPoint.Marker.CenterX, targetPoint.Marker.CenterY))
                .First();
            return new MotionGoal(
                zone.CenterX,
                zone.CenterY,
                MathF.Max(42f, zone.Width),
                MathF.Max(42f, zone.Height),
                $"capture_zone_for_point_{targetPoint.Index}");
        }

        return new MotionGoal(
            targetPoint.Marker.CenterX,
            targetPoint.Marker.CenterY,
            MathF.Max(42f, targetPoint.Marker.Width),
            MathF.Max(42f, targetPoint.Marker.Height),
            $"control_point_{targetPoint.Index}");
    }

    private static bool IsKothMode(GameModeKind mode)
    {
        return mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill;
    }

    private static SimulationWorld? CreateWorld(MotionProofOptions options, out PlayerEntity bot, out string failureReason)
    {
        bot = default!;
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        if (!world.TryLoadLevel(options.MapName, options.MapAreaIndex, preservePlayerStats: false))
        {
            failureReason = $"failed_to_load_level:{options.MapName}:a{options.MapAreaIndex}";
            return null;
        }

        world.PrepareLocalPlayerJoin();
        if (!world.TryPrepareNetworkPlayerJoin(BotSlot)
            || !world.TrySetNetworkPlayerTeam(BotSlot, options.Team)
            || !world.TryApplyNetworkPlayerClassSelection(BotSlot, options.ClassId)
            || !world.TryGetNetworkPlayer(BotSlot, out bot))
        {
            failureReason = "failed_to_spawn_probe_player";
            return null;
        }

        if (options.StartX.HasValue && options.StartBottom.HasValue)
        {
            var classDefinition = CharacterClassCatalog.GetDefinition(options.ClassId);
            bot.TeleportTo(options.StartX.Value, options.StartBottom.Value - classDefinition.CollisionBottom);
            SetPrivateInstanceProperty(bot, nameof(PlayerEntity.IsGrounded), true);
            SetPrivateInstanceProperty(bot, nameof(PlayerEntity.RemainingAirJumps), bot.MaxAirJumps);
        }

        failureReason = string.Empty;
        return world;
    }

    private static bool TryFindPrimitivePath(
        SimpleLevel level,
        PlayerTeam team,
        PlayerClass classId,
        bool carryingIntel,
        MotionState start,
        MotionGoal goal,
        MotionProofOptions options,
        int? searchBudgetMillisecondsOverride,
        out MotionPath path,
        out string stats)
    {
        path = MotionPath.Empty;
        var classDefinition = CharacterClassCatalog.GetDefinition(classId);
        var actions = BuildPrimitiveActions().ToArray();
        var queue = new PriorityQueue<SearchNode, float>();
        var startKey = MotionKey.From(start);
        var bestCostByKey = new Dictionary<MotionKey, int> { [startKey] = 0 };
        var nodes = new List<SearchNode>();
        var startNode = new SearchNode(0, ParentIndex: -1, start, default, CostTicks: 0);
        nodes.Add(startNode);
        queue.Enqueue(startNode, Heuristic(start, goal));

        var expanded = 0;
        var generated = 0;
        var rejected = 0;
        var bestDistance = DistanceToGoal(start, goal);
        var bestState = start;
        var stopwatch = Stopwatch.StartNew();
        var searchBudgetMilliseconds = searchBudgetMillisecondsOverride ?? options.SearchBudgetMilliseconds;
        while (queue.Count > 0
            && expanded < options.MaxExpandedNodes
            && stopwatch.ElapsedMilliseconds < searchBudgetMilliseconds)
        {
            var current = queue.Dequeue();
            if (current.Index >= nodes.Count || nodes[current.Index].CostTicks != current.CostTicks)
            {
                continue;
            }

            expanded += 1;
            if (IntersectsGoal(current.State, classDefinition, goal))
            {
                path = BuildPath(nodes, current.Index);
                stats =
                    $"expanded={expanded} generated={generated} rejected={rejected} best={bestDistance:0.0} {FormatBestState(bestState)} ms={stopwatch.ElapsedMilliseconds} goal={goal.Label}";
                return true;
            }

            foreach (var action in actions)
            {
                if (!TrySimulateAction(
                        level,
                        team,
                        classDefinition,
                        carryingIntel,
                        current.State,
                        action,
                        goal,
                        requireStableEnd: true,
                        out var nextState,
                        out var actualAction,
                        out var reachedGoal))
                {
                    rejected += 1;
                    continue;
                }

                generated += 1;
                var nextCost = current.CostTicks + actualAction.Ticks;
                if (reachedGoal)
                {
                    var goalNode = new SearchNode(nodes.Count, current.Index, nextState, actualAction, nextCost);
                    nodes.Add(goalNode);
                    path = BuildPath(nodes, goalNode.Index);
                    bestState = nextState;
                    bestDistance = 0f;
                    stats =
                        $"expanded={expanded} generated={generated} rejected={rejected} best={bestDistance:0.0} {FormatBestState(bestState)} ms={stopwatch.ElapsedMilliseconds} goal={goal.Label}";
                    return true;
                }

                var nextDistance = DistanceToGoal(nextState, goal);
                if (nextDistance < bestDistance)
                {
                    bestDistance = nextDistance;
                    bestState = nextState;
                }

                var key = MotionKey.From(nextState);
                if (bestCostByKey.TryGetValue(key, out var previousCost) && previousCost <= nextCost)
                {
                    continue;
                }

                bestCostByKey[key] = nextCost;
                var node = new SearchNode(nodes.Count, current.Index, nextState, actualAction, nextCost);
                nodes.Add(node);
                queue.Enqueue(node, nextCost + Heuristic(nextState, goal));
            }
        }

        stats =
            $"expanded={expanded} generated={generated} rejected={rejected} best={bestDistance:0.0} {FormatBestState(bestState)} ms={stopwatch.ElapsedMilliseconds} goal={goal.Label}";
        return false;
    }

    private static Dictionary<int, MotionPath> TryFindPrimitivePathsToGoals(
        SimpleLevel level,
        PlayerTeam team,
        PlayerClass classId,
        bool carryingIntel,
        MotionState start,
        MotionGoal[] goals,
        MotionProofOptions options,
        int? searchBudgetMillisecondsOverride,
        out string stats)
    {
        var pathsByGoalIndex = new Dictionary<int, MotionPath>();
        if (goals.Length == 0)
        {
            stats = "expanded=0 generated=0 rejected=0 ms=0";
            return pathsByGoalIndex;
        }

        var classDefinition = CharacterClassCatalog.GetDefinition(classId);
        var actions = BuildPrimitiveActions().ToArray();
        var queue = new PriorityQueue<SearchNode, float>();
        var startKey = MotionKey.From(start);
        var bestCostByKey = new Dictionary<MotionKey, int> { [startKey] = 0 };
        var nodes = new List<SearchNode>();
        var startNode = new SearchNode(0, ParentIndex: -1, start, default, CostTicks: 0);
        nodes.Add(startNode);
        queue.Enqueue(startNode, HeuristicToNearestGoal(start, goals, pathsByGoalIndex));

        var expanded = 0;
        var generated = 0;
        var rejected = 0;
        var stopwatch = Stopwatch.StartNew();
        var searchBudgetMilliseconds = searchBudgetMillisecondsOverride
            ?? GetMultiGoalSeedSearchBudgetMilliseconds(goals, options);
        while (queue.Count > 0
            && expanded < options.MaxExpandedNodes
            && stopwatch.ElapsedMilliseconds < searchBudgetMilliseconds
            && pathsByGoalIndex.Count < goals.Length)
        {
            var current = queue.Dequeue();
            if (current.Index >= nodes.Count || nodes[current.Index].CostTicks != current.CostTicks)
            {
                continue;
            }

            expanded += 1;
            TryRecordReachedGoals(current.Index);

            foreach (var action in actions)
            {
                if (!TrySimulateActionToAnyGoal(
                        level,
                        team,
                        classDefinition,
                        carryingIntel,
                        current.State,
                        action,
                        goals,
                        pathsByGoalIndex,
                        requireStableEnd: true,
                        out var nextState,
                        out var actualAction,
                        out var reachedGoalIndex))
                {
                    rejected += 1;
                    continue;
                }

                generated += 1;
                var nextCost = current.CostTicks + actualAction.Ticks;
                var node = new SearchNode(nodes.Count, current.Index, nextState, actualAction, nextCost);
                nodes.Add(node);
                if (reachedGoalIndex.HasValue && !pathsByGoalIndex.ContainsKey(reachedGoalIndex.Value))
                {
                    pathsByGoalIndex[reachedGoalIndex.Value] = BuildPath(nodes, node.Index);
                }

                TryRecordReachedGoals(node.Index);
                if (pathsByGoalIndex.Count >= goals.Length)
                {
                    break;
                }

                var key = MotionKey.From(nextState);
                if (bestCostByKey.TryGetValue(key, out var previousCost) && previousCost <= nextCost)
                {
                    continue;
                }

                bestCostByKey[key] = nextCost;
                queue.Enqueue(node, nextCost + HeuristicToNearestGoal(nextState, goals, pathsByGoalIndex));
            }
        }

        stats =
            $"expanded={expanded} generated={generated} rejected={rejected} ms={stopwatch.ElapsedMilliseconds} budgetMs={searchBudgetMilliseconds}";
        return pathsByGoalIndex;

        void TryRecordReachedGoals(int nodeIndex)
        {
            var state = nodes[nodeIndex].State;
            for (var goalIndex = 0; goalIndex < goals.Length; goalIndex += 1)
            {
                if (pathsByGoalIndex.ContainsKey(goalIndex)
                    || !IntersectsGoal(state, classDefinition, goals[goalIndex]))
                {
                    continue;
                }

                pathsByGoalIndex[goalIndex] = BuildPath(nodes, nodeIndex);
            }
        }
    }

    private static int GetMultiGoalSeedSearchBudgetMilliseconds(MotionGoal[] goals, MotionProofOptions options)
    {
        var walkableCount = goals.Count(IsWalkableCoverageGoal);
        var nonWalkableCount = goals.Length - walkableCount;
        var walkableBudget = walkableCount * options.WalkableSeedSearchBudgetMilliseconds;
        var objectiveBudget = nonWalkableCount > 0 ? options.SearchBudgetMilliseconds : 0;
        return Math.Max(1, Math.Max(objectiveBudget, walkableBudget));
    }

    private static float HeuristicToNearestGoal(
        MotionState state,
        MotionGoal[] goals,
        Dictionary<int, MotionPath> pathsByGoalIndex)
    {
        var best = float.MaxValue;
        for (var goalIndex = 0; goalIndex < goals.Length; goalIndex += 1)
        {
            if (pathsByGoalIndex.ContainsKey(goalIndex))
            {
                continue;
            }

            best = MathF.Min(best, Heuristic(state, goals[goalIndex]));
        }

        return best == float.MaxValue ? 0f : best;
    }

    private static bool TrySimulateActionToAnyGoal(
        SimpleLevel level,
        PlayerTeam team,
        CharacterClassDefinition classDefinition,
        bool carryingIntel,
        MotionState start,
        MotionAction action,
        MotionGoal[] goals,
        Dictionary<int, MotionPath> pathsByGoalIndex,
        bool requireStableEnd,
        out MotionState next,
        out MotionAction actualAction,
        out int? reachedGoalIndex)
    {
        var player = CreatePlayerFromState(classDefinition, team, carryingIntel, start);
        var previousInput = default(PlayerInputSnapshot);
        var startDistance = MathF.Abs(player.X - start.X) + MathF.Abs(player.Y - start.Y);
        var becameAirborne = !player.IsGrounded;
        actualAction = action;
        reachedGoalIndex = null;
        if (!player.IsAlive || startDistance > 4f)
        {
            next = default;
            return false;
        }

        for (var tick = 0; tick < action.Ticks; tick += 1)
        {
            var input = action.GetInput(tick, player);
            var jumpPressed = input.Up && !previousInput.Up;
            player.Advance(input, jumpPressed, level, team, TickSeconds);
            previousInput = input;
            if (!player.IsAlive)
            {
                next = default;
                return false;
            }

            var state = MotionState.FromPlayer(player);
            for (var goalIndex = 0; goalIndex < goals.Length; goalIndex += 1)
            {
                if (pathsByGoalIndex.ContainsKey(goalIndex)
                    || !IntersectsGoal(state, classDefinition, goals[goalIndex]))
                {
                    continue;
                }

                next = state;
                actualAction = action with { Ticks = tick + 1 };
                reachedGoalIndex = goalIndex;
                return true;
            }

            if (!player.IsGrounded)
            {
                becameAirborne = true;
            }

            if (ShouldStopPrimitiveOnLanding(action, tick, player, becameAirborne))
            {
                next = state;
                actualAction = action with { Ticks = tick + 1 };
                return true;
            }
        }

        next = MotionState.FromPlayer(player);
        if (requireStableEnd && ShouldRequireLanding(action) && !next.IsGrounded)
        {
            return false;
        }

        var movedDistance = Distance(next.X, next.Bottom, start.X, start.Bottom);
        return movedDistance >= MinimumPrimitiveProgressDistance;
    }

    private static IEnumerable<MotionAction> BuildPrimitiveActions()
    {
        foreach (var direction in new[] { -1, 1 })
        {
            foreach (var ticks in new[] { 8, 14, 22, 34, 52 })
            {
                yield return MotionAction.Run(direction, ticks);
            }

            foreach (var ticks in new[] { 120, 180, 240 })
            {
                yield return MotionAction.Jump(direction, ticks);
            }

            foreach (var ticks in new[] { 120, 180, 240 })
            {
                yield return MotionAction.Drop(direction, ticks);
            }

            foreach (var ticks in new[] { 120, 180, 240 })
            {
                yield return MotionAction.Fall(direction, ticks);
            }
        }

        foreach (var ticks in new[] { 120, 180 })
        {
            yield return MotionAction.Jump(0, ticks);
        }
    }

    private static string FormatBestState(MotionState state)
    {
        return $"bestState=({state.X:0.0},{state.Y:0.0}) bottom={state.Bottom:0.0} grounded={state.IsGrounded} air={state.RemainingAirJumps}";
    }

    private static bool TrySimulateAction(
        SimpleLevel level,
        PlayerTeam team,
        CharacterClassDefinition classDefinition,
        bool carryingIntel,
        MotionState start,
        MotionAction action,
        MotionGoal? goal,
        bool requireStableEnd,
        out MotionState next,
        out MotionAction actualAction,
        out bool reachedGoal)
    {
        var player = CreatePlayerFromState(classDefinition, team, carryingIntel, start);
        var previousInput = default(PlayerInputSnapshot);
        var startDistance = MathF.Abs(player.X - start.X) + MathF.Abs(player.Y - start.Y);
        var becameAirborne = !player.IsGrounded;
        actualAction = action;
        reachedGoal = false;
        if (!player.IsAlive || startDistance > 4f)
        {
            next = default;
            return false;
        }

        for (var tick = 0; tick < action.Ticks; tick += 1)
        {
            var input = action.GetInput(tick, player);
            var jumpPressed = input.Up && !previousInput.Up;
            player.Advance(input, jumpPressed, level, team, TickSeconds);
            previousInput = input;
            if (!player.IsAlive)
            {
                next = default;
                return false;
            }

            if (goal.HasValue && IntersectsGoal(MotionState.FromPlayer(player), classDefinition, goal.Value))
            {
                next = MotionState.FromPlayer(player);
                actualAction = action with { Ticks = tick + 1 };
                reachedGoal = true;
                return true;
            }

            if (!player.IsGrounded)
            {
                becameAirborne = true;
            }

            if (ShouldStopPrimitiveOnLanding(action, tick, player, becameAirborne))
            {
                next = MotionState.FromPlayer(player);
                actualAction = action with { Ticks = tick + 1 };
                return true;
            }
        }

        next = MotionState.FromPlayer(player);
        if (requireStableEnd && ShouldRequireLanding(action) && !next.IsGrounded)
        {
            return false;
        }

        var movedDistance = Distance(next.X, next.Bottom, start.X, start.Bottom);
        return movedDistance >= MinimumPrimitiveProgressDistance;
    }

    private static bool ShouldStopPrimitiveOnLanding(MotionAction action, int tick, PlayerEntity player, bool becameAirborne)
    {
        if (!ShouldRequireLanding(action) || !becameAirborne || tick < 6)
        {
            return false;
        }

        return player.IsGrounded && player.VerticalSpeed >= 0f;
    }

    private static bool ShouldRequireLanding(MotionAction action)
    {
        return action.Kind is MotionActionKind.Jump or MotionActionKind.Drop or MotionActionKind.Fall;
    }

    private static PlayerEntity CreatePlayerFromState(
        CharacterClassDefinition classDefinition,
        PlayerTeam team,
        bool carryingIntel,
        MotionState state)
    {
        var player = new PlayerEntity(id: 1, classDefinition, displayName: "motion-proof");
        player.Spawn(team, state.X, state.Y);
        player.TeleportTo(state.X, state.Y);
        player.AddImpulse(state.HorizontalSpeed, state.VerticalSpeed);
        SetPrivateInstanceProperty(player, nameof(PlayerEntity.IsGrounded), state.IsGrounded);
        SetPrivateInstanceProperty(player, nameof(PlayerEntity.RemainingAirJumps), state.RemainingAirJumps);
        SetPrivateInstanceProperty(player, "LegacyStateTickAccumulator", state.LegacyStateTickAccumulator);
        player.SetMovementState(state.MovementState);
        SetPrivateInstanceProperty(player, nameof(PlayerEntity.FacingDirectionX), state.FacingDirectionX);
        SetPrivateInstanceProperty(player, nameof(PlayerEntity.AimDirectionDegrees), state.AimDirectionDegrees);
        SetPrivateInstanceProperty(player, "SourceFacingDirectionX", state.SourceFacingDirectionX);
        SetPrivateInstanceProperty(player, "PreviousSourceFacingDirectionX", state.PreviousSourceFacingDirectionX);
        if (carryingIntel)
        {
            player.PickUpIntel(0f);
        }

        return player;
    }

    private static MotionPath BuildPath(List<SearchNode> nodes, int nodeIndex)
    {
        var actions = new List<MotionAction>();
        var current = nodeIndex;
        while (current >= 0)
        {
            var node = nodes[current];
            if (node.ParentIndex >= 0)
            {
                actions.Add(node.Action);
            }

            current = node.ParentIndex;
        }

        actions.Reverse();
        return new MotionPath(actions);
    }

    private static string FormatActionSequence(MotionPath path)
    {
        return string.Join(
            ",",
            path.Actions.Select(action => $"{action.Kind}:{action.Direction}:{action.Ticks}"));
    }

    private static int FindNearestGraphNode(IReadOnlyList<MotionGraphNode> nodes, float x, float bottom)
    {
        var bestIndex = 0;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < nodes.Count; index += 1)
        {
            var node = nodes[index];
            var distance = Distance(node.X, node.Bottom, x, bottom);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static int[] FindNearestGraphNodes(
        IReadOnlyList<MotionGraphNode> nodes,
        float x,
        float bottom,
        int count,
        bool preferStable = false)
    {
        return nodes
            .Select((node, index) => new
            {
                Index = index,
                Distance = Distance(node.X, node.Bottom, x, bottom),
                Score = Distance(node.X, node.Bottom, x, bottom) + (preferStable && !IsStableGraphNode(node) ? 10000f : 0f),
            })
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => entry.Distance)
            .Take(Math.Min(count, nodes.Count))
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static int[] FindGraphNodesWithinRadius(
        IReadOnlyList<MotionGraphNode> nodes,
        float x,
        float bottom,
        float radius,
        int maxCount,
        bool preferStable = false)
    {
        return nodes
            .Select((node, index) => new
            {
                Index = index,
                Distance = Distance(node.X, node.Bottom, x, bottom),
                Score = Distance(node.X, node.Bottom, x, bottom) + (preferStable && !IsStableGraphNode(node) ? radius : 0f),
            })
            .Where(entry => entry.Distance <= radius)
            .OrderBy(entry => entry.Score)
            .ThenBy(entry => entry.Distance)
            .Take(Math.Min(maxCount, nodes.Count))
            .Select(entry => entry.Index)
            .ToArray();
    }

    private static bool IsStableGraphNode(MotionGraphNode node)
    {
        return node.IsGrounded
            && MathF.Abs(node.HorizontalSpeed) <= 5f
            && MathF.Abs(node.VerticalSpeed) <= 5f;
    }

    private static GraphSearchIndex BuildGraphSearchIndex(MotionGraphArtifact graph)
    {
        return new GraphSearchIndex(
            graph.Edges
                .GroupBy(edge => edge.From)
                .ToDictionary(group => group.Key, group => group.ToArray()));
    }

    private static bool TryFindGraphPathToAnyGoal(
        MotionGraphArtifact graph,
        GraphSearchIndex graphIndex,
        int startIndex,
        IReadOnlyCollection<int> goalIndices,
        float goalX,
        float goalBottom,
        PlayerTeam team,
        bool carryingIntel,
        out MotionPath path,
        out int reachedGoalIndex)
    {
        path = MotionPath.Empty;
        reachedGoalIndex = -1;
        if (goalIndices.Count == 0)
        {
            return false;
        }

        var terminalNodes = goalIndices is HashSet<int> set ? set : new HashSet<int>(goalIndices);
        var queue = new PriorityQueue<int, float>();
        var costByNode = new Dictionary<int, int> { [startIndex] = 0 };
        var previousEdgeByNode = new Dictionary<int, MotionGraphEdge>();
        var settled = new HashSet<int>();
        var bestGoalIndex = -1;
        var bestGoalDistance = float.MaxValue;
        var bestGoalCost = int.MaxValue;
        queue.Enqueue(startIndex, 0f);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!settled.Add(current))
            {
                continue;
            }

            if (terminalNodes.Contains(current))
            {
                var currentNode = graph.Nodes[current];
                var currentDistance = Distance(currentNode.X, currentNode.Bottom, goalX, goalBottom);
                var terminalCost = costByNode[current];
                if (currentDistance < bestGoalDistance - 0.1f
                    || (MathF.Abs(currentDistance - bestGoalDistance) <= 0.1f && terminalCost < bestGoalCost))
                {
                    bestGoalIndex = current;
                    bestGoalDistance = currentDistance;
                    bestGoalCost = terminalCost;
                }
            }

            if (!graphIndex.EdgesByNode.TryGetValue(current, out var edges))
            {
                continue;
            }

            var currentCost = costByNode[current];
            foreach (var edge in edges)
            {
                if (!IsEdgeAllowedForTeam(edge, team) || !IsEdgeAllowedForCarry(edge, carryingIntel))
                {
                    continue;
                }

                var nextCost = currentCost + edge.CostTicks;
                if (costByNode.TryGetValue(edge.To, out var previousCost) && previousCost <= nextCost)
                {
                    continue;
                }

                costByNode[edge.To] = nextCost;
                previousEdgeByNode[edge.To] = edge;
                var node = graph.Nodes[edge.To];
                queue.Enqueue(edge.To, nextCost + Distance(node.X, node.Bottom, goalX, goalBottom) * 0.18f);
            }
        }

        if (bestGoalIndex < 0)
        {
            return false;
        }

        reachedGoalIndex = bestGoalIndex;
        path = BuildPathFromPreviousEdges(previousEdgeByNode, startIndex, bestGoalIndex);
        return path.Actions.Count > 0;
    }

    private static bool TryFindGraphPath(
        MotionGraphArtifact graph,
        int startIndex,
        int goalIndex,
        float goalX,
        float goalBottom,
        out MotionPath path)
    {
        path = MotionPath.Empty;
        var graphIndex = BuildGraphSearchIndex(graph);
        var queue = new PriorityQueue<int, float>();
        var costByNode = new Dictionary<int, int> { [startIndex] = 0 };
        var previousEdgeByNode = new Dictionary<int, MotionGraphEdge>();
        var settled = new HashSet<int>();
        queue.Enqueue(startIndex, 0f);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!settled.Add(current))
            {
                continue;
            }

            if (current == goalIndex)
            {
                var actions = new List<MotionAction>();
                var node = goalIndex;
                while (node != startIndex && previousEdgeByNode.TryGetValue(node, out var edge))
                {
                    actions.Add(edge.Action.ToMotionAction());
                    node = edge.From;
                }

                actions.Reverse();
                path = new MotionPath(actions);
                return true;
            }

            if (!graphIndex.EdgesByNode.TryGetValue(current, out var edges))
            {
                continue;
            }

            var currentCost = costByNode[current];
            foreach (var edge in edges)
            {
                var nextCost = currentCost + edge.CostTicks;
                if (costByNode.TryGetValue(edge.To, out var previousCost) && previousCost <= nextCost)
                {
                    continue;
                }

                costByNode[edge.To] = nextCost;
                previousEdgeByNode[edge.To] = edge;
                var node = graph.Nodes[edge.To];
                queue.Enqueue(edge.To, nextCost + Distance(node.X, node.Bottom, goalX, goalBottom) * 0.18f);
            }
        }

        return false;
    }

    private static MotionPath BuildPathFromPreviousEdges(
        IReadOnlyDictionary<int, MotionGraphEdge> previousEdgeByNode,
        int startIndex,
        int goalIndex)
    {
        var actions = new List<MotionAction>();
        var node = goalIndex;
        while (node != startIndex && previousEdgeByNode.TryGetValue(node, out var edge))
        {
            actions.Add(edge.Action.ToMotionAction());
            node = edge.From;
        }

        actions.Reverse();
        return new MotionPath(actions);
    }

    private static bool IsEdgeAllowedForTeam(MotionGraphEdge edge, PlayerTeam team)
    {
        return edge.TeamMask.Equals("Any", StringComparison.OrdinalIgnoreCase)
            || edge.TeamMask.Equals(team.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEdgeAllowedForCarry(MotionGraphEdge edge, bool carryingIntel)
    {
        return edge.CarryMask.Equals("Any", StringComparison.OrdinalIgnoreCase)
            || edge.CarryMask.Equals(carryingIntel ? "Carrying" : "Free", StringComparison.OrdinalIgnoreCase);
    }

    private static GraphReachability BuildGraphReachability(MotionGraphArtifact graph, int startIndex)
    {
        var edgesByNode = graph.Edges
            .GroupBy(edge => edge.From)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var queue = new PriorityQueue<int, int>();
        var costByNode = new Dictionary<int, int> { [startIndex] = 0 };
        var previousEdgeByNode = new Dictionary<int, MotionGraphEdge>();
        var settled = new HashSet<int>();
        queue.Enqueue(startIndex, 0);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!settled.Add(current))
            {
                continue;
            }

            if (!edgesByNode.TryGetValue(current, out var edges))
            {
                continue;
            }

            var currentCost = costByNode[current];
            foreach (var edge in edges)
            {
                var nextCost = currentCost + edge.CostTicks;
                if (costByNode.TryGetValue(edge.To, out var previousCost) && previousCost <= nextCost)
                {
                    continue;
                }

                costByNode[edge.To] = nextCost;
                previousEdgeByNode[edge.To] = edge;
                queue.Enqueue(edge.To, nextCost);
            }
        }

        return new GraphReachability(startIndex, costByNode, previousEdgeByNode);
    }

    private static MotionPath BuildPathFromReachability(GraphReachability reachable, int goalIndex)
    {
        var actions = new List<MotionAction>();
        var node = goalIndex;
        while (node != reachable.StartIndex && reachable.PreviousEdgeByNode.TryGetValue(node, out var edge))
        {
            actions.Add(edge.Action.ToMotionAction());
            node = edge.From;
        }

        actions.Reverse();
        return new MotionPath(actions);
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool ValidateMirrorSource(
        SimulationWorld world,
        MotionProofOptions options,
        MotionProofArtifact sourceArtifact,
        out string message)
    {
        if (!sourceArtifact.Map.Equals(world.Level.Name, StringComparison.OrdinalIgnoreCase))
        {
            message = $"map_mismatch:{sourceArtifact.Map}!={world.Level.Name}";
            return false;
        }

        if (sourceArtifact.Area != world.Level.MapAreaIndex)
        {
            message = $"area_mismatch:{sourceArtifact.Area}!={world.Level.MapAreaIndex}";
            return false;
        }

        if (!sourceArtifact.Mode.Equals(world.MatchRules.Mode.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            message = $"mode_mismatch:{sourceArtifact.Mode}!={world.MatchRules.Mode}";
            return false;
        }

        if (!sourceArtifact.Class.Equals(options.ClassId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            message = $"class_mismatch:{sourceArtifact.Class}!={options.ClassId}";
            return false;
        }

        if (sourceArtifact.Team.Equals(options.Team.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            message = $"same_team:{sourceArtifact.Team}";
            return false;
        }

        if (sourceArtifact.Attack.Count == 0)
        {
            message = "empty_attack";
            return false;
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag && sourceArtifact.Return.Count == 0)
        {
            message = "empty_return";
            return false;
        }

        message = "ok";
        return true;
    }

    private static MotionAction[] MirrorActions(IReadOnlyList<MotionProofAction> actions)
    {
        var mirrored = new MotionAction[actions.Count];
        for (var index = 0; index < actions.Count; index += 1)
        {
            var action = actions[index].ToMotionAction();
            mirrored[index] = action with { Direction = -action.Direction };
        }

        return mirrored;
    }

    private static void WriteProofArtifact(
        string outputPath,
        SimulationWorld world,
        MotionProofOptions options,
        MotionPath attackPath,
        MotionPath returnPath)
    {
        var artifact = new MotionProofArtifact(
            Version: 1,
            Map: world.Level.Name,
            Area: world.Level.MapAreaIndex,
            Mode: world.MatchRules.Mode.ToString(),
            Team: options.Team.ToString(),
            Class: options.ClassId.ToString(),
            AttackTicks: attackPath.TotalTicks,
            ReturnTicks: returnPath.TotalTicks,
            Attack: attackPath.Actions.Select(MotionProofAction.From).ToArray(),
            Return: returnPath.Actions.Select(MotionProofAction.From).ToArray());
        WriteJsonArtifact(outputPath, artifact);
    }

    private static void WriteJsonArtifact<T>(string outputPath, T artifact)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (fullPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.Create(fullPath);
            using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
            JsonSerializer.Serialize(gzip, artifact, ArtifactJsonOptions);
            return;
        }

        using var stream = File.Create(fullPath);
        JsonSerializer.Serialize(stream, artifact, ArtifactJsonOptions);
    }

    private static T? ReadJsonArtifact<T>(string path)
    {
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.OpenRead(path);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<T>(gzip, ArtifactJsonOptions);
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, ArtifactJsonOptions);
    }

    private static void ReplayPath(SimulationWorld world, PlayerEntity bot, PlayerTeam team, MotionPath path, string phase)
    {
        var previousInput = default(PlayerInputSnapshot);
        var tick = 0;
        foreach (var action in path.Actions)
        {
            for (var actionTick = 0; actionTick < action.Ticks; actionTick += 1)
            {
                var input = action.GetInput(actionTick, bot);
                var jumpPressed = input.Up && !previousInput.Up;
                _ = jumpPressed;
                if (!world.TrySetNetworkPlayerInput(BotSlot, input))
                {
                    throw new InvalidOperationException("failed_to_apply_replay_input");
                }

                world.AdvanceOneTick();
                previousInput = input;
                tick += 1;
            }
        }

        Console.WriteLine(
            $"motion-proof replay phase={phase} ticks={tick} pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0} carry={bot.IsCarryingIntel} caps={world.RedCaps}-{world.BlueCaps}");
    }

    private static void HoldInput(SimulationWorld world, int ticks)
    {
        var input = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: 0f,
            AimWorldY: 0f,
            DebugKill: false);
        for (var tick = 0; tick < ticks; tick += 1)
        {
            if (!world.TrySetNetworkPlayerInput(BotSlot, input))
            {
                throw new InvalidOperationException("failed_to_apply_hold_input");
            }

            world.AdvanceOneTick();
        }
    }

    private static bool IntersectsGoal(MotionState state, CharacterClassDefinition classDefinition, MotionGoal goal)
    {
        var left = state.X + classDefinition.CollisionLeft;
        var right = state.X + classDefinition.CollisionRight;
        var top = state.Y + classDefinition.CollisionTop;
        var bottom = state.Y + classDefinition.CollisionBottom;
        var markerLeft = goal.X - (goal.Width / 2f);
        var markerRight = goal.X + (goal.Width / 2f);
        var markerTop = goal.Y - (goal.Height / 2f);
        var markerBottom = goal.Y + (goal.Height / 2f);
        return left < markerRight
            && right > markerLeft
            && top < markerBottom
            && bottom > markerTop;
    }

    private static float Heuristic(MotionState state, MotionGoal goal)
    {
        return DistanceToGoal(state, goal) * 0.18f;
    }

    private static float DistanceToGoal(MotionState state, MotionGoal goal)
    {
        var dx = state.X - goal.X;
        var dy = state.Bottom - goal.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    private static string GetMovementProfileId(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => "Scout",
            PlayerClass.Heavy => "Heavy",
            PlayerClass.Soldier or PlayerClass.Sniper => "Soldier",
            PlayerClass.Engineer or PlayerClass.Demoman => "Demoman",
            PlayerClass.Pyro or PlayerClass.Medic or PlayerClass.Spy => "Fast",
            PlayerClass.Quote => "Quote",
            _ => classId.ToString(),
        };
    }

    private static PlayerClass GetMovementProfileRepresentative(string profileId)
    {
        return profileId switch
        {
            var value when value.Equals("Scout", StringComparison.OrdinalIgnoreCase) => PlayerClass.Scout,
            var value when value.Equals("Heavy", StringComparison.OrdinalIgnoreCase) => PlayerClass.Heavy,
            var value when value.Equals("Soldier", StringComparison.OrdinalIgnoreCase) => PlayerClass.Soldier,
            var value when value.Equals("Demoman", StringComparison.OrdinalIgnoreCase) => PlayerClass.Demoman,
            var value when value.Equals("Fast", StringComparison.OrdinalIgnoreCase) => PlayerClass.Pyro,
            var value when value.Equals("Quote", StringComparison.OrdinalIgnoreCase) => PlayerClass.Quote,
            _ => Enum.TryParse<PlayerClass>(profileId, ignoreCase: true, out var classId) ? classId : PlayerClass.Pyro,
        };
    }

    private static int[] BuildWeakComponentIndex(int nodeCount, IReadOnlyList<MotionGraphEdge> edges)
    {
        var parent = new int[nodeCount];
        for (var index = 0; index < parent.Length; index += 1)
        {
            parent[index] = index;
        }

        foreach (var edge in edges)
        {
            if ((uint)edge.From >= (uint)nodeCount || (uint)edge.To >= (uint)nodeCount)
            {
                continue;
            }

            Union(edge.From, edge.To);
        }

        for (var index = 0; index < parent.Length; index += 1)
        {
            parent[index] = Find(index);
        }

        return parent;

        int Find(int value)
        {
            while (parent[value] != value)
            {
                parent[value] = parent[parent[value]];
                value = parent[value];
            }

            return value;
        }

        void Union(int left, int right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot != rightRoot)
            {
                parent[rightRoot] = leftRoot;
            }
        }
    }

    private static void SetPrivateInstanceProperty<T>(object target, string propertyName, T value)
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
}

internal sealed class MotionProofOptions
{
    public const string Usage =
        "usage: dotnet run --project MotionProof.Tools -- --map Truefort --team Blue --class Heavy [--max-expanded N] [--search-budget-ms N] [--area N] [--output path] [--mirror-artifact path] [--bake-graph] [--seed-walkable-grid N] [--seed-walkable-limit N] [--seed-search-budget-ms N] [--prove-graph --graph path --goal-x X --goal-y Y]";

    public string MapName { get; private set; } = "Truefort";
    public int MapAreaIndex { get; private set; } = 1;
    public PlayerTeam Team { get; private set; } = PlayerTeam.Blue;
    public PlayerClass ClassId { get; private set; } = PlayerClass.Heavy;
    public string? MovementProfileId { get; private set; }
    public int MaxExpandedNodes { get; private set; } = 80_000;
    public int GraphExpansionMaxNodes { get; private set; } = 16_000;
    public int SearchBudgetMilliseconds { get; private set; } = 20_000;
    public string? OutputPath { get; private set; }
    public string? MirrorArtifactPath { get; private set; }
    public bool BakeGraph { get; private set; }
    public bool WriteFailedGraph { get; private set; }
    public bool ProveGraph { get; private set; }
    public bool CombatSmoke { get; private set; }
    public string? GraphPath { get; private set; }
    public float? GoalX { get; private set; }
    public float? GoalY { get; private set; }
    public float GoalRadius { get; private set; } = 96f;
    public float GraphAttachRadius { get; private set; } = 192f;
    public float? StartX { get; private set; }
    public float? StartBottom { get; private set; }
    public int SmokeTrials { get; private set; } = 1;
    public int SmokeSeed { get; private set; } = 1337;
    public int SmokeCombatTicks { get; private set; } = 420;
    public PlayerClass EnemyClassId { get; private set; } = PlayerClass.Scout;
    public float? WalkableSeedStride { get; private set; }
    public int WalkableSeedLimit { get; private set; } = 24;
    public int WalkableSeedSearchBudgetMilliseconds { get; private set; } = 8_000;
    public float WalkableSeedCoveredRadius { get; private set; } = 96f;
    public float CoverageSampleStride { get; private set; } = 192f;
    public float CoverageSampleRadius { get; private set; } = 128f;
    public float CoverageTargetRatio { get; private set; } = 0.85f;
    public bool SeedCoverageAnchors { get; private set; } = true;
    public bool RequireObjectiveReachability { get; private set; } = true;
    public float ObjectiveReachabilityTargetRatio { get; private set; } = 0.85f;
    public bool SkipGameplaySeedGoals { get; private set; }
    public bool SkipObjectiveSeedPaths { get; private set; }
    public bool SeedCoverageConnectivity { get; private set; } = true;
    public int CoverageNeighborLinks { get; private set; } = 6;
    public float CoverageLinkMaxDistance { get; private set; } = 360f;
    public int CoverageLinkSearchBudgetMilliseconds { get; private set; } = 1_200;
    public bool SeedCoverageHubPaths { get; private set; } = true;
    public int CoverageHubSearchBudgetMilliseconds { get; private set; } = 2_500;
    public int CoverageHubFanoutSearchBudgetMilliseconds { get; private set; } = 8_000;
    public int CoverageGapSearchBudgetMilliseconds { get; private set; } = 1_500;
    public int CoverageGapMaxSamples { get; private set; } = 12;
    public int CoverageGapSamplesPerComponent { get; private set; } = 2;
    public int CoveragePathParallelism { get; private set; } = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
    public bool AddVerifiedReverseSeedPaths { get; private set; } = true;
    public float VerifiedReversePathTolerance { get; private set; } = 96f;
    public float CoverageAnchorWeldRadius { get; private set; } = 144f;
    public int CoverageAnchorWeldSearchBudgetMilliseconds { get; private set; } = 250;
    public bool UseMultiGoalSeedSearch { get; private set; }
    public List<MotionSeedStart> ExtraSeedStarts { get; } = new();
    public List<MotionSeedGoal> ExtraSeedGoals { get; } = new();
    public List<MotionSeedPath> ExplicitSeedPaths { get; } = new();
    public List<string> ProofSeedArtifactPaths { get; } = new();
    public bool ShowHelp { get; private set; }

    public static MotionProofOptions Parse(IReadOnlyList<string> args)
    {
        var options = new MotionProofOptions();
        for (var index = 0; index < args.Count; index += 1)
        {
            var arg = args[index];
            if (arg is "--help" or "-h")
            {
                options.ShowHelp = true;
                continue;
            }

            if (arg.Equals("--map", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.MapName = args[++index].Trim();
                continue;
            }

            if (arg.Equals("--area", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.MapAreaIndex = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--team", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.Team = Enum.Parse<PlayerTeam>(args[++index], ignoreCase: true);
                continue;
            }

            if (arg.Equals("--class", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.ClassId = Enum.Parse<PlayerClass>(args[++index], ignoreCase: true);
                options.MovementProfileId ??= GetDefaultMovementProfileId(options.ClassId);
                continue;
            }

            if (arg.Equals("--movement-profile", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.MovementProfileId = args[++index].Trim();
                options.ClassId = GetMovementProfileRepresentative(options.MovementProfileId);
                continue;
            }

            if (arg.Equals("--max-expanded", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.MaxExpandedNodes = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--graph-max-expanded", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.GraphExpansionMaxNodes = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--search-budget-ms", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.SearchBudgetMilliseconds = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.OutputPath = args[++index];
                continue;
            }

            if (arg.Equals("--mirror-artifact", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.MirrorArtifactPath = args[++index];
                continue;
            }

            if (arg.Equals("--bake-graph", StringComparison.OrdinalIgnoreCase))
            {
                options.BakeGraph = true;
                continue;
            }

            if (arg.Equals("--write-failed-graph", StringComparison.OrdinalIgnoreCase))
            {
                options.WriteFailedGraph = true;
                continue;
            }

            if (arg.Equals("--prove-graph", StringComparison.OrdinalIgnoreCase))
            {
                options.ProveGraph = true;
                continue;
            }

            if (arg.Equals("--combat-smoke", StringComparison.OrdinalIgnoreCase))
            {
                options.CombatSmoke = true;
                continue;
            }

            if (arg.Equals("--graph", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.GraphPath = args[++index];
                continue;
            }

            if (arg.Equals("--goal-x", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.GoalX = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--goal-y", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.GoalY = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--goal-radius", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.GoalRadius = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--graph-attach-radius", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.GraphAttachRadius = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--start-x", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.StartX = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--start-bottom", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.StartBottom = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--smoke-trials", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.SmokeTrials = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--smoke-seed", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.SmokeSeed = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--smoke-combat-ticks", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.SmokeCombatTicks = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--enemy-class", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.EnemyClassId = Enum.Parse<PlayerClass>(args[++index], ignoreCase: true);
                continue;
            }

            if (arg.Equals("--seed-walkable-grid", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.WalkableSeedStride = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--seed-walkable-limit", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.WalkableSeedLimit = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--seed-search-budget-ms", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.WalkableSeedSearchBudgetMilliseconds = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--seed-covered-radius", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.WalkableSeedCoveredRadius = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-sample-stride", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageSampleStride = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-sample-radius", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageSampleRadius = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-target-ratio", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageTargetRatio = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--no-coverage-anchors", StringComparison.OrdinalIgnoreCase))
            {
                options.SeedCoverageAnchors = false;
                continue;
            }

            if (arg.Equals("--no-objective-reachability-gate", StringComparison.OrdinalIgnoreCase))
            {
                options.RequireObjectiveReachability = false;
                continue;
            }

            if (arg.Equals("--skip-objective-seed-paths", StringComparison.OrdinalIgnoreCase))
            {
                options.SkipObjectiveSeedPaths = true;
                continue;
            }

            if (arg.Equals("--skip-gameplay-seed-goals", StringComparison.OrdinalIgnoreCase))
            {
                options.SkipGameplaySeedGoals = true;
                continue;
            }

            if (arg.Equals("--no-coverage-connectivity", StringComparison.OrdinalIgnoreCase))
            {
                options.SeedCoverageConnectivity = false;
                continue;
            }

            if (arg.Equals("--coverage-neighbor-links", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageNeighborLinks = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-link-max-distance", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageLinkMaxDistance = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-link-search-budget-ms", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageLinkSearchBudgetMilliseconds = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--no-coverage-hub-paths", StringComparison.OrdinalIgnoreCase))
            {
                options.SeedCoverageHubPaths = false;
                continue;
            }

            if (arg.Equals("--coverage-hub-search-budget-ms", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageHubSearchBudgetMilliseconds = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-hub-fanout-search-budget-ms", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageHubFanoutSearchBudgetMilliseconds = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-gap-search-budget-ms", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageGapSearchBudgetMilliseconds = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-gap-max-samples", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageGapMaxSamples = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-gap-samples-per-component", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageGapSamplesPerComponent = Math.Max(1, int.Parse(args[++index], CultureInfo.InvariantCulture));
                continue;
            }

            if (arg.Equals("--coverage-path-parallelism", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoveragePathParallelism = Math.Max(1, int.Parse(args[++index], CultureInfo.InvariantCulture));
                continue;
            }

            if (arg.Equals("--no-verified-reverse-seed-paths", StringComparison.OrdinalIgnoreCase))
            {
                options.AddVerifiedReverseSeedPaths = false;
                continue;
            }

            if (arg.Equals("--verified-reverse-path-tolerance", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.VerifiedReversePathTolerance = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-anchor-weld-radius", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageAnchorWeldRadius = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--coverage-anchor-weld-search-budget-ms", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.CoverageAnchorWeldSearchBudgetMilliseconds = int.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--objective-reachability-target-ratio", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.ObjectiveReachabilityTargetRatio = float.Parse(args[++index], CultureInfo.InvariantCulture);
                continue;
            }

            if (arg.Equals("--multi-goal-seed-search", StringComparison.OrdinalIgnoreCase))
            {
                options.UseMultiGoalSeedSearch = true;
                continue;
            }

            if (arg.Equals("--seed-start", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                var parts = args[++index].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    options.ExtraSeedStarts.Add(new MotionSeedStart(
                        float.Parse(parts[0], CultureInfo.InvariantCulture),
                        float.Parse(parts[1], CultureInfo.InvariantCulture)));
                }

                continue;
            }

            if (arg.Equals("--seed-goal", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                var parts = args[++index].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var label = parts.Length >= 3 ? parts[2] : $"seed_goal_{options.ExtraSeedGoals.Count + 1}";
                    options.ExtraSeedGoals.Add(new MotionSeedGoal(
                        label,
                        float.Parse(parts[0], CultureInfo.InvariantCulture),
                        float.Parse(parts[1], CultureInfo.InvariantCulture)));
                }

                continue;
            }

            if (arg.Equals("--seed-proof-artifact", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                options.ProofSeedArtifactPaths.Add(args[++index]);
                continue;
            }

            if (arg.Equals("--seed-path", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                var parts = args[++index].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    var label = parts.Length >= 5 ? parts[4] : $"seed_path_{options.ExplicitSeedPaths.Count + 1}";
                    options.ExplicitSeedPaths.Add(new MotionSeedPath(
                        float.Parse(parts[0], CultureInfo.InvariantCulture),
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        float.Parse(parts[3], CultureInfo.InvariantCulture),
                        label));
                }

                continue;
            }
        }

        return options;
    }

    private static string GetDefaultMovementProfileId(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => "Scout",
            PlayerClass.Heavy => "Heavy",
            PlayerClass.Soldier or PlayerClass.Sniper => "Soldier",
            PlayerClass.Engineer or PlayerClass.Demoman => "Demoman",
            PlayerClass.Pyro or PlayerClass.Medic or PlayerClass.Spy => "Fast",
            PlayerClass.Quote => "Quote",
            _ => classId.ToString(),
        };
    }

    private static PlayerClass GetMovementProfileRepresentative(string profileId)
    {
        return profileId switch
        {
            var value when value.Equals("Scout", StringComparison.OrdinalIgnoreCase) => PlayerClass.Scout,
            var value when value.Equals("Heavy", StringComparison.OrdinalIgnoreCase) => PlayerClass.Heavy,
            var value when value.Equals("Soldier", StringComparison.OrdinalIgnoreCase) => PlayerClass.Soldier,
            var value when value.Equals("Demoman", StringComparison.OrdinalIgnoreCase) => PlayerClass.Demoman,
            var value when value.Equals("Fast", StringComparison.OrdinalIgnoreCase) => PlayerClass.Pyro,
            var value when value.Equals("Quote", StringComparison.OrdinalIgnoreCase) => PlayerClass.Quote,
            _ => Enum.TryParse<PlayerClass>(profileId, ignoreCase: true, out var classId) ? classId : PlayerClass.Pyro,
        };
    }
}

internal readonly record struct MotionSeedStart(float X, float Bottom);

internal readonly record struct MotionSeedGoal(string Label, float X, float Bottom);

internal readonly record struct MotionSeedPath(float StartX, float StartBottom, float GoalX, float GoalBottom, string Label);

internal readonly record struct MotionGraphBuildNode(int Id, MotionState State);

internal readonly record struct MotionCoverageSeedPath(MotionState Start, MotionPath Path, bool CarryingIntel);

internal sealed record GraphReachability(
    int StartIndex,
    IReadOnlyDictionary<int, int> CostByNode,
    IReadOnlyDictionary<int, MotionGraphEdge> PreviousEdgeByNode);

internal sealed record GraphSearchIndex(
    IReadOnlyDictionary<int, MotionGraphEdge[]> EdgesByNode);

internal readonly record struct MotionGraphKey(
    int X,
    int Bottom,
    int HorizontalSpeed,
    int VerticalSpeed,
    bool IsGrounded,
    int RemainingAirJumps,
    int MovementState,
    int FacingDirectionX,
    int SourceFacingDirectionX,
    int PreviousSourceFacingDirectionX)
{
    public static MotionGraphKey From(MotionState state)
    {
        return new MotionGraphKey(
            Quantize(state.X, 3f),
            Quantize(state.Bottom, 3f),
            Quantize(state.HorizontalSpeed, 15f),
            Quantize(state.VerticalSpeed, 15f),
            state.IsGrounded,
            Math.Clamp(state.RemainingAirJumps, 0, 2),
            (int)state.MovementState,
            Quantize(state.FacingDirectionX, 1f),
            Quantize(state.SourceFacingDirectionX, 1f),
            Quantize(state.PreviousSourceFacingDirectionX, 1f));
    }

    private static int Quantize(float value, float bucket)
    {
        return (int)MathF.Round(value / bucket);
    }
}

internal sealed record MotionGraphArtifact(
    int Version,
    string Map,
    int Area,
    string Mode,
    string Team,
    string Class,
    IReadOnlyList<MotionGraphNode> Nodes,
    IReadOnlyList<MotionGraphEdge> Edges);

internal sealed record MotionGraphNode(
    int Id,
    float X,
    float Y,
    float Bottom,
    bool IsGrounded,
    float HorizontalSpeed = 0f,
    float VerticalSpeed = 0f,
    int RemainingAirJumps = 0);

internal sealed record MotionGraphEdge(
    int From,
    int To,
    int CostTicks,
    MotionProofAction Action,
    string TeamMask = "Any",
    string CarryMask = "Any");

internal sealed record MotionProofArtifact(
    int Version,
    string Map,
    int Area,
    string Mode,
    string Team,
    string Class,
    int AttackTicks,
    int ReturnTicks,
    IReadOnlyList<MotionProofAction> Attack,
    IReadOnlyList<MotionProofAction> Return);

internal sealed record MotionProofAction(string Kind, int Direction, int Ticks)
{
    public static MotionProofAction From(MotionAction action)
    {
        return new MotionProofAction(action.Kind.ToString(), action.Direction, action.Ticks);
    }

    public MotionAction ToMotionAction()
    {
        return Enum.TryParse<MotionActionKind>(Kind, ignoreCase: true, out var kind)
            ? new MotionAction(kind, Direction, Ticks)
            : MotionAction.Idle(Math.Max(1, Ticks));
    }
}

internal enum MotionActionKind
{
    Idle,
    Run,
    Jump,
    Drop,
    Fall,
}

internal readonly record struct MotionAction(MotionActionKind Kind, int Direction, int Ticks)
{
    public static MotionAction Idle(int ticks) => new(MotionActionKind.Idle, 0, ticks);
    public static MotionAction Run(int direction, int ticks) => new(MotionActionKind.Run, direction, ticks);
    public static MotionAction Jump(int direction, int ticks) => new(MotionActionKind.Jump, direction, ticks);
    public static MotionAction Drop(int direction, int ticks) => new(MotionActionKind.Drop, direction, ticks);
    public static MotionAction Fall(int direction, int ticks) => new(MotionActionKind.Fall, direction, ticks);

    public PlayerInputSnapshot GetInput(int tick, PlayerEntity player)
    {
        var left = Direction < 0;
        var right = Direction > 0;
        var up = Kind == MotionActionKind.Jump && tick == 0;
        var down = Kind == MotionActionKind.Drop;
        var aimDirection = Direction == 0 ? 1 : Direction;
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
            AimWorldX: player.X + (aimDirection * 160f),
            AimWorldY: player.Y,
            DebugKill: false);
    }
}

internal readonly record struct MotionState(
    float X,
    float Y,
    float Bottom,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool IsGrounded,
    int RemainingAirJumps,
    float LegacyStateTickAccumulator,
    LegacyMovementState MovementState,
    float FacingDirectionX,
    float AimDirectionDegrees,
    float SourceFacingDirectionX,
    float PreviousSourceFacingDirectionX)
{
    public static MotionState FromPlayer(PlayerEntity player)
    {
        return new MotionState(
            player.X,
            player.Y,
            player.Bottom,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.IsGrounded,
            player.RemainingAirJumps,
            GetPrivateInstanceProperty<float>(player, "LegacyStateTickAccumulator"),
            player.MovementState,
            player.FacingDirectionX,
            player.AimDirectionDegrees,
            GetPrivateInstanceProperty<float>(player, "SourceFacingDirectionX"),
            GetPrivateInstanceProperty<float>(player, "PreviousSourceFacingDirectionX"));
    }

    public static MotionState FromGrounded(float x, float bottom, float collisionBottom, int remainingAirJumps)
    {
        return new MotionState(
            x,
            bottom - collisionBottom,
            bottom,
            HorizontalSpeed: 0f,
            VerticalSpeed: 0f,
            IsGrounded: true,
            RemainingAirJumps: remainingAirJumps,
            LegacyStateTickAccumulator: 0f,
            MovementState: LegacyMovementState.None,
            FacingDirectionX: 1f,
            AimDirectionDegrees: 0f,
            SourceFacingDirectionX: 1f,
            PreviousSourceFacingDirectionX: 1f);
    }

    private static T GetPrivateInstanceProperty<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is null)
        {
            throw new InvalidOperationException($"Property '{propertyName}' not found on {target.GetType().Name}.");
        }

        var value = property.GetValue(target);
        return value is T typed
            ? typed
            : throw new InvalidOperationException($"Property '{propertyName}' on {target.GetType().Name} was not a {typeof(T).Name}.");
    }
}

internal readonly record struct MotionKey(
    int X,
    int Bottom,
    int HorizontalSpeed,
    int VerticalSpeed,
    bool IsGrounded,
    int RemainingAirJumps,
    int MovementState,
    int FacingDirectionX,
    int SourceFacingDirectionX,
    int PreviousSourceFacingDirectionX)
{
    public static MotionKey From(MotionState state)
    {
        return new MotionKey(
            Quantize(state.X, 18f),
            Quantize(state.Bottom, 18f),
            Quantize(state.HorizontalSpeed, 90f),
            Quantize(state.VerticalSpeed, 90f),
            state.IsGrounded,
            Math.Clamp(state.RemainingAirJumps, 0, 2),
            (int)state.MovementState,
            Quantize(state.FacingDirectionX, 1f),
            Quantize(state.SourceFacingDirectionX, 1f),
            Quantize(state.PreviousSourceFacingDirectionX, 1f));
    }

    private static int Quantize(float value, float bucket)
    {
        return (int)MathF.Round(value / bucket);
    }
}

internal readonly record struct MotionGoal(float X, float Y, float Width, float Height, string Label);

internal readonly record struct SearchNode(
    int Index,
    int ParentIndex,
    MotionState State,
    MotionAction Action,
    int CostTicks);

internal sealed class MotionPath
{
    public static readonly MotionPath Empty = new(Array.Empty<MotionAction>());

    public MotionPath(IReadOnlyList<MotionAction> actions)
    {
        Actions = actions;
        TotalTicks = actions.Sum(action => action.Ticks);
    }

    public IReadOnlyList<MotionAction> Actions { get; }
    public int TotalTicks { get; }
}
