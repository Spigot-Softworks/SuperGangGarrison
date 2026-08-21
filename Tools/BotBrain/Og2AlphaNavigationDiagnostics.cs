using System.Diagnostics;
using System.Globalization;
using OpenGarrison.BotAI;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;

internal static class Og2AlphaNavigationDiagnostics
{
    private static readonly PlayerClass[] DefaultCaptureClasses =
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

    public static void RunReport(IReadOnlyDictionary<string, string> rawOptions)
    {
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_GRAPH", "1");
        if (rawOptions.TryGetValue("classes", out var reportClasses)
            && !string.IsNullOrWhiteSpace(reportClasses))
        {
            Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_CLASSES", reportClasses);
        }

        var maps = rawOptions.TryGetValue("maps", out var mapList)
            ? mapList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [rawOptions.TryGetValue("map", out var mapValue) ? mapValue : "Truefort"];
        var area = ReadInt(rawOptions, "area", 1);
        var dumpAlphaPath = rawOptions.ContainsKey("dump-alpha-path");
        var reportProbeClass = rawOptions.TryGetValue("probe-class", out var probeClassText)
            ? ParseClass(probeClassText) ?? PlayerClass.Scout
            : PlayerClass.Scout;
        var reportProbeTeam = rawOptions.TryGetValue("probe-team", out var probeTeamText)
            ? ParseTeam(probeTeamText) ?? PlayerTeam.Red
            : PlayerTeam.Red;
        var reportProbeCarrying = rawOptions.TryGetValue("probe-carrying", out var probeCarryingText)
            && probeCarryingText is "1" or "true" or "TRUE";

        foreach (var map in maps)
        {
            var level = SimpleLevelFactory.CreateImportedLevel(map, area);
            if (level is null)
            {
                Console.WriteLine($"alphaNav map={map} area={area} status=load_failed");
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            var graph = Og2NavigationGraphStore.GetOrBuild(level);
            stopwatch.Stop();

            var edgeCount = 0;
            var walkEdges = 0;
            var jumpEdges = 0;
            var fallEdges = 0;
            var dropdownEdges = 0;
            for (var nodeIndex = 0; nodeIndex < graph.NodeCount; nodeIndex += 1)
            {
                foreach (var edge in graph.GetEdges(nodeIndex))
                {
                    edgeCount += 1;
                    switch (edge.Kind)
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

            var redIntel = level.GetIntelBase(PlayerTeam.Red);
            var blueIntel = level.GetIntelBase(PlayerTeam.Blue);
            var redToBlue = redIntel.HasValue && blueIntel.HasValue
                ? FindPathSummary(graph, level.RedSpawns, PlayerTeam.Red, PlayerClass.Scout, blueIntel.Value.X, blueIntel.Value.Y, dumpAlphaPath, "redToBlue")
                : "missing_intel_base";
            var blueToRed = redIntel.HasValue && blueIntel.HasValue
                ? FindPathSummary(graph, level.BlueSpawns, PlayerTeam.Blue, PlayerClass.Scout, redIntel.Value.X, redIntel.Value.Y, dumpAlphaPath, "blueToRed")
                : "missing_intel_base";

            Console.WriteLine(
                $"alphaNav map={level.Name} area={level.MapAreaIndex} mode={level.Mode} " +
                $"buildMs={stopwatch.Elapsed.TotalMilliseconds:0.00} nodes={graph.NodeCount} edges={edgeCount} " +
                $"walk={walkEdges} jump={jumpEdges} fall={fallEdges} dropdown={dropdownEdges} " +
                $"redToBlue={redToBlue} blueToRed={blueToRed}");

            if (rawOptions.ContainsKey("dump-alpha-class-paths")
                && redIntel.HasValue
                && blueIntel.HasValue)
            {
                foreach (var playerClass in DefaultCaptureClasses)
                {
                    var classRedToBlue = FindPathSummary(
                        graph,
                        level.RedSpawns,
                        PlayerTeam.Red,
                        playerClass,
                        blueIntel.Value.X,
                        blueIntel.Value.Y,
                        dumpPath: true,
                        $"{playerClass}:redToBlue");
                    var classBlueToRed = FindPathSummary(
                        graph,
                        level.BlueSpawns,
                        PlayerTeam.Blue,
                        playerClass,
                        redIntel.Value.X,
                        redIntel.Value.Y,
                        dumpPath: true,
                        $"{playerClass}:blueToRed");
                    Console.WriteLine(
                        $"alphaNavClassPath map={level.Name} class={playerClass} " +
                        $"redToBlue={classRedToBlue} blueToRed={classBlueToRed}");
                }
            }

            if (rawOptions.TryGetValue("inspect-alpha-edges", out var edgeSpecs))
            {
                foreach (var edgeSpec in edgeSpecs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var parts = edgeSpec.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length != 2
                        || !int.TryParse(parts[0], out var fromNodeIndex)
                        || !int.TryParse(parts[1], out var toNodeIndex)
                        || fromNodeIndex < 0
                        || fromNodeIndex >= graph.NodeCount)
                    {
                        Console.WriteLine($"alphaNavEdgeInspect edge={edgeSpec} status=invalid");
                        continue;
                    }

                    var fromNode = graph.GetNode(fromNodeIndex);
                    foreach (var edge in graph.GetEdges(fromNodeIndex))
                    {
                        if (edge.ToNode != toNodeIndex)
                        {
                            continue;
                        }

                        Console.WriteLine(
                            $"alphaNavEdgeInspect edge={fromNodeIndex}->{toNodeIndex} " +
                            $"from=({fromNode.X:0.0},{fromNode.Y:0.0}) " +
                            $"to=({graph.GetNode(toNodeIndex).X:0.0},{graph.GetNode(toNodeIndex).Y:0.0}) " +
                            $"kind={edge.Kind} trigger={edge.JumpTriggerTick} probe={edge.ProbeTicks} " +
                            $"move={edge.ProbeMoveDirectionX:0.0} grounded={(edge.RequiresGroundedContinuation ? 1 : 0)} " +
                            $"carry={(edge.RequiresCarryingIntel ? 1 : 0)} " +
                            $"contact={(edge.IsOg2Contact ? 1 : 0)} " +
                            $"classMask={edge.SupportedClassMask} " +
                            $"recipe={(edge.LaunchRecipe.HasRecipe ? 1 : 0)} " +
                            $"jumpGround={(edge.LaunchRecipe.JumpStartsGrounded ? 1 : 0)} " +
                            $"launch=({edge.LaunchRecipe.LaunchMinX:0.0},{edge.LaunchRecipe.LaunchMaxX:0.0}," +
                            $"{edge.LaunchRecipe.LaunchMinY:0.0},{edge.LaunchRecipe.LaunchMaxY:0.0}," +
                            $"{edge.LaunchRecipe.LaunchTick}) " +
                            $"completion=({edge.Completion.MinX:0.0},{edge.Completion.MaxX:0.0},{edge.Completion.MinY:0.0},{edge.Completion.MaxY:0.0})");
                    }
                }
            }

            if (rawOptions.ContainsKey("audit-alpha-path")
                && redIntel.HasValue
                && blueIntel.HasValue)
            {
                AuditObjectivePath(
                    level,
                    graph,
                    level.RedSpawns,
                    PlayerTeam.Red,
                    blueIntel.Value.X,
                    blueIntel.Value.Y,
                    "redToBlue",
                    rawOptions.ContainsKey("audit-all-classes"));
            }

            if (rawOptions.TryGetValue("probe-goals", out var probeGoals))
            {
                foreach (var probe in probeGoals.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var coordinates = probe.Split(',', StringSplitOptions.TrimEntries);
                    if (coordinates.Length != 2
                        || !float.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var probeX)
                        || !float.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var probeY))
                    {
                        Console.WriteLine($"alphaNavProbe goal={probe} status=invalid");
                        continue;
                    }

                    var probeResult = FindPathSummary(
                        graph,
                        reportProbeTeam == PlayerTeam.Blue ? level.BlueSpawns : level.RedSpawns,
                        reportProbeTeam,
                        reportProbeClass,
                        probeX,
                        probeY,
                        dumpAlphaPath,
                        $"probe:{probe}");
                    Console.WriteLine($"alphaNavProbe goal=({probeX:0.0},{probeY:0.0}) result={probeResult}");
                }
            }

            if (rawOptions.TryGetValue("probe-routes", out var probeRoutes))
            {
                foreach (var probe in probeRoutes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var endpoints = probe.Split(':', StringSplitOptions.TrimEntries);
                    if (endpoints.Length != 2
                        || !TryParseCoordinates(endpoints[0], out var startX, out var startY)
                        || !TryParseCoordinates(endpoints[1], out var goalX, out var goalY))
                    {
                        Console.WriteLine($"alphaNavProbe route={probe} status=invalid");
                        continue;
                    }

                    var startNode = graph.FindNearestTraversalStartNode(startX, startY, maxAboveDistance: 48f);
                    var goalNode = graph.FindNearestNode(goalX, goalY);
                    var path = startNode >= 0 && goalNode >= 0
                        ? graph.FindPath(startNode, goalNode, reportProbeClass, team: reportProbeTeam, carryingIntel: reportProbeCarrying)
                        : null;
                    Console.WriteLine(
                        $"alphaNavProbe route=({startX:0.0},{startY:0.0})->({goalX:0.0},{goalY:0.0}) " +
                        $"startNode={startNode} goalNode={goalNode} result={(path is null ? "unreachable" : $"path:{path.Count} cost:{path.TotalCost:0}")}");
                    if (dumpAlphaPath && path is not null)
                    {
                        DumpPath($"probeRoute:{probe}", path, graph);
                    }
                }
            }

            if (rawOptions.TryGetValue("validate-transitions", out var validationRoutes))
            {
                foreach (var validationRoute in validationRoutes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!TryParseRoute(validationRoute, out var validationStartX, out var validationStartY, out var validationGoalX, out var validationGoalY))
                    {
                        Console.WriteLine($"alphaNavValidation route={validationRoute} status=invalid");
                        continue;
                    }

                    foreach (var playerClass in DefaultCaptureClasses)
                    {
                        var definition = CharacterClassCatalog.GetDefinition(playerClass);
                        var groundWorks = BotNavigationMovementValidator.TryBuildGroundTape(
                            level,
                            definition,
                            validationStartX,
                            validationStartY,
                            validationGoalX,
                            validationGoalY,
                            PlayerTeam.Red,
                            out _,
                            out _,
                            out var groundFailure);
                        var sharedJumpWorks = BotNavigationMovementValidator.TryBuildSharedJumpTape(
                            level,
                            DefaultCaptureClasses,
                            validationStartX,
                            validationStartY + 24f,
                            validationGoalX,
                            validationGoalY + 24f,
                            out _,
                            out _);
                        var jumpWorks = BotNavigationMovementValidator.TryBuildJumpTape(
                            level,
                            definition,
                            BotNavigationProfile.Standard,
                            validationStartX,
                            validationStartY,
                            validationGoalX,
                            validationGoalY,
                            PlayerTeam.Red,
                            out _,
                            out _);
                        Console.WriteLine(
                            $"alphaNavValidation route={validationRoute} class={playerClass} " +
                            $"ground={(groundWorks ? 1 : 0)} jump={(jumpWorks ? 1 : 0)} " +
                            $"sharedJump={(sharedJumpWorks ? 1 : 0)} failure={groundFailure}");
                    }
                }
            }

            if (rawOptions.ContainsKey("dump-nodes"))
            {
                DumpNodes(graph);
            }

            if (rawOptions.ContainsKey("dump-solids"))
            {
                DumpSolids(level);
            }

            if (rawOptions.ContainsKey("dump-room-objects"))
            {
                DumpRoomObjects(level);
            }

            if (rawOptions.ContainsKey("dump-legacy-path")
                && OpenGarrison.Core.BotBrain.BotNavigationAssetStore.TryLoadCachedGraph(level, out var legacyGraph))
            {
                DumpLegacyPath(level, legacyGraph);
            }
        }
    }

    public static void RunRawTransitionValidation(IReadOnlyDictionary<string, string> rawOptions)
    {
        var mapName = rawOptions.TryGetValue("map", out var mapValue) ? mapValue : "Truefort";
        var area = ReadInt(rawOptions, "area", 1);
        var level = SimpleLevelFactory.CreateImportedLevel(mapName, area)
            ?? throw new InvalidOperationException($"Could not load map '{mapName}' area {area}.");
        var routes = rawOptions.TryGetValue("routes", out var routeText)
            ? routeText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();
        var classes = rawOptions.TryGetValue("classes", out var classText)
            ? classText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseClass)
                .Where(static playerClass => playerClass.HasValue)
                .Select(static playerClass => playerClass!.Value)
                .Distinct()
                .ToArray()
            : DefaultCaptureClasses;

        foreach (var route in routes)
        {
            if (!TryParseRoute(route, out var sourceX, out var sourceY, out var targetX, out var targetY))
            {
                Console.WriteLine($"alphaRawValidation route={route} status=invalid");
                continue;
            }

            foreach (var playerClass in classes)
            {
                var definition = CharacterClassCatalog.GetDefinition(playerClass);
                var ground = BotNavigationMovementValidator.TryBuildGroundTape(
                    level,
                    definition,
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    PlayerTeam.Red,
                    out _,
                    out _,
                    out var groundFailure);
                var jump = BotNavigationMovementValidator.TryBuildJumpTape(
                    level,
                    definition,
                    BotNavigationProfile.Standard,
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    PlayerTeam.Red,
                    out _,
                    out _);
                Console.WriteLine(
                    $"alphaRawValidation map={level.Name} route={route} class={playerClass} " +
                    $"ground={(ground ? 1 : 0)} jump={(jump ? 1 : 0)} failure={groundFailure}");
            }
        }
    }

    public static void RunRawMovementSweep(IReadOnlyDictionary<string, string> rawOptions)
    {
        var mapName = rawOptions.TryGetValue("map", out var mapValue) ? mapValue : "Truefort";
        var area = ReadInt(rawOptions, "area", 1);
        var playerClass = rawOptions.TryGetValue("class", out var classText)
            ? ParseClass(classText) ?? PlayerClass.Scout
            : PlayerClass.Scout;
        var direction = rawOptions.TryGetValue("direction", out var directionText)
            && float.TryParse(directionText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDirection)
            ? MathF.Sign(parsedDirection)
            : 1f;
        var ticks = Math.Max(1, ReadInt(rawOptions, "ticks", 180));
        var jumpTick = ReadInt(rawOptions, "jump-tick", -1);
        var startHorizontalSpeed = rawOptions.TryGetValue("start-horizontal-speed", out var horizontalSpeedText)
            && float.TryParse(horizontalSpeedText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHorizontalSpeed)
            ? parsedHorizontalSpeed
            : 0f;
        var startVerticalSpeed = rawOptions.TryGetValue("start-vertical-speed", out var verticalSpeedText)
            && float.TryParse(verticalSpeedText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedVerticalSpeed)
            ? parsedVerticalSpeed
            : 0f;
        var carryingIntel = rawOptions.TryGetValue("carrying-intel", out var carryingIntelText)
            && carryingIntelText is "1" or "true" or "TRUE";
        var start = rawOptions.TryGetValue("start", out var startText)
            && TryParseCoordinates(startText, out var startX, out var startY)
            ? (X: startX, Y: startY)
            : throw new InvalidOperationException("--alpha-raw-sweep requires --start x,y.");
        var level = SimpleLevelFactory.CreateImportedLevel(mapName, area)
            ?? throw new InvalidOperationException($"Could not load map '{mapName}' area {area}.");
        level.GetBlockingTeamGates(PlayerTeam.Red, carryingIntel);
        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        var player = new PlayerEntity(1, definition, "alpha-raw-sweep");
        player.Spawn(PlayerTeam.Red, start.X, start.Y);
        player.TeleportTo(start.X, start.Y);
        if (startHorizontalSpeed != 0f || startVerticalSpeed != 0f)
        {
            player.AddImpulse(startHorizontalSpeed, startVerticalSpeed);
        }
        if (carryingIntel)
        {
            player.PickUpIntel();
        }
        // Match the generator's probe setup exactly. Carrying changes the
        // collision envelope and can require an overlap resolution before the
        // grounded state is restored.
        player.ResolveBlockingOverlap(level, PlayerTeam.Red);
        player.RestoreMovementProbeState(isGrounded: true, player.MaxAirJumps, direction);
        var previousX = player.X;
        var previousY = player.Y;
        var previousGrounded = player.IsGrounded;
        var previousInput = default(PlayerInputSnapshot);
        for (var tick = 1; tick <= ticks; tick += 1)
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
                AimWorldX: player.X + (direction * 256f),
                AimWorldY: player.Y,
                DebugKill: false);
            var jumpPressed = input.Up && !previousInput.Up;
            player.Advance(input, jumpPressed, level, PlayerTeam.Red, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;
            if (tick == 1
                || tick % 6 == 0
                || player.IsGrounded != previousGrounded
                || MathF.Abs(player.Y - previousY) > 2f)
            {
                Console.WriteLine(
                    $"alphaRawSweep class={playerClass} tick={tick} pos=({player.X:0.0},{player.Y:0.0}) " +
                    $"carrying={(player.IsCarryingIntel ? 1 : 0)} maxRun={player.MaxRunSpeed:0.0} " +
                    $"dx={(player.X - previousX):0.0} dy={(player.Y - previousY):0.0} grounded={(player.IsGrounded ? 1 : 0)} " +
                    $"vspeed={player.VerticalSpeed:0.0}");
            }

            previousX = player.X;
            previousY = player.Y;
            previousGrounded = player.IsGrounded;
            if (!player.IsAlive)
            {
                break;
            }
        }
    }

    private static void DumpNodes(NavGraph graph)
    {
        for (var nodeIndex = 0; nodeIndex < graph.NodeCount; nodeIndex += 1)
        {
            var node = graph.GetNode(nodeIndex);
            var edgeLabels = new List<string>();
            foreach (var edge in graph.GetEdges(nodeIndex))
            {
                edgeLabels.Add($"{edge.ToNode}/{edge.Kind}");
            }

            var edges = string.Join(',', edgeLabels);
            Console.WriteLine(
                $"alphaNavNode index={nodeIndex} kind={node.Kind} surface={node.SurfaceId?.ToString() ?? "none"} " +
                $"pos=({node.X:0.0},{node.Y:0.0}) edges={edges}");
        }
    }

    private static void DumpSolids(SimpleLevel level)
    {
        for (var solidIndex = 0; solidIndex < level.Solids.Count; solidIndex += 1)
        {
            var solid = level.Solids[solidIndex];
            Console.WriteLine(
                $"alphaNavSolid index={solidIndex} rect=({solid.Left:0.0},{solid.Top:0.0})-({solid.Right:0.0},{solid.Bottom:0.0})");
        }
    }

    private static void DumpRoomObjects(SimpleLevel level)
    {
        for (var objectIndex = 0; objectIndex < level.RoomObjects.Count; objectIndex += 1)
        {
            var roomObject = level.RoomObjects[objectIndex];
            Console.WriteLine(
                $"alphaNavObject index={objectIndex} type={roomObject.Type} " +
                $"rect=({roomObject.Left:0.0},{roomObject.Top:0.0})-({roomObject.Right:0.0},{roomObject.Bottom:0.0}) " +
                $"team={roomObject.Team?.ToString() ?? "none"}");
        }
    }

    private static void DumpLegacyPath(SimpleLevel level, NavGraph legacyGraph)
    {
        var goal = level.GetIntelBase(PlayerTeam.Blue);
        if (!goal.HasValue || level.RedSpawns.Count == 0)
        {
            return;
        }

        var startNode = legacyGraph.FindNearestTraversalStartNode(level.RedSpawns[0].X, level.RedSpawns[0].Y, 48f);
        var goalNode = legacyGraph.FindNearestReachableNode(goal.Value.X, goal.Value.Y, startNode, PlayerClass.Scout, team: PlayerTeam.Red);
        var path = startNode >= 0 && goalNode >= 0
            ? legacyGraph.FindPath(startNode, goalNode, PlayerClass.Scout, team: PlayerTeam.Red)
            : null;
        if (path is null)
        {
            Console.WriteLine("alphaLegacyPath status=no_path");
            return;
        }

        Console.WriteLine($"alphaLegacyPath nodes={path.Count} cost={path.TotalCost:0.0}");
        for (var index = 0; index < path.Count; index += 1)
        {
            var nodeIndex = path.GetWaypoint(index);
            var node = legacyGraph.GetNode(nodeIndex);
            var edgeText = path.TryGetIncomingEdge(index, out var edge)
                ? $" edge={edge.Kind}"
                : string.Empty;
            Console.WriteLine(
                $"alphaLegacyWaypoint index={index} node={nodeIndex} pos=({node.X:0.0},{node.Y:0.0}) kind={node.Kind}{edgeText}");
        }
    }

    public static void RunCaptureMatrix(IReadOnlyDictionary<string, string> rawOptions)
    {
        var requestedMaps = ReadList(rawOptions, "maps", "Truefort,Eiger,Corinth,Docking,Harvest,Conflict,Gallery,Valley");
        var requestedClasses = rawOptions.ContainsKey("classes")
            ? ReadList(rawOptions, "classes", null)
            : null;
        var classes = requestedClasses is null
            ? DefaultCaptureClasses
            : requestedClasses
                .Select(ParseClass)
                .Where(static playerClass => playerClass.HasValue)
                .Select(static playerClass => playerClass!.Value)
                .Distinct()
                .ToArray();
        var area = ReadInt(rawOptions, "area", 1);
        var ticks = Math.Max(1, ReadInt(rawOptions, "capture-ticks", 9_000));
        var failOnAcceptance = ReadBool(rawOptions, "fail-on-acceptance", true);
        var traceCapture = rawOptions.ContainsKey("trace-capture");
        var requestedTeams = rawOptions.TryGetValue("teams", out var teamText)
            ? teamText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseTeam)
                .Where(static team => team.HasValue)
                .Select(static team => team!.Value)
                .Distinct()
                .ToArray()
            : [PlayerTeam.Red, PlayerTeam.Blue];

        if (classes.Length == 0)
        {
            throw new InvalidOperationException("alpha capture matrix has no valid classes.");
        }

        var graphClasses = rawOptions.TryGetValue("graph-classes", out var graphClassText)
            ? graphClassText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseClass)
                .Where(static playerClass => playerClass.HasValue)
                .Select(static playerClass => playerClass!.Value)
                .Distinct()
                .ToArray()
            : classes;
        if (graphClasses.Length == 0)
        {
            throw new InvalidOperationException("alpha capture matrix graph has no valid classes.");
        }

        // The acceptance matrix must exercise the graph being accepted. Do
        // not silently fall back to the legacy heuristic builder or a probe
        // subset left behind by an earlier interactive diagnostic.
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_GRAPH", "1");
        // Reuse the immutable generated graph across diagnostic processes.
        // The cache key includes the generator fingerprint, map fingerprint,
        // class set, and sweep settings, so steering changes do not pay the
        // graph-build cost again while generator changes miss.
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_PERSISTENT_CACHE", "1");
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CACHE_TRACE", "1");
        Environment.SetEnvironmentVariable(
            "BOTBRAIN_NAV_ALPHA_CONTACT_CLASSES",
            string.Join(',', graphClasses));
        if (rawOptions.ContainsKey("trace-stair-inputs"))
        {
            Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_INPUTS", "1");
            Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_ALL_INPUTS", "1");
            Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_STAIR_INPUTS", "1");
        }
        TryRegisterPackagedQuoteCurlyGameplayPack();

        var passed = true;
        var total = 0;
        var passedCount = 0;
        var matrixStopwatch = Stopwatch.StartNew();
        foreach (var requestedMap in requestedMaps)
        {
            var mapName = NormalizeMapName(requestedMap);
            var level = SimpleLevelFactory.CreateImportedLevel(mapName, area);
            if (level is null)
            {
                Console.WriteLine(
                    $"alphaCapture map={requestedMap} loadedMap={mapName} area={area} status=load_failed");
                passed = false;
                continue;
            }

            var sharedGraph = Og2NavigationGraphStore.GetOrBuild(level);
            var mapPassed = true;
            var trials = requestedTeams
                .SelectMany(team => classes.Select(playerClass => (Team: team, PlayerClass: playerClass)))
                .ToArray();
            var results = new CaptureTrialResult[trials.Length];
            var parallelism = traceCapture
                ? 1
                : Math.Max(1, Environment.ProcessorCount - 1);
            Parallel.For(
                0,
                trials.Length,
                new ParallelOptions { MaxDegreeOfParallelism = parallelism },
                trialIndex =>
                {
                    var trial = trials[trialIndex];
                    results[trialIndex] = RunCaptureTrial(
                        mapName,
                        area,
                        trial.Team,
                        trial.PlayerClass,
                        level.Mode,
                        ticks,
                        sharedGraph,
                        traceCapture);
                });

            for (var trialIndex = 0; trialIndex < trials.Length; trialIndex += 1)
            {
                var trial = trials[trialIndex];
                var result = results[trialIndex];
                total += 1;
                passed &= result.Passed;
                mapPassed &= result.Passed;
                passedCount += result.Passed ? 1 : 0;
                Console.WriteLine(
                    $"alphaCapture map={requestedMap} loadedMap={mapName} area={area} mode={result.Mode} " +
                    $"team={trial.Team} class={trial.PlayerClass} passed={(result.Passed ? 1 : 0)} " +
                    $"tick={result.CompletionTick} reason={result.Reason} " +
                    $"start=({result.StartX:0.0},{result.StartY:0.0}) " +
                    $"end=({result.EndX:0.0},{result.EndY:0.0})");
            }

            Console.WriteLine(
                $"alphaCaptureMap map={requestedMap} loadedMap={mapName} area={area} " +
                $"mode={level.Mode} passed={(mapPassed ? 1 : 0)}");
        }

        matrixStopwatch.Stop();
        Console.WriteLine(
            $"alphaCaptureSuite maps={requestedMaps.Count} trials={total} passed={passedCount} " +
            $"failed={total - passedCount} elapsedMs={matrixStopwatch.Elapsed.TotalMilliseconds:0.0}");
        if (failOnAcceptance && !passed)
        {
            Environment.ExitCode = 1;
        }
    }

    public static void RunGraphGate(IReadOnlyDictionary<string, string> rawOptions)
    {
        var requestedMaps = ReadList(rawOptions, "maps", "Truefort");
        var requestedClasses = rawOptions.ContainsKey("classes")
            ? ReadList(rawOptions, "classes", null)
            : null;
        var classes = requestedClasses is null
            ? DefaultCaptureClasses
            : requestedClasses
                .Select(ParseClass)
                .Where(static playerClass => playerClass.HasValue)
                .Select(static playerClass => playerClass!.Value)
                .Distinct()
                .ToArray();
        var area = ReadInt(rawOptions, "area", 1);
        var failOnGate = ReadBool(rawOptions, "fail-on-gate", true);
        var dumpRoutes = rawOptions.ContainsKey("dump-graph-gate-routes");
        var dumpIssues = rawOptions.ContainsKey("dump-graph-gate-issues");

        if (classes.Length == 0)
        {
            throw new InvalidOperationException("alpha graph gate has no valid classes.");
        }

        // The gate is specifically for the contact-first graph. Do not let a
        // caller's interactive environment accidentally validate the legacy
        // heuristic builder or a single-class reduced probe set.
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_GRAPH", "1");
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_PERSISTENT_CACHE", "1");
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CACHE_TRACE", "1");
        Environment.SetEnvironmentVariable(
            "BOTBRAIN_NAV_ALPHA_CONTACT_CLASSES",
            string.Join(',', classes));
        TryRegisterPackagedQuoteCurlyGameplayPack();

        var passed = true;
        var totalRoutes = 0;
        var passedRoutes = 0;
        var stopwatch = Stopwatch.StartNew();
        foreach (var requestedMap in requestedMaps)
        {
            var mapName = NormalizeMapName(requestedMap);
            var level = SimpleLevelFactory.CreateImportedLevel(mapName, area);
            if (level is null)
            {
                Console.WriteLine(
                    $"alphaGraphGate map={requestedMap} loadedMap={mapName} area={area} status=load_failed");
                passed = false;
                continue;
            }

            var buildStopwatch = Stopwatch.StartNew();
            var graph = Og2NavigationGraphStore.GetOrBuild(level);
            buildStopwatch.Stop();
            var report = Og2NavigationGraphValidator.Validate(level, graph, classes);
            totalRoutes += report.RouteCount;
            passedRoutes += report.PassedRouteCount;
            passed &= report.Passed;

            Console.WriteLine(
                $"alphaGraphGate map={requestedMap} loadedMap={mapName} area={area} " +
                $"mode={level.Mode} passed={(report.Passed ? 1 : 0)} " +
                $"buildMs={buildStopwatch.Elapsed.TotalMilliseconds:0.0} " +
                $"nodes={graph.NodeCount} routes={report.PassedRouteCount}/{report.RouteCount} " +
                $"issues={report.ErrorCount}");

            if (dumpRoutes)
            {
                foreach (var route in report.Routes)
                {
                    Console.WriteLine(
                        $"alphaGraphRoute map={mapName} label={route.Label} passed={(route.Passed ? 1 : 0)} " +
                        $"startNode={route.StartNode} goalNode={route.GoalNode} " +
                        $"pathNodes={route.PathNodeCount} reason={route.Reason}");
                }
            }

            if (dumpIssues)
            {
                foreach (var issue in report.Issues)
                {
                    Console.WriteLine($"alphaGraphIssue map={mapName} code={issue.Code} message={issue.Message}");
                }
            }
            else if (report.Issues.Count > 0)
            {
                foreach (var issue in report.Issues.Take(12))
                {
                    Console.WriteLine($"alphaGraphIssue map={mapName} code={issue.Code} message={issue.Message}");
                }

                if (report.Issues.Count > 12)
                {
                    Console.WriteLine($"alphaGraphIssue map={mapName} code=truncated remaining={report.Issues.Count - 12}");
                }
            }
        }

        stopwatch.Stop();
        Console.WriteLine(
            $"alphaGraphGateSuite maps={requestedMaps.Count} passed={(passed ? 1 : 0)} " +
            $"routes={passedRoutes}/{totalRoutes} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.0}");
        if (failOnGate && !passed)
        {
            Environment.ExitCode = 1;
        }
    }

    public static void RunPrewarmShippedGraphs(IReadOnlyDictionary<string, string> rawOptions)
    {
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_GRAPH", "1");
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_PERSISTENT_CACHE", "1");
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SWEEP_TICKS", "32");
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_EXTENDED_SWEEP", "0");
        Environment.SetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_CLASSES", null);
        TryRegisterPackagedQuoteCurlyGameplayPack();

        var area = ReadInt(rawOptions, "area", 1);
        var requestedMaps = rawOptions.TryGetValue("maps", out var mapText)
            ? mapText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : DiscoverShippedGraphMaps(rawOptions);
        if (requestedMaps.Length == 0)
        {
            throw new InvalidOperationException("No eligible maps were found for alpha graph prewarm.");
        }

        var built = 0;
        var failed = 0;
        var stopwatch = Stopwatch.StartNew();
        foreach (var requestedMap in requestedMaps)
        {
            var mapName = NormalizeMapName(requestedMap);
            try
            {
                var level = SimpleLevelFactory.CreateImportedLevel(mapName, area);
                if (level is null)
                {
                    failed += 1;
                    Console.WriteLine(
                        $"alphaGraphPrewarm map={requestedMap} loadedMap={mapName} area={area} status=load_failed");
                    continue;
                }

                var key = Og2NavigationGraphCache.BuildKey(level);
                var graphStopwatch = Stopwatch.StartNew();
                var graph = Og2NavigationGraphStore.GetOrBuild(level);
                graphStopwatch.Stop();
                Og2NavigationGraphCache.SaveShipped(level, key, graph, out var shippedPath);
                built += 1;
                Console.WriteLine(
                    $"alphaGraphPrewarm map={requestedMap} loadedMap={mapName} area={area} " +
                    $"status=written buildMs={graphStopwatch.Elapsed.TotalMilliseconds:0.0} " +
                    $"nodes={graph.NodeCount} path=\"{shippedPath}\"");
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or EndOfStreamException
                or FormatException
                or InvalidOperationException
                or ArgumentException)
            {
                failed += 1;
                Console.WriteLine(
                    $"alphaGraphPrewarm map={requestedMap} loadedMap={mapName} area={area} " +
                    $"status=failed error={ex.GetType().Name}:{ex.Message}");
            }
        }

        stopwatch.Stop();
        Console.WriteLine(
            $"alphaGraphPrewarmSuite maps={requestedMaps.Length} built={built} failed={failed} " +
            $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.0}");
        if (failed > 0)
        {
            Environment.ExitCode = 1;
        }
    }

    private static string[] DiscoverShippedGraphMaps(IReadOnlyDictionary<string, string> rawOptions)
    {
        var mapNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in OpenGarrisonStockMapCatalog.SourceDefinitions)
        {
            if (!IsNonNavigableStockMap(definition.LevelName))
            {
                mapNames.Add(definition.LevelName);
            }
        }

        var mapsRoot = rawOptions.TryGetValue("maps-root", out var configuredRoot)
            ? configuredRoot
            : RuntimePaths.MapSearchDirectories.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(mapsRoot) && Directory.Exists(mapsRoot))
        {
            foreach (var directory in Directory.EnumerateDirectories(mapsRoot, "*", SearchOption.TopDirectoryOnly))
            {
                var mapName = Path.GetFileName(directory);
                if (!string.IsNullOrWhiteSpace(mapName) && !IsNonNavigableStockMap(mapName))
                {
                    mapNames.Add(mapName);
                }
            }
        }

        return mapNames
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsNonNavigableStockMap(string mapName)
    {
        var normalized = mapName.Trim();
        return normalized.StartsWith("dj_", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("jt_", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("nfa_", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("rj_", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("rr_", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("vip_", StringComparison.OrdinalIgnoreCase);
    }

    private static CaptureTrialResult RunCaptureTrial(
        string mapName,
        int area,
        PlayerTeam team,
        PlayerClass playerClass,
        GameModeKind expectedMode,
        int maxTicks,
        NavGraph sharedGraph,
        bool traceCapture)
    {
        var world = new SimulationWorld();
        if (!world.TryLoadLevel(mapName, area, preservePlayerStats: false))
        {
            return CaptureTrialResult.Failed(expectedMode, "world_load_failed");
        }

        world.DespawnEnemyDummy();
        world.DespawnFriendlyDummy();
        world.LocalPlayer.Kill();

        const byte botSlot = 2;
        if (!world.TryPrepareNetworkPlayerJoin(botSlot)
            || !world.TrySetNetworkPlayerTeam(botSlot, team)
            || !world.TryForceNetworkPlayerClassSelectionAndRespawn(botSlot, playerClass)
            || !world.TryGetNetworkPlayer(botSlot, out var bot))
        {
            return CaptureTrialResult.Failed(world.MatchRules.Mode, "bot_setup_failed");
        }

        var controller = new BotBrainController(sharedGraph, forceAlphaNavigation: true);
        controller.ForceObjectiveNavigationForDiagnostics = true;
        var graphForTrace = sharedGraph;
        var initialRedCaps = world.RedCaps;
        var initialBlueCaps = world.BlueCaps;
        var initialControlPointTeams = world.ControlPoints.ToDictionary(
            static point => point.Index,
            static point => point.Team);
        var startX = bot.X;
        var startY = bot.Y;
        var lastTracePathNode = -1;
        var lastTracePathIndex = -1;
        var lastTracePathCount = -1;
        var lastTraceCarryingIntel = false;
        var lastTraceFailedEdge = string.Empty;
        var defensiveObjectiveHoldTicks = 0;
        var traceInputs = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_INPUTS") is "1" or "true" or "TRUE";
        var traceAllInputs = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_ALL_INPUTS") is "1" or "true" or "TRUE";
        var traceStairInputs = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_STAIR_INPUTS") is "1" or "true" or "TRUE";

        for (var tick = 1; tick <= maxTicks; tick += 1)
        {
            if (!world.TryGetNetworkPlayer(botSlot, out bot))
            {
                return CaptureTrialResult.Failed(world.MatchRules.Mode, "bot_removed", startX, startY);
            }

            if (HasCapturedObjective(world, bot, team, initialRedCaps, initialBlueCaps, initialControlPointTeams))
            {
                return CaptureTrialResult.Succeeded(world.MatchRules.Mode, tick, "objective_captured", startX, startY, bot.X, bot.Y);
            }

            if (IsAttackDefenseDefensiveObjectiveSatisfied(world, bot, team))
            {
                defensiveObjectiveHoldTicks += 1;
                if (defensiveObjectiveHoldTicks >= 30)
                {
                    return CaptureTrialResult.Succeeded(
                        world.MatchRules.Mode,
                        tick,
                        "defensive_objective_reached",
                        startX,
                        startY,
                        bot.X,
                        bot.Y);
                }
            }
            else
            {
                defensiveObjectiveHoldTicks = 0;
            }

            if (bot.IsAlive)
            {
                var input = controller.Think(bot, world, team);
                world.TrySetNetworkPlayerInput(botSlot, input);

                if (traceInputs
                    && (traceAllInputs || bot.IsCarryingIntel)
                    && (traceAllInputs
                        || (traceStairInputs && bot.X >= 2500f && bot.X <= 3150f)
                        || (bot.X >= 4000f && bot.X <= 4400f)
                        || (bot.X >= 4380f && bot.X <= 4650f)
                        || (bot.X >= 900f && bot.X <= 1300f)))
                {
                    Console.WriteLine(
                        $"alphaCaptureInput map={mapName} team={team} class={playerClass} tick={tick} " +
                        $"pos=({bot.X:0.0},{bot.Y:0.0}) grounded={(bot.IsGrounded ? 1 : 0)} " +
                        $"pogo={(bot.IsCivviePogoActive ? 1 : 0)} pogoSuper={(bot.IsCivviePogoSuperJumpAirPhaseActive ? 1 : 0)} " +
                        $"input=({(input.Left ? 'L' : input.Right ? 'R' : '-')}{(input.Up ? 'J' : '-')}) " +
                        $"path={controller.CurrentPathIndex}/{controller.CurrentPathCount} node={controller.CurrentPathNode} " +
                        $"edge={controller.LastSteeringOutput.RecipeTrace.FromNode}->{controller.LastSteeringOutput.RecipeTrace.ToNode} " +
                        $"edgeTicks={controller.LastSteeringOutput.RecipeTrace.EdgeTicks} " +
                        $"recipeTick={controller.LastSteeringOutput.RecipeTrace.RecipeLaunchTick} " +
                        $"speed={controller.LastSteeringOutput.RecipeTrace.CurrentHorizontalSpeed:0.0} " +
                        $"recipeX=({controller.LastSteeringOutput.RecipeTrace.RecipeLaunchMinX:0.0},{controller.LastSteeringOutput.RecipeTrace.RecipeLaunchMaxX:0.0}) " +
                        $"recipeY=({controller.LastSteeringOutput.RecipeTrace.RecipeLaunchMinY:0.0},{controller.LastSteeringOutput.RecipeTrace.RecipeLaunchMaxY:0.0}) " +
                        $"recipeSpeed=({controller.LastSteeringOutput.RecipeTrace.RecipeLaunchMinHorizontalSpeed:0.0},{controller.LastSteeringOutput.RecipeTrace.RecipeLaunchMaxHorizontalSpeed:0.0}) " +
                        $"recipeReady={(controller.LastSteeringOutput.RecipeTrace.RecipeReady ? 1 : 0)} " +
                        $"runtimeResolved={(controller.LastSteeringOutput.RecipeTrace.RuntimeResolved ? 1 : 0)} " +
                        $"recipeWindow={(controller.LastSteeringOutput.RecipeTrace.InLaunchXWindow ? 1 : 0)} " +
                        $"recipeSuppress={(controller.LastSteeringOutput.RecipeTrace.SuppressJumpUntilLaunch ? 1 : 0)} " +
                        $"recipeDx={controller.LastSteeringOutput.RecipeTrace.SteeringDx:0.0}");
                }

                if (traceCapture
                    && (controller.CurrentPathNode != lastTracePathNode
                        || controller.CurrentPathIndex != lastTracePathIndex
                        || controller.CurrentPathCount != lastTracePathCount
                        || bot.IsCarryingIntel != lastTraceCarryingIntel))
                {
                    Console.WriteLine(
                        $"alphaCapturePath map={mapName} team={team} class={playerClass} tick={tick} " +
                        $"pos=({bot.X:0.0},{bot.Y:0.0}) carrying={(bot.IsCarryingIntel ? 1 : 0)} " +
                        $"path={controller.CurrentPathIndex}/{controller.CurrentPathCount} node={controller.CurrentPathNode} goal={controller.CurrentGoalNode}");
                    lastTracePathNode = controller.CurrentPathNode;
                    lastTracePathIndex = controller.CurrentPathIndex;
                    lastTracePathCount = controller.CurrentPathCount;
                    lastTraceCarryingIntel = bot.IsCarryingIntel;
                }

                var failedEdge = controller.LastSteeringOutput.FailedEdge;
                var failedEdgeTrace = failedEdge.HasFailure
                    ? $"{failedEdge.FromNode}->{failedEdge.ToNode}/{failedEdge.Kind}:{failedEdge.Reason}"
                    : string.Empty;
                if (traceCapture && failedEdgeTrace.Length > 0 && failedEdgeTrace != lastTraceFailedEdge)
                {
                    Console.WriteLine(
                        $"alphaCaptureEdge map={mapName} team={team} class={playerClass} tick={tick} " +
                        $"pos=({bot.X:0.0},{bot.Y:0.0}) edge={failedEdgeTrace}");
                    lastTraceFailedEdge = failedEdgeTrace;
                }
            }
            else
            {
                world.TrySetNetworkPlayerInput(botSlot, default);
            }

            world.AdvanceOneTick();
        }

        var controllerTrace = string.Join(
            ' ',
            new[]
            {
                controller.LastDirectDriveTrace,
                controller.LastSemanticRecoveryTrace,
                controller.LastSteeringOutput.FailedEdge.HasFailure
                    ? $"edge={controller.LastSteeringOutput.FailedEdge.FromNode}->{controller.LastSteeringOutput.FailedEdge.ToNode}/{controller.LastSteeringOutput.FailedEdge.Kind}:{controller.LastSteeringOutput.FailedEdge.Reason}"
                    : string.Empty,
                $"path={controller.CurrentPathIndex}/{controller.CurrentPathCount} goal={controller.CurrentGoalNode}",
                $"goalPos=({controller.CurrentGoalPosition.X:0.0},{controller.CurrentGoalPosition.Y:0.0})",
                $"carryingIntel={(bot.IsCarryingIntel ? 1 : 0)}",
                $"steer=move:{controller.LastSteeringOutput.MoveDirection:0} jump:{(controller.LastSteeringOutput.Jump ? 1 : 0)} drop:{(controller.LastSteeringOutput.DropDown ? 1 : 0)} state:{controller.LastSteeringOutput.State}",
                $"alive:{(bot.IsAlive ? 1 : 0)} grounded:{(bot.IsGrounded ? 1 : 0)} vspeed:{bot.VerticalSpeed:0.0}",
                controller.CurrentPath is { } currentPath
                    ? FormatPathTrace(currentPath, graphForTrace)
                    : string.Empty,
            }.Where(static trace => !string.IsNullOrWhiteSpace(trace)));
        var reason = world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            ? $"timeout_caps:{GetTeamCaps(world, team) - GetTeamCaps(initialRedCaps, initialBlueCaps, team)}"
            : $"timeout_point:{DescribeControlPointState(world, team, initialControlPointTeams)}";
        if (!string.IsNullOrWhiteSpace(controllerTrace))
        {
            reason += $" trace:{controllerTrace}";
        }
        return CaptureTrialResult.Failed(world.MatchRules.Mode, reason, startX, startY, bot.X, bot.Y);
    }

    private static bool HasCapturedObjective(
        SimulationWorld world,
        PlayerEntity bot,
        PlayerTeam team,
        int initialRedCaps,
        int initialBlueCaps,
        IReadOnlyDictionary<int, PlayerTeam?> initialControlPointTeams)
    {
        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            return GetTeamCaps(world, team) > GetTeamCaps(initialRedCaps, initialBlueCaps, team);
        }

        if (world.MatchRules.Mode is GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill or GameModeKind.ControlPoint)
        {
            return world.ControlPoints.Any(point =>
                initialControlPointTeams.TryGetValue(point.Index, out var initialTeam)
                && initialTeam != team
                && point.Team == team
                && !point.IsLocked);
        }

        return world.MatchState.IsEnded && world.MatchState.WinnerTeam == team;
    }

    private static bool IsAttackDefenseDefensiveObjectiveSatisfied(
        SimulationWorld world,
        PlayerEntity bot,
        PlayerTeam team)
    {
        if (world.MatchRules.Mode != GameModeKind.ControlPoint
            || team != PlayerTeam.Blue
            || world.Level.GetRoomObjects(RoomObjectType.ControlPointSetupGate).Count == 0)
        {
            return false;
        }

        var defensiveFrontier = world.ControlPoints
            .Where(point => point.Team == team && !point.IsLocked)
            .OrderBy(point => point.Index)
            .FirstOrDefault();
        return defensiveFrontier is not null
            && world.IsPlayerInControlPointCaptureZone(bot, defensiveFrontier.Index);
    }

    private static int GetTeamCaps(SimulationWorld world, PlayerTeam team)
        => team == PlayerTeam.Red ? world.RedCaps : world.BlueCaps;

    private static int GetTeamCaps(int redCaps, int blueCaps, PlayerTeam team)
        => team == PlayerTeam.Red ? redCaps : blueCaps;

    private static string DescribeControlPointState(
        SimulationWorld world,
        PlayerTeam team,
        IReadOnlyDictionary<int, PlayerTeam?> initialControlPointTeams)
    {
        return string.Join(
            ',',
            world.ControlPoints.Select(point =>
            {
                initialControlPointTeams.TryGetValue(point.Index, out var initialTeam);
                return $"{point.Index}:{FormatTeam(initialTeam)}>{FormatTeam(point.Team)}";
            }));
    }

    private static string FormatTeam(PlayerTeam? team) => team?.ToString() ?? "none";

    private static string FormatPathTrace(NavPath path, NavGraph graph)
        => $"waypoints={string.Join(';', Enumerable.Range(0, path.Count).Select(index =>
        {
            var nodeIndex = path.GetWaypoint(index);
            var node = graph.GetNode(nodeIndex);
            return $"{nodeIndex}@({node.X:0.0},{node.Y:0.0})";
        }))}";

    private static IReadOnlyList<string> ReadList(
        IReadOnlyDictionary<string, string> rawOptions,
        string key,
        string? fallback)
    {
        if (!rawOptions.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return fallback is null
                ? []
                : fallback.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static PlayerClass? ParseClass(string text)
        => Enum.TryParse<PlayerClass>(text, ignoreCase: true, out var playerClass) ? playerClass : null;

    private static PlayerTeam? ParseTeam(string text)
        => Enum.TryParse<PlayerTeam>(text, ignoreCase: true, out var team) ? team : null;

    private static bool TryParseCoordinates(string text, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        var coordinates = text.Split(',', StringSplitOptions.TrimEntries);
        return coordinates.Length == 2
            && float.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            && float.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
    }

    private static bool TryParseRoute(
        string text,
        out float startX,
        out float startY,
        out float goalX,
        out float goalY)
    {
        startX = startY = goalX = goalY = 0f;
        var endpoints = text.Split(':', StringSplitOptions.TrimEntries);
        return endpoints.Length == 2
            && TryParseCoordinates(endpoints[0], out startX, out startY)
            && TryParseCoordinates(endpoints[1], out goalX, out goalY);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> rawOptions, string key, bool fallback)
        => rawOptions.TryGetValue(key, out var text) && bool.TryParse(text, out var value) ? value : fallback;

    private static string NormalizeMapName(string requestedMap)
        => requestedMap;

    private static void TryRegisterPackagedQuoteCurlyGameplayPack()
    {
        if (CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(PlayerClass.Quote, out _))
        {
            return;
        }

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
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
            Console.WriteLine($"alphaCapture quotePack=missing path=\"{packDirectory}\"");
            return;
        }

        var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory);
        if (!CharacterClassCatalog.RuntimeRegistry.TryRegisterModPack(
            pack,
            allowRuntimeClassBindingOverride: true,
            out var errorMessage))
        {
            throw new InvalidOperationException($"Failed to register Quote/Curly gameplay pack: {errorMessage}");
        }

        Console.WriteLine($"alphaCapture quotePack=loaded path=\"{packDirectory}\"");
    }

    private static string FindRepoRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenGarrison.sln"))
                || Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string FindPathSummary(
        NavGraph graph,
        IReadOnlyList<SpawnPoint> spawns,
        PlayerTeam team,
        PlayerClass playerClass,
        float goalX,
        float goalY,
        bool dumpPath,
        string label)
    {
        if (spawns.Count == 0)
        {
            return "no_spawn";
        }

        var bestPath = (NavPath?)null;
        var bestGoalNode = -1;
        var bestNearestReachable = -1;
        foreach (var spawn in spawns)
        {
            var startNode = graph.FindNearestTraversalStartNode(spawn.X, spawn.Y, maxAboveDistance: 48f);
            var goalNode = graph.FindNearestNode(goalX, goalY);
            if (startNode < 0 || goalNode < 0)
            {
                continue;
            }

            var path = graph.FindPath(startNode, goalNode, playerClass, team: team);
            if (path is not null && (bestPath is null || path.TotalCost < bestPath.TotalCost))
            {
                bestPath = path;
                bestGoalNode = goalNode;
                bestNearestReachable = -1;
            }
            else if (path is null)
            {
                var nearestReachable = graph.FindNearestReachableNode(
                    goalX,
                    goalY,
                    startNode,
                    playerClass,
                    team: team);
                if (bestPath is null && nearestReachable >= 0)
                {
                    bestGoalNode = goalNode;
                    bestNearestReachable = nearestReachable;
                }
            }
        }

        if (bestPath is not null)
        {
            if (dumpPath)
            {
                DumpPath(label, bestPath, graph);
            }

            return $"path:{bestPath.Count} cost:{bestPath.TotalCost:0} goalNode:{bestGoalNode}";
        }

        if (dumpPath && bestNearestReachable >= 0)
        {
            foreach (var spawn in spawns)
            {
                var startNode = graph.FindNearestTraversalStartNode(spawn.X, spawn.Y, maxAboveDistance: 48f);
                var path = graph.FindPath(startNode, bestNearestReachable, playerClass, team: team);
                if (path is not null)
                {
                    DumpPath($"{label}:nearest", path, graph);
                    break;
                }
            }
        }

        return bestNearestReachable >= 0
            ? $"unreachable_goal:{bestGoalNode} nearest_reachable:{bestNearestReachable}"
            : "no_path";
    }

    private static void DumpPath(string label, NavPath path, NavGraph graph)
    {
        Console.WriteLine(
            $"alphaNavPath label={label} nodes={path.Count} cost={path.TotalCost:0.0} " +
            string.Join(' ', Enumerable.Range(0, path.Count).Select(index =>
            {
                var node = graph.GetNode(path.GetWaypoint(index));
                var edgeLabel = index > 0 && path.TryGetIncomingEdge(index, out var edge)
                    ? $"<-{edge.Kind}"
                    : string.Empty;
                return $"{path.GetWaypoint(index)}@({node.X:0.0},{node.Y:0.0}){edgeLabel}";
            })));
    }

    private static void AuditObjectivePath(
        SimpleLevel level,
        NavGraph graph,
        IReadOnlyList<SpawnPoint> spawns,
        PlayerTeam team,
        float goalX,
        float goalY,
        string label,
        bool auditAllClasses)
    {
        var startNode = spawns.Count > 0
            ? graph.FindNearestTraversalStartNode(spawns[0].X, spawns[0].Y, maxAboveDistance: 48f)
            : -1;
        var goalNode = graph.FindNearestNode(goalX, goalY);
        var path = startNode >= 0 && goalNode >= 0
            ? graph.FindPath(startNode, goalNode, PlayerClass.Scout, team: team)
            : null;
        if (path is null)
        {
            Console.WriteLine($"alphaNavEdgeAudit label={label} status=no_path");
            return;
        }

        var classes = auditAllClasses
            ? DefaultCaptureClasses
            : [PlayerClass.Scout];
        for (var index = 1; index < path.Count; index += 1)
        {
            var fromNodeIndex = path.GetWaypoint(index - 1);
            var toNodeIndex = path.GetWaypoint(index);
            var from = graph.GetNode(fromNodeIndex);
            var to = graph.GetNode(toNodeIndex);
            if (!path.TryGetIncomingEdge(index, out var edge))
            {
                Console.WriteLine($"alphaNavEdgeAudit label={label} edge={fromNodeIndex}->{toNodeIndex} status=missing_edge");
                continue;
            }

            if (to.Kind == NavNodeKind.Objective)
            {
                Console.WriteLine(
                    $"alphaNavEdgeAudit label={label} edge={fromNodeIndex}->{toNodeIndex} kind={edge.Kind} status=objective_anchor");
                continue;
            }

            foreach (var playerClass in classes)
            {
                var definition = CharacterClassCatalog.GetDefinition(playerClass);
                var failure = string.Empty;
                bool works;
                if (edge.Kind == NavEdgeKind.Jump)
                {
                    works = BotNavigationMovementValidator.TryBuildJumpTape(
                        level,
                        definition,
                        BotNavigationProfile.Standard,
                        from.X,
                        from.Y,
                        to.X,
                        to.Y,
                        team,
                        out _,
                        out _);
                }
                else
                {
                    works = BotNavigationMovementValidator.TryBuildGroundTape(
                        level,
                        definition,
                        from.X,
                        from.Y,
                        to.X,
                        to.Y,
                        team,
                        out _,
                        out _,
                        out failure);
                }
                Console.WriteLine(
                    $"alphaNavEdgeAudit label={label} edge={fromNodeIndex}->{toNodeIndex} " +
                    $"from=({from.X:0.0},{from.Y:0.0}) to=({to.X:0.0},{to.Y:0.0}) " +
                    $"kind={edge.Kind} class={playerClass} validator={(works ? "pass" : "fail")}" +
                    (works || edge.Kind == NavEdgeKind.Jump ? string.Empty : $" failure={failure}"));
            }
        }
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> rawOptions, string key, int fallback)
    {
        return rawOptions.TryGetValue(key, out var text) && int.TryParse(text, out var value)
            ? value
            : fallback;
    }

    private sealed record CaptureTrialResult(
        bool Passed,
        GameModeKind Mode,
        int CompletionTick,
        string Reason,
        float StartX,
        float StartY,
        float EndX,
        float EndY)
    {
        public static CaptureTrialResult Succeeded(
            GameModeKind mode,
            int tick,
            string reason,
            float startX,
            float startY,
            float endX,
            float endY)
            => new(true, mode, tick, reason, startX, startY, endX, endY);

        public static CaptureTrialResult Failed(
            GameModeKind mode,
            string reason,
            float startX = 0f,
            float startY = 0f,
            float endX = 0f,
            float endY = 0f)
            => new(false, mode, -1, reason, startX, startY, endX, endY);
    }
}
