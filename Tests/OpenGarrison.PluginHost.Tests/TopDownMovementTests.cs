using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(MapDirectoryTestGroup.Name)]
public sealed class TopDownMovementTests
{
    [Fact]
    public void TopDownMovementUsesBothAxesAndSuppressesJump()
    {
        var level = CreateTopDownLevel();
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Top-down test");
        player.Spawn(PlayerTeam.Red, 240f, 240f);
        player.TeleportTo(240f, 240f);
        var startY = player.Y;

        var input = CreateInput(up: true);
        var jumped = player.Advance(
            input,
            jumpPressed: true,
            level,
            PlayerTeam.Red,
            1d / SimulationConfig.DefaultTicksPerSecond);

        Assert.False(jumped);
        Assert.True(player.Y < startY, $"expected top-down up input to move vertically, start={startY:0.###}, actual={player.Y:0.###}");
        Assert.True(player.IsGrounded);
    }

    [Fact]
    public void TopDownMovementStopsAtWalkmaskSolid()
    {
        var wall = new LevelSolid(320f, 100f, 32f, 320f);
        var level = CreateTopDownLevel([wall]);
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Top-down test");
        player.Spawn(PlayerTeam.Red, 240f, 240f);
        player.TeleportTo(240f, 240f);

        var input = CreateInput(right: true);
        for (var tick = 0; tick < 120; tick += 1)
        {
            player.Advance(
                input,
                jumpPressed: false,
                level,
                PlayerTeam.Red,
                1d / SimulationConfig.DefaultTicksPerSecond);
        }

        Assert.True(
            player.Right <= wall.Left + 0.5f,
            $"expected top-down collision against walkmask wall, right={player.Right:0.###}, wallLeft={wall.Left:0.###}");
    }

    [Fact]
    public void TopDownMovementSlidesAlongWalkmaskSolid()
    {
        var wall = new LevelSolid(320f, 0f, 32f, 600f);
        var level = CreateTopDownLevel([wall]);
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Top-down test");
        player.Spawn(PlayerTeam.Red, 240f, 240f);
        player.TeleportTo(240f, 240f);

        var input = CreateInput(right: true, down: true);
        for (var tick = 0; tick < 60; tick += 1)
        {
            player.Advance(
                input,
                jumpPressed: false,
                level,
                PlayerTeam.Red,
                1d / SimulationConfig.DefaultTicksPerSecond);
        }

        Assert.True(player.Right <= wall.Left + 0.5f, $"right={player.Right:0.###}, wallLeft={wall.Left:0.###}, y={player.Y:0.###}");
        Assert.True(player.Y > 240f, $"expected free vertical movement while blocked horizontally, y={player.Y:0.###}");
    }

    [Fact]
    public void TopDownGraphRoutesAroundWalkmaskSolidToObjective()
    {
        var level = CreateTopDownLevel(
        [
            new LevelSolid(320f, 0f, 32f, 210f),
            new LevelSolid(320f, 310f, 32f, 290f),
        ]);

        var graph = Og2NavigationGraphBuilder.Build(level);
        var start = graph.FindNearestNode(160f, 300f);
        var goal = graph.FindNearestNode(520f, 300f);
        var path = graph.FindPath(start, goal, CharacterClassCatalog.Scout.Id, team: PlayerTeam.Red);

        Assert.True(graph.NodeCount > 0);
        Assert.NotNull(path);
        Assert.True(path!.Count > 2, $"expected graph to route around the wall gap, waypoints={path.Count}");
    }

    [Fact]
    public void TopDownGraphAttachesTriggerSpawnAndRoutesEveryClassAcrossOpenMap()
    {
        var level = CreateTopDownLevel(
            botSpawns:
            [
                new BotSpawnMarker(
                    128f,
                    300f,
                    string.Empty,
                    PlayerTeam.Red,
                    null,
                    BotSpawnKind.Bot,
                    true,
                    BotSpawnRespawnMode.NormalSpawn,
                    BotSpawnNameMode.Random,
                    string.Empty,
                    ForceNameplate: false,
                    ForceHealthBar: false,
                    string.Empty),
            ]);

        var graph = Og2NavigationGraphBuilder.Build(level);
        var start = graph.FindNearestNode(128f, 300f);
        var goal = graph.FindNearestNode(400f, 300f);

        Assert.True(graph.NodeCount > 0);
        foreach (var playerClass in Enum.GetValues<PlayerClass>())
        {
            var path = graph.FindPath(start, goal, CharacterClassCatalog.GetDefinition(playerClass).Id, team: PlayerTeam.Red);
            Assert.NotNull(path);
        }
    }

