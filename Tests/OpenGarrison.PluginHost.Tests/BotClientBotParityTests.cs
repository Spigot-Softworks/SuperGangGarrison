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
        Assert.Equal(BotRole.HuntCarrier, ModernPracticeBotController.ResolveModernBotRole(world, bot, PlayerTeam.Red, [bot, enemyCarrier]));
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
    public void ModernRouteAwareCaptureSelectionPreservesChosenZonePoint()
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
                new BotNavigationEdge { FromNodeId = 3, ToNodeId = 1, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
                new BotNavigationEdge { FromNodeId = 0, ToNodeId = 2, Kind = BotNavigationTraversalKind.Walk, Cost = 1f },
            ],
        };
        var graph = new BotNavigationRuntimeGraph(asset);
        var memory = new ModernPracticeBotController.BotMemory
        {
            NavigationGraphKey = graph.CacheKey,
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
            graph,
            memory,
            selection,
            out var routeAwareSelection);

        Assert.True(resolved);
        Assert.Equal((80f, 80f), routeAwareSelection.Destination);
        Assert.NotNull(routeAwareSelection.CaptureObjective);
        Assert.Equal((80f, 80f), routeAwareSelection.CaptureObjective!.Value.TargetPoint);
        Assert.Equal((80f, 80f), routeAwareSelection.CaptureObjective!.Value.TargetZone);
    }

    private static PlayerEntity CreatePlayer(int id, PlayerTeam team, float x, float y)
    {
        var player = new PlayerEntity(id, CharacterClassCatalog.Scout, $"Player {id}");
        player.Spawn(team, x, y);
        return player;
    }

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
