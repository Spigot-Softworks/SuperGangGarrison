using System.IO;
using System.Text;
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

        var shouldFireShotgun = ModernPracticeBotController.ResolveSecondaryWeaponFireFromLoadout(
            soldier,
            combatTargetX: soldier.X + 120f,
            combatTargetY: soldier.Y,
            healTarget: null,
            firePrimary: false,
            fireSecondary: false);

        Assert.True(shouldFireShotgun);
    }

    [Fact]
    public void ClientBot2020CompatModeBuildsInputsThroughCompatSelector()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        world.PrepareLocalPlayerJoin();

        const byte botSlot = 2;
        Assert.True(world.TryPrepareNetworkPlayerJoin(botSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(botSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(botSlot, PlayerClass.Scout));

        var controller = new ModernPracticeBotController();
        var controlledSlots = new Dictionary<byte, ControlledBotSlot>
        {
            [botSlot] = new(botSlot, PlayerTeam.Blue, PlayerClass.Scout, BotPathMode.ClientBot2020Compat),
        };

        var inputs = controller.BuildInputs(world, controlledSlots);

        Assert.True(inputs.ContainsKey(botSlot));
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
        Assert.Equal((50f, 50f), routeAwareSelection.Destination);
        Assert.NotNull(routeAwareSelection.CaptureObjective);
        Assert.Equal((50f, 50f), routeAwareSelection.CaptureObjective!.Value.TargetPoint);
        Assert.Equal((80f, 80f), routeAwareSelection.CaptureObjective!.Value.TargetZone);
    }

    [Theory]
    [InlineData("Waterway", @"C:\Users\level\Desktop\gg2\Gang-Garrison-2\GG2-FullBuild\MapNavMeshes\ctf_waterway.pts")]
    [InlineData("Corinth", @"C:\Users\level\Desktop\gg2\Gang-Garrison-2\GG2-FullBuild\MapNavMeshes\koth_corinth.pts")]
    public void OriginalGg2NavMeshMatchesGeneratedClientBotNavPoints(string levelName, string originalMeshPath)
    {
        Assert.True(File.Exists(originalMeshPath), $"missing original mesh {originalMeshPath}");

        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel(levelName));

        var generatedNavPoints = ClientBotNavPoints.Build(world.Level);
        var originalNavPoints = ReadOriginalGg2NavMesh(originalMeshPath);

        var differences = new List<string>();
        if (originalNavPoints.Count != generatedNavPoints.Points.Count)
        {
            differences.Add($"{levelName} count mismatch: original={originalNavPoints.Count} generated={generatedNavPoints.Points.Count}");
        }

        var comparableCount = Math.Min(originalNavPoints.Count, generatedNavPoints.Points.Count);
        for (var pointId = 0; pointId < comparableCount; pointId += 1)
        {
            var originalPoint = originalNavPoints[pointId];
            Assert.True(generatedNavPoints.TryGetPoint(pointId, out var generatedPoint), $"missing generated point {pointId}");

            var generatedOutgoing = generatedNavPoints.TryGetOutgoingConnections(pointId, out var generatedEdges)
                ? generatedEdges.Select(static edge => edge.ToNodeId).OrderBy(static nodeId => nodeId).ToArray()
                : Array.Empty<int>();

            var generatedBlocked = Enumerable.Range(0, comparableCount)
                .Where(fromPointId => generatedNavPoints.IsReverseBlocked(pointId, fromPointId))
                .OrderBy(static nodeId => nodeId)
                .ToArray();

            if (MathF.Abs(originalPoint.X - generatedPoint.X) > 0.01f
                || MathF.Abs(originalPoint.Y - generatedPoint.Y) > 0.01f
                || !originalPoint.Outgoing.SequenceEqual(generatedOutgoing)
                || !originalPoint.Blocked.SequenceEqual(generatedBlocked))
            {
                differences.Add(
                    $"id={pointId} original=({originalPoint.X:0},{originalPoint.Y:0}) out=[{string.Join(",", originalPoint.Outgoing)}] blocked=[{string.Join(",", originalPoint.Blocked)}] generated=({generatedPoint.X:0},{generatedPoint.Y:0}) out=[{string.Join(",", generatedOutgoing)}] blocked=[{string.Join(",", generatedBlocked)}]");
                if (differences.Count >= 32)
                {
                    break;
                }
            }
        }

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    [Theory]
    [InlineData("Waterway", @"C:\Users\level\Desktop\gg2\Gang-Garrison-2\GG2-FullBuild\MapNavMeshes\ctf_waterway.pts")]
    [InlineData("Corinth", @"C:\Users\level\Desktop\gg2\Gang-Garrison-2\GG2-FullBuild\MapNavMeshes\koth_corinth.pts")]
    public void OriginalGg2GoalWeightsMatchGeneratedClientBotGoalWeights(string levelName, string originalMeshPath)
    {
        Assert.True(File.Exists(originalMeshPath), $"missing original mesh {originalMeshPath}");

        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel(levelName));

        var generatedNavPoints = ClientBotNavPoints.Build(world.Level);
        var originalNavPoints = ReadOriginalGg2NavMesh(originalMeshPath);
        Assert.Equal(originalNavPoints.Count, generatedNavPoints.Points.Count);

        var differences = new List<string>();
        for (var goalPointId = 0; goalPointId < originalNavPoints.Count; goalPointId += 1)
        {
            var originalWeights = ComputeOriginalGg2GoalWeights(originalNavPoints, goalPointId);
            var generatedWeights = generatedNavPoints.GetGoalWeights(goalPointId);
            Assert.NotNull(generatedWeights);

            for (var pointId = 0; pointId < originalWeights.Length; pointId += 1)
            {
                if (originalWeights[pointId] == generatedWeights![pointId])
                {
                    continue;
                }

                differences.Add(
                    $"goal={goalPointId} point={pointId} original={originalWeights[pointId]} generated={generatedWeights[pointId]} outgoing=[{string.Join(",", originalNavPoints[pointId].Outgoing)}] blocked=[{string.Join(",", originalNavPoints[pointId].Blocked)}]");
                if (differences.Count >= 32)
                {
                    break;
                }
            }

            if (differences.Count >= 32)
            {
                break;
            }
        }

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    [Theory]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Scout)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Pyro)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Soldier)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Heavy)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Demoman)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Medic)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Engineer)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Spy)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Sniper)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Quote)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Scout)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Pyro)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Soldier)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Heavy)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Demoman)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Medic)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Engineer)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Spy)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Sniper)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Quote)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Scout)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Pyro)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Soldier)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Heavy)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Demoman)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Medic)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Engineer)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Spy)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Sniper)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Quote)]
    public void ModernBotsRouteToObjectiveWithoutLoopingOnKnownProblemMaps(string levelName, PlayerTeam team, PlayerClass classId)
    {
        RunObjectiveBotScenario(levelName, team, classId, simulationSeconds: 90);
    }

    [Theory]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Scout)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Pyro)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Soldier)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Heavy)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Demoman)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Medic)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Engineer)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Spy)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Sniper)]
    [InlineData("Corinth", PlayerTeam.Blue, PlayerClass.Quote)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Scout)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Pyro)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Soldier)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Heavy)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Demoman)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Medic)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Engineer)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Spy)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Sniper)]
    [InlineData("Waterway", PlayerTeam.Blue, PlayerClass.Quote)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Scout)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Pyro)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Soldier)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Heavy)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Demoman)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Medic)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Engineer)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Spy)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Sniper)]
    [InlineData("Waterway", PlayerTeam.Red, PlayerClass.Quote)]
    public void ClientBot2020CompatRoutesToObjectiveWithoutLoopingOnKnownProblemMaps(string levelName, PlayerTeam team, PlayerClass classId)
    {
        RunObjectiveBotScenario(levelName, team, classId, simulationSeconds: 90, pathMode: BotPathMode.ClientBot2020Compat);
    }

    [Theory]
    [InlineData("Truefort", PlayerTeam.Blue)]
    [InlineData("Truefort", PlayerTeam.Red)]
    [InlineData("TwodFortTwo", PlayerTeam.Blue)]
    [InlineData("TwodFortTwo", PlayerTeam.Red)]
    [InlineData("Conflict", PlayerTeam.Blue)]
    [InlineData("Conflict", PlayerTeam.Red)]
    [InlineData("ClassicWell", PlayerTeam.Blue)]
    [InlineData("ClassicWell", PlayerTeam.Red)]
    [InlineData("Waterway", PlayerTeam.Blue)]
    [InlineData("Waterway", PlayerTeam.Red)]
    [InlineData("Orange", PlayerTeam.Blue)]
    [InlineData("Orange", PlayerTeam.Red)]
    [InlineData("Avanti", PlayerTeam.Blue)]
    [InlineData("Avanti", PlayerTeam.Red)]
    [InlineData("Eiger", PlayerTeam.Blue)]
    [InlineData("Eiger", PlayerTeam.Red)]
    [InlineData("Valley", PlayerTeam.Blue)]
    [InlineData("Valley", PlayerTeam.Red)]
    [InlineData("Corinth", PlayerTeam.Blue)]
    [InlineData("Corinth", PlayerTeam.Red)]
    [InlineData("Harvest", PlayerTeam.Blue)]
    [InlineData("Harvest", PlayerTeam.Red)]
    [InlineData("Atalia", PlayerTeam.Blue)]
    [InlineData("Atalia", PlayerTeam.Red)]
    [InlineData("Sixties", PlayerTeam.Blue)]
    [InlineData("Sixties", PlayerTeam.Red)]
    [InlineData("Gallery", PlayerTeam.Blue)]
    [InlineData("Gallery", PlayerTeam.Red)]
    public void ModernBotsScoreOnAllStockCtfAndKothMaps(string levelName, PlayerTeam team)
    {
        RunObjectiveBotScenario(levelName, team, PlayerClass.Scout, simulationSeconds: 120);
    }

    [Theory]
    [InlineData("Truefort", PlayerTeam.Blue)]
    [InlineData("Truefort", PlayerTeam.Red)]
    [InlineData("TwodFortTwo", PlayerTeam.Blue)]
    [InlineData("TwodFortTwo", PlayerTeam.Red)]
    [InlineData("Conflict", PlayerTeam.Blue)]
    [InlineData("Conflict", PlayerTeam.Red)]
    [InlineData("ClassicWell", PlayerTeam.Blue)]
    [InlineData("ClassicWell", PlayerTeam.Red)]
    [InlineData("Waterway", PlayerTeam.Blue)]
    [InlineData("Waterway", PlayerTeam.Red)]
    [InlineData("Orange", PlayerTeam.Blue)]
    [InlineData("Orange", PlayerTeam.Red)]
    [InlineData("Avanti", PlayerTeam.Blue)]
    [InlineData("Avanti", PlayerTeam.Red)]
    [InlineData("Eiger", PlayerTeam.Blue)]
    [InlineData("Eiger", PlayerTeam.Red)]
    [InlineData("Valley", PlayerTeam.Blue)]
    [InlineData("Valley", PlayerTeam.Red)]
    [InlineData("Corinth", PlayerTeam.Blue)]
    [InlineData("Corinth", PlayerTeam.Red)]
    [InlineData("Harvest", PlayerTeam.Blue)]
    [InlineData("Harvest", PlayerTeam.Red)]
    [InlineData("Atalia", PlayerTeam.Blue)]
    [InlineData("Atalia", PlayerTeam.Red)]
    [InlineData("Sixties", PlayerTeam.Blue)]
    [InlineData("Sixties", PlayerTeam.Red)]
    [InlineData("Gallery", PlayerTeam.Blue)]
    [InlineData("Gallery", PlayerTeam.Red)]
    public void ClientBot2020CompatScoresOnAllStockCtfAndKothMaps(string levelName, PlayerTeam team)
    {
        RunObjectiveBotScenario(levelName, team, PlayerClass.Scout, simulationSeconds: 120, pathMode: BotPathMode.ClientBot2020Compat);
    }

    private static void RunObjectiveBotScenario(
        string levelName,
        PlayerTeam team,
        PlayerClass classId,
        int simulationSeconds,
        BotPathMode pathMode = BotPathMode.ModernHybrid)
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
            [botSlot] = new(botSlot, team, classId, pathMode),
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
    private readonly record struct OriginalGg2NavPoint(float X, float Y, int[] Outgoing, int[] Blocked);

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

    private static List<OriginalGg2NavPoint> ReadOriginalGg2NavMesh(string path)
    {
        var payload = File.ReadAllText(path).Trim();
        var topLevelEntries = ReadGameMakerSerializedList(HexToBytes(payload));
        var points = new List<OriginalGg2NavPoint>(topLevelEntries.Count);
        for (var index = 0; index < topLevelEntries.Count; index += 1)
        {
            var pointPayload = Assert.IsType<string>(topLevelEntries[index]);
            var pointEntries = ReadGameMakerSerializedList(HexToBytes(pointPayload));
            Assert.True(pointEntries.Count >= 3, $"original navpoint {index} has only {pointEntries.Count} entries");

            var x = (float)Assert.IsType<double>(pointEntries[0]);
            var y = (float)Assert.IsType<double>(pointEntries[1]);
            var outgoingPayload = Assert.IsType<string>(pointEntries[2]);
            var outgoingEntries = ReadGameMakerSerializedList(HexToBytes(outgoingPayload));
            var outgoing = outgoingEntries
                .Select(static value => (int)Math.Round(Assert.IsType<double>(value)))
                .OrderBy(static value => value)
                .ToArray();
            var blocked = Array.Empty<int>();
            if (pointEntries.Count >= 4)
            {
                var blockedPayload = Assert.IsType<string>(pointEntries[3]);
                var blockedEntries = ReadGameMakerSerializedList(HexToBytes(blockedPayload));
                blocked = blockedEntries
                    .Select(static value => (int)Math.Round(Assert.IsType<double>(value)))
                    .OrderBy(static value => value)
                    .ToArray();
            }

            points.Add(new OriginalGg2NavPoint(x, y, outgoing, blocked));
        }

        return points;
    }

    private static int[] ComputeOriginalGg2GoalWeights(IReadOnlyList<OriginalGg2NavPoint> navPoints, int goalPointId, int maximumDepth = 130)
    {
        var weights = new int[navPoints.Count];
        ProcessOriginalGg2Points(navPoints, [goalPointId], weights, branchDepth: 1, sourceNode: -1, maximumDepth);
        return weights;
    }

    private static void ProcessOriginalGg2Points(
        IReadOnlyList<OriginalGg2NavPoint> navPoints,
        IReadOnlyList<int> branches,
        int[] weights,
        int branchDepth,
        int sourceNode,
        int maximumDepth)
    {
        for (var branchIndex = 0; branchIndex < branches.Count; branchIndex += 1)
        {
            var branch = branches[branchIndex];
            if (branch < 0 || branch >= navPoints.Count)
            {
                continue;
            }

            if (weights[branch] != 0 && weights[branch] <= branchDepth)
            {
                continue;
            }

            if (sourceNode >= 0 && Array.IndexOf(navPoints[sourceNode].Blocked, branch) >= 0)
            {
                continue;
            }

            weights[branch] = branchDepth;
            if (branchDepth > maximumDepth)
            {
                continue;
            }

            ProcessOriginalGg2Points(navPoints, navPoints[branch].Outgoing, weights, branchDepth + 1, branch, maximumDepth);
        }
    }

    private static List<object> ReadGameMakerSerializedList(byte[] bytes)
    {
        Assert.True(bytes.Length >= 8, "serialized ds_list too short");
        Assert.Equal((byte)0x2D, bytes[0]);
        Assert.Equal((byte)0x01, bytes[1]);

        var count = BitConverter.ToInt32(bytes, 4);
        var values = new List<object>(count);
        var offset = 8;
        for (var index = 0; index < count; index += 1)
        {
            Assert.True(offset + 8 <= bytes.Length, $"serialized ds_list truncated at item {index}");
            var itemType = BitConverter.ToInt32(bytes, offset);
            switch (itemType)
            {
                case 0:
                    Assert.True(offset + 16 <= bytes.Length, $"serialized real truncated at item {index}");
                    values.Add(ReadGameMakerReal(bytes, offset + 8));
                    offset += 16;
                    break;

                case 1:
                    Assert.True(offset + 16 <= bytes.Length, $"serialized string header truncated at item {index}");
                    var stringLength = BitConverter.ToInt32(bytes, offset + 12);
                    Assert.True(stringLength >= 0 && offset + 16 + stringLength <= bytes.Length, $"serialized string payload truncated at item {index}");
                    values.Add(Encoding.ASCII.GetString(bytes, offset + 16, stringLength));
                    offset += 16 + stringLength;
                    break;

                default:
                    throw new InvalidDataException($"unsupported GameMaker ds_list item type {itemType} at index {index}");
            }
        }

        return values;
    }

    private static double ReadGameMakerReal(byte[] bytes, int offset)
    {
        var lowWord = BitConverter.ToUInt32(bytes, offset);
        var highWord = BitConverter.ToUInt32(bytes, offset + 4);
        var bits = ((ulong)lowWord << 32) | highWord;
        return BitConverter.Int64BitsToDouble((long)bits);
    }

    private static byte[] HexToBytes(string hex)
    {
        Assert.True(hex.Length % 2 == 0, "hex payload length must be even");
        var bytes = new byte[hex.Length / 2];
        for (var index = 0; index < bytes.Length; index += 1)
        {
            bytes[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
        }

        return bytes;
    }

}