    [Fact]
    public void CtfHangarRoutesBothTeamsFromRealSpawnsToEnemyIntel()
    {
        var mapsDirectory = ResolveCanonicalHangarMapsDirectory();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        var previousContentRoot = ContentRoot.Path;
        var coreContent = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content"));
        Assert.False(string.IsNullOrWhiteSpace(coreContent));
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", mapsDirectory);
        ContentRoot.Initialize(coreContent!);
        SimpleLevelFactory.ClearCachedCatalog();

        try
        {
        var level = SimpleLevelFactory.CreateImportedLevel("ctf_hangar");

        Assert.NotNull(level);
        Assert.True(level!.IsTopDown);
        Assert.NotEmpty(level.RedSpawns);
        Assert.NotEmpty(level.BlueSpawns);

        Assert.True(Og2NavigationGraphStore.TryLoadShipped(level, out var graph));
        foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
        {
            var startSpawn = team == PlayerTeam.Red ? level.RedSpawns[0] : level.BlueSpawns[0];
            var enemyIntel = level.GetIntelBase(team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red);
            Assert.True(enemyIntel.HasValue, $"missing enemy intel for {team}");

            var start = graph.FindNearestNode(startSpawn.X, startSpawn.Y);
            var goal = graph.FindNearestNode(enemyIntel!.Value.X, enemyIntel.Value.Y);
            var path = graph.FindPath(start, goal, CharacterClassCatalog.Scout.Id, team: team);
            var reachable = new HashSet<int> { start };
            var pending = new Queue<int>([start]);
            while (pending.Count > 0)
            {
                foreach (var edge in graph.GetEdges(pending.Dequeue()))
                {
                    if (reachable.Add(edge.ToNode))
                    {
                        pending.Enqueue(edge.ToNode);
                    }
                }
            }

            Assert.True(
                path is not null,
                $"{team} route missing: start=({startSpawn.X},{startSpawn.Y}) node={start} edges={graph.GetEdges(start).Length}, goal=({enemyIntel.Value.X},{enemyIntel.Value.Y}) node={goal} edges={graph.GetEdges(goal).Length}, reachable={reachable.Count}, goalReachable={reachable.Contains(goal)}, graphNodes={graph.NodeCount}, componentBounds=({reachable.Min(node => graph.GetNode(node).X):0.0},{reachable.Min(node => graph.GetNode(node).Y):0.0})-({reachable.Max(node => graph.GetNode(node).X):0.0},{reachable.Max(node => graph.GetNode(node).Y):0.0})");
            Assert.True(path!.Count > 1, $"{team} path did not leave spawn: waypoints={path.Count}");

            var ownIntelMarker = level.GetIntelBase(team);
            Assert.True(ownIntelMarker.HasValue, $"missing own intel for {team}");
            var ownIntel = graph.FindNearestNode(ownIntelMarker!.Value.X, ownIntelMarker.Value.Y);
            var returnPath = graph.FindPath(
                goal,
                ownIntel,
                CharacterClassCatalog.Scout.Id,
                team: team,
                carryingIntel: true);
            Assert.True(returnPath is not null, $"{team} carrier return route missing from node {goal} to {ownIntel}");
            Assert.True(returnPath!.Count > 1, $"{team} carrier return did not leave enemy base: waypoints={returnPath.Count}");
        }
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            ContentRoot.Initialize(previousContentRoot);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void CtfHangarBotLeavesEachSpawnAndCompletesCapture()
    {
        var mapsDirectory = ResolveCanonicalHangarMapsDirectory();
        var previousMapsDirectory = Environment.GetEnvironmentVariable("OPENGARRISON_MAPS_DIR");
        var previousContentRoot = ContentRoot.Path;
        var coreContent = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content"));
        Assert.False(string.IsNullOrWhiteSpace(coreContent));
        Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", mapsDirectory);
        ContentRoot.Initialize(coreContent!);
        SimpleLevelFactory.ClearCachedCatalog();

        try
        {
            foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
            {
                var world = new SimulationWorld(new SimulationConfig
                {
                    EnableEnemyTrainingDummy = false,
                    EnableFriendlySupportDummy = false,
                });
                Assert.True(world.TryLoadLevel("ctf_hangar"));

                const byte botSlot = 2;
                Assert.True(world.TryPrepareNetworkPlayerJoin(botSlot));
                Assert.True(world.TrySetNetworkPlayerTeam(botSlot, team));
                // The current packaged Hangar routes through authored fire
                // zones. Use Pyro so this remains a navigation/capture test
                // instead of repeatedly dying to the map's afterburn hazard.
                Assert.True(world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Pyro));
                Assert.True(world.TryGetNetworkPlayer(botSlot, out var bot));

                // The canonical Hangar map ships its OG2 graph. This assertion
                // keeps the integration harness on the same graph handoff as
                // live practice/server startup and prevents a cold build from
                // masking a missing shipped asset.
                Assert.True(Og2NavigationGraphStore.TryLoadShipped(world.Level, out var graph));

                var startX = bot.X;
                var startY = bot.Y;
                var controller = new BotBrainPracticeBotController();
                var controlledSlots = new Dictionary<byte, ControlledBotSlot>
                {
                    [botSlot] = new(botSlot, team, PlayerClass.Scout),
                };

                // Install the graph-backed brain through the normal practice
                // wrapper so this test still exercises world input plumbing.
                _ = controller.BuildInputsForSlots(world, controlledSlots, Array.Empty<byte>());
                var controllersField = typeof(BotBrainPracticeBotController).GetField(
                    "_controllersBySlot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(controllersField);
                var controllers = (Dictionary<byte, BotBrainController>)controllersField!.GetValue(controller)!;
                controllers[botSlot] = new BotBrainController(graph, forceAlphaNavigation: true)
                {
                    DisableCombatForDiagnostics = true,
                    ForceObjectiveNavigationForDiagnostics = true,
                };
                var capturedAtTick = -1;

                for (var tick = 0; tick < 4_000; tick += 1)
                {
                    var inputs = controller.BuildInputs(world, controlledSlots);

                    Assert.True(inputs.TryGetValue(botSlot, out var input));
                    Assert.True(world.TrySetNetworkPlayerInput(botSlot, input));
                    world.AdvanceOneTick();

                    var score = team == PlayerTeam.Red ? world.RedCaps : world.BlueCaps;
                    if (score > 0)
                    {
                        capturedAtTick = tick;
                        break;
                    }
                }

                Assert.True(
                    capturedAtTick >= 0,
                    $"{team} did not capture: start=({startX:0.0},{startY:0.0}) end=({bot.X:0.0},{bot.Y:0.0}) " +
                    $"carryingIntel={bot.IsCarryingIntel} " +
                    $"redCaps={world.RedCaps} blueCaps={world.BlueCaps} " +
                    $"trace={(controller.TryGetBotBrainController(botSlot, out var failedBrain) ? failedBrain!.LastTraversalTrace : "missing-brain")} " +
                    $"goal={(controller.TryGetBotBrainController(botSlot, out failedBrain) ? $"({failedBrain!.CurrentGoalPosition.X:0.0},{failedBrain.CurrentGoalPosition.Y:0.0})" : "missing" )} " +
                    $"path={(controller.TryGetBotBrainController(botSlot, out failedBrain) ? $"{failedBrain!.CurrentPathIndex}/{failedBrain.CurrentPathCount} node={failedBrain.CurrentPathNode}" : "missing")} " +
                    $"nodePos={(controller.TryGetBotBrainController(botSlot, out failedBrain) ? $"({failedBrain!.CurrentPathNodePosition.X:0.0},{failedBrain.CurrentPathNodePosition.Y:0.0})" : "missing")} " +
                    $"edge={(controller.TryGetBotBrainController(botSlot, out failedBrain) && failedBrain!.CurrentPath is { } failedPath && failedPath.TryGetCurrentEdge(out var failedEdge) ? $"team={failedEdge.SupportedTeamMask} carry={failedEdge.CarryingIntelRequirement}" : "none")} " +
                    $"barriers={world.Level.RoomObjects.Count(static marker => marker.Type == RoomObjectType.Barrier)} " +
                    $"steering={(controller.TryGetBotBrainController(botSlot, out failedBrain) ? $"({failedBrain!.LastSteeringOutput.MoveDirection:0.0},{failedBrain.LastSteeringOutput.MoveDirectionY:0.0}) state={failedBrain.LastSteeringOutput.State} repath={failedBrain.LastSteeringOutput.RequestRepath}" : "missing")}");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGARRISON_MAPS_DIR", previousMapsDirectory);
            ContentRoot.Initialize(previousContentRoot);
            SimpleLevelFactory.ClearCachedCatalog();
        }
    }

    [Fact]
    public void TopDownSentryStaysAtPlacementInsteadOfFalling()
    {
        var level = CreateTopDownLevel();
        var sentry = new SentryEntity(1, 2, PlayerTeam.Red, 400f, 240f, 1f);
        sentry.Advance(level, level.Bounds);

        Assert.Equal(240f, sentry.Y);
        Assert.True(sentry.HasLanded);
        Assert.Equal(0f, sentry.VerticalSpeed);
    }

    [Fact]
    public void TopDownSteeringEmitsVerticalIntentInsteadOfPlatformerJumpLogic()
    {
        var level = CreateTopDownLevel();
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Top-down test");
        player.Spawn(PlayerTeam.Red, 240f, 240f);
        player.TeleportTo(240f, 240f);

        var nodes = new[]
        {
            new NavNode(240f, 240f, NavNodeKind.Surface, 1),
            new NavNode(240f, 80f, NavNodeKind.Objective, 1),
        };
        var adjacency = new[]
        {
            new List<NavEdge> { new(1, NavEdgeKind.Walk, 160f) },
            new List<NavEdge>(),
        };
        var graph = new NavGraph(nodes, adjacency, levelName: level.Name, mode: level.Mode);
        var path = new NavPath([0, 1], 160f);

        var steering = new SteeringMachine().Update(player, graph, path, level, PlayerTeam.Red);

        Assert.Equal(0f, steering.MoveDirection);
        Assert.True(steering.MoveDirectionY < 0f);
        Assert.False(steering.Jump);
    }

    [Fact]
    public void TopDownRuntimeNavigationRecomputesWalkDirectionEveryTick()
    {
        var level = CreateTopDownLevel();
        var world = new SimulationWorld();
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        _ = setLevel!.Invoke(world, [level]);
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        var player = world.LocalPlayer;
        player.TeleportTo(160f, 300f);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);

        var graph = new NavGraph(
            nodes:
            [
                new NavNode(160f, 300f, NavNodeKind.Surface, 1),
                new NavNode(160f, 100f, NavNodeKind.Surface, 1),
                new NavNode(520f, 100f, NavNodeKind.Objective, 1),
            ],
            adjacency:
            [
                new List<NavEdge> { new(1, NavEdgeKind.Walk, 200f) },
                new List<NavEdge> { new(2, NavEdgeKind.Walk, 360f) },
                new List<NavEdge>(),
            ],
            levelName: level.Name,
            mode: level.Mode);
        var controller = new BotBrainController(graph, forceAlphaNavigation: true);

        var firstInput = controller.Think(player, world, PlayerTeam.Red);
        Assert.True(firstInput.Up);
        Assert.False(firstInput.Right);

        // Simulate the physics step reaching the corner while the scheduler is
        // still using its cheap per-tick navigation heartbeat. The next input
        // must be recomputed for the next walk edge; returning the cached Up
        // input is the regression that made bots walk into walls forever.
        player.TeleportTo(160f, 100f);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var cornerInput = controller.ThinkRuntimeContact(player, world, PlayerTeam.Red);

        Assert.True(cornerInput.Right);
        Assert.False(cornerInput.Up);
    }

    [Fact]
    public void TopDownFullAndCachedNavigationPreserveDiagonalIntent()
    {
        var level = CreateTopDownLevel();
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        _ = setLevel!.Invoke(world, [level]);
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        var player = world.LocalPlayer;
        player.TeleportTo(160f, 300f);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);

        var graph = CreateDiagonalObjectiveGraph(level);
        var controller = new BotBrainController(graph, forceAlphaNavigation: true)
        {
            ForceObjectiveNavigationForDiagnostics = true,
        };

        var fullInput = controller.Think(player, world, PlayerTeam.Red);
        Assert.True(fullInput.Right, "full brain lost horizontal half of diagonal route intent");
        Assert.True(fullInput.Up, "full brain lost vertical half of diagonal route intent");

        var cachedFrame = fullInput with
        {
            FirePrimary = true,
            AimWorldX = 777f,
            AimWorldY = 333f,
        };
        Assert.True(controller.TryAdvanceCachedNavigation(
            player,
            world,
            PlayerTeam.Red,
            cachedFrame,
            out var cachedInput));
        Assert.True(cachedInput.Right, "cached navigation lost horizontal diagonal intent");
        Assert.True(cachedInput.Up, "cached navigation lost vertical diagonal intent");
        Assert.True(cachedInput.FirePrimary, "cached navigation overwrote held combat state");
        Assert.Equal(777f, cachedInput.AimWorldX);
        Assert.Equal(333f, cachedInput.AimWorldY);
    }

    [Fact]
    public void TopDownFullThinkSeparatesOverlappingSameTeamBotsIntoStableLanes()
    {
        var level = CreateTopDownLevel();
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        _ = setLevel!.Invoke(world, [level]);
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(2, out var ally));

        var local = world.LocalPlayer;
        local.TeleportTo(240f, 240f);
        local.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        ally.TeleportTo(240f, 240f);
        ally.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var graph = CreateDiagonalObjectiveGraph(level);
        var localController = new BotBrainController(graph, forceAlphaNavigation: true)
        {
            ForceObjectiveNavigationForDiagnostics = true,
        };
        var allyController = new BotBrainController(graph, forceAlphaNavigation: true)
        {
            ForceObjectiveNavigationForDiagnostics = true,
        };

        var localInput = localController.Think(local, world, PlayerTeam.Red);
        var allyInput = allyController.Think(ally, world, PlayerTeam.Red);
        Assert.NotEqual(
            (localInput.Left, localInput.Right, localInput.Up, localInput.Down),
            (allyInput.Left, allyInput.Right, allyInput.Up, allyInput.Down));
    }

