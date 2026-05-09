using OpenGarrison.BotAI;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotClientBotParityTests
{
    [Fact]
    public void ModernBotRoleTracksCaptureTheFlagCarrierState()
    {
        var world = new SimulationWorld();
        var bot = CreatePlayer(100, PlayerTeam.Red, x: 100f, y: 100f);
        var allyCarrier = CreatePlayer(101, PlayerTeam.Red, x: 200f, y: 100f);
        var enemyCarrier = CreatePlayer(102, PlayerTeam.Blue, x: 300f, y: 100f);

        Assert.Equal(BotRole.None, ModernPracticeBotController.ResolveModernBotRole(world, bot, PlayerTeam.Red, [bot]));

        bot.PickUpIntel();
        Assert.Equal(BotRole.ReturnWithIntel, ModernPracticeBotController.ResolveModernBotRole(world, bot, PlayerTeam.Red, [bot]));

        bot.ScoreIntel();
        allyCarrier.PickUpIntel();
        Assert.Equal(BotRole.EscortCarrier, ModernPracticeBotController.ResolveModernBotRole(world, bot, PlayerTeam.Red, [bot, allyCarrier]));

        allyCarrier.ScoreIntel();
        enemyCarrier.PickUpIntel();
        Assert.Equal(BotRole.None, ModernPracticeBotController.ResolveModernBotRole(world, bot, PlayerTeam.Red, [bot, enemyCarrier]));
    }

    [Fact]
    public void ModernReachabilityAuditAcceptsNearestClientBotGoalNode()
    {
        var level = CreateCtfAuditLevel();
        var asset = CreateModernAuditAsset(
        [
            new BotNavigationEdge { FromNodeId = 0, ToNodeId = 1, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
            new BotNavigationEdge { FromNodeId = 2, ToNodeId = 3, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
        ]);

        var audit = BotNavigationAssetValidator.AuditAttackReachability(level, asset);

        Assert.Empty(audit.Issues);
    }

    [Fact]
    public void ModernReachabilityAuditReportsDisconnectedClientBotGoalNode()
    {
        var level = CreateCtfAuditLevel();
        var asset = CreateModernAuditAsset(
        [
            new BotNavigationEdge { FromNodeId = 2, ToNodeId = 3, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
        ]);

        var audit = BotNavigationAssetValidator.AuditAttackReachability(level, asset);

        Assert.Contains(audit.Issues, issue => issue.Code.StartsWith("attack-disconnected-Red", StringComparison.Ordinal));
    }

    [Fact]
    public void ModernReachabilityAuditAllowsReachableNearbyGoalWhenNearestGoalNodeIsDisconnected()
    {
        var level = CreateCtfAuditLevel();
        var asset = new BotNavigationAsset
        {
            FormatVersion = 1,
            LevelName = "ctf_audit",
            MapAreaIndex = 1,
            LevelFingerprint = "test",
            BuildStrategy = BotNavigationBuildStrategy.ModernClientBotPointGraph,
            Nodes =
            [
                new BotNavigationNode { Id = 0, X = 100f, Y = 100f, SurfaceId = 0, Kind = BotNavigationNodeKind.Spawn, Team = PlayerTeam.Red, Label = "red spawn" },
                new BotNavigationNode { Id = 1, X = 890f, Y = 100f, SurfaceId = 1, Kind = BotNavigationNodeKind.Surface },
                new BotNavigationNode { Id = 2, X = 850f, Y = 100f, SurfaceId = 2, Kind = BotNavigationNodeKind.Surface },
                new BotNavigationNode { Id = 3, X = 900f, Y = 400f, SurfaceId = 3, Kind = BotNavigationNodeKind.Spawn, Team = PlayerTeam.Blue, Label = "blue spawn" },
                new BotNavigationNode { Id = 4, X = 110f, Y = 400f, SurfaceId = 4, Kind = BotNavigationNodeKind.Surface },
                new BotNavigationNode { Id = 5, X = 120f, Y = 400f, SurfaceId = 5, Kind = BotNavigationNodeKind.Surface },
            ],
            Edges =
            [
                new BotNavigationEdge { FromNodeId = 0, ToNodeId = 2, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
                new BotNavigationEdge { FromNodeId = 3, ToNodeId = 4, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
            ],
        };

        var audit = BotNavigationAssetValidator.AuditAttackReachability(level, asset);

        Assert.Empty(audit.Issues);
    }

    [Fact]
    public void ModernReachabilityAuditAllowsReachableNearbyStartWhenNearestSpawnNodeIsDisconnected()
    {
        var level = CreateCtfAuditLevel();
        var asset = new BotNavigationAsset
        {
            FormatVersion = 1,
            LevelName = "ctf_audit",
            MapAreaIndex = 1,
            LevelFingerprint = "test",
            BuildStrategy = BotNavigationBuildStrategy.ModernClientBotPointGraph,
            Nodes =
            [
                new BotNavigationNode { Id = 0, X = 100f, Y = 100f, SurfaceId = 0, Kind = BotNavigationNodeKind.Spawn, Team = PlayerTeam.Red, Label = "red spawn" },
                new BotNavigationNode { Id = 1, X = 140f, Y = 100f, SurfaceId = 1, Kind = BotNavigationNodeKind.Surface },
                new BotNavigationNode { Id = 2, X = 880f, Y = 100f, SurfaceId = 2, Kind = BotNavigationNodeKind.Surface },
                new BotNavigationNode { Id = 3, X = 900f, Y = 400f, SurfaceId = 3, Kind = BotNavigationNodeKind.Spawn, Team = PlayerTeam.Blue, Label = "blue spawn" },
                new BotNavigationNode { Id = 4, X = 120f, Y = 400f, SurfaceId = 4, Kind = BotNavigationNodeKind.Surface },
            ],
            Edges =
            [
                new BotNavigationEdge { FromNodeId = 1, ToNodeId = 2, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
                new BotNavigationEdge { FromNodeId = 3, ToNodeId = 4, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
            ],
        };

        var audit = BotNavigationAssetValidator.AuditAttackReachability(level, asset);

        Assert.Empty(audit.Issues);
    }

    [Fact]
    public void ModernReachabilityAuditAllowsTopologyChainsLongerThanClientBotWeightWindow()
    {
        var level = CreateCtfAuditLevel();
        var asset = CreateLongChainModernAuditAsset();

        var audit = BotNavigationAssetValidator.AuditAttackReachability(level, asset);

        Assert.Empty(audit.Issues);
    }

    [Fact]
    public void ModernBotSecondaryWeaponInputUsesRuntimeLoadoutUtilityLane()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(2, out var soldier));

        var shouldFireShotgun = ModernPracticeBotController.ResolveAbilityInputFromLoadout(
            soldier,
            combatTargetX: soldier.X + 120f,
            combatTargetY: soldier.Y,
            healTarget: null,
            firePrimary: false,
            fireSecondary: false);

        Assert.True(shouldFireShotgun);
    }

    [Fact]
    public void ModernRouteAwareCaptureSelectionTargetsReachableZonePointAndPreservesGroupCenter()
    {
        var world = new SimulationWorld();
        var bot = CreatePlayer(100, PlayerTeam.Blue, x: 0f, y: 0f);
        var asset = new BotNavigationAsset
        {
            FormatVersion = 1,
            LevelName = "capture_audit",
            MapAreaIndex = 1,
            LevelFingerprint = "test",
            BuildStrategy = BotNavigationBuildStrategy.ModernClientBotPointGraph,
            Nodes =
            [
                new BotNavigationNode { Id = 0, X = 0f, Y = 0f, SurfaceId = 0, Kind = BotNavigationNodeKind.Surface, RequiresGroundSupport = true },
                new BotNavigationNode { Id = 1, X = 20f, Y = 20f, SurfaceId = 1, Kind = BotNavigationNodeKind.Surface, RequiresGroundSupport = true },
                new BotNavigationNode { Id = 2, X = 80f, Y = 80f, SurfaceId = 2, Kind = BotNavigationNodeKind.Surface, RequiresGroundSupport = true },
                new BotNavigationNode { Id = 3, X = 40f, Y = 40f, SurfaceId = 3, Kind = BotNavigationNodeKind.Surface, RequiresGroundSupport = true },
            ],
            Edges =
            [
                new BotNavigationEdge { FromNodeId = 0, ToNodeId = 3, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
                new BotNavigationEdge { FromNodeId = 3, ToNodeId = 0, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
                new BotNavigationEdge { FromNodeId = 3, ToNodeId = 1, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
                new BotNavigationEdge { FromNodeId = 1, ToNodeId = 3, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
                new BotNavigationEdge { FromNodeId = 0, ToNodeId = 2, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
                new BotNavigationEdge { FromNodeId = 2, ToNodeId = 0, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
            ],
        };
        var navPoints = ClientBotNavPoints.Build(asset);
        var memory = new ModernPracticeBotController.BotMemory
        {
            NavigationGraphKey = navPoints.CacheKey,
            CurrentPointId = 0,
        };
        var captureObjective = new ModernPracticeBotController.ModernCaptureObjective(
            TargetZone: (20f, 20f),
            TargetPoint: (50f, 50f),
            MinX: 20f,
            MaxX: 80f,
            MinY: 20f,
            MaxY: 80f,
            Points:
            [
                new ModernPracticeBotController.ModernCapturePoint(20f, 20f, GroupId: 0),
                new ModernPracticeBotController.ModernCapturePoint(80f, 80f, GroupId: 0),
            ],
            Groups:
            [
                new ModernPracticeBotController.ModernCaptureGroup(
                    GroupId: 0,
                    MinX: 20f,
                    MaxX: 80f,
                    MinY: 20f,
                    MaxY: 80f,
                    PointCount: 2,
                    NearestDistanceToBot: 0f),
            ]);
        var selection = new ModernPracticeBotController.ModernPathSelection(
            Destination: (50f, 50f),
            AllowDirectPath: false,
            IsCaptureObjective: true,
            CaptureObjective: captureObjective);

        var resolved = ModernPracticeBotController.TryResolveModernRouteAwareCaptureSelection(
            world,
            bot,
            navPoints,
            memory,
            selection,
            out var routeAwareSelection);

        Assert.True(resolved);
        Assert.Equal((80f, 80f), routeAwareSelection.Destination);
        Assert.NotNull(routeAwareSelection.CaptureObjective);
        Assert.Equal((50f, 50f), routeAwareSelection.CaptureObjective!.Value.TargetPoint);
        Assert.Equal((80f, 80f), routeAwareSelection.CaptureObjective!.Value.TargetZone);
    }

    [Theory]
    [InlineData("Harvest", PlayerTeam.Blue, PlayerClass.Scout)]
    [InlineData("Harvest", PlayerTeam.Red, PlayerClass.Scout)]
    [InlineData("Harvest", PlayerTeam.Blue, PlayerClass.Heavy)]
    [InlineData("Harvest", PlayerTeam.Red, PlayerClass.Heavy)]
    [InlineData("Harvest", PlayerTeam.Blue, PlayerClass.Pyro)]
    [InlineData("Harvest", PlayerTeam.Red, PlayerClass.Pyro)]
    public void ModernBotsRouteToObjectiveOnStableSmokeMaps(string levelName, PlayerTeam team, PlayerClass classId)
    {
        RunObjectiveBotScenario(levelName, team, classId, simulationSeconds: 90);
    }

    [Theory]
    [InlineData("Corinth", PlayerTeam.Blue)]
    [InlineData("Harvest", PlayerTeam.Blue)]
    [InlineData("Harvest", PlayerTeam.Red)]
    public void ModernBotsScoreOnStableStockSmokeMaps(string levelName, PlayerTeam team)
    {
        RunObjectiveBotScenario(levelName, team, PlayerClass.Scout, simulationSeconds: 120);
    }

    private static void RunObjectiveBotScenario(
        string levelName,
        PlayerTeam team,
        PlayerClass classId,
        int simulationSeconds)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel(levelName));
        world.PrepareLocalPlayerJoin();

        const byte botSlot = 2;
        Assert.True(world.TryPrepareNetworkPlayerJoin(botSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(botSlot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(botSlot, classId));
        Assert.True(world.TryGetNetworkPlayer(botSlot, out var bot));
        Assert.Equal(classId, bot.ClassId);

        var controller = new ModernPracticeBotController
        {
            CollectDiagnostics = true,
        };
        var controlledSlots = new Dictionary<byte, ControlledBotSlot>
        {
            [botSlot] = new(botSlot, team, classId),
        };

        var startX = bot.X;
        var startY = bot.Y;
        var objective = ResolveTeamObjective(world, bot, team);
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
        var routeTrace = new List<string>();
        var completedObjective = false;

        for (var tick = 0; tick < world.Config.TicksPerSecond * simulationSeconds; tick += 1)
        {
            var inputs = controller.BuildInputs(world, controlledSlots);
            Assert.True(inputs.TryGetValue(botSlot, out var input));
            Assert.True(world.TrySetNetworkPlayerInput(botSlot, input));
            world.AdvanceOneTick();

            objective = ResolveTeamObjective(world, bot, team);
            completedObjective = HasCompletedTeamObjective(
                world,
                objective,
                team,
                initialRedCaps,
                initialBlueCaps,
                initialRedKothTicks,
                initialBlueKothTicks);
            if (completedObjective)
            {
                break;
            }

            var diagnostic = controller.LastDiagnostics.Entries.Single(entry => entry.Slot == botSlot);
            var objectiveDistance = DistanceBetween(bot.X, bot.Y, objective.X, objective.Y);
            if (objectiveDistance + 16f < bestObjectiveDistance
                || diagnostic.RouteLabel.EndsWith("capture_zone_hold", StringComparison.Ordinal))
            {
                bestObjectiveDistance = Math.Min(bestObjectiveDistance, objectiveDistance);
                ticksSinceBestObjectiveProgress = 0;
            }
            else
            {
                ticksSinceBestObjectiveProgress += 1;
                longestNoProgressTicks = Math.Max(longestNoProgressTicks, ticksSinceBestObjectiveProgress);
            }

            maxDistanceFromSpawn = MathF.Max(maxDistanceFromSpawn, DistanceBetween(bot.X, bot.Y, startX, startY));
            maxStuckTicks = Math.Max(maxStuckTicks, diagnostic.StuckTicks);
            routeLabelCounts.TryGetValue(diagnostic.RouteLabel, out var routeLabelCount);
            routeLabelCounts[diagnostic.RouteLabel] = routeLabelCount + 1;
            if (tick % (world.Config.TicksPerSecond / 2) == 0)
            {
                routeTrace.Add($"t={tick / (float)world.Config.TicksPerSecond:0.0} pos=({bot.X:0.0},{bot.Y:0.0}) bottom={bot.Bottom:0.0} target=({diagnostic.MovementTargetX:0.0},{diagnostic.MovementTargetY:0.0}) input=L{input.Left}/R{input.Right}/J{input.Up} req={diagnostic.RequestedHorizontal}/{diagnostic.RequestedJump} move={diagnostic.MoveDebug} jump={diagnostic.JumpDebug} g={diagnostic.IsGrounded}/{diagnostic.ProbeGrounded} d={objectiveDistance:0.0} route={diagnostic.RouteLabel} cp={diagnostic.CurrentPointId} np={diagnostic.NextPointId} np2={diagnostic.NextPoint2Id} prev={diagnostic.PreviousCurrentPointId}->{diagnostic.PreviousNextPointId} goal={diagnostic.RouteGoalNodeId} sab={diagnostic.SecondAnchorBlockPointId}/{diagnostic.SecondAnchorBlockTicksRemaining}");
                if (routeTrace.Count > 24)
                {
                    routeTrace.RemoveAt(0);
                }
            }
        }

        var routeSummary = string.Join(", ", routeLabelCounts
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(static pair => $"{pair.Key}x{pair.Value}"));
        var traceSummary = string.Join(" | ", routeTrace);
        Assert.True(maxDistanceFromSpawn >= 256f, $"{levelName} {team} {classId} moved only {maxDistanceFromSpawn:0.0}px from spawn; routes={routeSummary}; trace={traceSummary}");
        Assert.True(completedObjective, $"{levelName} {team} {classId} did not complete objective: mode={world.MatchRules.Mode}, start={startObjectiveDistance:0.0}, best={bestObjectiveDistance:0.0}, final=({bot.X:0.0},{bot.Y:0.0}), score={world.RedCaps}-{world.BlueCaps}, koth={world.KothRedTimerTicksRemaining}-{world.KothBlueTimerTicksRemaining}; routes={routeSummary}; trace={traceSummary}");
        Assert.True(longestNoProgressTicks < world.Config.TicksPerSecond * 16, $"{levelName} {team} {classId} went {longestNoProgressTicks / world.Config.TicksPerSecond:0.0}s without objective progress; routes={routeSummary}; trace={traceSummary}");
        Assert.True(maxStuckTicks < 90, $"{levelName} {team} {classId} stayed stuck for {maxStuckTicks} ticks; routes={routeSummary}; trace={traceSummary}");
    }

    private static PlayerEntity CreatePlayer(int id, PlayerTeam team, float x, float y)
    {
        var player = new PlayerEntity(id, CharacterClassCatalog.Scout, $"Player {id}");
        player.Spawn(team, x, y);
        return player;
    }

    private static BotObjectiveProbe ResolveTeamObjective(SimulationWorld world, PlayerEntity bot, PlayerTeam team)
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
            if (ModernPracticeBotController.TryResolveModernCaptureObjective(world, bot, point.Marker, out var captureObjective))
            {
                return new BotObjectiveProbe(world.MatchRules.Mode, captureObjective.TargetPoint.X, captureObjective.TargetPoint.Y, point.Index);
            }

            return new BotObjectiveProbe(world.MatchRules.Mode, point.Marker.CenterX, point.Marker.CenterY, point.Index);
        }

        return new BotObjectiveProbe(world.MatchRules.Mode, world.Level.Bounds.Width * 0.5f, world.Level.Bounds.Height * 0.5f, ControlPointIndex: -1);
    }

    private static bool HasCompletedTeamObjective(
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

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private readonly record struct BotObjectiveProbe(GameModeKind Mode, float X, float Y, int ControlPointIndex);
    private static SimpleLevel CreateCtfAuditLevel()
    {
        return new SimpleLevel(
            name: "ctf_audit",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(1000f, 600f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(100f, 100f),
            redSpawns: [new SpawnPoint(100f, 100f)],
            blueSpawns: [new SpawnPoint(900f, 400f)],
            intelBases:
            [
                new IntelBaseMarker(PlayerTeam.Red, 100f, 400f),
                new IntelBaseMarker(PlayerTeam.Blue, 900f, 100f),
            ],
            roomObjects: [],
            floorY: 500f,
            solids: [new LevelSolid(0f, 500f, 1000f, 100f)],
            importedFromSource: false);
    }

    private static BotNavigationAsset CreateModernAuditAsset(IReadOnlyList<BotNavigationEdge> edges)
    {
        return new BotNavigationAsset
        {
            FormatVersion = 1,
            LevelName = "ctf_audit",
            MapAreaIndex = 1,
            LevelFingerprint = "test",
            BuildStrategy = BotNavigationBuildStrategy.ModernClientBotPointGraph,
            Nodes =
            [
                new BotNavigationNode { Id = 0, X = 100f, Y = 100f, SurfaceId = 0, Kind = BotNavigationNodeKind.Spawn, Team = PlayerTeam.Red, Label = "red spawn" },
                new BotNavigationNode { Id = 1, X = 880f, Y = 100f, SurfaceId = 1, Kind = BotNavigationNodeKind.Surface },
                new BotNavigationNode { Id = 2, X = 900f, Y = 400f, SurfaceId = 2, Kind = BotNavigationNodeKind.Spawn, Team = PlayerTeam.Blue, Label = "blue spawn" },
                new BotNavigationNode { Id = 3, X = 120f, Y = 400f, SurfaceId = 3, Kind = BotNavigationNodeKind.Surface },
            ],
            Edges = edges,
        };
    }

    private static BotNavigationAsset CreateLongChainModernAuditAsset()
    {
        var nodes = new List<BotNavigationNode>();
        var edges = new List<BotNavigationEdge>();
        const int chainNodeCount = 150;
        for (var index = 0; index < chainNodeCount; index += 1)
        {
            var x = 100f + (780f * index / (chainNodeCount - 1));
            nodes.Add(new BotNavigationNode
            {
                Id = index,
                X = x,
                Y = 100f,
                SurfaceId = index,
                Kind = index == 0 ? BotNavigationNodeKind.Spawn : BotNavigationNodeKind.Surface,
                Team = index == 0 ? PlayerTeam.Red : (PlayerTeam?)null,
                Label = index == 0 ? "red spawn" : string.Empty,
            });

            if (index > 0)
            {
                edges.Add(new BotNavigationEdge { FromNodeId = index - 1, ToNodeId = index, Kind = BotNavigationTraversalKind.Walk, Cost = 1f });
            }
        }

        nodes.Add(new BotNavigationNode { Id = 150, X = 900f, Y = 400f, SurfaceId = 150, Kind = BotNavigationNodeKind.Spawn, Team = PlayerTeam.Blue, Label = "blue spawn" });
        nodes.Add(new BotNavigationNode { Id = 151, X = 120f, Y = 400f, SurfaceId = 151, Kind = BotNavigationNodeKind.Surface });
        edges.Add(new BotNavigationEdge { FromNodeId = 150, ToNodeId = 151, Kind = BotNavigationTraversalKind.Walk, Cost = 1f });

        return new BotNavigationAsset
        {
            FormatVersion = 1,
            LevelName = "ctf_audit",
            MapAreaIndex = 1,
            LevelFingerprint = "test",
            BuildStrategy = BotNavigationBuildStrategy.ModernClientBotPointGraph,
            Nodes = nodes,
            Edges = edges,
        };
    }

}