    [Fact]
    public void ScheduledTopDownFullThinkReacquiresAndFiresWithActiveRoute()
    {
        var level = CreateTopDownLevel();
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        _ = setLevel!.Invoke(world, [level]);

        const byte botSlot = 2;
        Assert.True(world.TryPrepareNetworkPlayerJoin(botSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(botSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(botSlot, out var bot));
        bot.TeleportTo(240f, 240f);
        bot.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);

        var graph = CreateDiagonalObjectiveGraph(level);
        var practiceController = new BotBrainPracticeBotController();
        var controlledSlots = new Dictionary<byte, ControlledBotSlot>
        {
            [botSlot] = new(botSlot, PlayerTeam.Red, PlayerClass.Soldier),
        };

        // Configure the normal per-slot practice wrapper, then install a
        // deterministic graph-backed brain for this route/combat regression.
        _ = practiceController.BuildInputsForSlots(world, controlledSlots, Array.Empty<byte>());
        var controllersField = typeof(BotBrainPracticeBotController).GetField(
            "_controllersBySlot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(controllersField);
        var controllers = (Dictionary<byte, BotBrainController>)controllersField!.GetValue(practiceController)!;
        controllers[botSlot] = new BotBrainController(graph, forceAlphaNavigation: true);

        _ = practiceController.BuildInputsForSlots(world, controlledSlots, [botSlot]);
        Assert.True(practiceController.RequiresPerTickNavigationThink(botSlot));

        const byte enemySlot = 3;
        Assert.True(world.TryPrepareNetworkPlayerJoin(enemySlot));
        Assert.True(world.TrySetNetworkPlayerTeam(enemySlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(enemySlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(enemySlot, out var enemy));
        enemy.TeleportTo(320f, 240f);
        enemy.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);

        var input = practiceController.BuildInputsForSlots(world, controlledSlots, [botSlot])[botSlot];
        Assert.True(practiceController.TryGetBotBrainController(botSlot, out var brain));
        Assert.NotNull(brain);
        Assert.NotNull(brain!.LastCombatTarget);
        Assert.True(input.FirePrimary, "scheduled full think lost combat synthesis while top-down route was active");
    }

    [Fact]
    public void TopDownSteeringRequestsRepathWhenAStaticEdgeMakesNoProgress()
    {
        var level = CreateTopDownLevel();
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Top-down test");
        player.Spawn(PlayerTeam.Red, 240f, 240f);
        player.TeleportTo(240f, 240f);

        var nodes = new[]
        {
            new NavNode(240f, 240f, NavNodeKind.Surface, 1),
            new NavNode(480f, 240f, NavNodeKind.Objective, 1),
        };
        var adjacency = new[]
        {
            new List<NavEdge> { new(1, NavEdgeKind.Walk, 240f) },
            new List<NavEdge>(),
        };
        var graph = new NavGraph(nodes, adjacency, levelName: level.Name, mode: level.Mode);
        var path = new NavPath([0, 1], 240f);
        var steeringMachine = new SteeringMachine();

        SteeringOutput steering = default;
        for (var tick = 0; tick < 13; tick += 1)
        {
            steering = steeringMachine.Update(player, graph, path, level, PlayerTeam.Red);
        }

        Assert.True(steering.RequestRepath);
        Assert.Equal("top_down_stuck", steering.FailedEdge.Reason);
    }

    [Fact]
    public void TopDownSteeringSidestepsWhenWaypointIsImmediatelyBehindAWall()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Top-down test");
        player.Spawn(PlayerTeam.Red, 240f, 240f);
        player.TeleportTo(240f, 240f);
        var wall = new LevelSolid(
            player.Right + 1f,
            player.Top - 16f,
            160f,
            (player.Bottom - player.Top) + 32f);
        var level = CreateTopDownLevel([wall]);
        var nodes = new[]
        {
            new NavNode(240f, 240f, NavNodeKind.Surface, 1),
            new NavNode(480f, 240f, NavNodeKind.Objective, 1),
        };
        var adjacency = new[]
        {
            new List<NavEdge> { new(1, NavEdgeKind.Walk, 240f) },
            new List<NavEdge>(),
        };
        var graph = new NavGraph(nodes, adjacency, levelName: level.Name, mode: level.Mode);
        var path = new NavPath([0, 1], 240f);

        var steering = new SteeringMachine().Update(player, graph, path, level, PlayerTeam.Red);

        Assert.Equal(0f, steering.MoveDirection);
        Assert.NotEqual(0f, steering.MoveDirectionY);
        Assert.False(steering.Jump);
    }

    [Fact]
    public void TopDownSteeringKeepsCommittedDetourDirectionAcrossTicks()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Top-down test");
        player.Spawn(PlayerTeam.Red, 240f, 240f);
        // A small body makes both sides of the synthetic blocker clear at the
        // 8px probe distance, which isolates the detour commitment itself.
        player.SetPlayerScale(0.25f);
        player.TeleportTo(240f, 240f);
        var wall = new LevelSolid(
            player.Right + 1f,
            player.Top + 1f,
            160f,
            MathF.Max(1f, (player.Bottom - player.Top) - 2f));
        var level = CreateTopDownLevel([wall]);
        var graph = new NavGraph(
            nodes:
            [
                new NavNode(240f, 240f, NavNodeKind.Surface, 1),
                new NavNode(480f, 240f, NavNodeKind.Objective, 1),
            ],
            adjacency:
            [
                new List<NavEdge> { new(1, NavEdgeKind.Walk, 240f) },
                new List<NavEdge>(),
            ],
            levelName: level.Name,
            mode: level.Mode);
        var path = new NavPath([0, 1], 240f);
        var steeringMachine = new SteeringMachine();

        var first = steeringMachine.Update(player, graph, path, level, PlayerTeam.Red);
        var second = steeringMachine.Update(player, graph, path, level, PlayerTeam.Red);

        Assert.NotEqual(0f, first.MoveDirectionY);
        Assert.Equal(first.MoveDirectionY, second.MoveDirectionY);
        Assert.Equal(0f, first.MoveDirection);
        Assert.Equal(0f, second.MoveDirection);
    }

    [Fact]
    public void TopDownPathlessRecoveryDoesNotDriveIntoBlockedObjectiveAxis()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Top-down test");
        player.Spawn(PlayerTeam.Red, 240f, 240f);
        player.TeleportTo(240f, 240f);
        var wall = new LevelSolid(
            player.Right + 1f,
            player.Top - 16f,
            160f,
            (player.Bottom - player.Top) + 32f);
        var level = CreateTopDownLevel([wall]);

        var recovery = BotBrainController.ResolveTopDownObjectiveMove(
            level,
            player,
            PlayerTeam.Red,
            deltaX: 240f,
            deltaY: 0f);

        Assert.Equal(0f, recovery.MoveX);
        Assert.NotEqual(0f, recovery.MoveY);
        Assert.True(
            player.CanOccupy(
                level,
                PlayerTeam.Red,
                player.X,
                player.Y + (recovery.MoveY * 8f)));
    }

    [Fact]
    public void WarmedNonShippedGraphIsAttachedByBotThinkWithoutBuilding()
    {
        var level = CreateTopDownLevel(name: $"topdown_warmed_runtime_{Guid.NewGuid():N}");
        Assert.False(Og2NavigationGraphStore.TryLoadShipped(level, out _));
        Assert.False(Og2NavigationGraphStore.TryGetCached(level, out _));

        var warmedGraph = Og2NavigationGraphStore.GetOrBuild(level);
        Assert.True(Og2NavigationGraphStore.TryGetCached(level, out var cachedGraph));
        Assert.Same(warmedGraph, cachedGraph);

        var world = CreateWorldForTopDownLevel(level);
        var controller = new BotBrainController
        {
            ForceObjectiveNavigationForDiagnostics = true,
            DisableCombatForDiagnostics = true,
        };

        _ = controller.Think(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.True(controller.HasNavigationGraph);
        Assert.Equal("memory", controller.LastNavigationGraphSource);
        Assert.True(controller.CurrentPathCount > 0);
    }

    [Fact]
    public void UnwarmedControllerDoesNotGenerateAnOg2GraphDuringThink()
    {
        var level = CreateTopDownLevel(name: $"topdown_unwarmed_runtime_{Guid.NewGuid():N}");
        var world = CreateWorldForTopDownLevel(level);
        var controller = new BotBrainController(disableShippedNavigationGraph: false)
        {
            ForceObjectiveNavigationForDiagnostics = true,
            DisableCombatForDiagnostics = true,
        };

        Assert.False(Og2NavigationGraphStore.TryGetCached(level, out _));
        _ = controller.Think(world.LocalPlayer, world, PlayerTeam.Red);

        Assert.False(controller.HasNavigationGraph);
        Assert.Equal("none", controller.LastNavigationGraphSource);
        Assert.False(Og2NavigationGraphStore.TryGetCached(level, out _));
    }

    [Fact]
    public void TopDownAllySeparationNeverReversesAnActiveGraphRoute()
    {
        var level = CreateTopDownLevel(name: $"topdown_ally_route_{Guid.NewGuid():N}");
        var world = CreateWorldForTopDownLevel(level);
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(2, out var ally));

        var player = world.LocalPlayer;
        player.TeleportTo(240f, 240f);
        ally.TeleportTo(264f, 240f);
        var graph = new NavGraph(
            nodes:
            [
                new NavNode(240f, 240f, NavNodeKind.Surface, 1),
                new NavNode(520f, 240f, NavNodeKind.Objective, 1),
            ],
            adjacency:
            [
                new List<NavEdge> { new(1, NavEdgeKind.Walk, 280f) },
                new List<NavEdge>(),
            ],
            levelName: level.Name,
            mode: level.Mode);
        var controller = new BotBrainController(graph, forceAlphaNavigation: true)
        {
            ForceObjectiveNavigationForDiagnostics = true,
            DisableCombatForDiagnostics = true,
        };

        var input = controller.Think(player, world, PlayerTeam.Red);

        Assert.False(input.Left, "ally separation must not reverse the active graph edge");
        Assert.True(input.Right || input.Up || input.Down, "ally separation left the bot with no movement lane");
    }

    [Fact]
    public void TopDownRemainsPreserveDeathPosition()
    {
        var level = CreateTopDownLevel();
        var bounds = level.Bounds;

        var body = new DeadBodyEntity(
            id: 1,
            sourcePlayerId: 2,
            classId: PlayerClass.Scout,
            team: PlayerTeam.Red,
            animationKind: DeadBodyAnimationKind.Default,
            x: 240f,
            y: 240f,
            width: 24f,
            height: 40f,
            horizontalSpeed: 6f,
            verticalSpeed: 8f,
            facingLeft: false);
        body.Advance(level, bounds, preservePosition: level.IsTopDown);

        var gib = new PlayerGibEntity(
            id: 3,
            spriteName: "GibS",
            frameIndex: 0,
            x: 240f,
            y: 240f,
            velocityX: 6f,
            velocityY: 8f,
            rotationSpeedDegrees: 15f,
            horizontalFriction: 0.4f,
            rotationFriction: 0.6f,
            lifetimeTicks: 30);
        gib.Advance(level, bounds, preservePosition: level.IsTopDown);

        var bloodDrop = new BloodDropEntity(4, 240f, 240f, 6f, 8f);
        bloodDrop.Advance(level, bounds, preservePosition: level.IsTopDown);

        Assert.Equal(240f, body.X);
        Assert.Equal(240f, body.Y);
        Assert.Equal(240f, gib.X);
        Assert.Equal(240f, gib.Y);
        Assert.Equal(240f, bloodDrop.X);
        Assert.Equal(240f, bloodDrop.Y);
        Assert.True(bloodDrop.IsStuck);
    }

    [Fact]
    public void TopDownGibsExplodeAcrossGroundInsteadOfFreezingInPlace()
    {
        var level = CreateTopDownLevel();
        var gib = new PlayerGibEntity(
            id: 1,
            spriteName: "GibS",
            frameIndex: 0,
            x: 240f,
            y: 240f,
            velocityX: 6f,
            velocityY: -4f,
            rotationSpeedDegrees: 15f,
            horizontalFriction: 0.4f,
            rotationFriction: 0.6f,
            lifetimeTicks: 30);

        gib.Advance(level, level.Bounds, topDown: true);

        Assert.True(gib.X > 240f);
        Assert.True(gib.Y < 240f);
        Assert.NotEqual(0f, gib.RotationDegrees);
    }

    private static SimpleLevel CreateTopDownLevel(
        IReadOnlyList<LevelSolid>? solids = null,
        IReadOnlyList<BotSpawnMarker>? botSpawns = null,
        string? name = null)
    {
        return new SimpleLevel(
            name: name ?? "topdown_test",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(800f, 600f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(160f, 300f),
            redSpawns: [new SpawnPoint(160f, 300f)],
            blueSpawns: [new SpawnPoint(520f, 300f)],
            intelBases:
            [
                new IntelBaseMarker(PlayerTeam.Red, 160f, 300f),
                new IntelBaseMarker(PlayerTeam.Blue, 520f, 300f),
            ],
            roomObjects: [],
            floorY: 600f,
            solids: solids ?? [],
            importedFromSource: false,
            botSpawns: botSpawns,
            isTopDown: true);
    }

    private static SimulationWorld CreateWorldForTopDownLevel(SimpleLevel level)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        _ = setLevel!.Invoke(world, [level]);
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        world.LocalPlayer.RestoreMovementProbeState(
            isGrounded: true,
            remainingAirJumps: null,
            facingDirectionX: 1f);
        return world;
    }

    private static string ResolveCanonicalHangarMapsDirectory()
    {
        // Resolve the manifest rather than the directory: build output can
        // contain an empty Maps/ctf_hangar shadow directory, while the source
        // manifest uniquely identifies the canonical packaged map.
        var manifestPath = ProjectSourceLocator.FindFile(
            Path.Combine("Maps", "ctf_hangar", "ctf_hangar.json"));
        Assert.False(string.IsNullOrWhiteSpace(manifestPath));
        var mapDirectory = Path.GetDirectoryName(manifestPath!);
        Assert.False(string.IsNullOrWhiteSpace(mapDirectory));
        var mapsDirectory = Path.GetDirectoryName(mapDirectory!);
        Assert.False(string.IsNullOrWhiteSpace(mapsDirectory));
        return mapsDirectory!;
    }

    private static NavGraph CreateDiagonalObjectiveGraph(SimpleLevel level)
    {
        return new NavGraph(
            nodes:
            [
                new NavNode(160f, 300f, NavNodeKind.Surface, 1),
                new NavNode(300f, 180f, NavNodeKind.Surface, 1),
                new NavNode(520f, 300f, NavNodeKind.Objective, 1),
            ],
            adjacency:
            [
                new List<NavEdge> { new(1, NavEdgeKind.Walk, 184f) },
                new List<NavEdge> { new(2, NavEdgeKind.Walk, 251f) },
                new List<NavEdge>(),
            ],
            levelName: level.Name,
            mode: level.Mode);
    }

    private static PlayerInputSnapshot CreateInput(bool left = false, bool right = false, bool up = false, bool down = false)
    {
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
            AimWorldX: 0f,
            AimWorldY: 0f,
            DebugKill: false);
    }
}
